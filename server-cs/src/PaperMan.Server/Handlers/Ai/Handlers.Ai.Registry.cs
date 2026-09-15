// =============================================================================
// AI/PvE opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AiHandlers
{
    // =============================================================================
    // AI / PVE 防衛戰模式 handlers (docs/PACKETS.md §3.15h):
    //
    // 支援 AI 模式獎勵抽取 (918/919)、護盾受損 (922/923)、彈藥補給 (924-927)、
    // 接關 Continue (928/929)、狂暴/Fever 模式 (935/936)、波次推進 (939/940)、
    // 房內槽位重設 (944/945)。
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.GR_AI_GET_REWARD_ITEM_REQ, GR_AI_GET_REWARD_ITEM_REQ);
        add(Opcode.GR_AI_DAMAGE_SHIELD_REQ, GR_AI_DAMAGE_SHIELD_REQ);
        add(Opcode.GR_AI_RECHARGE_MAGAZINE_START_REQ, GR_AI_RECHARGE_MAGAZINE_START_REQ);
        add(Opcode.GR_AI_RECHARGE_MAGAZINE_END_REQ, GR_AI_RECHARGE_MAGAZINE_END_REQ);
        add(Opcode.GR_AI_CONTINUE_START_REQ, GR_AI_CONTINUE_START_REQ);
        add(Opcode.GR_AI_FEVER_START_REQ, GR_AI_FEVER_START_REQ);
        add(Opcode.GR_AI_GO_NEXT_WAVE_REQ, GR_AI_GO_NEXT_WAVE_REQ);
        add(Opcode.GR_RESET_GAMEROOMSLOT_REQ, GR_RESET_GAMEROOMSLOT_REQ);
    }
}
