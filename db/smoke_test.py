#!/usr/bin/env python3
"""
煙霧測試: 模擬伺服器處理封包時對離線 DB 的完整讀寫路徑。

用法:
  python3 db/smoke_test.py [--db /tmp/paperman.sqlite]

對應封包流程:
  GL_LOGIN_REQ(682) -> GM_CREATENICK(212) -> GL_MYINFO_ACK(198)
  -> GS_BUYITEM(204) -> GL_MYITEM_ACK(200 分頁) -> GI_CHANGEWP(220)
  -> GL_MAKEROOM(111) -> GR_END(133 結算) -> GP_CH*C 累計
  -> GL_FRIEND_ADD(429) -> GL_MSG_ADD(419) -> GQ_QUEST_ACCEPT(867)
"""
import argparse
import os
import sqlite3

DEFAULT_DB = os.environ.get('PAPERMAN_DB', '/tmp/paperman.sqlite')

parser = argparse.ArgumentParser(description='Exercise the offline SQLite schema and triggers')
parser.add_argument(
    '--db',
    default=DEFAULT_DB,
    help='generated SQLite path (default: PAPERMAN_DB or /tmp/paperman.sqlite)',
)
DB = parser.parse_args().db

con = sqlite3.connect(DB)
con.execute('PRAGMA foreign_keys = ON')
c = con.cursor()

def step(msg):
    print(f'[+] {msg}')

# --- 1. 帳號 + 玩家建立 (GL_LOGIN / GM_CREATENICK / GM_CREATECHAR) ---
step('accounts / users / trigger bootstrap')
c.execute("INSERT INTO accounts(login_name,pass_hash,pass_salt,cash) VALUES ('alice','h','s',5000)")
aid = c.lastrowid
c.execute("INSERT INTO users(account_id,nickname) VALUES (?, 'PaperAlice')", (aid,))
uid = c.lastrowid
assert c.execute('SELECT COUNT(*) FROM user_stats WHERE user_id=?', (uid,)).fetchone()[0] == 1
assert c.execute('SELECT COUNT(*) FROM weapon_groups WHERE user_id=?', (uid,)).fetchone()[0] == 4

# GL_INVENIN_ACK(255) provides five account-level NewSkill raw32 records.
# The first profile is always usable; profiles 1..4 retain a server-owned
# packed-minute expiration word that 466 cannot overwrite.
assert c.execute('SELECT selected_profile FROM new_skill_profile_state WHERE user_id=?', (uid,)).fetchone() == (0,)
assert c.execute('SELECT COUNT(*) FROM new_skill_profiles WHERE user_id=?', (uid,)).fetchone()[0] == 5
c.execute('UPDATE new_skill_profiles SET puzzle0=11010001, expires_at_packed_minute=0 WHERE user_id=? AND profile_index=0', (uid,))
assert c.execute('SELECT puzzle0, expires_at_packed_minute FROM new_skill_profiles WHERE user_id=? AND profile_index=0', (uid,)).fetchone() == (11010001, 0)
try:
    c.execute('INSERT INTO new_skill_profiles(user_id,profile_index) VALUES (?,5)', (uid,))
    raise AssertionError('NewSkill profile 5 should violate its 0..4 CHECK')
except sqlite3.IntegrityError:
    pass

c.execute("INSERT INTO accounts(login_name,pass_hash,pass_salt) VALUES ('bob','h','s')")
c.execute("INSERT INTO users(account_id,nickname) VALUES (?, 'PaperBob')", (c.lastrowid,))
uid2 = c.lastrowid

# 682 raw24 fingerprint: fresh databases enforce its exact native size in SQL,
# rather than relying on only the TypeScript packet reader to preserve this invariant.
c.execute("UPDATE accounts SET client_fingerprint=? WHERE account_id=?", (bytes(24), aid))
try:
    c.execute("UPDATE accounts SET client_fingerprint=? WHERE account_id=?", (bytes(23), aid))
    raise AssertionError('682 fingerprint 23 bytes should violate its CHECK constraint')
except sqlite3.IntegrityError:
    pass

