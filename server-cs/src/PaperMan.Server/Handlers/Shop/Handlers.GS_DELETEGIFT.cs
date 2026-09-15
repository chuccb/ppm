// =============================================================================
// GS_DELETEGIFT_REQ (453) → GS_DELETEGIFT_ACK (454)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 453 is exactly {s32 giftId, s32 itemId}; `sub_57BCF0` always reads the
    // same identifiers from 454. A status other than one preserves the native
    // client's cached gifts. The original selection/deletion policy is not
    // recovered, so this handler echoes only its non-mutating failure arm.
    private static ValueTask GS_DELETEGIFT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 8)
        {
            return ValueTask.CompletedTask;
        }

        int giftId = packet.ReadS32();
        int itemId = packet.ReadS32();
        return session.SendAsync(new Packet(Opcode.GS_DELETEGIFT_ACK)
            .WriteU8(0)
            .WriteS32(giftId)
            .WriteS32(itemId));
    }
}
