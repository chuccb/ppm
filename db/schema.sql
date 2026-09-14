-- ============================================================================
-- PaperMan 私服 — SQLite 伺服器端資料庫 (2026-09 重建)
-- 依據: PaperMan.exe.c (IDA/Hex-Rays 9.4 導出) 的封包協議逆向
--   * opcode 註冊表  : sub_9D2050  (674 packets, id 100..994)
--   * CClientData    : sub_523BF0 / sub_524010 / sub_524660 / sub_527550 / sub_527D00
--   * 背包           : sub_524B70  (5120 slots, 100/page)
--   * 商店           : sub_571910 / sub_570B00 (period 白名單, item kind)
--   * 房間           : sub_568CE0  (room 欄位)
--   * 好友/訊息      : sub_55AFC0 / sub_55A630
--   * 戰績           : GP_CH*C 家族 (sub_556730..)
--   * 任務           : sub_91CC70 (GQ_*)
--
-- 設計原則 (SQLite 最佳實務):
--   STRICT tables + WITHOUT ROWID where適用、外鍵 ON、CHECK 對應
--   反編譯中觀察到的硬上限、UTC epoch 秒存時間、觸發器維護 updated_at。
-- ============================================================================

PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous = NORMAL;

-- ----------------------------------------------------------------------------
-- 0. 協議參考表 — 由 sub_9D2050 抽出的完整 opcode 註冊表。
--    伺服器啟動時載入, 供日誌/監控/route table 使用。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS protocol_packets (
    opcode      INTEGER PRIMARY KEY CHECK (opcode BETWEEN 100 AND 65535),
    name        TEXT    NOT NULL UNIQUE,
    direction   TEXT    NOT NULL DEFAULT 'BOTH'
                CHECK (direction IN ('C2S','S2C','BOTH')),  -- _REQ=C2S, _ACK/_NOTIFY=S2C
    subsystem   TEXT    NOT NULL,                            -- GL/GR/GG/GS/GP/GI/GM/GC/GQ/GV/GX/PM/UDP/TCP/Y/MASTER/SECURITY/GT/GE
    notes       TEXT
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 1. 帳號 (GL_LOGIN_REQ 682: account + token + data revision + 安全狀態)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS accounts (
    account_id     INTEGER PRIMARY KEY,
    login_name     TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    -- 永不存明文：C# server 寫入 PBKDF2-SHA256（salt/iterations/hash）；
    -- 第一次成功登入時可安全升級舊版 SHA256(salt+password) row。
    pass_hash      TEXT    NOT NULL,
    pass_salt      TEXT    NOT NULL,
    client_data_revision INTEGER,           -- 682 u64 高 dword XOR 還原；來源為 datarevision.txt，非硬體 ID
    fingerprint_source INTEGER NOT NULL DEFAULT 0 CHECK (fingerprint_source IN (0,1,2)),
                                               -- 0=無來源, 1=first adapter MAC, 2=storage serial
    client_fingerprint BLOB CHECK (client_fingerprint IS NULL OR length(client_fingerprint) = 24),
                                               -- 682 raw24；serial 成功時最多 23 bytes + NUL，否則 MAC 前 6 bytes + 零尾端
    cash           INTEGER NOT NULL DEFAULT 0 CHECK (cash >= 0),        -- GS_CASH_ACK(357)
    is_gm          INTEGER NOT NULL DEFAULT 0 CHECK (is_gm IN (0,1)),   -- MASTER_* 權限
    is_banned      INTEGER NOT NULL DEFAULT 0 CHECK (is_banned IN (0,1)),
    ban_reason     TEXT,
    ban_until      INTEGER,                 -- epoch, NULL=永久或未封
    chat_ban_until INTEGER,                 -- MASTER_CHAT_BAN(822)
    created_at     INTEGER NOT NULL DEFAULT (unixepoch()),
    updated_at     INTEGER NOT NULL DEFAULT (unixepoch()),
    last_login_at  INTEGER,
    last_login_ip  TEXT
) STRICT;

-- ----------------------------------------------------------------------------
-- 2. 玩家 (user) — GL_MYINFO_ACK(198) 主體。1 帳號 1 玩家 (暱稱由 GM_CREATENICK 建)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS users (
    user_id        INTEGER PRIMARY KEY,                     -- sub_570550 的 s32 v19
    account_id     INTEGER NOT NULL UNIQUE REFERENCES accounts(account_id) ON DELETE CASCADE,
    nickname       TEXT    NOT NULL UNIQUE,                 -- GM_CHECKNICK(210)/CREATENICK(212)
    nick_reserved  TEXT,                                    -- GL_RESERVECHANGENICK(710)
    level          INTEGER NOT NULL DEFAULT 1  CHECK (level >= 1),
    exp            INTEGER NOT NULL DEFAULT 0  CHECK (exp >= 0),
    game_point     INTEGER NOT NULL DEFAULT 0  CHECK (game_point >= 0),  -- GP (sub_570550 v20)
    current_char   INTEGER NOT NULL DEFAULT 0  CHECK (current_char BETWEEN 0 AND 19),
    clan_id        INTEGER REFERENCES clans(clan_id) ON DELETE SET NULL,
    tutorial_flags BLOB,                                    -- GL_TUTORIALINDEX (≤20 bytes, sub_5A9B30)
    flags1         INTEGER NOT NULL DEFAULT 0,              -- CClientData +304
    flags2         INTEGER NOT NULL DEFAULT 0,              -- +305
    flags3         INTEGER NOT NULL DEFAULT 0,              -- +306
    extra_blob     BLOB,                                    -- +208 (0x30 bytes)
    created_at     INTEGER NOT NULL DEFAULT (unixepoch()),
    updated_at     INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT;
CREATE INDEX IF NOT EXISTS idx_users_clan ON users(clan_id) WHERE clan_id IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 3. 戰績計數器 — GP_CH*C 家族 (223..245, 363, 381..389, 882) 各自 +1/+n
--    與 user 1:1，拆表避免熱點寫入污染 users。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_stats (
    user_id     INTEGER PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    play_count  INTEGER NOT NULL DEFAULT 0,  -- GP_CHPLAYC   (222)
    round_count INTEGER NOT NULL DEFAULT 0,  -- GP_CHROUNDC  (224)
    disconnects INTEGER NOT NULL DEFAULT 0,  -- GP_CHDISC    (226)
    wins        INTEGER NOT NULL DEFAULT 0,  -- GP_CHWINC    (228)
    losses      INTEGER NOT NULL DEFAULT 0,  -- GP_CHLOSSC   (230)
    kills       INTEGER NOT NULL DEFAULT 0,  -- GP_CHKILLC   (232)
    deaths      INTEGER NOT NULL DEFAULT 0,  -- GP_CHDEADC   (234)
    headshots   INTEGER NOT NULL DEFAULT 0,  -- GP_CHHEADSC  (236)
    combos      INTEGER NOT NULL DEFAULT 0,  -- GP_CHACOMBOC (238)
    hearts      INTEGER NOT NULL DEFAULT 0,  -- GP_CHHEARTC  (240)
    double_kill INTEGER NOT NULL DEFAULT 0,  -- GP_CHDKILLC  (242)
    triple_kill INTEGER NOT NULL DEFAULT 0,  -- GP_CHTKILLC  (244)
    criticals   INTEGER NOT NULL DEFAULT 0,  -- GP_CHCRITICALC (362)
    multi_kill  INTEGER NOT NULL DEFAULT 0,  -- GP_CHMKILLC  (380)
    ultra_kill  INTEGER NOT NULL DEFAULT 0,  -- GP_CHUKILLC  (382)
    z_kill      INTEGER NOT NULL DEFAULT 0,  -- GP_CHZKILLC  (384)
    k_kill      INTEGER NOT NULL DEFAULT 0,  -- GP_CHKKILLC  (386)
    dd_kill     INTEGER NOT NULL DEFAULT 0,  -- GP_CHDDKILLC (388)
    play_time_s INTEGER NOT NULL DEFAULT 0   -- GP_CHPLAYTIMEC (882)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 4. 角色槽 — sub_524010: 最多 20 個, 每個 13 個 u16 (1 type + 12 裝備位)
-- ⚠ 裝備 u16 是「類別內偏移」not 完整 item_id (十二輪, getter 群
--   sub_525F10..526640 逐一定案): full_id = 類別基底 + (u16 % 100000)
--   wire[0]=19,900,000(套裝?) wire[1]=10,000,000 wire[2]=10,100,000 ...
--   wire[11]=11,000,000 (每槽 +100,000; 0 = 空)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS characters (
    user_id      INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    slot_no      INTEGER NOT NULL CHECK (slot_no BETWEEN 0 AND 19),
    char_type    INTEGER NOT NULL DEFAULT 0,     -- u8 角色種類 (GS_BUYCHAR 310)
    -- 12 個外觀欄位 (u16 = 類別內偏移, 0=空) — sub_524010 讀 12 個 u16
    -- 十八輪真值: [0]=角色(19.9M) [1]=髮型 [2]=臉 [3]=上衣 [4]=下裝
    -- [5]=鞋 [6]=外套 [7]=眼部 [8]=髮飾 [9]=臉飾 [10]=頭飾 [11]=特殊
    -- (欄名沿用舊稱, 對應順序如上; 武器另走武器編組表)
    eq_primary   INTEGER NOT NULL DEFAULT 0,
    eq_secondary INTEGER NOT NULL DEFAULT 0,
    eq_melee     INTEGER NOT NULL DEFAULT 0,
    eq_grenade   INTEGER NOT NULL DEFAULT 0,
    eq_head      INTEGER NOT NULL DEFAULT 0,
    eq_face      INTEGER NOT NULL DEFAULT 0,
    eq_upper     INTEGER NOT NULL DEFAULT 0,
    eq_lower     INTEGER NOT NULL DEFAULT 0,
    eq_hands     INTEGER NOT NULL DEFAULT 0,
    eq_back      INTEGER NOT NULL DEFAULT 0,
    eq_special   INTEGER NOT NULL DEFAULT 0,
    eq_set       INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, slot_no)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 5. 武器編組 — sub_524660: 4 組, 每組 u8 no + u16 equipped + 3×u16 + 8×s32 parts
--    (GI_CHANGEWP 220 / GL_WEAPONPARTS_EQUIP_CHANGE 912)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS weapon_groups (
    user_id   INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    group_no  INTEGER NOT NULL CHECK (group_no BETWEEN 0 AND 3),
    equipped  INTEGER NOT NULL DEFAULT 0,     -- u16 flag (144206)
    sub1      INTEGER NOT NULL DEFAULT 0,     -- u16 (144208)  group_no==3 時不用
    sub2      INTEGER NOT NULL DEFAULT 0,     -- u16 (144210)
    sub3      INTEGER NOT NULL DEFAULT 0,     -- u16 (144212)
    part0     INTEGER NOT NULL DEFAULT 0,     -- 8×s32 parts (144216..)
    part1     INTEGER NOT NULL DEFAULT 0,
    part2     INTEGER NOT NULL DEFAULT 0,
    part3     INTEGER NOT NULL DEFAULT 0,
    part4     INTEGER NOT NULL DEFAULT 0,
    part5     INTEGER NOT NULL DEFAULT 0,
    part6     INTEGER NOT NULL DEFAULT 0,
    part7     INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, group_no)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 6. 技能 / 快速槽 (卅六輪逐函數直查):
--    sub_527550 → sub_522480: 9×s32 技能槽 (無前導 count)
--    sub_527D00 → sub_527AF0: u8 n5 + 7×s32 (0x1C = 28B) 快速槽
--    (GI_CHANGE_SKILLITEMSLOT 466 / GL_COMBISKILLITEM 724)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS skill_slots (
    user_id   INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    slot_kind INTEGER NOT NULL CHECK (slot_kind IN (0,1)),  -- 0=skill(sub_527550, 9 槽) 1=quick(sub_527D00, 7 槽)
    idx       INTEGER NOT NULL,
    item_id   INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, slot_kind, idx),
    CHECK ((slot_kind = 0 AND idx BETWEEN 0 AND 8) OR        -- sub_522480 讀 9×s32
           (slot_kind = 1 AND idx BETWEEN 0 AND 6))          -- sub_527AF0 讀 7×s32 (0x1C)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 7. 道具目錄 (靜態資料, 由客戶端資料檔匯入) — sub_570B00 的 kind 白名單
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS item_catalog (
    item_id     INTEGER PRIMARY KEY,
    name        TEXT,
    kind        INTEGER NOT NULL CHECK (kind BETWEEN 0 AND 20),
       -- 0,1,3,14: 武器類(期限制) 2,4,9,15: 裝飾(永久) 5: 消耗(數量制)
       -- 10,11,16: 特殊(永久) 12,13,17: 可覆寫 (sub_524F70)
    price_gp    INTEGER NOT NULL DEFAULT 0 CHECK (price_gp   >= 0),
    price_cash  INTEGER NOT NULL DEFAULT 0 CHECK (price_cash >= 0),
    durability  INTEGER NOT NULL DEFAULT 0,   -- u16 出廠耐久
    is_hidden   INTEGER NOT NULL DEFAULT 0 CHECK (is_hidden IN (0,1)), -- GS_HIDDEN_ITEM_LIST(807)
    is_sellable INTEGER NOT NULL DEFAULT 1 CHECK (is_sellable IN (0,1))
) STRICT;

-- ----------------------------------------------------------------------------
-- 8. 背包 — sub_524B70: 5120 slots, 分頁 100/包
--    欄位對 GL_MYITEM_ACK(200): slot,s32 item,float f1,float f2,s32 period,u16 dura
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS inventory (
    user_id        INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    slot           INTEGER NOT NULL CHECK (slot BETWEEN 0 AND 5119),
    item_id        INTEGER NOT NULL REFERENCES item_catalog(item_id),
    stat_f1        REAL    NOT NULL DEFAULT 0,   -- float #1 (sub_592AC0)
    stat_f2        REAL    NOT NULL DEFAULT 0,   -- float #2
    period_days    INTEGER NOT NULL DEFAULT 0
                   CHECK (period_days IN (0,1,5,7,10,15,20,30,50,60,80,90,100)),
                   -- sub_570B00/sub_571100 白名單 (含消耗品數量 5/10/20/30/50/80/100)
    expires_at     INTEGER,                      -- epoch; NULL=永久 (period=0)
    durability_cur INTEGER NOT NULL DEFAULT 0 CHECK (durability_cur BETWEEN 0 AND 65535),
    durability_max INTEGER NOT NULL DEFAULT 0 CHECK (durability_max BETWEEN 0 AND 65535),
    acquired_at    INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, slot)
) STRICT, WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_inventory_item ON inventory(item_id);
CREATE INDEX IF NOT EXISTS idx_inventory_expiry ON inventory(expires_at)
    WHERE expires_at IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 9. 倉庫 — GL_MYWAREHOUSE*(855..863)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS warehouse (
    user_id     INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    slot        INTEGER NOT NULL CHECK (slot >= 0),
    item_id     INTEGER NOT NULL REFERENCES item_catalog(item_id),
    stat_f1     REAL    NOT NULL DEFAULT 0,
    stat_f2     REAL    NOT NULL DEFAULT 0,
    period_days INTEGER NOT NULL DEFAULT 0,
    expires_at  INTEGER,
    stored_at   INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, slot)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 10. 禮物 — GS_GIVEGIFT(296)/TAKEGIFT(298)/MOVEGIFT(300)/NEWGIFT(451)/DELETEGIFT(453)
--     sub_579830: from,to + u8×4
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gifts (
    gift_id      INTEGER PRIMARY KEY,
    from_user_id INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
    to_user_id   INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    item_id      INTEGER NOT NULL REFERENCES item_catalog(item_id),
    period_days  INTEGER NOT NULL DEFAULT 0,
    message      TEXT,
    state        INTEGER NOT NULL DEFAULT 0 CHECK (state IN (0,1,2)), -- 0=pending 1=claimed 2=deleted
    sent_at      INTEGER NOT NULL DEFAULT (unixepoch()),
    claimed_at   INTEGER
) STRICT;
CREATE INDEX IF NOT EXISTS idx_gifts_to ON gifts(to_user_id, state);

-- ----------------------------------------------------------------------------
-- 11. 好友 — GL_FRIEND_ADD(429)/DEL(431)/LIST(433)/INFO(435)/CHAT(439)/WHERE(441)
--     sub_55AFC0: string nick + s32 status
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS friends (
    user_id    INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    friend_id  INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    state      INTEGER NOT NULL DEFAULT 0 CHECK (state IN (0,1,2)), -- 0=requested 1=accepted 2=blocked
    created_at INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, friend_id),
    CHECK (user_id <> friend_id)
) STRICT, WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_friends_reverse ON friends(friend_id);

-- ----------------------------------------------------------------------------
-- 12. 站內訊息 — GL_MSG_ADD(419)/DEL(421)/READ(423)/RECVLIST(425)/SENDLIST(427)
--     sub_55A630: from,u8,title,u32 id,body(≤200),attach,u16 date
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS messages (
    msg_id       INTEGER PRIMARY KEY,
    from_user_id INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
    to_user_id   INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    title        TEXT    NOT NULL DEFAULT '',
    body         TEXT    NOT NULL DEFAULT '' CHECK (length(body) <= 200), -- v36[201] 上限
    msg_kind     INTEGER NOT NULL DEFAULT 0,      -- u8 (sub_55A630 v34)
    is_read      INTEGER NOT NULL DEFAULT 0 CHECK (is_read IN (0,1)),
    deleted_by_to   INTEGER NOT NULL DEFAULT 0 CHECK (deleted_by_to IN (0,1)),
    deleted_by_from INTEGER NOT NULL DEFAULT 0 CHECK (deleted_by_from IN (0,1)),
    sent_at      INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT;
CREATE INDEX IF NOT EXISTS idx_messages_to   ON messages(to_user_id, is_read) WHERE deleted_by_to = 0;
CREATE INDEX IF NOT EXISTS idx_messages_from ON messages(from_user_id)        WHERE deleted_by_from = 0;

-- ----------------------------------------------------------------------------
-- 13. 公會 — GC_CLAN_*(580..587) / 排名(890) / 錦標賽 TNMT(756..779)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS clans (
    clan_id     INTEGER PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE,
    leader_id   INTEGER,                                    -- FK 後補 (循環參照)
    emblem_id   INTEGER NOT NULL DEFAULT 0,
    score       INTEGER NOT NULL DEFAULT 0,                 -- GC_QUERY_CLANRANKING(890)
    wins        INTEGER NOT NULL DEFAULT 0,
    losses      INTEGER NOT NULL DEFAULT 0,
    notice      TEXT,
    created_at  INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT;
CREATE INDEX IF NOT EXISTS idx_clans_score ON clans(score DESC);

CREATE TABLE IF NOT EXISTS clan_members (
    clan_id    INTEGER NOT NULL REFERENCES clans(clan_id) ON DELETE CASCADE,
    user_id    INTEGER NOT NULL UNIQUE REFERENCES users(user_id) ON DELETE CASCADE,
    rank       INTEGER NOT NULL DEFAULT 0 CHECK (rank IN (0,1,2)),  -- 0=member 1=officer 2=leader
    joined_at  INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (clan_id, user_id)
) STRICT, WITHOUT ROWID;

-- 錦標賽報名 — GL_CLAN_TNMT_RECEIPT(756)/CANCEL(758)/ALL_INFO(771)
CREATE TABLE IF NOT EXISTS clan_tournaments (
    tnmt_id    INTEGER PRIMARY KEY,
    name       TEXT    NOT NULL,
    state      INTEGER NOT NULL DEFAULT 0,   -- GL_CLAN_TNMT_CURRENT_STATE(762) 的狀態機
    round_no   INTEGER NOT NULL DEFAULT 0,
    starts_at  INTEGER,
    ends_at    INTEGER
) STRICT;

CREATE TABLE IF NOT EXISTS clan_tournament_entries (
    tnmt_id    INTEGER NOT NULL REFERENCES clan_tournaments(tnmt_id) ON DELETE CASCADE,
    clan_id    INTEGER NOT NULL REFERENCES clans(clan_id) ON DELETE CASCADE,
    state      INTEGER NOT NULL DEFAULT 0,   -- 0=receipt 1=cancelled 2=eliminated 3=winner
    entered_at INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (tnmt_id, clan_id)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 14. 任務 — GQ_QUEST_*(866..881): list/accept/cancel/success/complete/daily/honor
--     sub_91CC70: accept 回 13-byte {s32 quest_id, s32 progress, u8 state, s32 t}
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS quest_catalog (
    quest_id    INTEGER PRIMARY KEY,
    name        TEXT,
    quest_type  INTEGER NOT NULL DEFAULT 0,   -- questindextype (sub_91CC70 v15)
    is_daily    INTEGER NOT NULL DEFAULT 0 CHECK (is_daily IN (0,1)),  -- GQ_..._DAILY(876)
    goal        INTEGER NOT NULL DEFAULT 1,
    reward_item INTEGER,
    reward_gp   INTEGER NOT NULL DEFAULT 0
) STRICT;

CREATE TABLE IF NOT EXISTS user_quests (
    user_id     INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    quest_id    INTEGER NOT NULL REFERENCES quest_catalog(quest_id),
    state       INTEGER NOT NULL DEFAULT 0
                CHECK (state IN (0,1,2,3,4)), -- 0=accepted 1=in-progress 2=success 3=completed(領獎) 4=cancelled
    progress    INTEGER NOT NULL DEFAULT 0,
    accepted_at INTEGER NOT NULL DEFAULT (unixepoch()),
    updated_at  INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, quest_id)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 15. 房間 (執行期狀態; 伺服器重啟可清空) — sub_568CE0 欄位
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS rooms (
    room_no     INTEGER PRIMARY KEY CHECK (room_no BETWEEN 0 AND 209),  -- n0xD2=210 上限
    title       TEXT    NOT NULL,
    state       INTEGER NOT NULL DEFAULT 0,   -- s8: 0=waiting 1=playing ...
    map_id      INTEGER NOT NULL DEFAULT 0,
    rule        INTEGER NOT NULL DEFAULT 0,   -- GR_RULECHANGE(169)
    win_count   INTEGER NOT NULL DEFAULT 0,   -- GR_WINCHANGE(171)
    time_limit  INTEGER NOT NULL DEFAULT 0,   -- GR_TIMECHANGE(173)
    max_players INTEGER NOT NULL DEFAULT 8 CHECK (max_players BETWEEN 1 AND 10),
    password    TEXT,                          -- GL_ENTERROOMPASS(216); NULL=無鎖
    item_mode   INTEGER NOT NULL DEFAULT 0 CHECK (item_mode  IN (0,1)),  -- GR_ITEMCHANGE(175)
    balance     INTEGER NOT NULL DEFAULT 0 CHECK (balance    IN (0,1)),  -- GR_BALANCECHANGE(364)
    team_shuffle INTEGER NOT NULL DEFAULT 0 CHECK (team_shuffle IN (0,1)),-- (368/894)
    skill_off   INTEGER NOT NULL DEFAULT 0 CHECK (skill_off  IN (0,1)),  -- GR_NOSKILL(712)
    observer    INTEGER NOT NULL DEFAULT 0 CHECK (observer   IN (0,1)),  -- GL_ENTERROOMOB(256)
    kill_limit  INTEGER NOT NULL DEFAULT 0,   -- GR_KILLCHANGE(340)/GL_LEVEL_KILL_LIMIT(704)
    master_id   INTEGER REFERENCES users(user_id) ON DELETE SET NULL,    -- GR_CHANGEMASTER(189)
    channel_id  INTEGER NOT NULL DEFAULT 0,   -- GC_CHANNEL(193)
    created_at  INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT, WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS room_slots (
    room_no   INTEGER NOT NULL REFERENCES rooms(room_no) ON DELETE CASCADE,
    slot_no   INTEGER NOT NULL CHECK (slot_no BETWEEN 0 AND 9),
    user_id   INTEGER UNIQUE REFERENCES users(user_id) ON DELETE SET NULL,
    team      INTEGER NOT NULL DEFAULT 0,     -- GR_CHANGESLOT(135)
    is_ready  INTEGER NOT NULL DEFAULT 0 CHECK (is_ready IN (0,1)),  -- GR_READY(127)
    is_observer INTEGER NOT NULL DEFAULT 0 CHECK (is_observer IN (0,1)),
    PRIMARY KEY (room_no, slot_no)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 16. 對戰記錄 (GR_END 133 / GG_JJGAMEEND 308 結算落地)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS match_results (
    match_id    INTEGER PRIMARY KEY,
    room_no     INTEGER NOT NULL,
    map_id      INTEGER NOT NULL DEFAULT 0,
    rule        INTEGER NOT NULL DEFAULT 0,
    started_at  INTEGER NOT NULL,
    ended_at    INTEGER NOT NULL DEFAULT (unixepoch()),
    winner_team INTEGER
) STRICT;

CREATE TABLE IF NOT EXISTS match_players (
    match_id  INTEGER NOT NULL REFERENCES match_results(match_id) ON DELETE CASCADE,
    user_id   INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    team      INTEGER NOT NULL DEFAULT 0,
    kills     INTEGER NOT NULL DEFAULT 0,
    deaths    INTEGER NOT NULL DEFAULT 0,
    headshots INTEGER NOT NULL DEFAULT 0,
    assists   INTEGER NOT NULL DEFAULT 0,   -- GG_ASSISTPOINT_NOTIFY(994)
    exp_gain  INTEGER NOT NULL DEFAULT 0,
    gp_gain   INTEGER NOT NULL DEFAULT 0,
    result    INTEGER NOT NULL DEFAULT 0 CHECK (result IN (0,1,2,3)), -- 0=loss 1=win 2=draw 3=disconnect
    PRIMARY KEY (match_id, user_id)
) STRICT, WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS idx_match_players_user ON match_players(user_id);

-- ----------------------------------------------------------------------------
-- 17. 商城交易流水 — GS_BUYITEM(204)/BUYCASHITEM(358)/BUY_ONCEITEM(695)/
--     SELLITEM(208)/DESTROYITEM(802)/BUYCHAR(310)/CAPSULE(900)/HUKUBUKURO(468)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS shop_transactions (
    tx_id       INTEGER PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    tx_type     INTEGER NOT NULL CHECK (tx_type BETWEEN 0 AND 9),
        -- 0=buy_gp 1=buy_cash 2=sell 3=destroy 4=gift_send 5=gift_claim
        -- 6=capsule 7=hukubukuro 8=buy_char 9=code_gift(461)
    item_id     INTEGER,
    period_days INTEGER NOT NULL DEFAULT 0,
    gp_delta    INTEGER NOT NULL DEFAULT 0,
    cash_delta  INTEGER NOT NULL DEFAULT 0,
    created_at  INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT;
CREATE INDEX IF NOT EXISTS idx_shop_tx_user ON shop_transactions(user_id, created_at);

-- ----------------------------------------------------------------------------
-- 18. 聲音自訂槽 — GL_VOICEITEMSLOT(791)/GI_VOICEITEMSLOT_ALL(793)/CHANGE(795)
--     (CVCustomizeManager::SendPacketMyVoiceCustomize / ...All / ...Change)
--
--   語音 char_idx = 0..14 (0=maru…14=devilgirl, sub_8859B0 名字表;
--   character/models/type1..15 = idx+1)。wire 語音塊:
--     s16 base_voice1, s16 base_voice2,
--     3 類 (command/tactics/infomation) × 9 句 × {s16 item, u8 flag}
--     → slot_no 0..26 (i*9+j), item = voice_item (語音表偏移), flag 原樣儲存。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS voice_customize (
    user_id     INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    char_idx    INTEGER NOT NULL CHECK (char_idx BETWEEN 0 AND 14),
    base_voice1 INTEGER NOT NULL DEFAULT 0,
    base_voice2 INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, char_idx)
) STRICT, WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS voice_slots (
    user_id  INTEGER NOT NULL,
    char_idx INTEGER NOT NULL CHECK (char_idx BETWEEN 0 AND 14),
    slot_no  INTEGER NOT NULL CHECK (slot_no BETWEEN 0 AND 26),
    item_id  INTEGER NOT NULL DEFAULT 0,
    flag     INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, char_idx, slot_no),
    FOREIGN KEY (user_id, char_idx)
        REFERENCES voice_customize(user_id, char_idx) ON DELETE CASCADE
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 18b. 角色倉庫 — GL_MYWAREHOUSE* (855-863), n11==19 倉庫場景
--   7 頁籤 0..6 (tab0 不用); 容量 sub_4F9B10: tab1=100, tab2/3/4=300, tab5/6=3000。
--   物品 28B 條目與背包 inventory 同構 (sub_524F70/sub_4FC540);
--   856/863 狀態塊 = 7 × 10B {s32 count, s32 到期(位元打包日期), u8 loaded, u8 pad}。
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS warehouse_items (
    user_id        INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    tab            INTEGER NOT NULL CHECK (tab BETWEEN 1 AND 6),
    slot           INTEGER NOT NULL CHECK (slot >= 0),      -- 倉庫頁籤內 slot
    item_id        INTEGER NOT NULL REFERENCES item_catalog(item_id),
    stat_f1        REAL    NOT NULL DEFAULT 0,              -- f32 #1 (同背包)
    stat_f2        REAL    NOT NULL DEFAULT 0,              -- f32 #2
    period_days    INTEGER NOT NULL DEFAULT 0,
    expires_at     INTEGER,                                 -- epoch; NULL=永久
    durability_cur INTEGER NOT NULL DEFAULT 0 CHECK (durability_cur BETWEEN 0 AND 65535),
    durability_max INTEGER NOT NULL DEFAULT 0 CHECK (durability_max BETWEEN 0 AND 65535),
    acquired_at    INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, tab, slot)
) STRICT, WITHOUT ROWID;

-- 頁籤租期 (expires_at epoch; 0/<=0 = 未持有 — client sub_4FA950 視為空)
CREATE TABLE IF NOT EXISTS warehouse_lockers (
    user_id    INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    tab        INTEGER NOT NULL CHECK (tab BETWEEN 1 AND 6),
    expires_at INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, tab)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 19. 遊戲中心 — GL_GAMECENTER_REC(472)/RANKING(480)/COIN(482)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS gamecenter_records (
    user_id    INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    game_no    INTEGER NOT NULL,
    high_score INTEGER NOT NULL DEFAULT 0,
    coins      INTEGER NOT NULL DEFAULT 0,
    play_count INTEGER NOT NULL DEFAULT 0,
    updated_at INTEGER NOT NULL DEFAULT (unixepoch()),
    PRIMARY KEY (user_id, game_no)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 20. 安全 / 稽核 — GG_CHEATER_REPORT(697)/GX_XIGNCODE(887)/GV_HACKBLOCK(567)/
--     MASTER_CHAT_BAN(822)/GG_CHATTING_PENALTY(820)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS security_events (
    event_id   INTEGER PRIMARY KEY,
    user_id    INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
    event_type INTEGER NOT NULL,
        -- 1=cheat_report 2=xigncode_ban 3=hack_block 4=chat_penalty
        -- 5=invalid_wpdata(714) 6=bomb_cheater(814) 7=critical_error(799)
    detail     TEXT,
    created_at INTEGER NOT NULL DEFAULT (unixepoch())
) STRICT;
CREATE INDEX IF NOT EXISTS idx_security_user ON security_events(user_id, created_at);

-- 封包統計 (營運監控, 對應 CPacketViewer)
CREATE TABLE IF NOT EXISTS packet_stats (
    day        TEXT    NOT NULL,               -- 'YYYY-MM-DD'
    opcode     INTEGER NOT NULL REFERENCES protocol_packets(opcode),
    rx_count   INTEGER NOT NULL DEFAULT 0,
    tx_count   INTEGER NOT NULL DEFAULT 0,
    rx_bytes   INTEGER NOT NULL DEFAULT 0,
    tx_bytes   INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (day, opcode)
) STRICT, WITHOUT ROWID;

-- ----------------------------------------------------------------------------
-- 21. 伺服器設定 — MASTER_MSET(285)/EVENTEXP(404)/EVENTPAGE(402)/
--     DISABLE_LOGIN(407)/XTRAP_RELOAD(800) 等運維開關
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS server_config (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
) STRICT, WITHOUT ROWID;

-- 公告排程 — GL_SCHEDULED_GM_NOTICE_NOTIFY(911)
CREATE TABLE IF NOT EXISTS gm_notices (
    notice_id  INTEGER PRIMARY KEY,
    body       TEXT    NOT NULL,
    starts_at  INTEGER NOT NULL,
    ends_at    INTEGER,
    repeat_sec INTEGER NOT NULL DEFAULT 0
) STRICT;

-- ============================================================================
-- 觸發器
-- ============================================================================
CREATE TRIGGER IF NOT EXISTS trg_users_touch
AFTER UPDATE ON users FOR EACH ROW
BEGIN
    UPDATE users SET updated_at = unixepoch() WHERE user_id = NEW.user_id;
END;

CREATE TRIGGER IF NOT EXISTS trg_accounts_touch
AFTER UPDATE OF pass_hash, pass_salt, client_data_revision, fingerprint_source,
                client_fingerprint, cash, is_banned, chat_ban_until ON accounts FOR EACH ROW
BEGIN
    UPDATE accounts SET updated_at = unixepoch() WHERE account_id = NEW.account_id;
END;

-- 建立 user 時自動配好 stats / 4 個武器編組
CREATE TRIGGER IF NOT EXISTS trg_users_bootstrap
AFTER INSERT ON users FOR EACH ROW
BEGIN
    INSERT INTO user_stats(user_id) VALUES (NEW.user_id);
    INSERT INTO weapon_groups(user_id, group_no) VALUES
        (NEW.user_id,0),(NEW.user_id,1),(NEW.user_id,2),(NEW.user_id,3);
END;

-- 對戰結束時把 per-match 數據累加進 user_stats (對應 GP_CH*C ACK 推播來源)
CREATE TRIGGER IF NOT EXISTS trg_match_rollup
AFTER INSERT ON match_players FOR EACH ROW
BEGIN
    UPDATE user_stats SET
        play_count  = play_count + 1,
        kills       = kills     + NEW.kills,
        deaths      = deaths    + NEW.deaths,
        headshots   = headshots + NEW.headshots,
        wins        = wins      + (NEW.result = 1),
        losses      = losses    + (NEW.result = 0),
        disconnects = disconnects + (NEW.result = 3)
    WHERE user_id = NEW.user_id;
    UPDATE users SET
        exp        = exp + NEW.exp_gain,
        game_point = game_point + NEW.gp_gain
    WHERE user_id = NEW.user_id;
END;

-- ============================================================================
-- 常用視圖
-- ============================================================================
-- GL_MYINFO_ACK(198) 一次取齊
CREATE VIEW IF NOT EXISTS v_myinfo AS
SELECT u.user_id, u.nickname, u.level, u.exp, u.game_point, u.current_char,
       a.cash, u.clan_id, c.name AS clan_name,
       s.play_count, s.wins, s.losses, s.kills, s.deaths, s.headshots,
       s.combos, s.hearts, s.double_kill, s.triple_kill, s.criticals,
       s.multi_kill, s.ultra_kill, s.z_kill, s.k_kill, s.dd_kill,
       s.round_count, s.disconnects, s.play_time_s
FROM users u
JOIN accounts   a ON a.account_id = u.account_id
JOIN user_stats s ON s.user_id    = u.user_id
LEFT JOIN clans c ON c.clan_id    = u.clan_id;

-- GL_MYITEM_ACK(200) 分頁查詢底層
CREATE VIEW IF NOT EXISTS v_inventory_wire AS
SELECT user_id, slot, item_id, stat_f1, stat_f2,
       CASE WHEN expires_at IS NULL THEN 0
            ELSE MAX(0, CAST((expires_at - unixepoch()) / 86400 AS INTEGER)) END AS period_days_left,
       durability_cur, durability_max
FROM inventory;

-- 排行 (MASTER/GC_QUERY_CLANRANKING)
CREATE VIEW IF NOT EXISTS v_user_ranking AS
SELECT u.user_id, u.nickname, u.level, u.exp, s.kills, s.deaths, s.wins, s.losses,
       CAST(s.kills AS REAL) / MAX(1, s.deaths) AS kd
FROM users u JOIN user_stats s ON s.user_id = u.user_id
ORDER BY u.exp DESC;
