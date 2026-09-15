// =============================================================================
// Player identity and canonical starter-character bootstrap.
//
// This private-server state is kept next to the native character-template
// evidence. It ensures a successful login can reach the client 197/198 flow
// without inventing a response for unknown entitlement policy.
// =============================================================================
using Microsoft.Data.Sqlite;
using PaperMan.Protocol;

namespace PaperMan.Server;

public sealed partial class Db
{
    // Native maps 0x402FF0/0x4030A0/0x403150/0x403200/0x4032B0 complete a
    // 19,900,001..19,900,015 body into its six-piece normal appearance.
    // The first six persisted words retain native ordinal order despite their
    // pre-existing SQL names: body, head, face, top, bottom, shoes.
    private const byte FirstCanonicalCharacterType = 1;
    private const byte LastCanonicalCharacterType = 15;
    private const int CharacterBodyItemBase = 19_900_000;

    internal readonly record struct CanonicalStarterAppearance(
        ushort BodyOffset, ushort HeadOffset, ushort FaceOffset, ushort TopOffset,
        ushort BottomOffset, ushort ShoesOffset)
    {
        public int BodyItemId => CharacterBodyItemBase + BodyOffset;
        public int HeadItemId => 10_000_000 + HeadOffset;
        public int FaceItemId => 10_100_000 + FaceOffset;
        public int TopItemId => 10_200_000 + TopOffset;
        public int BottomItemId => 10_300_000 + BottomOffset;
        public int ShoesItemId => 10_400_000 + ShoesOffset;
    }

    // Extracted directly from the five native body-template switch tables.
    // Keep this compact raw-offset form because 198/247 persistence uses u16
    // category-relative values, while 311 expands the same values to full IDs.
    private static readonly CanonicalStarterAppearance[] CanonicalStarterAppearances =
    [
        new(1, 1, 1, 1, 1, 1),
        new(2, 15, 10, 22, 12, 12),
        new(3, 28, 19, 45, 25, 24),
        new(4, 41, 28, 66, 36, 41),
        new(5, 55, 37, 90, 47, 52),
        new(6, 123, 111, 157, 99, 105),
        new(7, 124, 112, 167, 109, 115),
        new(8, 125, 113, 177, 119, 125),
        new(9, 126, 114, 187, 129, 135),
        new(10, 127, 115, 197, 139, 145),
        new(11, 1096, 839, 1069, 974, 952),
        new(12, 1428, 865, 1205, 1069, 1009),
        new(13, 1600, 866, 1213, 1072, 1012),
        new(14, 792, 385, 428, 376, 360),
        new(15, 30220, 920, 10011, 10011, 10114),
    ];

    internal static bool IsCanonicalCharacterType(int charType) =>
        charType is >= FirstCanonicalCharacterType and <= LastCanonicalCharacterType;

    internal static bool TryGetCanonicalCharacterType(int bodyItemId, out byte charType)
    {
        if (bodyItemId is >= CharacterBodyItemBase + FirstCanonicalCharacterType
            and <= CharacterBodyItemBase + LastCanonicalCharacterType)
        {
            charType = (byte)(bodyItemId - CharacterBodyItemBase);
            return true;
        }

        charType = 0;
        return false;
    }

    internal static CanonicalStarterAppearance GetCanonicalStarterAppearance(byte charType)
    {
        if (!IsCanonicalCharacterType(charType))
        {
            throw new ArgumentOutOfRangeException(nameof(charType));
        }

        return CanonicalStarterAppearances[charType - FirstCanonicalCharacterType];
    }

    /// <summary>
    /// The client trace proves it does not ask to create a nickname before its
    /// first 197. Create an explicit private-server starter identity instead of
    /// returning a success 681 whose 198 cannot be consumed.
    /// </summary>
    private PlayerIdentity? EnsurePlayerIdentity(AccountForLogin account, string accountName)
    {
        if (account.UserId > 0 && account.Nickname.Length > 0)
        {
            return EnsurePlayableCharacterStateUnlocked(account.UserId)
                ? new PlayerIdentity(account.UserId, account.Nickname)
                : null;
        }

        string generatedNickname = CreateGeneratedNickname(account.AccountId);
        string initialNickname = IsNativeNicknameLength(accountName)
            ? accountName
            : generatedNickname;

        long userId = CreateNickUnlocked(account.AccountId, initialNickname);
        if (userId > 0)
        {
            return new PlayerIdentity(userId, initialNickname);
        }

        // A manually created player can already use the login-name nickname.
        // The account-id form is deterministic, compact, and unique across
        // ordinary first-login accounts.
        if (initialNickname == generatedNickname)
        {
            return null;
        }

        userId = CreateNickUnlocked(account.AccountId, generatedNickname);
        return userId > 0 ? new PlayerIdentity(userId, generatedNickname) : null;
    }

