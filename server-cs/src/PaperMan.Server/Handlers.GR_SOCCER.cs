// =============================================================================
// GR_SOCCER_REQ (969) → GR_SOCCER_ACK (970)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 969 GR_SOCCER_REQ (sub_5860C0): u8 — 房主切換足球模式
    //   (名稱表未註冊, 由 GAMEROOM_SOCCER UI 字串補名)
    // → 970 ACK (sub_586180→sub_437D00): u8 — 寫 mode rule 物件 +14
    //   (sub_74F4D0) 並勾選 GAMEROOM_SOCCER
    private static async ValueTask GR_SOCCER_REQ(Session session, Packet packet, ServerContext context)
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

        room.Soccer = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_SOCCER_ACK).WriteU8(on));
    }

}
