// =============================================================================
// GC_CHANNEL_REQ (193) → GC_CHANNEL_ACK (194)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ChannelHandlers
{
    // 193 GC_CHANNEL_REQ exists only in the clan-channel scene. Normal-scene
    // 194 has a different reader, so this remains the clan reset response.
    private static async ValueTask GC_CHANNEL_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU32();
        if (packet.Remaining != 0)
        {
            return;
        }

        await session.SendAsync(new Packet(Opcode.GC_CHANNEL_ACK).WriteU32(2));
    }
}
