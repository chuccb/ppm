// =============================================================================
// GC_CLAN_PROTOCOL 隧道 — 對應反編譯 (五輪交叉驗證發現):
//
//   GC_CLAN_PROTOCOL_REQ(583) / _ACK(584) 是**容器封包**: payload 的第一個
//   欄位是 s32 sub_opcode, 之後才是子協定內容。
//
//   ACK 端 sub_54D040 (dispatcher case 584) 依 sub_opcode 分發 28 個子處理器;
//   REQ 端 24 個 builder 全部 `ctor(583)` + `WriteS32(sub)`。
//   子編號空間 (182..383) 與頂層 opcode 註冊表**無關** — 純戰隊系統私有。
// =============================================================================
namespace PaperMan.Protocol;

/// <summary>583/584 隧道的子協定編號 (sub_54D040 case 值)。</summary>
public enum ClanSubOp
{
    // --- client → server (builder @0x54Bxxx 系列) ---
    Create = 182,               // sub_54E890 處理結果
    Join = 184,
    Leave = 185,
    Kick = 186,
    Info = 187,
    MemberList = 188,
    Notice = 189,
    Invite = 191,
    InviteAnswer = 192,
    Disband = 193,
    Promote = 194,
    MarkList = 195,
    MemberIds = 196,            // s32 count + raw(4*count)
    MemberIds2 = 197,           // 同上第二型
    War = 198,
    WarAnswer = 199,
    WarResult = 200,
    Rank = 201,
    Search = 202,
    Mark = 203,
    MarkUpdate = 204,
    Message = 205,
    Board = 206,
    BoardWrite = 207,
    Donate = 208,
    Fund = 209,
    Settings = 210,
    TournamentInfo = 211,
    TournamentEntry = 212,
    TournamentResult = 213,
    TournamentTree = 214,
    TournamentEnd = 215,
    Extra218 = 218,
    Extra219 = 219,
    Stat381 = 381,
    Stat382 = 382,
    Stat383 = 383,
}

public static class ClanTunnel
{
    /// <summary>包一個 584 ACK: s32 sub_opcode + 子 payload 寫入委派。</summary>
    public static Packet Ack(ClanSubOp sub, Action<Packet>? body = null)
    {
        var p = new Packet(Opcode.GC_CLAN_PROTOCOL_ACK).WriteS32((int)sub);
        body?.Invoke(p);
        return p;
    }

    /// <summary>自 583 REQ 取出子協定編號 (payload 首 s32)。</summary>
    public static ClanSubOp ReadSubOp(Packet req) => (ClanSubOp)req.ReadS32();
}
