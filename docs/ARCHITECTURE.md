# PaperMan 私服全景架構 (廿九輪融會貫通版)

> 29 輪逆向的知識總圖 — 每個結論都可在 PACKETS/RESOURCES/LAYOUTS 找到
> 逐行證據與互證鏈。

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
The client has a distinct private dispatcher (`sub_595E80`). Its only completed
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

這些 boundary 都集中在 `Router`、`Session`、`ChannelHandlers`，使每一個
server-side transition 可搜尋、可記錄、可替換；它們不依賴 143 的
`String[24]` 語意（該 writer 仍是 **UNRESOLVED**）。


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
| ⑥ 戰場引擎 | sub_749B90 (1D37560) | TCP catalog 166 subtype 1-9; its relation to UDP is UNRESOLVED |
| + UDP 層 | sub_595E80 | private UDP dispatcher; private 20 completion is direct evidence, remaining case semantics require per-case proof |

## 3. 資料層 (7 表 37,044 條真實日版)

item 21,164 (id=基底+偏移編碼) / quest 844 (cond 雙機制) /
map 123 (模式bitmask) / weapon_parts 10,648 (8組) /
parts_ability 413 (31欄彈道) / recommend 3,180 / protocol 676

## 4. 加密四件套 (全部互逆驗證)

1. wire AES-128-CFB-128 (IV=0; key=트렁크점령전머지, 黃金向量三實作互證)
2. wire LZSS (dist≤1023, len 3-66, 門檻 694 協商)
3. pmFile per-byte 滾動 (keystream FA5387AD/0F3A94AA/48945DCA/1A68DCCF)
4. data.pat 容器 (pmFile→ROL混淆→zlib 1.2.3→CRC自帶表)

## 4b. C# Server 結構（2026-09 整理）

`Program` 只負責建立 DB、固定組態、Router、login/channel TCP listeners，及
`UdpControlServer`；每個 TCP socket 的 receive loop 按收到順序
`await Router.DispatchAsync`，不把同一 session 的 stateful request 平行化。
UDP 端點同樣串列處理收到的 datagram，但它是無 state 的、source-address
回覆的 19→20 control exchange，不是 TCP session dispatcher。Native client shutdown is
explicitly *not* copied: it uses `TerminateThread` before `closesocket`; the C# endpoint
uses cancellation and disposes its socket only after its receive loop exits.

- `Session` 持有單一 TCP socket 的 connection state、account identity、room
  seat、send gate；`ChannelEntryCompleted` 明確表示 143 handoff 與 195→196
  channel entry 之間的不同 state。
- `Router` 是唯一的 opcode registry 與 listener/state boundary；它不承載
  gameplay policy。各 `Handlers.*` 檔以協定子系統切分（Auth、Channel、Lobby、
  Room、Join、BattleRelay、BattleObjects、Shop、Stats、Clan、Quest、Friend、
  Voice、Warehouse、Master、GameCenter、Ai），使 opcode 的處理位置可直接搜尋。
- `Db` 依實際 persistence domain 分成 8 個 partial：主檔（connection、
  bootstrap、account、nickname、packet stats）以及 Player、Economy、Social、
  Rooms、Voice、Warehouse、GameCenter。共用 connection / lock / command creation
  只放在主檔；跨表不可分割操作在發生處以明確 transaction 包住。
- `ChannelAdmissionRegistry` 只保存一次性的 681→143 handoff；`RoomManager` /
  `SessionRegistry` 只持有 process-local live state。SQLite 是 account、inventory、
  quest 等可持久狀態的唯一來源。
- `UdpControlServer` 與 `UdpPacketCodec` 是刻意分離的 UDP-private 層：前者只
  parse source-proven opcode 19 並回覆空 opcode 20，後者只做 native AES framing
  （不帶 TCP LZ）。client-reported UDP values 不取得 `Session` 或 DB authority。

此切分是依 socket lifecycle 與 protocol domain，而非為了套用通用 pattern；
重要 side effect 仍可從 Router → Handler → Db / RoomManager 直接追蹤。

## 5. Server 現況

- handlers: 42 個獨立 opcode (19 個經通用轉發器)
- 覆蓋: 登入/大廳/商店(買賣禮)/好友/信箱/任務/戰隊/戰績/
  房間全流程/開戰鏈/戰鬥 TCP relay/查人/場景，以及 UDP-private 19→空 20 control
- UDP 範圍: 只實作 source-proven AES-only 19→20；其餘 private UDP opcode、P2P/
  NAT/relay 語意均未實作且不宣稱已定性
- 死協定 ~80 條已定性 (PM 中控/GV 工具/韓版安全) — 無需實作
- 待辦: docs/TODO_HANDLERS.md (照自動序列施工)

## 6. 關鍵互證鏈 (12+ 次資料↔逆向對撞全中)

id基底↔目錄段分佈 / QuestTerm↔sub_9252D0 cond / 8×parts↔8改裝組 /
事件號↔Quest cond21-36 / 驗證段↔稱號·拼圖段 / char_type↔ICT代號 /
hand*.tga↔試衣間 / AI獎勵xml↔919 / 994助攻↔cond36 /
pepachi swf↔701轉輪 / partsability段↔weaponparts組 / 130↔269 elapsed
