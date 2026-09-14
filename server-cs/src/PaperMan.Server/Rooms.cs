// =============================================================================
// 房間管理 — 對應反編譯 (docs/PACKETS.md §3.15):
//
//   111 GL_MAKEROOM_REQ (sub_56A5A0, 廿八輪逐欄定案):
//       u8 map, s8 has_pass, str title, [has_pass: str pass],
//       u8 rule(=modeIndex), u8 max_player, u8 x, u8 y
//   112 GL_MAKEROOM_ACK (sub_56A7B0):
//       u8 err(0=OK), u8 room_no(<210), u16, s32 room_uid, u8, s8 obs
//       — err==0 時 client 以自己為房主初始化房間物件並切狀態 10
//   113 GL_ENTERROOM_REQ: u8 room_no
//   114 GL_ENTERROOM_ACK: u8 sub_type 多態 (docs §3.15)
//
// 房號上限 210 (0xD2 — client 陣列硬上限); rule 用官方 modeIndex
// (權威清單見下方 GameMode — sub_53FBB0 mode factory 的 16 路 switch)。
// =============================================================================
using System.Collections.Concurrent;
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>
/// 官方 modeIndex — sub_53FBB0 (PaperMan.exe.c 139113) 依此值 new 出對應
/// CyGameModes::Cy*ModeLobbyUI (各 0x10 位元組) 存入 room+33, 並以
/// room+132 指向 16 位元組的 mode rule 物件 (vtable + 規則旗標)。
/// 14 在 switch 中無分支 (落 default → +33=null, 等同無效值);
/// 16 為 sub_53FBB0 的「不做任何事」哨兵 (預設 ctor 用之)。
/// </summary>
public enum GameMode : byte
{
    TeamDeath = 0,      // CyTeamMatchModeLobbyUI         (TD_ 地圖前綴)
    FreeForAll = 1,     // CyIndividualSurvivalModeLobbyUI (PS_)
    DefuseBomb = 2,     // CyDefuseBombModeLobbyUI        (資源稱 TeamHacking, TH_)
    TeamSurvival = 3,   // CyTeamSurvivalModeLobbyUI      (TS_)
    Steal = 4,          // CyStealModeLobbyUI             (資源稱 TeamSteal, TW_)
    Practice = 5,       // CyPracticeModeLobbyUI
    Tutorial = 6,       // CyTutorialModeLobbyUI
    Chatting = 7,       // CyChattingRoomModeLobbyUI
    PulpNRoll = 8,      // CyPulpnRollModeLobbyUI         (PNR)
    GunShooting = 9,    // CyGunShootingModeLobbyUI
    Occupy = 10,        // CyOccupyModeLobbyUI
    AiMulti = 11,       // CyAIMultiModeLobbyUI
    Soccer = 12,        // CyTeamSoccerModeLobbyUI        (SOCCER)
    OccupyRenewal = 13, // CyOccupyRenewalModeLobbyUI
    WeaponTest = 15,    // CyWeaponTestModeLobbyUI
}

/// <summary>OCC 的單一據點生命週期。數字值不在封包上直接傳送。</summary>
public enum OccupyPointPhase : byte
{
    Idle,
    Capturing,
    Captured,
}

/// <summary>
/// 由 OCC handler 取出的不可變快照。
///
/// <para><see cref="CaptureParticipantCount"/> 對應 903/907 第四個 byte。client 會把它
/// 寫入據點控制器的 <c>+24</c>，只接受 1..2 的活動值（<c>sub_768210</c>），且
/// 在大於 1 時加速占領計時（<c>sub_76A4B0</c>）。故其可驗證語意是同點參與者數，
/// 而非隊伍、slot 或保留欄。這個私服尚未有位置聚合器，start actor 是唯一已知的
/// 參與者，因此以狀態導出的 1 初始化。</para>
/// </summary>
public sealed record OccupyPointSnapshot(
    byte PointId,
    byte ActorSlot,
    int ActorUserId,
    byte CaptureParticipantCount,
    OccupyPointPhase Phase);

