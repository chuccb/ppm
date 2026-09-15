// =============================================================================
// MASTER_PRINTUSER_REQ (287) → MASTER_PRINTUSER_ACK (288)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 287 MASTER_PRINTUSER_REQ (sub_579100, 空) → 288 ACK: s32 count
    private static async ValueTask MASTER_PRINTUSER_REQ(Session session, Packet packet, ServerContext context)
    {
        int count = context.Sessions.All.Count(s => s.Authenticated);
        await session.SendAsync(new Packet(Opcode.MASTER_PRINTUSER_ACK).WriteS32(count));
    }
}
