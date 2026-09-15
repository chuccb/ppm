// =============================================================================
// GR_TEAMSHUFFLECHANGE_REQ (368) → GR_TEAMSHUFFLECHANGE_ACK (369)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 368 GR_TEAMSHUFFLECHANGE_REQ (sub_585CE0): u8 — 房主切換隊打散開關
    // → 369 ACK (sub_585D90→sub_4354B0): u8 — 寫 mode rule 物件 +13 並
    //   勾選 GAMEROOM_TEAMSHUFFLE
    private static async ValueTask GR_TEAMSHUFFLECHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TeamShuffle = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TEAMSHUFFLECHANGE_ACK).WriteU8(on));
    }

}
