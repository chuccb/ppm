// =============================================================================
// GL_JOIN_REQ (260) → GL_JOIN_ACK (261)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    /// <summary>261 的 code (sub_449920 的 switch)。</summary>
    private enum GL_JOIN_ACK_Code : byte
    {
        Loading = 0,            // 0x3E データ読み込み中 (稍候重試)
        Ending = 1,             // 0x43 ゲーム終了中 (不可加入, 稍後再試)
        Ok = 2,                 // 可進房 → client 自動發 264
        NoRoom = 3,             // 0x32C 参加できるゲームルームがありません
    }

    // 260 GL_JOIN_REQ (u8 room_no) → 261 (u8 code)。
    // 進房後以 114 sub_type==1 通知既有成員 (與 113 進房同款廣播),
    // 進房者本身由後續 264→265 取得完整房資訊。
    private static async ValueTask GL_JOIN_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();

        if (session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)GL_JOIN_ACK_Code.NoRoom));
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)GL_JOIN_ACK_Code.NoRoom));
            return;
        }

        // 已在同房 → 冪等回 OK; 在別房 → 先離房再進。
        if (session.RoomNo == roomNo)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)GL_JOIN_ACK_Code.Ok));
            return;
        }

        if (session.RoomNo is { } prevNo
            && context.Rooms.Find(prevNo) is { } prev
            && prev.Members.Values.Any(m => ReferenceEquals(m, session)))
        {
            await context.Rooms.RemoveMemberAsync(prev, session);
        }

        byte? slot = room.TakeFreeSlot();
        if (slot is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)GL_JOIN_ACK_Code.Ending));
            return;
        }

        room.Members[slot.Value] = session;
        session.RoomNo = roomNo;

        var newMember = RoomHandlers.LoadGL_ENTERROOM_ACK_MemberData(context.Db, session);
        var notice = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(1);
        RoomHandlers.WriteGL_ENTERROOM_ACK_MemberNotice(notice, session, slot.Value, newMember);
        await RoomManager.BroadcastAsync(room, notice, except: session);

        await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)GL_JOIN_ACK_Code.Ok));
    }
}
