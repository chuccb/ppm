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

import argparse
import os
import pathlib
import sqlite3
import struct

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEC = ROOT / "Extracted" / "decrypted"
DEFAULT_DB = os.environ.get("PAPERMAN_DB", "/tmp/paperman.sqlite")

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

        # t8 = 基底物品參照 (變體→原型, 十七輪定案); t4 = 稀有連動
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
                int(parts[idx["CharacterType"]] or 0),     # 0=全角色, 1..14 限定
                int(parts[idx["ClearItemOption1"]] or 0),  # 獎勵期限天數 (0=永久)
                1 if parts[idx["Hidden"]].strip() == "1" else 0,
            )
        )

    con.execute("DELETE FROM user_quests")
    con.execute("DELETE FROM quest_catalog")
    con.executemany(
        """
        INSERT OR REPLACE INTO quest_catalog
            (quest_id, name, quest_type, is_daily, goal_type, goal, reward_item,
             req_level, char_type, reward_period, hidden)
        VALUES (?,?,?,?,?,?,?,?,?,?,?)
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




def import_weapon_parts(con: sqlite3.Connection) -> int:
    """weaponparts.pat (CSV/cp932): Gun Item No + 4 組×10 改裝件 id。"""
    text = (DEC / "weaponparts.pat.dec").read_bytes().decode("cp932", errors="replace")
    lines = text.split("\r\n")

    con.execute(
        """
        CREATE TABLE IF NOT EXISTS weapon_parts_catalog (
            gun_item_id  INTEGER NOT NULL,
            grp          INTEGER NOT NULL CHECK (grp BETWEEN 0 AND 7),
            slot         INTEGER NOT NULL CHECK (slot BETWEEN 0 AND 9),
            part_item_id INTEGER NOT NULL,
            PRIMARY KEY (gun_item_id, grp, slot)
        ) STRICT, WITHOUT ROWID
        """
    )
    con.execute("DELETE FROM weapon_parts_catalog")

    # 十九輪修正: 實際 8 組 — Parts1..4(col1..40), Parts5(41..50),
    # Dot(51..60), Parts6(61..70), Parts7(71..80); grp 依欄位序 0..7
    rows = []
    for line in lines[2:]:
        parts = line.split(",")
        if not parts or not parts[0].strip().isdigit():
            continue
        gun = int(parts[0])
        for grp in range(8):
            for slot in range(10):
                col = 1 + grp * 10 + slot
                if col < len(parts) and parts[col].strip().isdigit() and int(parts[col]) > 0:
                    rows.append((gun, grp, slot, int(parts[col])))

    con.executemany("INSERT OR REPLACE INTO weapon_parts_catalog VALUES (?,?,?,?)", rows)
    return len(rows)


def import_parts_ability(con: sqlite3.Connection) -> int:
    """partsability.pat (CSV/cp932): 413 條武器彈道/傷害參數。"""
    text = (DEC / "partsability.pat.dec").read_bytes().decode("cp932", errors="replace")
    lines = text.split("\r\n")
    hdr = [h.strip().replace(" ", "_").lower() for h in lines[1].split(",")]
    idx = {h: i for i, h in enumerate(hdr)}

    con.execute(
        """
        CREATE TABLE IF NOT EXISTS parts_ability_catalog (
            item_id INTEGER PRIMARY KEY, recoil REAL, effective_range REAL,
            limit_range REAL, effective_damage REAL, limit_damage REAL,
            shot_delay REAL, move_speed REAL, shots_per_fire INTEGER
        ) STRICT
        """
    )
    con.execute("DELETE FROM parts_ability_catalog")

    rows = []
    for line in lines[2:]:
        p = line.split(",")
        if not p or not p[0].strip().isdigit():
            continue

        def g(name: str, cast=float):
            try:
                return cast(p[idx[name]])
            except (ValueError, IndexError):
                return 0

        rows.append(
            (int(p[0]), g("recoil"), g("effective_range"), g("limit_range"),
             g("effective_damage"), g("limit_damage"), g("shot_delay"),
             g("move_speed"), g("shots_per_fire", int))
        )

    con.executemany("INSERT OR REPLACE INTO parts_ability_catalog VALUES (?,?,?,?,?,?,?,?,?)", rows)
    return len(rows)


def import_recommend_sets(con: sqlite3.Connection) -> int:
    """RecommandItem.pat (CSV/cp932): 推薦套裝 → 809 GS_GET_RECOMMENDSET_INFO 資料源。"""
    text = (DEC / "RecommandItem.pat.dec").read_bytes().decode("cp932", errors="replace")
    lines = text.split("\r\n")

    con.execute(
        """
        CREATE TABLE IF NOT EXISTS recommend_set_catalog (
            set_id INTEGER NOT NULL, char_type INTEGER NOT NULL,
            concept INTEGER NOT NULL, slot INTEGER NOT NULL, item_id INTEGER NOT NULL,
            PRIMARY KEY (set_id, slot)
        ) STRICT, WITHOUT ROWID
        """
    )
    con.execute("DELETE FROM recommend_set_catalog")

    rows = []
    for line in lines[3:]:
        p = line.split(",")
        if len(p) < 12 or not p[0].strip().isdigit():
            continue
        sid, ct, cc = int(p[0]), int(p[1] or 0), int(p[2] or 0)
        for s in range(9):
            v = p[3 + s].strip()
            if v.isdigit() and int(v) > 0:
                rows.append((sid, ct, cc, s, int(v)))

    con.executemany("INSERT OR REPLACE INTO recommend_set_catalog VALUES (?,?,?,?,?)", rows)
    return len(rows)


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Import decrypted .pat resources into an offline SQLite DB"
    )
    ap.add_argument(
        "--db",
        default=DEFAULT_DB,
        help="generated SQLite path (default: PAPERMAN_DB or /tmp/paperman.sqlite)",
    )
    args = ap.parse_args()

    con = sqlite3.connect(args.db)
    con.execute("PRAGMA foreign_keys=OFF")

    # item_catalog 需要新欄位 (req_level/type2/type3)
    cols = [r[1] for r in con.execute("PRAGMA table_info(item_catalog)")]
    for col in ("req_level", "type2", "type3"):
        if col not in cols:
            con.execute(f"ALTER TABLE item_catalog ADD COLUMN {col} INTEGER NOT NULL DEFAULT 0")

    qcols = [r[1] for r in con.execute("PRAGMA table_info(quest_catalog)")]
    for col, decl in (
        ("goal_type", "INTEGER NOT NULL DEFAULT 0"),
        ("req_level", "INTEGER NOT NULL DEFAULT 0"),
        ("char_type", "INTEGER NOT NULL DEFAULT 0"),
        ("reward_period", "INTEGER NOT NULL DEFAULT 0"),
        ("hidden", "INTEGER NOT NULL DEFAULT 0"),
    ):
        if col not in qcols:
            con.execute(f"ALTER TABLE quest_catalog ADD COLUMN {col} {decl}")

    n_items = import_items(con)
    n_quests = import_quests(con)
    n_maps = import_maps(con)
    n_parts = import_weapon_parts(con)
    n_abil = import_parts_ability(con)
    n_rec = import_recommend_sets(con)
    con.commit()

    print(f"item_catalog : {n_items} 條 (真實日版目錄)")
    print(f"quest_catalog: {n_quests} 條")
    print(f"map_catalog  : {n_maps} 條")
    print(f"weapon_parts : {n_parts} 條")
    print(f"parts_ability: {n_abil} 條")
    print(f"recommend_set: {n_rec} 條")

    for row in con.execute(
        "SELECT kind, COUNT(*) FROM item_catalog GROUP BY kind ORDER BY 2 DESC LIMIT 8"
    ):
        print(f"  kind {row[0]:>3}: {row[1]}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
