// =============================================================================
// GL_TUTORIAL_INDEX_SET_REQ (689) → GL_TUTORIAL_INDEX_SET_ACK (690)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 689 GL_TUTORIAL_INDEX_SET_REQ (sub_55C7D0: s32 index) → 690 ACK (sub_582530): s32 index
    private static async ValueTask GL_TUTORIAL_INDEX_SET_REQ(Session session, Packet packet, ServerContext context)
    {
        int index = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        if (session.UserId != 0)
        {
            context.Db.SetTutorialIndex(session.UserId, index);
        }

        await session.SendAsync(new Packet(Opcode.GL_TUTORIAL_INDEX_SET_ACK).WriteS32(index));
    }
}
