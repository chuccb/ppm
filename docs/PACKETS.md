# PaperMan 網路協議完整分析（native wire evidence）

> **閱讀導覽。** 這份文件保存欄位級、consumer 級與 state 級的手工證據；先由
> [`docs/README.md`](README.md) 判斷它與 `LAYOUTS*.md`、`RESOURCES.md`、
> `TODO_HANDLERS.md` 的不同角色。`LAYOUTS*.md` 是自動 primitive inventory，
> 不能代替此處的 optional branch、count framing 或 service-policy boundary。
>
> 本檔保留歷史 section number，因為其他文件與 commit 會引用它；數字標題因此
> 不一定是閱讀順序。新增結論放到對應 protocol family，並附 native builder、
> reader/consumer、field/state data flow、confidence 與 unresolved limit；不要
> 僅因資源或 opcode 名稱存在就推導 server policy。
>
> **目前 server-ts 31 個 Packet 的逐欄 implementation audit**：見
> [`SERVER_TS_PACKET_FIELDS.md`](SERVER_TS_PACKET_FIELDS.md)。該文件把 native
> wire meaning、TS 實際用途、zero projection 與 `UNRESOLVED` 欄位分開，並記錄
> 2026-09-17 的 198/247 reserved/stat projection 修正。

> **雙向 layout cross-check（native ↔ current TS）**：
> server-ts ACK 寫入序列 ↔ client 讀取序列: 18/18 ✓;
> server-ts REQ 讀取序列 ↔ client 寫入序列: 27/27 ✓ (5 個機械標記經人工
> 複核均為變體混列/raw4≡s32/子函數未展開等誤報)。
> 附帶確認: 199 GL_MYITEM_REQ client 端**不送 start 欄位** —
> server 恆從 0 開始送背包 (server-ts remaining guard 已天然正確)。

> **符號漂移清理（本輪）：33 → 6 個真正未解。**
> 機器掃描發現本文件引用的 673 個 `sub_` 符號中，曾有 **33 個在新舊兩份
> `PaperMan.exe.c` 皆查無此函式** —— 屬更早期 IDA session 遺留的漂移，
> **與 2026-09 的新 dump 無關**（兩份都沒有）。
> `LAYOUTS.md`（311 個）與 `LAYOUTS_REQ.md`（17 個）因為是自動抽取的，
> **全部可對應**。
>
> 本輪以**追 `Packet(<opcode>)` builder** 重新定位，
> opcode 表格中的 **28 個獲得唯一解並已更正**，例如
> `310 GS_BUYCHAR_REQ` 由 `sub_529680` → `sub_572790`、
> `416 MASTER_KILLALL_REQ` 由 `sub_579DB0` → `sub_57C500`。
> ⚠ **不可用位址接近來猜**：214 的正解 `sub_572EB0` 與舊記 `sub_532AA0`
> 相距 `0x40410`，純靠 builder 追蹤才找到。
>
> **剩餘 6 個維持未解，並已就地標註 `⚠符號已漂移`：**
> `sub_523A00`／`sub_532AA0` 現在只出現在本節與更正說明中（正解已寫進對應列）；
> `sub_554E00`、`sub_593260`、`sub_ADC240` 是內文敘述引用、無 opcode 可反查；
> `sub_582530` 對應的 **690 `GL_TUTORIAL_INDEX_SET_ACK` 已改標 UNRESOLVED**
> —— 該名稱雖在 `sub_9D2050` 註冊，但 dispatcher 無 case 690、
> `LAYOUTS.md` 亦無此列，本 revision 找不到任何讀取器。
>
> 符號漂移**不影響欄位結論**（那些出自自動抽取的 layout 表與實測），
> 只影響「拿符號回 dump 查證」的可行性。
> `verify_dispatcher_coverage.py` 鎖定「本文件出現的漂移符號總數 = 8」
> （6 個未解 + 本節作為對照範例保留的 `sub_529680`／`sub_579DB0`），
> 防止再度累積。

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
| sub_5929A0,sub_5929E0 (寫) / sub_592A00,sub_5929C0 (讀) | raw2; caller may treat it as u16/s16 | 2 |
| sub_592A20 / sub_592A40 | raw4; caller may treat it as s32/u32 or another 4-byte value | 4 |
| sub_592A60 / sub_592A80 | raw4; this alias does not itself establish unsigned semantics | 4 |
| sub_592B20 / sub_592B40 | raw4; float semantics require a native float caller | 4 |
| sub_592AC0 | raw4; semantics come from its caller (142 calendar / 144 request context / 196 client flags are raw4 uses) | 4 |
| sub_592AE0 / sub_592B00 | raw8; 682 guard is a caller-defined u64 projection | 8 |
| sub_5926F0 / sub_592730 | ANSI 字串 (lstrlenA+1, 含 NUL) | 變長 |
| sub_592770 / sub_5927B0 | UTF-16 字串 (2*len+2) | 變長 |
| sub_5927F0 / sub_592850 | 內嵌整個 Packet (u16 opcode + u32 size + bytes) | 變長 |

> 8 個 u8 讀取別名 (592900/940/980) 底層都是 `sub_592500(this,a2,1)`;
> 寫入別名同理 (592920/8E0/960 → `sub_592580`)。`sub_5929A0/9E0` 的
> decompiler 參數雖顯示 `char`，函數實作仍從該參數位址複製 2 bytes；不可
> 只用 Hex-Rays 參數型別決定 signedness。所有 2/4/8-byte aliases 同理，
> wire 寬度以函數內的 `sub_592500/sub_592580` size 為準，語意必須回到 caller。

**primitive implementation boundary（`PaperMan.exe.c` 00592500–00592B80）：**
`sub_592500` 在 read cursor + requested size 超過 packet payload end 或
allocated end 時回傳 0 且不 advance；成功才 `memcpy` 並 advance。`sub_592580`
在 write cursor + size 不超過 allocated end 時才 copy、advance 並增加 packet
payload size。這是 helper-level boundary，不等於整個 native reader fail-closed：
108、426、434 等 reader 呼叫這些 helper 時沒有逐次檢查回傳值；上層 packet
stream framing 仍是另一層責任。`sub_5926F0/sub_592730` 則以 ANSI NUL
字串的 `lstrlenA()+1` 決定長度，因此字串語意與固定 buffer 上限仍須由各
caller/consumer 分別驗證。

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
     sub_4042A0(key_schedule, buf, n16, n2_4)   n2_4=1 CBC / 2 128-bit CFB / 0 ECB；
     封包路徑固定傳入 n2_4=2（CFB-128, IV=0）
     word2 := 加密前 word0 (**(WORD**)(this+16)), word0 := n16, flag|=4
     ⚠ word2 由且僅由 AES 層寫入
4. WSASend(this+24, word0 + 8); 加密失敗 → 客戶端 ExitProcess(0)!
5. send_count++ (sub_593260 ⚠符號已漂移, InterlockedIncrement)
```

**接收 (sub_555280 / sub_554E00 ⚠後者符號已漂移 — event loop):**
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
  但長度來自 recvfrom 而非累積 buffer，且它沒有 TCP 的 LZ 階段；empty
  logical payload remains one encrypted 16-byte block. See the UDP-private
  evidence boundary below rather than applying the TCP pipeline wholesale.

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
- 模式 (sub_4042A0 / sub_404470 的 n2 參數):
  - `n2 = 1`: CBC 模式 (IV 初始為全零)
  - `n2 = 2`: **128-bit CFB 模式** (IV 初始為全零, sub_403DE0 加密 IV 後與資料 XOR, 並以密文回授作為下一輪 IV)
  - `n2 = 0`: ECB 模式
- 封包路徑 `sub_592FB0`/`sub_593110` 傳入 `n2_4 = 2` → **AES-128-CFB (128-bit feedback, IV=0)**。
- ⚠ **兩個 CFB 測試向量互相矛盾，已證實至少一個有誤（Bun server 實作時發現）：**
  ```
  舊記 CFB(key, IV=0, 000102030405060708090A0B0C0D0E0F) = 3A736DBF81F4BA1AF40854FBF4E13F47
  舊記 CFB(key, IV=0, "PaperMan-Packet!")               = 60912185D998DDD7F70F57FCC48B3079
  ```
  CFB 首個區塊的 keystream 為 `AES(IV)`，**與明文無關**，故兩式反推的 keystream
  必須相同。實測：
  ```
  ks = c ^ p   由向量1 → 3A726FBC85F1BC1DFC015EF0F8EC3148
              由向量2 → 30F051E0ABD5BCB9DA5F369FAFEE4458   (僅 1/16 位元組相同)
  ```
  **兩者不可能同時成立。** 正確的 keystream 由本專案獨立實作的 AES-128 算出：
  ```
  AES(key, 00×16) = 3AF35BF885F6BC18FF0B59F8DFEC3248
  ⇒ CFB(key, IV=0, 000102030405060708090A0B0C0D0E0F) = 3AF259FB81F3BA1FF70253F3D3E13C47
  ⇒ CFB(key, IV=0, "PaperMan-Packet!")               = 6A922B9DF7BBDD76D25B389BB4894669
  ```
  該實作**通過 FIPS-197 C.1 附錄向量**，且**兩個 ECB 向量逐位相符**
  （`D7F8930C...` 與 `8B8ABD9B...`），故 AES 核心與金鑰均無誤 ——
  出錯的只有上面兩行 CFB 期望值。**以本節新值為準**，
  驗證見 `server-ts/test/aes.test.ts`。

**壓縮門檻協商 + 登入觸發 (694 的雙重功用, 十一輪定案)**:
全域 `n0x2580` 初始 0x2580(9600, 即「從不壓縮」)。
`GL_ACCOUNTCONNSUCC(694)` 攜帶一個 u16，**只有嚴格小於** `0x2580`
才覆寫門檻 (0x43E651 的 `n694==694` 分支)；`0x2580` 或更大值都被
client 忽略並保留 9600。這是 client 的消費行為，不是 server 可用值的
wire 限制：TS 只驗證值能放進 u16 並原樣寫出，包含 client 會忽略的值。
預設值仍是 `0x2580`，以維持雙向不壓縮；若 server 選擇低於 9600 的門檻，
`PacketCodec` 必須使用同一個門檻，否則 9600..server-threshold 範圍的
LZ frame 會無法正確解壓。
⚠ 更關鍵的第二功用: 讀完門檻後緊接呼叫 `sub_43DF00` = **682 登入
REQ 的 builder** (帳密欄位先驗證 sub_43DD60: 只允許 [0-9A-Za-z@],
非法則顯示 0xE1 訊息不送包)。所以 **694 是登入流程的觸發器**:
連線建立 → 伺服器發 694 → client 送 682 → 伺服器回 681。
私服: 連線時發一次 694 (門檻 0x2580=停用壓縮最穩), 登入後**不可**
再發 (client 會重送 682 → 無限迴圈)。

**GL_LOGIN_ACK(681) 完整結構** (0x43E651 同函數 `n694==681` 分支):
```
raw4 result           native 以 raw 4B 讀入，但分支只檢查 low byte；
                      low byte 1=成功、0=一般失敗、2=帳密錯(0x42)。
                      成功/失敗封包均應完整寫一個 s32 word；0xC8..0xD6
                      是低 byte 的逐碼 failure UI：
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
  s32  n100           原樣保存，143 會回送；100/101 也會開啟 client 的
                       CHARGE UI。後端業務名稱未能由 native code 確認，
                       應視為 opaque billing/charge UI mode，不是「玩家等級」
                       或 route token。
  s32  ext_count      0 或負值 = 不讀 extension；>0 時 client **只讀
                       一組** `s32 first, s32 second, u8 feature_flag` 再交給
                       sub_A1C870。它是 native positive gate，不是 tuple count；
                       count/gate 存在 dword_231800C，非零會影響多個
                       *_NETCAFE UI gate。TS production 預設仍送 0；只有持有
                       官方可重現設定時，才可透過 raw extension API 送 exact
                       gate + one tuple，不替三欄臆造業務名稱。
  raw2 server_count                   (native loop gate; signedness unresolved)
  repeat server_count:                 ← 伺服器清單
    raw2 server_id                     (domain/signedness unresolved)
    str  name  (ANSI; native char[50]，內容最多 49 bytes)
    str  host  (v124 char[16]，內容最多 15 bytes)
    raw2 server_port                   (sub_58AD90 取此 2-byte field，作為
                                       sub_554810 的 u_short TCP endpoint port)
    u8   flag                          (意義尚未確定)
    raw2 group                         (2-byte wire field; domain/signedness unresolved)
    repeat 3:                          ← 每台固定 3 個頻道分組
      raw2 max_users                   (native positive gate；UI `USERS` 的
                                       分母/容量，不是 record count)
      若 max_users > 0 (native 只讀一個 channel record):
        u8   ch_type
        str  ch_name                   (char[50]，內容最多 49 bytes)
        raw2 current_users             (UI `USERS` 的分子；不是 network port)
        u8   ch_flag                   (意義尚未確定)
        若 ch_type==3: u8 extra
  s32  billing_first, billing_second   (v142,v137；後續以 raw s32 進入 Tricod
                                       argument block，業務名稱未知，非 u32)
