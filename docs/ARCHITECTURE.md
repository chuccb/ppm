# PaperMan 私服全景架構

> 這是 server lifecycle、native/client state、wire boundary 與 runtime ownership 的
> 高層索引。欄位級證據回到 [`PACKETS.md`](PACKETS.md)、[`RESOURCES.md`](RESOURCES.md)、
> [`LAYOUTS.md`](LAYOUTS.md)、[`SERVER_TS_PACKET_FIELDS.md`](SERVER_TS_PACKET_FIELDS.md)
> 與三份登入／頻道 native audit；本頁不取代它們。

## 1. 完整生命週期 (實測定案的因果鏈)

```
【登入伺服器 TCP :40200 (握手=694 GL_ACCOUNTCONNSUCC)】
connect ──► server 發一次 694 (門檻 0x2580) ──► client 送 682 (帳密+硬體指紋)
        ──► server 回 681 (result=1 + 伺服器清單 + account/net-café feature ext + Tricod)
        ──► client 以 681 清單的 host:port 建立獨立頻道 TCP（帳號 TCP 的
            關閉時機未由此段 parser 單獨證實）
選頻道: 195 → 196; 進大廳: 250 (無回包) → client 自拉:
  197→198 MyInfo (統計佈局=任務cond對映!) 199→200 背包(28B條目)
  105→106 名單(exp!) 107→108 房間清單 433→434 好友 425→426 信箱
心跳: server 每30s 發 102, client 回 101

### 254/255 NewSkill profile scene

`GL_INVENIN_REQ(254)` 不是空封包，而是恰好一個 `u8 requestContextRaw`。
client 從當前 UI/entity 物件取出這個 byte；其業務語義仍是 **UNRESOLVED**，
server 只做結構性回送，不把它命名成倉庫頁籤或其他假定語義。

目前可確認且由 `server-ts` 實作的本機 user 分支是：

```text
254: u8 requestContextRaw
255: u8 mode=1, s32 user_id, u8 requestContextRaw,
    u8 unknownHeaderRaw=0, u8 selectedProfile,
    5 × { 7×s32 puzzleItemId, s32 expiresAtPackedMinute }
```

五個 profile 是 **user/account-level NewSkill state**，不是 198/247 的角色
12-slot appearance。新 player 只建立 profile 0 與五筆零值 raw32 record；profile 0
的 expiry word 由 client 忽略，profile 1..4 的 raw zero 表示沒有已確認的有效期限。
這是 bootstrap state，不是贈送、解鎖或商店政策。

198 的 selected NewSkill 七個 puzzle IDs 與 255 來自同一份 snapshot，避免兩個
response 顯示互相矛盾。`NewSkillLevTable.xml`、`NewSkillColorTable.xml` 只作
client 合成/顯示資料，沒有被當成 server grant 或效果驗證規則。

`GI_CHANGE_SKILLITEMSLOT(466/467)` 的七 ID ownership、profile 1..4 expiry
授予/延長及失敗碼仍未在 `server-ts` 實作；在取得足夠 server policy 證據前，
不能用 255 的 bootstrap record 假造 466 成功。

【房間流程】
111 建房→112 (room_uid) / 216 密碼→217 / 113 進房→114(sub_type多態)
125 房聊→126廣播 121 換圖→122 127 ready→128 135 換位→136
129 開戰→130廣播(17欄+16×s32) → 各員 183 載入完→184 → 187→188 開打

【頻道伺服器 TCP :40201 (握手=693 GL_TCPCONNSUCC; 681 清單指向此 port)】
connect → server 發 693 → client 送 143 (String[24] identity + n100/ext_count
  handoff claims) → 144 (1/2=success；3=version mismatch、4=already connected、
  5=unauthorized；server 需把 claim 綁定成功 681) → [CLobbyChannel 層會送]
  195 GC_ENTERCHANNEL(group,channel,replay) → 196（**只有 result=1** 才有
  ⭐UDP host/port + ch_type tail；第三 byte=active channel index；
     [3=AI→sub_875680 關卡塊] ) → state 119:=2 → tick 清 CClientData
  → 場景切換 (9=大廳/8=AI/2=回放) → CLobbyMainRoom 自動送 107
  → IPv4 UDP manager configured to 196 endpoint. Private op18 can issue the
     one-shot 141→142 endpoint confirmation; 142 contains active-channel byte +
     packed year/month/day/hour/minute calendar.   【bootstrap 再驗證】
port 佈局: 40200 登入(694) / 40201 頻道(693) / 40202 UDP control endpoint

【UDP evidence boundary】
The client has a distinct private dispatcher (`sub_595E80`). Its complete native
receive-case and outbound-builder matrix is documented in
[`LAYOUTS_REQ.md`](LAYOUTS_REQ.md) Appendices A and B; that matrix is client evidence, not
an instruction to expose those operations from the server. The only completed
server behavior here is encrypted private 19 → empty private 20, which prevents
the native sixth-attempt TCP 139 fallback. P2P/NAT/relay and generic gameplay
claims for the rest of the private namespace are **UNRESOLVED**.

【戰鬥 TCP catalog】
GG 中繼三模式: slot前綴轉發 / 復活六模式同構 / 聊天過濾
戰後: 133→134 回房; GP_CH*C 戰績上報 (絕對值+MAX單調);
  ACK 自動推進任務 (sub_92EF00 事件)
```

