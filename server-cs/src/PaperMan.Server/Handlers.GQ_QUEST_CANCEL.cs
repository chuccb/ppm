// =============================================================================
// GQ_QUEST_CANCEL_REQ (869) → GQ_QUEST_CANCEL_ACK (870)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    // 869 → 870 (sub_91D290): u8 result; !=0 → s32 idx, s32
    private static async ValueTask GQ_QUEST_CANCEL_REQ(Session session, Packet packet, ServerContext context)
    {
        int questIndex = packet.ReadS32();
        bool ok = session.UserId != 0 && context.Db.CancelQuest(session.UserId, questIndex);

        var ack = new Packet(Opcode.GQ_QUEST_CANCEL_ACK);
        if (ok)
        {
            ack.WriteU8(0);
        }
        else
        {
            ack.WriteU8(1)
               .WriteS32(questIndex)
               .WriteS32(0);
        }

        await session.SendAsync(ack);
    }
}
