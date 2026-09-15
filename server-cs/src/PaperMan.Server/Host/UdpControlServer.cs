// =============================================================================
// UDP-private control endpoint.
//
// Client evidence boundary:
//   196 success -> sub_595C90 configures CUDPManager's AF_INET UDP endpoint.
//   sub_596670 sends private opcode 19 on that primary endpoint.
//   private opcode 20 -> sub_5968C0 completes the client's pending control wait.
//
// The original server-side admission/identity rules have not been recovered.
// Therefore this intentionally implements only the observed client-compatible
// minimum: decode an encrypted UDP frame, require the exact opcode-19 wire
// shape, and reply immediately with an encrypted empty opcode 20 to its source.
// AES-CFB framing supplies no recovered authentication/MAC property.
// It does not elevate client-reported identity fields into server authority.
// =============================================================================
using System.Net;
using System.Net.Sockets;
using PaperMan.Protocol;

namespace PaperMan.Server;

/// <summary>
/// Stateless endpoint for the directly evidenced UDP-private 19 -> 20 control
/// exchange. Other private dispatcher values remain unsupported until their
/// client and server behavior are separately evidenced.
/// </summary>
public sealed class UdpControlServer : IDisposable
{
    private readonly Socket _socket;
    private readonly UdpPacketCodec _codec = new();

    public UdpControlServer(ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        IPAddress listenAddress = IPAddress.Parse(config.ListenHost);
        if (listenAddress.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new NotSupportedException(
                "The native CUDPManager uses AF_INET/inet_addr; its control endpoint must listen on IPv4.");
        }

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(listenAddress, config.UdpPort));
    }

    public IPEndPoint LocalEndpoint => (IPEndPoint)(_socket.LocalEndPoint
        ?? throw new InvalidOperationException("UDP socket has no local endpoint."));

    /// <summary>Receives and processes datagrams serially, preserving arrival order.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var receiveBuffer = new byte[UdpPacketCodec.MaxDatagramSize];
        EndPoint receiveTemplate = new IPEndPoint(IPAddress.Any, 0);

        while (!cancellationToken.IsCancellationRequested)
        {
            SocketReceiveFromResult received;
            try
            {
                received = await _socket.ReceiveFromAsync(
                    receiveBuffer,
                    SocketFlags.None,
                    receiveTemplate,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException exception)
            {
                Console.WriteLine($"[udp] receive error: {exception.SocketErrorCode} ({exception.Message})");
                continue;
            }

            if (received.RemoteEndPoint is not IPEndPoint remote)
            {
                Console.WriteLine("[udp] ignored datagram with a non-IP endpoint.");
                continue;
            }

            try
            {
                await HandleDatagramAsync(receiveBuffer.AsMemory(0, received.ReceivedBytes), remote, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is ArgumentException
                or EndOfStreamException
                or InvalidDataException)
            {
                Console.WriteLine($"[udp] ignored malformed datagram from {remote}: {exception.Message}");
            }
            catch (SocketException exception)
            {
                Console.WriteLine($"[udp] send error to {remote}: {exception.SocketErrorCode} ({exception.Message})");
            }
        }
    }

    private async ValueTask HandleDatagramAsync(
        ReadOnlyMemory<byte> datagram,
        IPEndPoint remote,
        CancellationToken cancellationToken)
    {
        Packet packet = _codec.DecodeDatagram(datagram.Span);
        if (packet.OpcodeRaw != (ushort)UdpPrivateOpcode.Opcode19)
        {
            Console.WriteLine($"[udp] ignored unsupported private opcode {packet.OpcodeRaw} from {remote}");
            return;
        }

        // Parsing every source-proven field deliberately rejects truncated or
        // appended opcode-19 shapes. Values are logged only as diagnostics:
        // source evidence does not justify using them as authorization.
        UdpControlRequest request = UdpControlRequest.Read(packet);
        Console.WriteLine(
            $"[udp] control request from {remote}: channel={request.ActiveChannelIndex}, " +
            $"roomSlot={request.CurrentRoomSlot}, playerId={request.ClientReportedPlayerId}, " +
            $"nickname='{ToLogSafe(request.LocalNickname)}'");

        // sub_5968C0 does not read its Packet argument. An empty encrypted
        // payload is therefore the only completion body directly justified.
        var completion = new Packet((Opcode)UdpPrivateOpcode.Opcode20);
        byte[] response = _codec.Encode(completion);
        _ = await _socket.SendToAsync(response, SocketFlags.None, remote, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _codec.Dispose();
        _socket.Dispose();
    }

    private static string ToLogSafe(string value) => value.Replace("\r", "\\r").Replace("\n", "\\n");
}
