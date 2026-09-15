// =============================================================================
// GG_EXERCISERESPON_REQ (455) → GG_EXERCISERESPON_ACK (456)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_EXERCISERESPON_REQ(Session session, Packet packet, ServerContext context) =>
        RelayRespawnAsync(session, packet, context, Opcode.GG_EXERCISERESPON_ACK);
}
