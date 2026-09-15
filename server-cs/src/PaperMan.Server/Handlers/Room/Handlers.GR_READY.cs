// =============================================================================
// GR_READY_REQ (127) → GR_READY_ACK (128)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 127 REQ 空 → 128 ACK (sub_5626D0): u8 ready_flag, u8 slot —
    // ready 狀態翻轉廣播 (server 維護 per-slot ready 集合)
    private static async ValueTask GR_READY_REQ(Session session, Packet packet, ServerContext context)
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
        bool nowReady = room.ToggleReady(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_READY_ACK)
            .WriteU8(nowReady ? (byte)1 : (byte)0)
            .WriteU8(slot));
    }

}
