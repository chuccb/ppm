// =============================================================================
// Lobby opcode registration. Concrete flows are kept in snapshot, interaction,
// and player-configuration partials so this remains the small routing entry point.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_USERLIST_REQ, UserList);
        add(Opcode.GL_GAMEROOMINFO_REQ, RoomList);
        add(Opcode.GL_MYINFO_REQ, MyInfo);
        add(Opcode.GL_MYITEM_REQ, MyItems);
        add(Opcode.GM_CHECKNICK_REQ, CheckNick);
        add(Opcode.GM_CREATENICK_REQ, CreateNick);
        add(Opcode.GL_CHATTING_REQ, Chat);
        add(Opcode.GL_LOBBYIN_REQ, LobbyEnter);
        add(Opcode.GL_SHOPIN_REQ, ShopEnter);
        add(Opcode.GL_INVENIN_REQ, InventoryEnter);
        add(Opcode.GL_CLIENTINFO_REQ, ClientInfo);
        add(Opcode.GL_MYINFO_OPEN, MyInfoOpen);
        add(Opcode.GL_SHOUTCHAT_REQ, Shout);

        // 教學 / 等級限制 / Token / 通訊完成 / 換頻道
        add(Opcode.GL_TUTORIALINDEX_REQ, TutorialIndex);
        add(Opcode.GL_TUTORIAL_INDEX_SET_REQ, TutorialIndexSet);
        add(Opcode.GL_LEVEL_KILL_LIMIT_REQ, LevelKillLimit);
        add(Opcode.GL_BILLTOKEN_REQ, BillToken);
        add(Opcode.GL_RACKINGWEB_TOKEN_REQ, RankingWebToken);
        add(Opcode.GL_DATA_RECV_COMPLETED_REQ, DataRecvCompleted);
        add(Opcode.GL_CHANGECHANNEL_REQ, ChangeChannel);

        // 角色建立 / 角色槽 / 裝備更換 / 武器 / 技能 / 零件
        add(Opcode.GM_CREATECHAR_REQ, CreateChar);
        add(Opcode.GI_CHANGEDATA_REQ, ChangeData);
        add(Opcode.GI_CHANGEWP_REQ, ChangeWeapon);
        add(Opcode.GI_CHANGESLOT_REQ, ChangeSlot);
        add(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ, ChangeSkillSlot);
        add(Opcode.GL_WEAPONPARTS_EQUIP_CHANGE_REQ, ChangeWeaponParts);
    }
}
