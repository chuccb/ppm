// =============================================================================
// GG_OCC_FAIL_REQ (906) → GG_OCC_FAIL_ACK (907)
// Canonical direct battle-object request entry; authority and state guards are
// retained exactly rather than treated as a relay.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    /// <summary>
    /// 906 → 907。907 的 0 action 分支與 903 相同地讀完整的五欄，然後呼叫
    /// <c>sub_771670(point-1, actorSlot, controllerValue)</c>。
    /// </summary>
    private static async ValueTask GG_OCC_FAIL_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!TryReadAuthorizedRequest(session, packet, context, out Room? room, out OccupyRequest? request))
        {
            return;
        }

        if (!room.BattleState.TryFailOccupy(
            request.PointId, request.ClaimedSlot, request.ClaimedUserId, out var state))
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, CreateStartOrFailAck(Opcode.GG_OCC_FAIL_ACK, state));
    }
}
