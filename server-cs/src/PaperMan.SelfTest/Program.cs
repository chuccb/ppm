// =============================================================================
// 自測 — 不需要遊戲客戶端即可驗證 codec 正確性:
//   1) Packet 讀寫原語 round-trip (含 CP949 字串)
//   2) PaperLz 壓縮/解壓 round-trip (含高重複與隨機資料)
//   3) PacketCodec 明文/AES/壓縮 三種管線 round-trip
//   4) header 欄位語意 (w0/w2/w3) 檢查
// 用法: dotnet run --project src/PaperMan.SelfTest
// =============================================================================
using System.Security.Cryptography;
using PaperMan.Protocol;

int pass = 0, fail = 0;

void Check(string name, bool ok)
{
    if (ok) { pass++; Console.WriteLine($"  ok  {name}"); }
    else { fail++; Console.WriteLine($"FAIL  {name}"); }
}

// ---- 1. Packet 原語 ---------------------------------------------------------
{
    var p = new Packet(Opcode.GL_LOGIN_ACK);
    p.WriteS32(1).WriteU8(0xAB).WriteU16(0xBEEF).WriteU64(0x1122334455667788UL)
     .WriteF32(3.5f).WriteStr("테스트abc").WriteWStr("寬字串W").WriteBool(true);

    var q = Packet.FromPayload(p.Opcode, p.Payload);

    Check("s32", q.ReadS32() == 1);
    Check("u8", q.ReadU8() == 0xAB);
    Check("u16", q.ReadU16() == 0xBEEF);
    Check("u64", q.ReadU64() == 0x1122334455667788UL);
    Check("f32", Math.Abs(q.ReadF32() - 3.5f) < 1e-6);
    Check("str(cp949)", q.ReadStr() == "테스트abc");
    Check("wstr", q.ReadWStr() == "寬字串W");
    Check("bool", q.ReadBool());
    Check("fully consumed", q.Remaining == 0);
}

// ---- 2. PaperLz -------------------------------------------------------------
{
    // 高重複 → 必壓縮
    var rep = new byte[4096];
    for (int i = 0; i < rep.Length; i++) rep[i] = (byte)(i % 7);
    var packed = PaperLz.Compress(rep);
    Check("lz compresses repetitive", packed.Length < rep.Length);
    var un = PaperLz.Decompress(packed, rep.Length);
    Check("lz roundtrip repetitive", un.AsSpan().SequenceEqual(rep));

    // 隨機 → 壓不小, 原樣返回
    var rnd = RandomNumberGenerator.GetBytes(4096);
    var packedR = PaperLz.Compress(rnd);
    Check("lz gives up on random", packedR.Length >= rnd.Length);

    // 文字類
    var text = Packet.Ansi.GetBytes(string.Concat(Enumerable.Repeat(
        "PaperMan private server LZ codec test 페이퍼맨 ", 80)));
    var packedT = PaperLz.Compress(text);
    var unT = PaperLz.Decompress(packedT, text.Length);
    Check("lz roundtrip text", unT.AsSpan().SequenceEqual(text));

    // 邊界: 距離 1 (RLE 型) 與最長 match 66
    var rle = new byte[300];
    Array.Fill(rle, (byte)0x41);
    var packedRle = PaperLz.Compress(rle);
    var unRle = PaperLz.Decompress(packedRle, rle.Length);
    Check("lz rle dist=1", unRle.AsSpan().SequenceEqual(rle));
}

// ---- 3. codec 管線 ----------------------------------------------------------
var key = RandomNumberGenerator.GetBytes(16);
foreach (var (label, codec) in new (string, PacketCodec)[]
{
    ("plain",        new PacketCodec(null, 0x2580)),
    ("aes",          new PacketCodec(key, 0x2580)),
    ("aes+compress", new PacketCodec(key, 64)),       // 門檻 64 → 大 payload 走壓縮
    ("compressOnly", new PacketCodec(null, 64)),
})
{
    // 空 payload (ping)
    var ping = new Packet(Opcode.GT_PING_REQ);
    var f0 = codec.Encode(ping);
    var d0 = codec.Decode(f0);
    Check($"{label}: empty payload", d0.Opcode == Opcode.GT_PING_REQ && d0.Length == 0);

    // 小 payload
    var small = new Packet(Opcode.GL_MYINFO_REQ).WriteS32(42).WriteStr("nick");
    var f1 = codec.Encode(small);
    var d1 = codec.Decode(f1);
    Check($"{label}: small payload", d1.ReadS32() == 42 && d1.ReadStr() == "nick");

    // 大且可壓縮 payload
    var big = new Packet(Opcode.GL_MYITEM_ACK);
    for (int i = 0; i < 500; i++) big.WriteS32(i % 3).WriteU16(7);
    var f2 = codec.Encode(big);
    var d2 = codec.Decode(f2);
    bool okBig = d2.Length == big.Length;
    for (int i = 0; okBig && i < 500; i++)
        okBig = d2.ReadS32() == i % 3 && d2.ReadU16() == 7;
    Check($"{label}: big payload roundtrip", okBig);

    // frame 長度語意
    Check($"{label}: FrameLength", PacketCodec.FrameLength(f2) == f2.Length);
}

// ---- 4. header 語意 ---------------------------------------------------------
{
    var codec = new PacketCodec(key, 0x2580);
    var p = new Packet(Opcode.GT_PING_ACK).WriteS32(123);   // 4 bytes
    var f = codec.Encode(p);
    ushort w0 = BitConverter.ToUInt16(f, 0);
    ushort w2 = BitConverter.ToUInt16(f, 4);
    ushort w3 = BitConverter.ToUInt16(f, 6);
    Check("w0 = align16(4) = 16", w0 == 16);
    Check("w2 = 加密前大小 4", w2 == 4);
    Check("w3 = 原始大小 4", w3 == 4);

    // 壞包必須擲例外 (原版 drop-all)
    f[8] ^= 0xFF;
    bool threw;
    try { var d = codec.Decode(f); threw = d.ReadS32() != 123; }
    catch { threw = true; }
    Check("tampered frame rejected/garbled", threw);
}

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
