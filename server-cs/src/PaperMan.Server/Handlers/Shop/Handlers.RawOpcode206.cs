// =============================================================================
// Raw opcode 206 → GS_BUY_WEAPONPARTS_ACK (207)
// No official 206 request token exists in the native registry or generated enum.
// `RawOpcode206_REQ` is deliberately a neutral local label, never an invented protocol name.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // 206 is exactly {s32 itemId,s32 rawContext,u8 itemKind,s32 rawPeriod}.
    // In `sub_571B60`, raw result zero enters the success decoder and mutates
    // the local parts/wallet cache. Any nonzero result has no tail.
    [RawOpcodeHandler(206)]
    private static ValueTask RawOpcode206_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 13)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUY_WEAPONPARTS_ACK).WriteU8(1));
    }
}