    /// <summary>
    /// Repairs only missing words in a valid canonical body template. The six
    /// columns are the native normal-order prefix (body, head, face, top,
    /// bottom, shoes), not their old SQL labels. A noncanonical nonzero body is
    /// historical state with no evidence-backed replacement, so it is untouched.
    /// Every nonzero equipment word is preserved.
    /// </summary>
    private bool EnsurePlayableCharacterStateUnlocked(long userId)
    {
        using var transaction = _conn.BeginTransaction();

        var incompleteCanonicalSlots = new List<(int SlotNo, byte CharacterType)>();
        using (var findIncompleteSlots = Cmd("""
            SELECT slot_no, char_type
            FROM characters
            WHERE user_id = @userId
              AND char_type BETWEEN @firstType AND @lastType
              AND (eq_primary = 0 OR eq_primary = char_type)
              AND (eq_primary = 0 OR eq_secondary = 0 OR eq_melee = 0
                   OR eq_grenade = 0 OR eq_head = 0 OR eq_face = 0)
            """,
            ("@userId", userId),
            ("@firstType", (int)FirstCanonicalCharacterType),
            ("@lastType", (int)LastCanonicalCharacterType)))
        {
            findIncompleteSlots.Transaction = transaction;
            using var reader = findIncompleteSlots.ExecuteReader();
            while (reader.Read())
            {
                incompleteCanonicalSlots.Add((reader.GetInt32(0), (byte)reader.GetInt32(1)));
            }
        }

        foreach (var (slotNo, charType) in incompleteCanonicalSlots)
        {
            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(charType);
            using var repairSlot = Cmd("""
                UPDATE characters
                SET eq_primary = CASE WHEN eq_primary = 0 THEN @body ELSE eq_primary END,
                    eq_secondary = CASE WHEN eq_secondary = 0 THEN @head ELSE eq_secondary END,
                    eq_melee = CASE WHEN eq_melee = 0 THEN @face ELSE eq_melee END,
                    eq_grenade = CASE WHEN eq_grenade = 0 THEN @top ELSE eq_grenade END,
                    eq_head = CASE WHEN eq_head = 0 THEN @bottom ELSE eq_head END,
                    eq_face = CASE WHEN eq_face = 0 THEN @shoes ELSE eq_face END
                WHERE user_id = @userId AND slot_no = @slotNo
                """,
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset),
                ("@userId", userId),
                ("@slotNo", slotNo));
            repairSlot.Transaction = transaction;
            if (repairSlot.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }
        }

        int currentCharacterIndex;
        using (var currentCharacter = Cmd(
            "SELECT current_char FROM users WHERE user_id=@userId", ("@userId", userId)))
        {
            currentCharacter.Transaction = transaction;
            object? value = currentCharacter.ExecuteScalar();
            if (value is null)
            {
                transaction.Rollback();
                return false;
            }

            currentCharacterIndex = Convert.ToInt32(value);
        }

        var occupiedSlots = new bool[20];
        var playablePositions = new List<bool>();
        using (var characters = Cmd("""
            SELECT slot_no, char_type, eq_primary
            FROM characters
            WHERE user_id=@userId
            ORDER BY slot_no
            """, ("@userId", userId)))
        {
            characters.Transaction = transaction;
            using var reader = characters.ExecuteReader();
            while (reader.Read())
            {
                int slotNo = reader.GetInt32(0);
                int charType = reader.GetInt32(1);
                int bodyOffset = reader.GetInt32(2);
                occupiedSlots[slotNo] = true;
                playablePositions.Add(
                    IsCanonicalCharacterType(charType) && IsCanonicalCharacterType(bodyOffset));
            }
        }

        int firstPlayablePosition = playablePositions.FindIndex(playable => playable);
        if (firstPlayablePosition < 0)
        {
            int emptySlot = Array.FindIndex(occupiedSlots, occupied => !occupied);
            if (emptySlot < 0)
            {
                transaction.Rollback();
                return false;
            }

            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(FirstCanonicalCharacterType);
            using var addStarter = Cmd("""
                INSERT INTO characters(
                    user_id, slot_no, char_type,
                    eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                VALUES(
                    @userId, @slotNo, @charType,
                    @body, @head, @face, @top, @bottom, @shoes)
                """,
                ("@userId", userId),
                ("@slotNo", emptySlot),
                ("@charType", (int)FirstCanonicalCharacterType),
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset));
            addStarter.Transaction = transaction;
            if (addStarter.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }

