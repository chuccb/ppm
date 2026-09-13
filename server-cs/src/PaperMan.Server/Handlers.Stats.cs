// =============================================================================
// GP_CH*C 戰績家族 — 三輪交叉驗證後的正確語意:
//
//   REQ (builder 例 sub_5567F0@230, sub_5568E0@232, sub_556B90@244):
//     payload = s32 「新的絕對累計值」 (client 送 total, 不是增量;
//     a1<0 時 client 不送)
//
//   ACK 分兩型 (client dispatcher case 223..245, 363, 381..389):
//     223..235 (playc/roundc/disc/winc/lossc/killc/deadc):
//         1×s32 = server 確認後的 total (sub_556730 系列)
//     237..245, 363, 381..389 (heads/acombo/heart/dkill/tkill/critical/
//         mkill/ukill/zkill/kkill/ddkill):
//         2×s32 = {total, extra} (sub_556A00 讀兩個; extra 進 EE8DB0.. 顯示,
//         多數 UI 當「本場增量/獎勵」用, 送 0 安全)
//
//   ⚠ GP_CHPLAYTIMEC_ACK(882) 沒有 REQ — client (case 882) 收 s32 總秒數,
//     與 dword_EE8D7C 差分後呼叫 sub_92EF00(20,23,delta,0), 屬 server 推播。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class StatHandlers
{
    private enum AckShape
    {
        Single,                                             // 223..235: 1×s32 total
        Pair,                                               // 237..389: s32 total + s32 extra
    }

    private static readonly (Opcode Req, Opcode Ack, string Column, AckShape Shape)[] Counters =
    [
        (Opcode.GP_CHPLAYC_REQ,     Opcode.GP_CHPLAYC_ACK,     "play_count",  AckShape.Single),
        (Opcode.GP_CHROUNDC_REQ,    Opcode.GP_CHROUNDC_ACK,    "round_count", AckShape.Single),
        (Opcode.GP_CHDISC_REQ,      Opcode.GP_CHDISC_ACK,      "disconnects", AckShape.Single),
        (Opcode.GP_CHWINC_REQ,      Opcode.GP_CHWINC_ACK,      "wins",        AckShape.Single),
        (Opcode.GP_CHLOSSC_REQ,     Opcode.GP_CHLOSSC_ACK,     "losses",      AckShape.Single),
        (Opcode.GP_CHKILLC_REQ,     Opcode.GP_CHKILLC_ACK,     "kills",       AckShape.Single),
        (Opcode.GP_CHDEADC_REQ,     Opcode.GP_CHDEADC_ACK,     "deaths",      AckShape.Single),
        (Opcode.GP_CHHEADSC_REQ,    Opcode.GP_CHHEADSC_ACK,    "headshots",   AckShape.Pair),
        (Opcode.GP_CHACOMBOC_REQ,   Opcode.GP_CHACOMBOC_ACK,   "combos",      AckShape.Pair),
        (Opcode.GP_CHHEARTC_REQ,    Opcode.GP_CHHEARTC_ACK,    "hearts",      AckShape.Pair),
        (Opcode.GP_CHDKILLC_REQ,    Opcode.GP_CHDKILLC_ACK,    "double_kill", AckShape.Pair),
        (Opcode.GP_CHTKILLC_REQ,    Opcode.GP_CHTKILLC_ACK,    "triple_kill", AckShape.Pair),
        (Opcode.GP_CHCRITICALC_REQ, Opcode.GP_CHCRITICALC_ACK, "criticals",   AckShape.Pair),
        (Opcode.GP_CHMKILLC_REQ,    Opcode.GP_CHMKILLC_ACK,    "multi_kill",  AckShape.Pair),
        (Opcode.GP_CHUKILLC_REQ,    Opcode.GP_CHUKILLC_ACK,    "ultra_kill",  AckShape.Pair),
        (Opcode.GP_CHZKILLC_REQ,    Opcode.GP_CHZKILLC_ACK,    "z_kill",      AckShape.Pair),
        (Opcode.GP_CHKKILLC_REQ,    Opcode.GP_CHKKILLC_ACK,    "k_kill",      AckShape.Pair),
        (Opcode.GP_CHDDKILLC_REQ,   Opcode.GP_CHDDKILLC_ACK,   "dd_kill",     AckShape.Pair),
    ];

    public static void Register(Registrar add)
    {
        foreach (var (req, ack, column, shape) in Counters)
        {
            add(req, MakeCounter(ack, column, shape));
        }
    }

    private static PacketHandler MakeCounter(Opcode ack, string column, AckShape shape) =>
        async (session, packet, context) =>
        {
            // REQ = client 的新絕對累計值 (sub_5567F0 等: a1>=0 才送)
            long total = packet.Remaining >= 4 ? packet.ReadS32() : 0;
            if (total < 0)
            {
                total = 0;
            }

            if (session.UserId != 0)
            {
                total = context.Db.SetStatMax(session.UserId, column, total);   // 只允許單調遞增
            }

            var reply = new Packet(ack).WriteS32((int)total);
            if (shape is AckShape.Pair)
            {
                reply.WriteS32(0);                                    // extra (UI 顯示用)
            }

            await session.SendAsync(reply);
        };

    /// <summary>882 推播: s32 累計總遊玩秒數 (client 自行差分, 無 REQ)。</summary>
    public static async ValueTask PushPlayTime(Session session, ServerContext context)
    {
        if (session.UserId == 0)
        {
            return;
        }

        long total = context.Db.SetStatMax(session.UserId, "play_time_s", 0);
        await session.SendAsync(new Packet(Opcode.GP_CHPLAYTIMEC_ACK).WriteS32((int)total));
    }
}
