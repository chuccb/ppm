// =============================================================================
// Player summary reads for GL_MYINFO_ACK(198) and GL_CLIENTINFO_ACK(247).
//
// These are database projections used by several handlers. They keep native
// counter order visible but do not own character, loadout, or mutation policy.
// =============================================================================
namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- myinfo
    /// <summary>user_stats 的 19 個計數器 (GP_CH*C 家族順序)。</summary>
    public sealed record Stats(
        long Wins, long Losses, long Kills, long Deaths, long Headshots,
        long Combos, long Hearts, long DoubleKill, long TripleKill, long Criticals,
        long MultiKill, long UltraKill, long ZKill, long KKill, long DdKill,
        long PlayCount, long RoundCount, long Disconnects, long PlayTimeS);

    public sealed record MyInfo(
        long UserId, string Nickname, int Level, long Exp, long Gp, int Cash,
        byte CurrentChar, Stats Stats);

    /// <summary>GL_MYINFO_ACK(198) 資料來源 (v_myinfo)。</summary>
    public MyInfo? GetMyInfo(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT user_id, nickname, level, exp, game_point, cash, current_char,
                       wins, losses, kills, deaths, headshots, combos, hearts,
                       double_kill, triple_kill, criticals, multi_kill, ultra_kill,
                       z_kill, k_kill, dd_kill, play_count, round_count, disconnects, play_time_s
                FROM v_myinfo WHERE user_id=@u
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return null;
            }

            return new(
                r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3),
                r.GetInt64(4), r.GetInt32(5), (byte)r.GetInt32(6),
                new Stats(
                    Wins: r.GetInt64(7), Losses: r.GetInt64(8),
                    Kills: r.GetInt64(9), Deaths: r.GetInt64(10),
                    Headshots: r.GetInt64(11), Combos: r.GetInt64(12),
                    Hearts: r.GetInt64(13), DoubleKill: r.GetInt64(14),
                    TripleKill: r.GetInt64(15), Criticals: r.GetInt64(16),
                    MultiKill: r.GetInt64(17), UltraKill: r.GetInt64(18),
                    ZKill: r.GetInt64(19), KKill: r.GetInt64(20),
                    DdKill: r.GetInt64(21), PlayCount: r.GetInt64(22),
                    RoundCount: r.GetInt64(23), Disconnects: r.GetInt64(24),
                    PlayTimeS: r.GetInt64(25)));
        }
    }

    /// <summary>247 GL_CLIENTINFO 用: 以暱稱查他人資料 (廿五輪)。</summary>
    public MyInfo? GetMyInfoByNick(string nickname)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n", ("@n", nickname));
            var uid = who.ExecuteScalar();

            return uid is null ? null : GetMyInfoUnlocked((long)uid);
        }
    }

    private MyInfo? GetMyInfoUnlocked(long userId)
    {
        using var cmd = Cmd("""
            SELECT user_id, nickname, level, exp, game_point, cash, current_char,
                   wins, losses, kills, deaths, headshots, combos, hearts,
                   double_kill, triple_kill, criticals, multi_kill, ultra_kill,
                   z_kill, k_kill, dd_kill, play_count, round_count, disconnects, play_time_s
            FROM v_myinfo WHERE user_id=@u
            """, ("@u", userId));
        using var r = cmd.ExecuteReader();

        if (!r.Read())
        {
            return null;
        }

        return new(
            r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3),
            r.GetInt64(4), r.GetInt32(5), (byte)r.GetInt32(6),
            new Stats(
                Wins: r.GetInt64(7), Losses: r.GetInt64(8),
                Kills: r.GetInt64(9), Deaths: r.GetInt64(10),
                Headshots: r.GetInt64(11), Combos: r.GetInt64(12),
                Hearts: r.GetInt64(13), DoubleKill: r.GetInt64(14),
                TripleKill: r.GetInt64(15), Criticals: r.GetInt64(16),
                MultiKill: r.GetInt64(17), UltraKill: r.GetInt64(18),
                ZKill: r.GetInt64(19), KKill: r.GetInt64(20),
                DdKill: r.GetInt64(21), PlayCount: r.GetInt64(22),
                RoundCount: r.GetInt64(23), Disconnects: r.GetInt64(24),
                PlayTimeS: r.GetInt64(25)));
    }

}
