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
using System.Diagnostics.CodeAnalysis;
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
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
        // membership snapshot / mutation — Handlers.Room.Membership.cs
        add(Opcode.GL_MAKEROOM_REQ, MakeRoom);
        add(Opcode.GL_ENTERROOM_REQ, EnterRoom);
        add(Opcode.GR_LEAVE_REQ, LeaveRoom);

        // lobby interaction — Handlers.Room.Lobby.cs
        add(Opcode.GR_CHATTING_REQ, RoomChat);
        add(Opcode.GR_MAPCHANGE_REQ, MapChange);
        add(Opcode.GR_READY_REQ, Ready);
        add(Opcode.GR_CHANGESLOT_REQ, ChangeSlot);
        add(Opcode.GR_CALLUSER_REQ, CallUser);
        add(Opcode.GL_ENTERROOMPASS_REQ, EnterRoomPass);

        // match lifecycle — Handlers.Room.Match.cs
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

        // 訊息 relay — Handlers.Room.Messages.cs
        add(Opcode.GR_RADIOMSG_REQ, Radio);
        add(Opcode.GG_ROOMBROADCAST_REQ, RoomBroadcast);
        add(Opcode.GG_OBSERVERCHAT_REQ, ObserverChatGame);

        // 房主強制踢人 (131/132)。
        add(Opcode.GR_FORCEOUT_REQ, ForceOut);

        // 718–722 投票與 983–989 配對目前只證實 client wire grammar／
        // reader；尚無 service-owned vote/matching state，故刻意不註冊。
        // 詳見 docs/TODO_HANDLERS.md 的 current evidence。
    }

    // ---- 共用房間 membership / authority guard ----------------------------

    /// <summary>取得 session 所在房與其 slot; 任一不成立回 false。</summary>
    private static bool TryGetRoom(
        Session session,
        ServerContext context,
        [NotNullWhen(true)] out Room? room,
        out byte slot)
    {
        room = null;
        slot = 0;

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } r)
        {
            return false;
        }

        foreach (var (memberSlot, member) in r.Members)
        {
            if (ReferenceEquals(member, session))
            {
                room = r;
                slot = memberSlot;
                return true;
            }
        }

        return false;
    }

    /// <summary>房設定變更僅房主可為 (client UI 只對房主開這些控制項)。</summary>
    private static bool IsMaster(Room room, byte slot) => slot == room.MasterSlot;

    /// <summary>
    /// sub_438990 的 server 側對照: 兩隊制的模式 (0/2/3/4/8/10/11/12/13)
    /// 才是隊伍房; 1/5/6/7/9/15 為個人/無分隊模式。
    /// </summary>
    private static bool IsTeamMode(byte mode) => mode is 0 or 2 or 3 or 4 or 8 or 10 or 11 or 12 or 13;
}
