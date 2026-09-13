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
1. 若 send_count(+19256)==0: word3 := word0 (原始大小)  (sub_591F90 = w3 setter)
   ⚠ 只在「第一次送出」設定 — 同物件重送不會重設 w3
2. 若 n0x2580>0 且 word0 ≥ n0x2580 → LZ 壓縮:
     sub_592CE0 → sub_592D30 → sub_591600
     word3 := 壓縮前大小 (再次經 sub_591F90 — 值同原始大小),
     word0 := 壓縮後大小 (sub_591F20), flag|=1
     (n16==0 或壓不小就放棄, 不設 flag)
     ⚠ 第二輪筆誤更正: 壓縮層寫的是 word3, 不是 word2
3. 一律 AES 加密: sub_592F60 → sub_592FB0
     n16 = 16-byte 對齊上取 (空 payload 也補一個 block); n16 ≥ 0x2578 → 失敗
     sub_4042A0(key_schedule, buf, n16, n2_4)   n2_4=1 CBC-加密 / 2 CBC-XOR先 / 其他 ECB
     word2 := 加密前 word0 (**(WORD**)(this+16)), word0 := n16, flag|=4
     ⚠ word2 由且僅由 AES 層寫入
4. WSASend(this+24, word0 + 8); 加密失敗 → 客戶端 ExitProcess(0)!
5. send_count++ (sub_593260, InterlockedIncrement)
```

**接收 (sub_555280 / sub_554E00 event loop):**
```
1. WSARecv 追加到 9600B 累積 buffer (a1+16, 已用量 a1[2404])
2. while (剩餘>0): sub_591FB0 把整段剩餘 bytes 灌進新 Packet
   frame_len = word0 + 8
   合法性 sub_591D50: 灌入量 ≥ 8 且 ≥ word0 (半包 → break 等下次 recv)
3. AES 解密: sub_5930C0 → sub_593110 → sub_404470(key, buf, n16, n2_4)
     驗證: word0 ≥ 16, word0 == align16(word2), word0 16 對齊, word0 < 0x2578
     word0 := word2 (還原大小), word3 不動, flag|=8; 失敗 → break (整緩衝殘留丟棄)
4. 若 word3 ≥ n0x2580 且 word0 < word3 (sub_592E50) → LZ 解壓:
     sub_592E00 → sub_592E90 → sub_591900
     驗證: 解壓後大小 == word3 且 < 0x2580; 失敗 → a1[2404]=0 (清空整緩衝)