```

681 的兩個「看起來像 port」欄位並不重複。`server_port` 是真實的
server-selector TCP endpoint：`sub_58AD90` 將 projection 的 `+102` host 與
`+118` field 傳給 `sub_554810(SOCKET*, char*, u_short)`，後者直接建構
`AF_INET/SOCK_STREAM` sockaddr 並呼叫 `WSAConnect`。相反地，channel record
的 `+122` field 會在 `sub_416DA0` 中與 `+124` 一起格式化為 `"%d/%d"`，
寫入 UI 欄位 `USERS`；`sub_4176C0` 也用 `max_users <= current_users` 判斷
滿載。因此 channel wire field 應稱 `current_users`，不可再叫 `port`。

讀取器仍只用 `sub_5929C0` 證明所有這些欄位是 2 bytes；server endpoint 的
consumer 另證明其有效解讀為 unsigned `u_short`。`server_id`、`group`、
`max_users`、`current_users` 的 wire signedness 不由 helper 單獨確定，TS
對後三者只發送對應 raw2/s16 bits，不將它們誤當成 endpoint。

681 的 server-list loop 會把 wire fields 讀入多個 local scratch，再於每次
有 channel record 時呼叫 `sub_58E690(byte_13242F8, src)`。`src` 從
`server_id` 的 scratch 開始，native vector helper 會以此位址複製固定 132
bytes；`sub_58E670` 依 projection 首 byte 排序，`sub_58E640` 另依
projection offset 129 排序。這是 native 的 stack-layout projection/lookup
context，不是額外的 132-byte wire field。官方資源 `Extracted/ui/cfg/pm_lobbydata.dat`
只提供 `LOBBYCHANNEL` 的 UI layout，沒有 endpoint schema；PaperManWiki 的
[遊戲起動編](https://wikiwiki.jp/paperman/ひよこ用/ゲーム起動編) 只確認
「server selection」與 channel/遊玩風格的階層，也沒有 port 欄位命名。故
本段的 endpoint/USERS 命名以 native producer/consumer 為準；後續 144→195 的
`sub_4179D0`→`sub_56FF40` 另以 projection `+129/+131` 交叉確認 `ch_type`
與 `ch_flag`，而 type-3-only projection `+130` 只在 `sub_416DA0` 的 UI/state
switch 出現。`flag`、`group` 與 billing fields 仍維持 UNRESOLVED；TS 不重建
native internal scratch object。完整 caller/callee 與 raw extension audit 見
`docs/S2C_NATIVE_AUDIT_681.md`。

`user_no` 另外被格式化成字串，和 `billing_first/billing_second`、常數
`5`、`0` 一起放入 `sub_7092C0` 的 Tricod argument block；這只能證明
client-side billing/telemetry consumer，不足以命名兩個 billing words 的
server business meaning。

**2026-09 login cross-check / server guardrails.** `server-ts` now keeps this wire contract in its packet builders/readers, with Bun tests that
mimic the native read order. `GL_LOGIN_REQ(682)` is structurally exact:
`str account, str password_or_token, u64 packed_data_revision, u8 fingerprint_source,
raw[24]`; no optional/trailing bytes are accepted. The client builder emits a
low fixed dword of `0xF1E1AB0E` and high dword
`dataRevision ^ 0xB1A9D7C7`; it is decoded only when that complete guard
matches. The revision comes from `datarevision.txt`; raw[24] is the separate
security/device fingerprint material.

The native client makes a **new** channel TCP connection after 681. Its 143
identity comes from `String[24]` and is therefore limited to 23 ANSI bytes;
its `n100` path is read and written as a 4-byte signed value. The server
consequently preserves the full signed-s32 billing/charge UI mode and grants a
short-lived, source-IP-bound, one-use account→channel admission. Crucially, it
does **not** use the identity as an account/nickname key: the available C
export proves the `String[24]` size and reuse but not its authoritative writer.
If two live logins from one IP have identical native echo values, the server
rejects the 143 as ambiguous rather than guessing an identity mapping. This is
the safest behavior available before a writer trace or observed packet settles
the identity semantics; 143 itself is not a cryptographic credential.

The final two billing words still have no backend semantic recovery. For 144,
the source plus CP932 `Extracted/ui/lang/msgtableres.lang` now establishes
several previously opaque roles: daily PG notice, rank warning, restriction
thresholds, and its optional `sNetCafeInfo` shape. The remaining raw fields
stay explicitly wire-oriented, not guessed as account or endpoint identities.

---

## 2. Opcode 註冊表 — sub_9D2050

`sub_9D2050` 用 `sub_9EAF50(name, id, ...)` 把 **674 個** packet 名稱註冊進
全域 map `dword_2317F50` (packet-viewer / debug 名稱表)。ID 即 wire opcode。
完整清單見 `db/packets.tsv`（**676** 個唯一 ID，100–994；GS_BASE=100）。
其中 674 筆來自 `sub_9D2050` 的直接 registration；990/991 是本輪以 UI
字串補名、未在該名稱表註冊的兩筆。此處刻意分開「source registration count」
與「本地 catalog row count」，避免將補名誤當成反編譯的直接事實。

> **⚠ 676 是「具名 opcode」數，不是 wire 上全部的 opcode 數（本輪實測）。**
> 交叉比對 `LAYOUTS.md` / `LAYOUTS_REQ.md` 中有 native reader/writer 實證的
> opcode 後，另有 **46 個 opcode 有真實的 native handler 但不在名稱表內**
> （45 個在兩份 layout 文件中名稱欄為空或標注 *unnamed*，屬正確標示；
> 第 46 個是本輪補進的 417，已在下方 MASTER 表具名）。可用
> `python3 tools/verify_dispatcher_coverage.py` 隨時複驗這些數字：
>
> * **29 個落在 100..994 的名稱表空隙**：203, 206, 295, 487, 488, 489, 828,
>   851, 852, 853, 880, 896, 898, 914, 930, 931, 932, 933, 946, 947, 949,
>   953, 954, 957, 958, 973, 975, 976, 992。
>   （206 已實作為 `RawOpcode206_REQ`，正是此類的代表。）
> * **16 個超出名稱表尾端 994**：995–1010（連號），handler 如
>   `sub_567AE0`(995)、`sub_567D50`(1001)、`sub_5884C0`(1005)，
>   其中 1007/1009 非 `sub_` 直呼。
>
> 這 45 個依方向乾淨二分，與兩份 layout 文件的分工一致：
> **23 個是 S2C**，實測**確實存在於主 dispatcher `sub_58B010` 的 case 表**
> （203, 488, 489, 852, 880, 914, 931, 933, 946, 947, 949, 954, 958, 976,
> 995, 997, 999, 1001, 1003, 1005, 1007, 1009, 1010）；
> **22 個是 C2S**，只有 request builder、不在 dispatcher
> （206, 295, 487, 828, 851, 853, 896, 898, 930, 932, 953, 957, 973, 975,
> 992, 996, 998, 1000, 1002, 1004, 1006, 1008）。
> dispatcher 共 **306** 個 case，其中 **24** 個不在名稱表內：
> 即上列 23 個 S2C，再加 **417**。417 已於下方 MASTER 表以
> `MASTER_KILLALL_ACK` 立項（416 的配對 ACK），但**未收進 `db/packets.tsv`**，
> 也未列入兩份 layout 文件 —— 它是 dispatcher 有 case、三處文件卻都漏掉的
> 唯一一個 opcode。其 case 不讀 payload，直接顯示 msg `0xA5`
> 「サーバーとの接続が終了しました。」，與 416「全服強制踢線」語義吻合。
>
> 也就是說 `sub_9D2050` 的名稱表**不是 opcode 空間的上界**。
> 任何「opcode 一定 ≤ 994」或「不在 packets.tsv 就不存在」的推論都是錯的；
> 新增 handler 前應同時查 layout 兩表。這些 opcode 的**名稱**仍 UNRESOLVED，
> 依專案慣例不得臆造協定名（206 用中性標籤 `RawOpcode206` 即為正解）。

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
| PM_ | channel / connection bootstrap (individual backend roles require evidence) | 16 |
| UDP_/TCP_/Y_ | transport-related catalog names; do not infer one common protocol | 14 |
| MASTER_ | GM/運維指令 | 79 |
| SECURITY_ | AhnLab HackShield | 2 |

_REQ = client→server, _ACK = server→client, _NOTIFY/_NOTICE = server 推播。
慣例: `ACK = REQ opcode + 1` (登入例外: GL_LOGIN_ACK=681 < REQ=682)。

主 dispatcher (client 端 lobby): `sub_58B010` — `switch(sub_591EE0(pkt))`
處理 TCP catalog ACK。它先轉發到場景與 `CGameRule`；UDP datagram 則另由
`CUDPManager::sub_595A60 → sub_595E80` 的 private dispatcher 處理。

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
## 2.5 UDP private transport and the only implemented control exchange (2026-09 revalidation)

> **Scope boundary — Fact/HIGH unless labelled otherwise.** `sub_595E80` is a
> separate UDP-private dispatcher. Its numbers are **not** the TCP opcode catalog
> (`sub_9D2050` / `server-ts/src/opcodes.ts`). Earlier wording in this document calling this
> layer “P2P”, “NAT hole punching”, “relay”, or a general UDP-ready handshake
> overstated the available evidence and is withdrawn. The client proof below
> establishes one control retry and its completion only.

### Transport lifecycle

| Conclusion | Native evidence | Confidence |
|---|---|---|
| A successful TCP `196 GC_ENTERCHANNEL_ACK` supplies the primary endpoint used to initialise this UDP transport. | `CLobbyChannel::sub_4179D0` reads the ANSI host then `s32` port only in its `result==1` tail; `sub_58ED30 → sub_595C90` passes the host and low `u16` port onward. | HIGH |
| The same successful-196 host/port is initially copied into a **separate secondary address field**. | Immediately after `sub_58ED30`, `sub_4179D0` calls `sub_596E60(&unk_1326908, cp, hostshort)`. `sub_596E60` writes a distinct sockaddr at offsets `+40..+55`; 142 (`sub_5565D0`) and 371 (`sub_570100`) also overwrite that secondary field. This initial equality does **not** prove that the two destinations remain interchangeable. | HIGH |
| Client transport is IPv4 UDP and is bound locally to `0.0.0.0:27000`. | `sub_596D60` calls `socket(AF_INET, SOCK_DGRAM, 0)`; `sub_596DA0` calls `bind` with `htonl(0)` and CUDPSocket's constructor default port 27000. It stores the configured remote host with `inet_addr` and `htons(port)`. | HIGH |
| The native client starts a receive thread before declaring the manager active. | `sub_595C90` opens/configures `dword_1324360`, creates/resumes a suspended `CUDPThread`, then calls `Sleep(1000)` and only then sets manager `+52=1`. The native function does not test the open/bind/thread-create return values before that sequence. | HIGH |
| Native stop is forceful rather than a join/close handshake. | `sub_58AF90 → sub_595D50 → sub_5957F0` clears active then `sub_597350` calls `TerminateThread`, `CloseHandle`, clears the handle, and only afterwards `sub_597040` calls `closesocket`. The infinite receiver loop has no cooperative stop check. **Inference/HIGH:** this ordering permits a receive/dispatch shutdown race; no join is present. | HIGH |
| UDP is a datagram transport with the shared 8-byte `Packet` header and AES-CFB-128, but **without TCP LZ**. | `sub_595980` does only `sub_591F90 → sub_592F60 → sendto`; it never calls `sub_592CE0`/`sub_592E00`. `sub_595A60` does `recvfrom(…,9600) → sub_591FB0/sub_591D50 → sub_5930C0`; it never calls LZ decompression. | HIGH |
| A logical empty UDP packet is still encrypted to a 16-byte ciphertext. | `sub_592FB0` and `sub_593110` align a zero `word0/word2` to the AES block size before encryption/decryption. | HIGH |
| The receiver accepts a received datagram at least `word0+8` bytes long; trailing bytes are not passed to the packet parser. | `sub_595A60` tests `received >= sub_591F00(packet)+8`; it does not require equality. | HIGH |

UDP header interpretation in this path is therefore:

```
u16le word0 = ciphertext byte count (always 16-byte aligned)
u16le word1 = UDP-private opcode
u16le word2 = unpadded pre-AES payload byte count
u16le word3 = original pre-send payload byte count
byte[word0] = AES-128-CFB-128 ciphertext, zero IV, fixed client key
```

`word2` and `word3` are both the uncompressed payload length on the directly
observed UDP sends. This is **not** permission to infer that every possible UDP
packet has no additional history or application state; it only records that the
native UDP send path omits the TCP compression stage.

### UDP private opcode 空間全圖（本輪機器掃描）

先前各節只談 19→20，因為那是**唯一已實作**的路徑。本輪把整個 UDP 面掃完，
確立它是一個**與 TCP 完全分離、自成一格的小 opcode 空間**（值域 1..~158，
與 TCP 的 100..1010 不重疊，故不會混淆）。

**接收端是 `sub_595E80`，可證為 UDP**：呼叫鏈為
`sub_595A60` → `sub_596F90` → **`recvfrom()`**（9600 B 緩衝，
同 TCP 的 frame 檢查 `sub_591D50` 後才分派）。該 dispatcher 有 **22 個 case**：

```
2 4 5 6 8 10 12 13 14 15 18 20 22 24 26 28 29 31 33 34 154 158
```
（`8` 與 `24` 共用 `sub_596940`；`26` 走 `unknown_libname_107`。）

**送出端**由 `Packet::possible_ctor_or_dtor_0(v, <op>)` 反查，客戶端會建構
**15 個** UDP opcode：`1 5 6 9 13 14 15 17 19 21 23 27 30 32 35`。

**REQ→ACK 以 `n → n+1` 成對（Fact / HIGH）。** 15 個送出中 **12 個**
的 `n+1` 確實存在於接收 case 表：
`1→2 · 5→6 · 9→10 · 13→14 · 14→15 · 17→18 · 19→20 · 21→22 · 23→24 ·
27→28 · 30→31 · 32→33`。
這與 TCP 面的奇偶配對慣例一致，可作為推斷未知 UDP opcode 方向的依據。

剩下三個不是缺口：**`5`／`6`／`13`／`14`／`15` 同時出現在送出與接收兩側**，
屬 peer 之間雙向互送的訊息；`35`（`sub_7463E0`，13 個寫入原語）只送不收。

#### UDP 送出封包的共用「寄件人標頭」（Fact / HIGH，本輪）

把 15 個送出 opcode 的 builder 逐一抽出寫入原語序列後，浮現一個清楚的分層：

| 形狀 | opcode | 寫入序列 |
|---|---|---|
| **共用標頭 + 各自 body**（9 個） | `1 9 19 21 23 27 30 32 35` | `u8 u8 u8 s32` 起頭，其後才是各自欄位 |
| 單一 `u8` | `5 6 13 14` | `u8` |
| 無 payload | `17` | `(空)` |
| 例外 | `15` | `u8 u8 u8 u8 u8 s32`（標頭中間多兩個 u8） |

那 4 個 byte 的前綴**就是 §Private opcode 19 的 offset 0..7 欄位**
（active channel index / room slot / `CMyData+840==2` 布林 / 本機 player id）。
證據不只是型別相同 —— **9 個 builder 全都從同一組來源取值**：
`sub_417D00`（channel index）、`byte_EE896D`（room slot）、
`dword_EE8CB4`（player id），部分另用 `sub_5928E0`。

因此 UDP 面的結構可定案為 **「寄件人識別標頭 + 各 opcode 專屬 body」**，
而非每個封包各自定義格式。各自的 body 例如：
`21` 多一個 `raw4`、`27` 多 `u8 u8`、`30` 多 `u64×3 + s32`、
`23` 多 `u8 u8 + s8×8 + s32`（`sub_744450`，13 個寫入原語中最長者）。

⚠ 但**未證實**：19 在標頭後還接一個 NUL 結尾暱稱字串，
其餘 8 個**沒有**該字串 —— 所以標頭是共用的，完整 payload 不是。
各 body 欄位的語義一律 UNRESOLVED。

**關鍵界線（不變）。** 以上只證明**客戶端的 UDP 字彙與方向**。
`sub_596670` 的 19→20 仍是唯一有完整欄位證據、且已實作的路徑；
其餘 opcode 的欄位、語義、以及伺服器是否該參與，
全部維持 **UNRESOLVED** —— 既有的 UDP scope gate 不因本節放寬。
本節的用途是**界定搜尋範圍**：日後分析 UDP 時知道總共有哪些 opcode、
哪些成對、哪些是雙向。

### Private opcode 19 → 20

`sub_596670(CUDPNetworkManager)` is the source of the only currently implemented
server behavior. On a normal retry it builds private opcode **19** in this exact
order (all scalar values are little-endian because the packet primitives write
native little-endian scalars):

| Offset | Wire type | Client source | Meaning / evidence boundary |
|---:|---|---|---|
| 0 | `u8` | `*sub_417D00()` via `sub_592920` | active channel index; set by the 142/196 channel paths. |
| 1 | `u8` | `CMyData + 5` (`byte_EE896D`) | current room-member slot. `sub_537690` writes it from room/join response slot fields and callers use it to select the local room member. |
| 2 | `s8` | `CMyData + 840 == 2` via `sub_5928E0` | exact boolean comparison only; the domain of `CMyData+840` remains **UNRESOLVED**. Normal client emissions are 0 or 1. |
| 3 | `s8` | `-2` when `CMyData+840==2`, otherwise `CMyData+13`, via `sub_592920` | source-dependent one-byte value. Its non-special domain is **UNRESOLVED**; do not rename it team/mode/peer id. |
| 4 | `s32` | `CMyData + 844` (`dword_EE8CB4`) via `sub_592A20` | client-reported local player identifier. It is compared with player records during room/join processing; no server-side authorization rule is recovered. |
| 8 | NUL-terminated CP949/ANSI string | `sub_537740(CMyData)` = `CMyData + 896`, via `sub_5926F0` | local nickname. It is copied from character UI data and reused in client chat/name comparisons. No length prefix; emission includes the NUL. |

The object provenance is direct: global `byte_EE8968` is a large `CMyData`, not
the decompiler's false `char[4]` declaration (`sub_AD91B0` constructs it and
`sub_ADC240` ⚠符號已漂移 destroys it). Accessors in `00536D70..00537740` establish the
object-relative offsets. The payload is `9 + encodedNicknameByteCount` bytes,
where the final one byte is the NUL. Its exact semantic names remain intentionally
source-oriented where the binary does not prove a domain interpretation.

**Retry/completion state machine:**

1. `sub_595C90` sets control next-tick (`CUDPNetworkManager+40`) and retry count
   (`+44`) to zero. The first call to `sub_596670` can consequently send now.
2. The direct callers are `sub_4070B0`, `sub_407290`, and the game-start screen
   tick `CLobbyGameStart::sub_43C380`. They poll this routine while their own UI/
   game transitions wait; it is not a generic always-on UDP heartbeat.
3. When `timeGetTime() >= manager+40` and retry count is at most 100, a normal
   call sends opcode 19 to the primary `196` endpoint, clears global
   `byte_1D0CFE7`, records a tick at `+36`, schedules `+40 = now + 500`, then
   increments `+44`. The increment and schedule happen even if `sendto` failed
   (its return is merely accumulated into raw global `dword_1D0CFF4`).
4. Attempt six increments the count from 5 to 6, calls TCP `sub_560720` (catalog
   opcode `139 GG_EXITGAME_REQ`), then stores 101. Values above 100 suppress
   further normal sends. This is a client-observable fallback, not a retry policy
   the server may freely redesign.
5. Before building 19, `sub_596670` checks `sub_67F120`: it observes whether the
   current local game-mode object reports internal state **9**. If true, the same
   completion mutation occurs locally without an opcode-19 send. The label for
   mode state 9 is **UNRESOLVED**.
6. UDP opcode **20** reaches `sub_595E80` only while the manager is active, a
   global dispatch dependency is non-null, and `sub_67EAC0()==0`. Case 20 calls
   `sub_5968C0`, which reads **zero payload fields**, sets `byte_1D0CFE7=1`,
   clears manager retry/control fields `+44`, `+8`, and `+4`, timestamps `+24`,
   and calls `sub_594F00`. The latter only clears `byte_1324331`.
7. `byte_1D0CFE7` is a broader game-transition gate, not an opcode-20 receipt
   bit: `CLobbyGameStart::sub_43C2A0` also sets it for internal mode states 6 or
   15, and several lobby/game transition functions clear it. Those clears do not
   themselves reset the retry counter. There is therefore no basis for treating
   op20 as an idempotent, authenticated session admission message.

`sub_4070B0` additionally compares `timeGetTime()` with an externally maintained
`dword_1326980`; `sub_407290` stops when external `n0x64 > 100`. The setters and
units of those outer limits are not fully recovered, so no numeric timeout beyond
the 500-ms scheduler and sixth-send fallback is claimed here.

### Server implementation boundary

`server-ts/src/packet.ts` and `server-ts/src/udp.ts` encodes exactly the native UDP AES-only
framing. AES-CFB encryption is **not** an authentication/MAC result, and no
native server admission token is recovered. `server-ts/src/udp.ts` binds the advertised IPv4
`UdpHost/UdpPort`, parses only the complete opcode-19 shape above, and immediately
returns an **empty, encrypted private opcode 20** to the datagram source. Empty is
intentional: `sub_5968C0` does not consume a packet field. The endpoint is
stateless and does **not** turn the reported channel/slot/player/nickname fields
into authority, because the original server-side admission/correlation behavior
has not been recovered.

This minimum exists to prevent the directly evidenced sixth-attempt TCP 139
fallback. It is not a claim that the remaining private opcode cases are a relay,
peer-to-peer protocol, NAT traversal, or complete gameplay transport. Unsupported
private opcodes are logged and ignored. Future work must evidence each case's
builder, receiver, state dependencies, remote-address usage, and server behavior
before expanding this endpoint.

### Adjacent send/address flows recovered, deliberately not implemented

The following is recorded because it is necessary counter-evidence against
turning 19→20 into a made-up general UDP protocol. It is **Fact/HIGH** for the
listed reads/writes and branches, but not a conclusion about the original
server's business rules.

| Native path | Directly observed behavior | Server consequence |
|---|---|---|
| `sub_595D80 → sub_596330` | The periodic path runs only while manager `+52` is active, `n2 != 2`, `sub_67F120()!=1`, and `sub_67EC20()!=1`. It passes raw manager `+72` into `sub_596330`; that field's provenance remains **UNRESOLVED**. `sub_596330` does nothing when bit `0x08` is set. Otherwise it emits primary-destination opcode 21: `u8 channel, u8 roomSlot, u8 sourceDependentSlot, s32 dword_EE8CB4, s32 manager+4, s32 dword_EE8978`. The two final values retain source-oriented names. | No opcode 21 is emitted or accepted by this server. Its trigger and server correlation are not recovered. |
| `sub_5937D0 → sub_593830` | State byte zero calls `sub_593830`. For `n2==2`, it sends primary opcode 15 (`u8 channel,u8 roomSlot`) three times, stores state 7. Otherwise, if elapsed since `dword_F2563C` exceeds 1000 and global `n0x3E8==0`, it sends primary opcode 1 (`u8 channel,u8 roomSlot,u8 sourceDependentSlot,s32 clientPlayerId`) and refreshes that tick. State 4 delegates to `sub_5941D0`; state 7 returns true. | Do not use this as a “ready” or P2P state name. No 1/15 behavior is implemented. |
| inbound 4 → `sub_593AB0` | A one-shot global guard admits only its first execution. It reads `u8 count`, then `count×{u8 participantKey,raw16 addressBlob}`; it maps each key through the 16 local member records, returns immediately for an unknown key (after any preceding mutations), stores matching blobs, and sends opcode 5 (`u8 sourceDependentSlot,raw4 dword_F25640`) three times directly to every matching nonlocal stored 16-byte address. It then stores state 4. | A raw16 blob is used as a sockaddr argument in this path, but its server generation/lifetime is not recovered. Do not create, relay, or zero-fill it. |
| inbound 5 → `sub_593E60` | It reads `u8 participantKey,raw4`. On first receipt for a matching member key it snapshots the `recvfrom` source sockaddr into that member's 16-byte storage, creates opcode 6 (`u8 sourceDependentSlot,raw4 dword_F25640`), and sends it three times directly to that received source; later receives only increment a byte counter. | This proves direct received-address reuse in this client branch, not the identity/authentication or server role. No 5/6 behavior is implemented. |
| `sub_596E60` versus CUDPSocket's stored primary | `sub_596DA0` sets primary sockaddr at CUDPSocket `+24..+39`; `sub_596E60` sets secondary sockaddr at `unk_1326908 +40..+55`. `sub_595900` sends packet 17 to primary, `sub_595940` sends it to secondary, and `sub_595980` is the explicit-address send primitive. | Endpoint config must remain separate in future design even though successful 196 initially copies the same pair into both fields. |

**Opcode-21 correction / exact layout.** The decompiler names around `sub_596330`
are misleading and should not be silently converted into player semantics. Its
payload is exactly:

```
u8  activeChannelIndex          = *sub_417D00()
u8  currentRoomSlot             = byte_EE896D
u8  sourceDependentSlot          = (n2 == 2) ? 0xFE : n0x10
s32 clientReportedPlayerId       = dword_EE8CB4
s32 managerOffsetPlus4Raw        = CUDPNetworkManager + 4
s32 cMyDataOffsetPlus844Raw      = dword_EE8978
```

The sixth field is **not** a second copy of `clientReportedPlayerId`: source
addresses distinguish `dword_EE8CB4` from `dword_EE8978`. The latter is an
unresolved `CMyData`-adjacent raw integer; the temporary variable name `v14`
in the decompile is not evidence of meaning. This table replaces the earlier
ambiguous prose and is Fact/HIGH for order/width/value source, UNRESOLVED for
both `manager+4` and `dword_EE8978` semantics.

**Y_TCP_INF 165/166 — TCP catalog packet pair (evidence boundary)**:
`166` is forwarded by `sub_58D820` to `sub_749B90` on the battle-engine object.
The client code inspected here does **not** prove that this pair is a fallback for
private UDP, nor that it is activated by NAT traversal; that older conclusion is
withdrawn. The field-reader notes below remain a separate TCP parser record:
```
166 = u8 slot, u8 subtype:
  1 = 位置心跳 (u8)          2 = 動作狀態
  3 = 射擊事件 (f32 tick, u8, u16 wp, u8×3, s32×2 ×2連發...)
  4 = 移彈/投擲 (f32, u16, s32, 6×f32 = pos+dir 向量!)
  5 = 技能 (f32, u8, s32, 6×f32)
  6 = 特殊 (f32, u16, s32, 6×f32, u8, str)
  7 = 聊天/表情 (str)   8/9 = 狀態
```
165 has multiple builder variants. The relationship of those variants to private
UDP opcode 32 and the original server's routing/admission behavior remains
**UNRESOLVED**; a relay implementation must not rely on the withdrawn
TCP-fallback interpretation.

**GC_CLAN_PROTOCOL container grammar (五輪發現)**:
`GC_CLAN_PROTOCOL_REQ(583)/_ACK(584)` 是**容器封包** — payload 第一個欄位是
`s32 sub_opcode`，之後才是子協定
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
202  (無)           次數查詢       — (sub_54D040 沒有 case 202；配對 ACK
                                    sub-op / body 是 UNRESOLVED，不可把 201 的
                                    sub_54F0F0 讀取形狀套過來)
203  str message    戰隊聊天       str nick, str message (sub_54F2D0;
                                    client sender 的 rank>1 gate)
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

**Fact/HIGH — 202 direction correction.** `sub_550CF0` 確實建 `583 / sub=202`
且不寫子欄位，但 `sub_54D040` 的完整 584 switch 沒有 `case 202`；不能把唯一
`case 201 → sub_54F0F0` 的兩個 s32 讀取形狀誤標成 202 ACK。202 的對應 response
sub-op/body 仍是 **UNRESOLVED**。

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
string  account              (sub_401B50 回傳, ANSI)
string  password_or_token    (同上第二次轉換；wire 本身沒有獨立長度欄)
u64     packed_data_revision (見下；不是 hardware key)
u8      fingerprint_source   2=storage serial, 1=fallback first adapter MAC,
                              0=兩者皆不可得
byte[24] fingerprint          source=2: hard-drive serial bytes，超過 23 bytes
                              則第 23 byte 改 `~`；source=1: 前 6 bytes 為
                              GetAdaptersInfo 第一個 adapter MAC，餘位為零；
                              source=0: 全零
```
`sub_401B50` is only a conversion wrapper in the recovered C: its `Target__7`
call resolves through `sub_A366EF` to `kernel32!WideCharToMultiByte`, then returns
the scratch at `unk_23197F0`. The decompiler elides that imported call's stack
arguments, so this proves ANSI conversion but not an account/password business
name or a smaller per-field wire limit. Keep both fields as NUL ANSI strings at
the packet boundary; credential-domain validation remains a separate Store
policy.

**2026-09-15 native primitive re-check.** `sub_43CBA0`/`sub_43CCF0` loads
`datarevision.txt` into `this+396`; `sub_43DF00` writes
`0xF1E1AB0E` into the first dword and `revision^0xB1A9D7C7` into the
second before calling `sub_592AE0`. Although the decompiler types the helper's
first argument as `char`, `sub_592AE0` copies eight contiguous bytes from that
stack address. A native 682 frame independently confirms the complete layout:
**lo32(wire)=0xF1E1AB0E** and **hi32(wire)=revision^0xB1A9D7C7**. For the
bundled `datarevision.txt` value `811034967` (`0x30576957`), the exact wire
value is `0x81FEBE90F1E1AB0E` (little-endian bytes
`0E AB E1 F1 90 BE FE 81`). The server restores
`revision=hi32^0xB1A9D7C7` and validates the complete low-dword guard.

Fingerprint source comes directly from the builder: `sub_9A8790` first tries
its storage-identification list and copies at most 23 bytes into the zeroed
24-byte block; only when it fails does `sub_9A86A0` use `GetAdaptersInfo` and
copy a six-byte MAC. Thus source=2 does **not** mean failure, and the packed
u64 does **not** identify a machine. Server stores the exact raw24, its source
code, and content revision separately. It additionally rejects impossible
source/raw24 combinations (source 0 nonzero bytes, source 1 nonzero bytes after
its six-byte MAC, or source 2 without the required final NUL); it does not
print fingerprint bytes or the password/token in logs.

### 3.2 GL_MYINFO_ACK (198) — handler sub_570550 → CClientData 反序列化
```
bool    success                 0 時直接顯示 resource 0x70 / code 17
                               （「資料庫連線障害」）；不是可用的空玩家狀態
若 success:
  s32   user_id (v19)
  --- sub_523BF0: 基本資料 ---
  string  nickname            (this+60,  char[24] / 0x18 bytes including NUL; sub_46F450 copies this run separately from +84)
  u8      selected_char_index (this+88; CHARSLOT list index, not char_type)
  s32   level/experience        (this+92,+96)
  s32   raw/unknown             (this+108; sub_9252D0 consumes it for task condition 1; server owner unresolved)
  s32   reserved x3             (this+136,+140,+144; no proven task/stat owner)
  s32   wins/losses             (this+148,+152)
  (native +100 is a derived class/level recomputed from exp, not a separately
   read wire word)
  s32   kills/deaths            (this+156,+160)
  s32   headshots/combos/hearts/criticals (wire order +164,+168,+172,+176)
  s32   double/triple/multi/ultra/z/k/dd (this+180..204)
  u8      flags x3              (this+304,305,306)
  s32     cash                  (this+104)
  s32     raw x2                (this+112,116)
  byte[48] play-time/mode blob (this+208; [52] play seconds, [53..60] mode counts,
                                [61..63] no proven consumer)
  u8      slot_current          (this+4)
  --- sub_524010: character normal appearance records (最多 20 個) ---
  u8      char_count
  repeat char_count (≤20):
    u8    char_type
    u16   x12: body, head, face, top, bottom, shoes, outer/set, eye,
              hair accessory, face accessory, head accessory, special
              (all category-relative normal-appearance offsets; no weapons)
  --- sub_524660: player weapon loadout groups (最多 4) ---
  u8      group_count (≤4)
  repeat:
    u8    group_no
    u16   primary_offset
    if group_no != 3: u16 secondary_offset, melee_offset, throw_offset
    if primary_offset != 0: s32 x8 (weapon part IDs)
  --- sub_527550 (sub_522480): 9×s32 — 無前導 count! (五輪修正)
      每個非零 id 需過 sub_535020 目錄驗證, 失敗 → client 錯誤 10
      ⭐ 五十四輪字串完全揭露 (sub_4C4990 / sub_4C4E70 陣列):
      9 個槽位依序為: [0] Crosshair (準心), [1] NAME (名牌), [2] MASTER (大師稱號),
      [3] ABILITY (主能力), [4] BOOST_EXP (經驗加成), [5] BOOST_PG (PG加成),
      [6] EXTRA_ABILITY (額外能力1), [7] EXTRA_ABILITY (額外能力2), [8] VOICE (語音自訂)!
  --- sub_527D00: raw u8 n5 (+144452) + 已選 NewSkill profile 的 raw 28B
      = 7×s32 (sub_527AF0); 非零 id 同樣驗證, 失敗 → client 錯誤 9。
      ⭐ 驗證段 11,010,001..11,070,000 = **ヘアパズル段** (1,273 條,
      kind 13) → 七個 NewSkill puzzle ordinals；這是 255 五-profile snapshot
      中 selected record 的鏡像，**不是快速槽**。n5 的原服語意仍 **UNRESOLVED**
      （現有 server 保留既有 raw value 5 convention）。
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

#### 3.2a 198 的 code 63 / 可用角色不變量（本輪驗證）

**Fact / HIGH**

- `sub_570550` 在完整讀取 198 後，以目前角色的 `word_EE8DE8[13*i]`
  （`sub_524010` 角色記錄的第一個 `u16`）判斷可用性。它是 0 時掃描
  已解析記錄；若仍找不到非 0 值，便取得 resource `0xCC` 並顯示 code 63。
- `sub_523BF0` 在 `char[24]` nickname 後讀 `CClientData+88`，並在 48-byte blob 後
  讀 `+4`；`sub_526CA0` / `sub_884160` 將 `+88` 用作 `CHARSLOT` 選取值，
  並由 `sub_884160` 原樣寫入 outbound opcode 312。`sub_525070`、
  `sub_525790` 等 consumer 以它索引最多 20 個 character-list slots；因此
  Server 必須在 **兩個** 198/247 基本資料欄位寫 0..19 的 character-list
  slot/index，不能在 `+88` 寫角色 type。真正的 `char_type` 是後續每筆
  `sub_524010` 記錄的首 byte。TS Store 將 DB 的 persistent `current_character`
  slot key 映射到 `ORDER BY slot` 後的 compact serialized-list index；這是
  server projection policy，不宣稱 native wire 還有一個 per-record persistent
  slot 欄位。若 current slot 不在 serialized rows 中，Store 拒絕產生含歧義的
  MyInfo，而不是把 persistent slot number 直接發到 198/247。
- 對精確的 `origin/main:Extracted/ui/cfg/itemdata.pat` 解密後，變長 ItemData
  stream 的 header 是 `(version=1,count=21164)`，可無殘餘地解析全部 21,164
  records。`19,900,001..19,900,015` 連續 15 筆角色本體在 record `+532` 的
  byte 恰為 `1..15`。`sub_533FB0` 直接回傳同一 `+532` byte；`sub_526730`
  將角色第一個 equipment offset 重建為此 body item 後使用該 lookup。

**Inference / MEDIUM (server persistence policy)**

- The five native body-template maps establish the first six ordinary appearance
  words, not only the body. Therefore a Server-created canonical type `t`
  (`1..15`) persists the exact `body/head/face/top/bottom/shoes` vector from
  `RESOURCES.md §5c-1`; type 1 is six strict `1` offsets. The final six
  normal-appearance words remain zero unless another evidence-backed operation
  owns them.
- At successful verified login, a type `1..15` row with an absent or canonical
  body can have only *missing* words in that six-word template repaired. The
  repair must also select an emitted record with a nonzero body. It must never
  replace a nonzero historic body/cosmetic value, grant weapons/UI items, or
  guess one of the final six appearance words.

**Assumption / bounded**

- `characters.slot_no` 目前由 Server 以連續 list position 配置。Client 的 198
  character records 本身沒有 slot id，因此 wire 選取值必須始終是序列化順序的
  index；若未來允許稀疏 slot storage，selection persistence 必須先明確做
  slot-id ↔ sorted-wire-index conversion，不能猜測兩者仍相等。

`server-ts` Bun test 的 198 reader test 會完整消費 basic/stat、四個 weapon
records、9 UI-item / selected-NewSkill-puzzle / tail，並斷言 selected index `0`、char count `1`、type `1`、
第一個 body `u16=1` 和其餘十一個 `u16=0`；另含 fresh identity、legacy
bodyless-row repair、nonzero body preservation、GM/purchase type validation coverage。

### 3.3 GL_MYITEM_ACK (200) — handler sub_570AB0 → sub_524B70 (分頁背包)
```
bool    success
s32     start_index          (分頁, 每包最多 100 條, 背包上限 5120)
repeat until sentinel:
  s32   inv_slot   (負值 = 結束)
  s32   item_id    (≤0/非法 = 中止)
  float f1         (native first float; exact item-domain meaning UNRESOLVED)
  float f2         (native second float; exact item-domain meaning UNRESOLVED;
                    NewSkillLevTable is client display/combine data, not authority
                    for naming or granting these server inventory values)
  s32   period     (剩餘天數)
  u8    extra      ⚠ 四輪修正: 200 有 extra (sub_570AB0 呼叫 sub_524B70(cd,pkt,1));
                   無-extra 版 (a3=0) 屬 290/294 MASTER_USERINFO 系
  u16   durability (寫入 *2 個 word: current=max)
