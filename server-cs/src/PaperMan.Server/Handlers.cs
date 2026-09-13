// =============================================================================
// 封包 handler — 每個 REQ 的 payload 佈局皆出自反編譯
// (builder 位址見 docs/PACKETS.md §3 與各處註解)。
// C# 14: 以 switch expression + Dictionary<Opcode, Handler> 註冊。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public delegate Task PacketHandler(Session s, Packet p, ServerContext ctx);

public sealed record ServerContext(Db Db, ServerConfig Config);

public sealed record ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 40200;
    /// <summary>exe .data 0xB69E88 抽出的 16 bytes; null = 明文模式 (測試用)。</summary>
    public byte[]? AesKey { get; init; }
    /// <summary>GL_ACCOUNTCONNSUCC(694) 送出的壓縮門檻; 0x2580 = 關閉壓縮。</summary>
    public ushort CompressThreshold { get; init; } = 0x2580;
    public string ServerName { get; init; } = "PaperMan Private";
    public string PublicHost { get; init; } = "127.0.0.1";
}

public static class Handlers
{
    public static readonly Dictionary<Opcode, PacketHandler> Table = new()
    {
        [Opcode.GT_PING_REQ] = Ping,                    // 101 (空 payload, builder 0x556xxx)
        [Opcode.GL_LOGIN_REQ] = Login,                  // 682
        [Opcode.GL_USERLIST_REQ] = UserList,            // 105 (payload s8=1, 客戶端限流 1s)
        [Opcode.GL_GAMEROOMINFO_REQ] = RoomList,        // 107 (空)
        [Opcode.GL_MYINFO_REQ] = MyInfo,                // 197 (空)
        [Opcode.GL_MYITEM_REQ] = MyItems,               // 199
        [Opcode.GM_CHECKNICK_REQ] = CheckNick,          // 210 (u8+str, sub_56B180)
        [Opcode.GM_CREATENICK_REQ] = CreateNick,        // 212 (u8+str)
        [Opcode.GS_CASH_REQ] = Cash,                    // 356
        [Opcode.GS_BUYITEM_REQ] = BuyItem,              // 204
        [Opcode.GS_BUY_ONCEITEM_REQ] = BuyOnceItem,     // 695
    };

    // ------------------------------------------------------------------ 101
    private static async Task Ping(Session s, Packet p, ServerContext ctx)
        => await s.SendAsync(new Packet(Opcode.GT_PING_ACK));           // 102 空 payload

    // ------------------------------------------------------------------ 682
    // REQ: str account, str token, u64 hw_key(混淆), u8 sec_state, byte[24]
    // ACK(681): 結構見 docs/PACKETS.md §1.4 (handler 0x43E651)
    private static async Task Login(Session s, Packet p, ServerContext ctx)
    {
        string account = p.ReadStr();
        string token = p.ReadStr();
        ulong hwObf = p.ReadU64();
        _ = p.ReadU8();                       // security_state
        _ = p.ReadRaw(Math.Min(24, p.Remaining));

        // 還原 hw_key 混淆: (v<<32|0xAA)^0xA4, hi^0xB1A9D7C7 (0x43E0F0)
        ulong hwKey = ((hwObf ^ 0xA4) & 0xFFFFFFFF) | (((hwObf >> 32) ^ 0xB1A9D7C7) << 32);

        var r = ctx.Db.Login(account, token, hwKey);
        var ack = new Packet(Opcode.GL_LOGIN_ACK).WriteS32(r.Result);

        if (r.Result == 1)
        {
            s.AccountId = r.AccountId;
            s.UserId = r.UserId;
            s.Nickname = r.Nickname;

            ack.WriteS32((int)r.UserId)       // user_no
               .WriteS32(100)                 // n100 (固定值)
               .WriteS32(0)                   // ext_count = 0 → 不帶 ext 三元組
               .WriteS16(1)                   // server_count
               // server entry
               .WriteS16(1)                                   // server id
               .WriteStr(ctx.Config.ServerName)               // name
               .WriteStr(ctx.Config.PublicHost)               // host (16B 定長區)
               .WriteS16((short)ctx.Config.Port)              // port
               .WriteU8(0)                                    // flag
               .WriteS16(0);                                  // group
            for (int g = 0; g < 3; g++)                       // 3 組 channel list
            {
                if (g == 0)
                {
                    ack.WriteS16(1)                           // ch_count
                       .WriteU8(0)                            // ch_type (≠3 → 無 extra byte)
                       .WriteStr("Ch.1")
                       .WriteS16((short)ctx.Config.Port)
                       .WriteU8(0);
                }
                else ack.WriteS16(0);
            }
            ack.WriteU32(0).WriteU32(0);                      // billing ×2
        }
        await s.SendAsync(ack);

        if (r.Result == 1)
        {
            // 694: u16 壓縮門檻 (client 0x43E651 若 <0x2580 就啟用壓縮)
            var succ = new Packet(Opcode.GL_ACCOUNTCONNSUCC)
                .WriteU16(ctx.Config.CompressThreshold);
            await s.SendAsync(succ);
        }
    }

