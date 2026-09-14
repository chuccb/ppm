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

    // 測試空字串與無結尾字串的安全讀取
    var emptyPkt = Packet.FromPayload(Opcode.GL_LOGIN_REQ, []);
    Check("empty payload ReadStr returns empty", emptyPkt.ReadStr() == string.Empty);
    Check("empty payload ReadWStr returns empty", emptyPkt.ReadWStr() == string.Empty);

    var rawStrPkt = Packet.FromPayload(Opcode.GL_LOGIN_REQ, "UserNoNul"u8);
    Check("non-nul terminated ReadStr returns text safely", rawStrPkt.ReadStr() == "UserNoNul");
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

// ---- 7. 語音封包簇與 sub_885D00 wire 格式 round-trip (本輪新增) -------------
// 驗證 sub_885D00 (85B: s16 base1, s16 base2, 27×{s16, u8}),
// 792 單人 (86B: u8 char + 85B), 794 全量 (1 + 15×86 = 1291B),
// 795 變體 A/B, 796 ACK, 378/379 RadioMsg, 114 / 269 負載尾塊。
{
    // (a) sub_885D00 85B 語音塊
    var voiceP = new Packet(Opcode.GL_ENTERROOM_ACK);
    voiceP.WriteS16(10).WriteS16(20);                       // base1, base2
    for (int i = 0; i < 27; i++)
    {
        voiceP.WriteS16((short)(100 + i)).WriteU8((byte)(1 + (i % 9)));
    }
    Check("sub_885D00 block length == 85B", voiceP.Length == 85);
    var vReader = Packet.FromPayload(voiceP.Opcode, voiceP.Payload);
    Check("voice base1 == 10", vReader.ReadS16() == 10);
    Check("voice base2 == 20", vReader.ReadS16() == 20);
    bool slotsOk = true;
    for (int i = 0; i < 27; i++)
    {
        short item = vReader.ReadS16();
        byte flag = vReader.ReadU8();
        if (item != 100 + i || flag != 1 + (i % 9))
            slotsOk = false;
    }
    Check("voice 27 slots roundtrip", slotsOk && vReader.Remaining == 0);

    // (b) 792 GL_VOICEITEMSLOT_ACK (86B)
    var p792 = new Packet(Opcode.GL_VOICEITEMSLOT_ACK).WriteU8(3); // char_idx = 3 (lich)
    p792.WriteS16(1).WriteS16(2);
    for (int i = 0; i < 27; i++) p792.WriteS16(0).WriteU8(0);
    Check("792 payload == 86B", p792.Length == 86);
    var r792 = Packet.FromPayload(p792.Opcode, p792.Payload);
    Check("792 char_idx == 3", r792.ReadU8() == 3);

    // (c) 794 GI_VOICEITEMSLOT_ALL_ACK (1 + 15×86 = 1291B)
    var p794 = new Packet(Opcode.GI_VOICEITEMSLOT_ALL_ACK).WriteU8(15);
    for (byte c = 0; c < 15; c++)
    {
        p794.WriteU8(c).WriteS16(c).WriteS16((short)(c * 2));
        for (int i = 0; i < 27; i++) p794.WriteS16(0).WriteU8(0);
    }
    Check("794 payload == 1291B", p794.Length == 1 + 15 * 86);

    // (d) 795 變體 A (單角色差分)
    var p795A = new Packet(Opcode.GI_CHANGE_VOICEITEMSLOT_REQ);
    p795A.WriteU8(0)                                        // char_idx = 0 (maru)
         .WriteBool(true)                                   // base_changed = true
         .WriteS16(5).WriteS16(6)                           // base1, base2
         .WriteU8(1).WriteU8(1).WriteS16(101).WriteU8(1)    // category 0: 1 slot (slot 1, item 101, flag 1)
         .WriteU8(0)                                        // category 1: 0 slots
         .WriteU8(0);                                       // category 2: 0 slots
    Check("795 variant A size < 117B", p795A.Length <= 117);

    // (e) 795 變體 B (20×全量, 1780B)
    var p795B = new Packet(Opcode.GI_CHANGE_VOICEITEMSLOT_REQ);
    for (int c = 0; c < 20; c++)
    {
        p795B.WriteS32(c).WriteS16(0).WriteS16(0);
        for (int i = 0; i < 27; i++) p795B.WriteS16(0).WriteU8(0);
    }
    Check("795 variant B size == 1780B", p795B.Length == 20 * (4 + 4 + 27 * 3));

    // (f) 378/379 RadioMsg
    var p378 = new Packet(Opcode.GR_RADIOMSG_REQ)
        .WriteU8(1)                                         // team
        .WriteU8(4)                                         // face
        .WriteU8(2)                                         // slot
        .WriteU8(5)                                         // wchar len
        .WriteWStr("Roger");
    Check("378 RadioMsg wire", p378.Length == 4 + 2 * 5 + 2);
}

