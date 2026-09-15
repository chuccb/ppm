// =============================================================================
// GL_MSG_RECVLIST_REQ (425) → GL_MSG_RECVLIST_ACK (426)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // REQ(425): s32 page → ACK(426) sub_55A630:
    //   u16 x, str self, u8 count, count×{str from, u8, str title,
    //   u32 msg_id, str body(≤201), str, u16 date}
    private static async ValueTask GL_MSG_RECVLIST_REQ(Session session, Packet packet, ServerContext context)
    {
        var messages = session.UserId != 0
            ? context.Db.GetMessages(session.UserId)
            : [];

        var ack = new Packet(Opcode.GL_MSG_RECVLIST_ACK)
            .WriteU16(0)
            .WriteStr(session.Nickname)
            .WriteU8((byte)Math.Min(messages.Count, 50));

        foreach (var m in messages.Take(50))
        {
            ack.WriteStr(m.From)
               .WriteU8(m.IsRead ? (byte)1 : (byte)0)
               .WriteStr(m.Title)
               .WriteU32((uint)m.MsgId)
               .WriteStr(m.Body.Length > 200 ? m.Body[..200] : m.Body)
               .WriteStr("")
               .WriteU16(m.DateCode);
        }

        await session.SendAsync(ack);
    }
}