```
相鄰 opcode (卅六輪型別定案 — 六輪的 f32 標註更正為 s32 鍵):
- **201 GL_MYPARTSUP_ACK** (sub_95A3B0): `s32 count` + count×20B
  `{raw4 key0, raw4 key1, raw1 kind, raw4 value, raw4 period}`;
  native `sub_95A4A0` uses `(key0,key1)` as the duplicate/update key and
  stores all five wire fields. The pair is consistent with the weapon/part
  catalog projection, but the value/period policy is not established by this
  reader.
- **202 GL_EXPIRE_PARTSUP_ACK** (sub_95AE40): same 20B field widths; each
  `{raw4 key0, raw4 key1, raw1 kind, raw4 value, raw4 period}` is passed to
  `sub_95A800(key1,key0)` and removes the matching native pair. The reverse
  callee argument order is direct native behavior; it does not by itself name
  the two wire fields as part/gun.
200 handler 完成後無論 success 都呼叫 `sub_41BF20(byte_BF0724)`（local
狀態 8→9），並設 `byte_EE8C05=1`；成功 record 另以
`sub_534450(item_id, durability)` 更新 client 目錄的 current/max durability。
額外驗證: start<=0 → 背包游標欄歸 0; start>=5020 → 夾到 5020; item_id 需通過
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
### 3.4a 204/468 bulk purchase routing, 206 part purchase, and 358 cash purchase

`sub_571100` validates the selected item families/periods, builds the normal
`204` body `{u8 count, count×{s32 itemId,u8 kind,s16 period,[s16 variant for
kind 12/13/17]}}`, and changes only the header to `468` when *any* selected ID
is in either Hukubukuro range. Thus 468 is not a distinct literal constructor.

`sub_571620` constructs **unnamed opcode 206** as
`{s32 itemId,s32 rawContext,u8 itemKind,s32 rawPeriod}`. The native name table
contains 207 but no label for 206; its “weapon-part purchase” association is an
**Inference / HIGH** from this immediate 206 sender and 207 consumer, not an
original symbol. `207` first reads `u8 rawResult`; **zero** enters the
success-only part/wallet decoder, while any nonzero value has no tail.

`358` is `{u8 count,count×{s32 itemId,s32 clientCalculatedPrice}}`. The client
calculates that price with `sub_534070` and a catalog discount getter before
sending it. This proves it is client cache/input, not a source for an
authoritative emulator price. `359` always begins `{u8 resultCount,s32
rawHeader}`; only a nonzero count reads entries `{u8 itemResult,[itemResult !=
0: s32 itemId,s32 rawA,s32 rawB,s32 rawC]}`.

### 3.4b GS_BUY_ONCEITEM_ACK (696) — corrected conditional prefix

`sub_571D70` first reads `{u8 rawResult,s32 rawItemOrClass}`. When
`rawResult==0`, it **unconditionally reads one raw `s32`**. Further reads vary
by the received item family/value; this is not one fixed “once purchase” tail.
The former fixed `u16`/universal structure was wrong. The `695` sender has
multiple item-family forms, so a server must not parse it as one common
`item/kind/period/variant` record.

The period allowlists in `sub_570B00`/`sub_571100` are client validation facts:
1/7/15/30/60/90 for several timed kinds; other families use zero or a
family-specific choice. They do **not** establish server price, ownership,
stock, currency, duplicate, or grant rules.

### 3.5 GS_CASH_ACK (357) — sub_572420: `u8 rawStatus, s32 rawCash`。
The consumer merely reads both fields; no original status polarity/billing
policy follows from this client code. A server must not claim a successful
external-cash balance without that policy.
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
### 3.6c GS_BUY_ONCEITEM_REQ (695) — multiple direct forms (reopened)

`sub_570B00` alone conditionally emits more than one layout: one item-family
branch writes `s32 itemId, str, u8 kind, u8 period`; other branches write a
short `s32` pair or `s32 itemId, u8 kind, u8 period`. Separate callers
`sub_5115E0` and `sub_8DE9C0` write an eight-byte
`{s32 itemId,u8 kind,u8 rawPeriod,s16 negativeVariant}` form. Therefore the
old universal fixed form andmerely
a stack-buffer artefact are both disproven. The request remains deliberately
unparsed by the server until every accepted item family, its selector source,
and the matching 696 response tail have been reconciled.
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
REQ 端 `sub_56A0F0`: `u8 (=1)` — native parameter is `unsigned __int8`; client 每 ≥1 秒 (timeGetTime 差
≥0x3E8) 送一次要求刷新名單, log `L"Send UserList"`; server 直接回
106。ACK 端 sub_56A250 (四輪修正):
```
raw2   gate (native only tests zero/nonzero; domain unresolved)
若 gate != 0:       ← gate==0 時後面什麼都沒有
  u8   flags        (bit0: clears progress and sets a mode-specific native list-state flag;
                     bit2: clears that flag; exact UI policy unresolved)
  u8   recordCount
  repeat recordCount: raw4 userKey, string nick, s32 exp
            if userKey>0 { raw4 custom_tex_key, string tex_name(64) }
```
⚠ 第三個 s32 是 **exp 不是 status** (十二輪定案): `sub_588560` 對它呼叫
`sub_403360(exp→Class index)`，`CUIWaiterList` 再用該 index 渲染 `Class`。
第一個 raw4 會作為 client user/profile table key；只有 key>0 且 lookup
成功時，custom texture key 才會註冊到 `EMBLEM`。wire width 仍不可因 local
consumer 的 cache/lookup 而縮成 u8。
### 3.9 GL_GAMEROOMINFO_ACK (108) — sub_568CE0 (卅七輪逐欄定案):
```
u8   mode (3 = 錦標賽樹狀圖, 委派 sub_580A80; 其他 = 房間清單)
if mode != 3:
  u8   count
  repeat count:
    u8    room_no (需 <0xD2=210), s8 state
    state>=0: title 由 client 查字串表 state+309 (msgtableres 0x135+state
              = 預設房名片語, 如「私達はペラペラだ！」「日々の努力が実力に
              なる」…); state<0: string title (自訂房名) — 之後皆為下列 12 欄:
      u8   cur_players   (+105; sub_44E970, 「cur/max」第一數)
      u8   has_pass      (+106)
      u8   max_players   (+129; 冗餘 — client 以 +110 popcount 重算覆寫)
      u16  max_slot_mask (+110; bit 0..max-1 = 1, sub_53FB10 以 popcount
                          重算 +129 並展開 +112..+127 逐槽旗標)
      u8   game_mode     (→ sub_53FBB0 建立 CyGameModes LobbyUI, 見下表)
      u8   room_type_A   (+108; sub_44E7B0 — ROOMTYPE bit)
      u8   mode_param_a  (→ mode 物件 +12)
      u8   room_type_B   (+109; sub_44DA70 — ROOMTYPE bit)
      u8   double_damage (+128; sub_44DBB0)
      u8   map           (+130; sub_540280/sub_540260 — 122 亦寫此欄,
                          124/125 = 特殊地圖 id)
      u8   mode_param_b  (→ mode 物件 +4, sub_74F450)
      u8   no_skill_bg   (+185; sub_44E820 — NOSKILLBG)
    若 mode==2: 兩組 {s32 team_id, u32 custom_tex_crc, str(75/87) tex_name,
                u8 x} (隊伍自訂圖示, 存 room+188.., CCustomTexture 註冊)
else:
  u8   n4, u8 i1, u8 flags142
  repeat i=n4-1 downto i1:
    u8   stage_raw
    u8   round_type
    u8   mode_raw
    raw4 stage_raw_word_1
    raw4 stage_raw_word_2
    u8   pair_count
    repeat pair_count:
      raw4 node_or_room_id
      u8   pair_byte_1
      u8   pair_byte_2
      u8   pair_byte_3
      u8   pair_byte_4
      raw2 pair_word
      if round_type==4: u8 round4_raw + 4×raw4 participant blocks
      else: 2×raw4 participant blocks
  u8 has_my
  if has_my != 0: u8 selected_raw, u8 footer_raw
  raw4 state494 (native local `float`, stored at client state [494])
```
`sub_580A80` 的 round-4/non-round-4 分支讀取數量不同；不要把 mode-3
header 的 `n4` 當 ordinary room count，也不要把 `pair_byte_3` 直接命名成
bool：native `sub_592900/sub_592940` 都只證明它們各是一個 byte。兩個
participant blocks 由 `sub_875C20` 消費，第一個 dword 會與 local identity
block (`sub_54B570(dword_131E238)`) 比對；其 uid/emblem/score 語意仍未定。
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
raw2 header, string field_s0 (native local `char[21]`, at most 20 ANSI bytes), u8 count; repeat: string field_s1, raw4 field_a3
```
`sub_537F60` accepts the full 4-byte record field but assigns it to the
one-byte table slot `this+61585+index`; only the low byte is visibly retained by
that local consumer. The 21-byte record-string copy loop has no visible per-byte clamp.

### 3.11 GL_MSG_RECVLIST_ACK (426) — sub_55A630:
```
raw2 header, string field_s0 (native local `char[21]`, at most 20 ANSI bytes), u8 count
repeat: string field_s1, u8 field_a3, string field_s2, raw4 field_a5, string field_s3 (native local 201-byte stride; reader does not visibly clamp), string field_s4 (native 2-byte stride), raw2 field_a8
```
Native `sub_5378C0` keeps at most 10 rows. Its string slots have strides 20/21/201/2
and are NUL-copy loops without an explicit clamp. The consumed `raw4 field_a5` is
then assigned to a one-byte table slot (`this+60536+index`), and the consumed `raw2
field_a8` is assigned to another one-byte table slot (`this+122107+index`); only the
low byte of each is visibly retained by this recovered consumer. This truncation is
a client storage fact, not permission to narrow the wire fields in a server writer.
The first per-record string does have a recovered state consumer: `sub_537D20`
marks a matching entry with byte `89`, `sub_537E90` tests that marker, and
`sub_55A1E0`/`sub_55A3C0` build 421/423 requests from the key and its state;
`sub_55A310`/`sub_55A4F0` receive those requests and invoke the corresponding
mark/unmark helper. This proves a string-key/read-state path, but not whether
the key is a sender, recipient, message id, or folder. The remaining record
strings and stored low-byte raw fields still have no recovered semantic
consumer; keep their wire names raw.

The message-list UI adds several direct consumer facts without proving a
server schema: the 20-byte key slots drive 421/423 and the row-removal/marker
helpers; the 21-byte string slots feed the `MSG_NAME`/reply path; the final
2-byte-stride string is compared with `F` and `M` to select friend-action or
reply controls; and a separate local dword array is formatted as `MSG_TIME`.
The dump does not show the wire raw4 being written to that dword array, so
raw4 must not be renamed timestamp. The 201-byte string has no recovered direct
address join to the `MESSAGE` control, and raw2/header/context remain raw.

The adjacent 421/422 and 423/424 readers consume `u8 statusRaw, str key`;
nonzero 422 removes the matching key and nonzero 424 writes marker `89`.
For 434, the client stores up to 100 string/raw4 rows and can immediately
construct a comma-separated 435 request from the stored strings; the 436
follow-up then updates online/location/channel state through a separate reader.
That handshake does not assign a meaning to 434's raw4. The corresponding
`Extracted/ui/lang/msgtableres.lang` entries selected by the native handlers
include the recipient-name check (`0x1E3`), message-send failure (`0x1E4`),
message-receive failure (`0x1E7`), friend self/duplicate/success/reconnect/not-
registered strings (`0x1E8..0x1EC`), and the `0x1ED..0x1EE` nonzero 432 paths.
These are localized branch text facts, not a complete server status enum.

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

**cond36 ↔ 994 事由碼的交叉印證 (本輪)。** 重新獨立解 `Quest.pat`
(844 條、47 欄, 分布與上表逐項相同) 後逐條列出 19 個 cond36 任務，
其**任務名稱直接點名** `sub_6750B0` 解出的 assist 事由:
「今日のパルプ」↔ 105/106 `ASSIST_PULP(_DESTROY)`、
「染料運搬の報酬」↔ 104 `ASSIST_DYE`、
「敵地占領指令」↔ 107 `ASSIST_OCCUPY`、
「設置と解除」↔ 101/103 `ASSIST_BOMB_PLANT`/`_DESTROY`。
目標值自 2 遞增至 100 (アシスト訓練 1..7 → 試験(練習) 50 → 試験(本番) 100)。
這確立 **994 是 cond36 的事件源**這條既有推論的第三方證據;
但任務進度如何由 994 推進仍是 server 政策, 維持 UNRESOLVED。
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
【199→200 MyItem】REQ 空! `sub_570A00` constructs opcode 199 with no
  payload; observed lobby `+1905` and scene `+748` state-machine callers show
  `INFORMATION`/`MYINFO` completion before the request, and client displays
  resource string `0x66`「載入中」. Server projection may send success 200 from
  start 0 — 199 has no start field; 200 見 §3.3
【201 GL_MYPARTSUP_ACK】(sub_95A3B0): s32 count × 20B 條目
  `{raw4 key0, raw4 key1, raw1 kind, raw4 value, raw4 period}`。
  `sub_95A4A0` 的 duplicate/update key 是 wire 前兩欄；weapon/part catalog
  projection 與 pair shape 相容，但 value/period 與兩 key 的 wire 命名仍分開保留。
【202 GL_EXPIRE_PARTSUP_ACK】(sub_95AE40): 同構；每筆 wire 前兩欄
  以 `sub_95A800(key1,key0)` 反序刪除 native pair。這個 callee argument
  順序是 native fact，不足以把欄位命名成 `(part,gun)`。
【250→251 LobbyIn】REQ 空 ×2 builder (sub_574080 帶 state:=2 /
  sub_584FE0 純送); 251 死協定 (無 case) — server 不回 ✓
【254→255 InvenIn】REQ 精確為一個 `u8 requestContextRaw` (sub_5741C0;
  呼叫端從目前 UI/entity 物件 `+4` 取得，**語意 UNRESOLVED**，不得再稱倉庫頁籤)，
  builder 帶 state:=7。255 (sub_574270) 的 **Fact / HIGH** wire branches:
  `u8 mode(0/1), s32 uid, u8 contextRaw, u8 unknownHeaderRaw`; 僅 mode==0
  另讀 `{u8,u8,s32}` 並以其中 slot 作 `sub_67D870` remote-user lookup。
  若前述 uid==本機 `EE8CB4`，再嚴格讀 `u8 selectedProfile(<5)` + 160 raw bytes
  (=5×32B)。`sub_4BDD80→sub_4AAB80` 將其載入 **五個 NewSkill profile**，絕非
  倉庫頁狀態：每 record 是 `7×s32 puzzle IDs + s32 packed-minute expiry`。
  profile 0 的尾字被 UI 強制忽略；1..4 的尾字經 `sub_48B9A0→sub_5309C0` 解析為
  YYYY(2000+top byte)/month/day/hour/minute，剩餘整分鐘不正時不可用。**Fact / HIGH:**
  native local-time conversion uses `_mktime64`, so bit-field values such as
  month 0/31, day 0/63, hour 63, minute 127 are normalized rather than a separate
  malformed-date wire error; raw zero is simply an already-expired profile 1..4.
  **Inference / MEDIUM:** 255 沒有角色索引且 snapshot 以 uid 定址，故 profiles
  應為 user/account-level，而非 198/247 的 character 12-slot 外觀。
【783→784 NewMsgCount】REQ 空 (sub_5643E0); 784 (sub_564480):
  s32 count → dword_F0C104 → UI vtbl+72(count!=0) 信箱紅點
【791→792 VoiceItemSlot】REQ 空 (sub_885590; 無 debug 字串 — 類名
  CVCustomizeManager 由 793/794/795 的 debug 字串洩漏); this+289 防重入;
  792 ACK 經 sub_885D00 → CMyVoiceCustomize **vtbl+12** = sub_876B00 解析,
  佈局 = u8 char_idx + 全量單角色塊 (86B):
    `u8 char_idx, s16 base_voice1, s16 base_voice2, 3×9×{s16 item, u8 flag}`
  (⚠ 卌七輪更正: 792 是**整塊覆寫**, 非 795 變體A 差分鏡像)
【793→794 VoiceItemSlotAll】REQ 空 (sub_885C00 = SendPacketMyVoiceCustomizeAll);
  794 經 sub_885DA0 → CMyVoiceCustomize **vtbl+16** = sub_876C90 解析:
    `u8 count, count×(u8 char_idx + 全量單角色塊)`
  (⚠ 卌七輪更正: 794 是 **u8 count 前綴**, 非 795 變體B 的 20×s32;
   server 送 count=15 即全角色, 總長 1 + 15×86 = 1291B)
【795→796 ChangeVoiceSlot】REQ 兩變體 (client→server 依長度判別 —
  B 固定 20×89=1780B, A ≤117B):
  A (sub_885F10, 單角色差分): `u8 char_idx, u8 base_changed,
    [s16,s16], 3×{u8 n, ≤9×{u8 slot(1..9), s16 item, u8 flag}}`
  B (sub_886330, 全量): `20×{s32 char_idx, s16, s16, 3×9×{s16,u8}}`
  796 ACK (sub_885E40): `u8 err, u8`(第二 byte 讀而未用); err≠0 →
  訊息 0x3FB「ボイスカスタマイズ設定保存に失敗しました。設定内容を
  もう一度確認してください。」並重拉 792 回滾 (呼叫 sub_885D00(this, a2, -1, 0)); this+290 pending 佇列
  自動重送 (791 進行中 this+289 的變更先入佇列)
【⭐ sub_885D00 全景呼叫圖 (五十一輪全 exe 逐呼叫點定案)】
  `int __thiscall sub_885D00(this, packet, slot_or_char, mode)`
  - `this`: CVCustomizeManager 單例 (sub_44D750)
  - `mode==1`: 本地角色語音 → CMyVoiceCustomize::sub_876B00 (vtbl+12, 讀 86B)
  - `mode==2`: 房間成員語音 → CGameInUserVoiceCustomize::sub_8765F0 (vtbl+12, 讀 85B: `s16,s16,27×{s16,u8}`)
  - `mode<=0 / slot<0`: 使用快取的 mode/slot (792/796 回退專用)
  **全 exe 共有 6 個封包呼叫 sub_885D00 解析語音塊**:
  1. `114 GR_ENTERROOM_ACK` (sub_56B360): case 1(單人進房)、case 2(自身進房成員迴圈)、case 3(單人更新) 皆以 mode=2 讀 85B 尾塊
  2. `269 GL_JOINPLAY_ACK` (sub_574B20): case 6(玩家加入快照)、case 7(觀戰全房成員迴圈) 皆以 mode=2 讀 85B 尾塊
  3. `765 GL_CLAN_TNMT_ENTERROOM_ACK` (sub_57E9A0): case 1/2/3 錦標賽進房成員條目皆以 mode=2 讀 85B 尾塊
  4. `985 GL_ENTERMATCHINGROOM_ACK` (sub_586610): case 1/2 配對房進房成員條目皆以 mode=2 讀 85B 尾塊
  5. `792 GL_VOICEITEMSLOT_ACK` (sub_58B010 dispatcher case 792): 以 mode=0/slot=-1 經 CMyVoiceCustomize 讀 86B
  6. `796 GI_CHANGE_VOICEITEMSLOT_ACK` (sub_885E40): err!=0 儲存失敗時呼叫 sub_885D00 回滾設定
【語音 char_idx】0..14 = maru/nari/dallae/lich/cacao/loki/hana/momo/
  wooka/pero/spy_11/robotgirl_12/tsunderegirl/magicgirl/devilgirl
  (sub_8859B0 名字表; character/models/type1..15 = idx+1); 15..19 為
  client 變體B 保留槽 (全 0)。**char_type 為 1-based** (1..15 = 同序
  15 角色; spy_11/robotgirl_12 的名字即內嵌其 char_type 11/12), 故
  語音 char_idx = char_type − 1。voice_item = 語音表偏移 (unk_EAFC40
  起, 0=角色原生), flag = 該槽位置 1..9 (0=未自訂/預設; UI 選格時寫
  i+1, server 原樣回傳)。base_voice×2 s16 選語音組 (voice_customize_path.xml
  的 sounds index) — 3 類 command/tactics/infomation ×9 句與
  voice_customize_contents.xml 的 command_1..9/tactics_1..9/
  infomation_1..9 互證 (Extracted 實測)。⚠ base_voice1(戦闘)/base_voice2
  (感情) 是**成對批次更動**: 訊息 0x3F9「戦闘/感情ボイスは個別設定が
  できません…一括変更」= 選語音組時兩者一起換 (UI 五頁籤 FIGHT+EMOTION
  合併於 VoiceCustomize_Fight_Emotion.xml 的佐證); 27 槽才各自獨立。
  ⚠ 變體B (sub_886330) 全 exe 無呼叫者 = **client 死碼**, 實際只會
  收到變體A。語音檔實體 `sound\soundsNN\<codename>\Radio_Message\`
  = command/tactics/information 三夾, `Voice\` 夾 = 本嗓 — 見 RESOURCES.md。
【378→379 RadioMsg 無線電語音廣播】
  378 REQ (sub_5593A0): `u8 team, u8 face(0..26 選單序號=3類×9句), u8 slot, u8 len(≤64), wchar[len]`
  379 ACK (sub_74C500): 欄位同構; client 依 face/slot/team 查 CVCustomizeManager 播放對應 wav
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
265 GL_JOININFO_ACK (10 欄頭 + 玩家清單; 見 §3.15f 逐欄定案): {u8
    status, u8 map, u8 count, u8 B, u16 slot_mask, u8 C, u8 time,
    u16 round, u8 item, u16 G} + count×{u8 slot, str name, u8}
    — 房單進房的目標房資訊 (非「跟隨好友」; 舊誤記)
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
702 PEPACHI_LIST_REQ: 無; 703 ACK: `{s32 normalCount,s32 rareCount,
(normalCount+rareCount)×s32 itemId}`. Negative counts trigger the client’s
“Invalid DB Pepachi Data” diagnostic; this is a catalog list, not a probability table.
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
    └ 事由碼字彙 (sub_6750B0, 外層 guard `!=0 && <0x6D`):
      1=ASSIST_DAMAGE 2=ASSIST_AIRSHOT 3=ASSIST_HP (跨模式)
      101=BOMB_PLANT 102=BOMB_EXPLO 103=BOMB_DESTROY 104=DYE
      105=PULP 106=PULP_DESTROY 107=OCCUPY 108=GOAL (模式專屬)
      編碼刻意不連續; 108 為本 revision 合法上界。點數值與門檻是
      service policy (Wiki 歷史值), 非 client 事實 — 見 WIKI_MECHANICS.md §5b-1。
155/156 Y_UDP_HOLE_INF: catalog names only. `sub_595E80`'s verified switch has no 155/156 case; their transport role is **UNRESOLVED**.
無 payload 通知: 766/778/811/833/889/908 (純觸發)
```