// ---- 8. 系統 / 角色 / 商城 / 任務 / 投票新封包 wire 格式 round-trip ---------
{
    // 685/686 Tutorial
    var p686 = new Packet(Opcode.GL_TUTORIALINDEX_ACK).WriteS32(5);
    Check("686 Tutorial ACK size == 4", p686.Length == 4 && p686.ReadS32() == 5);

    // 704/705 Level Kill Limit
    var p705 = new Packet(Opcode.GL_LEVEL_KILL_LIMIT_ACK).WriteS32(50).WriteF32(1.0f).WriteS32(30);
    Check("705 Level Limit ACK size == 12", p705.Length == 12 && p705.ReadS32() == 50);

    // 706/707 Bill Token
    var p707 = new Packet(Opcode.GL_BILLTOKEN_ACK).WriteStr("TOKEN");
    Check("707 Bill Token wire", p707.ReadStr() == "TOKEN");

    // 370/371 Change Channel
    var p371 = new Packet(Opcode.GL_CHANGECHANNEL_ACK).WriteU8(1).WriteU8(2).WriteStr("127.0.0.1").WriteS32(10000).WriteU8(0);
    Check("371 Change Channel status == 1", p371.ReadU8() == 1 && p371.ReadU8() == 2 && p371.ReadStr() == "127.0.0.1");

    // 131/132 Force Out (Kick)
    var p132 = new Packet(Opcode.GR_FORCEOUT_ACK).WriteU8(1).WriteU8(3);
    Check("132 Forceout ACK", p132.ReadU8() == 1 && p132.ReadU8() == 3);

    // 718-722 Voting
    var p720 = new Packet(Opcode.GR_START_VOTING).WriteS32(2).WriteS32(1).WriteS32(0).WriteS32(30).WriteU8(0);
    Check("720 Start Voting broadcast", p720.Length == 17 && p720.ReadS32() == 2);

    // 423/424 Msg Read
    var p424 = new Packet(Opcode.GL_MSG_READ_ACK).WriteU8(1).WriteStr("101");
    Check("424 Msg Read ACK", p424.ReadU8() == 1 && p424.ReadStr() == "101");

    // 453/454 Delete Gift
    var p454 = new Packet(Opcode.GS_DELETEGIFT_ACK).WriteU8(1).WriteS32(10).WriteS32(20);
    Check("454 Delete Gift ACK", p454.ReadU8() == 1 && p454.ReadS32() == 10);

    // 802/803 Destroy Item
    var p803 = new Packet(Opcode.GS_DESTROYITEM_ACK).WriteU8(0).WriteU8(0).WriteS32(1000).WriteS32(500).WriteU8(1).WriteS32(5).WriteS32(0);
    Check("803 Destroy Item ACK", p803.ReadU8() == 0 && p803.ReadU8() == 0);

    // 876/877 Daily Quest
    var p877 = new Packet(Opcode.GQ_QUEST_ACCEPT_DAILY_ACK).WriteU8(0).WriteS32(0);
    Check("877 Daily Quest ACK", p877.ReadU8() == 0 && p877.ReadS32() == 0);

    // 698/699 Pepachi Enter
    var p699 = new Packet(Opcode.GP_ENTER_PEPACHI_ACK).WriteU8(1).WriteS32(100).WriteS32(50);
    Check("699 Pepachi Enter ACK", p699.ReadU8() == 1 && p699.ReadS32() == 100);

    // 900/901 Capsule Machine Start
    var p901 = new Packet(Opcode.GS_CAPSULEMACHINE_START_ACK).WriteU8(1).WriteS32(1001).WriteS32(99);
    Check("901 Capsule Machine ACK", p901.ReadU8() == 1 && p901.ReadS32() == 1001);
}

