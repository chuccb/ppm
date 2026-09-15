// =============================================================================
// 經濟系統存取 — 背包 (200/205)、賣出 (208/209)、送禮 (296/297)、信箱 (419-426)。
// (partial — 主體/共用基礎見 Db.cs; 卅四輪依領域拆分, 佈局證據見
//  docs/PACKETS.md 對應章節)
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    // ------------------------------------------------------------- inventory
    public sealed record InvItem(
        int Slot, int ItemId, float F1, float F2, int PeriodDaysLeft,
        ushort DuraCur, ushort DuraMax);

    /// <summary>GL_MYITEM_ACK(200) 分頁 (sub_524B70: 100/頁, 上限 5120)。</summary>
    public List<InvItem> GetInventoryPage(long userId, int start, int count = 100)
    {
        lock (_gate)
        {
            List<InvItem> list = [];
            using var cmd = Cmd("""
                SELECT slot, item_id, stat_f1, stat_f2, period_days_left,
                       durability_cur, durability_max
                FROM v_inventory_wire WHERE user_id=@u AND slot>=@s
                ORDER BY slot LIMIT @c
                """, ("@u", userId), ("@s", start), ("@c", count));
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new(r.GetInt32(0), r.GetInt32(1), r.GetFloat(2), r.GetFloat(3),
                             r.GetInt32(4), (ushort)r.GetInt32(5), (ushort)r.GetInt32(6)));
            return list;
        }
    }

    public sealed record BuyResult(
        bool Ok, int ItemId, float F1, float F2, int Period, byte Kind, ushort Dura)
    {
        public static BuyResult Fail(int itemId) => new(false, itemId, 0, 0, 0, 0, 0);
    }

    /// <summary>GS_BUYITEM(204)/GS_BUY_ONCEITEM(695): 原子 扣款+入包+記帳。</summary>
    public BuyResult BuyItem(long userId, int itemId, byte periodDays, bool useCash)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                var result = BuyItemInTx(tx, userId, itemId, periodDays, useCash);

                if (result.Ok)
                {
                    tx.Commit();
                }
                else
                {
                    tx.Rollback();
                }

                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    private BuyResult BuyItemInTx(SqliteTransaction transaction, long userId, int itemId, byte periodDays, bool useCash)
    {
        SqliteCommand TxCmd(string sql, params ReadOnlySpan<(string, object?)> args)
        {
            var c = Cmd(sql, args);
            c.Transaction = transaction;
            return c;
        }

        // 1. 目錄查價
        byte kind;
        int price;
        ushort dura;
        using (var q = TxCmd(
            "SELECT kind, price_gp, price_cash, durability FROM item_catalog WHERE item_id=@i",
            ("@i", itemId)))
        using (var r = q.ExecuteReader())
        {
            if (!r.Read())
            {
                return BuyResult.Fail(itemId);              // 不在商品目錄
            }

            kind = (byte)r.GetInt32(0);
            price = useCash ? r.GetInt32(2) : r.GetInt32(1);
            dura = (ushort)r.GetInt32(3);
        }

        // 2. period 白名單 (sub_570B00)
        bool periodOk = kind switch
        {
            0 or 1 or 3 or 5 or 14 => periodDays is 1 or 7 or 15 or 30 or 60 or 90,
            2 or 4 or 9 or 10 or 11 or 15 or 16 => periodDays == 0,
            _ => false,
        };
        if (!periodOk)
        {
            return BuyResult.Fail(itemId);                  // period 不在白名單 (sub_570B00)
        }

        // 3. 扣款 (條件式 UPDATE = 原子餘額檢查)
        string wallet = useCash
            ? "UPDATE accounts SET cash=cash-@p WHERE account_id=(SELECT account_id FROM users WHERE user_id=@u) AND cash>=@p"
            : "UPDATE users SET game_point=game_point-@p WHERE user_id=@u AND game_point>=@p";
        using (var pay = TxCmd(wallet, ("@p", price), ("@u", userId)))
        {
            if (pay.ExecuteNonQuery() != 1)
            {
                return BuyResult.Fail(itemId);              // 餘額不足
            }
        }

        // 4. 最小空 slot (背包上限 5120, sub_524B70)
        int slot;
        using (var slotQ = TxCmd("""
            SELECT IFNULL(MIN(t.slot+1),0) FROM
              (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=@u) t
            WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=@u)
            """, ("@u", userId)))
        {
            slot = Convert.ToInt32(slotQ.ExecuteScalar());
        }

        if (slot >= 5120)
        {
            return BuyResult.Fail(itemId);                  // 背包已滿 (上限 sub_524B70)
        }

        // 5. 入包 + 記帳
        using (var ins = TxCmd("""
            INSERT INTO inventory(user_id,slot,item_id,period_days,expires_at,durability_cur,durability_max)
            VALUES(@u,@s,@i,@p,CASE WHEN @p=0 THEN NULL ELSE unixepoch()+@p*86400 END,@d,@d)
            """, ("@u", userId), ("@s", slot), ("@i", itemId), ("@p", (int)periodDays), ("@d", dura)))
        {
            ins.ExecuteNonQuery();
        }

        using (var log = TxCmd("""
            INSERT INTO shop_transactions(user_id,tx_type,item_id,period_days,gp_delta,cash_delta)
            VALUES(@u,@t,@i,@p,@g,@c)
            """, ("@u", userId), ("@t", useCash ? 1 : 0), ("@i", itemId), ("@p", (int)periodDays),
                 ("@g", useCash ? 0 : -price), ("@c", useCash ? -price : 0)))
        {
            log.ExecuteNonQuery();
        }

        return new(true, itemId, 0f, 0f, periodDays, kind, dura);
    }

    public int GetCash(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT cash FROM accounts WHERE account_id=(SELECT account_id FROM users WHERE user_id=@u)",
                ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    // ------------------------------------------------------------- sell
    /// <summary>
    /// 賣出背包單件 (208)。回收價 = 目錄 price_gp 的 20% (私服預設;
    /// 日版實價由 server 決定)。回 (ok, item_id, 賣後 GP)。
    /// </summary>
    public (bool Ok, int ItemId, long GpAfter) SellItem(long userId, int slot)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                using var find = Cmd(
                    "SELECT rowid, item_id FROM inventory WHERE user_id=@u AND slot=@s",
                    ("@u", userId), ("@s", slot));
                find.Transaction = tx;

                long rowId;
                int itemId;
                using (var r = find.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        tx.Rollback();
                        return (false, 0, 0);
                    }

                    rowId = r.GetInt64(0);
                    itemId = r.GetInt32(1);
                }

                using var price = Cmd(
                    "SELECT price_gp FROM item_catalog WHERE item_id=@i", ("@i", itemId));
                price.Transaction = tx;
                long refund = Convert.ToInt64(price.ExecuteScalar() ?? 0L) / 5;

                using var del = Cmd("DELETE FROM inventory WHERE rowid=@id", ("@id", rowId));
                del.Transaction = tx;
                del.ExecuteNonQuery();

                using var pay = Cmd(
                    "UPDATE users SET game_point = game_point + @g WHERE user_id=@u RETURNING game_point",
                    ("@g", refund), ("@u", userId));
                pay.Transaction = tx;
                long gpAfter = Convert.ToInt64(pay.ExecuteScalar()!);

                tx.Commit();
                return (true, itemId, gpAfter);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

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

    // ------------------------------------------------------------- messages
    public sealed record MailMsg(long MsgId, string From, string Title, string Body, bool IsRead, ushort DateCode);

    /// <summary>寄信 (419)。收件人須存在; body ≤200 (schema CHECK)。</summary>
    public bool SendMessage(long fromUserId, string toNick, string title, string body)
    {
        lock (_gate)
        {
            using var who = Cmd(
                "SELECT user_id FROM users WHERE nickname=@n", ("@n", toNick));
            var toId = who.ExecuteScalar();

            if (toId is null)
            {
                return false;
            }

            using var ins = Cmd("""
                INSERT INTO messages(from_user_id, to_user_id, title, body)
                VALUES(@f, @t, @ti, @b)
                """,
                ("@f", fromUserId),
                ("@t", (long)toId),
                ("@ti", title),
                ("@b", body.Length > 200 ? body[..200] : body));
            return ins.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>收件匣 (425→426)。date 編碼 = MMDD (u16)。</summary>
    public List<MailMsg> GetMessages(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT m.msg_id, COALESCE(u.nickname,'system'), m.title, m.body,
                       m.is_read, strftime('%m%d', m.sent_at, 'unixepoch')
                FROM messages m
                LEFT JOIN users u ON u.user_id = m.from_user_id
                WHERE m.to_user_id=@u
                ORDER BY m.msg_id DESC LIMIT 50
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();

            List<MailMsg> list = [];
            while (r.Read())
            {
                list.Add(new(
                    r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetInt64(4) != 0, ushort.Parse(r.GetString(5))));
            }

            return list;
        }
    }

    /// <summary>未讀信數 (783→784 信箱紅點)。</summary>
    public int CountUnreadMessages(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "SELECT COUNT(*) FROM messages WHERE to_user_id=@u AND is_read=0",
                ("@u", userId));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    /// <summary>刪信 (421)。</summary>
    public bool DeleteMessage(long userId, long msgId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "DELETE FROM messages WHERE msg_id=@m AND to_user_id=@u",
                ("@m", msgId), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    /// <summary>標記信件已讀 (GL_MSG_READ 423/424)。</summary>
    public bool MarkMessageRead(long userId, long msgId)
    {
        lock (_gate)
        {
            using var cmd = Cmd(
                "UPDATE messages SET is_read=1 WHERE msg_id=@m AND to_user_id=@u",
                ("@m", msgId), ("@u", userId));
            return cmd.ExecuteNonQuery() == 1;
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

    /// <summary>銷毀/丟棄道具 (GS_DESTROYITEM 802/803)。</summary>
    public bool DestroyInventoryItem(long userId, int invSlot, int itemId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                DELETE FROM inventory
                WHERE user_id=@u AND slot=@s AND item_id=@i
                """, ("@u", userId), ("@s", invSlot), ("@i", itemId));
            return cmd.ExecuteNonQuery() == 1;
        }
    }

}
