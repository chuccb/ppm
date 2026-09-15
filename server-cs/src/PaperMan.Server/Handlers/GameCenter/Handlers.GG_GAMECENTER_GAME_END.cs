// =============================================================================
// GG_GAMECENTER_GAME_END_REQ (476) → GG_GAMECENTER_GAME_END_ACK (477)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 476 GG_GAMECENTER_GAME_END_REQ (sub_584EE0: s16 game_id, raw score/stats)
    // → 477 ACK (sub_564A00 / sub_76E450): s16, raw32, raw44, s16, s32, raw24, raw8, s32, s32, s32, s32, s8, u8, u8, s8, s8
    private static async ValueTask GG_GAMECENTER_GAME_END_REQ(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        int score = 0;
        if (packet.Remaining >= 4)
        {
            score = packet.ReadS32();
        }

        int rewardGp = Math.Min(1000, Math.Max(50, score / 100));
        int rewardExp = Math.Min(500, Math.Max(20, score / 200));

        var (newHighScore, rank) = context.Db.SaveGameCenterScore(session.UserId, gameId, score, rewardGp, rewardExp);

        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_END_ACK)
            .WriteS16(gameId)
            .WriteBytes(new byte[32])    // v24 (0x20)
            .WriteBytes(new byte[44])    // v41 (0x2C)
            .WriteS16(0)                 // v45
            .WriteS32(newHighScore)      // v42
            .WriteBytes(new byte[24])    // v30 (0x18)
            .WriteBytes(new byte[8])     // v27 (8 bytes)
            .WriteS32(score)             // v40
            .WriteS32(rewardGp)          // v22 (GP)
            .WriteS32(rewardExp)         // v28 (Exp)
            .WriteS32(rank)              // v44 (Rank)
            .WriteU8(0)                  // v23
            .WriteU8(0)                  // v25
            .WriteU8(0)                  // v38
            .WriteU8(0)                  // v29
            .WriteU8(0);                 // v43

        await session.SendAsync(ack);
    }
}
