# PaperMan 網路協議完整分析 (根據 PaperMan.exe.c IDA 導出)

本文件是重建伺服器端的協議基礎。所有結論都直接取自反編譯代碼，
每一節都附上來源函數 (sub_XXXXXX) 供覆核。

---

## 1. Packet 類 (0x591AC0 系列, vftable `Packet::vftable` @ 0xAEE4F8)

### 1.1 記憶體佈局 (來源: `sub_591DA0` = Packet::Init)

| 偏移 | 大小 | 含義 |
|--------|------|------|
| +0 | 4 | vtable |
| +4 | 1 | `sealed` 旗標 (sub_5923D0 設 1) |
| +8 | 4 | 指向 +24 (header word0 = **payload size**) |
| +12 | 4 | 指向 +26 (header word1 = **opcode / packet type**) |
| +16 | 4 | 指向 +28 (header word2 = **checksum**) |
| +20 | 4 | 指向 +30 (header word3 = **加密前大小**) |
| +24 | 8 | **wire header** (直接送上網路) |
| +32 | ≤9592 | payload 緩衝 (總 buffer 9600) |
| +9625 | 9600 | 第二緩衝 (recv 重組用) |
| +19232 | 4 | read base = this+32 |
| +19236 | 4 | read cursor |
| +19240 | 4 | write cursor |
| +19244 | 4 | end limit = this+9624 |
| +19248 | 4 | total size (= payload+8) |
| +19252 | 1 | 狀態旗標: bit0=已壓縮, bit2=已加密 |
| +19256 | 4 | 其他 |

### 1.2 Wire 格式

```
offset 0   u16  payload_size          (sub_591F00 讀 / sub_591F20 寫)
offset 2   u16  opcode                (sub_591EC0 寫 / sub_591EE0 讀 — dispatcher 用它 switch)
offset 4   u16  checksum              (sub_592390 / sub_5923B0)
offset 6   u16  原始(加密前)大小       (sub_591F90 / sub_591F70)
offset 8   ...  payload (小端, 緊湊, 無對齊)
```

傳送: `sub_555090` → `WSASend(buf = this+24, len = payload_size + 8)`。
接收: `sub_555280` 依 header 的 size+8 重組 stream。
建構: `Packet::possible_ctor_or_dtor_0(this, opcode)` (0x591B40) — 即
`Packet(opcode)`；副本建構 0x591BD0；解構 0x591D00。

### 1.3 序列化原語 (寫入=sub_592580 memcpy+advance, 讀出=sub_592500)

| 函數 | 型別 | 大小 |
|------|------|------|
| sub_592920 / sub_592940 | u8 (write/read) | 1 |
| sub_5928E0 / sub_592900 | s8 / bool | 1 |
| sub_5929A0,sub_5929E0 / sub_592A00,sub_5929C0 | u16/s16 | 2 |
| sub_592A20,sub_592A60,sub_592B20 / sub_592A40,sub_592AC0,sub_592A80 | s32/u32/float | 4 |
| sub_592AE0 / sub_592B00 | u64 | 8 |
| sub_5926F0 / sub_592730 | ANSI 字串 (lstrlenA+1, 含 NUL) | 變長 |
| sub_592770 / sub_5927B0 | UTF-16 字串 (2*len+2) | 變長 |
| sub_5927F0 / sub_592850 | 內嵌整個 Packet (u16 opcode + u32 size + bytes) | 變長 |

**字串一律以 NUL 結尾直接寫進 payload，沒有長度前綴** (讀出端靠 lstrlenA)。

### 1.4 傳送/接收管線 — ⚠ 第二輪逆向的重要修正

**修正: `sub_5923D0` / `sub_592420` (popcount checksum + XOR, 即第一輪文檔的
「seal」) 在整個 binary 裡沒有任何呼叫者 — 是死碼。** 真實管線只有兩層:

**送出 (sub_555090 → sub_593280):**
```
1. 若 word3==0: word3 := word0 (原始 payload 大小)     (sub_591F90)
2. 若 n0x2580>0 且 word0 ≥ n0x2580 → LZ 壓縮:
     sub_592CE0 → sub_592D30 → sub_591600
     word2 := 壓縮前大小, word0 := 壓縮後大小, flag|=1
     (壓不小就放棄, 不設 flag)
3. 一律 AES 加密: sub_592F60 → sub_592FB0
     n16 = 16-byte 對齊上取 (空 payload 也補一個 block)
     sub_4042A0(key_schedule, buf, n16, n2_4)   n2_4=1 CBC-加密 / 2 CBC-XOR先 / 其他 ECB
     word2 := 加密前 word0, word0 := n16, flag|=4
4. WSASend(this+24, word0 + 8)
```

