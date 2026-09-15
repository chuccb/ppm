// =============================================================================
// GR_AI_GET_REWARD_ITEM_REQ (918) → GR_AI_GET_REWARD_ITEM_ACK (919)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AiHandlers
{
    // 918 GR_AI_GET_REWARD_ITEM_REQ (sub_761AC0: u8 reward_idx)
    // → 919 ACK (sub_761B20): u8 idx, u8 status(0=成功), s32 item_id, u8 slot, s32 count, u8 flag
    private static async ValueTask GR_AI_GET_REWARD_ITEM_REQ(Session session, Packet packet, ServerContext context)
    {
        byte idx = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        int rewardItemId = 10001; // 預設獎勵道具
        int count = 1;
        byte slot = session.SlotNo ?? 0;

        var ack = new Packet(Opcode.GR_AI_GET_REWARD_ITEM_ACK)
            .WriteU8(idx)
            .WriteU8(0)                  // 0 = 成功
            .WriteS32(rewardItemId)
            .WriteU8(slot)
            .WriteS32(count)
            .WriteU8(0);

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            await RoomManager.BroadcastAsync(room, ack);
        }
        else
        {
            await session.SendAsync(ack);
        }
    }
}
