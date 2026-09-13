// =============================================================================
// 房間 handlers — 111/112 建房, 113/114 進房, 123/124 離房 (廿八輪)。
//
// 佈局出自反編譯 (docs/PACKETS.md §3.15 + 廿八輪逐欄定案):
//   111 REQ: u8 map, s8 has_pass, str title, [str pass],
//            u8 rule(modeIndex), u8 max, u8 x, u8 y
//   112 ACK (sub_56A7B0): u8 err, u8 room_no(<210), u16, s32 room_uid,
//            u8, s8 obs — err==0 → client 自任房主, 狀態切 10
//   113 REQ: u8 room_no
//   114 ACK sub_type==1: 單人進房通知 (s32 uid, u8 slot, str nick +
//            CClientData 嵌入 — 簡版送 0 塊)
//   124 GR_LEAVE_ACK: u8 result; ≠0 → u8 slot (成員移除廣播)
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class RoomHandlers
{
    /// <summary>112 的 err 碼 (sub_56A7B0: err!=0 → 顯示失敗訊息)。</summary>
    private enum MakeRoomError : byte
    {
        Ok = 0,
        Full = 1,                                           // 210 房全滿
        BadParams = 2,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.GL_MAKEROOM_REQ, MakeRoom);
        add(Opcode.GL_ENTERROOM_REQ, EnterRoom);
        add(Opcode.GR_LEAVE_REQ, LeaveRoom);
    }

    // 111 → 112 (+108 更新大廳清單由 client 重拉)
    private static async ValueTask MakeRoom(Session s, Packet p, ServerContext ctx)
    {
        byte mapId = p.ReadU8();
        sbyte hasPass = p.ReadS8();
        var title = p.ReadStr();
        var pass = hasPass != 0 ? p.ReadStr() : null;
        byte rule = p.Remaining > 0 ? p.ReadU8() : (byte)0;
        byte max = p.Remaining > 0 ? p.ReadU8() : (byte)16;

        var room = s.UserId != 0
            ? ctx.Rooms.Create(s, mapId, title, pass, rule, max)
            : null;

        var err = room is null ? MakeRoomError.Full : MakeRoomError.Ok;
        var ack = new Packet(Opcode.GL_MAKEROOM_ACK).WriteU8((byte)err);

        if (room is not null)
        {
            s.RoomNo = room.RoomNo;
            ack.WriteU8(room.RoomNo)
               .WriteU16(0)                                 // v52 (保留)
               .WriteS32(room.RoomUid)                      // v57 → dword_F2A65C
               .WriteU8(0)                                  // v50
               .WriteS8(0);                                 // v53 obs flag
        }

        await s.SendAsync(ack);
    }

    // 113 → 114 (sub_type==1 單人通知) + 房內廣播
    private static async ValueTask EnterRoom(Session s, Packet p, ServerContext ctx)
    {
        byte roomNo = p.ReadU8();
        var room = ctx.Rooms.Find(roomNo);
        byte? slot = room?.TakeFreeSlot();

        if (room is null || slot is null || s.UserId == 0)
        {
            // sub_type==0 = 失敗回大廳
            await s.SendAsync(new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(0));
            return;
        }

        room.Members[slot.Value] = s;
        s.RoomNo = roomNo;

        // 廣播單人進房通知給房內其他人 (sub_type==1)
        var notice = new Packet(Opcode.GL_ENTERROOM_ACK)
            .WriteU8(1)
            .WriteS32((int)s.UserId)
            .WriteU8(slot.Value)
            .WriteStr(s.Nickname);
        await RoomManager.BroadcastAsync(room, notice, except: s);

        // 給進房者自己也發 sub_type==1 (client 依 uid 判斷是否本人)
        await s.SendAsync(Packet.FromPayload(notice.Opcode, notice.Payload));
    }

    // 123 GR_LEAVE_REQ → 124 ACK (u8 result + u8 slot 廣播)
    private static async ValueTask LeaveRoom(Session s, Packet p, ServerContext ctx)
    {
        if (s.RoomNo is not { } roomNo || ctx.Rooms.Find(roomNo) is not { } room)
        {
            await s.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        room.Members.TryRemove(slot, out _);
        s.RoomNo = null;

        if (room.Members.IsEmpty)
        {
            ctx.Rooms.Remove(roomNo);                       // 空房回收
        }
        else
        {
            var notice = new Packet(Opcode.GR_LEAVE_ACK)
                .WriteU8(1)
                .WriteU8(slot);
            await RoomManager.BroadcastAsync(room, notice);
        }

        await s.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }
}
