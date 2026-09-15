// =============================================================================
// GL_LEVEL_KILL_LIMIT_REQ (704) → GL_LEVEL_KILL_LIMIT_ACK (705)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 704 GL_LEVEL_KILL_LIMIT_REQ (sub_582570, 空)
    // → 705 ACK (sub_55C9B0): s32 kill_limit, f32 exp_rate, s32 max_level_limit
    private static async ValueTask GL_LEVEL_KILL_LIMIT_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GL_LEVEL_KILL_LIMIT_ACK)
            .WriteS32(50)                                   // 殺敵上限 50
            .WriteF32(1.0f)                                 // 經驗倍率 1.0
            .WriteS32(30);                                  // 最大等級限制 30

        await session.SendAsync(ack);
    }

}
