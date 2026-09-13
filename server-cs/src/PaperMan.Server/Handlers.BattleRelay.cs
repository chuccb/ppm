// =============================================================================
// GG 戰鬥中繼通用轉發器 — 廿五輪三模式定案的實作 (廿九輪):
//
//   模式1 slot 前綴轉發: ACK = u8 actor_slot + REQ 原 payload
//     (316/318/320/322/326/328/330/443/445/737/739/741/902/904/906...)
//   模式2 復活五連同構: REQ s32 token → ACK u8 slot, u8, s16×3 座標
//     (342/360/455/746/909/971 — 六模式共用)
//   模式3 聊天過濾轉發: REQ s32 tex, u8 slot, str → 原樣廣播
//     (344 live / 346 team / 348 dead / 350 teamdead)
//
// 戰鬥語意 (傷害/勝負判定) 由 client P2P 決定 — server 只做
// 驗證(在房內)+廣播, 與原版 relay 行為一致。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class BattleRelayHandlers
{
    /// <summary>模式1: REQ op → ACK op (slot 前綴轉發)。</summary>
    private static readonly (Opcode Req, Opcode Ack)[] SlotPrefixed =
    [
        (Opcode.GG_HACKSTART_REQ, Opcode.GG_HACKSTART_ACK),
        (Opcode.GG_HACKSUCC_REQ, Opcode.GG_HACKSUCC_ACK),
        (Opcode.GG_HACKFAIL_REQ, Opcode.GG_HACKFAIL_ACK),
        (Opcode.GG_BOMBSUCC_REQ, Opcode.GG_BOMBSUCC_ACK),
        (Opcode.GG_UNHACKSTART_REQ, Opcode.GG_UNHACKSTART_ACK),
        (Opcode.GG_UNHACKSUCC_REQ, Opcode.GG_UNHACKSUCC_ACK),
        (Opcode.GG_UNHACKFAIL_REQ, Opcode.GG_UNHACKFAIL_ACK),
        (Opcode.GG_STEALSUCK_REQ, Opcode.GG_STEALSUCK_ACK),
        (Opcode.GG_STEALPUSH_REQ, Opcode.GG_STEALPUSH_ACK),
        (Opcode.GG_GET_BALL_REQ, Opcode.GG_GET_BALL_ACK),
        (Opcode.GG_GET_GOAL_REQ, Opcode.GG_GET_GOAL_ACK),
        (Opcode.GG_EXITGAME_REQ, Opcode.GG_EXITGAME_ACK),
    ];

    /// <summary>模式2: 復活同構 (REQ s32 → ACK slot+座標)。</summary>
    private static readonly (Opcode Req, Opcode Ack)[] Respawns =
    [
        (Opcode.GG_SOLORESPON_REQ, Opcode.GG_SOLORESPON_ACK),
        (Opcode.GG_TSURRESPON_REQ, Opcode.GG_TSURRESPON_ACK),
        (Opcode.GG_EXERCISERESPON_REQ, Opcode.GG_EXERCISERESPON_ACK),
        (Opcode.GG_PNR_RESPON_REQ, Opcode.GG_PNR_RESPON_ACK),
        (Opcode.GG_OCC_RESPON_REQ, Opcode.GG_OCC_RESPON_ACK),
        (Opcode.GG_SOCCER_RESPON_REQ, Opcode.GG_SOCCER_RESPON_ACK),
    ];

    /// <summary>模式3: 聊天過濾轉發 (REQ op == 廣播 op, 無獨立 ACK 解析)。</summary>
    private static readonly Opcode[] Chats =
    [
        Opcode.GG_LIVECHAT_REQ,
        Opcode.GG_TEAMCHAT_REQ,
        Opcode.GG_DEADCHAT_REQ,
        Opcode.GG_TEAMDEADCHAT_REQ,
    ];

    public static void Register(Registrar add)
    {
        // Y_TCP_INF (165→166): TCP 備援戰鬥同步 (卅輪 — 第六處理層
        // sub_749B90 subtype 1-9)。165 REQ 首欄無 slot — server 轉發時
        // 以 166 = u8 slot + 原 payload 補上 (與 GG 模式1 同構)。
        add(Opcode.Y_TCP_INF_REQ, MakeSlotRelay(Opcode.Y_TCP_INF_ACK));

        // PM_TSPOSUPDATE (271→272): TS 模式位置更新 — 同上轉發
        add(Opcode.PM_TSPOSUPDATE_REQ, MakeSlotRelay(Opcode.PM_TSPOSUPDATE_ACK));

        foreach (var (req, ack) in SlotPrefixed)
        {
            add(req, MakeSlotRelay(ack));
        }

        foreach (var (req, ack) in Respawns)
        {
            add(req, MakeRespawn(ack));
        }

        foreach (var chat in Chats)
        {
            add(chat, ChatRelay);
        }
    }

    private static PacketHandler MakeSlotRelay(Opcode ack) =>
        async (s, p, ctx) =>
        {
            if (FindRoomSlot(s, ctx) is not var (room, slot))
            {
                return;
            }

            var notice = new Packet(ack)
                .WriteU8(slot)
                .WriteRaw(p.Payload[p.ReadPos..]);          // REQ 原 payload 續接
            await RoomManager.BroadcastAsync(room, notice);
        };

    private static PacketHandler MakeRespawn(Opcode ack) =>
        async (s, p, ctx) =>
        {
            if (FindRoomSlot(s, ctx) is not var (room, slot))
            {
                return;
            }

            _ = p.Remaining >= 4 ? p.ReadS32() : 0;         // respawn token

            // 座標 0,0,0 = client 使用地圖預設重生點
            var notice = new Packet(ack)
                .WriteU8(slot)
                .WriteU8(0)
                .WriteS16(0).WriteS16(0).WriteS16(0);
            await RoomManager.BroadcastAsync(room, notice);
        };

    // 344-350: REQ = s32 tex, u8 slot, str msg — 原樣轉發
    // (team/dead 過濾需戰場狀態; 單機版全房廣播)
    private static async ValueTask ChatRelay(Session s, Packet p, ServerContext ctx)
    {
        if (FindRoomSlot(s, ctx) is not var (room, _))
        {
            return;
        }

        var notice = Packet.FromPayload(p.Opcode, p.Payload);
        await RoomManager.BroadcastAsync(room, notice, except: s);
    }

    private static (Room Room, byte Slot)? FindRoomSlot(Session s, ServerContext ctx)
    {
        if (s.RoomNo is not { } roomNo || ctx.Rooms.Find(roomNo) is not { } room)
        {
            return null;
        }

        var slot = room.Members.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        return (room, slot);
    }
}
