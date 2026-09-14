// =============================================================================
// 房間 handlers — 111/112 建房, 113/114 進房, 123/124 離房, 房設定簇。
//
// 佈局出自反編譯 (docs/PACKETS.md §3.15 + §3.15b2, 多輪逐欄定案):
//   111 REQ: u8 map, s8 has_pass, str title, [str pass],
//            u8 rule(modeIndex), u8 max, u8 x, u8 y
//   112 ACK (sub_56A7B0): u8 err, u8 room_no(<210), u16 max_slot_mask,
//            s32 room_uid, u8 no_skill_bg(+185), u8 mode+13 — err==0 →
//            client 自任房主 (sub_53F920: +105=1 自身), 狀態切 10
//   113 REQ: u8 room_no
//   114 ACK (sub_56B360): u8 sub_type; 1=單人進房通知(既有成員),
//            2=完整房間狀態(進房者), 0=失敗 — 成員條目含完整 CClientData
//   124 GR_LEAVE_ACK: u8 result; ≠0 → u8 slot (成員移除廣播)
//
//   房設定簇 (REQ 限房主, ACK 同值廣播): 139/140 退場, 167/168 槽位點陣,
//   169/170 模式, 171/172 勝場, 173/174 時間, 175/176 道具, 177 死碼吸收,
//   340/341 擊殺, 364/365 平衡, 712/713 無技背景, 728/729 觀戰聊天,
//   990/991 雙倍傷害, 366/367 區域房, 368/369 隊打散開關, 969/970 足球,
//   894/895 隊打散執行 — 對應 room 欄位見 Rooms.cs 與 §3.15b2 總圖。
// =============================================================================
using System.Collections.Frozen;
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

    /// <summary>
    /// 各模式的預設地圖 — client 載入 `system/map_StartIndex.xml` (⚠ 非 ui/
    /// 根目錄那份舊版, 兩者 modeStartIndex 不同!) 填 room 的 mode→map 表,
    /// 169/170 改模式時 sub_426930(mode) 回推 modeStartIndex 寫 +130
    /// (sub_540280)。modeStartIndex 即 maplist (123 圖) 的絕對 map_id。
    /// server 鏡像以免 114/130/134 送舊圖。
    /// 其餘 mode (5=練習 6=教學 7=聊天 10/11/13 佔領/AI 15=武器試射
    /// 16=空) 無此表條目 → 保留原圖。
    /// </summary>
    private static readonly FrozenDictionary<byte, byte> ModeDefaultMap =
        new Dictionary<byte, byte>
        {
            [0] = 106,                                      // TeamDeath (TD_)
            [1] = 104,                                      // FreeForAll (PS_)
            [2] = 14,                                       // TeamHacking/駭入 (TH_)
            [3] = 107,                                      // TeamSurvival (TS_)
            [4] = 23,                                       // TeamSteal (TW_)
            [8] = 51,                                       // Pulp'n Roll (PNR)
            [9] = 89,                                       // GunShooting
            // ⚠ [12]=98 為 system/map_StartIndex.xml 的權威值 (SOCCER→98),
            // 但 maplist.pat 的 soccer bit (0x4000) 實際落在 99/100
            // (スルルスタジアム); 98 是 TeamSurvival 圖 (TS_33_tutor_castle,
            // modes=0x0002)。client sub_426930 在改模式時**就是寫 98** —
            // 此處鏡像 client 原行為, 不做「校正」(校正反而與 client 不一致)。
            [12] = 98,                                      // SOCCER (client 原值)
        }.ToFrozenDictionary();

    /// <summary>
    /// mode → maplist.pat `modes` bitmask 位 (docs/RESOURCES.md §4b,
    /// sub_53FBB0 模式枚舉 + map_StartIndex modeName + 檔名前綴三方互證)。
    /// mode 與 bit **不同編號** — 如 mode 0 (TeamDeath) ↔ bit 2。
    /// 無條目的 mode (5 練習 / 7 聊天 / 14 未用) 不參與地圖過濾。
    /// </summary>
    private static readonly FrozenDictionary<byte, byte> ModeMapBit =
        new Dictionary<byte, byte>
        {
            [0] = 2,                                        // TeamDeath     → TD  (bit 2)
            [1] = 0,                                        // FreeForAll    → PS  (bit 0)
            [2] = 3,                                        // TeamHacking   → TH  (bit 3)
            [3] = 1,                                        // TeamSurvival  → TS  (bit 1)
            [4] = 4,                                        // TeamSteal     → TW  (bit 4)
            [6] = 6,                                        // Tutorial      → TU  (bit 6=0x40; bit 5=0x20 為純 TU 專用圖)
            [8] = 9,                                        // Pulp'n Roll   → PNR (bit 9)
            [9] = 10,                                       // GunShooting   → AI  (bit 10)
            [10] = 12,                                      // Occupy        → OCC (bit 12)
            [11] = 13,                                      // AI Multi      → PVE (bit 13)
            [12] = 14,                                      // Soccer        → TS  worldcup (bit 14)
            [13] = 15,                                      // OccupyRenewal → OCC2 (bit 15)
            [15] = 11,                                      // WeaponTest    → ECT (bit 11)
        }.ToFrozenDictionary();

    /// <summary>
    /// 依 mode 過濾/校正地圖: 若 mode 有對應 bit 且 request 地圖不支援該 mode
    /// (map_catalog.modes 未含該 bit), 回退該 mode 預設圖; 目錄查無此地圖
    /// (無法求證) 或 mode 無過濾規則時原樣放行 — 不硬編造欄位。
    /// 回退值也再驗一次: 預設圖本身不支援該 mode 時 (如 SOCCER 預設 98 無
    /// soccer bit) 改取目錄第一張支援該 mode 的圖, 保證結果必合法。
    /// </summary>
    private static byte ResolveMap(byte requested, byte mode, Db db)
    {
        if (!ModeMapBit.TryGetValue(mode, out byte bit))
        {
            return requested;                               // 練習/聊天等 mode 不篩圖
        }

        var catalog = db.GetMapModes();
        if (!catalog.TryGetValue(requested, out int modes))
        {
            return requested;                               // 目錄無此圖 → 無法驗證, 放行
        }

        int mask = 1 << bit;
        if ((modes & mask) != 0)
        {
            return requested;                               // 該 mode 可用
        }

        if (ModeDefaultMap.TryGetValue(mode, out byte fallback)
            && catalog.TryGetValue(fallback, out int fallbackModes)
            && (fallbackModes & mask) != 0)
        {
            return fallback;                                // mode 預設圖可用
        }

        foreach (var (mapId, mapModes) in catalog)          // 預設圖不可用 → 第一張合法圖
        {
            if ((mapModes & mask) != 0)
            {
                return mapId;
            }
        }

        return requested;                                   // 目錄無任何支援圖 → 放行
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
        add(Opcode.GR_CALLUSER_REQ, CallUser);
        add(Opcode.GL_ENTERROOMPASS_REQ, EnterRoomPass);
        add(Opcode.GR_START_REQ, StartGame);
        add(Opcode.GR_ENDLOADING_REQ, EndLoading);
        add(Opcode.GG_STARTGAME_REQ, BeginBattle);
        add(Opcode.GR_END_REQ, EndGame);

        // 房設定簇 — REQ 限房主, 值寫入 room 欄位後以同值 ACK 廣播全房
        // (client 端 REQ 只設 pending 狀態, ACK handler 才落地 — 詳 §3.15b2)
        add(Opcode.GG_EXITGAME_REQ, ExitGame);
        add(Opcode.GR_CHANGEUSER_REQ, ChangeUserSlots);
        add(Opcode.GR_RULECHANGE_REQ, RuleChange);
        add(Opcode.GR_WINCHANGE_REQ, WinChange);
        add(Opcode.GR_TIMECHANGE_REQ, TimeChange);
        add(Opcode.GR_ITEMCHANGE_REQ, ItemChange);
        add(Opcode.GR_AUTOCHANGE_REQ, AutoChange);
        add(Opcode.GR_KILLCHANGE_REQ, KillChange);
        add(Opcode.GR_BALANCECHANGE_REQ, BalanceChange);
        add(Opcode.GR_NOSKILL_REQ, NoSkillChange);
        add(Opcode.GR_OBSERVERCHAT_REQ, ObserverChat);
        add(Opcode.GR_DAMAGEROOM_REQ, DamageRoomChange);
        add(Opcode.GR_LOCALROOM_REQ, LocalRoomChange);
        add(Opcode.GR_TEAMSHUFFLECHANGE_REQ, TeamShuffleChange);
        add(Opcode.GR_TEAMSHUFFLE_REQ, TeamShuffle);
        add(Opcode.GR_SOCCER_REQ, SoccerChange);
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
            .WriteBool(room.Soccer)                         // mode+14 隊旗 (sub_74F4D0; 969/970)
            .WriteS32(0)                                    // elapsed_ms 基準 (新局=0; sub_537670 存 timeGetTime()-x)
            .WriteU8(roomNo)                                // room_no (client 定址房物件)
            .WriteU8((byte)room.Members.Count)              // +105 cur_players
            .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣
            .WriteU8(room.MapId)                            // +130 map (sub_540280)
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU16(room.WinCount)                        // +144 勝場目標 (171/172)
            .WriteU8(room.ItemMode)                         // flags bit0→mode+4, bit1→mode+8 (175/176)
            .WriteBool(IsTeamMode(room.Rule))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
            .WriteU8(0)                                     // +109 room_type_B (client 僅鏡像)
            .WriteBool(room.TeamShuffle)                    // mode+13 隊打散開關 (368/369)
            .WriteBool(room.NoSkillBg)                      // +185 noskillbg (712/713)
            .WriteBool(room.DoubleDamage);                  // +128 double_damage (990/991)
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
            .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣 (回房恢復)
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU8(room.TimeLimit)                        // +136 時間 (173/174)
            .WriteU16(room.WinCount)                        // +144 勝場目標 (171/172)
            .WriteU8(room.ItemMode)                         // flags bit0→mode+4, bit1→mode+8
            .WriteU8(0)                                     // +146 (mode param, client 存而不讀)
            .WriteU16(room.KillCount)                       // +148 擊殺目標 (340/341)
            .WriteU8(0)                                     // +150 (mode param, client 存而不讀)
            .WriteBool(IsTeamMode(room.Rule))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
            .WriteU8(0)                                     // +109 room_type_B (client 僅鏡像)
            .WriteBool(room.TeamShuffle);                   // mode+13 隊打散開關 (368/369)
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

    // 191 REQ (sub_56FD60): str nick — 呼叫指定玩家 (房內點名)。
    // → 192 ACK (sub_56FE10): 僅當接收者房狀態==2 (在房內) 才讀 body,
    //   u8 caller_slot + str caller_nick → sub_406DB0 彈「呼叫」視窗;
    //   否則連 body 都不讀 (sub_5376F0(byte_EE8968)=+24 非 2 即返回)。
    //   server 側: 雙方需同房才送 body (離線/異房一律靜默 — 192 無錯誤碼)。
    private static async ValueTask CallUser(Session session, Packet packet, ServerContext context)
    {
        var targetNick = packet.ReadStr();
        if (targetNick.Length == 0)
        {
            return;
        }

        var target = context.Sessions.Find(targetNick);
        if (target is null || ReferenceEquals(target, session))
        {
            return;
        }

        // 雙方需同房 (sub_56FE10 依接收者房狀態==2 才讀 body)
        if (!TryGetRoom(session, context, out _, out var callerSlot)
            || target.RoomNo != session.RoomNo)
        {
            return;
        }

        await target.SendAsync(new Packet(Opcode.GR_CALLUSER_ACK)
            .WriteU8(callerSlot)                            // 呼叫者 slot
            .WriteStr(session.Nickname));                   // 呼叫者暱稱
    }

    // 125 REQ (sub_56E860 wstr 版; sub_56E6C0 str 版為死碼 — 無呼叫者):
    //   s32 uid(server 回 126 時 client 讀後丟棄), u8 slot, wstr message
    // → 126 ACK (sub_56EA80): s32 uid(丟棄), u8 slot, wstr message —
    //   以 slot 定址顯示, 全房廣播
    private static async ValueTask RoomChat(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        int uid = packet.ReadS32();
        _ = packet.ReadU8();                                     // client 附 slot (以 server 記錄為準)

        // 活路一律 wstr (sub_56E860); 但保留 str 回退以容忍怪客端
        var message = packet.Remaining >= 2 && packet.Remaining % 2 == 0
            ? packet.ReadWStr()
            : packet.ReadStr();

        if (message.Length == 0)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        var notice = new Packet(Opcode.GR_CHATTING_ACK)
            .WriteS32(uid)
            .WriteU8(slot)
            .WriteWStr(message);
        await RoomManager.BroadcastAsync(room, notice);
    }

    // 121 REQ (sub_56E480): u8 map → 122 ACK (sub_56E530): u8 map —
    // 房主換圖廣播; sub_42FC50 以 sub_540280 寫 room+130 (map)
    private static async ValueTask MapChange(Session session, Packet packet, ServerContext context)
    {
        byte mapId = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.MapId = ResolveMap(mapId, room.Rule, context.Db);  // 121 依 mode→bit 過濾

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
    }

    // ═══════════ 房設定簇 (REQ 限房主, ACK 以同值廣播全房) ═══════════
    // client 的 UI 變更函式 (sub_42FE20/sub_4306E0/…) 只寫 pending 狀態並送
    // REQ; 真正落地在 dispatcher 的 ACK handler (sub_42FE50/sub_430720/…)。
    // 因此 server 收到 REQ 後必須把新值寫回 room 欄位並廣播 ACK — 否則連
    // 房主自己的 UI 也會卡在 pending。

    /// <summary>取得 session 所在房與其 slot; 任一不成立回 false。</summary>
    private static bool TryGetRoom(Session session, ServerContext context, out Room room, out byte slot)
    {
        room = null!;
        slot = 0;

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } r)
        {
            return false;
        }

        room = r;
        foreach (var (s, member) in r.Members)
        {
            if (ReferenceEquals(member, session))
            {
                slot = s;
                return true;
            }
        }

        return false;
    }

    /// <summary>房設定變更僅房主可為 (client UI 只對房主開這些控制項)。</summary>
    private static bool IsMaster(Room room, byte slot) => slot == room.MasterSlot;

    // 139 GG_EXITGAME_REQ (sub_560720): 空 payload — 玩家離開對戰回房
    // → 140 ACK (sub_563430): u8 n2==1, u8 slot — 單人退場廣播 (n2==2 是
    //   整房重置, 由 133/134 流程觸發, 此處不涉及)
    private static async ValueTask ExitGame(Session session, Packet packet, ServerContext context)
    {
        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_EXITGAME_ACK)
            .WriteU8(1)
            .WriteU8(slot));
    }

    // 167 GR_CHANGEUSER_REQ (sub_56F360): u16 slot_mask — 房主改開放槽位點陣
    // → 168 ACK (sub_56F410→sub_4325D0): u16 slot_mask 寫 room+110 並以
    //   sub_53FB10 popcount 重算 +129 (最大人數); 點陣可非連續 (踢人/關槽)
    private static async ValueTask ChangeUserSlots(Session session, Packet packet, ServerContext context)
    {
        ushort mask = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.SlotMask = mask;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_CHANGEUSER_ACK).WriteU16(mask));
    }

    // 169 GR_RULECHANGE_REQ (sub_56F440): u8 mode(modeIndex) — 房主改遊戲模式
    // → 170 ACK (sub_56F4F0→sub_42FE50): u8 mode — client 以 sub_53FBB0 重建
    //   mode UI 並以 sub_426930(mode) 回推預設地圖寫 +130 (sub_540280)。
    //   server 同步鏡像: 有 map_StartIndex 條目的 mode 重設 MapId, 其餘
    //   (練習/教學/聊天/射擊館…無地圖目錄) 保留原圖 — 見 ModeDefaultMap。
    private static async ValueTask RuleChange(Session session, Packet packet, ServerContext context)
    {
        byte mode = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.Rule = mode;
        // client 端 sub_426930(mode) 會把 map 回推成該 mode 預設圖寫 +130;
        // server 鏡像: 有預設圖的 mode 重置, 其餘保留; 兩者皆再經 ResolveMap
        // 依 mode→bit 過濾 (防呆, 正常預設圖必合法故為 no-op)。
        byte oldMap = room.MapId;
        room.MapId = ResolveMap(
            ModeDefaultMap.TryGetValue(mode, out byte defaultMap) ? defaultMap : room.MapId,
            mode, context.Db);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_RULECHANGE_ACK).WriteU8(mode));
        // 例外: SOCCER 預設圖 98 是 TS 圖 (map_StartIndex 原廠 bug), ResolveMap
        // 會回退成 99 — client 卻仍照 98 寫 +130; 補發 122 把 client 拉回一致。
        if (room.MapId != oldMap)
        {
            await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
        }
    }

    // 171 GR_WINCHANGE_REQ (sub_56F520): u16 win_count — 房主改勝場目標
    // → 172 ACK (sub_56F5D0→sub_430720): u16 寫 room+144
    private static async ValueTask WinChange(Session session, Packet packet, ServerContext context)
    {
        ushort win = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.WinCount = win;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_WINCHANGE_ACK).WriteU16(win));
    }

    // 173 GR_TIMECHANGE_REQ (sub_56F600): u8 time_idx — 房主改遊戲時間
    // → 174 ACK (sub_56F6B0→sub_430920): u8 寫 room+136
    private static async ValueTask TimeChange(Session session, Packet packet, ServerContext context)
    {
        byte time = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TimeLimit = time;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TIMECHANGE_ACK).WriteU8(time));
    }

    // 175 GR_ITEMCHANGE_REQ (sub_56F6E0): u8 item_mode(2bit) — 房主改道具
    // → 176 ACK (sub_56F790→sub_430D50): bit0→sub_74F450(mode+4), bit1→
    //   sub_74F430(mode+8) — 兩把武器的道具開關
    private static async ValueTask ItemChange(Session session, Packet packet, ServerContext context)
    {
        byte item = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.ItemMode = item;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ITEMCHANGE_ACK).WriteU8(item));
    }

    // 177 GR_AUTOCHANGE_REQ (sub_56F8A0): s8 — client 端無呼叫者 (死碼),
    // 178 ACK 亦不在 sub_58B010 主 switch (走 vtable 前置轉發器)。僅吸收。
    private static ValueTask AutoChange(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadS8() : (sbyte)0;
        return ValueTask.CompletedTask;
    }

    // 340 GR_KILLCHANGE_REQ (sub_56F7C0): u16 kill_count — 與 171 同送 (sub_4306E0)
    // → 341 ACK (sub_56F870→sub_430F50): u16 寫 room+148
    private static async ValueTask KillChange(Session session, Packet packet, ServerContext context)
    {
        ushort kill = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.KillCount = kill;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_KILLCHANGE_ACK).WriteU16(kill));
    }

    // 364 GR_BALANCECHANGE_REQ (sub_56FA30): u8 — 房主切換隊伍平衡
    // → 365 ACK (sub_56FAE0→sub_431160): u8 只寫 GAMEROOM_TEAMBALANCE UI
    //   (一般房 room+186 不上 wire; 錦標賽 ctor sub_53F9F0 才寫 +186)
    private static async ValueTask BalanceChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TeamBalance = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_BALANCECHANGE_ACK).WriteU8(on));
    }

    // 712 GR_NOSKILL_REQ (sub_56FB10): u8 — 房主切換 no-skill 背景
    // → 713 ACK (sub_56FBC0→sub_4312C0): u8 寫 room+185 (noskillbg)
    private static async ValueTask NoSkillChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.NoSkillBg = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_NOSKILL_ACK).WriteU8(on));
    }

    // 728 GR_OBSERVERCHAT_REQ (sub_56E560): wstr sender, wstr message —
    // 觀戰者聊天 (觀戰/錦標賽 n2==2 才走這條; 一般房走 125)
    // → 729 ACK (sub_56E610): wstr sender, wstr message — 全房廣播
    private static async ValueTask ObserverChat(Session session, Packet packet, ServerContext context)
    {
        var sender = packet.ReadWStr();
        var message = packet.ReadWStr();
        if (!TryGetRoom(session, context, out var room, out _) || message.Length == 0)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_OBSERVERCHAT_ACK)
            .WriteWStr(sender)
            .WriteWStr(message));
    }

    // 990 GR_DAMAGEROOM_REQ (sub_56F950): u8 — 房主切換 double damage
    // → 991 ACK (sub_56FA00→sub_430FD0): u8 寫 room+128 (double_damage)
    private static async ValueTask DamageRoomChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.DoubleDamage = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_DAMAGEROOM_ACK).WriteU8(on));
    }

    // 366 GR_LOCALROOM_REQ (sub_585FD0): u8 — 房主切換區域限定房
    //   (n2_0==3 錦標賽場景時 client 不送; 名稱表未註冊, 由
    //   GAMEROOM_LOCALROOM UI 字串補名, 同 990/991 之例)
    // → 367 ACK (sub_586090→sub_437B50): u8 — 寫 GAMEROOM_LOCALROOM 勾選
    private static async ValueTask LocalRoomChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.LocalRoom = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_LOCALROOM_ACK).WriteU8(on));
    }

    // 368 GR_TEAMSHUFFLECHANGE_REQ (sub_585CE0): u8 — 房主切換隊打散開關
    // → 369 ACK (sub_585D90→sub_4354B0): u8 — 寫 mode rule 物件 +13 並
    //   勾選 GAMEROOM_TEAMSHUFFLE
    private static async ValueTask TeamShuffleChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TeamShuffle = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TEAMSHUFFLECHANGE_ACK).WriteU8(on));
    }

    // 969 GR_SOCCER_REQ (sub_5860C0): u8 — 房主切換足球模式
    //   (名稱表未註冊, 由 GAMEROOM_SOCCER UI 字串補名)
    // → 970 ACK (sub_586180→sub_437D00): u8 — 寫 mode rule 物件 +14
    //   (sub_74F4D0) 並勾選 GAMEROOM_SOCCER
    private static async ValueTask SoccerChange(Session session, Packet packet, ServerContext context)
    {
        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.Soccer = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_SOCCER_ACK).WriteU8(on));
    }

    // 894 GR_TEAMSHUFFLE_REQ (sub_585DC0): u8 room_no, u8 map — 房主執行
    //   隊打散 (map 為 client 目前地圖, server 不重驗)
    // → 895 ACK (sub_585E70→sub_435680): u8 status; ==1 → u16(讀後丟棄),
    //   u8 count, count×(u8 slot, s32 uid) — 全房依 uid 重排槽位。
    //   status 語意由 msgtableres.lang 解出 (sub_435680 各 case 播的
    //   sub_408080 訊息): 7=權限不足, 8=模式不支援, 6=不足3人,
    //   13=未分兩隊, 5=尚未全員 ready, 14=人數過多, 12=進行中不可,
    //   3/4/9/11=其他失敗 — 詳 docs/PACKETS.md §3.15b2。
    private static async ValueTask TeamShuffle(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                     // room_no (client 附自己房號)
        _ = packet.ReadU8();                                     // map (client 附目前地圖)

        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        byte status = 1;                                         // 成功
        if (!IsMaster(room, slot))
        {
            status = 7;                                          // 權限がないため…
        }
        else if (!IsTeamMode(room.Rule))
        {
            status = 8;                                          // 支援しないモードです
        }
        else if (room.Members.Count < 3)
        {
            status = 6;                                          // 3人以上のプレイヤーが必要
        }

        var ack = new Packet(Opcode.GR_TEAMSHUFFLE_ACK).WriteU8(status);
        if (status == 1)
        {
            var assignment = room.ShuffleSlots();
            ack.WriteU16(0)                                      // client 讀後丟棄
               .WriteU8((byte)assignment.Count);
            foreach (var (newSlot, member) in assignment)
            {
                ack.WriteU8(newSlot)
                   .WriteS32((int)member.UserId);
            }
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

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

    /// <summary>
    /// sub_438990 的 server 側對照: 兩隊制的模式 (0/2/3/4/8/10/11/12/13)
    /// 才是隊伍房; 1/5/6/7/9/15 為個人/無分隊模式。
    /// </summary>
    private static bool IsTeamMode(byte mode) => mode is 0 or 2 or 3 or 4 or 8 or 10 or 11 or 12 or 13;

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
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        await context.Rooms.RemoveMemberAsync(room, session);
        await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }
}
