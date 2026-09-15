// =============================================================================
// Quest opcode registry
// This contains no packet implementation: each official request token binds to
// its same-token handler entry in a direct request/ACK-family source file.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GL_SERVER_DATETIME_REQ, GL_SERVER_DATETIME_REQ);
        add(Opcode.GQ_QUEST_ACCEPT_REQ, GQ_QUEST_ACCEPT_REQ);
        add(Opcode.GQ_QUEST_CANCEL_REQ, GQ_QUEST_CANCEL_REQ);
        add(Opcode.GQ_QUEST_ACCEPT_DAILY_REQ, GQ_QUEST_ACCEPT_DAILY_REQ);
        add(Opcode.GQ_QUEST_USER_COMPLETE_HONOR_REQ, GQ_QUEST_USER_COMPLETE_HONOR_REQ);
    }
}
