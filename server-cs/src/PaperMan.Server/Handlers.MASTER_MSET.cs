// =============================================================================
// MASTER_MSET_REQ (285) → MASTER_MSET_ACK (286)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 285 MASTER_MSET_REQ (sub_579040: u8 flag) → 286 ACK (sub_578D20): u8 flag
    private static async ValueTask MASTER_MSET_REQ(Session session, Packet packet, ServerContext context)
    {
        byte flag = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        await session.SendAsync(new Packet(Opcode.MASTER_MSET_ACK).WriteU8(flag));
    }
}
