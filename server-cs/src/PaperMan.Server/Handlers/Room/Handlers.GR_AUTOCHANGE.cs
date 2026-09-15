// =============================================================================
// GR_AUTOCHANGE_REQ (177) → GR_AUTOCHANGE_ACK (178)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 177 GR_AUTOCHANGE_REQ (sub_56F8A0): s8 — client 端無呼叫者 (死碼),
    // 178 ACK 亦不在 sub_58B010 主 switch (走 vtable 前置轉發器)。僅吸收。
    private static ValueTask GR_AUTOCHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return ValueTask.CompletedTask;
        }

        _ = packet.ReadS8();
        return ValueTask.CompletedTask;
    }

}
