// =============================================================================
// GR_AI_GO_NEXT_WAVE_REQ (939) → GR_AI_GO_NEXT_WAVE_ACK (940)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AiHandlers
{
    // 939 GR_AI_GO_NEXT_WAVE_REQ (sub_75CE40: 空)
    // → 940 ACK (sub_7613D0): u8 next_wave, s32 wave_time
    private static async ValueTask GR_AI_GO_NEXT_WAVE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            byte nextWave = 1; // 下一波
            var ack = new Packet(Opcode.GR_AI_GO_NEXT_WAVE_ACK)
                .WriteU8(nextWave)
                .WriteS32(0);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }
}
