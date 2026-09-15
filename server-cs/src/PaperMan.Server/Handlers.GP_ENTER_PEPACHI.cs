// =============================================================================
// GP_ENTER_PEPACHI_REQ (698) → GP_ENTER_PEPACHI_ACK (699)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 698 is empty. In CLobbyShop::sub_46AD00, only status==1 is the entry
    // success branch; all three fields are read before that branch.
    private static ValueTask GP_ENTER_PEPACHI_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_ENTER_PEPACHI_ACK)
            .WriteU8(0)
            .WriteS32(0)
            .WriteS32(0));
    }
}
