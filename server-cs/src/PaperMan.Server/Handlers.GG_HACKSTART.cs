// =============================================================================
// GG_HACKSTART_REQ (316) → GG_HACKSTART_ACK (317)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_HACKSTART_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_HACKSTART_ACK, appendSlot: true, armsBomb: false);
}
