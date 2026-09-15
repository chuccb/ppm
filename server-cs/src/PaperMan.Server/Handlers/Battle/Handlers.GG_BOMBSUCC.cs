// =============================================================================
// GG_BOMBSUCC_REQ (322) → GG_BOMBSUCC_ACK (323)
// Canonical direct request entry. Its shared relay support preserves the existing
// room/slot guard and packet-family-specific wire behavior.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    // 炸彈屬哪隊。team 由 316/318 記下; 未植彈即收到 322 → 依「未確認
    // 不硬編」原則忽略 (無炸彈可爆)。
    private static async ValueTask GG_BOMBSUCC_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!TryFindRoomSlot(session, context, out var room, out _) || room.BombTeam is not { } team)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_BOMBSUCC_ACK).WriteU8(team));
    }
}