**接收 (sub_555280 / sub_554E00 event loop):**
```
1. 累積 stream 到 9600-byte buffer, 依 word0+8 切 frame (sub_591FB0)
2. 合法性: sub_591D50 (total≥8 且 total ≥ word0+8)
3. AES 解密: sub_5930C0 → sub_593110 → sub_404470(key, buf, n16, n2_4)
     驗證: word0 必須 16 對齊且 == align16(word2), 否則丟包
     word0 := word2 (還原大小), flag|=8
4. 若 word3 > word0 且 word3 ≥ n0x2580 → LZ 解壓:
     sub_592E50 判斷 → sub_592E00 → sub_592E90 → sub_591900
     驗證: 解壓後大小必須 == word3, 否則丟包
5. dispatch 到 handler (vtable+4 虛呼叫)
```

**AES 細節 (sub_403430 = key schedule 初始化):**
- 全域常數: `n16_0=16` (block), `n10=10` (rounds) → **AES-128**
- 金鑰: `unk_B69E88` 起 16 bytes **硬編碼在 .data 段** (Rijndael key
  expansion + T-table `dword_B69208/B69608/B69A08/B68E08`, SBox `byte_B66C08`)
- `sub_403DE0`/`sub_403650` = 單 block 加密, `sub_404040` = 單 block 解密
- 模式由全域 `n2_4` 決定: 1/2 = 兩種 CBC 變體 (IV=0, XOR 前/後), 其他 = ECB
- **注意**: 封包路徑上 `n2_4` 未見初始化 (BSS 預設 0) → 實際運行為 **ECB 模式**;
  出現在別處的 `n2_4=1/2` 賦值屬於 UI 狀態機變數重名, 與加密無關
- 金鑰 16 bytes 需從 exe .data 段 0xB69E88 抽出 (`.c` 導出檔沒帶資料段內容)

**壓縮門檻協商**: 全域 `n0x2580` 初始 0x2580(9600, 即「從不壓縮」)。
`GL_ACCOUNTCONNSUCC(694)` ACK 攜帶一個 u16, 若 <0x2580 則更新門檻
(見 0x43E651 附近 `n694==694` 分支) — 即 **由伺服器決定是否啟用壓縮**。
私服最簡策略: 不送 694 的門檻欄位 / 送 0x2580 → 完全停用壓縮層。

**GL_LOGIN_ACK(681) 完整結構** (0x43E651 同函數 `n694==681` 分支):
```
s32  result           1=成功, 2=帳密錯, 0xC8..0xD6=各種封鎖/維護錯誤碼
若 result==1:
  s32  user_no        (dword_EE8970)
  s32  n100           (伺服器等級參數, ==100 時檢查特殊 UI)
  s32  ext_count      >0 時: s32 a, s32 b, u8 c → sub_A1C870(頻道保留資訊)
  s16  server_count
  repeat server_count:                 ← 伺服器清單
    s16  server_id
    str  name  (ANSI)
    str  host? (v124, 16B buffer)
    s16  port?
    u8   flag
    s16  group
    repeat 3:                          ← 每台 3 個頻道分組
      s16  ch_count (>0 才有後續)
      u8   ch_type
      str  ch_name
      s16  ch_port
      u8   ch_flag
      若 ch_type==3: u8 extra
  u32  x2 (v142,v137 → 帳號計費/會員資訊)
```

---

## 2. Opcode 註冊表 — sub_9D2050

`sub_9D2050` 用 `sub_9EAF50(name, id, ...)` 把 **674 個** packet 名稱註冊進
全域 map `dword_2317F50` (packet-viewer / debug 名稱表)。ID 即 wire opcode。
完整清單見 `db/packets.tsv` (670 個唯一 ID, 100–994；GS_BASE=100)。

