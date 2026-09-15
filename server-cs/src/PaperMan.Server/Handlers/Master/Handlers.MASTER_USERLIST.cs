// =============================================================================
// MASTER_USERLIST_REQ (824) → unnamed opcode 417
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 824 MASTER_USERLIST_REQ (sub_582840: u8 mode, s32 page) → 825 MASTER_LOBBY_USERLIST_ACK (sub_582890)
    private static async ValueTask MASTER_USERLIST_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.MASTER_LOBBY_USERLIST_ACK)
            .WriteU8(0);                                     // count = 0
        await session.SendAsync(ack);
    }
}