### 3.15d3 GG 戰鬥中繼 — 逐函數定案 (本輪更正廿五輪「三模式」簡化)
⚠ 廿五輪把 GG 全族簡化為「slot 前綴轉發」是**錯的** — 逐函數重讀後
各族佈局不同, 已按下列真值重寫 server (compile-time handler discovery plus direct Battle request-family sources):

1. **TH 駭入/炸彈簇 316-331 — REQ 首欄是「team」(0/1) 不是 slot**:
   ```
   316 GG_HACKSTART_REQ  (sub_556E90): u8 team
   317 _ACK (sub_557040): u8 team, u8 slot        → sub_766490(team<2 定址隊列)
   318 GG_HACKSUCC_REQ   (sub_5571E0): u8 team, 6×f32 (爆點座標/計時)
   319 _ACK (sub_557400): u8 team, 6×f32, u8 slot → sub_7664C0
   320 GG_HACKFAIL_REQ   (sub_557580): u8 team
   321 _ACK (sub_557730): u8 team  (⚠ 無 slot!)
   322 GG_BOMBSUCC_REQ   (sub_557830): 空
   323 _ACK (sub_5579A0): u8 team  (爆炸的炸彈屬哪隊)
   326/328/330 GG_UNHACK* (sub_557B80/557DE0/5580A0): u8 team
   327/329/331 _ACK (sub_557D30/557F90/558250): u8 team, u8 slot
   ```
   → ACK = REQ 原欄位 + **尾附**發話者 slot (321/323 例外不加 slot);
   323 的 team 由 318 武裝成功時記下 (room.BombTeam), 未植彈即收 322
   則忽略 (316 開駭失敗不記隊)。

2. **復活六連同構**: 342 SOLO / 360 TSUR / 455 EXERCISE / 746 PNR /
   909 OCC / 971 SOCCER — REQ `s32 token` → ACK
   `u8 slot, u8, s16 x, s16 y, s16 z`。(455 例外: REQ = `s32 s8`,
   sub_55C620 多寫一個 s8 旗標 byte_1D37B79, server 讀 s32 即可忽略 s8;
   449 GG_STEALRESPON 無 builder 亦無 dispatcher case, 未納入)

3. **聊天四連 344-350**: REQ = `s32 tex, u8 slot, str msg`; ACK
   345/347/349/351 (sub_58D870/58D8A0/58D8D0/58D900 → sub_74A5F0 →
   sub_748E40) 讀 `s32, u8, str` — **同構但必須以 ACK opcode 廣播**
   (REQ opcode 344/346/348/350 在 dispatcher 無 case, 會被 client 忽略)。
   n3_1 = 0/1/5/6 對應 live/team/dead/teamdead 顯示通道。

4. **足球 964/967**: REQ 皆空 (sub_565F60/sub_566120); 965/968
   (sub_566040/sub_566200) 讀 `u8 flag, u8 slot` — flag 0 = 事件成立
   (得球/進球), 1 = 收回 → server 回 [0, slot]。

5. **奪寶 443/445/447 — 不可轉發**: REQ `u8, s16` (sub_55BDC0/sub_55C060);
   444/446/448 (sub_55BE80/sub_55C120) 讀 `u8, u8, u16×3` 分數組 —
   需奪寶計分狀態機才能產出, server 目前**不註冊** (送錯比不送更糟)。

其他未展開: 737/739/741 破壞, 730 奪紙漿, 820 檢舉, 902/906 佔領,
749 GIMMICK (s32×2+u8), 752 地圖重載, 962 掉落武器 (REQ 6 欄 →
ACK 13 欄 = server 附 drop_id+item 詳情); 474-483 射擊館 — 476 END
成績塊 (raw24 = {tick, 0, user_no, uid, score, wave} 6×s32; raw44
= {…, [12]=命中, [7]/[8]/[9]=擊殺分類, [5]=fever} 11×s32, sub_8EE1D0);
478 CHECK raw36 防作弊快照; 716 快速槽 4×s16。
334/336/338 SEEDKEY/UNIQUEKEY/DETECTCRACK = 反作弊挑戰 (安全模組直接
組包, 私服可忽略)。

### 3.15d3a OCC 與地面武器 — dispatcher/解析器交叉驗證 (五十六輪)

本節只記錄已由 **REQ builder → `sub_58B010` case → ACK parser** 三處交叉
確認的資料；未知值不以 `0` 佯裝已知。

#### OCC 902–908

三個 C2S builder 完全同構：`sub_564CF0` (902 start)、`sub_565120`
(904 success)、`sub_565470` (906 fail) 都寫：

```
u8 point_id, u8 claimed_slot, s32 claimed_user_id
```

`point_id` 是 controller field + 1；ACK parser 對 `point_id - 1` 設下
`< 3` 防護（`sub_771490`、`sub_7713C0`），故可接受範圍是 **1..3**。
`claimed_user_id` 的 builder 來源是 `dword_EE8CB4`，即本機登入玩家 uid；
`claimed_slot` 是本機戰場 slot（bot 分支為 254）。伺服器因此必須以 session
的 room membership、slot 與 uid 驗證三者，而非把 client 自報身份中繼。

| C2S → S2C | 已確認 ACK layout | client 行為 |
|---|---|---|
| 902 → 903 | `u8 action, u8 point, u8 actor_slot, u8 capture_participant_count, s32 actor_uid` | `action==0` 時 `sub_564E30` → `sub_771490(point-1, actor_slot, capture_participant_count, actor_uid)` |
| 904 → 905 | `u8 action, u8 point, u8 slot_a, u8 slot_b` | 原版 Occupy `action==0` → `sub_771550(point-1, slot_a, slot_b)`；Renewal client 只讀前二欄，因此四欄 payload 對兩者皆安全 |
| 906 → 907 | `u8 action, u8 point, u8 actor_slot, u8 capture_participant_count, s32 actor_uid` | `action==0` → `sub_771670(point-1, actor_slot, capture_participant_count)` |
| 908 | `(空)` | `sub_565850` 純觸發 `sub_771770`，尚未由可重現條件證明何時可發 |

`RoomBattleState` 是每房、受 `System.Threading.Lock` 保護的短生命週期狀態：
start 只能認領 idle point、success/fail 只能由同一 `(slot, uid)` 轉換；`GR_START`
與 `GR_END` 都清空它。903/907 的 `controller_value` 寫入 controller `+24`；
`sub_768210` 只接受 `1..2` 的活動值、`sub_76A4B0` 對大於 1 加速計時，故存為
`CaptureParticipantCount`。目前尚無位置聚合器，start actor 是唯一可驗證的參與者，
所以由狀態導出 `1`，而非以 `0` 當 padding。905 的兩個 slot 都從已驗證的
start actor 取得，絕不信任 REQ 的自報欄位。玩家離房時會取消該玩家尚在
capturing 的 claim（已完成的據點留至本局結束），防止斷線 slot 永久鎖點。

#### 地面武器 959–963

dispatcher mapping 已定案：959 → `sub_5666D0` (create)、960 →
`sub_566B30` (destroy)、961 → `sub_566BF0` (initial/list)、963 →
`sub_5672E0` (get-and-drop ACK)。完整 wire 讀序：

```
959 create: u16 drop_id, u8 actor_slot, s32 actor_data, u16 weapon,
            s16 x, s16 y, s16 z, u16 meta_a, u16 meta_b, f32 value, raw[32]
960 destroy: u8 count, count×u16 drop_id       # id 0 提早停止
961 list:    u8 count, count×(959 的單一條目) # id 0 提早停止
962 request: s16 ground_drop_id, s16 action_offset, u8 quick_slot,
             s16 data_a, s16 data_b, f32 value
963 ACK:     u8 result;
             result==0 才讀 u8 actor_slot, s32 actor_data, u16 drop_id,
             u8 quick_slot, u16 weapon, s16 x, s16 y, s16 z;
             weapon!=0 才追加 u16 meta_a, u16 meta_b, u16 meta_c,
             f32 value, raw[32]
```

`sub_566F50` 證實 962 的精確寫入序；`sub_5672E0` 只以首 byte 判斷
`result == 0` 才讀後續，任何非零均是拒絕路徑。現階段 server 收到已驗證的
962 會只回請求 session 的 `result=1`：959/961 都是 S2C，專案尚沒有可從地圖
資料或原服封包驗證的掉落物 seed，因此不得偽造一個成功 963（會使 client 刪除
指定 drop id 並按未證實的座標／32B weapon state 建物件）。`result=1` 是此
boolean 分支的真值，不是假定的官方細分 error code。

下一步要讓 962 成功：先取得可重現的 959/961 capture 或可驗證的地圖掉落物
定義，再在 `RoomBattleState` 加入 `drop_id → 完整 959 state` 表，將「取舊物、
生成替換物、960 destroy、963 ACK」置於同一把 room lock；不可直接 relay。

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
195 REQ (sub_56FF40; CLobbyChannel 的 144 wrapper 每次收到 144 都會送):
    u8 group    (頻道群組 = 681 清單 3 組之序, CLobbyChannel+129)
    u8 channel  (組內頻道編號, +131)
    u8 rawFlag  (native bool, wire domain 0/1; local option-derived;
                 business meaning UNRESOLVED — sub_7338D0/sub_735DE0)

196 ACK (CLobbyChannel::sub_4179D0 case 196 — 不在 dispatcher!
         經 vtable 場景層分發):
    u8  result — sub_4177B0 表：
        1=成功 (417D00()[0]:=v17 active channel index, state:=2);
        0=頻道滿(0xDA), 2=階級限制(0x148), 3=必須有 clan(0x328),
        6=(0x3A6), 8=(0x3A7), 4/5/7/9=一般錯誤(0x1A5)
    s32 channel_id (→ sub_417D00()[1])
    u8  channel_index (result==1 時由 sub_4177B0 寫入 active channel)
    **僅 result==1 續讀**:
      str  udp_host      ⭐ UDP control endpoint (no P2P/NAT role inferred)
      s32  udp_port      (sub_58ED30 存 + sub_596E60 取 low u16 填 sockaddr)
      u8   endpoint_opaque → 1D0CFE4
      u8   channel_type (==3 → 續讀完整 AI/tournament 大塊 sub_875680，詳見
              docs/S2C_NATIVE_AUDIT_196.md；其四個固定 4-byte 欄位是
              raw4，不是 f32，後續含 capped/unbounded count loops；TS 只有
              明確 raw `type3Tail` 才會發送此 continuation)
      raw4 client_flags (sub_592AC0；bit0 → byte_1D0D21B，⚠ 非 f32)
      u8   client_default → sub_417D00()[8] (native read target 預設 5)
```
**196 成功後的閉環 (卅四輪)**: state 119:=2 → CLobbyChannel tick
(sub_415F90) 清 CClientData + 場景切換 sub_405EB0(9=大廳/8=AI 頻道
[this+148==3]/2=回放) → CLobbyMainRoom 進場自動送 107 (房間清單) —
**頻道→大廳鏈全閉環**。196 handler 經場景 vtable (sub_407360 的
vtbl+52) 分發, 與 CLobbyShop 同層 (引用計數 1 = 純虛表呼叫證據)。

cross-check: 681 的 3 頻道組 ↔ 195 的 group 序號互證；`ch_type` 是 195
第一 byte（projection `+129`），`ch_flag` 是第二 byte（projection `+131`）。
`ch_type==3` 的第三 wire byte 是 projection `+130`，由 lobby UI/state switch
消費；目前維持 raw，不因 196 的 type-3 continuation 或資源標籤替它命名。

### 3.15e GL_JOINPLAY_ACK (269) — sub_574B20, 1524 行巨型函數 (全鏈定案)
中途加入/觀戰的「全房間快照」。頂層: `u8 n7` switch:
- 0: 失敗, 通知 UI (sub_406F20(0))
- 1..5, 8, 9: 各種拒絕碼 (sub_406F20(n7))
- 6: **以玩家身份入房 (自身快照)** — `s32 uid, u8 char_slot(≤16),
  str nick` + CClientData 嵌入 (sub_524360) + `u8, s16, s16, s16`
  (角色外觀) + `s32, s32, s32 custom_tex, str(64)` + 4×武器組
  {s16 equipped, kk!=3 → s16×2... , equipped→8×s32 parts} +
  `u8 extra_flag` + (extra_flag≠0 時 8×s32) + sub_527550 技能 (9×s32) +
  sub_527D00 已選 NewSkill puzzles (raw n5+7×s32) + **sub_885D00 語音自訂 85B 尾塊**
  (`s16 base1, s16 base2, 27×{s16 item, u8 flag}`) — 對應 268 REQ flag==0 (PLAY)
- 7 (fall-through 主體): **觀戰加入 (完整房間+全成員快照)** — 對應
  268 REQ flag==1 (OBSERVE):
  房間頭: `s32 room_uid, s32 elapsed_ms (同 130 的時間基準), u8 map, u8 count(jj_1),
  u8 room_no, u8 modeIndex, u16 win, u8 max, u8, u16, u8 flags(bit0/1 拆),
  u8 has_pass, u16, u8, u8, u8 obs` + `u8×4 (模式旗標)`
  然後 count× 成員條目:
  `s32 uid, u8 slot, str nick, u8 team, u8 ready(1&1→0 特例),
  s32, s32, s32 custom_tex, u8 alive, u8 dead_flag` +
  [alive==0: 16B blob, u16×2, u16, u16×2, s8 觀戰目標] +
  `u8 char_slot, u8 char_type, 12×u16 equip (sub_524360)`, `u8 char_type, s16×3 外觀`,
  `s32×2, s32 tex_crc, str(64) tex_name`, 4×武器組 (kk!=3 帶 sub-slot, equipped→8×s32 parts),
  `u8 extra_flag` (+8×s32 若≠0), [模式特定: rule 12 足球 u8 / rule 13 占領 s32],
  `u8 active_weapon_flag` (+武器件組 若≠0), sub_527550 技能 (9×s32),
  sub_527D00 已選 NewSkill puzzles (raw n5+7×s32), **sub_885D00 語音自訂 85B 尾塊**
  (`s16 base1, s16 base2, 27×{s16 item, u8 flag}`), 5×u8 局內旗標。
  尾部: 7×u8 局狀態 + s32(n0x3E8) + u8 + {u8, s32, s16×2, u8} + 16×s32 比分/戰績。
私服要點: 快照結構 = 114 (ENTERROOM sub_type==2) 的戰時擴充版; 兩者成員
條目欄位順序一致 (交叉驗證), 269 多了戰鬥中狀態 (alive/dead/觀戰目標/武器件)。

### 3.15f GL_JOIN 簇 260-269 全流程 (四十九輪逐函數定案)

房單 (108) 點「入室」→ 進房/加入進行中遊戲的流程; 與 113 ENTERROOM
(快速/受邀進房) 的差別在於: 由房單 UI 驅動 + 密碼關卡 + 觀戰分支。
client 端函數地址見括號, 訊息文字出自 msgtableres.lang (§8)。

```
流程 (client 側狀態機, room 列表 UI 物件 dword_E9FE70):
  密碼房: 262 (u8 room_no str pass) ──sub_449810──► 263
          263 == 0 → 0x92「ルーム入室失敗」(房單停留)
          263 != 0 → 轉發 260
  一般:   260 (u8 room_no) ──sub_4498C0 (0x8E「ルーム入室中」)──► 261
          261 code: 0=0x3E「データ読み込み中」, 1=0x43「ゲーム終了中」,
                    2=可進房 → 發 264 (房在狀態11/可觀戰時另發 266),
                    3=0x32C「参加できるゲームルームがありません」
  264 (u8 room_no) ──sub_5156E0──► 265 房資訊 → 顯示房 UI (sub_515DE0)
  房 UI 點 PLAY/OBSERVE → byte_1D0CFE6=0/1 → 266 (u8 room_no u8 flag)
          ──sub_574910 (0x45「ゲーム参加要請中」)──► 267
  267 code 全為「接受」(sub_516900): 0/2/3→狀態1, 1/4→狀態2(存 flag),
          5→狀態5; code 1/4 把 flag 寫回 byte_1D0CFE6
  268 (u8 room_no u8 flag) ──CLobbyJoinGame::sub_43C730──► 269
          269: 6=玩家自身快照, 7=觀戰全房快照, 其餘=回房單(sub_406F20)
```

**265 GL_JOININFO_ACK (sub_574550) 逐欄** — 寫入「目前房」物件
(dword_EA063C; n2_0==3 TeamSurvival 分支寫 byte_E9FBA8, 佈局同構):
```
u8  status   → modeUI+12 (0=可進房/PLAY・OBSERVE 鈕可用; ≠0=禁)
u8  map      → +409  sub_515DE0 顯示「ROOM_MAP」
u8  count    → +1   人數
u8  B        → +408  存而不讀 (全程無讀者) ─ 送 0
u16 slot_mask→ +6   槽位點陣 (bit=1 可入座)
u8  C        → +410  存而不讀 ─ 送 0
u8  time     → +411  顯示「ROOM_TIME」
u16 round    → +412  顯示「ROOM_ROUND」
u8  item     → +414  顯示「ROOM_ITEM」
u16 G        → +416  存而不讀 ─ 送 0
count × { u8 slot, str name(24B 讀入 v14[6]), u8 讀後丟棄(v13 無引用) ─ 送 0 }
```
sub_515DE0 只顯示 map/time/round/item 四欄 — +408/+410/+416 與
114 的 +146/+150 同為「wire 送、client 存而不讀」, 送 0 安全。
資源佐證: `ui/PopUpJoinRoom.xml` 的控制項名就是這四欄 —
ROOM_MAP/ROOM_ROUND/ROOM_TIME/ROOM_ITEM (另 ROOM_NO/ROOM_NAME +
BTN_JOIN/BTN_CANCEL), 即 265 落地後彈出的「進房確認」彈窗; 房單
`ui/lobbymain.xml` 的 ROOM_PLAY/ROOM_WAIT 兩 sprite 即房在進行中/
等待中的狀態圖示 (ROOMSTART=開戰 129、ENTERROOM=進房 113)。

**267 GL_JOINGAME_ACK (sub_5749E0)** = `u8 code, u8 flag`:
sub_516900(room, v2, code, flag) — flag 於 code∈{1,4} 寫回
byte_1D0CFE6; 故 code=0(玩家)/1(觀戰), flag 回傳 268 的觀戰旗標。

**268 GL_JOINPLAY_REQ (sub_574A60)** = `u8 room_no, u8 flag`
(flag = byte_1D0CFE6: 0=PLAY 玩家 / 1=OBSERVE 觀戰)。

**269 成功態旗標對應**: code 6 ↔ flag 0 (玩家自身快照, 存進 slot
sub_56B310 找到的空槽 m_0[240780*slot]); code 7 ↔ flag 1 (觀戰,
房物件欄位 + 全成員快照寫入房單 entry sub_407E80(::this_15, room_no)
— 與 108 同款房物件欄位 +105/+109/+110/+128/+129/+130/+136/+144/
+146/+148/+150/+185, modeUI+12/+13 由 sub_74F450/sub_74F4D0 寫入)。

**房物件欄位再確認** (sub_53F830 ctor + 269 code 7 寫入路徑交叉,
與 §3.15b2 getter 定案一致):
+105 cur_players (sub_44E970), +106 has_pass, +107=1 (active 房),
+108 room_type_A / +109 room_type_B (ROOMTYPE 兩 bit, sub_44E7B0/
sub_44DA70), +110 u16 slot_mask (sub_53FB10 展開 +112..+127 逐槽
旗標 + 重算 +129=popcount), +128 double_damage, +130 map
(sub_540280), +132 modeUI (sub_53FBB0 以 mode 值 0..15 new 出
Cy*ModeLobbyUI), +136 time, +144 u16 win, +146 (存而不讀),
+148 u16 kill, +150 (存而不讀), +185 (bool)。

**目前 server-ts boundary（不是 original-service battle policy）**：
目前 `server-ts/src/ops/` 沒有 260/262/264、267/268 或 269 的 packet modules；
這些 native reader、snapshot shape 與 room-field consumer 只保留在本文件作為
future implementation evidence。沒有 process-local room state、battle owner、
計分、存檔與成功 response 的完整鏈以前，`server-ts` 維持未註冊與 fail-closed，
不把 client snapshot grammar 宣稱成原始服務 policy。

### 3.15c3 倉庫五連 855-863 (廿二輪 + 卌八輪補完 — n11==19 倉庫場景)
```
855 GL_MYWAREHOUSEINFO_REQ (sub_585550): s32 self_uid
    (= dword_EE8CB4 自己 uid; builder 同時彈 0x49B「アイテム情報を要請中…」)
856 GL_MYWAREHOUSEINFO_ACK (sub_4FAC80): u8 err; err==0 → raw 0x46=70B
    倉庫狀態塊 (i_45 @0x1D0D259) = 7 頁籤 × 10B:
      {s32 count, s32 到期(位元打包日期), u8 loaded, u8 pad}
    err!=0 → 不套用 UI (無訊息)。到期欄為 sub_48B9A0 打包
    (年-2000)<<24|月<<19|日<<13|時<<7|分; sub_5309C0 解出後以
    0x495「期間:残り %dヶ月 %d日 %d時間 %d分」顯示; 0=未持有 →
    sub_4FA950 回 1 (空)。count 供容量 gauge (sub_4F7760: 滿載變紅) —
    ⚠ 858 列表不回填 count, 僅 860/862 尾 s32 經 sub_4FB200 更新。
857 GL_MYWAREHOUSEITEMLIST_REQ (sub_585630): u8 tab (1..6;
    ⚠ sub_4FB3A0 以 i_45[10*i] (count 的低 byte) 當 tab token —
    count 低 byte ≠ tab 號時自動載入會選錯頁籤 (client 端 quirk,
    原版亦然); 開倉 UI CLobbyWareHouse::sub_4F5DF0 另以 1..6
    逐頁籤直送, 使用者手動點頁籤即正常。私服照送真實 count
    (供 sub_4F7760 gauge 正確), 此 quirk 僅影響開倉自動載入)
858 GL_MYWAREHOUSEITEMLIST_ACK (sub_4FACE0): u8 err, u8 tab;
    err 1..5 → 0x49C「ロッカー情報のロードに失敗しました。」;
    err==0 → s32 count, s32 total, count×{s32 slot(<0 停),
    s32 item_id(≤0 停; 需過 sub_535020), f32 f1, f32 f2, s32 period,
    u8 kind, u16 dura(複製為 dura_max)} — 28B 條目與背包同構!
    (count==total → 一次載完 → 標 loaded)
859 GL_PUSH_TO_WAREHOUSE_REQ (sub_585720): u8 tab + s32 inv_slot
    (5B; 由 sub_4F9800 呼叫 — 先 sub_4F9B10 檢查容量, 滿 → 0x493
    「ロッカーに空きがありません。」)
