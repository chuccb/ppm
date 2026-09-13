// =============================================================================
// SQLite 存取層 — 使用 Microsoft.Data.Sqlite, 對應 db/schema.sql。
// 單一寫入者模型 (SQLite WAL): 所有寫入經由 lock 序列化, 讀取共用連線。
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed class Db : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly Lock _gate = new();

    public Db(string path)
    {
        _conn = new SqliteConnection($"Data Source={path};Pooling=false");
        _conn.Open();
        Exec("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=NORMAL;");
    }

    public void Dispose() => _conn.Dispose();

    private void Exec(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private SqliteCommand Cmd(string sql, params (string, object?)[] args)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args)
            cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return cmd;
    }

    // ------------------------------------------------------------- accounts
    public record LoginResult(int Result, long AccountId, long UserId, string Nickname, int Cash, long GamePoint, int Level, long Exp);

    /// <summary>GL_LOGIN_REQ(682) 驗證。result: 1=OK 2=帳密錯 0xC8=封鎖。</summary>
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
            if (!r.Read()) return new(2, 0, 0, "", 0, 0, 1, 0);
            if (r.GetInt64(3) != 0) return new(0xC8, 0, 0, "", 0, 0, 1, 0);

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(r.GetString(2) + tokenOrPass)));
            if (!hash.Equals(r.GetString(1), StringComparison.OrdinalIgnoreCase))
                return new(2, 0, 0, "", 0, 0, 1, 0);

            long aid = r.GetInt64(0);
            long uid = r.IsDBNull(5) ? 0 : r.GetInt64(5);
            var nick = r.IsDBNull(6) ? "" : r.GetString(6);
            r.Close();

            using var upd = Cmd(
                "UPDATE accounts SET hw_key=@h, last_login_at=unixepoch() WHERE account_id=@a",
                ("@h", unchecked((long)hwKey)), ("@a", aid));
            upd.ExecuteNonQuery();

            using var q2 = Cmd("""
                SELECT a.cash, IFNULL(u.game_point,0), IFNULL(u.level,1), IFNULL(u.exp,0)
                FROM accounts a LEFT JOIN users u ON u.account_id=a.account_id
                WHERE a.account_id=@a
                """, ("@a", aid));
            using var r2 = q2.ExecuteReader();
            r2.Read();
            return new(1, aid, uid, nick, r2.GetInt32(0), r2.GetInt64(1), r2.GetInt32(2), r2.GetInt64(3));
        }
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

    /// <summary>GM_CREATENICK(212): 建 user。回傳 user_id, 0=失敗。</summary>
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
            catch (SqliteException) { return 0; }
        }
    }

    // ------------------------------------------------------------- myinfo
    public record MyInfo(long UserId, string Nickname, int Level, long Exp, long Gp, int Cash,
                         byte CurrentChar, long[] Stats);

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
            if (!r.Read()) return null;
            var stats = new long[19];
            for (int i = 0; i < 19; i++) stats[i] = r.GetInt64(7 + i);
            return new(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3),
                       r.GetInt64(4), r.GetInt32(5), (byte)r.GetInt32(6), stats);
        }
    }

    public record CharSlot(byte SlotNo, byte CharType, ushort[] Equip);

    public List<CharSlot> GetCharacters(long userId)
    {
        lock (_gate)
        {
            var list = new List<CharSlot>();
            using var cmd = Cmd("""
                SELECT slot_no, char_type, eq_primary, eq_secondary, eq_melee, eq_grenade,
                       eq_head, eq_face, eq_upper, eq_lower, eq_hands, eq_back, eq_special, eq_set
                FROM characters WHERE user_id=@u ORDER BY slot_no
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var eq = new ushort[12];
                for (int i = 0; i < 12; i++) eq[i] = (ushort)r.GetInt32(2 + i);
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
            catch (SqliteException) { return false; }
        }
    }

    // ------------------------------------------------------------- inventory
    public record InvItem(int Slot, int ItemId, float F1, float F2, int PeriodDaysLeft,
                          ushort DuraCur, ushort DuraMax);

    /// <summary>GL_MYITEM_ACK(200) 分頁 (sub_524B70: 100/頁, 上限 5120)。</summary>
    public List<InvItem> GetInventoryPage(long userId, int start, int count = 100)
    {
        lock (_gate)
        {
            var list = new List<InvItem>();
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

    public record BuyResult(bool Ok, int ItemId, float F1, float F2, int Period, byte Kind, ushort Dura);

    /// <summary>GS_BUYITEM(204)/GS_BUY_ONCEITEM(695)。原子扣款+入包。</summary>
    public BuyResult BuyItem(long userId, int itemId, byte periodDays, bool useCash)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                using var q = Cmd(
                    "SELECT kind, price_gp, price_cash, durability FROM item_catalog WHERE item_id=@i",
                    ("@i", itemId));
                q.Transaction = tx;
                using var r = q.ExecuteReader();
                if (!r.Read()) { tx.Rollback(); return new(false, itemId, 0, 0, 0, 0, 0); }
                byte kind = (byte)r.GetInt32(0);
                int price = useCash ? r.GetInt32(2) : r.GetInt32(1);
                ushort dura = (ushort)r.GetInt32(3);
                r.Close();

                // sub_570B00 period 白名單
                bool periodOk = kind switch
                {
                    0 or 1 or 3 or 14 => periodDays is 1 or 7 or 15 or 30 or 60 or 90,
                    5 => periodDays is 1 or 7 or 15 or 30 or 60 or 90,
                    2 or 4 or 9 or 15 or 10 or 11 or 16 => periodDays == 0,
                    _ => false,
                };
                if (!periodOk) { tx.Rollback(); return new(false, itemId, 0, 0, 0, kind, 0); }

                string wallet = useCash
                    ? "UPDATE accounts SET cash=cash-@p WHERE account_id=(SELECT account_id FROM users WHERE user_id=@u) AND cash>=@p"
                    : "UPDATE users SET game_point=game_point-@p WHERE user_id=@u AND game_point>=@p";
                using var pay = Cmd(wallet, ("@p", price), ("@u", userId));
                pay.Transaction = tx;
                if (pay.ExecuteNonQuery() != 1) { tx.Rollback(); return new(false, itemId, 0, 0, 0, kind, 0); }

                using var slotQ = Cmd("""
                    SELECT IFNULL(MIN(t.slot+1),0) FROM
                      (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=@u) t
                    WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=@u)
                    """, ("@u", userId));
                slotQ.Transaction = tx;
                int slot = Convert.ToInt32(slotQ.ExecuteScalar());
                if (slot >= 5120) { tx.Rollback(); return new(false, itemId, 0, 0, 0, kind, 0); }

                using var ins = Cmd("""
                    INSERT INTO inventory(user_id,slot,item_id,period_days,expires_at,durability_cur,durability_max)
                    VALUES(@u,@s,@i,@p,CASE WHEN @p=0 THEN NULL ELSE unixepoch()+@p*86400 END,@d,@d)
                    """, ("@u", userId), ("@s", slot), ("@i", itemId), ("@p", (int)periodDays), ("@d", dura));
                ins.Transaction = tx;
                ins.ExecuteNonQuery();

                using var log = Cmd("""
                    INSERT INTO shop_transactions(user_id,tx_type,item_id,period_days,gp_delta,cash_delta)
                    VALUES(@u,@t,@i,@p,@g,@c)
                    """, ("@u", userId), ("@t", useCash ? 1 : 0), ("@i", itemId), ("@p", (int)periodDays),
                         ("@g", useCash ? 0 : -price), ("@c", useCash ? -price : 0));
                log.Transaction = tx;
                log.ExecuteNonQuery();

                tx.Commit();
                return new(true, itemId, 0f, 0f, periodDays, kind, dura);
            }
            catch { tx.Rollback(); throw; }
        }
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

    /// <summary>GP_CH*C REQ → 累計後回傳新值 (ACK payload)。</summary>
    public long BumpStat(long userId, string column, long delta)
    {
        lock (_gate)
        {
            // column 由 handler 白名單決定, 不接受外部字串
            using var cmd = Cmd(
                $"UPDATE user_stats SET {column}={column}+@d WHERE user_id=@u RETURNING {column}",
                ("@d", delta), ("@u", userId));
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
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
            try { cmd.ExecuteNonQuery(); } catch (SqliteException) { /* opcode 不在表中 */ }
        }
    }
}
