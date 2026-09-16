// =============================================================================
// 自測 — 不需要遊戲客戶端即可驗證 codec 正確性:
//   1) Packet 讀寫原語 round-trip (含 CP949 / 寬字串 / blob / 內嵌 packet)
//   2) PaperLz 壓縮/解壓 round-trip (高重複、隨機、RLE、文字)
//   3) PacketCodec 明文/AES/壓縮 管線 round-trip
//   4) header 欄位語意 (w0/w2/w3) — w2 僅 AES 層寫, w3 = 原始大小
//   5) UDP-private 19 -> empty 20 AES-only datagram and source-reply contract
//   6) 681/682/693/694 and 141/142/143/144/195/196 bootstrap wire contracts
//   6) 681→143 source-IP / one-use admission rules
//   7) zero-argument SQLite bootstrap, account upgrades, and legacy migration
// 用法: dotnet run --project src/PaperMan.SelfTest
// =============================================================================
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;
using PaperMan.Server;

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

SqliteConnection OpenExistingSqlite(string databasePath)
{
    var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWrite,
        ForeignKeys = true,
        Pooling = false,
    }.ConnectionString);
    connection.Open();
    return connection;
}

async Task<(TcpClient ServerClient, TcpClient PeerClient)> CreateConnectedTcpClientsAsync()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    var peerClient = new TcpClient();
    try
    {
        var listenerEndpoint = (IPEndPoint)listener.LocalEndpoint;
        Task<TcpClient> acceptedClient = listener.AcceptTcpClientAsync();
        await peerClient.ConnectAsync(IPAddress.Loopback, listenerEndpoint.Port);
        return (await acceptedClient, peerClient);
    }
    catch
    {
        peerClient.Dispose();
        throw;
    }
}

async Task<Packet> ReadServerPacketAsync(NetworkStream stream, PacketCodec codec)
{
    byte[] header = new byte[Packet.HeaderSize];
    await stream.ReadExactlyAsync(header);

    int frameLength = PacketCodec.FrameLength(header);
    byte[] frame = new byte[frameLength];
    header.CopyTo(frame, 0);
    if (frameLength > header.Length)
    {
        await stream.ReadExactlyAsync(frame.AsMemory(header.Length));
    }

    return codec.Decode(frame);
}

int GetAvailableIpv4UdpPort()
{
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    if (socket.LocalEndPoint is not IPEndPoint endpoint)
    {
        throw new InvalidOperationException("The UDP test socket has no IPv4 endpoint.");
    }

    return endpoint.Port;
}

bool IsNative198StarterAcknowledgement(Packet acknowledgement)
{
    var reader = Packet.FromPayload(acknowledgement.Opcode, acknowledgement.Payload);
    if (acknowledgement.Opcode != Opcode.GL_MYINFO_ACK
        || !reader.ReadBool()
        || reader.ReadS32() != 7
        || reader.ReadStr() != "Starter")
    {
        return false;
    }

    // sub_523BF0: +88 is a CHARSLOT list position, followed by the complete
    // stat/basic block and the separately read +4 current list position.
    if (reader.ReadU8() != 0)
    {
        return false;
    }

    for (int i = 0; i < 3 + 5 + 4 + 4 + 5; i++)
    {
        if (reader.ReadS32() != 0)
        {
            return false;
        }
    }

    if (reader.ReadU8() != 0 || reader.ReadU8() != 0 || reader.ReadU8() != 0
        || reader.ReadS32() != 0 || reader.ReadS32() != 0 || reader.ReadS32() != 0)
    {
        return false;
    }

    for (int i = 0; i < 48; i++)
    {
        if (reader.ReadU8() != 0)
        {
            return false;
        }
    }

    if (reader.ReadU8() != 0 || reader.ReadU8() != 1 || reader.ReadU8() != 1)
    {
        return false;
    }

    for (int i = 0; i < 12; i++)
    {
        if (reader.ReadU16() != (i < 6 ? 1 : 0))
        {
            return false;
        }
    }

    if (reader.ReadU8() != 4)
    {
        return false;
    }

    for (byte group = 0; group < 4; group++)
    {
        if (reader.ReadU8() != group || reader.ReadU16() != 0)
        {
            return false;
        }

        if (group != 3
            && (reader.ReadU16() != 0 || reader.ReadU16() != 0 || reader.ReadU16() != 0))
        {
            return false;
        }
    }

    for (int i = 0; i < 9; i++)
    {
        if (reader.ReadS32() != 0)
        {
            return false;
        }
    }

    if (reader.ReadU8() != 5)
    {
        return false;
    }

    for (int i = 0; i < 7; i++)
    {
        if (reader.ReadS32() != 0)
        {
            return false;
        }
    }

    return reader.ReadU16() == 0
        && reader.ReadS32() == 0
        && reader.ReadU8() == 0
        && reader.Remaining == 0;
}

