// =============================================================================
// Lobby snapshot responses. These handlers build client-consumed account, inventory,
// profile, and room views without hiding their protocol ordering in a generic serializer.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{

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
               .WriteU16(wg?.PrimaryOffset ?? (ushort)0);
            if (g != 3)
            {
                ack.WriteU16(wg?.SecondaryOffset ?? (ushort)0)
                    .WriteU16(wg?.MeleeOffset ?? (ushort)0)
                    .WriteU16(wg?.ThrowOffset ?? (ushort)0);
            }

            if (wg is { PrimaryOffset: not 0 })
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

}
