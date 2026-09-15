// =============================================================================
// GR_TEAMSHUFFLE_REQ (894) → GR_TEAMSHUFFLE_ACK (895)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 894 GR_TEAMSHUFFLE_REQ (sub_585DC0): u8 room_no, u8 map — 房主執行
    //   隊打散 (map 為 client 目前地圖, server 不重驗)
    // → 895 ACK (sub_585E70→sub_435680): u8 status; ==1 → u16(讀後丟棄),
    //   u8 count, count×(u8 slot, s32 uid) — 全房依 uid 重排槽位。
    //   status 語意由 msgtableres.lang 解出 (sub_435680 各 case 播的
    //   sub_408080 訊息): 7=權限不足, 8=模式不支援, 6=不足3人,
    //   13=未分兩隊, 5=尚未全員 ready, 14=人數過多, 12=進行中不可,
    //   3/4/9/11=其他失敗 — 詳 docs/PACKETS.md §3.15b2。
    private static async ValueTask GR_TEAMSHUFFLE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        _ = packet.ReadU8();                                     // room_no (client 附自己房號)
        _ = packet.ReadU8();                                     // map (client 附目前地圖)

        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        byte status = 1;                                         // 成功
        if (!IsMaster(room, slot))
        {
            status = 7;                                          // 權限がないため…
        }
        else if (!IsNativeTwoTeamMode(room.ModeIndex))
        {
            status = 8;                                          // 支援しないモードです
        }
        else if (room.Members.Count < 3)
        {
            status = 6;                                          // 3人以上のプレイヤーが必要
        }

        var ack = new Packet(Opcode.GR_TEAMSHUFFLE_ACK).WriteU8(status);
        if (status == 1)
        {
            var assignment = room.ShuffleSlots();
            ack.WriteU16(0)                                      // client 讀後丟棄
               .WriteU8((byte)assignment.Count);
            foreach (var (newSlot, member) in assignment)
            {
                ack.WriteU8(newSlot)
                   .WriteS32((int)member.UserId);
            }
        }

        await RoomManager.BroadcastAsync(room, ack);
    }
}
