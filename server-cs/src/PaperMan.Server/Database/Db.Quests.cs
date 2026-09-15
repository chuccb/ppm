// =============================================================================
// Quest acceptance/cancellation persistence for 867 through 870.
//
// The database enforces catalog/existing-row constraints; it does not invent
// unrecovered quest reward, completion, or progression policy.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- quests
    /// <summary>接任務 (867)。quest 必須在目錄且未接過; state=1 WORKING。</summary>
    public bool AcceptQuest(long userId, int questId)
    {
        lock (_gate)
        {
            try
            {
                using var cmd = Cmd("""
                    INSERT INTO user_quests(user_id, quest_id, state)
                    SELECT @u, quest_id, 1 FROM quest_catalog WHERE quest_id=@q
                    """,
                    ("@u", userId), ("@q", questId));
                return cmd.ExecuteNonQuery() == 1;
            }
            catch (SqliteException)
            {
                return false;                               // 已接過 (PK 落敗)
            }
        }
    }

    /// <summary>取消任務 (869)。只允許取消進行中的 (state 0/1)。</summary>
    public bool CancelQuest(long userId, int questId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                UPDATE user_quests SET state=4, updated_at=unixepoch()
                WHERE user_id=@u AND quest_id=@q AND state IN (0, 1)
                """,
                ("@u", userId), ("@q", questId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

}
