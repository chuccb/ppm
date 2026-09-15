// =============================================================================
// Shop opcode registry
// This contains no packet implementation: named entries bind their official request
// token to the same-token direct handler. Raw opcode 206 is the sole documented
// exception because this binary has no recovered request token for it.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ShopHandlers
{
    // =============================================================================
    // Shop request boundary
    //
    // The client wire shapes below are direct consumer/builder evidence.  The
    // original service's catalog, price, entitlement, reward, and probability
    // policies are not present in the client or Extracted resources.  Therefore
    // all purchase, sale, gift, bag/package, Pepachi, and capsule paths fail
    // closed: a structurally valid failure ACK, no request-dependent decoding, and
    // no wallet/inventory/gift mutation.  Do not turn any of these into success
    // paths without evidence for both the server policy and its success payload.
    // =============================================================================
    // `206` has no name in the native opcode-name registry. `sub_571620`
    // constructs it immediately before the 207 consumer, so this local name
    // is an implementation label rather than a recovered native symbol.
    private const Opcode RawOpcode206 = (Opcode)206;

    public static void Register(Registrar add)
    {
        add(Opcode.GS_CASH_REQ, GS_CASH_REQ);
        add(Opcode.GS_BUYITEM_REQ, GS_BUYITEM_REQ);
        add(RawOpcode206, RawOpcode206_REQ);
        add(Opcode.GS_SELLITEM_REQ, GS_SELLITEM_REQ);
        add(Opcode.GS_GIVEGIFT_REQ, GS_GIVEGIFT_REQ);
        add(Opcode.GS_BUYCHAR_REQ, GS_BUYCHAR_REQ);
        add(Opcode.GS_BUYCASHITEM_REQ, GS_BUYCASHITEM_REQ);
        add(Opcode.GS_DELETEGIFT_REQ, GS_DELETEGIFT_REQ);
        add(Opcode.GS_BUY_HUKUBUKURO_REQ, GS_BUY_HUKUBUKURO_REQ);
        add(Opcode.GS_GET_HUKUBUKURO_REQ, GS_GET_HUKUBUKURO_REQ);
        add(Opcode.GS_BUY_ONCEITEM_REQ, GS_BUY_ONCEITEM_REQ);
        add(Opcode.GS_GET_PRESENTPACKAGE_REQ, GS_GET_PRESENTPACKAGE_REQ);
        add(Opcode.GS_DESTROYITEM_REQ, GS_DESTROYITEM_REQ);
        add(Opcode.GS_HIDDEN_ITEM_LIST_REQ, GS_HIDDEN_ITEM_LIST_REQ);
        add(Opcode.GP_ENTER_PEPACHI_REQ, GP_ENTER_PEPACHI_REQ);
        add(Opcode.GP_START_GAME_REQ, GP_START_GAME_REQ);
        add(Opcode.GP_PEPACHI_LIST_REQ, GP_PEPACHI_LIST_REQ);
        add(Opcode.GS_CAPSULEMACHINE_START_REQ, GS_CAPSULEMACHINE_START_REQ);
    }
}
