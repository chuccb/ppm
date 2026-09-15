// =============================================================================
// Account-level NewSkill profile persistence for 255 / 466 / 467.
//
// The client owns only the conditional seven-id prefix in 466. Expiry and the
// five-profile snapshot remain server state, so their validation and transaction
// live together rather than being hidden in a generic inventory operation.
// =============================================================================
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------- NewSkill profiles
    // GL_INVENIN_ACK (255) carries five complete records. Each is seven puzzle
    // IDs plus a server-owned packed-minute expiry word; the active record is
    // copied to CClientData+144420 by sub_4AAB80. 466 writes only the seven-id
    // prefix of the profile being left, so its expiry must never come from C2S.
    public sealed record NewSkillProfile(int[] PuzzleItemIds, int ExpiresAtPackedMinute);

    public sealed record NewSkillProfileSnapshot(byte SelectedProfile, NewSkillProfile[] Profiles);

    /// <summary>Gets the five account-level NewSkill records used by 255.</summary>
    public NewSkillProfileSnapshot GetNewSkillProfileSnapshot(long userId)
    {
        lock (_gate)
        {
            using var transaction = _conn.BeginTransaction();
            EnsureNewSkillProfileRowsUnlocked(userId, transaction);
            NewSkillProfileSnapshot snapshot = ReadNewSkillProfileSnapshotUnlocked(userId, transaction);
            transaction.Commit();
            return snapshot;
        }
    }

    /// <summary>
    /// Applies a fully parsed 466 selection. The normal client saves the
    /// previous profile's seven IDs, then selects <paramref name="targetProfile"/>.
    /// The packed expiry remains server state and the returned target record is
    /// suitable for the one real raw32 metadata record in 467.
    /// </summary>
    public NewSkillProfile? ChangeNewSkillProfile(
        long userId,
        byte targetProfile,
        bool hasPreviousProfileUpdate,
        byte previousProfile,
        int[] previousProfilePuzzleItemIds)
    {
        if (targetProfile >= NewSkillProfileWire.ProfileCount
            || (hasPreviousProfileUpdate && previousProfile >= NewSkillProfileWire.ProfileCount)
            || (hasPreviousProfileUpdate && !IsValidNewSkillProfileItems(previousProfilePuzzleItemIds)))
        {
            return null;
        }

        lock (_gate)
        {
            using var transaction = _conn.BeginTransaction();
            EnsureNewSkillProfileRowsUnlocked(userId, transaction);
            byte selectedProfile = ReadSelectedNewSkillProfileUnlocked(userId, transaction);

            // The no-body variant is emitted when no profile needs saving. It
            // cannot legitimately jump away from the server's current record.
            if ((!hasPreviousProfileUpdate && targetProfile != selectedProfile)
                || (hasPreviousProfileUpdate && previousProfile != selectedProfile)
                || !IsNewSkillProfileAvailableUnlocked(userId, targetProfile, transaction)
                || (hasPreviousProfileUpdate
                    && !OwnsUsableNewSkillItemsUnlocked(userId, previousProfilePuzzleItemIds, transaction)))
            {
                return null;
            }

            if (hasPreviousProfileUpdate)
            {
                using var updatePreviousProfile = Cmd("""
                    UPDATE new_skill_profiles
                    SET puzzle0=@puzzle0, puzzle1=@puzzle1, puzzle2=@puzzle2,
                        puzzle3=@puzzle3, puzzle4=@puzzle4, puzzle5=@puzzle5,
                        puzzle6=@puzzle6
                    WHERE user_id=@userId AND profile_index=@profileIndex
                    """,
                    ("@puzzle0", previousProfilePuzzleItemIds[0]),
                    ("@puzzle1", previousProfilePuzzleItemIds[1]),
                    ("@puzzle2", previousProfilePuzzleItemIds[2]),
                    ("@puzzle3", previousProfilePuzzleItemIds[3]),
                    ("@puzzle4", previousProfilePuzzleItemIds[4]),
                    ("@puzzle5", previousProfilePuzzleItemIds[5]),
                    ("@puzzle6", previousProfilePuzzleItemIds[6]),
                    ("@userId", userId),
                    ("@profileIndex", (int)previousProfile));
                updatePreviousProfile.Transaction = transaction;
                if (updatePreviousProfile.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException("NewSkill profile row disappeared during its transaction.");
                }
            }

            NewSkillProfile selectedRecord;
            using (var selectTargetProfile = Cmd("""
                SELECT puzzle0, puzzle1, puzzle2, puzzle3, puzzle4, puzzle5, puzzle6,
                       expires_at_packed_minute
                FROM new_skill_profiles
                WHERE user_id=@userId AND profile_index=@profileIndex
                """, ("@userId", userId), ("@profileIndex", (int)targetProfile)))
            {
                selectTargetProfile.Transaction = transaction;
                using var reader = selectTargetProfile.ExecuteReader();
                if (!reader.Read())
                {
                    throw new InvalidOperationException("NewSkill target profile row disappeared during its transaction.");
                }

                var puzzleItemIds = new int[NewSkillProfileWire.PuzzleSlotCount];
                for (int slot = 0; slot < puzzleItemIds.Length; slot++)
                {
                    puzzleItemIds[slot] = reader.GetInt32(slot);
                }

                selectedRecord = new NewSkillProfile(
                    puzzleItemIds,
                    reader.GetInt32(NewSkillProfileWire.PuzzleSlotCount));
            }

            using (var selectState = Cmd("""
                UPDATE new_skill_profile_state
                SET selected_profile=@profileIndex
                WHERE user_id=@userId
                """, ("@profileIndex", (int)targetProfile), ("@userId", userId)))
            {
                selectState.Transaction = transaction;
                if (selectState.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException("NewSkill profile state row disappeared during its transaction.");
                }
            }

            transaction.Commit();
            return selectedRecord;
        }
    }

    private void EnsureNewSkillProfileRowsUnlocked(long userId, SqliteTransaction transaction)
    {
        // Preserve an existing installation's old seven-ID legacy block only for
        // profile zero's first creation. Later 466 changes never fall back to
        // this obsolete source.
        using (var insertFirstProfile = Cmd("""
            INSERT OR IGNORE INTO new_skill_profiles(
                user_id, profile_index,
                puzzle0, puzzle1, puzzle2, puzzle3, puzzle4, puzzle5, puzzle6)
            SELECT @userId, 0,
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=0 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=1 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=2 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=3 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=4 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=5 THEN item_id END), 0),
                   COALESCE(MAX(CASE WHEN slot_kind=1 AND idx=6 THEN item_id END), 0)
            FROM skill_slots
            WHERE user_id=@userId
            """, ("@userId", userId)))
        {
            insertFirstProfile.Transaction = transaction;
            insertFirstProfile.ExecuteNonQuery();
        }

        for (int profileIndex = 1; profileIndex < NewSkillProfileWire.ProfileCount; profileIndex++)
        {
            using var insertProfile = Cmd("""
                INSERT OR IGNORE INTO new_skill_profiles(user_id, profile_index)
                VALUES(@userId, @profileIndex)
                """, ("@userId", userId), ("@profileIndex", profileIndex));
            insertProfile.Transaction = transaction;
            insertProfile.ExecuteNonQuery();
        }

        using var insertState = Cmd("""
            INSERT OR IGNORE INTO new_skill_profile_state(user_id, selected_profile)
            VALUES(@userId, 0)
            """, ("@userId", userId));
        insertState.Transaction = transaction;
        insertState.ExecuteNonQuery();
    }

    private NewSkillProfileSnapshot ReadNewSkillProfileSnapshotUnlocked(long userId, SqliteTransaction transaction)
    {
        byte selectedProfile = ReadSelectedNewSkillProfileUnlocked(userId, transaction);
        var profiles = new NewSkillProfile[NewSkillProfileWire.ProfileCount];

        using var command = Cmd("""
            SELECT profile_index,
                   puzzle0, puzzle1, puzzle2, puzzle3, puzzle4, puzzle5, puzzle6,
                   expires_at_packed_minute
            FROM new_skill_profiles
            WHERE user_id=@userId
            ORDER BY profile_index
            """, ("@userId", userId));
        command.Transaction = transaction;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int profileIndex = reader.GetInt32(0);
            if (profileIndex is < 0 or >= NewSkillProfileWire.ProfileCount)
            {
                throw new InvalidOperationException("NewSkill profile table contains an out-of-range profile index.");
            }

            var puzzleItemIds = new int[NewSkillProfileWire.PuzzleSlotCount];
            for (int slot = 0; slot < puzzleItemIds.Length; slot++)
            {
                puzzleItemIds[slot] = reader.GetInt32(slot + 1);
            }

            profiles[profileIndex] = new NewSkillProfile(
                puzzleItemIds,
                reader.GetInt32(NewSkillProfileWire.PuzzleSlotCount + 1));
        }

        if (profiles.Any(profile => profile is null))
        {
            throw new InvalidOperationException("NewSkill profile bootstrap did not create five profile records.");
        }

        return new NewSkillProfileSnapshot(selectedProfile, profiles);
    }

    private byte ReadSelectedNewSkillProfileUnlocked(long userId, SqliteTransaction transaction)
    {
        using var command = Cmd("SELECT selected_profile FROM new_skill_profile_state WHERE user_id=@userId", ("@userId", userId));
        command.Transaction = transaction;
        object? value = command.ExecuteScalar();
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException("NewSkill profile bootstrap did not create profile state.");
        }

        int selectedProfile = Convert.ToInt32(value);
        if (selectedProfile is < 0 or >= NewSkillProfileWire.ProfileCount)
        {
            throw new InvalidOperationException("NewSkill profile state contains an out-of-range selected profile.");
        }

        return (byte)selectedProfile;
    }

    private static bool IsValidNewSkillProfileItems(int[] puzzleItemIds)
    {
        if (puzzleItemIds is null || puzzleItemIds.Length != NewSkillProfileWire.PuzzleSlotCount)
        {
            return false;
        }

        for (int slot = 0; slot < puzzleItemIds.Length; slot++)
        {
            int itemId = puzzleItemIds[slot];
            if (itemId != 0 && !IsNewSkillItemForSlot(itemId, slot))
            {
                return false;
            }
        }

        // The native UI explicitly rejects the same nonzero accessory puzzle
        // in both accessory positions (message table entry 900).
        return puzzleItemIds[5] == 0 || puzzleItemIds[5] != puzzleItemIds[6];
    }

    private static bool IsNewSkillItemForSlot(int itemId, int slot)
    {
        int firstItemId = slot switch
        {
            0 => 11010001,
            1 => 11020001,
            2 => 11030001,
            3 => 11040001,
            4 => 11050001,
            5 or 6 => 11060001,
            _ => 0,
        };
        int lastItemId = slot is 5 or 6 ? 11070000 : firstItemId + 9999;
        return itemId >= firstItemId && itemId <= lastItemId;
    }

    private bool OwnsUsableNewSkillItemsUnlocked(
        long userId,
        IReadOnlyList<int> puzzleItemIds,
        SqliteTransaction transaction)
    {
        foreach (int itemId in puzzleItemIds.Distinct())
        {
            if (itemId == 0)
            {
                continue;
            }

            using var command = Cmd("""
                SELECT EXISTS(
                    SELECT 1 FROM inventory
                    WHERE user_id=@userId AND item_id=@itemId
                      AND (expires_at IS NULL OR expires_at > unixepoch()))
                """, ("@userId", userId), ("@itemId", itemId));
            command.Transaction = transaction;
            if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsNewSkillProfileAvailableUnlocked(long userId, byte targetProfile, SqliteTransaction transaction)
    {
        // sub_4AAB80 forces profile zero available regardless of its raw tail.
        if (targetProfile == 0)
        {
            return true;
        }

        using var command = Cmd("""
            SELECT expires_at_packed_minute
            FROM new_skill_profiles
            WHERE user_id=@userId AND profile_index=@profileIndex
            """, ("@userId", userId), ("@profileIndex", (int)targetProfile));
        command.Transaction = transaction;
        object? value = command.ExecuteScalar();
        if (value is null || value is DBNull)
        {
            return false;
        }

        return HasAtLeastOneNativePackedMinuteRemaining(Convert.ToInt32(value));
    }

    /// <summary>
    /// Mirrors sub_48B9A0 then sub_5309C0: the raw32 tail encodes local
    /// year/month/day/hour/minute fields and a profile is active only while
    /// its integer remaining-minute component is positive.
    /// </summary>
    private static bool HasAtLeastOneNativePackedMinuteRemaining(int packedMinute)
    {
        uint raw = unchecked((uint)packedMinute);
        int year = 2000 + (int)(raw >> 24);
        int month = (int)((raw >> 19) & 0x1F);
        int day = (int)((raw >> 13) & 0x3F);
        int hour = (int)((raw >> 7) & 0x3F);
        int minute = (int)(raw & 0x7F);

        // _mktime64 accepts overflowing tm fields. Starting from January one
        // and adding each component provides the same normalization for every
        // representable 32-bit field combination (year 2000..2255).
        DateTime expiry = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Local)
            .AddMonths(month - 1)
            .AddDays(day - 1)
            .AddHours(hour)
            .AddMinutes(minute);
        return expiry - DateTime.Now >= TimeSpan.FromMinutes(1);
    }

}
