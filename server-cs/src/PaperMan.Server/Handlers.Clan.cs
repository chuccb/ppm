// =============================================================================
// 戰隊隧道 handlers — GC_CLAN_PROTOCOL_REQ(583) → _ACK(584)。
// 佈局出自反編譯 (docs/PACKETS.md §2 多層分發):
//   REQ payload = s32 sub_opcode + 子內容 (24 個 builder, ctor(583))
//   ACK payload = s32 sub_opcode + 子內容 (sub_54D040 分發 28 個 case)
// 目前為最小可用實作: 記錄未支援的子協定並優雅忽略 (與原版 default 一致);
// 建立/解散等由 Db 支撐, 其餘回「無戰隊」語意。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ClanHandlers
{
    public static void Register(Registrar add) =>
        add(Opcode.GC_CLAN_PROTOCOL_REQ, Tunnel);

    private static async ValueTask Tunnel(Session s, Packet p, ServerContext ctx)
    {
        var sub = ClanTunnel.ReadSubOp(p);
        switch (sub)
        {
            // 182 Create: REQ 帶 str name (+選項); ACK 182 = s32 clan_id
            //   (sub_54E890: s32 → 查戰隊物件, 成功則顯示歡迎訊息)
            case ClanSubOp.Create when s.UserId != 0:
            {
                var name = p.ReadStr();
                long clanId = ctx.Db.CreateClan(s.UserId, name);
                await s.SendAsync(ClanTunnel.Ack(ClanSubOp.Create,
                    body => body.WriteS32((int)clanId)));
                break;
            }

            // 187 Info / 188 MemberList: 未入隊 → 空回應即可 (client 判 s32<=0)
            case ClanSubOp.Info or ClanSubOp.MemberList:
                await s.SendAsync(ClanTunnel.Ack(sub, body => body.WriteS32(0)));
                break;

            default:
                Console.WriteLine($"[clan] 未實作 sub={sub} ({(int)sub}) from user={s.UserId}");
                break;
        }
    }
}
