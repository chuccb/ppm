// =============================================================================
// GR_CHATTING_REQ (125) → GR_CHATTING_ACK (126)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 125 REQ (sub_56E860 wstr 版; sub_56E6C0 str 版為死碼 — 無呼叫者):
    //   s32 uid(server 回 126 時 client 讀後丟棄), u8 slot, wstr message
    // → 126 ACK (sub_56EA80): s32 uid(丟棄), u8 slot, wstr message —
    //   以 slot 定址顯示, 全房廣播
    private static async ValueTask GR_CHATTING_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        if (packet.Remaining < 7)
        {
            return;
        }

        int uid = packet.ReadS32();
        _ = packet.ReadU8();                                     // client 附 slot (以 server 記錄為準)

        // 唯一可達的 writer 是 UTF-16LE。完整 payload 必須正好以其雙 NUL
        // 收尾，否則不讓 malformed/trailing bytes 進入 room relay。
        if (packet.Remaining < 2
            || packet.Remaining % 2 != 0
            || packet.Payload[^2] != 0
            || packet.Payload[^1] != 0)
        {
            return;
        }

        string message = packet.ReadWStr();
        if (packet.Remaining != 0 || message.Length == 0)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        var notice = new Packet(Opcode.GR_CHATTING_ACK)
            .WriteS32(uid)
            .WriteU8(slot)
            .WriteWStr(message);
        await RoomManager.BroadcastAsync(room, notice);
    }

}
