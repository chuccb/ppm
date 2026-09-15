// =============================================================================
// GG_UNHACKSUCC_REQ (328) → GG_UNHACKSUCC_ACK (329)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_UNHACKSUCC_REQ(Session session, Packet packet, ServerContext context) =>
        RelayHackAsync(session, packet, context, Opcode.GG_UNHACKSUCC_ACK, appendSlot: true, armsBomb: false);
}
