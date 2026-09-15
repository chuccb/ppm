// =============================================================================
// GG_TEAMDEADCHAT_REQ (350) → GG_TEAMDEADCHAT_ACK (351)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_TEAMDEADCHAT_REQ(Session session, Packet packet, ServerContext context) =>
        RelayBattleChatAsync(session, packet, context, Opcode.GG_TEAMDEADCHAT_ACK);
}