命名規約:
| 前綴 | 子系統 | 數量 |
|------|--------|------|
| GT_ | Transport (ping/錯誤回報) | 4 |
| GE_ | 登出 | 2 |
| GL_ | Lobby (大廳/帳號/好友/訊息/倉庫/商城入口) | 159 |
| GR_ | Game Room (房間管理/投票/AI 波次) | 105 |
| GG_ | In-Game (遊戲進行: 佔領/足球/爆破/道具掉落) | 131 |
| GS_ | Shop (購買/禮物/扭蛋/福袋) | 48 |
| GP_ | Player stats (戰績計數器 CH*C) + 柏青哥 | 43 |
| GI_ | Inventory (slot 變更) | 14 |
| GM_ | 暱稱/角色建立 | 6 |
| GC_ | Channel / Clan / 反外掛查詢 | 20 |
| GQ_ | Quest | 15 |
| GV_ | GM Viewer 工具 | 13 |
| GX_ | XIGNCODE 反外掛 | 3 |
| PM_ | P2P master / UDP 中繼 | 16 |
| UDP_/TCP_/Y_ | NAT 打洞與存活偵測 | 14 |
| MASTER_ | GM/運維指令 | 79 |
| SECURITY_ | AhnLab HackShield | 2 |

_REQ = client→server, _ACK = server→client, _NOTIFY/_NOTICE = server 推播。
慣例: `ACK = REQ opcode + 1` (登入例外: GL_LOGIN_ACK=681 < REQ=682)。

主 dispatcher (client 端 lobby): `sub_58B010` — `switch(sub_591EE0(pkt))`
處理所有 ACK。遊戲內 UDP/戰鬥 packet 走 GameNetwork (0x2313148 物件)。

---

## 3. 關鍵 payload 結構 (伺服器必須產生/解析)

### 3.1 GL_LOGIN_REQ (682) — 客戶端 0x43E0F0 附近組包
```
string  account          (sub_401B50 回傳, ANSI)
string  account2/token   (同上再寫一次)
u64     hw_key XOR 混淆  (v << 32 | 0xAA) ^ 0xA4 / hi ^ 0xB1A9D7C7
u8      n2               (安全模組狀態 0/1/2)
byte[24] 版本/機器指紋   (0x18 raw)
```

### 3.2 GL_MYINFO_ACK (198) — handler sub_570550 → CClientData 反序列化
```
bool    success
若 success:
  s32   user_id (v19)
  --- sub_523BF0: 基本資料 ---
  string  nickname            (this+60,  0x30 bytes 區)
  u8      char_type           (this+88)
  s32     level/exp 相關 x3   (this+92,96,108)
  s32     win/loss/kill/death/disconnect x5 (this+136..152)
  s32     headshot/combo/heart/dkill x4     (this+156..168)
  s32     tkill/mkill/ukill/zkill x4        (this+172..184)
  s32     kkill/ddkill/critical/playc/roundc x5 (this+188..204)
  u8      flags x3            (this+304,305,306)
  s32     cash?               (this+104)
  s32     x2                  (this+112,116)
  byte[48] extra blob         (this+208)
  u8      slot_current        (this+4)
  --- sub_524010: 角色槽 (最多 20 個) ---
  u8      char_count
  repeat char_count (≤20):
    u8    char_type
    u16   x12  (裝備欄位: 主武/副武/近戰/投擲/頭/臉/上衣/下裝/手/背/特殊/套裝)
  --- sub_524660: 武器編組 (4 組) ---
  u8      group_count (≤4)
  repeat:
    u8    group_no
    u16   equipped_flag
    if group_no != 3: u16 x3 (sub-slot)
    if equipped_flag: s32 x8 (parts item ids)
  --- sub_527550 / sub_527D00: 技能欄 + 快速槽 ---
  u8 + s32 x7 (skill item ids, sub_527AF0 讀 0x1C bytes)
  u16     clan/channel id
  s32     game_point (GP)
  --- 其餘: 20 bytes 教學進度 ---
  u8      tutorial_count; u8 x count (≤20)
```

### 3.3 GL_MYITEM_ACK (200) — handler sub_570AB0 → sub_524B70 (分頁背包)
```
bool    success
s32     start_index          (分頁, 每包最多 100 條, 背包上限 5120)
repeat until sentinel:
  s32   inv_slot   (負值 = 結束)
  s32   item_id    (負值/非法 = 中止)
  float f1         (耐久?)
  float f2
  s32   period     (剩餘天數)
  [u8   extra]     (僅 202 GL_EXPIRE_PARTSUP_ACK 帶)
  u16   durability (寫入 *2 個 word: current=max)
```

