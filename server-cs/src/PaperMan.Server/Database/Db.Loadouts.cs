// =============================================================================
// Weapon-group and selected NewSkill-slot projection.
//
// The selected seven-id prefix is read from the authoritative NewSkill profile
// record; it is not an independently persisted client-controlled slot list.
// =============================================================================
namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- loadout
    /// <summary>
    /// sub_524660 weapon profile.  Groups 0..2 hold primary/secondary/melee/
    /// throw offsets; group 3 holds the PRIMARYSLOT switch-weapon offset only.
    /// All u16 values are category-relative offsets, where zero is empty.
    /// </summary>
    public sealed record WeaponGroup(
        byte GroupNo,
        ushort PrimaryOffset,
        ushort SecondaryOffset,
        ushort MeleeOffset,
        ushort ThrowOffset,
        int[] Parts);

    /// <summary>sub_527550 的 9 UI-item 與 sub_527D00 的已選 NewSkill profile 七 puzzle IDs。</summary>
    public sealed record Slots(int[] Skill, int[] NewSkillPuzzleIds);

    public List<WeaponGroup> GetWeaponGroups(long userId)
    {
        lock (_gate)
        {
            List<WeaponGroup> list = [];
            using var cmd = Cmd("""
                SELECT group_no, equipped, sub1, sub2, sub3,
                       part0, part1, part2, part3, part4, part5, part6, part7
                FROM weapon_groups WHERE user_id=@u ORDER BY group_no
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(ReadWeaponGroup(r));
            }

            return list;
        }
    }

    public Slots GetSlots(long userId)
    {
        lock (_gate)
        {
            var skill = new int[9];                         // sub_522480 讀 9×s32
            using (var cmd = Cmd("""
                SELECT idx, item_id
                FROM skill_slots WHERE user_id=@u AND slot_kind=0
                """, ("@u", userId)))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int index = reader.GetInt32(0);
                    if (index is >= 0 and < 9)
                    {
                        skill[index] = reader.GetInt32(1);
                    }
                }
            }

            // sub_527D00's seven ids are the selected record from the five
            // authoritative GL_INVENIN profile records. slot_kind=1 was used
            // by an older server revision; it is read once only when the
            // profile records are first materialized (below).
            using var transaction = _conn.BeginTransaction();
            EnsureNewSkillProfileRowsUnlocked(userId, transaction);
            NewSkillProfileSnapshot snapshot = ReadNewSkillProfileSnapshotUnlocked(userId, transaction);
            transaction.Commit();
            return new Slots(skill, snapshot.Profiles[snapshot.SelectedProfile].PuzzleItemIds);
        }
    }

}
