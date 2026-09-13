// =============================================================================
// PaperMan 私服入口 — C# 14 / .NET 10
// 用法: dotnet run --project src/PaperMan.Server -- [db路徑] [port] [aes]
//   aes 參數: 省略 = 客戶端原生金鑰 (EUC-KR「트렁크점령전머지」,
//   已自反編譯 sub_403430 完整還原並過測試向量);
//   "off"/"plain" = 明文模式 (自測/代理除錯); 或 32 位 hex 自訂金鑰。
// =============================================================================
using System.Net;
using System.Net.Sockets;
using PaperMan.Protocol;
using PaperMan.Server;

var dbPath = args.Length > 0 ? args[0] : Path.Combine("..", "..", "db", "paperman.db");
var config = ServerConfig.FromArgs(args);

using var db = new Db(dbPath);
var ctx = new ServerContext(db, config);
var router = Router.Build();

Console.WriteLine(
    $"""
     [paperman] handlers : {router.Count}
     [paperman] database : {dbPath}
     [paperman] aes      : {(config.AesKey is null ? "OFF (明文模式)" : "ON")}
     [paperman] compress : threshold 0x{config.CompressThreshold:X4}{(config.CompressThreshold >= PacketCodec.NeverCompress ? " (停用)" : "")}
     """);

var listener = new TcpListener(IPAddress.Parse(config.ListenHost), config.Port);
listener.Start();
Console.WriteLine($"[paperman] listening on {config.ListenHost}:{config.Port}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

long nextSessionId = 0;
while (!cts.IsCancellationRequested)
{
    TcpClient client;
    try
    {
        client = await listener.AcceptTcpClientAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    _ = RunSessionAsync(client, Interlocked.Increment(ref nextSessionId), cts.Token);
}

listener.Stop();
Console.WriteLine("[paperman] bye");
return;

async Task RunSessionAsync(TcpClient client, long sid, CancellationToken ct)
{
    // codec 為 per-session (壓縮門檻是 per-connection 協商值)
    using var codec = new PacketCodec(config.AesKey, config.CompressThreshold);
    using var session = new Session(client, codec, sid);
    Console.WriteLine($"[s{sid}] connect {session.Remote}");

    // 心跳: 伺服器主動發 102, client 以 101 回應 (sub_58D6F0; 方向十輪定案)
    using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    _ = PingLoopAsync(session, pingCts.Token);

    try
    {
        // 694 GL_ACCOUNTCONNSUCC = 「連上帳號伺服器」歡迎包 (十一輪定案):
        // client 的 694 handler 收下 u16 門檻後呼叫 sub_43DF00 → 送出 682
        // 登入 REQ。所以 694 必須在連線建立時發一次 (登入的觸發器),
        // 且登入成功後不可再發 (否則 client 再送 682 → 無限迴圈)。
        await session.SendAsync(new Packet(Opcode.GL_ACCOUNTCONNSUCC)
            .WriteU16(config.CompressThreshold), ct);

        await foreach (var packet in session.ReceiveAsync(ct))
        {
            db.LogPacket(packet.OpcodeRaw, rx: true, packet.Length);
            try
            {
                if (!await router.DispatchAsync(session, packet, ctx))
                {
                    Console.WriteLine($"[s{sid}] unhandled {packet}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[s{sid}] handler {packet.Opcode} error: {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        // 伺服器關閉中 — 靜默結束
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[s{sid}] session error: {ex.Message}");
    }
    finally
    {
        pingCts.Cancel();
    }

    Console.WriteLine($"[s{sid}] disconnect {session.Remote}");
}

// 30 秒一次的 102 GT_PING_ACK 心跳; client 收到即回 101 (sub_58D6F0)。
static async Task PingLoopAsync(Session session, CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    try
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            await session.SendAsync(new Packet(Opcode.GT_PING_ACK), ct);
        }
    }
    catch (Exception)
    {
        // 連線收攤 / 取消 — 心跳自然停止
    }
}
