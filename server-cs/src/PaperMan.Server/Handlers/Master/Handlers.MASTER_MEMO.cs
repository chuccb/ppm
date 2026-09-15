// =============================================================================
// MASTER_MEMO_REQ (275) → MASTER_MEMO_ACK (276)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 275 MASTER_MEMO_REQ (sub_578830: wstr memo) → 276 ACK (sub_578920): wstr memo
    private static async ValueTask MASTER_MEMO_REQ(Session session, Packet packet, ServerContext context)
    {
        var memo = packet.ReadWStr();
        await session.SendAsync(new Packet(Opcode.MASTER_MEMO_ACK).WriteWStr(memo));
    }
}
