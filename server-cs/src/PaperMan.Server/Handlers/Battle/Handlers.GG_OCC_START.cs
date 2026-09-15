// =============================================================================
// GG_OCC_START_REQ (902) → GG_OCC_START_ACK (903)
// Canonical direct battle-object request entry; authority and state guards are
// retained exactly rather than treated as a relay.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    /// <summary>
    /// 902 → 903。903 的 0 action 分支讀
    /// <c>u8 action, u8 point, u8 slot, u8 controllerValue, s32 uid</c>
    /// （<c>sub_564E30 → sub_771490</c>）。
    /// </summary>
    private static async ValueTask GG_OCC_START_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!TryReadAuthorizedRequest(session, packet, context, out Room? room, out OccupyRequest? request))
        {
            return;
        }

        if (!room.BattleState.TryStartOccupy(
            request.PointId,
            request.ClaimedSlot,
            request.ClaimedUserId,
            out var state,
            out bool stateChanged))
        {
            return;
        }

        var ack = CreateStartOrFailAck(Opcode.GG_OCC_START_ACK, state);
        if (stateChanged)
        {
            await RoomManager.BroadcastAsync(room, ack);
        }
        else
        {
            await session.SendAsync(ack);                    // 只補給重送者，不重播 UI event
        }
    }
}
