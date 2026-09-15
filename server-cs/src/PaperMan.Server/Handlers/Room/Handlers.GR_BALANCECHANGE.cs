// =============================================================================
// GR_BALANCECHANGE_REQ (364) → GR_BALANCECHANGE_ACK (365)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 364 GR_BALANCECHANGE_REQ (sub_56FA30): u8 — 房主切換隊伍平衡
    // → 365 ACK (sub_56FAE0→sub_431160): u8 只寫 GAMEROOM_TEAMBALANCE UI
    //   (一般房 room+186 不上 wire; 錦標賽 ctor sub_53F9F0 才寫 +186)
    private static async ValueTask GR_BALANCECHANGE_REQ(Session session, Packet packet, ServerContext context)
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

        room.TeamBalance = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_BALANCECHANGE_ACK).WriteU8(on));
    }

}
