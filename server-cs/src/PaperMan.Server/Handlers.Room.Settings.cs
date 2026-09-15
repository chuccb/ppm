// =============================================================================
// 房間設定 handlers — room state 的房主 gate
//
// 每個 handler 只修改一個 Room property 並廣播 reader-proven ACK；共用 membership/owner guard 在 Handlers.Room.cs。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 139 GG_EXITGAME_REQ (sub_560720): 空 payload — 玩家離開對戰回房
    // → 140 ACK (sub_563430): u8 n2==1, u8 slot — 單人退場廣播 (n2==2 是
    //   整房重置, 由 133/134 流程觸發, 此處不涉及)
    private static async ValueTask ExitGame(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return;
        }

        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_EXITGAME_ACK)
            .WriteU8(1)
            .WriteU8(slot));
    }

    // 167 GR_CHANGEUSER_REQ (sub_56F360): u16 slot_mask — 房主改開放槽位點陣
    // → 168 ACK (sub_56F410→sub_4325D0): u16 slot_mask 寫 room+110 並以
    //   sub_53FB10 popcount 重算 +129 (最大人數); 點陣可非連續 (踢人/關槽)
    private static async ValueTask ChangeUserSlots(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort mask = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.SlotMask = mask;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_CHANGEUSER_ACK).WriteU16(mask));
    }

    // 169 GR_RULECHANGE_REQ (sub_56F440): u8 mode(modeIndex) — 房主改遊戲模式
    // → 170 ACK (sub_56F4F0→sub_42FE50): u8 mode — client 以 sub_53FBB0 重建
    //   mode UI 並以 sub_426930(mode) 回推預設地圖寫 +130 (sub_540280)。
    //   server 同步鏡像: 有 map_StartIndex 條目的 mode 重設 MapId, 其餘
    //   (練習/教學/聊天/射擊館…無地圖目錄) 保留原圖 — 見 ModeDefaultMap。
    private static async ValueTask RuleChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte mode = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.Rule = mode;
        // client 端 sub_426930(mode) 會把 map 回推成該 mode 預設圖寫 +130;
        // server 鏡像: 有預設圖的 mode 重置, 其餘保留; 兩者皆再經 ResolveMap
        // 依 mode→bit 過濾 (防呆, 正常預設圖必合法故為 no-op)。
        byte oldMap = room.MapId;
        room.MapId = ResolveMap(
            ModeDefaultMap.TryGetValue(mode, out byte defaultMap) ? defaultMap : room.MapId,
            mode, context.Db);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_RULECHANGE_ACK).WriteU8(mode));
        // 例外: SOCCER 預設圖 98 是 TS 圖 (map_StartIndex 原廠 bug), ResolveMap
        // 會回退成 99 — client 卻仍照 98 寫 +130; 補發 122 把 client 拉回一致。
        if (room.MapId != oldMap)
        {
            await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
        }
    }

    // 171 GR_WINCHANGE_REQ (sub_56F520): u16 win_count — 房主改勝場目標
    // → 172 ACK (sub_56F5D0→sub_430720): u16 寫 room+144
    private static async ValueTask WinChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort win = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.WinCount = win;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_WINCHANGE_ACK).WriteU16(win));
    }

    // 173 GR_TIMECHANGE_REQ (sub_56F600): u8 time_idx — 房主改遊戲時間
    // → 174 ACK (sub_56F6B0→sub_430920): u8 寫 room+136
    private static async ValueTask TimeChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte time = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TimeLimit = time;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TIMECHANGE_ACK).WriteU8(time));
    }

    // 175 GR_ITEMCHANGE_REQ (sub_56F6E0): u8 item_mode(2bit) — 房主改道具
    // → 176 ACK (sub_56F790→sub_430D50): bit0→sub_74F450(mode+4), bit1→
    //   sub_74F430(mode+8) — 兩把武器的道具開關
    private static async ValueTask ItemChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte item = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.ItemMode = item;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_ITEMCHANGE_ACK).WriteU8(item));
    }

    // 177 GR_AUTOCHANGE_REQ (sub_56F8A0): s8 — client 端無呼叫者 (死碼),
    // 178 ACK 亦不在 sub_58B010 主 switch (走 vtable 前置轉發器)。僅吸收。
    private static ValueTask AutoChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        _ = packet.ReadS8();
        return ValueTask.CompletedTask;
    }

    // 340 GR_KILLCHANGE_REQ (sub_56F7C0): u16 kill_count — 與 171 同送 (sub_4306E0)
    // → 341 ACK (sub_56F870→sub_430F50): u16 寫 room+148
    private static async ValueTask KillChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        ushort kill = packet.ReadU16();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.KillCount = kill;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_KILLCHANGE_ACK).WriteU16(kill));
    }

    // 364 GR_BALANCECHANGE_REQ (sub_56FA30): u8 — 房主切換隊伍平衡
    // → 365 ACK (sub_56FAE0→sub_431160): u8 只寫 GAMEROOM_TEAMBALANCE UI
    //   (一般房 room+186 不上 wire; 錦標賽 ctor sub_53F9F0 才寫 +186)
    private static async ValueTask BalanceChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TeamBalance = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_BALANCECHANGE_ACK).WriteU8(on));
    }

    // 712 GR_NOSKILL_REQ (sub_56FB10): u8 — 房主切換 no-skill 背景
    // → 713 ACK (sub_56FBC0→sub_4312C0): u8 寫 room+185 (noskillbg)
    private static async ValueTask NoSkillChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.NoSkillBg = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_NOSKILL_ACK).WriteU8(on));
    }

    // 990 GR_DAMAGEROOM_REQ (sub_56F950): u8 — 房主切換 double damage
    // → 991 ACK (sub_56FA00→sub_430FD0): u8 寫 room+128 (double_damage)
    private static async ValueTask DamageRoomChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.DoubleDamage = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_DAMAGEROOM_ACK).WriteU8(on));
    }

    // 366 GR_LOCALROOM_REQ (sub_585FD0): u8 — 房主切換區域限定房
    //   (n2_0==3 錦標賽場景時 client 不送; 名稱表未註冊, 由
    //   GAMEROOM_LOCALROOM UI 字串補名, 同 990/991 之例)
    // → 367 ACK (sub_586090→sub_437B50): u8 — 寫 GAMEROOM_LOCALROOM 勾選
    private static async ValueTask LocalRoomChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.LocalRoom = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_LOCALROOM_ACK).WriteU8(on));
    }

    // 368 GR_TEAMSHUFFLECHANGE_REQ (sub_585CE0): u8 — 房主切換隊打散開關
    // → 369 ACK (sub_585D90→sub_4354B0): u8 — 寫 mode rule 物件 +13 並
    //   勾選 GAMEROOM_TEAMSHUFFLE
    private static async ValueTask TeamShuffleChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.TeamShuffle = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_TEAMSHUFFLECHANGE_ACK).WriteU8(on));
    }

    // 969 GR_SOCCER_REQ (sub_5860C0): u8 — 房主切換足球模式
    //   (名稱表未註冊, 由 GAMEROOM_SOCCER UI 字串補名)
    // → 970 ACK (sub_586180→sub_437D00): u8 — 寫 mode rule 物件 +14
    //   (sub_74F4D0) 並勾選 GAMEROOM_SOCCER
    private static async ValueTask SoccerChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte on = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.Soccer = on != 0;
        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_SOCCER_ACK).WriteU8(on));
    }

    // 894 GR_TEAMSHUFFLE_REQ (sub_585DC0): u8 room_no, u8 map — 房主執行
    //   隊打散 (map 為 client 目前地圖, server 不重驗)
    // → 895 ACK (sub_585E70→sub_435680): u8 status; ==1 → u16(讀後丟棄),
    //   u8 count, count×(u8 slot, s32 uid) — 全房依 uid 重排槽位。
    //   status 語意由 msgtableres.lang 解出 (sub_435680 各 case 播的
    //   sub_408080 訊息): 7=權限不足, 8=模式不支援, 6=不足3人,
    //   13=未分兩隊, 5=尚未全員 ready, 14=人數過多, 12=進行中不可,
    //   3/4/9/11=其他失敗 — 詳 docs/PACKETS.md §3.15b2。
    private static async ValueTask TeamShuffle(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        _ = packet.ReadU8();                                     // room_no (client 附自己房號)
        _ = packet.ReadU8();                                     // map (client 附目前地圖)

        if (!TryGetRoom(session, context, out var room, out var slot))
        {
            return;
        }

        byte status = 1;                                         // 成功
        if (!IsMaster(room, slot))
        {
            status = 7;                                          // 權限がないため…
        }
        else if (!IsTeamMode(room.Rule))
        {
            status = 8;                                          // 支援しないモードです
        }
        else if (room.Members.Count < 3)
        {
            status = 6;                                          // 3人以上のプレイヤーが必要
        }

        var ack = new Packet(Opcode.GR_TEAMSHUFFLE_ACK).WriteU8(status);
        if (status == 1)
        {
            var assignment = room.ShuffleSlots();
            ack.WriteU16(0)                                      // client 讀後丟棄
               .WriteU8((byte)assignment.Count);
            foreach (var (newSlot, member) in assignment)
            {
                ack.WriteU8(newSlot)
                   .WriteS32((int)member.UserId);
            }
        }

        await RoomManager.BroadcastAsync(room, ack);
    }
}
