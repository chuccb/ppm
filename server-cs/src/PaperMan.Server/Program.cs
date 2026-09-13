// =============================================================================
// PaperMan 私服入口 — C# 14 / .NET 10
// 用法: dotnet run --project src/PaperMan.Server -- [db路徑] [port] [aeskey十六進位32字元]
//   AES 金鑰須自原版 PaperMan.exe 的 .data 段 VA 0xB69E88 抽 16 bytes
//   (IDA 匯出的 .c 不含資料段)。未提供 → 明文模式 (供自測/代理除錯)。
// =============================================================================
using System.Net;
using System.Net.Sockets;
using PaperMan.Protocol;
using PaperMan.Server;

string dbPath = args.Length > 0 ? args[0] : Path.Combine("..", "..", "db", "paperman.db");
int port = args.Length > 1 ? int.Parse(args[1]) : 40200;
byte[]? aesKey = args.Length > 2 ? Convert.FromHexString(args[2]) : null;

var config = new ServerConfig { Port = port, AesKey = aesKey };
using var db = new Db(dbPath);
var ctx = new ServerContext(db, config);

// handler 表: 一般 + GP_CH*C 家族
var table = Handlers.Table;
StatHandlers.Register(table);
Console.WriteLine($"[paperman] handlers: {table.Count}, db: {dbPath}, " +
                  $"aes: {(aesKey is null ? "OFF (明文)" : "ON")}, " +
                  $"compress threshold: 0x{config.CompressThreshold:X4}");

var listener = new TcpListener(IPAddress.Parse(config.ListenHost), config.Port);
listener.Start();
Console.WriteLine($"[paperman] listening on {config.ListenHost}:{config.Port}");

long nextSessionId = 0;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.IsCancellationRequested)
{
    TcpClient client;
    try { client = await listener.AcceptTcpClientAsync(cts.Token); }
    catch (OperationCanceledException) { break; }

    long sid = Interlocked.Increment(ref nextSessionId);
    _ = HandleSessionAsync(client, sid, cts.Token);
}

listener.Stop();
Console.WriteLine("[paperman] bye");
return;

async Task HandleSessionAsync(TcpClient client, long sid, CancellationToken ct)
{
    // 每 session 一份 codec (壓縮門檻是 per-connection 協商值)
    var codec = new PacketCodec(config.AesKey, config.CompressThreshold);
    using var session = new Session(client, codec, sid);
    var remote = client.Client.RemoteEndPoint;
    Console.WriteLine($"[s{sid}] connect {remote}");

    try
    {
        await foreach (var pkt in session.ReceiveAsync(ct))
        {
            db.LogPacket(pkt.OpcodeRaw, rx: true, pkt.Length);
            if (table.TryGetValue(pkt.Opcode, out var handler))
            {
                try { await handler(session, pkt, ctx); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[s{sid}] handler {pkt.Opcode} error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[s{sid}] unhandled {pkt.Opcode}({pkt.OpcodeRaw}) len={pkt.Length}");
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        Console.WriteLine($"[s{sid}] session error: {ex.Message}");
    }
    Console.WriteLine($"[s{sid}] disconnect {remote}");
}