860 GL_PUSH_TO_WAREHOUSE_ACK (sub_4FAEF0): u8 err, u8 tab, s32 slot
    (6B header); err 1..9 → 0x49A「アイテム移動が失敗しました。」;
    err==0 → 28B 物品{s32 slot(新倉庫 slot), s32 item_id, f32, f32,
    s32 period, u8 kind, u16 dura} + s32 tab_count(sub_4FB200 更新 count)
861 GL_POP_TO_WAREHOSUE_REQ (sub_585820): u8 tab + s32 wh_slot (5B;
    由 sub_4F9860 呼叫)
862 GL_POP_TO_WAREHOSUE_ACK (sub_4FB020): u8 err, u8 tab, s32 slot;
    case 0 → 28B 物品 + s32 tab_count (sub_4F9900 → sub_4F9660 →
    sub_524F70 入背包, slot 欄 client 讀而不用、背包自動配槽);
    case 1/2/3/4/6/7 → 0x49A; case 5 → 0x49E「インベントリーに空きが
    ありません。」; case 8/9 → 無訊息
863 GL_CHANGED_WAREHOUSEINFO_ACK (sub_4FB180): raw 70B 狀態塊
    (頁籤租期異動才推; 私服頁籤租期恆定, 不需推)
容量 (sub_4F9B10): tab1=100, tab2/3/4=300, tab5/6=3000, tab0=0。
物品 kind = item_catalog.kind (0..20); period = 剩餘天數。
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
    送 143 (`String[24]` identity + n100 + ext_count 回送；identity 的帳號/
    nickname 語意尚未由 writer 證實) → server 回 144。
    完整雙握手: **登入伺服器 = 694 GL_ACCOUNTCONNSUCC → 682 → 681**;
    **頻道伺服器 = 693 GL_TCPCONNSUCC → 143 → 144**。
    681 的伺服器清單 (host+port+3頻道組) 就是頻道伺服器的位址來源;
    登入成功後 sub_43E450 → sub_5374F0 存頻道位址 → 使用者選頻道
    即連線 → 693 觸發。
```

### 3.15d 連線生命週期 103/141-144（bootstrap 再驗證）
```
103 GE_LOGOUT_REQ (sub_58D660): 無 payload — client 登出通知
141 PM_CONNECT_REQ (sub_556530): 無 payload。由 private UDP op18 的一次性
    latch 觸發，是 UDP manager 已配置後的 endpoint 再確認，而非 TCP 登入；這
    不能單獨證明 P2P/NAT/session-admission semantics.
142 PM_CONNECT_ACK (sub_5565D0):
    str endpoint_host (client fixed buffer char[20]，最多 19 ANSI bytes)
    s32 endpoint_port (raw 4B；sub_58ED30/sub_596E60 實際只取 low u16)
    u8  active_channel_index (寫入 `sub_417D00()[0]`)
    u32 packed_calendar_time (sub_534F20 解為 WORD 年/月/日/時/分：
        `(year-2000)<<24 | month<<19 | day<<13 | hour<<7 | minute`；wire
        沒有時區，server 必須明確選擇要編碼的 wall-clock zone)

143 PM_UDPSTART_REQ (sub_555C60; 693 的唯一 bootstrap 觸發):
    str identity (`String[24]`，此匯出尚不能定名，最多 23 ANSI bytes)
    s32 n100 (681 回送；native 讀寫皆為 4B signed value)
    u8  literal 1
    s32 ext_count (681 回送)
    這是可比對 handoff claim，不是密碼學 credential；server 必須把它綁定
    最近成功的 681 session。`String[24]` 的 writer 尚未定位，不能猜為
    account/nickname。

144 PM_UDPSTART_ACK (sub_555D50 完整讀取後才分支):
    u8  result
    u8  rank_restricted_server_flag (`==1 && rank>10` 顯示 resource 0x11C:
        「目前的階級不能連線到所選 server」)
    raw4 daily_login_value (存 dword_1D0D23C；native 只在 >0 時以 `%d`
        顯示 CP932 table 0xC9「本日 login confirmed, %d PG awarded」；
        UI 文字支持 PG 顯示單位，但 wire helper 是 raw4)
    str raw_string_v71 (native local char[40]，最多 39 ANSI bytes；reader 後未找到 consumer，不能由欄位位置定名 channel/name)
    raw4 post_name_raw_0 (sub_555D50 讀取後未找到 consumer)
    raw4 post_name_raw_1 (同上)
    raw4 restriction_value (result 6/8/9/10 使用低 byte 作 `%d`；8/10
        顯示 low byte - 1；不可縮成 u8)
    f32 restriction_value_float (result 7/8/9/10 的 `%.1f`)
    raw4 client_request_context (sub_592AC0 → dword_F2A684；client 隨後
        原樣帶入多個 request，但 server-domain 意義尚未證實，非已證實 s32)
    u8  has_net_cafe_info
    if nonzero: u8×4 + raw4×8，依序交 `sub_A1C800` 初始化
        `sNetCafeInfo`；完整可發送 shape 已在 TypeScript login ACK builder
        建模，四個 byte/八個 slot 的業務域仍未命名。

    result 1=正常成功（state 2, normal path），2=alternate success mode；
    3=版本不符(0xA4), 4=已連線(0xCF), 5=未授權 ID(0x11B),
    6=中級 channel level 限制(0x31B), 7=中級 channel K/D 限制(0x31C),
    8=light server 限制(0x32D), 9=beginner server 限制(0x321),
    10=intermediate server 限制(0x334), 101..108=帳號/認證失敗資源
    0xB6/0x10B/0x98/0xDB/0x11E/0x3F/0xC8/default。

    第二層 `CLobbyChannel::sub_4179D0` 的 case 144 **不再檢查 result**
    就呼叫 `sub_56FF40(group@this+129, channel@this+131)` 送出 195；因此
    server 即使拒絕 143，也應把未認證的後續 195 回成明確的 non-success
    196，而不可讓它取得任何 authenticated lobby authority。

195 GC_ENTERCHANNEL_REQ (sub_56FF40): `u8 group, u8 channel, u8 rawFlag`; native writes the third byte from a boolean result of the local option block, so its wire domain is `0/1`; business meaning remains UNRESOLVED.
196 GC_ENTERCHANNEL_ACK (CLobbyChannel::sub_4179D0，不走主 dispatcher):
    u8 result, s32 channel_id, u8 channel_index
    **只有 result==1** 才續讀 `str endpoint_host, s32 endpoint_port,
    u8 endpoint_opaque, u8 channel_type, raw4 client_flags, u8 client_default`。
    result 1 會把第三欄寫成 active channel index；0=channel full (0xDA),
    2=rank restricted (0x148), 3=clan required (0x328), 4/5/7/9=generic
    error (0x1A5), 6/8 有各自 resource。`client_flags & 1` 是已證實的
    native flag；`channel_type==3` 還要求完整 `sub_875680` continuation。
    TS builder 只在明確提供 raw `type3Tail` 時發送；channel admission 也只有
    在 config 提供該 tail 時接受 type 3，未配置時維持保守拒絕。
```

**Fact/HIGH — current Bun/TypeScript bootstrap guardrails.** `server-ts` 目前的
runtime modules 覆蓋 681/682/693/694、143/144 與 195/196；`PM_CONNECT_ACK`
(142) 的 calendar grammar 仍是 native evidence，沒有被冒充成目前 runtime module。
TS 保留 694 threshold、144 mandatory prefix/optional NetCafe tail、以及 196
failure-prefix/success-only endpoint tail 的 wire boundary；type-3 只有在完整
`type3Tail` 存在時才可送出。Bun tests 覆蓋 144 optional shape、196 success-tail
與 694 ceiling；142 的 native bit layout 仍由 packet/resource audit 維護。

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
169 GR_RULECHANGE_REQ  (sub_56F440, UI sub_42FE20): u8 modeIndex
170 GR_RULECHANGE_ACK  (sub_56F4F0→sub_42FE50): u8 modeIndex —
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
192 GR_CALLUSER_ACK    (sub_56FE10): 無 header; 依本機房狀態
                       sub_5376F0(byte_EE8968)=+24 是否==2 決定讀不讀
                       body — ==2 (在房內) 才讀 u8 caller_slot +
                       str caller_nick → sub_406DB0(dword_BEFEF0)
                       (→sub_449E80→sub_4FE8D0) 彈呼叫視窗; 否則只回
                       狀態碼無 body。server: 雙方同房才送 (192 無錯誤碼)
193 GC_CHANNEL_REQ     (sub_550790): u32 n2 — 戰隊頻道資料請求
                       (0=戰隊資訊, 1=成員分頁, 2=重置; 只由戰隊場景
                       發出: sub_422F30 刷新 / /l 指令)
194 GC_CHANNEL_ACK     兩場景兩解讀 (同 opcode 不同 dispatcher):
                       • 一般場景 sub_56FE90: 5×u8 存 byte_BEFF76[0..4]
                         頻道資訊 → sub_522440 刷新 lobby channel UI
                         (sub_48BF00 顯示 %03d/%03d 頻道人數/200)
                       • 戰隊頻道 dispatcher sub_54D040 case 194→
                         sub_54EE10: u32 n2 (0→sub_54EA60 戰隊資訊+
                         成員, 1→sub_54ECE0 成員分頁, 2→sub_54EDE0 重置)
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
+132  mode rule 物件 (16B, 有 vtable — 卌二輪逐欄定案, 卌五輪補語意):
        +4  = item bit0 (sub_74F450; 175/176; 112 建房預設 =1 開)
        +8  = item bit1 (sub_74F430; 175/176)
        +12 = 是否隊伍房 (u8 1/0 — sub_56A7B0 建房時以 sub_438990
              定案: mode∈{0,2,3,4,8,10,11,12,13}→1, 其餘→0; 卌五輪
              逐行確認, 非「待原服確認」)
        +13 = 隊打散開關 (u8; 368/369)
        +14 = 足球旗標 (u8; sub_74F4D0 寫/sub_74F4B0 讀, 969/970;
              讀取受 sub_67F410()=「是否 mode==12 足球」gate,
              UI 字串 SOCCER_CHECK_TEXT 佐證)
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
預設地圖寫 +130 (sub_540280)。server 現已鏡像 (RoomHandlers.ModeIndexDefaultMap,
client 實際載入的 `system/map_StartIndex.xml` — ⚠ ui/ 根目錄另有一份
舊版 modeStartIndex 不同, 以 system/ 為準): 0→106 1→104 2→14 3→107
4→23 8→51 9→89 12→98; 其餘 mode (5/6/7/10/11/13/15/16) 無條目 → 保留原圖。
⚠ 交叉確認發現: modeStartIndex[SOCCER]=98 但 98 是 TeamSurvival 圖
(TS_33_tutor_castle, modes=0x0002), 真正 soccer bit(0x4000) 落在 99/100
(スルルスタジアム) — client sub_426930 **就是寫 98**, server 鏡像不「校正」。
**mode→bit 地圖過濾 (卌七輪落地)**: server 在 111 建房 / 121 換圖 / 169
改模式時以 maplist `modes` bitmask 驗證 map↔mode 相容性
(RoomHandlers.ResolveMap): 該 mode 有 bit 且 map 不含此 bit → 回退預設圖,
預設圖亦不含時取目錄第一張含該 bit 的圖; 目錄查無/mode 無規則 → 原樣放行
(不硬編)。mode→bit 對照見 RESOURCES.md §4b。

### 3.15b3 TeamHacking 駭入/炸彈協定 317-333 (廿二輪 — TH 模式核心)
```
317 GG_HACKSTART_ACK  (sub_557040): u8 team, u8 slot — 開始駭入
319 GG_HACKSUCC_ACK   (sub_557400): u8 n2(team) + 6×f32 (爆點座標/計時) +
                      u8 slot — 駭入成功, 炸彈啟動 (⚠ 首欄 team 非 slot)
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

### 3.15c2 禮物操作 299/301/315（後續逐 consumer 修正）
```
299 GS_TAKEGIFT_ACK (sub_57AEF0 → sub_524DB0):
    s32 start; then at most 50 records, each beginning `s32 giftId`.
    `giftId == -1` terminates early; every nonterminal record is
    `{str sender, str message, s32 itemId, s32 rawPeriod, raw4}`.
    The client accepts records only while `start + ordinal < 1024`. The two
    strings are protocol strings, not fixed 21-/52-byte fields, and raw4 is
    not proven to be a float or an expiry policy.
301 GS_MOVEGIFT_ACK (sub_57AFE0): `{u8 outcome,u8 rawDetail,s32 itemId,
    s32 count}`. It contains no gift ID and no embedded entries. On outcome 1
    the client removes its first `count` cached pending records; outcome 2
    queues a fresh empty 298 request; outcome 0 shows special UI for rawDetail
    1 or 5. This proves a server/session-correlated batch action, not the old
    claim that the request identifies a particular gift. Exact server
    selection, duplicate, expiry, capacity, and wallet policy remain
    UNRESOLVED.
315 GS_MOVEONEGIFT_ACK (sub_57B500): initially
    `{u8 result,u8 rawDetail,s32 rawGiftKey,s32 itemId}`. If the item hits
    one decompiler-address-dependent special comparison it then consumes `str`;
    otherwise it consumes `{raw4,raw4,s32,u8,u16}`. A separate native
    item-family predicate can consume a following `str`. Thus it has no
    universal short failure/success arm suitable for fabrication.
311 GS_BUYCHAR_ACK   (sub_5728A0): u8 ok; ok → 6×s32 (角色解鎖
    +餘額組), u8 char_type, s32×2 — 買角色
313 GI_CHANGESLOT_ACK(sub_573320): 空 handler (只刷 UI) — 換槽免驗證
207 GS_BUY_WEAPONPARTS_ACK (sub_571B60): u8 ok; ok → (n11==20 特判)
    s32 gun_id, s32, u8, f32×2 + s32 balance×2 — 買改裝件
    (對應 weapon_parts_catalog 10,648 條)
```

### 3.15c 好友/訊息家族 419-441 (九輪讀畢; 439-442 本輪補完)
```
419 GL_MSG_ADD_REQ → 420 ACK (sub_559810): str to_nick, u8 x, u8 result
    (0=成功 1=對方拒收 2=信箱滿; 讀序 str→u8→u8)
421 GL_MSG_DEL_REQ → 422 ACK (sub_55A310): str key → `u8 statusRaw, str key`
    The client sends 421 only when this key exists in the 426 local table; 422 status
    nonzero invokes the local key-removal helper, while zero selects a localized error.
423 GL_MSG_READ_REQ → 424 ACK (sub_55A4F0): str key → `u8 statusRaw, str key`
    The client sends 423 only when the key is not already marked `89`; 424 status
    nonzero invokes the helper that marks the matching 426 entry `89`. The dump/UI
    prove this key/state transition, not a server database column named `msg_id`.
429 GL_FRIEND_ADD_REQ (builder): str characterName/key
430 GL_FRIEND_ADD_ACK (sub_55AA90): `u8 statusRaw, str characterName/key`
    Native status branches select resource IDs `0x1E8..0x1EC`, status 0 inserts the
    returned string into the 100-entry local friend table, and every response sends
    an empty 433 refresh request. Do not assign a complete result-code policy from
    the status values alone.
431 GL_FRIEND_DEL_REQ: str characterName/key → 432 ACK (sub_55AE10):
    `u8 statusRaw, str characterName/key`; status 0 removes the key from the local
    friend table and sends 433, while nonzero statuses only select localized paths.
433 GL_FRIEND_LIST_REQ: 無 payload
435 GL_FRIEND_INFO_REQ: one comma-separated string list assembled from 434
    row strings → 436 ACK (sub_55B2C0): u8 count, count×{str key, u8 online,
    [online: str where, u8 channel]} → sub_5382D0(key, online, where, ch+1)
439 GL_FRIEND_CHAT_REQ (sub_55B510): s32 uid(dword_F2A684), str my_nick,
    str friend_nick, str message (ANSI ×3; message ≤180 才送)
440 GL_FRIEND_CHAT_ACK (sub_55B660): u8 status, str nick1, str nick2,
    [status==2: str comment] — status:
      0=0x1EF「%s というキャラクター名は存在しません」
      1=0x1F0「%s さんはオフラインです」
      2=0x1D9「← %s さんのコメント」(comment=訊息本文)
      3=0x1D8「%s さんを見つけることが出来ませんでした」
    ⚠ nick1/nick2 讀後僅推進游標 (顯示靠全域伙伴名 unk_23193F0
    sub_401B20 + comment); client 不本地顯示己方訊息 → server 需回聲
441 GL_FRIEND_WHERE_REQ (sub_55B940): str nick (查所在位置)
442 GL_FRIEND_WHERE_ACK (sub_55B9F0): u8 status;
    status==1 → u8 where_type, u8 channel, u8 room_no —
      where_type 11=教學(0x314「%sさんはチュートリアル中です」),
      9/10=大師/線上(依 v25==當前頻道 → 進房 v24, 否則切頻道),
      其他=大廳(0x21E「%sさんはロビーで待機中です」, channel/room 不讀)
    status==2/0 → 0x21D「%sさんの情報が見つかりませんでした。\r\n
      リトライしてください。」(只讀 status, 不讀後續)
```

### 3.15g comm 簇 437/438・378/379・726/727・836/837 (本輪逐函數定案)

### 3.15h 系統、角色、商城、投票與轉蛋封包簇 (五十三輪全鏈定案)

| Opcode | 封包名稱 | 來源函數 | 方向 | Wire 格式 |
|---|---|---|---|---|
| 685 | `GL_TUTORIALINDEX_REQ` | `sub_55C6F0` | C2S | `(空)` |
| 686 | `GL_TUTORIALINDEX_ACK` | `sub_55C790` | S2C | `s32 tutorial_index` (旗標/步驟) |
| 689 | `GL_TUTORIAL_INDEX_SET_REQ` | `sub_55C7D0` | C2S | `s32 tutorial_index` |
| 690 | `GL_TUTORIAL_INDEX_SET_ACK` | *(無可達 handler — 舊記 `sub_582530` 在任一份 dump 皆不存在；dispatcher 無 case 690，`LAYOUTS.md` 亦無此列)* | S2C | **UNRESOLVED** — 名稱有在 `sub_9D2050` 註冊，但本 revision 找不到任何讀取器，欄位無從證實 |
| 704 | `GL_LEVEL_KILL_LIMIT_REQ` | `sub_582570` | C2S | `(空)` |
| 705 | `GL_LEVEL_KILL_LIMIT_ACK` | `sub_55C9B0` | S2C | `s32 kill_limit, f32 exp_rate, s32 max_level_limit` (12B) |
| 706 | `GL_BILLTOKEN_REQ` | `sub_460480` | C2S | `(空)` |
| 707 | `GL_BILLTOKEN_ACK` | `sub_46AD00` | S2C | `str token` |
| 787 | `GL_RACKINGWEB_TOKEN_REQ` | `sub_581E40` | C2S | `(空)` |
| 788 | `GL_RACKINGWEB_TOKEN_ACK` | `sub_44BEA0` | S2C | `str token` |
| 834 | `GL_DATA_RECV_COMPLETED_REQ` | `sub_583120` | C2S | `s32 raw client request context` (原樣取 `dword_F2A684`, 與 144 的 propagated raw4 共用；不可命名為 user_id) |
| 835 | `GL_DATA_RECV_COMPLETED_ACK` | `sub_5831D0` | S2C | `(空)` |
| 370 | `GL_CHANGECHANNEL_REQ` | `sub_570030` | C2S | `u8 channel_id` |
| 371 | `GL_CHANGECHANNEL_ACK` | `sub_570100` | S2C | `u8 status, u8 channel_id, str host_ip, s32 host_port, u8 extra`; client passes this independently to `sub_596E60` (secondary UDP address field). Its relation to successful-196 primary endpoint is **UNRESOLVED**; do not merge endpoint state. |
| 131 | `GR_FORCEOUT_REQ` | `sub_56EC10` | C2S | `u8 target_slot` (房主踢人) |
| 132 | `GR_FORCEOUT_ACK` | `sub_56ECC0` | S2C | `u8 status(1), u8 target_slot` (廣播並移除成員) |
| 718 | `GR_START_VOTING_REQ` | `sub_A191D0` | C2S | `s32 target_slot, s32 reason, s32 initiator_slot` |
| 719 | `GR_START_VOTING_ACK` | `sub_9BF430` | S2C | `u8 status(1)` (給發起人) |
| 720 | `GR_START_VOTING` | `sub_9BF430` | S2C | `s32 target, s32 reason, s32 initiator, s32 duration, u8 team` (廣播) |
| 721 | `GR_DO_VOTING` | `sub_A192B0` | C2S | `u8 vote` (1=同意, 2=反對) |
| 722 | `GR_VOTING_RESULT` | `sub_9BF430` | S2C | `s32 target, u8 result` (1=通過踢出, 0=否決) |
| 214 | `GM_CREATECHAR_REQ` | `sub_572EB0`（舊記 `sub_532AA0` 在任一份 dump 皆不存在，本輪更正） | C2S | `u8 char_type, s16 hair, s16 face, s16 coat`（builder 依序 `sub_592920` + 3×`sub_5929E0`，共 7 B）|
| 215 | `GM_CREATECHAR_ACK` | `sub_572F80` | S2C | `u8 status(0=成功)` |
| 218 | `GI_CHANGEDATA_REQ` | `sub_572FC0` | C2S | `u8 char_slot` |
| 219 | `GI_CHANGEDATA_ACK` | `sub_573230` | S2C | `u8 status(1=成功)` |
| 220 | `GI_CHANGEWP_REQ` | `sub_573340` | C2S | `u8 count, repeat weapon_group` |
| 221 | `GI_CHANGEWP_ACK` | `sub_5735F0` | S2C | `u8 count(4), 4×weapon_group` |
| 312 | `GI_CHANGESLOT_REQ` | `sub_573270` | C2S | `u8 slot_no` |
| 313 | `GI_CHANGESLOT_ACK` | `sub_573320` | S2C | `u8 slot_no` |
| 466 | `GI_CHANGE_SKILLITEMSLOT_REQ` | `sub_5738A0` | C2S | `u8 target_profile, u8 previous_update_raw, [u8 previous_profile, 7×s32 puzzle]`; raw 0→2B, nonzero→31B |
| 467 | `GI_CHANGE_SKILLITEMSLOT_ACK` | `sub_573A70` | S2C | `u8 resultRaw, u8 unknownHeaderRaw, u8 count, count×{u8 profile, raw32}` |
| 912 | `GL_WEAPONPARTS_EQUIP_CHANGE_REQ`| `sub_95AEF0` sender / `sub_9591F0` part lookup | C2S | op 0 remove / 1 install: `u8,s32 weapon,s32 part`; op 2 replace: plus `s32 old_part` |
| 913 | `GL_WEAPONPARTS_EQUIP_CHANGE_ACK`| `sub_95B180` | S2C | `u8 errorRaw`; only `0` continues with the matching 912 body; nonzero error values unresolved |
| 310 | `GS_BUYCHAR_REQ` | `sub_572790` | C2S | `s32 char_type, 5×s32 items` |
| 311 | `GS_BUYCHAR_ACK` | `sub_5728A0` | S2C | `u8 status(1), s32 slot, s32 char_type, s32 exp, s32 cash, s32 gp, s32 dura` |
| 453 | `GS_DELETEGIFT_REQ` | `sub_57BC40` | C2S | `s32 gift_uid, s32 item_id` |
| 454 | `GS_DELETEGIFT_ACK` | `sub_57BCF0` | S2C | `u8 status` (only exactly 1 mutates the local cached list), `s32 gift_uid, s32 item_id` |
| 802 | `GS_DESTROYITEM_REQ` | **UNRESOLVED** | C2S | The earlier five-field claim was not an evidenced packet constructor (`sub_894E70` is not one). Do not consume request-dependent fields or mutate inventory until the actual builder and its caller are reconciled. |
| 803 | `GS_DESTROYITEM_ACK` | `sub_895EE0` | S2C | `u8 result, u8 raw_code`; if `result!=0`, then `u8 affected_count` + `affected_count×{s32 raw_id,u8 raw_value}`. The success arm instead consumes `s32 raw_value_a, s32 coupon_after, u8 affected_count` + `affected_count×{s32 item_id,s32 remaining_raw}`. Only the failure arm is currently safe to emit. |
| 423 | `GL_MSG_READ_REQ` | `sub_55A3C0` | C2S | `str key` (native sends only when the 426 local key is not marked `89`) |
| 424 | `GL_MSG_READ_ACK` | `sub_55A4F0` | S2C | `u8 statusRaw, str key`; nonzero invokes the local `89` marker helper |
| 876 | `GQ_QUEST_ACCEPT_DAILY_REQ` | `sub_91D730` | C2S | `(空)` |
| 877 | `GQ_QUEST_ACCEPT_DAILY_ACK` | `sub_91D7E0` | S2C | `u8 err(0), s32 count(0), count×13B snapshot` |
| 878 | `GQ_QUEST_USER_COMPLETE_HONOR_REQ` | `sub_91C9D0` | C2S | `s8 flag` |
| 879 | `GQ_QUEST_USER_COMPLETE_HONOR_ACK` | `sub_91CAA0` | S2C | `u8 err(0), str title, raw blob` |
| 698 | `GP_ENTER_PEPACHI_REQ` | `sub_46E080` | C2S | `(空)` |
| 699 | `GP_ENTER_PEPACHI_ACK` | `CLobbyShop::sub_46AD00` case 699 | S2C | `u8 status, s32 rawA, s32 rawB`; only status 1 enters the Pepachi scene. The two words are not proven currency fields. |
| 700 | `GP_START_GAME_REQ` | `sub_8458D0`, called by `sub_8459C0` | C2S | `u8 paymentDrawSelector, s32 selectedCharacterId`; exact 5-byte body. The second field is `19,900,000 + (sub_525790(activeCharacter) % 100000)`, not a coin type or draw count. |
| 701 | `GP_START_GAME_ACK` | `sub_84A000` → `sub_84A490` | S2C | `u8 result, u8 rawCode`; only `result==1` continues with `s32 rawA,s32 rawB,u8 rawMode,u8 prizeCount, prizeCount×{s32 reelA,s32 reelB,s32 reelC}` (client processes at most 11 prize triples). **`reelC` 是伺服器指定的「演出級別」**，不是外觀參數 — 見下方 §3.15p。 |
| 702 | `GP_PEPACHI_LIST_REQ` | `sub_45C9B0` | C2S | `(空)` |
| 703 | `GP_PEPACHI_LIST_ACK` | `CLobbyShop::sub_46AD00` case 703 | S2C | `s32 start, s32 count, (start+count)×s16 signedEntry`; `{0,0}` is a structural empty list only—not a probability-table assertion. |
| 900 | `GS_CAPSULEMACHINE_START_REQ` | `sub_99CFA0`, called by `sub_99D0A0` | C2S | `u8 paymentSelector, u8 drawCount`; exact two-byte body, not an `s32 machine_id`. |
| 901 | `GS_CAPSULEMACHINE_START_ACK` | `sub_9A1A30` | S2C | `u8 result, s32 prizeCount, prizeCount×{u8 rawClass,s32 rawA,s32 rawB}, s32 rawTailA,s32 rawTailB,s32 rawTailC`; `result==0` performs local state/reward processing, nonzero shows failure UI. |



