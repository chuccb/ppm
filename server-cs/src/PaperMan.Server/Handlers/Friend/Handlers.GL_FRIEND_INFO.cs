// =============================================================================
// GL_FRIEND_INFO_REQ (435) → GL_FRIEND_INFO_ACK (436)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
    // 436 (sub_55B2C0): u8 count, count×{str nick, u8 online,
    //   [online==1: str where, u8 channel]}
    private static async ValueTask GL_FRIEND_INFO_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        // 單機伺服器: 查詢對象一律回「離線」(online=0 → 不帶 where/ch)
        await session.SendAsync(new Packet(Opcode.GL_FRIEND_INFO_ACK)
            .WriteU8(1)
            .WriteStr(nick)
            .WriteU8(0));
    }
}
