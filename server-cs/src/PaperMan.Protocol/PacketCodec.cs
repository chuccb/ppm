// =============================================================================
// Wire codec — 傳送/接收管線, 逐函數對應反編譯:
//
//   送出 sub_555090 → sub_593280:
//     1) word3 := 原始大小 (sub_591F90, 僅首次)
//     2) 若 word0 ≥ n0x2580 門檻 → LZ 壓縮 (sub_592CE0→sub_592D30→sub_591600)
//        word2 := 壓縮前大小, flag|=1; 壓不小則放棄
//     3) 一律 AES-128 加密 (sub_592F60→sub_592FB0→sub_4042A0)
//        n16 = align16(word0) (空 payload 也補一塊), word2 := 加密前 word0,
//        word0 := n16, flag|=4
//     4) WSASend(header+payload, word0+8)
//
//   接收 sub_554E00/sub_555280:
//     1) 依 word0+8 切 frame (sub_591FB0 + sub_591D50 檢查)
//     2) AES 解密 (sub_5930C0→sub_593110→sub_404470):
//        word0 必須 16 對齊且 == align16(word2), word0 := word2, flag|=8
//     3) 若 word3 ≥ 門檻且 word0 < word3 → LZ 解壓 (sub_592E50→sub_592E90→sub_591900)
//        解壓後大小必須 == word3
//
//   ⚠ 死碼備註: sub_5923D0/sub_592420 (popcount+XOR "seal") 無任何呼叫者。
//   ⚠ n2_4 (AES 模式選擇) 於封包路徑無初始化 → ECB。
//   ⚠ n0x2580 門檻初始 9600 = 永不壓縮; 由 GL_ACCOUNTCONNSUCC(694) 之 u16 協商。
// =============================================================================
using System.Buffers.Binary;

namespace PaperMan.Protocol;

public sealed class PacketCodec(byte[]? aesKey = null, ushort compressThreshold = 0x2580)
{
    /// <summary>n0x2580 全域門檻。0x2580=9600 → 永不壓縮 (預設)。</summary>
    public ushort CompressThreshold { get; set; } = compressThreshold;

    /// <summary>AES-128 金鑰 (原版 .data 0xB69E88 的 16 bytes)。null = 明文模式 (私服可選)。</summary>
    public byte[]? AesKey { get; } = aesKey is { Length: 16 } ? aesKey :
        aesKey is null ? null : throw new ArgumentException("AES key must be 16 bytes");

    private readonly PaperAes? _aes = aesKey is { Length: 16 } ? new PaperAes(aesKey) : null;

    // ------------------------------------------------------------------- send
    /// <summary>組出完整 wire frame: [w0 size][w1 op][w2][w3] + processed payload。</summary>
    public byte[] Encode(Packet p)
    {
        var payload = p.Payload.ToArray();
        ushort w2 = 0;
        ushort w3 = (ushort)payload.Length;                    // sub_591F90

        // --- LZ 壓縮 (sub_592D30: 只有壓得更小才生效) ---
        if (CompressThreshold > 0 && payload.Length >= CompressThreshold)
        {
            var packed = PaperLz.Compress(payload);
            if (packed.Length < payload.Length)
            {
                w2 = (ushort)payload.Length;                   // **(WORD**)(this+16) = 原大小
                payload = packed;
            }
        }

        // --- AES (sub_592FB0: n16 上取 16 對齊, 空 payload 也補一塊) ---
        if (_aes is not null)
        {
            int n16 = payload.Length == 0 ? 16 : (payload.Length + 15) & ~15;
            if (n16 >= 0x2578) throw new InvalidOperationException("payload too large to encrypt");
            var block = new byte[n16];
            payload.CopyTo(block, 0);
            _aes.EncryptEcb(block);                            // sub_4042A0 (n2_4=0 → ECB)
            w2 = (ushort)payload.Length;                       // 加密前大小
            payload = block;
        }

        var frame = new byte[Packet.HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0), (ushort)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), p.OpcodeRaw);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), w2);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), w3);
        payload.CopyTo(frame.AsSpan(8));
        return frame;
    }

    // ------------------------------------------------------------------- recv
    /// <summary>frame 需要的總長度 = word0 + 8 (sub_555280 重組)。傳 -1 = header 未滿。</summary>
    public static int FrameLength(ReadOnlySpan<byte> buffer) =>
        buffer.Length < Packet.HeaderSize ? -1
        : BinaryPrimitives.ReadUInt16LittleEndian(buffer) + Packet.HeaderSize;

    /// <summary>解一個完整 frame → Packet。壞包擲例外 (原版行為: 丟棄+斷線)。</summary>
    public Packet Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < Packet.HeaderSize) throw new EndOfStreamException("short header");
        ushort w0 = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        ushort op = BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]);
        ushort w2 = BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]);
        ushort w3 = BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]);
        if (frame.Length < Packet.HeaderSize + w0) throw new EndOfStreamException("short payload");

        var payload = frame.Slice(Packet.HeaderSize, w0).ToArray();

        // --- AES 解密 (sub_593110 驗證: w0 必須 16 對齊且 == align16(w2)) ---
        if (_aes is not null)
        {
            int expect = w2 == 0 ? 16 : (w2 + 15) & ~15;
            if ((w0 & 0xF) != 0 || w0 != expect || w0 >= 0x2578)
                throw new InvalidDataException($"bad encrypted frame (w0={w0}, w2={w2})");
            _aes.DecryptEcb(payload);
            payload = payload[..w2];                           // word0 := word2
        }

        // --- LZ 解壓 (sub_592E50: w3 ≥ 門檻且目前大小 < w3) ---
        if (CompressThreshold > 0 && w3 >= CompressThreshold && payload.Length < w3)
        {
            var plain = PaperLz.Decompress(payload, w3);
            if (plain.Length != w3)
                throw new InvalidDataException($"lz size mismatch ({plain.Length} != {w3})");
            payload = plain;
        }

        var p = new Packet { OpcodeRaw = op, Word2 = w2, Word3 = w3 };
        p.SetPayload(payload);
        return p;
    }
}
