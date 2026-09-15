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
    /// 各 modeIndex 的預設地圖 — client 載入 `system/map_StartIndex.xml`
    /// (⚠ 非 ui/ 根目錄那份舊版, 兩者 modeStartIndex 不同!) 填 room 的
    /// modeIndex→map 表；169/170 改模式時 sub_426930(modeIndex) 回推
    /// modeStartIndex 寫 +130 (sub_540280)。modeStartIndex 即 maplist (123 圖)
    /// 的絕對 map_id。server 鏡像以免 114/130/134 送舊圖。
    /// 其餘 modeIndex (Practice=5 / Tutorial=6 / ChattingRoom=7 /
    /// Occupy=10 / AIMulti=11 / OccupyRenewal=13 / WeaponTest=15 /
    /// 16=空) 無此表條目 → 保留原圖。
    /// </summary>
    private static readonly FrozenDictionary<byte, byte> ModeIndexDefaultMap =
        new Dictionary<byte, byte>
        {
            [(byte)GameMode.TeamMatch] = 106,               // map_StartIndex: TeamDeath (TD_)
            [(byte)GameMode.IndividualSurvival] = 104,      // map_StartIndex: FreeForAll (PS_)
            [(byte)GameMode.DefuseBomb] = 14,               // map_StartIndex: TeamHacking (TH_)
            [(byte)GameMode.TeamSurvival] = 107,            // map_StartIndex: TeamSurvival (TS_)
            [(byte)GameMode.Steal] = 23,                    // map_StartIndex: TeamSteal (TW_)
            [(byte)GameMode.PulpnRoll] = 51,                // map_StartIndex: PNR
            [(byte)GameMode.GunShooting] = 89,              // map_StartIndex: GunShooting
            // ⚠ TeamSoccer=12 maps to the authoritative system/map_StartIndex.xml
            // value 98 (SOCCER→98), but maplist.pat's soccer bit (0x4000) occurs
            // at 99/100 (スルルスタジアム). 98 is TeamSurvival (TS_33_tutor_castle,
            // modes=0x0002). sub_426930 writes 98 when the client changes mode;
            // mirror that native behavior rather than "correcting" it here.
            [(byte)GameMode.TeamSoccer] = 98,
        }.ToFrozenDictionary();

    /// <summary>
    /// mode → maplist.pat `modes` bitmask 位 (docs/RESOURCES.md §4b,
    /// sub_53FBB0 模式枚舉 + map_StartIndex modeName + 檔名前綴三方互證)。
    /// modeIndex 與 bit **不同編號** — 如 TeamMatch=0（XML 顯示名 TeamDeath）
    /// ↔ bit 2。無條目的 modeIndex (Practice=5 / ChattingRoom=7 / 14 未用)
    /// 不參與地圖過濾。
    /// </summary>
    private static readonly FrozenDictionary<byte, byte> ModeIndexMapBit =
        new Dictionary<byte, byte>
        {
            [(byte)GameMode.TeamMatch] = 2,                 // TeamDeath → TD  (bit 2)
            [(byte)GameMode.IndividualSurvival] = 0,        // FreeForAll → PS  (bit 0)
            [(byte)GameMode.DefuseBomb] = 3,                // TeamHacking → TH  (bit 3)
            [(byte)GameMode.TeamSurvival] = 1,              // TeamSurvival → TS  (bit 1)
            [(byte)GameMode.Steal] = 4,                     // TeamSteal → TW  (bit 4)
            [(byte)GameMode.Tutorial] = 6,                  // TU (bit 6=0x40; bit 5=0x20 is TU-only)
            [(byte)GameMode.PulpnRoll] = 9,                 // PNR (bit 9)
            [(byte)GameMode.GunShooting] = 10,              // AI  (bit 10)
            [(byte)GameMode.Occupy] = 12,                   // OCC (bit 12)
            [(byte)GameMode.AIMulti] = 13,                  // PVE (bit 13)
            [(byte)GameMode.TeamSoccer] = 14,               // TS worldcup (bit 14)
            [(byte)GameMode.OccupyRenewal] = 15,            // OCC2 (bit 15)
            [(byte)GameMode.WeaponTest] = 11,               // ECT (bit 11)
        }.ToFrozenDictionary();

    /// <summary>
    /// 依 modeIndex 過濾/校正地圖：若 modeIndex 有對應 bit 而 request 地圖不支援
    /// 該模式 (map_catalog.modes 未含該 bit)，回退該 modeIndex 預設圖；目錄查無
    /// 此地圖（無法求證）或 modeIndex 無過濾規則時原樣放行，不硬編造欄位。
    /// 回退值也再驗一次：預設圖本身不支援該 modeIndex 時（如 TeamSoccer 的
    /// XML 預設 98 沒有 soccer bit）改取目錄第一張支援該 modeIndex 的圖。
    /// </summary>
    private static byte ResolveMap(byte requested, byte modeIndex, Db db)
    {
        if (!ModeIndexMapBit.TryGetValue(modeIndex, out byte bit))
        {
            return requested;                               // 此 modeIndex 沒有 source-proven map filter
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

        if (ModeIndexDefaultMap.TryGetValue(modeIndex, out byte fallback)
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
    private static bool IsNativeTwoTeamMode(byte modeIndex) => modeIndex is
        (byte)GameMode.TeamMatch
        or (byte)GameMode.DefuseBomb
        or (byte)GameMode.TeamSurvival
        or (byte)GameMode.Steal
        or (byte)GameMode.PulpnRoll
        or (byte)GameMode.Occupy
        or (byte)GameMode.AIMulti
        or (byte)GameMode.TeamSoccer
        or (byte)GameMode.OccupyRenewal;
}
