// =============================================================================
// GS_GIVEGIFT_REQ (296) → GS_GIVEGIFT_ACK (297)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// Its request-shape verifier remains request-local.
// =============================================================================
using System.Buffers.Binary;
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 296 has a six-byte compact form and a NUL-terminated-string form. Result
    // zero has a five-s32 success-only balance tail; result one is a no-tail
    // client error arm.
    private static ValueTask GS_GIVEGIFT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsGiftRequest(packet))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GIVEGIFT_ACK).WriteU8(1));
    }

    private static bool IsGiftRequest(Packet packet)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        if (payload.Length == 6)
        {
            return true;
        }

        int recipientLength = payload.IndexOf((byte)0);
        if (recipientLength <= 0)
        {
            return false;
        }

        int offset = recipientLength + 1;
        if (offset >= payload.Length)
        {
            return false;
        }

        byte messageLengthRaw = payload[offset++];
        if (messageLengthRaw != 0)
        {
            int messageLength = payload[offset..].IndexOf((byte)0);
            if (messageLength <= 0)
            {
                return false;
            }

            offset += messageLength + 1;
        }

        if (payload.Length - offset < 6)
        {
            return false;
        }

        byte itemKind = payload[offset + 4];
        offset += 6;
        if (itemKind is 12 or 13 or 17)
        {
            if (payload.Length - offset != 2
                || BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2)) >= 0)
            {
                return false;
            }

            offset += 2;
        }

        return offset == payload.Length;
    }
}
