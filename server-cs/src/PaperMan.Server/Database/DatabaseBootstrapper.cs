// =============================================================================
// First-run SQLite bootstrap.
//
// schema.sql and packets.tsv are embedded in PaperMan.Server.csproj, so a
// published server can create a complete database without requiring Python,
// repository-relative files, or command-line setup steps.
// =============================================================================
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

/// <summary>
/// Applies the embedded schema and seeds static protocol/operations reference
/// data. Every write is idempotent: existing player and administrator data is
/// never replaced during normal server startup.
/// </summary>
internal static class DatabaseBootstrapper
{
    private const string SchemaResourceSuffix = ".Database.schema.sql";
    private const string PacketCatalogResourceSuffix = ".Database.packets.tsv";

    private static readonly ServerSetting[] DefaultServerSettings =
    [
        new("login_enabled", "1"),
        new("event_exp_rate", "100"),
        new("event_page", "0"),
        new("billing_disabled", "0"),
        new("gms_disabled", "0"),
        new("packet_delay_allow_sec", "5"),
        new("compress_threshold", "9600"),
        new("protocol_base", "100"),
        new("max_rooms", "210"),
        new("max_room_players", "10"),
        new("max_inventory_slots", "5120"),
        new("inventory_page_size", "100"),
        new("max_characters", "20"),
        new("weapon_groups", "4"),
        new("skill_slots", "9"),
        new("new_skill_profile_count", "5"),
        new("new_skill_puzzle_slots", "7"),
        new("schema_version", "1"),
    ];

    /// <summary>Creates missing tables, then seeds only stable reference rows.</summary>
    public static int EnsureCurrent(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // schema.sql is also the standalone Python tool's source of truth and
        // deliberately starts with connection PRAGMAs. SQLite forbids changing
        // synchronous mode inside a transaction, so apply that script first,
        // then make its static reference data one atomic seed transaction.
        ExecuteSchema(connection);
        MigrateLegacyRoomModeIndexColumns(connection);
        var packetDefinitions = ReadPacketDefinitions();
        SeedReferenceData(connection, packetDefinitions);
        return packetDefinitions.Count;
    }

