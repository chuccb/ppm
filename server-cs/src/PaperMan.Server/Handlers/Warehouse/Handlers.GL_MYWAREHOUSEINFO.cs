// =============================================================================
// GL_MYWAREHOUSEINFO_REQ (855) → GL_MYWAREHOUSEINFO_ACK (856)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class WarehouseHandlers
{
    // 855 (s32 self_uid) → 856: u8 err + [err==0] 70B 狀態塊
    private static async ValueTask GL_MYWAREHOUSEINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        // dword_EE8CB4 = 自己 uid (client 恆送自己; 私服以 session 為準, 僅消耗不驗證)
        if (packet.Remaining >= 4)
        {
            packet.ReadS32();
        }

        var ack = new Packet(Opcode.GL_MYWAREHOUSEINFO_ACK).WriteU8(0);
        if (session.UserId != 0)
        {
            WriteGL_MYWAREHOUSEINFO_ACK_StatusBlock(ack, context.Db.GetWarehouseInfo(session.UserId));
        }
        else
        {
            WriteGL_MYWAREHOUSEINFO_ACK_StatusBlock(ack, new Db.WarehouseTabState[Db.WarehouseTabCount]);
        }

        await session.SendAsync(ack);
    }

    /// <summary>70B 狀態塊 = 7 × {s32 count, s32 到期(打包), u8 loaded, u8 pad}。</summary>
    private static void WriteGL_MYWAREHOUSEINFO_ACK_StatusBlock(Packet pkt, ReadOnlySpan<Db.WarehouseTabState> tabs)
    {
        for (int tab = 0; tab < Db.WarehouseTabCount; tab++)
        {
            var state = tab < tabs.Length ? tabs[tab] : default;
            pkt.WriteS32(state.Count)
               .WriteS32(state.ExpiryPacked)
               .WriteU8(0)                                   // loaded: client 載入後自行標記
               .WriteU8(0);                                  // pad
        }
    }
}
