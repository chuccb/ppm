// =============================================================================
// GL_GAMECENTER_REC_REQ (472) → GL_GAMECENTER_REC_ACK (473)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 472 GL_GAMECENTER_REC_REQ (sub_5848B0: s16 game_id)
    // → 473 ACK (sub_584910): s16 game_id, s32 score, u8 top3_cnt, u8 top10_cnt, u8 v24, u8 v35, s16 v28, s32 v30, raw16, u8 v23
    private static async ValueTask GL_GAMECENTER_REC_REQ(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        var rec = context.Db.GetGameCenterRecord(session.UserId, gameId);

        var ack = new Packet(Opcode.GL_GAMECENTER_REC_ACK)
            .WriteS16(gameId)
            .WriteS32(rec.HighScore)
            .WriteU8(0)                  // top3 count = 0
            .WriteU8(0)                  // top10 count = 0
            .WriteU8(0)                  // v24
            .WriteU8(0)                  // v35 (no 0x20 blob)
            .WriteS16(0)                 // v28
            .WriteS32(0)                 // v30
            .WriteBytes(new byte[16])    // v27
            .WriteU8(0);                 // v23 (no 0x2C blob)

        await session.SendAsync(ack);
    }
}
