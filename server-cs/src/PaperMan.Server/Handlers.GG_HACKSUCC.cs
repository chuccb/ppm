// =============================================================================
// GG_HACKSUCC_REQ (318) → GG_HACKSUCC_ACK (319)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_HACKSUCC_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_HACKSUCC_ACK, appendSlot: true, armsBomb: true);
}
