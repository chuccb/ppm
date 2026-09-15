// =============================================================================
// GG_GAMECENTER_GAME_PLAY_CHECK_REQ (478) → GG_GAMECENTER_GAME_PLAY_CHECK_ACK (479)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 478 GG_GAMECENTER_GAME_PLAY_CHECK_REQ (sub_564A40: 0x24 raw)
    // → 479 ACK: u8 status(1)
    private static async ValueTask GG_GAMECENTER_GAME_PLAY_CHECK_REQ(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_PLAY_CHECK_ACK).WriteU8(1);
        await session.SendAsync(ack);
    }
}
