// =============================================================================
// GG_LIVECHAT_REQ (344) → GG_LIVECHAT_ACK (345)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    private static ValueTask GG_LIVECHAT_REQ(Session session, Packet packet, ServerContext context) =>
        RelayBattleChatAsync(session, packet, context, Opcode.GG_LIVECHAT_ACK);
}
