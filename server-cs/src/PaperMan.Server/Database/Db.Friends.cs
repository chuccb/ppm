// =============================================================================
// Friend-list persistence for 429 through 436.
//
// This file owns only durable friend rows. Online lookup and chat delivery stay
// in process-local session state / the canonical friend handlers.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- friends
    public enum FriendAdd
    {
        Ok,
        Duplicate,
        NotFound,
        Full,
    }

    /// <summary>加好友 (429)。上限 50; state=1 直接視為已接受。</summary>
    public FriendAdd AddFriend(long userId, string friendNick)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n",
                ("@n", friendNick));
            var friendId = who.ExecuteScalar();

            if (friendId is null || (long)friendId == userId)
            {
                return FriendAdd.NotFound;
            }

            using var cnt = Cmd(
                "SELECT COUNT(*) FROM friends WHERE user_id=@u",
                ("@u", userId));
            if (Convert.ToInt32(cnt.ExecuteScalar()) >= 50)
            {
                return FriendAdd.Full;
            }

            try
            {
                using var ins = Cmd(
                    "INSERT INTO friends(user_id, friend_id, state) VALUES(@u, @f, 1)",
                    ("@u", userId), ("@f", (long)friendId));
                ins.ExecuteNonQuery();
                return FriendAdd.Ok;
            }
            catch (SqliteException)
            {
                return FriendAdd.Duplicate;                 // PK 落敗 = 已是好友
            }
        }
    }

    /// <summary>刪好友 (431)。</summary>
    public bool DeleteFriend(long userId, string friendNick)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                DELETE FROM friends
                WHERE user_id=@u
                  AND friend_id=(SELECT user_id FROM users WHERE nickname=@n)
                """,
                ("@u", userId), ("@n", friendNick));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>好友清單 (433) — (nick, status) 對; status 用 state 欄位。</summary>
    public List<(string Nick, int Status)> GetFriends(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT u.nickname, f.state
                FROM friends f
                JOIN users u ON u.user_id = f.friend_id
                WHERE f.user_id=@u
                ORDER BY u.nickname
                """,
                ("@u", userId));
            using var r = cmd.ExecuteReader();

            var list = new List<(string, int)>();
            while (r.Read())
            {
                list.Add((r.GetString(0), r.GetInt32(1)));
            }

            return list;
        }
    }

}
