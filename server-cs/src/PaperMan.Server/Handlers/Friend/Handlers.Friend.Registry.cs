// =============================================================================
// Friend opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class FriendHandlers
{
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
    //   439 CHAT_REQ (sub_55B510): s32 uid(dword_F2A684 頁籤/頻道 id),
    //                 str my_nick, str friend_nick, str message (≤180 才送)
    //             → 440 ACK (sub_55B660): u8 status, str nick1, str nick2,
    //                 [status==2: str comment] — 0=名稱不存在(0x1EF),
    //                 1=離線(0x1F0), 2=訊息(0x1D9 ←%s さんのコメント), 3=找不到(0x1D8)
    //   441 WHERE_REQ (sub_55B940): str nick
    //             → 442 ACK (sub_55B9F0): u8 status; ==1 → u8 where_type,
    //                 u8 channel, u8 room_no (11=教學 0x314, 9/10=大師/線上,
    //                 其他=大廳 0x21E); ==2/0 → 0x21D 找不到資訊
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.GL_FRIEND_ADD_REQ, GL_FRIEND_ADD_REQ);
        add(Opcode.GL_FRIEND_DEL_REQ, GL_FRIEND_DEL_REQ);
        add(Opcode.GL_FRIEND_LIST_REQ, GL_FRIEND_LIST_REQ);
        add(Opcode.GL_FRIEND_INFO_REQ, GL_FRIEND_INFO_REQ);
        add(Opcode.GL_MSG_ADD_REQ, GL_MSG_ADD_REQ);
        add(Opcode.GL_MSG_RECVLIST_REQ, GL_MSG_RECVLIST_REQ);
        add(Opcode.GL_MSG_DEL_REQ, GL_MSG_DEL_REQ);
        add(Opcode.GL_MSG_READ_REQ, GL_MSG_READ_REQ);
        add(Opcode.GL_NEW_MSG_COUNT_REQ, GL_NEW_MSG_COUNT_REQ);
        add(Opcode.GL_FRIEND_CHAT_REQ, GL_FRIEND_CHAT_REQ);
        add(Opcode.GL_FRIEND_WHERE_REQ, GL_FRIEND_WHERE_REQ);
    }
}
