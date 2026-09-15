// =============================================================================
// GL_NEW_MSG_COUNT_REQ (783) → GL_NEW_MSG_COUNT_ACK (784)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // 783 (空) → 784 (sub_564480): s32 未讀數 → dword_F0C104 →
    // UI vtbl+72(count!=0) 信箱紅點 (卅六輪)
    private static async ValueTask GL_NEW_MSG_COUNT_REQ(Session session, Packet packet, ServerContext context)
    {
        int unread = session.UserId != 0
            ? context.Db.CountUnreadMessages(session.UserId)
            : 0;

        await session.SendAsync(new Packet(Opcode.GL_NEW_MSG_COUNT_ACK)
            .WriteS32(unread));
    }
}
