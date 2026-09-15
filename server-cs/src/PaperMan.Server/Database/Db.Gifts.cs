// =============================================================================
// Gift-box persistence for the 296/297 and 299/301/453/454 families.
//
// These operations own only durable gift rows and their client-observed count;
// they do not infer an original-service catalog, entitlement, or grant policy.
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- gifts
    /// <summary>
    /// 送禮 (296)。回 297 的 result 碼: 0=成功, 2=收件人不存在,
    /// 3=物品不在目錄, 1=其他失敗。
    /// </summary>
    public byte GiveGift(long fromUserId, string toNick, int itemId, byte periodDays, string? message)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n",
                ("@n", toNick));
            var toId = who.ExecuteScalar();

            if (toId is null)
            {
                return 2;                                   // 收件人不存在
            }

            try
            {
                using var ins = Cmd("""
                    INSERT INTO gifts(from_user_id, to_user_id, item_id, period_days, message)
                    VALUES(@f, @t, @i, @p, @m)
                    """,
                    ("@f", fromUserId),
                    ("@t", (long)toId),
                    ("@i", itemId),
                    ("@p", (int)periodDays),
                    ("@m", message));
                ins.ExecuteNonQuery();
                return 0;
            }
            catch (SqliteException)
            {
                return 3;                                   // FK 落敗: 物品不在目錄
            }
        }
    }

    /// <summary>
    /// 198 MyInfo 尾段的 u16 = 禮物盒 pending 數 (client i_23 / F0C100;
    /// 299 寫入 CClientData+36117, 301 收下/刪除時遞減 — sub_57AFE0)。
    /// </summary>
    public ushort GetGiftCount(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT COUNT(*) FROM gifts WHERE to_user_id=@u AND state=0",
                ("@u", userId));
            return Convert.ToUInt16(Convert.ToInt64(cmd.ExecuteScalar() ?? 0L));
        }
    }

    /// <summary>刪除禮物 (GS_DELETEGIFT 453/454)。</summary>
    public bool DeleteGift(long userId, long giftId, int itemId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                DELETE FROM gifts
                WHERE gift_id=@g AND to_user_id=@u AND item_id=@i
                """, ("@g", giftId), ("@u", userId), ("@i", itemId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

}
