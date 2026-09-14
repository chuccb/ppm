// =============================================================================
// Login / channel-bootstrap wire contracts.
//
// Evidence:
//   * GL_LOGIN_REQ(682) builder: sub_43DF00
//   * GL_LOGIN_ACK(681) reader:  CLobbyLogin::sub_43E500
//   * account greeting 694:      CLobbyLogin::sub_43E500 → sub_43DF00
//   * channel greeting 693:      sub_57CAE0 → sub_555C60 (143)
//
// The native 681 reader is not a generic list decoder.  It reads exactly three
// channel groups per server, and for a non-empty group it consumes exactly one
// channel record even if the transmitted count is larger.  The models below
// deliberately make that safe subset (zero or one entry per group) the only
// representable form.
// =============================================================================
using System.Text;

namespace PaperMan.Protocol;

/// <summary>Exact packet contracts used before the lobby/channel session begins.</summary>
public static class LoginWire
{
    private const int LoginRequestFixedTailBytes = sizeof(ulong) + sizeof(byte) + 24;

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

    /// <summary>
    /// Decodes the u64 emitted by sub_43DF00. The low dword is fixed to
    /// <c>0x0000000E</c>; the high dword is the value loaded from
    /// <c>datarevision.txt</c>, XORed with <c>0xB1A9D7C7</c>. It is a client
    /// content revision guard, not a hardware identifier; the hardware-related
    /// client material is the separate raw 24-byte fingerprint.
    /// </summary>
    public static bool TryDecodeDataRevision(ulong obfuscatedDataRevision, out uint dataRevision)
    {
        dataRevision = (uint)(obfuscatedDataRevision >> 32) ^ 0xB1A9D7C7u;
        return (uint)obfuscatedDataRevision == 0x0000000Eu;
    }

