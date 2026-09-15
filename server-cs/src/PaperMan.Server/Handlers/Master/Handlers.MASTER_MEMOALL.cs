// =============================================================================
// MASTER_MEMOALL_REQ (277) → MASTER_MEMOALL_ACK (278)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 277 MASTER_MEMOALL_REQ (sub_5789D0: wstr memo) → 278 ACK (sub_578BC0): wstr memo (全服廣播)
    private static async ValueTask MASTER_MEMOALL_REQ(Session session, Packet packet, ServerContext context)
    {
        var memo = packet.ReadWStr();
        var ack = new Packet(Opcode.MASTER_MEMOALL_ACK).WriteWStr(memo);

        foreach (var s in context.Sessions.All)
        {
            if (s.Authenticated)
            {
                try
                {
                    await s.SendAsync(Packet.FromPayload(ack.Opcode, ack.Payload));
                }
                catch
                {
                    // 忽略個別斷線
                }
            }
        }
    }
}
