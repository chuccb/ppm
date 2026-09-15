// =============================================================================
// MASTER opcode registry — binding only; no packet implementation.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class MasterHandlers
{
    // =============================================================================
    // GM / MASTER 管理指令 handlers (docs/PACKETS.md §3.15d4):
    //
    // 支援 GM 全服公告 (275-278)、強制踢線/踢房 (279-284)、GM 標記 (285/286)、
    // 全服在線統計 (287/288)、GM 查用戶資料 (289/290, 293/294)、房間管理 (394/395)、
    // 活動加倍率設定 (402-405, 841-846)、禁言 (822-831)、用戶追蹤與瞬移 (883-886)。
    // =============================================================================
    public static void Register(Registrar add)
    {
        add(Opcode.MASTER_MEMO_REQ, MASTER_MEMO_REQ);
        add(Opcode.MASTER_MEMOALL_REQ, MASTER_MEMOALL_REQ);
        add(Opcode.MASTER_USERCUT_REQ, MASTER_USERCUT_REQ);
        add(Opcode.MASTER_USERCUT2_REQ, MASTER_USERCUT2_REQ);
        add(Opcode.MASTER_ROOMCUT_REQ, MASTER_ROOMCUT_REQ);
        add(Opcode.MASTER_MSET_REQ, MASTER_MSET_REQ);
        add(Opcode.MASTER_PRINTUSER_REQ, MASTER_PRINTUSER_REQ);
        add(Opcode.MASTER_USERINFO_REQ, MASTER_USERINFO_REQ);
        add(Opcode.MASTER_LISTCUT_REQ, MASTER_LISTCUT_REQ);
        add(Opcode.MASTER_USERINFODB_REQ, MASTER_USERINFODB_REQ);
        add(Opcode.MASTER_ROOMINFO_REQ, MASTER_ROOMINFO_REQ);
        add(Opcode.MASTER_EVENTPAGE_REQ, MASTER_EVENTPAGE_REQ);
        add(Opcode.MASTER_EVENTEXP_REQ, MASTER_EVENTEXP_REQ);
        add(Opcode.MASTER_KILLALL_REQ, MASTER_KILLALL_REQ);
        add(Opcode.MASTER_CHAT_BAN_REQ, MASTER_CHAT_BAN_REQ);
        add(Opcode.MASTER_USERLIST_REQ, MASTER_USERLIST_REQ);
        add(Opcode.MASTER_CHAT_FORCE_BAN_REQ, MASTER_CHAT_FORCE_BAN_REQ);
        add(Opcode.MASTER_SETALL_EVENTEXP_REQ, MASTER_SETALL_EVENTEXP_REQ);
        add(Opcode.MASTER_SETALL_EVENTPAGE_REQ, MASTER_SETALL_EVENTPAGE_REQ);
        add(Opcode.MASTER_VIEWALL_EVENTSTATE_REQ, MASTER_VIEWALL_EVENTSTATE_REQ);
        add(Opcode.MASTER_FIND_USER_REQ, MASTER_FIND_USER_REQ);
        add(Opcode.MASTER_PLAY_WITH_REQ, MASTER_PLAY_WITH_REQ);
    }
}
