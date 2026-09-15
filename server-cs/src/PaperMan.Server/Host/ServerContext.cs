// =============================================================================
// Process-wide dependencies and explicitly process-local state.
//
// A Session receives this container from Program. Durable state remains in Db;
// room/session/admission registries remain in State rather than in handlers.
// =============================================================================
namespace PaperMan.Server;

public sealed record ServerContext(Db Db, ServerConfig Config)
{
    /// <summary>全服房間表。</summary>
    public RoomManager Rooms { get; } = new();

    /// <summary>線上 session 對照表 (nick → Session; 191 呼出/跨房操作)。</summary>
    public SessionRegistry Sessions { get; } = new();

    /// <summary>
    /// 登入 TCP 與頻道 TCP 是不同連線；此表將 681 後的單次 143 claim
    /// 安全地交接到頻道 session。
    /// </summary>
    public ChannelAdmissionRegistry ChannelAdmissions { get; } = new();
}
