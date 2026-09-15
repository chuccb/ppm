// =============================================================================
// GL_FRIEND_WHERE_REQ (441) → GL_FRIEND_WHERE_ACK (442)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // 441 GL_FRIEND_WHERE_REQ (sub_55B940): str nick — 查好友所在位置。
    // → 442 (sub_55B9F0): status==2 只讀 status (0x21D 找不到資訊);
    //   status==1 續讀 u8 where_type, u8 channel, u8 room_no。
    //   私服無教學/大師/錦標賽, 上線一律回 where_type=0 (大廳 → 0x21E
    //   "%sさんはロビーで待機中です"), channel/room_no=0 (單頻道, 房位留後續)。
    private static async ValueTask GL_FRIEND_WHERE_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        if (nick.Length == 0)
        {
            return;
        }

        if (context.Sessions.Find(nick) is null)
        {
            // 離線/不存在 → 找不到資訊 (0x21D)
            await session.SendAsync(new Packet(Opcode.GL_FRIEND_WHERE_ACK).WriteU8(2));
            return;
        }

        // 上線 → 大廳 (client 對 n9 ∉ {9,10,11} 一律顯示 0x21E 大廳待機)
        await session.SendAsync(new Packet(Opcode.GL_FRIEND_WHERE_ACK)
            .WriteU8(1)                                     // 找到
            .WriteU8(0)                                     // where_type: 0 = 大廳
            .WriteU8(0)                                     // channel (單頻道 = 0)
            .WriteU8(0));                                   // room_no (房位留後續)
    }
}
