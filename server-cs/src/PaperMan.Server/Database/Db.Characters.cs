// =============================================================================
// Character-row reads and canonical-character creation.
//
// Character type/body validation delegates to the source-proven template values
// in Db.PlayerBootstrap.cs. This file owns only character-row query/insert work.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
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

}
