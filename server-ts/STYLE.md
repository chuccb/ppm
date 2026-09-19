# 程式風格

為數個月後的人類讀者最佳化，LLM 是第二位讀者。兩條規則支配其他所有規則：

1. **名字就用協定裡的名字。** 客戶端叫它 `GL_LOGIN_ACK`，我們也叫
   `GL_LOGIN_ACK`。手持 `docs/PACKETS.md` 的讀者應該能用官方名 grep 到程式碼。
2. **一層的問題不加第二層。** 間接層必須靠「除去的困惑多於增加的」才配存在。

## 命名

以 `db/packets.tsv` 為準，它是逆向工程的單一事實來源。

**opcode 名字住在檔名與明確的 registry map 裡。** 一個 module 一個 packet，
放在其方向的資料夾：

```
src/ops/c2s/GL_LOGIN_REQ.ts   客戶端送出；我們讀取
src/ops/s2c/GL_LOGIN_ACK.ts   我們送出；由我們組建
```

module 內的 default function 也以 opcode 命名：

```ts
export default function GL_LOGIN_ACK(op: number, outcome: Result | Success): Packet
```

registry 刻意把每個名字重複一次，使完整 runtime surface 與 outbound
參數型別對 TypeScript 可見。builder 以自己的 opcode 作為第一個參數；
它不再帶第二份數值表。要找一個 packet 的程式碼，開啟同名檔案即可。

**方向看資料夾，不是 `_REQ`/`_ACK` 後綴。** 後綴描述客戶端觀點，不一定
符合我們的方向：`GT_PING_ACK` 是 *server* 端送出的 `_ACK`，`GT_PING_REQ`
是它*收到*的 `_REQ`。用後綴規則會把那對搞反；用資料夾不會。

其他一律用普通的 camelCase：`frameLength`、`verifyLogin`。對於找回的
wire 欄位，這只是機械式的分隔轉換：`user_no` 變 `userNo`，而 `uid`、
`flag`、`extra`、`unknown`、`raw` 維持保守命名。不要僅因另一份實作
採用某個語意名，就把 raw 欄位提拔成語意名。

兩道守門讓慣例可執行，而非只是期望：

- registry 明確 import 每個 packet module，使所有 C2S 與 S2C
  operation 全部列在一個短檔內（數量由 `summary()` 回報，測試釘住）。Bun 的 `Glob` 只用來攔截「加了檔案卻
  未註冊」的 packet。這條界線在 registry module load 時就會驗證——
  `bun test`、`bun start` 一 import 它就生效，不需另外指令。
- outbound 物件就是編譯期地圖：`OutboundName` 與 `OutboundArgs<N>` 由實際
  builder 函式推導。`build()` 只做結果是 `Packet` 的輕量 runtime 檢查；
  不需要 `any` 或動態 module 轉接器。
- 啟動時 registry 把每個註冊名對照 `db/packets.tsv`，並把每個檔案對照明確
  map。缺漏、改名或未知的 operation 立即失敗；wire layout 與 module 實作
  不隨之改變。

編目中的 opcode 家族，僅供定向：
`GL_` 大廳 · `GG_` 戰鬥中繼 · `GR_` 房間 · `GS_` 商店 · `GP_` 遊玩 ·
`GC_` 戰隊 · `GQ_` 任務 · `GI_` 背包 · `GT_` 傳輸 · `MASTER_` GM。

## 結構

- **一個概念一個檔案。** `packet.ts` 擁有整個 wire 格式 —— header、
  cipher、reader、writer —— 因為動任何一塊都不可能不碰其餘。
- **一個詞一個意思。**「wire」只指位元組格式，因此它只屬於 `packet.ts`。
  每個 opcode 一個 module 住在 `src/ops/`，因為每個檔案*就是*一個 opcode，
  而 opcode 是 `docs/PACKETS.md` 最高頻的詞彙。
- **不寫只有單一實作的 interface。** 直接依賴 Bun 的 `Socket`，而不是
  發明 `SessionSink` 把它包起來。
- **不用只用一次的包裝物件。** 若一個型別只是某個函式的參數，直接傳欄位。
- **優先 flat function 而非 class**，除非有實例狀態。`Connection` 是 class，
  因為每個 socket 都有自己的 buffer 與 timer；registry 用純函式，因為
  永遠只有一份。
