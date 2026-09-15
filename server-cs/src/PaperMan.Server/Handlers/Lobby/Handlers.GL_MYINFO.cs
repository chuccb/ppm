// =============================================================================
// GL_MYINFO_REQ (197) → GL_MYINFO_ACK (198)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // ACK(198): 完整 CClientData 序列化 (docs/PACKETS.md §3.2)
    private static async ValueTask GL_MYINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        var info = session.UserId != 0 ? context.Db.GetMyInfo(session.UserId) : null;
        if (info is null)
        {
            Console.WriteLine($"[s{session.Id}] GL_MYINFO_REQ: user not found (UserId={session.UserId}), sending false");
            await session.SendAsync(new Packet(Opcode.GL_MYINFO_ACK).WriteBool(false));
            return;
        }

        Console.WriteLine($"[s{session.Id}] GL_MYINFO_REQ: generating CClientData for '{info.Nickname}' (UserId={info.UserId}, Level={info.Level}, Cash={info.Cash}, GP={info.Gp})");
        await session.SendAsync(CreateGL_MYINFO_ACK(
            info,
            context.Db.GetCharacters(info.UserId),
            context.Db.GetWeaponGroups(info.UserId),
            context.Db.GetSlots(info.UserId),
            context.Db.GetGiftCount(info.UserId)));
    }

    internal static Packet CreateGL_MYINFO_ACK(
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
            .WriteRaw(CreateGL_MYINFO_ACK_PlayModeBlob(st))                       // [52..63] 模式別計數 blob
            .WriteU8(info.CurrentChar);                            // slot_current (+4)

        return AppendGL_MYINFO_ACK_Payload(ack, chars, weaponGroups, slots, info, giftCount);
    }

    /// <summary>
    /// [52..63] 48B blob — 十二輪破解 (sub_9252D0 cond20+模式條件):
    /// dword[52] = 累計遊玩秒數 (任務 cond20), [53..60] = 各遊戲模式
    /// 完成場次 (模式 id 經 sub_923BF0 對照), [61..63] 未引用。
    /// </summary>
    private static byte[] CreateGL_MYINFO_ACK_PlayModeBlob(Db.Stats st)
    {
        var blob = new byte[48];
        BitConverter.TryWriteBytes(blob, (int)st.PlayTimeS);    // [52] cond20
        return blob;
    }

    private static Packet AppendGL_MYINFO_ACK_Payload(
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

}
