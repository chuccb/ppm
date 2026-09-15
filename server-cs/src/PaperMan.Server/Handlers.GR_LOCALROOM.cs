// =============================================================================
// GR_LOCALROOM_REQ (366) → GR_LOCALROOM_ACK (367)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 366 GR_LOCALROOM_REQ (sub_585FD0): u8 — 房主切換區域限定房
    //   (n2_0==3 錦標賽場景時 client 不送; 名稱表未註冊, 由
    //   GAMEROOM_LOCALROOM UI 字串補名, 同 990/991 之例)
    // → 367 ACK (sub_586090→sub_437B50): u8 — 寫 GAMEROOM_LOCALROOM 勾選
    private static async ValueTask GR_LOCALROOM_REQ(Session session, Packet packet, ServerContext context)
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

        room.LocalRoom = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_LOCALROOM_ACK).WriteU8(on));
    }

}
