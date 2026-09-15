// =============================================================================
// GL_JOINGAME_REQ (266) → GL_JOINGAME_ACK (267)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    // 266 GL_JOINGAME_REQ (u8 room_no u8 flag) → 267 (u8 code u8 flag)。
    // sub_516900 全 code 皆為「接受」: 0/2/3→狀態1, 1/4→狀態2(存 flag),
    // 5→狀態5 — 故依 flag 回 code (0=玩家 / 1=觀戰) 並回傳 flag。
    private static async ValueTask GL_JOINGAME_REQ(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                // room_no
        byte flag = packet.ReadU8();                        // byte_1D0CFE6: 0=PLAY, 1=OBSERVE

        byte code = flag == 0 ? (byte)0 : (byte)1;
        await session.SendAsync(new Packet(Opcode.GL_JOINGAME_ACK)
            .WriteU8(code)
            .WriteU8(flag));
    }

}
