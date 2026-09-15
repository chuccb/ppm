// =============================================================================
// Shared Room compatibility support
// No opcode callback lives here. This file holds only the map compatibility
// lookup and room/member authority guards used by multiple exact opcode files.
// =============================================================================
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
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
    private static bool IsNativeTwoTeamMode(byte mode) => mode is 0 or 2 or 3 or 4 or 8 or 10 or 11 or 12 or 13;
}
