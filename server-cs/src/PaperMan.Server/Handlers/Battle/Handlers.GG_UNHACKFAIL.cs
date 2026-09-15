// =============================================================================
// GG_UNHACKFAIL_REQ (330) → GG_UNHACKFAIL_ACK (331)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_UNHACKFAIL_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_UNHACKFAIL_ACK, appendSlot: true, armsBomb: false);
}
