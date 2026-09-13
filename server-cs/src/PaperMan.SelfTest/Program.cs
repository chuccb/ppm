// =============================================================================
// 自測 — 不需要遊戲客戶端即可驗證 codec 正確性:
//   1) Packet 讀寫原語 round-trip (含 CP949 / 寬字串 / blob / 內嵌 packet)
//   2) PaperLz 壓縮/解壓 round-trip (高重複、隨機、RLE、文字)
//   3) PacketCodec 明文/AES/壓縮 管線 round-trip
//   4) header 欄位語意 (w0/w2/w3) — w2 僅 AES 層寫, w3 = 原始大小
// 用法: dotnet run --project src/PaperMan.SelfTest
// =============================================================================
using System.Buffers.Binary;
using System.Security.Cryptography;
using PaperMan.Protocol;

int pass = 0, fail = 0;

void Check(string name, bool ok)
{
    if (ok)
    {
        pass++;
        Console.WriteLine($"  ok  {name}");
    }
    else
    {
        fail++;
        Console.WriteLine($"FAIL  {name}");
    }
}

// ---- 1. Packet 原語 ---------------------------------------------------------
{
    var inner = new Packet(Opcode.GT_PING_ACK).WriteS32(7);
    var p = new Packet(Opcode.GL_LOGIN_ACK)
        .WriteS32(1).WriteU8(0xAB).WriteU16(0xBEEF).WriteU64(0x1122334455667788UL)
        .WriteF32(3.5f).WriteStr("테스트abc").WriteWStr("寬字串W").WriteBool(true)
        .WriteBlob([1, 2, 3])
        .WritePacket(inner);

    var q = Packet.FromPayload(p.Opcode, p.Payload);
    Check("s32", q.ReadS32() == 1);
    Check("u8", q.ReadU8() == 0xAB);
    Check("u16", q.ReadU16() == 0xBEEF);
    Check("u64", q.ReadU64() == 0x1122334455667788UL);
    Check("f32", Math.Abs(q.ReadF32() - 3.5f) < 1e-6);
    Check("str(cp949)", q.ReadStr() == "테스트abc");
    Check("wstr", q.ReadWStr() == "寬字串W");
    Check("bool", q.ReadBool());
    Check("blob", q.ReadBlob() is [1, 2, 3]);
    var innerBack = q.ReadPacket();
    Check("embedded packet", innerBack.Opcode == Opcode.GT_PING_ACK && innerBack.ReadS32() == 7);
    Check("fully consumed", q.Remaining == 0);
}

// ---- 2. PaperLz -------------------------------------------------------------
{
    var rep = new byte[4096];
    for (int i = 0; i < rep.Length; i++)
    {
        rep[i] = (byte)(i % 7);
    }
    var packed = PaperLz.Compress(rep);
    Check("lz compresses repetitive", packed.Length < rep.Length);
    Check("lz roundtrip repetitive", PaperLz.Decompress(packed, rep.Length).AsSpan().SequenceEqual(rep));

    var rnd = RandomNumberGenerator.GetBytes(4096);
    Check("lz gives up on random", PaperLz.Compress(rnd).Length >= rnd.Length);

    var text = Packet.Ansi.GetBytes(string.Concat(
        Enumerable.Repeat("PaperMan private server LZ codec test 페이퍼맨 ", 80)));
    var packedT = PaperLz.Compress(text);
    Check("lz roundtrip text", PaperLz.Decompress(packedT, text.Length).AsSpan().SequenceEqual(text));

    var rle = new byte[300];
    Array.Fill(rle, (byte)0x41);
    var packedRle = PaperLz.Compress(rle);
    Check("lz rle dist=1", PaperLz.Decompress(packedRle, rle.Length).AsSpan().SequenceEqual(rle));
}

// ---- 3. codec 管線 ----------------------------------------------------------
var key = RandomNumberGenerator.GetBytes(16);
(string Label, PacketCodec Codec)[] codecs =
[
    ("plain",        new PacketCodec()),
    ("aes",          new PacketCodec(key)),
    ("aes+compress", new PacketCodec(key, compressThreshold: 64)),
    ("compressOnly", new PacketCodec(compressThreshold: 64)),
];

