// =============================================================================
// GG_GAMECENTER_RANKING_REQ (480) → GG_GAMECENTER_RANKING_ACK (481)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 480 GG_GAMECENTER_RANKING_REQ (sub_585020: s16 game_id, u8 mode)
    // → 481 ACK (sub_585080): s16 v16, u8 v18, s16 v13, s32 v14, u8 count, count×(0x38 bytes)
    private static async ValueTask GG_GAMECENTER_RANKING_REQ(Session session, Packet packet, ServerContext context)
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
}
