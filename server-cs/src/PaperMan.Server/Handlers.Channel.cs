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
    // sub_555D50 consumes the full 144 header before acting on n108.
    private enum UdpStartStatus : byte
    {
        Failed = 0,
        Ok = 1,
        OkAlternateMode = 2,
        Kicked = 3,
        DuplicateLogin = 4,
        Rejected = 5,
    }

    // CLobbyChannel::sub_4179D0 always reads this three-field prefix of 196.
    private enum EnterChannelResult : byte
    {
        Full = 0,
        Ok = 1,
        Maintenance = 2,
        VersionMismatch = 3,
        GenericError = 4,
        Error6 = 6,
        Error8 = 8,
    }

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
                session,
                context.Config,
                UdpStartStatus.Kicked));
            return;
        }

        UdpStartStatus status;
        try
        {
            string identity = packet.ReadNulTerminatedAnsiString(LoginWire.MaxChannelIdentityBytes);
            int billingUiMode = packet.ReadS32();
            sbyte requiredOne = packet.ReadS8();
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
                status = UdpStartStatus.Ok;
                Console.WriteLine($"[s{session.Id}] accepted channel handoff for account '{ToLogSafe(identity)}'");
            }
            else
            {
                status = UdpStartStatus.Kicked;
                Console.WriteLine($"[s{session.Id}] rejected channel handoff for identity '{ToLogSafe(identity)}'");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or EndOfStreamException or InvalidDataException)
        {
            status = UdpStartStatus.Kicked;
            Console.WriteLine($"[s{session.Id}] malformed PM_UDPSTART_REQ: {exception.Message}");
        }

        await session.SendAsync(CreateUdpStartAcknowledgement(session, context.Config, status));
    }

    /// <summary>195 → 196. Only the sole 681-advertised group/channel is accepted by this single-channel host.</summary>
    private static async ValueTask EnterChannel(Session session, Packet packet, ServerContext context)
    {
        EnterChannelResult result = EnterChannelResult.GenericError;
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
                result = EnterChannelResult.Ok;
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

        var metadata = context.Config.PmConnectMetadata;
        await session.SendAsync(new Packet(Opcode.PM_CONNECT_ACK)
            .WriteStr(context.Config.UdpHost)
            .WriteS32(context.Config.UdpPort)
            .WriteU8(metadata.OpaqueFlag)
            .WriteU32(metadata.OpaqueConfiguration));
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

    private static Packet CreateUdpStartAcknowledgement(Session session, ServerConfig config, UdpStartStatus status)
    {
        if (session.Id is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidOperationException("144 connection id exceeds the native signed 32-bit field.");
        }

        int sessionId = (int)session.Id;

        // sub_555D50 unconditionally reads all fields below. For this normal
        // route it uses n108 to trigger 195; named metadata keeps the remaining
        // proven-but-not-yet-semantic fields configurable without inventing a
        // relationship to account, server, or UDP endpoint state.
        var metadata = config.UdpStartMetadata;
        return new Packet(Opcode.PM_UDPSTART_ACK)
            .WriteU8((byte)status)
            .WriteU8(metadata.SecondaryStatus)
            .WriteS32(sessionId)
            .WriteStr(config.ChannelName)
            .WriteS32(metadata.FirstOpaqueValue)
            .WriteS32(metadata.SecondOpaqueValue)
            .WriteS32(metadata.ThirdOpaqueValue)
            .WriteF32(metadata.FourthOpaqueValue)
            .WriteU32(metadata.FifthOpaqueValue) // sub_592AC0, not s32
            .WriteU8(metadata.OptionalBlockFlag);
    }

    private static Packet CreateEnterChannelAcknowledgement(
        EnterChannelResult result,
        byte selectedChannel,
        ServerConfig config)
    {
        var acknowledgement = new Packet(Opcode.GC_ENTERCHANNEL_ACK)
            .WriteU8((byte)result)
            .WriteS32(config.ChannelId)
            .WriteU8(selectedChannel);

        if (result is not EnterChannelResult.Ok)
        {
            return acknowledgement;
        }

        var metadata = config.EnterChannelMetadata;
        return acknowledgement
            .WriteStr(config.UdpHost)
            .WriteS32(config.UdpPort)
            .WriteU8(metadata.OpaqueEndpointFlag)
            .WriteU8(config.ChannelType)
            .WriteU32(metadata.ClientFlags) // sub_592AC0; bit 0 controls a native flag
            .WriteU8(metadata.ClientDefaultValue);
    }

    private static string ToLogSafe(string value) => string.Create(value.Length, value, static (destination, source) =>
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = char.IsControl(source[index]) ? '.' : source[index];
        }
    });
}
