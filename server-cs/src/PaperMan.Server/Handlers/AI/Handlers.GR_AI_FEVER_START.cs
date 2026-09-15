// =============================================================================
// GR_AI_FEVER_START_REQ (935) → GR_AI_FEVER_START_ACK (936)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AIHandlers
{
    // 935 GR_AI_FEVER_START_REQ (sub_7622C0: 空)
    // → 936 ACK (sub_7623A0): u8 status(1), u8 unk(0), s32 time(10000), u8 fever_type(1)
    private static async ValueTask GR_AI_FEVER_START_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_FEVER_START_ACK)
                .WriteU8(1)           // status
                .WriteU8(0)
                .WriteS32(10000)      // duration ms
                .WriteU8(1);          // fever mode type
            await RoomManager.BroadcastAsync(room, ack);
        }
    }
}
