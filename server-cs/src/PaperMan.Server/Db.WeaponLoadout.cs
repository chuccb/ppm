// =============================================================================
// 武器負載組寫入 — GI_CHANGEWP 220 / 221。
//
// Native facts (PaperMan.exe.c):
//   sub_573340 sends only changed groups; sub_5735F0 receives a group list.
//   sub_4C7C00 names group fields PRIMARYSLOT / SECONDARYSLOT / MELEESLOT /
//   THROWSLOT, with group 3 serving SWITCHWEAPONSLOT (primary only).
//   sub_4C9440 prevents a duplicate within each item family before it sends.
//   sub_524A50 writes eight parts only if the primary offset is non-zero.
//
// The client resource alone cannot prove the historical server's ownership
// checks. The active-inventory check below is a server-side policy, isolated
// here as an Inference / MEDIUM, and deliberately fails closed.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    private const int PrimaryWeaponItemBase = 12_100_000;
    private const int SecondaryWeaponItemBase = 12_200_000;
    private const int MeleeWeaponItemBase = 12_300_000;
    private const int ThrowWeaponItemBase = 12_400_000;
    private const int WeaponLoadoutGroupCount = 4;
    private const int SelectableWeaponLoadoutGroupCount = 3;
    private const int WeaponPartSlotCount = 8;

    /// <summary>
    /// Persists the changed 220 groups atomically and returns the complete
    /// four-group snapshot required by the 221 receiver.  A false result
    /// means no row was changed.
    /// </summary>
    public bool TryChangeWeaponGroups(
        long userId,
        IReadOnlyList<WeaponGroup> changedGroups,
        out List<WeaponGroup> allGroups)
    {
        ArgumentNullException.ThrowIfNull(changedGroups);

        allGroups = [];
        if (userId <= 0 || changedGroups.Count is < 1 or > WeaponLoadoutGroupCount)
        {
            return false;
        }

        lock (_gate)
        {
            using var transaction = _conn.BeginTransaction();
            if (!HasExactlyOneOfEachGroupUnlocked(userId, transaction))
            {
                transaction.Rollback();
                return false;
            }

            var changedByNumber = new Dictionary<byte, WeaponGroup>();
            foreach (WeaponGroup group in changedGroups)
            {
                if (group.GroupNo >= WeaponLoadoutGroupCount
                    || group.Parts is null
                    || group.Parts.Length != WeaponPartSlotCount
                    || !changedByNumber.TryAdd(group.GroupNo, group))
                {
                    transaction.Rollback();
                    return false;
                }
            }

            List<WeaponGroup> mergedGroups = GetWeaponGroupsUnlocked(userId, transaction);
            foreach (var (groupNumber, group) in changedByNumber)
            {
                mergedGroups[groupNumber] = new WeaponGroup(
                    groupNumber,
                    group.PrimaryOffset,
                    group.SecondaryOffset,
                    group.MeleeOffset,
                    group.ThrowOffset,
                    [.. group.Parts]);
            }

            if (!AreValidWeaponGroupsUnlocked(userId, mergedGroups, transaction))
            {
                transaction.Rollback();
                return false;
            }

            foreach (byte groupNumber in changedByNumber.Keys)
            {
                // Persist the copy just validated above, not the caller-owned
                // array that was used to construct the delta request.
                WeaponGroup group = mergedGroups[groupNumber];
                using var update = Cmd("""
                    UPDATE weapon_groups
                    SET equipped=@primary, sub1=@secondary, sub2=@melee, sub3=@throw,
                        part0=@part0, part1=@part1, part2=@part2, part3=@part3,
                        part4=@part4, part5=@part5, part6=@part6, part7=@part7
                    WHERE user_id=@userId AND group_no=@groupNo
                    """,
                    ("@primary", (int)group.PrimaryOffset),
                    ("@secondary", (int)group.SecondaryOffset),
                    ("@melee", (int)group.MeleeOffset),
                    ("@throw", (int)group.ThrowOffset),
                    ("@part0", group.Parts[0]),
                    ("@part1", group.Parts[1]),
                    ("@part2", group.Parts[2]),
                    ("@part3", group.Parts[3]),
                    ("@part4", group.Parts[4]),
                    ("@part5", group.Parts[5]),
                    ("@part6", group.Parts[6]),
                    ("@part7", group.Parts[7]),
                    ("@userId", userId),
                    ("@groupNo", (int)group.GroupNo));
                if (update.ExecuteNonQuery() != 1)
                {
                    transaction.Rollback();
                    return false;
                }
            }

            transaction.Commit();
            allGroups = mergedGroups;
            return true;
        }
    }

    private bool HasExactlyOneOfEachGroupUnlocked(long userId, SqliteTransaction transaction)
    {
        // A normal users INSERT trigger creates these rows.  Do not silently
        // manufacture a partial/unknown legacy state during an equipment
        // write: the next verified login repair owns that migration boundary.
        using var command = Cmd("""
            SELECT COUNT(*), COUNT(DISTINCT group_no), MIN(group_no), MAX(group_no)
            FROM weapon_groups WHERE user_id=@userId
            """, ("@userId", userId));
        command.Transaction = transaction;
        using var reader = command.ExecuteReader();
        return reader.Read()
            && reader.GetInt32(0) == WeaponLoadoutGroupCount
            && reader.GetInt32(1) == WeaponLoadoutGroupCount
            && reader.GetInt32(2) == 0
            && reader.GetInt32(3) == WeaponLoadoutGroupCount - 1;
    }

    private List<WeaponGroup> GetWeaponGroupsUnlocked(long userId, SqliteTransaction transaction)
    {
        List<WeaponGroup> groups = [];
        using var command = Cmd("""
            SELECT group_no, equipped, sub1, sub2, sub3,
                   part0, part1, part2, part3, part4, part5, part6, part7
            FROM weapon_groups WHERE user_id=@userId ORDER BY group_no
            """, ("@userId", userId));
        command.Transaction = transaction;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            groups.Add(ReadWeaponGroup(reader));
        }

        return groups;
    }

    private static WeaponGroup ReadWeaponGroup(SqliteDataReader reader)
    {
        int groupNumber = reader.GetInt32(0);
        int primaryOffset = reader.GetInt32(1);
        int secondaryOffset = reader.GetInt32(2);
        int meleeOffset = reader.GetInt32(3);
        int throwOffset = reader.GetInt32(4);
        if (groupNumber is < 0 or >= WeaponLoadoutGroupCount
            || primaryOffset is < ushort.MinValue or > ushort.MaxValue
            || secondaryOffset is < ushort.MinValue or > ushort.MaxValue
            || meleeOffset is < ushort.MinValue or > ushort.MaxValue
            || throwOffset is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new InvalidDataException("weapon_groups contains an invalid native u16 loadout value.");
        }

        int[] parts = new int[WeaponPartSlotCount];
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = reader.GetInt32(5 + i);
        }

        return new WeaponGroup(
            (byte)groupNumber,
            (ushort)primaryOffset,
            (ushort)secondaryOffset,
            (ushort)meleeOffset,
            (ushort)throwOffset,
            parts);
    }

    private bool AreValidWeaponGroupsUnlocked(
        long userId,
        IReadOnlyList<WeaponGroup> groups,
        SqliteTransaction transaction)
    {
        if (groups.Count != WeaponLoadoutGroupCount
            || groups.Any(group => group.GroupNo >= WeaponLoadoutGroupCount
                || group.Parts is null
                || group.Parts.Length != WeaponPartSlotCount))
        {
            return false;
        }

        // group 3 has no secondary/melee/throw words on the native wire.
        WeaponGroup switchWeapon = groups[WeaponLoadoutGroupCount - 1];
        if (switchWeapon.SecondaryOffset != 0
            || switchWeapon.MeleeOffset != 0
            || switchWeapon.ThrowOffset != 0)
        {
            return false;
        }

        if (HasDuplicateNonZeroOffset(groups.Select(group => group.PrimaryOffset))
            || HasDuplicateNonZeroOffset(groups.Take(SelectableWeaponLoadoutGroupCount).Select(group => group.SecondaryOffset))
            || HasDuplicateNonZeroOffset(groups.Take(SelectableWeaponLoadoutGroupCount).Select(group => group.MeleeOffset))
            || HasDuplicateNonZeroOffset(groups.Take(SelectableWeaponLoadoutGroupCount).Select(group => group.ThrowOffset)))
        {
            return false;
        }

        foreach (WeaponGroup group in groups)
        {
            int primaryItemId = ToItemId(PrimaryWeaponItemBase, group.PrimaryOffset);
            if (!HasActiveInventoryItemUnlocked(userId, primaryItemId, transaction)
                || !HasActiveInventoryItemUnlocked(userId, ToItemId(SecondaryWeaponItemBase, group.SecondaryOffset), transaction)
                || !HasActiveInventoryItemUnlocked(userId, ToItemId(MeleeWeaponItemBase, group.MeleeOffset), transaction)
                || !HasActiveInventoryItemUnlocked(userId, ToItemId(ThrowWeaponItemBase, group.ThrowOffset), transaction))
            {
                return false;
            }

            if (primaryItemId == 0)
            {
                if (group.Parts.Any(part => part != 0))
                {
                    return false;
                }

                continue;
            }

            for (int partSlot = 0; partSlot < group.Parts.Length; partSlot++)
            {
                int partItemId = group.Parts[partSlot];
                if (partItemId != 0
                    && (!HasActiveInventoryItemUnlocked(userId, partItemId, transaction)
                        || !IsCompatibleWeaponPartUnlocked(primaryItemId, partSlot, partItemId, transaction)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int ToItemId(int itemIdBase, ushort offset) =>
        offset == 0 ? 0 : itemIdBase + offset;

    private static bool HasDuplicateNonZeroOffset(IEnumerable<ushort> offsets)
    {
        var seen = new HashSet<ushort>();
        foreach (ushort offset in offsets)
        {
            if (offset != 0 && !seen.Add(offset))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActiveInventoryItemUnlocked(long userId, int itemId, SqliteTransaction transaction)
    {
        if (itemId == 0)
        {
            return true;
        }

        using var command = Cmd("""
            SELECT EXISTS(
                SELECT 1 FROM inventory
                WHERE user_id=@userId AND item_id=@itemId
                  AND (expires_at IS NULL OR expires_at > unixepoch())
            )
            """, ("@userId", userId), ("@itemId", itemId));
        command.Transaction = transaction;
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private bool IsCompatibleWeaponPartUnlocked(
        int primaryItemId,
        int partSlot,
        int partItemId,
        SqliteTransaction transaction)
    {
        // weapon_parts_catalog is populated by db/import_pats.py from the
        // decrypted client resource.  A new server creates this empty table;
        // absent resource data means "not proven compatible", never allow-all.
        using var command = Cmd("""
            SELECT EXISTS(
                SELECT 1 FROM weapon_parts_catalog
                WHERE gun_item_id=@primaryItemId AND grp=@partSlot AND part_item_id=@partItemId
            )
            """,
            ("@primaryItemId", primaryItemId),
            ("@partSlot", partSlot),
            ("@partItemId", partItemId));
        command.Transaction = transaction;
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }
}
