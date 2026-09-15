// =============================================================================
// Process-local room table and member/broadcast operations.
//
// Room numbers remain constrained by the client 0xD2 array. This manager owns
// membership lifecycle and post-removal broadcasts, not packet-field parsing or
// persistent player data.
// =============================================================================
using System.Collections.Concurrent;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<byte, Room> _rooms = new();
    private int _nextUid;

    public const byte MaxRooms = 210;                       // 0xD2 client 硬上限

    public Room? Create(
        Session master,
        byte mapId,
        string title,
        string? password,
        byte modeIndex,
        byte maxPlayers,
        bool noSkillBackground)
    {
        // Replacing an in-room session's RoomNo would leave it in the first
        // room's Members map. A session owns at most one room membership.
        if (master.RoomNo is not null)
        {
            return null;
        }

        for (byte no = 0; no < MaxRooms; no++)
        {
            var room = new Room
            {
                RoomNo = no,
                RoomUid = Interlocked.Increment(ref _nextUid),
                Title = title,
                Password = password,
                MapId = mapId,
                ModeIndex = modeIndex,
                NoSkillBg = noSkillBackground,
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
