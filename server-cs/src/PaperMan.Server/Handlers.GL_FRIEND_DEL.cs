// =============================================================================
// GL_FRIEND_DEL_REQ (431) → GL_FRIEND_DEL_ACK (432)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    private static async ValueTask GL_FRIEND_DEL_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        bool ok = session.UserId != 0 && context.Db.DeleteFriend(session.UserId, nick);

        await session.SendAsync(new Packet(Opcode.GL_FRIEND_DEL_ACK)
            .WriteU8(ok ? (byte)0 : (byte)1)
            .WriteStr(nick));
    }
}
