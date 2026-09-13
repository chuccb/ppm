// =============================================================================
// 社交系統存取 — 好友 (429-436)、任務 (867-870)、戰隊 (585/586)。
// (partial — 主體/共用基礎見 Db.cs; 卅四輪依領域拆分, 佈局證據見
//  docs/PACKETS.md 對應章節)
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

    // ------------------------------------------------------------- quests
    /// <summary>接任務 (867)。quest 必須在目錄且未接過; state=1 WORKING。</summary>
    public bool AcceptQuest(long userId, int questId)
    {
        lock (_gate)
        {
            try
            {
                using var cmd = Cmd("""
                    INSERT INTO user_quests(user_id, quest_id, state)
                    SELECT @u, quest_id, 1 FROM quest_catalog WHERE quest_id=@q
                    """,
                    ("@u", userId), ("@q", questId));
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (SqliteException)
            {
                return false;                               // 已接過 (PK 落敗)
            }
        }
    }

    /// <summary>取消任務 (869)。只允許取消進行中的 (state 0/1)。</summary>
    public bool CancelQuest(long userId, int questId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                UPDATE user_quests SET state=4, updated_at=unixepoch()
                WHERE user_id=@u AND quest_id=@q AND state IN (0, 1)
                """,
                ("@u", userId), ("@q", questId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    // ------------------------------------------------------------- clans
    /// <summary>
    /// 建戰隊 — 走獨立對 GC_CLAN_CREATE_REQ(585)/_ACK(586), 非隧道
    /// (八輪更正)。回 clan_id; 名稱重複或已入隊 → 0。
    /// </summary>
    public long CreateClan(long leaderUserId, string name, byte emblem = 0)
    {
        if (name is not { Length: > 0 and <= 16 })
        {
            return 0;
        }

        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                using var ins = Cmd(
                    "INSERT INTO clans(name, leader_id, emblem_id) VALUES(@n, @u, @e) RETURNING clan_id",
                    ("@n", name), ("@u", leaderUserId), ("@e", (int)emblem));
                ins.Transaction = tx;
                long clanId = Convert.ToInt64(ins.ExecuteScalar()!);

                using var mem = Cmd(
                    "INSERT INTO clan_members(clan_id, user_id, rank) VALUES(@c, @u, 2)",
                    ("@c", clanId), ("@u", leaderUserId));
                mem.Transaction = tx;
                mem.ExecuteNonQuery();

                using var upd = Cmd(
                    "UPDATE users SET clan_id=@c WHERE user_id=@u",
                    ("@c", clanId), ("@u", leaderUserId));
                upd.Transaction = tx;
                upd.ExecuteNonQuery();

                tx.Commit();
                return clanId;
            }
            catch (SqliteException)   // UNIQUE(name) / UNIQUE(user_id) 落敗
            {
                tx.Rollback();
                return 0;
            }
        }
    }
}
