// =============================================================================
// Shared channel-bootstrap wire values.
//
// ChannelEndpoint is the same four-byte-port representation consumed by the
// independent 142 and successful 196 readers. Validation remains packet-layout
// validation; it does not decide whether a client may use that endpoint.
// =============================================================================
namespace PaperMan.Protocol;

public static partial class ChannelBootstrapWire
{
    private static void ValidateEndpoint(ChannelEndpoint endpoint, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        LoginWire.RequireAnsiString(endpoint.Host, LoginWire.MaxUdpHostBytes, fieldName);
        if (endpoint.Port is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endpoint),
                $"{fieldName} port must be within the low unsigned 16-bit range (1..65535).");
        }
    }
}

/// <summary>
/// A 142/196 network endpoint. The wire port is four bytes, but the native
/// connection functions consume its lower unsigned 16 bits.
/// </summary>
public sealed record ChannelEndpoint(string Host, int Port);