```
437 GG_ROOMBROADCAST_REQ (sub_55B430): u8 flag + s32 len + raw[len]。
    ⚠ flag/blob 語意無從確認 — builder 無直接呼叫者 (經函式指標/訊息表),
    且 dispatcher 與房訊息表皆無 438 case (client 從不解析 438), 屬
    遺留/特殊工具 opcode。server 依 REQ→ACK 慣例原樣轉播全房 (438 同構),
    不硬編欄位。

378 GR_RADIOMSG_REQ (sub_5593A0): u8 team(*(player+320) 0/1), u8 face
    (頁*9+項目, 0..26 無線電選單), u8 slot(發話者自身 sub_67D010),
    u8 len(≤64 wchar 字數), wchar[len] (2*len bytes)。
    builder 呼叫者 (radio 選單): n8=項目(0..8) + 9*頁(0..2), v9=*(player+320)
379 GR_RADIOMSG_ACK (sub_74C500): 與 378 完全同構 — u8 team, u8 face
    (n2=face/9 列, n8=face%9 行), u8 slot, u8 len(≤0x40 截斷),
    wchar[len]。以 slot 定位發話者、face 查選單語音 (sub_889A10),
    僅 n2!=2 (非戰鬥中) 且收者非自己時顯示 → server 原樣轉播全房即可。

726 GG_OBSERVERCHAT_REQ (builder case 10 @4169xx): str my_nick, str message
    ⚠ ANSI str — 與 728 GR_OBSERVERCHAT 的 wstr 不同!
727 GG_OBSERVERCHAT_ACK (dispatcher 727 → sub_58D840 → sub_74A540):
    str nick, str message — 全房轉播。

836 GL_SHOUTCHAT_REQ (sub_583370): s32 uid(自己 dword_EE8CB4),
    s32 strlen, str text。client 前置: 3s 牆鐘限流 (dword_1D0D24C < 0xBB8)
    + CHAT_SHOUT 動作表 entry[4]!=0 (p_p_p_p_p_n1189, sub_526E20 掃 5120
    條目, key=unk_E975A8, entry[4]=+214)。
837 GL_SHOUTCHAT_ACK (sub_583C20): u8 flag(0/1 皆顯示), s32 uid,
    s32 timer, str nick, s32 raw_len, raw[raw_len] (訊息無 NUL, 長度前綴)。
    uid==dword_EE8CB4 (自己) → client 把 CHAT_SHOUT 動作表 cooldown 設為
    timer (0=不可再喊, 非0=可再喊)。timer 精確單位原服未明 (與 391 同款
    server 動作值), 私服送 1 保持可用, 由 client 3s 牆鐘限流防洗頻。
    GL = 大廳全域 → 廣播全服。
```

### 3.15i GM / MASTER 管理指令簇 (五十五輪全鏈定案)

| Opcode | 封包名稱 | 來源函數 | 方向 | Wire 格式與行為 |
|---|---|---|---|---|
| 275 | `MASTER_MEMO_REQ` | `sub_578830` | C2S | `wstr memo` (GM 私人通知) |
| 276 | `MASTER_MEMO_ACK` | `sub_578920` | S2C | `wstr memo` (顯示 GM 私訊彈窗) |
| 277 | `MASTER_MEMOALL_REQ` | `sub_5789D0` | C2S | `wstr memo` (全服系統廣播) |
| 278 | `MASTER_MEMOALL_ACK` | `sub_578BC0` | S2C | `wstr memo` (全體在線玩家彈窗廣播) |
| 279 | `MASTER_USERCUT_REQ` | `sub_578E50` | C2S | `u8 mode, str nick` (強制踢線) |
| 280 | `MASTER_USERCUT_ACK` | `sub_578FB0` | S2C | `(空)` |
| 281 | `MASTER_USERCUT2_REQ` | `sub_578F00` | C2S | `s32 uid` (依 UID 強制踢線) |
| 282 | `MASTER_USERCUT2_ACK` | — | S2C | `(空)` |
| 283 | `MASTER_ROOMCUT_REQ` | `sub_578FF0` | C2S | `u8 room_no` (強制解散房間) |
| 284 | `MASTER_ROOMCUT_ACK` | — | S2C | `(空)` |
| 285 | `MASTER_MSET_REQ` | `sub_578C70` | C2S | `u8 flag` (設定 GM 隱身/管理旗標) |
| 286 | `MASTER_MSET_ACK` | `sub_578D20` | S2C | `u8 flag` |
| 287 | `MASTER_PRINTUSER_REQ` | `sub_5796D0` | C2S | `(空)` (查詢在線人數) |
| 288 | `MASTER_PRINTUSER_ACK` | — | S2C | `s32 user_count` |
| 289 | `MASTER_USERINFO_REQ` | `sub_579780` | C2S | `str nick` (查詢玩家資料) |
| 290 | `MASTER_USERINFO_ACK` | `sub_579830` | S2C | `bool found, s32 uid, str nick` (全量快照) |
| 291 | `MASTER_LISTCUT_REQ` | `sub_579960` | C2S | `str nick` |
| 292 | `MASTER_LISTCUT_ACK` | — | S2C | `(空)` |
| 293 | `MASTER_USERINFODB_REQ` | `sub_579A20` | C2S | `str nick` (查 DB 用戶) |
| 294 | `MASTER_USERINFODB_ACK` | `sub_57A540` | S2C | `bool found, s32 uid, str nick` |
| 394 | `MASTER_ROOMINFO_REQ` | `sub_5790B0` | C2S | `u8 room_no` (查詢房內成員與 IP) |
| 395 | `MASTER_ROOMINFO_ACK` | `sub_579160` | S2C | `u8 count, count×(u8 slot, str nick, str ip)` |
| 402 | `MASTER_EVENTPAGE_REQ` | `sub_579430` | C2S | `f32 rate` (設定活動 PG 倍率) |
| 403 | `MASTER_EVENTPAGE_ACK` | `sub_579500` | S2C | `f32 rate` |
| 404 | `MASTER_EVENTEXP_REQ` | `sub_579580` | C2S | `f32 rate` (設定活動 EXP 倍率) |
| 405 | `MASTER_EVENTEXP_ACK` | `sub_579650` | S2C | `f32 rate` |
| 416 | `MASTER_KILLALL_REQ` | `sub_57C500` | C2S | `(空)` (全服強制踢線維護) |
| 417 | `MASTER_KILLALL_ACK` | *(inline, 無獨立 handler)* | S2C | `(空)` — dispatcher `case 417u` 直接顯示 msg `0xA5`「サーバーとの接続が終了しました。」再走 `sub_9A7DE0(msg, 1, 1)`；**不讀任何 payload** |
| 822 | `MASTER_CHAT_BAN_REQ` | `sub_582020` | C2S | `u8 mode, u8 duration_min, str nick` (禁言) |
| 823 | `MASTER_CHAT_BAN_ACK` | `sub_5827C0` | S2C | `u8 status` |
| 824 | `MASTER_USERLIST_REQ` | `sub_5821A0` | C2S | `u8 mode, s32 page` (分頁玩家清單) |
| 825 | `MASTER_USERLIST_ACK` | `sub_582890` | S2C | `u8 count, count×(s32 uid, str nick)` |
| 830 | `MASTER_CHAT_FORCE_BAN_REQ` | `sub_5820E0` | C2S | `u8 mode, str nick, s32 duration_sec` |
| 831 | `MASTER_CHAT_FORCE_BAN_ACK` | `sub_582BE0` | S2C | `u8 status` |
| 841 | `MASTER_SETALL_EVENTEXP_REQ` | `sub_5840B0` | C2S | `f32 rate` |
| 842 | `MASTER_SETALL_EVENTEXP_ACK` | — | S2C | `f32 rate` |
| 843 | `MASTER_SETALL_EVENTPAGE_REQ` | `sub_584160` | C2S | `f32 rate` |
| 844 | `MASTER_SETALL_EVENTPAGE_ACK` | — | S2C | `f32 rate` |
| 845 | `MASTER_VIEWALL_EVENTSTATE_REQ` | `sub_584400` | C2S | `(空)` |
| 846 | `MASTER_VIEWALL_EVENTSTATE_ACK` | `sub_584400` | S2C | `f32 exp_rate, f32 page_rate` |
| 883 | `MASTER_FIND_USER_REQ` | `sub_579AD0` | C2S | `s32 uid` (追蹤玩家所在頻道與房間) |
| 884 | `MASTER_FIND_USER_ACK` | `sub_579BC0` | S2C | `u8 status(1=找到), s32 uid, str nick, u8 channel, u8 room_no` |
| 885 | `MASTER_PLAY_WITH_REQ` | `sub_57A450` | C2S | `s32 uid` (GM 瞬移進入目標房間) |
| 886 | `MASTER_PLAY_WITH_ACK` | — | S2C | `u8 status` |

### 3.15j 遊戲中心 GameCenter 迷你遊戲協定 (五十五輪全鏈定案)

| Opcode | 封包名稱 | 來源函數 | 方向 | Wire 格式與行為 |
|---|---|---|---|---|
| 472 | `GL_GAMECENTER_REC_REQ` | `sub_584850` | C2S | `s16 game_id` (查詢小遊戲紀錄) |
| 473 | `GL_GAMECENTER_REC_ACK` | `sub_584910` | S2C | `s16 game_id, s32 high_score, u8 top3_cnt, u8 top10_cnt, u8 v24, u8 v35, s16 v28, s32 v30, raw16, u8 v23` |
| 474 | `GG_GAMECENTER_GAME_START_REQ` | `sub_584DB0` | C2S | `s16 game_id, u8 stage` |
| 475 | `GG_GAMECENTER_GAME_START_ACK` | `sub_584E80` | S2C | `u8 status(1), s16 game_id, u8 stage` |
| 476 | `GG_GAMECENTER_GAME_END_REQ` | `sub_564930` | C2S | `s16 game_id, raw24 score_data, raw44 stats_data` |
| 477 | `GG_GAMECENTER_GAME_END_ACK` | `sub_564A00` / `sub_76E450` | S2C | `s16 game_id, raw32, raw44, s16, s32 high_score, raw24, raw8, s32 score, s32 reward_gp, s32 reward_exp, s32 rank, s8, u8, u8, s8, s8` |
| 478 | `GG_GAMECENTER_GAME_PLAY_CHECK_REQ` | `sub_564A40` | C2S | `raw36 check_data` (小遊戲反作弊心跳) |
| 479 | `GG_GAMECENTER_GAME_PLAY_CHECK_ACK` | — | S2C | `u8 status(1)` |
| 480 | `GG_GAMECENTER_RANKING_REQ` | `sub_585320` | C2S | `s16 game_id, u8 mode` |
| 481 | `GG_GAMECENTER_RANKING_ACK` | `sub_585080` | S2C | `s16 game_id, u8 v18, s16 v13, s32 v14, u8 count, count×(0x38 排名條目)` |
| 483 | `GG_GAMECENTER_GAME_START_OK_REQ` | `sub_584EC0` | C2S | `s16 game_id` |
| 484 | `GG_GAMECENTER_GAME_START_OK_ACK` | `sub_584F70` | S2C | `s16, u8 status(1), u16 game_id, s32` |
| 485 | `GL_GET_GAMEROOM_PROGRESSTIME_REQ` | `sub_56AD60` | C2S | `u8 room_no` (查詢戰局進行時間) |
| 486 | `GL_GET_GAMEROOM_PROGRESSTIME_ACK` | `sub_56AE30` | S2C | `u8 n3, s16 room_no, u8 id, s32 elapsed_sec, u8, s8, u8, s8, u8, s8, u8, s8` |

### 3.15k AI / PVE 防衛戰模式協定 (五十五輪全鏈定案)

| Opcode | 封包名稱 | 來源函數 | 方向 | Wire 格式與行為 |
|---|---|---|---|---|
| 918 | `GR_AI_GET_REWARD_ITEM_REQ` | `sub_761A70` | C2S | `u8 reward_idx` (PVE 結算抽獎) |
| 919 | `GR_AI_GET_REWARD_ITEM_ACK` | `sub_761B20` | S2C | `u8 idx, u8 status(0=成功), s32 item_id, u8 slot, s32 count, u8 flag` |
| 922 | `GR_AI_DAMAGE_SHIELD_REQ` | `sub_761580` | C2S | `s16 shield_id, s16 damage, s16 remain, f32 unk` (防衛核心受損) |
| 923 | `GR_AI_DAMAGE_SHIELD_ACK` | `sub_761710` | S2C | `s16 shield_id, s16 damage, s16 remain, f32 unk` (房間廣播同步) |
| 924 | `GR_AI_RECHARGE_MAGAZINE_START_REQ` | `sub_558350` | C2S | `u8 slot, u8 team, u8 unk` (彈藥補給開始) |
| 925 | `GR_AI_RECHARGE_MAGAZINE_START_ACK` | `sub_558550` | S2C | `u8 slot, u8 team, u8 unk` (房間廣播) |
| 926 | `GR_AI_RECHARGE_MAGAZINE_END_REQ` | `sub_5586B0` | C2S | `u8 slot, u8 team, s8 status` (彈藥補給完成) |
| 927 | `GR_AI_RECHARGE_MAGAZINE_END_ACK` | `sub_558880` | S2C | `u8 slot, u8 team, u8 status` (房間廣播) |
| 928 | `GR_AI_CONTINUE_START_REQ` | `sub_761DB0` | C2S | `s32 continue_count` (PVE 接關復活) |
| 929 | `GR_AI_CONTINUE_START_ACK` | `sub_761E90` | S2C | `u8 status(1=成功), s32 continue_count` |
| 935 | `GR_AI_FEVER_START_REQ` | `sub_7622C0` | C2S | `(空)` (啟動 Fever 狂暴狀態) |
| 936 | `GR_AI_FEVER_START_ACK` | `sub_7623A0` | S2C | `u8 status(1), u8 flag(0), s32 duration_ms(10000), u8 type(1)` |
| 939 | `GR_AI_GO_NEXT_WAVE_REQ` | `sub_75CE40` | C2S | `(空)` (波次切換推進) |
| 940 | `GR_AI_GO_NEXT_WAVE_ACK` | `sub_7613D0` | S2C | `u8 next_wave, s32 wave_time` |
| 944 | `GR_RESET_GAMEROOMSLOT_REQ` | `sub_585E90` | C2S | `(空)` (重置房間槽位) |
| 945 | `GR_RESET_GAMEROOMSLOT_ACK` | `sub_585F30` | S2C | `u8 status(1)` |


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
    u8 mode(→sub_53FBB0), u16 (+144 勝場目標), u8 flags
    (bit0→mode+4 item / bit1→mode+8),
    u8 mode+12 (是否隊伍房 1/0 — 卌五輪 sub_56A7B0 定案), u8 +109,
    u8 mode+13 (隊打散), u8 +185 (noskillbg), u8 +128 (double damage) →
    寫入房間物件, 然後 16×s32 (per-slot 值 → dword_F6DD1C[60195*i],
    165 開戰 REQ 以 sub_592AA0 原樣回送)
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
| 198 [34..36] | 保留槽, 0 安全 | 尚未找到 task/stat consumer（目前只見於基本資料複製） |
| 198 [27] (+108) | raw/unknown; task condition 1 threshold input | `sub_9252D0` 直接把它作 condition 1 的輸入比較；沒有證據可投影為 Store stat |
| 198 flags u8×3 (+304..306) | 閒置, 0 安全 | 讀入後無引用 (別名斷鏈) |
| 198 [28][29] (+112/116) | 閒置, 0 安全 | 僅複製建構 |
| 198 blob [52..60] | 遊玩秒+模式場次 | cond20 + sub_923BF0 |
| 681 n100 | 計費模式 id | ==100/101 → CHARGE UI (Tricod) |
| 681 ext (a,b,c) | raw positive gate plus one `s32,s32,u8` tuple; native UI/feature gate is proven, business names remain unresolved | `sub_43E500` → `sub_A1C870`, `dword_231800C`, `byte_231807D`, `CHANNEL_NETCAFE` branches |
| 681 billing ×2 | Tricod SDK session 參數 | sub_7092C0 → CTricodLog |
| 106 第三個 s32 | **exp** (顯示等級用) | sub_588560 → sub_403360 |
| 205 尾 7×s32 | PG/CASH/CP + 保留×3 + 旗標 | UI 標籤 (十一輪) |
| 120 custom_tex | 頭像貼圖 crc | CCustomTexture 快取請求 |

### 3.15pre0 198 統計欄位佈局破解 (十二輪 — 任務條件檢查器鐵證)
`sub_9252D0` (任務條件) 逐欄引用 CClientData, 加上 GP ACK 的
`sub_92EF00(事件號)` 對照, 統計欄位語意全部定案:
```
wire 群組2 = [34][35][36] (尚無 task/stat consumer — 保守保留), [37]=wins(cond5),
             [38]=losses(cond6)
wire 群組3 = [39]=kills(3), [40]=deaths(4), [41]=cond7 + UI HEADSHOT,
             [42]=cond10 + UI AIRCOMBO
wire 群組4 = [43]=cond8 + UI HEARTBREAK, [45]=double(11), [46]=triple(12),
             [44]=cond9 + UI CRITCALSHOT   ⚠ wire 順序 43,45,46,44 — 亂序!
wire 群組5 = [47]=multi(13), [48]=ultra(14), [49]=z(15), [50]=k(16),
             [51]=dd(17)
其他: [25]=level(cond18, client 由 exp 查表 sub_403360 重算 — wire[23]
      的 level 僅參考), [27]=cond1 threshold input (Store owner unresolved),
      [52]=累計遊玩秒(cond20), [53..60]=各模式完成場次
      (sub_923BF0 模式id對照), [61..63] 未引用。
`sub_9252D0` proves the task-condition indices; `sub_5206F0` separately proves
only the UI labels shown above. Neither function proves that Store
`disconnects`, `playCount`, or `roundCount` owns an unnamed word.
```
→ **舊 server 佈局把 wins 放 [34] 全體錯位 5 欄** — CreateGL_MYINFO_ACK 已重排。
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
武器不在此 12 槽 — primary/secondary/melee/throw weapons use the 12.1M/
12.2M/12.3M/12.4M families and the separate `sub_524660` loadout groups.
```
(sub_5280F0 寫回時同公式驗證 sub_535020, 逐槽失敗碼 99/1/2...)
其他已定案的 id 區段:
- 15,300,007 (E975A7) = 戰隊常數; 15,300,032 (E975C0) = 205 特判 id
- 15,301,001..15,302,000 = 204/205 可購段 A = **福袋/袋物段** (830 條:
  福袋(☆)/武器袋/ボイス袋 — AK Paper 種子恰在此段)
- 15,304,001..15,306,000 = **稱號段** (568 條). It is a catalog family,
  not a claim that all nine 198 `sub_527550` UI ordinals are title slots:
  `sub_4C6120` assigns the NAME and MASTER controls their own narrower ranges;
  the complete per-ordinal ranges are in `RESOURCES.md §5c-2`.
- 15,310,001..15,320,000 = 可購段 B = **合購袋段** (443 條: SOUL
  WEAPON DUO 袋等)
- 11,010,001..11,070,000 = **ヘアパズル段** (1,273 條) = 7×s32 拼圖槽
  驗證段
→ 204 直購白名單只允許「袋物」兩段 — 單品武器/外觀須經福袋開出
  或 695 GS_BUY_ONCEITEM (十九輪與真實目錄對撞定案)
→ **DB item_catalog 種子 (1001/2001) 與真實 id 空間不符**, 實用時
必須以上述區段填目錄, 否則 client 過不了 sub_535020 驗證。

### 3.15pre1a GS_BUYCHAR (310/311) — canonical body-template creation

**REQ 310 — Fact / HIGH:** `sub_572790` first obtains `n99=sub_533F50(a1)`
and returns unless `n99==99` (the character-body item category).  It then
writes exactly six `s32` values through `sub_592A20`: the full body item ID
followed by five scalar arguments.  The decompiler prototype renders the
latter arguments as `char`, while the packet primitive serializes all six as
`s32`; their original-server business meaning is **UNRESOLVED**.  A server must
therefore require the complete 24-byte request and validate the full body-ID
range, but must not invent a name or validation rule for the trailing five
words.

**ACK 311 — Fact / HIGH:** `sub_5728A0` reads:

```
u8  ok
if ok:
    s32 body_full_id
    s32 face_full_id
    s32 head_full_id
    s32 top_full_id
    s32 bottom_full_id
    s32 shoes_full_id
