// =============================================================================
// GC_CLAN_PROTOCOL 隧道 — 對應反編譯 (五輪發現, 八輪逐一佈局定案):
//
//   GC_CLAN_PROTOCOL_REQ(583) / _ACK(584) 是**容器封包**: payload 的第一個
//   欄位是 s32 sub_opcode, 之後才是子協定內容。
//
//   ACK 端 sub_54D040 (dispatcher case 584) 依 sub_opcode 分發 28 個子處理器;
//   REQ 端 24 個 builder 全部 `ctor(583)` + `WriteS32(sub)`。
//   子編號空間 (182..383) 與頂層 opcode 註冊表**無關** — 純戰隊系統私有。
//
//   ⚠ 建隊不走隧道 — 走獨立對 GC_CLAN_CREATE_REQ(585)/_ACK(586)。
//   佈局逐條見 docs/PACKETS.md §2「戰隊隧道協定」表。
// =============================================================================
namespace PaperMan.Protocol;

/// <summary>
/// 583/584 隧道的子協定編號 (sub_54D040 case 值)。
/// 名稱依八輪逐一讀出的 handler 語意命名。
/// </summary>
public enum ClanSubOp
{
    InviteConfirm = 182,        // ACK: s32 clan_id → 歡迎訊息 (sub_54E890)
    JoinRequest = 184,          // REQ: str clan_name; ACK: s32 id, str nick
    JoinAnswer = 185,           // REQ: s32, s32 uid, s32 11, s32 0
    Kick = 186,                 // REQ: str nick; ACK: s32 uid, str nick
    Info = 187,                 // REQ: s32 clan_id
    MemberPage = 188,           // REQ: s32 clan_id, s32 page
    Notice = 189,               // REQ: s32 clan_id; ACK: s32, str ×2
    Invite = 191,               // REQ: s32 n3, str nick
    InviteAnswer = 192,         // REQ: s32; ACK: s32, str×4, s32×5 摘要塊
    Disband = 193,              // REQ: s32 flag
    Promote194 = 194,           // ACK only (sub_54EE10)
    MarkQuery = 195,            // REQ: s32 id, str
    MemberIds = 196,            // REQ: s32 count + raw(4*count)
    MemberIds2 = 197,           // 同上第二型
    WarInvite = 198,            // ACK: s32, s32, str×2, s32 (sub_54F450)
    WarAnswer = 199,            // ACK (sub_54F5A0)
    MemberList = 200,           // REQ: s32 clan_id; ACK: s32 count +
                                //   count×{s32 rank, s32 uid, s32 level,
                                //   str nick, str, s32 status} (sub_54EE70)
    Rank = 201,                 // ACK: s32, [s32] (sub_54F0F0)
    CountQuery = 202,           // REQ: 無; ACK: s32, s32
    Chat = 203,                 // REQ: str message (需 rank>1, sub_54F2D0)
    ChatEcho = 204,             // ACK (sub_54F330)
    Message = 205,              // REQ: str from(自動), str title, str body;
                                //   ACK: str×3 (sub_54F3D0)
    Board = 206,                // ACK (sub_54F700)
    BoardWrite = 207,           // ACK (sub_54F8D0)
    Donate = 208,               // REQ: 無欄位
    Fund = 209,                 // REQ: 無欄位
    Settings = 210,             // REQ: str; ACK: s32 uid, str (sub_54E770)
    Promote = 211,              // REQ: s32 uid; ACK: s32 id, str nick →
                                //   rank:=5 (sub_54FBA0, 訊息 0x3E9)
    Demote = 212,               // 同 211 鏡像 (sub_54FD50)
    Tournament213 = 213,
    Tournament214 = 214,
    Tournament215 = 215,
    Extra218 = 218,
    Extra219 = 219,
    Stat381 = 381,              // ACK: s32×2 (sub_54FF00)
    Stat382 = 382,              // ACK: s32×2 (sub_54FFC0)
    Stat383 = 383,
}

public static class ClanTunnel
{
    /// <summary>包一個 584 ACK: s32 sub_opcode + 子 payload 寫入委派。</summary>
    public static Packet Ack(ClanSubOp sub, Action<Packet>? body = null)
    {
        var packet = new Packet(Opcode.GC_CLAN_PROTOCOL_ACK).WriteS32((int)sub);
        body?.Invoke(packet);
        return packet;
    }

    /// <summary>自 583 REQ 取出子協定編號 (payload 首 s32)。</summary>
    public static ClanSubOp ReadSubOp(Packet req) =>
        (ClanSubOp)req.ReadS32();
}
