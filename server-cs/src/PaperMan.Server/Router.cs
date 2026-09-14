// =============================================================================
// 封包路由 — 對應客戶端 dispatcher sub_58B010 (365 case 的巨型 switch)。
// 伺服端改為註冊表 + frozen lookup; 未知 opcode 記錄後靜默忽略
// (原版 default: return 同樣行為)。
// =============================================================================
using System.Collections.Frozen;
using PaperMan.Protocol;

namespace PaperMan.Server;

public delegate ValueTask PacketHandler(Session session, Packet packet, ServerContext context);

public sealed class Router
{
    private readonly FrozenDictionary<ushort, PacketHandler> _table;

    private Router(Dictionary<ushort, PacketHandler> table) =>
        _table = table.ToFrozenDictionary();

    public int Count => _table.Count;

    public static Router Build()
    {
        var table = new Dictionary<ushort, PacketHandler>();
        void Add(Opcode op, PacketHandler h) => table.Add((ushort)op, h);

        AuthHandlers.Register(Add);
        LobbyHandlers.Register(Add);
        ShopHandlers.Register(Add);
        StatHandlers.Register(Add);
        ClanHandlers.Register(Add);
        QuestHandlers.Register(Add);
        FriendHandlers.Register(Add);
        RoomHandlers.Register(Add);
        BattleRelayHandlers.Register(Add);
        JoinHandlers.Register(Add);
        ChannelHandlers.Register(Add);
        VoiceHandlers.Register(Add);
        WarehouseHandlers.Register(Add);
        MasterHandlers.Register(Add);
        GameCenterHandlers.Register(Add);
        AiHandlers.Register(Add);
        return new(table);
    }

    /// <summary>回傳 false = 無 handler (原版 default: return)。</summary>
    public async ValueTask<bool> DispatchAsync(Session session, Packet packet, ServerContext context)
    {
        if (session.Role == ServerRole.Login)
        {
            // 694 triggers exactly one 682 in CLobbyLogin. After successful
            // 681, the client transitions away from this login conversation;
            // accepting another 682 would silently replace this session's
            // account identity.
            if (session.Authenticated && packet.Opcode == Opcode.GL_LOGIN_REQ)
            {
                Console.WriteLine($"[s{session.Id}] !! rejected repeated GL_LOGIN_REQ after successful 681");
                return true;
            }

            if (packet.Opcode != Opcode.GT_PING_REQ && packet.Opcode != Opcode.GL_LOGIN_REQ)
            {
                Console.WriteLine($"[s{session.Id}] !! rejected {packet.Opcode} on Login listener");
                return true;
            }
        }
        else
        {
            if (packet.Opcode == Opcode.GL_LOGIN_REQ)
            {
                Console.WriteLine($"[s{session.Id}] !! rejected GL_LOGIN_REQ on Channel listener");
                return true;
            }

            if (!session.Authenticated)
            {
                // The fresh channel socket carries no account state until its
                // one 143 claim succeeds. The native CLobbyChannel wrapper
                // still sends 195 after every delivered 144, including a
                // rejected 143, so 195 remains allowed to produce its explicit
                // non-success 196 response.
                if (packet.Opcode != Opcode.GT_PING_REQ
                    && packet.Opcode != Opcode.PM_UDPSTART_REQ
                    && packet.Opcode != Opcode.GC_ENTERCHANNEL_REQ)
                {
                    Console.WriteLine($"[s{session.Id}] !! rejected {packet.Opcode} before channel handoff");
                    return true;
                }
            }
            else if (!session.ChannelEntryCompleted)
            {
                // 143 proves the account handoff, not channel selection. The
                // 195 → 196 success transition is required before the client
                // may reach lobby, room, economy, or gameplay handlers.
                if (packet.Opcode != Opcode.GT_PING_REQ
                    && packet.Opcode != Opcode.PM_UDPSTART_REQ
                    && packet.Opcode != Opcode.GC_ENTERCHANNEL_REQ)
                {
                    Console.WriteLine($"[s{session.Id}] !! rejected {packet.Opcode} before successful 196");
                    return true;
                }
            }
        }

        if (!_table.TryGetValue(packet.OpcodeRaw, out var handler))
        {
            Console.WriteLine($"[s{session.Id}] ?? UNMAPPED OPCODE: {packet.Opcode} ({packet.OpcodeRaw} / 0x{packet.OpcodeRaw:X4})");
            return false;
        }

        Console.WriteLine($"[s{session.Id}] >> DISPATCH {packet.Opcode} ({packet.OpcodeRaw})");
        await handler(session, packet, context).ConfigureAwait(false);
        return true;
    }
}

public delegate void Registrar(Opcode opcode, PacketHandler handler);
