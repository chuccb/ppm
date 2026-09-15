// =============================================================================
// GG_EXITGAME_REQ (139) → GG_EXITGAME_ACK (140)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 139 GG_EXITGAME_REQ (sub_560720): 空 payload — 玩家離開對戰回房
    // → 140 ACK (sub_563430): u8 n2==1, u8 slot — 單人退場廣播 (n2==2 是
    //   整房重置, 由 133/134 流程觸發, 此處不涉及)
    private static async ValueTask GG_EXITGAME_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_EXITGAME_ACK)
            .WriteU8(1)
            .WriteU8(slot));
    }

}
