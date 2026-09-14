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
//   439 CHAT_REQ (sub_55B510): s32 uid(dword_F2A684 頁籤/頻道 id),
//                 str my_nick, str friend_nick, str message (≤180 才送)
//             → 440 ACK (sub_55B660): u8 status, str nick1, str nick2,
//                 [status==2: str comment] — 0=名稱不存在(0x1EF),
//                 1=離線(0x1F0), 2=訊息(0x1D9 ←%s さんのコメント), 3=找不到(0x1D8)
//   441 WHERE_REQ (sub_55B940): str nick
//             → 442 ACK (sub_55B9F0): u8 status; ==1 → u8 where_type,
//                 u8 channel, u8 room_no (11=教學 0x314, 9/10=大師/線上,
//                 其他=大廳 0x21E); ==2/0 → 0x21D 找不到資訊
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
        add(Opcode.GL_NEW_MSG_COUNT_REQ, NewMessageCount);
        add(Opcode.GL_FRIEND_CHAT_REQ, FriendChat);
        add(Opcode.GL_FRIEND_WHERE_REQ, FriendWhere);
    }

    // 783 (空) → 784 (sub_564480): s32 未讀數 → dword_F0C104 →
    // UI vtbl+72(count!=0) 信箱紅點 (卅六輪)
    private static async ValueTask NewMessageCount(Session session, Packet packet, ServerContext context)
    {
        int unread = session.UserId != 0
            ? context.Db.CountUnreadMessages(session.UserId)
            : 0;

        await session.SendAsync(new Packet(Opcode.GL_NEW_MSG_COUNT_ACK)
            .WriteS32(unread));
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

    // 439 GL_FRIEND_CHAT_REQ (sub_55B510): s32 uid + str my_nick +
    //   str friend_nick + str message — 1:1 好友聊天。uid 為 dword_F2A684
    //   (client 自 144 回帶的頁籤/頻道 id, 僅回帶不需判讀); my_nick 以
    //   server session 為準 (防冒名)。
    // → 440 (sub_55B660): status 2 = 訊息 (遞送 + 回聲, client 不本地顯示,
    //   故需回聲); 0 = 名稱不存在 / 1 = 離線 回給發話者。nick1/nick2 為
    //   發話者/收話者暱稱 (client 讀後僅推進游標, 顯示靠全域伙伴名 + comment)。
    private static async ValueTask FriendChat(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadS32();                                // dword_F2A684 回帶值
        _ = packet.ReadStr();                                // my_nick (以 session 為準)
        var friendNick = packet.ReadStr();
        var message = packet.ReadStr();

        if (session.Nickname.Length == 0 || friendNick.Length == 0 || message.Length == 0)
        {
            return;
        }

        // 名稱不存在 (0x1EF "%s というキャラクター名は存在しません")
        if (context.Db.GetMyInfoByNick(friendNick) is null)
        {
            await session.SendAsync(BuildChatAck(ChatStatus.NameNotFound, friendNick, session.Nickname));
            return;
        }

        // 在 DB 但離線 (0x1F0 "%s さんはオフラインです")
        var target = context.Sessions.Find(friendNick);
        if (target is null || ReferenceEquals(target, session))
        {
            await session.SendAsync(BuildChatAck(ChatStatus.Offline, friendNick, session.Nickname));
            return;
        }

        // 上線 → 遞送給好友並回聲給自己 (0x1D9 "← %s さんのコメント")
        var ack = BuildChatAck(ChatStatus.Message, session.Nickname, friendNick, message);
        await target.SendAsync(ack);
        await session.SendAsync(ack);
    }

    /// <summary>440 的 status 碼 (sub_55B660 的 switch)。</summary>
    private enum ChatStatus : byte
    {
        NameNotFound = 0,                                   // 0x1EF 名稱不存在
        Offline = 1,                                        // 0x1F0 離線
        Message = 2,                                        // 0x1D9 訊息 (帶 comment)
        NotFound = 3,                                       // 0x1D8 找不到 (未用)
    }

    private static Packet BuildChatAck(ChatStatus status, string nick1, string nick2, string? comment = null)
    {
        var ack = new Packet(Opcode.GL_FRIEND_CHAT_ACK)
            .WriteU8((byte)status)
            .WriteStr(nick1)
            .WriteStr(nick2);
        return status == ChatStatus.Message ? ack.WriteStr(comment ?? "") : ack;
    }

    // 441 GL_FRIEND_WHERE_REQ (sub_55B940): str nick — 查好友所在位置。
    // → 442 (sub_55B9F0): status==2 只讀 status (0x21D 找不到資訊);
    //   status==1 續讀 u8 where_type, u8 channel, u8 room_no。
    //   私服無教學/大師/錦標賽, 上線一律回 where_type=0 (大廳 → 0x21E
    //   "%sさんはロビーで待機中です"), channel/room_no=0 (單頻道, 房位留後續)。
    private static async ValueTask FriendWhere(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        if (nick.Length == 0)
        {
            return;
        }

        if (context.Sessions.Find(nick) is null)
        {
            // 離線/不存在 → 找不到資訊 (0x21D)
            await session.SendAsync(new Packet(Opcode.GL_FRIEND_WHERE_ACK).WriteU8(2));
            return;
        }

        // 上線 → 大廳 (client 對 n9 ∉ {9,10,11} 一律顯示 0x21E 大廳待機)
        await session.SendAsync(new Packet(Opcode.GL_FRIEND_WHERE_ACK)
            .WriteU8(1)                                     // 找到
            .WriteU8(0)                                     // where_type: 0 = 大廳
            .WriteU8(0)                                     // channel (單頻道 = 0)
            .WriteU8(0));                                   // room_no (房位留後續)
    }
}
