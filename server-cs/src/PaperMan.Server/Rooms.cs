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
// (0=TeamDeath 1=FreeForAll 2=TeamHacking 3=TeamSurvival 4=TeamSteal
//  8=PNR 9=GunShooting 12=SOCCER — map_StartIndex.xml)。
// =============================================================================
using System.Collections.Concurrent;
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>單一房間的即時狀態 (記憶體為主, DB rooms 表為快照)。</summary>
public sealed class Room
{
    public required byte RoomNo { get; init; }
    public required int RoomUid { get; init; }
    public required string Title { get; set; }
    public string? Password { get; set; }
    public byte MapId { get; set; }
    public byte Rule { get; set; }                          // modeIndex
    public byte MaxPlayers { get; set; } = 16;

    /// <summary>
    /// 房物件 +110: 上限槽位點陣 — bit 0..MaxPlayers-1 為 1。
    /// client sub_53FB10 以 popcount 此點陣得出 +129 (最大人數) 並展開
    /// +112..+127 逐槽旗標; 108 房單 / 112 建房 / 114 進房 / 130 開戰 /
    /// 134 回房皆送此點陣 (全數交叉驗證, 非勝場點陣)。
    /// </summary>
    public ushort MaxSlotMask =>
        (ushort)((1 << Math.Clamp(MaxPlayers, 0, 16)) - 1);

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
        for (byte s = 0; s < MaxPlayers && s < 16; s++)
        {
            if (!Members.ContainsKey(s))
            {
                return s;
            }
        }

        return null;
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
                MaxPlayers = Math.Clamp(maxPlayers, (byte)2, (byte)16),
            };

            if (!_rooms.TryAdd(no, room))
            {
                continue;                                   // 房號已占用, 試下一個
            }

            room.Members[0] = master;
            room.MasterSlot = 0;
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
        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, member)).Key;
        bool wasMaster = slot == room.MasterSlot;
        room.Members.TryRemove(slot, out _);
        member.RoomNo = null;

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
