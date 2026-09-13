// =============================================================================
// 好友 handlers — GL_FRIEND 家族 (九輪逐行讀畢, docs/PACKETS.md §3.15c):
//
//   429 ADD_REQ:  str nick → 430 ACK (sub_55AA90): u8 result, str nick
//                 result: 0=成功, 1..4 = 重複/不存在/滿/對方拒
//   431 DEL_REQ:  str nick → 432 ACK (sub_55AE10): u8 result(0/1/2), str nick
//   433 LIST_REQ: 空       → 434 ACK (sub_55AFC0): u16 x, str self,
//                 u8 count, count×{str nick, s32 status}
//   435 INFO_REQ: str nick → 436 ACK (sub_55B2C0): u8 count,
//                 count×{str nick, u8 online, [online: str where, u8 ch]}
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class FriendHandlers
{
    /// <summary>430 的 result 碼 (sub_55AA90 的 if-chain)。</summary>
    private enum AddResult : byte
    {
        Ok = 0,
        AlreadyFriend = 1,
        NotFound = 2,
        ListFull = 3,
        Refused = 4,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.GL_FRIEND_ADD_REQ, Add);
        add(Opcode.GL_FRIEND_DEL_REQ, Delete);
        add(Opcode.GL_FRIEND_LIST_REQ, List);
        add(Opcode.GL_FRIEND_INFO_REQ, Info);
    }

    private static async ValueTask Add(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();

        var result = s.UserId switch
        {
            0 => AddResult.Refused,
            _ => ctx.Db.AddFriend(s.UserId, nick) switch
            {
                Db.FriendAdd.Ok => AddResult.Ok,
                Db.FriendAdd.Duplicate => AddResult.AlreadyFriend,
                Db.FriendAdd.NotFound => AddResult.NotFound,
                _ => AddResult.ListFull,
            },
        };

        await s.SendAsync(new Packet(Opcode.GL_FRIEND_ADD_ACK)
            .WriteU8((byte)result)
            .WriteStr(nick));
    }

    private static async ValueTask Delete(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();
        bool ok = s.UserId != 0 && ctx.Db.DeleteFriend(s.UserId, nick);

        await s.SendAsync(new Packet(Opcode.GL_FRIEND_DEL_ACK)
            .WriteU8(ok ? (byte)0 : (byte)1)
            .WriteStr(nick));
    }

    // 434 (sub_55AFC0): u16 x, str self, u8 count, count×{str nick, s32 status}
    private static async ValueTask List(Session s, Packet p, ServerContext ctx)
    {
        var friends = s.UserId != 0
            ? ctx.Db.GetFriends(s.UserId)
            : [];

        var ack = new Packet(Opcode.GL_FRIEND_LIST_ACK)
            .WriteU16(0)
            .WriteStr(s.Nickname)
            .WriteU8((byte)Math.Min(friends.Count, 255));

        foreach (var (nick, status) in friends.Take(255))
        {
            ack.WriteStr(nick)
               .WriteS32(status);
        }

        await s.SendAsync(ack);
    }

    // 436 (sub_55B2C0): u8 count, count×{str nick, u8 online,
    //   [online==1: str where, u8 channel]}
    private static async ValueTask Info(Session s, Packet p, ServerContext ctx)
    {
        var nick = p.ReadStr();

        // 單機伺服器: 查詢對象一律回「離線」(online=0 → 不帶 where/ch)
        await s.SendAsync(new Packet(Opcode.GL_FRIEND_INFO_ACK)
            .WriteU8(1)
            .WriteStr(nick)
            .WriteU8(0));
    }
}
