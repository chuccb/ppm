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
        add(Opcode.GL_LOBBYIN_REQ, SceneEnter);
        add(Opcode.GL_SHOPIN_REQ, SceneEnter);
        add(Opcode.GL_INVENIN_REQ, InventoryEnter);
        add(Opcode.GL_CLIENTINFO_REQ, ClientInfo);
        add(Opcode.GL_MYINFO_OPEN, MyInfoOpen);
        add(Opcode.GL_SHOUTCHAT_REQ, Shout);

        // 教學 / 等級限制 / Token / 通訊完成 / 換頻道
        add(Opcode.GL_TUTORIALINDEX_REQ, TutorialIndex);
        add(Opcode.GL_TUTORIAL_INDEX_SET_REQ, TutorialIndexSet);
        add(Opcode.GL_LEVEL_KILL_LIMIT_REQ, LevelKillLimit);
        add(Opcode.GL_BILLTOKEN_REQ, BillToken);
        add(Opcode.GL_RACKINGWEB_TOKEN_REQ, RankingWebToken);
        add(Opcode.GL_DATA_RECV_COMPLETED_REQ, DataRecvCompleted);
        add(Opcode.GL_CHANGECHANNEL_REQ, ChangeChannel);

        // 角色建立 / 角色槽 / 裝備更換 / 武器 / 技能 / 零件
        add(Opcode.GM_CREATECHAR_REQ, CreateChar);
        add(Opcode.GI_CHANGEDATA_REQ, ChangeData);
        add(Opcode.GI_CHANGEWP_REQ, ChangeWeapon);
        add(Opcode.GI_CHANGESLOT_REQ, ChangeSlot);
        add(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ, ChangeSkillSlot);
        add(Opcode.GL_WEAPONPARTS_EQUIP_CHANGE_REQ, ChangeWeaponParts);
    }

    // 270 GL_MYINFO_OPEN (sub_556680): s8 — 個資公開開關 (單向通知,
    // 無 ACK; 卅六輪) — 記錄即可
    private static ValueTask MyInfoOpen(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadS8() : (sbyte)0;
        return ValueTask.CompletedTask;
    }

    // 836 GL_SHOUTCHAT_REQ (sub_583370): s32 uid(自己 dword_EE8CB4),
    //   s32 strlen, str text — client 前置: 3s 牆鐘限流 (dword_1D0D24C)
    //   + CHAT_SHOUT 動作表 entry[4]!=0 (可喊)。uid/strlen 以 server 為準
    //   (防冒名), 僅 text 有意義。
    // → 837 ACK (sub_583C20): u8 flag(0/1 皆顯示), s32 uid, s32 timer,
    //   str nick, s32 raw_len, raw[raw_len] — uid==自己 → client 把
    //   CHAT_SHOUT 動作表 cooldown 設為 timer (0=不可再喊, 非0=可再喊)。
    //   timer 精確單位原服未明 (與 391 同款 server 動作值); 送 1 保持可用,
    //   由 client 3s 牆鐘限流防洗頻。GL = 大廳全域, 廣播全服。
    private static async ValueTask Shout(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0 || session.Nickname.Length == 0)
        {
            return;
        }

        _ = packet.ReadS32();                                // 自己 uid (以 session 為準)
        _ = packet.ReadS32();                                // strlen (client 附, 不重算)
        var text = packet.ReadStr();
        if (text.Length == 0)
        {
            return;
        }

        var shout = new Packet(Opcode.GL_SHOUTCHAT_ACK)
            .WriteU8(0)                                      // flag: 0/1 皆顯示 (sub_583C20)
            .WriteS32((int)session.UserId)
            .WriteS32(ShoutCooldown)                         // CHAT_SHOUT 動作值 (非0=可用)
            .WriteStr(session.Nickname)
            .WriteS32(Packet.Ansi.GetByteCount(text))
            .WriteRaw(Packet.Ansi.GetBytes(text));

        foreach (var target in context.Sessions.All)
        {
            if (!target.Authenticated)
            {
                continue;
            }

            try
            {
                await target.SendAsync(Packet.FromPayload(shout.Opcode, shout.Payload));
            }
            catch
            {
                // 個別連線斷線不影響其他人
            }
        }
    }

    /// <summary>837 的 timer — CHAT_SHOUT 動作表 cooldown (非0=可再喊)。</summary>
    private const int ShoutCooldown = 1;

    // 250 GL_LOBBYIN / 252 GL_SHOPIN — client state transition notices.
    // Their nominal ACK opcodes have no direct dispatcher consumer.
    private static ValueTask SceneEnter(Session session, Packet packet, ServerContext context)
    {
        // client 狀態機自行推進 (sub_537710); server 只需記錄場景
        return ValueTask.CompletedTask;
    }

    // 254 → 255 (sub_5741C0 / sub_574270). This is not an empty scene ACK:
    // the local-user branch consumes a selected index and five 32-byte
    // NewSkill profile records after the mode-1 header.
    private static async ValueTask InventoryEnter(Session session, Packet packet, ServerContext context)
    {
        byte requestContextRaw = packet.ReadU8();
        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("GL_INVENIN_REQ must contain exactly one context byte.");
        }

        if (session.UserId == 0)
        {
            throw new InvalidDataException("GL_INVENIN_REQ requires an authenticated player identity.");
        }

        Db.NewSkillProfileSnapshot snapshot = context.Db.GetNewSkillProfileSnapshot(session.UserId);
        NewSkillProfileRecord[] profiles = snapshot.Profiles
            .Select(profile => new NewSkillProfileRecord(profile.PuzzleItemIds, profile.ExpiresAtPackedMinute))
            .ToArray();
        Packet acknowledgement = NewSkillProfileWire.CreateInventoryEnterAcknowledgement(
            checked((int)session.UserId),
            requestContextRaw,
            snapshot.SelectedProfile,
            profiles);
        await session.SendAsync(acknowledgement);
    }

    // 246 GL_CLIENTINFO_REQ: str nick → 247 ACK (sub_573EB0):
    //   u8 ok(==1) + sub_523BF0 基本資料塊 + sub_524360 單角色外觀
    //   (十一輪: 與 198 首段同構 — 重用 BuildMyInfoAck 的統計佈局)
    private static async ValueTask ClientInfo(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);

        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_CLIENTINFO_ACK).WriteU8(0));
            return;
        }

        var chars = context.Db.GetCharacters(info.UserId);
        var ack = BuildClientInfoAck(info, chars);
        await session.SendAsync(ack);
    }

    /// <summary>247 = sub_523BF0 統計塊 + sub_524360 單角色外觀。</summary>
    private static Packet BuildClientInfoAck(Db.MyInfo info, List<Db.CharSlot> chars)
    {
        var st = info.Stats;
        // 247 uses the same sub_523BF0 block as 198: CClientData+88 is the
        // selected CHARSLOT list index, not a character type.
        byte selectedCharacterSlot = info.CurrentChar;
        var ack = new Packet(Opcode.GL_CLIENTINFO_ACK)
            .WriteU8(1)
            // sub_523BF0 — 與 198 首段完全同構 (佈局見 BuildMyInfoAck)
            .WriteStr(info.Nickname)
            .WriteU8(selectedCharacterSlot)
            .WriteS32(info.Level)
            .WriteS32((int)info.Exp)
            .WriteS32(0)
            .WriteS32((int)st.PlayCount)
            .WriteS32((int)st.RoundCount)
            .WriteS32((int)st.Criticals)
            .WriteS32((int)st.Wins)
            .WriteS32((int)st.Losses)
            .WriteS32((int)st.Kills)
            .WriteS32((int)st.Deaths)
            .WriteS32((int)st.Disconnects)
            .WriteS32((int)st.Hearts)
            .WriteS32((int)st.Headshots)
            .WriteS32((int)st.DoubleKill)
            .WriteS32((int)st.TripleKill)
            .WriteS32((int)st.Combos)
            .WriteS32((int)st.MultiKill)
            .WriteS32((int)st.UltraKill)
            .WriteS32((int)st.ZKill)
            .WriteS32((int)st.KKill)
            .WriteS32((int)st.DdKill)
            .WriteU8(0).WriteU8(0).WriteU8(0)
            .WriteS32(info.Cash)
            .WriteS32(0).WriteS32(0)
            .WriteRaw(new byte[48])                         // [28],[29] 後的 48B 保留區 (零)
            .WriteU8(info.CurrentChar);

        // sub_524360: u8 slot + u8 char_type + 12×u16 外觀
        var slot = chars.FirstOrDefault(c => c.SlotNo == info.CurrentChar) ?? chars.FirstOrDefault();
        ack.WriteU8(slot?.SlotNo ?? (byte)0)
           .WriteU8(slot?.CharType ?? (byte)1);
        for (int i = 0; i < 12; i++)
        {
            ack.WriteU16(slot?.Equip.ElementAtOrDefault(i) ?? (ushort)0);
        }

        return ack;
    }

    // REQ(119) builder @0x56E2xx: str message (ANSI)
    // ACK(120) sub_56E300: s32 custom_tex, str nick, wstr message
    //   ⚠ 訊息回送用「寬字串」(sub_5927B0 讀 UTF-16LE) — 與 REQ 的 ANSI 不對稱!
    //   client 端還會拿 nick 過 sub_539320 黑名單 (忽略清單) 過濾
    private static async ValueTask Chat(Session session, Packet packet, ServerContext context)
    {
        // 兩變體 (廿四輪): 完整版 s32 tex + str nick + wstr msg (與 ACK 同構);
        // 簡版只有 str。以剩餘長度判別。
        int tex = 0;
        string nick = session.Nickname;
        string message;

        if (packet.Remaining > 8)
        {
            tex = packet.ReadS32();
            nick = packet.ReadStr();
            message = packet.ReadWStr();
        }
        else
        {
            message = packet.ReadStr();
        }

        if (session.UserId == 0 || message.Length == 0)
        {
            return;
        }

        // 單人大廳: 回聲給自己 (多人時原樣廣播 — client 已附 nick+tex)
        await session.SendAsync(new Packet(Opcode.GL_CHATTING_ACK)
            .WriteS32(tex)
            .WriteStr(nick)
            .WriteWStr(message));
    }

    // ACK(106) sub_56A250: u16 count; 若 count!=0 才有 u8 flags, u8 n,
    // repeat n{s32 uid, str nick, s32 exp; uid>0 時 +s32 custom_tex, str(64)}
    // ⚠ 第三個 s32 = exp (sub_588560 → sub_403360 exp→level 查表, 十二輪);
    // count==0 → 之後不再讀任何欄位 (交叉驗證確認)
    private static async ValueTask UserList(Session session, Packet packet, ServerContext context) =>
        await session.SendAsync(new Packet(Opcode.GL_USERLIST_ACK).WriteU16(0));

    // ACK(108) sub_568CE0 (卅七輪逐欄定案):
    //   u8 mode (3=錦標賽樹 sub_580A80); 其他: u8 count, repeat{
    //     u8 room_no(<210), s8 state (state>=0 → 標題查 client 字串表 state+309;
    //     state<0 → str title), 之後 12 欄:
    //     u8 cur_players(+105), bool has_pass(+106), u8 max_players(+129 冗餘,
    //     client 以 +110 popcount 重算), u16 max_slot_mask(+110),
    //     u8 game_mode(→sub_53FBB0), bool room_type_A(+108), bool mode+12
    //     (是否隊伍房 sub_438990?1:0), bool room_type_B(+109),
    //     bool double_damage(+128), u8 map(+130), u8 mode_param_b(+4 道具),
    //     bool no_skill_bg(+185) }
    private static async ValueTask RoomList(Session session, Packet packet, ServerContext context)
    {
        var rooms = context.Rooms.All.Take(50).ToList();

        var ack = new Packet(Opcode.GL_GAMEROOMINFO_ACK)
            .WriteU8(0)                                     // mode 0 = 一般清單
            .WriteU8((byte)rooms.Count);

        foreach (var room in rooms)
        {
            ack.WriteU8(room.RoomNo)
               .WriteS8(-1)                                 // state<0 → 自訂標題 (str 版條目)
               .WriteStr(room.Title)
               .WriteU8((byte)room.Members.Count)           // +105 cur_players
               .WriteBool(room.Password is not null)        // +106 has_pass
               .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
               .WriteU16(room.MaxSlotMask)                  // +110 上限槽位點陣 (popcount = 最大人數)
               .WriteU8(room.Rule)                          // game_mode → sub_53FBB0 (0..15)
               .WriteBool(false)                            // +108 room_type bit A (server 側未定)
               .WriteBool(IsTeamMode(room.Rule))            // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
               .WriteBool(false)                            // +109 room_type bit B (server 側未定)
               .WriteBool(room.DoubleDamage)                // +128 double_damage (990/991)
               .WriteU8(room.MapId)                         // +130 map (sub_540280/540260; 122 亦寫此欄)
               .WriteU8((byte)(room.ItemMode & 1))          // mode+4 = item bit0 (sub_74F450; 175/176)
               .WriteBool(room.NoSkillBg);                  // +185 no_skill_bg (712/713)
        }

        await session.SendAsync(ack);
    }

    /// <summary>sub_438990 的 server 側對照: 兩隊制模式 (0/2/3/4/8/10/11/12/13)。</summary>
    private static bool IsTeamMode(byte mode) => mode is 0 or 2 or 3 or 4 or 8 or 10 or 11 or 12 or 13;

    // ACK(198): 完整 CClientData 序列化 (docs/PACKETS.md §3.2)
    private static async ValueTask MyInfo(Session session, Packet packet, ServerContext context)
    {
        var info = session.UserId != 0 ? context.Db.GetMyInfo(session.UserId) : null;
        if (info is null)
        {
            Console.WriteLine($"[s{session.Id}] GL_MYINFO_REQ: user not found (UserId={session.UserId}), sending false");
            await session.SendAsync(new Packet(Opcode.GL_MYINFO_ACK).WriteBool(false));
            return;
        }

        Console.WriteLine($"[s{session.Id}] GL_MYINFO_REQ: generating CClientData for '{info.Nickname}' (UserId={info.UserId}, Level={info.Level}, Cash={info.Cash}, GP={info.Gp})");
        await session.SendAsync(BuildMyInfoAck(
            info,
            context.Db.GetCharacters(info.UserId),
            context.Db.GetWeaponGroups(info.UserId),
            context.Db.GetSlots(info.UserId),
            context.Db.GetGiftCount(info.UserId)));
    }

    internal static Packet BuildMyInfoAck(
        Db.MyInfo info, List<Db.CharSlot> chars, List<Db.WeaponGroup> weaponGroups, Db.Slots slots,
        ushort giftCount)
    {
        var st = info.Stats;
        // sub_526CA0/sub_884160 use CClientData+88 as the CHARSLOT list index
        // and sub_884160 sends that same u8 in 312. It is not char_type.
        byte selectedCharacterSlot = info.CurrentChar;

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
            .WriteU8(selectedCharacterSlot)                        // selected character-list slot (+88)
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
            .WriteU8(0).WriteU8(0).WriteU8(0)                      // flags (+76/+305/+306 dword, 閒置 0 安全)
            .WriteS32(info.Cash)                                   // [26] (+104)
            .WriteS32(0).WriteS32(0)                               // [28],[29] (+112,116)
            .WriteRaw(BuildPlayModeBlob(st))                       // [52..63] 模式別計數 blob
            .WriteU8(info.CurrentChar);                            // slot_current (+4)

        return FinishMyInfoAck(ack, chars, weaponGroups, slots, info, giftCount);
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

    private static Packet FinishMyInfoAck(
        Packet ack, List<Db.CharSlot> chars, List<Db.WeaponGroup> weaponGroups, Db.Slots slots,
        Db.MyInfo info, ushort giftCount)
    {
        // --- sub_524010 角色槽 (≤20, 每個 u8 type + 12×u16 裝備) ---
        if (chars.Count == 0)
        {
            // Defensive formatter fallback only. Successful Login persists the
            // same canonical starter; never use an all-zero body because the
            // native 198 availability gate emits resource 0xCC / code 63.
            Db.CanonicalStarterAppearance starter = Db.GetCanonicalStarterAppearance(1);
            ack.WriteU8(1)                                        // one character record
               .WriteU8(1)                                        // canonical type 1 (Hayate)
               .WriteU16(starter.BodyOffset)
               .WriteU16(starter.HeadOffset)
               .WriteU16(starter.FaceOffset)
               .WriteU16(starter.TopOffset)
               .WriteU16(starter.BottomOffset)
               .WriteU16(starter.ShoesOffset);
            for (int i = 6; i < 12; i++)
            {
                ack.WriteU16(0);
            }
        }
        else
        {
            ack.WriteU8((byte)Math.Min(chars.Count, 20));
            foreach (var c in chars.Take(20))
            {
                ack.WriteU8(c.CharType);
                foreach (var eq in c.Equip)
                {
                    ack.WriteU16(eq);
                }
            }
        }

        // --- sub_524660 武器編組: u8 count(4) + 每組
        //     u8 kind + u16 equipped + (kind!=3 → 3×u16 sub) + (equipped!=0 → 8×s32 parts)
        //     無記錄的組別以空組回退 (equipped=0 → 不帶 parts) ---
        ack.WriteU8(4);
        for (byte g = 0; g < 4; g++)
        {
            var wg = weaponGroups.FirstOrDefault(x => x.GroupNo == g);
            ack.WriteU8(g)
               .WriteU16(wg?.Equipped ?? (ushort)0);
            if (g != 3)
            {
                ack.WriteU16(wg?.Sub1 ?? (ushort)0)
                    .WriteU16(wg?.Sub2 ?? (ushort)0)
                    .WriteU16(wg?.Sub3 ?? (ushort)0);
            }

            if (wg is { Equipped: not 0 })
            {
                foreach (var part in wg.Parts)
                {
                    ack.WriteS32(part);
                }
            }
        }

        // --- sub_527550 (sub_522480): 9×s32 技能槽 (無前導 count; 每個非零
        //     id 都要過 sub_535020 目錄驗證, 否則 client 錯誤 10) ---
        foreach (var item in slots.Skill)
        {
            ack.WriteS32(item);
        }

        // --- sub_527D00: raw u8 n5 + selected NewSkill profile's 7×s32
        //     puzzle IDs (0x1C); n5's original semantic is unresolved. ---
        ack.WriteU8(5);                                            // existing native-compatible raw convention
        foreach (var item in slots.NewSkillPuzzleIds)
        {
            ack.WriteS32(item);
        }

        // --- sub_570550 尾段 (u16 → i_23 = 禮物盒 pending 數,
        //     s32 → sub_5392A0 GP,
        //     u8 count + count×u8 教學旗標 → sub_5A9B30, 最多 20) ---
        return ack
            .WriteU16(giftCount)                                   // i_23 (禮物盒數, F0C100)
            .WriteS32((int)info.Gp)                                // game_point
            .WriteU8(0);                                           // tutorial flag count
    }

    // ACK(200) sub_570AB0 → sub_524B70(cd, pkt, extra=1):
    //   bool ok; ok 時: s32 start, repeat{s32 slot(<0 結束), s32 item, f32, f32,
    //   s32 period, u8 extra(200 專屬), u16 dura}
    //   (a3=0 的無-extra 版本屬 290/294 MASTER_USERINFO 系 sub_523A50 —
    //    GM 查他人資料, 與一般玩家路徑無關; 五輪驗證定案)
    private static async ValueTask MyItems(Session session, Packet packet, ServerContext context)
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

    // 210 REQ builder @0x572D30: 只有 str nick (u8+str 是 216/262 的格式)
    // → 211 ACK sub_572D80 → sub_41BBB0 (十輪逐分支讀出):
    //   1 = 可用 (訊息 0xE0), 2 = 已被使用 (格式訊息 0xDF 帶名字),
    //   0 = 一般錯誤 (彈窗 0x70/17) — 三種都停在暱稱畫面 (state:=2)
    private static async ValueTask CheckNick(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        byte result = (IsValidNick(nick), context.Db.IsNickTaken(nick)) switch
        {
            (false, _) => 0,                                // 非法 → 一般錯誤
            (_, true) => 2,                                 // 重複 → 0xDF 訊息
            _ => 1,                                         // 可用 → 0xE0 訊息
        };

        Console.WriteLine($"[s{session.Id}] GM_CHECKNICK_REQ: nick='{nick}' -> result={result}");
        await session.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    // 212 REQ builder sub_572DC0: 只有 str nick
    // → 213 ACK sub_572E70 → sub_41BD40 (十輪重大更正):
    //   ⚠ 1 = 成功 (拷貝統計欄位, state:=5 進大廳), 0 = 失敗 (state:=4)
    //   — 舊實作成功回 0 會讓 client 卡在失敗畫面!
    private static async ValueTask CreateNick(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        byte result = 0;

        if (session.Authenticated && IsValidNick(nick))
        {
            long uid = context.Db.CreateNick(session.AccountId, nick);
            if (uid != 0)
            {
                (session.UserId, session.Nickname, result) = (uid, nick, (byte)1);
            }
        }

        Console.WriteLine($"[s{session.Id}] GM_CREATENICK_REQ: nick='{nick}', accountId={session.AccountId} -> result={result}, userId={session.UserId}");
        await session.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    private static bool IsValidNick(string nick) => nick.Length is >= 2 and <= 16;

    // 685 GL_TUTORIALINDEX_REQ (sub_55C6F0, 空) → 686 ACK (sub_55C790): s32 index
    private static async ValueTask TutorialIndex(Session session, Packet packet, ServerContext context)
    {
        int index = session.UserId != 0 ? context.Db.GetTutorialIndex(session.UserId) : 0;
        await session.SendAsync(new Packet(Opcode.GL_TUTORIALINDEX_ACK).WriteS32(index));
    }

    // 689 GL_TUTORIAL_INDEX_SET_REQ (sub_55C7D0: s32 index) → 690 ACK (sub_582530): s32 index
    private static async ValueTask TutorialIndexSet(Session session, Packet packet, ServerContext context)
    {
        int index = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        if (session.UserId != 0)
        {
            context.Db.SetTutorialIndex(session.UserId, index);
        }

        await session.SendAsync(new Packet(Opcode.GL_TUTORIAL_INDEX_SET_ACK).WriteS32(index));
    }

    // 704 GL_LEVEL_KILL_LIMIT_REQ (sub_582570, 空)
    // → 705 ACK (sub_55C9B0): s32 kill_limit, f32 exp_rate, s32 max_level_limit
    private static async ValueTask LevelKillLimit(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GL_LEVEL_KILL_LIMIT_ACK)
            .WriteS32(50)                                   // 殺敵上限 50
            .WriteF32(1.0f)                                 // 經驗倍率 1.0
            .WriteS32(30);                                  // 最大等級限制 30

        await session.SendAsync(ack);
    }

    // 706 GL_BILLTOKEN_REQ (sub_460480, 空) → 707 ACK (sub_46AD00 case 707): str token
    private static async ValueTask BillToken(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_BILLTOKEN_ACK).WriteStr("TOKEN_PAPERMAN_OK"));
    }

    // 787 GL_RACKINGWEB_TOKEN_REQ (sub_581E40, 空) → 788 ACK (sub_44BEA0): str token
    private static async ValueTask RankingWebToken(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_RACKINGWEB_TOKEN_ACK).WriteStr("RANKING_TOKEN_OK"));
    }

    // 834 GL_DATA_RECV_COMPLETED_REQ (sub_583120: s32 uid) → 835 ACK (sub_5831D0): 空包
    private static async ValueTask DataRecvCompleted(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_DATA_RECV_COMPLETED_ACK));
    }

    // 370 GL_CHANGECHANNEL_REQ (sub_570030: u8 channel_id)
    // → 371 ACK (sub_570100): u8 status(1=成功), u8 channel_id, str host_ip, s32 host_port, u8 extra
    private static async ValueTask ChangeChannel(Session session, Packet packet, ServerContext context)
    {
        byte ch = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var ack = new Packet(Opcode.GL_CHANGECHANNEL_ACK)
            .WriteU8(1)                                     // status 1 = 成功
            .WriteU8(ch)                                    // channel_id
            // This is sub_570100 → sub_596E60's *secondary* UDP address.
            // Do not substitute the successful-196 UdpHost/UdpPort here:
            // client evidence has not established the two fields' equivalence.
            .WriteStr(context.Config.PublicHost)
            .WriteS32(context.Config.ChannelPort + 1)       // independent s32; native consumer takes low u16
            .WriteU8(0);                                    // extra

        await session.SendAsync(ack);
    }

    // 214 GM_CREATECHAR_REQ (sub_532AA0: u8 char_type, s16 hair, s16 face, s16 coat)
    // → 215 ACK (sub_572F80 / sub_550170): u8 status(0=成功)
    private static async ValueTask CreateChar(Session session, Packet packet, ServerContext context)
    {
        byte charType = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;
        bool ok = session.UserId != 0 && context.Db.CreateChar(session.UserId, 0, charType);
        await session.SendAsync(new Packet(Opcode.GM_CREATECHAR_ACK).WriteU8(ok ? (byte)0 : (byte)1));
    }

    // 218 GI_CHANGEDATA_REQ (sub_523A00: u8 char_slot)
    // → 219 ACK (sub_573230): u8 status(1=成功)
    private static async ValueTask ChangeData(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGEDATA_ACK).WriteU8(1));
    }

    // 312 GI_CHANGESLOT_REQ (sub_523FB0: u8 slot_no)
    // → 313 ACK (sub_573320): u8 slot_no
    private static async ValueTask ChangeSlot(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGESLOT_ACK).WriteU8(slotNo));
    }

    // 220 GI_CHANGEWP_REQ (sub_573340 / sub_57C270: u8 count, repeat weapon_group)
    // → 221 ACK (sub_5735F0): u8 count(4), 4×weapon_group
    private static async ValueTask ChangeWeapon(Session session, Packet packet, ServerContext context)
    {
        var groups = session.UserId != 0 ? context.Db.GetWeaponGroups(session.UserId) : [];
        var ack = new Packet(Opcode.GI_CHANGEWP_ACK).WriteU8(4);

        for (byte g = 0; g < 4; g++)
        {
            var wg = groups.FirstOrDefault(x => x.GroupNo == g);
            ack.WriteU8(g)
               .WriteU16(wg?.Equipped ?? (ushort)0);
            if (g != 3)
            {
                ack.WriteU16(wg?.Sub1 ?? (ushort)0)
                   .WriteU16(wg?.Sub2 ?? (ushort)0)
                   .WriteU16(wg?.Sub3 ?? (ushort)0);
            }

            if (wg is { Equipped: not 0 })
            {
                foreach (var part in wg.Parts)
                {
                    ack.WriteS32(part);
                }
            }
        }

        await session.SendAsync(ack);
    }

    // 466 → 467 (sub_5738A0 / sub_573A70): target profile, a conditional
    // previous-profile seven-id save, then an authoritative raw32 profile
    // metadata record. This is unrelated to the 9×s32 sub_527550 item block.
    private static async ValueTask ChangeSkillSlot(Session session, Packet packet, ServerContext context)
    {
        NewSkillProfileChange request = NewSkillProfileWire.ReadChangeRequest(packet);
        if (session.UserId == 0)
        {
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ requires an authenticated player identity.");
        }

        Db.NewSkillProfile? selectedRecord = context.Db.ChangeNewSkillProfile(
            session.UserId,
            request.TargetProfile,
            request.HasPreviousProfileUpdate,
            request.PreviousProfile,
            request.PreviousProfilePuzzleItemIds);
        if (selectedRecord is null)
        {
            // The client parser reveals no server rejection-code mapping for
            // 467. Do not forge a nominal success or replace the server-owned
            // raw32 record with zeros; reject without state mutation instead.
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ failed NewSkill ownership, profile, or expiry validation.");
        }

        var profile = new NewSkillProfileRecord(
            selectedRecord.PuzzleItemIds,
            selectedRecord.ExpiresAtPackedMinute);
        // sub_573A70 unconditionally reads and discards these two raw header
        // bytes. The original success/error meanings are still unobserved;
        // retain the server's established zero convention, but never call it
        // a semantic success flag.
        Packet acknowledgement = NewSkillProfileWire.CreateChangeAcknowledgement(
            resultRaw: 0,
            unknownHeaderRaw: 0,
            profileIndex: request.TargetProfile,
            profile: profile);
        await session.SendAsync(acknowledgement);
    }

    // 912 GL_WEAPONPARTS_EQUIP_CHANGE_REQ (sub_9591F0): u8 op_type, s32 weapon_id, s32 part_id, [s32 old_part]
    // → 913 ACK (sub_95B180): u8 err(0=成功), u8 op_type, s32 weapon_id, s32 part_id, [s32 old_part]
    private static async ValueTask ChangeWeaponParts(Session session, Packet packet, ServerContext context)
    {
        byte opType = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;
        int weaponId = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        int partId = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        int oldPartId = (opType == 2 && packet.Remaining >= 4) ? packet.ReadS32() : 0;

        var ack = new Packet(Opcode.GL_WEAPONPARTS_EQUIP_CHANGE_ACK)
            .WriteU8(0)                                     // err 0 = 成功
            .WriteU8(opType)
            .WriteS32(weaponId)
            .WriteS32(partId);

        if (opType == 2)
        {
            ack.WriteS32(oldPartId);
        }

        await session.SendAsync(ack);
    }
}
