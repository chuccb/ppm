// =============================================================================
// GG_TEAMCHAT_REQ (346) → GG_TEAMCHAT_ACK (347)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_TEAMCHAT_REQ(Session session, Packet packet, ServerContext context) =>
        RelayBattleChatAsync(session, packet, context, Opcode.GG_TEAMCHAT_ACK);
}
