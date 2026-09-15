// =============================================================================
// Battle-object opcode registry
// Bound by BattleRelayHandlers.Register; this file contains no packet body.
// =============================================================================
using PaperMan.Protocol;
namespace PaperMan.Server;
public static partial class BattleObjectHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GG_OCC_START_REQ, GG_OCC_START_REQ);
        add(Opcode.GG_OCC_SUCC_REQ, GG_OCC_SUCC_REQ);
        add(Opcode.GG_OCC_FAIL_REQ, GG_OCC_FAIL_REQ);
        add(Opcode.GG_DROPWEAPON_GET_AND_DROP_REQ, GG_DROPWEAPON_GET_AND_DROP_REQ);
    }
}
