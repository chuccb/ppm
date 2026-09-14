// =============================================================================
// 自測 — 不需要遊戲客戶端即可驗證 codec 正確性:
//   1) Packet 讀寫原語 round-trip (含 CP949 / 寬字串 / blob / 內嵌 packet)
//   2) PaperLz 壓縮/解壓 round-trip (高重複、隨機、RLE、文字)
//   3) PacketCodec 明文/AES/壓縮 管線 round-trip
//   4) header 欄位語意 (w0/w2/w3) — w2 僅 AES 層寫, w3 = 原始大小
//   5) 681/682/693/694 and 141/142/143/144/195/196 bootstrap wire contracts
//   6) 681→143 source-IP / one-use admission rules
//   7) zero-argument SQLite bootstrap, account upgrades, and legacy migration
// 用法: dotnet run --project src/PaperMan.SelfTest
// =============================================================================
using System.Buffers.Binary;
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

// ---- 1b. Login / channel bootstrap wire contract ---------------------------
{
    const uint revision = 0x1234ABCD;
    ulong obfuscatedDataRevision = ((ulong)(revision ^ 0xB1A9D7C7u) << 32) | 0x0000000Eu;
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
    Check("682 revision low-word guard", !LoginWire.TryDecodeDataRevision(obfuscatedDataRevision ^ 1, out _));

    var groups = new LoginChannelEntry?[]
    {
        new LoginChannelEntry(0, "Normal", 40201, 0),
        null,
        new LoginChannelEntry(3, "AI", 40202, 9, TypeThreeExtension: 0x7E),
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
    native681Layout &= nativeReader.ReadS16() == 1
        && nativeReader.ReadU8() == 0
        && nativeReader.ReadNulTerminatedAnsiString(49) == "Normal"
        && nativeReader.ReadS16() == unchecked((short)40201)
        && nativeReader.ReadU8() == 0
        && nativeReader.ReadS16() == 0
        && nativeReader.ReadS16() == 1
        && nativeReader.ReadU8() == 3
        && nativeReader.ReadNulTerminatedAnsiString(49) == "AI"
        && nativeReader.ReadS16() == unchecked((short)40202)
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

// ---- 1c. Login-to-channel admission contract -------------------------------
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

// ---- 1d. Zero-command SQLite bootstrap and login migration -----------------
{
    string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"paperman-selftest-{Guid.NewGuid():N}");
    string temporaryDatabasePath = Path.Combine(temporaryDirectory, "data", "paperman.db");
    string legacyDatabasePath = Path.Combine(temporaryDirectory, "legacy", "paperman.db");
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

            Check("SQLite second open preserves database and operator configuration",
                !secondOpen.Initialization.CreatedDatabaseFile
                && secondOpen.Initialization.ProtocolPacketDefinitionCount == 676
                && newAccount is { Result: LoginCode.Ok, AccountId: > 0, UserId: 0 }
                && acceptedPassword.Result == LoginCode.Ok
                && rejectedPassword.Result == LoginCode.BadCredentials);
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
            Db.LoginResult legacyLogin = thirdOpen.Login(
                accountName: "LegacyAccount",
                passwordOrToken: legacyPassword,
                clientDataRevision: 1,
                fingerprintSource: LoginFingerprintSource.Unavailable,
                clientFingerprint: new byte[24],
                remoteIp: "127.0.0.1");
            Check("legacy SHA256 credential authenticates once for PBKDF2 upgrade",
                legacyLogin is { Result: LoginCode.Ok, AccountId: > 0 });
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
        Directory.CreateDirectory(Path.GetDirectoryName(legacyDatabasePath)!);
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

// ---- 10. OCC 與地面武器條件式 payload（五十六輪） --------------------------
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
