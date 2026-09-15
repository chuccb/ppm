// =============================================================================
// GL_ACCOUNTCONNSUCC (694) connection-greeting writer.
//
// Evidence: CLobbyLogin::sub_43E500 applies the received compression threshold
// before the account/login exchange. Threshold normalization is wire-peer
// compatibility, not compression policy.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class LoginWire
{
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

}