### 3.4 GS_BUYITEM_ACK (205) — handler sub_571910
```
u8      count
repeat count:
  bool  ok
  若 ok: s32 item_id, float f1, float f2, s32 period_days,
         u8 item_kind, u16 durability
```
GS_BUY_ONCEITEM_REQ (695): u8/s32 item_id, string opt, u8 kind, u8 period。
period 合法值: 1/7/15/30/60/90 天 (kind 0,1,3,14)、0 = 永久型 (kind 2,4,9,15,10,11,16)。

### 3.5 GS_CASH_ACK (357) — sub_572420: `bool ok, s32 cash`。
### 3.6 GS_SELLITEM/DESTROY (209) — sub_5725D0: `u8 count, s32 money; repeat{bool ok, s32 item_id, float, float}`。
### 3.7 GM_CHECKNICK_ACK (211) / GM_CREATENICK_ACK (213) — sub_572D80/572E70: `u8 result`。
### 3.8 GL_USERLIST_ACK (106) — sub_56A250:
```
u16    count
u8     flags, u8 n
repeat n: s32 user_id, string nick, s32 x; if user_id>0 { s32 custom_tex_id, string }
```
### 3.9 GL_GAMEROOMINFO_ACK (108) — sub_568CE0:
```
u8   mode (3 = 委派 sub_580A80)
u8   count
repeat count:
  u8    room_no (≥0xD2=210 上限), s8 state
  string title (state<0 時只送 100-byte 定長塊)
  u8 map, bool, u8 rule, u16 win_count, u8 max_player, bool has_pass,
  bool item_mode, bool balance, u8 time_limit, u8 skill_off, u8 observer
  若 mode==2: {s32,u32 custom_tex,string,u8} x2 (隊伍圖示)
```
### 3.10 GL_FRIEND_LIST_ACK (434) — sub_55AFC0:
```
u16 x, string self, u8 count; repeat: string nick, s32 status
```
### 3.11 GL_MSG_RECVLIST_ACK (426) — sub_55A630:
```
u16 x, string self, u8 count
repeat: string from, u8, string title, u32 msg_id, string body(≤201), string, u16 date
```
### 3.12 GP_CH*C_ACK (223–245, 363, 381–389, 882) — sub_556730 系列:
全部是 `s32 new_value` (server 端累計後回推)。對應欄位:
playc/roundc/disc/winc/lossc/killc/deadc/headsc/acomboc/heartc/dkillc/tkillc/
criticalc/mkillc/ukillc/zkillc/kkillc/ddkillc/playtimec。
### 3.13 GQ_QUEST_ACCEPT_ACK (868) — sub_91CC70:
```
u8 result (0=OK, 7=特殊錯誤); result!=0: s32 quest_index_type
result==0: 13-byte 快照 {s32 quest_id, s32, u8, s32}
```
### 3.14 GS_GIVEGIFT_ACK (297) — sub_579830: `string from, string to, u8 x4`。

---

## 4. 對伺服器 DB 的直接推論

1. **背包上限 5120 格、每包分頁 100 條** (sub_524B70) → `inventory.slot 0..5119`。
2. **角色槽最多 20** (sub_524010 迴圈上限 20) → `characters.slot_no 0..19`，
   每角色 12 個裝備 u16 欄位。
3. **武器編組固定 4 組** (sub_524660 上限 4)，每組 1 個 flag + 3 個副欄 + 8 個 parts。
4. **技能/快速槽 7 格** (sub_527AF0 讀 0x1C=7*4)。
5. **戰績 19 個計數器** (GP_CH*C 家族)。
6. **道具屬性**: item_id(s32), 兩個 float(耐久/強化), period(天), kind(u8), durability(u16)。
7. **房間**: no(≤210), title, map, rule, win_count, time_limit, max_player(≤10 slots),
   password, item_mode, balance, skill_off, observer。
8. **好友/訊息/倉庫/任務/公會/禮物** 都有對應 packet 家族 → 各自建表。
9. period 天數 & 商店 kind 白名單直接寫進 CHECK constraint。