- **一個 packet 的 module 就是那個 packet 的全部故事。** `GL_LOGIN_REQ.ts`
  解析*且*驗證*且*回覆。它不可以解析完再呼叫 `Connection` 上的某個
  method —— 那會把一件簡單的事拆到兩個檔案，並讓 `Connection` 長出
  每個 opcode 一個 method。

## 註解

註解寫*為什麼*，並引用證據。有價值的註解是那種阻止別人「修好」刻意怪癖的：

```ts
// 694 必須恰好送一次：它攜帶壓縮門檻，同時觸發客戶端的 682 builder，
// 若在登入後重送，客戶端會一直重送憑證。(docs/PACKETS.md §1.4)
```

任何不顯然的東西都引用 `sub_XXXXXX` 或文件節號。永遠不用註解把程式碼
重講一遍。

## 對未知誠實

該服務於 2016 年停止營運，許多遊戲行為只曾存在於原廠 server。凡 `docs/`
標示 UNRESOLVED 之處，寧可什麼都不做、也不發明看似合理的規則，並在註解
中說明。缺漏的功能可以被除錯；憑空編造的規則會永遠靜靜地偏離原作。

## TypeScript

strict，另加 `exactOptionalPropertyTypes`、`noUncheckedIndexedAccess` 與
`erasableSyntaxOnly`。`tsc --noEmit` 保持乾淨 —— 這些設定已經抓到過一個
真實的 aliasing bug。

預設用 Bun 原生 API：`Bun.listen`、`bun:sqlite`、`Bun.password`、
`bun test`。僅在 Bun 無對應時才動用 `node:`。

## 官方命名權威層級（2026-09-19 定案）

改名、補名、欄位選詞，只允許引用下列來源，且優先序即列序：

1. **資源面板字串**（`sub_6A8D80` 系列標籤、msgtable / uidatatable 資源 id、
   UI 類名與視窗名）
2. **`db/packets.tsv`** 註冊的 opcode 官方名（唯一 Fact 來源）
3. **wire 欄名**（`active_channel_index`、`channel_id`、`channel_type`、
   `client_flags`、`client_default`、`endpoint_opaque` 等已回收的讀寫位名）
4. **`docs/RESOURCES.md`** schema 欄名
5. **`docs/LAYOUTS.md` native grammar**（`sub_XXXXXX` 函式體欄位序與
   key0/key1 之類的參數位名；Part I/II 的 helper 稽核列是 **signedness
   權威**——文件互衝突時以它為準）
6. **`docs/PACKETS.md` 私有命名總表的〔推定〕名**：引用時必須連〔推定〕
   標記一起寫，絕不升格為 Fact

不屬於以上任何來源的「改善名」一律不動。**未命名（UNRESOLVED）是結論，
不是缺漏**；489/933/1007/1009 類不得因看起來眼熟而補名。

## op module 註解與程式碼的一致性 invariant

`test/style.test.ts` 機檢；手寫時先自查：

- **註解提到的參數必須存在。** 寫 `` `foo` argument/parameter/參數 `` 之前，
  `foo` 必須真的在函式簽章裡。改名後重跑 `bun test` 會攔截。
- **「echo」只能用在真的被傳進來的值。** 常數 0 不是 echo；若原生 denial
  arm 不讀該欄，寫明「不讀，0 是唯一不捏造值」，不寫 echo。
- **無條件尾段算在 wire 裡。** 原生 consumer「status 有條件分支、但尾段
  UNCONDITIONALLY read」時，wire 描述必須包含尾段（哪怕全零），
  禁止寫「zero trailing fields」與實際 byte 數並存。
- **數字住在程式碼與測試，不住在註解。** 模組數、測試數、member 數這類
  會長大的計數，註解一律不寫死（registry「15/16」是反例）；
  需要計數時從 `summary()`／`OPCODE_COUNT` 取，或以測試釘住。
- **不留孤兒註解塊。** `/** */` 之後必須直接接宣告（檔首 module doc
  例外）；`strMax`/label 撤除後留下的空殼註解同罪。
- **s2c 只序列化。** 零 `throw`、零驗證、零 label：值域交 `packet.ts`
  typed writer；buffer 容量寫在註釋。`.label(` 與 `strMax` 字樣禁止
  回到 `src/ops/`。
- **檔頭配對註解照抄實號。** 「`476 -> 477 GG_…_ACK`」的兩個數字與名稱
  必須與 `db/packets.tsv` 完全一致（request/ack 互指）。
