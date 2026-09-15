// =============================================================================
// GP_PEPACHI_LIST_REQ (702) → GP_PEPACHI_LIST_ACK (703)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 702 is empty. 703 is {s32 start,s32 count,(start+count)×s16}; `{0,0}`
    // is its structurally empty list and never a reward grant.
    private static ValueTask GP_PEPACHI_LIST_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_PEPACHI_LIST_ACK)
            .WriteS32(0)
            .WriteS32(0));
    }
}
