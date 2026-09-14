// =============================================================================
// PaperMan wire packet — 逐函數對應 PaperMan.exe.c 反編譯:
//
//   物件佈局 (size 19260, vftable @0xAEE4F8):
//     +0   vftable          +4   dispatch-done flag (u8)
//     +8   →w0 (this+24)    +12  →w1 (+26)   +16  →w2 (+28)   +20  →w3 (+30)
//     +24  8B header         +32  payload buffer (9592B 可用, 總 9600)
//     +9625 第二 9600B buffer (原始 payload 備份, sub_592C60 還原用)
//     +19228 備份長度        +19232 →payload 起點
//     +19236 read cursor     +19240 write cursor    +19244 →buffer 終點 (+9624)
//     +19248 total (w0+8)    +19252 stage flags (1=LZ, 2=解LZ, 4=AES, 8=解AES)
//     +19256 送出計數 (InterlockedIncrement, 守衛 w3 只設一次)
//
//   讀寫原語 (讀失敗回 0 不擲例外; 這裡改為擲例外 = server 端嚴格模式):
//     核心   sub_592580 (write raw) / sub_592500 (read raw, 雙重邊界檢查)
//     u8     sub_592920/592960 w, sub_592940/592980 r    s8  sub_5928E0/592900
//     u16    sub_5929A0 w, sub_592A00 r                  s16 sub_5929E0/5929C0
//     s32    sub_592A20 w, sub_592A40 r                  u32 sub_592A60/592A80
//     u64    sub_592AE0/592B00, 592B60/592B80 (兩對)     f32 sub_592B20/592B40
//     raw4   sub_592A60/592A80 or 592AC0 (semantic signedness from caller)
//     16B    sub_592C20 w, sub_592C40 r (GUID/hash 塊)
//     str    sub_5926F0 w (lstrlenA+1, 含 NUL), sub_592730 r
//     wstr   sub_592770 w (lstrlenW*2+2), sub_5927B0 r
//     內嵌   sub_5927F0 w / sub_592850 r: u16 opcode + u32 size + payload
//     len前綴 blob sub_592BA0 w / sub_592BE0 r: u16 len + bytes
//
// C# 14 / .NET 10。
// =============================================================================
using System.Buffers.Binary;
using System.Text;

namespace PaperMan.Protocol;

public sealed class Packet(Opcode opcode)
{
    /// <summary>payload 上限: buffer 9600 - header 8 (sub_591DA0)。</summary>
    public const int MaxPayload = 9592;
    public const int HeaderSize = 8;

    /// <summary>CP949 (韓服 ANSI, lstrlenA 語意)。初始化時註冊 CodePages provider
    /// (.NET 10 shared framework 內建, 無需 NuGet 套件)。</summary>
    public static readonly Encoding Ansi = CreateAnsi();