// ---- 9. GM / MASTER、GameCenter、AI 模式 wire 格式 round-trip --------------
{
    // 275/276 MASTER_MEMO
    var p276 = new Packet(Opcode.MASTER_MEMO_ACK).WriteWStr("Server Notice");
    Check("276 Memo ACK wire", p276.ReadWStr() == "Server Notice");

    // 277/278 MASTER_MEMOALL
    var p278 = new Packet(Opcode.MASTER_MEMOALL_ACK).WriteWStr("Broadcast");
    Check("278 MemoAll ACK wire", p278.ReadWStr() == "Broadcast");

    // 285/286 MASTER_MSET
    var p286 = new Packet(Opcode.MASTER_MSET_ACK).WriteU8(1);
    Check("286 MSet ACK wire", p286.ReadU8() == 1);

    // 394/395 MASTER_ROOMINFO
    var p395 = new Packet(Opcode.MASTER_ROOMINFO_ACK).WriteU8(1).WriteU8(0).WriteStr("Alice").WriteStr("127.0.0.1");
    Check("395 RoomInfo ACK wire", p395.ReadU8() == 1 && p395.ReadU8() == 0 && p395.ReadStr() == "Alice");

    // 402/403 MASTER_EVENTPAGE
    var p403 = new Packet(Opcode.MASTER_EVENTPAGE_ACK).WriteF32(2.0f);
    Check("403 EventPage ACK wire", Math.Abs(p403.ReadF32() - 2.0f) < 0.001f);

    // 883/884 MASTER_FIND_USER
    var p884 = new Packet(Opcode.MASTER_FIND_USER_ACK).WriteU8(1).WriteS32(1001).WriteStr("Bob").WriteU8(0).WriteU8(5);
    Check("884 FindUser ACK wire", p884.ReadU8() == 1 && p884.ReadS32() == 1001 && p884.ReadStr() == "Bob");

    // 472/473 GameCenter Rec
    var p473 = new Packet(Opcode.GL_GAMECENTER_REC_ACK).WriteS16(1).WriteS32(9999).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteS16(0).WriteS32(0).WriteBytes(new byte[16]).WriteU8(0);
    Check("473 GameCenter Rec ACK wire", p473.ReadS16() == 1 && p473.ReadS32() == 9999);

    // 474/475 GameCenter Start
    var p475 = new Packet(Opcode.GG_GAMECENTER_GAME_START_ACK).WriteU8(1).WriteS16(1).WriteU8(2);
    Check("475 GameCenter Start ACK wire", p475.ReadU8() == 1 && p475.ReadS16() == 1 && p475.ReadU8() == 2);

    // 485/486 Progress Time
    var p486 = new Packet(Opcode.GL_GET_GAMEROOM_PROGRESSTIME_ACK).WriteU8(0).WriteS16(7).WriteU8(0).WriteS32(60).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0);
    Check("486 Progress Time ACK wire", p486.ReadU8() == 0 && p486.ReadS16() == 7 && p486.ReadS32() == 60);

    // 918/919 AI Get Reward
    var p919 = new Packet(Opcode.GR_AI_GET_REWARD_ITEM_ACK).WriteU8(0).WriteU8(0).WriteS32(10001).WriteU8(0).WriteS32(1).WriteU8(0);
    Check("919 AI Reward ACK wire", p919.ReadU8() == 0 && p919.ReadU8() == 0 && p919.ReadS32() == 10001);

    // 922/923 AI Damage Shield
    var p923 = new Packet(Opcode.GR_AI_DAMAGE_SHIELD_ACK).WriteS16(1).WriteS16(50).WriteS16(950).WriteF32(1.0f);
    Check("923 AI Damage Shield ACK wire", p923.ReadS16() == 1 && p923.ReadS16() == 50);

    // 928/929 AI Continue
    var p929 = new Packet(Opcode.GR_AI_CONTINUE_START_ACK).WriteU8(1).WriteS32(2);
    Check("929 AI Continue ACK wire", p929.ReadU8() == 1 && p929.ReadS32() == 2);

    // 935/936 AI Fever
    var p936 = new Packet(Opcode.GR_AI_FEVER_START_ACK).WriteU8(1).WriteU8(0).WriteS32(10000).WriteU8(1);
    Check("936 AI Fever ACK wire", p936.ReadU8() == 1 && p936.ReadS32() == 10000);

    // 939/940 AI Go Next Wave
    var p940 = new Packet(Opcode.GR_AI_GO_NEXT_WAVE_ACK).WriteU8(2).WriteS32(0);
    Check("940 AI Next Wave ACK wire", p940.ReadU8() == 2);

    // 944/945 Reset Game Room Slot
    var p945 = new Packet(Opcode.GR_RESET_GAMEROOMSLOT_ACK).WriteU8(1);
    Check("945 Reset Slot ACK wire", p945.ReadU8() == 1);
}

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
