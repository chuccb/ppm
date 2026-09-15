// =============================================================================
// GR_NOSKILL_REQ (712) → GR_NOSKILL_ACK (713)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 712 GR_NOSKILL_REQ (sub_56FB10): u8 — 房主切換 no-skill 背景
    // → 713 ACK (sub_56FBC0→sub_4312C0): u8 寫 room+185 (noskillbg)
    private static async ValueTask GR_NOSKILL_REQ(Session session, Packet packet, ServerContext context)
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

        room.NoSkillBg = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_NOSKILL_ACK).WriteU8(on));
    }

}
