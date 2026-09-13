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
| sub_592920,sub_5928E0,sub_592960 (寫) / sub_592900,sub_592940,sub_592980 (讀) | u8 (1B) | 1 |
| sub_5929A0,sub_5929E0 (寫) / sub_592A00,sub_5929C0 (讀) | u16/s16 | 2 |
| sub_592A20,sub_592A60,sub_592B20 (寫) / sub_592A40,sub_592AC0,sub_592A80 (讀) | s32/u32/float | 4 |
| sub_592AE0 / sub_592B00 | u64 | 8 |
| sub_5926F0 / sub_592730 | ANSI 字串 (lstrlenA+1, 含 NUL) | 變長 |
| sub_592770 / sub_5927B0 | UTF-16 字串 (2*len+2) | 變長 |
| sub_5927F0 / sub_592850 | 內嵌整個 Packet (u16 opcode + u32 size + bytes) | 變長 |

> 8 個 u8 讀取別名 (592900/940/980) 底層都是 `sub_592500(this,a2,1)`;
> 寫入別名同理 (592920/8E0/960 → `sub_592580`)。Hex-Rays 的 `char` 參數
> 只是 byte 寬度, wire 寬度以 size 為準 (u16=2B/u32=4B 亦然)。

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
完整清單見 `db/packets.tsv` (672 個唯一 ID, 100–994；GS_BASE=100)。
其中 990/991 為本輪以 UI 字串補名 (sub_9D2050 名稱表未註冊)。

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

**UDP P2P 完整協定圖 (卅輪 — 收發兩端配對)**:
```
op  送端(builder)              收端(handler)         語意
2                              sub_593A60            session 建立
4                              sub_593AB0: u8 n+     地址表廣播
                               n×{u8 uid,16B addr}
5   u8 uid, s32 tick           sub_593E60            打洞探測 →
6   u8 uid, s32 tick           sub_5940E0            探測回應
9   u8×3, s32          →10     sub_594460            中繼協商
13  u8 uid, s32        →13/14  sub_594A10/594CA0     中繼保活
15  u8×2                       sub_593DF0            短探測
17  (空)/str                   —                     keepalive
19  u8,u8,s8,u8,s32,str →20    sub_5968C0            P2P 訊息
21  u8×3, s32×3         →22    sub_5964E0: u8,u8 n,  RTT 量測
                               n×{u8 uid,f32 rtt}
32  u8,u8,u8,s32,u8,s8, →33/34 sub_594EC0/594F20:    ⭐移動同步!
    u16 x,y,z                  u8×5, u16 x,y,z       (兩型收端)
28  —                          u8×3                  狀態通知
154/158                        ping表/斷線 (七輪)
```
奇數=送 偶數=收 的 P2P 對稱設計; 32→33/34 = 位置封包 (u16 量化座標,
與 GG_DROPWEAPON 的 s16×3 同一座標系)。中繼伺服器只需在打洞失敗時
原樣轉發 — 無需解 16B addr blob。

**Y_TCP_INF 165/166 — TCP 備援戰鬥同步 (卅輪 — 第六處理層!)**:
UDP 打洞失敗時, 戰鬥事件改走 TCP 165 (client 送) / 166 (收) —
166 handler = sub_58D820 → **sub_749B90 (戰場引擎物件 1D37560,
第六個封包處理層)**:
```
166 = u8 slot, u8 subtype:
  1 = 位置心跳 (u8)          2 = 動作狀態
  3 = 射擊事件 (f32 tick, u8, u16 wp, u8×3, s32×2 ×2連發...)
  4 = 移彈/投擲 (f32, u16, s32, 6×f32 = pos+dir 向量!)
  5 = 技能 (f32, u8, s32, 6×f32)
  6 = 特殊 (f32, u16, s32, 6×f32, u8, str)
  7 = 聊天/表情 (str)   8/9 = 狀態
```
165 REQ 三變體 (17-20 欄) = 對應子型的送端 (座標 f32 全精度 —
與 UDP 32 的 u16 量化互補: TCP 精確/UDP 高頻)。
→ 私服 relay: 166 原樣轉發給房內其他人即可 (= GG 模式3)。

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
  u16     → i_23 (禮物盒 pending 數; F0C100 — 299 寫入禮物盒,
            301 收下/刪除時遞減 sub_57AFE0; 大廳禮物通知徽章)
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
相鄰 opcode (卅六輪型別定案 — 六輪的 f32 標註更正為 s32 鍵):
- **201 GL_MYPARTSUP_ACK** (sub_95A3B0): `s32 count` + count×20B
  `{s32 gun_item, s32 part_item, u8 kind, s32 val, s32 period}`
  — 武器改裝裝配表; (gun,part) 雙鍵 (比較子 sub_95A0C0) 與
  weapon_parts_catalog 結構互證
- **202 GL_EXPIRE_PARTSUP_ACK** (sub_95AE40): 同構; 逐條
  (part,gun) 進 sub_95A800 移除 = 改裝件到期拆除
額外驗證: start<=0 → 背包游標歸 0; start>=5020 → 夾到 5020; item_id 需通過
sub_535020 目錄檢查, 失敗即 sub_528960(6,...) 錯誤處理並中止本包。

