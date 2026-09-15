// =============================================================================
// GI_CHANGESLOT_REQ (312) → GI_CHANGESLOT_ACK (313)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 312 GI_CHANGESLOT_REQ (sub_523FB0: u8 slot_no)
    // → 313 ACK (sub_573320): u8 slot_no
    private static async ValueTask GI_CHANGESLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGESLOT_ACK).WriteU8(slotNo));
    }

}