# --- 2. 角色槽 (GM_CREATECHAR 214 / native normal appearance order, ≤20) ---
step('characters (slot 0..19 bound; canonical six-word starter)')
# The first six legacy-named columns retain native ordinal meaning:
# body, head, face, top, bottom, shoes. Type 1 maps to six raw offsets of 1.
c.execute('''INSERT INTO characters(
             user_id,slot_no,char_type,eq_primary,eq_secondary,eq_melee,
             eq_grenade,eq_head,eq_face)
             VALUES (?,0,1,1,1,1,1,1,1)''', (uid,))
assert c.execute('''SELECT eq_primary,eq_secondary,eq_melee,eq_grenade,eq_head,eq_face
                    FROM characters WHERE user_id=? AND slot_no=0''', (uid,)).fetchone() == (1, 1, 1, 1, 1, 1)
try:
    c.execute('INSERT INTO characters(user_id,slot_no,char_type) VALUES (?,20,1)', (uid,))
    raise AssertionError('slot 20 應該被 CHECK 擋下')
except sqlite3.IntegrityError:
    pass

# --- 3. 商店購買 -> 背包 (GS_BUYITEM 204 -> GL_MYITEM_ACK 200) ---
step('item_catalog / inventory / period 白名單')
# 真實 id 空間 (十二輪定案): 可購武器段 A = 15,301,001..15,302,000;
# 裝飾類 full_id = 類別基底 + u16 偏移 (見 docs/PACKETS.md §3.15pre1)
c.execute("INSERT OR IGNORE INTO item_catalog(item_id,name,kind,price_gp,durability) VALUES (15301001,'AK Paper',0,800,100)")
c.execute("INSERT OR IGNORE INTO item_catalog(item_id,name,kind,price_gp) VALUES (10400001,'Red Cap',2,300)")
c.execute("""INSERT INTO inventory(user_id,slot,item_id,period_days,expires_at,
             durability_cur,durability_max)
             VALUES (?,0,15301001,30,unixepoch()+30*86400,100,100)""", (uid,))
c.execute('INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES (?,1,10400001,0)', (uid,))
try:
    c.execute('INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES (?,2,15301001,45)', (uid,))
    raise AssertionError('period 45 應該被 CHECK 擋下 (白名單 sub_570B00)')
except sqlite3.IntegrityError:
    pass
c.execute("""INSERT INTO shop_transactions(user_id,tx_type,item_id,period_days,gp_delta)
             VALUES (?,0,15301001,30,-800)""", (uid,))

# 分頁 wire view (100/頁)
rows = c.execute('SELECT * FROM v_inventory_wire WHERE user_id=? ORDER BY slot LIMIT 100 OFFSET 0',
                 (uid,)).fetchall()
assert len(rows) == 2 and rows[0][5] in (29, 30)   # period_days_left

# --- 4. 武器編組 (GI_CHANGEWP 220) ---
step('weapon_groups and weaponparts compatibility catalog')
# sub_527DB0 expands the group-0 primary offset from base 12,100,000.
# weaponparts.pat's exact (gun,group,slot,part) relation belongs in its own
# table; it is neither a character appearance value nor a starter grant.
c.execute("INSERT INTO item_catalog(item_id,name,kind) VALUES (12100016,'Primary test',0)")
c.execute("INSERT INTO item_catalog(item_id,name,kind) VALUES (15210001,'Barrel test',0)")
c.execute('INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES (?,2,12100016,0)', (uid,))
c.execute('INSERT INTO inventory(user_id,slot,item_id,period_days) VALUES (?,3,15210001,0)', (uid,))
c.execute('INSERT INTO weapon_parts_catalog(gun_item_id,grp,slot,part_item_id) VALUES (12100016,0,0,15210001)')
c.execute('UPDATE weapon_groups SET equipped=1, part0=15210001 WHERE user_id=? AND group_no=0', (uid,))
assert c.execute('SELECT equipped,part0 FROM weapon_groups WHERE user_id=? AND group_no=0', (uid,)).fetchone() == (1, 15210001)
assert c.execute('SELECT 1 FROM weapon_parts_catalog WHERE gun_item_id=12100016 AND grp=0 AND part_item_id=15210001').fetchone() == (1,)
try:
    c.execute('UPDATE weapon_groups SET equipped=65536 WHERE user_id=? AND group_no=0', (uid,))
    raise AssertionError('weapon primary offset 65536 should violate its native u16 CHECK')