### 登入／頻道的 server state boundary（2026-09 重新整理）

下表刻意區分 client 端可直接證實的 **Fact**，與為了不讓 server 接受
client 不會正常送出的越序 request 而採用的 **Inference**。後者不是對原廠
server 實作的宣稱。

| 轉換 | 證據／信心 | Server 行為 |
|---|---|---|
| Login TCP connect → 694 → 682 | **Fact / HIGH**：`CLobbyLogin::sub_43E500` 的 694 分支讀 u16 後直接呼叫 `sub_43DF00`；後者是 682 builder。 | Program 只在新 login socket 發一次 694。 |
| 681 result low byte = 1 | **Fact / HIGH**：同一 reader 設 `this+131=1`，完成 server list 後呼叫 `sub_43E450`；682 builder 只在 694 分支出現。 | **Inference / HIGH**：拒絕同一 login socket 的第二個 682，避免覆寫已確認的 account identity；仍允許 101 pong。 |
| 新 Channel TCP → 693 → 143 → 144 | **Fact / HIGH**：681 讀出的 host/port 用於獨立 channel connection；`sub_555C60` 寫出 143 的固定順序。 | 143 只可 claim 一個尚未使用的 681 admission；143 成功僅代表 handoff 已驗證。 |
| 195 → successful 196 | **Fact / HIGH**：196 reader 僅在 `result==1` 讀 endpoint tail，並進入 selected channel 的後續場景。 | **Inference / HIGH**：只在成功 196 寫入完成後標記 `ChannelEntryCompleted`；在此之前拒絕 lobby、room、economy 與 gameplay request。 |
| 143 未通過後的 195 | **Fact / MEDIUM**：native channel wrapper 仍會在收到 144 後送 195；196 有完整非成功形狀。 | 保留 195，回傳沒有 endpoint tail 的非成功 196；不因此授權 socket。 |

這些 boundary 都集中在 `connection.ts`、`admission.ts`、packet handlers，使每一個
server-side transition 可搜尋、可記錄、可替換；它們不依賴 143 的
`String[24]` 語意（全域 identity scratch；唯一可見 mutator＝393
`GL_CHANGEID_ACK` 的 `'_'` append，初始化屬 .c 盲區——見 PACKETS.md
§2.6-C；非權威身份來源，仍不作 account/nickname 授權依據）。


### 生命週期終章 (卅五輪 — 斷線/登出/房主遷移)
- 103 GE_LOGOUT: 空 payload; 104 ACK = 死協定 → server 只解綁不回包
- 斷線清理 = 主動離房同流程: 124 (u8 slot) 廣播 → 房主離開再廣播
  190 (u8 = client F6DCF4[slot] 快取的玩家識別 — 即我方 114 寫入的
  uid 低 8 位; sub_56FBF0 逐行定案) → 空房回收
- client 收 190: 全員 host 旗標清 0 → 中選 slot 設 1; 自己中選 →
  獲得房主 UI (sub_5375F0(1))

## 2. 六層封包處理架構 (client 端)

| 層 | 位置 | 處理 |
|---|---|---|
| ① dispatcher | sub_58B010 | 306 case 主分發 |
| ② 場景 vtable | sub_407360→CLobbyShop 等 | 699-723/788 |
| ③ 登入層 | 0x43E651 | 681/694/882 |
| ④ 轉蛋控制器 | 0x84A000 | 701 (11組轉輪) |
| ⑤ 語音系統 | sub_885D00 (CVCustomizeManager) | 791-796、378/379 及 114/269/765/985 成員負載尾塊 |
| ⑥ 戰場引擎 | sub_749B90 (1D37560) | TCP catalog 166 子派送器；case 0x18 內層 2/0x10 觸發 **UDP op35** 回應（PACKETS.md §2.6-E），其餘子令接 sub_74xxxx 戰鬥動作 |
| + UDP 層 | sub_595E80 | private UDP dispatcher；逐 op 語義已定案於 PACKETS.md §2.6（G0-G 生命週期、A/B hole-punch 狀態機、19↔20、8/24 移動、ping/通知；僅 4 項達證據上限的 UNRESOLVED） |

