// =============================================================================
// GR_RESET_GAMEROOMSLOT_REQ (944) → GR_RESET_GAMEROOMSLOT_ACK (945)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AIHandlers
{
    // 944 GR_RESET_GAMEROOMSLOT_REQ (sub_585E90: 空) → 945 ACK (sub_585F30): u8 status(1)
    private static async ValueTask GR_RESET_GAMEROOMSLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GR_RESET_GAMEROOMSLOT_ACK).WriteU8(1);
        await session.SendAsync(ack);
    }
}
