// =============================================================================
// Battle opcode registry
// Binding only: every listed receive entry is visible by its canonical token.
// BattleObjectHandlers adds the four state-authoritative OCC/drop registrations.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleRelayHandlers
{
    // =============================================================================
    // GG 戰鬥中繼 handlers — 本輪(五十一)逐函數重驗, 更正廿五輪「三模式」簡化:
    //
    //   廿五輪把 GG 全族簡化成「slot 前綴轉發」, 但逐函數重讀後確認各族佈局
    //   其實不同 (docs/PACKETS.md §3.15d3 已同步更正):
    //
    //   ① TH 駭入/炸彈簇 (316-331): REQ 首欄是「team」(0/1) 不是 slot!
    //      317 sub_557040 讀 u8 team, u8 slot → sub_766490(team<2 定址);
    //      319 sub_557400 讀 u8 team, 6×f32, u8 slot;
    //      321 sub_557730 只讀 u8 team (無 slot!);
    //      323 sub_5579A0 只讀 u8 team; 327/329/331 讀 u8 team, u8 slot。
    //      → ACK = REQ 原欄位 + 尾附發話者 slot (321/323 例外不加 slot)。
    //      322 REQ 空 → 回 [BombTeam] (318 武裝成功時記下, 未植彈則忽略)。
    //   ② 足球 964/967 REQ 皆空 → ACK 965/968 (sub_566040/sub_566200)
    //      讀 u8 flag, u8 slot; flag 0 =「該事件成立」(得球/進球) — 依 REQ
    //      語意回 0, 非硬編 (flag 1 = 取消/收回)。
    //   ③ 奪寶 443/445 (sub_55BDC0/sub_55C060): REQ u8+s16; 444/446
    //      (sub_55BE80/sub_55C120) 讀 u8,u8,u16×3 分數組 — 需奪寶計分
    //      狀態機才能產出, **不可轉發** → 不註冊 (見 TODO)。
    //   ④ 聊天四連 344/346/348/350: REQ = s32 tex, u8 slot, str;
    //      ACK 345/347/349/351 (sub_58D870/58D8A0/58D8D0/58D900 →
    //      sub_74A5F0 → sub_748E40) 讀 s32, u8, str — 同構。⚠ 必須以
    //      **ACK opcode** 廣播 (REQ opcode 無 dispatcher case, 會被忽略)。
    //
    // 復活五連 (342/360/455/746/909/971) 與 Y_TCP_INF/PM_TSPOSUPDATE 維持
    // 既有實作。這些 TCP handler 的 room validation/broadcast is an explicit
    // server implementation boundary; the recovered client does not prove an
    // original-server P2P/relay authority model or its relation to private UDP.
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.Y_TCP_INF_REQ, Y_TCP_INF_REQ);
        add(Opcode.PM_TSPOSUPDATE_REQ, PM_TSPOSUPDATE_REQ);
        add(Opcode.GG_HACKSTART_REQ, GG_HACKSTART_REQ);
        add(Opcode.GG_HACKSUCC_REQ, GG_HACKSUCC_REQ);
        add(Opcode.GG_HACKFAIL_REQ, GG_HACKFAIL_REQ);
        add(Opcode.GG_UNHACKSTART_REQ, GG_UNHACKSTART_REQ);
        add(Opcode.GG_UNHACKSUCC_REQ, GG_UNHACKSUCC_REQ);
        add(Opcode.GG_UNHACKFAIL_REQ, GG_UNHACKFAIL_REQ);
        add(Opcode.GG_BOMBSUCC_REQ, GG_BOMBSUCC_REQ);
        add(Opcode.GG_GET_BALL_REQ, GG_GET_BALL_REQ);
        add(Opcode.GG_GET_GOAL_REQ, GG_GET_GOAL_REQ);
        add(Opcode.GG_SOLORESPON_REQ, GG_SOLORESPON_REQ);
        add(Opcode.GG_TSURRESPON_REQ, GG_TSURRESPON_REQ);
        add(Opcode.GG_EXERCISERESPON_REQ, GG_EXERCISERESPON_REQ);
        add(Opcode.GG_PNR_RESPON_REQ, GG_PNR_RESPON_REQ);
        add(Opcode.GG_OCC_RESPON_REQ, GG_OCC_RESPON_REQ);
        add(Opcode.GG_SOCCER_RESPON_REQ, GG_SOCCER_RESPON_REQ);
        add(Opcode.GG_LIVECHAT_REQ, GG_LIVECHAT_REQ);
        add(Opcode.GG_TEAMCHAT_REQ, GG_TEAMCHAT_REQ);
        add(Opcode.GG_DEADCHAT_REQ, GG_DEADCHAT_REQ);
        add(Opcode.GG_TEAMDEADCHAT_REQ, GG_TEAMDEADCHAT_REQ);
        BattleObjectHandlers.Register(add);
    }
}
