// =============================================================================
// SQLite 存取層 — Microsoft.Data.Sqlite, 對應 db/schema.sql。
// 單一寫入者模型 (WAL): 寫入以 Lock 序列化, schema 的 STRICT/CHECK 約束
// 直接承載逆向得到的值域 (slot 0..5119, period 白名單, 角色槽 0..19...)。
// =============================================================================
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>Result of idempotent database initialization at process startup.</summary>
public readonly record struct DatabaseInitialization(
    bool CreatedDatabaseFile,
    int ProtocolPacketDefinitionCount);

public sealed partial class Db : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly Lock _gate = new();       // .NET 9+ System.Threading.Lock

    /// <summary>Absolute on-disk SQLite path used by this server process.</summary>
    public string DatabasePath { get; }

    /// <summary>Details recorded while opening and initializing this database.</summary>
    public DatabaseInitialization Initialization { get; }

    /// <summary>
    /// Opens a SQLite database, creating its parent directory, schema, protocol
    /// catalog, and default operational settings when they do not yet exist.
    /// This is intentionally the sole first-run database path; no Python setup
    /// command or hand-created empty file is required.
    /// </summary>
    public Db(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        string parentDirectory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("The database path must have a parent directory.");
        Directory.CreateDirectory(parentDirectory);

        bool databaseFileWasCreated = !File.Exists(DatabasePath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = true,
        };
        _conn = new SqliteConnection(connectionString.ConnectionString);
        try
        {
            _conn.Open();
            ConfigureConnection();
            int protocolPacketDefinitionCount = DatabaseBootstrapper.EnsureCurrent(_conn);
            MigrateLegacyLoginMetadataColumns();
            EnsureCurrentAccountGuards();
            Initialization = new DatabaseInitialization(databaseFileWasCreated, protocolPacketDefinitionCount);
        }
        catch
        {
            _conn.Dispose();
            throw;
        }
    }

    private void ConfigureConnection()
    {
        using var command = _conn.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=NORMAL;";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Preserves old database data while correcting 682 field names. sub_43DF00
    /// proves the decoded u32 comes from datarevision.txt; sub_9A8790 and
    /// sub_9A86A0 prove raw24 is storage serial or fallback adapter MAC.
    /// </summary>
    private void MigrateLegacyLoginMetadataColumns()
    {
        bool hasLegacyHardwareKey = false;
        bool hasClientDataRevision = false;
        bool hasLegacySecurityState = false;
        bool hasFingerprintSource = false;
        bool hasClientFingerprint = false;

        // Complete the schema read before issuing ALTER TABLE on this same
        // connection; the scoped using makes that ordering visible.
        using (var columnQuery = Cmd("PRAGMA table_info(accounts)"))
        using (var reader = columnQuery.ExecuteReader())
        {
            while (reader.Read())
            {
                string columnName = reader.GetString(1);
                hasLegacyHardwareKey |= columnName.Equals("hw_key", StringComparison.OrdinalIgnoreCase);
                hasClientDataRevision |= columnName.Equals("client_data_revision", StringComparison.OrdinalIgnoreCase);
                hasLegacySecurityState |= columnName.Equals("security_state", StringComparison.OrdinalIgnoreCase);
                hasFingerprintSource |= columnName.Equals("fingerprint_source", StringComparison.OrdinalIgnoreCase);
                hasClientFingerprint |= columnName.Equals("client_fingerprint", StringComparison.OrdinalIgnoreCase);
            }
        }

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

        if (!hasClientFingerprint)
        {
            using var migrate = Cmd("ALTER TABLE accounts ADD COLUMN client_fingerprint BLOB");
            migrate.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Old SQLite databases cannot acquire a new column CHECK through
    /// <c>ALTER TABLE ADD COLUMN</c>. These idempotent triggers provide the
    /// same 24-byte invariant for migrated databases as the fresh schema's
    /// <c>accounts.client_fingerprint</c> CHECK constraint.
    /// </summary>
    private void EnsureCurrentAccountGuards()
    {
        using var command = Cmd("""
            DROP TRIGGER IF EXISTS trg_accounts_touch;

            CREATE TRIGGER trg_accounts_touch
            AFTER UPDATE OF pass_hash, pass_salt, client_data_revision,
                            fingerprint_source, client_fingerprint, cash,
                            is_banned, chat_ban_until ON accounts
            FOR EACH ROW
            BEGIN
                UPDATE accounts SET updated_at = unixepoch() WHERE account_id = NEW.account_id;
            END;

            CREATE TRIGGER IF NOT EXISTS trg_accounts_fingerprint_insert
            BEFORE INSERT ON accounts
            FOR EACH ROW
            WHEN NEW.client_fingerprint IS NOT NULL
                 AND length(NEW.client_fingerprint) != 24
            BEGIN
                SELECT RAISE(ABORT, 'client_fingerprint must be exactly 24 bytes');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_accounts_fingerprint_update
            BEFORE UPDATE OF client_fingerprint ON accounts
            FOR EACH ROW
            WHEN NEW.client_fingerprint IS NOT NULL
                 AND length(NEW.client_fingerprint) != 24
            BEGIN
                SELECT RAISE(ABORT, 'client_fingerprint must be exactly 24 bytes');
            END;
            """);
        command.ExecuteNonQuery();
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
    private const string PasswordHashAlgorithm = "PBKDF2-SHA256";
    private const int PasswordHashIterations = 210_000;
    private const int PasswordSaltByteCount = 16;
    private const int PasswordHashByteCount = 32;
    private const int MinimumAcceptedPasswordIterations = 100_000;
    private const int MaximumAcceptedPasswordIterations = 1_000_000;
    private const int SqliteConstraintErrorCode = 19; // SQLITE_CONSTRAINT primary result code

    public sealed record LoginResult(
        LoginCode Result, long AccountId = 0, long UserId = 0, string Nickname = "",
        int Cash = 0, long GamePoint = 0, int Level = 1, long Exp = 0);

    /// <summary>
    /// Authenticates GL_LOGIN_REQ(682), records its verified metadata, and
    /// provisions an otherwise unknown private-server account on its first
    /// valid login. New credentials use PBKDF2-SHA256; valid legacy SHA-256
    /// rows are upgraded transparently after successful authentication.
    /// </summary>
    public LoginResult Login(
        string accountName,
        string passwordOrToken,
        uint clientDataRevision,
        LoginFingerprintSource fingerprintSource,
        byte[] clientFingerprint,
        string remoteIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentNullException.ThrowIfNull(passwordOrToken);
        ArgumentNullException.ThrowIfNull(clientFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIp);
        if (clientFingerprint.Length != 24)
        {
            throw new ArgumentException("682 fingerprint must contain exactly 24 bytes.", nameof(clientFingerprint));
        }

        lock (_gate)
        {
            var account = FindAccountForLogin(accountName);
            if (account is null)
            {
                long accountId = CreateAccountForFirstLogin(
                    accountName,
                    passwordOrToken,
                    clientDataRevision,
                    fingerprintSource,
                    clientFingerprint,
                    remoteIp);
                if (accountId == 0)
                {
                    // Another process inserted this login name after our lookup.
                    // Do not retry recursively while holding the database lock.
                    return new LoginResult(LoginCode.BadCredentials);
                }

                return new LoginResult(LoginCode.Ok, AccountId: accountId);
            }

            if (account.IsBanned)
            {
                return new LoginResult(LoginCode.Banned);
            }

            PasswordVerification verification = VerifyPassword(
                account.PasswordSalt,
                passwordOrToken,
                account.PasswordHash);
            if (!verification.IsValid)
            {
                return new LoginResult(LoginCode.BadCredentials);
            }

            var result = new LoginResult(
                LoginCode.Ok,
                AccountId: account.AccountId,
                UserId: account.UserId,
                Nickname: account.Nickname,
                Cash: account.Cash,
                GamePoint: account.GamePoint,
                Level: account.Level,
                Exp: account.Exp);
            UpdateSuccessfulLoginMetadata(
                result.AccountId,
                clientDataRevision,
                fingerprintSource,
                clientFingerprint,
                remoteIp,
                upgradeCredentials: verification.RequiresUpgrade ? CreatePasswordCredentials(passwordOrToken) : null);
            return result;
        }
    }

    private AccountForLogin? FindAccountForLogin(string accountName)
    {
        using var command = Cmd("""
            SELECT a.account_id, a.pass_hash, a.pass_salt, a.is_banned, a.cash,
                   u.user_id, u.nickname, u.game_point, u.level, u.exp
            FROM accounts a LEFT JOIN users u ON u.account_id = a.account_id
            WHERE a.login_name = @accountName
            """, ("@accountName", accountName));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AccountForLogin(
            AccountId: reader.GetInt64(0),
            PasswordHash: reader.GetString(1),
            PasswordSalt: reader.GetString(2),
            IsBanned: reader.GetInt64(3) != 0,
            Cash: reader.GetInt32(4),
            UserId: reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
            Nickname: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            GamePoint: reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
            Level: reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
            Exp: reader.IsDBNull(9) ? 0 : reader.GetInt64(9));
    }

    private long CreateAccountForFirstLogin(
        string accountName,
        string passwordOrToken,
        uint clientDataRevision,
        LoginFingerprintSource fingerprintSource,
        byte[] clientFingerprint,
        string remoteIp)
    {
        PasswordCredentials credentials = CreatePasswordCredentials(passwordOrToken);
        using var command = Cmd("""
            INSERT INTO accounts(
                login_name, pass_hash, pass_salt, client_data_revision,
                fingerprint_source, client_fingerprint, last_login_at, last_login_ip)
            VALUES (
                @accountName, @passwordHash, @passwordSalt, @dataRevision,
                @fingerprintSource, @fingerprint, unixepoch(), @remoteIp)
            ON CONFLICT(login_name) DO NOTHING
            RETURNING account_id
            """,
            ("@accountName", accountName),
            ("@passwordHash", credentials.Hash),
            ("@passwordSalt", credentials.Salt),
            ("@dataRevision", (long)clientDataRevision),
            ("@fingerprintSource", (int)fingerprintSource),
            ("@fingerprint", clientFingerprint),
            ("@remoteIp", remoteIp));
        return command.ExecuteScalar() is long accountId ? accountId : 0;
    }

    private void UpdateSuccessfulLoginMetadata(
        long accountId,
        uint clientDataRevision,
        LoginFingerprintSource fingerprintSource,
        byte[] clientFingerprint,
        string remoteIp,
        PasswordCredentials? upgradeCredentials)
    {
        using var command = Cmd("""
            UPDATE accounts
            SET client_data_revision = @dataRevision,
                fingerprint_source = @fingerprintSource,
                client_fingerprint = @fingerprint,
                last_login_at = unixepoch(),
                last_login_ip = @remoteIp,
                pass_hash = COALESCE(@passwordHash, pass_hash),
                pass_salt = COALESCE(@passwordSalt, pass_salt)
            WHERE account_id = @accountId
            """,
            ("@dataRevision", (long)clientDataRevision),
            ("@fingerprintSource", (int)fingerprintSource),
            ("@fingerprint", clientFingerprint),
            ("@remoteIp", remoteIp),
            ("@passwordHash", upgradeCredentials?.Hash),
            ("@passwordSalt", upgradeCredentials?.Salt),
            ("@accountId", accountId));
        command.ExecuteNonQuery();
    }

    private static PasswordCredentials CreatePasswordCredentials(string passwordOrToken)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(PasswordSaltByteCount);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passwordOrToken),
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            PasswordHashByteCount);
        return new PasswordCredentials(
            Convert.ToBase64String(salt),
            $"{PasswordHashAlgorithm}${PasswordHashIterations}${Convert.ToBase64String(hash)}");
    }

    private static PasswordVerification VerifyPassword(
        string storedSalt,
        string passwordOrToken,
        string storedHash)
    {
        string[] hashParts = storedHash.Split('$');
        int iterations = 0;
        bool isPbkdf2Hash = hashParts.Length == 3
            && hashParts[0] == PasswordHashAlgorithm
            && int.TryParse(hashParts[1], out iterations);
        if (isPbkdf2Hash)
        {
            if (iterations < MinimumAcceptedPasswordIterations
                || iterations > MaximumAcceptedPasswordIterations)
            {
                return new PasswordVerification(false, false);
            }

            try
            {
                byte[] salt = Convert.FromBase64String(storedSalt);
                byte[] expectedHash = Convert.FromBase64String(hashParts[2]);
                if (salt.Length < PasswordSaltByteCount || expectedHash.Length != PasswordHashByteCount)
                {
                    return new PasswordVerification(false, false);
                }

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(passwordOrToken),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    PasswordHashByteCount);
                bool isValid = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
                bool needsRehash = isValid && iterations < PasswordHashIterations;
                return new PasswordVerification(isValid, needsRehash);
            }
            catch (FormatException)
            {
                return new PasswordVerification(false, false);
            }
        }

        // The original private-server schema stored SHA256(salt + password) as
        // hexadecimal. Keep it readable only long enough to upgrade a valid
        // legacy row; all new account inserts use PBKDF2 above.
        try
        {
            byte[] expectedHash = Convert.FromHexString(storedHash);
            byte[] actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(storedSalt + passwordOrToken));
            return new PasswordVerification(
                CryptographicOperations.FixedTimeEquals(actualHash, expectedHash),
                RequiresUpgrade: true);
        }
        catch (FormatException)
        {
            return new PasswordVerification(false, false);
        }
    }

    private sealed record AccountForLogin(
        long AccountId,
        string PasswordHash,
        string PasswordSalt,
        bool IsBanned,
        int Cash,
        long UserId,
        string Nickname,
        long GamePoint,
        int Level,
        long Exp);

    private readonly record struct PasswordCredentials(string Salt, string Hash);

    private readonly record struct PasswordVerification(bool IsValid, bool RequiresUpgrade);

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

    /// <summary>
    /// Creates the first player identity for GM_CREATENICK(212). The user row,
    /// trigger-created stats/groups, and starter character commit together so a
    /// failed starter setup never leaves a half-created account identity.
    /// </summary>
    public long CreateNick(long accountId, string nickname)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);

        lock (_gate)
        {
            using var transaction = _conn.BeginTransaction();
            try
            {
                using var userCommand = Cmd(
                    "INSERT INTO users(account_id,nickname) VALUES(@accountId,@nickname) RETURNING user_id",
                    ("@accountId", accountId), ("@nickname", nickname));
                userCommand.Transaction = transaction;
                long userId = (long)userCommand.ExecuteScalar()!;

                // trg_users_bootstrap provides user_stats and all four weapon
                // groups. The explicit character remains server policy, so it
                // belongs in this transaction rather than a post-commit repair.
                using var characterCommand = Cmd(
                    "INSERT INTO characters(user_id,slot_no,char_type) VALUES(@userId,0,1)",
                    ("@userId", userId));
                characterCommand.Transaction = transaction;
                characterCommand.ExecuteNonQuery();

                transaction.Commit();
                return userId;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintErrorCode)
            {
                // This is the normal domain failure path: duplicate nickname,
                // already-created identity, or an invalid account reference.
                // Other SQLite failures must reach the session error log.
                return 0;
            }
        }
    }

    /// <summary>
    /// Records traffic for a registered opcode. Unknown opcodes remain visible
    /// in the router log but do not violate packet_stats' reference-data foreign
    /// key or hide an unrelated SQLite failure.
    /// </summary>
    public void LogPacket(ushort opcode, bool isReceive, int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        int receivedPacketCount = isReceive ? 1 : 0;
        int transmittedPacketCount = isReceive ? 0 : 1;
        int receivedByteCount = isReceive ? bytes : 0;
        int transmittedByteCount = isReceive ? 0 : bytes;

        lock (_gate)
        {
            using var command = Cmd("""
                INSERT INTO packet_stats(day, opcode, rx_count, tx_count, rx_bytes, tx_bytes)
                SELECT date('now'), @opcode, @receivedPacketCount, @transmittedPacketCount,
                       @receivedByteCount, @transmittedByteCount
                WHERE EXISTS (SELECT 1 FROM protocol_packets WHERE opcode = @opcode)
                ON CONFLICT(day, opcode) DO UPDATE SET
                    rx_count = rx_count + @receivedPacketCount,
                    tx_count = tx_count + @transmittedPacketCount,
                    rx_bytes = rx_bytes + @receivedByteCount,
                    tx_bytes = tx_bytes + @transmittedByteCount
                """,
                ("@opcode", (int)opcode),
                ("@receivedPacketCount", receivedPacketCount),
                ("@transmittedPacketCount", transmittedPacketCount),
                ("@receivedByteCount", receivedByteCount),
                ("@transmittedByteCount", transmittedByteCount));
            command.ExecuteNonQuery();
        }
    }
}
