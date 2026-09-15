// =============================================================================
// GL_GAMEROOMINFO_REQ (107) → GL_GAMEROOMINFO_ACK (108)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // ACK(108) sub_568CE0 (卅七輪逐欄定案):
    //   u8 mode (3=錦標賽樹 sub_580A80); 其他: u8 count, repeat{
    //     u8 room_no(<210), s8 state (state>=0 → 標題查 client 字串表 state+309;
    //     state<0 → str title), 之後 12 欄:
    //     u8 cur_players(+105), bool has_pass(+106), u8 max_players(+129 冗餘,
    //     client 以 +110 popcount 重算), u16 max_slot_mask(+110),
    //     u8 game_mode(→sub_53FBB0), bool room_type_A(+108), bool mode+12
    //     (是否隊伍房 sub_438990?1:0), bool room_type_B(+109),
    //     bool double_damage(+128), u8 map(+130), u8 mode_param_b(+4 道具),
    //     bool no_skill_bg(+185) }
    private static async ValueTask GL_GAMEROOMINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        var rooms = context.Rooms.All.Take(50).ToList();

        var ack = new Packet(Opcode.GL_GAMEROOMINFO_ACK)
            .WriteU8(0)                                     // mode 0 = 一般清單
            .WriteU8((byte)rooms.Count);

        foreach (var room in rooms)
        {
            ack.WriteU8(room.RoomNo)
               .WriteS8(-1)                                 // state<0 → 自訂標題 (str 版條目)
               .WriteStr(room.Title)
               .WriteU8((byte)room.Members.Count)           // +105 cur_players
               .WriteBool(room.Password is not null)        // +106 has_pass
               .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
               .WriteU16(room.MaxSlotMask)                  // +110 上限槽位點陣 (popcount = 最大人數)
               .WriteU8(room.ModeIndex)                          // game_mode → sub_53FBB0 (0..15)
               .WriteBool(false)                            // +108 room_type bit A (server 側未定)
               .WriteBool(IsNativeTwoTeamMode(room.ModeIndex))            // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
               .WriteBool(false)                            // +109 room_type bit B (server 側未定)
               .WriteBool(room.DoubleDamage)                // +128 double_damage (990/991)
               .WriteU8(room.MapId)                         // +130 map (sub_540280/540260; 122 亦寫此欄)
               .WriteU8((byte)(room.ItemMode & 1))          // mode+4 = item bit0 (sub_74F450; 175/176)
               .WriteBool(room.NoSkillBg);                  // +185 no_skill_bg (712/713)
        }

        await session.SendAsync(ack);
    }

    /// <summary>sub_438990 的 server 側對照: 兩隊制模式 (0/2/3/4/8/10/11/12/13)。</summary>
    private static bool IsNativeTwoTeamMode(byte modeIndex) => modeIndex is
        (byte)GameMode.TeamMatch
        or (byte)GameMode.DefuseBomb
        or (byte)GameMode.TeamSurvival
        or (byte)GameMode.Steal
        or (byte)GameMode.PulpnRoll
        or (byte)GameMode.Occupy
        or (byte)GameMode.AIMulti
        or (byte)GameMode.TeamSoccer
        or (byte)GameMode.OccupyRenewal;
}
