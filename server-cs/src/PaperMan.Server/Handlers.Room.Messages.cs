// =============================================================================
// 房間訊息 relay handlers — observer、radio、raw broadcast
//
// 此處只放 source-proven 的同形 relay；count-prefixed payload 必須與宣告長度完全一致。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 728 GR_OBSERVERCHAT_REQ (sub_56E560): wstr sender, wstr message —
    // 觀戰者聊天 (觀戰/錦標賽 n2==2 才走這條; 一般房走 125)
    // → 729 ACK (sub_56E610): wstr sender, wstr message — 全房廣播
    private static async ValueTask ObserverChat(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 4
            || packet.Remaining % 2 != 0
            || packet.Payload[^2] != 0
            || packet.Payload[^1] != 0)
        {
            return;
        }

        string sender = packet.ReadWStr();
        if (packet.Remaining < 2)
        {
            return;
        }

        string message = packet.ReadWStr();
        if (packet.Remaining != 0
            || !TryGetRoom(session, context, out var room, out _)
            || message.Length == 0)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_OBSERVERCHAT_ACK)
            .WriteWStr(sender)
            .WriteWStr(message));
    }

    // 726 GG_OBSERVERCHAT_REQ (builder case 10): str my_nick, str message —
    //   對戰中的觀戰者聊天 (⚠ ANSI str, 與 728 GR_OBSERVERCHAT 的 wstr 不同)
    // → 727 ACK (dispatcher 727 → sub_58D840 → sub_74A540): str nick,
    //   str message — 以 nick 定址顯示, 全房轉播。client 已附 nick,
    //   原樣轉播即可。
    private static async ValueTask ObserverChatGame(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 2 || packet.Payload[^1] != 0)
        {
            return;
        }

        string sender = packet.ReadStr();
        if (packet.Remaining < 1)
        {
            return;
        }

        string message = packet.ReadStr();
        if (packet.Remaining != 0
            || !TryGetRoom(session, context, out var room, out _)
            || message.Length == 0)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_OBSERVERCHAT_ACK)
            .WriteStr(sender.Length > 0 ? sender : session.Nickname)
            .WriteStr(message));
    }

    // 378 GR_RADIOMSG_REQ (sub_5593A0): u8 team(*(player+320) 0/1),
    //   u8 face(頁*9+項目, 0..26 無線電選單), u8 slot(發話者自身),
    //   u8 len(≤64 wchar 字數), wchar[len] (2*len bytes)
    // → 379 ACK (sub_74C500): 完全同構 — 以 slot 定位發話者、face 查選單
    //   語音。builder 已附 slot/team/face, server 原樣轉播全房即可 (不重組)。
    private static async ValueTask Radio(Session session, Packet packet, ServerContext context)
    {
        if (packet.Length < 4 || !TryGetRoom(session, context, out var room, out _))
        {
            return;
        }

        _ = packet.ReadU8();                                 // team (0/1)
        _ = packet.ReadU8();                                 // face (0..26)
        _ = packet.ReadU8();                                 // slot (發話者自身)
        byte len = packet.ReadU8();                          // wchar 字數
        if (len > 64 || packet.Remaining != 2 * len)
        {
            return;                                          // 對齊 client 上限 (sub_5593A0/74C500)
        }

        // 378/379 wire 同構 → 原樣轉播 (欄位皆 client 端已定, 不硬編)
        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_RADIOMSG_ACK).WriteRaw(packet.Payload));
    }

    // 437 GG_ROOMBROADCAST_REQ (sub_55B430): u8 flag + s32 len + raw[len]。
    //   ⚠ flag/blob 語意無從確認: builder 無直接呼叫者 (經函式指標/訊息表),
    //   且 dispatcher 與房訊息表皆無 438 case — client 從不解析 438,
    //   屬遺留/特殊工具 opcode。server 依 REQ→ACK 慣例原樣轉播全房 (438
    //   同構), 不硬編欄位。
    private static async ValueTask RoomBroadcast(Session session, Packet packet, ServerContext context)
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
