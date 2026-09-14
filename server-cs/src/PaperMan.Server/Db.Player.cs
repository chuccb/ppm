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

    // ------------------------------------------------------------- loadout
    /// <summary>sub_524660 武器編組 — equipped(與 sub1..3) 為 u16 類別內索引, 0=空。</summary>
    public sealed record WeaponGroup(
        byte GroupNo, ushort Equipped, ushort Sub1, ushort Sub2, ushort Sub3, int[] Parts);

    /// <summary>sub_527550/sub_527D00 的技能(9)與快速(7)槽 — item_id 為完整 id, 0=空。</summary>
    public sealed record Slots(int[] Skill, int[] Quick);

    public List<WeaponGroup> GetWeaponGroups(long userId)
    {
        lock (_gate)
        {
            List<WeaponGroup> list = [];
            using var cmd = Cmd("""
                SELECT group_no, equipped, sub1, sub2, sub3,
                       part0, part1, part2, part3, part4, part5, part6, part7
                FROM weapon_groups WHERE user_id=@u ORDER BY group_no
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var parts = new int[8];
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = r.GetInt32(5 + i);
                }

                list.Add(new(
                    (byte)r.GetInt32(0), (ushort)r.GetInt32(1),
                    (ushort)r.GetInt32(2), (ushort)r.GetInt32(3), (ushort)r.GetInt32(4),
                    parts));
            }

            return list;
        }
    }

    public Slots GetSlots(long userId)
    {
        lock (_gate)
        {
            var skill = new int[9];                         // sub_522480 讀 9×s32
            var quick = new int[7];                         // sub_527AF0 讀 7×s32 (0x1C)
            using var cmd = Cmd("""
                SELECT slot_kind, idx, item_id
                FROM skill_slots WHERE user_id=@u
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int kind = r.GetInt32(0);
                int idx = r.GetInt32(1);
                int item = r.GetInt32(2);
                if (kind == 0 && idx is >= 0 and < 9)
                {
                    skill[idx] = item;
                }
                else if (kind == 1 && idx is >= 0 and < 7)
                {
                    quick[idx] = item;
                }
            }

            return new Slots(skill, quick);
        }
    }

    // ------------------------------------------------------------- stats/misc
    /// <summary>教學步驟索引 (GL_TUTORIALINDEX 685/686 與 689/690)。</summary>
    public int GetTutorialIndex(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT flags1 FROM users WHERE user_id=@u", ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    public bool SetTutorialIndex(long userId, int tutorialIndex)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE users SET flags1=@t, updated_at=unixepoch() WHERE user_id=@u",
                ("@t", tutorialIndex), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>切換現役角色槽 (GI_CHANGEDATA 218/219, GI_CHANGESLOT 312/313)。</summary>
    public bool SetCurrentChar(long userId, byte slotNo)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE users SET current_char=@s, updated_at=unixepoch() WHERE user_id=@u AND @s BETWEEN 0 AND 19",
                ("@s", (int)slotNo), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>購買新角色槽 (GS_BUYCHAR 310/311)。</summary>
    public bool BuyCharacter(long userId, byte slotNo, byte charType, int priceGp = 0)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                if (priceGp > 0)
                {
                    using var pay = Cmd(
                        "UPDATE users SET game_point=game_point-@p WHERE user_id=@u AND game_point>=@p",
                        ("@p", priceGp), ("@u", userId));
                    pay.Transaction = tx;
                    if (pay.ExecuteNonQuery() != 1)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                using var ins = Cmd("""
                    INSERT INTO characters(user_id, slot_no, char_type)
                    VALUES(@u, @s, @c)
                    ON CONFLICT(user_id, slot_no) DO UPDATE SET char_type=@c
                    """, ("@u", userId), ("@s", (int)slotNo), ("@c", (int)charType));
                ins.Transaction = tx;
                ins.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                return false;
            }
        }
    }

    /// <summary>更新技能槽 (GI_CHANGE_SKILLITEMSLOT 466/467)。</summary>
    public bool UpdateSkillSlot(long userId, byte slotKind, byte idx, int itemId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                INSERT INTO skill_slots(user_id, slot_kind, idx, item_id)
                VALUES(@u, @k, @i, @item)
                ON CONFLICT(user_id, slot_kind, idx) DO UPDATE SET item_id=@item
                """, ("@u", userId), ("@k", (int)slotKind), ("@i", (int)idx), ("@item", itemId));
            try
            {
                return cmd.ExecuteNonQuery() == 1;
            }
            catch
            {
                return false;
            }
        }
    }

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
