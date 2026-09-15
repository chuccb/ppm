// =============================================================================
// GL_MYITEM_REQ (199) → GL_MYITEM_ACK (200)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // ACK(200) sub_570AB0 → sub_524B70(cd, pkt, extra=1):
    //   bool ok; ok 時: s32 start, repeat{s32 slot(<0 結束), s32 item, f32, f32,
    //   s32 period, u8 extra(200 專屬), u16 dura}
    //   (a3=0 的無-extra 版本屬 290/294 MASTER_USERINFO 系 sub_523A50 —
    //    GM 查他人資料, 與一般玩家路徑無關; 五輪驗證定案)
    private static async ValueTask GL_MYITEM_REQ(Session session, Packet packet, ServerContext context)
    {
        int start = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var ack = new Packet(Opcode.GL_MYITEM_ACK).WriteBool(true).WriteS32(start);

        int count = 0;
        if (session.UserId != 0)
        {
            foreach (var it in context.Db.GetInventoryPage(session.UserId, start))
            {
                ack.WriteS32(it.Slot).WriteS32(it.ItemId)
                   .WriteF32(it.F1).WriteF32(it.F2)
                   .WriteS32(it.PeriodDaysLeft)
                   .WriteU8(0)                                     // extra (sub_524B70 a3=1)
                   .WriteU16(it.DuraCur);
                count++;
            }
        }

        Console.WriteLine($"[s{session.Id}] GL_MYITEM_REQ: start={start}, item count={count}");
        await session.SendAsync(ack.WriteS32(-1));                       // sentinel
    }
}
