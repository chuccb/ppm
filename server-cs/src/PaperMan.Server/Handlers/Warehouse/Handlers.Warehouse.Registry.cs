// =============================================================================
// Warehouse opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class WarehouseHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_MYWAREHOUSEINFO_REQ, GL_MYWAREHOUSEINFO_REQ);
        add(Opcode.GL_MYWAREHOUSEITEMLIST_REQ, GL_MYWAREHOUSEITEMLIST_REQ);
        add(Opcode.GL_PUSH_TO_WAREHOUSE_REQ, GL_PUSH_TO_WAREHOUSE_REQ);
        add(Opcode.GL_POP_TO_WAREHOSUE_REQ, GL_POP_TO_WAREHOSUE_REQ);
    }
}
