// =============================================================================
// MASTER_USERCUT_REQ (279) → MASTER_USERCUT_ACK (280)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 279 MASTER_USERCUT_REQ (sub_578E50: u8 mode, str nick) → 280 ACK (sub_578FB0): 空包/成功
    private static async ValueTask MASTER_USERCUT_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var nick = packet.ReadStr();
        var target = context.Sessions.Find(nick);
        if (target is not null)
        {
            target.Dispose();
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERCUT_ACK));
    }
}
