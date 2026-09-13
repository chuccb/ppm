// =============================================================================
// 頻道伺服器握手 handlers (卅一輪 — 使用者釐清 + 逐行證據):
//
//   連線建立 → server 發 693 GL_TCPCONNSUCC (無 payload)
//     → client (sub_57CAE0) 顯示「連線中」訊息並呼叫 sub_555C60
//     → client 送 143 PM_UDPSTART_REQ:
//         str nick, s32 n100 (登入 681 給的值原樣回送),
//         s8 1, s32 ext_count (681 ext 塊 count 回送)
//     → server 回 144 PM_UDPSTART_ACK (sub_555D50 讀序, 卅一輪重驗):
//         u8 n108 (0=正常 1/2=模式切換 3=踢出), u8 flag65,
//         s32 session_id (→dword_1D0D23C), str channel_name(64),
//         s32 ×3, f32, s32, u8 flag66,
//         [flag66≠0: u8×4 + 8×s32 延伸參數 → sub_A1C800]
//
//   n100/ext_count 是隱形 session token — 與登入時發出的值比對,
//   可防止跳過登入直連頻道。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ChannelHandlers
{
    /// <summary>144 的 n108 狀態碼 (sub_555D50 分支)。</summary>
    private enum UdpStartStatus : byte
    {
        Ok = 0,
        SwitchModeA = 1,
        SwitchModeB = 2,
        Kicked = 3,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.PM_UDPSTART_REQ, UdpStart);
        add(Opcode.PM_CONNECT_REQ, PmConnect);
    }

    // 143 → 144: 頻道進入確認 (驗 n100 回送 token)
    private static async ValueTask UdpStart(Session session, Packet packet, ServerContext context)
    {
        var nickname = packet.ReadStr();
        int echoedN100 = packet.Remaining >= 4 ? packet.ReadS32() : 0;

        // 681 送的 n100=100 — 回送不符 = 未經登入流程, 視為踢出
        var status = echoedN100 == 100
            ? UdpStartStatus.Ok
            : UdpStartStatus.Kicked;

        if (session.Nickname.Length == 0 && nickname.Length > 0)
        {
            session.Nickname = nickname;                    // 單機合一模式補綁定
        }

        var ack = new Packet(Opcode.PM_UDPSTART_ACK)
            .WriteU8((byte)status)
            .WriteU8(0)                                     // flag65
            .WriteS32((int)session.Id)                      // session id → 1D0D23C
            .WriteStr(context.Config.ServerName)                // 頻道名 (str 64)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0)
            .WriteF32(0f)
            .WriteS32(0)
            .WriteU8(0);                                    // flag66=0 → 無延伸塊

        await session.SendAsync(ack);
    }

    // 141 → 142 PM_CONNECT_ACK (sub_5565D0, 卅二輪定案):
    //   str host, s32 port, u8, f32 — host/port 經 sub_596E60 直填
    //   UDP sockaddr = UDP 打洞伺服器目標。
    //   時序: 144 (n108=0) 成功後 client 自動續送 141 (handler 尾端
    //   ctor(141)) — 本方法是頻道進入鏈的最後一步。
    private static async ValueTask PmConnect(Session session, Packet packet, ServerContext context)
    {
        // 142 的 host/port 是 UDP 打洞伺服器目標 (sub_596E60 直填 sockaddr)。
        // 單機模式預留 UDP port = ChannelPort + 1 (UDP relay 為未來擴充)。
        await session.SendAsync(new Packet(Opcode.PM_CONNECT_ACK)
            .WriteStr(context.Config.PublicHost)
            .WriteS32(context.Config.ChannelPort + 1)
            .WriteU8(0)
            .WriteF32(0f));
    }
}
