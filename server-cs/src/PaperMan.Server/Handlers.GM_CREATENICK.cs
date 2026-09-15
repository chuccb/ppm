// =============================================================================
// GM_CREATENICK_REQ (212) → GM_CREATENICK_ACK (213)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 212 REQ builder sub_572DC0: 只有 str nick
    // → 213 ACK sub_572E70 → sub_41BD40 (十輪重大更正):
    //   ⚠ 1 = 成功 (拷貝統計欄位, state:=5 進大廳), 0 = 失敗 (state:=4)
    //   — 舊實作成功回 0 會讓 client 卡在失敗畫面!
    private static async ValueTask GM_CREATENICK_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        byte result = 0;

        if (session.Authenticated && IsValidGM_CREATENICK_REQ_Nickname(nick))
        {
            long uid = context.Db.CreateNick(session.AccountId, nick);
            if (uid != 0)
            {
                (session.UserId, session.Nickname, result) = (uid, nick, (byte)1);
            }
        }

        Console.WriteLine($"[s{session.Id}] GM_CREATENICK_REQ: nick='{nick}', accountId={session.AccountId} -> result={result}, userId={session.UserId}");
        await session.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    private static bool IsValidGM_CREATENICK_REQ_Nickname(string nick) => nick.Length is >= 2 and <= 16;

}