bool IsNative311FailureAcknowledgement(Packet acknowledgement)
{
    var reader = Packet.FromPayload(acknowledgement.Opcode, acknowledgement.Payload);
    return acknowledgement.Opcode == Opcode.GS_BUYCHAR_ACK
        && !reader.ReadBool()
        && reader.ReadU8() == 0
        && reader.ReadS32() == 0
        && reader.Remaining == 0;
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

// ---- 1a. NewSkill profile wire contract -----------------------------------
{
    var updateRequest = new Packet(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ)
        .WriteU8(3)
        .WriteU8(0xA5) // sub_5738A0 branches on any nonzero raw byte
        .WriteU8(1)
        .WriteS32(11010001)
        .WriteS32(11020001)
        .WriteS32(11030001)
        .WriteS32(11040001)
        .WriteS32(11050001)
        .WriteS32(11060001)
        .WriteS32(11060002);
    NewSkillProfileChange parsedUpdate = NewSkillProfileWire.ReadChangeRequest(
        Packet.FromPayload(updateRequest.Opcode, updateRequest.Payload));
    Check("466 has target, conditional previous index, and exactly seven s32 ids",
        updateRequest.Length == NewSkillProfileWire.ChangeWithUpdateByteCount
        && parsedUpdate is
        {
            TargetProfile: 3,
            PreviousProfileUpdateRaw: 0xA5,
            HasPreviousProfileUpdate: true,
            PreviousProfile: 1,
            PreviousProfilePuzzleItemIds: [11010001, 11020001, 11030001, 11040001, 11050001, 11060001, 11060002],
        });

    bool rejectsMalformedNewSkillChange;
    try
    {
        _ = NewSkillProfileWire.ReadChangeRequest(
            new Packet(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ).WriteU8(0).WriteU8(0).WriteU8(0));
        rejectsMalformedNewSkillChange = false;
    }
    catch (InvalidDataException)
    {
        rejectsMalformedNewSkillChange = true;
    }
    Check("466 rejects a trailing block when its raw update byte is zero", rejectsMalformedNewSkillChange);

    bool rejectsMissingNonzeroUpdateBlock;
    try
    {
        _ = NewSkillProfileWire.ReadChangeRequest(
            new Packet(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ).WriteU8(0).WriteU8(0x7F));
        rejectsMissingNonzeroUpdateBlock = false;
    }
    catch (InvalidDataException)
    {
        rejectsMissingNonzeroUpdateBlock = true;
    }
    Check("466 requires the complete 31-byte variant for every nonzero raw update byte",
        rejectsMissingNonzeroUpdateBlock);

    NewSkillProfileRecord[] profiles = Enumerable.Range(0, NewSkillProfileWire.ProfileCount)
        .Select(profile => new NewSkillProfileRecord(
            Enumerable.Range(0, NewSkillProfileWire.PuzzleSlotCount)
                .Select(slot => profile * 100 + slot)
                .ToArray(),
            0x65000000 + profile))
        .ToArray();
    Packet inventoryEnter = NewSkillProfileWire.CreateInventoryEnterAcknowledgement(
        userId: 7,
        requestContextRaw: 0xD2,
        selectedProfile: 2,
        profiles: profiles);
    var snapshotReader = Packet.FromPayload(inventoryEnter.Opcode, inventoryEnter.Payload);
    bool snapshotLayout = inventoryEnter.Length == 168
        && snapshotReader.ReadU8() == NewSkillProfileWire.SelfSnapshotMode
        && snapshotReader.ReadS32() == 7
        && snapshotReader.ReadU8() == 0xD2
        && snapshotReader.ReadU8() == 0
        && snapshotReader.ReadU8() == 2;
    for (int profile = 0; snapshotLayout && profile < profiles.Length; profile++)
    {
        for (int slot = 0; slot < NewSkillProfileWire.PuzzleSlotCount; slot++)
        {
            snapshotLayout &= snapshotReader.ReadS32() == profiles[profile].PuzzleItemIds[slot];
        }

        snapshotLayout &= snapshotReader.ReadS32() == profiles[profile].ExpiresAtPackedMinute;
    }
    Check("255 mode-1 self snapshot contains selected index plus five raw32 profiles",
        snapshotLayout && snapshotReader.Remaining == 0);

    Packet changeAcknowledgement = NewSkillProfileWire.CreateChangeAcknowledgement(
        resultRaw: 0,
        unknownHeaderRaw: 9,
        profileIndex: 2,
        profile: profiles[2]);
    var changeReader = Packet.FromPayload(changeAcknowledgement.Opcode, changeAcknowledgement.Payload);
    bool changeLayout = changeAcknowledgement.Length == 36
        && changeReader.ReadU8() == 0
        && changeReader.ReadU8() == 9
        && changeReader.ReadU8() == 1
        && changeReader.ReadU8() == 2;
    for (int slot = 0; changeLayout && slot < NewSkillProfileWire.PuzzleSlotCount; slot++)
    {
        changeLayout &= changeReader.ReadS32() == profiles[2].PuzzleItemIds[slot];
    }
    Check("467 carries a real stored raw32 record instead of a zero placeholder",
        changeLayout
        && changeReader.ReadS32() == profiles[2].ExpiresAtPackedMinute
        && changeReader.Remaining == 0);
}

// ---- 1b. 198 starter character availability contract ----------------------
{
    var starterStats = new Db.Stats(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0);
    var starterInfo = new Db.MyInfo(7, "Starter", 1, 0, 0, 0, 0, starterStats);
    Packet myInfo = LobbyHandlers.CreateGL_MYINFO_ACK(
        starterInfo,
        [new Db.CharSlot(0, 1, [1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0])],
        [],
        new Db.Slots(new int[9], new int[7]),
        giftCount: 0);

    Check("198 native-order starter has selected slot zero and canonical body one",
        IsNative198StarterAcknowledgement(myInfo));

    Packet defensiveFallback = LobbyHandlers.CreateGL_MYINFO_ACK(
        starterInfo,
        [],
        [],
        new Db.Slots(new int[9], new int[7]),
        giftCount: 0);
    Check("198 no-row formatter fallback remains natively playable",
        IsNative198StarterAcknowledgement(defensiveFallback));
}

// ---- 1c. Login / channel bootstrap wire contract ---------------------------
{
    const uint revision = 0x1234ABCD;
    ulong obfuscatedDataRevision = ((ulong)(revision ^ 0xB1A9D7C7u) << 32) | 0xF1E1AB0Eu;
    var loginRequestPacket = new Packet(Opcode.GL_LOGIN_REQ)
        .WriteStr("Alpha9@")
        .WriteStr("not-logged")
        .WriteU64(obfuscatedDataRevision)
        .WriteU8(2)
        .WriteRaw(new byte[24]);
    var loginRequest = LoginWire.ReadRequest(Packet.FromPayload(loginRequestPacket.Opcode, loginRequestPacket.Payload));
    Check("682 exact request fields", loginRequest.AccountName == "Alpha9@"
        && loginRequest.PasswordOrToken == "not-logged"
        && loginRequest.FingerprintSource == LoginFingerprintSource.StorageSerial
        && loginRequest.Fingerprint.Length == 24);
    Check("682 data revision decode", LoginWire.TryDecodeDataRevision(loginRequest.ObfuscatedDataRevision, out uint decodedDataRevision)
        && decodedDataRevision == revision);
    Check("682 captured client revision decode", LoginWire.TryDecodeDataRevision(
        0x81FEBE90F1E1AB0EUL,
        out uint capturedDataRevision)
        && capturedDataRevision == 0x30576957u);
    Check("682 revision low-word guard", !LoginWire.TryDecodeDataRevision(obfuscatedDataRevision ^ 1, out _));

    var groups = new LoginChannelGroup[]
    {
        new LoginChannelGroup(100, new LoginChannelEntry(0, "Normal", 0, 0)),
        new LoginChannelGroup(0, null),
        new LoginChannelGroup(100, new LoginChannelEntry(3, "AI", 0, 9, TypeThreeExtension: 0x7E)),
    };
    var loginAck = LoginWire.CreateAcknowledgement(new LoginAcknowledgement(
        ResultCode: 1,
        Success: new LoginAcknowledgementSuccess(
            UserId: 77,
            BillingUiMode: 101,
            FeatureExtension: new LoginFeatureExtension(0x11111111, -7, FeatureFlag: 0xA5),
            Servers:
            [
                new LoginServerEntry(-2, "Private", "127.0.0.1", 40201, 4, -3, groups),
            ],
            Billing: new LoginBillingMetadata(unchecked((int)0x89ABCDEF), 0x10203040))));

    // This reader intentionally follows CLobbyLogin::sub_43E500, including
    // its exactly-three groups and one-record-only group interpretation.
    var nativeReader = Packet.FromPayload(loginAck.Opcode, loginAck.Payload);
    bool native681Layout = nativeReader.ReadS32() == 1
        && nativeReader.ReadS32() == 77
        && nativeReader.ReadS32() == 101
        && nativeReader.ReadS32() == 1
        && nativeReader.ReadS32() == 0x11111111
        && nativeReader.ReadS32() == -7
        && nativeReader.ReadU8() == 0xA5
        && nativeReader.ReadS16() == 1
        && nativeReader.ReadS16() == -2
        && nativeReader.ReadNulTerminatedAnsiString(49) == "Private"
        && nativeReader.ReadNulTerminatedAnsiString(15) == "127.0.0.1"
        && nativeReader.ReadS16() == unchecked((short)40201)
        && nativeReader.ReadU8() == 4
        && nativeReader.ReadS16() == -3;

    // Group 0: one normal channel. Group 1: empty. Group 2: one type-3
    // channel, whose extra trailing byte is mandatory.
    native681Layout &= nativeReader.ReadS16() == 100
        && nativeReader.ReadU8() == 0
        && nativeReader.ReadNulTerminatedAnsiString(49) == "Normal"
        && nativeReader.ReadS16() == 0
        && nativeReader.ReadU8() == 0
        && nativeReader.ReadS16() == 0
        && nativeReader.ReadS16() == 100
        && nativeReader.ReadU8() == 3
        && nativeReader.ReadNulTerminatedAnsiString(49) == "AI"
        && nativeReader.ReadS16() == 0
        && nativeReader.ReadU8() == 9
        && nativeReader.ReadU8() == 0x7E
        && nativeReader.ReadS32() == unchecked((int)0x89ABCDEF)
        && nativeReader.ReadS32() == 0x10203040
        && nativeReader.Remaining == 0;
    Check("681 success exact native reader order", native681Layout);

    var failureAck = LoginWire.CreateAcknowledgement(new LoginAcknowledgement((int)2));
    var failureReader = Packet.FromPayload(failureAck.Opcode, failureAck.Payload);
    Check("681 failure is result word only", failureAck.Length == 4
        && failureReader.ReadS32() == 2
        && failureReader.Remaining == 0);

    var accountGreeting = LoginWire.CreateAccountConnectionSuccess(0x2580);
    var accountGreetingReader = Packet.FromPayload(accountGreeting.Opcode, accountGreeting.Payload);
    Check("694 is one u16 threshold", accountGreeting.Opcode == Opcode.GL_ACCOUNTCONNSUCC
        && accountGreetingReader.ReadU16() == 0x2580
        && accountGreetingReader.Remaining == 0);
    Check("693 is empty", LoginWire.CreateTcpConnectionSuccess().Opcode == Opcode.GL_TCPCONNSUCC
        && LoginWire.CreateTcpConnectionSuccess().Length == 0);

    // sub_5565D0 decodes the final 142 word as calendar bit fields, not as
    // opaque configuration. Its channel byte becomes the active channel index.
    var calendarTime = new PmConnectCalendarTime(2026, 9, 15, 14, 23);
    var pmConnectAck = ChannelBootstrapWire.CreatePmConnectAcknowledgement(
        new ChannelEndpoint("198.51.100.42", 40202),
        channelIndex: 0,
        calendarTime: calendarTime);
    var pmConnectReader = Packet.FromPayload(pmConnectAck.Opcode, pmConnectAck.Payload);
    uint expectedCalendarWord = (26u << 24) | (9u << 19) | (15u << 13) | (14u << 7) | 23u;
    Check("142 endpoint, active channel, and packed calendar", pmConnectReader.ReadNulTerminatedAnsiString(19) == "198.51.100.42"
        && pmConnectReader.ReadS32() == 40202
        && pmConnectReader.ReadU8() == 0
        && pmConnectReader.ReadU32() == expectedCalendarWord
        && PmConnectCalendarTime.FromWireValue(expectedCalendarWord) == calendarTime
        && pmConnectReader.Remaining == 0);

    // sub_555D50 always reads 144's mandatory fields and, when flag66 is set,
    // exactly four u8 values plus eight raw dwords for sNetCafeInfo.
    var udpStartAck = ChannelBootstrapWire.CreateUdpStartAcknowledgement(new UdpStartAcknowledgement(
        UdpStartResult.Success,
        RankRestrictedServerFlag: 1,
        DailyLoginRewardPoints: 25,
        ChannelName: "Ch.1",
        ReservedValueAfterChannelNameOne: 11,
        ReservedValueAfterChannelNameTwo: 12,
        ChannelRestrictionLevel: 13,
        ChannelRestrictionKdr: 1.5f,
        ClientRequestContextValue: 0xAABBCCDD,
        NetCafeInfo: new NetCafeBootstrapInfo(1, 2, 3, 4, [10, 20, 30, 40, 50, 60, 70, 80])));
    var udpStartReader = Packet.FromPayload(udpStartAck.Opcode, udpStartAck.Payload);
    bool native144Layout = udpStartReader.ReadU8() == (byte)UdpStartResult.Success
        && udpStartReader.ReadU8() == 1
        && udpStartReader.ReadS32() == 25
        && udpStartReader.ReadNulTerminatedAnsiString(39) == "Ch.1"
        && udpStartReader.ReadS32() == 11
        && udpStartReader.ReadS32() == 12
        && udpStartReader.ReadS32() == 13
        && Math.Abs(udpStartReader.ReadF32() - 1.5f) < 1e-6
        && udpStartReader.ReadU32() == 0xAABBCCDD
        && udpStartReader.ReadU8() == 1
        && udpStartReader.ReadU8() == 1
        && udpStartReader.ReadU8() == 2
        && udpStartReader.ReadU8() == 3
        && udpStartReader.ReadU8() == 4;
    for (int slot = 1; slot <= 8; slot++)
    {
        native144Layout &= udpStartReader.ReadS32() == slot * 10;
    }
    Check("144 complete optional net-café shape", native144Layout && udpStartReader.Remaining == 0);

    // The 196 reader consumes its endpoint tail only when result == 1; its
    // third prefix byte becomes the selected channel index.
    var enterChannelAck = ChannelBootstrapWire.CreateEnterChannelAcknowledgement(new EnterChannelAcknowledgement(
        EnterChannelResult.Success,
        ChannelId: 77,
        ChannelIndex: 0,
        Endpoint: new ChannelEndpoint("198.51.100.42", 40202),
        EndpointOpaqueByte: 0xA1,
        ChannelType: 0,
        ClientFlags: 1,
        ClientDefaultValue: 5));
    var enterChannelReader = Packet.FromPayload(enterChannelAck.Opcode, enterChannelAck.Payload);
    Check("196 successful full native shape", enterChannelReader.ReadU8() == (byte)EnterChannelResult.Success
        && enterChannelReader.ReadS32() == 77
        && enterChannelReader.ReadU8() == 0
        && enterChannelReader.ReadNulTerminatedAnsiString(19) == "198.51.100.42"
        && enterChannelReader.ReadS32() == 40202
        && enterChannelReader.ReadU8() == 0xA1
        && enterChannelReader.ReadU8() == 0
        && enterChannelReader.ReadU32() == 1
        && enterChannelReader.ReadU8() == 5
        && enterChannelReader.Remaining == 0);

    var rejectedEnterChannelAck = ChannelBootstrapWire.CreateEnterChannelAcknowledgement(
        new EnterChannelAcknowledgement(EnterChannelResult.GenericError4, ChannelId: 77, ChannelIndex: 0));
    var rejectedEnterChannelReader = Packet.FromPayload(
        rejectedEnterChannelAck.Opcode,
        rejectedEnterChannelAck.Payload);
    Check("196 rejection has prefix and no success tail", rejectedEnterChannelAck.Length == 6
        && rejectedEnterChannelReader.ReadU8() == (byte)EnterChannelResult.GenericError4
        && rejectedEnterChannelReader.ReadS32() == 77
        && rejectedEnterChannelReader.ReadU8() == 0
        && rejectedEnterChannelReader.Remaining == 0);

    bool rejectedEndpointOn196Failure = false;
    try
    {
        ChannelBootstrapWire.CreateEnterChannelAcknowledgement(new EnterChannelAcknowledgement(
            EnterChannelResult.GenericError4,
            ChannelId: 77,
            ChannelIndex: 0,
            Endpoint: new ChannelEndpoint("198.51.100.42", 40202)));
    }
    catch (ArgumentException)
    {
        rejectedEndpointOn196Failure = true;
    }
    Check("196 rejects an impossible endpoint tail on failure", rejectedEndpointOn196Failure);

    bool rejectedNullNetCafeSlots = false;
    bool rejectedWrongSizedNetCafeSlots = false;
    try
    {
        ChannelBootstrapWire.CreateUdpStartAcknowledgement(new UdpStartAcknowledgement(
            UdpStartResult.Success,
            0,
            0,
            "Ch.1",
            0,
            0,
            0,
            0,
            0,
            // Intentionally crosses the non-nullable public boundary to verify
            // that the wire-contract validator rejects malformed external data.
            new NetCafeBootstrapInfo(0, 0, 0, 0, null!)));
    }
    catch (ArgumentNullException)
    {
        rejectedNullNetCafeSlots = true;
    }

    try
    {
        ChannelBootstrapWire.CreateUdpStartAcknowledgement(new UdpStartAcknowledgement(
            UdpStartResult.Success,
            0,
            0,
            "Ch.1",
            0,
            0,
            0,
            0,
            0,
            new NetCafeBootstrapInfo(0, 0, 0, 0, [1, 2, 3])));
    }
    catch (ArgumentException)
    {
        rejectedWrongSizedNetCafeSlots = true;
    }
    Check("144 rejects null net-café slots", rejectedNullNetCafeSlots);
    Check("144 rejects non-eight net-café slots", rejectedWrongSizedNetCafeSlots);

    bool rejectedInvalidConfiguredNetCafeSlots = false;
    try
    {
        new ServerConfig
        {
            UdpStartMetadata = UdpStartAcknowledgementMetadata.Neutral with
            {
                NetCafeInfo = new NetCafeBootstrapInfo(0, 0, 0, 0, [1, 2, 3]),
            },
        }.Validate();
    }
    catch (ArgumentException)
    {
        rejectedInvalidConfiguredNetCafeSlots = true;
    }
    Check("ServerConfig rejects malformed 144 net-café settings", rejectedInvalidConfiguredNetCafeSlots);

    bool rejectedInvalidCalendarDate = false;
    bool rejectedInvalidCalendarEncoding = false;
    try
    {
        new PmConnectCalendarTime(2026, 2, 29, 0, 0).ToWireValue();
    }
    catch (ArgumentOutOfRangeException)
    {
        rejectedInvalidCalendarDate = true;
    }

    try
    {
        PmConnectCalendarTime.FromWireValue(26u << 24);
    }
    catch (ArgumentOutOfRangeException)
    {
        rejectedInvalidCalendarEncoding = true;
    }
    Check("142 rejects an invalid Gregorian date", rejectedInvalidCalendarDate);
    Check("142 rejects an invalid packed calendar", rejectedInvalidCalendarEncoding);

    var normalizedZeroThresholdGreeting = LoginWire.CreateAccountConnectionSuccess(0);
    var normalizedZeroThresholdReader = Packet.FromPayload(
        normalizedZeroThresholdGreeting.Opcode,
        normalizedZeroThresholdGreeting.Payload);
    using var zeroThresholdCodec = new PacketCodec(aesKey: null, compressThreshold: 0);
    Check("694 canonicalizes zero threshold to native ceiling",
        normalizedZeroThresholdReader.ReadU16() == PacketCodec.NeverCompress
        && normalizedZeroThresholdReader.Remaining == 0
        && zeroThresholdCodec.CompressThreshold == PacketCodec.NeverCompress);

    bool rejectedOutOfRangeThreshold = false;
    try
    {
        new ServerConfig { CompressThreshold = PacketCodec.NeverCompress + 1 }.Validate();
    }
    catch (ArgumentOutOfRangeException)
    {
        rejectedOutOfRangeThreshold = true;
    }
    Check("694 rejects client-ignored compression threshold", rejectedOutOfRangeThreshold);

    bool rejectedUnterminatedRequest = false;
    try
    {
        LoginWire.ReadRequest(Packet.FromPayload(Opcode.GL_LOGIN_REQ, "unterminated"u8));
    }
    catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
    {
        rejectedUnterminatedRequest = true;
    }
    Check("682 rejects malformed NUL field", rejectedUnterminatedRequest);
}

// ---- 1d. Login-to-channel admission contract -------------------------------
{
    var admissions = new ChannelAdmissionRegistry();
    var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.FromHours(8));
    admissions.Issue(
        accountId: 10,
        userId: 20,
        loginName: "Alpha9",
        nickname: "",
        billingUiMode: 100,
        featureExtensionCount: 0,
        remoteIp: "203.0.113.9",
        lifetime: TimeSpan.FromMinutes(2),
        now: now);

    Check("143 admission rejects wrong source", !admissions.TryClaim(
        billingUiMode: 100,
        featureExtensionCount: 0,
        remoteIp: "203.0.113.10",
        now: now,
        out _));
    Check("143 admission claims exact native echo once", admissions.TryClaim(
        billingUiMode: 100,
        featureExtensionCount: 0,
        remoteIp: "203.0.113.9",
        now: now,
        out var claimedAdmission)
        && claimedAdmission.AccountId == 10
        && !admissions.TryClaim(100, 0, "203.0.113.9", now, out _));

    // The String[24] writer is not known, so two otherwise indistinguishable
    // login handoffs behind one NAT must fail closed instead of guessing an
    // account/nickname identity alias.
    admissions.Issue(11, 21, "Bravo", "B", 100, 0, "203.0.113.11", TimeSpan.FromMinutes(2), now);
    admissions.Issue(12, 22, "Charlie", "C", 100, 0, "203.0.113.11", TimeSpan.FromMinutes(2), now);
    Check("143 admission rejects ambiguous NAT claims", !admissions.TryClaim(100, 0, "203.0.113.11", now, out _));

    admissions.Issue(13, 23, "Delta", "D", 101, 1, "203.0.113.12", TimeSpan.FromSeconds(1), now);
    Check("143 admission expires before claim", !admissions.TryClaim(
        101,
        1,
        "203.0.113.12",
        now.AddSeconds(1),
        out _));
}

