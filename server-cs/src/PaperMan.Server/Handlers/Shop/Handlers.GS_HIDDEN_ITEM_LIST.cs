// =============================================================================
// GS_HIDDEN_ITEM_LIST_REQ (806) → GS_HIDDEN_ITEM_LIST_ACK (807)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// Its native selector verifier remains request-local.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 806 is exactly one signed category selector. Both recovered 807 readers
    // consume a byte, a u16 count, and a u16 category before their record loops.
    // A count of zero skips every unverified server-controlled record and still
    // lets the client resolve its resource-backed base shop/parts view. The
    // first u8 has no recovered reader use; zero is only a structural value.
    private static ValueTask GS_HIDDEN_ITEM_LIST_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return ValueTask.CompletedTask;
        }

        short category = packet.ReadS16();
        if (!IsNativeHiddenItemCategory(category))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_HIDDEN_ITEM_LIST_ACK)
            .WriteU8(0)
            .WriteU16(0)
            .WriteU16((ushort)category));
    }

    // Native shop UI emits 1..13 and 15..24; 14 has no recovered sender.
    // CLobbyPartsUpRoom independently emits 25 during initialization.
    private static bool IsNativeHiddenItemCategory(short category) =>
        category is >= 1 and <= 13
            or >= 15 and <= 25;
}
