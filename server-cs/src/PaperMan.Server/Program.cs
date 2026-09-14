// =============================================================================
// PaperMan 私服入口 — C# 14 / .NET 10
// Zero-configuration launch:
//   dotnet run --project server-cs/src/PaperMan.Server
//
// No command-line argument or prebuilt SQLite file is required. ServerDataPaths
// chooses the repository db/paperman.db during development (or data/paperman.db
// beside a published executable), while Db creates/migrates/seeds it on open.
// =============================================================================
using System.Net;
using System.Net.Sockets;
using PaperMan.Protocol;
using PaperMan.Server;

var config = new ServerConfig().Validate();
using var db = new Db(ServerDataPaths.GetDatabasePath());
var ctx = new ServerContext(db, config);
var router = Router.Build();

Console.WriteLine(
    $"""
     [paperman] handlers : {router.Count}
     [paperman] database : {db.DatabasePath} ({(db.Initialization.CreatedDatabaseFile ? "created" : "ready")}; {db.Initialization.ProtocolPacketDefinitionCount} protocol definitions)
     [paperman] aes      : {(config.AesKey is null ? "OFF (明文模式)" : "ON")}
     [paperman] compress : threshold 0x{config.EffectiveCompressionThreshold:X4}{(config.EffectiveCompressionThreshold >= PacketCodec.NeverCompress ? " (停用)" : "")}
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
    using var codec = new PacketCodec(config.AesKey, config.EffectiveCompressionThreshold);
    using var session = new Session(client, codec, sessionId, role);
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
            ServerRole.Channel => LoginWire.CreateTcpConnectionSuccess(),
            _ => LoginWire.CreateAccountConnectionSuccess(config.EffectiveCompressionThreshold),
        };
        Console.WriteLine($"[s{sessionId}] sending greeting handshake ({greeting.Opcode}) to {session.Remote}...");
        await session.SendAsync(greeting, cancellationToken);
        Console.WriteLine($"[s{sessionId}] greeting handshake sent, entering packet receive loop");

        await foreach (var packet in session.ReceiveAsync(cancellationToken))
        {
            db.LogPacket(packet.OpcodeRaw, isReceive: true, packet.Length);
            try
            {
                if (!await router.DispatchAsync(session, packet, ctx))
                {
                    Console.WriteLine($"[s{sessionId}] ?? unhandled packet {packet}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[s{sessionId}] !! handler exception for {packet.Opcode}: {ex}");
            }

            // 登入綁定暱稱後註冊進線上對照表 (191 GR_CALLUSER 反查目標連線)。
            // 冪等覆寫, 每包呼叫成本 O(1); 綁定前 (nick 空) 自動略過。
            if (session.Authenticated && session.Nickname.Length > 0)
            {
                ctx.Sessions.Register(session);
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"[s{sessionId}] session cancelled (server shutting down)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[s{sessionId}] !! session error: {ex}");
    }
    finally
    {
        pingCts.Cancel();
        ctx.Sessions.Unregister(session);

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
