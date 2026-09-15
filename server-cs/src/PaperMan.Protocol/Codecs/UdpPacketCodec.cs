// =============================================================================
// UDP datagram framing — direct counterpart of CUDPManager's transport path.
//
// Native send:    sub_595980 -> sub_591F90 -> sub_592F60 -> sendto(word0 + 8)
// Native receive: sub_595A60 -> sub_591FB0/sub_591D50 -> sub_5930C0
//
// Unlike TCP's sub_593280/sub_593320 pipeline, this path does NOT call the LZ
// functions. UDP always encrypts a 16-byte-padded payload, including an empty
// payload; it uses the client's fixed PaperAes.DefaultKey and a zero IV.
// =============================================================================
using System.Buffers.Binary;

namespace PaperMan.Protocol;

/// <summary>
/// Encodes and decodes one native UDP datagram. It deliberately cannot use the
/// configurable TCP compression threshold: CUDPManager has no LZ stage.
/// </summary>
public sealed class UdpPacketCodec : IDisposable
{
    /// <summary>sub_595A60's recvfrom buffer length.</summary>
    public const int MaxDatagramSize = 9600;

    /// <summary>sub_592FB0/sub_593110 reject this ciphertext size and above.</summary>
    private const int MaxEncryptedSizeExclusive = 0x2578;

    private readonly PaperAes _aes = new(PaperAes.DefaultKey);

    /// <summary>
    /// Builds the exact header and CFB-128 ciphertext sent by sub_595980. No
    /// UDP LZ compression is attempted, even when a TCP session negotiated it.
    /// </summary>
    public byte[] Encode(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        ReadOnlySpan<byte> payload = packet.Payload;
        int ciphertextLength = Align16(payload.Length);
        if (ciphertextLength >= MaxEncryptedSizeExclusive)
        {
            throw new InvalidOperationException(
                $"UDP payload is too large for the native AES path ({ciphertextLength} >= 0x{MaxEncryptedSizeExclusive:X}).");
        }

        var frame = new byte[Packet.HeaderSize + ciphertextLength];
        var ciphertext = frame.AsSpan(Packet.HeaderSize);
        payload.CopyTo(ciphertext);
        _aes.EncryptCfb(ciphertext);

        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)ciphertextLength));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), packet.OpcodeRaw);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), checked((ushort)payload.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), checked((ushort)payload.Length));
        return frame;
    }

    /// <summary>
    /// Decodes one datagram accepted by sub_595A60. Native code accepts a
    /// datagram with bytes after <c>word0 + 8</c>; those trailing bytes are not
    /// packet payload and are ignored here for fidelity.
    /// </summary>
    public Packet DecodeDatagram(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < Packet.HeaderSize)
        {
            throw new EndOfStreamException("UDP datagram has a short Packet header.");
        }

        ushort ciphertextLength = BinaryPrimitives.ReadUInt16LittleEndian(datagram);
        ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(datagram[2..]);
        ushort plaintextLength = BinaryPrimitives.ReadUInt16LittleEndian(datagram[4..]);
        ushort originalLength = BinaryPrimitives.ReadUInt16LittleEndian(datagram[6..]);
        int frameLength = checked(Packet.HeaderSize + ciphertextLength);

        if (datagram.Length < frameLength)
        {
            throw new EndOfStreamException(
                $"UDP datagram is shorter than its header declares ({datagram.Length} < {frameLength}).");
        }

        if (!HasNativeAesShape(ciphertextLength, plaintextLength))
        {
            throw new InvalidDataException(
                $"UDP datagram has an invalid AES shape: w0={ciphertextLength}, w2={plaintextLength}.");
        }

        var plaintextPadded = datagram.Slice(Packet.HeaderSize, ciphertextLength).ToArray();
        _aes.DecryptCfb(plaintextPadded);
        return Packet.FromWire(opcode, plaintextLength, originalLength, plaintextPadded[..plaintextLength]);
    }

    public void Dispose() => _aes.Dispose();

    private static int Align16(int length) => length == 0 ? 16 : (length + 15) & ~15;

    private static bool HasNativeAesShape(ushort ciphertextLength, ushort plaintextLength) =>
        ciphertextLength >= 16
        && (ciphertextLength & 0xF) == 0
        && ciphertextLength == Align16(plaintextLength)
        && ciphertextLength < MaxEncryptedSizeExclusive;
}