**205 入帳鏈 (卅五輪)**: per-item ok 塊 → sub_534450(item, dura)
同步兩處: itemdata 目錄執行期欄 +1200/+1202 (dura cur/max) +
背包快取 28B 條目 word[18]/[20]; 再 sub_524F70 (廿一輪 28B 寫入,
kind 12/13/17 覆寫) — client 端帳目完全由 205 驅動, server 是
唯一事實源 ✓ (與 200 分頁快取一致)。

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
### 3.8 GL_USERLIST_REQ (105) / GL_USERLIST_ACK (106)
REQ 端 `sub_56A0F0`: `s8 (=1)` — client 每 ≥1 秒 (timeGetTime 差
≥0x3E8) 送一次要求刷新名單, log `L"Send UserList"`; server 直接回
106。ACK 端 sub_56A250 (四輪修正):
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
### 3.9 GL_GAMEROOMINFO_ACK (108) — sub_568CE0 (卅七輪逐欄定案):
```
u8   mode (3 = 錦標賽樹狀圖, 委派 sub_580A80; 其他 = 房間清單)
u8   count
repeat count:
  u8    room_no (需 <0xD2=210), s8 state
  state>=0: title 由 client 查字串表 state+309 (地圖預設房名);
            state<0: string title (自訂房名) — 之後皆為下列 12 欄:
    u8   cur_players   (+105; sub_44E970, 「cur/max」第一數)
    bool has_pass      (+106)
    u8   max_players   (+129; 冗餘 — client 以 +110 popcount 重算覆寫)
    u16  max_slot_mask (+110; bit 0..max-1 = 1, sub_53FB10 以 popcount
                        重算 +129 並展開 +112..+127 逐槽旗標)
    u8   game_mode     (→ sub_53FBB0 建立 CyGameModes LobbyUI, 見下表)
    bool room_type_A   (+108; sub_44E7B0 — ROOMTYPE bit)
    u8   mode_param_a  (→ mode 物件 +12)
    bool room_type_B   (+109; sub_44DA70 — ROOMTYPE bit)
    bool double_damage (+128; sub_44DBB0)
    u8   map           (+130; sub_540280/sub_540260 — 122 亦寫此欄,
                        124/125 = 特殊地圖 id)
    u8   mode_param_b  (→ mode 物件 +4, sub_74F450)
    bool no_skill_bg   (+185; sub_44E820 — NOSKILLBG)
  若 mode==2: 兩組 {s32 team_id, u32 custom_tex_crc, str(75/87) tex_name,
              u8 x} (隊伍自訂圖示, 存 room+188.., CCustomTexture 註冊)
```
房物件語義 (getter 定案): `+105=cur_players (sub_44E970)`,
`+129=max_players (sub_44E990; sub_5403F0 取 /2 為單隊上限)`,
`+110=上限槽位點陣 (popcount=最大人數; 非勝場點陣)`, `sub_44E7D0 =
+129 - +105 = 空位數`; `+106=has_pass`, `+108/+109=ROOMTYPE 兩 bit`,
`+128=double_damage`, `+130=map (sub_540260 取; 124/125 特殊地圖)`,
`+185=noskillbg`。
**game_mode 枚舉 (sub_53FBB0 factory, 0x10=16 → 原樣不改; 其他 → null):**
`0=TeamMatch, 1=IndividualSurvival, 2=DefuseBomb, 3=TeamSurvival,
 4=Steal, 5=Practice, 6=Tutorial, 7=ChattingRoom, 8=Pulp'n'Roll,
 9=GunShooting, 10=Occupy, 11=AIMulti, 12=TeamSoccer,
 13=OccupyRenewal, 15=WeaponTest`
— 與 `map_StartIndex.xml` 的 modeIndex 同源 (0=TeamDeath 1=FreeForAll
2=TeamHacking 3=TeamSurvival 4=TeamSteal 8=PNR 9=GunShooting 12=SOCCER)。
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

