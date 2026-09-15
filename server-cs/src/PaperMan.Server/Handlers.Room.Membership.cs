// =============================================================================
// 房間 membership handlers — 建房、進房、成員 snapshot、離房、強制離房
//
// membership mutation 與 112/114 writer 刻意同檔，讓 state 和 client-visible snapshot 可一起 review。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 111 → 112 (sub_56A7B0 卌二輪補完 — 前 6 欄恆送, err==0 另加 9 欄):
    //   u8 err(0=OK), u8 room_no(<210), u16 slot_mask, s32 room_uid,
    //   u8 +185 no_skill_bg, u8 mode+13 隊打散, [成功:] u8 team_mode
    //   (2=隊伍房), 2×{u32 team_uid, u32 team_crc, str team_name,
    //   u8 team_flag}
    //   — 後 9 欄 client 在 err==0 時無條件讀取 (sub_592730 一路讀到
    //   NUL), 故 server 必送 (空隊伍 = uid/crc 0 + 空字串 + flag 0)。
    private static async ValueTask MakeRoom(Session session, Packet packet, ServerContext context)
    {
        byte mapId = packet.ReadU8();
        sbyte hasPass = packet.ReadS8();
        var title = packet.ReadStr();
        var pass = hasPass != 0 ? packet.ReadStr() : null;
        byte rule = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;
        byte max = packet.Remaining > 0 ? packet.ReadU8() : (byte)16;
        mapId = ResolveMap(mapId, rule, context.Db);        // 111 依 mode→bit 過濾可選地圖

        var room = session.UserId != 0
            ? context.Rooms.Create(session, mapId, title, pass, rule, max)
            : null;

        var err = room is null ? MakeRoomError.Full : MakeRoomError.Ok;
        var ack = new Packet(Opcode.GL_MAKEROOM_ACK)
            .WriteU8((byte)err)
            .WriteU8(room?.RoomNo ?? 0)                     // room_no (失敗時為 0)
            .WriteU16(room?.MaxSlotMask ?? 0)               // v52 → +110 上限槽位點陣
            .WriteS32(room?.RoomUid ?? 0)                   // v57 → dword_F2A65C
            .WriteBool(room?.NoSkillBg ?? false)            // v50 → +185 no_skill_bg
            .WriteBool(room?.TeamShuffle ?? false);         // v53 → mode+13 隊打散

        if (room is not null)
        {
            session.RoomNo = room.RoomNo;
            ack.WriteU8(IsTeamMode(rule) ? (byte)2 : (byte)0) // n2_1 team_mode (2=隊伍房 → CCustomTexture)
               .WriteU32(0)                                 // team A uid (新房間尚無分隊)
               .WriteU32(0)                                 // team A crc
               .WriteStr("")                                // team A name
               .WriteU8(0)                                  // team A flag
               .WriteU32(0)                                 // team B uid
               .WriteU32(0)                                 // team B crc
               .WriteStr("")                                // team B name
               .WriteU8(0);                                 // team B flag
        }

        await session.SendAsync(ack);
    }

    // 113 → 114 (sub_56B360 完整佈局, 卅七輪逐欄定案, 本輪補齊 sub_885D00 語音塊):
    //   u8 sub_type; 0=失敗回大廳; 1=單人進房通知 (給既有成員);
    //   2=完整房間狀態 (給進房者, 房物件欄位 + count×成員條目)。
    //   成員條目 = s32 uid, u8 slot, str nick, s32 exp(level 由 client 查表),
    //   u8 char_type, + 負載 (sub_524360 char, custom_tex/crc/tex,
    //   武器組×4, extra_flag, sub_527550 9 UI-item, sub_527D00 selected NewSkill puzzles,
    //   sub_885D00 語音自訂 85B 塊);
    //   sub_type==2 的條目另含 crown/status/observer 三枚 u8。
    private static async ValueTask EnterRoom(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte roomNo = packet.ReadU8();
        var room = context.Rooms.Find(roomNo);
        byte? slot = room?.TakeFreeSlot();

        if (room is null || slot is null || session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(0));
            return;
        }

        room.Members[slot.Value] = session;
        session.RoomNo = roomNo;

        // 1. 通知既有成員: sub_type==1 單人加入 (含完整負載)
        var newMember = LoadMemberData(context.Db, session);
        var joinNotice = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(1);
        WriteMemberNotice(joinNotice, session, slot.Value, newMember);
        await RoomManager.BroadcastAsync(room, joinNotice, except: session);

        // 2. 給進房者: sub_type==2 完整房間狀態 (房物件欄位 + 全員條目)
        var fullState = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(2);
        WriteRoomState(fullState, room);
        foreach (var (memberSlot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            WriteMemberEntry(fullState, member, memberSlot, memberSlot == room.MasterSlot,
                LoadMemberData(context.Db, member));
        }

        await session.SendAsync(fullState);
    }

    // ---- 114 序列化助手 (sub_56B360 佈局) --------------------------------

    /// <summary>成員的完整負載資料 (與 198 MyInfo 同源, 含語音自訂)。</summary>
    internal readonly record struct MemberData(
        Db.MyInfo? Info, Db.CharSlot? CurChar, List<Db.WeaponGroup> Groups, Db.Slots Slots, Db.Voice? Voice);

    internal static MemberData LoadMemberData(Db db, Session member)
    {
        var info = db.GetMyInfo(member.UserId);
        var chars = db.GetCharacters(member.UserId);
        var curChar = chars.FirstOrDefault(c => c.SlotNo == (info?.CurrentChar ?? 0));
        byte charIdx = (byte)(curChar is { CharType: >= 1 and <= Db.VoiceCharCount } ? curChar.CharType - 1 : 0);
        var voice = db.GetVoice(member.UserId, charIdx);
        return new(info, curChar, db.GetWeaponGroups(member.UserId), db.GetSlots(member.UserId), voice);
    }

    /// <summary>
    /// 寫入 85-byte wire 語音塊 (CGameInUserVoiceCustomize::sub_8765F0 讀序):
    /// s16 base_voice1, s16 base_voice2,
    /// 3 類 (command/tactics/infomation) × 9 句 × {s16 voice_item, u8 flag}。
    /// sub_885D00 (mode 2) 逐函數定案, 114/269/765/985 房間成員條目皆以此收尾。
    /// </summary>
    internal static void WriteVoiceBlock(Packet ack, Db.Voice? voice)
    {
        if (voice is not null)
        {
            ack.WriteS16(voice.BaseVoice1)
               .WriteS16(voice.BaseVoice2);
            for (int i = 0; i < Db.VoiceSlotCount; i++)
            {
                if (i < voice.Slots.Length)
                {
                    ack.WriteS16(voice.Slots[i].Item)
                       .WriteU8(voice.Slots[i].Flag);
                }
                else
                {
                    ack.WriteS16(0).WriteU8(0);
                }
            }
        }
        else
        {
            ack.WriteS16(0).WriteS16(0);
            for (int i = 0; i < Db.VoiceSlotCount; i++)
            {
                ack.WriteS16(0).WriteU8(0);
            }
        }
    }

    /// <summary>
    /// 成員負載尾部 (uid/slot/nick 前綴之外): sub_524360 單角色外觀
    /// (u8 角色槽 0..0x13, u8 char_type, 12×u16 equip) + 自訂貼圖
    /// (custom_tex/crc/tex, server 不追蹤 → 0/空) + 武器組×4 (固定四組,
    /// 組號即順位 — 異於 198 sub_524660 的 count+kind 版) +
    /// extra_flag(0 → 無 8×s32 尾塊) + sub_527550 技能 9×s32 +
    /// sub_527D00 raw n5 + 已選 NewSkill profile 7×s32 puzzle IDs + sub_885D00 語音自訂 85B 塊
    /// (s16 base1, s16 base2, 27×{s16 item, u8 flag})。首欄為「角色槽」(CurrentChar) 而非房槽。
    /// </summary>
    internal static void WriteMemberLoadout(
        Packet ack, Db.CharSlot? curChar, List<Db.WeaponGroup> groups, Db.Slots slots, Db.Voice? voice)
    {
        ack.WriteU8(curChar?.SlotNo ?? (byte)0)             // sub_524360 n0x14: 角色槽 0..0x13
           .WriteU8(curChar?.CharType ?? (byte)0);
        for (int i = 0; i < 12; i++)
        {
            ack.WriteU16(curChar?.Equip[i] ?? (ushort)0);
        }

        // custom_tex (member+24) / tex_crc (CCustomTexture) / tex name — 0/空 跳過
        ack.WriteS32(0).WriteS32(0).WriteStr("");

        for (byte g = 0; g < 4; g++)
        {
            var wg = groups.FirstOrDefault(x => x.GroupNo == g);
            ack.WriteU16(wg?.PrimaryOffset ?? (ushort)0);
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

        ack.WriteU8(0);                                     // extra_flag (byte_F33129)

        foreach (var skill in slots.Skill)                  // sub_527550: 9×s32
        {
            ack.WriteS32(skill);
        }

        ack.WriteU8(5);                                     // unresolved n5: retain existing raw convention
        foreach (var puzzleItemId in slots.NewSkillPuzzleIds) // sub_527D00: selected profile 7×s32
        {
            ack.WriteS32(puzzleItemId);
        }

        WriteVoiceBlock(ack, voice);                        // sub_885D00 語音塊 (85B, 逐函數定案)
    }

    /// <summary>sub_type==1 單人進房通知 (給既有成員)。</summary>
    internal static void WriteMemberNotice(Packet ack, Session member, byte slot, MemberData data)
    {
        ack.WriteS32((int)member.UserId)
           .WriteU8(slot)
           .WriteStr(member.Nickname)
           .WriteS32((int)(data.Info?.Exp ?? 0))            // v194 → member+25 exp
           .WriteU8(data.CurChar?.CharType ?? (byte)0);     // v179 → byte_F6DD61 (現役角色型別)
        WriteMemberLoadout(ack, data.CurChar, data.Groups, data.Slots, data.Voice);
    }

    /// <summary>sub_type==2 成員條目 (比 sub_type==1 多 crown/status/observer)。</summary>
    internal static void WriteMemberEntry(Packet ack, Session member, byte slot, bool isMaster, MemberData data)
    {
        ack.WriteS32((int)member.UserId)
           .WriteU8(slot)
           .WriteStr(member.Nickname)
           .WriteU8(isMaster ? (byte)1 : (byte)0)           // v184 → crown (sub_548AC0)
           .WriteU8(0)                                      // v190 → status flag (sub_548B00)
           .WriteS32((int)(data.Info?.Exp ?? 0))            // v143 exp
           .WriteU8(data.CurChar?.CharType ?? (byte)0)      // v179 char_type
           .WriteU8(0);                                     // v140 observer (0 = 完整資料)
        WriteMemberLoadout(ack, data.CurChar, data.Groups, data.Slots, data.Voice);
    }

    /// <summary>sub_type==2 房間狀態首段 (sub_56B360 case 2 的 19 欄
    /// — 134 的 16 欄 + room_uid 前綴 + +185/+128/mode+14 三尾欄)。</summary>
    private static void WriteRoomState(Packet ack, Room room)
    {
        ack.WriteS32(room.RoomUid)                         // v192 → dword_F2A65C
           .WriteU8(room.MapId)                            // v176 → +130 map (sub_540280)
           .WriteU8((byte)room.Members.Count)              // ii_1 成員數 (cur)
           .WriteU8(room.RoomNo)                           // v191[2] room_no (sub_537690 我的房號)
           .WriteU8(room.OpenSlotCount)                    // v170 → +129 最大人數 (client 以 +110 重算)
           .WriteU16(room.MaxSlotMask)                     // v181 → +110 上限槽位點陣 (popcount = 最大人數)
           .WriteU8(room.Rule)                             // thisa_1 → sub_53FBB0 遊戲模式 (0..15)
           .WriteU8(room.TimeLimit)                        // v167 → +136 時間
           .WriteU16(room.WinCount)                        // v178 → +144 勝場目標
           .WriteU8(room.ItemMode)                         // v187 flags (bit0→mode+4, bit1→mode+8)
           .WriteU8(0)                                     // v193 → +146 (server 側語意未定, client 僅鏡像)
           .WriteU16(room.KillCount)                       // v191[3] → +148 擊殺目標
           .WriteU8(0)                                     // v169 → +150 (server 側語意未定, client 僅鏡像)
           .WriteBool(IsTeamMode(room.Rule))               // v173 → mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
           .WriteU8(0)                                     // v185 → +109 room_type_B (client 僅鏡像)
           .WriteBool(room.TeamShuffle)                    // v141[0] → mode+13 隊打散開關 (368/369)
           .WriteBool(room.NoSkillBg)                      // v177 → +185 no_skill_bg
           .WriteBool(room.DoubleDamage)                   // v171 → +128 double_damage
           .WriteBool(room.Soccer);                        // v142 → mode+14 (sub_74F4D0; 969/970)
    }

    // 123 GR_LEAVE_REQ → 124 ACK (u8 result; ≠0 → u8 slot 離房廣播)。
    // 與斷線清理共用 RemoveMemberAsync — 房主離房時一併廣播 190 新房主
    // (否則新房主不會戴皇冠, UI 卡在無房主狀態)。
    private static async ValueTask LeaveRoom(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        await context.Rooms.RemoveMemberAsync(room, session);
        await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }

    // 131 GR_FORCEOUT_REQ (sub_56EC10: u8 target_slot)
    // → 132 GR_FORCEOUT_ACK (sub_56ECC0: u8 status==1, u8 target_slot)
    private static async ValueTask ForceOut(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo)
        {
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null || !room.IsMaster(session))
        {
            return;
        }

        byte targetSlot = packet.ReadU8();
        if (!room.Members.TryGetValue(targetSlot, out var targetSession) || targetSession is null)
        {
            return;
        }

        // 廣播踢人 ACK
        var ack = new Packet(Opcode.GR_FORCEOUT_ACK)
            .WriteU8(1)                                     // status 1 = 成功踢出
            .WriteU8(targetSlot);
        await RoomManager.BroadcastAsync(room, ack);

        // 移除成員
        await context.Rooms.RemoveMemberAsync(room, targetSession);
    }
}
