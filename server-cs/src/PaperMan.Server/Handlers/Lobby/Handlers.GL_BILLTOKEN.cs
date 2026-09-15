// =============================================================================
// GL_BILLTOKEN_REQ (706) → GL_BILLTOKEN_ACK (707)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 706 GL_BILLTOKEN_REQ (sub_460480, 空) → 707 ACK (sub_46AD00 case 707): str token
    private static async ValueTask GL_BILLTOKEN_REQ(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_BILLTOKEN_ACK).WriteStr("TOKEN_PAPERMAN_OK"));
    }

}
