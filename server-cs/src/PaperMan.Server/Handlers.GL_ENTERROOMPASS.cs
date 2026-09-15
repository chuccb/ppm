// =============================================================================
// GL_ENTERROOMPASS_REQ (216) → GL_ENTERROOMPASS_ACK (217)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 216 REQ: u8 room_no, str pass → 217 ACK: u8 result → 成功後 client 送 113
    private static async ValueTask GL_ENTERROOMPASS_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 2 || packet.Payload[^1] != 0)
        {
            return;
        }

        byte roomNo = packet.ReadU8();
        string pass = packet.ReadStr();
        if (packet.Remaining != 0)
        {
            return;
        }

        var room = context.Rooms.Find(roomNo);

        bool ok = room is not null
            && (room.Password is null || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_ENTERROOMPASS_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0));
    }

}
