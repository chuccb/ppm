// =============================================================================
// GL_LOGIN_ACK (681) exact writer contract.
//
// Evidence: CLobbyLogin::sub_43E500. The native reader has a special three
// channel-group shape, so this model represents only its safe 0/1 entry form.
// It does not establish authentication, billing, or channel-admission policy.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class LoginWire
{
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
                  // sub_58AD90 passes this raw2 field to sub_554810 as a
                  // Winsock u_short TCP endpoint port.
                  .WriteU16(server.Port)
                  .WriteU8(server.ListingFlag)
                  .WriteS16(server.Group);

            foreach (var channelGroup in server.ChannelGroups)
            {
                packet.WriteS16(channelGroup.MaxUsers);
                var channel = channelGroup.Channel;
                if (channel is null)
                {
                    continue;
                }

                packet.WriteU8(channel.Type)
                      .WriteStr(channel.Name)
                      // Native renders this field as the USERS numerator;
                      // it is not a second endpoint port.
                      .WriteS16(channel.CurrentUsers)
                      .WriteU8(channel.ListingFlag);

                if (channel.Type == 3)
                {
                    byte typeThreeExtension = channel.TypeThreeExtension
                        ?? throw new InvalidOperationException(
                            "A validated type-3 channel must have its extension byte.");
                    packet.WriteU8(typeThreeExtension);
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

            foreach (var channelGroup in server.ChannelGroups)
            {
                if (channelGroup.MaxUsers < 0 || (channelGroup.MaxUsers == 0) != (channelGroup.Channel is null))
                {
                    throw new ArgumentException(
                        "A 681 group needs one channel exactly when max users is positive.",
                        nameof(success));
                }

                var channel = channelGroup.Channel;
                if (channel is null)
                {
                    continue;
                }

                if (channel.CurrentUsers < 0)
                {
                    throw new ArgumentException("681 current users must be non-negative.", nameof(success));
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
    IReadOnlyList<LoginChannelGroup> ChannelGroups);

/// <summary>
/// One of the three fixed 681 groups. The native positive gate is the
/// max-users/USERS denominator, not a count of channel records.
/// </summary>
public sealed record LoginChannelGroup(short MaxUsers, LoginChannelEntry? Channel);

/// <summary>One selectable channel in a 681 channel group.</summary>
public sealed record LoginChannelEntry(
    byte Type,
    string Name,
    short CurrentUsers,
    byte ListingFlag,
    byte? TypeThreeExtension = null);

/// <summary>Confirmed wire container for the final two s32 words of GL_LOGIN_ACK.</summary>
public readonly record struct LoginBillingMetadata(int FirstValue, int SecondValue)
{
    public static LoginBillingMetadata None => new(0, 0);
}
