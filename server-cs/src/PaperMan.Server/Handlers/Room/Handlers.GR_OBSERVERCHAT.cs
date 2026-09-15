// =============================================================================
// GR_OBSERVERCHAT_REQ (728) → GR_OBSERVERCHAT_ACK (729)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 728 GR_OBSERVERCHAT_REQ (sub_56E560): wstr sender, wstr message —
    // 觀戰者聊天 (觀戰/錦標賽 n2==2 才走這條; 一般房走 125)
    // → 729 ACK (sub_56E610): wstr sender, wstr message — 全房廣播
    private static async ValueTask GR_OBSERVERCHAT_REQ(Session session, Packet packet, ServerContext context)
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

}
