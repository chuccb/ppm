// =============================================================================
// GL_JOIN opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_JOIN_REQ, GL_JOIN_REQ);
        add(Opcode.GL_JOINPASS_REQ, GL_JOINPASS_REQ);
        add(Opcode.GL_JOININFO_REQ, GL_JOININFO_REQ);
        add(Opcode.GL_JOINGAME_REQ, GL_JOINGAME_REQ);
        add(Opcode.GL_JOINPLAY_REQ, GL_JOINPLAY_REQ);
    }
}
