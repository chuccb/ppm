// =============================================================================
// GS_CAPSULEMACHINE_START_REQ (900) → GS_CAPSULEMACHINE_START_ACK (901)
// File and handler entry use the canonical opcode token verbatim. The source retains the established fail-closed wire boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 900 is exactly {u8 paymentSelector,u8 drawCount}. The listed pairs are
    // the direct caller combinations, including code paths whose XML buttons
    // are commented out in this resource revision. `sub_9A1A30` always reads
    // count and three trailing s32s even for failure. Count zero prevents
    // per-award reads and nonzero status avoids wallet/reward updates.
    private static ValueTask GS_CAPSULEMACHINE_START_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return ValueTask.CompletedTask;
        }

        byte paymentSelector = packet.ReadU8();
        byte drawCount = packet.ReadU8();
        bool isNativeCallerPair = (paymentSelector, drawCount) is (1, 1) or (1, 10) or (2, 1) or (3, 1);
        if (!isNativeCallerPair)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_CAPSULEMACHINE_START_ACK)
            .WriteU8(1)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0));
    }
}
