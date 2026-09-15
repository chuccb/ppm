// =============================================================================
// MASTER_PLAY_WITH_REQ (885) → MASTER_PLAY_WITH_ACK (886)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 885 MASTER_PLAY_WITH_REQ (sub_579C70: s32 target_uid) → 886 ACK: u8 0
    private static async ValueTask MASTER_PLAY_WITH_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        await session.SendAsync(new Packet(Opcode.MASTER_PLAY_WITH_ACK).WriteU8(0));
    }
}
