// =============================================================================
// MASTER_USERCUT2_REQ (281) → MASTER_USERCUT2_ACK (282)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 281 MASTER_USERCUT2_REQ (sub_578F00: s32 uid) → 282 ACK: 空包
    private static async ValueTask MASTER_USERCUT2_REQ(Session session, Packet packet, ServerContext context)
    {
        int uid = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var target = context.Sessions.All.FirstOrDefault(s => s.UserId == uid);
        if (target is not null)
        {
            target.Dispose();
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERCUT2_ACK));
    }
}
