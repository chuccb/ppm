// =============================================================================
// Auth opcode registry
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AuthHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GT_PING_REQ, GT_PING_REQ);
        add(Opcode.GL_LOGIN_REQ, GL_LOGIN_REQ);
    }
}
