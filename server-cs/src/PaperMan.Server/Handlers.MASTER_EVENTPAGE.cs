// =============================================================================
// MASTER_EVENTPAGE_REQ (402) → MASTER_EVENTPAGE_ACK (403)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 402 MASTER_EVENTPAGE_REQ (sub_579450: f32 rate) → 403 ACK (sub_579500): f32 rate
    private static async ValueTask MASTER_EVENTPAGE_REQ(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_EVENTPAGE_ACK).WriteF32(rate));
    }
}
