// =============================================================================
// TCP session — 對應客戶端 recv 迴圈 (0x555000 附近):
//   * 9600-byte 累積緩衝
//   * frame = header.word0 + 8; 不足 → 等更多資料
//   * 解碼失敗 → 原版策略: 丟棄整個緩衝 (drop-all-on-error)
// 伺服器端行為採同樣框架, 但額外做逐 session 序列化送出。
// =============================================================================
using System.Net.Sockets;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed class Session(TcpClient client, PacketCodec codec, long id) : IDisposable
{
    public long Id { get; } = id;
    public TcpClient Client { get; } = client;

    // 登入後綁定
    public long AccountId { get; set; }
    public long UserId { get; set; }
    public string Nickname { get; set; } = "";
    public bool Authenticated => AccountId != 0;

    private readonly NetworkStream _stream = client.GetStream();
    private readonly byte[] _rxBuf = new byte[9600];      // 客戶端同款緩衝大小
    private int _rxLen;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly PacketCodec _codec = codec;

    public async Task SendAsync(Packet p, CancellationToken ct = default)
    {
        var frame = _codec.Encode(p);
        await _sendGate.WaitAsync(ct);
        try { await _stream.WriteAsync(frame, ct); }
        finally { _sendGate.Release(); }
    }

    /// <summary>讀 socket 並逐 frame 產出封包。連線終止時結束。</summary>
    public async IAsyncEnumerable<Packet> ReceiveAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            int n;
            try { n = await _stream.ReadAsync(_rxBuf.AsMemory(_rxLen), ct); }
            catch (IOException) { yield break; }
            if (n == 0) yield break;
            _rxLen += n;

            while (_rxLen >= Packet.HeaderSize)
            {
                int frameLen = PacketCodec.FrameLength(_rxBuf.AsSpan(0, _rxLen));
                if (frameLen > _rxBuf.Length) { _rxLen = 0; break; }    // 不可能的長度 → 丟棄
                if (_rxLen < frameLen) break;                            // 半包 → 等待

                Packet? pkt;
                try { pkt = _codec.Decode(_rxBuf.AsSpan(0, frameLen)); }
                catch (Exception)
                {
                    _rxLen = 0;                                          // 原版: 解不開 → 清空緩衝
                    break;
                }

                Buffer.BlockCopy(_rxBuf, frameLen, _rxBuf, 0, _rxLen - frameLen);
                _rxLen -= frameLen;
                if (pkt is not null) yield return pkt;
            }
        }
    }

    public void Dispose()
    {
        _sendGate.Dispose();
        _stream.Dispose();
        Client.Dispose();
    }
}