5. dispatch sub_58B010(opcode switch); 之後 memmove 剩餘 bytes 到 buffer 頭
```

**dispatcher 覆蓋範圍 (sub_58B010, 365 個 case)**: 除了註冊表的名字外,
還處理 **27 個未註冊 opcode** (203, 367, 417, 488, 489, 852, 880, 914, 931,
933, 946, 947, 949, 954, 958, 970, 976, 991, 995, 997, 999, 1001, 1003,
1005, 1007, 1009, 1010) — 協定實際延伸到 1010; 203 = 武器編組同步 ACK
(sub_571D50 → sub_524660 反序列化), 995..1010 = 較新的 room/match 家族。
未知 opcode → default: return (靜默忽略)。

**Packet 物件其他機制 (伺服器不需要, 記錄供參考):**
- `this+9625` 第二個 9600B buffer + `this+19228` 長度 = 原始 payload 備份;
  sub_592C60 可從備份還原 (重送/重加密用)。
- `sub_5927F0/sub_592850` 內嵌 packet: u16 opcode + **u32** size + payload。
- 拷貝建構 (sub_592030/592110/592600) 會校正讀寫游標的相對位移。
- UDP 路徑 (sub_595A60, CUDPManager) 也走同一 AES 解密 (sub_5930C0),
  但長度來自 recvfrom 而非累積 buffer。

**AES 細節 (sub_403430 = key schedule 初始化) — 金鑰已完整還原:**
- 全域常數: `n16_0=16` (block), `n10=10` (rounds) → **AES-128**
- **金鑰 = EUC-KR 字串字面量「트렁크점령전머지」(後車廂佔領戰merge)**
  ```
  C6 AE B7 B7 C5 A9 C1 A1 B7 C9 C0 FC B8 D3 C1 F6
  ```
  (新版 Hex-Rays 9.4 導出直接展開了 sub_403430 的字串來源; 舊導出只見
  `unk_B69E88` 位址。) 大端組字 w[0..3] 後做標準 RotWord/SubWord/rcon
  展開 — 已逐位對照 FIPS-197 驗證為標準 AES-128 key expansion。
- 三重驗證測試向量 (獨立純 Python AES, 先過 FIPS-197 C.1 自檢):
  - `ECB(key, 000102030405060708090A0B0C0D0E0F) = D7F8930CFE8758AD7BF2FEF759EBB845`
  - `ECB(key, "PaperMan-Packet!") = 8B8ABD9B2B743448188ED7E554BD4AA2`
- 加密輪金鑰存 `dword_23199F8`, 解密輪金鑰 (逆序 + InvMixColumns 預處理)
  存 `dword_2319D18`; T-table `dword_B68E08/B69208/B69608/B69A08`,
  S-box `byte_B66C08`, rcon `unk_B69E08`
- `sub_403DE0`/`sub_403650` = 單 block 加密, `sub_404040` = 單 block 解密
- 模式 (sub_4042A0 的 n2): 1 = CBC 加密, 2 = CBC 解密, 其他 = **ECB**
- 封包路徑 `sub_592FB0`/`sub_593110` 傳全域 `n2_4` (未初始化, BSS=0) → **ECB**;
  別處的 `n2_4=1/2` 賦值屬 UI 狀態機重名 (五輪重驗: 127 處引用全部檢查,
  無一在網路路徑上)

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
      s16  ch_count
      若 ch_count > 0 (⚠ 即使 >1 也只讀一個條目 — 四輪驗證確認):
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

**多層分發 (五輪發現)**: `sub_58B010` 進 switch 前先呼叫兩個前置轉發器,
所以有些 opcode **不在主 switch 的 306 個 case 裡**也會被處理:
1. `sub_407360(dword_BEFEF0, pkt)` — 自己攔 **788 GL_RACKINGWEB_TOKEN_ACK**
   (u8 ok + str token≤16), 然後經 vtable+52 轉發給目前 UI 場景物件。
   已確認的場景級處理器:
   - `CLobbyShop::sub_46AD00`: **699** (u8, s32, s32), **703** (s32, s32 n,
     n×f32), **707** (str→this+521173), **709** (u8 n10; n10≠1 → s32, s32,
     s32), **807** (u8, s16, s16 n22; 迴圈 {s32, s16, u8, u8 len, raw len}
     ×3 組), **809** (s32)
   - `IVotingNetwork::sub_9BF430`: **719/720/722/723** (投票系統)
2. `CGameRule::sub_67CF90(n9_0, pkt)` — 遊戲規則層攔截。
**UDP 私有 opcode 空間 (五輪確認)**: `sub_595A60` (CUDPManager 收包:
recvfrom ≤9600 → 同一 Packet 檢核 `sub_591D50` + AES 解密 `sub_5930C0`)
→ `sub_595E80` 的 switch 用**獨立編號 2–34** (2,4,5,6,8,10,12,13,14,15,
18,20,22,24,26,28,29,31,33,34 + 154 UDP_ALL_PING_ACK / 158
UDP_TCP_DEAD_ACK 兩個註冊表編號)。UDP 戰鬥協定的編號與 TCP 註冊表
**不共用**, 私服做 relay 時不可混淆兩個空間。

**戰隊隧道協定 (五輪發現)**: `GC_CLAN_PROTOCOL_REQ(583)/_ACK(584)` 是
**容器封包** — payload 第一個欄位是 `s32 sub_opcode`, 之後才是子協定
內容。ACK 端 `sub_54D040` (case 584) 依 sub_opcode 分發:
182/184/185/186/189/192/193/194/198–201/203–207/210–215/218/219/381–383
(這些數字與頂層 opcode 空間**無關**, 是戰隊系統私有編號)。
REQ 端 24 個 builder 全部 `ctor(583)` + `WriteS32(sub_op)`:
182,184–189,191–193,195–197,200,202,203,205,208–212 (196/197 帶
`s32 count + raw(4*count)` 的成員 id 陣列)。伺服器實作戰隊功能時
必須解析/產生這層內嵌結構。

---

## 3. 關鍵 payload 結構 (伺服器必須產生/解析)

### 3.1 GL_LOGIN_REQ (682) — 客戶端 builder (30873 行附近)
```
string  account          (sub_401B50 回傳, ANSI)
string  account2/token   (同上再寫一次)
u64     hw 混淆值        (見下)
u8      n2               (安全模組狀態: 2=未檢, 0/1=sub_9A86A0 結果)
byte[24] 版本/機器指紋   (0x18 raw, builder 中全零初始化)
```
hw 混淆 (四輪交叉驗證精確化): `v5 = (u64)hw32 << 32` (hw32 來自 this+396),
`wire = sub_592AE0(pkt, (v5|0xAA)^0xA4, HIDWORD(v5)^0xB1A9D7C7)`
→ **lo32(wire) = 0x0E 恆定** (0xAA^0xA4), **hi32(wire) = hw32 ^ 0xB1A9D7C7**。
伺服器還原: `hw32 = hi32(wire) ^ 0xB1A9D7C7`, 並可用 lo32==0x0E 驗完整性。

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
  --- sub_527550 (sub_522480): 9×s32 — 無前導 count! (五輪修正)
      每個非零 id 需過 sub_535020 目錄驗證, 失敗 → client 錯誤 10
  --- sub_527D00: u8 n5 (+144452) + raw 28B = 7×s32 (sub_527AF0);
      非零 id 同樣驗證, 失敗 → client 錯誤 9
  --- sub_570550 尾段 (五輪補完, 先前部分遺漏):
  u16     → i_23 (clan/channel id)
  s32     game_point (GP, sub_5392A0)
  u8      tutorial_count (≤20); repeat count: u8 flag → sub_5A9B30
```
⚠ 198 的解析器是 sub_570550 (dispatcher case 198 直查確認)。嵌入完整
CClientData 的 sub_523A50 (523BF0+524010+524660+524B70(a3=0)) 其實屬於
**290 MASTER_USERINFO_ACK (sub_579830)** 與 **294 MASTER_USERINFODB_ACK
(sub_57A540)** — GM 查詢他人資料, 與 198 無關 (四輪誤記, 五輪更正)。

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
  u8    extra      ⚠ 四輪修正: 200 有 extra (sub_570AB0 呼叫 sub_524B70(cd,pkt,1));
                   無-extra 版 (a3=0) 屬 290/294 MASTER_USERINFO 系
  u16   durability (寫入 *2 個 word: current=max)
