// =============================================================================
// PM_CONNECT_ACK (142) exact writer contract and packed calendar codec.
//
// Evidence: sub_556530 builds the empty 141 request; sub_5565D0 reads 142;
// sub_534E80 / sub_534F20 encode and decode its packed calendar. No timezone
// or endpoint-admission policy is carried by this contract.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class ChannelBootstrapWire
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

}

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
