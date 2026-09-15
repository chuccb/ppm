// =============================================================================
// MASTER_ROOMINFO_REQ (394) → MASTER_ROOMINFO_ACK (395)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 394 MASTER_ROOMINFO_REQ (sub_5790A0: u8 room_no)
    // → 395 ACK (sub_579160): u8 count, count×(u8 slot, str nick, str ip)
    private static async ValueTask MASTER_ROOMINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var room = context.Rooms.Find(roomNo);

        var ack = new Packet(Opcode.MASTER_ROOMINFO_ACK);
        if (room is null)
        {
            ack.WriteU8(0);
        }
        else
        {
            ack.WriteU8((byte)room.Members.Count);
            foreach (var (slot, member) in room.Members)
            {
                ack.WriteU8(slot)
                   .WriteStr(member.Nickname)
                   .WriteStr(member.RemoteIp);
            }
        }

        await session.SendAsync(ack);
    }
}
