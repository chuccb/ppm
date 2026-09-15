// =============================================================================
// GR_AI_RECHARGE_MAGAZINE_END_REQ (926) → GR_AI_RECHARGE_MAGAZINE_END_ACK (927)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AIHandlers
{
    // 926 GR_AI_RECHARGE_MAGAZINE_END_REQ (sub_5586B0: u8 slot, u8 team, s8 status)
    // → 927 ACK (sub_558880): 同步給房間內所有玩家
    private static async ValueTask GR_AI_RECHARGE_MAGAZINE_END_REQ(Session session, Packet packet, ServerContext context)
    {
        byte slot = packet.Remaining >= 1 ? packet.ReadU8() : (session.SlotNo ?? 0);
        byte team = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        sbyte status = packet.Remaining >= 1 ? (sbyte)packet.ReadU8() : (sbyte)0;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_RECHARGE_MAGAZINE_END_ACK)
                .WriteU8(slot)
                .WriteU8(team)
                .WriteU8((byte)status);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }
}
