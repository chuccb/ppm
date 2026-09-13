# PaperMan 網路協議完整分析 (根據 PaperMan.exe.c IDA 導出)

> **廿六輪終極對賬 (兩方向自動審計)**:
> C# ACK 寫入序列 ↔ client 讀取序列: 18/18 ✓;
> C# REQ 讀取序列 ↔ client 寫入序列: 27/27 ✓ (5 個機械標記經人工
> 複核均為變體混列/raw4≡s32/子函數未展開等誤報)。
> 附帶確認: 199 GL_MYITEM_REQ client 端**不送 start 欄位** —
> server 恆從 0 開始送背包 (C# Remaining 守門已天然正確)。

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

**壓縮門檻協商 + 登入觸發 (694 的雙重功用, 十一輪定案)**:
全域 `n0x2580` 初始 0x2580(9600, 即「從不壓縮」)。
`GL_ACCOUNTCONNSUCC(694)` 攜帶一個 u16, 若 <0x2580 則更新門檻
(0x43E651 的 `n694==694` 分支) — **由伺服器決定是否啟用壓縮**。
⚠ 更關鍵的第二功用: 讀完門檻後緊接呼叫 `sub_43DF00` = **682 登入
REQ 的 builder** (帳密欄位先驗證 sub_43DD60: 只允許 [0-9A-Za-z@],
非法則顯示 0xE1 訊息不送包)。所以 **694 是登入流程的觸發器**:
連線建立 → 伺服器發 694 → client 送 682 → 伺服器回 681。
私服: 連線時發一次 694 (門檻 0x2580=停用壓縮最穩), 登入後**不可**
再發 (client 會重送 682 → 無限迴圈)。

**GL_LOGIN_ACK(681) 完整結構** (0x43E651 同函數 `n694==681` 分支):
```
s32  result           1=成功, 2=帳密錯(0x42), 其他≠0=一般失敗(0x23D);
                      0xC8..0xD6 逐碼定案 (十一輪, 字串表 id):
                      0xC8=200 帳號封鎖彈窗(0x70/17)
                      0xC9=201 / 0xCA=202 維護中(0x23E)
                      0xCB=203 / 0xD2=210 重複登入(0xA4)
                      0xCC=204 防沉迷/時段限制(0x316)
                      0xCD=205 (0x317)  0xCE=206 (0x318)
                      0xCF=207 (0x319)  0xD0=208 (0x31E)
                      0xD1=209 (0x31F)  0xD3=211 格式訊息(0x387)
                      0xD4=212 格式訊息(0x388, 帶參數 212)
                      0xD5=213 (0x3C4)
                      0xD6=214 GM 帳號 IP 不允許 (硬編碼英文訊息)
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
七輪補: **154 UDP_ALL_PING_ACK** (sub_5965D0) = `u8 count,
count×{u8 room_slot, u8 ping_grade}` — 以 slot 對照房間成員表更新
ping 顯示; **158 UDP_TCP_DEAD_ACK** (sub_596910) = 無 payload 的
斷線通知。
九輪補 (CUDPManager 打洞/測延遲層, 物件 0x1324330):
- 通用條目頭: `u8 slot_uid` + 16B blob (sub_592C40) 或 `u8 + f32/u32`
- **4** (sub_593AB0): `u8 count, count×{u8 uid, 16B addr_blob}` —
  對方地址表; 回覆時 ctor(5) 帶 `u8 my_slot(n2==2 時 -2), u32 tick`
- **5** (sub_593E60): `u8 uid, f32 tick` — 打洞探測; 首見該 uid 記錄
  來源位址 (recvfrom 的 sockaddr @1326944) 並回 ctor(6)
- **6** (sub_5940E0): `u8 uid, f32 tick` — 打洞回應確認
- **8/24** (sub_596940): 空 payload keep-alive
- **10/12/13/14** (sub_594460/5946C0/594A10/594CA0): `u8 uid, 16B blob`
  + 回覆 `u8 slot, u32 tick, 3×...` — 中繼協商序列
- **15** (sub_593DF0) / **29** (sub_593E20): 短探測
- **22** (sub_5964E0): `u8, u8 count, count×{u8 uid, f32 rtt}` —
  RTT 表回報
- 18/20/26/28/31/33/34: 狀態機推進 (無/極短 payload)
私服結論: 這層只做 P2P 打洞與測速, 中繼伺服器只需回聲/轉發,
不需理解 16B blob 內容 (原樣轉發即可)。

**戰隊隧道協定 (五輪發現)**: `GC_CLAN_PROTOCOL_REQ(583)/_ACK(584)` 是
**容器封包** — payload 第一個欄位是 `s32 sub_opcode`, 之後才是子協定
內容。ACK 端 `sub_54D040` (case 584) 依 sub_opcode 分發:
182/184/185/186/189/192/193/194/198–201/203–207/210–215/218/219/381–383
(這些數字與頂層 opcode 空間**無關**, 是戰隊系統私有編號)。
REQ 端 24 個 builder 全部 `ctor(583)` + `WriteS32(sub_op)`:
182,184–189,191–193,195–197,200,202,203,205,208–212 (196/197 帶
`s32 count + raw(4*count)` 的成員 id 陣列)。伺服器實作戰隊功能時
必須解析/產生這層內嵌結構。

**廿四輪自動定案 — 全 24 個 REQ builder 寫入序列**
(sub 編號後的欄位; s=str, i=s32, R=raw):
182:(無) 184:str 185:i,i,i,i 186:str 187:i 188:i,i 189:i
191:i,str 192:i 193:i 195:i,str 196:i,[R] 197:i,[R] 200:i 202:(無)
203:str 205:str×3 208:(無) 209:(無) 210:str 211:i 212:i

**八輪逐一佈局 (REQ 段 = sub 之後的欄位; ACK 段 = 584 內容):**
```
sub  REQ (client→server)          ACK (server→client, sub_54D040 分發)
182  (無)   邀請入隊確認           s32 clan_id → 顯示歡迎訊息 (sub_54E890)
184  str clan_name  申請入隊       s32 clan_id, str nick (sub_54E490)
185  s32 x, s32 uid, s32 11, s32 0 s32 clan_id, s32 reason (sub_54E670)
186  str nick       踢除           s32 uid, str nick (sub_54E2E0)
187  s32 clan_id    戰隊資訊       (查表更新)
188  s32 clan_id, s32 page 成員頁  —
189  s32 clan_id    公告           s32 clan_id, str notice ×2 (sub_54F9B0,
                                   兩個 str 都 strncmp 0x18 比對)
191  s32 n3, str nick  邀請        —
192  s32 answer     邀請答覆       s32, str ×4, s32 ×5 (sub_54E0F0
                                   完整戰隊摘要塊)
193  s32 flag       解散           (this 語境處理 sub_54E020)
195  s32 id, str    標誌查詢       —
196  s32 count, raw(4n)  成員id    —
197  s32 count, raw(4n)  成員id2   —
200  s32 clan_id    成員清單       s32 count, count×{s32 rank(0..4),
                                   s32 uid, s32 level, str nick,
                                   str, s32 status} (sub_54EE70)
202  (無)           次數查詢       s32, s32 (sub_54F0F0)
203  str message    戰隊聊天       (sub_54F2D0; 需 rank>1 才可送)
205  str×3          戰隊訊息       str from, str title, str body
                                   (sub_54F3D0)
208  (無)           捐獻           — (sub 208 只有 REQ)
209  (無)           基金           — (同上)
210  str            設定變更       s32 uid, str (sub_54E770)
211  s32 uid        升職           s32 clan_id, str nick → rank:=5
                                   (sub_54FBA0, 過場訊息 0x3E9)
212  s32 uid        降職           同 211 鏡像 (sub_54FD50)
198  —              戰爭邀請       s32, s32, str ×2, s32 (sub_54F450)
199  —              戰爭答覆       (sub_54F5A0)
201  —              排名           s32, [s32] (sub_54F0F0 同構)
381..383  —         戰隊戰績       s32 ×2 (sub_54FF00/54FFC0, 帶 sub 參數)
```
**獨立對: 585 GC_CLAN_CREATE_REQ / 586 _ACK (不走隧道!)**:
REQ (sub_5505F0) = `str name, str slogan, str intro, s32 emblem`
(廿四輪修正: sub_592A20 = s32 非 u8);
ACK (sub_54CB90) = `s8 result` — 0=成功 (再讀 `s32 → EE8D18` 扣費後 GP,
sub_54DD70), 1..7 = 錯誤碼 (重名/GP 不足/等級不夠...)。
建立成功後 client 自行送 583/187 拉戰隊資訊。

---

## 3. 關鍵 payload 結構 (伺服器必須產生/解析)

### 3.1 GL_LOGIN_REQ (682) — 客戶端 builder (30873 行附近)
```
string  account          (sub_401B50 回傳, ANSI)
string  account2/token   (同上再寫一次)
u64     hw 混淆值        (見下)
u8      n2               (十八輪定案: 2=無法取得, 否則 = sub_9A86A0
                          「是否取得 MAC」 — GetAdaptersInfo 實作)
byte[24] 機器指紋         (sub_9A8790: 機器識別字串 ≤23B+NUL;
                          sub_9A7B90 失敗時全零 — 這就是常見的
                          全零指紋塊。伺服器可存字串做多開檢測)
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
      ⭐ 十九輪語意定案: 驗證段 15,304,001..15,306,000 = **稱號段**
      (キリ番ゲッター等 568 條) → 這 9 個 s32 是「持有稱號槽」!
  --- sub_527D00: u8 n5 (+144452) + raw 28B = 7×s32 (sub_527AF0);
      非零 id 同樣驗證, 失敗 → client 錯誤 9
      ⭐ 驗證段 11,010,001..11,070,000 = **ヘアパズル段** (1,273 條,
      kind 13) → 7 個「髮型拼圖槽」; n5 = 已解鎖拼圖數?
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
  float f1         (⭐ 廿一輪定案: 外觀技能 roll 值 — NewSkillLevTable
  float f2          0..140 稀有度分級, Hair/Jacket/Pants/Shoes/Accessory/
                    Set 六槽適用; 舊制 ItemAbility 為負值懲罰表。
                    server 送 0 = 無技能 (合法); 進階可隨機 roll)
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
尾端固定 7×s32 (無條件讀取; ⚠ 十一輪以商店 UI 標籤逐槽定案,
先前 CASH/GP 對映相反):
  s32 v22 (保留), s32 v16  (v16 → *EE8D18 → "PG" 欄位 = GP 點數)
  s32 v26 (保留), s32 v20  (v20 → "CASH" 欄位 = 現金)
  s32 v15 (保留), s32 v27  (v27 → *EE8D1C → "CP" 欄位 = 第三貨幣)
  s32 v18 (旗標, 進 sub_468470 最後參數)
(UI 於 36084 行: EE8D18 印在 "PG" label, ArgList=v20 印在 "CASH",
EE8D1C 印在 "CP" — 標籤即鐵證)
count==0 失敗路徑的 `u8 err` (→ sub_468470 a3, 十一輪逐 case):
0=一般失敗(0xB7; a4==0 且 a5(item_id)≠0 時帶名字 0x348/0x349),
1=餘額不足(0xCD), 4=格式訊息(0x39A), 5/7=期限/重複(0x63),
8=背包滿(0x327), 9=其他 — 只有這 7 個值有訊息, 其他值靜默。
```
GS_BUY_ONCEITEM_REQ (695): u8/s32 item_id, string opt, u8 kind, u8 period。
### 3.6d GS_BUYCASHITEM_ACK (359) — sub_5725D0 (廿三輪正位!):
`u8 result, s32, bool, s32 item_id, f32 f1, f32 f2, s32 period` —
四/六輪曾誤把此函數當 209 賣出; 自動審計正位: 它是 **CASH 購買 ACK**
(單件 + 技能 roll 值)。
### 3.6e GS_BUY_ONCEITEM_ACK (696) — sub_571D70 (廿三輪首錄):
`u8 mode, s32 item_id` +
- mode==0 → u32 (n100 直購)
- mode==1 且 item==E975BE(15,300,030 コイン充填) → s32 gp → GP 更新
- item 為 MAC 特殊 id (sub_9A8660) → s32×2 (目錄調整)
- item==E975A1 (sub_9A8620) → str + 完整 19 欄 (名稱+能力+餘額組)
即 695 的回包 — 依購買物種類多型!
period 合法值: 1/7/15/30/60/90 天 (kind 0,1,3,14)、0 = 永久型 (kind 2,4,9,15,10,11,16)。

### 3.5 GS_CASH_ACK (357) — sub_572420: `bool ok, s32 cash`。
### 3.6 GS_SELLITEM (208/209) — ⚠ 廿三輪自動審計重修!
**REQ 208** (sub_572AD0): `s32 slot_idx` — 賣出單件 (背包槽序)。
### GS_SELLITEM_ACK (209) — sub_572B80:
`bool ok; ok → s32 v11, s32 gp_after(→*EE8D18 = PG 顯示), s32 item_id`
— client 以 item_id 掃背包快取 (EE8FF4, 28B/條) 移除該件並左移
壓縮陣列, PG 餘額更新。**單件交易, 無 count 迴圈** — 四/六輪的
「u8 count + repeat」版本是誤讀他函數 (sub_5725D0 非 209 handler,
dispatcher case 209 → sub_572B80 直查定案)。
### 3.6b GS_BUYITEM_REQ (204) — builder @0x570A2C (六輪逐行驗證):
`u8 count; repeat{s32 item_id, u8 kind, s16 period, [s16 -(idx+1) 只在
kind 12/13/17 = 顏色/貼圖變體]}`
### 3.6c GS_BUY_ONCEITEM_REQ (695) — sub_570B00 (廿四輪定案):
`s32 item_id, u8 kind, u8 period, u16 variant` — 三個呼叫點序列
一致; 七輪的「str(64) 版」是誤讀 String 緩衝宣告, 已更正。
### 3.7 GM_CHECKNICK (210/211) / GM_CREATENICK (212/213)
REQ (builder @0x572D30 / sub_572DC0): **只有 `str nick`** (⚠ 四輪修正:
u8+str 是 216/262 的格式 sub_56B180/56B230, 先前誤植)。
ACK (sub_572D80/572E70): `u8 result` — **result 語意十輪逐分支定案**:
- 211 (sub_41BBB0): `1` = 可用 (訊息 0xE0), `2` = 已被使用
  (格式訊息 0xDF), `0` = 一般錯誤 (彈窗 0x70/17); 三者皆 state:=2
- 213 (sub_41BD40): ⚠ **`1` = 成功** (拷貝 6 個統計欄位到全域,
  state:=5 進大廳), `0` = 失敗 (彈窗, state:=4), 其他值被忽略
  (client 卡在原畫面) — 成功碼是 1 不是 0!
### 3.8 GL_USERLIST_ACK (106) — sub_56A250 (四輪修正):
```
u16    count
若 count != 0:      ← count==0 時後面什麼都沒有
  u8   flags        (bit0: 開啟清單 UI; bit0|bit2: 關閉)
  u8   n
  repeat n: s32 user_id, string nick, s32 exp
            if user_id>0 { s32 custom_tex_id, string tex_name(64) }
```
⚠ 第三個 s32 是 **exp 不是 status** (十二輪定案): sub_588560 對它呼叫
sub_403360(exp→level 查表) 後把 level 顯示在清單。custom_tex_id 進
CCustomTexture 快取請求 (個人頭像貼圖)。
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
**十一輪補 — ACK 的隱藏副作用 (任務進度 hook sub_92EF00)**:
多數奇數 ACK 存值後還驅動任務系統: 223 playc → `sub_92EF00(21,23,Δ,0)`
(差分後餵), 229 winc → `(5,23,1,0)`, 231 lossc → `(6,23,1,0)`,
233 killc → 差分進 EE8DAC 累計, 882 playtime → `(20,23,Δ,0)`。
→ 伺服器送這些 ACK 等於在幫 client 推進「贏 N 場」「殺 N 人」型任務;
total 送錯會讓任務進度爆走 (再次強調: 必須 MAX 單調)。
### 3.12b 任務條件類型全表 (十八輪 — Quest.pat 844 條實測分布)
兩類條件 (雙機制互證):
- **cond 1..20 = 累計型** (sub_9252D0 查 CClientData): 3=kills(302條!),
  5=wins(155), 20=playtime(30), 7=disc, 8=heads, 9=combos, 10=hearts,
  11=double, 12=triple, 2=exp/level(13)...
- **cond 21..36 = 事件型** (sub_92EF00 事件流直接推進, CClientData 無
  對應槽): 21=連勝『最強の傭兵』(67條), 26=模式事件『略奪者阻止』,
  36=助攻『アシストチャレンジ』, 19=星星『星集める者たち』, 24/25/32/35
  = 房間/特殊事件 — 與十一輪在 C 檔找到的 sub_92EF00(19/21/24/25/26/
  32/35/36,...) 呼叫點一一對應!
其他欄位: PeriodType 恆 1; QuestRepeat 215 條可重複; LimitDate 分鐘制
(180/1200/1440); TermItem1-5 (305 條要求持有物品); UseWeapon (20 條
限定武器)。

### 3.12c 庫存條目記憶體結構 (sub_524F70, 28B) — 廿一輪
`{u32 flags=0, s32 item_id, f32 f1, f32 f2, s32 period, u8 kind,
u16 dura, u16 dura_max}` (7 dword × 最多 5,120 槽 @ this+210)。
kind 0/1/14 與 12/13/17 (可覆寫類) 走覆寫路徑, 其他 kind 重複購買
會新增槽位 — 對應 200/205 條目欄位一一吻合。

### 3.12d Quest.pat 欄位補完 (廿一輪)
- CharacterType: 0=全角色 (606 條), 1..14=限定角色任務 (各 19 條)
- ChanelList: "4_5" 格式 = 頻道限定 (20 條)
- Hidden: 119 條隱藏任務 (achievements)
- ClearItemOption1 = **獎勵期限天數** (0=永久 612, 1/7/15/30 天)
- ClearItemLimit 全 0 (未使用)
→ quest_catalog 已補 char_type/reward_period/hidden 欄位

### 3.13 GQ_QUEST 任務家族 (八輪全家讀畢)
13-byte 任務快照 = `{s32 quest_index, s32 progress, u8 state, s32 extra}`
(state: 0=NONE 1=WORKING 2=SUCCESS 3=FAILED — sub_91C7B0 的除錯字串直接
印出這些名稱, 交叉驗證用)。
```
865 GL_SERVER_DATETIME_ACK (sub_585AB0): s32 unix_time
866 GQ_QUEST_LIST_ACK  (sub_91C7B0): s32 count(<3), count×快照(13B),
    然後 3 個 raw 塊: 7B (this+59682, len@59681=4→實為7B 初始),
    再 2 塊 (len@59699/59765; 全 exe 僅此處引用 → 客戶端未再使用,
    伺服器可送空/固定長度)
867 GQ_QUEST_ACCEPT_REQ (sub_91CB80): s32 quest_index
868 GQ_QUEST_ACCEPT_ACK (sub_91CC70): u8 result (0=OK; !=0 → s32 idx);
    OK → 13B 快照
869/870 CANCEL: REQ s32 idx; ACK (sub_91D290) u8 result
    (!=0 → s32 idx, s32) — 成功後 client 清 working 槽
871/872 SUCCESS: ACK (sub_91C6F0) u8 result, s32 idx —
    十三輪細節: **任務類別 = quest_index / 10000**; 只有類別 2/3/4
    會轉 SUCCESS 狀態 (sub_91D700(idx, state=2)), 其他類別忽略。
    → quest_id 編碼: 類別×10000 + 序號 (quest_catalog 需照此配 id)
873/874 COMPLETE: ACK (sub_91C1E0) u8 result, s32 idx
    (n4==1 領獎, 2/3/4 分支; n4==4 特殊獎勵)
875 CHANGEDSTATE_ACK (sub_91D690): raw 5B = {s32 idx, u8 new_state}
877 ACCEPT_DAILY_ACK (sub_91D7E0): u8 ok; ok → s32 count +
    raw(13*count) 快照陣列 (每日任務批次)
879 USER_COMPLETE_HONOR_ACK (sub_91CAA0): u8 result (0=成功),
    str title, raw(len@239104) 榮譽塊
881 CURRENTITEMQUEST_ACK (sub_91DC90): s32 item_quest_id
```
### 3.14 GS_GIVEGIFT (296/297) — 七輪修正 (sub_579830 屬 290):
**REQ 296** 兩變體 (builder @0x57A6xx):
- 簡短版: `s32, u8, u8`
- 完整版: `str to_nick, u8 has_msg(0 → 只寫 0), [str message], s32 item_id,
  u8 kind, u8 period, [u16 -(idx+1) 只在 kind 12/13/17]` — 與 204 同樣的
  顏色/貼圖變體尾欄
**ACK 297** (sub_57AA50): `u8 result` — 0=成功, 之後 5×s32
(cash/餘額顯示組); 1..11 = 錯誤碼 (11 種禮物失敗訊息)。

### 3.15d1 高欄位數漏網 opcode (廿三輪自動表定位)
```
765 CLAN_TNMT_ENTERROOM_ACK (25欄): 前綴與 114 進房完全同構
    (u8+s32+u8+str...) — 錦標賽進房 = 114 的克隆
265 GL_JOININFO_ACK (25欄): {u8 map,u8,u8,u16 win,u8 max,u8,u16,u8,
    u16,u8,str title} ×2 組 — 跨頻道跟隨好友的目標房資訊
257 GL_ENTERROOMOB_ACK (19欄): 觀戰進房 (114 的觀戰版)
486 GAMEROOM_PROGRESSTIME (23欄): 房間進行時間+成員狀態同步
986 GR_MATCHINGROOM_START_ACK (17欄): 與 130 GR_START_ACK 完全同構
    (配對房開戰 = 130 克隆)
734 GG_SPAWNPULP_ACK (25欄): PVE 怪物生成 (座標+屬性+HP)
740 GG_DESTROY_SUCC_ACK (24欄): PVE 破壞成功結算
959/961/963 GG_DROPWEAPON 三連 (10-13欄): 掉落武器系統
    {u16 drop_id, u8, s32 item, u16 dura, s16×3 座標, u16×2, f32}
    — create/info/get&drop 三態同構
919 GR_AI_GET_REWARD_ITEM_ACK: u8+{u8,s32}×4 — AI 模式獎勵
    (對應 AiMultiCompensation.xml 的難度×等級表!)
725 GL_COMBISKILLITEM_ACK: u8 + 10×s32 + f32×2 + u8 + bool —
    組合技能 (COMBI 表, NewSkillLevTable 的 COMBI 節點對應)
```
同構鏈總結: 114⇔257⇔765⇔985 (進房四變體), 130⇔986 (開戰),
959⇔961⇔963 (掉落) — client 重用解析器的鐵證。

### 3.15d4 MASTER GM 工具組全表 (廿五輪 — 最後一族掃畢)
```
275/276 MEMO (wstr 私訊) / 277/278 MEMOALL (wstr 全服公告)
279 USERCUT (u8+str 踢人) / 281 USERCUT2 (s32 uid) / 283 ROOMCUT (u8 房)
285/286 MSET (u8 GM模式) / 287/288 PRINTUSER (線上清單)
289/290 USERINFO (str nick → str×2+u8×4 + sub_523A50 全量)
293/294 USERINFODB (str → u8+str DB 查詢)
394/395 ROOMINFO (u8 → u8×2+str×2)
402-405 EVENTPAGE/EXP (f32 經驗/點數倍率! → 841-846 SETALL 版本
  + s32 影響人數回報, 846 VIEWALL = str+s32+f32×2 per 伺服器)
406/407 ENABLE/DISABLE_LOGIN (開關登入) / 416 KILLALL (全服踢出)
412/414 DISGMS/DISLOG (str+s32 停用服務) / 800 XTRAP_RELOAD (反作弊重載)
822/823 CHAT_BAN (u8+u8+str → u8) / 830 FORCE_BAN (u8+str+s32 天數)
824-826 USERLIST (u8 type+s32 page → 大廳版 s32×2+str×2 /
  房間版 s32×3+str×2)
883/884 FIND_USER (s32 uid → bool+s32+str×2+s32+u8×4 所在位置)
885 PLAY_WITH (s32 — GM 跳到玩家房間)
773/804/892 RELOAD (錦標賽/隱藏商品/戰隊排名 熱重載)
812-815 特殊能力槽/炸彈作弊檢測開關 (s8 → bool)
831 RESET_PACKET_DELAY (s32 — 流控參數!)
```
GM 權限: 這些 REQ 無等級檢查 — server 端必須以帳號 GM flag gate
(客戶端 builder 存在不代表可用; 681 result 0xD6 的「GM IP 白名單」
是唯一 client 端 gate)。

### 3.15d3 GG 戰鬥中繼全 58 對 — 轉發模式分類 (廿五輪自動配對)
Server 的 GG 處理 = **驗證 + 廣播**, 三種模式:
1. **slot 前綴轉發** (最常見): ACK = `u8 actor_slot` + REQ 原欄位
   [+附加]。實證: 316 駭入 (REQ u8 → ACK u8+u8), 318 駭入成功
   (REQ u8+6×f32 → ACK 同+u8), 326/328/330 拆彈, 737/739/741 破壞,
   730 奪紙漿, 820 檢舉, 902/906 佔領
2. **復活五連同構**: 342 SOLO / 360 TSUR / 455 EXERCISE / 746 PNR /
   909 OCC / 971 SOCCER — REQ 全是 `s32 (respawn token)`,
   ACK 全是 `u8 slot, u8, s16 x, s16 y, s16 z` (座標指派) —
   六個模式共用一個復活協定!
3. **聊天四連**: 344/346/348/350 (live/team/dead/teamdead) REQ 全 =
   `s32 tex, u8 slot, str msg`; ACK 端無讀取 (client 以自身緩衝顯示)
   → server 依模式過濾聽眾後原樣轉發
其他: 443/445/447 奪寶三連 (ACK u8+u8+u16×3 分數組); 964/967 足球
(得球/進球 = u8×2); 749 GIMMICK (s32×2+u8); 752 地圖重載;
962 掉落武器 (REQ 6 欄 → ACK 13 欄 = server 附 drop_id+item 詳情);
474-483 射擊館 — 476 END 成績塊解構 (廿五輪): raw24 =
{tick, 0, user_no(sub_525070), uid(EE8CB4), score, wave} 6×s32;
raw44 = {…, [12]=命中, [7]/[8]/[9]=擊殺分類, [5]=fever} 11×s32
(sub_8EE1D0 射擊館統計物件); 478 CHECK raw36 防作弊快照;
716 快速槽 4×s16; 437 房間廣播 (u8+s32+rawN 自由載荷)。
334/336/338 SEEDKEY/UNIQUEKEY/DETECTCRACK = 反作弊挑戰 (REQ/ACK 皆
無 builder/parser — 由安全模組直接組包, 私服可忽略)。

### 3.15d2 剩餘家族速覽 (廿二輪終掃)
```
984 GL_MATCHINGROOM_MAKE_ACK  (sub_5865A0): u8 result — 配對房建立
985 GL_ENTERMATCHINGROOM_ACK  (sub_586610): u8 result; OK → s32 uid,
    u8 slot, str nick + [s32 …] 成員迴圈 — 配對進房 (與 114 同構)
989 GL_MATCHINGROOM_CANCLE_ACK: 取消配對
757 GL_CLAN_TNMT_RECEIPT_ACK  (sub_57E030): u8 result — 錦標賽報名
763 TNMT_CURRENT_STATE_NOTICE (sub_57E5A0): f32 time, u8 state,
    u8 round, u8; state==2 → s32 tnmt_id (比對自己 clan) — 狀態推播
772 TNMT_ALL_INFO_ACK (sub_57E550): 錦標賽總覽
473 GL_GAMECENTER_REC_ACK (sub_584910): u16, s32, u8 n1 ×
    raw(0x38=56B 紀錄塊), u8 — 射擊館排行榜 (56B/條)
482 GAMECENTER_COIN_CHANGED (sub_585F50): 遊戲幣變動推播
705 GL_LEVEL_KILL_LIMIT_ACK (sub_55C9B0): s32 level_limit,
    f32 → BEFEE8, s32 kill_limit — 房間等級/擊殺限制參數
816 GL_ADDICTION_PREVENT_ALARM (sub_582500): u8, u8, f32 —
    防沉迷警告 (遊玩時數提醒)
```
至此 dispatcher 306 case 中: 大廳/商店/房間/戰隊/任務/好友/倉庫/
配對/錦標賽/GameCenter 全部家族皆有佈局記錄; 未逐條展開者僅餘
GG 戰鬥事件中繼 (server 原樣轉發即可) 與 MASTER_* GM 工具組。

### 3.15e GL_JOINPLAY_ACK (269) — sub_574B20, 1524 行巨型函數 (十輪讀畢)
中途加入/觀戰的「全房間快照」。頂層: `u8 n7` switch:
- 0: 失敗, 通知 UI (sub_406F20(0))
- 1..5, 8, 9: 各種拒絕碼 (sub_406F20(n7))
- 6: **觀戰者自己入房** — `s32 v482, u8 slot(≤16), str nick` +
  CClientData 嵌入 (sub_524360) + `u8, s16, s16, s16` (角色外觀) +
  `s32, s32, s32 custom_tex, str(64)` + 4×武器組 {s16 equipped,
  kk!=3 → s16×2... , equipped→8×s32 parts} + `u8` + [8×s32] + ...
- 7 (fall-through 主體): **完整房間+全成員快照**:
  房間頭: `s32 room_uid, s32 elapsed_ms (同 130 的時間基準), u8 map, u8 count(jj_1),
  u8 room_no, u8 rule, u16 win, u8 max, u8, u16, u8 flags(bit0/1 拆),
  u8 has_pass, u16, u8, u8, u8 obs` + `u8×4 (n2_10 等模式旗標)`
  然後 count× 成員條目:
  `s32 uid, u8 slot, str nick, u8 team, u8 ready(1&1→0 特例),
  s32, s32, s32 custom_tex, u8 alive, u8 dead_flag` +
  [alive==0: 16B blob, u16×2, u16, u16×2, s8 觀戰目標] +
  strcmp 自己→special, `u8 char_type, s16×3 外觀`, `s32×2, s32 tex,
  str(64)`, 4×武器組 (kk!=3 帶 sub-slot, equipped→8×s32 parts), u8 + 8×s32
私服要點: 快照結構 = 114 (ENTERROOM sub_type==2) 的擴充版; 兩者成員
條目欄位順序一致 (交叉驗證), 269 多了戰鬥中狀態 (alive/dead/觀戰目標)。

### 3.15c3 倉庫五連 856-863 (廿二輪 — n11==19 倉庫場景)
```
856 GL_MYWAREHOUSEINFO_ACK: (n11==19 才處理) 倉庫基本資訊
858 GL_MYWAREHOUSEITEMLIST_ACK (sub_4FACE0): u8 err, u8;
    err!=0 → ≤5 錯誤碼 (0x49C 訊息);
    err==0 → s32 count, s32 total, count×{s32 slot(<0 停),
    s32 item_id(需過 sub_535020), f32 f1, f32 f2, s32 period,
    u8 kind, u16 dura(複製為 dura_max)} — 與背包 28B 條目同構!
860/862 PUSH/POP_TO_WAREHOUSE_ACK: 存入/取出確認
863 GL_CHANGED_WAREHOUSEINFO_ACK (sub_4FB180): raw 0x46=70B
    倉庫狀態塊
```

### 3.15c4 訊息/喊話/物品推播 (廿二輪)
```
782 GL_RECEIVE_NEW_MSG (sub_5643C0): 新信推播 → 信箱圖示
784 GL_NEW_MSG_COUNT_ACK (sub_564480): 未讀數
837 GL_SHOUTCHAT_ACK (sub_583C20): u8 type(0/1), s32 uid,
    s32 custom_tex, str nick, s32 len, raw[len] message —
    喊話 (シャウトチャット item 15300008 觸發, 全頻廣播)
691 GL_ITEM_MODIFY_NOTIFIER (sub_55C880): s32 count, f32; count×
    {u32 flags; flags&1 → s32×2; flags&0x10 → s32...} — 物品變動
    差分推播 (期限到期/耐久歸零時 server 主動通知)
686 GL_TUTORIALINDEX_ACK (sub_55C790): 教學進度
⭐ 693 GL_TCPCONNSUCC (sub_57CAE0) = **直接呼叫 sub_555C60 = 143
    PM_UDPSTART_REQ builder**! 戰鬥伺服器握手鏈完整版:
    client 連上戰鬥 TCP → server 發 693 → client 送 143 (帶 nick +
    n100 + ext_count 回送) → server 回 144 — 與大廳 694→682→681
    完全平行的第二握手!
```

### 3.15d 連線生命週期 103/141-144 (九輪讀畢)
```
103 GE_LOGOUT_REQ (sub_58D660): 無 payload — client 登出通知
141 PM_CONNECT_REQ (sub_556530): 無 payload — 進房 TCP 握手
142 PM_CONNECT_ACK (sub_5565D0): str host, s32 port, u8, f32 —
    伺服器指示戰鬥連線目標 (client 隨即連 UDP)
143 PM_UDPSTART_REQ (sub_555C60): str nick, s32 n100 (login 681 的
    n100 原樣回送), s8 1, s32 ext_count (⚠ 十三輪定案:
    dword_231800C = dword_2318008[1] = 681 ext 塊的 count, 由
    sub_A1C870 第4參數寫入 — 即 681 的兩個值都會被 143 回送,
    可作雙重 session 驗證)
144 PM_UDPSTART_ACK (sub_555D50): u8 n108 (0=正常 1/2=模式切換
    3=踢出), u8 flag65, s32 → 1D0D23C, str(64), s32, s32, s32, f32;
    u8 flag66!=0 → {u8, u8, u8, u8, 8×s32} (延伸參數塊 →
    sub_A1C800); n108 1/2/3 各自進不同狀態機
```

### 3.15b2 房間管理/戰場雜項 (廿二輪掃畢)
```
122 GR_MAPCHANGE_ACK   (sub_56E530): u8 map_id — 房主換圖廣播
124 GR_LEAVE_ACK       (sub_5607C0): u8 result; ≠0 → u8 slot 迴圈
                       比對並移除成員 (n11==6 觀戰特判)
126 GR_CHATTING_ACK    (sub_56EA80): s32 custom_tex, u8 slot,
                       wstr message — 房內聊天 (與 120 大廳同構,
                       但以 slot 而非 nick 定位)
140 GG_EXITGAME_ACK    (sub_563430): u8 n2 (1→u8 slot 單人退場;
                       2→回房重置)
168 GR_CHANGEUSER_ACK  (sub_56F410): u16 — 房員數變更
170 GR_RULECHANGE_ACK  (sub_56F4F0): u8 rule (modeIndex!)
172 GR_WINCHANGE_ACK   (sub_56F5D0): u16 win_count
174 GR_TIMECHANGE_ACK  (sub_56F6B0): u8 time_idx
176 GR_ITEMCHANGE_ACK  (sub_56F790): u8 item_mode
184 GR_ENDLOADING_ACK  (sub_563B00): u8 n2; 迴圈 u8 slot ×2
                       (n2==2 特判) + u8 — 載入完成同步
188 GG_STARTGAME_ACK   (sub_563D60): u8 n2 (1→u8 count+slots 清單;
                       2→...) — 開戰廣播
190 GR_CHANGEMASTER_ACK(sub_56FBF0): u8 new_master_slot
                       (n0x10 比對自己 → 房主 UI 切換)
192 GR_CALLUSER_ACK    (sub_56FE10): n2==2 時 u8 slot + str nick
                       — 呼叫玩家
194 GC_CHANNEL_ACK     (sub_56FE90): u8 — 頻道確認
```

### 3.15b3 TeamHacking 駭入/炸彈協定 317-333 (廿二輪 — TH 模式核心)
```
317 GG_HACKSTART_ACK  (sub_557040): u8 team, u8 slot — 開始駭入
319 GG_HACKSUCC_ACK   (sub_557400): u8 n2 + 4×f32 (爆點座標/計時) —
                      駭入成功, 炸彈啟動
321 GG_HACKFAIL_ACK   (sub_557730): u8 — 駭入失敗
323 GG_BOMBSUCC_ACK   (sub_5579A0): u8 n2 — 爆炸成功 (回合結束)
325 GG_BOMBEND_ACK    (unknown_libname_88): — 拆除/結束
327/329/331 GG_UNHACK*(sub_557D30/557F90/558250): 拆彈三階段
333 GG_KILLJJ_ACK     (sub_561E40): u8 slot, u8 n9, u8 + [s32] —
                      擊殺 JJ (寵物/目標)
341 GR_KILLCHANGE_ACK (sub_56F870): u16 kill_target — 目標擊殺數變更
343 GG_SOLORESPON_ACK (sub_558AB0): u8 slot, u8, s16 x3 (復活點) —
                      個人重生
345/347 GX_*          (sub_58D870/58D8A0): 極短 — 心跳/回應層
```
(303-309 GG_JJ* 為 JJ 寵物系統 create/change/get/gameend — 同構
u8+slot 系列)

### 3.15c2 禮物操作 299/301/315 (廿二輪)
```
299 GS_TAKEGIFT_ACK  (sub_57AEF0 → sub_524DB0): 禮物箱分頁:
    s32 start; ≤50 條 × {s32 gift_id(-1=結束; <1024 槽上限),
    str from_nick(21B), str message(52B), s32 item_id, s32 period,
    f32} — 進 CClientData +36119 禮物陣列
301 GS_MOVEGIFT_ACK  (sub_57AFE0): u8 n2(1=收下/2=刪除), u8 n5,
    s32 gift_id(段檢 E7EF01..E7EF64), s32 count; n2==1 → count×條目
    搬進背包; n2==2 → 從清單移除
315 GS_MOVEONEGIFT_ACK (sub_57B500): u8, u8, s32, s32 item_id
    (==E975A1 特殊分支: str + f32×2 + s32 + u8 — 單件轉移含技能值)
311 GS_BUYCHAR_ACK   (sub_5728A0): u8 ok; ok → 6×s32 (角色解鎖
    +餘額組), u8 char_type, s32×2 — 買角色
313 GI_CHANGESLOT_ACK(sub_573320): 空 handler (只刷 UI) — 換槽免驗證
207 GS_BUY_WEAPONPARTS_ACK (sub_571B60): u8 ok; ok → (n11==20 特判)
    s32 gun_id, s32, u8, f32×2 + s32 balance×2 — 買改裝件
    (對應 weapon_parts_catalog 10,648 條)
```

### 3.15c 好友/訊息家族 419-441 (九輪讀畢)
```
419 GL_MSG_ADD_REQ → 420 ACK (sub_559810): str to_nick, u8 x, u8 result
    (0=成功 1=對方拒收 2=信箱滿; 讀序 str→u8→u8)
421 GL_MSG_DEL_REQ → 422 ACK (sub_55A310): u8 ok, str msg_key
423 GL_MSG_READ_REQ → 424 ACK (sub_55A4F0): u8 ok, str msg_key (與 422 同構)
429 GL_FRIEND_ADD_REQ (builder): str nick
430 GL_FRIEND_ADD_ACK (sub_55AA90): u8 result (0=成功 1..4 錯誤碼:
    重複/不存在/滿/對方拒), str nick
431 GL_FRIEND_DEL_REQ: str nick → 432 ACK (sub_55AE10):
    u8 result (0/1/2), str nick
433 GL_FRIEND_LIST_REQ: 無 payload
435 GL_FRIEND_INFO_REQ: str nick → 436 ACK (sub_55B2C0):
    u8 count, count×{str nick, u8 online(1=線上), [online: str where,
    u8 channel] } → sub_5382D0(nick, online, where, ch+1)
439 GL_FRIEND_CHAT_REQ: s32 uid(dword_F2A684), str to_nick,
    str from_nick, str message (ANSI ×3)
441 GL_FRIEND_WHERE_REQ: (查所在頻道)
```

### 3.15b 房間戰鬥流程 GR 家族 (九輪讀畢)
```
127 GR_READY_REQ  (sub_562640): 無 payload
128 GR_READY_ACK  (sub_5626D0): u8 ready_flag, u8 slot(<16) —
    以 slot 對照房間成員陣列翻 ready 狀態
129 GR_START_REQ  (sub_5627C0): u8 n125 (倒數秒/模式參數)
130 GR_START_ACK  (sub_562870): u8 result; ==1 →
    u8, s32 elapsed_ms (⚠ 十三輪更正: 是「已進行毫秒數」—
    sub_537670 存 timeGetTime()-x 當時間基準, 供中途加入同步;
    開新局送 0), u8 slot, u8, u8, u16, u8, u8 host,
    u16, u8 flags(bit0/bit1 拆開), u8, u8, u8, u8, u8 →
    寫入房間物件 (+128/+4/+105/+129/+144/+110/+109/+185...),
    然後 16×s32 (per-slot 值 → dword_F6DD1C[60195*i])
131 GR_FORCEOUT_REQ / 132 _ACK (sub_56ECC0): u8 ok; ok →
    u8 slot, [mode==2: s32, str, s32, str (兩組隊伍名)], [mode==3: ...]
133 GR_END_REQ    (sub_562E00): 無 payload
134 GR_END_ACK    (sub_562EA0): u8 result; ==1 →
    u8, u8 count, u8 slot, u8, u16, u8 host, u8, u16, u8 flags,
    u8, u16, u8, u8, u8 → 回房重置 (與 130 鏡像的房間物件更新)
135 GR_CHANGESLOT_REQ (sub_56EE90): u8 n254, u8 slot(<16)
136 GR_CHANGESLOT_ACK (sub_56EF40): u8 ok; ok → (n11 10/11 特判)
    u8 from, u8 to, s32, s32, u8 count, count×條目
```

### 3.15pre-1 「補 0/佔位」欄位審計總表 (十二輪)
| 欄位 | 判定 | 證據 |
|---|---|---|
| 198 [34..36] | 保留槽, 0 安全 | 全 exe 無讀取者 (僅複製建構) |
| 198 [27] | 任務 cond1 計數 | sub_9252D0 cond1 |
| 198 flags u8×3 (+304..306) | 閒置, 0 安全 | 讀入後無引用 (別名斷鏈) |
| 198 [28][29] (+112/116) | 閒置, 0 安全 | 僅複製建構 |
| 198 blob [52..60] | 遊玩秒+模式場次 | cond20 + sub_923BF0 |
| 681 n100 | 計費模式 id | ==100/101 → CHARGE UI (Tricod) |
| 681 ext (a,b,c) | 物品等級 gate ×2 + 隱藏物品可見 flag | sub_A1CE20 / byte_231807D |
| 681 billing ×2 | Tricod SDK session 參數 | sub_7092C0 → CTricodLog |
| 106 第三個 s32 | **exp** (顯示等級用) | sub_588560 → sub_403360 |
| 205 尾 7×s32 | PG/CASH/CP + 保留×3 + 旗標 | UI 標籤 (十一輪) |
| 120 custom_tex | 頭像貼圖 crc | CCustomTexture 快取請求 |

### 3.15pre0 198 統計欄位佈局破解 (十二輪 — 任務條件檢查器鐵證)
`sub_9252D0` (任務條件) 逐欄引用 CClientData, 加上 GP ACK 的
`sub_92EF00(事件號)` 對照, 統計欄位語意全部定案:
```
wire 群組2 = [34][35][36] (無任何讀取者 — 保留), [37]=wins(cond5),
             [38]=losses(cond6)
wire 群組3 = [39]=kills(3), [40]=deaths(4), [41]=disc(7), [42]=hearts(10)
wire 群組4 = [43]=headshots(8), [45]=double(11), [46]=triple(12),
             [44]=combos(9)   ⚠ wire 順序 43,45,46,44 — 亂序!
wire 群組5 = [47]=multi(13), [48]=ultra(14), [49]=z(15), [50]=k(16),
             [51]=dd(17)
其他: [25]=level(cond18, client 由 exp 查表 sub_403360 重算 — wire[23]
      的 level 僅參考), [27]=cond1 計數, [52]=累計遊玩秒(cond20),
      [53..60]=各模式完成場次 (sub_923BF0 模式id對照), [61..63] 未引用
```
→ **舊 C# 佈局把 wins 放 [34] 全體錯位 5 欄** — BuildMyInfoAck 已重排。
GP ACK 全域槽 (與 CClientData 分離, 只供大廳 UI):
223→EE8D34, 225→EE8D38, 227→EE8D3C, 229 winc→EE8D40("WIN"),
231 lossc→EE8D44("LOSE"), 233 killc→EE8D48+EE8DAC 差分,
235 deadc→EE8D4C("DEATH"), 237→EE8D50, 239→EE8D54, 241→EE8D58,
363→EE8D5C, 243→EE8D60, 245→EE8D64, 381..389→EE8D68..EE8D78。

### 3.15pre1 Item ID 空間全圖 (十二輪 — 「只補 0」欄位的真實編碼)
**裝備 u16 是類別內偏移, 不是完整 item_id!**
getter 群 (sub_525F10..sub_526640) 逐一還原:
`full_item_id = 類別基底 + (u16 % 100000)`, u16==0 = 空槽。
```
(十八輪以真實 21,164 條目錄逐段驗證 — 這是「外觀」12 槽, 非武器!)
wire[0]  基底 19,900,000  角色本體 (ハヤテ=1..)   store this+158
wire[1]  基底 10,000,000  髮型 (ゴスメイク風)      +159
wire[2]  基底 10,100,000  臉   (はつらつ)          +160
wire[3]  基底 10,200,000  上衣 (縞柄Ｔシャツ)      +161
wire[4]  基底 10,300,000  下裝 (カーゴズボン)      +162
wire[5]  基底 10,400,000  鞋   (ダンクハイ)        +163
wire[6]  基底 10,500,000  外套/套裝 (コート)       +164
wire[7]  基底 10,600,000  眼部 (サングラス)        +165
wire[8]  基底 10,700,000  髮飾 (花飾り)            +166
wire[9]  基底 10,800,000  臉飾 (絆創膏)            +167
wire[10] 基底 10,900,000  頭飾 (カチューシャ)      +168
wire[11] 基底 11,000,000  特殊/ヘアパズル          +169
武器不在此 12 槽 — 武器走 15.3M/15.4M 段 + 武器編組 (sub_524660)。
```
(sub_5280F0 寫回時同公式驗證 sub_535020, 逐槽失敗碼 99/1/2...)
其他已定案的 id 區段:
- 15,300,007 (E975A7) = 戰隊常數; 15,300,032 (E975C0) = 205 特判 id
- 15,301,001..15,302,000 = 204/205 可購段 A = **福袋/袋物段** (830 條:
  福袋(☆)/武器袋/ボイス袋 — AK Paper 種子恰在此段)
- 15,304,001..15,306,000 = **稱號段** (568 條) = 198 的 9×s32 稱號槽
  驗證段 (十二輪「房間武器顯示段」說法更正)
- 15,310,001..15,320,000 = 可購段 B = **合購袋段** (443 條: SOUL
  WEAPON DUO 袋等)
- 11,010,001..11,070,000 = **ヘアパズル段** (1,273 條) = 7×s32 拼圖槽
  驗證段
→ 204 直購白名單只允許「袋物」兩段 — 單品武器/外觀須經福袋開出
  或 695 GS_BUY_ONCEITEM (十九輪與真實目錄對撞定案)
→ **DB item_catalog 種子 (1001/2001) 與真實 id 空間不符**, 實用時
必須以上述區段填目錄, 否則 client 過不了 sub_535020 驗證。

### 3.15pre2 GL_CLIENTINFO (246/247) — 查看他人資料 (十一輪發現)
247 ACK (sub_573EB0): `u8 ok(==1)` → **sub_523BF0 完整基本資料塊**
(與 198 首段完全同構 — 21×欄位 + 48B blob) + **sub_524360 單角色外觀**
`u8 slot(<20), u8 char_type, 12×u16 equip` (與 198 的 sub_524010 條目
逐欄位一致, 互為交叉驗證)。n11==9 時再驅動個人資料視窗 UI。
→ 伺服器實作 247 時可重用 BuildMyInfoAck 的首段 builder。

### 3.15pre-2 客戶端狀態機 + 官方模式表 (二十輪)
**客戶端狀態 (sub_537710 set / sub_5376F0 get, byte_EE8968+24)**:
2=帳號伺服器已連(250 GL_LOBBYIN 前後), 3=商店(252), 9=大廳(198 後),
10=房內/戰鬥(GR START), 12/14/15=特殊模式(回放/教學), 4..8/16..19=
過場狀態。dispatcher 內大量 `sub_5376F0()==9/10` 分支即以此判斷
「同一 ACK 在大廳 vs 房內」的不同處理 — 佈局裡的 [n11==9]/[n11==10]
特判全部對應此狀態機。

**官方遊戲模式表 (map_StartIndex.xml — 二十輪, 正名十七輪的猜測)**:
```
modeIndex 0 = TeamDeath     (TD_, bit2)   ← 建房 111 的 u8 rule 用這套
modeIndex 1 = FreeForAll    (PS_, bit0)
modeIndex 2 = TeamHacking   (TH_, bit3)   ← 「爆破」正名: 駭入模式
modeIndex 3 = TeamSurvival  (TS_, bit1)
modeIndex 4 = TeamSteal     (TW_, bit4)   ← 「佔領」正名: 奪寶
modeIndex 8 = PNR (bit9)    9 = GunShooting  12 = SOCCER (bit14)
```
map bitmask (maplist.pat +0) 與 modeIndex 是**兩套編號**: bitmask 管
「這張圖可玩哪些模式」, modeIndex 管「這房間玩什麼」。

### 3.15pre Ping 方向 (十輪更正 — 重要!)
`GT_PING_ACK(102)` 是**伺服器→client** 的主動心跳; client 的
dispatcher case 102 → `sub_58D6F0` 立即 `ctor(101)` 回送
`GT_PING_REQ(101)` (空 payload)。101 在 client 端**沒有** builder 以外
的用途, 102 在 client 端沒有 handler 以外的用途。
→ 伺服器: 週期發 102 當 keepalive, 收 101 更新 last-seen;
**絕不可收 101 回 102** (無限迴圈)。

### 3.15a 大廳聊天/名單 (八輪讀畢)
- **119 GL_CHATTING_REQ** (廿四輪修正 — 兩變體):
  簡版 `str message`; 完整版 `s32 custom_tex, str nick, wstr message`
  — 與 120 ACK 完全同構! client 已附自己的 nick+tex, server 可
  原樣廣播 (無需重組)
- **120 GL_CHATTING_ACK** (sub_56E300): `s32 custom_tex, str nick,
  wstr message` — ⚠ 訊息用**寬字串** (sub_5927B0, UTF-16LE 雙 NUL),
  與 REQ 的 ANSI 不對稱; nick 過黑名單 sub_539320 過濾, n11==16
  (回放模式) 時整包忽略
- **116 GL_ADDUSER_ACK** (sub_56A4D0): `s32 uid, str nick` (大廳加人)
- **118 GL_DELETEUSER_ACK** (sub_56A550): `str nick` (大廳減人)

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
- **220 GI_CHANGEWP_REQ** (builder @0x573380): `u8 count, count×{u8 group_no,
  s16 equipped, [3×s16 若 group_no!=3], [8×u32 parts 若 equipped!=0]}`
  (sub_524A50 — 與 198 的 sub_524660 讀端完全鏡像; 只送有變更的編組,
  差異偵測 sub_525680)
- **221 GI_CHANGEWP_ACK** (sub_5735F0): `u8 result` + 特殊模式 10 時
  的 slot 更新通知

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
