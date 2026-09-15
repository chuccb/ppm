// =============================================================================
// GL_USERLIST_REQ (105) → GL_USERLIST_ACK (106)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // ACK(106) sub_56A250: u16 count; 若 count!=0 才有 u8 flags, u8 n,
    // repeat n{s32 uid, str nick, s32 exp; uid>0 時 +s32 custom_tex, str(64)}
    // ⚠ 第三個 s32 = exp (sub_588560 → sub_403360 exp→level 查表, 十二輪);
    // count==0 → 之後不再讀任何欄位 (交叉驗證確認)
    private static async ValueTask GL_USERLIST_REQ(Session session, Packet packet, ServerContext context) =>
        await session.SendAsync(new Packet(Opcode.GL_USERLIST_ACK).WriteU16(0));
}
