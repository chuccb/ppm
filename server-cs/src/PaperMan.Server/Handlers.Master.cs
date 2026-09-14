// =============================================================================
// GM / MASTER 管理指令 handlers (docs/PACKETS.md §3.15d4):
//
// 支援 GM 全服公告 (275-278)、強制踢線/踢房 (279-284)、GM 標記 (285/286)、
// 全服在線統計 (287/288)、GM 查用戶資料 (289/290, 293/294)、房間管理 (394/395)、
// 活動加倍率設定 (402-405, 841-846)、禁言 (822-831)、用戶追蹤與瞬移 (883-886)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class MasterHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.MASTER_MEMO_REQ, Memo);
        add(Opcode.MASTER_MEMOALL_REQ, MemoAll);
        add(Opcode.MASTER_USERCUT_REQ, UserCut);
        add(Opcode.MASTER_USERCUT2_REQ, UserCut2);
        add(Opcode.MASTER_ROOMCUT_REQ, RoomCut);
        add(Opcode.MASTER_MSET_REQ, MSet);
        add(Opcode.MASTER_PRINTUSER_REQ, PrintUser);
        add(Opcode.MASTER_USERINFO_REQ, UserInfo);
        add(Opcode.MASTER_LISTCUT_REQ, ListCut);
        add(Opcode.MASTER_USERINFODB_REQ, UserInfoDb);
        add(Opcode.MASTER_ROOMINFO_REQ, RoomInfo);
        add(Opcode.MASTER_EVENTPAGE_REQ, EventPage);
        add(Opcode.MASTER_EVENTEXP_REQ, EventExp);
        add(Opcode.MASTER_KILLALL_REQ, KillAll);
        add(Opcode.MASTER_CHAT_BAN_REQ, ChatBan);
        add(Opcode.MASTER_USERLIST_REQ, UserList);
        add(Opcode.MASTER_CHAT_FORCE_BAN_REQ, ChatForceBan);
        add(Opcode.MASTER_SETALL_EVENTEXP_REQ, SetAllEventExp);
        add(Opcode.MASTER_SETALL_EVENTPAGE_REQ, SetAllEventPage);
        add(Opcode.MASTER_VIEWALL_EVENTSTATE_REQ, ViewAllEventState);
        add(Opcode.MASTER_FIND_USER_REQ, FindUser);
        add(Opcode.MASTER_PLAY_WITH_REQ, PlayWith);
    }

    // 275 MASTER_MEMO_REQ (sub_578830: wstr memo) → 276 ACK (sub_578920): wstr memo
    private static async ValueTask Memo(Session session, Packet packet, ServerContext context)
    {
        var memo = packet.ReadWStr();
        await session.SendAsync(new Packet(Opcode.MASTER_MEMO_ACK).WriteWStr(memo));
    }

    // 277 MASTER_MEMOALL_REQ (sub_5789D0: wstr memo) → 278 ACK (sub_578BC0): wstr memo (全服廣播)
    private static async ValueTask MemoAll(Session session, Packet packet, ServerContext context)
    {
        var memo = packet.ReadWStr();
        var ack = new Packet(Opcode.MASTER_MEMOALL_ACK).WriteWStr(memo);

        foreach (var s in context.Sessions.All)
        {
            if (s.Authenticated)
            {
                try
                {
                    await s.SendAsync(Packet.FromPayload(ack.Opcode, ack.Payload));
                }
                catch
                {
                    // 忽略個別斷線
                }
            }
        }
    }

    // 279 MASTER_USERCUT_REQ (sub_578E50: u8 mode, str nick) → 280 ACK (sub_578FB0): 空包/成功
    private static async ValueTask UserCut(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var nick = packet.ReadStr();
        var target = context.Sessions.Find(nick);
        if (target is not null)
        {
            target.Dispose();
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERCUT_ACK));
    }

    // 281 MASTER_USERCUT2_REQ (sub_578F00: s32 uid) → 282 ACK: 空包
    private static async ValueTask UserCut2(Session session, Packet packet, ServerContext context)
    {
        int uid = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var target = context.Sessions.All.FirstOrDefault(s => s.UserId == uid);
        if (target is not null)
        {
            target.Dispose();
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERCUT2_ACK));
    }

    // 283 MASTER_ROOMCUT_REQ (sub_578FF0: u8 room_no) → 284 ACK: 空包
    private static async ValueTask RoomCut(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var room = context.Rooms.Find(roomNo);
        if (room is not null)
        {
            var kickAck = new Packet(Opcode.GR_FORCEOUT_ACK).WriteU8(1).WriteU8(0);
            await RoomManager.BroadcastAsync(room, kickAck);
            context.Rooms.Remove(roomNo);
        }

        await session.SendAsync(new Packet(Opcode.MASTER_ROOMCUT_ACK));
    }

    // 285 MASTER_MSET_REQ (sub_579040: u8 flag) → 286 ACK (sub_578D20): u8 flag
    private static async ValueTask MSet(Session session, Packet packet, ServerContext context)
    {
        byte flag = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        await session.SendAsync(new Packet(Opcode.MASTER_MSET_ACK).WriteU8(flag));
    }

    // 287 MASTER_PRINTUSER_REQ (sub_579100, 空) → 288 ACK: s32 count
    private static async ValueTask PrintUser(Session session, Packet packet, ServerContext context)
    {
        int count = context.Sessions.All.Count(s => s.Authenticated);
        await session.SendAsync(new Packet(Opcode.MASTER_PRINTUSER_ACK).WriteS32(count));
    }

    // 289 MASTER_USERINFO_REQ (str nick) → 290 ACK (sub_579830: 完整 CClientData 快照)
    private static async ValueTask UserInfo(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);
        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.MASTER_USERINFO_ACK).WriteBool(false));
            return;
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERINFO_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            .WriteStr(info.Nickname));
    }

    // 291 MASTER_LISTCUT_REQ (str nick) → 292 ACK: 空包
    private static async ValueTask ListCut(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadStr();
        await session.SendAsync(new Packet(Opcode.MASTER_LISTCUT_ACK));
    }

    // 293 MASTER_USERINFODB_REQ (str nick) → 294 ACK (sub_57A540)
    private static async ValueTask UserInfoDb(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);
        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.MASTER_USERINFODB_ACK).WriteBool(false));
            return;
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERINFODB_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            .WriteStr(info.Nickname));
    }

    // 394 MASTER_ROOMINFO_REQ (sub_5790A0: u8 room_no)
    // → 395 ACK (sub_579160): u8 count, count×(u8 slot, str nick, str ip)
    private static async ValueTask RoomInfo(Session session, Packet packet, ServerContext context)
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

    // 402 MASTER_EVENTPAGE_REQ (sub_579450: f32 rate) → 403 ACK (sub_579500): f32 rate
    private static async ValueTask EventPage(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_EVENTPAGE_ACK).WriteF32(rate));
    }

    // 404 MASTER_EVENTEXP_REQ (sub_5795A0: f32 rate) → 405 ACK (sub_579650): f32 rate
    private static async ValueTask EventExp(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_EVENTEXP_ACK).WriteF32(rate));
    }

    // 416 MASTER_KILLALL_REQ (空) → 全服踢除
    private static ValueTask KillAll(Session session, Packet packet, ServerContext context)
    {
        foreach (var s in context.Sessions.All)
        {
            if (!ReferenceEquals(s, session))
            {
                s.Dispose();
            }
        }

        return ValueTask.CompletedTask;
    }

    // 822 MASTER_CHAT_BAN_REQ (sub_582770: u8 mode, u8 dur, str nick) → 823 ACK (sub_5827C0)
    private static async ValueTask ChatBan(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.ReadStr();
        await session.SendAsync(new Packet(Opcode.MASTER_CHAT_BAN_ACK));
    }

    // 824 MASTER_USERLIST_REQ (sub_582840: u8 mode, s32 page) → 825 MASTER_LOBBY_USERLIST_ACK (sub_582890)
    private static async ValueTask UserList(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.MASTER_LOBBY_USERLIST_ACK)
            .WriteU8(0);                                     // count = 0
        await session.SendAsync(ack);
    }

    // 830 MASTER_CHAT_FORCE_BAN_REQ (sub_582B90: u8 mode, str nick, s32 dur) → 823 MASTER_CHAT_BAN_ACK
    private static async ValueTask ChatForceBan(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        _ = packet.ReadStr();
        _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        await session.SendAsync(new Packet(Opcode.MASTER_CHAT_BAN_ACK));
    }

    // 841 MASTER_SETALL_EVENTEXP_REQ (f32 rate) → 842 ACK: f32 rate
    private static async ValueTask SetAllEventExp(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_SETALL_EVENTEXP_ACK).WriteF32(rate));
    }

    // 843 MASTER_SETALL_EVENTPAGE_REQ (f32 rate) → 844 ACK: f32 rate
    private static async ValueTask SetAllEventPage(Session session, Packet packet, ServerContext context)
    {
        float rate = packet.Remaining >= 4 ? packet.ReadF32() : 1.0f;
        await session.SendAsync(new Packet(Opcode.MASTER_SETALL_EVENTPAGE_ACK).WriteF32(rate));
    }

    // 845 MASTER_VIEWALL_EVENTSTATE_REQ (空) → 846 ACK (sub_584400): f32 exp, f32 page
    private static async ValueTask ViewAllEventState(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.MASTER_VIEWALL_EVENTSTATE_ACK)
            .WriteF32(1.0f)
            .WriteF32(1.0f);
        await session.SendAsync(ack);
    }

    // 883 MASTER_FIND_USER_REQ (sub_579B10: s32 uid)
    // → 884 ACK (sub_579BC0): u8 status(1=找到), s32 uid, str nick, u8 channel, u8 room_no
    private static async ValueTask FindUser(Session session, Packet packet, ServerContext context)
    {
        int uid = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        var target = context.Sessions.All.FirstOrDefault(s => s.UserId == uid);

        var ack = new Packet(Opcode.MASTER_FIND_USER_ACK);
        if (target is not null)
        {
            ack.WriteU8(1)                                  // status 1 = 找到
               .WriteS32(uid)
               .WriteStr(target.Nickname)
               .WriteU8(0)                                  // channel
               .WriteU8(target.RoomNo ?? 0);                // room_no
        }
        else
        {
            ack.WriteU8(0);                                 // status 0 = 找不到
        }

        await session.SendAsync(ack);
    }

    // 885 MASTER_PLAY_WITH_REQ (sub_579C70: s32 target_uid) → 886 ACK: u8 0
    private static async ValueTask PlayWith(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        await session.SendAsync(new Packet(Opcode.MASTER_PLAY_WITH_ACK).WriteU8(0));
    }
}
