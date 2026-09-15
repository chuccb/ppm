// =============================================================================
// MASTER_SETALL_EVENTPAGE_REQ (843) → MASTER_SETALL_EVENTPAGE_ACK (844)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 843 MASTER_SETALL_EVENTPAGE_REQ (f32 rate) → 844 ACK: f32 rate
    private static async ValueTask MASTER_SETALL_EVENTPAGE_REQ(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_SETALL_EVENTPAGE_ACK).WriteF32(rate));
    }
}
