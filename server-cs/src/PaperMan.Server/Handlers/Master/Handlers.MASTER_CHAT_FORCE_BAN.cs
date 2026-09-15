// =============================================================================
// MASTER_CHAT_FORCE_BAN_REQ (830) → unnamed opcode 417
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 830 MASTER_CHAT_FORCE_BAN_REQ (sub_582B90: u8 mode, str nick, s32 dur) → 823 MASTER_CHAT_BAN_ACK
    private static async ValueTask MASTER_CHAT_FORCE_BAN_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.ReadStr();
        _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        await session.SendAsync(new Packet(Opcode.MASTER_CHAT_BAN_ACK));
    }
}
