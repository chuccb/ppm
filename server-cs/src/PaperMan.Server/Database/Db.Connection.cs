// =============================================================================
// SQLite connection, first-run initialization, and migration boundary.
//
// This file owns the single connection/gate and the idempotent startup work.
// Persisted feature operations live in Db.* files; packet handlers never open
// their own SQLite connection.
// =============================================================================
using Microsoft.Data.Sqlite;

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

    /// <summary>
    /// Reads a mandatory integer returned by a SQLite <c>RETURNING</c> command.
    /// A missing row is a database invariant failure, not a zero-valued domain
    /// result; callers keep their transaction active so its disposal rolls back.
    /// </summary>
    private static long ReadRequiredReturnedInt64(SqliteCommand command, string operation)
    {
        object? scalar = command.ExecuteScalar();
        if (scalar is null or DBNull)
        {
            throw new InvalidOperationException($"{operation} did not return its required integer value.");
        }

        return Convert.ToInt64(scalar);
    }

}
