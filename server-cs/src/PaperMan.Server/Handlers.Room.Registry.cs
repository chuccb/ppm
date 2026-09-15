// =============================================================================
// Room opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    public static void Register(Registrar add)
    {
        // membership flows: GL_MAKEROOM_REQ / GL_ENTERROOM_REQ / GR_LEAVE_REQ
        add(Opcode.GL_MAKEROOM_REQ, GL_MAKEROOM_REQ);
        add(Opcode.GL_ENTERROOM_REQ, GL_ENTERROOM_REQ);
        add(Opcode.GR_LEAVE_REQ, GR_LEAVE_REQ);

        // room interaction flows
        add(Opcode.GR_CHATTING_REQ, GR_CHATTING_REQ);
        add(Opcode.GR_MAPCHANGE_REQ, GR_MAPCHANGE_REQ);
        add(Opcode.GR_READY_REQ, GR_READY_REQ);
        add(Opcode.GR_CHANGESLOT_REQ, GR_CHANGESLOT_REQ);
        add(Opcode.GR_CALLUSER_REQ, GR_CALLUSER_REQ);
        add(Opcode.GL_ENTERROOMPASS_REQ, GL_ENTERROOMPASS_REQ);

        // match lifecycle flows
        add(Opcode.GR_START_REQ, GR_START_REQ);
        add(Opcode.GR_ENDLOADING_REQ, GR_ENDLOADING_REQ);
        add(Opcode.GG_STARTGAME_REQ, GG_STARTGAME_REQ);
        add(Opcode.GR_END_REQ, GR_END_REQ);

        // 房設定簇 — REQ 限房主, 值寫入 room 欄位後以同值 ACK 廣播全房
        // (client 端 REQ 只設 pending 狀態, ACK handler 才落地 — 詳 §3.15b2)
        add(Opcode.GG_EXITGAME_REQ, GG_EXITGAME_REQ);
        add(Opcode.GR_CHANGEUSER_REQ, GR_CHANGEUSER_REQ);
        add(Opcode.GR_RULECHANGE_REQ, GR_RULECHANGE_REQ);
        add(Opcode.GR_WINCHANGE_REQ, GR_WINCHANGE_REQ);
        add(Opcode.GR_TIMECHANGE_REQ, GR_TIMECHANGE_REQ);
        add(Opcode.GR_ITEMCHANGE_REQ, GR_ITEMCHANGE_REQ);
        add(Opcode.GR_AUTOCHANGE_REQ, GR_AUTOCHANGE_REQ);
        add(Opcode.GR_KILLCHANGE_REQ, GR_KILLCHANGE_REQ);
        add(Opcode.GR_BALANCECHANGE_REQ, GR_BALANCECHANGE_REQ);
        add(Opcode.GR_NOSKILL_REQ, GR_NOSKILL_REQ);
        add(Opcode.GR_OBSERVERCHAT_REQ, GR_OBSERVERCHAT_REQ);
        add(Opcode.GR_DAMAGEROOM_REQ, GR_DAMAGEROOM_REQ);
        add(Opcode.GR_LOCALROOM_REQ, GR_LOCALROOM_REQ);
        add(Opcode.GR_TEAMSHUFFLECHANGE_REQ, GR_TEAMSHUFFLECHANGE_REQ);
        add(Opcode.GR_TEAMSHUFFLE_REQ, GR_TEAMSHUFFLE_REQ);
        add(Opcode.GR_SOCCER_REQ, GR_SOCCER_REQ);

        // source-proven message relay flows
        add(Opcode.GR_RADIOMSG_REQ, GR_RADIOMSG_REQ);
        add(Opcode.GG_ROOMBROADCAST_REQ, GG_ROOMBROADCAST_REQ);
        add(Opcode.GG_OBSERVERCHAT_REQ, GG_OBSERVERCHAT_REQ);

        // 房主強制踢人 (131/132)。
        add(Opcode.GR_FORCEOUT_REQ, GR_FORCEOUT_REQ);

        // 718–722 投票與 983–989 配對目前只證實 client wire grammar／
        // reader；尚無 service-owned vote/matching state，故刻意不註冊。
        // 詳見 docs/TODO_HANDLERS.md 的 current evidence。
    }
}
