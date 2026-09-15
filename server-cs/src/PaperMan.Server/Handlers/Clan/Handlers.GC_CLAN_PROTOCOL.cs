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
        var subOpcode = GC_CLAN_PROTOCOL_Wire.ReadRequestSubOpcode(packet);
        switch (subOpcode)
        {
            // 187: REQ = s32 clan_id. Fact/HIGH: sub_54EA60 reads the clan id
            // first and accepts no tail when it has no local clan record.
            // UNRESOLVED (original-service policy): emit the safe zero-clan-id branch only.
            case GC_CLAN_PROTOCOL_SubOpcode.Sub187:
            {
                await session.SendAsync(
                    GC_CLAN_PROTOCOL_Wire.CreateAcknowledgement(subOpcode, body => body.WriteS32(0)));
                break;
            }

            // 200: member-list REQ = s32 clan_id; ACK = s32 count +
            // count×{s32 rank(0..4), s32 uid, s32 level, str nick, str, s32 status}
            // (sub_54EE70). UNRESOLVED (original-service policy): emit only the
            // valid empty list; this is not a claimed roster response.
            case GC_CLAN_PROTOCOL_SubOpcode.Sub200:
            {
                await session.SendAsync(
                    GC_CLAN_PROTOCOL_Wire.CreateAcknowledgement(subOpcode, body => body.WriteS32(0)));
                break;
            }

            // 203: REQ = str message; ACK = str nick, str message
            // (sub_54F2D0 reads both). Its rank>1 sender gate is native.
            // Inference/MEDIUM (private-server policy): broadcast belongs to
            // channel/room state, so a single-user server echoes the
            // authenticated sender and message only to that sender.
            case GC_CLAN_PROTOCOL_SubOpcode.Sub203 when session.UserId != 0:
            {
                var message = packet.ReadStr();
                await session.SendAsync(
                    GC_CLAN_PROTOCOL_Wire.CreateAcknowledgement(
                        subOpcode,
                        body => body.WriteStr(session.Nickname).WriteStr(message)));
                break;
            }

            default:
            {
                Console.WriteLine(
                    $"[clan] unimplemented GC_CLAN_PROTOCOL sub={subOpcode} ({(int)subOpcode}) user={session.UserId}");
                break;
            }
        }
    }
}
