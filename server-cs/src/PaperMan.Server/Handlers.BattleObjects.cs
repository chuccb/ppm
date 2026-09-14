// =============================================================================
// 有狀態戰場物件 handlers。
//
// 這個檔刻意不把 OCC 或地面武器當成「原 payload 換 ACK opcode」的 relay：
// client 端 ACK 會直接修改據點/地面物件表，偽造 slot、uid 或物件 id 會讓房內
// 狀態分歧。每個入口先驗證 room membership、session slot、session user id，再
// 於 Room.BattleState 的 Lock 內完成轉換。
//
// 逐函數證據：docs/PACKETS.md §3.15d3a、docs/LAYOUTS.md 903/905/907/963。
// =============================================================================
using System.Diagnostics.CodeAnalysis;
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>GG_OCC_*（902–908）的權威據點轉換。</summary>
public static class OccupyHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GG_OCC_START_REQ, Start);
        add(Opcode.GG_OCC_SUCC_REQ, Succeed);
        add(Opcode.GG_OCC_FAIL_REQ, Fail);
    }

    // 902 sub_564CF0 / 904 sub_565120 / 906 sub_565470 都寫：
    //   u8 point_id (client field + 1, 因此 wire 值 1..3),
    //   u8 claimed_slot, s32 claimed_user_id (dword_EE8CB4)。
    // claimed identity 是 client 自報欄位，絕不能原樣轉給其他玩家。
    private sealed record OccupyRequest(byte PointId, byte ClaimedSlot, int ClaimedUserId);

    /// <summary>
    /// 902 → 903。903 的 0 action 分支讀
    /// <c>u8 action, u8 point, u8 slot, u8 controllerValue, s32 uid</c>
    /// （<c>sub_564E30 → sub_771490</c>）。
    /// </summary>
    private static async ValueTask Start(Session session, Packet packet, ServerContext context)
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

    /// <summary>
    /// 904 → 905。原版 Occupy client 的 0 action 分支會讀四個 byte，並將後兩個
    /// 作為 <c>sub_771550</c> 的兩個玩家 slot。只有已記錄的 start actor 可以完成；
    /// 第四欄取相同的權威 actor，而非信任 client 自報值。
    /// </summary>
    private static async ValueTask Succeed(Session session, Packet packet, ServerContext context)
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

    /// <summary>
    /// 906 → 907。907 的 0 action 分支與 903 相同地讀完整的五欄，然後呼叫
    /// <c>sub_771670(point-1, actorSlot, controllerValue)</c>。
    /// </summary>
    private static async ValueTask Fail(Session session, Packet packet, ServerContext context)
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

    private static Packet CreateStartOrFailAck(Opcode opcode, OccupyPointSnapshot state) =>
        new Packet(opcode)
            .WriteU8(0)                                     // client action code: start/fail event
            .WriteU8(state.PointId)
            .WriteU8(state.ActorSlot)
            .WriteU8(state.CaptureParticipantCount)
            .WriteS32(state.ActorUserId);

    private static bool TryReadAuthorizedRequest(
        Session session,
        Packet packet,
        ServerContext context,
        [NotNullWhen(true)] out Room? room,
        [NotNullWhen(true)] out OccupyRequest? request)
    {
        room = null;
        request = null;

        // 三個 client builder 都精確寫入 6 bytes；拒絕截斷和未證實的尾隨變體。
        if (packet.Remaining != 6)
        {
            return false;
        }

        if (!BattleRelayHandlers.TryFindRoomSlot(
            session,
            context,
            out Room? sessionRoom,
            out byte actualSlot))
        {
            return false;
        }

        if (!IsOccupyMode(sessionRoom))
        {
            return false;
        }

        byte pointId = packet.ReadU8();
        byte claimedSlot = packet.ReadU8();
        int claimedUserId = packet.ReadS32();

        // dword_EE8CB4 在 902/904/906 builder 中是登入玩家 uid；不接受 slot/uid spoof。
        if (claimedSlot != actualSlot || !MatchesSessionUserId(session, claimedUserId))
        {
            return false;
        }

        room = sessionRoom;
        request = new OccupyRequest(pointId, claimedSlot, claimedUserId);
        return true;
    }

    private static bool IsOccupyMode(Room room) =>
        room.Rule is (byte)GameMode.Occupy or (byte)GameMode.OccupyRenewal;

    private static bool MatchesSessionUserId(Session session, int wireUserId) =>
        session.UserId is >= int.MinValue and <= int.MaxValue
        && session.UserId != 0
        && wireUserId == (int)session.UserId;
}

/// <summary>GG_DROPWEAPON_GET_AND_DROP_REQ（962）的安全失敗回覆。</summary>
public static class DropWeaponHandlers
{
    // sub_566F50 writes u16, u16, u8, u16, u16, f32 without a variable tail.
    private const int GetAndDropRequestLength = 13;

    public static void Register(Registrar add) =>
        add(Opcode.GG_DROPWEAPON_GET_AND_DROP_REQ, GetAndDrop);

    private static async ValueTask GetAndDrop(Session session, Packet packet, ServerContext context)
    {
        // This endpoint has no verified 959/961 object seed or state
        // transition, so framing is the only request validation with an
        // observable effect today. Do not invent ranges for unimplemented
        // object fields.
        if (packet.Remaining != GetAndDropRequestLength)
        {
            return;
        }

        if (!BattleRelayHandlers.TryFindRoomSlot(session, context, out Room? room, out _))
        {
            return;
        }

        if (!room.BattleState.IsMatchActive)
        {
            return;
        }

        // 962's builder and 963's parser establish the wire shape, but 959/961
        // are server-to-client only and no evidenced map-object source can seed
        // a room object table. A fabricated success would make the client
        // replace GroundWeaponId with an object whose position/state is unknown.
        //
        // sub_5672E0 proves only this result behavior: 0 means success and a
        // nonzero value stops further payload parsing. Send the proven nonzero
        // shape to the requester and do not broadcast a nonexistent object.
        await SendRejectedAsync(session);
    }

    /// <summary>963 的 fail 分支只需要首個 nonzero result byte。</summary>
    private static Task SendRejectedAsync(Session session) =>
        session.SendAsync(new Packet(Opcode.GG_DROPWEAPON_GET_AND_DROP_ACK).WriteBool(true));
}