    // ------------------------------------------------------------------ 105
    // ACK(106) sub_56A250: u16 count, u8 flags, u8 n, repeat n{...}
    private static async Task UserList(Session s, Packet p, ServerContext ctx)
    {
        var ack = new Packet(Opcode.GL_USERLIST_ACK)
            .WriteU16(0)      // total count
            .WriteU8(0)       // flags
            .WriteU8(0);      // n entries
        await s.SendAsync(ack);
    }

    // ------------------------------------------------------------------ 107
    // ACK(108) sub_568CE0: u8 mode, u8 count, repeat{...}
    private static async Task RoomList(Session s, Packet p, ServerContext ctx)
    {
        var ack = new Packet(Opcode.GL_GAMEROOMINFO_ACK)
            .WriteU8(0)       // mode (0 = 一般清單)
            .WriteU8(0);      // room count
        await s.SendAsync(ack);
    }

    // ------------------------------------------------------------------ 197
    // ACK(198) sub_570550 → CClientData (docs/PACKETS.md §3.2)
    private static async Task MyInfo(Session s, Packet p, ServerContext ctx)
    {
        var info = s.UserId != 0 ? ctx.Db.GetMyInfo(s.UserId) : null;
        var ack = new Packet(Opcode.GL_MYINFO_ACK);
        if (info is null)
        {
            ack.WriteBool(false);
            await s.SendAsync(ack);
            return;
        }

        var st = info.Stats;   // wins,losses,kills,deaths,headshots,combos,hearts,dkill,tkill,
                               // criticals,mkill,ukill,zkill,kkill,ddkill,playc,roundc,disc,playtime
        ack.WriteBool(true)
           .WriteS32((int)info.UserId)
           // --- sub_523BF0 基本資料 ---
           .WriteStr(info.Nickname)
           .WriteU8(info.CurrentChar)                 // char_type this+88
           .WriteS32(info.Level).WriteS32((int)info.Exp).WriteS32(0)   // this+92,96,108
           .WriteS32((int)st[0]).WriteS32((int)st[1]) // win, loss
           .WriteS32((int)st[2]).WriteS32((int)st[3]) // kill, death
           .WriteS32((int)st[17])                     // disconnect
           .WriteS32((int)st[4]).WriteS32((int)st[5]) // headshot, combo
           .WriteS32((int)st[6]).WriteS32((int)st[7]) // heart, dkill
           .WriteS32((int)st[8]).WriteS32((int)st[10])// tkill, mkill
           .WriteS32((int)st[11]).WriteS32((int)st[12]) // ukill, zkill
           .WriteS32((int)st[13]).WriteS32((int)st[14]) // kkill, ddkill
           .WriteS32((int)st[9])                      // critical
           .WriteS32((int)st[15]).WriteS32((int)st[16]) // playc, roundc
           .WriteU8(0).WriteU8(0).WriteU8(0)          // flags this+304..306
           .WriteS32(info.Cash)                       // this+104
           .WriteS32(0).WriteS32(0)                   // this+112,116
           .WriteRaw(new byte[48])                    // extra blob this+208
           .WriteU8(info.CurrentChar);                // slot_current this+4

        // --- sub_524010 角色槽 ---
        var chars = ctx.Db.GetCharacters(info.UserId);
        ack.WriteU8((byte)Math.Min(chars.Count, 20));
        foreach (var c in chars.Take(20))
        {
            ack.WriteU8(c.CharType);
            foreach (var e in c.Equip) ack.WriteU16(e);
        }

        // --- sub_524660 武器編組 (4 組, 未裝備) ---
        ack.WriteU8(4);
        for (byte g = 0; g < 4; g++)
        {
            ack.WriteU8(g).WriteU16(0);                // group_no, equipped=0 → 不帶 parts
            if (g != 3) ack.WriteU16(0).WriteU16(0).WriteU16(0);
        }

        // --- 技能欄 + 快速槽 (各 7×s32) ---
        ack.WriteU8(7);
        for (int i = 0; i < 7; i++) ack.WriteS32(0);
        ack.WriteU8(7);
        for (int i = 0; i < 7; i++) ack.WriteS32(0);

        ack.WriteU16(0)                                // clan/channel id
           .WriteS32((int)info.Gp)                     // game_point
           .WriteU8(0);                                // tutorial_count
        await s.SendAsync(ack);
    }

