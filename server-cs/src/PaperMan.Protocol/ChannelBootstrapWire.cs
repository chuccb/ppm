// =============================================================================
// Channel-bootstrap wire contracts.
//
// Evidence:
//   * PM_CONNECT_REQ(141) builder: sub_556530
//   * PM_CONNECT_ACK(142) reader:  sub_5565D0
//   * PM_UDPSTART_ACK(144) reader: sub_555D50
//   * GC_ENTERCHANNEL_ACK(196) reader: CLobbyChannel::sub_4179D0
//   * packed calendar codec:        sub_534E80 / sub_534F20
//
// The source does not establish a backend-domain name for every 144 value.
// Those values intentionally retain explicit wire-oriented names below instead
// of being guessed as account IDs, endpoint IDs, or credentials.
// =============================================================================
namespace PaperMan.Protocol;

/// <summary>Wire contracts used after a channel TCP connection has sent 143.</summary>
public static class ChannelBootstrapWire
{
    /// <summary>
    /// Serializes PM_CONNECT_ACK(142). The original client sends an empty 141,
    /// then consumes an ANSI endpoint, a raw four-byte port field (using its
    /// low u16 at connect time), its selected channel index, and a packed
    /// calendar timestamp.
    /// </summary>
    public static Packet CreatePmConnectAcknowledgement(
        ChannelEndpoint endpoint,
        byte channelIndex,
        PmConnectCalendarTime calendarTime)
    {
        ValidateEndpoint(endpoint, "142 endpoint");

        return new Packet(Opcode.PM_CONNECT_ACK)
            .WriteStr(endpoint.Host)
            .WriteS32(endpoint.Port)
            .WriteU8(channelIndex)
            .WriteU32(calendarTime.ToWireValue());
    }

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

    private static void ValidateEndpoint(ChannelEndpoint endpoint, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        LoginWire.RequireAnsiString(endpoint.Host, LoginWire.MaxUdpHostBytes, fieldName);
        if (endpoint.Port is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endpoint),
                $"{fieldName} port must be within the low unsigned 16-bit range (1..65535).");
        }
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
/// A 142/196 network endpoint. The wire port is four bytes, but the native
/// connection functions consume its lower unsigned 16 bits.
/// </summary>
public sealed record ChannelEndpoint(string Host, int Port);

/// <summary>
/// PM_CONNECT_ACK(142)'s bit-packed local calendar value. The original decoder
/// writes year/month/day/hour/minute to a SYSTEMTIME-like WORD buffer:
/// <c>year = bits 24..31 + 2000</c>, <c>month = bits 19..23</c>,
/// <c>day = bits 13..18</c>, <c>hour = bits 7..12</c>, <c>minute = bits 0..6</c>.
/// No timezone is carried on the wire.
/// </summary>
public readonly record struct PmConnectCalendarTime(
    ushort Year,
    byte Month,
    byte Day,
    byte Hour,
    byte Minute)
{
    /// <summary>Builds the wire calendar fields from the supplied wall-clock offset.</summary>
    public static PmConnectCalendarTime From(DateTimeOffset timestamp) =>
        new(
            checked((ushort)timestamp.Year),
            checked((byte)timestamp.Month),
            checked((byte)timestamp.Day),
            checked((byte)timestamp.Hour),
            checked((byte)timestamp.Minute));

    /// <summary>Encodes the exact sub_534E80 inverse representation.</summary>
    public uint ToWireValue()
    {
        if (Year is < 2000 or > 2255)
        {
            throw new ArgumentOutOfRangeException(nameof(Year), "142 packed calendar year must be in 2000..2255.");
        }

        if (Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(Month), "142 packed calendar month must be in 1..12.");
        }

        int daysInMonth = DateTime.DaysInMonth(Year, Month);
        if (Day is < 1 || Day > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(nameof(Day), "142 packed calendar day is invalid for its year and month.");
        }

        if (Hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(Hour), "142 packed calendar hour must be in 0..23.");
        }

        if (Minute > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(Minute), "142 packed calendar minute must be in 0..59.");
        }

        return ((uint)(Year - 2000) << 24)
            | ((uint)Month << 19)
            | ((uint)Day << 13)
            | ((uint)Hour << 7)
            | Minute;
    }

    /// <summary>Decodes and validates a packed 142 calendar value for diagnostics and tests.</summary>
    public static PmConnectCalendarTime FromWireValue(uint wireValue)
    {
        var calendarTime = new PmConnectCalendarTime(
            checked((ushort)((wireValue >> 24) + 2000)),
            checked((byte)((wireValue >> 19) & 0x1F)),
            checked((byte)((wireValue >> 13) & 0x3F)),
            checked((byte)((wireValue >> 7) & 0x3F)),
            checked((byte)(wireValue & 0x7F)));
        calendarTime.ToWireValue();
        return calendarTime;
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
