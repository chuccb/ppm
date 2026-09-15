// =============================================================================
// GQ_QUEST_USER_COMPLETE_HONOR_REQ (878) → GQ_QUEST_USER_COMPLETE_HONOR_ACK (879)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    // 878 GQ_QUEST_USER_COMPLETE_HONOR_REQ (sub_91C9D0: s8 flag) → 879 ACK (sub_91CAA0):
    //   u8 err (0=成功), str title, raw payload
    private static async ValueTask GQ_QUEST_USER_COMPLETE_HONOR_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;

        var ack = new Packet(Opcode.GQ_QUEST_USER_COMPLETE_HONOR_ACK)
            .WriteU8(0)                                     // err 0 = 成功
            .WriteStr("Honor");

        await session.SendAsync(ack);
    }
}
