// =============================================================================
// 好友 handlers — GL_FRIEND 家族 (九輪逐行讀畢, docs/PACKETS.md §3.15c):
//
//   429 ADD_REQ:  str nick → 430 ACK (sub_55AA90): u8 result, str nick
//                 result: 0=成功, 1..4 = 重複/不存在/滿/對方拒
//   431 DEL_REQ:  str nick → 432 ACK (sub_55AE10): u8 result(0/1/2), str nick
//   433 LIST_REQ: 空       → 434 ACK (sub_55AFC0): u16 x, str self,
//                 u8 count, count×{str nick, s32 status}
//   435 INFO_REQ: str nick → 436 ACK (sub_55B2C0): u8 count,
//                 count×{str nick, u8 online, [online: str where, u8 ch]}
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class FriendHandlers
{
    /// <summary>430 的 result 碼 (sub_55AA90 的 if-chain)。</summary>
    private enum AddResult : byte
    {
        Ok = 0,
        AlreadyFriend = 1,
        NotFound = 2,
        ListFull = 3,
        Refused = 4,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.GL_FRIEND_ADD_REQ, Add);
        add(Opcode.GL_FRIEND_DEL_REQ, Delete);
        add(Opcode.GL_FRIEND_LIST_REQ, List);
        add(Opcode.GL_FRIEND_INFO_REQ, Info);
        add(Opcode.GL_MSG_ADD_REQ, MsgSend);
        add(Opcode.GL_MSG_RECVLIST_REQ, MsgList);
        add(Opcode.GL_MSG_DEL_REQ, MsgDelete);
    }

    // REQ(419): s32, str to, str title, str body, str, u16 date, u8
    // ACK(420) sub_559810: str to_nick, u8, u8 result
    //   (0=成功 1=拒收 2=信箱滿 — 九輪逐分支)
    private static async ValueTask MsgSend(Session session, Packet packet, ServerContext context)
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

    // REQ(425): s32 page → ACK(426) sub_55A630:
    //   u16 x, str self, u8 count, count×{str from, u8, str title,
    //   u32 msg_id, str body(≤201), str, u16 date}
    private static async ValueTask MsgList(Session session, Packet packet, ServerContext context)
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

    // REQ(421): str msg_key → ACK(422) sub_55A310: u8 ok, str key
    private static async ValueTask MsgDelete(Session session, Packet packet, ServerContext context)
    {
        var key = packet.ReadStr();
        bool ok = session.UserId != 0
            && long.TryParse(key, out long msgId)
            && context.Db.DeleteMessage(session.UserId, msgId);

        await session.SendAsync(new Packet(Opcode.GL_MSG_DEL_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0)
            .WriteStr(key));
    }

    private static async ValueTask Add(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        var result = session.UserId switch
        {
            0 => AddResult.Refused,
            _ => context.Db.AddFriend(session.UserId, nick) switch
            {
                Db.FriendAdd.Ok => AddResult.Ok,
                Db.FriendAdd.Duplicate => AddResult.AlreadyFriend,
                Db.FriendAdd.NotFound => AddResult.NotFound,
                _ => AddResult.ListFull,
            },
        };

        await session.SendAsync(new Packet(Opcode.GL_FRIEND_ADD_ACK)
            .WriteU8((byte)result)
            .WriteStr(nick));
    }

    private static async ValueTask Delete(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        bool ok = session.UserId != 0 && context.Db.DeleteFriend(session.UserId, nick);

        await session.SendAsync(new Packet(Opcode.GL_FRIEND_DEL_ACK)
            .WriteU8(ok ? (byte)0 : (byte)1)
            .WriteStr(nick));
    }

    // 434 (sub_55AFC0): u16 x, str self, u8 count, count×{str nick, s32 status}
    private static async ValueTask List(Session session, Packet packet, ServerContext context)
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

    // 436 (sub_55B2C0): u8 count, count×{str nick, u8 online,
    //   [online==1: str where, u8 channel]}
    private static async ValueTask Info(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        // 單機伺服器: 查詢對象一律回「離線」(online=0 → 不帶 where/ch)
        await session.SendAsync(new Packet(Opcode.GL_FRIEND_INFO_ACK)
            .WriteU8(1)
            .WriteStr(nick)
            .WriteU8(0));
    }
}
