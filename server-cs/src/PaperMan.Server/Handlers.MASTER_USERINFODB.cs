// =============================================================================
// MASTER_USERINFODB_REQ (293) → MASTER_USERINFODB_ACK (294)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 293 MASTER_USERINFODB_REQ (str nick) → 294 ACK (sub_57A540)
    private static async ValueTask MASTER_USERINFODB_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);
        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.MASTER_USERINFODB_ACK).WriteBool(false));
            return;
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERINFODB_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            .WriteStr(info.Nickname));
    }
}
