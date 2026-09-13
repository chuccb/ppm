// =============================================================================
// 房間 handlers — 111/112 建房, 113/114 進房, 123/124 離房。
//
// 佈局出自反編譯 (docs/PACKETS.md §3.15 + 多輪逐欄定案):
//   111 REQ: u8 map, s8 has_pass, str title, [str pass],
//            u8 rule(modeIndex), u8 max, u8 x, u8 y
//   112 ACK (sub_56A7B0): u8 err, u8 room_no(<210), u16 max_slot_mask,
//            s32 room_uid, u8 no_skill_bg(+185), u8 mode+13 — err==0 →
//            client 自任房主 (sub_53F920: +105=1 自身), 狀態切 10
//   113 REQ: u8 room_no
//   114 ACK (sub_56B360): u8 sub_type; 1=單人進房通知(既有成員),
//            2=完整房間狀態(進房者), 0=失敗 — 成員條目含完整 CClientData
//   124 GR_LEAVE_ACK: u8 result; ≠0 → u8 slot (成員移除廣播)
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class RoomHandlers
{
    /// <summary>112 的 err 碼 (sub_56A7B0: err!=0 → 顯示失敗訊息)。</summary>
    private enum MakeRoomError : byte
    {
        Ok = 0,
        Full = 1,                                           // 210 房全滿
        BadParams = 2,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.GL_MAKEROOM_REQ, MakeRoom);
        add(Opcode.GL_ENTERROOM_REQ, EnterRoom);
        add(Opcode.GR_LEAVE_REQ, LeaveRoom);
        add(Opcode.GR_CHATTING_REQ, RoomChat);
        add(Opcode.GR_MAPCHANGE_REQ, MapChange);
        add(Opcode.GR_READY_REQ, Ready);
        add(Opcode.GR_CHANGESLOT_REQ, ChangeSlot);
        add(Opcode.GL_ENTERROOMPASS_REQ, EnterRoomPass);
        add(Opcode.GR_START_REQ, StartGame);
        add(Opcode.GR_ENDLOADING_REQ, EndLoading);
        add(Opcode.GG_STARTGAME_REQ, BeginBattle);
        add(Opcode.GR_END_REQ, EndGame);
    }

    // 129 REQ: u8 n125 → 130 ACK (sub_562870 讀序):
    //   u8 result(1=開戰), u8 隊旗(→mode+14), s32 elapsed_ms(新局=0),
    //   u8 room_no, u8 cur_players(+105), u8 max_players(+129 冗餘,
    //   client 以 +110 popcount 重算), u16 max_slot_mask(+110),
    //   u8 map(+130), u8 mode(→sub_53FBB0), u16 (+144), u8 flags(bit0→mode+4),
    //   u8 mode+12, u8 +109, u8 mode+13, u8 +185, u8 +128,
    //   16×s32 (per-slot → dword_F6DD1C)
    //   — room_no 為 client 以 sub_407E80 定址房物件的索引
    private static async ValueTask StartGame(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                     // n125 (倒數參數)

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var ack = new Packet(Opcode.GR_START_ACK)
            .WriteU8(1)                                     // result: 開戰
            .WriteS8(0)                                     // 隊旗 (未確認)
            .WriteS32(0)                                    // elapsed_ms (新局=0)
            .WriteU8(roomNo)                                // room_no (client 定址房物件)
            .WriteU8((byte)room.Members.Count)              // +105 cur_players
            .WriteU8(room.MaxPlayers)                       // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣
            .WriteU8(room.MapId)                            // +130 map (sub_540280)
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU16(0)                                    // +144 (未確認)
            .WriteU8(0)                                     // flags (bit0→mode+4)
            .WriteU8(0)                                     // mode+12
            .WriteU8(0)                                     // +109
            .WriteU8(0)                                     // mode+13
            .WriteU8(0)                                     // +185
            .WriteU8(0);                                    // +128 (未確認)
        for (int i = 0; i < 16; i++)
        {
            ack.WriteS32(0);                                // per-slot 值
        }

        room.ResetLoading();
        await RoomManager.BroadcastAsync(room, ack);
    }

    // 183 REQ 空 → 184 ACK (sub_563B00): u8 n2(1), u8 slot, u8 slot2, u8
    //   — 每位成員載入完成後廣播; 全員到齊由 client 觸發 187
    private static async ValueTask EndLoading(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        room.MarkLoaded(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ENDLOADING_ACK)
            .WriteU8(1)
            .WriteU8(slot)
            .WriteU8(slot)
            .WriteU8(0));
    }

    // 187 REQ 空 → 188 ACK (sub_563D60): u8 n2==1, u8 count, count×u8 slot
    //   — 開打廣播 (帶已載入成員名單)
    private static async ValueTask BeginBattle(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var loaded = room.LoadedSlots;
        var ack = new Packet(Opcode.GG_STARTGAME_ACK)
            .WriteU8(1)
            .WriteU8((byte)loaded.Count);
        foreach (var slot in loaded)
        {
            ack.WriteU8(slot);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

    // 133 REQ 空 → 134 ACK (sub_562EA0 讀序): 回房重置
    //   u8 result(1=回房), u8 map(+130), u8(讀後丟棄), u8 room_no,
    //   u8 max_players(+129 冗餘, client 以 +110 popcount 重算),
    //   u16 max_slot_mask(+110, 回房恢復大廳), u8 mode(→sub_53FBB0),
    //   u8(+136), u16(+144), u8 flags(bit0→mode+4), u8(+146),
    //   u16(+148), u8(+150), u8 mode+12, u8 +109, u8 mode+13 —
    //   比 114 case-2 少 room_uid 前綴與 +185/+128/mode+14
    private static async ValueTask EndGame(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var ack = new Packet(Opcode.GR_END_ACK)
            .WriteU8(1)                                     // result: 回房
            .WriteU8(room.MapId)                            // +130 map (sub_540280)
            .WriteU8(0)                                     // client 讀後丟棄 (i_1)
            .WriteU8(roomNo)                                // room_no (client 定址房物件)
            .WriteU8(room.MaxPlayers)                       // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣 (回房恢復)
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU8(0)                                     // +136 (未確認)
            .WriteU16(0)                                    // +144 (未確認)
            .WriteU8(0)                                     // flags (bit0→mode+4)
            .WriteU8(0)                                     // +146 (未確認)
            .WriteU16(0)                                    // +148 (未確認)
            .WriteU8(0)                                     // +150 (未確認)
            .WriteU8(0)                                     // mode+12
            .WriteU8(0)                                     // +109
            .WriteU8(0);                                    // mode+13
        await RoomManager.BroadcastAsync(room, ack);
    }

    // 127 REQ 空 → 128 ACK (sub_5626D0): u8 ready_flag, u8 slot —
    // ready 狀態翻轉廣播 (server 維護 per-slot ready 集合)
    private static async ValueTask Ready(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        bool nowReady = room.ToggleReady(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_READY_ACK)
            .WriteU8(nowReady ? (byte)1 : (byte)0)
            .WriteU8(slot));
    }

    // 135 REQ (sub_56EE90): u8 n254, u8 target_slot → 136 ACK (sub_56EF40):
    //   u8 mode; mode!=0 → 僅失敗提示音 (client 不再讀 body);
    //   mode==0 → u8 from_slot, u8 new_slot, s32(保留, 讀後丟棄),
    //             s32 mover_uid, u8 count, count×(u8 slot, s32 uid)
    //   全房依 uid 對位重建 slot 佈局 (更新 client dword_F6DCF4)。
    private static async ValueTask ChangeSlot(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                     // n254 (模式參數)
        byte target = packet.ReadU8();

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room
            || target >= 16 || room.Members.ContainsKey(target))
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        var pair = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session));
        if (pair.Value is null)
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        byte from = pair.Key;
        room.Members.TryRemove(from, out _);
        room.Members[target] = session;
        if (from == room.MasterSlot)
        {
            room.MasterSlot = target;
        }

        // mode==0: 全房重建佈局 (mover 本人以 mover_uid + new_slot 更新自己)
        var ack = new Packet(Opcode.GR_CHANGESLOT_ACK)
            .WriteU8(0)                                     // mode: 完整重建
            .WriteU8(from)                                  // 舊 slot (client 讀後丟棄)
            .WriteU8(target)                                // 新 slot (本人 slot 更新判準)
            .WriteS32(0)                                    // 保留 (client 讀後丟棄)
            .WriteS32((int)session.UserId)                  // mover_uid
            .WriteU8((byte)room.Members.Count);
        foreach (var (slot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            ack.WriteU8(slot)
               .WriteS32((int)member.UserId);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

    // 216 REQ: u8 room_no, str pass → 217 ACK: u8 result → 成功後 client 送 113
    private static async ValueTask EnterRoomPass(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        var pass = packet.ReadStr();
        var room = context.Rooms.Find(roomNo);

        bool ok = room is not null
            && (room.Password is null || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_ENTERROOMPASS_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0));
    }

    // 125 REQ (與 119 同構) → 126 ACK (sub_56EA80):
    //   s32 custom_tex, u8 slot, wstr message — 房內廣播
    private static async ValueTask RoomChat(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        // 廿八輪自動表: 125 = s32 tex, u8 slot, wstr msg (次變體 str)
        int tex = packet.ReadS32();
        _ = packet.ReadU8();                                     // client 附 slot (以 server 記錄為準)

        var message = packet.Remaining >= 2 && packet.Remaining % 2 == 0
            ? packet.ReadWStr()
            : packet.ReadStr();

        if (message.Length == 0)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        var notice = new Packet(Opcode.GR_CHATTING_ACK)
            .WriteS32(tex)
            .WriteU8(slot)
            .WriteWStr(message);
        await RoomManager.BroadcastAsync(room, notice);
    }

    // 121 REQ: u8 map → 122 ACK (sub_56E530): u8 map — 房主換圖廣播
    private static async ValueTask MapChange(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        byte mapId = packet.ReadU8();
        room.MapId = mapId;

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(mapId));
    }

    // 111 → 112 (+108 更新大廳清單由 client 重拉)
    private static async ValueTask MakeRoom(Session session, Packet packet, ServerContext context)
    {
        byte mapId = packet.ReadU8();
        sbyte hasPass = packet.ReadS8();
        var title = packet.ReadStr();
        var pass = hasPass != 0 ? packet.ReadStr() : null;
        byte rule = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;
        byte max = packet.Remaining > 0 ? packet.ReadU8() : (byte)16;

        var room = session.UserId != 0
            ? context.Rooms.Create(session, mapId, title, pass, rule, max)
            : null;

        var err = room is null ? MakeRoomError.Full : MakeRoomError.Ok;
        var ack = new Packet(Opcode.GL_MAKEROOM_ACK).WriteU8((byte)err);

        if (room is not null)
        {
            session.RoomNo = room.RoomNo;
            ack.WriteU8(room.RoomNo)
               .WriteU16(room.MaxSlotMask)                  // v52 → +110 上限槽位點陣 (popcount = 最大人數)
               .WriteS32(room.RoomUid)                      // v57 → dword_F2A65C
               .WriteU8(0)                                  // v50 → +185 no_skill_bg (server 不開)
               .WriteS8(0);                                 // v53 → mode+13
        }

        await session.SendAsync(ack);
    }

    // 113 → 114 (sub_56B360 完整佈局, 卅七輪逐欄定案):
    //   u8 sub_type; 0=失敗回大廳; 1=單人進房通知 (給既有成員);
    //   2=完整房間狀態 (給進房者, 房物件欄位 + count×成員條目)。
    //   成員條目 = s32 uid, u8 slot, str nick, s32 exp(level 由 client 查表),
    //   u8 char_type, + 負載 (sub_524360 char, custom_tex/crc/tex,
    //   武器組×4, extra_flag, sub_527550 技能, sub_527D00 快速槽);
    //   sub_type==2 的條目另含 crown/status/observer 三枚 u8。
    private static async ValueTask EnterRoom(Session session, Packet packet, ServerContext context)
    {
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

    /// <summary>成員的完整負載資料 (與 198 MyInfo 同源)。</summary>
    private readonly record struct MemberData(
        Db.MyInfo? Info, Db.CharSlot? CurChar, List<Db.WeaponGroup> Groups, Db.Slots Slots);

    private static MemberData LoadMemberData(Db db, Session member)
    {
        var info = db.GetMyInfo(member.UserId);
        var chars = db.GetCharacters(member.UserId);
        var curChar = chars.FirstOrDefault(c => c.SlotNo == (info?.CurrentChar ?? 0));
        return new(info, curChar, db.GetWeaponGroups(member.UserId), db.GetSlots(member.UserId));
    }

    /// <summary>
    /// 成員負載尾部 (uid/slot/nick 前綴之外): sub_524360 單角色外觀
    /// (u8 角色槽 0..0x13, u8 char_type, 12×u16 equip) + 自訂貼圖
    /// (custom_tex/crc/tex, server 不追蹤 → 0/空) + 武器組×4 (固定四組,
    /// 組號即順位 — 異於 198 sub_524660 的 count+kind 版) +
    /// extra_flag(0 → 無 8×s32 尾塊) + sub_527550 技能 9×s32 +
    /// sub_527D00 快速槽 u8+7×s32。首欄為「角色槽」(CurrentChar) 而非房槽。
    /// </summary>
    private static void WriteMemberLoadout(
        Packet ack, Db.CharSlot? curChar, List<Db.WeaponGroup> groups, Db.Slots slots)
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
            ack.WriteU16(wg?.Equipped ?? (ushort)0);
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

        ack.WriteU8(0);                                     // extra_flag (byte_F33129)

        foreach (var skill in slots.Skill)                  // sub_527550: 9×s32
        {
            ack.WriteS32(skill);
        }

        ack.WriteU8(5);                                     // n5 預設 5
        foreach (var quick in slots.Quick)                  // sub_527D00: 7×s32
        {
            ack.WriteS32(quick);
        }
    }

    /// <summary>sub_type==1 單人進房通知 (給既有成員)。</summary>
    private static void WriteMemberNotice(Packet ack, Session member, byte slot, MemberData data)
    {
        ack.WriteS32((int)member.UserId)
           .WriteU8(slot)
           .WriteStr(member.Nickname)
           .WriteS32((int)(data.Info?.Exp ?? 0))            // v194 → member+25 exp
           .WriteU8(data.CurChar?.CharType ?? (byte)0);     // v179 → byte_F6DD61 (現役角色型別)
        WriteMemberLoadout(ack, data.CurChar, data.Groups, data.Slots);
    }

    /// <summary>sub_type==2 成員條目 (比 sub_type==1 多 crown/status/observer)。</summary>
    private static void WriteMemberEntry(Packet ack, Session member, byte slot, bool isMaster, MemberData data)
    {
        ack.WriteS32((int)member.UserId)
           .WriteU8(slot)
           .WriteStr(member.Nickname)
           .WriteU8(isMaster ? (byte)1 : (byte)0)           // v184 → crown (sub_548AC0)
           .WriteU8(0)                                      // v190 → status flag (sub_548B00)
           .WriteS32((int)(data.Info?.Exp ?? 0))            // v143 exp
           .WriteU8(data.CurChar?.CharType ?? (byte)0)      // v179 char_type
           .WriteU8(0);                                     // v140 observer (0 = 完整資料)
        WriteMemberLoadout(ack, data.CurChar, data.Groups, data.Slots);
    }

    /// <summary>sub_type==2 房間狀態首段 (sub_56B360 case 2 的 19 欄
    /// — 134 的 16 欄 + room_uid 前綴 + +185/+128/mode+14 三尾欄)。</summary>
    private static void WriteRoomState(Packet ack, Room room)
    {
        ack.WriteS32(room.RoomUid)                         // v192 → dword_F2A65C
           .WriteU8(room.MapId)                            // v176 → +130 map (sub_540280)
           .WriteU8((byte)room.Members.Count)              // ii_1 成員數 (cur)
           .WriteU8(room.RoomNo)                           // v191[2] room_no (sub_537690 我的房號)
           .WriteU8(room.MaxPlayers)                       // v170 → +129 最大人數 (client 以 +110 重算)
           .WriteU16(room.MaxSlotMask)                     // v181 → +110 上限槽位點陣 (popcount = 最大人數)
           .WriteU8(room.Rule)                             // thisa_1 → sub_53FBB0 遊戲模式 (0..15)
           .WriteU8(0)                                     // v167 → +136
           .WriteU16(0)                                    // v178 → +144
           .WriteU8(0)                                     // v187 flags (bit0→mode+4, bit1)
           .WriteU8(0)                                     // v193 → +146
           .WriteU16(0)                                    // v191[3] → +148
           .WriteU8(0)                                     // v169 → +150
           .WriteU8(0)                                     // v173 → mode+12
           .WriteU8(0)                                     // v185 → +109
           .WriteU8(0)                                     // v141[0] → mode+13
           .WriteU8(0)                                     // v177 → +185 no_skill_bg
           .WriteU8(0)                                     // v171 → +128
           .WriteU8(0);                                    // v142 → mode+14 (sub_74F4D0)
    }

    // 123 GR_LEAVE_REQ → 124 ACK (u8 result + u8 slot 廣播)
    private static async ValueTask LeaveRoom(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        room.Members.TryRemove(slot, out _);
        session.RoomNo = null;

        if (room.Members.IsEmpty)
        {
            context.Rooms.Remove(roomNo);                       // 空房回收
        }
        else
        {
            var notice = new Packet(Opcode.GR_LEAVE_ACK)
                .WriteU8(1)
                .WriteU8(slot);
            await RoomManager.BroadcastAsync(room, notice);
        }

        await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }
}