### 3.12e 九族精讀總表 (卅六輪 — 指定深挖)
```
【197→198 MyInfo】REQ 空 (sub_5704B0); 198 見 §3.2。
  另: 270 GL_MYINFO_OPEN (sub_556680): s8 — 個資公開開關 (單向)
【199→200 MyItem】REQ 空! (sub_570A00; client 顯示 0x66「載入中」;
  server 恆從 0 送 — 廿六輪結論三驗) ; 200 見 §3.3
【201 GL_MYPARTSUP_ACK】(sub_95A3B0): s32 count × 20B 條目
  {s32 gun_item, s32 part_item, u8 kind, s32 val, s32 period}
  — ⭐卅六輪語意定案: PARTSUP = 武器改裝(Parts-Up)裝配表!
  等鍵比較子 sub_95A0C0 = ([0],[4]) 雙鍵 = (gun,part) —
  與 weapon_parts_catalog 10,648 條 (gun,part) 結構互證!
【202 GL_EXPIRE_PARTSUP_ACK】(sub_95AE40): 同構; 逐條以
  (part,gun) 呼叫 sub_95A800 紅黑樹移除 = 改裝件到期拆除
【250→251 LobbyIn】REQ 空 ×2 builder (sub_574080 帶 state:=2 /
  sub_584FE0 純送); 251 死協定 (無 case) — server 不回 ✓
【254→255 InvenIn】REQ u8 = 倉庫頁籤 (呼叫端 v212=倉庫物件+4);
  builder 帶 state:=7 (倉庫場景)。255 (sub_574270):
  u8 mode(0/1) + mode==1:{s32 uid, u8 slot, u8} /
  mode==0:{u8, u8 slot, s32 uid} (讀序相反!) — slot 經 sub_67D870
  映射大廳走位; uid==自己(EE8CB4) 再讀 u8 n5(<5) + raw 160B
  = 5×32B 倉庫頁狀態塊
【783→784 NewMsgCount】REQ 空 (sub_5643E0); 784 (sub_564480):
  s32 count → dword_F0C104 → UI vtbl+72(count!=0) 信箱紅點
【791→792 VoiceItemSlot】REQ 空 (CVCustomizeManager::
  SendPacketMyVoiceCustomize; debug 字串洩類名!); this+289 防重入;
  792 ACK 經第五層 sub_885D00→vtbl+16 解析 (佈局=795 變體A 鏡像)
【793→794 VoiceItemSlotAll】REQ 空 (SendPacketMyVoiceCustomizeAll);
  794 = 795 變體B 鏡像: 20×{s32 char_idx, s16 base_voice, s16 x2,
  3×9×{s16 voice_item, u8 flag}} — 3 類 (command/tactics/infomation)
  ×9 句 = voice_customize_contents.xml 的 command_1..9/tactics_1..9/
  infomation_1..9 完全互證 (Extracted 實測)!
【795→796 ChangeVoiceSlot】REQ 兩變體:
  A (sub_885F10, 單角色差分): u8 char, u8 base_changed,
    [s16 voice,s16], 3×{u8 n, ≤9×{u8 slot(1..9), s16 item, u8 flag}}
  B (sub_886330, 全量): 同 794 結構
  796 ACK (sub_885E40, 卅六輪全文): u8 err, u8; err≠0 → 訊息 0x3FB
  + 續讀 792 單角色塊 (server 回滾用); this+290 pending 佇列自動重送
  — 防重入設計: 791 進行中 (this+289) 的變更先入佇列
```

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

