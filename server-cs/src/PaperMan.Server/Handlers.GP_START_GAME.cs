// =============================================================================
// GP_START_GAME_REQ (700) → GP_START_GAME_ACK (701)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 700 is exactly {u8 selector,s32 selectedCharacterId}. The four selector
    // values below are direct caller values; the item-id range is the native
    // character-body family from which that writer derives its second field.
    // `sub_84A490` uses this complete two-byte failure arm for 701. Only a
    // first byte of exactly one opens the award/reel decoder; do not forge it.
    private static ValueTask GP_START_GAME_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 5)
        {
            return ValueTask.CompletedTask;
        }

        byte selector = packet.ReadU8();
        int selectedCharacterId = packet.ReadS32();
        if (selector is not (1 or 2 or 4 or 5)
            || selectedCharacterId is < 19_900_001 or > 19_900_015)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_START_GAME_ACK)
            .WriteU8(0)
            .WriteU8(0));
    }
}
