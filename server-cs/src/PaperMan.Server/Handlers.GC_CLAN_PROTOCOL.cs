// =============================================================================
// GC_CLAN_PROTOCOL_REQ (583) → GC_CLAN_PROTOCOL_ACK (584)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ClanHandlers
{
    // 583 隧道: s32 sub_opcode + 子內容
    private static async ValueTask GC_CLAN_PROTOCOL_REQ(Session session, Packet packet, ServerContext context)
    {
        var sub = ClanTunnel.ReadSubOp(packet);
        switch (sub)
        {
            // 187 Info: REQ = s32 clan_id; 未入隊 → 回 0 即可
            case ClanSubOp.Info:
            {
                await session.SendAsync(ClanTunnel.Ack(sub, body => body.WriteS32(0)));
                break;
            }

            // 200 MemberList: REQ = s32 clan_id;
            // ACK = s32 count + count×{s32 rank(0..4), s32 uid, s32 level,
            //       str nick, str, s32 status} (sub_54EE70)
            case ClanSubOp.MemberList:
            {
                await session.SendAsync(ClanTunnel.Ack(sub, body => body.WriteS32(0)));
                break;
            }

            // 203 戰隊聊天: REQ = str message (rank>1 才可送, sub_54F2D0);
            // 廣播由房間/頻道層做, 單人伺服器先回聲給自己
            case ClanSubOp.Chat when session.UserId != 0:
            {
                var message = packet.ReadStr();
                await session.SendAsync(ClanTunnel.Ack(sub, body => body.WriteStr(message)));
                break;
            }

            default:
            {
                Console.WriteLine($"[clan] 未實作 sub={sub} ({(int)sub}) from user={session.UserId}");
                break;
            }
        }
    }
}
