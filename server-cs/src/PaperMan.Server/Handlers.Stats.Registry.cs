// =============================================================================
// Stats opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
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
    public static void Register(Registrar add)
    {
        add(Opcode.GP_CHPLAYC_REQ, GP_CHPLAYC_REQ);
        add(Opcode.GP_CHROUNDC_REQ, GP_CHROUNDC_REQ);
        add(Opcode.GP_CHDISC_REQ, GP_CHDISC_REQ);
        add(Opcode.GP_CHWINC_REQ, GP_CHWINC_REQ);
        add(Opcode.GP_CHLOSSC_REQ, GP_CHLOSSC_REQ);
        add(Opcode.GP_CHKILLC_REQ, GP_CHKILLC_REQ);
        add(Opcode.GP_CHDEADC_REQ, GP_CHDEADC_REQ);
        add(Opcode.GP_CHHEADSC_REQ, GP_CHHEADSC_REQ);
        add(Opcode.GP_CHACOMBOC_REQ, GP_CHACOMBOC_REQ);
        add(Opcode.GP_CHHEARTC_REQ, GP_CHHEARTC_REQ);
        add(Opcode.GP_CHDKILLC_REQ, GP_CHDKILLC_REQ);
        add(Opcode.GP_CHTKILLC_REQ, GP_CHTKILLC_REQ);
        add(Opcode.GP_CHCRITICALC_REQ, GP_CHCRITICALC_REQ);
        add(Opcode.GP_CHMKILLC_REQ, GP_CHMKILLC_REQ);
        add(Opcode.GP_CHUKILLC_REQ, GP_CHUKILLC_REQ);
        add(Opcode.GP_CHZKILLC_REQ, GP_CHZKILLC_REQ);
        add(Opcode.GP_CHKKILLC_REQ, GP_CHKKILLC_REQ);
        add(Opcode.GP_CHDDKILLC_REQ, GP_CHDDKILLC_REQ);
    }
}
