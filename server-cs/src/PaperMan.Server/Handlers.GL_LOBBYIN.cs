// =============================================================================
// GL_LOBBYIN_REQ (250)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 250 is an exact-empty local transition notice. `sub_574080` advances
    // the client state itself; no 251 consumer was recovered.
    private static ValueTask GL_LOBBYIN_REQ(Session session, Packet packet, ServerContext context)
    {
        return ValueTask.CompletedTask;
    }
}
