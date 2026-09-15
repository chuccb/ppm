// =============================================================================
// GQ_QUEST_ACCEPT_DAILY_REQ (876) → GQ_QUEST_ACCEPT_DAILY_ACK (877)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    // 876 GQ_QUEST_ACCEPT_DAILY_REQ (sub_91D730, 空) → 877 ACK (sub_91D7E0):
    //   u8 err (0=成功), s32 count (每日任務數), count×13B 快照
    private static async ValueTask GQ_QUEST_ACCEPT_DAILY_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GQ_QUEST_ACCEPT_DAILY_ACK)
            .WriteU8(0)                                     // err 0 = 成功
            .WriteS32(0);                                   // 0 個額外每日任務

        await session.SendAsync(ack);
    }
}
