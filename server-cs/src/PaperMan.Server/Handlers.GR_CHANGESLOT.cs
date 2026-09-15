// =============================================================================
// GR_CHANGESLOT_REQ (135) → GR_CHANGESLOT_ACK (136)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 135 REQ (sub_56EE90): u8 n254, u8 target_slot → 136 ACK (sub_56EF40):
    //   u8 mode; mode!=0 → 僅失敗提示音 (client 不再讀 body);
    //   mode==0 → u8 from_slot, u8 new_slot, s32(保留, 讀後丟棄),
    //             s32 mover_uid, u8 count, count×(u8 slot, s32 uid)
    //   全房依 uid 對位重建 slot 佈局 (更新 client dword_F6DCF4)。
    private static async ValueTask GR_CHANGESLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        _ = packet.ReadU8();                                     // n254 (模式參數)
        byte target = packet.ReadU8();

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room
            || target >= 16 || room.Members.ContainsKey(target))
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        var pair = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session));
        if (pair.Value is null)
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        byte from = pair.Key;
        room.Members.TryRemove(from, out _);
        room.Members[target] = session;
        if (from == room.MasterSlot)
        {
            room.MasterSlot = target;
        }

        // mode==0: 全房重建佈局 (mover 本人以 mover_uid + new_slot 更新自己)
        var ack = new Packet(Opcode.GR_CHANGESLOT_ACK)
            .WriteU8(0)                                     // mode: 完整重建
            .WriteU8(from)                                  // 舊 slot (client 讀後丟棄)
            .WriteU8(target)                                // 新 slot (本人 slot 更新判準)
            .WriteS32(0)                                    // 保留 (client 讀後丟棄)
            .WriteS32((int)session.UserId)                  // mover_uid
            .WriteU8((byte)room.Members.Count);
        foreach (var (slot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            ack.WriteU8(slot)
               .WriteS32((int)member.UserId);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

}
