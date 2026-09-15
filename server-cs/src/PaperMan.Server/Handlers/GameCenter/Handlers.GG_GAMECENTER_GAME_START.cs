// =============================================================================
// GG_GAMECENTER_GAME_START_REQ (474) → GG_GAMECENTER_GAME_START_ACK (475)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 474 GG_GAMECENTER_GAME_START_REQ (sub_584E20: s16 game_id, u8 stage)
    // → 475 ACK (sub_584E80)
    private static async ValueTask GG_GAMECENTER_GAME_START_REQ(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        byte stage = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;

        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_START_ACK)
            .WriteU8(1)                  // status = 1 (成功)
            .WriteS16(gameId)
            .WriteU8(stage);

        await session.SendAsync(ack);
    }
}
