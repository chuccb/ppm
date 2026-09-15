// =============================================================================
// GL_FRIEND_LIST_REQ (433) → GL_FRIEND_LIST_ACK (434)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // 434 (sub_55AFC0): u16 x, str self, u8 count, count×{str nick, s32 status}
    private static async ValueTask GL_FRIEND_LIST_REQ(Session session, Packet packet, ServerContext context)
    {
        var friends = session.UserId != 0
            ? context.Db.GetFriends(session.UserId)
            : [];

        var ack = new Packet(Opcode.GL_FRIEND_LIST_ACK)
            .WriteU16(0)
            .WriteStr(session.Nickname)
            .WriteU8((byte)Math.Min(friends.Count, 255));

        foreach (var (nick, status) in friends.Take(255))
        {
            ack.WriteStr(nick)
               .WriteS32(status);
        }

        await session.SendAsync(ack);
    }
}
