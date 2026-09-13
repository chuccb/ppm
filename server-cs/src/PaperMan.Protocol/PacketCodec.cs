// =============================================================================
// Wire codec — 傳送/接收管線, 逐函數對應反編譯:
//
//   送出 sub_555090 → sub_593280:
//     1) 首次送出時 word3 := word0 (sub_591F90; +19256 送出計數守衛)
//     2) 若 word0 ≥ n0x2580 門檻 → LZ 壓縮 (sub_592CE0→sub_592D30→sub_591600)
//        word3 := 壓縮前大小 (值即原始大小), word0 := 壓縮後, flag|=1; 壓不小放棄
//        ⚠ 壓縮層寫 word3 — word2 由且僅由 AES 層寫入
//     3) 一律 AES-128 加密 (sub_592F60→sub_592FB0→sub_4042A0)
//        n16 = align16(word0) (空 payload 也補一塊), n16≥0x2578 → 失敗
//        word2 := 加密前 word0, word0 := n16, flag|=4
//        加密失敗時原版客戶端直接 ExitProcess(0)
//     4) WSASend(header+payload, word0+8), 送出計數++ (sub_593260)
//
//   接收 sub_555280 (TCP) / sub_595A60 (UDP 同管線):
//     1) 依 word0+8 切 frame (sub_591FB0 + sub_591D50 檢查, 半包等待)
//     2) AES 解密 (sub_5930C0→sub_593110→sub_404470):
//        驗 word0≥16, word0==align16(word2), word0%16==0, word0<0x2578
//        word0 := word2, flag|=8
//     3) 若 word3 ≥ 門檻且 word0 < word3 (sub_592E50) → LZ 解壓
//        (sub_592E00→sub_592E90→sub_591900), 解壓後必須 ==word3 且 <0x2580
//     4) 失敗 → 原版丟棄整個累積緩衝
//
//   ⚠ 死碼: sub_5923D0/sub_592420 (popcount+XOR "seal") 無呼叫者, 不實作。
//   ⚠ n2_4 (AES 模式) 於封包路徑無初始化 → ECB。
//   ⚠ n0x2580 門檻初始 9600 = 永不壓縮; 由 GL_ACCOUNTCONNSUCC(694) u16 協商。
// =============================================================================
using System.Buffers.Binary;

namespace PaperMan.Protocol;

public sealed class PacketCodec(byte[]? aesKey = null, ushort compressThreshold = PacketCodec.NeverCompress) : IDisposable
{
    /// <summary>n0x2580 初始值: 門檻==buffer 大小 → 永不壓縮。</summary>
    public const ushort NeverCompress = 0x2580;

    /// <summary>sub_592FB0/593110 的加密大小上限。</summary>
    private const int MaxEncryptedSize = 0x2578;

    /// <summary>
    /// n0x2580 全域門檻 (per-connection, 由 694 協商)。
    /// 0 視同關閉 (原版 sub_593280 亦有 n0x2580&gt;0 檢查)。
    /// </summary>
    public ushort CompressThreshold
    {
        get;
        set => field = value == 0 ? NeverCompress : value;   // C# 14 field keyword
    } = compressThreshold == 0 ? NeverCompress : compressThreshold;

    /// <summary>AES-128 金鑰 (原生金鑰見 PaperAes.DefaultKey)。null = 明文模式 (自測/代理)。</summary>
    public bool Encrypted => _aes is not null;

    private readonly PaperAes? _aes = aesKey switch
    {
        null => null,
        { Length: 16 } => new PaperAes(aesKey),
        _ => throw new ArgumentException("AES key must be exactly 16 bytes", nameof(aesKey)),
    };

    public void Dispose() => _aes?.Dispose();

    private static int Align16(int n) => n == 0 ? 16 : (n + 15) & ~15;

    // ------------------------------------------------------------------- send
    /// <summary>組出完整 wire frame: [w0 size][w1 opcode][w2][w3] + payload。</summary>
    public byte[] Encode(Packet p)
    {
        ReadOnlySpan<byte> payload = p.Payload;
        ushort w2 = 0;
        ushort w3 = (ushort)payload.Length;                    // sub_591F90 (首次送出)

        // --- LZ (sub_592D30: 只有壓得更小才生效; 寫 w3, 不碰 w2) ---
        if (payload.Length >= CompressThreshold)
        {
            var packed = PaperLz.Compress(payload);
            if (packed.Length < payload.Length)
            {
                payload = packed;                              // w3 仍 = 原始大小
            }
        }

        // --- AES (sub_592FB0: 上取 16 對齊, 空 payload 也補一塊) ---
        if (_aes is not null)
        {
            int n16 = Align16(payload.Length);
            if (n16 >= MaxEncryptedSize)
            {
                throw new InvalidOperationException($"payload too large to encrypt ({n16} >= 0x2578)");
            }

            var block = new byte[n16];
            payload.CopyTo(block);
            _aes.EncryptEcb(block);                            // sub_4042A0 (n2_4=0 → ECB)
            w2 = (ushort)payload.Length;                       // ⚠ 唯一寫 w2 之處
            payload = block;
        }

        var frame = new byte[Packet.HeaderSize + payload.Length];
        var h = frame.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(h, (ushort)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(h[2..], p.OpcodeRaw);
        BinaryPrimitives.WriteUInt16LittleEndian(h[4..], w2);
        BinaryPrimitives.WriteUInt16LittleEndian(h[6..], w3);
        payload.CopyTo(h[Packet.HeaderSize..]);
        return frame;
    }

    // ------------------------------------------------------------------- recv
    /// <summary>frame 總長 = word0 + 8 (sub_555280 重組)。header 未滿傳 -1。</summary>
    public static int FrameLength(ReadOnlySpan<byte> buffer) =>
        buffer.Length < Packet.HeaderSize
            ? -1
            : BinaryPrimitives.ReadUInt16LittleEndian(buffer) + Packet.HeaderSize;

    /// <summary>解一個完整 frame → Packet。壞包擲例外 (原版行為: 丟棄整個緩衝)。</summary>
    public Packet Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < Packet.HeaderSize)
        {
            throw new EndOfStreamException("short header");
        }

        ushort w0 = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        ushort op = BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]);
        ushort w2 = BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]);
        ushort w3 = BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]);
        if (frame.Length < Packet.HeaderSize + w0)
        {
            throw new EndOfStreamException("short payload");
        }

        var payload = frame.Slice(Packet.HeaderSize, w0).ToArray();

        // --- AES 解密 (sub_593110 驗證群) ---
        if (_aes is not null)
        {
            bool valid = w0 >= 16 && (w0 & 0xF) == 0 && w0 == Align16(w2) && w0 < MaxEncryptedSize;
            if (!valid)
            {
                throw new InvalidDataException($"bad encrypted frame (w0={w0}, w2={w2})");
            }

            _aes.DecryptEcb(payload);
            payload = payload[..w2];                           // word0 := word2
        }

        // --- LZ 解壓 (sub_592E50: w3 ≥ 門檻且目前大小 < w3) ---
        if (w3 >= CompressThreshold && payload.Length < w3)
        {
            if (w3 >= NeverCompress)
            {
                throw new InvalidDataException($"decompressed size out of range ({w3})");
            }

            var plain = PaperLz.Decompress(payload, w3);
            if (plain.Length != w3)
            {
                throw new InvalidDataException($"lz size mismatch ({plain.Length} != {w3})");
            }

            payload = plain;
        }

        return Packet.FromWire(op, w2, w3, payload);
    }
}
