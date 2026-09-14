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

    // Native maps 0x402FF0/0x4030A0/0x403150/0x403200/0x4032B0 complete a
    // 19,900,001..19,900,015 body into its six-piece normal appearance.
    // The first six persisted words retain native ordinal order despite their
    // pre-existing SQL names: body, head, face, top, bottom, shoes.
    private const byte FirstCanonicalCharacterType = 1;
    private const byte LastCanonicalCharacterType = 15;
    private const int CharacterBodyItemBase = 19_900_000;

    internal readonly record struct CanonicalStarterAppearance(
        ushort BodyOffset, ushort HeadOffset, ushort FaceOffset, ushort TopOffset,
        ushort BottomOffset, ushort ShoesOffset)
    {
        public int BodyItemId => CharacterBodyItemBase + BodyOffset;
        public int HeadItemId => 10_000_000 + HeadOffset;
        public int FaceItemId => 10_100_000 + FaceOffset;
        public int TopItemId => 10_200_000 + TopOffset;
        public int BottomItemId => 10_300_000 + BottomOffset;
        public int ShoesItemId => 10_400_000 + ShoesOffset;
    }

    // Extracted directly from the five native body-template switch tables.
    // Keep this compact raw-offset form because 198/247 persistence uses u16
    // category-relative values, while 311 expands the same values to full IDs.
    private static readonly CanonicalStarterAppearance[] CanonicalStarterAppearances =
    [
        new(1, 1, 1, 1, 1, 1),
        new(2, 15, 10, 22, 12, 12),
        new(3, 28, 19, 45, 25, 24),
        new(4, 41, 28, 66, 36, 41),
        new(5, 55, 37, 90, 47, 52),
        new(6, 123, 111, 157, 99, 105),
        new(7, 124, 112, 167, 109, 115),
        new(8, 125, 113, 177, 119, 125),
        new(9, 126, 114, 187, 129, 135),
        new(10, 127, 115, 197, 139, 145),
        new(11, 1096, 839, 1069, 974, 952),
        new(12, 1428, 865, 1205, 1069, 1009),
        new(13, 1600, 866, 1213, 1072, 1012),
        new(14, 792, 385, 428, 376, 360),
        new(15, 30220, 920, 10011, 10011, 10114),
    ];

    internal static bool IsCanonicalCharacterType(int charType) =>
        charType is >= FirstCanonicalCharacterType and <= LastCanonicalCharacterType;

    internal static bool TryGetCanonicalCharacterType(int bodyItemId, out byte charType)
    {
        if (bodyItemId is >= CharacterBodyItemBase + FirstCanonicalCharacterType
            and <= CharacterBodyItemBase + LastCanonicalCharacterType)
        {
            charType = (byte)(bodyItemId - CharacterBodyItemBase);
            return true;
        }

        charType = 0;
        return false;
    }

    internal static CanonicalStarterAppearance GetCanonicalStarterAppearance(byte charType)
    {
        if (!IsCanonicalCharacterType(charType))
        {
            throw new ArgumentOutOfRangeException(nameof(charType));
        }

        return CanonicalStarterAppearances[charType - FirstCanonicalCharacterType];
    }

    public sealed record LoginResult(
        LoginCode Result, long AccountId = 0, long UserId = 0, string Nickname = "",
        int Cash = 0, long GamePoint = 0, int Level = 1, long Exp = 0);

    /// <summary>
    /// Authenticates GL_LOGIN_REQ(682), records its verified metadata, and
    /// ensures that every accepted account has a playable identity. The client
    /// requests 197→198 immediately after entering a channel; a success 681
    /// with no user row makes that native 198 reader show its database error.
    /// New credentials use PBKDF2-SHA256; valid legacy SHA-256 rows are
    /// upgraded transparently after successful authentication.
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
            AccountForLogin? account = FindAccountForLogin(accountName);
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

                account = FindAccountForLogin(accountName);
                if (account is null)
                {
                    return new LoginResult(LoginCode.Unavailable);
                }

                return CreateSuccessfulLoginResult(account, accountName);
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

            UpdateSuccessfulLoginMetadata(
                account.AccountId,
                clientDataRevision,
                fingerprintSource,
                clientFingerprint,
                remoteIp,
                upgradeCredentials: verification.RequiresUpgrade ? CreatePasswordCredentials(passwordOrToken) : null);
            return CreateSuccessfulLoginResult(account, accountName);
        }
    }

    /// <summary>
    /// Supplies the user id and nickname required by successful 681/143/197
    /// flow. A prior account-only auto-registration is repaired here after its
    /// password has been verified.
    /// </summary>
    private LoginResult CreateSuccessfulLoginResult(AccountForLogin account, string accountName)
    {
        PlayerIdentity? identity = EnsurePlayerIdentity(account, accountName);
        if (identity is null)
        {
            return new LoginResult(LoginCode.Unavailable);
        }

        return new LoginResult(
            LoginCode.Ok,
            AccountId: account.AccountId,
            UserId: identity.UserId,
            Nickname: identity.Nickname,
            Cash: account.Cash,
            GamePoint: account.GamePoint,
            Level: account.Level,
            Exp: account.Exp);
    }

    /// <summary>
    /// The client trace proves it does not ask to create a nickname before its
    /// first 197. Create an explicit private-server starter identity instead of
    /// returning a success 681 whose 198 cannot be consumed.
    /// </summary>
    private PlayerIdentity? EnsurePlayerIdentity(AccountForLogin account, string accountName)
    {
        if (account.UserId > 0 && account.Nickname.Length > 0)
        {
            return EnsurePlayableCharacterStateUnlocked(account.UserId)
                ? new PlayerIdentity(account.UserId, account.Nickname)
                : null;
        }

        string generatedNickname = CreateGeneratedNickname(account.AccountId);
        string initialNickname = IsNativeNicknameLength(accountName)
            ? accountName
            : generatedNickname;

        long userId = CreateNickUnlocked(account.AccountId, initialNickname);
        if (userId > 0)
        {
            return new PlayerIdentity(userId, initialNickname);
        }

        // A manually created player can already use the login-name nickname.
        // The account-id form is deterministic, compact, and unique across
        // ordinary first-login accounts.
        if (initialNickname == generatedNickname)
        {
            return null;
        }

        userId = CreateNickUnlocked(account.AccountId, generatedNickname);
        return userId > 0 ? new PlayerIdentity(userId, generatedNickname) : null;
    }

    /// <summary>
    /// Repairs only missing words in a valid canonical body template. The six
    /// columns are the native normal-order prefix (body, head, face, top,
    /// bottom, shoes), not their old SQL labels. A noncanonical nonzero body is
    /// historical state with no evidence-backed replacement, so it is untouched.
    /// Every nonzero equipment word is preserved.
    /// </summary>
    private bool EnsurePlayableCharacterStateUnlocked(long userId)
    {
        using var transaction = _conn.BeginTransaction();

        var incompleteCanonicalSlots = new List<(int SlotNo, byte CharacterType)>();
        using (var findIncompleteSlots = Cmd("""
            SELECT slot_no, char_type
            FROM characters
            WHERE user_id = @userId
              AND char_type BETWEEN @firstType AND @lastType
              AND (eq_primary = 0 OR eq_primary = char_type)
              AND (eq_primary = 0 OR eq_secondary = 0 OR eq_melee = 0
                   OR eq_grenade = 0 OR eq_head = 0 OR eq_face = 0)
            """,
            ("@userId", userId),
            ("@firstType", (int)FirstCanonicalCharacterType),
            ("@lastType", (int)LastCanonicalCharacterType)))
        {
            findIncompleteSlots.Transaction = transaction;
            using var reader = findIncompleteSlots.ExecuteReader();
            while (reader.Read())
            {
                incompleteCanonicalSlots.Add((reader.GetInt32(0), (byte)reader.GetInt32(1)));
            }
        }

        foreach (var (slotNo, charType) in incompleteCanonicalSlots)
        {
            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(charType);
            using var repairSlot = Cmd("""
                UPDATE characters
                SET eq_primary = CASE WHEN eq_primary = 0 THEN @body ELSE eq_primary END,
                    eq_secondary = CASE WHEN eq_secondary = 0 THEN @head ELSE eq_secondary END,
                    eq_melee = CASE WHEN eq_melee = 0 THEN @face ELSE eq_melee END,
                    eq_grenade = CASE WHEN eq_grenade = 0 THEN @top ELSE eq_grenade END,
                    eq_head = CASE WHEN eq_head = 0 THEN @bottom ELSE eq_head END,
                    eq_face = CASE WHEN eq_face = 0 THEN @shoes ELSE eq_face END
                WHERE user_id = @userId AND slot_no = @slotNo
                """,
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset),
                ("@userId", userId),
                ("@slotNo", slotNo));
            repairSlot.Transaction = transaction;
            if (repairSlot.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }
        }

        int currentCharacterIndex;
        using (var currentCharacter = Cmd(
            "SELECT current_char FROM users WHERE user_id=@userId", ("@userId", userId)))
        {
            currentCharacter.Transaction = transaction;
            object? value = currentCharacter.ExecuteScalar();
            if (value is null)
            {
                transaction.Rollback();
                return false;
            }

            currentCharacterIndex = Convert.ToInt32(value);
        }

        var occupiedSlots = new bool[20];
        var playablePositions = new List<bool>();
        using (var characters = Cmd("""
            SELECT slot_no, char_type, eq_primary
            FROM characters
            WHERE user_id=@userId
            ORDER BY slot_no
            """, ("@userId", userId)))
        {
            characters.Transaction = transaction;
            using var reader = characters.ExecuteReader();
            while (reader.Read())
            {
                int slotNo = reader.GetInt32(0);
                int charType = reader.GetInt32(1);
                int bodyOffset = reader.GetInt32(2);
                occupiedSlots[slotNo] = true;
                playablePositions.Add(
                    IsCanonicalCharacterType(charType) && IsCanonicalCharacterType(bodyOffset));
            }
        }

        int firstPlayablePosition = playablePositions.FindIndex(playable => playable);
        if (firstPlayablePosition < 0)
        {
            int emptySlot = Array.FindIndex(occupiedSlots, occupied => !occupied);
            if (emptySlot < 0)
            {
                transaction.Rollback();
                return false;
            }

            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(FirstCanonicalCharacterType);
            using var addStarter = Cmd("""
                INSERT INTO characters(
                    user_id, slot_no, char_type,
                    eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                VALUES(
                    @userId, @slotNo, @charType,
                    @body, @head, @face, @top, @bottom, @shoes)
                """,
                ("@userId", userId),
                ("@slotNo", emptySlot),
                ("@charType", (int)FirstCanonicalCharacterType),
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset));
            addStarter.Transaction = transaction;
            if (addStarter.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }

            // 198 omits physical slot numbers. It selects the sorted character
            // record by position, so account for any preserved legacy rows.
            firstPlayablePosition = occupiedSlots.Take(emptySlot).Count(occupied => occupied);
            playablePositions.Add(true);
        }

        bool currentCharacterIsPlayable = currentCharacterIndex >= 0
            && currentCharacterIndex < playablePositions.Count
            && playablePositions[currentCharacterIndex];
        if (!currentCharacterIsPlayable)
        {
            using var selectPlayableCharacter = Cmd("""
                UPDATE users
                SET current_char=@characterIndex, updated_at=unixepoch()
                WHERE user_id=@userId
                """,
                ("@characterIndex", firstPlayablePosition),
                ("@userId", userId));
            selectPlayableCharacter.Transaction = transaction;
            if (selectPlayableCharacter.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }
        }

        transaction.Commit();
        return true;
    }

    private static bool IsNativeNicknameLength(string nickname) =>
        Packet.Ansi.GetByteCount(nickname) is >= 2 and <= 16;

    /// <summary>
    /// Encodes every positive Int64 account id in base-36, keeping the `P`
    /// prefix plus worst-case value within the client's 2..16-byte name input
    /// limit.
    /// </summary>
    private static string CreateGeneratedNickname(long accountId)
    {
        const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<char> characters = stackalloc char[13];
        ulong value = (ulong)accountId;
        int firstCharacter = characters.Length;
        do
        {
            characters[--firstCharacter] = Digits[(int)(value % 36)];
            value /= 36;
        }
        while (value > 0);

        return "P" + new string(characters[firstCharacter..]);
    }

    private sealed record PlayerIdentity(long UserId, string Nickname);

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
            return CreateNickUnlocked(accountId, nickname);
        }
    }

    /// <summary>
    /// Creates a user, its trigger-provided stats/groups, and starter character
    /// in one transaction. Callers that already hold the database gate use this
    /// rather than attempting a nested lock.
    /// </summary>
    private long CreateNickUnlocked(long accountId, string nickname)
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
            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(FirstCanonicalCharacterType);
            using var characterCommand = Cmd("""
                INSERT INTO characters(
                    user_id, slot_no, char_type,
                    eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                VALUES(
                    @userId, 0, @charType,
                    @body, @head, @face, @top, @bottom, @shoes)
                """,
                ("@userId", userId),
                ("@charType", (int)FirstCanonicalCharacterType),
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset));
            characterCommand.Transaction = transaction;
            if (characterCommand.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return 0;
            }

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
