// =============================================================================
// GG_GET_BALL_REQ (964) → GG_GET_BALL_ACK (965)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_GET_BALL_REQ(Session session, Packet packet, ServerContext context) =>
        RelaySoccerEventAsync(session, packet, context, Opcode.GG_GET_BALL_ACK);
}
