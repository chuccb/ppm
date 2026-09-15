// =============================================================================
// GR_AI_CONTINUE_START_REQ (928) → GR_AI_CONTINUE_START_ACK (929)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AiHandlers
{
    // 928 GR_AI_CONTINUE_START_REQ (sub_761DB0: s32 continue_count)
    // → 929 ACK (sub_761E90): u8 status(1=成功), s32 continue_count
    private static async ValueTask GR_AI_CONTINUE_START_REQ(Session session, Packet packet, ServerContext context)
    {
        int count = packet.Remaining >= 4 ? packet.ReadS32() : 1;
        var ack = new Packet(Opcode.GR_AI_CONTINUE_START_ACK)
            .WriteU8(1)               // status 1 = 成功
            .WriteS32(count);

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
