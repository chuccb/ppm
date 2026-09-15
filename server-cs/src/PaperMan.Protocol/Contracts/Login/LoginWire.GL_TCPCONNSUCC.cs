// =============================================================================
// GL_TCPCONNSUCC (693) channel-listener greeting writer.
//
// Evidence: sub_57CAE0 sends the empty greeting before the channel bootstrap
// exchange. This file intentionally contains no channel session policy.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class LoginWire
{
    /// <summary>Constructs the empty channel-listener greeting, GL_TCPCONNSUCC(693).</summary>
    public static Packet CreateTcpConnectionSuccess() =>
        new(Opcode.GL_TCPCONNSUCC);

}