// ---- 1e. Zero-command SQLite bootstrap and login migration -----------------
{
    string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"paperman-selftest-{Guid.NewGuid():N}");
    string temporaryDatabasePath = Path.Combine(temporaryDirectory, "data", "paperman.db");
    string legacyDatabasePath = Path.Combine(temporaryDirectory, "legacy", "paperman.db");
    long bootstrapUserId = 0;
    try
    {
        using (var firstOpen = new Db(temporaryDatabasePath))
        {
            Check("SQLite first open creates database and complete protocol catalog",
                firstOpen.Initialization.CreatedDatabaseFile
                && File.Exists(temporaryDatabasePath)
                && firstOpen.Initialization.ProtocolPacketDefinitionCount == 676);
        }

        // The bootstrap owns defaults only, not an operator's later decision.
        using (var connection = OpenExistingSqlite(temporaryDatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT OR REPLACE INTO server_config(key,value) VALUES('event_exp_rate','275')";
            command.ExecuteNonQuery();
        }

        using (var secondOpen = new Db(temporaryDatabasePath))
        {
            var fingerprint = new byte[24];
            Db.LoginResult newAccount = secondOpen.Login(
                accountName: "BootstrapAccount",
                passwordOrToken: "fresh-password",
                clientDataRevision: 0x1020_3040,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: fingerprint,
                remoteIp: "127.0.0.1");
            long duplicateUserId = secondOpen.CreateNick(newAccount.AccountId, "BootstrapNickname");
            Db.MyInfo? provisionedIdentity = newAccount.UserId > 0
                ? secondOpen.GetMyInfo(newAccount.UserId)
                : null;
            List<Db.CharSlot> freshCharacters = newAccount.UserId > 0
                ? secondOpen.GetCharacters(newAccount.UserId)
                : [];
            bootstrapUserId = newAccount.UserId;
            Db.LoginResult acceptedPassword = secondOpen.Login(
                accountName: "BootstrapAccount",
                passwordOrToken: "fresh-password",
                clientDataRevision: 0,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: fingerprint,
                remoteIp: "127.0.0.1");
            Db.LoginResult rejectedPassword = secondOpen.Login(
                accountName: "BootstrapAccount",
                passwordOrToken: "incorrect-password",
                clientDataRevision: 0,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: fingerprint,
                remoteIp: "127.0.0.1");
            secondOpen.LogPacket((ushort)Opcode.GT_PING_REQ, isReceive: true, bytes: 12);
            secondOpen.LogPacket(ushort.MaxValue, isReceive: true, bytes: 12);

            ServerConfig channelConfig = new ServerConfig { AesKey = null }.Validate();
            var channelContext = new ServerContext(secondOpen, channelConfig);
            channelContext.ChannelAdmissions.Issue(
                accountId: newAccount.AccountId,
                userId: newAccount.UserId,
                loginName: "BootstrapAccount",
                nickname: newAccount.Nickname,
                billingUiMode: channelConfig.BillingUiMode,
                featureExtensionCount: 0,
                remoteIp: "127.0.0.1",
                lifetime: TimeSpan.FromMinutes(1),
                now: DateTimeOffset.UtcNow);

            var (serverClient, peerClient) = await CreateConnectedTcpClientsAsync();
            using var clientPeer = peerClient;
            using var channelCodec = new PacketCodec(
                aesKey: null,
                compressThreshold: PacketCodec.NeverCompress);
            using var channelSession = new Session(
                client: serverClient,
                codec: channelCodec,
                id: 1,
                role: ServerRole.Channel);
            Router router = Router.Build();

            bool handoffWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.PM_UDPSTART_REQ)
                    .WriteStr("unresolved-143-identity")
                    .WriteS32(channelConfig.BillingUiMode)
                    .WriteU8(1)
                    .WriteS32(0),
                channelContext);
            bool channelWasNotEnteredAfterHandoff = !channelSession.ChannelEntryCompleted;
            bool selectionWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GC_ENTERCHANNEL_REQ)
                    .WriteU8(channelConfig.ChannelGroupIndex)
                    .WriteU8(channelConfig.ChannelIndex)
                    .WriteU8(0),
                channelContext);
            Packet udpStartAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            Packet enterChannelAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);

            // The client grammar for vote/matching requests is known, but the
            // original service state is not. Keep those operations unmapped
            // rather than sending a fabricated success response.
            bool unsupportedRoomWorkflowsAreUnmapped =
                !await router.DispatchAsync(
                    channelSession,
                    new Packet(Opcode.GR_START_VOTING_REQ).WriteS32(0).WriteS32(0).WriteS32(0),
                    channelContext)
                && !await router.DispatchAsync(
                    channelSession,
                    new Packet(Opcode.GR_DO_VOTING).WriteS8(0),
                    channelContext)
                && !await router.DispatchAsync(
                    channelSession,
                    new Packet(Opcode.GL_MATCHINGROOM_MAKE_REQ)
                        .WriteU8(0).WriteStr("").WriteS32(0)
                        .WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0)
                        .WriteU8(0).WriteU8(0).WriteU8(0).WriteU8(0),
                    channelContext)
                && !await router.DispatchAsync(
                    channelSession,
                    new Packet(Opcode.GL_MATCHINGROOM_CANCLE_REQ),
                    channelContext);
            Check("unresolved voting and matching workflows remain unregistered",
                selectionWasProcessed
                && channelSession.ChannelEntryCompleted
                && unsupportedRoomWorkflowsAreUnmapped);

            // The native UI creates only the title form: 0xFF, password flag,
            // title/[password], USERS, GAMEMODE, selected map, no-skill flag.
            // Send malformed variants first. They must neither create a room
            // nor leave an ACK ahead of the valid request below.
            bool alternateMakeRoomFormWasHandled = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_MAKEROOM_REQ)
                    .WriteU8(0)                              // native no-title form, unreachable
                    .WriteS8(0)
                    .WriteU8(8).WriteU8(2).WriteU8(14).WriteU8(1),
                channelContext);
            bool unterminatedMakeRoomWasHandled = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_MAKEROOM_REQ)
                    .WriteU8(byte.MaxValue)
                    .WriteS8(0)
                    .WriteRaw(Packet.Ansi.GetBytes("broken"))
                    .WriteU8(8).WriteU8(2).WriteU8(14).WriteU8(1),
                channelContext);
            bool trailingMakeRoomWasHandled = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_MAKEROOM_REQ)
                    .WriteU8(byte.MaxValue)
                    .WriteS8(0)
                    .WriteStr("broken")
                    .WriteU8(8).WriteU8(2).WriteU8(14).WriteU8(1)
                    .WriteU8(0),
                channelContext);
            bool malformedMakeRoomDidNotMutate = channelSession.RoomNo is null
                && !channelContext.Rooms.All.Any();

            bool makeRoomWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_MAKEROOM_REQ)
                    .WriteU8(byte.MaxValue)
                    .WriteS8(1)
                    .WriteStr("source-shaped room")
                    .WriteStr("room-password")
                    .WriteU8(8)                             // USERS
                    .WriteU8(2)                             // GAMEMODE: TeamHacking
                    .WriteU8(14)                            // selected map for mode 2
                    .WriteU8(1),                            // CHKBTN_NOSKILL
                channelContext);
            Packet makeRoomAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            Room? createdRoom = channelSession.RoomNo is { } createdRoomNo
                ? channelContext.Rooms.Find(createdRoomNo)
                : null;
            bool makeRoomLayout = false;
            if (makeRoomAcknowledgement.Opcode == Opcode.GL_MAKEROOM_ACK
                && createdRoom is { } expectedRoom)
            {
                var makeRoomReader = Packet.FromPayload(
                    makeRoomAcknowledgement.Opcode,
                    makeRoomAcknowledgement.Payload);
                makeRoomLayout = makeRoomReader.ReadU8() == 0
                    && makeRoomReader.ReadU8() == expectedRoom.RoomNo
                    && makeRoomReader.ReadU16() == 0x00FF   // USERS = eight slots
                    && makeRoomReader.ReadS32() == expectedRoom.RoomUid
                    && makeRoomReader.ReadBool()            // no-skill background
                    && !makeRoomReader.ReadBool()           // team shuffle default
                    && makeRoomReader.ReadU8() == 2;        // team mode
                for (int team = 0; makeRoomLayout && team < 2; team++)
                {
                    makeRoomLayout &= makeRoomReader.ReadU32() == 0
                        && makeRoomReader.ReadU32() == 0
                        && makeRoomReader.ReadStr() == string.Empty
                        && makeRoomReader.ReadU8() == 0;
                }

                makeRoomLayout &= makeRoomReader.Remaining == 0;
            }
            bool secondMakeRoomWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_MAKEROOM_REQ)
                    .WriteU8(byte.MaxValue)
                    .WriteS8(0)
                    .WriteStr("second room must not be created")
                    .WriteU8(8).WriteU8(2).WriteU8(14).WriteU8(1),
                channelContext);
            Packet secondMakeRoomAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var secondMakeRoomReader = Packet.FromPayload(
                secondMakeRoomAcknowledgement.Opcode,
                secondMakeRoomAcknowledgement.Payload);
            bool secondMakeRoomWasRejected = secondMakeRoomAcknowledgement.Opcode == Opcode.GL_MAKEROOM_ACK
                && secondMakeRoomReader.ReadU8() == 1        // existing Full result
                && secondMakeRoomReader.ReadU8() == 0
                && secondMakeRoomReader.ReadU16() == 0
                && secondMakeRoomReader.ReadS32() == 0
                && !secondMakeRoomReader.ReadBool()
                && !secondMakeRoomReader.ReadBool()
                && secondMakeRoomReader.Remaining == 0;
            List<Room> roomsAfterSecondRequest = [.. channelContext.Rooms.All];
            bool secondMakeRoomDidNotMutate = createdRoom is not null
                && channelSession.RoomNo == createdRoom.RoomNo
                && roomsAfterSecondRequest.Count == 1
                && ReferenceEquals(roomsAfterSecondRequest[0], createdRoom);

            Check("111 accepts only the source-shaped title form and preserves room semantics",
                alternateMakeRoomFormWasHandled
                && unterminatedMakeRoomWasHandled
                && trailingMakeRoomWasHandled
                && malformedMakeRoomDidNotMutate
                && makeRoomWasProcessed
                && makeRoomLayout
                && createdRoom is
                {
                    Title: "source-shaped room",
                    Password: "room-password",
                    ModeIndex: 2,
                    MapId: 14,
                    NoSkillBg: true,
                    SlotMask: 0x00FF,
                }
                && secondMakeRoomWasProcessed
                && secondMakeRoomWasRejected
                && secondMakeRoomDidNotMutate);

            bool shopEnterWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_SHOPIN_REQ),
                channelContext);
            Packet shopEnterAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            bool hiddenItemListWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GS_HIDDEN_ITEM_LIST_REQ).WriteS16(10),
                channelContext);
            Packet hiddenItemListAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var hiddenItemListReader = Packet.FromPayload(
                hiddenItemListAcknowledgement.Opcode, hiddenItemListAcknowledgement.Payload);
            bool hiddenItemListLayout = hiddenItemListAcknowledgement.Opcode == Opcode.GS_HIDDEN_ITEM_LIST_ACK
                && hiddenItemListReader.ReadU8() == 0
                && hiddenItemListReader.ReadU16() == 0
                && hiddenItemListReader.ReadU16() == 10
                && hiddenItemListReader.Remaining == 0;
            bool partsHiddenItemListWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GS_HIDDEN_ITEM_LIST_REQ).WriteS16(25),
                channelContext);
            Packet partsHiddenItemListAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var partsHiddenItemListReader = Packet.FromPayload(
                partsHiddenItemListAcknowledgement.Opcode, partsHiddenItemListAcknowledgement.Payload);
            bool partsHiddenItemListLayout = partsHiddenItemListAcknowledgement.Opcode == Opcode.GS_HIDDEN_ITEM_LIST_ACK
                && partsHiddenItemListReader.ReadU8() == 0
                && partsHiddenItemListReader.ReadU16() == 0
                && partsHiddenItemListReader.ReadU16() == 25
                && partsHiddenItemListReader.Remaining == 0;

            bool characterPurchaseWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GS_BUYCHAR_REQ)
                    .WriteS32(19_900_002)
                    .WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0),
                channelContext);
            Packet characterPurchaseAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            bool truncatedCharacterPurchaseWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GS_BUYCHAR_REQ).WriteS32(19_900_003),
                channelContext);
            Packet truncatedCharacterPurchaseAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            List<Db.CharSlot> charactersAfterPurchase = secondOpen.GetCharacters(newAccount.UserId);

            // The handler must not use the purchase request as an entitlement
            // grant. This direct DB fixture keeps the unrelated weapon-loadout
            // test deterministic without claiming a native purchase policy.
            bool fixtureSecondCharacterCreated = secondOpen.CreateChar(newAccount.UserId, 2, 1);

            bool inventoryEnterWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GL_INVENIN_REQ).WriteU8(0xD2),
                channelContext);
            Packet inventoryEnterAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var inventoryEnterReader = Packet.FromPayload(
                inventoryEnterAcknowledgement.Opcode,
                inventoryEnterAcknowledgement.Payload);
            bool inventoryEnterLayout = inventoryEnterAcknowledgement.Opcode == Opcode.GL_INVENIN_ACK
                && inventoryEnterReader.ReadU8() == NewSkillProfileWire.SelfSnapshotMode
                && inventoryEnterReader.ReadS32() == newAccount.UserId
                && inventoryEnterReader.ReadU8() == 0xD2
                && inventoryEnterReader.ReadU8() == 0
                && inventoryEnterReader.ReadU8() == 0;
            for (int profile = 0; inventoryEnterLayout && profile < NewSkillProfileWire.ProfileCount; profile++)
            {
                for (int slot = 0; slot < NewSkillProfileWire.PuzzleSlotCount; slot++)
                {
                    inventoryEnterLayout &= inventoryEnterReader.ReadS32() == 0;
                }

                inventoryEnterLayout &= inventoryEnterReader.ReadS32() == 0;
            }

            bool newSkillChangeWasProcessed = await router.DispatchAsync(
                channelSession,
                new Packet(Opcode.GI_CHANGE_SKILLITEMSLOT_REQ).WriteU8(0).WriteU8(0),
                channelContext);
            Packet newSkillChangeAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var newSkillChangeReader = Packet.FromPayload(
                newSkillChangeAcknowledgement.Opcode,
                newSkillChangeAcknowledgement.Payload);
            bool newSkillChangeLayout = newSkillChangeAcknowledgement.Opcode == Opcode.GI_CHANGE_SKILLITEMSLOT_ACK
                && newSkillChangeReader.ReadU8() == 0
                && newSkillChangeReader.ReadU8() == 0
                && newSkillChangeReader.ReadU8() == 1
                && newSkillChangeReader.ReadU8() == 0;
            for (int slot = 0; newSkillChangeLayout && slot < NewSkillProfileWire.PuzzleSlotCount; slot++)
            {
                newSkillChangeLayout &= newSkillChangeReader.ReadS32() == 0;
            }
            newSkillChangeLayout &= newSkillChangeReader.ReadS32() == 0
                && newSkillChangeReader.Remaining == 0;

            // 220 accepts only owned category-relative weapons and exact
            // weaponparts.pat compatibility.  Insert a tiny, self-contained
            // catalog fixture: one item from each loadout family and one
            // barrel that is truly compatible with the primary weapon.
            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO item_catalog(item_id,name,kind) VALUES
                        (12100016,'primary test',0),
                        (12200001,'secondary test',0),
                        (12300001,'melee test',0),
                        (12400001,'throw test',0),
                        (15210001,'compatible barrel test',0),
                        (15210002,'incompatible barrel test',0),
                        (15210003,'unowned compatible barrel test',0);
                    INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES
                        (@userId,0,12100016,0), (@userId,1,12200001,0),
                        (@userId,2,12300001,0), (@userId,3,12400001,0),
                        (@userId,4,15210001,0), (@userId,5,15210002,0);
                    INSERT INTO weapon_parts_catalog(gun_item_id,grp,slot,part_item_id)
                    VALUES(12100016,0,0,15210001), (12100016,0,1,15210003);
                    """;
                command.Parameters.AddWithValue("@userId", newAccount.UserId);
                command.ExecuteNonQuery();
            }

            var changeWeaponRequest = new Packet(Opcode.GI_CHANGEWP_REQ)
                .WriteU8(1)                 // one changed group (group zero)
                .WriteU8(0)
                .WriteU16(16)               // 12,100,016 primary
                .WriteU16(1)                // 12,200,001 secondary
                .WriteU16(1)                // 12,300,001 melee
                .WriteU16(1)                // 12,400,001 throw
                .WriteS32(15_210_001);      // weaponparts.pat group 0
            for (int partSlot = 1; partSlot < 8; partSlot++)
            {
                changeWeaponRequest.WriteS32(0);
            }

            bool weaponChangeWasProcessed = await router.DispatchAsync(
                channelSession, changeWeaponRequest, channelContext);
            Packet weaponChangeAcknowledgement = await ReadServerPacketAsync(clientPeer.GetStream(), channelCodec);
            var weaponChangeReader = Packet.FromPayload(
                weaponChangeAcknowledgement.Opcode, weaponChangeAcknowledgement.Payload);
            bool weaponChangeLayout = weaponChangeAcknowledgement.Opcode == Opcode.GI_CHANGEWP_ACK
                && weaponChangeAcknowledgement.Length == 63 // count + 41B group0 + 9B group1/2 + 3B group3
                && weaponChangeReader.ReadU8() == 4
                && weaponChangeReader.ReadU8() == 0
                && weaponChangeReader.ReadU16() == 16
                && weaponChangeReader.ReadU16() == 1
                && weaponChangeReader.ReadU16() == 1
                && weaponChangeReader.ReadU16() == 1
                && weaponChangeReader.ReadS32() == 15_210_001;
            for (int partSlot = 1; weaponChangeLayout && partSlot < 8; partSlot++)
            {
                weaponChangeLayout &= weaponChangeReader.ReadS32() == 0;
            }

            for (byte group = 1; weaponChangeLayout && group < 4; group++)
            {
                weaponChangeLayout &= weaponChangeReader.ReadU8() == group
                    && weaponChangeReader.ReadU16() == 0;
                if (group != 3)
                {
                    weaponChangeLayout &= weaponChangeReader.ReadU16() == 0
                        && weaponChangeReader.ReadU16() == 0
                        && weaponChangeReader.ReadU16() == 0;
                }
            }
            weaponChangeLayout &= weaponChangeReader.Remaining == 0;

            bool rejectedTruncatedWeaponPacket = false;
            try
            {
                await router.DispatchAsync(
                    channelSession,
                    new Packet(Opcode.GI_CHANGEWP_REQ).WriteU8(1).WriteU8(0),
                    channelContext);
            }
            catch (InvalidDataException)
            {
                rejectedTruncatedWeaponPacket = true;
            }
            List<Db.WeaponGroup> afterTruncatedPacketRejection = secondOpen.GetWeaponGroups(newAccount.UserId);

            bool IsPersistedWeaponSnapshot(IReadOnlyList<Db.WeaponGroup> snapshot) => snapshot is
            [
                { GroupNo: 0, PrimaryOffset: 16, SecondaryOffset: 1, MeleeOffset: 1, ThrowOffset: 1,
                  Parts: [15_210_001, 0, 0, 0, 0, 0, 0, 0] },
                { GroupNo: 1, PrimaryOffset: 0, SecondaryOffset: 0, MeleeOffset: 0, ThrowOffset: 0 },
                { GroupNo: 2, PrimaryOffset: 0, SecondaryOffset: 0, MeleeOffset: 0, ThrowOffset: 0 },
                { GroupNo: 3, PrimaryOffset: 0, SecondaryOffset: 0, MeleeOffset: 0, ThrowOffset: 0 },
            ];

            bool rejectedDuplicatePrimary = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(1, 16, 0, 0, 0, new int[8])],
                out List<Db.WeaponGroup> rejectedWeaponSnapshot);
            List<Db.WeaponGroup> afterDuplicateRejection = secondOpen.GetWeaponGroups(newAccount.UserId);

            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    UPDATE inventory SET expires_at=1
                    WHERE user_id=@userId AND item_id=12100016
                    """;
                command.Parameters.AddWithValue("@userId", newAccount.UserId);
                command.ExecuteNonQuery();
            }
            bool rejectedExpiredPrimary = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(0, 16, 0, 1, 1, [15_210_001, 0, 0, 0, 0, 0, 0, 0])],
                out List<Db.WeaponGroup> expiredPrimarySnapshot);
            List<Db.WeaponGroup> afterExpiredPrimaryRejection = secondOpen.GetWeaponGroups(newAccount.UserId);

            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    UPDATE inventory SET expires_at=NULL
                    WHERE user_id=@userId AND item_id=12100016
                    """;
                command.Parameters.AddWithValue("@userId", newAccount.UserId);
                command.ExecuteNonQuery();
            }
            bool rejectedUnownedPrimary = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(0, 17, 1, 1, 1, new int[8])],
                out List<Db.WeaponGroup> unownedPrimarySnapshot);
            List<Db.WeaponGroup> afterUnownedPrimaryRejection = secondOpen.GetWeaponGroups(newAccount.UserId);
            bool rejectedUnownedCompatiblePart = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(0, 16, 1, 1, 1, [15_210_003, 0, 0, 0, 0, 0, 0, 0])],
                out List<Db.WeaponGroup> unownedPartSnapshot);
            List<Db.WeaponGroup> afterUnownedPartRejection = secondOpen.GetWeaponGroups(newAccount.UserId);
            bool rejectedIncompatibleOwnedPart = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(0, 16, 1, 1, 1, [15_210_002, 0, 0, 0, 0, 0, 0, 0])],
                out List<Db.WeaponGroup> incompatiblePartSnapshot);
            List<Db.WeaponGroup> afterIncompatiblePartRejection = secondOpen.GetWeaponGroups(newAccount.UserId);
            bool rejectedPartsWithoutPrimary = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(0, 0, 1, 1, 1, [15_210_001, 0, 0, 0, 0, 0, 0, 0])],
                out List<Db.WeaponGroup> partsWithoutPrimarySnapshot);
            List<Db.WeaponGroup> afterPartsWithoutPrimaryRejection = secondOpen.GetWeaponGroups(newAccount.UserId);
            bool rejectedSwitchWeaponSubslots = !secondOpen.TryChangeWeaponGroups(
                newAccount.UserId,
                [new Db.WeaponGroup(3, 0, 1, 0, 0, new int[8])],
                out List<Db.WeaponGroup> switchWeaponSubslotsSnapshot);
            List<Db.WeaponGroup> afterSwitchWeaponSubslotsRejection = secondOpen.GetWeaponGroups(newAccount.UserId);

            Check("SQLite first login provisions a playable identity",
                !secondOpen.Initialization.CreatedDatabaseFile
                && secondOpen.Initialization.ProtocolPacketDefinitionCount == 676
                && newAccount is { Result: LoginCode.Ok, AccountId: > 0, UserId: > 0, Nickname: "BootstrapAccount" }
                && provisionedIdentity is { UserId: > 0, Nickname: "BootstrapAccount", CurrentChar: 0 }
                && freshCharacters is [{ SlotNo: 0, CharType: 1 }]
                && freshCharacters[0].Equip.SequenceEqual<ushort>([1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0])
                && duplicateUserId == 0
                && acceptedPassword is { Result: LoginCode.Ok, UserId: > 0, Nickname: "BootstrapAccount" }
                && rejectedPassword.Result == LoginCode.BadCredentials);
            Check("channel entry completes only after successful 195 → 196",
                handoffWasProcessed
                && udpStartAcknowledgement.Opcode == Opcode.PM_UDPSTART_ACK
                && channelSession.Authenticated
                && channelWasNotEnteredAfterHandoff
                && selectionWasProcessed
                && enterChannelAcknowledgement.Opcode == Opcode.GC_ENTERCHANNEL_ACK
                && channelSession.ChannelEntryCompleted);
            Check("252 → 253 uses the project-permitted empty interoperability ACK",
                shopEnterWasProcessed
                && shopEnterAcknowledgement.Opcode == Opcode.GL_SHOPIN_ACK
                && shopEnterAcknowledgement.Length == 0);
            Check("806 → 807 returns exact zero-record shop and parts arms without mutation",
                hiddenItemListWasProcessed
                && hiddenItemListLayout
                && partsHiddenItemListWasProcessed
                && partsHiddenItemListLayout);
            Check("310 → 311 valid and truncated requests fail closed without granting a character",
                characterPurchaseWasProcessed
                && truncatedCharacterPurchaseWasProcessed
                && IsNative311FailureAcknowledgement(characterPurchaseAcknowledgement)
                && IsNative311FailureAcknowledgement(truncatedCharacterPurchaseAcknowledgement)
                && charactersAfterPurchase is [{ SlotNo: 0, CharType: 1 }]
                && fixtureSecondCharacterCreated);
            Check("254 → 255 returns five authoritative NewSkill profile records",
                inventoryEnterWasProcessed
                && inventoryEnterLayout
                && inventoryEnterReader.Remaining == 0);
            Check("466 → 467 returns the persisted raw32 profile, never a truncated placeholder",
                newSkillChangeWasProcessed
                && newSkillChangeLayout);
            Check("220 → 221 applies one validated delta and returns a complete four-group snapshot",
                weaponChangeWasProcessed
                && weaponChangeLayout
                && IsPersistedWeaponSnapshot(afterDuplicateRejection));
            Check("220 rejects a truncated native record without mutation",
                rejectedTruncatedWeaponPacket
                && IsPersistedWeaponSnapshot(afterTruncatedPacketRejection));
            Check("220 rejects a duplicate primary without mutating the authoritative snapshot",
                rejectedDuplicatePrimary
                && rejectedWeaponSnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterDuplicateRejection));
            Check("220 rejects expired, unowned, and incompatible items without mutation",
                rejectedExpiredPrimary
                && expiredPrimarySnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterExpiredPrimaryRejection)
                && rejectedUnownedPrimary
                && unownedPrimarySnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterUnownedPrimaryRejection)
                && rejectedUnownedCompatiblePart
                && unownedPartSnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterUnownedPartRejection)
                && rejectedIncompatibleOwnedPart
                && incompatiblePartSnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterIncompatiblePartRejection));
            Check("220 rejects parts without a primary and switch-weapon subslots without mutation",
                rejectedPartsWithoutPrimary
                && partsWithoutPrimarySnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterPartsWithoutPrimaryRejection)
                && rejectedSwitchWeaponSubslots
                && switchWeaponSubslotsSnapshot.Count == 0
                && IsPersistedWeaponSnapshot(afterSwitchWeaponSubslotsRejection));
        }

        // Concrete legacy fixtures: a canonical type-1 record lost its body;
        // its existing type-2 neighbor has one historical nonzero head while
        // the other canonical words are missing.
        using (var connection = OpenExistingSqlite(temporaryDatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE characters SET eq_primary=0 WHERE user_id=@userId AND slot_no=0;
                UPDATE characters
                SET eq_primary=2, eq_secondary=99, eq_melee=0, eq_grenade=0, eq_head=0, eq_face=0
                WHERE user_id=@userId AND slot_no=1;
                """;
            command.Parameters.AddWithValue("@userId", bootstrapUserId);
            command.ExecuteNonQuery();
        }

        string legacySalt = "legacy-salt";
        string legacyPassword = "legacy-password";
        string legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacySalt + legacyPassword)));
        using (var connection = OpenExistingSqlite(temporaryDatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO accounts(login_name,pass_hash,pass_salt)
                VALUES(@loginName,@passwordHash,@passwordSalt)
                """;
            command.Parameters.AddWithValue("@loginName", "LegacyAccount");
            command.Parameters.AddWithValue("@passwordHash", legacyHash);
            command.Parameters.AddWithValue("@passwordSalt", legacySalt);
            command.ExecuteNonQuery();
        }

        using (var thirdOpen = new Db(temporaryDatabasePath))
        {
            Db.LoginResult repairedLogin = thirdOpen.Login(
                accountName: "BootstrapAccount",
                passwordOrToken: "fresh-password",
                clientDataRevision: 2,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: new byte[24],
                remoteIp: "127.0.0.1");
            List<Db.CharSlot> repairedCharacters = thirdOpen.GetCharacters(bootstrapUserId);
            ushort[][] canonicalStarterOffsets =
            [
                [1, 1, 1, 1, 1, 1],
                [2, 15, 10, 22, 12, 12],
                [3, 28, 19, 45, 25, 24],
                [4, 41, 28, 66, 36, 41],
                [5, 55, 37, 90, 47, 52],
                [6, 123, 111, 157, 99, 105],
                [7, 124, 112, 167, 109, 115],
                [8, 125, 113, 177, 119, 125],
                [9, 126, 114, 187, 129, 135],
                [10, 127, 115, 197, 139, 145],
                [11, 1096, 839, 1069, 974, 952],
                [12, 1428, 865, 1205, 1069, 1009],
                [13, 1600, 866, 1213, 1072, 1012],
                [14, 792, 385, 428, 376, 360],
                [15, 30220, 920, 10011, 10011, 10114],
            ];
            bool createsCanonicalBodies = thirdOpen.CreateChar(bootstrapUserId, 2, 2)
                && thirdOpen.BuyCharacter(bootstrapUserId, 3, 3)
                && Enumerable.Range(4, 12).All(charType =>
                    thirdOpen.CreateChar(bootstrapUserId, (byte)charType, (byte)charType))
                && !thirdOpen.CreateChar(bootstrapUserId, 16, 0)
                && !thirdOpen.BuyCharacter(bootstrapUserId, 16, 16);
            List<Db.CharSlot> createdCharacters = thirdOpen.GetCharacters(bootstrapUserId);
            bool everyCanonicalStarterPersists = createdCharacters
                .Where(character => character.SlotNo != 1)
                .All(character => character.Equip.AsSpan(0, 6)
                    .SequenceEqual(canonicalStarterOffsets[character.CharType - 1]));

            Check("verified login fills only missing canonical words and keeps selection playable",
                repairedLogin is { Result: LoginCode.Ok, UserId: > 0 }
                && repairedCharacters.Count == 2
                && repairedCharacters[0] is { SlotNo: 0, CharType: 1 }
                && repairedCharacters[0].Equip.SequenceEqual<ushort>([1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0])
                && repairedCharacters[1] is { SlotNo: 1, CharType: 2 }
                && repairedCharacters[1].Equip.SequenceEqual<ushort>([2, 99, 10, 22, 12, 12, 0, 0, 0, 0, 0, 0])
                && thirdOpen.GetMyInfo(bootstrapUserId) is { CurrentChar: 0 });
            Check("GM and purchase creation persist all 15 native starter vectors and reject invalid types",
                createsCanonicalBodies
                && createdCharacters.Count == 16
                && everyCanonicalStarterPersists
                && createdCharacters[2] is { SlotNo: 2, CharType: 2 }
                && createdCharacters[3] is { SlotNo: 3, CharType: 3 });

            Db.NewSkillProfileSnapshot initialProfiles = thirdOpen.GetNewSkillProfileSnapshot(bootstrapUserId);
            long lazyProfileUserId;
            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO accounts(login_name,pass_hash,pass_salt) VALUES('LazyProfileAccount','h','s');
                    INSERT INTO users(account_id,nickname) VALUES(last_insert_rowid(),'LazyProfileUser');
                    DELETE FROM new_skill_profiles WHERE user_id=(SELECT user_id FROM users WHERE nickname='LazyProfileUser');
                    DELETE FROM new_skill_profile_state WHERE user_id=(SELECT user_id FROM users WHERE nickname='LazyProfileUser');
                    INSERT INTO skill_slots(user_id,slot_kind,idx,item_id)
                    VALUES((SELECT user_id FROM users WHERE nickname='LazyProfileUser'),1,0,11010001),
                          ((SELECT user_id FROM users WHERE nickname='LazyProfileUser'),1,5,11060002);
                    """;
                command.ExecuteNonQuery();
                command.CommandText = "SELECT user_id FROM users WHERE nickname='LazyProfileUser'";
                lazyProfileUserId = Convert.ToInt64(command.ExecuteScalar());
            }
            Db.NewSkillProfileSnapshot lazyMigratedProfiles = thirdOpen.GetNewSkillProfileSnapshot(lazyProfileUserId);
            Db.NewSkillProfileSnapshot lazyProfilesReloaded = thirdOpen.GetNewSkillProfileSnapshot(lazyProfileUserId);
            Check("missing NewSkill rows lazily import legacy profile zero exactly once",
                lazyMigratedProfiles.SelectedProfile == 0
                && lazyMigratedProfiles.Profiles[0].PuzzleItemIds.SequenceEqual([11010001, 0, 0, 0, 0, 11060002, 0])
                && lazyMigratedProfiles.Profiles.Skip(1).All(profile =>
                    profile.PuzzleItemIds.SequenceEqual(new int[NewSkillProfileWire.PuzzleSlotCount]))
                && lazyProfilesReloaded.Profiles[0].PuzzleItemIds.SequenceEqual(
                    lazyMigratedProfiles.Profiles[0].PuzzleItemIds));

            DateTime profileExpiry = DateTime.Now.AddDays(7);
            int packedProfileExpiry = ((profileExpiry.Year - 2000) << 24)
                | (profileExpiry.Month << 19)
                | (profileExpiry.Day << 13)
                | (profileExpiry.Hour << 7)
                | profileExpiry.Minute;
            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO item_catalog(item_id,name,kind) VALUES(11010001,'profile test hair',2);
                    INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES(@userId,5119,11010001,0);
                    UPDATE new_skill_profiles
                    SET expires_at_packed_minute=@expiry
                    WHERE user_id=@userId AND profile_index=1;
                    """;
                command.Parameters.AddWithValue("@userId", bootstrapUserId);
                command.Parameters.AddWithValue("@expiry", packedProfileExpiry);
                command.ExecuteNonQuery();
            }

            Db.NewSkillProfile? selectedProfileOne = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId,
                targetProfile: 1,
                hasPreviousProfileUpdate: true,
                previousProfile: 0,
                previousProfilePuzzleItemIds: new int[NewSkillProfileWire.PuzzleSlotCount]);
            Db.NewSkillProfile? selectedProfileZero = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId,
                targetProfile: 0,
                hasPreviousProfileUpdate: true,
                previousProfile: 1,
                previousProfilePuzzleItemIds: [11010001, 0, 0, 0, 0, 0, 0]);
            Db.NewSkillProfileSnapshot persistedProfiles = thirdOpen.GetNewSkillProfileSnapshot(bootstrapUserId);
            Db.NewSkillProfile? wrongFamilyWasRejected = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId,
                targetProfile: 0,
                hasPreviousProfileUpdate: true,
                previousProfile: 0,
                previousProfilePuzzleItemIds: [11020001, 0, 0, 0, 0, 0, 0]);
            Check("NewSkill 255 storage creates five profiles and 466 persists only validated previous ids",
                initialProfiles is { SelectedProfile: 0, Profiles.Length: NewSkillProfileWire.ProfileCount }
                && initialProfiles.Profiles.All(profile => profile.PuzzleItemIds.SequenceEqual(new int[NewSkillProfileWire.PuzzleSlotCount]))
                && selectedProfileOne is { ExpiresAtPackedMinute: var expiry } && expiry == packedProfileExpiry
                && selectedProfileZero is not null
                && persistedProfiles.SelectedProfile == 0
                && persistedProfiles.Profiles[1].PuzzleItemIds.SequenceEqual([11010001, 0, 0, 0, 0, 0, 0])
                && persistedProfiles.Profiles[1].ExpiresAtPackedMinute == packedProfileExpiry
                && wrongFamilyWasRejected is null);

            DateTime expiredProfileTime = DateTime.Now.AddMinutes(-2);
            int packedExpiredProfileTime = ((expiredProfileTime.Year - 2000) << 24)
                | (expiredProfileTime.Month << 19)
                | (expiredProfileTime.Day << 13)
                | (expiredProfileTime.Hour << 7)
                | expiredProfileTime.Minute;
            using (var connection = OpenExistingSqlite(temporaryDatabasePath))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    UPDATE new_skill_profiles SET expires_at_packed_minute=@expired WHERE user_id=@userId AND profile_index=2;
                    UPDATE new_skill_profiles SET expires_at_packed_minute=0 WHERE user_id=@userId AND profile_index=3;
                    UPDATE new_skill_profiles SET expires_at_packed_minute=@allBitsSet WHERE user_id=@userId AND profile_index=4;
                    """;
                command.Parameters.AddWithValue("@userId", bootstrapUserId);
                command.Parameters.AddWithValue("@expired", packedExpiredProfileTime);
                command.Parameters.AddWithValue("@allBitsSet", -1);
                command.ExecuteNonQuery();
            }

            Db.NewSkillProfile? pastExpiryWasRejected = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId, 2, true, 0, new int[NewSkillProfileWire.PuzzleSlotCount]);
            Db.NewSkillProfile? zeroExpiryWasRejected = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId, 3, true, 0, new int[NewSkillProfileWire.PuzzleSlotCount]);
            Db.NewSkillProfile? allBitsSetExpiryWasAccepted = thirdOpen.ChangeNewSkillProfile(
                bootstrapUserId, 4, true, 0, new int[NewSkillProfileWire.PuzzleSlotCount]);
            Check("NewSkill packed-minute expiry rejects past/zero and normalizes full-width raw fields",
                pastExpiryWasRejected is null
                && zeroExpiryWasRejected is null
                && allBitsSetExpiryWasAccepted is { ExpiresAtPackedMinute: -1 });

            Db.LoginResult legacyLogin = thirdOpen.Login(
                accountName: "LegacyAccount",
                passwordOrToken: legacyPassword,
                clientDataRevision: 1,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: new byte[24],
                remoteIp: "127.0.0.1");
            List<Db.CharSlot> legacyCharacters = legacyLogin.UserId > 0
                ? thirdOpen.GetCharacters(legacyLogin.UserId)
                : [];
            Check("legacy orphan account receives a player identity and PBKDF2 upgrade",
                legacyLogin is { Result: LoginCode.Ok, AccountId: > 0, UserId: > 0, Nickname: "LegacyAccount" }
                && legacyCharacters is [{ SlotNo: 0, CharType: 1 }]
                && legacyCharacters[0].Equip.SequenceEqual<ushort>([1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0]));
        }

        using (var connection = OpenExistingSqlite(temporaryDatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT
                    (SELECT value FROM server_config WHERE key='event_exp_rate'),
                    (SELECT pass_hash FROM accounts WHERE login_name='BootstrapAccount'),
                    (SELECT pass_salt FROM accounts WHERE login_name='BootstrapAccount'),
                    (SELECT pass_hash FROM accounts WHERE login_name='LegacyAccount'),
                    (SELECT COUNT(*) FROM packet_stats WHERE opcode = 101),
                    (SELECT COUNT(*) FROM packet_stats WHERE opcode = 65535)
                """;
            using var reader = command.ExecuteReader();
            reader.Read();
            string currentRate = reader.GetString(0);
            string freshHash = reader.GetString(1);
            string freshSalt = reader.GetString(2);
            string upgradedLegacyHash = reader.GetString(3);
            long knownPacketStats = reader.GetInt64(4);
            long unknownPacketStats = reader.GetInt64(5);
            Check("bootstrap retains config, PBKDF2 format, and unknown-opcode guard",
                currentRate == "275"
                && freshHash.StartsWith("PBKDF2-SHA256$210000$", StringComparison.Ordinal)
                && Convert.FromBase64String(freshSalt).Length == 16
                && upgradedLegacyHash.StartsWith("PBKDF2-SHA256$210000$", StringComparison.Ordinal)
                && knownPacketStats == 1
                && unknownPacketStats == 0);
        }

        // Reproduce only the old 682 column names; Db must preserve the value
        // while giving the present code its source-verified names and raw24 guard.
        string legacyDatabaseDirectory = Path.GetDirectoryName(legacyDatabasePath)
            ?? throw new InvalidOperationException("The self-test legacy database path must have a parent directory.");
        Directory.CreateDirectory(legacyDatabaseDirectory);
        using (var connection = new SqliteConnection($"Data Source={legacyDatabasePath};Mode=ReadWriteCreate;Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE accounts (
                    account_id INTEGER PRIMARY KEY,
                    login_name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    pass_hash TEXT NOT NULL,
                    pass_salt TEXT NOT NULL,
                    hw_key INTEGER,
                    security_state INTEGER NOT NULL DEFAULT 0,
                    cash INTEGER NOT NULL DEFAULT 0,
                    is_gm INTEGER NOT NULL DEFAULT 0,
                    is_banned INTEGER NOT NULL DEFAULT 0,
                    ban_reason TEXT,
                    ban_until INTEGER,
                    chat_ban_until INTEGER,
                    created_at INTEGER,
                    updated_at INTEGER,
                    last_login_at INTEGER,
                    last_login_ip TEXT
                );
                INSERT INTO accounts(login_name,pass_hash,pass_salt,hw_key,security_state)
                VALUES('OldMetadata','h','s',123,2);
                """;
            command.ExecuteNonQuery();
        }

        using (var migratedOpen = new Db(legacyDatabasePath))
        {
            Check("legacy database opens through automatic 682 metadata migration",
                !migratedOpen.Initialization.CreatedDatabaseFile);
        }

        using (var connection = OpenExistingSqlite(legacyDatabasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT
                    (SELECT client_data_revision FROM accounts WHERE login_name='OldMetadata'),
                    (SELECT fingerprint_source FROM accounts WHERE login_name='OldMetadata'),
                    EXISTS(SELECT 1 FROM pragma_table_info('accounts') WHERE name='client_fingerprint')
                """;
            using var reader = command.ExecuteReader();
            reader.Read();
            long revision = reader.GetInt64(0);
            long source = reader.GetInt64(1);
            bool hasFingerprint = reader.GetInt64(2) != 0;

            bool rejectedWrongFingerprintLength;
            try
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO accounts(login_name,pass_hash,pass_salt,client_fingerprint) VALUES('BadFingerprint','h','s',@fingerprint)";
                insert.Parameters.AddWithValue("@fingerprint", new byte[23]);
                insert.ExecuteNonQuery();
                rejectedWrongFingerprintLength = false;
            }
            catch (SqliteException)
            {
                rejectedWrongFingerprintLength = true;
            }

            Check("legacy metadata values and migrated raw24 invariant are preserved",
                revision == 123
                && source == 2
                && hasFingerprint
                && rejectedWrongFingerprintLength);
        }
    }
    finally
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
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

    byte[] emptyAesFrame = codec.Encode(new Packet(Opcode.GT_PING_ACK));
    Check("generic AES empty payload is one encrypted block",
        BinaryPrimitives.ReadUInt16LittleEndian(emptyAesFrame) == 16
        && BinaryPrimitives.ReadUInt16LittleEndian(emptyAesFrame.AsSpan(4)) == 0
        && codec.Decode(emptyAesFrame).Length == 0);

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
// 模式 = sub_4042A0 / sub_404470 (n2_4=2 → 128-bit CFB, IV=0)。
{
    using var aes = new PaperAes(PaperAes.DefaultKey);

    byte[] block1 = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
    aes.EncryptCfb(block1);
    Check("native key: CFB(000102..0F)",
        Convert.ToHexString(block1) == "3A736DBF81F4BA1AF40854FBF4E13F47");

    byte[] block2 = "PaperMan-Packet!"u8.ToArray();
    aes.EncryptCfb(block2);
    Check("native key: CFB('PaperMan-Packet!')",
        Convert.ToHexString(block2) == "60912185D998DDD7F70F57FCC48B3079");

    aes.DecryptCfb(block2);
    Check("native key: decrypt roundtrip", block2.AsSpan().SequenceEqual("PaperMan-Packet!"u8));

    Check("native key bytes = EUC-KR 트렁크점령전머지",
        Convert.ToHexString(PaperAes.DefaultKey) == "C6AEB7B7C5A9C1A1B7C9C0FCB8D3C1F6");
}

// ---- 6. 黃金 frame 測試向量 (十三輪/本輪 CFB-128 驗證) -------------------
// Encode(GT_PING_ACK(102), payload = s32 123) 以原生金鑰必須逐 byte 等於:
//   header: w0=0010 op=0066 w2=0004 w3=0004 (LE)
//   body  : AES-128-CFB(00000-pad 至 16B, IV=0)
{
    using var codec = new PacketCodec(PaperAes.DefaultKey.ToArray());
    var frame = codec.Encode(new Packet(Opcode.GT_PING_ACK).WriteS32(123));
    const string golden = "100066000400040041F35BF885F6BC18FF0B59F8DFEC3248";
    Check("golden frame: byte-exact vs CFB-128 獨立實作", Convert.ToHexString(frame) == golden);

    var back = codec.Decode(Convert.FromHexString(golden));
    Check("golden frame: decode", back.Opcode == Opcode.GT_PING_ACK && back.ReadS32() == 123);
}

// ---- 7. UDP-private 19 -> empty 20 control framing -------------------------
// sub_595980 / sub_595A60 use AES only: no TCP LZ stage, and even an empty
// completion is a 16-byte ciphertext with w2=w3=0.
{
    using var udpCodec = new UdpPacketCodec();
    var requestPacket = new Packet((Opcode)UdpPrivateOpcode.Opcode19)
        .WriteU8(3)
        .WriteU8(7)
        .WriteS8(0)
        .WriteS8(-2)
        .WriteS32(42)
        .WriteStr("테스트닉");

    byte[] requestFrame = udpCodec.Encode(requestPacket);
    ushort requestW0 = BinaryPrimitives.ReadUInt16LittleEndian(requestFrame);
    ushort requestW2 = BinaryPrimitives.ReadUInt16LittleEndian(requestFrame.AsSpan(4));
    ushort requestW3 = BinaryPrimitives.ReadUInt16LittleEndian(requestFrame.AsSpan(6));
    var request = UdpControlRequest.Read(udpCodec.DecodeDatagram(requestFrame));
    Check("udp 19 uses AES-only Packet header",
        requestW0 == ((requestPacket.Length + 15) & ~15)
        && requestW2 == requestPacket.Length
        && requestW3 == requestPacket.Length);
    Check("udp 19 exact field order", request.ActiveChannelIndex == 3
        && request.CurrentRoomSlot == 7
        && request.SourceModeEqualsTwoFlag == 0
        && request.SourceDependentSlot == -2
        && request.ClientReportedPlayerId == 42
        && request.LocalNickname == "테스트닉");

    byte[] completionFrame = udpCodec.Encode(new Packet((Opcode)UdpPrivateOpcode.Opcode20));
    ushort completionW0 = BinaryPrimitives.ReadUInt16LittleEndian(completionFrame);
    ushort completionW2 = BinaryPrimitives.ReadUInt16LittleEndian(completionFrame.AsSpan(4));
    ushort completionW3 = BinaryPrimitives.ReadUInt16LittleEndian(completionFrame.AsSpan(6));
    Packet completion = udpCodec.DecodeDatagram(completionFrame);
    Check("udp empty 20 remains AES-encrypted", completionW0 == 16 && completionW2 == 0 && completionW3 == 0);
    Check("udp empty 20 decodes to no payload", completion.OpcodeRaw == 20 && completion.Length == 0);

    var payloadWithNoUdpLz = new Packet((Opcode)UdpPrivateOpcode.Opcode19)
        .WriteRaw(new byte[64]);
    byte[] noLzFrame = udpCodec.Encode(payloadWithNoUdpLz);
    Check("udp codec never applies TCP LZ",
        BinaryPrimitives.ReadUInt16LittleEndian(noLzFrame) == 64
        && BinaryPrimitives.ReadUInt16LittleEndian(noLzFrame.AsSpan(4)) == 64
        && BinaryPrimitives.ReadUInt16LittleEndian(noLzFrame.AsSpan(6)) == 64);

    byte[] trailingDatagram = [.. requestFrame, 0xAA, 0xBB];
    Check("udp native-compatible trailing bytes are ignored",
        UdpControlRequest.Read(udpCodec.DecodeDatagram(trailingDatagram)).LocalNickname == "테스트닉");
}

// ---- 8. UDP control endpoint source-address completion ----------------------
{
    int udpPort = GetAvailableIpv4UdpPort();
    var config = new ServerConfig
    {
        ListenHost = "127.0.0.1",
        PublicHost = "127.0.0.1",
        UdpPortOverride = udpPort,
    }.Validate();

    using var server = new UdpControlServer(config);
    using var serverStop = new CancellationTokenSource();
    Task serverTask = server.RunAsync(serverStop.Token);
    using var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    using var clientCodec = new UdpPacketCodec();
    using var receiveStop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var request = new Packet((Opcode)UdpPrivateOpcode.Opcode19)
        .WriteU8(1).WriteU8(2).WriteS8(1).WriteS8(3).WriteS32(4).WriteStr("udp-test");

    try
    {
        byte[] requestFrame = clientCodec.Encode(request);
        _ = await client.SendToAsync(requestFrame, SocketFlags.None, server.LocalEndpoint, receiveStop.Token);

        var responseBuffer = new byte[UdpPacketCodec.MaxDatagramSize];
        SocketReceiveFromResult response = await client.ReceiveFromAsync(
            responseBuffer,
            SocketFlags.None,
            new IPEndPoint(IPAddress.Any, 0),
            receiveStop.Token);
        Packet completion = clientCodec.DecodeDatagram(responseBuffer.AsSpan(0, response.ReceivedBytes));
        Check("udp endpoint replies 20 to request source",
            completion.OpcodeRaw == (ushort)UdpPrivateOpcode.Opcode20 && completion.Length == 0);
    }
    catch (OperationCanceledException)
    {
        Check("udp endpoint replies 20 to request source", false);
    }
    finally
    {
        serverStop.Cancel();
        await serverTask;
    }
}

// ---- 9. 語音封包簇與 sub_885D00 wire 格式 round-trip (本輪新增) -------------
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

// ---- 10. 系統 / 角色 / 商城 / 任務 / 投票新封包 wire 格式 round-trip --------
{
    // 143/144 PM_UDPSTART (status 1 = OK)
    var p144 = new Packet(Opcode.PM_UDPSTART_ACK)
        .WriteU8(1)
        .WriteU8(0)
        .WriteS32(42)
        .WriteStr("Channel 1")
        .WriteS32(0).WriteS32(0).WriteS32(0)
        .WriteF32(0f)
        .WriteS32(0)
        .WriteU8(0);
    Check("144 PM_UDPSTART_ACK status == 1", p144.ReadU8() == 1 && p144.ReadU8() == 0 && p144.ReadS32() == 42 && p144.ReadStr() == "Channel 1");

    // 195/196 GC_ENTERCHANNEL (result 1 = OK)
    var p196 = new Packet(Opcode.GC_ENTERCHANNEL_ACK)
        .WriteU8(1)
        .WriteS32(1)
        .WriteU8(0)
        .WriteStr("127.0.0.1")
        .WriteS32(40202)
        .WriteU8(0)
        .WriteU8(0)
        .WriteS32(0)
        .WriteU8(5);
    Check("196 GC_ENTERCHANNEL_ACK result == 1", p196.ReadU8() == 1 && p196.ReadS32() == 1 && p196.ReadU8() == 0 && p196.ReadStr() == "127.0.0.1" && p196.ReadS32() == 40202);

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

    // 720 voting notification wire grammar. Server-side vote ownership, timer,
    // and result policy remain unimplemented; this is not a handler success test.
    var p720 = new Packet(Opcode.GR_START_VOTING).WriteS32(2).WriteS32(1).WriteS32(0).WriteS32(30).WriteU8(0);
    Check("720 Start Voting broadcast wire", p720.Length == 17 && p720.ReadS32() == 2);

    // 423/424 Msg Read
    var p424 = new Packet(Opcode.GL_MSG_READ_ACK).WriteU8(1).WriteStr("101");
    Check("424 Msg Read ACK", p424.ReadU8() == 1 && p424.ReadStr() == "101");

    // 453/454 Delete Gift: the fail-closed arm preserves the cached gift.
    var p454 = new Packet(Opcode.GS_DELETEGIFT_ACK).WriteU8(0).WriteS32(10).WriteS32(20);
    Check("454 Delete Gift failure ACK", p454.ReadU8() == 0 && p454.ReadS32() == 10 && p454.ReadS32() == 20);

    // Shop fail-closed ACKs: these shapes are read by the native consumers
    // before every branch. They intentionally contain no reward, wallet, or
    // inventory success payload.
    var p205 = new Packet(Opcode.GS_BUYITEM_ACK).WriteU8(0).WriteU8(0).WriteU8(0)
        .WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0);
    Check("205 bulk purchase failure includes error pair and seven-word trailer",
        p205.Length == 31 && p205.ReadU8() == 0 && p205.ReadU8() == 0 && p205.ReadU8() == 0
        && Enumerable.Range(0, 7).All(_ => p205.ReadS32() == 0) && p205.Remaining == 0);

    var p207 = new Packet(Opcode.GS_BUY_WEAPONPARTS_ACK).WriteU8(1);
    var p209 = new Packet(Opcode.GS_SELLITEM_ACK).WriteU8(0);
    var p297 = new Packet(Opcode.GS_GIVEGIFT_ACK).WriteU8(1);
    Check("207 weapon-parts, 209 sale, and 297 gift failures have no success tail",
        p207.Length == 1 && p207.ReadU8() != 0 && p207.Remaining == 0
        && p209.Length == 1 && p209.ReadU8() == 0 && p209.Remaining == 0
        && p297.Length == 1 && p297.ReadU8() != 0 && p297.Remaining == 0);

    var p357 = new Packet(Opcode.GS_CASH_ACK).WriteU8(0).WriteS32(0);
    var p359 = new Packet(Opcode.GS_BUYCASHITEM_ACK).WriteU8(0).WriteS32(0);
    Check("357 and 359 do not assert a cash balance or an item purchase",
        p357.Length == 5 && p357.ReadU8() == 0 && p357.ReadS32() == 0 && p357.Remaining == 0
        && p359.Length == 5 && p359.ReadU8() == 0 && p359.ReadS32() == 0 && p359.Remaining == 0);

    var p469 = new Packet(Opcode.GS_BUY_HUKUBUKURO_ACK).WriteU8(1);
    var p471 = new Packet(Opcode.GS_GET_HUKUBUKURO_ACK).WriteU8(1);
    var p781 = new Packet(Opcode.GS_GET_PRESENTPACKAGE_ACK).WriteU8(1);
    Check("469, 471, and 781 use their nonzero no-list failure arms",
        p469.Length == 1 && p469.ReadU8() != 0 && p469.Remaining == 0
        && p471.Length == 1 && p471.ReadU8() != 0 && p471.Remaining == 0
        && p781.Length == 1 && p781.ReadU8() != 0 && p781.Remaining == 0);

    var p696 = new Packet(Opcode.GS_BUY_ONCEITEM_ACK).WriteU8(0).WriteS32(0).WriteS32(0);
    Check("696 once-item failure consumes its zero-result raw s32", p696.Length == 9
        && p696.ReadU8() == 0 && p696.ReadS32() == 0 && p696.ReadS32() == 0 && p696.Remaining == 0);

    var p699 = new Packet(Opcode.GP_ENTER_PEPACHI_ACK).WriteU8(0).WriteS32(0).WriteS32(0);
    var p700 = new Packet(Opcode.GP_START_GAME_REQ).WriteU8(1).WriteS32(19_900_001);
    var p701 = new Packet(Opcode.GP_START_GAME_ACK).WriteU8(0).WriteU8(0);
    var p703 = new Packet(Opcode.GP_PEPACHI_LIST_ACK).WriteS32(0).WriteS32(0);
    Check("Pepachi request is selector plus selected character id; failures do not award",
        p700.Length == 5 && p700.ReadU8() == 1 && p700.ReadS32() == 19_900_001 && p700.Remaining == 0
        && p699.Length == 9 && p699.ReadU8() == 0 && p699.ReadS32() == 0 && p699.ReadS32() == 0
        && p701.Length == 2 && p701.ReadU8() == 0 && p701.ReadU8() == 0
        && p703.Length == 8 && p703.ReadS32() == 0 && p703.ReadS32() == 0);

    var p900 = new Packet(Opcode.GS_CAPSULEMACHINE_START_REQ).WriteU8(1).WriteU8(10);
    var p901 = new Packet(Opcode.GS_CAPSULEMACHINE_START_ACK)
        .WriteU8(1).WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0);
    Check("900 is selector plus draw count; 901 failure includes its complete zero-award tail",
        p900.Length == 2 && p900.ReadU8() == 1 && p900.ReadU8() == 10 && p900.Remaining == 0
        && p901.Length == 17 && p901.ReadU8() != 0 && p901.ReadS32() == 0
        && p901.ReadS32() == 0 && p901.ReadS32() == 0 && p901.ReadS32() == 0 && p901.Remaining == 0);

    // 807's two fixed words are read before its record loop. A zero record
    // count makes this a structural empty server-controlled item set, not a
    // purchase, grant, or entitlement response.
    var p807 = new Packet(Opcode.GS_HIDDEN_ITEM_LIST_ACK).WriteU8(0).WriteU16(0).WriteU16(10);
    Check("807 zero-record hidden-item response preserves the requested category", p807.Length == 5
        && p807.ReadU8() == 0 && p807.ReadU16() == 0 && p807.ReadU16() == 10 && p807.Remaining == 0);

    // 802's request layout is unresolved. The only safe 803 response is the
    // consumer's fully evidenced no-mutation failure arm.
    var p803 = new Packet(Opcode.GS_DESTROYITEM_ACK).WriteU8(1).WriteU8(0).WriteU8(0);
    Check("803 Destroy Item failure ACK", p803.ReadU8() != 0 && p803.ReadU8() == 0
        && p803.ReadU8() == 0 && p803.Remaining == 0);

    // 876/877 Daily Quest
    var p877 = new Packet(Opcode.GQ_QUEST_ACCEPT_DAILY_ACK).WriteU8(0).WriteS32(0);
    Check("877 Daily Quest ACK", p877.ReadU8() == 0 && p877.ReadS32() == 0);

}

// ---- 11. GM / MASTER、GameCenter、AI 模式 wire 格式 round-trip -------------
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

// ---- 12. OCC 與地面武器條件式 payload（五十六輪） -------------------------
{
    // 902/904/906 的 builder 同構: u8 point, u8 self slot, s32 self uid.
    var p902 = new Packet(Opcode.GG_OCC_START_REQ).WriteU8(3).WriteU8(7).WriteS32(12345);
    Check("902 Occupy start REQ = 6B",
        p902.Length == 6 && p902.ReadU8() == 3 && p902.ReadU8() == 7 && p902.ReadS32() == 12345);

    // 903/907 action=0 的 parser 都讀完整 8B，沒有舊表格誤列的第六欄。
    var p903 = new Packet(Opcode.GG_OCC_START_ACK)
        .WriteU8(0).WriteU8(3).WriteU8(7).WriteU8(1).WriteS32(12345);
    Check("903 Occupy start ACK = 8B",
        p903.Length == 8 && p903.ReadU8() == 0 && p903.ReadU8() == 3
        && p903.ReadU8() == 7 && p903.ReadU8() == 1 && p903.ReadS32() == 12345);

    var p905 = new Packet(Opcode.GG_OCC_SUCC_ACK).WriteU8(0).WriteU8(3).WriteU8(7).WriteU8(7);
    Check("905 Occupy success ACK = 4B", p905.Length == 4 && p905.ReadU8() == 0 && p905.ReadU8() == 3);

    var p907 = new Packet(Opcode.GG_OCC_FAIL_ACK)
        .WriteU8(0).WriteU8(3).WriteU8(7).WriteU8(1).WriteS32(12345);
    Check("907 Occupy fail ACK = 8B",
        p907.Length == 8 && p907.ReadU8() == 0 && p907.ReadU8() == 3
        && p907.ReadU8() == 7 && p907.ReadU8() == 1 && p907.ReadS32() == 12345);

    // 962 是固定 13B；963 失敗則只有 result，client 不得讀 success tail。
    var p962 = new Packet(Opcode.GG_DROPWEAPON_GET_AND_DROP_REQ)
        .WriteS16(unchecked((short)0xC001)).WriteS16(100).WriteU8(2)
        .WriteS16(3).WriteS16(4).WriteF32(99.5f);
    Check("962 get-and-drop REQ = 13B", p962.Length == 13);

    var p963Rejected = new Packet(Opcode.GG_DROPWEAPON_GET_AND_DROP_ACK).WriteBool(true);
    Check("963 rejection is nonzero 1B result",
        p963Rejected.Length == 1 && p963Rejected.ReadU8() != 0 && p963Rejected.Remaining == 0);

    // result==0 + weapon!=0: 17B base + 42B metadata/state tail = 59B.
    byte[] weaponState = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
    var p963Success = new Packet(Opcode.GG_DROPWEAPON_GET_AND_DROP_ACK)
        .WriteU8(0).WriteU8(7).WriteS32(12345).WriteU16(0xC001).WriteU8(2).WriteU16(100)
        .WriteS16(10).WriteS16(20).WriteS16(30)
        .WriteU16(3).WriteU16(4).WriteU16(5).WriteF32(99.5f).WriteRaw(weaponState);
    Check("963 success with weapon state = 59B",
        p963Success.Length == 59
        && p963Success.ReadU8() == 0 && p963Success.ReadU8() == 7
        && p963Success.ReadS32() == 12345 && p963Success.ReadU16() == 0xC001
        && p963Success.ReadU8() == 2 && p963Success.ReadU16() == 100
        && p963Success.ReadS16() == 10 && p963Success.ReadS16() == 20 && p963Success.ReadS16() == 30
        && p963Success.ReadU16() == 3 && p963Success.ReadU16() == 4 && p963Success.ReadU16() == 5
        && Math.Abs(p963Success.ReadF32() - 99.5f) < 1e-6f
        && p963Success.ReadRaw(32).SequenceEqual(weaponState) && p963Success.Remaining == 0);
}

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