### 3.15d2a ペパチ轉蛋機完整鏈 (廿七輪 — 場景層補完)
```
698 ENTER_PEPACHI_REQ: 無 payload (builder 由 UI 直組, 抽取器未見)
699 ENTER_PEPACHI_ACK: u8, s32, s32 (五輪已錄, CLobbyShop 層)
700 START_GAME_REQ (builder): u8 machine, s32 (投幣型式)
701 START_GAME_ACK (sub_84A000→sub_84A490 — 轉蛋動畫控制器層!):
    bool err, u8 err_code(2→訊息264, 3→...; 429 預設);
    OK → s32×2 (jackpot/餘幣), u8, u8 count(≤11),
    count×{s32 reel_a, s32 reel_b, s32 reel_c} — 11 組轉輪結果!
    count>=11 → 大獎模式 (全轉輪顯示, 15.0 秒動畫)
702 PEPACHI_LIST_REQ: 無; 703 ACK: s32, s32 n, n×f32 (機率表)
```
對應資源: Extracted/pepachi/*.swf (轉輪動畫 Flash!) —
id 格式 機台_轉輪_變體.swf; 賠率由 server 的 703 機率表控制。
handler 不在 dispatcher 也不在 CLobbyShop — 在**轉蛋動畫控制器**
(0x84A000, 第四個封包處理層!)

### 3.15d2b 推播族 NOTIFY/NOTICE 補遺 (廿六輪終掃)
```
587 CLAN_GAMEEND_RESULT: (s32×3+u16×2)×2 — 戰隊戰兩隊結算
683 GL_SERVERLIST_NOTICE: 不在 dispatcher (登入層 0x43E651 處理)
745 GG_DEFENSE_REWARD: u8+u16×2 防守獎勵
760/761 TNMT 報名成功/取消: s32 [+f32]
775 TNMT 廣播: u8+str+s32+u8+str; 779 CLAN_INFO 變更: u8+s32×2+u8+str
847/848 網咖武器: s32 開通 / u16 停用
911 GM 排程公告: str
921 AI_APPEARED_BOT: u16×5 (波次怪物出現)
934 AI_TEAMSCORE: s32+u8+s32+u8+s32×2 (協力分數板)
937 AI_FEVER_END: u8; 942 獎勵選擇開始: u8+s32+u8×2+s32×2
960 DROPWEAPON_DESTROY: u8+u16 (掉落武器消失)
994 GG_ASSISTPOINT: u8×2+s32+u8×2+s32×3 (助攻點數 → cond36 事件源!)
155/156 Y_UDP_HOLE_INF: UDP 打洞層 (sub_595E80 編號 155/156 處理)
無 payload 通知: 766/778/811/833/889/908 (純觸發)
```

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

### 3.15d5 GC_ENTERCHANNEL 195/196 — 頻道選擇完整協定 (卅三輪)
```
195 REQ (sub_56FF40; 由 CLobbyChannel 於 144 成功後自動送):
    u8 group    (頻道群組 = 681 清單 3 組之序, CLobbyChannel+129)
    u8 channel  (組內頻道編號, +131)
    u8 replay   (回放模組啟用 flag — sub_7338D0/sub_735DE0 檢查)

196 ACK (CLobbyChannel::sub_4179D0 case 196 — 不在 dispatcher!
         經 vtable 場景層分發):
    u8  result — 卅四輪全表 (sub_4177B0 十碼):
        1=成功 (417D00()[0]:=v17 頻道號回顯, state:=2);
        0=頻道滿(0xDA) 2=維護(0x148) 3=版本不符(0x328)
        6=(0x3A6) 8=(0x3A7) 4/5/7/9=一般錯誤(0x1A5)
    s32 v15      (→ sub_417D00()[1] 頻道 id)
    u8  v17      (與 result 一起進 sub_4177B0 錯誤表)
    result==1 續讀:
      str  udp_host      ⭐ UDP 打洞伺服器位址!
      s32  udp_port      (sub_58ED30 存 + sub_596E60 直填 sockaddr)
      u8   → 1D0CFE4
      u8   n2 (頻道類型; ==3 → 續讀 AI multi 大塊 sub_875680:
              s32×2, str, f32×4, u8×3, s32×2, u8×6, s32×2... —
              AI 協力頻道的關卡/波次參數!)
      f32  v11 (bit0 → byte_1D0D21B 旗標)
      u8   n5 → sub_417D00()[8] (預設 5)
```
**196 成功後的閉環 (卅四輪)**: state 119:=2 → CLobbyChannel tick
(sub_415F90) 清 CClientData + 場景切換 sub_405EB0(9=大廳/8=AI 頻道
[this+148==3]/2=回放) → CLobbyMainRoom 進場自動送 107 (房間清單) —
**頻道→大廳鏈全閉環**。196 handler 經場景 vtable (sub_407360 的
vtbl+52) 分發, 與 CLobbyShop 同層 (引用計數 1 = 純虛表呼叫證據)。

festival: 681 的 3 頻道組 ↔ 195 的 group 序號互證; 頻道類型 n2==3
= AI 頻道 (bitmask 1024 段地圖) — 與 ch_type==3 讀 extra byte
(二輪 681 佈局) 同源!

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
    PM_UDPSTART_REQ builder**! **頻道伺服器**握手鏈 (卅一輪正名 —
    使用者釐清 + PM 家族=頻道管理語意):
    client 連上頻道 TCP → server 發 693 → client 顯示 0xFF 訊息並
    送 143 (nick + n100 + ext_count 回送) → server 回 144。
    完整雙握手: **登入伺服器 = 694 GL_ACCOUNTCONNSUCC → 682 → 681**;
    **頻道伺服器 = 693 GL_TCPCONNSUCC → 143 → 144**。
    681 的伺服器清單 (host+port+3頻道組) 就是頻道伺服器的位址來源;
    登入成功後 sub_43E450 → sub_5374F0 存頻道位址 → 使用者選頻道
    即連線 → 693 觸發。
```

### 3.15d 連線生命週期 103/141-144 (九輪讀畢)
```
103 GE_LOGOUT_REQ (sub_58D660): 無 payload — client 登出通知
141 PM_CONNECT_REQ (sub_556530): 無 payload — 進房 TCP 握手
142 PM_CONNECT_ACK (sub_5565D0): str host, s32 port, u8→1D0CFE4?,
    f32→word_1D0D1F8 — host/port 經 sub_596E60 直填 UDP sockaddr。
    141 REQ 的觸發 = **UDP op 18** (sub_596300, n0x3E8_1 一次性
    latch) — 屬 UDP session 建立後的位址再確認/重連路徑。
    **頻道進入正鏈 (卅三輪定案, 取代卅二輪誤讀)**:
    connect → 693 → 143 (token) → 144 (n108=0, 雙層處理) →
    [CLobbyChannel 層] 195 GC_ENTERCHANNEL(group,channel,replay) →
    196 (result==1: **UDP host/port 在這裡!** sub_596E60) →
    UDP session 開始 (2→4→5/6 打洞) → UDP op18 → 141 → 142 (再確認)
143 PM_UDPSTART_REQ (sub_555C60; 卅三輪全鏈定案):
    str nick, s32 n100 (681 回送), s8 1, s32 ext_count (681 回送)
    — **唯一觸發點 = 693 handler sub_57CAE0** (兩個 caller: 自身
    wrapper + 693)。雙 token 可作 session 驗證 (十三輪)
144 PM_UDPSTART_ACK — **雙層處理** (卅三輪精讀):
    dispatcher 層 sub_555D50 讀:
      u8 n108, u8 flag65, s32→1D0D23C, str(64) 頻道名, s32×3, f32,
      f32/s32, u8 flag66 [flag66≠0: u8×4 + 8×s32 → sub_A1C800]
      n108≠0 錯誤碼: 1→0xA4(格式7082 重複登入) 2→0xCF(53)
      3→0x11B(54 踢出) — 各自彈窗
    第二層 CLobbyChannel::sub_4179D0 case 144 (n108==0 才會走到):
      **呼叫 sub_56FF40(group@this+129, channel@this+131) = 送出
      195 GC_ENTERCHANNEL_REQ** — 144 成功的真正下一步!
    (⚠ 卅二輪「144 尾端 ctor(141)」為誤讀 — 141 由 UDP op18
     一次性 latch 觸發 sub_596300, 屬 UDP 建立後的補充回報)
```

### 3.15b2 房間管理/戰場雜項 (廿二輪掃畢; 卅八輪補 REQ 端+設定簇)
```
121 GR_MAPCHANGE_REQ   (sub_56E480): u8 map_id — 房主換圖請求
122 GR_MAPCHANGE_ACK   (sub_56E530): u8 map_id — 寫 room+130 (map,
                       sub_42FC50→sub_540280); 房主換圖廣播
124 GR_LEAVE_ACK       (sub_5607C0): u8 result; ≠0 → u8 slot 迴圈
                       比對並移除成員 (n11==6 觀戰特判)
125 GR_CHATTING_REQ    (sub_56E860 wstr 版; sub_56E6C0 str 版為死碼
                       — 無呼叫者): s32 uid(dword_F2A684), u8 slot,
                       wstr message — 房內聊天 (a3!=0 走本地 echo)
126 GR_CHATTING_ACK    (sub_56EA80): s32 uid(讀後丟棄), u8 slot,
                       wstr message — 以 slot 定址顯示 (與 120 大廳
                       同構, 但以 slot 而非 nick 定位)
139 GG_EXITGAME_REQ    (sub_560720): 空 payload — 玩家離開對戰回房
140 GG_EXITGAME_ACK    (sub_563430): u8 n2 (1→u8 slot 單人退場;
                       2→回房重置)
—— 房設定簇 (REQ=UI 變更送端 / ACK=dispatcher 收端寫入房物件) ——
167 GR_CHANGEUSER_REQ  (sub_56F360, UI sub_4325A0): s16 slot_mask
168 GR_CHANGEUSER_ACK  (sub_56F410→sub_4325D0): s16 slot_mask —
                       寫 room+110=上限槽位點陣 與 room mgr +214,
                       sub_53FB10 重算 +129=popcount(最大人數)
169 GR_RULECHANGE_REQ  (sub_56F440, UI sub_42FE20): u8 mode
170 GR_RULECHANGE_ACK  (sub_56F4F0→sub_42FE50): u8 mode (modeIndex) —
                       sub_53FBB0 重建 mode UI(+132), 再由 mode 設定表
                       (sub_426930) 回推 map 寫 +130, 並重繪 USERSLOTS
171 GR_WINCHANGE_REQ   (sub_56F520, UI sub_4306E0): s16 win_count
172 GR_WINCHANGE_ACK   (sub_56F5D0→sub_430720): s16 win_count — room+144
173 GR_TIMECHANGE_REQ  (sub_56F600, UI sub_4308F0): u8 time_idx
174 GR_TIMECHANGE_ACK  (sub_56F6B0→sub_430920): u8 time_idx — room+136
175 GR_ITEMCHANGE_REQ  (sub_56F6E0, UI sub_430AF0): u8 item_mode(2bit)
176 GR_ITEMCHANGE_ACK  (sub_56F790→sub_430D50): u8 item_mode — bit0→
                       sub_74F450(mode+4), bit1→sub_74F430(mode+8)
177 GR_AUTOCHANGE_REQ  (sub_56F8A0): s8 — 無呼叫者 (僅 builder 存在)
178 GR_AUTOCHANGE_ACK  : 註冊表有, 但不在 sub_58B010 主 switch
                       (走 vtable 前置轉發器, 場景層處理)
340 GR_KILLCHANGE_REQ  (sub_56F7C0, UI sub_430F20): s16 — 與 171 同送
341 GR_KILLCHANGE_ACK  (sub_56F870→sub_430F50): s16 — room+148
364 GR_BALANCECHANGE_REQ (sub_56FA30, UI sub_431130/432170): u8(1)
365 GR_BALANCECHANGE_ACK (sub_56FAE0→sub_431160): u8 — 寫
                       GAMEROOM_TEAMBALANCE UI (room 欄位不變)
368 GR_TEAMSHUFFLECHANGE_REQ (sub_585CE0): u8 — 房主切隊打散開關
                       (發送前置 sub_435480: *(this+112)=27 後送)
369 GR_TEAMSHUFFLECHANGE_ACK (sub_585D90→sub_4354B0): u8 — 寫
                       mode rule 物件 +13 並 sub_436730 重繪 USERSLOTS
894 GR_TEAMSHUFFLE_REQ  (sub_585DC0): u8 room_no, u8 map — 房主執行
                       隊打散 (發送前置 sub_435520: 房主檢查 + mode rule
                       vtable+28 非零 + sub_437060 取目前地圖後送出)
895 GR_TEAMSHUFFLE_ACK  (sub_585E70→sub_435680): u8 status,
                       u16 (client 讀後丟棄, 卌三輪定型: case 1 裡
                       sub_592A00 讀入 v35[6] 後無任何引用 — 純保留欄
                       送 0), u8 count,
                       count×(u8 slot, u32 uid; ⚠ 卌三輪以原語表定型
                       sub_592940=u8 讀 slot、sub_592A40=u32 讀 uid) —
                       status 1=逐槽重排
                       (dword_F3312C 比對 uid, dword_F6DCF4 寫新 slot,
                       sub_4360B0 更新 USERSLOTS; 若 uid=自己則
                       sub_537610 記我的新 slot); 2=shuffle 忙碌
                       (*(this+344)=1+sub_436E70); 錯誤碼語意
                       (msgtableres.lang 解碼, 卌三輪):
                       3/4/9/11→0x4B5「チームシャッフルに失敗しました」
                       5/10→0x87「全員がレディー状態になってから
                       スタート可能です」6→0x4B6「3人以上必要」
                       7→0x178「権限がないためこのメニューは利用
                       できません」8/15/16→0x4B7「支援しないモード
                       です」12→0x2CE「移動する事が出来ません」
                       13→0x111「２つのチームに分かれてください」
                       14→0xD9「定員オーバーです」
366 GR_LOCALROOM_REQ   (sub_585FD0): u8, 僅 n2_0!=3 送 — 區域限定房
                       (n2_0==3 為錦標賽場景 dword_EA0F30, 一般房為
                       dword_EA10D0 — 366/969 只存在於一般房;
                       名稱表未註冊, 由 GAMEROOM_LOCALROOM UI 字串補名;
                       server 現已實作同 990/991 之例)
367 GR_LOCALROOM_ACK   (sub_586090→sub_437B50): u8 — 勾選
                       GAMEROOM_LOCALROOM
969 GR_SOCCER_REQ      (sub_5860C0): u8, 僅 n2_0!=3 送 — 足球模式開關
970 GR_SOCCER_ACK      (sub_586180→sub_437D00): u8 — 寫 mode rule +14
                       (sub_74F4D0) 並勾選 GAMEROOM_SOCCER
712 GR_NOSKILL_REQ     (sub_56FB10, UI sub_431290): u8
713 GR_NOSKILL_ACK     (sub_56FBC0→sub_4312C0): u8 — room+185
                       (noskillbg) + GAMEROOM_{NORMAL,CLAN}_NOSKILL UI
728 GR_OBSERVERCHAT_REQ(sub_56E560): wstr sender, wstr message
729 GR_OBSERVERCHAT_ACK(sub_56E610): wstr sender, wstr message —
                       觀戰者聊天 (sub_431EC0 顯示, 錦標賽 sub_478CF0)
990 GR_DAMAGEROOM_REQ  (sub_56F950, UI sub_430FA0): u8 — 房主切換
991 GR_DAMAGEROOM_ACK  (sub_56FA00→sub_430FD0): u8 — room+128
                       (double_damage) + GAMEROOM_DAMAGEROOM UI
                       (sub_9D2050 名稱表未註冊; server 以 UI 字串
                       補名 GR_DAMAGEROOM_* 入 db/packets.tsv)
184 GR_ENDLOADING_ACK  (sub_563B00): u8 n2; 迴圈 u8 slot ×2
                       (n2==2 特判) + u8 — 載入完成同步
188 GG_STARTGAME_ACK   (sub_563D60): u8 n2 (1→u8 count+slots 清單;
                       2→...) — 開戰廣播
190 GR_CHANGEMASTER_ACK(sub_56FBF0): u8 new_master_slot
                       (n0x10 比對自己 → 房主 UI 切換)
191 GR_CALLUSER_REQ    (sub_56FD60): str nick — 呼叫指定玩家
192 GR_CALLUSER_ACK    (sub_56FE10): n2==2 時 u8 slot + str nick
                       — 呼叫玩家
194 GC_CHANNEL_ACK     (sub_56FE90): u8 — 頻道確認
```

**房物件 (CLobbyGameRoom) 欄位總圖 — 四十一輪逐欄定案** (wire 序經
sub_568CE0/sub_53F830/sub_53F920/sub_53F9F0 三 ctor 交叉驗證):
```
+4   room_no                     +105  cur_players (108 送 cur)
+5..104 title (100B, qmemcpy)    +106  has_pass
+107  1 (=active 房)             +108  room_type_A (ROOMTYPE bit)
+109  room_type_B (ROOMTYPE bit) +110  max_slot_mask (u16, bit0..max-1)
+112..127 逐槽旗標 (sub_53FB10)  +128  double_damage (990/991)
+129  max_players = popcount(+110)  (108 的 max 欄為冗餘, 被重算覆寫)
+130  map (sub_540280; 122 換圖亦寫)  +136 time (173/174)
+144  win_count (u16, 171/172)   +146  mode param (u8, wire 114/130/134/
+148  kill_count (u16, 340/341)        309 送; client 存而不讀 — 送 0 安全)
+150  mode param (u8, 同 +146,   +185  no_skill_bg (712/713)
      client 存而不讀 — 送 0 安全)
+186  team_balance (僅錦標賽 ctor sub_53F9F0 寫; 一般房 364/365
      只切 GAMEROOM_TEAMBALANCE UI)
+33   mode LobbyUI 物件 (sub_53FBB0 建, modeIndex 0..15)
+132  mode rule 物件 (16B, 有 vtable — 卌二輪逐欄定案):
        +4  = item bit0 (sub_74F450; 175/176)
        +8  = item bit1 (sub_74F430; 175/176)
        +12 = rule param (u8, wire 直寫; 112 本地初始化以 sub_438990
              「是否隊伍房」寫 1/0 — server 側語意仍待原服確認)
        +13 = 隊打散開關 (u8; 368/369)
        +14 = 足球旗標 (u8; sub_74F4D0 寫/sub_74F4B0 讀; 969/970)
```

**112 GL_MAKEROOM_ACK 補完 (卌二輪 — sub_56A7B0 重讀, 先前 6 欄漏了尾 9 欄):**
```
u8 err(0=OK), u8 room_no(<210), u16 slot_mask, s32 room_uid,
u8 +185 no_skill_bg, u8 mode+13 隊打散            ← 6 欄恆送 (err!=0 亦然)
[err==0:] u8 team_mode(2=隊伍房),
2×{u32 team_uid, u32 team_crc, str team_name, u8 team_flag}
```
後 9 欄 err==0 時 client 無條件讀取 (team_name 走 sub_592730 直到
NUL), 故 server 必送 — 新房間 = team_mode(2 若 mode∈{0,2,3,4,8,10,
11,12,13}, 否則 0) + 兩組空隊伍 (uid/crc 0, 空字串, flag 0)。
模式變更 (169/170) 後 client 以 mode 設定表 `sub_426930(mode)` 回推
預設地圖寫 +130 (sub_540280)。server 現已鏡像 (RoomHandlers.ModeDefaultMap,
client 實際載入的 `system/map_StartIndex.xml` — ⚠ ui/ 根目錄另有一份
舊版 modeStartIndex 不同, 以 system/ 為準): 0→106 1→104 2→14 3→107
4→23 8→51 9→89 12→98; 其餘 mode (5/6/7/10/11/13/15/16) 無條目 → 保留原圖。

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
    u8 mode+14 (足球隊旗 sub_74F4D0, 969/970; ⚠ 卌三輪確認是
    這個欄位不是 mode), s32 elapsed_ms (⚠ 十三輪更正: 是
    「已進行毫秒數」— sub_537670 存 timeGetTime()-x 當時間基準,
    供中途加入同步; 開新局送 0), u8 room_no(sub_407E80 定址房物件),
    u8 cur_players(+105), u8 max_players(+129 冗餘, client 以 +110
    popcount 重算), u16 max_slot_mask(+110), u8 map(+130),
    u8 mode(→sub_53FBB0), u16 (+144), u8 flags(bit0→mode+4/bit1 拆開),
    u8 mode+12, u8 +109, u8 mode+13, u8 +185, u8 +128 →
    寫入房間物件, 然後 16×s32 (per-slot 值 → dword_F6DD1C[60195*i])
131 GR_FORCEOUT_REQ / 132 _ACK (sub_56ECC0): u8 ok; ok →
    u8 slot, [mode==2: s32, str, s32, str (兩組隊伍名)], [mode==3: ...]
133 GR_END_REQ    (sub_562E00): 無 payload
134 GR_END_ACK    (sub_562EA0): u8 result; ==1 →
    u8 map(+130), u8(讀後丟棄), u8 room_no(sub_407E80 定址),
    u8 max_players(+129 冗餘), u16 max_slot_mask(+110, 回房恢復),
    u8 mode(→sub_53FBB0), u8(+136), u16(+144), u8 flags,
    u8(+146), u16(+148), u8(+150), u8 mode+12, u8 +109, u8 mode+13 →
    回房重置 (與 130 鏡像的房間物件更新)
135 GR_CHANGESLOT_REQ (sub_56EE90): u8 n254, u8 slot(<16)
136 GR_CHANGESLOT_ACK (sub_56EF40): u8 mode; mode!=0 → 失敗音效(不再讀 body);
    mode==0 → u8 from(讀後丟棄), u8 new_slot, s32(讀後丟棄), s32 mover_uid,
    u8 count, count×(u8 slot, s32 uid) — 全房依 uid 對位重建 F6DCF4
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
- **112 GL_MAKEROOM_ACK** (sub_56A7B0): `u8 err, u8 room_no(<210),
  u16 max_slot_mask(+110), s32 room_uid, u8 no_skill_bg(+185),
  u8 mode+13` + err==0 時: `u8 n2, {s32 team_id, s32 tex_crc, str,
  u8}×2 (mode==2)` — 建房成功即以自己為房主初始化房間物件
  (sub_53F920: +105=1 自身、+106=0 無密碼、+110 上限槽位點陣、
  +136=3、+144=7/10, 其餘取自 client 建房時自存的 rule/mode 全域)
- **113 GL_ENTERROOM_REQ**: `u8 room_no` (單欄位)
- **114 GL_ENTERROOM_ACK** (sub_56B360, 卅七輪逐欄):
  `u8 sub_type` + 0=失敗回大廳;
  ==1 單人進房通知: `s32 uid, u8 slot, str nick, s32 exp(level 由 client
  查表), u8 char_type, 成員負載`;
  ==2 完整房間狀態: `s32 room_uid, u8 map(+130), u8 count, u8 room_no,
  u8 max_players(+129 冗餘), u16 max_slot_mask(+110),
  u8 mode(→sub_53FBB0), u8(+136), u16(+144), u8 flags(bit0→mode+4),
  u8(+146), u16(+148), u8(+150), u8 mode+12, u8(+109), u8 mode+13,
  u8(+185), u8(+128), u8 mode+14` +
  count×成員條目 {s32 uid, u8 slot, str nick, u8 crown, u8 status,
  s32 exp, u8 char_type, u8 observer(1=OB 簡版, 不再讀負載), [負載]};
  成員負載 = `u8 角色槽 0..0x13, u8 char_type, 12×u16 equip (sub_524360),
  s32 custom_tex, s32 tex_crc, str tex_name, 武器組×4 (固定四組:
  u16 equipped, [3×u16 sub 若組≠3], [8×s32 parts 若 equipped≠0]),
  u8 extra_flag([8×s32] 若≠0), 9×s32 技能 (sub_527550),
  u8 n5 + 7×s32 快速槽 (sub_527D00)`;
  ==3: 同 ==1 的單人更新 (以 slot 定址)
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

## 3.99 廿六輪終極盤點 — 670 opcode 全分類收官
```
✔ dispatcher 直讀     300 條 (LAYOUTS.md 自動表)
✔ REQ builder         261 條 (LAYOUTS_REQ.md 自動表)
✔ 場景 vtable 層      699/703/707/807/809 (CLobbyShop), 719-723
                      (IVotingNetwork), 788 (sub_407360)
✔ 登入層 0x43E651     681/694/882
✔ UDP 層 sub_595E80   2-34 私有編號 + 153-164 + 155/156 HOLE_INF
✘ 真·死協定 ~80 條    無 builder 無 parser 無別層引用:
   PM_MASTER/ID/LOGOUT/CH_SERVER (145-152 舊版中控殘留),
   GR_STARTTIME/AUTOCHANGE/CRYSTAL 系 (棄用模式),
   GV_VIEWER 組 560-570 (外部觀戰工具協定, client 不實作),
   GS_STOREOK/NEWGIFT/HUKUBUKURO/PRESENTPACKAGE (棄用商店流程),
   SECURITY_AHNLAB/NPGAMEGUARD (韓版安全模組, 日版不用),
   MASTER_TEST/UPITEM 等 GM 殘留, *_BASE 佔位 (100/560/580/680)
→ 私服無需理會死協定; 622 條活協定全部有佈局/序列記錄。
```

## 3.98 CClientData 記憶體總圖 (廿七輪彙整 — 歷輪碎片權威版)
byte 偏移 (this 為物件基址):
```
+4     u8   slot_current (198 尾段寫)
+60    char nick[24]     (wire str)
+88    u8   char_type    (wire u8; 1..14 = ICT_* 角色)
+92    s32  [23] wire level (參考值)
+96    s32  [24] exp
+100   s32  [25] level ← client 由 exp 查表 sub_403360 重算
+104   s32  [26] cash
+108   s32  [27] 任務 cond1 計數
+112/116 s32 [28]/[29] 閒置
+136..144 s32 [34..36] 保留 (無讀取者)
+148   s32  [37] wins    (任務 cond5)
+152   s32  [38] losses  (cond6)
+156..168 s32 [39..42] kills/deaths/disc/hearts (cond3/4/7/10)
+172   s32  [43] headshots (cond8)
+176   s32  [44] combos  (cond9; wire 亂序: 43,45,46,44)
+180/184 s32 [45]/[46] double/triple (cond11/12)
+188..204 s32 [47..51] multi/ultra/z/k/dd (cond13..17)
+208   48B  [52..63] 遊玩秒(cond20)+模式場次[53..60]
+304..306 u8×3 閒置旗標
+313   u8   角色槽數
+314   20×26B 角色槽 {u8 slot, u8 type, 12×u16 外觀偏移}
       (u16 = full_id - 類別基底; 基底表見 §3.15pre1)
+628   13×u16×20 = wire 讀入鏡像 (sub_524010 的 157+13i 區)
+840   5120×28B 背包 {flags,id,f1,f2,period,kind,dura×2}
+36117 s32  禮物數; +36119 1024×23B 禮物條目
+36095 區   9×s32 稱號槽 (sub_527550)
+144201 u8  武器編組數; +144204 4×44B 編組
       {u8 no, u16 equipped, 3×u16 sub, 8×u32 parts}
+144420 28B 快速槽 7×s32 ヘアパズル (sub_527D00)
+144452 u8  n5 拼圖參數
```
封包處理層全圖 (五層): ① dispatcher sub_58B010 (306 case)
② 場景 vtable sub_407360→CLobbyShop 等 ③ 登入層 0x43E651
④ 轉蛋動畫控制器 0x84A000 (701) ⑤ 語音 vtable sub_885D00
(792/794/796) + UDP 層 sub_595E80。

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
