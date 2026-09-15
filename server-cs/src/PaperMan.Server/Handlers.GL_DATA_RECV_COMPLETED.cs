// =============================================================================
// GL_DATA_RECV_COMPLETED_REQ (834) → GL_DATA_RECV_COMPLETED_ACK (835)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 834 GL_DATA_RECV_COMPLETED_REQ (sub_583120: s32 uid) → 835 ACK (sub_5831D0): 空包
    private static async ValueTask GL_DATA_RECV_COMPLETED_REQ(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_DATA_RECV_COMPLETED_ACK));
    }

}
