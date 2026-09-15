// =============================================================================
// GL_TUTORIALINDEX_REQ (685) → GL_TUTORIALINDEX_ACK (686)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 685 GL_TUTORIALINDEX_REQ (sub_55C6F0, 空) → 686 ACK (sub_55C790): s32 index
    private static async ValueTask GL_TUTORIALINDEX_REQ(Session session, Packet packet, ServerContext context)
    {
        int index = session.UserId != 0 ? context.Db.GetTutorialIndex(session.UserId) : 0;
        await session.SendAsync(new Packet(Opcode.GL_TUTORIALINDEX_ACK).WriteS32(index));
    }
}
