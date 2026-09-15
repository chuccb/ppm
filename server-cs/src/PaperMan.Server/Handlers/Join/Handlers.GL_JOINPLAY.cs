// =============================================================================
// GL_JOINPLAY_REQ (268) → GL_JOINPLAY_ACK (269)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class JoinHandlers
{
    // 268 GL_JOINPLAY_REQ (u8 room_no u8 flag) → 269 (sub_574B20, 逐欄定案):
    //   flag 0: 以玩家加入 (PLAY) → code 6: 回傳自身完整 snapshot (含 sub_885D00 語音塊)
    //   flag 1: 以觀戰加入 (OBSERVE) → code 7: 回傳全房快照 + 全成員條目 (含 sub_885D00 語音塊)
    //   無此房/未登入/加入失敗 → code 0 (回房單提示錯誤)
    private static async ValueTask GL_JOINPLAY_REQ(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        byte flag = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;

        if (session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
            return;
        }

        if (flag == 0)
        {
            // 玩家模式加入 (code 6: 自身 snapshot)
            byte? slot = room.TakeFreeSlot();
            if (slot is null)
            {
                await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
                return;
            }

            room.Members[slot.Value] = session;
            session.RoomNo = roomNo;

            var data = RoomHandlers.LoadGL_ENTERROOM_ACK_MemberData(context.Db, session);

            // 廣播給既有成員 (114 sub_type==1)
            var notice = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(1);
            RoomHandlers.WriteGL_ENTERROOM_ACK_MemberNotice(notice, session, slot.Value, data);
            await RoomManager.BroadcastAsync(room, notice, except: session);

            // 回給加入者: 269 code 6
            var ack = new Packet(Opcode.GL_JOINPLAY_ACK)
                .WriteU8(6)
                .WriteS32((int)session.UserId)
                .WriteU8(slot.Value)
                .WriteStr(session.Nickname);

            // 外觀/模型/自訂貼圖
            ack.WriteU8(data.CurChar?.SlotNo ?? (byte)0)
               .WriteU8(data.CurChar?.CharType ?? (byte)0);
            for (int i = 0; i < 12; i++)
            {
                ack.WriteU16(data.CurChar?.Equip[i] ?? (ushort)0);
            }

            ack.WriteU8(data.CurChar?.CharType ?? (byte)0)
               .WriteS16(0).WriteS16(0).WriteS16(0)
               .WriteS32(0).WriteS32(0).WriteS32(0).WriteStr("");

            // 4 武器組
            for (byte g = 0; g < 4; g++)
            {
                var wg = data.Groups.FirstOrDefault(x => x.GroupNo == g);
                ack.WriteU16(wg?.PrimaryOffset ?? (ushort)0);
                if (g != 3)
                {
                    ack.WriteU16(wg?.SecondaryOffset ?? (ushort)0)
                       .WriteU16(wg?.MeleeOffset ?? (ushort)0)
                       .WriteU16(wg?.ThrowOffset ?? (ushort)0);
                }
                if (wg is { PrimaryOffset: not 0 })
                {
                    foreach (var part in wg.Parts)
                    {
                        ack.WriteS32(part);
                    }
                }
            }

            ack.WriteU8(0);                                     // extra_flag
            foreach (var skill in data.Slots.Skill)
            {
                ack.WriteS32(skill);
            }

            ack.WriteU8(5); // unresolved n5: retain existing raw convention
            foreach (var puzzleItemId in data.Slots.NewSkillPuzzleIds)
            {
                ack.WriteS32(puzzleItemId);
            }

            RoomHandlers.WriteVoiceBlock(ack, data.Voice);       // sub_885D00 語音塊 (85B)
            await session.SendAsync(ack);
        }
        else
        {
            // 觀戰模式加入 (code 7: 全房 snapshot)
            var ack = new Packet(Opcode.GL_JOINPLAY_ACK)
                .WriteU8(7)
                .WriteS32(room.RoomUid)
                .WriteS32(0)                                    // elapsed_ms
                .WriteU8(room.MapId)
                .WriteU8((byte)room.Members.Count)
                .WriteU8(room.RoomNo)
                .WriteU8(room.ModeIndex)
                .WriteU16(room.WinCount)
                .WriteU8(room.OpenSlotCount)
                .WriteU8(room.TimeLimit)
                .WriteU16(0)                                    // round
                .WriteU8(room.ItemMode)
                .WriteU8(0)                                     // skill_off
                .WriteU16(0)
                .WriteU8(0).WriteU8(0)
                .WriteU8((byte)(string.IsNullOrEmpty(room.Password) ? 0 : 1))
                .WriteU8(0).WriteU8(0).WriteU8(0)
                .WriteU8(0);                                    // observer

            foreach (var (memberSlot, member) in room.Members.OrderBy(kv => kv.Key))
            {
                var data = RoomHandlers.LoadGL_ENTERROOM_ACK_MemberData(context.Db, member);
                bool isMaster = memberSlot == room.MasterSlot;

                ack.WriteS32((int)member.UserId)
                   .WriteU8(memberSlot)
                   .WriteStr(member.Nickname)
                   .WriteU8(isMaster ? (byte)1 : (byte)0)
                   .WriteU8(0)                                  // status
                   .WriteS32(0).WriteS32(0).WriteS32(0)         // custom_tex
                   .WriteU8(1)                                  // alive
                   .WriteU8(0);                                 // dead_flag

                // 外觀
                ack.WriteU8(data.CurChar?.SlotNo ?? (byte)0)
                   .WriteU8(data.CurChar?.CharType ?? (byte)0);
                for (int i = 0; i < 12; i++)
                {
                    ack.WriteU16(data.CurChar?.Equip[i] ?? (ushort)0);
                }

                ack.WriteU8(data.CurChar?.CharType ?? (byte)0)
                   .WriteS16(0).WriteS16(0).WriteS16(0)
                   .WriteS32(0).WriteS32(0).WriteS32(0).WriteStr("");

                // 4 武器組
                for (byte g = 0; g < 4; g++)
                {
                    var wg = data.Groups.FirstOrDefault(x => x.GroupNo == g);
                    ack.WriteU16(wg?.PrimaryOffset ?? (ushort)0);
                    if (g != 3)
                    {
                        ack.WriteU16(wg?.SecondaryOffset ?? (ushort)0)
                           .WriteU16(wg?.MeleeOffset ?? (ushort)0)
                           .WriteU16(wg?.ThrowOffset ?? (ushort)0);
                    }
                    if (wg is { PrimaryOffset: not 0 })
                    {
                        foreach (var part in wg.Parts)
                        {
                            ack.WriteS32(part);
                        }
                    }
                }

                ack.WriteU8(0);                                 // extra_flag

                if (room.ModeIndex == (byte)GameMode.TeamSoccer)     // TeamSoccer=12
                {
                    ack.WriteU8(0);
                }
                else if (room.ModeIndex == (byte)GameMode.OccupyRenewal)
                {
                    ack.WriteS32(0);
                }

                ack.WriteU8(0);                                 // active_weapon_flag

                foreach (var skill in data.Slots.Skill)
                {
                    ack.WriteS32(skill);
                }

                ack.WriteU8(5); // unresolved n5: retain existing raw convention
                foreach (var puzzleItemId in data.Slots.NewSkillPuzzleIds)
                {
                    ack.WriteS32(puzzleItemId);
                }

                RoomHandlers.WriteVoiceBlock(ack, data.Voice);   // sub_885D00 語音塊 (85B)
                ack.WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0); // in-game flags
            }

            // room state tail
            ack.WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0)
               .WriteS32(0)                                     // n0x3E8
               .WriteU8(0)
               .WriteU8(0).WriteS32(0).WriteS16(0).WriteS16(0).WriteU8(0);

            for (int i = 0; i < 16; i++)
            {
                ack.WriteS32(0);                                // 16×s32 scores
            }

            await session.SendAsync(ack);
        }
    }
}
