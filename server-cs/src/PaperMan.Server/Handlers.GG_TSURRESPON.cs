// =============================================================================
// GG_TSURRESPON_REQ (360) → GG_TSURRESPON_ACK (361)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_TSURRESPON_REQ(Session session, Packet packet, ServerContext context) =>
        RelayRespawnAsync(session, packet, context, Opcode.GG_TSURRESPON_ACK);
}
