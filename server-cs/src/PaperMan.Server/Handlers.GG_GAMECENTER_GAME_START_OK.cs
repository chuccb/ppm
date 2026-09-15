// =============================================================================
// GG_GAMECENTER_GAME_START_OK_REQ (483) → GG_GAMECENTER_GAME_START_OK_ACK (484)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 483 GG_GAMECENTER_GAME_START_OK_REQ (sub_584F10: s16 game_id)
    // → 484 ACK (sub_584F70): s16 v8, u8 v7, u16 v5, s32 v6
    private static async ValueTask GG_GAMECENTER_GAME_START_OK_REQ(Session session, Packet packet, ServerContext context)
    {
        short gameId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;

        var ack = new Packet(Opcode.GG_GAMECENTER_GAME_START_OK_ACK)
            .WriteS16(0)                 // v8
            .WriteU8(1)                  // v7 (status 1 = ok)
            .WriteU16((ushort)gameId)    // v5
            .WriteS32(0);                // v6

        await session.SendAsync(ack);
    }
}
