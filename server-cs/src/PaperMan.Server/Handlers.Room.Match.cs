// =============================================================================
// 房間對戰生命週期 handlers — 129/183/187/133
//
// 房間 state transition 與對應 S2C snapshot。固定長度 request 的 gate 保留在各 transition 旁。
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
    private static async ValueTask StartGame(Session session, Packet packet, ServerContext context)
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
            .WriteBool(IsTeamMode(room.Rule))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
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

    // 183 REQ 空 → 184 ACK (sub_563B00): u8 n2(1), u8 slot, u8 slot2, u8
    //   — 每位成員載入完成後廣播; 全員到齊由 client 觸發 187
    private static async ValueTask EndLoading(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        room.MarkLoaded(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ENDLOADING_ACK)
            .WriteU8(1)
            .WriteU8(slot)
            .WriteU8(slot)
            .WriteU8(0));
    }

    // 187 REQ 空 → 188 ACK (sub_563D60): u8 n2==1, u8 count, count×u8 slot
    //   — 開打廣播 (帶已載入成員名單)
    private static async ValueTask BeginBattle(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var loaded = room.LoadedSlots;
        var ack = new Packet(Opcode.GG_STARTGAME_ACK)
            .WriteU8(1)
            .WriteU8((byte)loaded.Count);
        foreach (var slot in loaded)
        {
            ack.WriteU8(slot);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

    // 133 REQ 空 → 134 ACK (sub_562EA0 讀序): 回房重置
    //   u8 result(1=回房), u8 map(+130), u8(讀後丟棄), u8 room_no,
    //   u8 max_players(+129 冗餘, client 以 +110 popcount 重算),
    //   u16 max_slot_mask(+110, 回房恢復大廳), u8 mode(→sub_53FBB0),
    //   u8(+136), u16(+144), u8 flags(bit0→mode+4), u8(+146),
    //   u16(+148), u8(+150), u8 mode+12, u8 +109, u8 mode+13 —
    //   比 114 case-2 少 room_uid 前綴與 +185/+128/mode+14
    private static async ValueTask EndGame(Session session, Packet packet, ServerContext context)
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
            .WriteU8(room.Rule)                             // mode → sub_53FBB0
            .WriteU8(room.TimeLimit)                        // +136 時間 (173/174)
            .WriteU16(room.WinCount)                        // +144 勝場目標 (171/172)
            .WriteU8(room.ItemMode)                         // flags bit0→mode+4, bit1→mode+8
            .WriteU8(0)                                     // +146 (mode param, client 存而不讀)
            .WriteU16(room.KillCount)                       // +148 擊殺目標 (340/341)
            .WriteU8(0)                                     // +150 (mode param, client 存而不讀)
            .WriteBool(IsTeamMode(room.Rule))               // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
            .WriteU8(0)                                     // +109 room_type_B (client 僅鏡像)
            .WriteBool(room.TeamShuffle);                   // mode+13 隊打散開關 (368/369)
        room.Playing = false;
        room.BattleState.EndMatch();                         // 同一把 state lock 內封閉並清掉本局 OCC
        await RoomManager.BroadcastAsync(room, ack);
    }
}