u8  account_update_target
s32 account_update_value
```

The non-intuitive face-before-head wire order is direct parser dataflow:
`v24,v30,v31,v25,v27,v28` are read in that order, then stored as
`body=v24`, `head=v31`, `face=v30`, `top=v25`, `bottom=v27`, `shoes=v28` after
subtracting bases `19900000`, `10000000`, `10100000`, `10200000`, `10300000`,
`10400000`.  `sub_5831F0` is called on every full item ID before storage.

The tail is read even after a failed `ok`: `account_update_target` changes one
of three client globals only for values 1, 2, or 3; its concrete business name
is **UNRESOLVED**.  `target=0, value=0` is the explicit parser no-update path,
so it is the safe neutral candidate for a future module. The current
`server-ts` runtime does not register 310/311. The old shape `ok +
slot/exp/cash/gp/durability` is not a 311 layout and must not be emitted.

**Assumption / LOW (bounded malformed-request behavior):** the original server's
response/disconnect choice for a deliberately truncated 310 request is not
observable from this client-only corpus. The current `server-ts` runtime does
not register 310/311; the parser-valid failed-311 form above (`ok=0`, neutral
tail) is a future consumer-safe candidate, not a claim that the runtime emits it.

**Inference / MEDIUM:** combined with the native body-template maps documented
in `RESOURCES.md §5c-1`, newly created canonical characters should receive the
complete six-ID vector.  This matches the client’s materialized creation state;
the original server’s historic 311 producer is not available.

### 3.15pre2 GL_CLIENTINFO (246/247) — 查看他人資料 (十一輪發現)
247 ACK (sub_573EB0): `u8 ok(==1)` → **sub_523BF0 完整基本資料塊**
(與 198 首段完全同構 — 21×欄位 + 48B blob) + **sub_524360 單角色外觀**
`u8 slot(<20), u8 char_type, 12×u16 equip` (與 198 的 sub_524010 條目
逐欄位一致, 互為交叉驗證)。`sub_51EFB0` 的 non-self UI lookup temporary 是
`CHAR[132]`，其 `strlen`-sized copy 不足以證明 246 request 有 20-byte
boundary；server reader 因此不額外收窄 nickname。n11==9 時再驅動個人資料
視窗 UI。
→ 伺服器實作 247 時可重用 CreateGL_MYINFO_ACK 的首段 builder。

### 3.15pre-2 客戶端狀態機 + 官方模式表 (二十輪)
**客戶端狀態 (sub_537710 set / sub_5376F0 get, byte_EE8968+24)**:
2=帳號伺服器已連(250 GL_LOBBYIN 前後), 3=商店(252), 9=大廳(198 後),
10=房內/戰鬥(GR START), 12/14/15=特殊模式(回放/教學), 4..8/16..19=
過場狀態。dispatcher 內大量 `sub_5376F0()==9/10` 分支即以此判斷
「同一 ACK 在大廳 vs 房內」的不同處理 — 佈局裡的 [n11==9]/[n11==10]
特判全部對應此狀態機。

**官方遊戲模式表 (map_StartIndex.xml — 二十輪, 正名十七輪的猜測)**:
```
modeIndex 0 = TeamDeath     (TD_, bit2)   ← 建房 111 的 u8 modeIndex 用這套
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
- **111 GL_MAKEROOM_REQ** (**Fact/HIGH**, `sub_449320 → sub_56A5A0`): 唯一可達 UI caller
  固定傳 `a1=-1`，故 wire 是 `u8 0xFF title_form_marker, s8 has_password, str title,
  [has_password: str password], u8 max_player, u8 modeIndex, u8 requested_map, u8 no_skill_bg`。
  `USERS` control → max、`GAMEMODE` → modeIndex、`sub_44C140(..., modeIndex)` → requested map、
  `CHKBTN_NOSKILL` → no-skill flag；`0xFF` 是 title-form discriminator，**不是 map id**。
  `sub_56A5A0` 仍有 no-title serialization branch，但沒有可達 caller，故 server 僅接受這個
  title form，並拒絕 truncated、unterminated 或 trailing C2S payload。
- **112 GL_MAKEROOM_ACK** (sub_56A7B0): `u8 err, u8 room_no(<210),
  u16 max_slot_mask(+110), s32 room_uid, u8 no_skill_bg(+185),
  u8 mode+13` + err==0 時: `u8 n2, {s32 team_id, s32 tex_crc, str,
  u8}×2 (mode==2)` — 建房成功即以自己為房主初始化房間物件
  (sub_53F920: +105=1 自身、+106=0 無密碼、+110 上限槽位點陣、
  +136=3、+144=7/10, 其餘取自 client 建房時自存的 modeIndex/mode 全域)
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
  成員負載 = `u8 角色槽 0..0x13, u8 char_type, 12×u16 normal appearance
  (sub_524360), s32 custom_tex, s32 tex_crc, str tex_name, 武器 loadout×4
  (u16 primary, [secondary/melee/throw when group!=3],
  [8×s32 parts when primary!=0]), u8 extra_flag([8×s32] 若≠0),
  9×s32 UI items (sub_527550),
  raw u8 n5 + selected NewSkill profile 7×s32 puzzle IDs (sub_527D00), sub_885D00 語音塊 (85B:
  s16 base1, s16 base2, 27×{s16 item, u8 flag})`;
  ==3: 同 ==1 的單人更新 (以 slot 定址)
- **110 GL_ROOMINFOCHANGE_ACK** (sub_569240): `bool ok, u8 sub_type` +
  sub_type 1/2: room_no + 標題/密碼/規則變更組; 3..9: u8 room_no 單欄位
- **216 GL_ENTERROOMPASS_REQ / 262 GL_JOINPASS_REQ**: `u8 room_no, str pass`
  (sub_56B180/sub_56B230 — 六輪已證非暱稱包)
- **218 GI_CHANGEDATA_REQ** (sub_572FC0): `u8 char_type, u8 count,
  count×{u8 slot_idx, u16 item×12 (sub_5244E0: 1+12 欄位)}` — 只送有
  變更的角色槽 (sub_525450 差異偵測)
- **219 GI_CHANGEDATA_ACK** (sub_573230): `u8 result` → UI 解鎖 + 重繪
- **220 GI_CHANGEWP_REQ** (sub_573340): `u8 changedCount`, then
  `changedCount×{u8 groupNo, u16 primaryOffset, [u16 secondaryOffset,
  u16 meleeOffset, u16 throwOffset when groupNo != 3],
  [8×s32 partId when primaryOffset != 0]}`. `sub_525680` sends only groups
  differing from its last authoritative CClientData snapshot. `sub_4C7C00`
  establishes groups 0..2 as primary/secondary/melee/throw profiles and group
  3 as a primary-only switch weapon; all fields are category-relative offsets,
  not flags.
- **221 GI_CHANGEWP_ACK** (sub_5735F0): same repeated group structure. The
  receiver starts from a fresh CClientData and copies it over the global
  profile after reading the packet. **Inference / HIGH:** an ACK to a delta
  220 must therefore return the complete authoritative four-group state;
  returning only the changed records would clear all omitted groups locally.
  The current `server-ts` runtime does not register 220/221; this full-snapshot
  requirement is preserved as future implementation evidence, not emitted policy.
- **912 GL_WEAPONPARTS_EQUIP_CHANGE_REQ** (sub_95AEF0) — **Fact / HIGH:**
  exact forms are `{u8 operation, s32 weaponId, s32 partId}` for operations
  `0` (remove) and `1` (install into an empty part position), or the 13-byte
  `{u8 operation=2, s32 weaponId, s32 partId, s32 oldPartId}` replacement
  form. `sub_9591F0` obtains the eight current part IDs, and the sender emits
  operation 0 only when the selected current part matches, operation 1 when
  the position is empty, and operation 2 when replacing a nonzero current
  part. `sub_95B180` derives the affected `0..7` part-array index from eight
  contiguous full-ID intervals `15210001..15220000`, …,
  `15280001..15290000`; this independently matches the `weaponparts.pat`
  column group. It updates every current loadout row whose primary weapon is
  `weaponId`, not a character's normal appearance.
- **913 GL_WEAPONPARTS_EQUIP_CHANGE_ACK** (sub_95B180) — **Fact / HIGH:**
  its first `u8 errorRaw` gates the rest: nonzero returns without consuming or
  changing the local parts state; only zero reads the operation/body matching
  912. **UNRESOLVED:** the original server's nonzero error values and its exact
  ownership/expiry policy. The current `server-ts` runtime leaves 912/913
  unregistered rather than replying fake success.
- **466 GI_CHANGE_SKILLITEMSLOT_REQ** (sub_5738A0) — **Fact / HIGH:**
  exact body is either 2 bytes `{u8 targetProfile, u8 previousProfileUpdateRaw=0}`
  or 31 bytes `{u8 targetProfile, u8 previousProfileUpdateRaw!=0,
  u8 previousProfile, 7×s32 previousProfilePuzzleIds}`. The raw byte is a branch
  condition, not a Boolean constrained to 0/1; an emulator must retain/accept
  every nonzero value with the 31-byte form. `sub_4AADE0` copies
  the currently active 32-byte profile to the profile being left, copies the
  target profile into `CClientData+144420`, and calls this sender. Both profile
  indices are bounded `0..4`; neither is a character slot or a 9-slot ordinal.
- **467 GI_CHANGE_SKILLITEMSLOT_ACK** (sub_573A70) — **Fact / HIGH:**
  `{u8 resultRaw, u8 unknownHeaderRaw, u8 recordCount,
  recordCount×{u8 profileIndex, raw32}}`. For each record, the client reads all
  32 bytes but writes only its final `s32` to that profile's expiry metadata.
  It does not branch on the first two bytes in this receiver. **UNRESOLVED:**
  original-server meanings/error values of those two header bytes and the
  authoritative operation that grants/extends profile 1..4 expiry. The current
  `server-ts` runtime does not register 466/467; it must not emit a fake all-zero
  raw32 success response.
- **NewSkill validity boundary — Fact / HIGH:** `sub_527AF0` accepts only zero
  or a catalog entry in `11010001..11070000`; `sub_4AC8F0` partitions the seven
  ordinals as hair `11010001..11020000`, jacket `11020001..11030000`, pants
  `11030001..11040000`, shoes `11040001..11050000`, set `11050001..11060000`,
  accessory1/2 `11060001..11070000`. The resource text at message 900 says the
  same nonzero accessory puzzle cannot occupy both accessory positions.
- **Future server validation boundary — Inference / MEDIUM (not an
  original-server control-flow fact):** a future 466 module may accept a profile
  only when its conditional previous record belongs to the active profile, all
  nonzero IDs fit the native ordinal family, distinct submitted IDs are owned
  and unexpired, and a target profile 1..4 has a positive native packed-minute
  remainder. Invalid requests must not mutate state or invent a 467 success.
  The direct localization evidence proves the duplicate-accessory *rule text*,
  not the exact native branch.
- **Current TS persistence boundary:** `server-ts/src/store.ts` creates selected
  profile 0 plus five zero raw32 records for a new player. It does not register
  466/467 and does not import legacy `skill_slots`; expiry/grant migration remains
  **UNRESOLVED**.

---

### 3.15q 748 GR_SELECTRANDOMMAP_ACK 與 122 GR_MAPCHANGE_ACK 是**同一個 handler**

748 是少數「有 ACK、catalog 卻沒有配對 REQ」的 opcode（745–752 區段中
746/747、749/750 都成對，748 單身）。本輪把兩個 handler 的函式本體
逐字元比對，結論是**完全相同**：

```c
// 748 -> sub_564090          // 122 -> sub_56E530
unsigned __int8 v2;           unsigned __int8 v2;
sub_592940(a1, &v2);          sub_592940(a1, &v2);
return sub_42FC50(dword_EA10D0, v2);   return sub_42FC50(dword_EA10D0, v2);
```

兩者都是「讀 1 個 `u8`，交給同一個地圖設定器 `sub_42FC50`、寫進同一個
全域房間物件 `dword_EA10D0`」。`sub_42FC50` 全檔**僅這兩處**被呼叫。

**因此 748 的 `u8` 就是 mapId，語義等同 122。** 差別只在使用情境：
122 是房主手動改圖的 ACK，748 是「隨機選圖」流程的結果廣播
（UI 面為 `SelectRandomMap.xml`，另有 `Port_RANDOM_MAP.dds` /
`Port_HOTRANDOM_MAP.dds` 兩張非實體地圖的縮圖）。

**對私服的直接意義。** 若日後要實作隨機選圖，**不需要新的狀態機**：
沿用 `GR_MAPCHANGE_ACK` 既有的 `room.MapId` 廣播路徑，
只是改用 opcode 748 送出即可，客戶端的處理完全一樣。
但**選圖規則本身**（可選池、是否排除當前圖、誰有權觸發）仍無
client 可證事實，維持 UNRESOLVED，現在不應主動發送 748。

### 3.15q2 全表掃描：只有 **3 組** S2C opcode 共用完全相同的 handler 本體

把 `LAYOUTS.md` 的每個 S2C handler 從 dump 取出函式本體、正規化空白後兩兩比對
（285 個相異 handler），**只有三組**是「不同函式、相同實作」：

| 組 | opcode | handler | 共用實作的意義 |
|---|---|---|---|
| A | `122 GR_MAPCHANGE_ACK` / `748 GR_SELECTRANDOMMAP_ACK` | `sub_56E530` / `sub_564090` | 讀 `u8` mapId → 同一個 `sub_42FC50`（全檔僅這 2 處呼叫），見 §3.15q |
| B | `276 MASTER_MEMO_ACK` / `278 MASTER_MEMOALL_ACK` | `sub_578920` / `sub_578BC0` | 讀 `wstr` → 同一個 `sub_541BF0(dword_F2A688, Buffer, 10000)` 系統訊息顯示器，逾時參數同為 10000ms |
| C | `361 GG_TSURRESPON_ACK` / `972 GG_SOCCER_RESPON_ACK` | `sub_558DD0` / `sub_566400` | 戰鬥中繼的同構回應（釣り／足球共用同一套 respawn/回應處理） |

**這個掃描的價值在於「反面」。** 它證明**其餘 282 個 S2C handler 都各自不同**，
所以不能因為兩個 opcode 名稱相近就假設它們同構 —— 除上述三組外，
每個 ACK 都必須各自照 `LAYOUTS.md` 的欄位序實作。

三組的共同模式也一致：**同一種資料、兩個觸發情境**
（手動改圖 vs 隨機選圖、單人備忘 vs 全體備忘、釣魚 vs 足球的同型回應）。
B 組同時說明 276/278 對伺服器而言只是**同一則系統訊息的兩種送達對象**，
欄位完全相同。

**界線。** 「實作相同」只證明**客戶端處理相同**，不證明伺服器可以互換使用：
觸發權限、對象範圍（單人 vs 全體）仍屬 service policy，維持 UNRESOLVED。
可用 `verify_dispatcher_coverage.py` 複驗 A 組恆等式。

### 3.15s 錦標賽狀態機：763 的 `state` 1..6 由 client 自己的字串表定名 (本輪)

既有紀錄只寫 `763 = f32 time, u8 state, u8 round, u8`，**沒有寫 state 的值域與語義**。
本輪把它補完，且**不靠 Wiki 猜測** —— 名稱來自 client 自己的 `msgtableres.lang`。

**欄位再確認（Fact / HIGH，`sub_57E5A0` @ `0x57E5A0`）。** 讀取序為
`raw4`(`sub_592AC0`) → `u8 state` → `u8 round` → `u8`(讀後未用)，
且**僅當 `state==2`** 才續讀 `s32 tournamentId` 與自己的 `[11]` 比對，
不符即走 `sub_57E490()` 忽略。三個持久欄位是
`[12] = state`、`+17 = round`、`+18 = roundType`（後兩者實際由 **772**
`sub_580A80` 的錦標賽樹狀圖寫入，763 只更新 `+18`）。

**state 的權威名稱（Fact / HIGH）。** 大廳 UI `0x91195` 起的分支
逐一把 `[12]` 映到訊息 id，文字即官方狀態名：

| state | msg id | 訊息原文 | 對應 Wiki 階段 |
|---:|---:|---|---|
| 1 | — | （有分支，無字串） | — |
| 2 | 949 | `トーナメント情報公開` | 開催時間（資訊公開） |
| 3 | 950 | **`受付中`** | 開始 **+5 分**「受付開始」 |
| 4 | 966 | **`入場中`** | 開始 **+10 分**「参加者の入場開始」 |
| 5 | 952–960 | `NN強戦、試合中` / `NN強戦、終了` | 開始 **+15 分**「トーナメント開始」 |
| 6 | 982 | `%sトーナメントが終了しました。` | 賽事結束 |

