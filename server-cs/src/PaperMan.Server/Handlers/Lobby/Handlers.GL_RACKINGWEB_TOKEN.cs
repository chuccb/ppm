// =============================================================================
// GL_RACKINGWEB_TOKEN_REQ (787) → GL_RACKINGWEB_TOKEN_ACK (788)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 787 GL_RACKINGWEB_TOKEN_REQ (sub_581E40, 空) → 788 ACK (sub_44BEA0): str token
    private static async ValueTask GL_RACKINGWEB_TOKEN_REQ(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_RACKINGWEB_TOKEN_ACK).WriteStr("RANKING_TOKEN_OK"));
    }

}