```
相鄰 opcode (五輪讀畢, GameNetwork 物件 0x2313148):
- **201 GL_MYPARTSUP_ACK** (sub_95A3B0): `s32 count` +
  count×`{f32, f32, u8, f32, f32}` (each → 20B part-up 條目, sub_95A4A0
  插入排序容器; 完成後 sub_538470 通知)
- **202 GL_EXPIRE_PARTSUP_ACK** (sub_95AE40): 同 201 佈局
  `s32 count` + count×`{f32 a, f32 b, u8, f32, f32}` — 但只取 (b,a) 呼叫
  sub_95A800 移除對應條目
額外驗證: start<=0 → 背包游標歸 0; start>=5020 → 夾到 5020; item_id 需通過
sub_535020 目錄檢查, 失敗即 sub_528960(6,...) 錯誤處理並中止本包。

### 3.4 GS_BUYITEM_ACK (205) — handler sub_571910 (四輪修正: 完整結構)
```
u8      count
repeat count:
  bool  ok
  若 ok: s32 item_id, float f1, float f2, s32 period_days,
         u8 item_kind, u16 durability
若 count==0: bool err, u8 err2     (錯誤碼對, sub_468470 顯示)
尾端固定 7×s32 (無條件讀取):
  s32 a, s32 cash    (cash → dword_EE8D18, CASH UI 顯示 "%10d")
  s32 b, s32 gp      (gp → GP UI)
  s32 c, s32 d       (d → dword_EE8D1C)
  s32 flag           (進 sub_468470 最後參數)
