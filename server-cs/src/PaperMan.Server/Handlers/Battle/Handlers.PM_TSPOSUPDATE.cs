// =============================================================================
// PM_TSPOSUPDATE_REQ (271) → PM_TSPOSUPDATE_ACK (272)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask PM_TSPOSUPDATE_REQ(Session session, Packet packet, ServerContext context) =>
        RelayWithSlotAsync(session, packet, context, Opcode.PM_TSPOSUPDATE_ACK);
}