/// <summary>
/// 房內短生命週期的戰場狀態。它不寫入 SQLite：地面武器與占領進度只在一局
/// 對戰有效，且必須在同一房間內原子更新。
/// </summary>
public sealed class RoomBattleState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<byte, OccupyPointSnapshot> _occupyPoints = [];
    private bool _matchActive;

    /// <summary>在與據點轉換相同的 lock 內開始新局並清空上一局狀態。</summary>
    public void BeginMatch()
    {
        lock (_gate)
        {
            _occupyPoints.Clear();
            _matchActive = true;
        }
    }

    /// <summary>在與據點轉換相同的 lock 內結束對局，禁止任何後續戰場事件。</summary>
    public void EndMatch()
    {
        lock (_gate)
        {
            _matchActive = false;
            _occupyPoints.Clear();
        }
    }

    /// <summary>供沒有可變狀態的 battle handler 檢查局是否仍有效。</summary>
    public bool IsMatchActive
    {
        get
        {
            lock (_gate)
            {
                return _matchActive;
            }
        }
    }

    /// <summary>
    /// OCC UI 對 pointId-1 只處理三個據點（<c>sub_771490</c> / <c>sub_7713C0</c>
    /// 的 index &lt; 3 防護），所以拒絕 0 與大於 3 的 wire id。<paramref name="stateChanged"/>
    /// 僅在真正建立 claim 時為 true；相同 actor 的 TCP 重送可安全只回給原請求者。
    /// </summary>
    public bool TryStartOccupy(
        byte pointId,
        byte actorSlot,
        int actorUserId,
        out OccupyPointSnapshot snapshot,
        out bool stateChanged)
    {
        lock (_gate)
        {
            if (!_matchActive || !IsValidPoint(pointId))
            {
                snapshot = null!;
                stateChanged = false;
                return false;
            }

            if (_occupyPoints.TryGetValue(pointId, out var current))
            {
                // TCP 重送同一個 start 時保持冪等；另一人或已完成的據點不能搶寫。
                if (current.Phase != OccupyPointPhase.Capturing
                    || current.ActorSlot != actorSlot
                    || current.ActorUserId != actorUserId)
                {
                    snapshot = null!;
                    stateChanged = false;
                    return false;
                }

                snapshot = current;
                stateChanged = false;
                return true;
            }

            snapshot = new(
                pointId,
                actorSlot,
                actorUserId,
                CaptureParticipantCount: 1,
                Phase: OccupyPointPhase.Capturing);
            _occupyPoints.Add(pointId, snapshot);
            stateChanged = true;
            return true;
        }
    }

    /// <summary>僅啟動該據點的同一玩家可以送 OCC_SUCC。</summary>
    public bool TryCompleteOccupy(
        byte pointId,
        byte actorSlot,
        int actorUserId,
        out OccupyPointSnapshot snapshot,
        out bool stateChanged)
    {
        lock (_gate)
        {
            if (!_matchActive
                || !_occupyPoints.TryGetValue(pointId, out var current)
                || current.ActorSlot != actorSlot
                || current.ActorUserId != actorUserId)
            {
                snapshot = null!;
                stateChanged = false;
                return false;
            }

            if (current.Phase == OccupyPointPhase.Captured)
            {
                snapshot = current;                          // 成功 ACK 僅須補給重送者
                stateChanged = false;
                return true;
            }

            if (current.Phase != OccupyPointPhase.Capturing)
            {
                snapshot = null!;
                stateChanged = false;
                return false;
            }

            snapshot = current with { Phase = OccupyPointPhase.Captured };
            _occupyPoints[pointId] = snapshot;
            stateChanged = true;
            return true;
        }
    }

    /// <summary>僅啟動者可以送 OCC_FAIL；成功或他人的事件不可清除狀態。</summary>
    public bool TryFailOccupy(byte pointId, byte actorSlot, int actorUserId, out OccupyPointSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!_matchActive
                || !_occupyPoints.TryGetValue(pointId, out var current)
                || current.Phase != OccupyPointPhase.Capturing
                || current.ActorSlot != actorSlot
                || current.ActorUserId != actorUserId)
            {
                snapshot = null!;
                return false;
            }

            _occupyPoints.Remove(pointId);
            snapshot = current with { Phase = OccupyPointPhase.Idle };
            return true;
        }
    }

    /// <summary>
    /// 離開房間的玩家不可以繼續鎖住仍在 capture 中的據點。已完成據點保留到
    /// <see cref="EndMatch"/>，因為它已是房內可見的結果。
    /// </summary>
    public void AbandonCapturesBy(byte actorSlot, long actorUserId)
    {
        if (actorUserId is < int.MinValue or > int.MaxValue)
        {
            return;
        }

        int wireUserId = (int)actorUserId;
        lock (_gate)
        {
            var abandoned = _occupyPoints
                .Where(pair => pair.Value.Phase == OccupyPointPhase.Capturing
                    && pair.Value.ActorSlot == actorSlot
                    && pair.Value.ActorUserId == wireUserId)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (byte pointId in abandoned)
            {
                _occupyPoints.Remove(pointId);
            }
        }
    }

    private static bool IsValidPoint(byte pointId) => pointId is >= 1 and <= 3;
}

