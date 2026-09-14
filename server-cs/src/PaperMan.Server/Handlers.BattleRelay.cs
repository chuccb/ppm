// =============================================================================
// GG 戰鬥中繼 handlers — 本輪(五十一)逐函數重驗, 更正廿五輪「三模式」簡化:
//
//   廿五輪把 GG 全族簡化成「slot 前綴轉發」, 但逐函數重讀後確認各族佈局
//   其實不同 (docs/PACKETS.md §3.15d3 已同步更正):
//
//   ① TH 駭入/炸彈簇 (316-331): REQ 首欄是「team」(0/1) 不是 slot!
//      317 sub_557040 讀 u8 team, u8 slot → sub_766490(team<2 定址);
//      319 sub_557400 讀 u8 team, 6×f32, u8 slot;
//      321 sub_557730 只讀 u8 team (無 slot!);
//      323 sub_5579A0 只讀 u8 team; 327/329/331 讀 u8 team, u8 slot。
//      → ACK = REQ 原欄位 + 尾附發話者 slot (321/323 例外不加 slot)。
//      322 REQ 空 → 回 [BombTeam] (318 武裝成功時記下, 未植彈則忽略)。
//   ② 足球 964/967 REQ 皆空 → ACK 965/968 (sub_566040/sub_566200)
//      讀 u8 flag, u8 slot; flag 0 =「該事件成立」(得球/進球) — 依 REQ
//      語意回 0, 非硬編 (flag 1 = 取消/收回)。
//   ③ 奪寶 443/445 (sub_55BDC0/sub_55C060): REQ u8+s16; 444/446
//      (sub_55BE80/sub_55C120) 讀 u8,u8,u16×3 分數組 — 需奪寶計分
//      狀態機才能產出, **不可轉發** → 不註冊 (見 TODO)。
//   ④ 聊天四連 344/346/348/350: REQ = s32 tex, u8 slot, str;
//      ACK 345/347/349/351 (sub_58D870/58D8A0/58D8D0/58D900 →
//      sub_74A5F0 → sub_748E40) 讀 s32, u8, str — 同構。⚠ 必須以
//      **ACK opcode** 廣播 (REQ opcode 無 dispatcher case, 會被忽略)。
//
// 復活五連 (342/360/455/746/909/971) 與 Y_TCP_INF/PM_TSPOSUPDATE 維持
// 既有實作。戰鬥語意 (傷害/勝負) 由 client P2P 決定 — server 只驗證
// (在房內)+廣播, 與原版 relay 行為一致。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class BattleRelayHandlers
{
    /// <summary>
    /// ① TH 駭入/炸彈簇 — ACK = REQ(首欄 team) + [尾附 slot]。
    /// ArmsBomb: 該事件「正式武裝炸彈」— 只有 318 駭入成功才記 room.BombTeam
    /// (316 開駭若失敗, 322 爆炸時不可用錯隊)。
    /// </summary>
    private static readonly (Opcode Req, Opcode Ack, bool AppendSlot, bool ArmsBomb)[] HackCluster =
    [
        (Opcode.GG_HACKSTART_REQ,   Opcode.GG_HACKSTART_ACK,   true,  false),
        (Opcode.GG_HACKSUCC_REQ,    Opcode.GG_HACKSUCC_ACK,    true,  true),
        (Opcode.GG_HACKFAIL_REQ,    Opcode.GG_HACKFAIL_ACK,    false, false),
        (Opcode.GG_UNHACKSTART_REQ, Opcode.GG_UNHACKSTART_ACK, true,  false),
        (Opcode.GG_UNHACKSUCC_REQ,  Opcode.GG_UNHACKSUCC_ACK,  true,  false),
        (Opcode.GG_UNHACKFAIL_REQ,  Opcode.GG_UNHACKFAIL_ACK,  true,  false),
    ];

    /// <summary>② 復活同構 (REQ s32 → ACK slot+座標)。</summary>
    private static readonly (Opcode Req, Opcode Ack)[] Respawns =
    [
        (Opcode.GG_SOLORESPON_REQ, Opcode.GG_SOLORESPON_ACK),
        (Opcode.GG_TSURRESPON_REQ, Opcode.GG_TSURRESPON_ACK),
        (Opcode.GG_EXERCISERESPON_REQ, Opcode.GG_EXERCISERESPON_ACK),
        (Opcode.GG_PNR_RESPON_REQ, Opcode.GG_PNR_RESPON_ACK),
        (Opcode.GG_OCC_RESPON_REQ, Opcode.GG_OCC_RESPON_ACK),
        (Opcode.GG_SOCCER_RESPON_REQ, Opcode.GG_SOCCER_RESPON_ACK),
    ];

    /// <summary>④ 聊天四連 — 必須以 ACK opcode 廣播 (REQ opcode 無 dispatcher case)。</summary>
    private static readonly (Opcode Req, Opcode Ack)[] Chats =
    [
        (Opcode.GG_LIVECHAT_REQ,     Opcode.GG_LIVECHAT_ACK),      // 345
        (Opcode.GG_TEAMCHAT_REQ,     Opcode.GG_TEAMCHAT_ACK),      // 347
        (Opcode.GG_DEADCHAT_REQ,     Opcode.GG_DEADCHAT_ACK),      // 349
        (Opcode.GG_TEAMDEADCHAT_REQ, Opcode.GG_TEAMDEADCHAT_ACK),  // 351
    ];

    public static void Register(Registrar add)
    {
        // Y_TCP_INF (165→166): TCP 備援戰鬥同步 (卅輪 — 第六處理層
        // sub_749B90 subtype 1-9)。165 REQ 首欄無 slot — server 轉發時
        // 以 166 = u8 slot + 原 payload 補上。
        add(Opcode.Y_TCP_INF_REQ, MakeSlotRelay(Opcode.Y_TCP_INF_ACK));

        // PM_TSPOSUPDATE (271→272): TS 模式位置更新 — 同上轉發
        add(Opcode.PM_TSPOSUPDATE_REQ, MakeSlotRelay(Opcode.PM_TSPOSUPDATE_ACK));

        foreach (var (req, ack, appendSlot, armsBomb) in HackCluster)
        {
            add(req, MakeHackRelay(ack, appendSlot, armsBomb));
        }

        // 322 GG_BOMBSUCC_REQ 空 payload → 323 [u8 team] (炸彈所屬隊伍)
        add(Opcode.GG_BOMBSUCC_REQ, BombSucceeded);

        // 足球 964 得球 / 967 進球 → ACK [u8 flag=0(事件成立), u8 slot]
        add(Opcode.GG_GET_BALL_REQ, MakeSoccerEvent(Opcode.GG_GET_BALL_ACK));
        add(Opcode.GG_GET_GOAL_REQ, MakeSoccerEvent(Opcode.GG_GET_GOAL_ACK));

        // 有狀態的戰場物件不是 blind relay。各 handler 只接受房內、slot 與
        // uid 都和 session 相符的 REQ，並以 Room.BattleState 原子轉換。
        OccupyHandlers.Register(add);
        DropWeaponHandlers.Register(add);

        foreach (var (req, ack) in Respawns)
        {
            add(req, MakeRespawn(ack));
        }

        foreach (var (req, ack) in Chats)
        {
            add(req, MakeChatRelay(ack));
        }
    }

    // ------------------------------------------------------------------ ① TH
    /// <summary>
    /// TH 簇通用: REQ 首欄 = team (0/1, sub_766490 以 n2&lt;2 定址隊伍);
    /// ACK = team + [REQ 續接欄位] + [尾附發話者 slot]。
    /// 318 GG_HACKSUCC_REQ 的續接欄位是 6×f32 (爆點座標/計時, sub_5571E0),
    /// 原樣轉播; 其餘 REQ 讀完 team 即無剩餘。
    /// </summary>
    private static PacketHandler MakeHackRelay(Opcode ack, bool appendSlot, bool armsBomb) =>
        async (session, packet, context) =>
        {
            if (!TryFindRoomSlot(session, context, out var room, out var slot) || packet.Remaining < 1)
            {
                return;
            }

            byte team = packet.ReadU8();
            if (team > 1)
            {
                return;                                          // team 僅 0/1
            }

            if (armsBomb)
            {
                room.BombTeam = team;                            // 318 武裝成功才記 (322 回 323 用)
            }

            var notice = new Packet(ack).WriteU8(team);
            if (appendSlot)
            {
                notice.WriteRaw(packet.Payload[packet.ReadPos..]) // 318: 6×f32 續接
                       .WriteU8(slot);                           // 尾附發話者 slot
            }

            await RoomManager.BroadcastAsync(room, notice);
        };

    // 322 GG_BOMBSUCC_REQ (空) → 323 ACK (sub_5579A0): u8 team — 爆炸的
    // 炸彈屬哪隊。team 由 316/318 記下; 未植彈即收到 322 → 依「未確認
    // 不硬編」原則忽略 (無炸彈可爆)。
    private static async ValueTask BombSucceeded(Session session, Packet packet, ServerContext context)
    {
        if (!TryFindRoomSlot(session, context, out var room, out _) || room.BombTeam is not { } team)
        {
            return;
        }

        await RoomManager.BroadcastAsync(room, new Packet(Opcode.GG_BOMBSUCC_ACK).WriteU8(team));
    }

    // ------------------------------------------------------------------ ② 足球
    // 964 GG_GET_BALL_REQ / 967 GG_GET_GOAL_REQ (sub_565F60/sub_566120)
    // 皆空 payload; 965/968 (sub_566040/sub_566200) 讀 u8 flag, u8 slot —
    // flag 0 = 事件成立 (得球/進球), 1 = 收回。REQ 本身即事件 → 回 0。
    private static PacketHandler MakeSoccerEvent(Opcode ack) =>
        async (session, packet, context) =>
        {
            if (!TryFindRoomSlot(session, context, out var room, out var slot))
            {
                return;
            }

            await RoomManager.BroadcastAsync(room,
                new Packet(ack).WriteU8(0).WriteU8(slot));
        };

    // ------------------------------------------------------------------ 復活
    private static PacketHandler MakeRespawn(Opcode ack) =>
        async (session, packet, context) =>
        {
            if (!TryFindRoomSlot(session, context, out var room, out var slot))
            {
                return;
            }

            _ = packet.Remaining >= 4 ? packet.ReadS32() : 0;     // respawn token

            // 座標 0,0,0 = client 使用地圖預設重生點
            var notice = new Packet(ack)
                .WriteU8(slot)
                .WriteU8(0)
                .WriteS16(0).WriteS16(0).WriteS16(0);
            await RoomManager.BroadcastAsync(room, notice);
        };

    // ------------------------------------------------------------------ 聊天
    // 344-350 REQ = s32 tex, u8 slot, str — ACK 同構, 但必須用 ACK opcode
    // 廣播 (client 只對 345/347/349/351 有 dispatcher case)。發話者本地已
    // 顯示 → except 自己。(team/dead 聽眾過濾需戰場狀態, 單機版全房廣播)
    private static PacketHandler MakeChatRelay(Opcode ack) =>
        async (session, packet, context) =>
        {
            if (!TryFindRoomSlot(session, context, out var room, out _))
            {
                return;
            }

            var notice = Packet.FromPayload(ack, packet.Payload);
            await RoomManager.BroadcastAsync(room, notice, except: session);
        };

    // ------------------------------------------------------------------ 共用
    /// <summary>165/271: ACK = u8 slot + REQ 原 payload (REQ 首欄無 slot)。</summary>
    private static PacketHandler MakeSlotRelay(Opcode ack) =>
        async (session, packet, context) =>
        {
            if (!TryFindRoomSlot(session, context, out var room, out var slot))
            {
                return;
            }

            var notice = new Packet(ack)
                .WriteU8(slot)
                .WriteRaw(packet.Payload[packet.ReadPos..]);      // REQ 原 payload 續接
            await RoomManager.BroadcastAsync(room, notice);
        };

    /// <summary>取 session 所在房與其 slot (非房內回 false)。</summary>
    internal static bool TryFindRoomSlot(Session session, ServerContext context, out Room room, out byte slot)
    {
        room = null!;
        slot = 0;

        if (session.RoomNo is not { } roomNo || context.Rooms.Find(roomNo) is not { } r)
        {
            return false;
        }

        room = r;
        foreach (var (s, member) in r.Members)
        {
            if (ReferenceEquals(member, session))
            {
                slot = s;
                return true;
            }
        }

        return false;
    }
}
