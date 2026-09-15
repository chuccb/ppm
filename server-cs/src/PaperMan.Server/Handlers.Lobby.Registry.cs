// =============================================================================
// Lobby opcode registry. This is the sole non-opcode-family filename: it maps
// each official request token to the same verbatim handler method name.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_USERLIST_REQ, GL_USERLIST_REQ);
        add(Opcode.GL_GAMEROOMINFO_REQ, GL_GAMEROOMINFO_REQ);
        add(Opcode.GL_MYINFO_REQ, GL_MYINFO_REQ);
        add(Opcode.GL_MYITEM_REQ, GL_MYITEM_REQ);
        add(Opcode.GM_CHECKNICK_REQ, GM_CHECKNICK_REQ);
        add(Opcode.GM_CREATENICK_REQ, GM_CREATENICK_REQ);
        add(Opcode.GL_CHATTING_REQ, GL_CHATTING_REQ);
        add(Opcode.GL_LOBBYIN_REQ, GL_LOBBYIN_REQ);
        add(Opcode.GL_SHOPIN_REQ, GL_SHOPIN_REQ);
        add(Opcode.GL_INVENIN_REQ, GL_INVENIN_REQ);
        add(Opcode.GL_CLIENTINFO_REQ, GL_CLIENTINFO_REQ);
        add(Opcode.GL_MYINFO_OPEN, GL_MYINFO_OPEN);
        add(Opcode.GL_SHOUTCHAT_REQ, GL_SHOUTCHAT_REQ);

        // 教學 / 等級限制 / Token / 通訊完成 / 換頻道
        add(Opcode.GL_TUTORIALINDEX_REQ, GL_TUTORIALINDEX_REQ);
        add(Opcode.GL_TUTORIAL_INDEX_SET_REQ, GL_TUTORIAL_INDEX_SET_REQ);
        add(Opcode.GL_LEVEL_KILL_LIMIT_REQ, GL_LEVEL_KILL_LIMIT_REQ);
        add(Opcode.GL_BILLTOKEN_REQ, GL_BILLTOKEN_REQ);
        add(Opcode.GL_RACKINGWEB_TOKEN_REQ, GL_RACKINGWEB_TOKEN_REQ);
        add(Opcode.GL_DATA_RECV_COMPLETED_REQ, GL_DATA_RECV_COMPLETED_REQ);
        add(Opcode.GL_CHANGECHANNEL_REQ, GL_CHANGECHANNEL_REQ);

        // 角色建立 / 角色槽 / 裝備更換 / 武器 / 技能 / 零件
        add(Opcode.GM_CREATECHAR_REQ, GM_CREATECHAR_REQ);
        add(Opcode.GI_CHANGEDATA_REQ, GI_CHANGEDATA_REQ);
        add(Opcode.GI_CHANGEWP_REQ, GI_CHANGEWP_REQ);
        add(Opcode.GI_CHANGESLOT_REQ, GI_CHANGESLOT_REQ);
        add(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ, GI_CHANGE_SKILLITEMSLOT_REQ);
        add(Opcode.GL_WEAPONPARTS_EQUIP_CHANGE_REQ, GL_WEAPONPARTS_EQUIP_CHANGE_REQ);
    }
}
