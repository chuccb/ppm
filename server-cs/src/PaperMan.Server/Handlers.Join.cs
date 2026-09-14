// =============================================================================
// GL_JOIN 簇 (260-269) — 從房單 (108) 選房進房/加入進行中遊戲的流程。
//
// 與 113 GL_ENTERROOM (快速進房/受邀進房) 的差異: GL_JOIN 由房單 UI 驅動,
// 含密碼關卡與「加入進行中遊戲」的分支。client 端流程 (逐函數反編譯):
//
//   260 GL_JOIN_REQ (sub_56D9C0, u8 room_no):
//       房單點「入室」→ sub_4498C0 顯示「ルーム入室中」(0x8E) 後送出。
//   261 GL_JOIN_ACK (sub_56DA70 → sub_449920, u8 code):
//       0 =「データ読み込み中」(0x3E, 稍候); 1 =「ゲーム終了中」(0x43);
//       2 = 可進房 → client 自動發 264; 3 =「参加できるゲームルームが
//       ありません」(0x32C)。code==2 且房在狀態 11/可觀戰時另發 266。
//   262 GL_JOINPASS_REQ (sub_56B230, u8 room_no str pass):
//       密碼房: 房單點入室先送密碼 (sub_449810)。
//   263 GL_JOINPASS_ACK (sub_56B2E0 → sub_449860, u8 code):
//       0 = 密碼錯 (0x92「ルーム入室失敗」); ≠0 = 通過 → 轉發 260。
//   264 GL_JOININFO_REQ (sub_5744A0, u8 room_no):
//       索取房資訊 (sub_5156E0, 記住 room_no 與密碼)。
//   265 GL_JOININFO_ACK (sub_574550): 房資訊 + 玩家名單 (佈局見下)。
//   266 GL_JOINGAME_REQ (sub_574910, u8 room_no u8 觀戰旗標):
//       房 UI 的 PLAY(0)/OBSERVE(1) 鈕 (byte_1D0CFE6) →「ゲーム参加要請中」
//       (0x45)。僅房在進行中才用得到。
//   267 GL_JOINGAME_ACK (sub_5749E0 → sub_516900, u8 code u8 flag):
//       code 0/2/3→狀態1; 1/4→狀態2(存 flag); 5→狀態5 — 全部為「接受」。
//   268 GL_JOINPLAY_REQ (sub_574A60, u8 room_no u8 觀戰旗標):
//       CLobbyJoinGame 畫面送出 (sub_43C730)。
//   269 GL_JOINPLAY_ACK (sub_574B20, u8 code):
//       6 = 以玩家身份加入 (回傳自身完整狀態); 7 = 觀戰加入 (回傳全房
//       + 全玩家狀態); 0/1/2/3/4/5/8/9 = 失敗回房單 (sub_406F20)。
//
// 本服僅建模「大廳房」(無進行中遊戲狀態), 故:
//   260/262/264 → 261/263/265 為「房單進房」完整落地;
//   266 → 267 依觀戰旗標回 code (大廳房 client 不會發, 保底接受);
//   268 → 269 回 code 0 (無進行中遊戲可加, client 回房單) — 269 的
//   code 6/7 成功態需遊戲狀態機, 留待後續 (不硬編未確認欄位)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class JoinHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_JOIN_REQ, Join);
        add(Opcode.GL_JOINPASS_REQ, JoinPass);
        add(Opcode.GL_JOININFO_REQ, JoinInfo);
        add(Opcode.GL_JOINGAME_REQ, JoinGame);
        add(Opcode.GL_JOINPLAY_REQ, JoinPlay);
    }

    /// <summary>261 的 code (sub_449920 的 switch)。</summary>
    private enum JoinAck : byte
    {
        Loading = 0,            // 0x3E データ読み込み中 (稍候重試)
        Ending = 1,             // 0x43 ゲーム終了中 (不可加入, 稍後再試)
        Ok = 2,                 // 可進房 → client 自動發 264
        NoRoom = 3,             // 0x32C 参加できるゲームルームがありません
    }

    // 260 GL_JOIN_REQ (u8 room_no) → 261 (u8 code)。
    // 進房後以 114 sub_type==1 通知既有成員 (與 113 進房同款廣播),
    // 進房者本身由後續 264→265 取得完整房資訊。
    private static async ValueTask Join(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();

        if (session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)JoinAck.NoRoom));
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)JoinAck.NoRoom));
            return;
        }

        // 已在同房 → 冪等回 OK; 在別房 → 先離房再進。
        if (session.RoomNo == roomNo)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)JoinAck.Ok));
            return;
        }

        if (session.RoomNo is { } prevNo
            && context.Rooms.Find(prevNo) is { } prev
            && prev.Members.Values.Any(m => ReferenceEquals(m, session)))
        {
            await context.Rooms.RemoveMemberAsync(prev, session);
        }

        byte? slot = room.TakeFreeSlot();
        if (slot is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)JoinAck.Ending));
            return;
        }

        room.Members[slot.Value] = session;
        session.RoomNo = roomNo;

        var newMember = RoomHandlers.LoadMemberData(context.Db, session);
        var notice = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(1);
        RoomHandlers.WriteMemberNotice(notice, session, slot.Value, newMember);
        await RoomManager.BroadcastAsync(room, notice, except: session);

        await session.SendAsync(new Packet(Opcode.GL_JOIN_ACK).WriteU8((byte)JoinAck.Ok));
    }

    // 262 GL_JOINPASS_REQ (u8 room_no str pass) → 263 (u8 code)。
    // code: 0 = 密碼錯/房不存在; 1 = 通過 (client 轉發 260)。
    private static async ValueTask JoinPass(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        string pass = packet.ReadStr();

        var room = context.Rooms.Find(roomNo);
        bool ok = room is not null && (string.IsNullOrEmpty(room.Password) || room.Password == pass);

        await session.SendAsync(new Packet(Opcode.GL_JOINPASS_ACK).WriteU8(ok ? (byte)1 : (byte)0));
    }

    // 264 GL_JOININFO_REQ (u8 room_no) → 265 房資訊。
    // 265 佈局 (sub_574550, 兩分支同構; n2_0==3 的 TeamSurvival 分支寫入
    // byte_E9FBA8, 一般分支寫入 dword_EA063C「目前房」物件):
    //   u8 status(0=可進房, →modeUI+12), u8 map(+409「ROOM_MAP」),
    //   u8 count(+1), u8 B(+408 存而不讀), u16 slot_mask(+6),
    //   u8 C(+410 存而不讀), u8 time(+411「ROOM_TIME」),
    //   u16 round(+412「ROOM_ROUND」), u8 item(+414「ROOM_ITEM」),
    //   u16 G(+416 存而不讀), count × (u8 slot, str name[24], u8 讀後丟棄)
    //   — sub_515DE0 只顯示 map/time/round/item 四欄, +408/+410/+416
    //   全程無讀者 (同 114 的 +146/+150 送 0 安全), 尾欄 u8 讀入 v13
    //   後無引用, 送 0。
    private static async ValueTask JoinInfo(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        var room = context.Rooms.Find(roomNo);

        if (room is null)
        {
            var gone = new Packet(Opcode.GL_JOININFO_ACK)
                .WriteU8(1)                                 // status: 不可進房
                .WriteU8(0).WriteU8(0).WriteU8(0)           // map / count / B
                .WriteU16(0).WriteU8(0).WriteU8(0)          // slot_mask / C / time
                .WriteU16(0).WriteU8(0).WriteU16(0);        // round / item / G
            await session.SendAsync(gone);
            return;
        }

        var ack = new Packet(Opcode.GL_JOININFO_ACK)
            .WriteU8(0)                                     // status: 可進房
            .WriteU8(room.MapId)                            // +409 ROOM_MAP
            .WriteU8((byte)room.Members.Count)              // +1 人數
            .WriteU8(0)                                     // +408 (client 存而不讀)
            .WriteU16(room.SlotMask)                        // +6 槽位點陣
            .WriteU8(0)                                     // +410 (client 存而不讀)
            .WriteU8(room.TimeLimit)                        // +411 ROOM_TIME
            .WriteU16(room.WinCount)                        // +412 ROOM_ROUND
            .WriteU8(room.ItemMode)                         // +414 ROOM_ITEM
            .WriteU16(0);                                   // +416 (client 存而不讀)

        foreach (var (slot, member) in room.Members.OrderBy(kv => kv.Key))
        {
            ack.WriteU8(slot)                               // 玩家槽位
               .WriteStr(member.Nickname)                   // 24B 名字 (sub_592730 讀入 v14[6])
               .WriteU8(0);                                 // 尾欄 (client 讀後丟棄)
        }

        await session.SendAsync(ack);
    }

    // 266 GL_JOINGAME_REQ (u8 room_no u8 flag) → 267 (u8 code u8 flag)。
    // sub_516900 全 code 皆為「接受」: 0/2/3→狀態1, 1/4→狀態2(存 flag),
    // 5→狀態5 — 故依 flag 回 code (0=玩家 / 1=觀戰) 並回傳 flag。
    private static async ValueTask JoinGame(Session session, Packet packet, ServerContext context)
    {
        _ = packet.ReadU8();                                // room_no
        byte flag = packet.ReadU8();                        // byte_1D0CFE6: 0=PLAY, 1=OBSERVE

        byte code = flag == 0 ? (byte)0 : (byte)1;
        await session.SendAsync(new Packet(Opcode.GL_JOINGAME_ACK)
            .WriteU8(code)
            .WriteU8(flag));
    }

    // 268 GL_JOINPLAY_REQ (u8 room_no u8 flag) → 269 (sub_574B20, 逐欄定案):
    //   flag 0: 以玩家加入 (PLAY) → code 6: 回傳自身完整 snapshot (含 sub_885D00 語音塊)
    //   flag 1: 以觀戰加入 (OBSERVE) → code 7: 回傳全房快照 + 全成員條目 (含 sub_885D00 語音塊)
    //   無此房/未登入/加入失敗 → code 0 (回房單提示錯誤)
    private static async ValueTask JoinPlay(Session session, Packet packet, ServerContext context)
    {
        byte roomNo = packet.ReadU8();
        byte flag = packet.Remaining > 0 ? packet.ReadU8() : (byte)0;

        if (session.UserId == 0)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
            return;
        }

        var room = context.Rooms.Find(roomNo);
        if (room is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
            return;
        }

        if (flag == 0)
        {
            // 玩家模式加入 (code 6: 自身 snapshot)
            byte? slot = room.TakeFreeSlot();
            if (slot is null)
            {
                await session.SendAsync(new Packet(Opcode.GL_JOINPLAY_ACK).WriteU8(0));
                return;
            }

            room.Members[slot.Value] = session;
            session.RoomNo = roomNo;

            var data = RoomHandlers.LoadMemberData(context.Db, session);

            // 廣播給既有成員 (114 sub_type==1)
            var notice = new Packet(Opcode.GL_ENTERROOM_ACK).WriteU8(1);
            RoomHandlers.WriteMemberNotice(notice, session, slot.Value, data);
            await RoomManager.BroadcastAsync(room, notice, except: session);

            // 回給加入者: 269 code 6
            var ack = new Packet(Opcode.GL_JOINPLAY_ACK)
                .WriteU8(6)
                .WriteS32((int)session.UserId)
                .WriteU8(slot.Value)
                .WriteStr(session.Nickname);

            // 外觀/模型/自訂貼圖
            ack.WriteU8(data.CurChar?.SlotNo ?? (byte)0)
               .WriteU8(data.CurChar?.CharType ?? (byte)0);
            for (int i = 0; i < 12; i++)
            {
                ack.WriteU16(data.CurChar?.Equip[i] ?? (ushort)0);
            }

            ack.WriteU8(data.CurChar?.CharType ?? (byte)0)
               .WriteS16(0).WriteS16(0).WriteS16(0)
               .WriteS32(0).WriteS32(0).WriteS32(0).WriteStr("");

            // 4 武器組
            for (byte g = 0; g < 4; g++)
            {
                var wg = data.Groups.FirstOrDefault(x => x.GroupNo == g);
                ack.WriteU16(wg?.Equipped ?? (ushort)0);
                if (g != 3)
                {
                    ack.WriteU16(wg?.Sub1 ?? (ushort)0)
                       .WriteU16(wg?.Sub2 ?? (ushort)0)
                       .WriteU16(wg?.Sub3 ?? (ushort)0);
                }
                if (wg is { Equipped: not 0 })
                {
                    foreach (var part in wg.Parts)
                    {
                        ack.WriteS32(part);
                    }
                }
            }

            ack.WriteU8(0);                                     // extra_flag
            foreach (var skill in data.Slots.Skill)
            {
                ack.WriteS32(skill);
            }

            ack.WriteU8(5); // unresolved n5: retain existing raw convention
            foreach (var puzzleItemId in data.Slots.NewSkillPuzzleIds)
            {
                ack.WriteS32(puzzleItemId);
            }

            RoomHandlers.WriteVoiceBlock(ack, data.Voice);       // sub_885D00 語音塊 (85B)
            await session.SendAsync(ack);
        }
        else
        {
            // 觀戰模式加入 (code 7: 全房 snapshot)
            var ack = new Packet(Opcode.GL_JOINPLAY_ACK)
                .WriteU8(7)
                .WriteS32(room.RoomUid)
                .WriteS32(0)                                    // elapsed_ms
                .WriteU8(room.MapId)
                .WriteU8((byte)room.Members.Count)
                .WriteU8(room.RoomNo)
                .WriteU8(room.Rule)
                .WriteU16(room.WinCount)
                .WriteU8(room.OpenSlotCount)
                .WriteU8(room.TimeLimit)
                .WriteU16(0)                                    // round
                .WriteU8(room.ItemMode)
                .WriteU8(0)                                     // skill_off
                .WriteU16(0)
                .WriteU8(0).WriteU8(0)
                .WriteU8((byte)(string.IsNullOrEmpty(room.Password) ? 0 : 1))
                .WriteU8(0).WriteU8(0).WriteU8(0)
                .WriteU8(0);                                    // observer

            foreach (var (memberSlot, member) in room.Members.OrderBy(kv => kv.Key))
            {
                var data = RoomHandlers.LoadMemberData(context.Db, member);
                bool isMaster = memberSlot == room.MasterSlot;

                ack.WriteS32((int)member.UserId)
                   .WriteU8(memberSlot)
                   .WriteStr(member.Nickname)
                   .WriteU8(isMaster ? (byte)1 : (byte)0)
                   .WriteU8(0)                                  // status
                   .WriteS32(0).WriteS32(0).WriteS32(0)         // custom_tex
                   .WriteU8(1)                                  // alive
                   .WriteU8(0);                                 // dead_flag

                // 外觀
                ack.WriteU8(data.CurChar?.SlotNo ?? (byte)0)
                   .WriteU8(data.CurChar?.CharType ?? (byte)0);
                for (int i = 0; i < 12; i++)
                {
                    ack.WriteU16(data.CurChar?.Equip[i] ?? (ushort)0);
                }

                ack.WriteU8(data.CurChar?.CharType ?? (byte)0)
                   .WriteS16(0).WriteS16(0).WriteS16(0)
                   .WriteS32(0).WriteS32(0).WriteS32(0).WriteStr("");

                // 4 武器組
                for (byte g = 0; g < 4; g++)
                {
                    var wg = data.Groups.FirstOrDefault(x => x.GroupNo == g);
                    ack.WriteU16(wg?.Equipped ?? (ushort)0);
                    if (g != 3)
                    {
                        ack.WriteU16(wg?.Sub1 ?? (ushort)0)
                           .WriteU16(wg?.Sub2 ?? (ushort)0)
                           .WriteU16(wg?.Sub3 ?? (ushort)0);
                    }
                    if (wg is { Equipped: not 0 })
                    {
                        foreach (var part in wg.Parts)
                        {
                            ack.WriteS32(part);
                        }
                    }
                }

                ack.WriteU8(0);                                 // extra_flag

                if (room.Rule == 12)                            // soccer
                {
                    ack.WriteU8(0);
                }
                else if (room.Rule == 13)                       // occupy renewal
                {
                    ack.WriteS32(0);
                }

                ack.WriteU8(0);                                 // active_weapon_flag

                foreach (var skill in data.Slots.Skill)
                {
                    ack.WriteS32(skill);
                }

                ack.WriteU8(5); // unresolved n5: retain existing raw convention
                foreach (var puzzleItemId in data.Slots.NewSkillPuzzleIds)
                {
                    ack.WriteS32(puzzleItemId);
                }

                RoomHandlers.WriteVoiceBlock(ack, data.Voice);   // sub_885D00 語音塊 (85B)
                ack.WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0); // in-game flags
            }

            // room state tail
            ack.WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0)
               .WriteS32(0)                                     // n0x3E8
               .WriteU8(0)
               .WriteU8(0).WriteS32(0).WriteS16(0).WriteS16(0).WriteU8(0);

            for (int i = 0; i < 16; i++)
            {
                ack.WriteS32(0);                                // 16×s32 scores
            }

            await session.SendAsync(ack);
        }
    }
}
