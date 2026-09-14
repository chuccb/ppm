// =============================================================================
// 遊戲中心 GameCenter (迷你遊戲) handlers (docs/PACKETS.md §3.15g):
//
// 支援迷你遊戲紀錄查詢 (472/473)、遊戲開始 (474/475, 483/484)、
// 結算與獎勵 (476/477)、排行榜 (480/481)、房間進行時間同步 (485/486)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class GameCenterHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_GAMECENTER_REC_REQ, Rec);
        add(Opcode.GG_GAMECENTER_GAME_START_REQ, GameStart);
        add(Opcode.GG_GAMECENTER_GAME_END_REQ, GameEnd);
        add(Opcode.GG_GAMECENTER_GAME_PLAY_CHECK_REQ, GamePlayCheck);
        add(Opcode.GG_GAMECENTER_RANKING_REQ, Ranking);
        add(Opcode.GG_GAMECENTER_GAME_START_OK_REQ, GameStartOk);
        add(Opcode.GL_GET_GAMEROOM_PROGRESSTIME_REQ, GetGameRoomProgressTime);
    }

    // 472 GL_GAMECENTER_REC_REQ (sub_5848B0: s16 game_id)
    // → 473 ACK (sub_584910): s16 game_id, s32 score, u8 top3_cnt, u8 top10_cnt, u8 v24, u8 v35, s16 v28, s32 v30, raw16, u8 v23
    private static async ValueTask Rec(Session session, Packet packet, ServerContext context)
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

    // 474 GG_GAMECENTER_GAME_START_REQ (sub_584E20: s16 game_id, u8 stage)
    // → 475 ACK (sub_584E80)
    private static async ValueTask GameStart(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        byte stage = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;

        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_START_ACK)
            .WriteU8(1)                  // status = 1 (成功)
            .WriteS16(gameId)
            .WriteU8(stage);

        await session.SendAsync(ack);
    }

    // 476 GG_GAMECENTER_GAME_END_REQ (sub_584EE0: s16 game_id, raw score/stats)
    // → 477 ACK (sub_564A00 / sub_76E450): s16, raw32, raw44, s16, s32, raw24, raw8, s32, s32, s32, s32, s8, u8, u8, s8, s8
    private static async ValueTask GameEnd(Session session, Packet packet, ServerContext context)
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

    // 478 GG_GAMECENTER_GAME_PLAY_CHECK_REQ (sub_564A40: 0x24 raw)
    // → 479 ACK: u8 status(1)
    private static async ValueTask GamePlayCheck(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_PLAY_CHECK_ACK).WriteU8(1);
        await session.SendAsync(ack);
    }

    // 480 GG_GAMECENTER_RANKING_REQ (sub_585020: s16 game_id, u8 mode)
    // → 481 ACK (sub_585080): s16 v16, u8 v18, s16 v13, s32 v14, u8 count, count×(0x38 bytes)
    private static async ValueTask Ranking(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;

        var rankings = context.Db.GetGameCenterRankings(gameId, 10);

        var ack = new Packet(Opcode.GG_GAMECENTER_RANKING_ACK)
            .WriteS16(gameId)            // v16
            .WriteU8(0)                  // v18
            .WriteS16(0)                 // v13
            .WriteS32(0)                 // v14
            .WriteU8((byte)Math.Min(rankings.Count, 3)); // n_1 (top 3)

        for (int i = 0; i < Math.Min(rankings.Count, 3); i++)
        {
            var entryBytes = new byte[0x38];
            var nickBytes = System.Text.Encoding.ASCII.GetBytes(rankings[i].Nickname);
            Array.Copy(nickBytes, entryBytes, Math.Min(nickBytes.Length, 16));
            BitConverter.GetBytes(rankings[i].Score).CopyTo(entryBytes, 32);
            ack.WriteBytes(entryBytes);
        }

        await session.SendAsync(ack);
    }

    // 483 GG_GAMECENTER_GAME_START_OK_REQ (sub_584F10: s16 game_id)
    // → 484 ACK (sub_584F70): s16 v8, u8 v7, u16 v5, s32 v6
    private static async ValueTask GameStartOk(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;

        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_START_OK_ACK)
            .WriteS16(0)                 // v8
            .WriteU8(1)                  // v7 (status 1 = ok)
            .WriteU16((ushort)gameId)    // v5
            .WriteS32(0);                // v6

        await session.SendAsync(ack);
    }

    // 485 GL_GET_GAMEROOM_PROGRESSTIME_REQ (sub_56AD90: u8 room_no)
    // → 486 ACK (sub_56AE30): u8 n3, s16 v30, u8 Id, s32 n999, u8 v26, s8 v24, u8 v32, s8 v28, u8 n11, s8 v27, u8 v29, s8 v23
    private static async ValueTask GetGameRoomProgressTime(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var room = context.Rooms.Find(roomNo);

        int progressSec = 0;
        if (room is not null && room.Playing)
        {
            progressSec = 60; // 模擬戰鬥已進行 60 秒
        }

        var ack = new Packet(Opcode.GL_GET_GAMEROOM_PROGRESSTIME_ACK)
            .WriteU8(0)                  // n3 = 0 (一般模式)
            .WriteS16(roomNo)            // v30
            .WriteU8(0)                  // Id
            .WriteS32(progressSec)       // n999 (progress time seconds)
            .WriteU8(0)                  // v26
            .WriteU8(0)                  // v24
            .WriteU8(0)                  // v32
            .WriteU8(0)                  // v28
            .WriteU8(0)                  // n11
            .WriteU8(0)                  // v27
            .WriteU8(0)                  // v29
            .WriteU8(0);                 // v23

        await session.SendAsync(ack);
    }
}
