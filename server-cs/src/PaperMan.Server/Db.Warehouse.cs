// =============================================================================
// 角色倉庫 (ロッカー) 存取 — GL_MYWAREHOUSE* 家族 (855-863)。
//
//   反編譯定案 (docs/PACKETS.md §3.15c3):
//    * 7 頁籤 0..6, tab0 不用; 容量 sub_4F9B10: 1=100, 2/3/4=300, 5/6=3000。
//    * 856/863 狀態塊 = 7 × 10B 記錄 {s32 count, s32 到期(打包日期), u8 loaded, u8 pad}
//        — 到期欄為 sub_48B9A0 的位元打包日期: (年-2000)<<24|月<<19|日<<13|時<<7|分
//          (sub_5309C0 解出後以 0x495「期間:残り %dヶ月…」顯示; 0 = 未持有)。
//    * 物品條目 28B = 背包 inventory 同構 {s32 slot, s32 item_id, f32, f32,
//        s32 period, u8 kind, u16 dura} (sub_524F70 建造; 858 ACK 逐條 sub_4FC540)。
//
//   私服策略: 倉庫頁籤預設全持有 (遠未來到期), 使 client 正常載入/顯示;
//   物品的 kind 由 item_catalog 查得 (非 0); 搬移時原樣保留 period_days
//   與 expires_at (不改動到期時刻)。
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    public const int WarehouseTabCount = 7;                 // 頁籤 0..6 (0 不用)
    public const int WarehouseFirstTab = 1;
    public const int WarehouseLastTab = 6;
    public const int InventoryCapacity = 5120;              // sub_524B70 背包上限

    /// <summary>頁籤容量 — sub_4F9B10 switch: 1=100, 2/3/4=300, 5/6=3000。</summary>
    public static int WarehouseCapacity(byte tab) => tab switch
    {
        1 => 100,
        2 or 3 or 4 => 300,
        5 or 6 => 3000,
        _ => 0,
    };

    /// <summary>倉庫物品 — wire 23B 條目 (slot/item_id/f1/f2/period/kind/dura)。</summary>
    public sealed record WarehouseItem(
        int Slot, int ItemId, float F1, float F2, int PeriodDaysLeft,
        byte Kind, ushort DuraCur, ushort DuraMax);

    /// <summary>單一頁籤狀態 (供 856/863 的 10B 記錄)。</summary>
    public readonly record struct WarehouseTabState(int Count, int ExpiryPacked);

    // 搬移時保留的原始儲存欄位 (wire 的 PeriodDaysLeft 是 floor(剩餘天數), 非原始 period_days)
    private readonly record struct StoredItem(
        int Slot, int ItemId, float F1, float F2, int DaysLeft,
        byte Kind, ushort DuraCur, ushort DuraMax, int PeriodDays, long? ExpiresAt)
    {
        public WarehouseItem ToWire(int newSlot) =>
            new(newSlot, ItemId, F1, F2, DaysLeft, Kind, DuraCur, DuraMax);
    }

    // 與 item_catalog LEFT JOIN 取 kind; 後方接 FROM/WHERE 子句
    private const string StoredItemSelect = """
        SELECT i.slot, i.item_id, i.stat_f1, i.stat_f2,
               CASE WHEN i.expires_at IS NULL THEN 0
                    ELSE MAX(0, CAST((i.expires_at - unixepoch()) / 86400 AS INTEGER)) END,
               COALESCE(c.kind, 0), i.durability_cur, i.durability_max,
               i.period_days, i.expires_at
        """;

    private static StoredItem ReadStoredItem(SqliteDataReader r) => new(
        r.GetInt32(0), r.GetInt32(1), r.GetFloat(2), r.GetFloat(3),
        r.GetInt32(4), (byte)r.GetInt32(5),
        (ushort)r.GetInt32(6), (ushort)r.GetInt32(7),
        r.GetInt32(8), r.IsDBNull(9) ? null : r.GetInt64(9));

    // ------------------------------------------------------------- 查詢
    /// <summary>856/863 的 7×10B 狀態塊資料 (tab0 恆零)。</summary>
    public WarehouseTabState[] GetWarehouseInfo(long userId)
    {
        lock (_gate)
        {
            EnsureLockerRows(userId);                       // 私服預設全持有
            var tabs = new WarehouseTabState[WarehouseTabCount];
            using var cmd = Cmd("""
                SELECT w.tab, COUNT(i.item_id),
                       CASE WHEN w.expires_at <= 0 THEN 0 ELSE w.expires_at END
                FROM warehouse_lockers w
                LEFT JOIN warehouse_items i ON i.user_id = w.user_id AND i.tab = w.tab
                WHERE w.user_id = @u
                GROUP BY w.tab
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int tab = r.GetInt32(0);
                if (tab is < WarehouseFirstTab or > WarehouseLastTab)
                {
                    continue;
                }

                tabs[tab] = new(r.GetInt32(1), PackExpiry(r.GetInt64(2)));
            }

            return tabs;
        }
    }

    /// <summary>858 的物品清單 (依 slot 排序; 完整頁籤一次送完, count==total)。</summary>
    public List<WarehouseItem> GetWarehouseItems(long userId, byte tab)
    {
        lock (_gate)
        {
            List<WarehouseItem> list = [];
            using var cmd = Cmd($"""
                {StoredItemSelect}
                FROM warehouse_items i
                LEFT JOIN item_catalog c ON c.item_id = i.item_id
                WHERE i.user_id = @u AND i.tab = @t
                ORDER BY i.slot
                """, ("@u", userId), ("@t", tab));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var it = ReadStoredItem(r);
                list.Add(it.ToWire(it.Slot));
            }

            return list;
        }
    }

    // ------------------------------------------------------------- 存入/取出
    /// <summary>859: 背包 slot → 倉庫頁籤。回 (成功, 錯誤碼, 新條目, 新頁籤計數)。</summary>
    public (bool Ok, byte Err, WarehouseItem? Item, int Count) PushToWarehouse(long userId, byte tab, int invSlot)
    {
        if (tab is < WarehouseFirstTab or > WarehouseLastTab || invSlot < 0)
        {
            return (false, 1, null, 0);
        }

        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                SqliteCommand TxCmd(string sql, params ReadOnlySpan<(string, object?)> args)
                {
                    var c = Cmd(sql, args);
                    c.Transaction = tx;
                    return c;
                }

                // 1. 背包條目快照 (須存在)
                StoredItem item;
                using (var q = TxCmd($"""
                    {StoredItemSelect}
                    FROM inventory i
                    LEFT JOIN item_catalog c ON c.item_id = i.item_id
                    WHERE i.user_id = @u AND i.slot = @s
                    """, ("@u", userId), ("@s", invSlot)))
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        tx.Rollback();
                        return (false, 1, null, 0);        // 背包無此條目
                    }

                    item = ReadStoredItem(r);
                }

                // 2. 倉庫容量檢查 (sub_4F9B10; client 端已擋, server 複驗)
                int count = Convert.ToInt32(TxCmd(
                    "SELECT COUNT(*) FROM warehouse_items WHERE user_id=@u AND tab=@t",
                    ("@u", userId), ("@t", tab)).ExecuteScalar() ?? 0L);
                if (count >= WarehouseCapacity(tab))
                {
                    tx.Rollback();
                    return (false, 1, null, count);        // 倉庫已滿
                }

                // 3. 最小空 slot + 搬移 (單一交易)
                int slot = Convert.ToInt32(TxCmd("""
                    SELECT IFNULL(MIN(t.slot+1),0) FROM
                      (SELECT -1 AS slot UNION SELECT slot FROM warehouse_items WHERE user_id=@u AND tab=@t) t
                    WHERE t.slot+1 NOT IN (SELECT slot FROM warehouse_items WHERE user_id=@u AND tab=@t)
                    """, ("@u", userId), ("@t", tab)).ExecuteScalar());

                TxCmd("DELETE FROM inventory WHERE user_id=@u AND slot=@s",
                    ("@u", userId), ("@s", invSlot)).ExecuteNonQuery();

                using (var ins = TxCmd("""
                    INSERT INTO warehouse_items
                        (user_id, tab, slot, item_id, stat_f1, stat_f2, period_days, expires_at,
                         durability_cur, durability_max)
                    VALUES (@u, @t, @slot, @i, @f1, @f2, @p, @e, @dc, @dm)
                    """,
                    ("@u", userId), ("@t", tab), ("@slot", slot), ("@i", item.ItemId),
                    ("@f1", item.F1), ("@f2", item.F2), ("@p", item.PeriodDays),
                    ("@e", item.ExpiresAt),
                    ("@dc", (int)item.DuraCur), ("@dm", (int)item.DuraMax)))
                {
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
                return (true, 0, item.ToWire(slot), count + 1);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    /// <summary>861: 倉庫 slot → 背包。回 (成功, 錯誤碼, 新條目, 新頁籤計數)。</summary>
    public (bool Ok, byte Err, WarehouseItem? Item, int Count) PopFromWarehouse(long userId, byte tab, int whSlot)
    {
        if (tab is < WarehouseFirstTab or > WarehouseLastTab || whSlot < 0)
        {
            return (false, 1, null, 0);
        }

        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                SqliteCommand TxCmd(string sql, params ReadOnlySpan<(string, object?)> args)
                {
                    var c = Cmd(sql, args);
                    c.Transaction = tx;
                    return c;
                }

                // 1. 倉庫條目快照 (須存在)
                StoredItem item;
                using (var q = TxCmd($"""
                    {StoredItemSelect}
                    FROM warehouse_items i
                    LEFT JOIN item_catalog c ON c.item_id = i.item_id
                    WHERE i.user_id = @u AND i.tab = @t AND i.slot = @s
                    """, ("@u", userId), ("@t", tab), ("@s", whSlot)))
                using (var r = q.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        tx.Rollback();
                        return (false, 1, null, 0);        // 倉庫無此條目
                    }

                    item = ReadStoredItem(r);
                }

                // 2. 背包最小空 slot (sub_524B70 的 5120 上限)
                int slot = Convert.ToInt32(TxCmd("""
                    SELECT IFNULL(MIN(t.slot+1),0) FROM
                      (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=@u) t
                    WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=@u)
                    """, ("@u", userId)).ExecuteScalar());

                if (slot >= InventoryCapacity)
                {
                    tx.Rollback();
                    return (false, 5, null, 0);            // 背包已滿 (0x49E「インベントリーに空きがありません」)
                }

                // 3. 搬移
                TxCmd("DELETE FROM warehouse_items WHERE user_id=@u AND tab=@t AND slot=@s",
                    ("@u", userId), ("@t", tab), ("@s", whSlot)).ExecuteNonQuery();

                using (var ins = TxCmd("""
                    INSERT INTO inventory
                        (user_id, slot, item_id, stat_f1, stat_f2, period_days, expires_at,
                         durability_cur, durability_max)
                    VALUES (@u, @slot, @i, @f1, @f2, @p, @e, @dc, @dm)
                    """,
                    ("@u", userId), ("@slot", slot), ("@i", item.ItemId),
                    ("@f1", item.F1), ("@f2", item.F2), ("@p", item.PeriodDays),
                    ("@e", item.ExpiresAt),
                    ("@dc", (int)item.DuraCur), ("@dm", (int)item.DuraMax)))
                {
                    ins.ExecuteNonQuery();
                }

                int count = Convert.ToInt32(TxCmd(
                    "SELECT COUNT(*) FROM warehouse_items WHERE user_id=@u AND tab=@t",
                    ("@u", userId), ("@t", tab)).ExecuteScalar() ?? 0L);

                tx.Commit();
                return (true, 0, item.ToWire(slot), count);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    // ------------------------------------------------------------- 工具
    /// <summary>私服預設: 補齊 6 個頁籤的持有紀錄 (已存在者保留)。</summary>
    private void EnsureLockerRows(long userId)
    {
        long farFuture = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds();
        using var cmd = Cmd("""
            INSERT OR IGNORE INTO warehouse_lockers(user_id, tab, expires_at)
            VALUES (@u, 1, @e), (@u, 2, @e), (@u, 3, @e), (@u, 4, @e), (@u, 5, @e), (@u, 6, @e)
            """, ("@u", userId), ("@e", farFuture));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// epoch 秒 → sub_48B9A0 的位元打包日期 (年-2000/月/日/時/分)。
    /// client 以 localtime 解讀 (sub_5309C0), 日版以 JST(+9) 為準 —
    /// 固定 +9 打包, 避免 server 時區影響顯示。
    /// </summary>
    private static int PackExpiry(long epochSeconds)
    {
        if (epochSeconds <= 0)
        {
            return 0;                                       // 未持有
        }

        var t = DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToOffset(TimeSpan.FromHours(9));
        int year = Math.Clamp(t.Year - 2000, 0, 255);
        return (year << 24) | (t.Month << 19) | (t.Day << 13) | (t.Hour << 7) | t.Minute;
    }
}
