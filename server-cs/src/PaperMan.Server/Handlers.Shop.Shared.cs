// =============================================================================
// Shop shared request-framing support
// This contains no receive entry. It is limited to source-proven framing/range checks
// required by more than one canonical Shop request family.
// =============================================================================
using System.Buffers.Binary;
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 204 and 468 have one body grammar. `sub_571100` changes the opcode to
    // 468 iff at least one selected ID is in a Hukubukuro range. This checks
    // only native framing/routing, not purchase authority or resource price.
    private static bool IsBulkPurchaseRequest(Packet packet, bool requireHukubukuroItem)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        if (payload.Length < 1 || payload[0] == 0)
        {
            return false;
        }

        int offset = 1;
        bool hasHukubukuroItem = false;
        for (int i = 0; i < payload[0]; i++)
        {
            if (payload.Length - offset < 7)
            {
                return false;
            }

            int itemId = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, 4));
            byte itemKind = payload[offset + 4];
            offset += 7;
            hasHukubukuroItem |= IsHukubukuroItemId(itemId);

            if (itemKind is 12 or 13 or 17)
            {
                if (payload.Length - offset < 2
                    || BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2)) >= 0)
                {
                    return false;
                }

                offset += 2;
            }
        }

        return offset == payload.Length && hasHukubukuroItem == requireHukubukuroItem;
    }

    private static bool IsPackageDetailRequest(Packet packet, Func<int, bool> hasExpectedItemRange)
    {
        if (packet.Remaining != 10)
        {
            return false;
        }

        int itemId = BinaryPrimitives.ReadInt32LittleEndian(packet.Payload.Slice(4, 4));
        short encodedVariant = BinaryPrimitives.ReadInt16LittleEndian(packet.Payload.Slice(8, 2));
        return encodedVariant < 0 && hasExpectedItemRange(itemId);
    }

    private static bool IsHukubukuroItemId(int itemId) =>
        itemId is >= 15_301_001 and <= 15_302_000
            or >= 15_310_001 and <= 15_320_000;
}
