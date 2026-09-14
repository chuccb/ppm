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
        add(Opcode.GL_CLAN_TNMT_ENTERROOM_REQ, ClanTournamentEnterRoom);
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
    private static async ValueTask Create(Session session, Packet packet, ServerContext context)
    {
        var name = packet.ReadStr();
        var slogan = packet.ReadStr();
        var intro = packet.ReadStr();
        int emblem = packet.Remaining >= 4 ? packet.ReadS32() : 0;    // s32 (廿四輪修正)
        _ = (slogan, intro);                                // schema 暫存於 notice 欄位外

        var result = session.UserId switch
        {
            0 => CreateResult.Failed,
            _ => context.Db.CreateClan(session.UserId, name, (byte)Math.Clamp(emblem, 0, 255)) switch
            {
                > 0 => CreateResult.Ok,
                _ => CreateResult.DuplicateName,
            },
        };

        var ack = new Packet(Opcode.GC_CLAN_CREATE_ACK).WriteS8((sbyte)result);
        if (result is CreateResult.Ok)
        {
            var info = context.Db.GetMyInfo(session.UserId);
            ack.WriteS32((int)(info?.Gp ?? 0));             // sub_54DD70 → EE8D18
        }

        await session.SendAsync(ack);
    }

    // 583 隧道: s32 sub_opcode + 子內容
    private static async ValueTask Tunnel(Session session, Packet packet, ServerContext context)
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

    // 764 GL_CLAN_TNMT_ENTERROOM_REQ (sub_57E8F0: u8 room_no, s32 clan_id)
    // → 765 GL_CLAN_TNMT_ENTERROOM_ACK (sub_57E9A0: 與 114 同構, 錦標賽版進房, 含 sub_885D00 語音塊)
    private static async ValueTask ClanTournamentEnterRoom(Session session, Packet packet, ServerContext context)
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
        var newMember = RoomHandlers.LoadMemberData(context.Db, session);
        var joinNotice = new Packet(Opcode.GL_CLAN_TNMT_ENTERROOM_ACK).WriteU8(1);
        RoomHandlers.WriteMemberNotice(joinNotice, session, slot.Value, newMember);
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
                 .WriteU8(room.Rule)
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
            RoomHandlers.WriteMemberEntry(fullState, member, memberSlot, memberSlot == room.MasterSlot,
                RoomHandlers.LoadMemberData(context.Db, member));
        }

        await session.SendAsync(fullState);
    }
}