            // 198 omits physical slot numbers. It selects the sorted character
            // record by position, so account for any preserved legacy rows.
            firstPlayablePosition = occupiedSlots.Take(emptySlot).Count(occupied => occupied);
            playablePositions.Add(true);
        }

        bool currentCharacterIsPlayable = currentCharacterIndex >= 0
            && currentCharacterIndex < playablePositions.Count
            && playablePositions[currentCharacterIndex];
        if (!currentCharacterIsPlayable)
        {
            using var selectPlayableCharacter = Cmd("""
                UPDATE users
                SET current_char=@characterIndex, updated_at=unixepoch()
                WHERE user_id=@userId
                """,
                ("@characterIndex", firstPlayablePosition),
                ("@userId", userId));
            selectPlayableCharacter.Transaction = transaction;
            if (selectPlayableCharacter.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return false;
            }
        }

        transaction.Commit();
        return true;
    }

    private static bool IsNativeNicknameLength(string nickname) =>
        Packet.Ansi.GetByteCount(nickname) is >= 2 and <= 16;

    /// <summary>
    /// Encodes every positive Int64 account id in base-36, keeping the `P`
    /// prefix plus worst-case value within the client's 2..16-byte name input
    /// limit.
    /// </summary>
    private static string CreateGeneratedNickname(long accountId)
    {
        const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<char> characters = stackalloc char[13];
        ulong value = (ulong)accountId;
        int firstCharacter = characters.Length;
        do
        {
            characters[--firstCharacter] = Digits[(int)(value % 36)];
            value /= 36;
        }
        while (value > 0);

        return "P" + new string(characters[firstCharacter..]);
    }

    private sealed record PlayerIdentity(long UserId, string Nickname);

    // ------------------------------------------------------------- nickname
    /// <summary>暱稱是否已被使用 (GM_CHECKNICK 210 用; result 碼由 handler 對映)。</summary>
    public bool IsNickTaken(string nick)
    {
        lock (_gate)
        {
            using var cmd = Cmd("SELECT 1 FROM users WHERE nickname=@n", ("@n", nick));
            return cmd.ExecuteScalar() is not null;
        }
    }

    /// <summary>
    /// Creates the first player identity for GM_CREATENICK(212). The user row,
    /// trigger-created stats/groups, and starter character commit together so a
    /// failed starter setup never leaves a half-created account identity.
    /// </summary>
    public long CreateNick(long accountId, string nickname)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);

        lock (_gate)
        {
            return CreateNickUnlocked(accountId, nickname);
        }
    }

    /// <summary>
    /// Creates a user, its trigger-provided stats/groups, and starter character
    /// in one transaction. Callers that already hold the database gate use this
    /// rather than attempting a nested lock.
    /// </summary>
    private long CreateNickUnlocked(long accountId, string nickname)
    {
        using var transaction = _conn.BeginTransaction();
        try
        {
            using var userCommand = Cmd(
                "INSERT INTO users(account_id,nickname) VALUES(@accountId,@nickname) RETURNING user_id",
                ("@accountId", accountId), ("@nickname", nickname));
            userCommand.Transaction = transaction;
            long userId = ReadRequiredReturnedInt64(userCommand, "Creating a user");

            // trg_users_bootstrap provides user_stats and all four weapon
            // groups. The explicit character remains server policy, so it
            // belongs in this transaction rather than a post-commit repair.
            CanonicalStarterAppearance starter = GetCanonicalStarterAppearance(FirstCanonicalCharacterType);
            using var characterCommand = Cmd("""
                INSERT INTO characters(
                    user_id, slot_no, char_type,
                    eq_primary, eq_secondary, eq_melee, eq_grenade, eq_head, eq_face)
                VALUES(
                    @userId, 0, @charType,
                    @body, @head, @face, @top, @bottom, @shoes)
                """,
                ("@userId", userId),
                ("@charType", (int)FirstCanonicalCharacterType),
                ("@body", (int)starter.BodyOffset),
                ("@head", (int)starter.HeadOffset),
                ("@face", (int)starter.FaceOffset),
                ("@top", (int)starter.TopOffset),
                ("@bottom", (int)starter.BottomOffset),
                ("@shoes", (int)starter.ShoesOffset));
            characterCommand.Transaction = transaction;
            if (characterCommand.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return 0;
            }

            transaction.Commit();
            return userId;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            // This is the normal domain failure path: duplicate nickname,
            // already-created identity, or an invalid account reference.
            // Other SQLite failures must reach the session error log.
            return 0;
        }
    }

}