/// <summary>單一房間的即時狀態 (記憶體為主, DB rooms 表為快照)。</summary>
public sealed class Room
{
    public required byte RoomNo { get; init; }
    public required int RoomUid { get; init; }
    public required string Title { get; set; }
    public string? Password { get; set; }
    public byte MapId { get; set; }
    public byte Rule { get; set; }                          // modeIndex (GameMode, 0..16)

    /// <summary>
    /// 房物件 +110: 開放槽位點陣 (bit 0..15 為 1 = 可入座)。
    /// client sub_53FB10 以 popcount 此點陣得出 +129 (最大人數) 並展開
    /// +112..+127 逐槽旗標; 108 房單 / 112 建房 / 114 進房 / 130 開戰 /
    /// 134 回房皆送此點陣。建檔時 = (1&lt;&lt;maxPlayers)-1; 房主可經
    /// 167 GR_CHANGEUSER 改為任意點陣 (含非連續槽位)。
    /// </summary>
    public ushort SlotMask { get; set; }

    /// <summary>+129: 開放槽位數 = popcount(SlotMask)。</summary>
    public byte OpenSlotCount => (byte)ushort.PopCount(SlotMask);

    /// <summary>開放槽位點陣 (沿用舊名, 同 SlotMask)。</summary>
    public ushort MaxSlotMask => SlotMask;

    // ── 房設定簇 (REQ/ACK 寫入的 room 欄位; 預設對齊 sub_53F920 建檔 ctor) ──
    public byte TimeLimit { get; set; } = 3;                // +136 (173/174 時間)
    public ushort WinCount { get; set; } = 10;              // +144 (171/172 勝場目標)
    public ushort KillCount { get; set; }                   // +148 (340/341 擊殺目標)
    public byte ItemMode { get; set; }                      // flags bit0=主武器, bit1=副武器 (175/176)
    public bool NoSkillBg { get; set; }                     // +185 (712/713)
    public bool TeamBalance { get; set; }                   // +186 (364/365; 一般房僅 client UI, 錦標賽才上 wire)
    public bool DoubleDamage { get; set; }                  // +128 (990/991)
    public bool LocalRoom { get; set; }                     // 區域限定房 (366/367; GAMEROOM_LOCALROOM)
    public bool TeamShuffle { get; set; }                   // 隊打散開關 (368/369; GAMEROOM_TEAMSHUFFLE)
    public bool Soccer { get; set; }                        // 足球模式開關 (969/970; GAMEROOM_SOCCER → mode+14)

    /// <summary>戰鬥進行中旗標 (130 GR_START 開戰設為 true, 134 GR_END 設為 false)。</summary>
    public bool Playing { get; set; }

    /// <summary>僅在本房、本局存在的權威戰場物件與據點狀態。</summary>
    public RoomBattleState BattleState { get; } = new();

    /// <summary>判斷指定 session 是否為當前房主。</summary>
    public bool IsMaster(Session session) =>
        Members.TryGetValue(MasterSlot, out var master) && ReferenceEquals(master, session);

