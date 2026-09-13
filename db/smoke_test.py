#!/usr/bin/env python3
"""
煙霧測試: 模擬伺服器處理封包時對 DB 的完整讀寫路徑。
對應封包流程:
  GL_LOGIN_REQ(682) -> GM_CREATENICK(212) -> GL_MYINFO_ACK(198)
  -> GS_BUYITEM(204) -> GL_MYITEM_ACK(200 分頁) -> GI_CHANGEWP(220)
  -> GL_MAKEROOM(111) -> GR_END(133 結算) -> GP_CH*C 累計
  -> GL_FRIEND_ADD(429) -> GL_MSG_ADD(419) -> GQ_QUEST_ACCEPT(867)
"""
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'paperman.db')

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

c.execute("INSERT INTO accounts(login_name,pass_hash,pass_salt) VALUES ('bob','h','s')")
c.execute("INSERT INTO users(account_id,nickname) VALUES (?, 'PaperBob')", (c.lastrowid,))
uid2 = c.lastrowid

# --- 2. 角色槽 (GM_CREATECHAR 214, ≤20) ---
step('characters (slot 0..19 bound)')
c.execute('INSERT INTO characters(user_id,slot_no,char_type) VALUES (?,0,1)', (uid,))
try:
    c.execute('INSERT INTO characters(user_id,slot_no,char_type) VALUES (?,20,1)', (uid,))
    raise AssertionError('slot 20 應該被 CHECK 擋下')
except sqlite3.IntegrityError:
    pass

# --- 3. 商店購買 -> 背包 (GS_BUYITEM 204 -> GL_MYITEM_ACK 200) ---
step('item_catalog / inventory / period 白名單')
# 真實 id 空間 (十二輪定案): 可購武器段 A = 15,301,001..15,302,000;
# 裝飾類 full_id = 類別基底 + u16 偏移 (見 docs/PACKETS.md §3.15pre1)
c.execute("INSERT INTO item_catalog(item_id,name,kind,price_gp,durability) VALUES (15301001,'AK Paper',0,800,100)")
c.execute("INSERT INTO item_catalog(item_id,name,kind,price_gp) VALUES (10400001,'Red Cap',2,300)")
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
step('weapon_groups update')
c.execute('UPDATE weapon_groups SET equipped=1, part0=15301001 WHERE user_id=? AND group_no=0', (uid,))

# --- 5. 房間 (GL_MAKEROOM 111 / GR_CHANGESLOT 135) ---
step('rooms / room_slots')
c.execute("""INSERT INTO rooms(room_no,title,map_id,rule,win_count,max_players,master_id)
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
c.execute("""INSERT INTO match_results(room_no,map_id,rule,started_at,winner_team)
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

con.commit()
fk = con.execute('PRAGMA foreign_key_check').fetchall()
assert not fk, fk
print('\n所有煙霧測試通過 ✔')
con.close()
