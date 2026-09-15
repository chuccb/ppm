// =============================================================================
// GG_PNR_RESPON_REQ (746) → GG_PNR_RESPON_ACK (747)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_PNR_RESPON_REQ(Session session, Packet packet, ServerContext context) =>
        RelayRespawnAsync(session, packet, context, Opcode.GG_PNR_RESPON_ACK);
}
