// =============================================================================
// Y_TCP_INF_REQ (165) → Y_TCP_INF_ACK (166)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask Y_TCP_INF_REQ(Session session, Packet packet, ServerContext context) =>
        RelayWithSlotAsync(session, packet, context, Opcode.Y_TCP_INF_ACK);
}
