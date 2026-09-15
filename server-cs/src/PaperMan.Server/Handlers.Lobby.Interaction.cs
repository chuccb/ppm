// =============================================================================
// Lobby interaction and client-driven scene transitions. This covers chat, lists,
// scene notices, tutorial/token acknowledgements, and the secondary channel endpoint.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{

    // 270 GL_MYINFO_OPEN (sub_556680): s8 — 個資公開開關 (單向通知,
    // 無 ACK; 卅六輪) — 記錄即可
    private static ValueTask MyInfoOpen(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadS8() : (sbyte)0;
        return ValueTask.CompletedTask;
    }

    // 836 GL_SHOUTCHAT_REQ (sub_583370): s32 uid(自己 dword_EE8CB4),
    //   s32 strlen, str text — client 前置: 3s 牆鐘限流 (dword_1D0D24C)
    //   + CHAT_SHOUT 動作表 entry[4]!=0 (可喊)。uid/strlen 以 server 為準
    //   (防冒名), 僅 text 有意義。
    // → 837 ACK (sub_583C20): u8 flag(0/1 皆顯示), s32 uid, s32 timer,
    //   str nick, s32 raw_len, raw[raw_len] — uid==自己 → client 把
    //   CHAT_SHOUT 動作表 cooldown 設為 timer (0=不可再喊, 非0=可再喊)。
    //   timer 精確單位原服未明 (與 391 同款 server 動作值); 送 1 保持可用,
    //   由 client 3s 牆鐘限流防洗頻。GL = 大廳全域, 廣播全服。
    private static async ValueTask Shout(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0 || session.Nickname.Length == 0)
        {
            return;
        }

        _ = packet.ReadS32();                                // 自己 uid (以 session 為準)
        _ = packet.ReadS32();                                // strlen (client 附, 不重算)
        var text = packet.ReadStr();
        if (text.Length == 0)
        {
            return;
        }

        var shout = new Packet(Opcode.GL_SHOUTCHAT_ACK)
            .WriteU8(0)                                      // flag: 0/1 皆顯示 (sub_583C20)
            .WriteS32((int)session.UserId)
            .WriteS32(ShoutCooldown)                         // CHAT_SHOUT 動作值 (非0=可用)
            .WriteStr(session.Nickname)
            .WriteS32(Packet.Ansi.GetByteCount(text))
            .WriteRaw(Packet.Ansi.GetBytes(text));

        foreach (var target in context.Sessions.All)
        {
            if (!target.Authenticated)
            {
                continue;
            }

            try
            {
                await target.SendAsync(Packet.FromPayload(shout.Opcode, shout.Payload));
            }
            catch
            {
                // 個別連線斷線不影響其他人
            }
        }
    }

    /// <summary>837 的 timer — CHAT_SHOUT 動作表 cooldown (非0=可再喊)。</summary>
    private const int ShoutCooldown = 1;

    // 250 is an exact-empty local transition notice. `sub_574080` advances
    // the client state itself; no 251 consumer was recovered.
    private static ValueTask LobbyEnter(Session session, Packet packet, ServerContext context)
    {
        return ValueTask.CompletedTask;
    }

    // 252 is also exactly empty. `sub_574120` sends it and immediately puts
    // the client in shop state 3. There is no recovered native 253 consumer,
    // but the project explicitly permits the empty 253 interoperability ACK.
    // Do not attach catalog, account, or entitlement data to this ack.
    private static ValueTask ShopEnter(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GL_SHOPIN_ACK));
    }
    // REQ(119) builder @0x56E2xx: str message (ANSI)
    // ACK(120) sub_56E300: s32 custom_tex, str nick, wstr message
    //   ⚠ 訊息回送用「寬字串」(sub_5927B0 讀 UTF-16LE) — 與 REQ 的 ANSI 不對稱!
    //   client 端還會拿 nick 過 sub_539320 黑名單 (忽略清單) 過濾
    private static async ValueTask Chat(Session session, Packet packet, ServerContext context)
    {
        // 兩變體 (廿四輪): 完整版 s32 tex + str nick + wstr msg (與 ACK 同構);
        // 簡版只有 str。以剩餘長度判別。
        int tex = 0;
        string nick = session.Nickname;
        string message;

        if (packet.Remaining > 8)
        {
            tex = packet.ReadS32();
            nick = packet.ReadStr();
            message = packet.ReadWStr();
        }
        else
        {
            message = packet.ReadStr();
        }

        if (session.UserId == 0 || message.Length == 0)
        {
            return;
        }

        // 單人大廳: 回聲給自己 (多人時原樣廣播 — client 已附 nick+tex)
        await session.SendAsync(new Packet(Opcode.GL_CHATTING_ACK)
            .WriteS32(tex)
            .WriteStr(nick)
            .WriteWStr(message));
    }

    // ACK(106) sub_56A250: u16 count; 若 count!=0 才有 u8 flags, u8 n,
    // repeat n{s32 uid, str nick, s32 exp; uid>0 時 +s32 custom_tex, str(64)}
    // ⚠ 第三個 s32 = exp (sub_588560 → sub_403360 exp→level 查表, 十二輪);
    // count==0 → 之後不再讀任何欄位 (交叉驗證確認)
    private static async ValueTask UserList(Session session, Packet packet, ServerContext context) =>
        await session.SendAsync(new Packet(Opcode.GL_USERLIST_ACK).WriteU16(0));

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
    private static async ValueTask RoomList(Session session, Packet packet, ServerContext context)
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
               .WriteU8(room.Rule)                          // game_mode → sub_53FBB0 (0..15)
               .WriteBool(false)                            // +108 room_type bit A (server 側未定)
               .WriteBool(IsTeamMode(room.Rule))            // mode+12 是否隊伍房 (sub_56A7B0: sub_438990?1:0)
               .WriteBool(false)                            // +109 room_type bit B (server 側未定)
               .WriteBool(room.DoubleDamage)                // +128 double_damage (990/991)
               .WriteU8(room.MapId)                         // +130 map (sub_540280/540260; 122 亦寫此欄)
               .WriteU8((byte)(room.ItemMode & 1))          // mode+4 = item bit0 (sub_74F450; 175/176)
               .WriteBool(room.NoSkillBg);                  // +185 no_skill_bg (712/713)
        }

        await session.SendAsync(ack);
    }

    /// <summary>sub_438990 的 server 側對照: 兩隊制模式 (0/2/3/4/8/10/11/12/13)。</summary>
    private static bool IsTeamMode(byte mode) => mode is 0 or 2 or 3 or 4 or 8 or 10 or 11 or 12 or 13;

    // 685 GL_TUTORIALINDEX_REQ (sub_55C6F0, 空) → 686 ACK (sub_55C790): s32 index
    private static async ValueTask TutorialIndex(Session session, Packet packet, ServerContext context)
    {
        int index = session.UserId != 0 ? context.Db.GetTutorialIndex(session.UserId) : 0;
        await session.SendAsync(new Packet(Opcode.GL_TUTORIALINDEX_ACK).WriteS32(index));
    }

    // 689 GL_TUTORIAL_INDEX_SET_REQ (sub_55C7D0: s32 index) → 690 ACK (sub_582530): s32 index
    private static async ValueTask TutorialIndexSet(Session session, Packet packet, ServerContext context)
    {
        int index = packet.Remaining >= 4 ? packet.ReadS32() : 0;
        if (session.UserId != 0)
        {
            context.Db.SetTutorialIndex(session.UserId, index);
        }

        await session.SendAsync(new Packet(Opcode.GL_TUTORIAL_INDEX_SET_ACK).WriteS32(index));
    }

    // 704 GL_LEVEL_KILL_LIMIT_REQ (sub_582570, 空)
    // → 705 ACK (sub_55C9B0): s32 kill_limit, f32 exp_rate, s32 max_level_limit
    private static async ValueTask LevelKillLimit(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GL_LEVEL_KILL_LIMIT_ACK)
            .WriteS32(50)                                   // 殺敵上限 50
            .WriteF32(1.0f)                                 // 經驗倍率 1.0
            .WriteS32(30);                                  // 最大等級限制 30

        await session.SendAsync(ack);
    }

    // 706 GL_BILLTOKEN_REQ (sub_460480, 空) → 707 ACK (sub_46AD00 case 707): str token
    private static async ValueTask BillToken(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_BILLTOKEN_ACK).WriteStr("TOKEN_PAPERMAN_OK"));
    }

    // 787 GL_RACKINGWEB_TOKEN_REQ (sub_581E40, 空) → 788 ACK (sub_44BEA0): str token
    private static async ValueTask RankingWebToken(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_RACKINGWEB_TOKEN_ACK).WriteStr("RANKING_TOKEN_OK"));
    }

    // 834 GL_DATA_RECV_COMPLETED_REQ (sub_583120: s32 uid) → 835 ACK (sub_5831D0): 空包
    private static async ValueTask DataRecvCompleted(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GL_DATA_RECV_COMPLETED_ACK));
    }

    // 370 GL_CHANGECHANNEL_REQ (sub_570030: u8 channel_id)
    // → 371 ACK (sub_570100): u8 status(1=成功), u8 channel_id, str host_ip, s32 host_port, u8 extra
    private static async ValueTask ChangeChannel(Session session, Packet packet, ServerContext context)
    {
        byte ch = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var ack = new Packet(Opcode.GL_CHANGECHANNEL_ACK)
            .WriteU8(1)                                     // status 1 = 成功
            .WriteU8(ch)                                    // channel_id
            // This is sub_570100 → sub_596E60's *secondary* UDP address.
            // Do not substitute the successful-196 UdpHost/UdpPort here:
            // client evidence has not established the two fields' equivalence.
            .WriteStr(context.Config.PublicHost)
            .WriteS32(context.Config.ChannelPort + 1)       // independent s32; native consumer takes low u16
            .WriteU8(0);                                    // extra

        await session.SendAsync(ack);
    }

}
