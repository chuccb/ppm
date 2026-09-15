// =============================================================================
// MASTER_USERINFO_REQ (289) → MASTER_USERINFO_ACK (290)
// File and handler entry use the canonical opcode token verbatim.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class MasterHandlers
{
    // 289 MASTER_USERINFO_REQ (str nick) → 290 ACK (sub_579830: 完整 CClientData 快照)
    private static async ValueTask MASTER_USERINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);
        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.MASTER_USERINFO_ACK).WriteBool(false));
            return;
        }

        await session.SendAsync(new Packet(Opcode.MASTER_USERINFO_ACK)
            .WriteBool(true)
            .WriteS32((int)info.UserId)
            .WriteStr(info.Nickname));
    }
}
