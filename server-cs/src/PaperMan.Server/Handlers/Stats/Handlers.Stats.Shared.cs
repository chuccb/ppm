// =============================================================================
// GP_CH counter packet-family support
// This contains no receive entry. Every direct GP_CH*_REQ file supplies its verified
// ACK opcode, database column whitelist value, and native one- or two-s32 ACK shape.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class StatHandlers
{
    private enum GP_CH_CounterAcknowledgementShape
    {
        Single,                                             // 223..235: 1×s32 total
        Pair,                                               // 237..389: s32 total + s32 extra
    }

    private static async ValueTask UpdateGP_CH_CounterAsync(
        Session session,
        Packet packet,
        ServerContext context,
        Opcode acknowledgementOpcode,
        string column,
        GP_CH_CounterAcknowledgementShape acknowledgementShape)
    {
        // REQ = client 的新絕對累計值 (sub_5567F0 等: a1>=0 才送)
        long total = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        if (total < 0)
        {
            total = 0;
        }

        if (session.UserId != 0)
        {
            total = context.Db.SetStatMax(session.UserId, column, total);   // 只允許單調遞增
        }

        var reply = new Packet(acknowledgementOpcode).WriteS32((int)total);
        if (acknowledgementShape is GP_CH_CounterAcknowledgementShape.Pair)
        {
            reply.WriteS32(0);                                    // extra (UI 顯示用)
        }

        await session.SendAsync(reply);
    }
}
