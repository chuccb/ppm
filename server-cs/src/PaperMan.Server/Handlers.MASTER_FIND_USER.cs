// =============================================================================
// MASTER_FIND_USER_REQ (883) → MASTER_FIND_USER_ACK (884)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 883 MASTER_FIND_USER_REQ (sub_579B10: s32 uid)
    // → 884 ACK (sub_579BC0): u8 status(1=找到), s32 uid, str nick, u8 channel, u8 room_no
    private static async ValueTask MASTER_FIND_USER_REQ(Session session, Packet packet, ServerContext context)
    {
        int uid = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var target = context.Sessions.All.FirstOrDefault(s => s.UserId == uid);

        var ack = new Packet(Opcode.MASTER_FIND_USER_ACK);
        if (target is not null)
        {
            ack.WriteU8(1)                                  // status 1 = 找到
               .WriteS32(uid)
               .WriteStr(target.Nickname)
               .WriteU8(0)                                  // channel
               .WriteU8(target.RoomNo ?? 0);                // room_no
        }
        else
        {
            ack.WriteU8(0);                                 // status 0 = 找不到
        }

        await session.SendAsync(ack);
    }
}