except sqlite3.IntegrityError:
    pass

# --- 5. 房間 (GL_MAKEROOM 111 / GR_CHANGESLOT 135) ---
step('rooms / room_slots')
c.execute("""INSERT INTO rooms(room_no,title,map_id,mode_index,win_count,max_players,master_id)
             VALUES (7,'来玩吧',3,1,5,8,?)""", (uid,))
c.execute('INSERT INTO room_slots(room_no,slot_no,user_id,team,is_ready) VALUES (7,0,?,0,1)', (uid,))
c.execute('INSERT INTO room_slots(room_no,slot_no,user_id,team) VALUES (7,1,?,1)', (uid2,))
try:
    c.execute("INSERT INTO rooms(room_no,title) VALUES (210,'overflow')")
    raise AssertionError('room 210 應該被 CHECK 擋下 (上限 0xD2)')
except sqlite3.IntegrityError:
    pass

# --- 6. 對戰結算 (GR_END 133) -> trigger 累計 user_stats ---
step('match_results -> trg_match_rollup -> user_stats/users')
c.execute("""INSERT INTO match_results(room_no,map_id,mode_index,started_at,winner_team)
             VALUES (7,3,1,unixepoch()-600,0)""")
mid = c.lastrowid
c.execute("""INSERT INTO match_players(match_id,user_id,team,kills,deaths,headshots,exp_gain,gp_gain,result)
             VALUES (?,?,0,12,5,4,300,150,1)""", (mid, uid))
c.execute("""INSERT INTO match_players(match_id,user_id,team,kills,deaths,result)
             VALUES (?,?,1,5,12,0)""", (mid, uid2))
s = c.execute('SELECT play_count,wins,kills,headshots FROM user_stats WHERE user_id=?', (uid,)).fetchone()
assert s == (1, 1, 12, 4), s
gp = c.execute('SELECT game_point, exp FROM users WHERE user_id=?', (uid,)).fetchone()
assert gp == (150, 300), gp

# --- 7. 好友 / 訊息 (GL_FRIEND_ADD 429 / GL_MSG_ADD 419) ---
step('friends / messages (body ≤200)')
c.execute('INSERT INTO friends(user_id,friend_id,state) VALUES (?,?,1)', (uid, uid2))
c.execute("INSERT INTO messages(from_user_id,to_user_id,title,body) VALUES (?,?,'hi','見面打一場?')",
          (uid, uid2))
try:
    c.execute('INSERT INTO messages(from_user_id,to_user_id,title,body) VALUES (?,?,?,?)',
              (uid, uid2, 'x', 'A' * 201))
    raise AssertionError('body 201 字應該被 CHECK 擋下 (sub_55A630 緩衝 201)')
except sqlite3.IntegrityError:
    pass

# --- 8. 任務 (GQ_QUEST_ACCEPT 867) ---
step('quests')
c.execute("INSERT INTO quest_catalog(quest_id,name,quest_type,goal,reward_gp) VALUES (501,'首勝',1,1,100)")
c.execute('INSERT INTO user_quests(user_id,quest_id,state,progress) VALUES (?,501,1,1)', (uid,))

# --- 9. 公會 (GC_CLAN_CREATE 585) ---
step('clans')
c.execute("INSERT INTO clans(name,leader_id) VALUES ('PaperKnights',?)", (uid,))
cid = c.lastrowid
c.execute('INSERT INTO clan_members(clan_id,user_id,rank) VALUES (?,?,2)', (cid, uid))
c.execute('UPDATE users SET clan_id=? WHERE user_id=?', (cid, uid))

# --- 10. v_myinfo (GL_MYINFO_ACK 198 組包來源) ---
step('v_myinfo wire view')
row = c.execute('SELECT nickname,cash,clan_name,kills,wins FROM v_myinfo WHERE user_id=?',
                (uid,)).fetchone()
assert row == ('PaperAlice', 5000, 'PaperKnights', 12, 1), row

# --- 11. opcode 註冊表可查詢 ---
step('protocol_packets lookup')
assert c.execute("SELECT opcode FROM protocol_packets WHERE name='GL_MYINFO_ACK'").fetchone()[0] == 198
assert c.execute("SELECT name FROM protocol_packets WHERE opcode=682").fetchone()[0] == 'GL_LOGIN_REQ'

