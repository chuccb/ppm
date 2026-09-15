// =============================================================================
// MASTER_KILLALL_REQ (416) → unnamed opcode 417
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 416 MASTER_KILLALL_REQ (空) → 全服踢除
    private static ValueTask MASTER_KILLALL_REQ(Session session, Packet packet, ServerContext context)
    {
        foreach (var s in context.Sessions.All)
        {
            if (!ReferenceEquals(s, session))
            {
                s.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }
}
