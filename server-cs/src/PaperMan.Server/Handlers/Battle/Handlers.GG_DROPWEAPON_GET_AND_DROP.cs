// =============================================================================
// GG_DROPWEAPON_GET_AND_DROP_REQ (962) → GG_DROPWEAPON_GET_AND_DROP_ACK (963)
// Canonical direct battle-object request entry; authority and state guards are
// retained exactly rather than treated as a relay.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    // sub_566F50 writes u16, u16, u8, u16, u16, f32 without a variable tail.
    private const int GetAndDropRequestLength = 13;
    private static async ValueTask GG_DROPWEAPON_GET_AND_DROP_REQ(Session session, Packet packet, ServerContext context)
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
