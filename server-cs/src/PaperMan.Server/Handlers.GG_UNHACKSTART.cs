// =============================================================================
// GG_UNHACKSTART_REQ (326) → GG_UNHACKSTART_ACK (327)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_UNHACKSTART_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_UNHACKSTART_ACK, appendSlot: true, armsBomb: false);
}
