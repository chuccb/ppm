// =============================================================================
// GI_CHANGE_SKILLITEMSLOT_REQ (466) → GI_CHANGE_SKILLITEMSLOT_ACK (467)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 466 → 467 (sub_5738A0 / sub_573A70): target profile, a conditional
    // previous-profile seven-id save, then an authoritative raw32 profile
    // metadata record. This is unrelated to the 9×s32 sub_527550 item block.
    private static async ValueTask GI_CHANGE_SKILLITEMSLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        NewSkillProfileChange request = NewSkillProfileWire.ReadChangeRequest(packet);
        if (session.UserId == 0)
        {
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ requires an authenticated player identity.");
        }

        Db.NewSkillProfile? selectedRecord = context.Db.ChangeNewSkillProfile(
            session.UserId,
            request.TargetProfile,
            request.HasPreviousProfileUpdate,
            request.PreviousProfile,
            request.PreviousProfilePuzzleItemIds);
        if (selectedRecord is null)
        {
            // The client parser reveals no server rejection-code mapping for
            // 467. Do not forge a nominal success or replace the server-owned
            // raw32 record with zeros; reject without state mutation instead.
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ failed NewSkill ownership, profile, or expiry validation.");
        }

        var profile = new NewSkillProfileRecord(
            selectedRecord.PuzzleItemIds,
            selectedRecord.ExpiresAtPackedMinute);
        // sub_573A70 unconditionally reads and discards these two raw header
        // bytes. The original success/error meanings are still unobserved;
        // retain the server's established zero convention, but never call it
        // a semantic success flag.
        Packet acknowledgement = NewSkillProfileWire.CreateChangeAcknowledgement(
            resultRaw: 0,
            unknownHeaderRaw: 0,
            profileIndex: request.TargetProfile,
            profile: profile);
        await session.SendAsync(acknowledgement);
    }

}
