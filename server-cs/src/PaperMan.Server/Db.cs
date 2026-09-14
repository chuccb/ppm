// =============================================================================
// SQLite 存取層 — Microsoft.Data.Sqlite, 對應 db/schema.sql。
// 單一寫入者模型 (WAL): 寫入以 Lock 序列化, schema 的 STRICT/CHECK 約束
// 直接承載逆向得到的值域 (slot 0..5119, period 白名單, 角色槽 0..19...)。
// =============================================================================
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed partial class Db : IDisposable
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
        MigrateLegacyLoginMetadataColumns();
    }

    /// <summary>
    /// Preserves old database data while correcting 682 field names. sub_43DF00
    /// proves the decoded u32 comes from datarevision.txt; sub_9A8790 and
    /// sub_9A86A0 prove raw24 is storage serial or fallback adapter MAC.
    /// </summary>
    private void MigrateLegacyLoginMetadataColumns()
    {
        using var columns = Cmd("PRAGMA table_info(accounts)");
        using var reader = columns.ExecuteReader();
        bool hasLegacyHardwareKey = false;
        bool hasClientDataRevision = false;
        bool hasLegacySecurityState = false;
        bool hasFingerprintSource = false;
        bool hasClientFingerprint = false;
        while (reader.Read())
        {
            string name = reader.GetString(1);
            hasLegacyHardwareKey |= name.Equals("hw_key", StringComparison.OrdinalIgnoreCase);
            hasClientDataRevision |= name.Equals("client_data_revision", StringComparison.OrdinalIgnoreCase);
            hasLegacySecurityState |= name.Equals("security_state", StringComparison.OrdinalIgnoreCase);
            hasFingerprintSource |= name.Equals("fingerprint_source", StringComparison.OrdinalIgnoreCase);
            hasClientFingerprint |= name.Equals("client_fingerprint", StringComparison.OrdinalIgnoreCase);
        }

        reader.Close();
        if (hasLegacyHardwareKey && !hasClientDataRevision)
        {
            using var migrate = Cmd("ALTER TABLE accounts RENAME COLUMN hw_key TO client_data_revision");
            migrate.ExecuteNonQuery();
        }

        if (hasLegacySecurityState && !hasFingerprintSource)
        {
            using var migrate = Cmd("ALTER TABLE accounts RENAME COLUMN security_state TO fingerprint_source");
            migrate.ExecuteNonQuery();
        }

        if (!hasClientFingerprint && (hasClientDataRevision || hasLegacyHardwareKey))
        {
            using var migrate = Cmd("ALTER TABLE accounts ADD COLUMN client_fingerprint BLOB");
            migrate.ExecuteNonQuery();
        }
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
    public LoginResult Login(
        string account,
        string tokenOrPass,
        uint clientDataRevision,
        LoginFingerprintSource fingerprintSource,
        byte[] clientFingerprint)
    {
        ArgumentNullException.ThrowIfNull(clientFingerprint);
        if (clientFingerprint.Length != 24)
        {
            throw new ArgumentException("682 fingerprint must contain exactly 24 bytes.", nameof(clientFingerprint));
        }

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
                r.Close();
                if (!string.IsNullOrWhiteSpace(account))
                {
                    CreateAccount(account, tokenOrPass);
                    return Login(account, tokenOrPass, clientDataRevision, fingerprintSource, clientFingerprint);
                }
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
                """
                UPDATE accounts
                SET client_data_revision=@r,
                    fingerprint_source=@s,
                    client_fingerprint=@f,
                    last_login_at=unixepoch()
                WHERE account_id=@a
                """,
                ("@r", (long)clientDataRevision),
                ("@s", (byte)fingerprintSource),
                ("@f", clientFingerprint),
                ("@a", result.AccountId));
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
    /// <summary>暱稱是否已被使用 (GM_CHECKNICK 210 用; result 碼由 handler 對映)。</summary>
    public bool IsNickTaken(string nick)
    {
        lock (_gate)
        {
            using var cmd = Cmd("SELECT 1 FROM users WHERE nickname=@n", ("@n", nick));
            return cmd.ExecuteScalar() is not null;
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
                long userId = (long)cmd.ExecuteScalar()!;

                using var stCmd = Cmd("INSERT OR IGNORE INTO user_stats(user_id) VALUES(@u)", ("@u", userId));
                stCmd.ExecuteNonQuery();

                using var charCmd = Cmd("INSERT OR IGNORE INTO characters(user_id,slot_no,char_type) VALUES(@u,0,1)", ("@u", userId));
                charCmd.ExecuteNonQuery();

                return userId;
            }
            catch (SqliteException)
            {
                return 0;                                   // UNIQUE(nickname) 落敗
            }
        }
    }

    public void LogPacket(ushort opcode, bool isReceive, int bytes)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                INSERT INTO packet_stats(day,opcode,rx_count,tx_count,rx_bytes,tx_bytes)
                VALUES(date('now'),@o,@rc,@tc,@rb,@tb)
                ON CONFLICT(day,opcode) DO UPDATE SET
                  rx_count=rx_count+@rc, tx_count=tx_count+@tc,
                  rx_bytes=rx_bytes+@rb, tx_bytes=tx_bytes+@tb
                """, ("@o", opcode), ("@rc", isReceive ? 1 : 0), ("@tc", isReceive ? 0 : 1),
                     ("@rb", isReceive ? bytes : 0), ("@tb", isReceive ? 0 : bytes));
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
