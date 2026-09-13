// =============================================================================
// PaperMan wire packet — 逐函數對應 PaperMan.exe.c 反編譯:
//   header 佈局      <- sub_591DA0 (init) / sub_591F00 (word0) / sub_591EE0 (word1)
//                       sub_5923B0 (word2) / sub_591F70 (word3)
//   write 原語       <- sub_592580 家族 (sub_592920 u8, sub_5929A0 u16, sub_592A20 s32,
//                       sub_592AE0 u64, sub_592B20 f32, sub_5926F0 str, sub_592770 wstr)
//   read 原語        <- sub_592500 家族 (sub_592940 u8, sub_592A00 u16, sub_592A40 s32,
//                       sub_592B00 u64, sub_592AC0 f32, sub_592730 str, sub_5927B0 wstr)
//   內嵌 packet      <- sub_5927F0 / sub_852850
// C# 14 / .NET 10。
// =============================================================================
using System.Buffers.Binary;
using System.Text;

namespace PaperMan.Protocol;

public sealed class Packet
{
    /// <summary>payload 上限: buffer 9600 - header 8 (sub_591DA0)。</summary>
    public const int MaxPayload = 9592;
    public const int HeaderSize = 8;

    // CP949 (韓服 ANSI 編碼, lstrlenA 語意)。
    // 注意: 不能用 static ctor + 欄位初始器 — 欄位初始器先於 static ctor 本體執行。
    public static readonly Encoding Ansi = CreateAnsi();

