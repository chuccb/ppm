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

// 雙 listener 架構 (卅一輪定案): client 登入後會「另開連線」到 681
// 指示的頻道 host:port — 單機模式用兩個 port 區分角色:
//   config.Port     → 登入伺服器 (握手 694 GL_ACCOUNTCONNSUCC)
//   config.Port + 1 → 頻道伺服器 (握手 693 GL_TCPCONNSUCC)
var loginListener = new TcpListener(IPAddress.Parse(config.ListenHost), config.Port);
var channelListener = new TcpListener(IPAddress.Parse(config.ListenHost), config.ChannelPort);
loginListener.Start();
channelListener.Start();
Console.WriteLine($"[paperman] login   server on {config.ListenHost}:{config.Port}");
Console.WriteLine($"[paperman] channel server on {config.ListenHost}:{config.ChannelPort}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

long nextSessionId = 0;

Task AcceptLoopAsync(TcpListener listener, ServerRole role) => Task.Run(async () =>
{
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

        _ = RunSessionAsync(client, Interlocked.Increment(ref nextSessionId), role, cts.Token);
    }
});

await Task.WhenAll(
    AcceptLoopAsync(loginListener, ServerRole.Login),
    AcceptLoopAsync(channelListener, ServerRole.Channel));

loginListener.Stop();
channelListener.Stop();
Console.WriteLine("[paperman] bye");
return;

async Task RunSessionAsync(TcpClient client, long sessionId, ServerRole role, CancellationToken cancellationToken)
{
    // codec 為 per-session (壓縮門檻是 per-connection 協商值)
    using var codec = new PacketCodec(config.AesKey, config.CompressThreshold);
    using var session = new Session(client, codec, sessionId);
    Console.WriteLine($"[s{sessionId}] connect {session.Remote} ({role})");

    // 心跳: 伺服器主動發 102, client 以 101 回應 (sub_58D6F0; 方向十輪定案)
    using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _ = PingLoopAsync(session, pingCts.Token);

    try
    {
        // 握手分流 (卅一輪, 使用者釐清 + 逐行證據):
        //   登入伺服器 → 694 GL_ACCOUNTCONNSUCC (u16 門檻; client 收到
        //     後呼叫 sub_43DF00 送 682 登入 — 十一輪定案)
        //   頻道伺服器 → 693 GL_TCPCONNSUCC (client 收到後顯示訊息 0xFF
        //     並呼叫 sub_555C60 送 143 PM_UDPSTART — 卅一輪 sub_57CAE0)
        // 兩者都只在連線建立時發一次, 之後不可重發 (會觸發 client 重跑握手)。
        var greeting = role switch
        {
            ServerRole.Channel => new Packet(Opcode.GL_TCPCONNSUCC),
            _ => new Packet(Opcode.GL_ACCOUNTCONNSUCC)
                    .WriteU16(config.CompressThreshold),
        };
        await session.SendAsync(greeting, cancellationToken);

        await foreach (var packet in session.ReceiveAsync(cancellationToken))
        {
            db.LogPacket(packet.OpcodeRaw, isReceive: true, packet.Length);
            try
            {
                if (!await router.DispatchAsync(session, packet, ctx))
                {
                    Console.WriteLine($"[s{sessionId}] unhandled {packet}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[s{sessionId}] handler {packet.Opcode} error: {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        // 伺服器關閉中 — 靜默結束
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[s{sessionId}] session error: {ex.Message}");
    }
    finally
    {
        pingCts.Cancel();

        // 斷線清理: 還在房內 → 與主動離房相同流程
        // (124 離房廣播 + 190 房主遷移 + 空房回收) — 防殭屍成員
        if (session.RoomNo is { } roomNo && ctx.Rooms.Find(roomNo) is { } room)
        {
            try
            {
                await ctx.Rooms.RemoveMemberAsync(room, session);
            }
            catch
            {
                // 清理失敗不影響斷線流程
            }
        }
    }

    Console.WriteLine($"[s{sessionId}] disconnect {session.Remote}");
}

// 30 秒一次的 102 GT_PING_ACK 心跳; client 收到即回 101 (sub_58D6F0)。
static async Task PingLoopAsync(Session session, CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await session.SendAsync(new Packet(Opcode.GT_PING_ACK), cancellationToken);
        }
    }
    catch (Exception)
    {
        // 連線收攤 / 取消 — 心跳自然停止
    }
}
