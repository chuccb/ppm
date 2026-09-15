// =============================================================================
// Room configuration and membership state.
//
// GameMode values are native modeIndex values from sub_53FBB0. Room fields keep
// their verified client layout/behavior names; this type carries no durable
// SQLite policy and does not own battle-object transitions.
// =============================================================================
using System.Collections.Concurrent;

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
