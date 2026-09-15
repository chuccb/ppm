// =============================================================================
// GM_CREATECHAR_REQ (214) → GM_CREATECHAR_ACK (215)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 214 GM_CREATECHAR_REQ (sub_532AA0: u8 char_type, s16 hair, s16 face, s16 coat)
    // → 215 ACK (sub_572F80 / sub_550170): u8 status(0=成功)
    private static async ValueTask GM_CREATECHAR_REQ(Session session, Packet packet, ServerContext context)
    {
        byte charType = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;
        bool ok = session.UserId != 0 && context.Db.CreateChar(session.UserId, 0, charType);
        await session.SendAsync(new Packet(Opcode.GM_CREATECHAR_ACK).WriteU8(ok ? (byte)0 : (byte)1));
    }

}
