// =============================================================================
// GR_RULECHANGE_REQ (169) → GR_RULECHANGE_ACK (170)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 169 GR_RULECHANGE_REQ (sub_56F440): u8 mode(modeIndex) — 房主改遊戲模式
    // → 170 ACK (sub_56F4F0→sub_42FE50): u8 mode — client 以 sub_53FBB0 重建
    //   mode UI 並以 sub_426930(mode) 回推預設地圖寫 +130 (sub_540280)。
    //   server 同步鏡像: 有 map_StartIndex 條目的 mode 重設 MapId, 其餘
    //   (練習/教學/聊天/射擊館…無地圖目錄) 保留原圖 — 見 ModeIndexDefaultMap。
    private static async ValueTask GR_RULECHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte modeIndex = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot)
        {
            return;
        }

        room.ModeIndex = modeIndex;
        // client sub_426930(modeIndex) writes that mode's default map to +130;
        // server 鏡像: 有預設圖的 mode 重置, 其餘保留; 兩者皆再經 ResolveMap
        // 依 mode→bit 過濾 (防呆, 正常預設圖必合法故為 no-op)。
        byte oldMap = room.MapId;
        room.MapId = ResolveMap(
            ModeIndexDefaultMap.TryGetValue(modeIndex, out byte defaultMap) ? defaultMap : room.MapId,
            modeIndex, context.Db);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_RULECHANGE_ACK).WriteU8(modeIndex));
        // 例外: SOCCER 預設圖 98 是 TS 圖 (map_StartIndex 原廠 bug), ResolveMap
        // 會回退成 99 — client 卻仍照 98 寫 +130; 補發 122 把 client 拉回一致。
        if (room.MapId != oldMap)
        {
            await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
        }
    }

}
