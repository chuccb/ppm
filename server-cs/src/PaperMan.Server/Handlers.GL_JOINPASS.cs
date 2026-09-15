// =============================================================================
// GL_JOINPASS_REQ (262) → GL_JOINPASS_ACK (263)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    // 262 GL_JOINPASS_REQ (u8 room_no str pass) → 263 (u8 code)。
    // code: 0 = 密碼錯/房不存在; 1 = 通過 (client 轉發 260)。
    private static async ValueTask GL_JOINPASS_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        string pass = packet.ReadStr();

        var room = context.Rooms.Find(roomNo);
        bool ok = room is not null && (string.IsNullOrEmpty(room.Password) || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_JOINPASS_ACK).WriteU8(ok ? (byte)1 : (byte)0));
    }

}
