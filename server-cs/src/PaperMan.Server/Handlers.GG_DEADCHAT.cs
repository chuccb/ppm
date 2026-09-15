// =============================================================================
// GG_DEADCHAT_REQ (348) → GG_DEADCHAT_ACK (349)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_DEADCHAT_REQ(Session session, Packet packet, ServerContext context) =>
        RelayBattleChatAsync(session, packet, context, Opcode.GG_DEADCHAT_ACK);
}
