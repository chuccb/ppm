// =============================================================================
// GP_CHPLAYTIMEC_ACK (882) server notification
// There is no paired request. This remains a callable server-originated packet helper,
// not a Router receive entry.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    /// <summary>882 推播: s32 累計總遊玩秒數 (client 自行差分, 無 REQ)。</summary>
    public static async ValueTask PushGP_CHPLAYTIMEC_ACK(Session session, ServerContext context)
    {
        if (session.UserId == 0)
        {
            return;
        }

        long total = context.Db.SetStatMax(session.UserId, "play_time_s", 0);
        await session.SendAsync(new Packet(Opcode.GP_CHPLAYTIMEC_ACK).WriteS32((int)total));
    }
}
