// =============================================================================
// GS_BUYCASHITEM_REQ (358) → GS_BUYCASHITEM_ACK (359)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// Its request-shape verifier remains request-local.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 358 is {u8 count, count×{s32 itemId,s32 clientCalculatedPrice}}.
    // `sub_5725D0` always reads {u8 resultCount, s32 rawHeader}; zero result
    // count has no item records and does not mutate client state.
    private static ValueTask GS_BUYCASHITEM_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!IsCashPurchaseRequest(packet))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUYCASHITEM_ACK)
            .WriteU8(0)
            .WriteS32(0));
    }

    private static bool IsCashPurchaseRequest(Packet packet)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        return payload.Length >= 1 && payload.Length == 1 + payload[0] * 8;
    }
}
