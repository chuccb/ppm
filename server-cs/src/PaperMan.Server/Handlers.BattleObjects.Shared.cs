// =============================================================================
// GG_OCC shared authority support
// No receive entry: exact framing, room/slot/user identity and mode checks are
// common to all three canonical OCC request families.
// =============================================================================
using System.Diagnostics.CodeAnalysis;
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    // 902 sub_564CF0 / 904 sub_565120 / 906 sub_565470 都寫：
    //   u8 point_id (client field + 1, 因此 wire 值 1..3),
    //   u8 claimed_slot, s32 claimed_user_id (dword_EE8CB4)。
    // claimed identity 是 client 自報欄位，絕不能原樣轉給其他玩家。
    private sealed record OccupyRequest(byte PointId, byte ClaimedSlot, int ClaimedUserId);
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
