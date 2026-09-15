// =============================================================================
// GL_MSG_READ_REQ (423) → GL_MSG_READ_ACK (424)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // REQ(423) sub_55A3C0: str msg_key → ACK(424) sub_55A4F0: u8 ok, str key
    private static async ValueTask GL_MSG_READ_REQ(Session session, Packet packet, ServerContext context)
    {
        var key = packet.ReadStr();
        bool ok = session.UserId != 0
            && long.TryParse(key, out long msgId)
            && context.Db.MarkMessageRead(session.UserId, msgId);

        await session.SendAsync(new Packet(Opcode.GL_MSG_READ_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0)
            .WriteStr(key));
    }
}
