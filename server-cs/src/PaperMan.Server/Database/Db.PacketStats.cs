// =============================================================================
// Registered protocol traffic counters.
//
// Unknown opcodes remain router diagnostics rather than foreign-key-invalid
// packet_stats rows. This is operational accounting, not packet behavior.
// =============================================================================
namespace PaperMan.Server;

public sealed partial class Db
{
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
    }}
