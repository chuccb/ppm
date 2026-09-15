// =============================================================================
// GL_MYINFO_OPEN (270)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 270 GL_MYINFO_OPEN (sub_556680): s8 — 個資公開開關 (單向通知,
    // 無 ACK; 卅六輪) — 記錄即可
    private static ValueTask GL_MYINFO_OPEN(Session session, Packet packet, ServerContext context)
    {
        _ = packet.Remaining >= 1 ? packet.ReadS8() : (sbyte)0;
        return ValueTask.CompletedTask;
    }
}
