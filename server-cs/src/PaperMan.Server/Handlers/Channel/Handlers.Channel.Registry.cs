// =============================================================================
// Channel opcode registry
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ChannelHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.PM_UDPSTART_REQ, PM_UDPSTART_REQ);
        add(Opcode.GC_ENTERCHANNEL_REQ, GC_ENTERCHANNEL_REQ);
        add(Opcode.GC_CHANNEL_REQ, GC_CHANNEL_REQ);
        add(Opcode.PM_CONNECT_REQ, PM_CONNECT_REQ);
    }
}
