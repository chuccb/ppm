// =============================================================================
// GR_START_REQ (129) → GR_START_ACK (130)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 129 REQ: u8 n125 → 130 ACK (sub_562870 讀序):
    //   u8 result(1=開戰), u8 隊旗(→mode+14), s32 elapsed_ms(新局=0),
    //   u8 room_no, u8 cur_players(+105), u8 max_players(+129 冗餘,
    //   client 以 +110 popcount 重算), u16 max_slot_mask(+110),
    //   u8 map(+130), u8 mode(→sub_53FBB0), u16 (+144), u8 flags(bit0→mode+4),
    //   u8 mode+12, u8 +109, u8 mode+13, u8 +185, u8 +128,
    //   16×s32 (per-slot → dword_F6DD1C)
    //   — room_no 為 client 以 sub_407E80 定址房物件的索引
    private static async ValueTask GR_START_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        _ = packet.ReadU8();                                     // n125 (倒數參數)

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var ack = new Packet(Opcode.GR_START_ACK)
            .WriteU8(1)                                     // result: 開戰
            .WriteBool(room.Soccer)                         // mode+14 隊旗 (sub_74F4D0; 969/970)
            .WriteS32(0)                                    // elapsed_ms 基準 (新局=0; sub_537670 存 timeGetTime()-x)
            .WriteU8(roomNo)                                // room_no (client 定址房物件)
            .WriteU8((byte)room.Members.Count)              // +105 cur_players
            .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣
            .WriteU8(room.MapId)                            // +130 map (sub_540280)
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU16(room.WinCount)                        // +144 勝場目標 (171/172)
            .WriteU8(room.ItemMode)                         // flags bit0→mode+4, bit1→mode+8 (175/176)
            .WriteBool(IsNativeTwoTeamMode(room.Rule))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
            .WriteU8(0)                                     // +109 room_type_B (client 僅鏡像)
            .WriteBool(room.TeamShuffle)                    // mode+13 隊打散開關 (368/369)
            .WriteBool(room.NoSkillBg)                      // +185 noskillbg (712/713)
            .WriteBool(room.DoubleDamage);                  // +128 double_damage (990/991)
        for (int i = 0; i < 16; i++)
        {
            ack.WriteS32(0);                                // per-slot 值
        }

        room.Playing = true;
        room.ResetLoading();
        room.BattleState.BeginMatch();                       // 同一把 state lock 內開始新局並清除前局
        await RoomManager.BroadcastAsync(room, ack);
    }

}
