// =============================================================================
// GL_WEAPONPARTS_EQUIP_CHANGE_REQ (912) → GL_WEAPONPARTS_EQUIP_CHANGE_ACK (913)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 912/913: the exact three operation forms are now known, but the native
    // corpus does not disclose original-server failure values or its complete
    // ownership/expiry mutation contract.  Parse strictly and fail closed;
    // never retain the former non-persistent fake 913 success.
    private static ValueTask GL_WEAPONPARTS_EQUIP_CHANGE_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0 || packet.Remaining < 1)
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ requires an authenticated operation.");
        }

        byte operation = packet.ReadU8();
        if (operation is not (0 or 1 or 2))
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ has an unknown operation.");
        }

        int requiredOperandBytes = operation == 2 ? 12 : 8;
        if (packet.Remaining != requiredOperandBytes)
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ has an invalid operation shape.");
        }

        _ = packet.ReadS32(); // weaponId — retained only after the mutation contract is complete.
        _ = packet.ReadS32(); // partId
        if (operation == 2)
        {
            _ = packet.ReadS32(); // oldPartId
        }

        // sub_95B180 consumes no body at all when 913.errorRaw != 0, but the
        // original nonzero code values are unresolved.  Sending an invented
        // error or a success ACK is both less faithful than rejecting with no
        // state mutation and no fabricated packet.
        throw new NotSupportedException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ mutation and 913 failure values remain unresolved.");
    }
}