```
GS_BUY_ONCEITEM_REQ (695): u8/s32 item_id, string opt, u8 kind, u8 period。
period 合法值: 1/7/15/30/60/90 天 (kind 0,1,3,14)、0 = 永久型 (kind 2,4,9,15,10,11,16)。

### 3.5 GS_CASH_ACK (357) — sub_572420: `bool ok, s32 cash`。
### 3.6 GS_SELLITEM_ACK (209) — sub_5725D0 (六輪修正):
`u8 count, s32 money; repeat count{bool ok; ok 時: s32 item_id, f32, f32,
s32 flags}` — 每條目尾端的 s32 flags 先前遺漏; count==0 → 失敗 UI。
### 3.6b GS_BUYITEM_REQ (204) — builder @0x570A2C (六輪逐行驗證):
`u8 count; repeat{s32 item_id, u8 kind, s16 period, [s16 -(idx+1) 只在
kind 12/13/17 = 顏色/貼圖變體]}`
### 3.6c GS_BUY_ONCEITEM_REQ (695) — sub_570B00 兩變體:
有名版 `s32 item, str(64), u8 kind, u8 period`; 無名版省略 str。
### 3.7 GM_CHECKNICK (210/211) / GM_CREATENICK (212/213)
REQ (builder @0x572D30 / sub_572DC0): **只有 `str nick`** (⚠ 四輪修正:
u8+str 是 216/262 的格式 sub_56B180/56B230, 先前誤植)。
ACK (sub_572D80/572E70): `u8 result`。
### 3.8 GL_USERLIST_ACK (106) — sub_56A250 (四輪修正):
```
u16    count
若 count != 0:      ← count==0 時後面什麼都沒有
  u8   flags        (bit0: 開啟清單 UI; bit0|bit2: 關閉)
  u8   n
  repeat n: s32 user_id, string nick, s32 status
            if user_id>0 { s32 custom_tex_id, string tex_name }
```
### 3.9 GL_GAMEROOMINFO_ACK (108) — sub_568CE0 (五輪完整讀畢):
```
u8   mode (3 = 錦標賽樹狀圖, 委派 sub_580A80; 其他 = 房間清單)
u8   count
repeat count:
  u8    room_no (需 <0xD2=210), s8 state
  state>=0: u8 map, bool b1, u8 rule, u16 win_count, u8 max_player,
            bool has_pass, u8[1] title_first (title 由 client 表查
            room_no+309), 之後同下
  state<0 : string title, 之後 u8 map, bool b1, u8 rule, u16 win_count,
            u8 max_player, bool has_pass, bool title0
  共同尾段: bool b2, bool b3, u8 flag1, u8 flag2, u8 flag3
  (寫入 room+109=b2, room+128=b3)
  若 mode==2: 兩組 {s32 team_id, u32 custom_tex_crc, str(75/87) tex_name,
              u8 x} (隊伍自訂圖示, CCustomTexture 註冊)
