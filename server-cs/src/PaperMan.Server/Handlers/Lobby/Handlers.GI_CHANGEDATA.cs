// =============================================================================
// GI_CHANGEDATA_REQ (218) → GI_CHANGEDATA_ACK (219)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 218 GI_CHANGEDATA_REQ (sub_523A00: u8 char_slot)
    // → 219 ACK (sub_573230): u8 status(1=成功)
    private static async ValueTask GI_CHANGEDATA_REQ(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGEDATA_ACK).WriteU8(1));
    }

}