    // ------------------------------------------------------------------ 199
    // ACK(200) sub_570AB0: bool ok, s32 start, repeat{s32 slot(<0 結束), s32 item,
    //                       f32, f32, s32 period, u16 dura}
    private static async Task MyItems(Session s, Packet p, ServerContext ctx)
    {
        int start = p.Remaining >= 4 ? p.ReadS32() : 0;
        List<Db.InvItem> page = s.UserId != 0 ? ctx.Db.GetInventoryPage(s.UserId, start) : [];
        var ack = new Packet(Opcode.GL_MYITEM_ACK)
            .WriteBool(true)
            .WriteS32(start);
        foreach (var it in page)
            ack.WriteS32(it.Slot).WriteS32(it.ItemId)
               .WriteF32(it.F1).WriteF32(it.F2)
               .WriteS32(it.PeriodDaysLeft)
               .WriteU16(it.DuraCur);
        ack.WriteS32(-1);                              // sentinel: slot<0 = 結束
        await s.SendAsync(ack);
    }

    // ------------------------------------------------------------------ 210 / 212
    // REQ: u8, str nick (sub_56B180) — ACK(211/213): u8 result (sub_572D80/572E70)
    private static async Task CheckNick(Session s, Packet p, ServerContext ctx)
    {
        _ = p.ReadU8();
        var nick = p.ReadStr();
        byte result = nick.Length is < 2 or > 16 ? (byte)2 : ctx.Db.CheckNick(nick);
        await s.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    private static async Task CreateNick(Session s, Packet p, ServerContext ctx)
    {
        _ = p.ReadU8();
        var nick = p.ReadStr();
        byte result = 1;
        if (s.Authenticated && nick.Length is >= 2 and <= 16)
        {
            long uid = ctx.Db.CreateNick(s.AccountId, nick);
            if (uid != 0) { s.UserId = uid; s.Nickname = nick; result = 0; }
        }
        await s.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    // ------------------------------------------------------------------ 356
    // ACK(357) sub_572420: bool ok, s32 cash
    private static async Task Cash(Session s, Packet p, ServerContext ctx)
    {
        int cash = s.UserId != 0 ? ctx.Db.GetCash(s.UserId) : 0;
        await s.SendAsync(new Packet(Opcode.GS_CASH_ACK).WriteBool(true).WriteS32(cash));
    }

    // ------------------------------------------------------------------ 204 / 695
    // ACK(205) sub_571910: u8 count, repeat{bool ok, s32 item, f32, f32, s32 period,
    //                       u8 kind, u16 dura}
    private static async Task BuyItem(Session s, Packet p, ServerContext ctx)
    {
        byte count = p.ReadU8();
        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(count);
        for (int i = 0; i < count && p.Remaining > 0; i++)
        {
            int itemId = p.ReadS32();
            byte period = p.Remaining > 0 ? p.ReadU8() : (byte)0;
            byte useCash = p.Remaining > 0 ? p.ReadU8() : (byte)0;
            var r = s.UserId != 0
                ? ctx.Db.BuyItem(s.UserId, itemId, period, useCash != 0)
                : new Db.BuyResult(false, itemId, 0, 0, 0, 0, 0);
            ack.WriteBool(r.Ok);
            if (r.Ok)
                ack.WriteS32(r.ItemId).WriteF32(r.F1).WriteF32(r.F2)
                   .WriteS32(r.Period).WriteU8(r.Kind).WriteU16(r.Dura);
        }
        await s.SendAsync(ack);
    }

    // 695: s32 item_id, string opt, u8 kind, u8 period (docs §3.4)
    private static async Task BuyOnceItem(Session s, Packet p, ServerContext ctx)
    {
        int itemId = p.ReadS32();
        _ = p.ReadStr();
        _ = p.ReadU8();
        byte period = p.Remaining > 0 ? p.ReadU8() : (byte)0;
        var r = s.UserId != 0
            ? ctx.Db.BuyItem(s.UserId, itemId, period, useCash: true)
            : new Db.BuyResult(false, itemId, 0, 0, 0, 0, 0);
        var ack = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(1).WriteBool(r.Ok);
        if (r.Ok)
            ack.WriteS32(r.ItemId).WriteF32(r.F1).WriteF32(r.F2)
               .WriteS32(r.Period).WriteU8(r.Kind).WriteU16(r.Dura);
        await s.SendAsync(ack);
    }
}
