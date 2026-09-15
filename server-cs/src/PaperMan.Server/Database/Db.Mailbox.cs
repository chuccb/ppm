// =============================================================================
// Player mailbox persistence for 419 through 426 and unread-count 783/784.
//
// Sender/recipient lookup, message body truncation, read state, and deletion
// remain explicit here instead of being hidden behind a generic social service.
// =============================================================================
namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- messages
    public sealed record MailMsg(long MsgId, string From, string Title, string Body, bool IsRead, ushort DateCode);

    /// <summary>寄信 (419)。收件人須存在; body ≤200 (schema CHECK)。</summary>
    public bool SendMessage(long fromUserId, string toNick, string title, string body)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n", ("@n", toNick));
            var toId = who.ExecuteScalar();

            if (toId is null)
            {
                return false;
            }

            using var ins = Cmd("""
                INSERT INTO messages(from_user_id, to_user_id, title, body)
                VALUES(@f, @t, @ti, @b)
                """,
                ("@f", fromUserId),
                ("@t", (long)toId),
                ("@ti", title),
                ("@b", body.Length > 200 ? body[..200] : body));
            return ins.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>收件匣 (425→426)。date 編碼 = MMDD (u16)。</summary>
    public List<MailMsg> GetMessages(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT m.msg_id, COALESCE(u.nickname,'system'), m.title, m.body,
                       m.is_read, strftime('%m%d', m.sent_at, 'unixepoch')
                FROM messages m
                LEFT JOIN users u ON u.user_id = m.from_user_id
                WHERE m.to_user_id=@u
                ORDER BY m.msg_id DESC LIMIT 50
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();

            List<MailMsg> list = [];
            while (r.Read())
            {
                list.Add(new(
                    r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetInt64(4) != 0, ushort.Parse(r.GetString(5))));
            }

            return list;
        }
    }

    /// <summary>未讀信數 (783→784 信箱紅點)。</summary>
    public int CountUnreadMessages(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT COUNT(*) FROM messages WHERE to_user_id=@u AND is_read=0",
                ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    /// <summary>刪信 (421)。</summary>
    public bool DeleteMessage(long userId, long msgId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "DELETE FROM messages WHERE msg_id=@m AND to_user_id=@u",
                ("@m", msgId), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>標記信件已讀 (GL_MSG_READ 423/424)。</summary>
    public bool MarkMessageRead(long userId, long msgId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE messages SET is_read=1 WHERE msg_id=@m AND to_user_id=@u",
                ("@m", msgId), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

}
