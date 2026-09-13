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
    public ushort WinCount { get; set; }

    /// <summary>slot → session (最多 16 人)。</summary>
    public ConcurrentDictionary<byte, Session> Members { get; } = new();

    public byte MasterSlot { get; set; }

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

    private static Packet Clone(Packet p) =>
        Packet.FromPayload(p.Opcode, p.Payload);
}
