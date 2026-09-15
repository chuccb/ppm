// =============================================================================
// GL_INVENIN_REQ (254) → GL_INVENIN_ACK (255)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 254 → 255 (sub_5741C0 / sub_574270). This is not an empty scene ACK:
    // the local-user branch consumes a selected index and five 32-byte
    // NewSkill profile records after the mode-1 header.
    private static async ValueTask GL_INVENIN_REQ(Session session, Packet packet, ServerContext context)
    {
        byte requestContextRaw = packet.ReadU8();
        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("GL_INVENIN_REQ must contain exactly one context byte.");
        }

        if (session.UserId == 0)
        {
            throw new InvalidDataException("GL_INVENIN_REQ requires an authenticated player identity.");
        }

        Db.NewSkillProfileSnapshot snapshot = context.Db.GetNewSkillProfileSnapshot(session.UserId);
        NewSkillProfileRecord[] profiles = snapshot.Profiles
            .Select(profile => new NewSkillProfileRecord(profile.PuzzleItemIds, profile.ExpiresAtPackedMinute))
            .ToArray();
        Packet acknowledgement = NewSkillProfileWire.CreateInventoryEnterAcknowledgement(
            checked((int)session.UserId),
            requestContextRaw,
            snapshot.SelectedProfile,
            profiles);
        await session.SendAsync(acknowledgement);
    }
}
