// =============================================================================
// GR_MAPCHANGE_REQ (121) → GR_MAPCHANGE_ACK (122)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 121 REQ (sub_56E480): u8 map → 122 ACK (sub_56E530): u8 map —
    // 房主換圖廣播; sub_42FC50 以 sub_540280 寫 room+130 (map)
    private static async ValueTask GR_MAPCHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte mapId = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.MapId = ResolveMap(mapId, room.Rule, context.Db);  // 121 依 mode→bit 過濾

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
    }

}
