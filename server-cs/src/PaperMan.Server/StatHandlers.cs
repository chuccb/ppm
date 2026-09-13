// =============================================================================
// GP_CH*C 家族 (docs/PACKETS.md §3.12) — REQ payload = s32 delta (通常 1),
// ACK = s32 new_value (server 累計後回推, client handler sub_556730 系列)。
// opcode 對 user_stats 欄位的映射寫死為白名單, 防 SQL 注入。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class StatHandlers
{
    // (REQ opcode, ACK opcode, user_stats 欄位)
    private static readonly (Opcode Req, Opcode Ack, string Column)[] Map =
    [
        (Opcode.GP_CHPLAYC_REQ,     Opcode.GP_CHPLAYC_ACK,     "play_count"),
        (Opcode.GP_CHROUNDC_REQ,    Opcode.GP_CHROUNDC_ACK,    "round_count"),
        (Opcode.GP_CHDISC_REQ,      Opcode.GP_CHDISC_ACK,      "disconnects"),
        (Opcode.GP_CHWINC_REQ,      Opcode.GP_CHWINC_ACK,      "wins"),
        (Opcode.GP_CHLOSSC_REQ,     Opcode.GP_CHLOSSC_ACK,     "losses"),
        (Opcode.GP_CHKILLC_REQ,     Opcode.GP_CHKILLC_ACK,     "kills"),
        (Opcode.GP_CHDEADC_REQ,     Opcode.GP_CHDEADC_ACK,     "deaths"),
        (Opcode.GP_CHHEADSC_REQ,    Opcode.GP_CHHEADSC_ACK,    "headshots"),
        (Opcode.GP_CHACOMBOC_REQ,   Opcode.GP_CHACOMBOC_ACK,   "combos"),
        (Opcode.GP_CHHEARTC_REQ,    Opcode.GP_CHHEARTC_ACK,    "hearts"),
        (Opcode.GP_CHDKILLC_REQ,    Opcode.GP_CHDKILLC_ACK,    "double_kill"),
        (Opcode.GP_CHTKILLC_REQ,    Opcode.GP_CHTKILLC_ACK,    "triple_kill"),
        (Opcode.GP_CHCRITICALC_REQ, Opcode.GP_CHCRITICALC_ACK, "criticals"),
        (Opcode.GP_CHMKILLC_REQ,    Opcode.GP_CHMKILLC_ACK,    "multi_kill"),
        (Opcode.GP_CHUKILLC_REQ,    Opcode.GP_CHUKILLC_ACK,    "ultra_kill"),
        (Opcode.GP_CHZKILLC_REQ,    Opcode.GP_CHZKILLC_ACK,    "z_kill"),
        (Opcode.GP_CHKKILLC_REQ,    Opcode.GP_CHKKILLC_ACK,    "k_kill"),
        (Opcode.GP_CHDDKILLC_REQ,   Opcode.GP_CHDDKILLC_ACK,   "dd_kill"),
        // 注意: GP_CHPLAYTIMEC_ACK(882) 沒有對應 REQ — client handler (case 882)
        // 讀 s32 總累計秒數並與 dword_EE8D7C 做差分, 由伺服器主動推播
        // (見 PushPlayTime), 不在此表註冊。
    ];

    public static void Register(Dictionary<Opcode, PacketHandler> table)
    {
        foreach (var (req, ackOp, column) in Map)
        {
            table[req] = async (s, p, ctx) =>
            {
                long delta = p.Remaining >= 4 ? p.ReadS32() : 1;
                if (delta is < 0 or > 10_000) delta = 1;          // 基本防灌水
                long value = s.UserId != 0 ? ctx.Db.BumpStat(s.UserId, column, delta) : 0;
                await s.SendAsync(new Packet(ackOp).WriteS32((int)value));
            };
        }
    }

    /// <summary>
    /// GP_CHPLAYTIMEC_ACK(882) 為 server 推播: payload = s32 累計總遊玩秒數。
    /// client (case 882) 取與上次值的差分呼叫 sub_92EF00(20,23,delta,0)。
    /// </summary>
    public static async Task PushPlayTime(Session s, ServerContext ctx)
    {
        if (s.UserId == 0) return;
        long total = ctx.Db.BumpStat(s.UserId, "play_time_s", 0);   // 讀取現值
        await s.SendAsync(new Packet(Opcode.GP_CHPLAYTIMEC_ACK).WriteS32((int)total));
    }
}
