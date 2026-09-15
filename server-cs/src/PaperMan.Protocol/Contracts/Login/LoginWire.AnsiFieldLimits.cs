// =============================================================================
// Shared ANSI constraints for independently evidenced bootstrap packet grammars.
//
// The constants identify native fixed reader buffers. They are deliberately a
// protocol primitive rather than server validation policy: callers choose the
// packet-specific bound that their exact reader proves.
// =============================================================================
using System.Text;

namespace PaperMan.Protocol;

public static partial class LoginWire
{
    /// <summary>
    /// <c>String[24]</c> is written by the native client into PM_UDPSTART(143).
    /// The NUL terminator occupies one byte, leaving at most 23 ANSI bytes.
    /// </summary>
    public const int MaxChannelIdentityBytes = 23;

    /// <summary>Native 681 reader uses <c>char name[50]</c>.</summary>
    public const int MaxServerOrChannelNameBytes = 49;

    /// <summary>Native 681 reader uses <c>char host[16]</c>.</summary>
    public const int MaxLoginServerHostBytes = 15;

    /// <summary>Native 144 reader uses <c>char channelName[40]</c>.</summary>
    public const int MaxUdpStartChannelNameBytes = 39;

    /// <summary>Native 196 reader uses <c>char udpHost[20]</c>.</summary>
    public const int MaxUdpHostBytes = 19;

    /// <summary>Validates an ANSI field which the native reader stores in a fixed buffer.</summary>
    public static void RequireAnsiString(string value, int maxContentBytes, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxContentBytes);

        if (value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException($"{fieldName} cannot contain a NUL character.", fieldName);
        }

        int byteCount;
        try
        {
            var strictAnsi = Encoding.GetEncoding(
                Packet.Ansi.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
            byteCount = strictAnsi.GetByteCount(value);
        }
        catch (EncoderFallbackException ex)
        {
            throw new ArgumentException($"{fieldName} cannot be represented in CP{Packet.Ansi.CodePage}.", fieldName, ex);
        }

        if (byteCount > maxContentBytes)
        {
            throw new ArgumentOutOfRangeException(
                fieldName,
                $"{fieldName} encodes to {byteCount} byte(s), exceeding the native {maxContentBytes}-byte content limit.");
        }
    }
}
