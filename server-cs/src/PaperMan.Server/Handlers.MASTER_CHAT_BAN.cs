// =============================================================================
// MASTER_CHAT_BAN_REQ (822) → MASTER_CHAT_BAN_ACK (823)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 822 MASTER_CHAT_BAN_REQ (sub_582770: u8 mode, u8 dur, str nick) → 823 ACK (sub_5827C0)
    private static async ValueTask MASTER_CHAT_BAN_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.ReadStr();
        await session.SendAsync(new Packet(Opcode.MASTER_CHAT_BAN_ACK));
    }
}
