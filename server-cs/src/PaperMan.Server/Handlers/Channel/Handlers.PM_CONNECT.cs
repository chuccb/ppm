// =============================================================================
// PM_CONNECT_REQ (141) → PM_CONNECT_ACK (142)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ChannelHandlers
{
    /// <summary>141 → 142, issued after UDP op18 asks for its endpoint confirmation.</summary>
    private static async ValueTask PM_CONNECT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!session.Authenticated || packet.Remaining != 0)
        {
            return;
        }

        var endpoint = new ChannelEndpoint(context.Config.UdpHost, context.Config.UdpPort);
        var calendarTime = PmConnectCalendarTime.From(context.Config.GetProtocolCalendarTime());
        await session.SendAsync(ChannelBootstrapWire.CreatePmConnectAcknowledgement(
            endpoint,
            context.Config.ChannelIndex,
            calendarTime));
    }
}
