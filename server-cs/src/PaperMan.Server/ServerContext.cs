// =============================================================================
// 伺服器組態與共享狀態。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed record ServerContext(Db Db, ServerConfig Config);

public sealed record ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 40200;

    /// <summary>exe .data 0xB69E88 抽出的 16 bytes; null = 明文模式 (自測/代理)。</summary>
    public byte[]? AesKey { get; init; }

    /// <summary>GL_ACCOUNTCONNSUCC(694) 送出的壓縮門檻; 0x2580 = 停用壓縮。</summary>
    public ushort CompressThreshold { get; init; } = PacketCodec.NeverCompress;

    public string ServerName { get; init; } = "PaperMan Private";
    public string PublicHost { get; init; } = "127.0.0.1";

    public static ServerConfig FromArgs(string[] args) => new()
    {
        Port = args.Length > 1 ? int.Parse(args[1]) : 40200,
        AesKey = args.Length > 2 ? Convert.FromHexString(args[2]) : null,
    };
}

/// <summary>GL_LOGIN_ACK(681) result 碼 (0x43E651 分支)。</summary>
public enum LoginCode
{
    Ok = 1,
    BadCredentials = 2,
    Banned = 0xC8,          // 0xC8..0xD6 = 各種封鎖/維護碼
}