    /// <summary>
    /// TH 模式最近一次植彈的隊伍 (0/1) — 316 GG_HACKSTART_REQ 首欄即 team,
    /// 但開駭可能失敗, 故只在 318 GG_HACKSUCC_REQ (正式武裝成功) 記下;
    /// 322 GG_BOMBSUCC_REQ 為空 payload, server 需回 323 [u8 team] 告知
    /// 哪隊的炸彈爆炸。null = 本回合尚無人成功植彈 (此時 322 無從回覆,
    /// 依「未確認不硬編」原則直接忽略)。
    /// </summary>
    public byte? BombTeam { get; set; }

    /// <summary>slot → session (最多 16 人)。</summary>
    public ConcurrentDictionary<byte, Session> Members { get; } = new();

    public byte MasterSlot { get; set; }

    private readonly HashSet<byte> _ready = [];
    private readonly HashSet<byte> _loaded = [];

    /// <summary>183 GR_ENDLOADING: 標記已載入。</summary>
    public void MarkLoaded(byte slot)
    {
        lock (_loaded)
        {
            _loaded.Add(slot);
        }
    }

    /// <summary>已載入 slot 快照 (188 開打名單)。</summary>
    public List<byte> LoadedSlots
    {
        get
        {
            lock (_loaded)
            {
                return [.. _loaded.Order()];
            }
        }
    }

    /// <summary>129 開戰時重置載入狀態。</summary>
    public void ResetLoading()
    {
        lock (_loaded)
        {
            _loaded.Clear();
        }
    }

    /// <summary>房主離開時選最小 slot 為新房主 (190 GR_CHANGEMASTER 廣播用)。</summary>
    public byte? ElectNewMaster()
    {
        var next = Members.Keys.Order().Cast<byte?>().FirstOrDefault();
        if (next is { } slot)
        {
            MasterSlot = slot;
        }

        return next;
    }

    /// <summary>128 GR_READY: 翻轉 slot 的 ready 狀態, 回新值。</summary>
    public bool ToggleReady(byte slot)
    {
        lock (_ready)
        {
            if (!_ready.Add(slot))
            {
                _ready.Remove(slot);
                return false;
            }

            return true;
        }
    }

