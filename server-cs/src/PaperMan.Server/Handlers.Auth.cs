// =============================================================================
// 登入/連線 handlers — 佈局出自反編譯 (docs/PACKETS.md §1.4, §3.1):
//   682 GL_LOGIN_REQ    → 681 GL_LOGIN_ACK (0x43E651 分支) + 694 門檻協商
//   ping 方向 (十輪更正): 伺服器主動發 102, client (case 102 →
//   sub_58D6F0) 回 101 — 收到 101 只需更新 last-seen, 不可回 102
//   (否則形成無限 ping 迴圈)。101/102 皆空 payload。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class AuthHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GT_PING_REQ, PingReply);
        add(Opcode.GL_LOGIN_REQ, Login);
    }

    // 101 = client 對伺服器 102 的回應 (sub_58D6F0: 收 102 → ctor(101) 送出)。
    // 靜默吸收即可; 週期性發 102 屬 keepalive 機制 (Session 層可選)。
    private static ValueTask PingReply(Session session, Packet packet, ServerContext context)
    {
        session.LastPongAt = DateTimeOffset.UtcNow;
        return ValueTask.CompletedTask;
    }

    // REQ builder (0x43DFxx, sub_401B50 取帳號):
    //   str account ×2, u64 hw(混淆), u8 sec_state(0/1/2, sub_9A8790/9A86A0),
    //   byte[24] 指紋塊 (0x18, 全零初始化)
    // hw 混淆 (交叉驗證修正): v5 = (u64)hw32 << 32;
    //   wire = sub_592AE0(pkt, (v5|0xAA)^0xA4, HIDWORD(v5)^0xB1A9D7C7)
    //   → lo32(wire) = 0xAA^0xA4 = 0x0E (恆定), hi32(wire) = hw32^0xB1A9D7C7
    private static async ValueTask Login(Session session, Packet packet, ServerContext context)
    {
        var account = packet.ReadStr();
        var token = packet.ReadStr();
        ulong hwObf = packet.ReadU64();
        _ = packet.ReadU8();                                    // security_state
        _ = packet.ReadRaw(Math.Min(24, packet.Remaining));          // 版本/指紋塊

        // 還原: hw32 = hi32 ^ 0xB1A9D7C7; lo32 恆 0x0E 可作完整性檢查
        uint hw32 = (uint)(hwObf >> 32) ^ 0xB1A9D7C7;
        bool hwValid = (uint)hwObf == 0x0E;
        ulong hwKey = hwValid ? hw32 : hwObf;              // 異常時保留原始值供記錄

        var r = context.Db.Login(account, token, hwKey);

        if (r.Result is LoginCode.Ok)
        {
            (session.AccountId, session.UserId, session.Nickname) = (r.AccountId, r.UserId, r.Nickname);
        }

        // ⚠ 694 絕不可在此重發 — client 的 694 handler (0x43F...) 讀完門檻
        //   會呼叫 sub_43DF00 再送一次 682 → 無限登入迴圈。
        //   694 屬連線建立時的歡迎包 (見 Program.RunSessionAsync)。
        await session.SendAsync(BuildLoginAck(r, context.Config));
    }

    /// <summary>681 結構: 見 docs/PACKETS.md §1.4 GL_LOGIN_ACK。</summary>
    private static Packet BuildLoginAck(Db.LoginResult r, ServerConfig cfg)
    {
        var ack = new Packet(Opcode.GL_LOGIN_ACK).WriteS32((int)r.Result);
        if (r.Result is not LoginCode.Ok)
        {
            return ack;
        }

        ack.WriteS32((int)r.UserId)                        // user_no
           .WriteS32(100)                                  // n100 伺服器等級參數
           .WriteS32(0)                                    // ext_count=0 → 無 ext 三元組
           .WriteS16(1)                                    // server_count
           .WriteS16(1)                                    //   server_id
           .WriteStr(cfg.ServerName)
           .WriteStr(cfg.PublicHost)                       //   host (16B 定長區)
           .WriteS16((short)cfg.Port)
           .WriteU8(0)                                     //   flag
           .WriteS16(0);                                   //   group

        for (int group = 0; group < 3; group++)            // 每台 3 組頻道
        {
            if (group != 0)
            {
                ack.WriteS16(0);                            // 空頻道組: 只寫 ch_count=0
                continue;
            }

            ack.WriteS16(1)                                //   ch_count
               .WriteU8(0)                                 //   ch_type (≠3 → 無 extra byte)
               .WriteStr("Ch.1")
               .WriteS16((short)cfg.ChannelPort)           //   頻道 port (握手 693!)
               .WriteU8(0);
        }

        return ack.WriteU32(0).WriteU32(0);                // billing ×2
    }
}
