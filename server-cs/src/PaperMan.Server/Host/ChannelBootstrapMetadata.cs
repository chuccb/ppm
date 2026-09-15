// =============================================================================
// Wire-oriented metadata for PM_UDPSTART_ACK(144) and GC_ENTERCHANNEL_ACK(196).
//
// These types retain neutral/raw names where source evidence establishes shape
// but not the original-server domain meaning or policy.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>
/// Confirmed PM_UDPSTART_ACK(144) fields after the status byte. The native
/// client consumes the complete sequence in sub_555D50 before branching on its
/// result. Neutral is meaningful: no rank warning, no daily PG grant, no
/// restriction values, no client request context, and no optional net-café
/// tail.
/// </summary>
public readonly record struct UdpStartAcknowledgementMetadata(
    byte RankRestrictedServerFlag,
    int DailyLoginRewardPoints,
    int ReservedValueAfterChannelNameOne,
    int ReservedValueAfterChannelNameTwo,
    int ChannelRestrictionLevel,
    float ChannelRestrictionKdr,
    uint ClientRequestContextValue,
    NetCafeBootstrapInfo? NetCafeInfo)
{
    public static UdpStartAcknowledgementMetadata Neutral => new(0, 0, 0, 0, 0, 0f, 0, null);

    /// <summary>Validates the exact optional 144 sNetCafeInfo tail shape.</summary>
    public void Validate()
    {
        if (RankRestrictedServerFlag > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RankRestrictedServerFlag),
                "144 rank-restricted-server flag must be the native 0 or 1 value.");
        }

        if (NetCafeInfo is { SlotValues: null }
            || NetCafeInfo?.SlotValues is { Count: not NetCafeBootstrapInfo.SlotValueCount })
        {
            throw new ArgumentException(
                $"144 NetCafeInfo must contain exactly {NetCafeBootstrapInfo.SlotValueCount} raw four-byte values.",
                nameof(NetCafeInfo));
        }
    }
}

/// <summary>Confirmed trailing fields of a successful non-AI 196.</summary>
public readonly record struct EnterChannelAcknowledgementMetadata(
    byte EndpointOpaqueByte,
    uint ClientFlags,
    byte ClientDefaultValue)
{
    // sub_4179D0 initializes this last byte to 5 before reading the packet.
    // The three server-domain meanings remain unresolved, so the values retain
    // wire-oriented names rather than being misrepresented as an endpoint ID.
    public static EnterChannelAcknowledgementMetadata NativeClientInitialValues => new(0, 0, 5);
}
