// =============================================================================
// GR_LEAVE_REQ (123) → GR_LEAVE_ACK (124)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 123 GR_LEAVE_REQ → 124 ACK (u8 result; ≠0 → u8 slot 離房廣播)。
    // 與斷線清理共用 RemoveMemberAsync — 房主離房時一併廣播 190 新房主
    // (否則新房主不會戴皇冠, UI 卡在無房主狀態)。
    private static async ValueTask GR_LEAVE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        await context.Rooms.RemoveMemberAsync(room, session);
        await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }

}
