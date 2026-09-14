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
        add(Opcode.GS_BUYCHAR_REQ, BuyCharacter);
        add(Opcode.GS_DELETEGIFT_REQ, DeleteGift);
        add(Opcode.GS_DESTROYITEM_REQ, DestroyItem);
        add(Opcode.GP_ENTER_PEPACHI_REQ, EnterPepachi);
        add(Opcode.GP_PEPACHI_LIST_REQ, PepachiList);
        add(Opcode.GP_START_GAME_REQ, StartPepachi);
        add(Opcode.GS_CAPSULEMACHINE_START_REQ, StartCapsuleMachine);
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

    // 310 GS_BUYCHAR_REQ (sub_529680): 6×s32 (char_type, item1..item5)
    // → 311 GS_BUYCHAR_ACK (sub_5728A0): u8 ok(1=成功), 6×s32
    private static async ValueTask BuyCharacter(Session session, Packet packet, ServerContext context)
    {
        int charType = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var existing = context.Db.GetCharacters(session.UserId);
        byte slotNo = (byte)existing.Count;
        // Validate the signed wire value before narrowing it to u8. Otherwise
        // 257/256/etc. could wrap into a legitimate canonical character type.
        bool ok = session.UserId != 0
            && slotNo < 20
            && Db.IsCanonicalCharacterType(charType)
            && context.Db.BuyCharacter(session.UserId, slotNo, (byte)charType);

        var ack = new Packet(Opcode.GS_BUYCHAR_ACK).WriteU8(ok ? (byte)1 : (byte)0);
        if (ok)
        {
            var info = context.Db.GetMyInfo(session.UserId);
            int cash = context.Db.GetCash(session.UserId);
            ack.WriteS32(slotNo)
               .WriteS32(charType)
               .WriteS32((int)(info?.Exp ?? 0))
               .WriteS32(cash)
               .WriteS32((int)(info?.Gp ?? 0))
               .WriteS32(100);                              // 出廠耐久
        }

        await session.SendAsync(ack);
    }

    // 453 GS_DELETEGIFT_REQ (sub_57BC40): s32 gift_uid, s32 item_id
    // → 454 GS_DELETEGIFT_ACK (sub_57BCF0): u8 ok(1=成功), s32 gift_uid, s32 item_id
    private static async ValueTask DeleteGift(Session session, Packet packet, ServerContext context)
    {
        int giftUid = packet.ReadS32();
        int itemId = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        bool ok = session.UserId != 0 && context.Db.DeleteGift(session.UserId, giftUid, itemId);

        var ack = new Packet(Opcode.GS_DELETEGIFT_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0)
            .WriteS32(giftUid)
            .WriteS32(itemId);

        await session.SendAsync(ack);
    }

    // 802 GS_DESTROYITEM_REQ (sub_894E70): s32 inv_id, s32 item_id, u8 type, s32 char_slot, s32 count
    // → 803 GS_DESTROYITEM_ACK (sub_895EE0): u8 err(0=成功), u8 unk(0), s32 pg, s32 cash, u8 count_affected, count×{s32 inv_id, s32 remain}
    private static async ValueTask DestroyItem(Session session, Packet packet, ServerContext context)
    {
        int invSlot = packet.ReadS32();
        int itemId = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        bool ok = session.UserId != 0 && context.Db.DestroyInventoryItem(session.UserId, invSlot, itemId);

        var ack = new Packet(Opcode.GS_DESTROYITEM_ACK);
        if (ok)
        {
            var info = context.Db.GetMyInfo(session.UserId);
            int cash = context.Db.GetCash(session.UserId);
            ack.WriteU8(0)                                  // err 0 = 成功
               .WriteU8(0)                                  // unk
               .WriteS32((int)(info?.Gp ?? 0))
               .WriteS32(cash)
               .WriteU8(1)                                  // 1 個 affected
               .WriteS32(invSlot)
               .WriteS32(0);                                // 剩餘 0 (已刪除)
        }
        else
        {
            ack.WriteU8(1)                                  // err != 0 失敗
               .WriteU8(0);
        }

        await session.SendAsync(ack);
    }

    // 698 GP_ENTER_PEPACHI_REQ (sub_580640, 空)
    // → 699 GP_ENTER_PEPACHI_ACK (sub_46AD00 case 699): u8 status(1), s32 coins, s32 cash
    private static async ValueTask EnterPepachi(Session session, Packet packet, ServerContext context)
    {
        int cash = session.UserId != 0 ? context.Db.GetCash(session.UserId) : 0;
        var ack = new Packet(Opcode.GP_ENTER_PEPACHI_ACK)
            .WriteU8(1)
            .WriteS32(100)                                  // coins
            .WriteS32(cash);

        await session.SendAsync(ack);
    }

    // 702 GP_PEPACHI_LIST_REQ (sub_580970, 空)
    // → 703 GP_PEPACHI_LIST_ACK (sub_46AD00 case 703): s32 normal_count, s32 rare_count, repeat s32 item_id
    private static async ValueTask PepachiList(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GP_PEPACHI_LIST_ACK)
            .WriteS32(0)                                    // normal_count
            .WriteS32(0);                                   // rare_count

        await session.SendAsync(ack);
    }

    // 700 GP_START_GAME_REQ (sub_580790): u8 count, s32 coin_type
    // → 701 GP_START_GAME_ACK (sub_84A000): u8 status(1), s32 win_item_id, s32 win_count, s32 remain_coins
    private static async ValueTask StartPepachi(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GP_START_GAME_ACK)
            .WriteU8(1)                                     // status 1 = 成功
            .WriteS32(0)                                    // win item
            .WriteS32(1)
            .WriteS32(99);                                  // remain coins

        await session.SendAsync(ack);
    }

    // 900 GS_CAPSULEMACHINE_START_REQ (sub_58D5D0): u8 count, s32 machine_id
    // → 901 GS_CAPSULEMACHINE_START_ACK (sub_9A1A30): u8 status(1), s32 win_item_id, s32 remain_tokens
    private static async ValueTask StartCapsuleMachine(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GS_CAPSULEMACHINE_START_ACK)
            .WriteU8(1)                                     // status 1 = 成功
            .WriteS32(0)                                    // win item
            .WriteS32(99);                                  // remain tokens

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