    private static void ExecuteSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedText(SchemaResourceSuffix);
        command.ExecuteNonQuery();
    }

    // `rule` was a local shorthand for the client u8 modeIndex. Preserve values
    // in existing standalone databases while aligning the schema with the wire
    // field; no original-service persistence policy is implied.
    private static void MigrateLegacyRoomModeIndexColumns(SqliteConnection connection)
    {
        RenameColumnIfPresent(connection, "rooms", "rule", "mode_index");
        RenameColumnIfPresent(connection, "match_results", "rule", "mode_index");
    }

    private static void RenameColumnIfPresent(
        SqliteConnection connection,
        string table,
        string oldColumn,
        string newColumn)
    {
        if (!HasColumn(connection, table, oldColumn))
        {
            return;
        }

        if (HasColumn(connection, table, newColumn))
        {
            throw new InvalidOperationException(
                $"Cannot rename {table}.{oldColumn}: {table}.{newColumn} already exists.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} RENAME COLUMN {oldColumn} TO {newColumn};";
        command.ExecuteNonQuery();
    }

    private static bool HasColumn(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void SeedReferenceData(
        SqliteConnection connection,
        IReadOnlyList<ProtocolPacketDefinition> packetDefinitions)
    {
        using var transaction = connection.BeginTransaction();
        UpsertProtocolPacketDefinitions(connection, transaction, packetDefinitions);
        InsertDefaultServerSettings(connection, transaction);
        transaction.Commit();
    }

    private static void UpsertProtocolPacketDefinitions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<ProtocolPacketDefinition> packetDefinitions)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO protocol_packets(opcode, name, direction, subsystem)
            VALUES (@opcode, @name, @direction, @subsystem)
            ON CONFLICT(opcode) DO UPDATE SET
                name = excluded.name,
                direction = excluded.direction,
                subsystem = excluded.subsystem
            """;
        var opcode = command.Parameters.Add("@opcode", SqliteType.Integer);
        var name = command.Parameters.Add("@name", SqliteType.Text);
        var direction = command.Parameters.Add("@direction", SqliteType.Text);
        var subsystem = command.Parameters.Add("@subsystem", SqliteType.Text);

        foreach (var definition in packetDefinitions)
        {
            opcode.Value = (int)definition.Opcode;
            name.Value = definition.Name;
            direction.Value = definition.Direction;
            subsystem.Value = definition.Subsystem;
            command.ExecuteNonQuery();
        }
    }

    private static void InsertDefaultServerSettings(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO server_config(key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO NOTHING
            """;
        var key = command.Parameters.Add("@key", SqliteType.Text);
        var value = command.Parameters.Add("@value", SqliteType.Text);

        foreach (var setting in DefaultServerSettings)
        {
            key.Value = setting.Key;
            value.Value = setting.Value;
            command.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<ProtocolPacketDefinition> ReadPacketDefinitions()
    {
        string catalog = ReadEmbeddedText(PacketCatalogResourceSuffix);
        var definitions = new List<ProtocolPacketDefinition>();
        var seenOpcodes = new HashSet<ushort>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        using var reader = new StringReader(catalog);
        for (int lineNumber = 1; reader.ReadLine() is { } line; lineNumber++)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int separator = line.IndexOf('\t');
            if (separator <= 0 || separator == line.Length - 1)
            {
                throw new InvalidDataException($"Embedded packets.tsv line {lineNumber} is not opcode<TAB>name.");
            }

            ReadOnlySpan<char> opcodeText = line.AsSpan(0, separator);
            string name = line[(separator + 1)..].Trim();
            if (!ushort.TryParse(opcodeText, out ushort opcode)
                || opcode < 100
                || name.Length == 0
                || !seenOpcodes.Add(opcode)
                || !seenNames.Add(name))
            {
                throw new InvalidDataException($"Embedded packets.tsv line {lineNumber} is invalid or duplicates a packet.");
            }

            definitions.Add(new ProtocolPacketDefinition(
                opcode,
                name,
                GetPacketDirection(name),
                GetSubsystem(name)));
        }

        if (definitions.Count == 0)
        {
            throw new InvalidDataException("Embedded packets.tsv does not contain any packet definitions.");
        }

        return definitions;
    }

    private static string ReadEmbeddedText(string resourceSuffix)
    {
        Assembly assembly = typeof(DatabaseBootstrapper).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing embedded database resource ending with '{resourceSuffix}'.");
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open embedded database resource '{resourceName}'.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string GetPacketDirection(string packetName)
    {
        if (packetName.EndsWith("_REQ", StringComparison.Ordinal))
        {
            return "C2S";
        }

        if (packetName.EndsWith("_ACK", StringComparison.Ordinal)
            || packetName.EndsWith("_NOTIFY", StringComparison.Ordinal)
            || packetName.EndsWith("_NOTICE", StringComparison.Ordinal)
            || packetName.EndsWith("_INF", StringComparison.Ordinal))
        {
            return "S2C";
        }

        return "BOTH";
    }

    private static string GetSubsystem(string packetName)
    {
        string prefix = packetName.Split('_', 2)[0];
        return prefix switch
        {
            "GT" => "transport",
            "GE" => "logout",
            "GL" => "lobby",
            "GR" => "room",
            "GG" => "ingame",
            "GS" => "shop",
            "GP" => "stats/pachinko",
            "GI" => "inventory",
            "GM" => "nickname",
            "GC" => "channel/clan",
            "GQ" => "quest",
            "GV" => "gm-viewer",
            "GX" => "xigncode",
            "PM" => "p2p-master",
            "UDP" or "Y" => "nat",
            "TCP" => "nat",
            "MASTER" => "ops",
            "SECURITY" => "anticheat",
            _ => prefix.ToLowerInvariant(),
        };
    }

    private readonly record struct ProtocolPacketDefinition(
        ushort Opcode,
        string Name,
        string Direction,
        string Subsystem);

    private readonly record struct ServerSetting(string Key, string Value);
}
