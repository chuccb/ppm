// =============================================================================
// 伺服器組態與共享狀態。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>
/// 伺服器角色 — 決定連線建立時送哪個握手包 (卅一輪, 使用者釐清):
///   Login   → 694 GL_ACCOUNTCONNSUCC (client 收到後送 682 登入)
///   Channel → 693 GL_TCPCONNSUCC     (client 收到後送 143 PM_UDPSTART)
/// </summary>
public enum ServerRole
{
    Login,
    Channel,
}

public sealed record ServerContext(Db Db, ServerConfig Config)
{
    /// <summary>全服房間表 (廿八輪)。</summary>
    public RoomManager Rooms { get; } = new();
}

public sealed record ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 40200;

    /// <summary>
    /// AES-128 金鑰。預設 = 客戶端硬編碼金鑰 (sub_403430 的 EUC-KR 字串
    /// 「트렁크점령전머지」, 新導出 C 檔完整還原, 過 FIPS-197 測試向量)。
    /// null = 明文模式 (自測/代理)。
    /// </summary>
    public byte[]? AesKey { get; init; } = PaperAes.DefaultKey.ToArray();

    /// <summary>GL_ACCOUNTCONNSUCC(694) 送出的壓縮門檻; 0x2580 = 停用壓縮。</summary>
    public ushort CompressThreshold { get; init; } = PacketCodec.NeverCompress;

    public string ServerName { get; init; } = "PaperMan Private";
    public string PublicHost { get; init; } = "127.0.0.1";

    /// <summary>本進程角色 (單機模式 = Login+Channel 合一, 兩個握手都由同一連線發)。</summary>
    public ServerRole Role { get; init; } = ServerRole.Login;

    /// <summary>單機合一模式: 登入成功後同連線直接當頻道伺服器用。</summary>
    public bool SingleProcessMode { get; init; } = true;

    public static ServerConfig FromArgs(string[] args) => new()
    {
        Port = args.Length > 1 ? int.Parse(args[1]) : 40200,
        AesKey = args.Length > 2 switch
        {
            true when args[2] is "off" or "plain" => null,   // 明文模式
            true => Convert.FromHexString(args[2]),          // 自訂金鑰
            false => PaperAes.DefaultKey.ToArray(),          // 客戶端原生金鑰
        },
    };
}

/// <summary>GL_LOGIN_ACK(681) result 碼 (0x43E651 分支)。</summary>
public enum LoginCode
{
    Ok = 1,
    BadCredentials = 2,
    Banned = 0xC8,          // 0xC8..0xD6 = 各種封鎖/維護碼
}
