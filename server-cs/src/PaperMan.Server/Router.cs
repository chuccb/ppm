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
        ChannelHandlers.Register(Add);
        VoiceHandlers.Register(Add);
        WarehouseHandlers.Register(Add);
        return new(table);
    }

    /// <summary>回傳 false = 無 handler (原版 default: return)。</summary>
    public async ValueTask<bool> DispatchAsync(Session session, Packet packet, ServerContext context)
    {
        if (!_table.TryGetValue(packet.OpcodeRaw, out var handler))
        {
            return false;
        }

        await handler(session, packet, context).ConfigureAwait(false);
        return true;
    }
}

public delegate void Registrar(Opcode opcode, PacketHandler handler);
