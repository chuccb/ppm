// =============================================================================
// GT_PING_REQ (101) → GT_PING_ACK (102)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AuthHandlers
{
    // 101 = client response to server-originated 102 (sub_58D6F0). It is not a
    // request/reply pair: replying with 102 here would create a ping loop.
    private static ValueTask GT_PING_REQ(Session session, Packet packet, ServerContext context)
    {
        session.LastPongAt = DateTimeOffset.UtcNow;
        return ValueTask.CompletedTask;
    }
}
