// =============================================================================
// 玩家資料存取 — MyInfo (198/247 資料源) 與戰績 (GP_CH*C SetStatMax)。
// (partial — 主體/共用基礎見 Db.cs; 卅四輪依領域拆分, 佈局證據見
//  docs/PACKETS.md 對應章節)
// =============================================================================
using Microsoft.Data.Sqlite;

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

    public sealed record CharSlot(byte SlotNo, byte CharType, ushort[] Equip);

    public List<CharSlot> GetCharacters(long userId)
    {
        lock (_gate)
        {
            List<CharSlot> list = [];
            using var cmd = Cmd("""
                SELECT slot_no, char_type, eq_primary, eq_secondary, eq_melee, eq_grenade,
                       eq_head, eq_face, eq_upper, eq_lower, eq_hands, eq_back, eq_special, eq_set
                FROM characters WHERE user_id=@u ORDER BY slot_no
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var eq = new ushort[12];
                for (int i = 0; i < eq.Length; i++)
                    eq[i] = (ushort)r.GetInt32(2 + i);
                list.Add(new((byte)r.GetInt32(0), (byte)r.GetInt32(1), eq));
            }

            return list;
        }
    }

    public bool CreateChar(long userId, byte slotNo, byte charType)
    {
        lock (_gate)
        {
            try
            {
                using var cmd = Cmd(
                    "INSERT INTO characters(user_id,slot_no,char_type) VALUES(@u,@s,@c)",
                    ("@u", userId), ("@s", slotNo), ("@c", charType));
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (SqliteException)
            {
                return false;                               // UNIQUE(user_id,slot_no) 落敗
            }
        }
    }

    // ------------------------------------------------------------- stats/misc
    /// <summary>
    /// GP_CH*C: client REQ 帶「新的絕對累計值」(sub_5567F0 等) — 只允許
    /// 單調遞增 (MAX), 防倒退/重播; 回傳確認後的 total。
    /// column 由 StatHandlers 白名單提供, 不接受外部字串。
    /// </summary>
    public long SetStatMax(long userId, string column, long newTotal)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                $"UPDATE user_stats SET {column}=MAX({column},@v) WHERE user_id=@u RETURNING {column}",
                ("@v", newTotal), ("@u", userId));
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
    }

}
