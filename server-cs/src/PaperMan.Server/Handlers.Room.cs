// =============================================================================
// 房間 handlers — 111/112 建房, 113/114 進房, 123/124 離房 (廿八輪)。
//
// 佈局出自反編譯 (docs/PACKETS.md §3.15 + 廿八輪逐欄定案):
//   111 REQ: u8 map, s8 has_pass, str title, [str pass],
//            u8 rule(modeIndex), u8 max, u8 x, u8 y
//   112 ACK (sub_56A7B0): u8 err, u8 room_no(<210), u16, s32 room_uid,
//            u8, s8 obs — err==0 → client 自任房主, 狀態切 10
//   113 REQ: u8 room_no
//   114 ACK sub_type==1: 單人進房通知 (s32 uid, u8 slot, str nick +
//            CClientData 嵌入 — 簡版送 0 塊)
//   124 GR_LEAVE_ACK: u8 result; ≠0 → u8 slot (成員移除廣播)
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class RoomHandlers
{
    /// <summary>112 的 err 碼 (sub_56A7B0: err!=0 → 顯示失敗訊息)。</summary>
    private enum MakeRoomError : byte
    {
        Ok = 0,
        Full = 1,                                           // 210 房全滿
        BadParams = 2,
    }

    public static void Register(Registrar add)
    {
        add(Opcode.GL_MAKEROOM_REQ, MakeRoom);
        add(Opcode.GL_ENTERROOM_REQ, EnterRoom);
        add(Opcode.GR_LEAVE_REQ, LeaveRoom);
        add(Opcode.GR_CHATTING_REQ, RoomChat);
        add(Opcode.GR_MAPCHANGE_REQ, MapChange);
        add(Opcode.GR_READY_REQ, Ready);
        add(Opcode.GR_CHANGESLOT_REQ, ChangeSlot);
        add(Opcode.GL_ENTERROOMPASS_REQ, EnterRoomPass);
        add(Opcode.GR_START_REQ, StartGame);
        add(Opcode.GR_ENDLOADING_REQ, EndLoading);
        add(Opcode.GG_STARTGAME_REQ, BeginBattle);
        add(Opcode.GR_END_REQ, EndGame);
    }

    // 129 REQ: u8 n125 → 130 ACK (sub_562870 讀序, 廿九輪逐變數):
    //   u8 result(1=開戰), s8, s32 elapsed_ms(新局=0), u8 slot, u8, u8,
    //   u16, u8, u8 host_slot, u16, u8 flags, s8×3, u8, s8, 16×s32
    private static async ValueTask StartGame(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                     // n125 (倒數參數)

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var masterSlot = room.MasterSlot;
        var ack = new Packet(Opcode.GR_START_ACK)
            .WriteU8(1)                                     // result: 開戰
            .WriteS8(0)
            .WriteS32(0)                                    // elapsed_ms (新局)
            .WriteU8(masterSlot)                            // slot
            .WriteU8(room.MapId)                            // +105
            .WriteU8(room.Rule)                             // +129
            .WriteU16(room.WinCount)
            .WriteU8(room.MaxPlayers)
            .WriteU8(masterSlot)                            // host
            .WriteU16(0)                                    // +144
            .WriteU8(0)                                     // flags (bit0/1)
            .WriteS8(0).WriteS8(0).WriteS8(0)
            .WriteU8(0)
            .WriteS8(0);
        for (int i = 0; i < 16; i++)
        {
            ack.WriteS32(0);                                // per-slot 值
        }

        room.ResetLoading();
        await RoomManager.BroadcastAsync(room, ack);
    }

    // 183 REQ 空 → 184 ACK (sub_563B00): u8 n2(1), u8 slot, u8 slot2, u8
    //   — 每位成員載入完成後廣播; 全員到齊由 client 觸發 187
    private static async ValueTask EndLoading(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
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

    // 133 REQ 空 → 134 ACK (sub_562EA0, 與 130 鏡像): 回房重置
    private static async ValueTask EndGame(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        var ack = new Packet(Opcode.GR_END_ACK)
            .WriteU8(1)
            .WriteU8(0)                                     // count
            .WriteU8(slot)
            .WriteU8(room.MapId)
            .WriteU8(room.Rule)
            .WriteU16(room.WinCount)
            .WriteU8(room.MaxPlayers)
            .WriteU8(room.MasterSlot)
            .WriteU16(0)
            .WriteU8(0)
            .WriteU16(0)
            .WriteS8(0).WriteS8(0).WriteS8(0).WriteS8(0)
            .WriteU8(0)
            .WriteU8(0);
        await RoomManager.BroadcastAsync(room, ack);
    }

    // 127 REQ 空 → 128 ACK (sub_5626D0): u8 ready_flag, u8 slot —
    // ready 狀態翻轉廣播 (server 維護 per-slot ready 集合)
    private static async ValueTask Ready(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        bool nowReady = room.ToggleReady(slot);

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_READY_ACK)
            .WriteU8(nowReady ? (byte)1 : (byte)0)
            .WriteU8(slot));
    }

    // 135 REQ: u8 n254, u8 target_slot → 136 ACK: u8 ok + from/to 廣播
    private static async ValueTask ChangeSlot(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                     // n254 (模式參數)
        byte target = packet.ReadU8();

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room
            || target >= 16 || room.Members.ContainsKey(target))
        {
            await session.SendAsync(new Packet(Opcode.GR_CHANGESLOT_ACK).WriteU8(0));
            return;
        }

        var from = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        room.Members.TryRemove(from, out _);
        room.Members[target] = s;

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GR_CHANGESLOT_ACK)
            .WriteU8(1)
            .WriteU8(from)
            .WriteU8(target));
    }

    // 216 REQ: u8 room_no, str pass → 217 ACK: u8 result → 成功後 client 送 113
    private static async ValueTask EnterRoomPass(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        var pass = packet.ReadStr();
        var room = context.Rooms.Find(roomNo);

        bool ok = room is not null
            && (room.Password is null || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_ENTERROOMPASS_ACK)
            .WriteU8(ok ? (byte)1 : (byte)0));
    }

    // 125 REQ (與 119 同構) → 126 ACK (sub_56EA80):
    //   s32 custom_tex, u8 slot, wstr message — 房內廣播
    private static async ValueTask RoomChat(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        // 廿八輪自動表: 125 = s32 tex, u8 slot, wstr msg (次變體 str)
        int tex = packet.ReadS32();
        _ = packet.ReadU8();                                     // client 附 slot (以 server 記錄為準)

        var message = packet.Remaining >= 2 && packet.Remaining % 2 == 0
            ? packet.ReadWStr()
            : packet.ReadStr();

        if (message.Length == 0)
        {
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        var notice = new Packet(Opcode.GR_CHATTING_ACK)
            .WriteS32(tex)
            .WriteU8(slot)
            .WriteWStr(message);
        await RoomManager.BroadcastAsync(room, notice);
    }

    // 121 REQ: u8 map → 122 ACK (sub_56E530): u8 map — 房主換圖廣播
    private static async ValueTask MapChange(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            return;
        }

        byte mapId = packet.ReadU8();
        room.MapId = mapId;

        await RoomManager.BroadcastAsync(room,
            new Packet(Opcode.GR_MAPCHANGE_ACK).WriteU8(mapId));
    }

    // 111 → 112 (+108 更新大廳清單由 client 重拉)
    private static async ValueTask MakeRoom(Session session, Packet packet, ServerContext context)
    {
        byte mapId = packet.ReadU8();
        sbyte hasPass = packet.ReadS8();
        var title = packet.ReadStr();
        var pass = hasPass != 0 ? packet.ReadStr() : null;
        byte rule = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;
        byte max = packet.Remaining > 0 ? packet.ReadU8() : (byte)16;

        var room = session.UserId != 0
            ? context.Rooms.Create(s, mapId, title, pass, rule, max)
            : null;

        var err = room is null ? MakeRoomError.Full : MakeRoomError.Ok;
        var ack = new Packet(Opcode.GL_MAKEROOM_ACK).WriteU8((byte)err);

        if (room is not null)
        {
            session.RoomNo = room.RoomNo;
            ack.WriteU8(room.RoomNo)
               .WriteU16(0)                                 // v52 (保留)
               .WriteS32(room.RoomUid)                      // v57 → dword_F2A65C
               .WriteU8(0)                                  // v50
               .WriteS8(0);                                 // v53 obs flag
        }

        await session.SendAsync(ack);
    }

    // 113 → 114 (sub_type==1 單人通知) + 房內廣播
    private static async ValueTask EnterRoom(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        var room = context.Rooms.Find(roomNo);
        byte? slot = room?.TakeFreeSlot();

        if (room is null || slot is null || session.UserId == 0)
        {
            // sub_type==0 = 失敗回大廳
            await session.SendAsync(new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(0));
            return;
        }

        room.Members[slot.Value] = s;
        session.RoomNo = roomNo;

        // 廣播單人進房通知給房內其他人 (sub_type==1)
        var notice = new Packet(Opcode.GL_ENTERROOM_ACK)
            .WriteU8(1)
            .WriteS32((int)session.UserId)
            .WriteU8(slot.Value)
            .WriteStr(session.Nickname);
        await RoomManager.BroadcastAsync(room, notice, except: s);

        // 給進房者自己也發 sub_type==1 (client 依 uid 判斷是否本人)
        await session.SendAsync(Packet.FromPayload(notice.Opcode, notice.Payload));
    }

    // 123 GR_LEAVE_REQ → 124 ACK (u8 result + u8 slot 廣播)
    private static async ValueTask LeaveRoom(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } room)
        {
            await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
            return;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        room.Members.TryRemove(slot, out _);
        session.RoomNo = null;

        if (room.Members.IsEmpty)
        {
            context.Rooms.Remove(roomNo);                       // 空房回收
        }
        else
        {
            var notice = new Packet(Opcode.GR_LEAVE_ACK)
                .WriteU8(1)
                .WriteU8(slot);
            await RoomManager.BroadcastAsync(room, notice);
        }

        await session.SendAsync(new Packet(Opcode.GR_LEAVE_ACK).WriteU8(0));
    }
}
