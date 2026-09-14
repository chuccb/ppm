// =============================================================================
// 角色倉庫 handlers — GL_MYWAREHOUSE* 家族 (855-863), n11==19 倉庫場景。
//
//   wire 佈局 (docs/PACKETS.md §3.15c3, 反編譯定案):
//     855 REQ  s32 self_uid (dword_EE8CB4)  → 856 ACK u8 err + [err==0] 70B 狀態塊
//     857 REQ  u8 tab                      → 858 ACK u8 err, u8 tab +
//                [err==0] s32 count, s32 total, count×物品(23B)
//     859 REQ  u8 tab, s32 inv_slot        → 860 ACK u8 err, u8 tab, s32 slot +
//                [err==0] 物品(23B) + s32 tab_count
//     861 REQ  u8 tab, s32 wh_slot         → 862 ACK u8 err, u8 tab, s32 slot +
//                [err==0] 物品(23B) + s32 tab_count
//     863 NOTIFY (server→client): 70B 狀態塊 (頁籤租期異動才推; 本實作無)
//
//   物品(23B) = {s32 slot, s32 item_id, f32 f1, f32 f2, s32 period(天),
//                u8 kind, u16 dura} — 與背包 GL_MYITEM_ACK 同構 (sub_524F70)。
//   錯誤碼 (msgtableres.lang):
//     858 err 1..5 → 0x49C「ロッカー情報のロードに失敗しました。」
//     860 err 1..9 → 0x49A「アイテム移動が失敗しました。」
//     862 err 1/2/3/4/6/7 → 0x49A; err 5 → 0x49E「インベントリーに空きがありません。」
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class WarehouseHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_MYWAREHOUSEINFO_REQ, SendInfo);
        add(Opcode.GL_MYWAREHOUSEITEMLIST_REQ, SendItemList);
        add(Opcode.GL_PUSH_TO_WAREHOUSE_REQ, Push);
        add(Opcode.GL_POP_TO_WAREHOSUE_REQ, Pop);
    }

    // 855 (s32 self_uid) → 856: u8 err + [err==0] 70B 狀態塊
    private static async ValueTask SendInfo(Session session, Packet packet, ServerContext context)
    {
        // dword_EE8CB4 = 自己 uid (client 恆送自己; 私服以 session 為準, 僅消耗不驗證)
        if (packet.Remaining >= 4)
        {
            packet.ReadS32();
        }

        var ack = new Packet(Opcode.GL_MYWAREHOUSEINFO_ACK).WriteU8(0);
        if (session.UserId != 0)
        {
            WriteStatusBlock(ack, context.Db.GetWarehouseInfo(session.UserId));
        }
        else
        {
            WriteStatusBlock(ack, new Db.WarehouseTabState[Db.WarehouseTabCount]);
        }

        await session.SendAsync(ack);
    }

    // 857 (u8 tab) → 858: u8 err, u8 tab + [err==0] s32 count, s32 total, 物品…
    private static async ValueTask SendItemList(Session session, Packet packet, ServerContext context)
    {
        byte tab = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;

        var ack = new Packet(Opcode.GL_MYWAREHOUSEITEMLIST_ACK);
        if (!IsValidTab(tab) || session.UserId == 0)
        {
            await session.SendAsync(ack.WriteU8(1).WriteU8(tab));  // 0x49C 錯誤
            return;
        }

        var items = context.Db.GetWarehouseItems(session.UserId, tab);
        ack.WriteU8(0).WriteU8(tab).WriteS32(items.Count).WriteS32(items.Count);
        foreach (var it in items)
        {
            WriteItem(ack, it);
        }

        await session.SendAsync(ack);
    }

    // 859 (u8 tab, s32 inv_slot) → 860: u8 err, u8 tab, s32 slot + [err==0] 物品 + s32 tab_count
    private static async ValueTask Push(Session session, Packet packet, ServerContext context)
    {
        byte tab = packet.Remaining >= 5 ? packet.ReadU8() : (byte)0;
        int slot = packet.Remaining >= 4 ? packet.ReadS32() : -1;

        var ack = new Packet(Opcode.GL_PUSH_TO_WAREHOUSE_ACK);
        if (session.UserId == 0 || !IsValidTab(tab))
        {
            await session.SendAsync(ack.WriteU8(1).WriteU8(tab).WriteS32(slot));  // 0x49A
            return;
        }

        var (ok, err, item, count) = context.Db.PushToWarehouse(session.UserId, tab, slot);
        ack.WriteU8(err).WriteU8(tab).WriteS32(slot);
        if (ok && item is not null)
        {
            WriteItem(ack, item);
            ack.WriteS32(count);
        }

        await session.SendAsync(ack);
    }

    // 861 (u8 tab, s32 wh_slot) → 862: u8 err, u8 tab, s32 slot + [err==0] 物品 + s32 tab_count
    private static async ValueTask Pop(Session session, Packet packet, ServerContext context)
    {
        byte tab = packet.Remaining >= 5 ? packet.ReadU8() : (byte)0;
        int slot = packet.Remaining >= 4 ? packet.ReadS32() : -1;

        var ack = new Packet(Opcode.GL_POP_TO_WAREHOSUE_ACK);
        if (session.UserId == 0 || !IsValidTab(tab))
        {
            await session.SendAsync(ack.WriteU8(1).WriteU8(tab).WriteS32(slot));  // 0x49A
            return;
        }

        var (ok, err, item, count) = context.Db.PopFromWarehouse(session.UserId, tab, slot);
        ack.WriteU8(err).WriteU8(tab).WriteS32(slot);
        if (ok && item is not null)
        {
            WriteItem(ack, item);
            ack.WriteS32(count);
        }

        await session.SendAsync(ack);
    }

    // ------------------------------------------------------------- helpers
    private static bool IsValidTab(byte tab) =>
        tab is >= Db.WarehouseFirstTab and <= Db.WarehouseLastTab;

    /// <summary>物品 23B = {s32 slot, s32 item_id, f32, f32, s32 period, u8 kind, u16 dura}。</summary>
    private static void WriteItem(Packet pkt, Db.WarehouseItem it) =>
        pkt.WriteS32(it.Slot).WriteS32(it.ItemId)
           .WriteF32(it.F1).WriteF32(it.F2)
           .WriteS32(it.PeriodDaysLeft)
           .WriteU8(it.Kind)
           .WriteU16(it.DuraCur);

    /// <summary>70B 狀態塊 = 7 × {s32 count, s32 到期(打包), u8 loaded, u8 pad}。</summary>
    private static void WriteStatusBlock(Packet pkt, ReadOnlySpan<Db.WarehouseTabState> tabs)
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
