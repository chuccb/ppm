// =============================================================================
// GL_CHANGECHANNEL_REQ (370) → GL_CHANGECHANNEL_ACK (371)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 370 GL_CHANGECHANNEL_REQ (sub_570030: u8 channel_id)
    // → 371 ACK (sub_570100): u8 status(1=成功), u8 channel_id, str host_ip, s32 host_port, u8 extra
    private static async ValueTask GL_CHANGECHANNEL_REQ(Session session, Packet packet, ServerContext context)
    {
        byte ch = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        var ack = new Packet(Opcode.GL_CHANGECHANNEL_ACK)
            .WriteU8(1)                                     // status 1 = 成功
            .WriteU8(ch)                                    // channel_id
            // This is sub_570100 → sub_596E60's *secondary* UDP address.
            // Do not substitute the successful-196 UdpHost/UdpPort here:
            // client evidence has not established the two fields' equivalence.
            .WriteStr(context.Config.PublicHost)
            .WriteS32(context.Config.ChannelPort + 1)       // independent s32; native consumer takes low u16
            .WriteU8(0);                                    // extra

        await session.SendAsync(ack);
    }

}
