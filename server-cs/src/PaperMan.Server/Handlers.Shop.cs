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
    }

    private static async ValueTask Cash(Session s, Packet p, ServerContext ctx)
    {
        int cash = s.UserId != 0 ? ctx.Db.GetCash(s.UserId) : 0;
        await s.SendAsync(new Packet(Opcode.GS_CASH_ACK).WriteBool(true).WriteS32(cash));
    }

    // ACK(205) sub_571910 — ⚠ 交叉驗證修正的完整結構:
    //   u8 count
    //   repeat count: bool ok; ok 時 {s32 item, f32, f32, s32 period, u8 kind, u16 dura}
    //   若 count==0: 額外 bool + u8 (錯誤碼對)
    //   尾端固定 7×s32: pair(?,cash) pair(?,gp) pair(?,x) + s32 last
    //   (count!=0 時 v16→EE8D18=cash 顯示, v20→GP, v27→EE8D1C)
    private static async ValueTask BuyItems(Session s, Packet p, ServerContext ctx)
    {
        byte count = p.ReadU8();
        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(count);
        for (int i = 0; i < count && p.Remaining > 0; i++)
        {
            int itemId = p.ReadS32();
            byte period = p.Remaining > 0 ? p.ReadU8() : (byte)0;
            bool useCash = p.Remaining > 0 && p.ReadU8() != 0;
            WriteResult(ack, Buy(s, ctx, itemId, period, useCash));
        }
        await s.SendAsync(WriteTail(ack, s, ctx));
    }

    private static async ValueTask BuyOnceItem(Session s, Packet p, ServerContext ctx)
    {
        int itemId = p.ReadS32();
        _ = p.ReadStr();                                   // opt
        _ = p.ReadU8();                                    // kind (server 以 catalog 為準)
        byte period = p.Remaining > 0 ? p.ReadU8() : (byte)0;

        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(1);
        WriteResult(ack, Buy(s, ctx, itemId, period, useCash: true));
        await s.SendAsync(WriteTail(ack, s, ctx));
    }

    private static Db.BuyResult Buy(Session s, ServerContext ctx, int itemId, byte period, bool useCash) =>
        s.UserId != 0
            ? ctx.Db.BuyItem(s.UserId, itemId, period, useCash)
            : Db.BuyResult.Fail(itemId);

    private static void WriteResult(Packet ack, Db.BuyResult r)
    {
        ack.WriteBool(r.Ok);
        if (r.Ok)
            ack.WriteS32(r.ItemId).WriteF32(r.F1).WriteF32(r.F2)
               .WriteS32(r.Period).WriteU8(r.Kind).WriteU16(r.Dura);
    }

    /// <summary>205 尾端 7×s32 (client 無條件讀取, 順序見 sub_571910 v22/v16/v26/v20/v15/v27/v18)。</summary>
    private static Packet WriteTail(Packet ack, Session s, ServerContext ctx)
    {
        int cash = s.UserId != 0 ? ctx.Db.GetCash(s.UserId) : 0;
        var info = s.UserId != 0 ? ctx.Db.GetMyInfo(s.UserId) : null;
        int gp = (int)(info?.Gp ?? 0);
        return ack
            .WriteS32(0).WriteS32(cash)     // v22, v16 → EE8D18 (CASH 顯示)
            .WriteS32(0).WriteS32(gp)       // v26, v20 → GP 顯示
            .WriteS32(0).WriteS32(0)        // v15, v27 → EE8D1C
            .WriteS32(0);                   // v18 (旗標, 進 UI callback)
    }
}
