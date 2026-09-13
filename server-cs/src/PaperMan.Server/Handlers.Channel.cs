// =============================================================================
// 頻道伺服器 handlers (卅三輪全鏈定案 — 兩層處理逐行證據):
//
//   connect → server 發 693 GL_TCPCONNSUCC
//   → client (sub_57CAE0) 送 143 PM_UDPSTART_REQ:
//       str nick, s32 n100 (681 回送), s8 1, s32 ext_count (681 回送)
//   → server 回 144 PM_UDPSTART_ACK (雙層處理):
//       dispatcher 層 sub_555D50 存資料/錯誤彈窗;
//       CLobbyChannel::sub_4179D0 case 144 → 自動送 195
//   → client 送 195 GC_ENTERCHANNEL_REQ (sub_56FF40):
//       u8 group (681 清單 3 組之序), u8 channel, u8 replay_flag
//   → server 回 196 GC_ENTERCHANNEL_ACK (CLobbyChannel case 196):
//       u8 result (1=成功; 0=頻道滿 0xDA, 2=維護 0x148), s32 channel_id,
//       u8; 成功→ str udp_host, s32 udp_port (⭐sub_596E60 直填
//       sockaddr = UDP 打洞目標!), u8, u8 channel_type (3=AI 頻道→
//       續讀 sub_875680 關卡大塊), f32 flags, u8 n5
//   → client 開 UDP session → UDP op18 → 141 → 142 (位址再確認)
//
//   n100/ext_count 雙 token 防跳登入直連 (144 n108=3 踢出)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ChannelHandlers
{
    /// <summary>144 的 n108 狀態碼 (sub_555D50 錯誤分支: 1=重複登入 2=?, 3=踢出)。</summary>
    private enum UdpStartStatus : byte
    {
        Ok = 0,
        DuplicateLogin = 1,
        Rejected = 2,
        Kicked = 3,
    }

    /// <summary>196 的 result 碼 (sub_4177B0 錯誤表: 0=頻道滿 0xDA, 2=維護 0x148)。</summary>
    private enum EnterChannelResult : byte
    {
        Full = 0,
        Ok = 1,
        Maintenance = 2,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.PM_UDPSTART_REQ, UdpStart);
        add(Opcode.GC_ENTERCHANNEL_REQ, EnterChannel);
        add(Opcode.PM_CONNECT_REQ, PmConnect);
    }

    // 143 → 144: 頻道進入第一步 (驗 n100 token; 成功後 client 自動送 195)
    private static async ValueTask UdpStart(Session session, Packet packet, ServerContext context)
    {
        // 143 全 4 欄 (sub_555C60 builder): str, s32, s8, s32
        var nickname = packet.ReadStr();
        int echoedN100 = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        sbyte constantOne = packet.Remaining >= 1 ? packet.ReadS8() : (sbyte)0;
        int echoedExtCount = packet.Remaining >= 4 ? packet.ReadS32() : 0;

        // 681 送的 n100=100 / ext_count=0 / 常數 1 — 三重回送驗證
        var status = echoedN100 == 100 && constantOne == 1 && echoedExtCount == 0
            ? UdpStartStatus.Ok
            : UdpStartStatus.Kicked;

        if (session.Nickname.Length == 0 && nickname.Length > 0)
        {
            session.Nickname = nickname;                    // 頻道連線補綁定
        }

        var ack = new Packet(Opcode.PM_UDPSTART_ACK)
            .WriteU8((byte)status)
            .WriteU8(0)                                     // flag65
            .WriteS32((int)session.Id)                      // → dword_1D0D23C
            .WriteStr(context.Config.ServerName)            // 頻道名 (str 64)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0)
            .WriteF32(0f)
            .WriteS32(0)
            .WriteU8(0);                                    // flag66=0 → 無延伸塊

        await session.SendAsync(ack);
    }

    // 195 → 196: 頻道選擇確認 + 下發 UDP 打洞目標 (卅三輪 — 正主在這!)
    private static async ValueTask EnterChannel(Session session, Packet packet, ServerContext context)
    {
        byte group = packet.ReadU8();
        byte channel = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;
        _ = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;   // replay flag

        var ack = new Packet(Opcode.GC_ENTERCHANNEL_ACK)
            .WriteU8((byte)EnterChannelResult.Ok)
            .WriteS32(group << 8 | channel)                 // channel_id → 417D00()[1]
            .WriteU8(0)                                     // v17
            // --- result==1 成功塊 ---
            .WriteStr(context.Config.PublicHost)            // ⭐ UDP 打洞位址
            .WriteS32(context.Config.ChannelPort + 1)       //    (預留 :40202)
            .WriteU8(0)                                     // → 1D0CFE4
            .WriteU8(0)                                     // channel_type (0=一般; 3=AI 需大塊)
            .WriteF32(0f)                                   // flags (bit0 → 1D0D21B)
            .WriteU8(5);                                    // n5 → 417D00()[8] (client 預設 5)

        await session.SendAsync(ack);
    }

    // 141 → 142 (sub_5565D0): UDP 位址再確認 — 由 UDP op18 觸發的重連路徑
    private static async ValueTask PmConnect(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.PM_CONNECT_ACK)
            .WriteStr(context.Config.PublicHost)
            .WriteS32(context.Config.ChannelPort + 1)
            .WriteU8(0)
            .WriteF32(0f));
    }
}