# --- 12. 倉庫 (GL_MYWAREHOUSE* 855-863) ---
step('warehouse lockers bootstrap + push/pop')
# 856 狀態塊資料: 補齊 6 頁籤 (INSERT OR IGNORE), 回 {tab, count, expires_at}
c.executemany('INSERT OR IGNORE INTO warehouse_lockers(user_id,tab,expires_at) VALUES (?,?,?)',
              [(uid, t, 4102444800) for t in range(1, 7)])
info = c.execute("""
    SELECT w.tab, COUNT(i.item_id),
           CASE WHEN w.expires_at <= 0 THEN 0 ELSE w.expires_at END
    FROM warehouse_lockers w
    LEFT JOIN warehouse_items i ON i.user_id = w.user_id AND i.tab = w.tab
    WHERE w.user_id = ? GROUP BY w.tab ORDER BY w.tab
""", (uid,)).fetchall()
assert len(info) == 6 and all(row[1] == 0 for row in info), info

# 859 push: 買入件入背包 → 搬進倉庫 tab1 (最小空 slot)
c.execute("INSERT INTO item_catalog(item_id,name,kind,price_gp,durability) VALUES (9001,'倉庫測試槍',3,1000,40)")
inv_slot = c.execute("""
    SELECT IFNULL(MIN(t.slot+1),0) FROM
      (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=?) t
    WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=?)
""", (uid, uid)).fetchone()[0]
c.execute("INSERT INTO inventory(user_id,slot,item_id,period_days,durability_cur,durability_max) VALUES (?,?,9001,7,40,40)", (uid, inv_slot))
wh_slot = c.execute("""
    SELECT IFNULL(MIN(t.slot+1),0) FROM
      (SELECT -1 AS slot UNION SELECT slot FROM warehouse_items WHERE user_id=? AND tab=1) t
    WHERE t.slot+1 NOT IN (SELECT slot FROM warehouse_items WHERE user_id=? AND tab=1)
""", (uid, uid)).fetchone()[0]
assert wh_slot == 0, wh_slot
c.execute('DELETE FROM inventory WHERE user_id=? AND slot=?', (uid, inv_slot))
c.execute("""INSERT INTO warehouse_items
    (user_id,tab,slot,item_id,stat_f1,stat_f2,period_days,expires_at,durability_cur,durability_max)
    VALUES (?,1,?,9001,0,0,7,NULL,40,40)""", (uid, wh_slot))
assert c.execute('SELECT COUNT(*) FROM warehouse_items WHERE user_id=? AND tab=1', (uid,)).fetchone()[0] == 1

# 861 pop: 倉庫搬回背包 (最小空 slot = 原背包 slot 已空出)
back = c.execute("""
    SELECT IFNULL(MIN(t.slot+1),0) FROM
      (SELECT -1 AS slot UNION SELECT slot FROM inventory WHERE user_id=?) t
    WHERE t.slot+1 NOT IN (SELECT slot FROM inventory WHERE user_id=?)
""", (uid, uid)).fetchone()[0]
assert back == inv_slot, (back, inv_slot)
c.execute('DELETE FROM warehouse_items WHERE user_id=? AND tab=1 AND slot=?', (uid, wh_slot))
c.execute("""INSERT INTO inventory
    (user_id,slot,item_id,stat_f1,stat_f2,period_days,expires_at,durability_cur,durability_max)
    VALUES (?,?,9001,0,0,7,NULL,40,40)""", (uid, back))
assert c.execute('SELECT COUNT(*) FROM warehouse_items WHERE user_id=? AND tab=1', (uid,)).fetchone()[0] == 0

# --- 13. 語音自訂 (791-796) ---
step('voice customize (791-796)')
c.execute("INSERT INTO voice_customize(user_id,char_idx,base_voice1,base_voice2) VALUES (?,0,1,2)", (uid,))
c.executemany("INSERT INTO voice_slots(user_id,char_idx,slot_no,item_id,flag) VALUES (?,0,?,?,?)",
              [(uid, s, 100 + s, 1 + (s % 9)) for s in range(27)])
