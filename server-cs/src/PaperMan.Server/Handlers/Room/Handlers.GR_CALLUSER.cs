// =============================================================================
// GR_CALLUSER_REQ (191) → GR_CALLUSER_ACK (192)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 191 REQ (sub_56FD60): str nick — 呼叫指定玩家 (房內點名)。
    // → 192 ACK (sub_56FE10): 僅當接收者房狀態==2 (在房內) 才讀 body,
    //   u8 caller_slot + str caller_nick → sub_406DB0 彈「呼叫」視窗;
    //   否則連 body 都不讀 (sub_5376F0(byte_EE8968)=+24 非 2 即返回)。
    //   server 側: 雙方需同房才送 body (離線/異房一律靜默 — 192 無錯誤碼)。
    private static async ValueTask GR_CALLUSER_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 1 || packet.Payload[^1] != 0)
        {
            return;
        }

        string targetNick = packet.ReadStr();
        if (packet.Remaining != 0 || targetNick.Length == 0)
        {
            return;
        }

        var target = context.Sessions.Find(targetNick);
        if (target is null || ReferenceEquals(target, session))
        {
            return;
        }

        // 雙方需同房 (sub_56FE10 依接收者房狀態==2 才讀 body)
        if (!TryGetRoom(session, context, out _, out var callerSlot)
            || target.RoomNo != session.RoomNo)
        {
            return;
        }

        await target.SendAsync(new Packet(Opcode.GR_CALLUSER_ACK)
            .WriteU8(callerSlot)                            // 呼叫者 slot
            .WriteStr(session.Nickname));                   // 呼叫者暱稱
    }

}
