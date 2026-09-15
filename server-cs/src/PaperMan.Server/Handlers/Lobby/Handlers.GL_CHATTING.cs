// =============================================================================
// GL_CHATTING_REQ (119) → GL_CHATTING_ACK (120)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // REQ(119) builder @0x56E2xx: str message (ANSI)
    // ACK(120) sub_56E300: s32 custom_tex, str nick, wstr message
    //   ⚠ 訊息回送用「寬字串」(sub_5927B0 讀 UTF-16LE) — 與 REQ 的 ANSI 不對稱!
    //   client 端還會拿 nick 過 sub_539320 黑名單 (忽略清單) 過濾
    private static async ValueTask GL_CHATTING_REQ(Session session, Packet packet, ServerContext context)
    {
        // 兩變體 (廿四輪): 完整版 s32 tex + str nick + wstr msg (與 ACK 同構);
        // 簡版只有 str。以剩餘長度判別。
        int tex = 0;
        string nick = session.Nickname;
        string message;

        if (packet.Remaining > 8)
        {
            tex = packet.ReadS32();
            nick = packet.ReadStr();
            message = packet.ReadWStr();
        }
        else
        {
            message = packet.ReadStr();
        }

        if (session.UserId == 0 || message.Length == 0)
        {
            return;
        }

        // 單人大廳: 回聲給自己 (多人時原樣廣播 — client 已附 nick+tex)
        await session.SendAsync(new Packet(Opcode.GL_CHATTING_ACK)
            .WriteS32(tex)
            .WriteStr(nick)
            .WriteWStr(message));
    }
}
