// =============================================================================
// 玩家資料存取 — MyInfo (198/247 資料源) 與戰績 (GP_CH*C SetStatMax)。
// (partial — 主體/共用基礎見 Db.cs; 卅四輪依領域拆分, 佈局證據見
//  docs/PACKETS.md 對應章節)
// =============================================================================
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- myinfo
    /// <summary>user_stats 的 19 個計數器 (GP_CH*C 家族順序)。</summary>
    public sealed record Stats(
        long Wins, long Losses, long Kills, long Deaths, long Headshots,
        long Combos, long Hearts, long DoubleKill, long TripleKill, long Criticals,
        long MultiKill, long UltraKill, long ZKill, long KKill, long DdKill,
        long PlayCount, long RoundCount, long Disconnects, long PlayTimeS);

    public sealed record MyInfo(
        long UserId, string Nickname, int Level, long Exp, long Gp, int Cash,
        byte CurrentChar, Stats Stats);

    /// <summary>GL_MYINFO_ACK(198) 資料來源 (v_myinfo)。</summary>
    public MyInfo? GetMyInfo(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT user_id, nickname, level, exp, game_point, cash, current_char,
                       wins, losses, kills, deaths, headshots, combos, hearts,
                       double_kill, triple_kill, criticals, multi_kill, ultra_kill,
                       z_kill, k_kill, dd_kill, play_count, round_count, disconnects, play_time_s
                FROM v_myinfo WHERE user_id=@u
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return null;
            }

            return new(
                r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3),
                r.GetInt64(4), r.GetInt32(5), (byte)r.GetInt32(6),
                new Stats(
                    Wins: r.GetInt64(7), Losses: r.GetInt64(8),
                    Kills: r.GetInt64(9), Deaths: r.GetInt64(10),
                    Headshots: r.GetInt64(11), Combos: r.GetInt64(12),
                    Hearts: r.GetInt64(13), DoubleKill: r.GetInt64(14),
                    TripleKill: r.GetInt64(15), Criticals: r.GetInt64(16),
                    MultiKill: r.GetInt64(17), UltraKill: r.GetInt64(18),
                    ZKill: r.GetInt64(19), KKill: r.GetInt64(20),
                    DdKill: r.GetInt64(21), PlayCount: r.GetInt64(22),
                    RoundCount: r.GetInt64(23), Disconnects: r.GetInt64(24),
                    PlayTimeS: r.GetInt64(25)));
        }
    }

    /// <summary>247 GL_CLIENTINFO 用: 以暱稱查他人資料 (廿五輪)。</summary>
    public MyInfo? GetMyInfoByNick(string nickname)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n", ("@n", nickname));
            var uid = who.ExecuteScalar();

            return uid is null ? null : GetMyInfoUnlocked((long)uid);
        }
    }

    private MyInfo? GetMyInfoUnlocked(long userId)
    {
        using var cmd = Cmd("""
            SELECT user_id, nickname, level, exp, game_point, cash, current_char,
                   wins, losses, kills, deaths, headshots, combos, hearts,
                   double_kill, triple_kill, criticals, multi_kill, ultra_kill,
                   z_kill, k_kill, dd_kill, play_count, round_count, disconnects, play_time_s
            FROM v_myinfo WHERE user_id=@u
            """, ("@u", userId));
        using var r = cmd.ExecuteReader();

        if (!r.Read())
        {
            return null;
        }

        return new(
            r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3),
            r.GetInt64(4), r.GetInt32(5), (byte)r.GetInt32(6),
            new Stats(
                Wins: r.GetInt64(7), Losses: r.GetInt64(8),
                Kills: r.GetInt64(9), Deaths: r.GetInt64(10),
                Headshots: r.GetInt64(11), Combos: r.GetInt64(12),
                Hearts: r.GetInt64(13), DoubleKill: r.GetInt64(14),
                TripleKill: r.GetInt64(15), Criticals: r.GetInt64(16),
                MultiKill: r.GetInt64(17), UltraKill: r.GetInt64(18),
                ZKill: r.GetInt64(19), KKill: r.GetInt64(20),
                DdKill: r.GetInt64(21), PlayCount: r.GetInt64(22),
                RoundCount: r.GetInt64(23), Disconnects: r.GetInt64(24),
                PlayTimeS: r.GetInt64(25)));
    }

    public sealed record CharSlot(byte SlotNo, byte CharType, ushort[] Equip);

    public List<CharSlot> GetCharacters(long userId)
    {
        lock (_gate)
        {
            List<CharSlot> list = [];
            using var cmd = Cmd("""
                SELECT slot_no, char_type, eq_primary, eq_secondary, eq_melee, eq_grenade,
                       eq_head, eq_face, eq_upper, eq_lower, eq_hands, eq_back, eq_special, eq_set
                FROM characters WHERE user_id=@u ORDER BY slot_no
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var eq = new ushort[12];
                for (int i = 0; i < eq.Length; i++)
                    eq[i] = (ushort)r.GetInt32(2 + i);
                list.Add(new((byte)r.GetInt32(0), (byte)r.GetInt32(1), eq));
            }

            return list;
        }
    }

    public bool CreateChar(long userId, byte slotNo, byte charType)
    {
        if (slotNo >= 20 || !IsCanonicalCharacterType(charType))
        {
            return false;
        }

        CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(charType);
        lock (_gate)
        {
            try
            {
                using var cmd = Cmd("""
                    INSERT INTO characters(
                        user_id, slot_no, char_type,
                        eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                    VALUES(
                        @userId, @slotNo, @charType,
                        @body, @head, @face, @top, @bottom, @shoes)
                    """,
                    ("@userId", userId),
                    ("@slotNo", slotNo),
                    ("@charType", charType),
                    ("@body", (int)starter.BodyOffset),
                    ("@head", (int)starter.HeadOffset),
                    ("@face", (int)starter.FaceOffset),
                    ("@top", (int)starter.TopOffset),
                    ("@bottom", (int)starter.BottomOffset),
                    ("@shoes", (int)starter.ShoesOffset));
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (SqliteException)
            {
                return false;                               // UNIQUE(user_id,slot_no) 落敗
            }
        }
    }

    // ------------------------------------------------------------- loadout
    /// <summary>sub_524660 武器編組 — equipped(與 sub1..3) 為 u16 類別內索引, 0=空。</summary>
    public sealed record WeaponGroup(
        byte GroupNo, ushort Equipped, ushort Sub1, ushort Sub2, ushort Sub3, int[] Parts);

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
                var parts = new int[8];
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = r.GetInt32(5 + i);
                }

                list.Add(new(
                    (byte)r.GetInt32(0), (ushort)r.GetInt32(1),
                    (ushort)r.GetInt32(2), (ushort)r.GetInt32(3), (ushort)r.GetInt32(4),
                    parts));
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

    // ------------------------------------------------------------- stats/misc
    /// <summary>教學步驟索引 (GL_TUTORIALINDEX 685/686 與 689/690)。</summary>
    public int GetTutorialIndex(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT flags1 FROM users WHERE user_id=@u", ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    public bool SetTutorialIndex(long userId, int tutorialIndex)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE users SET flags1=@t, updated_at=unixepoch() WHERE user_id=@u",
                ("@t", tutorialIndex), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>切換現役角色槽 (GI_CHANGEDATA 218/219, GI_CHANGESLOT 312/313)。</summary>
    public bool SetCurrentChar(long userId, byte slotNo)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE users SET current_char=@s, updated_at=unixepoch() WHERE user_id=@u AND @s BETWEEN 0 AND 19",
                ("@s", (int)slotNo), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>
    /// 購買新角色槽 (GS_BUYCHAR 310/311)。Writes the same six native normal
    /// appearance words as creation; 311 expands them to full item IDs.
    /// </summary>
    public bool BuyCharacter(long userId, byte slotNo, byte charType, int priceGp = 0)
    {
        if (slotNo >= 20 || !IsCanonicalCharacterType(charType) || priceGp < 0)
        {
            return false;
        }

        CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(charType);
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                if (priceGp > 0)
                {
                    using var pay = Cmd(
                        "UPDATE users SET game_point=game_point-@p WHERE user_id=@u AND game_point>=@p",
                        ("@p", priceGp), ("@u", userId));
                    pay.Transaction = tx;
                    if (pay.ExecuteNonQuery() != 1)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                using var ins = Cmd("""
                    INSERT INTO characters(
                        user_id, slot_no, char_type,
                        eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                    VALUES(
                        @userId, @slotNo, @charType,
                        @body, @head, @face, @top, @bottom, @shoes)
                    """,
                    ("@userId", userId),
                    ("@slotNo", (int)slotNo),
                    ("@charType", (int)charType),
                    ("@body", (int)starter.BodyOffset),
                    ("@head", (int)starter.HeadOffset),
                    ("@face", (int)starter.FaceOffset),
                    ("@top", (int)starter.TopOffset),
                    ("@bottom", (int)starter.BottomOffset),
                    ("@shoes", (int)starter.ShoesOffset));
                ins.Transaction = tx;
                if (ins.ExecuteNonQuery() != 1)
                {
                    tx.Rollback();
                    return false;
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                return false;
            }
        }
    }

    /// <summary>
    /// GP_CH*C: client REQ 帶「新的絕對累計值」(sub_5567F0 等) — 只允許
    /// 單調遞增 (MAX), 防倒退/重播; 回傳確認後的 total。
    /// column 由 StatHandlers 白名單提供, 不接受外部字串。
    /// </summary>
    public long SetStatMax(long userId, string column, long newTotal)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                $"UPDATE user_stats SET {column}=MAX({column},@v) WHERE user_id=@u RETURNING {column}",
                ("@v", newTotal), ("@u", userId));
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
    }

}
