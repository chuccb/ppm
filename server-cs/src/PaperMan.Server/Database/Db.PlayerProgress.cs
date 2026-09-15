// =============================================================================
// Player progression and selected-character mutations.
//
// This groups tutorial index, current character, evidence-bounded character
// purchase, and monotonic GP_CH*C totals. Each remains an explicit SQLite
// operation with the handler-controlled policy visible at its caller.
// =============================================================================
namespace PaperMan.Server;

public sealed partial class Db
{
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
    /// column 僅由 `Handlers.Stats.Shared.cs` 的 direct GP_CH request entries 提供,
    /// 不接受外部字串。
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
