// =============================================================================
// GL_VOICEITEMSLOT_REQ (791) → GL_VOICEITEMSLOT_ACK (792)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class VoiceHandlers
{
    // 791 (空) → 792: 目前角色語音塊
    private static async ValueTask GL_VOICEITEMSLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        var voice = context.Db.GetVoice(session.UserId, context.Db.GetCurrentVoiceChar(session.UserId));

        var ack = new Packet(Opcode.GL_VOICEITEMSLOT_ACK).WriteU8(voice.CharIdx);
        WriteVoiceBlock(ack, voice);
        await session.SendAsync(ack);
    }
}
