// =============================================================================
// GR_AI_RECHARGE_MAGAZINE_START_REQ (924) → GR_AI_RECHARGE_MAGAZINE_START_ACK (925)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AIHandlers
{
    // 924 GR_AI_RECHARGE_MAGAZINE_START_REQ (sub_558350: u8 slot, u8 team, u8 unk)
    // → 925 ACK (sub_558550): 同步給房間內所有玩家
    private static async ValueTask GR_AI_RECHARGE_MAGAZINE_START_REQ(Session session, Packet packet, ServerContext context)
    {
        byte slot = packet.Remaining >= 1 ? packet.ReadU8() : (session.SlotNo ?? 0);
        byte team = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        byte unk = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_RECHARGE_MAGAZINE_START_ACK)
                .WriteU8(slot)
                .WriteU8(team)
                .WriteU8(unk);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }
}