    private static Encoding CreateAnsi()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949);
    }

    private byte[] _buf = new byte[256];

    /// <summary>header word1 — dispatcher 以此 switch (sub_58B010)。</summary>
    public ushort OpcodeRaw { get; set; } = (ushort)opcode;

    public Opcode Opcode
    {
        get => (Opcode)OpcodeRaw;
        set => OpcodeRaw = (ushort)value;
    }

    /// <summary>header word2 — 僅 AES 層寫入 (加密前大小)。</summary>
    public ushort Word2 { get; init; }

    /// <summary>header word3 — 原始 payload 大小 (sub_591F90, 首次送出時寫入)。</summary>
    public ushort Word3 { get; init; }

    /// <summary>目前 payload 長度 (header word0)。</summary>
    public int Length { get; private set; }

    /// <summary>read cursor (this+19236 相對 +19232)。</summary>
    public int ReadPos { get; private set; }

    public int Remaining => Length - ReadPos;

    public ReadOnlySpan<byte> Payload => _buf.AsSpan(0, Length);

    /// <summary>codec 解出明文後建構 (對應 sub_591FB0 灌包)。</summary>
    internal static Packet FromWire(ushort opcode, ushort w2, ushort w3, ReadOnlySpan<byte> payload)
    {
        var p = new Packet((Opcode)opcode) { Word2 = w2, Word3 = w3 };
        p.SetPayload(payload);
        return p;
    }

    /// <summary>從原始 payload 建立可讀 Packet (測試/工具用)。</summary>
    public static Packet FromPayload(Opcode opcode, ReadOnlySpan<byte> payload)
    {
        var p = new Packet(opcode);
        p.SetPayload(payload);
        return p;
    }

    private void SetPayload(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaxPayload)
        {
            throw new ArgumentException($"payload {data.Length} exceeds {MaxPayload}");
        }

        if (data.Length > _buf.Length)
        {
            _buf = new byte[data.Length];
        }

        data.CopyTo(_buf);
        Length = data.Length;
        ReadPos = 0;
    }

    // ------------------------------------------------------------------ write
    private Span<byte> Grow(int byteCount)
    {
        // sub_592580: write cursor + n 不得超過 buffer 終點
        if (Length + byteCount > MaxPayload)
        {
            throw new InvalidOperationException($"payload would exceed {MaxPayload}");
        }

        if (Length + byteCount > _buf.Length)
        {
            Array.Resize(ref _buf, Math.Max(_buf.Length * 2, Length + byteCount));
        }

        var span = _buf.AsSpan(Length, byteCount);
        Length += byteCount;
        return span;
    }

    /// <summary>sub_592920: 單一 byte。</summary>
    public Packet WriteU8(byte value)
    {
        Grow(1)[0] = value;
        return this;
    }

    /// <summary>sub_5928E0: 帶號 byte。</summary>
    public Packet WriteS8(sbyte value)
    {
        Grow(1)[0] = unchecked((byte)value);
        return this;
    }

    /// <summary>sub_592900 讀端對應: 0/1 旗標。</summary>
    public Packet WriteBool(bool value) =>
        WriteU8(value ? (byte)1 : (byte)0);

    /// <summary>sub_5929A0: u16 little-endian。</summary>
    public Packet WriteU16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(Grow(2), value);
        return this;
    }

    /// <summary>sub_5929E0: s16 little-endian。</summary>
    public Packet WriteS16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(Grow(2), value);
        return this;
    }

    /// <summary>sub_592A60: u32 little-endian。</summary>
    public Packet WriteU32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(Grow(4), value);
        return this;
    }

    /// <summary>sub_592A20: s32 little-endian。</summary>
    public Packet WriteS32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(Grow(4), value);
        return this;
    }

    /// <summary>sub_592AE0: u64 little-endian。</summary>
    public Packet WriteU64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(Grow(8), value);
        return this;
    }

    /// <summary>sub_592B20: IEEE-754 單精度。</summary>
    public Packet WriteF32(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(Grow(4), value);
        return this;
    }

    /// <summary>sub_5926F0: NUL 結尾 ANSI (CP949) 字串, 無長度前綴。</summary>
    public Packet WriteStr(string s)
    {
        int n = Ansi.GetByteCount(s);
        var span = Grow(n + 1);
        Ansi.GetBytes(s, span);
        span[n] = 0;
        return this;
    }

    /// <summary>sub_592770: 雙 NUL 結尾 UTF-16LE 字串。</summary>
    public Packet WriteWStr(string s)
    {
        int n = Encoding.Unicode.GetByteCount(s);
        var span = Grow(n + 2);
        Encoding.Unicode.GetBytes(s, span);
        span[n] = 0;
        span[n + 1] = 0;
        return this;
    }

    public Packet WriteRaw(ReadOnlySpan<byte> data)
    {
        data.CopyTo(Grow(data.Length));
        return this;
    }

    /// <summary>WriteRaw 別名，供 byte 陣列/Span 寫入。</summary>
    public Packet WriteBytes(ReadOnlySpan<byte> data) => WriteRaw(data);

    /// <summary>sub_592BA0: u16 長度前綴 + raw bytes。</summary>
    public Packet WriteBlob(ReadOnlySpan<byte> data) =>
        WriteU16((ushort)data.Length).WriteRaw(data);

    /// <summary>sub_5927F0: 內嵌 packet = u16 opcode + u32 size + payload。</summary>
    public Packet WritePacket(Packet inner) =>
        WriteU16(inner.OpcodeRaw).WriteU32((uint)inner.Length).WriteRaw(inner.Payload);

    // ------------------------------------------------------------------- read
    private ReadOnlySpan<byte> Take(int byteCount)
    {
        // sub_592500: cursor+n 同時對 w0 與 buffer 終點做上限檢查
        if (ReadPos + byteCount > Length)
        {
            throw new EndOfStreamException($"read {byteCount} at {ReadPos}/{Length} (op={Opcode})");
        }

        var span = _buf.AsSpan(ReadPos, byteCount);
        ReadPos += byteCount;
        return span;
    }

    /// <summary>sub_592940。</summary>
    public byte ReadU8() =>
        Take(1)[0];

    /// <summary>sub_592900。</summary>
    public sbyte ReadS8() =>
        unchecked((sbyte)Take(1)[0]);

    /// <summary>u8 != 0。</summary>
    public bool ReadBool() =>
        Take(1)[0] != 0;

    /// <summary>sub_592A00。</summary>
    public ushort ReadU16() =>
        BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    /// <summary>sub_5929C0。</summary>
    public short ReadS16() =>
        BinaryPrimitives.ReadInt16LittleEndian(Take(2));

    /// <summary>sub_592A80。</summary>
    public uint ReadU32() =>
        BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    /// <summary>sub_592A40。</summary>
    public int ReadS32() =>
        BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    /// <summary>sub_592B00。</summary>
    public ulong ReadU64() =>
        BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

    /// <summary>sub_592B40。</summary>
    public float ReadF32() =>
        BinaryPrimitives.ReadSingleLittleEndian(Take(4));

    /// <summary>sub_592730: 讀到 NUL。maxBytes 對應客戶端定長 buffer。</summary>
    public string ReadStr(int maxBytes = MaxPayload)
    {
        if (Remaining <= 0)
        {
            return string.Empty;
        }

        int limit = Math.Min(Remaining, maxBytes);
        int end = Array.IndexOf(_buf, (byte)0, ReadPos, limit);
        if (end < 0)
        {
            var s = Ansi.GetString(_buf, ReadPos, limit);
            ReadPos += limit;
            return s.TrimEnd('\0');
        }

        var str = Ansi.GetString(_buf, ReadPos, end - ReadPos);
        ReadPos = end + 1;
        return str;
    }

    /// <summary>
    /// 嚴格讀取一個 NUL 結尾 ANSI 字串。用於原生 reader 寫入固定長度
    /// stack buffer 的協定欄位；缺 NUL 或內容超過 buffer 時立即拒絕，
    /// 不讓下一個欄位在錯位下繼續被解析。
    /// </summary>
    public string ReadNulTerminatedAnsiString(int maxContentBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxContentBytes);
        if (Remaining <= 0)
        {
            throw new EndOfStreamException($"missing NUL-terminated string at {ReadPos}/{Length} (op={Opcode})");
        }

        int scanLength = Math.Min(Remaining, checked(maxContentBytes + 1));
        int end = Array.IndexOf(_buf, (byte)0, ReadPos, scanLength);
        if (end < 0)
        {
            throw new InvalidDataException(
                $"unterminated ANSI string exceeds {maxContentBytes} byte(s) at {ReadPos}/{Length} (op={Opcode})");
        }

        var value = Ansi.GetString(_buf, ReadPos, end - ReadPos);
        ReadPos = end + 1;
        return value;
    }

    /// <summary>sub_5927B0: UTF-16LE 讀到雙 NUL。</summary>
    public string ReadWStr()
    {
        if (Remaining < 2)
        {
            ReadPos = Length;
            return string.Empty;
        }

        int i = ReadPos;
        while (i + 1 < Length && (_buf[i] != 0 || _buf[i + 1] != 0))
        {
            i += 2;
        }

        var s = Encoding.Unicode.GetString(_buf, ReadPos, i - ReadPos);
        ReadPos = Math.Min(i + 2, Length);
        return s;
    }

    public ReadOnlySpan<byte> ReadRaw(int n) => Take(n);

    /// <summary>sub_592BE0: u16 長度前綴 + raw bytes。</summary>
    public byte[] ReadBlob() => Take(ReadU16()).ToArray();

    /// <summary>sub_592850: 內嵌 packet。</summary>
    public Packet ReadPacket()
    {
        ushort op = ReadU16();
        int size = (int)ReadU32();
        return FromPayload((Opcode)op, Take(size));
    }

    public override string ToString() => $"<Packet {Opcode}({OpcodeRaw}) len={Length} rpos={ReadPos}>";
}
