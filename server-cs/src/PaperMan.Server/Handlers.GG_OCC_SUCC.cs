// =============================================================================
// GG_OCC_SUCC_REQ (904) → GG_OCC_SUCC_ACK (905)
// Canonical direct battle-object request entry; authority and state guards are
// retained exactly rather than treated as a relay.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    /// <summary>
    /// 904 → 905。原版 Occupy client 的 0 action 分支會讀四個 byte，並將後兩個
    /// 作為 <c>sub_771550</c> 的兩個玩家 slot。只有已記錄的 start actor 可以完成；
    /// 第四欄取相同的權威 actor，而非信任 client 自報值。
    /// </summary>
    private static async ValueTask GG_OCC_SUCC_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!TryReadAuthorizedRequest(session, packet, context, out Room? room, out OccupyRequest? request))
        {
            return;
        }

        if (!room.BattleState.TryCompleteOccupy(
            request.PointId,
            request.ClaimedSlot,
            request.ClaimedUserId,
            out var state,
            out bool stateChanged))
        {
            return;
        }

        var ack = new Packet(Opcode.GG_OCC_SUCC_ACK)
            .WriteU8(0)                                     // sub_565230 的成功 action 分支
            .WriteU8(state.PointId)
            .WriteU8(state.ActorSlot)
            .WriteU8(state.ActorSlot);                      // 兩個 slot 均由同一 OCC actor 產生
        if (stateChanged)
        {
            await RoomManager.BroadcastAsync(room, ack);
        }
        else
        {
            await session.SendAsync(ack);                    // 只補給重送者，不重播成功效果
        }
    }
}
