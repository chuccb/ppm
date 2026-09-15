// =============================================================================
// GC_ENTERCHANNEL_ACK (196) exact writer contract.
//
// Evidence: CLobbyChannel::sub_4179D0 reads the three-word prefix and consumes
// the endpoint tail only for result 1. This type does not establish channel
// admission or account policy.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class ChannelBootstrapWire
{
    /// <summary>
    /// Serializes GC_ENTERCHANNEL_ACK(196). A client reads only the first three
    /// fields unless Result is exactly <see cref="EnterChannelResult.Success"/>.
    /// </summary>
    public static Packet CreateEnterChannelAcknowledgement(EnterChannelAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);

        var packet = new Packet(Opcode.GC_ENTERCHANNEL_ACK)
            .WriteU8((byte)acknowledgement.Result)
            .WriteS32(acknowledgement.ChannelId)
            .WriteU8(acknowledgement.ChannelIndex);

        if (acknowledgement.Result is not EnterChannelResult.Success)
        {
            if (acknowledgement.Endpoint is not null)
            {
                throw new ArgumentException(
                    "A non-success 196 has no endpoint tail in the native reader.",
                    nameof(acknowledgement));
            }

            return packet;
        }

        var endpoint = acknowledgement.Endpoint
            ?? throw new ArgumentException("A successful 196 requires an endpoint tail.", nameof(acknowledgement));
        ValidateEndpoint(endpoint, "196 endpoint");

        return packet.WriteStr(endpoint.Host)
            .WriteS32(endpoint.Port)
            .WriteU8(acknowledgement.EndpointOpaqueByte)
            .WriteU8(acknowledgement.ChannelType)
            .WriteU32(acknowledgement.ClientFlags)
            .WriteU8(acknowledgement.ClientDefaultValue);
    }

}

/// <summary>GC_ENTERCHANNEL_ACK(196) result byte values handled by sub_4177B0.</summary>
public enum EnterChannelResult : byte
{
    ChannelFull = 0,
    Success = 1,
    RankRestricted = 2,
    ClanRequired = 3,
    GenericError4 = 4,
    GenericError5 = 5,
    Error6 = 6,
    GenericError7 = 7,
    Error8 = 8,
    GenericError9 = 9,
}

/// <summary>
/// GC_ENTERCHANNEL_ACK(196)'s prefix and, for Success only, its endpoint tail.
/// The client stores ChannelIndex as its active channel selection. ClientFlags
/// bit zero is known to control one native client flag; remaining bits and the
/// two surrounding u8 values remain wire-oriented pending server captures.
/// </summary>
public sealed record EnterChannelAcknowledgement(
    EnterChannelResult Result,
    int ChannelId,
    byte ChannelIndex,
    ChannelEndpoint? Endpoint = null,
    byte EndpointOpaqueByte = 0,
    byte ChannelType = 0,
    uint ClientFlags = 0,
    byte ClientDefaultValue = 5);