[トーナメント](https://wikiwiki.jp/paperman/トーナメント)（2013-10-18）描述的
「受付開始 → 入場開始 → トーナメント開始」**三階段順序與 3→4→5 完全一致**，
且 client 字串 `受付中`／`入場中` 與 Wiki 用詞逐字相同。
**但 Wiki 的 +5/+10/+15 分鐘間隔在 client 中沒有任何常數**——
時間推進完全由伺服器以 763 推播，**不得寫死**。

**state==5 的 round 展開（Fact / HIGH）。** `+17` 是**剩餘輪次倒數**，
`+18`(roundType) 選「進行中」或「已結束」兩套文案：

| `+17` | roundType==2（試合中） | roundType==3/4（終了） |
|---:|---|---|
| 4 | `32強戦、試合中` (952) | — |
| 3 | `16強戦、試合中` (953) | `32強戦、終了` (957) |
| 2 | `8強戦、試合中` (954) | `16強戦、終了` (958) |
| 1 | `4強戦、試合中` (955) | `8強戦、終了` (959) |
| 0 | `決勝戦、試合中` (956) | `4強戦、終了` (960) |
| −1 | （沿用前次 buffer） | — |

即賽制為 **32→16→8→4→決勝，最多 5 輪**，`+17` 由 4 遞減至 0。
這與 `ui/TNMT_Awardproperty.xml` 的 `award_1..3` ＋ `nomarl/abnomarl_award`
版面（§5b-18）相容，但該檔只有座標、不含輪次，兩者是獨立證據。

**Wiki 的 5 人門檻找到對應訊息，但不是同一條件。** msg **933** 是
`トーナメント参加申し込みの受付が終了しました。予備メンバーを含めて１クラン１０名まで入場可能です。`
—— 這是 763 在 `state==4` 時顯示的**入場上限 10 人**，
與 Wiki 的「クランメンバーが5名以上いないと参加が出来ません」（**報名**下限 5 人）
是**不同的兩個數字**：10 是入場上限、5 是報名下限。
client 只證實前者；**報名下限 5 無 client 常數，維持 UNRESOLVED**。

**界線。** 以上全是 **client 顯示與本地狀態機**。誰能推進 state、
輪次配對、勝敗判定（Wiki 所述「キル数＞デス数＞参加人数＞特殊ショット」的
平手裁決）、賽程表與獎品發放，**全部是 server policy，無 client 證據**，
維持 UNRESOLVED。本節的實益是：實作 763 時 **state 必須落在 1..6**，
且 `state==2` 的 `s32` 必須是收訊者自己的 tournament id，否則 client 靜默丟棄。

### 3.15p Pepachi 701 的 `reelC` = 伺服器指定的演出級別 (本輪, resource+native 互證)

701 每筆獎品三元組的第三欄 `reelC` **不是外觀參數，而是抽獎結果的級別**，
由伺服器決定、客戶端只負責照演。完整鏈路（全部可在 `PaperMan.exe.c` 追到）：

```
sub_84A490 解出 701           → v13[] 獎品陣列 (11 格 stride: reelA=v13[i], reelB=v13[i+11], reelC=v13[i+22])
sub_84A320(this, …, a5=reelC) → switch(a5) 寫 this+8 級別
sub_842A30(…, a2=this+8)      → sub_8433E0(table, a2)
sub_8433E0                    → v8[]={2,0,1,0,3} 重映射，再 rand() % 該組演出數
pmSlotMachineMovieSequenceTable → 讀 pepachi/pe-pachi_scenario.xml
```

`pe-pachi_scenario.xml` 恰有 **4 個區段**，與重映射值域 0..3 完全對上：

| reelC | `this+8` | 重映射 | 區段 | 演出變體數 |
|---:|---:|---:|---|---:|
| 3 | 3 | 0 | `Rare`（大獎） | 3 |
| 2 | 2 | 1 | `Atari`（中獎） | 11 |
| 1 | 0 | 2 | `Zannen`（可惜） | 8 |
| 0 | 4 | 3 | `Suka`（槓龜） | 44 |

**區段是依「出現順序」讀取，不是依名稱（Fact / HIGH）。**
`sub_843600` 以 `for(i…)` 走訪並存進 `this + 16*i + 4`，
建構子 `'eh vector constructor iterator'(this+1, 0x10u, 4, …)` 也正好配置
**4 個 16B 槽**。exe 裡的 `off_BDBBC0[2] = {L"Rare", L"Atari"}` 只是除錯標籤，
**不參與選擇邏輯**（陣列只有 2 個元素卻要走 4 圈，正說明它非邏輯所需）。
因此**調換 XML 區段順序會直接改變抽獎演出的對應**。

**唯一的 `rand()` 在客戶端，但它只挑「同級別內的第幾種演出」**
（例如 `Suka` 的 44 種變體），級別本身完全來自 701。
`p_n11 >= 11` 分支即 Wiki 所述的「11 連抽」模式。

**界線。** 這證明 **701 的 `reelC` 具有結果權威**，因此私服若要實作 701，
必須自行決定級別並保持與獎品內容一致 —— 但**中獎率、獎池、保底、扣款**
仍無任何 client 可證事實，維持 UNRESOLVED，現有 fail-closed 回覆不得改動。

## 3.99 廿六輪終極盤點 — 676-entry catalog 全分類收官
```
✔ dispatcher 直讀     300 條 (LAYOUTS.md 自動表)
✔ REQ builder         261 條 (LAYOUTS_REQ.md 自動表)
✔ 場景 vtable 層      699/703/707/807/809 (CLobbyShop), 719-723
                      (IVotingNetwork), 788 (sub_407360)
✔ 登入層 0x43E651     681/694/882
✔ UDP transport evidence   separate `sub_595E80` private dispatcher; only 19→empty-20 server exchange implemented
△ remaining private cases   values/case labels exist but transport/domain meaning is UNRESOLVED pending per-case analysis
✘ 真·死協定 ~80 條    無 builder 無 parser 無別層引用:
   PM_MASTER/ID/LOGOUT/CH_SERVER (145-152 舊版中控殘留),
   GR_STARTTIME/AUTOCHANGE/CRYSTAL 系 (棄用模式),
   GV_VIEWER 組 560-570 (外部觀戰工具協定, client 不實作),
   GS_STOREOK/NEWGIFT (still no native sender/ACK consumer recovered),
   while HUKUBUKURO/PRESENTPACKAGE have now been recovered separately below,
   SECURITY_AHNLAB/NPGAMEGUARD (韓版安全模組, 日版不用),
   MASTER_TEST/UPITEM 等 GM 殘留, *_BASE 佔位 (100/560/580/680)
→ 私服無需理會死協定; the 622 TCP-catalog live entries have their recorded layout/sequence scope. The separate private UDP dispatcher is excluded from that catalog count and remains only partially evidenced as stated above.
```

## 3.98a 商店流程再驗證與 fail-closed 邊界（2026-09）

> **Scope warning.** This section replaces older shop prose that treated UI
> labels, zero-filled ItemData price fields, or a locally convenient database
> mutation as original-server economic evidence. The client proves its wire
> reader, local state gates, and resource lookup; it does not contain the
> retired service’s price sheet, entitlement checks, gift delivery rules,
> Hukubukuro contents, or random reward server state.

### Evidence ledger

| Conclusion | Classification | Provenance / limit |
|---|---|---|
| `GL_SHOPIN_REQ` (252) is an empty client request. Immediately after its send, `sub_574120` locally transitions the lobby scene to state 3; no recovered primary or secondary consumer compares opcode 253. | **Fact / HIGH** for request/state/absence; **implementation choice / user-directed** for response | `sub_574120`, `sub_537710(byte_EE8968, 3)`, dispatcher cases 174–315, and an inventory of all 19 non-prototype `sub_591EE0` opcode-getter uses. The current `server-ts` module accepts only exact-empty 252 and, by explicit project direction, emits the empty 253 interoperability ACK. It carries no catalog, account, entitlement, or scene-success claim. `179/180 GS_STOREOK` semantics remain **UNRESOLVED**. |
| 468 is the 204 bulk body routed for Hukubukuro IDs. | **Fact / HIGH** | `sub_571100` plus `sub_591EC0` header setter. |
| 470 and 780 share `{s32 rawContext,s32 itemId,s16 -(variantIndex+1)}` from `sub_57B2E0`; their messages are null in `sub_4D7790`. | **Fact / HIGH for wire; UNRESOLVED for rawContext/entitlement** | Generic sender, both callers, and packet primitives. |
| `15301001..15302000` and `15310001..15320000` route 468/470; `15302001..15304000` and `15320001..15330000` route 780. Decoded ItemData names corroborate bag versus package catalog families. | **Fact / HIGH for ranges/routing; Inference / MEDIUM for product labels** | `sub_571100`, `sub_4D7790`, and same-hash `main:Extracted/ui/cfg/itemdata.pat`. No price or contents policy follows. |
| ItemData has 21,164 records, but its relevant shop price slots are effectively zero-filled in this Japanese resource revision. | **Fact / HIGH** | Native record loader and exact decoded `itemdata.pat`. The resource cannot authorize the former local zero-price success implementation. |
| `Total_Package_Index.xml` has 114 `total_package` entries, each mapping a package index to fourteen `type_N` item IDs. | **Fact / HIGH** | `sub_A03E90`–`sub_A041D0` and exact main resource. It is a client selection/display map, not a grant list. |
| `RecommandItem.pat` has 1,030 set rows (plus two header rows). `808` sends `{s32 count,count×s32 recommendationId}` only for a nonempty client selection; `809` returns `{s32 count,count×{s32 itemId,u8 rawClass,u8 rawValue}}`. | **Fact / HIGH** | decoded resource, `sub_46E140`, `CLobbyShop::sub_46AD00` case 809. Client maps classes 1/4→22, 2/5→23, 3→24, but the server-side lookup/meaning is **UNRESOLVED**. No 808 handler is registered. |
| `ui/Gaccha.xml` enables only `START_CASH` and `START_TEN_CASH`; its `START_PG` and `START_CP` blocks are commented out. `ui/pepachi.xml` enables `START_PG`, `START_CASH`, `START_PG_10`, and `START_CASH_10`. | **Fact / HIGH (UI revision only)** | exact `main:Extracted` XML. The residual code can still recognize the commented Gaccha control names, so a code path is not evidence that this resource revision exposes that purchase. |
| 700 is a five-byte request `{u8 selector,s32 selectedCharacterId}`. Its writer derives the second value as `19,900,000 + (selectedCharacterValue mod 100,000)` and its caller supplies raw selectors 1, 2, 4, and 5. | **Fact / HIGH** | `sub_8458D0`, `sub_8459C0`, `sub_525790`; the old `{count,coin_type}` description is disproven. |
| The Pepachi caller's local balance gates and four UI names associate selector 1/2/4/5 with cash-single / PG-single / cash-ten / PG-ten. | **Fact / HIGH** (upgraded from Inference; see §3.15r) | `sub_8459C0` gates selectors 1/4 on the cash balance at 1/300 and 2/5 on the other balance at 1/10,000; XML has those exact four labels. The decompiler lost the four wide-string initializers, but the **level gate is attached to only one branch**, which disambiguates them — see §3.15r. |

#### 3.15r 700/900 的送出前置條件：三個 client gate 與 995 錢包推播（本輪定案）

**Fact / HIGH.** `sub_8459C0`(700 caller) 與 `sub_99D0A0`(900 caller) 結構同構，
各自持有一份常數，在**送出封包之前**做四道本地檢查，失敗則顯示
`msgtableres.lang` 訊息並 `return 0`（封包完全不送出）：

| # | 條件 | 700 常數 | 900 常數 | 失敗 msg id / 原文 |
|---|---|---|---|---|
| 1 | CASH `*dword_EE8D0C > 0`（ten 需 `≥300`） | — | — | **264** `ＣＡＳＨが不足しています。` |
| 2 | PG `*dword_EE8D18 > 0`（ten 需 `≥10000`） | — | — | **252** `PGが不足しています。` |
| 3 | 禮物盒 `i_23 < 上限` | `dword_BDBC9C`=**200** | `dword_BEAE50`=**200** | **847** `プレゼントボックスに空きがありません。…%d個まで保管できます。` |
| 4 | **僅 PG 分支**：等級 `n10_2 >= 下限` | `dword_BDBC98`=**10** | `dword_BEAE4C`=**10** | **846** `ペーパチはレベル「%d」以上からご利用できます。` |

三個 global 的身分由 **995**（`sub_567AE0`，dispatcher `case 995u`，layout `s32 s32 s32`）
一次定案 —— 它就是錢包／等級推播：

```
995 field[0] → *dword_EE8D18  = PG    （與 198 尾段 game_point 同一 global，sub_5392A0）
995 field[1] → *dword_EE8D0C  = CASH  （反編譯器誤命名為 `ArgList`；實為 B0F0xx 全域，非堆疊變數）
995 field[2] →  n10_2         = level （EE8D10）
```

`n10_2` = 等級的旁證：`sub_92EF00(18, 23, n10_2, 0)`；大廳以 `n10_2 - 1` 索引
`Class` 資源表取階級圖示；並且它是 `itemdata.pat +644`（需求等級）的比較對象
——`sub_534FE0(id) > n10_2` 時顯示 msg **922**。
`i_23` = 禮物盒待領數的旁證：198 (`sub_570550`) 尾段的 `u16` 寫入它（§3.2）、
299 寫入時遞增、301 由 `sub_57AFE0` 遞減。

**selector 定名（本輪由 gate 掛法消歧）。** `sub_99D0A0` 先依控制項設 `this+148`
為 1/2/3，再依「是否為單抽控制項」決定 drawCount = 1 或 10，實際只送四組：
`{1,10}` `START_TEN_CASH`、`{1,1}` `START_CASH`、`{2,1}` `START_PG`、`{3,1}` `START_CP`。
由於**等級檢查只掛在 `Source__240` 分支**、且 Wiki 與 msg 846 都指明「只有 PG 有等級限制」，
故 `Source__240`=`START_PG`(selector **2**)、`Source__241`=`START_CASH`(selector **1**)、
selector **3**=CP。700 的 `Source__242/243` 同理為 ten 版本，其 raw selector 1/2/4/5
的 cash/PG 歸屬隨之確定。

**Server 的唯一可操作結論。** 實作 700/900 成功路徑前，**必須先以 995 建立
client 的 PG/CASH/level**，否則請求會被本地 gate 攔下、封包不會到達伺服器。
這是 wire ordering 事實。

**仍 UNRESOLVED（不得回填）。** `>0` / `≥300` / `≥10000` 是**餘額門檻**而非價格；
它們與 Wiki 的「30CASH／1000PG 單抽」數值相容（十連即 300／10,000），但 client
從不用這些常數扣款——錢包一律由 995 覆寫。獎池、機率、保底、扣款額與伺服端是否
覆核同一 gate，全部維持 UNRESOLVED，700→701 / 900→901 的 fail-closed 回覆不變。
| 900 is a two-byte request `{u8 selector,u8 drawCount}`. Its caller directly sends `{3,1}` for `START_CP` and `{1,10}` for `START_TEN_CASH`; the two residual control paths send `{1,1}` and `{2,1}`. | **Fact / HIGH for raw body/pairs; Inference / MEDIUM for residual-control names** | `sub_99CFA0`, `sub_99D0A0`, and `ui/Gaccha.xml`. The active resource leaves only cash single/ten controls; its PG/CP XML is commented out. |
| 461 is sent only after the PaperCode UI has exactly 16 upper-case ASCII alphanumeric characters. 464 is `{u8 duplicateChoice}` for cancel (0) or `{u8=1,str}` for its GET action. | **Fact / HIGH** | `CUILobbyStorePaperCode::sub_4C3E70`, `sub_4C4060`, `CPopupDuplicatedItem::sub_50FFC0`, `sub_510010`, `sub_57CCE0`. This establishes local syntax and choice wire—not a valid-code or grant policy. |
| 463 is an empty C2S send. The PaperCode UI sends it only on the first `a2==1` activation while its local `+240` sentinel is zero, then sets that sentinel to one. The opcode's `NOTIFY` name does not reverse this observed direction. | **Fact / HIGH for wire/local gate; UNRESOLVED for service effect** | `sub_57CB20` and the caller at `0x4C3CF0`. There is no recovered server response/consumer relation that permits a code/session state mutation. |
| 804 is an empty C2S request constructed by `sub_581F80`; 805 is registered by name but has no recovered native consumer. 179/180 StoreOK and 451/452 NewGift are likewise registry name pairs with no recovered native sender (for their REQs) or ACK consumer. | **Fact / HIGH for observed absences in this binary; UNRESOLVED for original-service use** | Complete constructor and S2C-dispatch searches, plus opcode-name registration. Do not manufacture status/notification packets from their paired numbers. |
| 806 takes a signed-16 category selector. Direct shop UI emitters use `1..13` and `15..24` (there is no recovered `14` sender); parts-room initialization separately sends `25`. 807 consumes `{u8 rawHeader,u16 recordCount,u16 category,recordCount×{s32 itemId,u16 rawVariant,u8 rawPeriod,u8 blobLength,blobLength raw bytes}}`. | **Fact / HIGH** for wire and known selector emitters; **UNRESOLVED** for server record policy | `sub_46C760`, direct UI control names in `shop.xml`, `CLobbyPartsUpRoom::sub_9C1DD0`, `CLobbyPartsUpRoom::sub_9C22F0`, and `CLobbyShop::sub_46AD00` case 807. Both consumers read but do not use `rawHeader`; `recordCount=0` skips their entire record loops. Shop sets its per-category cache byte, then re-enters `sub_46C760`; category 21 clears/rebuilds its recommendation UI path, while 22–24 update their category list. Item records reach category-specific UI/map calls only after parsing and are selectively suppressed from the shared local mapping by `sub_535AD0`. The original source, visibility/filter, variant/period/blob semantics, and response population remain unresolved. |

#### 806/807 selector, field, and state-flow audit

The recovered direct shop sender, `sub_46C760`, sends 806 only while its byte
at `this+521720+selector` is not already one; a received 807 writes that cache
byte before re-entering the same routine. The separately constructed
`CLobbyPartsUpRoom::sub_9C1DD0` packet is `{s16=25}` during parts-scene setup.
The recovered shop selector/control relation is:

| selector | direct UI source | post-807 route observed in `sub_46C760` / `sub_46AD00` |
|---:|---|---|
| 1–4 | `CAT_PAPER_CHARACTER_PACKAGE`, `CAT_PAPER_CHARACTER`, `CAT_PAPER_FACE`, `CAT_PAPER_HAIR` | paper-character subcategories |
| 5–9 | `CAT_DRESS_SET`, `CAT_DRESS_UPPER`, `CAT_DRESS_PANTS`, `CAT_DRESS_SHOES`, `CAT_DRESS_ACCESSORY` | dress subcategories |
| 10–13 | `CAT_WEAPON_PRIMARY`, `CAT_WEAPON_SECONDARY`, `CAT_WEAPON_MELEE`, `CAT_DRESS_THROWING` | four loadout weapon classes; the XML spelling `CAT_DRESS_THROWING` is retained verbatim |
| 14 | no recovered source | rejected by this server; `sub_46C760` has no case 14 |
| 15–20 | `SETITEM`, `SHOP_HUKUBUKURO`, `SUPPORT`, `SHOP_CROSSHAIR`, `SHOP_SPECIALABILITY`, `SHOP_NEWSKILL` | corresponding Paper Center / special category |
| 21 | `CAT_PAPER_RECOMMAND` / `RECOMMAND_AVATAR` path | clears the recommender item-id set, inserts each received `itemId`, then calls `sub_45B5A0` |
| 22–24 | `RECOMMAND_NEW_SORT`, `RECOMMAND_HOT_SORT`, `RECOMMAND_SALE_SORT` | clear then populate the matching recommender collection through `sub_9F64F0` |
| 25 | no shop control; parts constructor only | `CLobbyPartsUpRoom::sub_9C22F0` consumes the same records for its parts-side mapping |

This mapping is **Fact / HIGH** for the local controls/routes, based on
`main:Extracted/ui/shop.xml`, `main:Extracted/ui/parts.xml`, the direct callers,
and the two 807 consumers. It is not an original service category authority,
and it does not make any local resource item purchasable.

| 807 field | native consumer/data flow | evidence-bound server treatment |
|---|---|---|
| `u8 rawHeader` | read first by both consumers but has no recovered branch, store, or call use | `0` is a structural, receiver-unused value only; it has no asserted status meaning. |
| `u16 recordCount` | exact loop bound in both consumers | `0` is the only emitted count. It prevents every record parser, UI insert, and shared mapping update. |
| `u16 category` | shop directly writes its cache at `this+521720+category` and re-enters `sub_46C760`; parts reads it but has no separate category branch | server echoes only the validated direct-client selector, never a client-provided out-of-range value. |
| `s32 itemId` | consulted by `sub_535AD0`; category 21 inserts it into the recommender set; 22–24 pass it to `sub_9F64F0`; other shop/parts paths may add an item-to-variant/period entry to `unk_EE3F28` | never emitted: an ID's asset or ItemData presence is not service visibility/ownership proof. |
| `u16 rawVariant`, `u8 rawPeriod` | category 22–24 forward both into `sub_9F64F0`; local map allocations retain both for other paths | semantics, ranges, expiry, and ownership effects remain **UNRESOLVED**; never invented. |
| `u8 blobLength, raw[blobLength]` | category 21/ordinary/parts readers copy it to temporary storage without a recovered semantic consumer. In 22–24, any nonzero length is subsequently treated as fifteen raw `{s32,s32}` pairs for extra mapping candidates, without a local length validation. | never emitted. A fabricated nonzero blob could make the native reader use uninitialized temporary bytes or overrun its fixed workspace; zero-record framing avoids the path entirely. |

`RecommandItem.pat` is a local recommendation **presentation** input, not an
807 response source: its pmFile-decoded CSV has 1,030 set rows and a maximum
concept id of 20, and `sub_9F5C80` loads it before recommendation selection.
When a nonempty local recommendation selection exists, `sub_46E140` separately
sends 808; 809 has its own record consumer. Neither resource existence nor that
secondary request proves the original server's 807 contents. This is **Fact /
HIGH** for the resource/load/request separation; record-production policy is
**UNRESOLVED**.

### Evidence-bounded consumer-safe arms（not current handler registrations）

The following entries are intentionally **fail closed**. “Raw zero” means a
field whose original error meaning is unproven; it does not mean success.
They are evidence-bounded candidate responses for future modules, not a list of
currently registered handlers. The current `server-ts/src/ops/` surface is the
31 modules listed in `SERVER_TS_PACKET_FIELDS.md`; the shop request families
below remain unregistered unless explicitly stated elsewhere. Do not mutate
wallet, inventory, characters, gifts, bags, or reward state from this table.

**Implementation boundary, not a native-server fact.** For the fully recovered
request grammars 204/468, 206, 208, 296, 310, 356, 358, 453, 470, 698, 700,
702, 780, and 900, the rows below record the minimum reader-safe shape if a
future handler is added. They do not claim that the historical server used the
same rejection transport or error code.

除 `252→253` shop-entry compatibility projection與本頁另有明確標註者外，
下表各列目前都不是 `server-ts` runtime 的 registered handler；它們只記錄
未來實作時不可破壞的 client consumer-safe boundary。

| flow | future consumer-safe candidate / current registration status | direct consumer/state gate |
|---|---|---|
| 252→253 shop entry | `(empty)` | User-directed interoperability response only. `sub_574120` already changed the native client to state 3 before any response; no 253 consumer was recovered after checking primary and secondary opcode paths. |
| 806→807 hidden-item list | `{u8 rawHeader=0,u16 recordCount=0,u16 echoedCategory}` for exact direct-client selectors `1..13`, `15..25` only | `CLobbyShop::sub_46AD00` and `CLobbyPartsUpRoom::sub_9C22F0` read all three fixed fields before their record loops. Zero count prevents record-derived UI/map insertion, item mutation, currency data, or fabricated overrides; shop then re-runs its cached category transition. |
| 204→205 normal bulk purchase | `{u8 count=0,u8 rawResult=0,u8 rawError=0,7×s32=0}` | `sub_571910` consumes its count-zero error pair and mandatory seven-word trailer; no item/cache update. |
| 206→207 unnamed part purchase | `{u8 rawResult=1}` | `sub_571B60`: nonzero has no tail; zero opens the part/cache/wallet decoder. |
| 208→209 sell | `{u8=0}` | `sub_572B80`: only nonzero reads item/GP and removes local inventory. |
| 296→297 gift send | `{u8=1}` | `sub_57AA50`: zero alone has the five-s32 success balance tail. |
| 310→311 character purchase | `{u8=0,u8 accountUpdateTarget=0,s32 accountUpdateValue=0}` | `sub_5728A0`: false skips all six appearance IDs but always reads the final pair. Canonical visual defaults are not payment entitlement. |
| 356→357 cash query | `{u8=0,s32=0}` | `sub_572420` reads exactly five bytes; billing status semantics are unresolved. |
| 358→359 cash purchase | `{u8 count=0,s32 rawHeader=0}` | `sub_5725D0`: zero count does not consume item records. |
| 468→469 Hukubukuro purchase | `{u8=1}` | `sub_57CE30`: any nonzero status has no six-s32 success/state tail. |
| 470→471 Hukubukuro detail | `{u8=1}` | `sub_57D210`: nonzero status has no item list. |
| 695→696 once item | `{u8=0,s32=0,s32=0}` | `sub_571D70` consumes the conditional zero-result s32; no success-only tail is reached for item ID zero. |
| 780→781 PresentPackage detail | `{u8=1}` | `sub_57D6B0`: nonzero status has no item list. |
| 802→803 destroy | `{u8 nonzero,u8 rawCode=0,u8 affectedCount=0}` | `sub_895EE0`; the 802 request is still **UNRESOLVED**, so no item is read/deleted. |
| 698→699 Pepachi entry | `{u8 status=0,s32=0,s32=0}` | case 699 reads all fields; only status 1 enters its success UI path. |
| 700→701 Pepachi spin | `{u8=0,u8 rawError=0}` | `sub_84A490`: only first byte 1 opens award/reel decoding. |
| 702→703 Pepachi list | `{s32 start=0,s32 count=0}` | case 703 builds an empty local signed-16 list, never grants a reward. |
| 900→901 capsule | `{u8 nonzero,s32 count=0,s32=0,s32=0,s32=0}` | `sub_9A1A30` always consumes count plus three tails; count zero prevents award records and nonzero avoids local wallet/reward updates. |
| 453→454 delete gift | Future candidate: `{u8 result=0,s32 echoedGiftId,s32 echoedItemId}` | `sub_57BCF0` always consumes the two IDs and only result exactly 1 removes a cached gift. Current `server-ts` has no 453/454 module; a future module must validate the exact eight-byte request and keep this non-mutating arm. |

453's wire and client cache key are known, but the original service's pending
versus claimed state, deletion authority, and interaction with 298/300/315 are
not. The current `server-ts` runtime does not register 453/454; a future module
must not delete persisted data merely because a local SQLite row happens to
match. Truncated or trailing requests must receive no synthetic echo because
the protocol has no safe correlation value to invent.

### Still unresolved—not approximated

* 179/180 StoreOK, 451/452 NewGift direction/producer, 461/462 and 464/465
  PaperCode server policy, **806/807 hidden-item record production/filtering**,
  and 808/809 recommendation resolution remain unresolved. The implemented
  806 zero-record arm is only an exact consumer-safe “no server-controlled
  records” response; it is not an approximation of the historical hidden-item
  catalog or a source of ownership. 298/299,
  300/301, and 314/315 gift listing/claim/move semantics are likewise not
  approximated: their correlated selection, pending/claimed state, and exact
  mutation rules are still incomplete.
* Successful 205/207/209/297/311/359/469/471/696/701/781/901 paths need an
  original-service data source or captures covering authorization, currency,
  price/discount, duplicate/period/variant checks, atomic mutation, and the
  exact success response payload. Resource/UI names alone are insufficient.
* 701 success is `{u8=1,u8 rawCode,s32 rawA,s32 rawB,u8 rawMode,u8 count,
  count×{s32 reelA,s32 reelB,s32 reelC}}` (count capped at 11 by the client),
  not the former fabricated `{itemId,quantity,coins}` record. 901 success is
  likewise a count-driven list plus three state values, not a fixed one-item
  packet.

## 3.98 CClientData 記憶體總圖 (廿七輪彙整 — 歷輪碎片權威版)
byte 偏移 (this 為物件基址):
```
+4     u8   current_char_list_index (198/247 的 basic blob 尾段讀入)
+60    char nick[24]     (wire str)
+88    u8   selected_char_list_index (wire u8; `CHARSLOT` UI 讀寫，
                         sub_884160 將此值原樣送 opcode 312；不是 char_type)
+92    s32  [23] wire level (參考值)
+96    s32  [24] exp
+100   s32  [25] level ← client 由 exp 查表 sub_403360 重算
+104   s32  [26] cash
+108   s32  [27] raw/unknown（`sub_9252D0` 的 task cond1 輸入；server owner 未定）
+112/116 s32 [28]/[29] 閒置
+136..144 s32 [34..36] 保留（尚無 task/stat consumer）
+148   s32  [37] wins    (任務 cond5)
+152   s32  [38] losses  (cond6)
+156/160 s32 [39]/[40] kills/deaths (cond3/4)
+164   s32  [41] cond7 + UI HEADSHOT
+168   s32  [42] cond10 + UI AIRCOMBO
+172   s32  [43] cond8 + UI HEARTBREAK
+176   s32  [44] cond9 + UI CRITCALSHOT (wire 亂序: 43,45,46,44)
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
+36095 區   9×s32 UI-item slots (sub_527550: crosshair/name/master/ability/
             EXP boost/PG boost/extra ability×2/VOICE; see RESOURCES §5c-2)
+144201 u8  weapon loadout group count; +144204 4×44B groups
       {u8 no, u16 primary, 3×u16 secondary/melee/throw, 8×u32 parts};
       group 3 is the primary-only switch-weapon wire form
+144420 28B 已選 NewSkill profile 的 7×s32 ヘアパズル (sub_527D00)
+144452 u8  raw n5（語意 UNRESOLVED）
```
封包處理層全圖 (五層): ① dispatcher sub_58B010 (306 case)
② 場景 vtable sub_407360→CLobbyShop 等 ③ 登入層 0x43E651
④ 轉蛋動畫控制器 0x84A000 (701) ⑤ 語音 vtable sub_885D00
(792/794/796) + UDP 層 sub_595E80。

## 4. 對伺服器 DB 的直接推論

1. **背包上限 5120 格、每包分頁 100 條** (sub_524B70) → `inventory.slot 0..5119`。
2. **角色槽最多 20** (sub_524010 迴圈上限 20) → `characters.slot_no 0..19`，
   每角色 12 個 normal-appearance u16 欄位（不是武器欄位）。
3. **武器 loadout 固定 4 組** (sub_524660 上限 4)：groups 0..2 各有
   primary/secondary/melee/throw offsets；group 3 is primary-only switch
   weapon; a nonempty primary carries 8 parts.
4. **9-slot UI-item block** (sub_522480) 與 **NewSkill 5×7 profile**（selected record 由 sub_527AF0 讀 0x1C=7*4）分離儲存；466 操作後者。
5. **戰績 19 個計數器** (GP_CH*C 家族)。
6. **道具欄位**: item_id(s32), raw f1/f2 floats（domain UNRESOLVED）, period(s32), raw extra(u8), durability(u16)。NewSkillLevTable 不作這些 inventory wire 欄位的 server authority。
7. **房間**: no(≤210), title, map, modeIndex, win_count, time_limit, max_player(≤10 slots),
   password, item_mode, balance, skill_off, observer。
8. **好友/訊息/倉庫/任務/公會/禮物** 都有對應 packet 家族 → 各自建表。
9. period 天數 & 商店 kind 白名單直接寫進 CHECK constraint。
�店 kind 白名單直接寫進 CHECK constraint。
