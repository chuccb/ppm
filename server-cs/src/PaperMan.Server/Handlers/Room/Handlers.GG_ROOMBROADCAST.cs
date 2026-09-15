// =============================================================================
// GG_ROOMBROADCAST_REQ (437) → GG_ROOMBROADCAST_ACK (438)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 437 GG_ROOMBROADCAST_REQ (sub_55B430): u8 flag + s32 len + raw[len]。
    //   ⚠ flag/blob 語意無從確認: builder 無直接呼叫者 (經函式指標/訊息表),
    //   且 dispatcher 與房訊息表皆無 438 case — client 從不解析 438,
    //   屬遺留/特殊工具 opcode。server 依 REQ→ACK 慣例原樣轉播全房 (438
    //   同構), 不硬編欄位。
    private static async ValueTask GG_ROOMBROADCAST_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Length < 5 || !TryGetRoom(session, context, out var room, out _))
        {
            return;
        }

        _ = packet.ReadU8();                                 // flag (語意未明, 原樣保留)
        uint len = packet.ReadU32();                         // 後隨 blob 長度
        if (len != (uint)packet.Remaining)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GG_ROOMBROADCAST_ACK).WriteRaw(packet.Payload));
    }
}
