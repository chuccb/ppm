// =============================================================================
// Source-proven canonical character body/appearance template values.
//
// These raw category-relative offsets come from the named native body-template
// switch tables. They are shared by bootstrap, character creation, purchase,
// and the 198/247 projection without introducing item/grant policy.
// =============================================================================
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

}
