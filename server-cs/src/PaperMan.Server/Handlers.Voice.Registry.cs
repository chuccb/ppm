// =============================================================================
// Voice opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class VoiceHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_VOICEITEMSLOT_REQ, GL_VOICEITEMSLOT_REQ);
        add(Opcode.GI_VOICEITEMSLOT_ALL_REQ, GI_VOICEITEMSLOT_ALL_REQ);
        add(Opcode.GI_CHANGE_VOICEITEMSLOT_REQ, GI_CHANGE_VOICEITEMSLOT_REQ);
    }
}