    /// <summary>
    /// Constructs GL_ACCOUNTCONNSUCC(694). The native reader applies only a
    /// value strictly below 0x2580; zero is normalized to the original 0x2580
    /// no-compression default, and larger values are rejected to keep the peer
    /// codec thresholds identical.
    /// </summary>
    public static Packet CreateAccountConnectionSuccess(ushort compressionThreshold)
    {
        if (compressionThreshold > PacketCodec.NeverCompress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionThreshold),
                "The native 694 negotiation supports only 0 or 1..0x2580.");
        }

        ushort effectiveThreshold = compressionThreshold == 0
            ? PacketCodec.NeverCompress
            : compressionThreshold;
        return new Packet(Opcode.GL_ACCOUNTCONNSUCC).WriteU16(effectiveThreshold);
    }

    /// <summary>Constructs the empty channel-listener greeting, GL_TCPCONNSUCC(693).</summary>
    public static Packet CreateTcpConnectionSuccess() =>
        new(Opcode.GL_TCPCONNSUCC);

    /// <summary>
    /// Serializes GL_LOGIN_ACK(681).  A failure is exactly one signed 32-bit
    /// result word.  A successful acknowledgement appends its complete native
    /// reader contract.
    /// </summary>
    public static Packet CreateAcknowledgement(LoginAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);

        var packet = new Packet(Opcode.GL_LOGIN_ACK).WriteS32(acknowledgement.ResultCode);
        if (acknowledgement.ResultCode != 1)
        {
            return packet;
        }

        var success = acknowledgement.Success
            ?? throw new ArgumentException("A successful GL_LOGIN_ACK requires success data.", nameof(acknowledgement));
        ValidateSuccess(success);

        packet.WriteS32(success.UserId)
              .WriteS32(success.BillingUiMode);

        if (success.FeatureExtension is { } extension)
        {
            packet.WriteS32(1)
                  .WriteS32(extension.FirstValue)
                  .WriteS32(extension.SecondValue)
                  .WriteU8(extension.FeatureFlag);
        }
        else
        {
            // Count zero is semantic: there is no extension tuple to emit.
            packet.WriteS32(0);
        }

        packet.WriteS16((short)success.Servers.Count);
        foreach (var server in success.Servers)
        {
            packet.WriteS16(server.Id)
                  .WriteStr(server.Name)
                  .WriteStr(server.Host)
                  // Native reads this with sub_5929C0 (s16), but immediately
                  // uses the bit pattern as a Winsock u_short port.
                  .WriteS16(unchecked((short)server.Port))
                  .WriteU8(server.ListingFlag)
                  .WriteS16(server.Group);

            foreach (var channel in server.ChannelGroups)
            {
                if (channel is null)
                {
                    packet.WriteS16(0);
                    continue;
                }

                packet.WriteS16(1)
                      .WriteU8(channel.Type)
                      .WriteStr(channel.Name)
                      .WriteS16(unchecked((short)channel.Port))
                      .WriteU8(channel.ListingFlag);

                if (channel.Type == 3)
                {
                    packet.WriteU8(channel.TypeThreeExtension!.Value);
                }
            }
        }

        // The native 681 reader uses sub_592A40 for these two s32 words and
        // passes them to its Tricod account/billing client. Their business
        // semantics remain unknown, but signed width and order are confirmed.
        return packet.WriteS32(success.Billing.FirstValue).WriteS32(success.Billing.SecondValue);
    }

    private static void ValidateSuccess(LoginAcknowledgementSuccess success)
    {
        ArgumentNullException.ThrowIfNull(success.Servers);

        // sub_555C60 later narrows native 681 n100 to a signed byte before
        // widening it into 143. Keep the advertised value lossless across the
        // mandatory login-to-channel handoff instead of silently changing it.
        if (success.BillingUiMode is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(success),
                "681 billing UI mode must fit the signed-byte 143 echo path.");
        }

        if (success.Servers.Count > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(success), "681 server count exceeds signed 16-bit range.");
        }

        foreach (var server in success.Servers)
        {
            ArgumentNullException.ThrowIfNull(server);
            RequireAnsiString(server.Name, MaxServerOrChannelNameBytes, "681 server name");
            RequireAnsiString(server.Host, MaxLoginServerHostBytes, "681 server host");

            if (server.ChannelGroups is null || server.ChannelGroups.Count != 3)
            {
                throw new ArgumentException("Every 681 server must contain exactly three channel groups.", nameof(success));
            }

            foreach (var channel in server.ChannelGroups)
            {
                if (channel is null)
                {
                    continue;
                }

                RequireAnsiString(channel.Name, MaxServerOrChannelNameBytes, "681 channel name");
                if ((channel.Type == 3) != channel.TypeThreeExtension.HasValue)
                {
                    throw new ArgumentException(
                        "A type-3 channel requires exactly one trailing extension byte; other types require none.",
                        nameof(success));
                }
            }
        }
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

/// <summary>GL_LOGIN_ACK(681) result plus the success-only tail.</summary>
public sealed record LoginAcknowledgement(int ResultCode, LoginAcknowledgementSuccess? Success = null);

/// <summary>Success-only tail of GL_LOGIN_ACK(681).</summary>
public sealed record LoginAcknowledgementSuccess(
    int UserId,
    int BillingUiMode,
    LoginFeatureExtension? FeatureExtension,
    IReadOnlyList<LoginServerEntry> Servers,
    LoginBillingMetadata Billing);

/// <summary>
/// The one extension tuple the native 681 reader supports safely.  A non-null
/// tuple writes ext_count=1; no tuple writes ext_count=0.  The client uses the
/// count as a net-café/account-feature gate, while the exact two integers and
/// final raw <c>u8</c> flag are not identified by the available native code.
/// </summary>
public readonly record struct LoginFeatureExtension(int FirstValue, int SecondValue, byte FeatureFlag);

/// <summary>One server selector entry in GL_LOGIN_ACK(681).</summary>
public sealed record LoginServerEntry(
    short Id,
    string Name,
    string Host,
    ushort Port,
    byte ListingFlag,
    short Group,
    IReadOnlyList<LoginChannelEntry?> ChannelGroups);

/// <summary>One selectable channel in a 681 channel group.</summary>
public sealed record LoginChannelEntry(
    byte Type,
    string Name,
    ushort Port,
    byte ListingFlag,
    byte? TypeThreeExtension = null);

/// <summary>Confirmed wire container for the final two s32 words of GL_LOGIN_ACK.</summary>
public readonly record struct LoginBillingMetadata(int FirstValue, int SecondValue)
{
    public static LoginBillingMetadata None => new(0, 0);
}
