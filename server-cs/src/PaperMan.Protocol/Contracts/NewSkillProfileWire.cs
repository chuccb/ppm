// =============================================================================
// NewSkill profile wire contracts.
//
// Native evidence:
//   GL_INVENIN_REQ/ACK 254/255: sub_5741C0 / sub_574270
//   GI_CHANGE_SKILLITEMSLOT_REQ/ACK 466/467: sub_5738A0 / sub_573A70
//
// A profile is seven s32 puzzle item ids followed by one packed-minute s32.
// The 466 request intentionally carries only the seven-id prefix when it
// saves the profile being left; its 32nd byte belongs to server authority.
// =============================================================================
namespace PaperMan.Protocol;

public sealed record NewSkillProfileRecord(int[] PuzzleItemIds, int ExpiresAtPackedMinute);

public sealed record NewSkillProfileChange(
    byte TargetProfile,
    byte PreviousProfileUpdateRaw,
    byte PreviousProfile,
    int[] PreviousProfilePuzzleItemIds)
{
    /// <summary>Native sender branches on any nonzero raw flag, not only one.</summary>
    public bool HasPreviousProfileUpdate => PreviousProfileUpdateRaw != 0;
}

/// <summary>Exact client-visible layouts for the five account NewSkill profiles.</summary>
public static class NewSkillProfileWire
{
    public const int ProfileCount = 5;
    public const int PuzzleSlotCount = 7;
    public const int ProfileByteCount = 32;
    public const int ChangeWithoutUpdateByteCount = 2;
    public const int ChangeWithUpdateByteCount = 31;

    // sub_574270 accepts modes 0 and 1. The mode-1 branch consumes this
    // self snapshot header in its natural order: uid, request context, raw.
    public const byte SelfSnapshotMode = 1;

    /// <summary>
    /// Reads the full 466 request. The native sender emits exactly 2 bytes, or
    /// exactly 31 bytes when the previous selected profile has to be saved.
    /// </summary>
    public static NewSkillProfileChange ReadChangeRequest(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Length < ChangeWithoutUpdateByteCount)
        {
            throw new InvalidDataException(
                $"GI_CHANGE_SKILLITEMSLOT_REQ must include target and update bytes, not {packet.Length} bytes.");
        }

        byte targetProfile = packet.ReadU8();
        byte previousProfileUpdateRaw = packet.ReadU8();
        int expectedLength = previousProfileUpdateRaw == 0
            ? ChangeWithoutUpdateByteCount
            : ChangeWithUpdateByteCount;
        if (packet.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"GI_CHANGE_SKILLITEMSLOT_REQ with update byte {previousProfileUpdateRaw} must be {expectedLength} bytes, not {packet.Length}.");
        }

        if (previousProfileUpdateRaw == 0)
        {
            return new NewSkillProfileChange(targetProfile, previousProfileUpdateRaw, 0, []);
        }

        byte previousProfile = packet.ReadU8();
        var puzzleItemIds = new int[PuzzleSlotCount];
        for (int slot = 0; slot < puzzleItemIds.Length; slot++)
        {
            puzzleItemIds[slot] = packet.ReadS32();
        }

        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ has trailing data after its seven-id update block.");
        }

        return new NewSkillProfileChange(targetProfile, previousProfileUpdateRaw, previousProfile, puzzleItemIds);
    }

    /// <summary>
    /// Builds the local-user mode-1 255 profile snapshot. <paramref name="requestContextRaw"/>
    /// is echoed structurally; client code supplies it from an opaque UI object
    /// and this corpus has not established its domain semantics.
    /// </summary>
    public static Packet CreateInventoryEnterAcknowledgement(
        int userId,
        byte requestContextRaw,
        byte selectedProfile,
        IReadOnlyList<NewSkillProfileRecord> profiles)
    {
        ValidateSnapshot(selectedProfile, profiles);

        var acknowledgement = new Packet(Opcode.GL_INVENIN_ACK)
            .WriteU8(SelfSnapshotMode)
            .WriteS32(userId)
            .WriteU8(requestContextRaw)
            // The fourth mode-1 header byte is read but has no observed
            // local-user consumer in sub_574270. It remains an explicit raw
            // reserved value rather than being treated as payload padding.
            .WriteU8(0)
            .WriteU8(selectedProfile);

        foreach (NewSkillProfileRecord profile in profiles)
        {
            WriteProfile(acknowledgement, profile);
        }

        return acknowledgement;
    }

    /// <summary>
    /// Builds one authoritative 467 metadata update. sub_573A70 consumes the
    /// whole raw32 record but writes only its final packed-minute word to the
    /// selected local profile record. Sending the stored seven-id prefix as
    /// well keeps the record wire-compatible and avoids a fabricated zero raw32.
    /// </summary>
    public static Packet CreateChangeAcknowledgement(
        byte resultRaw,
        byte unknownHeaderRaw,
        byte profileIndex,
        NewSkillProfileRecord profile)
    {
        if (profileIndex >= ProfileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(profileIndex));
        }

        ValidateProfile(profile);
        return new Packet(Opcode.GI_CHANGE_SKILLITEMSLOT_ACK)
            .WriteU8(resultRaw)
            .WriteU8(unknownHeaderRaw)
            .WriteU8(1)
            .WriteU8(profileIndex)
            .WriteProfile(profile);
    }

    private static Packet WriteProfile(this Packet packet, NewSkillProfileRecord profile)
    {
        foreach (int itemId in profile.PuzzleItemIds)
        {
            packet.WriteS32(itemId);
        }

        return packet.WriteS32(profile.ExpiresAtPackedMinute);
    }

    private static void ValidateSnapshot(byte selectedProfile, IReadOnlyList<NewSkillProfileRecord> profiles)
    {
        if (selectedProfile >= ProfileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedProfile));
        }

        if (profiles is null || profiles.Count != ProfileCount)
        {
            throw new ArgumentException($"A NewSkill snapshot requires exactly {ProfileCount} records.", nameof(profiles));
        }

        foreach (NewSkillProfileRecord profile in profiles)
        {
            ValidateProfile(profile);
        }
    }

    private static void ValidateProfile(NewSkillProfileRecord profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.PuzzleItemIds is null || profile.PuzzleItemIds.Length != PuzzleSlotCount)
        {
            throw new ArgumentException($"A NewSkill profile requires exactly {PuzzleSlotCount} puzzle item ids.", nameof(profile));
        }
    }
}
