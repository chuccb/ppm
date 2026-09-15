// =============================================================================
// Single-process listener and source-bounded login/channel configuration.
//
// Validation happens before listeners open. Wire-specific metadata types live in
// ChannelBootstrapMetadata.cs so this file keeps configuration flow together.
// =============================================================================
using System.Net;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed record ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";

    /// <summary>登入 listener TCP port；頻道 listener 使用下一個 port。</summary>
    public int Port { get; init; } = 40200;

    /// <summary>
    /// AES-128 金鑰。預設 = 客戶端硬編碼金鑰 (sub_403430 的 EUC-KR 字串
    /// 「트렁크점령전머지」)。null = 明文模式 (自測/代理)。
    /// </summary>
    public byte[]? AesKey { get; init; } = PaperAes.DefaultKey.ToArray();

    /// <summary>
    /// GL_ACCOUNTCONNSUCC(694) compression threshold. A zero value means the
    /// native default 0x2580 (compression disabled). The original client only
    /// accepts a greeting value strictly below 0x2580; therefore values above
    /// that native ceiling are rejected by <see cref="Validate"/> rather than
    /// letting the channel codec and client disagree about incoming LZ frames.
    /// </summary>
    public ushort CompressThreshold { get; init; } = PacketCodec.NeverCompress;

    /// <summary>Canonical threshold written to 694 and supplied to each session codec.</summary>
    public ushort EffectiveCompressionThreshold =>
        CompressThreshold == 0 ? PacketCodec.NeverCompress : CompressThreshold;

    /// <summary>681 server selector 顯示名稱；native buffer = 50 bytes incl. NUL.</summary>
    public string ServerName { get; init; } = "PaperMan Private";

    /// <summary>681 server selector 的 channel TCP host；native buffer = 16 bytes incl. NUL.</summary>
    public string PublicHost { get; init; } = "127.0.0.1";

    /// <summary>頻道 listener port；681 的 server/channel port 都指向此處。</summary>
    public int ChannelPort => checked(Port + 1);

    /// <summary>
    /// 196 / 142 下發的 IPv4 UDP control endpoint。預設是 channel port 的下一個
    /// port。The implemented server behavior is only private 19→empty-20; this
    /// setting must not be read as a claim that a UDP relay exists. Set
    /// <see cref="UdpHostOverride"/> / <see cref="UdpPortOverride"/> only when
    /// that control endpoint is hosted on another IPv4 machine.
    /// </summary>
    public string UdpHost => UdpHostOverride ?? PublicHost;
    public string? UdpHostOverride { get; init; }
    public int UdpPort => UdpPortOverride ?? checked(ChannelPort + 1);
    public int? UdpPortOverride { get; init; }

    /// <summary>
    /// PM_UDPSTART_ACK(144) metadata after its channel name. The native client
    /// uses the documented fields in <see cref="UdpStartAcknowledgement"/>;
    /// intentionally wire-oriented names remain where the server-domain
    /// semantics are still unproven.
    /// </summary>
    public UdpStartAcknowledgementMetadata UdpStartMetadata { get; init; } =
        UdpStartAcknowledgementMetadata.Neutral;

    /// <summary>
    /// Clock used to form PM_CONNECT_ACK(142)'s timezone-less calendar value.
    /// Inject this in tests; production defaults to the system clock.
    /// </summary>
    public TimeProvider ProtocolTimeProvider { get; init; } = System.TimeProvider.System;

    /// <summary>
    /// Wall-clock zone encoded in PM_CONNECT_ACK(142). Because the native wire
    /// value carries no UTC offset, set this deliberately when the game service
    /// and desired game calendar use different zones.
    /// </summary>
    public TimeZoneInfo ProtocolTimeZone { get; init; } = TimeZoneInfo.Local;

    /// <summary>Fields following a successful 196 UDP endpoint and channel type.</summary>
    public EnterChannelAcknowledgementMetadata EnterChannelMetadata { get; init; } =
        EnterChannelAcknowledgementMetadata.NativeClientInitialValues;

    /// <summary>Gets the configured wall-clock time to encode in PM_CONNECT_ACK(142).</summary>
    public DateTimeOffset GetProtocolCalendarTime() =>
        TimeZoneInfo.ConvertTime(ProtocolTimeProvider.GetUtcNow(), ProtocolTimeZone);

    // ---------------- GL_LOGIN_ACK (681) success tail ----------------

    /// <summary>
    /// Native variable <c>n100</c>: client stores it verbatim and echoes it in
    /// 143.  Values 100/101 additionally enable the native charge UI.  Its
    /// backend business name is not recoverable, so it is deliberately a named
    /// billing UI mode rather than a misleading "level" constant.
    /// </summary>
    public int BillingUiMode { get; init; } = 100;

    /// <summary>
    /// Optional one-tuple account/net-café feature extension.  null is the
    /// semantic ext_count=0 form; a value is ext_count=1.  The native parser
    /// does not safely consume a second tuple.
    /// </summary>
    public LoginFeatureExtension? LoginFeatureExtension { get; init; }

    /// <summary>Final two 681 s32 values passed to the native Tricod billing client.</summary>
    public LoginBillingMetadata LoginBilling { get; init; } = LoginBillingMetadata.None;

    /// <summary>681 s16 server id.</summary>
    public short LoginServerId { get; init; } = 1;

    /// <summary>681 opaque s16 group associated with the listed server.</summary>
    public short LoginServerGroup { get; init; }

    /// <summary>681 opaque server-list flag.</summary>
    public byte LoginServerListingFlag { get; init; }

    /// <summary>Which of the exactly three 681 channel groups contains this server's channel.</summary>
    public byte ChannelGroupIndex { get; init; }

    /// <summary>195's group-local channel index. One advertised entry means the default is zero.</summary>
    public byte ChannelIndex { get; init; }

    /// <summary>681 channel display name; 144 reuses it as its channel-name field.</summary>
    public string ChannelName { get; init; } = "Ch.1";

    /// <summary>681 channel type. Type 3 requires <see cref="ChannelTypeThreeExtension"/>.</summary>
    public byte ChannelType { get; init; }

    /// <summary>Required only for a type-3 681 channel; native reads it as an extra u8.</summary>
    public byte? ChannelTypeThreeExtension { get; init; }

    /// <summary>681 opaque channel-list flag.</summary>
    public byte ChannelListingFlag { get; init; }

    /// <summary>196's server-assigned s32 channel id.</summary>
    public int ChannelId { get; init; } = 1;

    /// <summary>How long a successful 681 may be claimed once through 143 on the channel TCP connection.</summary>
    public TimeSpan ChannelAdmissionLifetime { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Fails before listeners are opened when a value would overflow a native fixed buffer or wire field.</summary>
    public ServerConfig Validate()
    {
        if (!IPAddress.TryParse(ListenHost, out var listenAddress)
            || listenAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // CUDPManager creates socket(AF_INET, SOCK_DGRAM) and the server
            // now owns that advertised endpoint as well as the TCP listeners.
            throw new ArgumentException("Listen host must be a numeric IPv4 address.", nameof(ListenHost));
        }

        if (!IPAddress.TryParse(UdpHost, out var udpAddress)
            || udpAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // sub_596DA0 passes the successful-196 endpoint to inet_addr.
            // A DNS name or IPv6 literal is not a client-compatible UDP host.
            throw new ArgumentException("UDP host must be a numeric IPv4 address.", nameof(UdpHost));
        }

        // Need login, channel, and (by default) UDP ports without overflow.
        if (Port is < 1 or > 65533)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), "Port must leave valid ports for both channel and UDP endpoints (1..65533).");
        }

        if (UdpPort is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(UdpPort), "UDP port must be in 1..65535.");
        }

        // CLobbyLogin::sub_43E500 receives 694 but retains its initial 0x2580
        // threshold whenever the advertised value is >= 0x2580. Do not let a
        // server encoder use a larger threshold than the client can negotiate.
        if (CompressThreshold > PacketCodec.NeverCompress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CompressThreshold),
                "694 accepts only 0 (native default), or 1..0x2580; larger values are ignored by the client.");
        }

        ArgumentNullException.ThrowIfNull(ProtocolTimeProvider);
        ArgumentNullException.ThrowIfNull(ProtocolTimeZone);
        if (AesKey is { Length: not 16 })
        {
            throw new ArgumentException("AES-128 key must be exactly 16 bytes.", nameof(AesKey));
        }

        // Fail at startup, rather than after the UDP manager has requested
        // 141, if an injected clock cannot produce the native 142 calendar.
        _ = PmConnectCalendarTime.From(GetProtocolCalendarTime()).ToWireValue();

        if (ChannelGroupIndex > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(ChannelGroupIndex), "GL_LOGIN_ACK has exactly three channel groups (indices 0..2).");
        }

        if (ChannelIndex != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChannelIndex), "This 681 builder advertises one channel per group, so its selectable index is zero.");
        }

        if (ChannelAdmissionLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ChannelAdmissionLifetime), "Channel admission lifetime must be positive.");
        }

        // sub_555C60 copies native n100 through a signed char local before
        // widening it to the s32 written in 143. Keep an advertised token in
        // that lossless range so 143 echoes precisely what 681 advertised.
        if (BillingUiMode is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(BillingUiMode), "681 billing UI mode must fit the signed-byte 143 echo path.");
        }

        LoginWire.RequireAnsiString(ServerName, LoginWire.MaxServerOrChannelNameBytes, nameof(ServerName));
        LoginWire.RequireAnsiString(PublicHost, LoginWire.MaxLoginServerHostBytes, nameof(PublicHost));
        LoginWire.RequireAnsiString(ChannelName, LoginWire.MaxServerOrChannelNameBytes, nameof(ChannelName));
        LoginWire.RequireAnsiString(ChannelName, LoginWire.MaxUdpStartChannelNameBytes, "144 channel name");
        LoginWire.RequireAnsiString(UdpHost, LoginWire.MaxUdpHostBytes, nameof(UdpHost));

        if ((ChannelType == 3) != ChannelTypeThreeExtension.HasValue)
        {
            throw new ArgumentException("A type-3 channel needs exactly one type-three extension byte; other types need none.");
        }

        UdpStartMetadata.Validate();

        // CLobbyChannel::sub_4179D0 reads a large sub_875680 AI payload when
        // 196 declares channel type 3. This server implements every non-type-3
        // 196 branch, but must reject a configuration that would advertise the
        // AI packet tail it cannot produce.
        if (ChannelType == 3)
        {
            throw new NotSupportedException("Type-3 AI channel bootstrap is not implemented; configure a normal channel type.");
        }

        return this;
    }

    /// <summary>Builds the one-server/one-channel 681 list represented by this single-process host.</summary>
    public LoginAcknowledgement CreateLoginAcknowledgement(int userId)
    {
        LoginChannelEntry?[] channelGroups = [null, null, null];
        channelGroups[ChannelGroupIndex] = new LoginChannelEntry(
            ChannelType,
            ChannelName,
            checked((ushort)ChannelPort),
            ChannelListingFlag,
            ChannelTypeThreeExtension);

        var server = new LoginServerEntry(
            LoginServerId,
            ServerName,
            PublicHost,
            checked((ushort)ChannelPort),
            LoginServerListingFlag,
            LoginServerGroup,
            channelGroups);

        return new LoginAcknowledgement(
            (int)LoginCode.Ok,
            new LoginAcknowledgementSuccess(
                userId,
                BillingUiMode,
                LoginFeatureExtension,
                [server],
                LoginBilling));
    }

}
