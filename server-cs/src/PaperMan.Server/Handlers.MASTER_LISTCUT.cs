// =============================================================================
// MASTER_LISTCUT_REQ (291) → MASTER_LISTCUT_ACK (292)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 291 MASTER_LISTCUT_REQ (str nick) → 292 ACK: 空包
    private static async ValueTask MASTER_LISTCUT_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadStr();
        await session.SendAsync(new Packet(Opcode.MASTER_LISTCUT_ACK));
    }
}
