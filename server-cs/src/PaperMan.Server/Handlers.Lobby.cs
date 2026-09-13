// =============================================================================
// 大廳 handlers — 佈局出自反編譯:
//   105 GL_USERLIST_REQ (s8=1, 客戶端限流 1s) → 106 (sub_56A250 解析)
//   107 GL_GAMEROOMINFO_REQ (空)             → 108 (sub_568CE0 解析)
//   197 GL_MYINFO_REQ (空)                   → 198 (sub_570550 → CClientData)
//   199 GL_MYITEM_REQ                        → 200 (sub_570AB0 → sub_524B70 分頁)
//   210/212 GM_CHECK/CREATENICK (u8+str)     → 211/213 (u8 result)
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class LobbyHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_USERLIST_REQ, UserList);
        add(Opcode.GL_GAMEROOMINFO_REQ, RoomList);
        add(Opcode.GL_MYINFO_REQ, MyInfo);
        add(Opcode.GL_MYITEM_REQ, MyItems);
        add(Opcode.GM_CHECKNICK_REQ, CheckNick);
        add(Opcode.GM_CREATENICK_REQ, CreateNick);
    }

    // ACK(106) sub_56A250: u16 count; 若 count!=0 才有 u8 flags, u8 n,
    // repeat n{s32 uid, str nick, s32 status; uid>0 時 +s32 custom_tex, str}
    // count==0 → 之後不再讀任何欄位 (交叉驗證確認)
    private static async ValueTask UserList(Session s, Packet p, ServerContext ctx) =>
        await s.SendAsync(new Packet(Opcode.GL_USERLIST_ACK).WriteU16(0));

    // ACK(108) sub_568CE0: u8 mode (3=委派 sub_580A80), 其他: u8 count, repeat{
    //   u8 room_no, s8 state; state>=0 → 定長塊 (u8,s8,u8,u16,u8,s8,s8[100],s8,s8,u8,u8,u8)
    //                        state<0  → str title + 同組欄位 }
    private static async ValueTask RoomList(Session s, Packet p, ServerContext ctx) =>
        await s.SendAsync(new Packet(Opcode.GL_GAMEROOMINFO_ACK)
            .WriteU8(0).WriteU8(0));

    // ACK(198): 完整 CClientData 序列化 (docs/PACKETS.md §3.2)
    private static async ValueTask MyInfo(Session s, Packet p, ServerContext ctx)
    {
        var info = s.UserId != 0 ? ctx.Db.GetMyInfo(s.UserId) : null;
        if (info is null)
        {
            await s.SendAsync(new Packet(Opcode.GL_MYINFO_ACK).WriteBool(false));
            return;
        }
        await s.SendAsync(BuildMyInfoAck(info, ctx.Db.GetCharacters(info.UserId)));
    }

    private static Packet BuildMyInfoAck(Db.MyInfo info, List<Db.CharSlot> chars)
    {
        var st = info.Stats;
        var ack = new Packet(Opcode.GL_MYINFO_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            // --- sub_523BF0 基本資料 ---
            .WriteStr(info.Nickname)
            .WriteU8(info.CurrentChar)                             // char_type (+88)
            .WriteS32(info.Level).WriteS32((int)info.Exp).WriteS32(0)
            .WriteS32((int)st.Wins).WriteS32((int)st.Losses)
            .WriteS32((int)st.Kills).WriteS32((int)st.Deaths)
            .WriteS32((int)st.Disconnects)
            .WriteS32((int)st.Headshots).WriteS32((int)st.Combos)
            .WriteS32((int)st.Hearts).WriteS32((int)st.DoubleKill)
            .WriteS32((int)st.TripleKill).WriteS32((int)st.MultiKill)
            .WriteS32((int)st.UltraKill).WriteS32((int)st.ZKill)
            .WriteS32((int)st.KKill).WriteS32((int)st.DdKill)
            .WriteS32((int)st.Criticals)
            .WriteS32((int)st.PlayCount).WriteS32((int)st.RoundCount)
            .WriteU8(0).WriteU8(0).WriteU8(0)                      // flags (+304..306)
            .WriteS32(info.Cash)                                   // (+104)
            .WriteS32(0).WriteS32(0)                               // (+112,116)
            .WriteRaw(stackalloc byte[48])                         // extra blob (+208)
            .WriteU8(info.CurrentChar);                            // slot_current (+4)

        // --- sub_524010 角色槽 (≤20, 每個 1 type + 12 裝備 u16) ---
        ack.WriteU8((byte)Math.Min(chars.Count, 20));
        foreach (var c in chars.Take(20))
        {
            ack.WriteU8(c.CharType);
            foreach (var eq in c.Equip) ack.WriteU16(eq);
        }

        // --- sub_524660 武器編組 (4 組, equipped=0 → 不帶 8×parts) ---
        ack.WriteU8(4);
        for (byte g = 0; g < 4; g++)
        {
            ack.WriteU8(g).WriteU16(0);
            if (g != 3) ack.WriteU16(0).WriteU16(0).WriteU16(0);
        }

        // --- sub_527550/527D00 技能欄 + 快速槽 (各 7×s32) ---
        for (int block = 0; block < 2; block++)
        {
            ack.WriteU8(7);
            for (int i = 0; i < 7; i++) ack.WriteS32(0);
        }

        return ack
            .WriteU16(0)                                           // clan/channel id
            .WriteS32((int)info.Gp)                                // game_point
            .WriteU8(0);                                           // tutorial_count
    }

    // ACK(200) sub_570AB0 → sub_524B70(cd, pkt, extra=1):
    //   bool ok; ok 時: s32 start, repeat{s32 slot(<0 結束), s32 item, f32, f32,
    //   s32 period, u8 extra(僅 200 帶; 202 走 sub_523A50 → extra=0), u16 dura}
    //   ⚠ 交叉驗證修正: 200 有 u8 extra, 202 反而沒有 (先前記反了)
    private static async ValueTask MyItems(Session s, Packet p, ServerContext ctx)
    {
        int start = p.Remaining >= 4 ? p.ReadS32() : 0;
        var ack = new Packet(Opcode.GL_MYITEM_ACK).WriteBool(true).WriteS32(start);

        if (s.UserId != 0)
        {
            foreach (var it in ctx.Db.GetInventoryPage(s.UserId, start))
                ack.WriteS32(it.Slot).WriteS32(it.ItemId)
                   .WriteF32(it.F1).WriteF32(it.F2)
                   .WriteS32(it.PeriodDaysLeft)
                   .WriteU8(0)                                     // extra (sub_524B70 a3=1)
                   .WriteU16(it.DuraCur);
        }
        await s.SendAsync(ack.WriteS32(-1));                       // sentinel
    }

    // 210 REQ builder @0x572D30: 只有 str nick (u8+str 是 216/262 的格式)
    // → 211 ACK sub_572D80: u8 result
    private static async ValueTask CheckNick(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();
        byte result = IsValidNick(nick) ? ctx.Db.CheckNick(nick) : (byte)2;
        await s.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    // 212 REQ builder sub_572DC0: 只有 str nick → 213 ACK sub_572E70: u8 result
    private static async ValueTask CreateNick(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();
        byte result = 1;
        if (s.Authenticated && IsValidNick(nick))
        {
            long uid = ctx.Db.CreateNick(s.AccountId, nick);
            if (uid != 0)
                (s.UserId, s.Nickname, result) = (uid, nick, (byte)0);
        }
        await s.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    private static bool IsValidNick(string nick) => nick.Length is >= 2 and <= 16;
}
