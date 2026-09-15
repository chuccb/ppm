// =============================================================================
// GI_VOICEITEMSLOT_ALL_REQ (793) → GI_VOICEITEMSLOT_ALL_ACK (794)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class VoiceHandlers
{
    // 793 (空) → 794: 全 15 角色語音塊 (count + count×(char_idx + 塊))
    private static async ValueTask GI_VOICEITEMSLOT_ALL_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GI_VOICEITEMSLOT_ALL_ACK).WriteU8(Db.VoiceCharCount);
        for (byte charIdx = 0; charIdx < Db.VoiceCharCount; charIdx++)
        {
            ack.WriteU8(charIdx);
            WriteVoiceBlock(ack, context.Db.GetVoice(session.UserId, charIdx));
        }

        await session.SendAsync(ack);
    }
}
