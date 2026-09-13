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
        add(Opcode.GL_CHATTING_REQ, Chat);
    }

    // REQ(119) builder @0x56E2xx: str message (ANSI)
    // ACK(120) sub_56E300: s32 custom_tex, str nick, wstr message
    //   ⚠ 訊息回送用「寬字串」(sub_5927B0 讀 UTF-16LE) — 與 REQ 的 ANSI 不對稱!
    //   client 端還會拿 nick 過 sub_539320 黑名單 (忽略清單) 過濾
    private static async ValueTask Chat(Session s, Packet p, ServerContext ctx)
    {
        var message = p.ReadStr();
        if (s.UserId == 0 || message.Length == 0)
        {
            return;
        }

        // 單人大廳: 回聲給自己 (多人時應廣播給同頻道所有 session)
        await s.SendAsync(new Packet(Opcode.GL_CHATTING_ACK)
            .WriteS32(0)                                    // custom_tex crc
            .WriteStr(s.Nickname)
            .WriteWStr(message));
    }

    // ACK(106) sub_56A250: u16 count; 若 count!=0 才有 u8 flags, u8 n,
    // repeat n{s32 uid, str nick, s32 exp; uid>0 時 +s32 custom_tex, str(64)}
    // ⚠ 第三個 s32 = exp (sub_588560 → sub_403360 exp→level 查表, 十二輪);
    // count==0 → 之後不再讀任何欄位 (交叉驗證確認)
    private static async ValueTask UserList(Session s, Packet p, ServerContext ctx) =>
        await s.SendAsync(new Packet(Opcode.GL_USERLIST_ACK).WriteU16(0));

    // ACK(108) sub_568CE0 (五輪完整讀畢):
    //   u8 mode (3=錦標賽樹 sub_580A80); 其他: u8 count, repeat{
    //     u8 room_no(<210), s8 state;
    //     state>=0 → u8 map, bool, u8 rule, u16 win, u8 max, bool pass, u8[1]
    //     state<0  → str title + 同欄位;
    //     共同尾段 bool,bool,u8,u8,u8; mode==2 加 2×{s32,u32 crc,str,u8} }
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

        // 統計欄位佈局 — 十二輪以任務條件檢查器 sub_9252D0 逐欄破解:
        //   cond5→dword[37]=wins, cond6→[38]=losses, cond3→[39]=kills,
        //   cond4→[40]=deaths, cond7→[41]=disc, cond10→[42]=hearts,
        //   cond8→[43]=headshots, cond11→[45]=double, cond12→[46]=triple,
        //   cond9→[44]=combos, cond13..17→[47..51]=multi/ultra/z/k/dd
        //   (事件號經 GP ACK handler sub_556B30 等 → sub_92EF00(n,...) 對齊)
        // wire 讀序 (sub_523BF0):
        //   群組2 = [34],[35],[36],[37],[38]  (34..36 無讀取者 — 保留槽)
        //   群組3 = [39],[40],[41],[42]
        //   群組4 = [43],[45],[46],[44]  ⚠ 亂序: heads, double, triple, combos
        //   群組5 = [47],[48],[49],[50],[51]
        var ack = new Packet(Opcode.GL_MYINFO_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            // --- sub_523BF0 基本資料 ---
            .WriteStr(info.Nickname)
            .WriteU8(info.CurrentChar)                             // char_type (+88)
            .WriteS32(info.Level)                                  // [23]
            .WriteS32((int)info.Exp)                               // [24] (level 由 client 查表重算)
            .WriteS32(0)                                           // [27] 任務 cond1 計數
            // 群組2: [34..36] 保留, [37]=wins, [38]=losses
            .WriteS32((int)st.PlayCount)                           // [34] (未證, 放次要值)
            .WriteS32((int)st.RoundCount)                          // [35] (未證)
            .WriteS32((int)st.Criticals)                           // [36] (未證)
            .WriteS32((int)st.Wins)                                // [37] 任務 cond5
            .WriteS32((int)st.Losses)                              // [38] 任務 cond6
            // 群組3: [39..42]
            .WriteS32((int)st.Kills)                               // [39] cond3
            .WriteS32((int)st.Deaths)                              // [40] cond4
            .WriteS32((int)st.Disconnects)                         // [41] cond7
            .WriteS32((int)st.Hearts)                              // [42] cond10
            // 群組4 (wire 亂序 43,45,46,44):
            .WriteS32((int)st.Headshots)                           // [43] cond8
            .WriteS32((int)st.DoubleKill)                          // [45] cond11
            .WriteS32((int)st.TripleKill)                          // [46] cond12
            .WriteS32((int)st.Combos)                              // [44] cond9
            // 群組5: [47..51]
            .WriteS32((int)st.MultiKill)                           // [47] cond13
            .WriteS32((int)st.UltraKill)                           // [48] cond14
            .WriteS32((int)st.ZKill)                               // [49] cond15
            .WriteS32((int)st.KKill)                               // [50] cond16
            .WriteS32((int)st.DdKill)                              // [51] cond17
            .WriteU8(0).WriteU8(0).WriteU8(0)                      // flags (+304..306)
            .WriteS32(info.Cash)                                   // [26] (+104)
            .WriteS32(0).WriteS32(0)                               // [28],[29] (+112,116)
            .WriteRaw(BuildPlayModeBlob(st))                       // [52..63] 模式別計數 blob
            .WriteU8(info.CurrentChar);                            // slot_current (+4)

        return FinishMyInfoAck(ack, chars, info);
    }

    /// <summary>
    /// [52..63] 48B blob — 十二輪破解 (sub_9252D0 cond20+模式條件):
    /// dword[52] = 累計遊玩秒數 (任務 cond20), [53..60] = 各遊戲模式
    /// 完成場次 (模式 id 經 sub_923BF0 對照), [61..63] 未引用。
    /// </summary>
    private static byte[] BuildPlayModeBlob(Db.Stats st)
    {
        var blob = new byte[48];
        BitConverter.TryWriteBytes(blob, (int)st.PlayTimeS);    // [52] cond20
        return blob;
    }

    private static Packet FinishMyInfoAck(Packet ack, List<Db.CharSlot> chars, Db.MyInfo info)
    {
        // --- sub_524010 角色槽 (≤20, 每個 1 type + 12 裝備 u16) ---
        ack.WriteU8((byte)Math.Min(chars.Count, 20));
        foreach (var c in chars.Take(20))
        {
            ack.WriteU8(c.CharType);
            foreach (var eq in c.Equip)
            {
                ack.WriteU16(eq);
            }
        }

        // --- sub_524660 武器編組 (4 組, equipped=0 → 不帶 8×parts) ---
        ack.WriteU8(4);
        for (byte g = 0; g < 4; g++)
        {
            ack.WriteU8(g).WriteU16(0);
            if (g != 3)
            {
                ack.WriteU16(0).WriteU16(0).WriteU16(0);    // 非第 4 組 → 3 個 sub-slot
            }
        }

        // --- sub_527550 (sub_522480): 9×s32 稱號槽 (十九輪: 驗證段
        //     15,304,001..15,306,000 = 稱號段), 無前導 count!
        //     每個非零 id 都要過 sub_535020 目錄驗證, 否則 client 錯誤 10
        for (int i = 0; i < 9; i++)
        {
            ack.WriteS32(0);
        }

        // --- sub_527D00: u8 n5 + 7×s32 ヘアパズル槽 (十九輪: 驗證段
        //     11,010,001..11,070,000 = 髮型拼圖段), 失敗 → client 錯誤 9
        ack.WriteU8(5);                                            // n5 預設值 5
        for (int i = 0; i < 7; i++)
        {
            ack.WriteS32(0);
        }

        // --- sub_570550 尾段 (u16 → i_23, s32 → sub_5392A0 GP,
        //     u8 count + count×u8 教學旗標 → sub_5A9B30, 最多 20) ---
        return ack
            .WriteU16(0)                                           // i_23 (clan/channel)
            .WriteS32((int)info.Gp)                                // game_point
            .WriteU8(0);                                           // tutorial flag count
    }

    // ACK(200) sub_570AB0 → sub_524B70(cd, pkt, extra=1):
    //   bool ok; ok 時: s32 start, repeat{s32 slot(<0 結束), s32 item, f32, f32,
    //   s32 period, u8 extra(200 專屬), u16 dura}
    //   (a3=0 的無-extra 版本屬 290/294 MASTER_USERINFO 系 sub_523A50 —
    //    GM 查他人資料, 與一般玩家路徑無關; 五輪驗證定案)
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
    // → 211 ACK sub_572D80 → sub_41BBB0 (十輪逐分支讀出):
    //   1 = 可用 (訊息 0xE0), 2 = 已被使用 (格式訊息 0xDF 帶名字),
    //   0 = 一般錯誤 (彈窗 0x70/17) — 三種都停在暱稱畫面 (state:=2)
    private static async ValueTask CheckNick(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();

        byte result = (IsValidNick(nick), ctx.Db.IsNickTaken(nick)) switch
        {
            (false, _) => 0,                                // 非法 → 一般錯誤
            (_, true) => 2,                                 // 重複 → 0xDF 訊息
            _ => 1,                                         // 可用 → 0xE0 訊息
        };

        await s.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    // 212 REQ builder sub_572DC0: 只有 str nick
    // → 213 ACK sub_572E70 → sub_41BD40 (十輪重大更正):
    //   ⚠ 1 = 成功 (拷貝統計欄位, state:=5 進大廳), 0 = 失敗 (state:=4)
    //   — 舊實作成功回 0 會讓 client 卡在失敗畫面!
    private static async ValueTask CreateNick(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();
        byte result = 0;

        if (s.Authenticated && IsValidNick(nick))
        {
            long uid = ctx.Db.CreateNick(s.AccountId, nick);
            if (uid != 0)
            {
                (s.UserId, s.Nickname, result) = (uid, nick, (byte)1);
            }
        }

        await s.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    private static bool IsValidNick(string nick) => nick.Length is >= 2 and <= 16;
}
