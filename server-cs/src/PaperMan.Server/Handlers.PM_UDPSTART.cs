// =============================================================================
// PM_UDPSTART_REQ (143) → PM_UDPSTART_ACK (144)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ChannelHandlers
{
    // =============================================================================
    // Channel-listener bootstrap handlers.
    //
    // Native path:
    //   channel TCP connect → 693 → 143 → 144 → 195 → 196 → UDP bootstrap.
    // 143's identity and the two values echoed from 681 are bound to a recent,
    // single-use login admission; a new TCP connection must not become an arbitrary
    // account merely by sending the public default 100 / 0 values.
    // =============================================================================
    /// <summary>143 → 144. Claims the one recent successful 681 for this account.</summary>
    private static async ValueTask PM_UDPSTART_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.Authenticated)
        {
            // 143 is a one-time bootstrap claim. In particular, do not let an
            // already authenticated channel socket consume another same-IP
            // admission and replace its account identity.
            await session.SendAsync(CreatePM_UDPSTART_ACK(
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

            if (requiredOne != 1)
            {
                status = UdpStartResult.UnauthorizedAccount;
                Console.WriteLine($"[s{session.Id}] rejected channel handoff with an invalid literal for identity '{ToLogSafe(identity)}'");
            }
            else if (context.ChannelAdmissions.TryClaim(
                billingUiMode,
                featureExtensionCount,
                session.RemoteIp,
                DateTimeOffset.UtcNow,
                out ChannelAdmission? admission))
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

        await session.SendAsync(CreatePM_UDPSTART_ACK(context.Config, status));
    }

    private static Packet CreatePM_UDPSTART_ACK(ServerConfig config, UdpStartResult result)
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

    private static string ToLogSafe(string value) => string.Create(value.Length, value, static (destination, source) =>
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = char.IsControl(source[index]) ? '.' : source[index];
        }
    });
}
