// =============================================================================
// GameCenter SQLite 存取擴充 — 迷你遊戲個人紀錄與排行榜
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    public sealed record GameCenterRecord(int HighScore, int Rank, int PlayCount, int Coins);
    public sealed record GameCenterRankEntry(string Nickname, int Score, int Rank);

    public GameCenterRecord GetGameCenterRecord(long userId, short gameId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT high_score, coins, play_count
                FROM gamecenter_records
                WHERE user_id = @u AND game_no = @g
                """, ("@u", userId), ("@g", (int)gameId));

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int highScore = reader.GetInt32(0);
                int coins = reader.GetInt32(1);
                int playCount = reader.GetInt32(2);

                // 計算排名
                using var rankCmd = Cmd("""
                    SELECT COUNT(*) + 1
                    FROM gamecenter_records
                    WHERE game_no = @g AND high_score > @s
                    """, ("@g", (int)gameId), ("@s", highScore));
                int rank = Convert.ToInt32(rankCmd.ExecuteScalar() ?? 1);

                return new GameCenterRecord(highScore, rank, playCount, coins);
            }

            return new GameCenterRecord(0, 0, 0, 0);
        }
    }

    public (int NewHighScore, int Rank) SaveGameCenterScore(long userId, short gameId, int score, int gpDelta, int expDelta)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                INSERT INTO gamecenter_records (user_id, game_no, high_score, play_count, updated_at)
                VALUES (@u, @g, @s, 1, unixepoch())
                ON CONFLICT(user_id, game_no) DO UPDATE SET
                    high_score = MAX(high_score, @s),
                    play_count = play_count + 1,
                    updated_at = unixepoch()
                RETURNING high_score;
                """, ("@u", userId), ("@g", (int)gameId), ("@s", score));

            int newHighScore = Convert.ToInt32(cmd.ExecuteScalar() ?? score);

            if (gpDelta > 0 || expDelta > 0)
            {
                using var rewardCmd = Cmd("""
                    UPDATE users
                    SET game_point = game_point + @gp,
                        exp = exp + @exp
                    WHERE user_id = @u
                    """, ("@u", userId), ("@gp", gpDelta), ("@exp", expDelta));
                rewardCmd.ExecuteNonQuery();
            }

            using var rankCmd = Cmd("""
                SELECT COUNT(*) + 1
                FROM gamecenter_records
                WHERE game_no = @g AND high_score > @s
                """, ("@g", (int)gameId), ("@s", newHighScore));
            int rank = Convert.ToInt32(rankCmd.ExecuteScalar() ?? 1);

            return (newHighScore, rank);
        }
    }

    public List<GameCenterRankEntry> GetGameCenterRankings(short gameId, int limit = 20)
    {
        lock (_gate)
        {
            var list = new List<GameCenterRankEntry>();
            using var cmd = Cmd("""
                SELECT u.nickname, r.high_score
                FROM gamecenter_records r
                JOIN users u ON u.user_id = r.user_id
                WHERE r.game_no = @g AND r.high_score > 0
                ORDER BY r.high_score DESC
                LIMIT @lim
                """, ("@g", (int)gameId), ("@lim", limit));

            using var reader = cmd.ExecuteReader();
            int rank = 1;
            while (reader.Read())
            {
                list.Add(new GameCenterRankEntry(
                    Nickname: reader.GetString(0),
                    Score: reader.GetInt32(1),
                    Rank: rank++
                ));
            }
            return list;
        }
    }
}
