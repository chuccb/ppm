// =============================================================================
// Warehouse packet-family support
// This contains no receive entry. Its helpers encode or validate structures shared
// by the direct canonical request/ACK-family handler source files in this domain.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class WarehouseHandlers
{
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
    // ------------------------------------------------------------- helpers
    private static bool IsValidWarehouseTab(byte tab) =>
        tab is >= Db.WarehouseFirstTab and <= Db.WarehouseLastTab;


    /// <summary>物品 23B = {s32 slot, s32 item_id, f32, f32, s32 period, u8 kind, u16 dura}。</summary>
    private static void WriteWarehouseItem(Packet pkt, Db.WarehouseItem it) =>
        pkt.WriteS32(it.Slot).WriteS32(it.ItemId)
           .WriteF32(it.F1).WriteF32(it.F2)
           .WriteS32(it.PeriodDaysLeft)
           .WriteU8(it.Kind)
           .WriteU16(it.DuraCur);
}
