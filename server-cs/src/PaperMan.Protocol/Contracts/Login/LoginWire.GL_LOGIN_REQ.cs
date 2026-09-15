// =============================================================================
// GL_LOGIN_REQ (682) exact reader contract.
//
// Evidence: sub_43DF00 builds 682; CLobbyLogin::sub_43E500 consumes its
// corresponding login flow. DataRevisionGuard/Mask are the 682 u64 layout,
// not an account or hardware-identity rule.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class LoginWire
{
    private const int LoginRequestFixedTailBytes = sizeof(ulong) + sizeof(byte) + 24;

    /// <summary>GL_LOGIN_REQ(682): two ANSI strings, a packed u64, u8 state, raw[24].</summary>
    public static LoginRequest ReadRequest(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        // sub_5926F0 writes both strings by calling lstrlenA, so neither has
        // an independent wire length prefix. The native login UI does copy
        // saved edit text with wcsncpy(..., 0x18), but that is not a proven
        // packet-level byte bound. Enforce only the exact packet capacity and
        // reserve enough bytes for every following required field.
        var accountName = ReadStringLeaving(packet, minimumFollowingBytes: 1 + LoginRequestFixedTailBytes);
        var passwordOrToken = ReadStringLeaving(packet, minimumFollowingBytes: LoginRequestFixedTailBytes);
        ulong obfuscatedDataRevision = packet.ReadU64();
        var fingerprintSource = (LoginFingerprintSource)packet.ReadU8();
        byte[] fingerprint = packet.ReadRaw(24).ToArray();

        if (packet.Remaining != 0)
        {
            throw new InvalidDataException($"GL_LOGIN_REQ has {packet.Remaining} unexpected trailing byte(s).");
        }

        return new LoginRequest(accountName, passwordOrToken, obfuscatedDataRevision, fingerprintSource, fingerprint);
    }

    // sub_43DF00 writes this as the first dword passed to sub_592AE0. Although
    // the decompiler displays that helper's first value as a char, sub_592AE0
    // copies eight contiguous bytes from its stack address. A native 682 frame
    // confirms this complete dword, not merely its low byte (0x0E).
    private const uint DataRevisionGuard = 0xF1E1AB0Eu;
    private const uint DataRevisionMask = 0xB1A9D7C7u;

    /// <summary>
    /// Decodes the u64 emitted by sub_43DF00. The low dword is the fixed
    /// <c>0xF1E1AB0E</c> guard; the high dword is the value loaded from
    /// <c>datarevision.txt</c>, XORed with <c>0xB1A9D7C7</c>. It is a client
    /// content revision guard, not a hardware identifier; the hardware-related
    /// client material is the separate raw 24-byte fingerprint.
    /// </summary>
    public static bool TryDecodeDataRevision(ulong obfuscatedDataRevision, out uint dataRevision)
    {
        dataRevision = (uint)(obfuscatedDataRevision >> 32) ^ DataRevisionMask;
        return (uint)obfuscatedDataRevision == DataRevisionGuard;
    }

    private static string ReadStringLeaving(Packet packet, int minimumFollowingBytes)
    {
        if (packet.Remaining <= minimumFollowingBytes)
        {
            throw new EndOfStreamException(
                $"GL_LOGIN_REQ needs a NUL-terminated string plus {minimumFollowingBytes} following byte(s).");
        }

        // One byte is required for this string's NUL terminator. The remainder
        // is bounded by Packet.MaxPayload rather than a made-up UI byte limit.
        int maxContentBytes = packet.Remaining - minimumFollowingBytes - 1;
        return packet.ReadNulTerminatedAnsiString(maxContentBytes);
    }
}

/// <summary>Decoded GL_LOGIN_REQ(682). <see cref="Fingerprint"/> is always 24 bytes.</summary>
public sealed record LoginRequest(
    string AccountName,
    string PasswordOrToken,
    ulong ObfuscatedDataRevision,
    LoginFingerprintSource FingerprintSource,
    byte[] Fingerprint);

/// <summary>How sub_43DF00 sourced the raw 24-byte fingerprint.</summary>
public enum LoginFingerprintSource : byte
{
    Unavailable = 0,
    AdapterMacAddress = 1,
    StorageSerial = 2,
}
