// =============================================================================
// GameCenter opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // =============================================================================
    // 遊戲中心 GameCenter (迷你遊戲) handlers (docs/PACKETS.md §3.15g):
    //
    // 支援迷你遊戲紀錄查詢 (472/473)、遊戲開始 (474/475, 483/484)、
    // 結算與獎勵 (476/477)、排行榜 (480/481)、房間進行時間同步 (485/486)。
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.GL_GAMECENTER_REC_REQ, GL_GAMECENTER_REC_REQ);
        add(Opcode.GG_GAMECENTER_GAME_START_REQ, GG_GAMECENTER_GAME_START_REQ);
        add(Opcode.GG_GAMECENTER_GAME_END_REQ, GG_GAMECENTER_GAME_END_REQ);
        add(Opcode.GG_GAMECENTER_GAME_PLAY_CHECK_REQ, GG_GAMECENTER_GAME_PLAY_CHECK_REQ);
        add(Opcode.GG_GAMECENTER_RANKING_REQ, GG_GAMECENTER_RANKING_REQ);
        add(Opcode.GG_GAMECENTER_GAME_START_OK_REQ, GG_GAMECENTER_GAME_START_OK_REQ);
        add(Opcode.GL_GET_GAMEROOM_PROGRESSTIME_REQ, GL_GET_GAMEROOM_PROGRESSTIME_REQ);
    }
}
