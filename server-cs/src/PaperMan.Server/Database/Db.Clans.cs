// =============================================================================
// Clan-creation persistence for GC_CLAN_CREATE 585 / 586.
//
// Other clan behavior remains in the top-level GC_CLAN_PROTOCOL 583/584 handler
// until an evidence-backed durable operation is available.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- clans
    /// <summary>
    /// 建戰隊 — 走獨立對 GC_CLAN_CREATE_REQ(585)/_ACK(586), 不經
    /// GC_CLAN_PROTOCOL 583/584 container (八輪更正)。回 clan_id; 名稱重複或已入隊 → 0。
    /// </summary>
    public long CreateClan(long leaderUserId, string name, byte emblem = 0)
    {
        if (name is not { Length: > 0 and <= 16 })
        {
            return 0;
        }

        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                using var ins = Cmd(
                    "INSERT INTO clans(name, leader_id, emblem_id) VALUES(@n, @u, @e) RETURNING clan_id",
                    ("@n", name), ("@u", leaderUserId), ("@e", (int)emblem));
                ins.Transaction = tx;
                long clanId = ReadRequiredReturnedInt64(ins, "Creating a clan");

                using var mem = Cmd(
                    "INSERT INTO clan_members(clan_id, user_id, rank) VALUES(@c, @u, 2)",
                    ("@c", clanId), ("@u", leaderUserId));
                mem.Transaction = tx;
                mem.ExecuteNonQuery();

                using var upd = Cmd(
                    "UPDATE users SET clan_id=@c WHERE user_id=@u",
                    ("@c", clanId), ("@u", leaderUserId));
                upd.Transaction = tx;
                upd.ExecuteNonQuery();

                tx.Commit();
                return clanId;
            }
            catch (SqliteException)   // UNIQUE(name) / UNIQUE(user_id) 落敗
            {
                tx.Rollback();
                return 0;
            }
        }
    }}
