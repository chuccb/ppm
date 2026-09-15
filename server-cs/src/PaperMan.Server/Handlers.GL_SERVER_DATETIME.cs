// =============================================================================
// GL_SERVER_DATETIME_REQ (864) → GL_SERVER_DATETIME_ACK (865)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    // 865 (sub_585AB0): s32 unix_time — client 用來對時每日任務重置
    private static async ValueTask GL_SERVER_DATETIME_REQ(Session session, Packet packet, ServerContext context) =>
        await session.SendAsync(new Packet(Opcode.GL_SERVER_DATETIME_ACK)
            .WriteS32((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
}