```
sub_580A80 (mode==3 錦標賽): u8 n4, u8 i1, u8 flags142;
repeat i = n4-1 downto i1 {u8, u8 round_type, u8, s32, s32, u8 pair_count;
repeat pair_count {s32 room, u8, u8, bool, u8, u16} + [u8 只在
round_type==4] + 2×{s32 uid (+s32)} }; 之後 u8 has_my (≠0 → u8 room,
u8), f32 → 存 [494]。
### 3.10 GL_FRIEND_LIST_ACK (434) — sub_55AFC0:
```
u16 x, string self, u8 count; repeat: string nick, s32 status
```
### 3.11 GL_MSG_RECVLIST_ACK (426) — sub_55A630:
```
u16 x, string self, u8 count
repeat: string from, u8, string title, u32 msg_id, string body(≤201), string, u16 date
```
### 3.12 GP_CH*C 家族 (222–245, 362–363, 380–389, 882) — 四輪交叉驗證修正:
**REQ** (builder sub_5567F0@230 / sub_5568E0@232 / sub_556B90@244 等):
`s32 新的絕對累計值` — client 送 **total 而非增量** (a1<0 時不送)。
**ACK 分兩型**:
- 223/225/227/229/231/233/235 (playc/roundc/disc/winc/lossc/killc/deadc):
  `s32 total` (sub_556730/556780/5567A0/5567C0/5568B0/5569A0/5569E0)
- 237/239/241/243/245/363/381/383/385/387/389 (headsc/acomboc/heartc/dkillc/
  tkillc/criticalc/mkillc/ukillc/zkillc/kkillc/ddkillc):
  `s32 total, s32 extra` — sub_556A00 讀**兩個** s32 (extra 進 EE8DB0..
  顯示區, 送 0 安全)
- 882 (playtimec): **無 REQ**, server 推播 `s32 總秒數`, client case 882
  自行差分 (dword_EE8D7C)。
### 3.13 GQ_QUEST_ACCEPT_ACK (868) — sub_91CC70:
```
u8 result (0=OK, 7=特殊錯誤); result!=0: s32 quest_index_type
result==0: 13-byte 快照 {s32 quest_id, s32, u8, s32}
```
### 3.14 GS_GIVEGIFT_ACK (297) — sub_57AA50 (七輪修正; sub_579830 屬 290):
`u8 result` — 0=成功, 之後 5×s32 (cash/餘額顯示組); 1..11 = 錯誤碼
(對應 11 種禮物失敗訊息)。

### 3.15 房間系統 (七輪讀畢)
- **111 GL_MAKEROOM_REQ** (builder @0x569xxx): `u8 map(a1<0 時 0xFF), u8 pass_flag,
  [str title 無密碼版/密碼版], str pass, u8 rule, u8 max_player, u8 x, u8 y`
  (兩個分支: a3!=0 帶密碼, 否則 title 版)
- **112 GL_MAKEROOM_ACK** (sub_56A7B0): `u8 err, u8 room_no(<210), u16, f32;
  u8, bool` + err==0 時: `u8 n2, {s32 team_id, s32 tex_crc, str, u8}×2
  (mode==2)` — 建房成功即以自己為房主初始化房間物件
- **113 GL_ENTERROOM_REQ**: `u8 room_no` (單欄位)
- **114 GL_ENTERROOM_ACK** (sub_56B360): `u8 sub_type` +
  sub_type==0: 失敗回大廳; ==1: `s32 uid, u8 slot, str nick, [s32, u8]...`
  單人進房通知 (含 CClientData 嵌入 sub_524360 + s32 custom_tex + str);
  ==2: `f32, u8, u8 count, u8 slot, u8 host, u16 ...` 完整房間狀態 +
  count×成員條目 {s32 uid, u8 slot, str nick, bool, bool, [完整資料]};
  ==3..9: 各種單欄位/雙欄位變更通知 (首欄皆 u8 room_no)
- **110 GL_ROOMINFOCHANGE_ACK** (sub_569240): `bool ok, u8 sub_type` +
  sub_type 1/2: room_no + 標題/密碼/規則變更組; 3..9: u8 room_no 單欄位
- **216 GL_ENTERROOMPASS_REQ / 262 GL_JOINPASS_REQ**: `u8 room_no, str pass`
  (sub_56B180/sub_56B230 — 六輪已證非暱稱包)
- **218 GI_CHANGEDATA_REQ** (sub_572FC0): `u8 char_type, u8 count,
  count×{u8 slot_idx, u16 item×12 (sub_5244E0: 1+12 欄位)}` — 只送有
  變更的角色槽 (sub_525450 差異偵測)
- **219 GI_CHANGEDATA_ACK** (sub_573230): `u8 result` → UI 解鎖 + 重繪

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
