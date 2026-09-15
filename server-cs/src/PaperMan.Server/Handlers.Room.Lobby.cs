// =============================================================================
// 房間大廳互動 handlers — ready、slot、密碼、call、chat、map
//
// variable string grammar 留在各 handler；固定形狀在 mutation 前拒絕 truncated 或 trailing payload。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    // 127 REQ 空 → 128 ACK (sub_5626D0): u8 ready_flag, u8 slot —
    // ready 狀態翻轉廣播 (server 維護 per-slot ready 集合)
    private static async ValueTask Ready(Session session, Packet packet, ServerContext context)
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
        bool nowReady = room.ToggleReady(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_READY_ACK)
            .WriteU8(nowReady ? (byte)1 : (byte)0)
            .WriteU8(slot));
    }

    // 135 REQ (sub_56EE90): u8 n254, u8 target_slot → 136 ACK (sub_56EF40):
    //   u8 mode; mode!=0 → 僅失敗提示音 (client 不再讀 body);
    //   mode==0 → u8 from_slot, u8 new_slot, s32(保留, 讀後丟棄),
    //             s32 mover_uid, u8 count, count×(u8 slot, s32 uid)
    //   全房依 uid 對位重建 slot 佈局 (更新 client dword_F6DCF4)。
    private static async ValueTask ChangeSlot(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return;
        }

        _ = packet.ReadU8();                                     // n254 (模式參數)
        byte target = packet.ReadU8();

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room
            || target >= 16 || room.Members.ContainsKey(target))
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        var pair = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session));
        if (pair.Value is null)
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(1));
            return;
        }

        byte from = pair.Key;
        room.Members.TryRemove(from, out _);
        room.Members[target] = session;
        if (from == room.MasterSlot)
        {
            room.MasterSlot = target;
        }

        // mode==0: 全房重建佈局 (mover 本人以 mover_uid + new_slot 更新自己)
        var ack = new Packet(Opcode.GR_CHANGESLOT_ACK)
            .WriteU8(0)                                     // mode: 完整重建
            .WriteU8(from)                                  // 舊 slot (client 讀後丟棄)
            .WriteU8(target)                                // 新 slot (本人 slot 更新判準)
            .WriteS32(0)                                    // 保留 (client 讀後丟棄)
            .WriteS32((int)session.UserId)                  // mover_uid
            .WriteU8((byte)room.Members.Count);
        foreach (var (slot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            ack.WriteU8(slot)
               .WriteS32((int)member.UserId);
        }

        await RoomManager.BroadcastAsync(room, ack);
    }

    // 216 REQ: u8 room_no, str pass → 217 ACK: u8 result → 成功後 client 送 113
    private static async ValueTask EnterRoomPass(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 2 || packet.Payload[^1] != 0)
        {
            return;
        }

        byte roomNo = packet.ReadU8();
        string pass = packet.ReadStr();
        if (packet.Remaining != 0)
        {
            return;
        }

        var room = context.Rooms.Find(roomNo);

        bool ok = room is not null
            && (room.Password is null || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_ENTERROOMPASS_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0));
    }

    // 191 REQ (sub_56FD60): str nick — 呼叫指定玩家 (房內點名)。
    // → 192 ACK (sub_56FE10): 僅當接收者房狀態==2 (在房內) 才讀 body,
    //   u8 caller_slot + str caller_nick → sub_406DB0 彈「呼叫」視窗;
    //   否則連 body 都不讀 (sub_5376F0(byte_EE8968)=+24 非 2 即返回)。
    //   server 側: 雙方需同房才送 body (離線/異房一律靜默 — 192 無錯誤碼)。
    private static async ValueTask CallUser(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining < 1 || packet.Payload[^1] != 0)
        {
            return;
        }

        string targetNick = packet.ReadStr();
        if (packet.Remaining != 0 || targetNick.Length == 0)
        {
            return;
        }

        var target = context.Sessions.Find(targetNick);
        if (target is null || ReferenceEquals(target, session))
        {
            return;
        }

        // 雙方需同房 (sub_56FE10 依接收者房狀態==2 才讀 body)
        if (!TryGetRoom(session, context, out _, out var callerSlot)
            || target.RoomNo != session.RoomNo)
        {
            return;
        }

        await target.SendAsync(new Packet(Opcode.GR_CALLUSER_ACK)
            .WriteU8(callerSlot)                            // 呼叫者 slot
            .WriteStr(session.Nickname));                   // 呼叫者暱稱
    }

    // 125 REQ (sub_56E860 wstr 版; sub_56E6C0 str 版為死碼 — 無呼叫者):
    //   s32 uid(server 回 126 時 client 讀後丟棄), u8 slot, wstr message
    // → 126 ACK (sub_56EA80): s32 uid(丟棄), u8 slot, wstr message —
    //   以 slot 定址顯示, 全房廣播
    private static async ValueTask RoomChat(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        if (packet.Remaining < 7)
        {
            return;
        }

        int uid = packet.ReadS32();
        _ = packet.ReadU8();                                     // client 附 slot (以 server 記錄為準)

        // 唯一可達的 writer 是 UTF-16LE。完整 payload 必須正好以其雙 NUL
        // 收尾，否則不讓 malformed/trailing bytes 進入 room relay。
        if (packet.Remaining < 2
            || packet.Remaining % 2 != 0
            || packet.Payload[^2] != 0
            || packet.Payload[^1] != 0)
        {
            return;
        }

        string message = packet.ReadWStr();
        if (packet.Remaining != 0 || message.Length == 0)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, session)).Key;
        var notice = new Packet(Opcode.GR_CHATTING_ACK)
            .WriteS32(uid)
            .WriteU8(slot)
            .WriteWStr(message);
        await RoomManager.BroadcastAsync(room, notice);
    }

    // 121 REQ (sub_56E480): u8 map → 122 ACK (sub_56E530): u8 map —
    // 房主換圖廣播; sub_42FC50 以 sub_540280 寫 room+130 (map)
    private static async ValueTask MapChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 1)
        {
            return;
        }

        byte mapId = packet.ReadU8();
        if (!TryGetRoom(session, context, out var room, out var slot) || !IsMaster(room, slot))
        {
            return;
        }

        room.MapId = ResolveMap(mapId, room.Rule, context.Db);  // 121 依 mode→bit 過濾

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(room.MapId));
    }

}
