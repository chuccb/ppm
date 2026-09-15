// =============================================================================
// GL_MSG_ADD_REQ (419) → GL_MSG_ADD_ACK (420)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // REQ(419): s32, str to, str title, str body, str, u16 date, u8
    // ACK(420) sub_559810: str to_nick, u8, u8 result
    //   (0=成功 1=拒收 2=信箱滿 — 九輪逐分支)
    private static async ValueTask GL_MSG_ADD_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadS32();
        var to = packet.ReadStr();
        var title = packet.ReadStr();
        var body = packet.ReadStr();

        byte result = session.UserId != 0 && context.Db.SendMessage(session.UserId, to, title, body)
            ? (byte)0
            : (byte)1;

        await session.SendAsync(new Packet(Opcode.GL_MSG_ADD_ACK)
            .WriteStr(to)
            .WriteU8(0)
            .WriteU8(result));
    }
}
