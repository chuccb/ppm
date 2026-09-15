// =============================================================================
// GR_ENDLOADING_REQ (183) → GR_ENDLOADING_ACK (184)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 183 REQ 空 → 184 ACK (sub_563B00): u8 n2(1), u8 slot, u8 slot2, u8
    //   — 每位成員載入完成後廣播; 全員到齊由 client 觸發 187
    private static async ValueTask GR_ENDLOADING_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        room.MarkLoaded(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ENDLOADING_ACK)
            .WriteU8(1)
            .WriteU8(slot)
            .WriteU8(slot)
            .WriteU8(0));
    }

}
