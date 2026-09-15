// =============================================================================
// Clan opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ClanHandlers
{
    // =============================================================================
    // 戰隊 handlers — 兩條路徑 (八輪交叉驗證定案):
    //
    //   1) 建隊走「獨立對」GC_CLAN_CREATE_REQ(585) / _ACK(586) — 不走隧道!
    //      REQ (builder sub_5505F0): str name, str slogan, str intro, u8 emblem
    //      ACK (sub_54CB90):        s8 result — 0=成功 (再讀 s32 = 扣費後 GP,
    //                               sub_54DD70 → EE8D18), 1..7 = 錯誤碼
    //
    //   2) 其餘全部走隧道 GC_CLAN_PROTOCOL_REQ(583) / _ACK(584):
    //      payload = s32 sub_opcode + 子內容 (24 REQ builder / 28 ACK case,
    //      逐一佈局見 docs/PACKETS.md §2)
    //
    // 未支援的子協定記錄後靜默忽略 (與客戶端 default: return 對稱)。
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.GC_CLAN_CREATE_REQ, GC_CLAN_CREATE_REQ);
        add(Opcode.GC_CLAN_PROTOCOL_REQ, GC_CLAN_PROTOCOL_REQ);
        add(Opcode.GL_CLAN_TNMT_ENTERROOM_REQ, GL_CLAN_TNMT_ENTERROOM_REQ);
    }
}
