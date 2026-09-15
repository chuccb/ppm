// =============================================================================
// GG_OBSERVERCHAT_REQ (726) → GG_OBSERVERCHAT_ACK (727)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 726 GG_OBSERVERCHAT_REQ (builder case 10): str my_nick, str message —
    //   對戰中的觀戰者聊天 (⚠ ANSI str, 與 728 GR_OBSERVERCHAT 的 wstr 不同)
    // → 727 ACK (dispatcher 727 → sub_58D840 → sub_74A540): str nick,
    //   str message — 以 nick 定址顯示, 全房轉播。client 已附 nick,
    //   原樣轉播即可。
    private static async ValueTask GG_OBSERVERCHAT_REQ(Session session, Packet packet, ServerContext context)
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

}
