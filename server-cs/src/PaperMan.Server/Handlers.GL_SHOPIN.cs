// =============================================================================
// GL_SHOPIN_REQ (252) → GL_SHOPIN_ACK (253)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 252 is also exactly empty. `sub_574120` sends it and immediately puts
    // the client in shop state 3. There is no recovered native 253 consumer,
    // but the project explicitly permits the empty 253 interoperability ACK.
    // Do not attach catalog, account, or entitlement data to this ack.
    private static ValueTask GL_SHOPIN_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GL_SHOPIN_ACK));
    }
}
