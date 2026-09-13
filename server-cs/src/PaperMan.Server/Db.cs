// =============================================================================
// SQLite 存取層 — Microsoft.Data.Sqlite, 對應 db/schema.sql。
// 單一寫入者模型 (WAL): 寫入以 Lock 序列化, schema 的 STRICT/CHECK 約束
// 直接承載逆向得到的值域 (slot 0..5119, period 白名單, 角色槽 0..19...)。
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed class Db : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly Lock _gate = new();       // .NET 9+ System.Threading.Lock

    public Db(string path)
    {
        _conn = new SqliteConnection($"Data Source={path};Pooling=false");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();

    private SqliteCommand Cmd(string sql, params ReadOnlySpan<(string Name, object? Value)> args)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    // ------------------------------------------------------------- accounts
    public sealed record LoginResult(
        LoginCode Result, long AccountId = 0, long UserId = 0, string Nickname = "",
        int Cash = 0, long GamePoint = 0, int Level = 1, long Exp = 0);

    /// <summary>GL_LOGIN_REQ(682) 驗證; 密碼 = SHA256(salt + token)。</summary>
    public LoginResult Login(string account, string tokenOrPass, ulong hwKey)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT a.account_id, a.pass_hash, a.pass_salt, a.is_banned, a.cash,
                       u.user_id, u.nickname, u.game_point, u.level, u.exp
                FROM accounts a LEFT JOIN users u ON u.account_id = a.account_id
                WHERE a.login_name = @n
                """, ("@n", account));
            using var r = cmd.ExecuteReader();

            if (!r.Read())
            {
                return new(LoginCode.BadCredentials);
            }

            if (r.GetInt64(3) != 0)
            {
                return new(LoginCode.Banned);
            }

            if (!VerifyPassword(r.GetString(2), tokenOrPass, r.GetString(1)))
            {
                return new(LoginCode.BadCredentials);
            }

            var result = new LoginResult(
                LoginCode.Ok,
                AccountId: r.GetInt64(0),
                UserId: r.IsDBNull(5) ? 0 : r.GetInt64(5),
                Nickname: r.IsDBNull(6) ? "" : r.GetString(6),
                Cash: r.GetInt32(4),
                GamePoint: r.IsDBNull(7) ? 0 : r.GetInt64(7),
                Level: r.IsDBNull(8) ? 1 : r.GetInt32(8),
                Exp: r.IsDBNull(9) ? 0 : r.GetInt64(9));
            r.Close();

            using var upd = Cmd(
                "UPDATE accounts SET hw_key=@h, last_login_at=unixepoch() WHERE account_id=@a",
                ("@h", unchecked((long)hwKey)), ("@a", result.AccountId));
            upd.ExecuteNonQuery();
            return result;
        }
    }

    private static bool VerifyPassword(string salt, string password, string expectedHash)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(salt + password)));
        return hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public void CreateAccount(string name, string pass)
    {
        lock (_gate)
        {
            var salt = Guid.NewGuid().ToString("N")[..16];
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(salt + pass)));
            using var cmd = Cmd(
                "INSERT OR IGNORE INTO accounts(login_name,pass_hash,pass_salt) VALUES(@n,@h,@s)",
                ("@n", name), ("@h", hash), ("@s", salt));
            cmd.ExecuteNonQuery();
        }
    }

    // ------------------------------------------------------------- nickname
    /// <summary>GM_CHECKNICK(210): 0=可用 1=重複。</summary>
    public byte CheckNick(string nick)
    {
        lock (_gate)
        {
            using var cmd = Cmd("SELECT 1 FROM users WHERE nickname=@n", ("@n", nick));
            return cmd.ExecuteScalar() is null ? (byte)0 : (byte)1;
        }
    }

    /// <summary>GM_CREATENICK(212): 建 user; 回傳 user_id, 0=失敗。</summary>
    public long CreateNick(long accountId, string nick)
    {
        lock (_gate)
        {
            try
            {
                using var cmd = Cmd(
                    "INSERT INTO users(account_id,nickname) VALUES(@a,@n) RETURNING user_id",
                    ("@a", accountId), ("@n", nick));
                return (long)cmd.ExecuteScalar()!;
            }
            catch (SqliteException)
            {
                return 0;                                   // UNIQUE(nickname) 落敗
            }
        }
    }

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

    // ------------------------------------------------------------- inventory
    public sealed record InvItem(
        int Slot, int ItemId, float F1, float F2, int PeriodDaysLeft,
        ushort DuraCur, ushort DuraMax);

    /// <summary>GL_MYITEM_ACK(200) 分頁 (sub_524B70: 100/頁, 上限 5120)。</summary>
    public List<InvItem> GetInventoryPage(long userId, int start, int count = 100)
    {
        lock (_gate)
        {
            List<InvItem> list = [];
            using var cmd = Cmd("""
                SELECT slot, item_id, stat_f1, stat_f2, period_days_left,
                       durability_cur, durability_max
                FROM v_inventory_wire WHERE user_id=@u AND slot>=@s
                ORDER BY slot LIMIT @c
                """, ("@u", userId), ("@s", start), ("@c", count));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new(r.GetInt32(0), r.GetInt32(1), r.GetFloat(2), r.GetFloat(3),
                             r.GetInt32(4), (ushort)r.GetInt32(5), (ushort)r.GetInt32(6)));
            return list;
        }
    }

    public sealed record BuyResult(
        bool Ok, int ItemId, float F1, float F2, int Period, byte Kind, ushort Dura)
    {
        public static BuyResult Fail(int itemId) => new(false, itemId, 0, 0, 0, 0, 0);
    }

    /// <summary>GS_BUYITEM(204)/GS_BUY_ONCEITEM(695): 原子 扣款+入包+記帳。</summary>
    public BuyResult BuyItem(long userId, int itemId, byte periodDays, bool useCash)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                var result = BuyItemInTx(tx, userId, itemId, periodDays, useCash);

                if (result.Ok)
                {
                    tx.Commit();
                }
                else
                {
                    tx.Rollback();
                }

                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    private BuyResult BuyItemInTx(SqliteTransaction tx, long userId, int itemId, byte periodDays, bool useCash)
    {
        SqliteCommand TxCmd(string sql, params ReadOnlySpan<(string, object?)> args)
        {
            var c = Cmd(sql, args);
            c.Transaction = tx;
            return c;
        }

        // 1. 目錄查價
        byte kind;
        int price;
        ushort dura;
        using (var q = TxCmd(
            "SELECT kind, price_gp, price_cash, durability FROM item_catalog WHERE item_id=@i",
            ("@i", itemId)))
        using (var r = q.ExecuteReader())
        {
            if (!r.Read())
            {
                return BuyResult.Fail(itemId);              // 不在商品目錄
            }

            kind = (byte)r.GetInt32(0);
            price = useCash ? r.GetInt32(2) : r.GetInt32(1);
            dura = (ushort)r.GetInt32(3);
        }

        // 2. period 白名單 (sub_570B00)
        bool periodOk = kind switch
        {
            0 or 1 or 3 or 5 or 14 => periodDays is 1 or 7 or 15 or 30 or 60 or 90,
            2 or 4 or 9 or 10 or 11 or 15 or 16 => periodDays == 0,
            _ => false,
        };
        if (!periodOk)
        {
            return BuyResult.Fail(itemId);                  // period 不在白名單 (sub_570B00)
        }

        // 3. 扣款 (條件式 UPDATE = 原子餘額檢查)
        string wallet = useCash
            ? "UPDATE accounts SET cash=cash-@p WHERE account_id=(SELECT account_id FROM users WHERE user_id=@u) AND cash>=@p"
            : "UPDATE users SET game_point=game_point-@p WHERE user_id=@u AND game_point>=@p";
        using (var pay = TxCmd(wallet, ("@p", price), ("@u", userId)))
        {
            if (pay.ExecuteNonQuery() != 1)
            {
                return BuyResult.Fail(itemId);              // 餘額不足
            }
        }

        // 4. 最小空 slot (背包上限 5120, sub_524B70)
        int slot;
        using (var slotQ = TxCmd("""
            SELECT IFNULL(MIN(t.slot+1),0) FROM
              (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=@u) t
            WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=@u)
            """, ("@u", userId)))
        {
            slot = Convert.ToInt32(slotQ.ExecuteScalar());
        }

        if (slot >= 5120)
        {
            return BuyResult.Fail(itemId);                  // 背包已滿 (上限 sub_524B70)
        }

        // 5. 入包 + 記帳
        using (var ins = TxCmd("""
            INSERT INTO inventory(user_id,slot,item_id,period_days,expires_at,durability_cur,durability_max)
            VALUES(@u,@s,@i,@p,CASE WHEN @p=0 THEN NULL ELSE unixepoch()+@p*86400 END,@d,@d)
            """, ("@u", userId), ("@s", slot), ("@i", itemId), ("@p", (int)periodDays), ("@d", dura)))
        {
            ins.ExecuteNonQuery();
        }

        using (var log = TxCmd("""
            INSERT INTO shop_transactions(user_id,tx_type,item_id,period_days,gp_delta,cash_delta)
            VALUES(@u,@t,@i,@p,@g,@c)
            """, ("@u", userId), ("@t", useCash ? 1 : 0), ("@i", itemId), ("@p", (int)periodDays),
                 ("@g", useCash ? 0 : -price), ("@c", useCash ? -price : 0)))
        {
            log.ExecuteNonQuery();
        }

        return new(true, itemId, 0f, 0f, periodDays, kind, dura);
    }

    public int GetCash(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT cash FROM accounts WHERE account_id=(SELECT account_id FROM users WHERE user_id=@u)",
                ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
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

    // ------------------------------------------------------------- gifts
    /// <summary>
    /// 送禮 (296)。回 297 的 result 碼: 0=成功, 2=收件人不存在,
    /// 3=物品不在目錄, 1=其他失敗。
    /// </summary>
    public byte GiveGift(long fromUserId, string toNick, int itemId, byte periodDays, string? message)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n",
                ("@n", toNick));
            var toId = who.ExecuteScalar();

            if (toId is null)
            {
                return 2;                                   // 收件人不存在
            }

            try
            {
                using var ins = Cmd("""
                    INSERT INTO gifts(from_user_id, to_user_id, item_id, period_days, message)
                    VALUES(@f, @t, @i, @p, @m)
                    """,
                    ("@f", fromUserId),
                    ("@t", (long)toId),
                    ("@i", itemId),
                    ("@p", (int)periodDays),
                    ("@m", message));
                ins.ExecuteNonQuery();
                return 0;
            }
            catch (SqliteException)
            {
                return 3;                                   // FK 落敗: 物品不在目錄
            }
        }
    }

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

    public void LogPacket(ushort opcode, bool rx, int bytes)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                INSERT INTO packet_stats(day,opcode,rx_count,tx_count,rx_bytes,tx_bytes)
                VALUES(date('now'),@o,@rc,@tc,@rb,@tb)
                ON CONFLICT(day,opcode) DO UPDATE SET
                  rx_count=rx_count+@rc, tx_count=tx_count+@tc,
                  rx_bytes=rx_bytes+@rb, tx_bytes=tx_bytes+@tb
                """, ("@o", opcode), ("@rc", rx ? 1 : 0), ("@tc", rx ? 0 : 1),
                     ("@rb", rx ? bytes : 0), ("@tb", rx ? 0 : bytes));
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // opcode 不在 protocol_packets — 統計表 FK 落敗, 可忽略
            }
        }
    }
}