    private static Encoding CreateAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949);
    }

    private byte[] _buf = new byte[256];

    /// <summary>header word1 — dispatcher 以此 switch (sub_58B010)。</summary>
    public ushort OpcodeRaw { get; set; }

    public Opcode Opcode
    {
        get => (Opcode)OpcodeRaw;
        set => OpcodeRaw = (ushort)value;
    }

    /// <summary>header word2 — 壓縮前大小 (LZ) / 加密前大小 (AES)。</summary>
    public ushort Word2 { get; set; }

    /// <summary>header word3 — 原始 payload 大小 (sub_591F90 首次寫入)。</summary>
    public ushort Word3 { get; set; }

    /// <summary>目前 payload 長度 (header word0)。</summary>
    public int Length { get; private set; }

    /// <summary>read cursor (this+19236)。</summary>
    public int ReadPos { get; private set; }

    public Packet() { }

    public Packet(Opcode opcode) => Opcode = opcode;   // Packet::ctor_0 (0x591B40)

    public ReadOnlySpan<byte> Payload => _buf.AsSpan(0, Length);

    public int Remaining => Length - ReadPos;

    // ------------------------------------------------------------------ write
    private Span<byte> Grow(int n)
    {
        if (Length + n > MaxPayload)
            throw new InvalidOperationException($"payload would exceed {MaxPayload}");
        if (Length + n > _buf.Length)
            Array.Resize(ref _buf, Math.Max(_buf.Length * 2, Length + n));
        var span = _buf.AsSpan(Length, n);
        Length += n;
        return span;
    }

    public Packet WriteU8(byte v) { Grow(1)[0] = v; return this; }                     // sub_592920
    public Packet WriteS8(sbyte v) { Grow(1)[0] = unchecked((byte)v); return this; }   // sub_5928E0
    public Packet WriteBool(bool v) => WriteU8(v ? (byte)1 : (byte)0);

    public Packet WriteU16(ushort v) { BinaryPrimitives.WriteUInt16LittleEndian(Grow(2), v); return this; }  // sub_5929A0
    public Packet WriteS16(short v)  { BinaryPrimitives.WriteInt16LittleEndian(Grow(2), v);  return this; }  // sub_5929E0
    public Packet WriteU32(uint v)   { BinaryPrimitives.WriteUInt32LittleEndian(Grow(4), v); return this; }  // sub_592A60
    public Packet WriteS32(int v)    { BinaryPrimitives.WriteInt32LittleEndian(Grow(4), v);  return this; }  // sub_592A20
    public Packet WriteU64(ulong v)  { BinaryPrimitives.WriteUInt64LittleEndian(Grow(8), v); return this; }  // sub_592AE0
    public Packet WriteF32(float v)  { BinaryPrimitives.WriteSingleLittleEndian(Grow(4), v); return this; }  // sub_592B20

    /// <summary>sub_5926F0: ANSI 字串 + NUL, 無長度前綴。</summary>
    public Packet WriteStr(string s)
    {
        var bytes = Ansi.GetBytes(s);
        var span = Grow(bytes.Length + 1);
        bytes.CopyTo(span);
        span[^1] = 0;
        return this;
    }

    /// <summary>sub_592770: UTF-16LE 字串 + 雙 NUL。</summary>
    public Packet WriteWStr(string s)
    {
        var bytes = Encoding.Unicode.GetBytes(s);
        var span = Grow(bytes.Length + 2);
        bytes.CopyTo(span);
        span[^2] = 0;
        span[^1] = 0;
        return this;
    }

    public Packet WriteRaw(ReadOnlySpan<byte> data)
    {
        data.CopyTo(Grow(data.Length));
        return this;
    }

    /// <summary>sub_5927F0: 內嵌 packet = u16 opcode + u32 size + payload。</summary>
    public Packet WritePacket(Packet inner) =>
        WriteU16(inner.OpcodeRaw).WriteU32((uint)inner.Length).WriteRaw(inner.Payload);

    // ------------------------------------------------------------------- read
    private ReadOnlySpan<byte> Take(int n)
    {
        if (ReadPos + n > Length)
            throw new EndOfStreamException($"read {n} at {ReadPos}/{Length} (op={Opcode})");
        var span = _buf.AsSpan(ReadPos, n);
        ReadPos += n;
        return span;
    }

    public byte ReadU8() => Take(1)[0];                                                            // sub_592940
    public sbyte ReadS8() => unchecked((sbyte)Take(1)[0]);                                         // sub_592900
    public bool ReadBool() => Take(1)[0] != 0;
    public ushort ReadU16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));                   // sub_592A00
    public short ReadS16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));                     // sub_5929C0
    public uint ReadU32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));                     // sub_592A80
    public int ReadS32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));                       // sub_592A40
    public ulong ReadU64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));                    // sub_592B00
    public float ReadF32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));                    // sub_592AC0

    /// <summary>sub_592730: 讀到 NUL (lstrlenA 語意)。可設 maxBytes 對應客戶端定長 buffer。</summary>
    public string ReadStr(int maxBytes = MaxPayload)
    {
        int end = Array.IndexOf(_buf, (byte)0, ReadPos, Math.Min(Length - ReadPos, maxBytes));
        if (end < 0) throw new EndOfStreamException("unterminated string");
        var s = Ansi.GetString(_buf, ReadPos, end - ReadPos);
        ReadPos = end + 1;
        return s;
    }

    /// <summary>sub_5927B0: UTF-16LE 讀到雙 NUL。</summary>
    public string ReadWStr()
    {
        int i = ReadPos;
        while (i + 1 < Length && (_buf[i] != 0 || _buf[i + 1] != 0)) i += 2;
        var s = Encoding.Unicode.GetString(_buf, ReadPos, i - ReadPos);
        ReadPos = i + 2;
        return s;
    }

    public ReadOnlySpan<byte> ReadRaw(int n) => Take(n);

    // ------------------------------------------------------------------- misc
    /// <summary>從原始 payload 建立可讀 Packet (測試/工具用)。</summary>
    public static Packet FromPayload(Opcode opcode, ReadOnlySpan<byte> payload)
    {
        var p = new Packet(opcode);
        p.SetPayload(payload);
        return p;
    }

    /// <summary>codec 解出明文後回填 (內部用)。</summary>
    internal void SetPayload(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaxPayload) throw new ArgumentException("payload too large");
        if (data.Length > _buf.Length) _buf = new byte[data.Length];
        data.CopyTo(_buf);
        Length = data.Length;
        ReadPos = 0;
    }

    public override string ToString() => $"<Packet {Opcode}({OpcodeRaw}) len={Length} rpos={ReadPos}>";
}
