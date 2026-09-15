// =============================================================================
// GR_END_REQ (133) → GR_END_ACK (134)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 133 REQ 空 → 134 ACK (sub_562EA0 讀序): 回房重置
    //   u8 result(1=回房), u8 map(+130), u8(讀後丟棄), u8 room_no,
    //   u8 max_players(+129 冗餘, client 以 +110 popcount 重算),
    //   u16 max_slot_mask(+110, 回房恢復大廳), u8 mode(→sub_53FBB0),
    //   u8(+136), u16(+144), u8 flags(bit0→mode+4), u8(+146),
    //   u16(+148), u8(+150), u8 mode+12, u8 +109, u8 mode+13 —
    //   比 114 case-2 少 room_uid 前綴與 +185/+128/mode+14
    private static async ValueTask GR_END_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var ack = new Packet(Opcode.GR_END_ACK)
            .WriteU8(1)                                     // result: 回房
            .WriteU8(room.MapId)                            // +130 map (sub_540280)
            .WriteU8(0)                                     // client 讀後丟棄 (i_1)
            .WriteU8(roomNo)                                // room_no (client 定址房物件)
            .WriteU8(room.OpenSlotCount)                    // +129 max_players (client 以 +110 重算)
            .WriteU16(room.MaxSlotMask)                     // +110 上限槽位點陣 (回房恢復)
            .WriteU8(room.ModeIndex)                             // mode → sub_53FBB0
            .WriteU8(room.TimeLimit)                        // +136 時間 (173/174)
            .WriteU16(room.WinCount)                        // +144 勝場目標 (171/172)
            .WriteU8(room.ItemMode)                         // flags bit0→mode+4, bit1→mode+8
            .WriteU8(0)                                     // +146 (mode param, client 存而不讀)
            .WriteU16(room.KillCount)                       // +148 擊殺目標 (340/341)
            .WriteU8(0)                                     // +150 (mode param, client 存而不讀)
            .WriteBool(IsNativeTwoTeamMode(room.ModeIndex))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
            .WriteU8(0)                                     // +109 room_type_B (client 僅鏡像)
            .WriteBool(room.TeamShuffle);                   // mode+13 隊打散開關 (368/369)
        room.Playing = false;
        room.BattleState.EndMatch();                         // 同一把 state lock 內封閉並清掉本局 OCC
        await RoomManager.BroadcastAsync(room, ack);
    }
}