assert c.execute("SELECT COUNT(*) FROM voice_slots WHERE user_id=? AND char_idx=0", (uid,)).fetchone()[0] == 27
row = c.execute("SELECT base_voice1, base_voice2 FROM voice_customize WHERE user_id=? AND char_idx=0", (uid,)).fetchone()
assert row == (1, 2), row

# 測試 constraints: char_idx 0..14, slot_no 0..26
try:
    c.execute("INSERT INTO voice_customize(user_id,char_idx) VALUES (?,15)", (uid,))
    assert False, "char_idx 15 should violate CHECK constraint"
except sqlite3.IntegrityError:
    pass

try:
    c.execute("INSERT INTO voice_slots(user_id,char_idx,slot_no) VALUES (?,0,27)", (uid,))
    assert False, "slot_no 27 should violate CHECK constraint"
except sqlite3.IntegrityError:
    pass

# --- 14. 系統 / 角色 / 商店 / 訊息 CRUD ---
step('tutorial, characters, messages, gifts CRUD')
c.execute("UPDATE users SET flags1=5 WHERE user_id=?", (uid,))
assert c.execute("SELECT flags1 FROM users WHERE user_id=?", (uid,)).fetchone()[0] == 5

c.execute("UPDATE users SET current_char=1 WHERE user_id=?", (uid,))
assert c.execute("SELECT current_char FROM users WHERE user_id=?", (uid,)).fetchone()[0] == 1

c.execute("INSERT INTO characters(user_id, slot_no, char_type) VALUES (?, 1, 2)", (uid,))
assert c.execute("SELECT char_type FROM characters WHERE user_id=? AND slot_no=1", (uid,)).fetchone()[0] == 2

c.execute("INSERT INTO skill_slots(user_id, slot_kind, idx, item_id) VALUES (?, 0, 1, 1001) ON CONFLICT(user_id, slot_kind, idx) DO UPDATE SET item_id=1001", (uid,))
assert c.execute("SELECT item_id FROM skill_slots WHERE user_id=? AND slot_kind=0 AND idx=1", (uid,)).fetchone()[0] == 1001

c.execute("UPDATE messages SET is_read=1 WHERE to_user_id=1000", ()) # test update query syntax

# --- 15. 遊戲中心 (GL_GAMECENTER_REC 472 / RANKING 480 / END 476) ---
step('gamecenter records & rankings (472-484)')
c.execute("""INSERT INTO gamecenter_records(user_id, game_no, high_score, coins, play_count, updated_at)
             VALUES (?, 1, 15000, 10, 5, unixepoch())""", (uid,))
c.execute("""INSERT INTO gamecenter_records(user_id, game_no, high_score, coins, play_count, updated_at)
             VALUES (?, 1, 28000, 20, 12, unixepoch())""", (uid2,))
gc_row = c.execute("SELECT high_score, play_count FROM gamecenter_records WHERE user_id=? AND game_no=1", (uid,)).fetchone()
assert gc_row == (15000, 5), gc_row

top_rank = c.execute("""
    SELECT u.nickname, r.high_score
    FROM gamecenter_records r
    JOIN users u ON u.user_id = r.user_id
    WHERE r.game_no = 1
    ORDER BY r.high_score DESC
    LIMIT 1
""").fetchone()
assert top_rank == ('PaperBob', 28000), top_rank

# --- 16. 安全與 GM 管理日誌 (security_events / server_config) ---
step('security events & GM server config (275-294, 822-831)')
c.execute("INSERT INTO security_events(user_id, event_type, detail) VALUES (?, 4, 'Chat ban 10 mins')", (uid2,))
sec_row = c.execute("SELECT event_type, detail FROM security_events WHERE user_id=?", (uid2,)).fetchone()
assert sec_row == (4, 'Chat ban 10 mins'), sec_row

c.execute("INSERT OR REPLACE INTO server_config(key, value) VALUES ('event_exp_rate', '200')")
assert c.execute("SELECT value FROM server_config WHERE key='event_exp_rate'").fetchone()[0] == '200'

con.commit()
fk = con.execute('PRAGMA foreign_key_check').fetchall()
assert not fk, fk
print('\n所有煙霧測試通過 ✔')
con.close()