    public byte? TakeFreeSlot()
    {
        for (byte s = 0; s < 16; s++)
        {
            if ((SlotMask & (1 << s)) != 0 && !Members.ContainsKey(s))
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>
    /// 隊打散 (894 GR_TEAMSHUFFLE): 把現有成員隨機重排到已佔用槽位,
    /// ready/loaded/房主旗標跟著成員走。回傳新的 (slot, member) 對照
    /// (以 slot 排序), 供 895 ACK 逐欄寫 (u8 slot, s32 uid)。
    /// </summary>
    public List<(byte Slot, Session Member)> ShuffleSlots()
    {
        var members = Members.OrderBy(kv => kv.Key).ToList();
        var newSlots = members.Select(m => m.Key).OrderBy(_ => Random.Shared.Next()).ToList();

        var next = new ConcurrentDictionary<byte, Session>();
        var slotMap = new Dictionary<byte, byte>(members.Count);       // 舊槽 → 新槽
        for (int i = 0; i < members.Count; i++)
        {
            byte oldSlot = members[i].Key;
            byte newSlot = newSlots[i];
            slotMap[oldSlot] = newSlot;
            next[newSlot] = members[i].Value;
            if (oldSlot == MasterSlot)
            {
                MasterSlot = newSlot;
            }
        }

        byte Remap(byte oldSlot) => slotMap.TryGetValue(oldSlot, out var s) ? s : oldSlot;

        lock (_ready)
        {
            var remapped = _ready.Select(Remap).ToArray();
            _ready.Clear();
            _ready.UnionWith(remapped);
        }

        lock (_loaded)
        {
            var remapped = _loaded.Select(Remap).ToArray();
            _loaded.Clear();
            _loaded.UnionWith(remapped);
        }

        Members.Clear();
        foreach (var (slot, member) in next)
        {
            member.SlotNo = slot;
            Members[slot] = member;
        }

        return next.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
    }
}

/// <summary>全服房間表 (room_no 0..209)。</summary>
public sealed class RoomManager
{
    private readonly ConcurrentDictionary<byte, Room> _rooms = new();
    private int _nextUid;

    public const byte MaxRooms = 210;                       // 0xD2 client 硬上限

    public Room? Create(Session master, byte mapId, string title, string? pass,
                        byte rule, byte maxPlayers)
    {
        for (byte no = 0; no < MaxRooms; no++)
        {
            var room = new Room
            {
                RoomNo = no,
                RoomUid = Interlocked.Increment(ref _nextUid),
                Title = title,
                Password = pass,
                MapId = mapId,
                Rule = rule,
                SlotMask = (ushort)((1 << Math.Clamp((int)maxPlayers, 2, 16)) - 1),
            };

            if (!_rooms.TryAdd(no, room))
            {
                continue;                                   // 房號已占用, 試下一個
            }

            room.Members[0] = master;
            room.MasterSlot = 0;
            master.RoomNo = no;
            master.SlotNo = 0;
            return room;
        }

        return null;                                        // 210 房全滿
    }

    public Room? Find(byte roomNo) =>
        _rooms.TryGetValue(roomNo, out var r) ? r : null;

    public IEnumerable<Room> All =>
        _rooms.Values.OrderBy(r => r.RoomNo);

    public void Remove(byte roomNo) =>
        _rooms.TryRemove(roomNo, out _);

    /// <summary>
    /// 成員離房共用流程 (主動離房 124 與斷線清理共用):
    /// 移除成員 → 空房回收 / 廣播 124 (u8 slot) →
    /// 房主離開時再廣播 190 (u8 = 新房主的 slot 號 0..15)。
    /// </summary>
    public async ValueTask RemoveMemberAsync(Room room, Session member)
    {
        // FirstOrDefault 的 default key 是 0：若不先驗證 value，競態/重複斷線
        // 可能誤刪真正房主的 slot 0。
        var entry = room.Members.FirstOrDefault(pair => ReferenceEquals(pair.Value, member));
        if (!ReferenceEquals(entry.Value, member))
        {
            return;
        }

        byte slot = entry.Key;
        bool wasMaster = slot == room.MasterSlot;
        if (!room.Members.TryRemove(slot, out _))
        {
            return;
        }

        room.BattleState.AbandonCapturesBy(slot, member.UserId);
        member.RoomNo = null;
        member.SlotNo = null;

        if (room.Members.IsEmpty)
        {
            Remove(room.RoomNo);
            return;
        }

        var leaveNotice = new Packet(Opcode.GR_LEAVE_ACK)
            .WriteU8(1)
            .WriteU8(slot);
        await BroadcastAsync(room, leaveNotice);

        if (wasMaster && room.ElectNewMaster() is { } newMaster)
        {
            // 190 的 u8 = client dword_F6DCF4[slot] 快取值 = 該槽的 slot 號
            // (112 寫 0=房主 / 114 寫 server 送的 slot 號 / 136 換位時更新)。
            // sub_56FBF0 以 sub_592940 讀 u8 後對照 F6DCF4 找出新房主 slot
            // 並戴皇冠 — 故送「新房主的 slot 號」, 而非 uid 低 8 位。
            var masterNotice = new Packet(Opcode.GR_CHANGEMASTER_ACK)
                .WriteU8(newMaster);
            await BroadcastAsync(room, masterNotice);
        }
    }

    /// <summary>對房內所有成員廣播 (逐 session 送出)。</summary>
    public static async ValueTask BroadcastAsync(Room room, Packet packet, Session? except = null)
    {
        foreach (var member in room.Members.Values)
        {
            if (ReferenceEquals(member, except))
            {
                continue;
            }

            try
            {
                await member.SendAsync(Clone(packet));
            }
            catch
            {
                // 個別成員斷線不影響其他人
            }
        }
    }

    private static Packet Clone(Packet packet) =>
        Packet.FromPayload(packet.Opcode, packet.Payload);
}
