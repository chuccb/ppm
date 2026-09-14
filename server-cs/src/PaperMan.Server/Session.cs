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

public sealed class Session(TcpClient client, PacketCodec codec, long id) : IDisposable
{
    public long Id { get; } = id;
    public string Remote { get; } = client.Client.RemoteEndPoint?.ToString() ?? "?";

    // 登入後綁定
    public long AccountId { get; set; }
    public long UserId { get; set; }
    public string Nickname { get; set; } = "";

    /// <summary>最後一次收到 101 GT_PING_REQ (client pong) 的時間。</summary>
    public DateTimeOffset LastPongAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>目前所在房號 (null = 大廳)。</summary>
    public byte? RoomNo { get; set; }

    /// <summary>目前所在房內槽位 (0..15, null = 未入座)。</summary>
    public byte? SlotNo { get; set; }

    public bool Authenticated => AccountId != 0;

    private readonly NetworkStream _stream = client.GetStream();
    private readonly byte[] _rxBuf = new byte[9600];          // 客戶端同款緩衝
    private int _rxLen;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    public async Task SendAsync(Packet packet, CancellationToken cancellationToken = default)
    {
        var frame = codec.Encode(packet);

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
                yield break;                                 // 對端斷線
            }

            if (n == 0)
            {
                yield break;                                 // 正常關閉
            }

            _rxLen += n;

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
                _rxLen = 0;                                  // 不可能的長度 → 整緩衝丟棄
                return null;
            }

            if (_rxLen < frameLen)
            {
                return null;                                 // 半包, 等更多資料 (sub_591D50)
            }

            Packet? pkt;
            try
            {
                pkt = codec.Decode(_rxBuf.AsSpan(0, frameLen));
            }
            catch
            {
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
