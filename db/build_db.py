#!/usr/bin/env python3
"""
建立 PaperMan 私服 SQLite DB。

  python3 db/build_db.py [--db /tmp/paperman.sqlite] [--fresh]

DB path:
  --db overrides the output path; otherwise PAPERMAN_DB is used when set,
  falling back to /tmp/paperman.sqlite. Generated SQLite files stay outside
  the repository by default.

步驟:
  1. 執行 db/schema.sql
  2. 從 db/packets.tsv 匯入 sub_9D2050 抽出的完整 opcode 註冊表
  3. 寫入預設 server_config
  4. 自我檢查 (foreign_key_check / integrity_check / 統計)
"""
import argparse
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_DB = os.environ.get('PAPERMAN_DB', '/tmp/paperman.sqlite')

SUBSYSTEM = {
    'GT': 'transport', 'GE': 'logout', 'GL': 'lobby', 'GR': 'room',
    'GG': 'ingame', 'GS': 'shop', 'GP': 'stats/pachinko', 'GI': 'inventory',
    'GM': 'nickname', 'GC': 'channel/clan', 'GQ': 'quest', 'GV': 'gm-viewer',
    'GX': 'xigncode', 'PM': 'p2p-master', 'UDP': 'nat', 'TCP': 'nat', 'Y': 'nat',
    'MASTER': 'ops', 'SECURITY': 'anticheat',
}


def direction(name: str) -> str:
    if name.endswith('_REQ'):
        return 'C2S'
    if name.endswith(('_ACK', '_NOTIFY', '_NOTICE', '_INF')):
        return 'S2C'
    return 'BOTH'


def migrate_legacy_room_mode_columns(con: sqlite3.Connection) -> None:
    """Preserve the local `rule` values under the source-proven mode_index name."""
    for table in ('rooms', 'match_results'):
        columns = {row[1] for row in con.execute(f'PRAGMA table_info({table})')}
        if 'rule' not in columns:
            continue
        if 'mode_index' in columns:
            raise RuntimeError(f'cannot rename {table}.rule: {table}.mode_index already exists')
        con.execute(f'ALTER TABLE {table} RENAME COLUMN rule TO mode_index')


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        '--db',
        default=DEFAULT_DB,
        help='generated SQLite path (default: PAPERMAN_DB or /tmp/paperman.sqlite)',
    )
    ap.add_argument('--fresh', action='store_true', help='刪掉舊 DB 重建')
    args = ap.parse_args()

    if args.fresh and os.path.exists(args.db):
        os.remove(args.db)

    con = sqlite3.connect(args.db)
    con.executescript(open(os.path.join(HERE, 'schema.sql'), encoding='utf-8').read())
    migrate_legacy_room_mode_columns(con)

    # --- opcode 註冊表 (sub_9D2050) ---
    rows = []
    with open(os.path.join(HERE, 'packets.tsv'), encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            opc, name = line.split('\t')
            prefix = name.split('_', 1)[0]
            rows.append((int(opc), name, direction(name),
                         SUBSYSTEM.get(prefix, prefix.lower())))
    con.executemany(
        'INSERT OR REPLACE INTO protocol_packets(opcode,name,direction,subsystem) '
        'VALUES (?,?,?,?)', rows)

    # --- 預設運維開關 (MASTER_* 對應) ---
    defaults = {
        'login_enabled': '1',            # MASTER_ENABLE/DISABLE_LOGIN (406/407)
        'event_exp_rate': '100',         # MASTER_EVENTEXP (404), 百分比
        'event_page': '0',               # MASTER_EVENTPAGE (402)
        'billing_disabled': '0',         # MASTER_DISBILL (408)
        'gms_disabled': '0',             # MASTER_DISGMS (412)
        'packet_delay_allow_sec': '5',   # MASTER_RESET_PACKET_DELAY... (831)
        'compress_threshold': '9600',    # sub_593280 的 n0x2580 門檻
        'protocol_base': '100',          # GS_BASE
        'max_rooms': '210',              # sub_568CE0 n0xD2
        'max_room_players': '10',
        'max_inventory_slots': '5120',   # sub_524B70
        'inventory_page_size': '100',
        'max_characters': '20',          # sub_524010
        'weapon_groups': '4',            # sub_524660
        'skill_slots': '9',              # sub_527550 → sub_522480 (9×s32 UI items)
        'new_skill_profile_count': '5',
        'new_skill_puzzle_slots': '7',   # selected profile → sub_527D00 (u8 + 7×s32)
        'schema_version': '1',
    }
    con.executemany(
        'INSERT OR REPLACE INTO server_config(key,value) VALUES (?,?)',
        defaults.items())

    con.commit()

    # --- 自我檢查 ---
    fk = con.execute('PRAGMA foreign_key_check').fetchall()
    ic = con.execute('PRAGMA integrity_check').fetchone()[0]
    n_pkt = con.execute('SELECT COUNT(*) FROM protocol_packets').fetchone()[0]
    n_tbl = con.execute(
        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
    ).fetchone()[0]
    n_view = con.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='view'").fetchone()[0]
    n_trg = con.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger'").fetchone()[0]

    print(f'DB          : {args.db}')
    print(f'integrity   : {ic}')
    print(f'fk problems : {len(fk)}')
    print(f'tables      : {n_tbl}, views: {n_view}, triggers: {n_trg}')
    print(f'packets     : {n_pkt} opcodes loaded (from sub_9D2050)')
    for sub, cnt in con.execute(
            'SELECT subsystem, COUNT(*) FROM protocol_packets '
            'GROUP BY subsystem ORDER BY 2 DESC'):
        print(f'  {sub:<15} {cnt}')
    con.close()
    return 0 if (ic == 'ok' and not fk) else 1


if __name__ == '__main__':
    sys.exit(main())
