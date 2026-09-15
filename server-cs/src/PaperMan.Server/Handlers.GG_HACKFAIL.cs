// =============================================================================
// GG_HACKFAIL_REQ (320) → GG_HACKFAIL_ACK (321)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_HACKFAIL_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_HACKFAIL_ACK, appendSlot: false, armsBomb: false);
}
