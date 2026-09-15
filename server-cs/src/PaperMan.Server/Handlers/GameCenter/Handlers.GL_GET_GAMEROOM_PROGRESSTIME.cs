// =============================================================================
// GL_GET_GAMEROOM_PROGRESSTIME_REQ (485) → GL_GET_GAMEROOM_PROGRESSTIME_ACK (486)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class GameCenterHandlers
{
    // 485 GL_GET_GAMEROOM_PROGRESSTIME_REQ (sub_56AD90: u8 room_no)
    // → 486 ACK (sub_56AE30): u8 n3, s16 v30, u8 Id, s32 n999, u8 v26, s8 v24, u8 v32, s8 v28, u8 n11, s8 v27, u8 v29, s8 v23
    private static async ValueTask GL_GET_GAMEROOM_PROGRESSTIME_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var room = context.Rooms.Find(roomNo);

        int progressSec = 0;
        if (room is not null && room.Playing)
        {
            progressSec = 60; // 模擬戰鬥已進行 60 秒
        }

        var ack = new Packet(Opcode.GL_GET_GAMEROOM_PROGRESSTIME_ACK)
            .WriteU8(0)                  // n3 = 0 (一般模式)
            .WriteS16(roomNo)            // v30
            .WriteU8(0)                  // Id
            .WriteS32(progressSec)       // n999 (progress time seconds)
            .WriteU8(0)                  // v26
            .WriteU8(0)                  // v24
            .WriteU8(0)                  // v32
            .WriteU8(0)                  // v28
            .WriteU8(0)                  // n11
            .WriteU8(0)                  // v27
            .WriteU8(0)                  // v29
            .WriteU8(0);                 // v23

        await session.SendAsync(ack);
    }
}
