// =============================================================================
// 登入/連線 handlers — 佈局出自反編譯 (docs/PACKETS.md §1.4, §3.1):
//   682 GL_LOGIN_REQ    → 681 GL_LOGIN_ACK (0x43E651 分支) + 694 門檻協商
//   101 GT_PING_REQ     → 102 GT_PING_ACK (空 payload)
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class AuthHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GT_PING_REQ, Ping);
        add(Opcode.GL_LOGIN_REQ, Login);
    }

    private static async ValueTask Ping(Session s, Packet p, ServerContext ctx) =>
        await s.SendAsync(new Packet(Opcode.GT_PING_ACK));

    // REQ builder (0x43DFxx, sub_401B50 取帳號):
    //   str account ×2, u64 hw(混淆), u8 sec_state(0/1/2, sub_9A8790/9A86A0),
    //   byte[24] 指紋塊 (0x18, 全零初始化)
    // hw 混淆 (交叉驗證修正): v5 = (u64)hw32 << 32;
    //   wire = sub_592AE0(pkt, (v5|0xAA)^0xA4, HIDWORD(v5)^0xB1A9D7C7)
    //   → lo32(wire) = 0xAA^0xA4 = 0x0E (恆定), hi32(wire) = hw32^0xB1A9D7C7
    private static async ValueTask Login(Session s, Packet p, ServerContext ctx)
    {
        var account = p.ReadStr();
        var token = p.ReadStr();
        ulong hwObf = p.ReadU64();
        _ = p.ReadU8();                                    // security_state
        _ = p.ReadRaw(Math.Min(24, p.Remaining));          // 版本/指紋塊

        // 還原: hw32 = hi32 ^ 0xB1A9D7C7; lo32 恆 0x0E 可作完整性檢查
        uint hw32 = (uint)(hwObf >> 32) ^ 0xB1A9D7C7;
        bool hwValid = (uint)hwObf == 0x0E;
        ulong hwKey = hwValid ? hw32 : hwObf;              // 異常時保留原始值供記錄

        var r = ctx.Db.Login(account, token, hwKey);
        await s.SendAsync(BuildLoginAck(r, ctx.Config));

        if (r.Result is LoginCode.Ok)
        {
            (s.AccountId, s.UserId, s.Nickname) = (r.AccountId, r.UserId, r.Nickname);
            // 694: u16 壓縮門檻 (client 收到 <0x2580 才啟用壓縮)
            await s.SendAsync(new Packet(Opcode.GL_ACCOUNTCONNSUCC)
                .WriteU16(ctx.Config.CompressThreshold));
        }
    }

    /// <summary>681 結構: 見 docs/PACKETS.md §1.4 GL_LOGIN_ACK。</summary>
    private static Packet BuildLoginAck(Db.LoginResult r, ServerConfig cfg)
    {
        var ack = new Packet(Opcode.GL_LOGIN_ACK).WriteS32((int)r.Result);
        if (r.Result is not LoginCode.Ok)
            return ack;

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
            if (group != 0) { ack.WriteS16(0); continue; }
            ack.WriteS16(1)                                //   ch_count
               .WriteU8(0)                                 //   ch_type (≠3 → 無 extra byte)
               .WriteStr("Ch.1")
               .WriteS16((short)cfg.Port)
               .WriteU8(0);
        }
        return ack.WriteU32(0).WriteU32(0);                // billing ×2
    }
}
