// =============================================================================
// PM_UDPSTART_ACK (144) exact writer contract.
//
// Evidence: sub_555D50 reads every mandatory field before its result branch.
// Its optional net-cafe tail retains raw positional names where the source has
// not established business semantics.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class ChannelBootstrapWire
{
    /// <summary>
    /// Serializes the complete PM_UDPSTART_ACK(144) shape. The client reads all
    /// mandatory fields before checking <see cref="UdpStartAcknowledgement.Result"/>.
    /// A non-null NetCafeInfo therefore appends exactly four u8 values and eight
    /// raw four-byte values after its presence flag.
    /// </summary>
    public static Packet CreateUdpStartAcknowledgement(UdpStartAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (acknowledgement.RankRestrictedServerFlag > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acknowledgement),
                "144 rank-restricted-server flag must be the native 0 or 1 value.");
        }

        LoginWire.RequireAnsiString(
            acknowledgement.ChannelName,
            LoginWire.MaxUdpStartChannelNameBytes,
            "144 channel name");

        var packet = new Packet(Opcode.PM_UDPSTART_ACK)
            .WriteU8((byte)acknowledgement.Result)
            .WriteU8(acknowledgement.RankRestrictedServerFlag)
            .WriteS32(acknowledgement.DailyLoginRewardPoints)
            .WriteStr(acknowledgement.ChannelName)
            .WriteS32(acknowledgement.ReservedValueAfterChannelNameOne)
            .WriteS32(acknowledgement.ReservedValueAfterChannelNameTwo)
            .WriteS32(acknowledgement.ChannelRestrictionLevel)
            .WriteF32(acknowledgement.ChannelRestrictionKdr)
            .WriteU32(acknowledgement.ClientRequestContextValue);

        if (acknowledgement.NetCafeInfo is not { } netCafeInfo)
        {
            return packet.WriteU8(0);
        }

        ValidateNetCafeInfo(netCafeInfo);
        packet.WriteU8(1)
              .WriteU8(netCafeInfo.FirstWireByte)
              .WriteU8(netCafeInfo.SecondWireByte)
              .WriteU8(netCafeInfo.ThirdWireByte)
              .WriteU8(netCafeInfo.FourthWireByte);
        foreach (int value in netCafeInfo.SlotValues)
        {
            packet.WriteS32(value);
        }

        return packet;
    }

    private static void ValidateNetCafeInfo(NetCafeBootstrapInfo netCafeInfo)
    {
        ArgumentNullException.ThrowIfNull(netCafeInfo.SlotValues);
        if (netCafeInfo.SlotValues.Count != NetCafeBootstrapInfo.SlotValueCount)
        {
            throw new ArgumentException(
                $"144 NetCafeInfo must contain exactly {NetCafeBootstrapInfo.SlotValueCount} raw four-byte values.",
                nameof(netCafeInfo));
        }
    }
}

/// <summary>
/// All client-visible PM_UDPSTART_ACK(144) status values recovered from
/// sub_555D50 and the CP932 message table. Only Success / SuccessAlternateMode
/// continue into the channel bootstrap; the remaining values display the
/// corresponding login/channel error UI.
/// </summary>
public enum UdpStartResult : byte
{
    Failed = 0,
    Success = 1,
    SuccessAlternateMode = 2,
    VersionMismatch = 3,
    AlreadyConnected = 4,
    UnauthorizedAccount = 5,
    IntermediateChannelLevelRestricted = 6,
    IntermediateChannelKdrRestricted = 7,
    LightServerRestricted = 8,
    BeginnerServerRestricted = 9,
    IntermediateServerRestricted = 10,
    AccountDoesNotExist = 101,
    WithdrawnAccount = 102,
    SuspendedAccount = 103,
    AuthenticationFailure = 104,
    NonMember = 105,
    ConnectionBlocked = 106,
    BannedAccount = 107,
    OtherLoginFailure = 108,
}

/// <summary>
/// Complete PM_UDPSTART_ACK(144) payload. The two reserved values are consumed
/// but otherwise unused by sub_555D50. The client propagates
/// ClientRequestContextValue into later requests, but its server-domain meaning
/// is not yet established. A DailyLoginRewardPoints value above zero displays a
/// CP932 resource message saying that many PG were granted today.
/// </summary>
public sealed record UdpStartAcknowledgement(
    UdpStartResult Result,
    byte RankRestrictedServerFlag,
    int DailyLoginRewardPoints,
    string ChannelName,
    int ReservedValueAfterChannelNameOne,
    int ReservedValueAfterChannelNameTwo,
    int ChannelRestrictionLevel,
    float ChannelRestrictionKdr,
    uint ClientRequestContextValue,
    NetCafeBootstrapInfo? NetCafeInfo = null);

/// <summary>
/// Optional 144 net-café tail. Its presence flag initializes the native
/// sNetCafeInfo object. Four individual byte meanings and eight slot-value
/// domains are not yet recoverable, so their wire position is retained in the
/// names rather than assigned an unproven business meaning.
/// </summary>
public sealed record NetCafeBootstrapInfo(
    byte FirstWireByte,
    byte SecondWireByte,
    byte ThirdWireByte,
    byte FourthWireByte,
    IReadOnlyList<int> SlotValues)
{
    /// <summary>Number of raw four-byte values consumed by sub_555D50.</summary>
    public const int SlotValueCount = 8;
}
