// =============================================================================
// GC_ENTERCHANNEL_REQ (195) → GC_ENTERCHANNEL_ACK (196)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ChannelHandlers
{
    /// <summary>195 → 196. Only the sole 681-advertised group/channel is accepted by this single-channel host.</summary>
    private static async ValueTask GC_ENTERCHANNEL_REQ(Session session, Packet packet, ServerContext context)
    {
        EnterChannelResult result = EnterChannelResult.GenericError4;
        byte selectedChannel = 0;
        try
        {
            byte selectedGroup = packet.ReadU8();
            selectedChannel = packet.ReadU8();
            _ = packet.ReadU8(); // replay feature flag: parsed for framing; this host has no replay service.
            if (packet.Remaining != 0)
            {
                throw new InvalidDataException("GC_ENTERCHANNEL_REQ has trailing data.");
            }

            if (session.Authenticated
                && selectedGroup == context.Config.ChannelGroupIndex
                && selectedChannel == context.Config.ChannelIndex)
            {
                result = EnterChannelResult.Success;
            }
        }
        catch (EndOfStreamException)
        {
            // Return a well-framed non-success 196 rather than accidentally
            // interpreting missing bytes as channel zero.
        }
        catch (InvalidDataException)
        {
            // As above.
        }

        Packet acknowledgement = CreateGC_ENTERCHANNEL_ACK(result, selectedChannel, context.Config);
        await session.SendAsync(acknowledgement);

        // The receive loop awaits this handler, so completing the state only
        // after the success 196 was written preserves client-observable packet
        // order. A write failure leaves the connection unentered and is handled
        // by the session's normal disconnect path.
        if (result == EnterChannelResult.Success && !session.ChannelEntryCompleted)
        {
            session.CompleteChannelEntry();
        }
    }

    private static Packet CreateGC_ENTERCHANNEL_ACK(
        EnterChannelResult result,
        byte selectedChannel,
        ServerConfig config)
    {
        var metadata = config.EnterChannelMetadata;
        var endpoint = result is EnterChannelResult.Success
            ? new ChannelEndpoint(config.UdpHost, config.UdpPort)
            : null;

        return ChannelBootstrapWire.CreateEnterChannelAcknowledgement(new EnterChannelAcknowledgement(
            result,
            config.ChannelId,
            selectedChannel,
            endpoint,
            metadata.EndpointOpaqueByte,
            config.ChannelType,
            metadata.ClientFlags,
            metadata.ClientDefaultValue));
    }
}
