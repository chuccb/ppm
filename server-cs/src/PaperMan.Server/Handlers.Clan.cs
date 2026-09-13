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
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ClanHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GC_CLAN_CREATE_REQ, Create);
        add(Opcode.GC_CLAN_PROTOCOL_REQ, Tunnel);
    }

    /// <summary>586 的 result 碼 (sub_54CB90 的 switch 分支)。</summary>
    private enum CreateResult : sbyte
    {
        Ok = 0,             // → 續讀 s32 扣費後 GP
        DuplicateName = 1,  // 0x1A7 重名
        NotEnoughGp = 2,    // 0x1A8 GP 不足
        Failed = 3,         // 其他失敗 (3..7 各有訊息)
    }

    // REQ(585) sub_5505F0: str name, str slogan, str intro, u8 emblem
    private static async ValueTask Create(Session s, Packet p, ServerContext ctx)
    {
        var name = p.ReadStr();
        var slogan = p.ReadStr();
        var intro = p.ReadStr();
        int emblem = p.Remaining >= 4 ? p.ReadS32() : 0;    // s32 (廿四輪修正)
        _ = (slogan, intro);                                // schema 暫存於 notice 欄位外

        var result = s.UserId switch
        {
            0 => CreateResult.Failed,
            _ => ctx.Db.CreateClan(s.UserId, name, (byte)Math.Clamp(emblem, 0, 255)) switch
            {
                > 0 => CreateResult.Ok,
                _ => CreateResult.DuplicateName,
            },
        };

        var ack = new Packet(Opcode.GC_CLAN_CREATE_ACK).WriteS8((sbyte)result);
        if (result is CreateResult.Ok)
        {
            var info = ctx.Db.GetMyInfo(s.UserId);
            ack.WriteS32((int)(info?.Gp ?? 0));             // sub_54DD70 → EE8D18
        }

        await s.SendAsync(ack);
    }

    // 583 隧道: s32 sub_opcode + 子內容
    private static async ValueTask Tunnel(Session s, Packet p, ServerContext ctx)
    {
        var sub = ClanTunnel.ReadSubOp(p);
        switch (sub)
        {
            // 187 Info: REQ = s32 clan_id; 未入隊 → 回 0 即可
            case ClanSubOp.Info:
            {
                await s.SendAsync(ClanTunnel.Ack(sub, body => body.WriteS32(0)));
                break;
            }

            // 200 MemberList: REQ = s32 clan_id;
            // ACK = s32 count + count×{s32 rank(0..4), s32 uid, s32 level,
            //       str nick, str, s32 status} (sub_54EE70)
            case ClanSubOp.MemberList:
            {
                await s.SendAsync(ClanTunnel.Ack(sub, body => body.WriteS32(0)));
                break;
            }

            // 203 戰隊聊天: REQ = str message (rank>1 才可送, sub_54F2D0);
            // 廣播由房間/頻道層做, 單人伺服器先回聲給自己
            case ClanSubOp.Chat when s.UserId != 0:
            {
                var message = p.ReadStr();
                await s.SendAsync(ClanTunnel.Ack(sub, body => body.WriteStr(message)));
                break;
            }

            default:
            {
                Console.WriteLine($"[clan] 未實作 sub={sub} ({(int)sub}) from user={s.UserId}");
                break;
            }
        }
    }
}
