// =============================================================================
// GR_AI_DAMAGE_SHIELD_REQ (922) → GR_AI_DAMAGE_SHIELD_ACK (923)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class AIHandlers
{
    // 922 GR_AI_DAMAGE_SHIELD_REQ (sub_7616B0: s16 shield_id, s16 damage, s16 remain, f32 unk)
    // → 923 ACK (sub_761710): 同步給房間內所有玩家
    private static async ValueTask GR_AI_DAMAGE_SHIELD_REQ(Session session, Packet packet, ServerContext context)
    {
        short shieldId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        short damage = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        short remain = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        float unk = packet.Remaining >= 4 ? packet.ReadF32() : 0f;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_DAMAGE_SHIELD_ACK)
                .WriteS16(shieldId)
                .WriteS16(damage)
                .WriteS16(remain)
                .WriteF32(unk);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }
}
