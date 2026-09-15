// =============================================================================
// GL_CLAN_TNMT_ENTERROOM_REQ (764) → GL_CLAN_TNMT_ENTERROOM_ACK (765)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ClanHandlers
{
    // 764 GL_CLAN_TNMT_ENTERROOM_REQ (sub_57E8F0: u8 room_no, s32 clan_id)
    // → 765 GL_CLAN_TNMT_ENTERROOM_ACK (sub_57E9A0: 與 114 同構, 錦標賽版進房, 含 sub_885D00 語音塊)
    private static async ValueTask GL_CLAN_TNMT_ENTERROOM_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        int clanId = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        _ = clanId;

        var room = context.Rooms.Find(roomNo);
        byte? slot = room?.TakeFreeSlot();

        if (room is null || slot is null || session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_CLAN_TNMT_ENTERROOM_ACK).WriteU8(0));
            return;
        }

        room.Members[slot.Value] = session;
        session.RoomNo = roomNo;

        // 1. 廣播給既有成員: sub_type==1
        var newMember = RoomHandlers.LoadGL_ENTERROOM_ACK_MemberData(context.Db, session);
        var joinNotice = new Packet(Opcode.GL_CLAN_TNMT_ENTERROOM_ACK).WriteU8(1);
        RoomHandlers.WriteGL_ENTERROOM_ACK_MemberNotice(joinNotice, session, slot.Value, newMember);
        await RoomManager.BroadcastAsync(room, joinNotice, except: session);

        // 2. 給進房者: sub_type==2
        var fullState = new Packet(Opcode.GL_CLAN_TNMT_ENTERROOM_ACK).WriteU8(2);
        // sub_57E9A0 case 2 房狀態頭 (同 114 case 2)
        fullState.WriteS32(room.RoomUid)
                 .WriteU8(room.MapId)
                 .WriteU8((byte)room.Members.Count)
                 .WriteU8(room.RoomNo)
                 .WriteU8(room.OpenSlotCount)
                 .WriteU16(room.MaxSlotMask)
                 .WriteU8(room.ModeIndex)
                 .WriteU8(room.TimeLimit)
                 .WriteU16(room.WinCount)
                 .WriteU8(room.ItemMode)
                 .WriteU8(0)                                    // skill_off
                 .WriteU16(0)
                 .WriteU8(0)
                 .WriteU8(0)
                 .WriteU8((byte)(string.IsNullOrEmpty(room.Password) ? 0 : 1))
                 .WriteU8(0)
                 .WriteU8(0);

        foreach (var (memberSlot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            RoomHandlers.WriteGL_ENTERROOM_ACK_MemberEntry(fullState, member, memberSlot, memberSlot == room.MasterSlot,
                RoomHandlers.LoadGL_ENTERROOM_ACK_MemberData(context.Db, member));
        }

        await session.SendAsync(fullState);
    }
}
