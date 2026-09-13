// =============================================================================
// 商店 handlers — 佈局出自反編譯:
//   356 GS_CASH_REQ         → 357 (sub_572420: bool ok, s32 cash)
//   204 GS_BUYITEM_REQ      → 205 (sub_571910: u8 count, repeat{bool, s32 item,
//                                  f32, f32, s32 period, u8 kind, u16 dura})
//   695 GS_BUY_ONCEITEM_REQ (s32 item, str opt, u8 kind, u8 period)
// period 白名單 (sub_570B00): 1/7/15/30/60/90 天 或 0=永久型。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ShopHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GS_CASH_REQ, Cash);
        add(Opcode.GS_BUYITEM_REQ, BuyItems);
        add(Opcode.GS_BUY_ONCEITEM_REQ, BuyOnceItem);
        add(Opcode.GS_GIVEGIFT_REQ, GiveGift);
        add(Opcode.GS_SELLITEM_REQ, SellItem);
    }

    // REQ(208) sub_572AD0: s32 slot_idx — 賣出單件
    // ACK(209) sub_572B80 (廿三輪自動審計重修): bool ok;
    //   ok → s32 v11, s32 gp_after(→PG 顯示), s32 item_id
    //   (client 以 item_id 掃背包快取移除該件; 單件交易無迴圈 —
    //    四/六輪的 count+repeat 版為誤讀, dispatcher 直查定案)
    private static async ValueTask SellItem(Session session, Packet packet, ServerContext context)
    {
        int slot = packet.ReadS32();
        var r = session.UserId != 0
            ? context.Db.SellItem(session.UserId, slot)
            : ((bool Ok, int ItemId, long GpAfter))(false, 0, 0);

        var ack = new Packet(Opcode.GS_SELLITEM_ACK).WriteBool(r.Ok);
        if (r.Ok)
        {
            ack.WriteS32(0)                                 // v11 (保留)
               .WriteS32((int)r.GpAfter)                    // → *EE8D18 PG 顯示
               .WriteS32(r.ItemId);                         // 背包快取移除鍵
        }

        await session.SendAsync(ack);
    }

    private static async ValueTask Cash(Session session, Packet packet, ServerContext context)
    {
        int cash = session.UserId != 0 ? context.Db.GetCash(session.UserId) : 0;
        await session.SendAsync(new Packet(Opcode.GS_CASH_ACK).WriteBool(true).WriteS32(cash));
    }

    // ACK(205) sub_571910 — ⚠ 交叉驗證修正的完整結構:
    //   u8 count
    //   repeat count: bool ok; ok 時 {s32 item, f32, f32, s32 period, u8 kind, u16 dura}
    //   若 count==0: 額外 bool + u8 (錯誤碼對)
    //   尾端固定 7×s32: pair(?,cash) pair(?,gp) pair(?,x) + s32 last
    //   (count!=0 時 v16→EE8D18=cash 顯示, v20→GP, v27→EE8D1C)
    // REQ(204) builder @0x570A2C (六輪逐行驗證):
    //   u8 count; repeat count {s32 item_id, u8 kind, s16 period,
    //   [s16 -(idx+1) 只在 kind 12/13/17 = 顏色/貼圖類]}
    private static async ValueTask BuyItems(Session session, Packet packet, ServerContext context)
    {
        byte count = packet.ReadU8();
        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(count);
        for (int i = 0; i < count && packet.Remaining > 0; i++)
        {
            int itemId = packet.ReadS32();
            byte kind = packet.ReadU8();
            short period = packet.ReadS16();
            if (kind is 12 or 13 or 17 && packet.Remaining >= 2)
            {
                _ = packet.ReadS16();                            // 變體索引 (負編碼)
            }

            WriteResult(ack, Buy(session, context, itemId, (byte)period, useCash: true));
        }

        await session.SendAsync(WriteTail(ack, session, context));
    }

    // REQ(695) builder sub_570B00 (廿四輪自動抽取定案):
    //   s32 item_id, u8 kind, u8 period, u16 variant
    //   (七輪的 str(64) 版是誤讀 String 緩衝宣告 — 三個 builder 呼叫點
    //    序列一致: 592A20+592920+592920+5929A0, 無字串寫入)
    private static async ValueTask BuyOnceItem(Session session, Packet packet, ServerContext context)
    {
        int itemId = packet.ReadS32();
        _ = packet.ReadU8();                                    // kind (server 以 catalog 為準)
        byte period = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;

        if (packet.Remaining >= 2)
        {
            _ = packet.ReadU16();                               // 顏色/貼圖變體 (負編碼)
        }

        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(1);
        WriteResult(ack, Buy(session, context, itemId, period, useCash: true));
        await session.SendAsync(WriteTail(ack, session, context));
    }

    private static Db.BuyResult Buy(Session session, ServerContext context, int itemId, byte period, bool useCash) =>
        session.UserId != 0
            ? context.Db.BuyItem(session.UserId, itemId, period, useCash)
            : Db.BuyResult.Fail(itemId);

    // REQ(296) 完整版 builder @0x57A6xx (七輪讀畢):
    //   str to_nick, u8 has_msg, [str message], s32 item_id, u8 kind,
    //   u8 period, [u16 變體 只在 kind 12/13/17]
    // ACK(297) sub_57AA50: u8 result (0=成功 → 另 5×s32; 1..11 = 錯誤碼)
    private static async ValueTask GiveGift(Session session, Packet packet, ServerContext context)
    {
        var toNick = packet.ReadStr();
        var message = packet.ReadBool() ? packet.ReadStr() : null;
        int itemId = packet.ReadS32();
        byte kind = packet.ReadU8();
        byte period = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;

        if (kind is 12 or 13 or 17 && packet.Remaining >= 2)
        {
            _ = packet.ReadU16();                                // 顏色/貼圖變體 (負編碼)
        }

        byte result = session.UserId != 0
            ? context.Db.GiveGift(session.UserId, toNick, itemId, period, message)
            : (byte)1;

        var ack = new Packet(Opcode.GS_GIVEGIFT_ACK).WriteU8(result);
        if (result == 0)
        {
            int cash = context.Db.GetCash(session.UserId);
            ack.WriteS32(cash)                              // 扣款後餘額顯示組
               .WriteS32(0)
               .WriteS32(0)
               .WriteS32(0)
               .WriteS32(0);
        }

        await session.SendAsync(ack);
    }

    private static void WriteResult(Packet ack, Db.BuyResult r)
    {
        ack.WriteBool(r.Ok);

        if (r.Ok)
        {
            ack.WriteS32(r.ItemId)
               .WriteF32(r.F1)
               .WriteF32(r.F2)
               .WriteS32(r.Period)
               .WriteU8(r.Kind)
               .WriteU16(r.Dura);
        }
    }

    /// <summary>
    /// 205 尾端 7×s32 (client 無條件讀取; 順序 sub_571910
    /// v22/v16/v26/v20/v15/v27/v18)。
    /// ⚠ 十一輪以 UI 標籤逐槽定案 (先前 CASH/GP 對映相反):
    ///   v16 → *EE8D18 → 商店 "PG" 欄位 (GP 點數)
    ///   v20 → ArgList → 商店 "CASH" 欄位 (現金)
    ///   v27 → *EE8D1C → 商店 "CP" 欄位 (第三貨幣)
    /// </summary>
    private static Packet WriteTail(Packet ack, Session session, ServerContext context)
    {
        int cash = session.UserId != 0 ? context.Db.GetCash(session.UserId) : 0;
        var info = session.UserId != 0 ? context.Db.GetMyInfo(session.UserId) : null;
        int gp = (int)(info?.Gp ?? 0);

        return ack
            .WriteS32(0)                    // v22 (保留)
            .WriteS32(gp)                   // v16 → EE8D18 = "PG" (GP)
            .WriteS32(0)                    // v26 (保留)
            .WriteS32(cash)                 // v20 → "CASH"
            .WriteS32(0)                    // v15 (保留)
            .WriteS32(0)                    // v27 → EE8D1C = "CP"
            .WriteS32(0);                   // v18 (旗標, 進 UI callback)
    }
}
