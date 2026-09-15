// =============================================================================
// GL_FRIEND_ADD_REQ (429) → GL_FRIEND_ADD_ACK (430)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    /// <summary>430 的 result 碼 (sub_55AA90 的 if-chain)。</summary>
    private enum GL_FRIEND_ADD_ACK_Result : byte
    {
        Ok = 0,
        AlreadyFriend = 1,
        NotFound = 2,
        ListFull = 3,
        Refused = 4,
    }

    private static async ValueTask GL_FRIEND_ADD_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        var result = session.UserId switch
        {
            0 => GL_FRIEND_ADD_ACK_Result.Refused,
            _ => context.Db.AddFriend(session.UserId, nick) switch
            {
                Db.FriendAdd.Ok => GL_FRIEND_ADD_ACK_Result.Ok,
                Db.FriendAdd.Duplicate => GL_FRIEND_ADD_ACK_Result.AlreadyFriend,
                Db.FriendAdd.NotFound => GL_FRIEND_ADD_ACK_Result.NotFound,
                _ => GL_FRIEND_ADD_ACK_Result.ListFull,
            },
        };

        await session.SendAsync(new Packet(Opcode.GL_FRIEND_ADD_ACK)
            .WriteU8((byte)result)
            .WriteStr(nick));
    }
}