## 3. 資源與資料層（native/resource inventory；不等同 runtime schema）

item 21,164 (id=基底+偏移編碼) / quest 844 (cond 雙機制) /
map 123 (模式bitmask) / weapon_parts 10,648 (8組) /
parts_ability 413 (31欄彈道) / recommend 3,180 / protocol 676

## 4. 加密四件套 (全部互逆驗證)

1. wire AES-128-CFB-128 (IV=0; key=트렁크점령전머지, 黃金向量三實作互證)
2. wire LZSS (dist≤1023, len 3-66, 門檻 694 協商)
3. pmFile per-byte 滾動 (keystream FA5387AD/0F3A94AA/48945DCA/1A68DCCF)
4. data.pat 容器 (pmFile→ROL混淆→zlib 1.2.3→CRC自帶表)

## 4.1 TypeScript / Bun server 結構（2026-09-17）

`server-ts/src/main.ts` 負責組態、SQLite bootstrap、login/channel TCP listeners
與 UDP control server；每個 TCP socket 的 receive loop 按收到順序串接 dispatch，
不把同一 connection 的 stateful request 平行化。UDP 端點同樣串列處理 datagram，
但它是無 state、source-address 回覆的 19→20 control exchange，不是 TCP session
dispatcher。Server 使用 cancellation 與 receive-loop 結束後的 socket cleanup，
不複製 client shutdown 的 native thread 行為。

- `connection.ts` 持有單一 TCP socket 的 reassembly、connection state、account
  identity、send gate 與 ordered dispatch；`admission.ts` 明確表示 143 handoff 與
  195→196 channel entry 之間的不同 state。
- `ops/registry.ts` 是 opcode lookup、typed packet builder 與 handler boundary；
  以 explicit imports 綁定 `src/ops/c2s/` 與 `src/ops/s2c/` 的 packet modules，
  並以 Bun directory check 防止新增檔案遺漏。TypeScript compile-time types
  不取代 packet reader 的 runtime width、framing、fixed-buffer 與 malformed-input checks。
- 每一個 packet module 以 opcode 命名並依方向分目錄；registry 將 filename 綁定
  到 `db/packets.tsv`，避免重複的名稱常數。未有足夠 evidence 的 opcode 保留
  fail-closed/no-op，不臆造 service policy。
- `store.ts` 是 `bun:sqlite` 的 account、identity、角色與 NewSkill projection
  邊界；跨表操作在明確 transaction 中完成。`ChannelAdmissionRegistry` 只保存
  一次性的 681→143 handoff，process-local live state 不冒充 persistent authority。
- `udp.ts` 與 `packet.ts` 刻意分離：前者只 parse source-proven private opcode 19
  並回覆 empty opcode 20，後者負責 native AES framing（不帶 TCP LZ）。
  client-reported UDP values 不取得 account 或 DB authority。

此切分是依 socket lifecycle 與 protocol domain，而非為了套用通用 pattern；
重要 side effect 仍可從 connection → ops registry/handler → store 直接追蹤。

## 5. Server 現況

- Packet modules：15 個 C2S、16 個 S2C；由 `ops/registry.ts` explicit registry
  綁定，並以 directory check 防止遺漏檔案；filename 必須存在於 `db/packets.tsv`，
  重複或未知 opcode 會在啟動時失敗。
- 已涵蓋：694/682/681 login handshake、693/143/144/195/196 channel handshake、
  lobby bootstrap（197/198、199/200、105/106、107/108、425/426、433/434）、
  250/252/254 compatibility projections、keepalive，以及 private UDP 19→空 20。
- Wire safety：9600-byte frame、AES-CFB、native field width、fixed-buffer bounds、
  count/length limits、single-use admission 與 196 success gate 均保留；未知 policy
  不做成功 mutation。
- UDP 範圍：只實作 source-proven AES-only 19→20；其餘 private UDP、P2P、NAT、
  relay 與 gameplay semantics 均未實作且不宣稱已定性。
- 下一步與未實作 request：[`TODO_HANDLERS.md`](TODO_HANDLERS.md)；native/resource
  cross-check：[`SERVER_TS_PACKET_FIELDS.md`](SERVER_TS_PACKET_FIELDS.md)。

## 6. 關鍵互證鏈 (12+ 次資料↔逆向對撞全中)

id基底↔目錄段分佈 / QuestTerm↔sub_9252D0 cond / 8×parts↔8改裝組 /
事件號↔Quest cond21-36 / 驗證段↔稱號·拼圖段 / char_type↔ICT代號 /
hand*.tga↔試衣間 / AI獎勵xml↔919 / 994助攻↔cond36 /
pepachi swf↔701轉輪 / partsability段↔weaponparts組 / 130↔269 elapsed
