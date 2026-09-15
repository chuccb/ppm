// =============================================================================
// GL_JOININFO_REQ (264) → GL_JOININFO_ACK (265)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    // 264 GL_JOININFO_REQ (u8 room_no) → 265 房資訊。
    // 265 佈局 (sub_574550, 兩分支同構; n2_0==3 的 TeamSurvival 分支寫入
    // byte_E9FBA8, 一般分支寫入 dword_EA063C「目前房」物件):
    //   u8 status(0=可進房, →modeUI+12), u8 map(+409「ROOM_MAP」),
    //   u8 count(+1), u8 B(+408 存而不讀), u16 slot_mask(+6),
    //   u8 C(+410 存而不讀), u8 time(+411「ROOM_TIME」),
    //   u16 round(+412「ROOM_ROUND」), u8 item(+414「ROOM_ITEM」),
    //   u16 G(+416 存而不讀), count × (u8 slot, str name[24], u8 讀後丟棄)
    //   — sub_515DE0 只顯示 map/time/round/item 四欄, +408/+410/+416
    //   全程無讀者 (同 114 的 +146/+150 送 0 安全), 尾欄 u8 讀入 v13
    //   後無引用, 送 0。
    private static async ValueTask GL_JOININFO_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        var room = context.Rooms.Find(roomNo);

        if (room is null)
        {
            var gone = new Packet(Opcode.GL_JOININFO_ACK)
                .WriteU8(1)                                 // status: 不可進房
                .WriteU8(0).WriteU8(0).WriteU8(0)           // map / count / B
                .WriteU16(0).WriteU8(0).WriteU8(0)          // slot_mask / C / time
                .WriteU16(0).WriteU8(0).WriteU16(0);        // round / item / G
            await session.SendAsync(gone);
            return;
        }

        var ack = new Packet(Opcode.GL_JOININFO_ACK)
            .WriteU8(0)                                     // status: 可進房
            .WriteU8(room.MapId)                            // +409 ROOM_MAP
            .WriteU8((byte)room.Members.Count)              // +1 人數
            .WriteU8(0)                                     // +408 (client 存而不讀)
            .WriteU16(room.SlotMask)                        // +6 槽位點陣
            .WriteU8(0)                                     // +410 (client 存而不讀)
            .WriteU8(room.TimeLimit)                        // +411 ROOM_TIME
            .WriteU16(room.WinCount)                        // +412 ROOM_ROUND
            .WriteU8(room.ItemMode)                         // +414 ROOM_ITEM
            .WriteU16(0);                                   // +416 (client 存而不讀)

        foreach (var (slot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            ack.WriteU8(slot)                               // 玩家槽位
               .WriteStr(member.Nickname)                   // 24B 名字 (sub_592730 讀入 v14[6])
               .WriteU8(0);                                 // 尾欄 (client 讀後丟棄)
        }

        await session.SendAsync(ack);
    }

}
