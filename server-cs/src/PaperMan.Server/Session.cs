// =============================================================================
// TCP session — 對應客戶端 recv 迴圈 sub_555280:
//   * 9600-byte 累積緩衝, frame = header.word0 + 8
//   * 半包 → 等更多資料 (sub_591D50 檢查)
//   * 解碼失敗 → 原版策略: 丟棄整個緩衝 (drop-all-on-error)
// 送出以 SemaphoreSlim 逐 session 序列化 (對應原版 send 臨界區)。
// =============================================================================
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed class Session(TcpClient client, PacketCodec codec, long id, ServerRole role) : IDisposable
{
    public long Id { get; } = id;
    public ServerRole Role { get; } = role;
    public string Remote { get; } = client.Client.RemoteEndPoint?.ToString() ?? "?";
    public string RemoteIp => (client.Client.RemoteEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "<unknown>";

    // 登入後綁定。LoginName 是資料庫帳號；Nickname 是後續房間/好友協定
    // 使用的玩家名稱。143 的 native String[24] 寫入者尚未證實，故不可把
    // 它推定為任一欄位或作為 admission lookup key。
    public long AccountId { get; internal set; }
    public long UserId { get; internal set; }
    public string LoginName { get; internal set; } = "";
    public string Nickname { get; internal set; } = "";

    /// <summary>最後一次收到 101 GT_PING_REQ (client pong) 的時間。</summary>
    public DateTimeOffset LastPongAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>目前所在房號 (null = 大廳)。</summary>
    public byte? RoomNo { get; set; }

    /// <summary>目前所在房內槽位 (0..15, null = 未入座)。</summary>
    public byte? SlotNo { get; set; }

    public bool Authenticated => AccountId != 0;

    /// <summary>Applies one verified login or login-to-channel handoff atomically.</summary>
    internal void BindAuthentication(long accountId, long userId, string loginName, string nickname)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        AccountId = accountId;
        UserId = userId;
        LoginName = loginName;
        Nickname = nickname;
    }

    private readonly NetworkStream _stream = client.GetStream();
    private readonly byte[] _rxBuf = new byte[9600];          // 客戶端同款緩衝
    private int _rxLen;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    public async Task SendAsync(Packet packet, CancellationToken cancellationToken = default)
    {
        var frame = codec.Encode(packet);
        Console.WriteLine($"[s{Id}] >> SEND {packet.Opcode}({packet.OpcodeRaw}) payload={packet.Length}B frame={frame.Length}B");

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>讀 socket 並逐 frame 產出封包; 連線關閉時自然結束。</summary>
    public async IAsyncEnumerable<Packet> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int n;
            try
            {
                n = await _stream.ReadAsync(_rxBuf.AsMemory(_rxLen), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or SocketException)
            {
                Console.WriteLine($"[s{Id}] connection terminated by remote: {e.Message}");
                yield break;                                 // 對端斷線
            }

            if (n == 0)
            {
                Console.WriteLine($"[s{Id}] connection gracefully closed by remote (EOF)");
                yield break;                                 // 正常關閉
            }

            _rxLen += n;
            Console.WriteLine($"[s{Id}] received {n} bytes from socket (buffered total: {_rxLen}B)");

            while (TryTakeFrame() is { } pkt)
            {
                yield return pkt;
            }
        }
    }

    /// <summary>切出並解碼一個 frame; 半包或緩衝已清空時傳 null。</summary>
    private Packet? TryTakeFrame()
    {
        while (_rxLen >= Packet.HeaderSize)
        {
            int frameLen = PacketCodec.FrameLength(_rxBuf.AsSpan(0, _rxLen));

            if (frameLen > _rxBuf.Length)
            {
                Console.WriteLine($"[s{Id}] !! INVALID frame length {frameLen} > {_rxBuf.Length}, dropping buffer");
                _rxLen = 0;                                  // 不可能的長度 → 整緩衝丟棄
                return null;
            }

            if (_rxLen < frameLen)
            {
                Console.WriteLine($"[s{Id}] partial frame ({_rxLen}/{frameLen}B), waiting for more data");
                return null;                                 // 半包, 等更多資料 (sub_591D50)
            }

            ushort w0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_rxBuf.AsSpan(0, 2));
            ushort op = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_rxBuf.AsSpan(2, 2));
            ushort w2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_rxBuf.AsSpan(4, 2));
            ushort w3 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_rxBuf.AsSpan(6, 2));
            var rawHex = Convert.ToHexString(_rxBuf.AsSpan(0, frameLen));
            Console.WriteLine($"[s{Id}] raw frame header: w0={w0}(payloadLen), op={op}(0x{op:X4}/{(Opcode)op}), w2={w2}(encOrigLen), w3={w3}(origLen), totalFrame={frameLen}B");
            Console.WriteLine($"[s{Id}] raw wire bytes: {rawHex}");

            Packet? pkt;
            try
            {
                pkt = codec.Decode(_rxBuf.AsSpan(0, frameLen));
                var hexSnippet = Convert.ToHexString(pkt.Payload);
                Console.WriteLine($"[s{Id}] << RECV {pkt.Opcode}({pkt.OpcodeRaw}) payload={pkt.Length}B hex=[{hexSnippet}]");
            }
            catch (Exception ex)
            {
                var hex = Convert.ToHexString(_rxBuf.AsSpan(0, Math.Min(frameLen, 48)));
                Console.WriteLine($"[s{Id}] !! DECODE ERROR: {ex.Message} (frame {frameLen}B: [{hex}{(frameLen > 48 ? "..." : "")}])");
                _rxLen = 0;                                  // 原版: 解不開 → 清空緩衝
                return null;
            }

            _rxBuf.AsSpan(frameLen, _rxLen - frameLen).CopyTo(_rxBuf);   // memmove 剩餘
            _rxLen -= frameLen;
            return pkt;
        }

        return null;
    }

    public void Dispose()
    {
        _sendGate.Dispose();
        _stream.Dispose();
        client.Dispose();
    }
}
