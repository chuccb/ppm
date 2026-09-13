#!/usr/bin/env python3
"""
從 Extracted/ 的已解密 .pat 檔灌入 SQLite 目錄表 (十五輪)。

來源 (先用 server/pmfile.py 解密到 Extracted/decrypted/):
  itemdata.pat.dec  → item_catalog   (21,164 條, 997B 變長條目)
  Quest.pat.dec     → quest_catalog  (844 條, CSV/EUC-KR)
  maplist.pat.dec   → map_catalog    (836B 定長條目)

itemdata 條目 (載入器 @131262, 檔案序):
  [s32 id][s32][s32][s32][s32 name_len][name UTF-16LE]
  [tail 721B: b532,kind(b533),3×u8,3×s32(540),5×s32(564),6×s32(584),
   8×s32(608),b640,b641,s32 req_level(644), ... 價格/期限區]
"""
from __future__ import annotations

import pathlib
import sqlite3
import struct
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEC = ROOT / "Extracted" / "decrypted"
DB = ROOT / "db" / "paperman.db"

TAIL = 721


def import_items(con: sqlite3.Connection) -> int:
    data = (DEC / "itemdata.pat.dec").read_bytes()
    ver, count = struct.unpack_from("<ii", data, 0)

    rows = []
    off = 8
    while off + 20 <= len(data):
        eid, t4, t8, t12 = struct.unpack_from("<4i", data, off)
        (nl,) = struct.unpack_from("<i", data, off + 16)
        if not 0 < nl <= 512:
            break

        name = (
            data[off + 20 : off + 20 + nl]
            .decode("utf-16-le", errors="replace")
            .split("\x00")[0]
            .strip()
        )
        tail = data[off + 20 + nl : off + 20 + nl + TAIL]

        kind = tail[1]                                     # b533 (kind 主分類)
        (req_level,) = struct.unpack_from("<i", tail, 95)  # +644
        v540 = struct.unpack_from("<3i", tail, 5)
        v584 = struct.unpack_from("<6i", tail, 37)

        rows.append((eid, name, min(kind, 20), 0, 0, v540[0], req_level, t4, t8))
        off += 20 + nl + TAIL

    con.execute("DELETE FROM inventory")
    con.execute("DELETE FROM gifts")
    con.execute("DELETE FROM shop_transactions")
    con.execute("DELETE FROM item_catalog")
    con.executemany(
        """
        INSERT OR REPLACE INTO item_catalog
            (item_id, name, kind, price_gp, price_cash, durability, req_level, type2, type3)
        VALUES (?,?,?,?,?,?,?,?,?)
        """,
        rows,
    )
    return len(rows)


def import_quests(con: sqlite3.Connection) -> int:
    text = (DEC / "Quest.pat.dec").read_bytes().decode("cp932", errors="replace")
    lines = text.split("\r\n")
    header = [h.strip() for h in lines[1].split(",")]
    idx = {name: i for i, name in enumerate(header)}

    rows = []
    for line in lines[2:]:
        parts = line.split(",")
        if not parts or not parts[0].strip().isdigit():
            continue

        qid = int(parts[0])
        rows.append(
            (
                qid,
                parts[idx["QuestName"]].strip()[:120],
                qid // 10000,                              # 類別 (sub_91C6F0: /10000)
                1 if parts[idx["QuestRepeat"]].strip() == "1" else 0,
                int(parts[idx["QuestTerm"]] or 0),         # 條件類型 (sub_9252D0 cond)
                int(parts[idx["QuestTermData"]] or 0),     # 目標值
                int(parts[idx["ClearItem1"]] or 0) or None,
                int(parts[idx["UserLevel"]] or 0),
            )
        )

    con.execute("DELETE FROM user_quests")
    con.execute("DELETE FROM quest_catalog")
    con.executemany(
        """
        INSERT OR REPLACE INTO quest_catalog
            (quest_id, name, quest_type, is_daily, goal_type, goal, reward_item, req_level)
        VALUES (?,?,?,?,?,?,?,?)
        """,
        rows,
    )
    return len(rows)


def import_maps(con: sqlite3.Connection) -> int:
    data = (DEC / "maplist.pat.dec").read_bytes()
    # 頭: f32? + s32 count (載入器讀 2×4B 後進迴圈; 條目 836B)
    (count,) = struct.unpack_from("<i", data, 4)

    rows = []
    off = 8
    for _ in range(count):
        if off + 836 > len(data):
            break
        # 十五輪三驗: +0 = 模式 bitmask (大量重複), +4 = 唯一 map_id
        flags, map_id = struct.unpack_from("<ii", data, off)
        # 十五輪實測: +8 = 檔名 (maps\\*.pmm), +136 = 顯示名 (日文)
        fname = data[off + 8 : off + 136].decode("utf-16-le", errors="replace").split("\x00")[0]
        disp = data[off + 136 : off + 264].decode("utf-16-le", errors="replace").split("\x00")[0]
        rows.append((map_id, disp.strip(), fname.strip(), flags))
        off += 836

    con.execute(
        """
        CREATE TABLE IF NOT EXISTS map_catalog (
            map_id    INTEGER PRIMARY KEY,
            name      TEXT,
            file_name TEXT,
            modes     INTEGER NOT NULL DEFAULT 0
        ) STRICT
        """
    )
    con.execute("DELETE FROM map_catalog")
    con.executemany(
        "INSERT OR REPLACE INTO map_catalog (map_id, name, file_name, modes) VALUES (?,?,?,?)",
        rows,
    )
    return len(rows)


def main() -> int:
    con = sqlite3.connect(DB)
    con.execute("PRAGMA foreign_keys=OFF")

    # item_catalog 需要新欄位 (req_level/type2/type3)
    cols = [r[1] for r in con.execute("PRAGMA table_info(item_catalog)")]
    for col in ("req_level", "type2", "type3"):
        if col not in cols:
            con.execute(f"ALTER TABLE item_catalog ADD COLUMN {col} INTEGER NOT NULL DEFAULT 0")

    qcols = [r[1] for r in con.execute("PRAGMA table_info(quest_catalog)")]
    for col, decl in (("goal_type", "INTEGER NOT NULL DEFAULT 0"), ("req_level", "INTEGER NOT NULL DEFAULT 0")):
        if col not in qcols:
            con.execute(f"ALTER TABLE quest_catalog ADD COLUMN {col} {decl}")

    n_items = import_items(con)
    n_quests = import_quests(con)
    n_maps = import_maps(con)
    con.commit()

    print(f"item_catalog : {n_items} 條 (真實日版目錄)")
    print(f"quest_catalog: {n_quests} 條")
    print(f"map_catalog  : {n_maps} 條")

    for row in con.execute(
        "SELECT kind, COUNT(*) FROM item_catalog GROUP BY kind ORDER BY 2 DESC LIMIT 8"
    ):
        print(f"  kind {row[0]:>3}: {row[1]}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
