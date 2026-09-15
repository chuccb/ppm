// =============================================================================
// GM_CHECKNICK_REQ (210) → GM_CHECKNICK_ACK (211)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 210 REQ builder @0x572D30: 只有 str nick (u8+str 是 216/262 的格式)
    // → 211 ACK sub_572D80 → sub_41BBB0 (十輪逐分支讀出):
    //   1 = 可用 (訊息 0xE0), 2 = 已被使用 (格式訊息 0xDF 帶名字),
    //   0 = 一般錯誤 (彈窗 0x70/17) — 三種都停在暱稱畫面 (state:=2)
    private static async ValueTask GM_CHECKNICK_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        byte result = (IsValidGM_CHECKNICK_REQ_Nickname(nick), context.Db.IsNickTaken(nick)) switch
        {
            (false, _) => 0,                                // 非法 → 一般錯誤
            (_, true) => 2,                                 // 重複 → 0xDF 訊息
            _ => 1,                                         // 可用 → 0xE0 訊息
        };

        Console.WriteLine($"[s{session.Id}] GM_CHECKNICK_REQ: nick='{nick}' -> result={result}");
        await session.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    private static bool IsValidGM_CHECKNICK_REQ_Nickname(string nick) => nick.Length is >= 2 and <= 16;

}
