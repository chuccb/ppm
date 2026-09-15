// =============================================================================
// GL_FRIEND_CHAT_REQ (439) → GL_FRIEND_CHAT_ACK (440)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // 439 GL_FRIEND_CHAT_REQ (sub_55B510): s32 uid + str my_nick +
    //   str friend_nick + str message — 1:1 好友聊天。uid 為 dword_F2A684
    //   (client 自 144 回帶的頁籤/頻道 id, 僅回帶不需判讀); my_nick 以
    //   server session 為準 (防冒名)。
    // → 440 (sub_55B660): status 2 = 訊息 (遞送 + 回聲, client 不本地顯示,
    //   故需回聲); 0 = 名稱不存在 / 1 = 離線 回給發話者。nick1/nick2 為
    //   發話者/收話者暱稱 (client 讀後僅推進游標, 顯示靠全域伙伴名 + comment)。
    private static async ValueTask GL_FRIEND_CHAT_REQ(Session session, Packet packet, ServerContext context)
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
            await session.SendAsync(CreateGL_FRIEND_CHAT_ACK(GL_FRIEND_CHAT_ACK_Status.NameNotFound, friendNick, session.Nickname));
            return;
        }

        // 在 DB 但離線 (0x1F0 "%s さんはオフラインです")
        var target = context.Sessions.Find(friendNick);
        if (target is null || ReferenceEquals(target, session))
        {
            await session.SendAsync(CreateGL_FRIEND_CHAT_ACK(GL_FRIEND_CHAT_ACK_Status.Offline, friendNick, session.Nickname));
            return;
        }

        // 上線 → 遞送給好友並回聲給自己 (0x1D9 "← %s さんのコメント")
        var ack = CreateGL_FRIEND_CHAT_ACK(GL_FRIEND_CHAT_ACK_Status.Message, session.Nickname, friendNick, message);
        await target.SendAsync(ack);
        await session.SendAsync(ack);
    }

    /// <summary>440 的 status 碼 (sub_55B660 的 switch)。</summary>
    private enum GL_FRIEND_CHAT_ACK_Status : byte
    {
        NameNotFound = 0,                                   // 0x1EF 名稱不存在
        Offline = 1,                                        // 0x1F0 離線
        Message = 2,                                        // 0x1D9 訊息 (帶 comment)
        NotFound = 3,                                       // 0x1D8 找不到 (未用)
    }

    private static Packet CreateGL_FRIEND_CHAT_ACK(GL_FRIEND_CHAT_ACK_Status status, string nick1, string nick2, string? comment = null)
    {
        var ack = new Packet(Opcode.GL_FRIEND_CHAT_ACK)
            .WriteU8((byte)status)
            .WriteStr(nick1)
            .WriteStr(nick2);
        return status == GL_FRIEND_CHAT_ACK_Status.Message ? ack.WriteStr(comment ?? "") : ack;
    }
}