foreach (var (label, codec) in codecs)
{
    var ping = new Packet(Opcode.GT_PING_REQ);
    var decodedEmpty = codec.Decode(codec.Encode(ping));
    Check($"{label}: empty payload", decodedEmpty.Opcode == Opcode.GT_PING_REQ && decodedEmpty.Length == 0);

    var small = new Packet(Opcode.GL_MYINFO_REQ).WriteS32(42).WriteStr("nick");
    var decodedSmall = codec.Decode(codec.Encode(small));
    Check($"{label}: small payload", decodedSmall.ReadS32() == 42 && decodedSmall.ReadStr() == "nick");

    var big = new Packet(Opcode.GL_MYITEM_ACK);
    for (int i = 0; i < 500; i++)
    {
        big.WriteS32(i % 3).WriteU16(7);
    }
    var bigFrame = codec.Encode(big);
    var decodedBig = codec.Decode(bigFrame);
    bool okBig = decodedBig.Length == big.Length;
    for (int i = 0; okBig && i < 500; i++)
        okBig = decodedBig.ReadS32() == i % 3 && decodedBig.ReadU16() == 7;
    Check($"{label}: big payload roundtrip", okBig);
    Check($"{label}: FrameLength", PacketCodec.FrameLength(bigFrame) == bigFrame.Length);
}

// ---- 4. header 語意 ---------------------------------------------------------
{
    var (_, codec) = codecs[1];                              // aes, 門檻停用
    var frame = codec.Encode(new Packet(Opcode.GT_PING_ACK).WriteS32(123));  // 4B payload
    ushort w0 = BinaryPrimitives.ReadUInt16LittleEndian(frame);
    ushort w2 = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(4));
    ushort w3 = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(6));
    Check("w0 = align16(4) = 16", w0 == 16);
    Check("w2 = 加密前大小 4 (僅 AES 層寫)", w2 == 4);
    Check("w3 = 原始大小 4", w3 == 4);

    // 壓縮管線: w3 = 原始大小, w2 = 加密前(=壓縮後)大小
    var (_, compressingCodec) = codecs[2];                               // aes+compress, 門檻 64
    var big = new Packet(Opcode.GL_MYITEM_ACK);
    for (int i = 0; i < 300; i++)
    {
        big.WriteS32(1);
    }
    var compressedFrame = compressingCodec.Encode(big);
    ushort compressedWord2 = BinaryPrimitives.ReadUInt16LittleEndian(compressedFrame.AsSpan(4));
    ushort compressedWord3 = BinaryPrimitives.ReadUInt16LittleEndian(compressedFrame.AsSpan(6));
    Check("compressed: w3 = 原始 1200", compressedWord3 == 1200);
    Check("compressed: w2 < w3 (壓縮後)", compressedWord2 < compressedWord3);

    // 壞包必須擲例外或亂碼 (原版 drop-all)
    frame[8] ^= 0xFF;
    bool rejected;
    try
    {
        rejected = codec.Decode(frame).ReadS32() != 123;
    }
    catch
    {
        rejected = true;
    }

    Check("tampered frame rejected/garbled", rejected);
}

foreach (var (_, codec) in codecs)
{
    codec.Dispose();
}

// ---- 5. 客戶端原生 AES 金鑰測試向量 -----------------------------------------
// 金鑰 = sub_403430 的 EUC-KR 字串「트렁크점령전머지」;
// 期望值以獨立純 Python AES (過 FIPS-197 C.1) 生成, 三重交叉驗證。
{
    using var aes = new PaperAes(PaperAes.DefaultKey);

    byte[] block1 = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
    aes.EncryptEcb(block1);
    Check("native key: ECB(000102..0F)",
        Convert.ToHexString(block1) == "D7F8930CFE8758AD7BF2FEF759EBB845");

    byte[] block2 = "PaperMan-Packet!"u8.ToArray();
    aes.EncryptEcb(block2);
    Check("native key: ECB('PaperMan-Packet!')",
        Convert.ToHexString(block2) == "8B8ABD9B2B743448188ED7E554BD4AA2");

    aes.DecryptEcb(block2);
    Check("native key: decrypt roundtrip", block2.AsSpan().SequenceEqual("PaperMan-Packet!"u8));

    Check("native key bytes = EUC-KR 트렁크점령전머지",
        Convert.ToHexString(PaperAes.DefaultKey) == "C6AEB7B7C5A9C1A1B7C9C0FCB8D3C1F6");
}

// ---- 6. 黃金 frame 測試向量 (十三輪, 獨立 Python 第三方實作生成) --------
// Encode(GT_PING_ACK(102), payload = s32 123) 以原生金鑰必須逐 byte 等於:
//   header: w0=0010 op=0066 w2=0004 w3=0004 (LE)
//   body  : AES-128-ECB(00000-pad 至 16B)
{
    using var codec = new PacketCodec(PaperAes.DefaultKey.ToArray());
    var frame = codec.Encode(new Packet(Opcode.GT_PING_ACK).WriteS32(123));
    const string golden = "1000660004000400CDD0757BFFCBBB8B427D5AE5277A3F89";
    Check("golden frame: byte-exact vs 獨立實作", Convert.ToHexString(frame) == golden);

    var back = codec.Decode(Convert.FromHexString(golden));
    Check("golden frame: decode", back.Opcode == Opcode.GT_PING_ACK && back.ReadS32() == 123);
}

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
