// =============================================================================
// Channel-listener bootstrap handlers.
//
// Native path:
//   channel TCP connect → 693 → 143 → 144 → 195 → 196 → UDP bootstrap.
// 143's identity and the two values echoed from 681 are bound to a recent,
// single-use login admission; a new TCP connection must not become an arbitrary
// account merely by sending the public default 100 / 0 values.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ChannelHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.PM_UDPSTART_REQ, UdpStart);
        add(Opcode.GC_ENTERCHANNEL_REQ, EnterChannel);
        add(Opcode.GC_CHANNEL_REQ, ChannelQuery);
        add(Opcode.PM_CONNECT_REQ, PmConnect);
    }

    /// <summary>143 → 144. Claims the one recent successful 681 for this account.</summary>
    private static async ValueTask UdpStart(Session session, Packet packet, ServerContext context)
    {
        if (session.Authenticated)
        {
            // 143 is a one-time bootstrap claim. In particular, do not let an
            // already authenticated channel socket consume another same-IP
            // admission and replace its account identity.
            await session.SendAsync(CreateUdpStartAcknowledgement(
                context.Config,
                UdpStartResult.AlreadyConnected));
            return;
        }

        UdpStartResult status;
        try
        {
            string identity = packet.ReadNulTerminatedAnsiString(LoginWire.MaxChannelIdentityBytes);
            int billingUiMode = packet.ReadS32();
            byte requiredOne = packet.ReadU8();
            int featureExtensionCount = packet.ReadS32();
            if (packet.Remaining != 0)
            {
                throw new InvalidDataException("PM_UDPSTART_REQ has trailing data.");
            }

            bool claimed = requiredOne == 1
                && context.ChannelAdmissions.TryClaim(
                    billingUiMode,
                    featureExtensionCount,
                    session.RemoteIp,
                    DateTimeOffset.UtcNow,
                    out var admission);
            if (claimed)
            {
                session.BindAuthentication(
                    admission.AccountId,
                    admission.UserId,
                    admission.LoginName,
                    admission.Nickname);
                status = UdpStartResult.Success;
                Console.WriteLine($"[s{session.Id}] accepted channel handoff for account '{ToLogSafe(identity)}'");
            }
            else
            {
                status = UdpStartResult.UnauthorizedAccount;
                Console.WriteLine($"[s{session.Id}] rejected channel handoff for identity '{ToLogSafe(identity)}'");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or EndOfStreamException or InvalidDataException)
        {
            status = UdpStartResult.UnauthorizedAccount;
            Console.WriteLine($"[s{session.Id}] malformed PM_UDPSTART_REQ: {exception.Message}");
        }

        await session.SendAsync(CreateUdpStartAcknowledgement(context.Config, status));
    }

    /// <summary>195 → 196. Only the sole 681-advertised group/channel is accepted by this single-channel host.</summary>
    private static async ValueTask EnterChannel(Session session, Packet packet, ServerContext context)
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

        await session.SendAsync(CreateEnterChannelAcknowledgement(result, selectedChannel, context.Config));
    }

    /// <summary>141 → 142, issued after UDP op18 asks for its endpoint confirmation.</summary>
    private static async ValueTask PmConnect(Session session, Packet packet, ServerContext context)
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

    // 193 GC_CHANNEL_REQ exists only in the clan-channel scene. Normal-scene
    // 194 has a different reader, so this remains the clan reset response.
    private static async ValueTask ChannelQuery(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU32();
        if (packet.Remaining != 0)
        {
            return;
        }

        await session.SendAsync(new Packet(Opcode.GC_CHANNEL_ACK).WriteU32(2));
    }

    private static Packet CreateUdpStartAcknowledgement(ServerConfig config, UdpStartResult result)
    {
        // sub_555D50 consumes the full 144 header before acting on Result.
        // UdpStartMetadata therefore exposes every mandatory value in native
        // order, including the optional sNetCafeInfo tail when configured.
        var metadata = config.UdpStartMetadata;
        return ChannelBootstrapWire.CreateUdpStartAcknowledgement(new UdpStartAcknowledgement(
            result,
            metadata.RankRestrictedServerFlag,
            metadata.DailyLoginRewardPoints,
            config.ChannelName,
            metadata.ReservedValueAfterChannelNameOne,
            metadata.ReservedValueAfterChannelNameTwo,
            metadata.ChannelRestrictionLevel,
            metadata.ChannelRestrictionKdr,
            metadata.ClientRequestContextValue,
            metadata.NetCafeInfo));
    }

    private static Packet CreateEnterChannelAcknowledgement(
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

    private static string ToLogSafe(string value) => string.Create(value.Length, value, static (destination, source) =>
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = char.IsControl(source[index]) ? '.' : source[index];
        }
    });
}
