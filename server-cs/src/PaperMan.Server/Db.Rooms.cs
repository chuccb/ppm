// =============================================================================
// 地圖目錄存取 — map_catalog (db/import_pats.py 自 maplist.pat 建表)。
//
//   maplist.pat 條目 836B (sub_723B10 載入器): +0 s32 mode bitmask,
//   +4 s32 map_id, +8 128B 檔名 UTF-16, +136 128B 顯示名 UTF-16。
//   模式→bit 對照見 Handlers.Room.cs 的 ModeMapBit (docs/RESOURCES.md §4b)。
// =============================================================================
using System.Collections.Frozen;
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    private FrozenDictionary<byte, int>? _mapModes;

    /// <summary>map_catalog: map_id → modes bitmask (只收 u8 可表示的 map_id)。</summary>
    public FrozenDictionary<byte, int> GetMapModes()
    {
        lock (_gate)
        {
            if (_mapModes is not null)
            {
                return _mapModes;
            }

            var table = new Dictionary<byte, int>();
            using var cmd = Cmd("SELECT map_id, modes FROM map_catalog ORDER BY map_id");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int id = r.GetInt32(0);
                if (id is >= 0 and <= 255)
                {
                    table[(byte)id] = r.GetInt32(1);
                }
            }

            return _mapModes = table.ToFrozenDictionary();
        }
    }
}
