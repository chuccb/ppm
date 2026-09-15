// =============================================================================
// Account authentication and credential storage.
//
// GL_LOGIN_REQ(682) supplies client metadata; this code records its verified
// shape but does not treat it as an original-service authentication policy.
// Password migration is explicit so valid legacy rows upgrade only after a
// successful check.
// =============================================================================
using System.Security.Cryptography;
using System.Text;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed partial class Db
{
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

}
