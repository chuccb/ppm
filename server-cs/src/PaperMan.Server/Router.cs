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
        // The account and channel listeners intentionally share a codec but
        // not an authority boundary. A client may only use 682 (and pong 101)
        // before it transitions to a fresh Channel-role TCP session.
        if (session.Role is ServerRole.Login
            && packet.Opcode is not Opcode.GT_PING_REQ and not Opcode.GL_LOGIN_REQ)
        {
            Console.WriteLine($"[s{session.Id}] !! rejected {packet.Opcode} on Login listener");
            return true;
        }

        if (session.Role is ServerRole.Channel && packet.Opcode is Opcode.GL_LOGIN_REQ)
        {
            Console.WriteLine($"[s{session.Id}] !! rejected GL_LOGIN_REQ on Channel listener");
            return true;
        }

        // The fresh channel socket carries no account state until its one 143
        // claim succeeds. The native CLobbyChannel wrapper nevertheless sends
        // 195 immediately after *every* delivered 144, including a rejected
        // 143. Permit only that non-authorizing request so ChannelHandlers can
        // return an explicit non-success 196; do not let the socket reach any
        // lobby, economy, social, or room handler merely because it connected
        // to the public channel port.
        if (session.Role is ServerRole.Channel
            && !session.Authenticated
            && packet.Opcode is not Opcode.GT_PING_REQ
            and not Opcode.PM_UDPSTART_REQ
            and not Opcode.GC_ENTERCHANNEL_REQ)
        {
            Console.WriteLine($"[s{session.Id}] !! rejected {packet.Opcode} before channel handoff");
            return true;
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
