// =============================================================================
// GL_SHOUTCHAT_REQ (836) → GL_SHOUTCHAT_ACK (837)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 836 GL_SHOUTCHAT_REQ (sub_583370): s32 uid(自己 dword_EE8CB4),
    //   s32 strlen, str text — client 前置: 3s 牆鐘限流 (dword_1D0D24C)
    //   + CHAT_SHOUT 動作表 entry[4]!=0 (可喊)。uid/strlen 以 server 為準
    //   (防冒名), 僅 text 有意義。
    // → 837 ACK (sub_583C20): u8 flag(0/1 皆顯示), s32 uid, s32 timer,
    //   str nick, s32 raw_len, raw[raw_len] — uid==自己 → client 把
    //   CHAT_SHOUT 動作表 cooldown 設為 timer (0=不可再喊, 非0=可再喊)。
    //   timer 精確單位原服未明 (與 391 同款 server 動作值); 送 1 保持可用,
    //   由 client 3s 牆鐘限流防洗頻。GL = 大廳全域, 廣播全服。
    private static async ValueTask GL_SHOUTCHAT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0 || session.Nickname.Length == 0)
        {
            return;
        }

        _ = packet.ReadS32();                                // 自己 uid (以 session 為準)
        _ = packet.ReadS32();                                // strlen (client 附, 不重算)
        var text = packet.ReadStr();
        if (text.Length == 0)
        {
            return;
        }

        var shout = new Packet(Opcode.GL_SHOUTCHAT_ACK)
            .WriteU8(0)                                      // flag: 0/1 皆顯示 (sub_583C20)
            .WriteS32((int)session.UserId)
            .WriteS32(ShoutCooldown)                         // CHAT_SHOUT 動作值 (非0=可用)
            .WriteStr(session.Nickname)
            .WriteS32(Packet.Ansi.GetByteCount(text))
            .WriteRaw(Packet.Ansi.GetBytes(text));

        foreach (var target in context.Sessions.All)
        {
            if (!target.Authenticated)
            {
                continue;
            }

            try
            {
                await target.SendAsync(Packet.FromPayload(shout.Opcode, shout.Payload));
            }
            catch
            {
                // 個別連線斷線不影響其他人
            }
        }
    }

    /// <summary>837 的 timer — CHAT_SHOUT 動作表 cooldown (非0=可再喊)。</summary>
    private const int ShoutCooldown = 1;
}
