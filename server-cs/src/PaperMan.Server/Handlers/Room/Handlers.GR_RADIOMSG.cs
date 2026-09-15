// =============================================================================
// GR_RADIOMSG_REQ (378) → GR_RADIOMSG_ACK (379)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 378 GR_RADIOMSG_REQ (sub_5593A0): u8 team(*(player+320) 0/1),
    //   u8 face(頁*9+項目, 0..26 無線電選單), u8 slot(發話者自身),
    //   u8 len(≤64 wchar 字數), wchar[len] (2*len bytes)
    // → 379 ACK (sub_74C500): 完全同構 — 以 slot 定位發話者、face 查選單
    //   語音。builder 已附 slot/team/face, server 原樣轉播全房即可 (不重組)。
    private static async ValueTask GR_RADIOMSG_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Length < 4 || !TryGetRoom(session, context, out var room, out _))
        {
            return;
        }

        _ = packet.ReadU8();                                 // team (0/1)
        _ = packet.ReadU8();                                 // face (0..26)
        _ = packet.ReadU8();                                 // slot (發話者自身)
        byte len = packet.ReadU8();                          // wchar 字數
        if (len > 64 || packet.Remaining != 2 * len)
        {
            return;                                          // 對齊 client 上限 (sub_5593A0/74C500)
        }

        // 378/379 wire 同構 → 原樣轉播 (欄位皆 client 端已定, 不硬編)
        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_RADIOMSG_ACK).WriteRaw(packet.Payload));
    }

}
