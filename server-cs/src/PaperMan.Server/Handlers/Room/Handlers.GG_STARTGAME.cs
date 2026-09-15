// =============================================================================
// GG_STARTGAME_REQ (187) → GG_STARTGAME_ACK (188)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 187 REQ 空 → 188 ACK (sub_563D60): u8 n2==1, u8 count, count×u8 slot
    //   — 開打廣播 (帶已載入成員名單)
    private static async ValueTask GG_STARTGAME_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var loaded = room.LoadedSlots;
        var ack = new Packet(Opcode.GG_STARTGAME_ACK)
            .WriteU8(1)
            .WriteU8((byte)loaded.Count);
        foreach (var slot in loaded)
        {
            ack.WriteU8(slot);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

}
