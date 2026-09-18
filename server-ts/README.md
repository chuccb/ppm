# server-ts — Bun 上的 PaperMan 伺服器

為 PaperMan 客戶端從零撰寫的伺服器，依據 [`../docs/`](../docs/) 中的逆向筆記實作。

## 技術棧（2026-09-17 preview 基線）

這是唯一的 server 實作。runtime 與 lockfile 刻意鎖定當日的 preview 工具鏈；
不得再引入另一種 server 語言或資料庫 runtime。

| 元件 | 版本 | 備註 |
|---|---|---|
| Bun | 1.4.3-canary | `bun:sqlite`、`Bun.listen`、`Bun.password` —— 不用 Node shim |
| TypeScript | 7.1.0-dev.20260915.1 | strict，另加 `exactOptionalPropertyTypes`、`noUncheckedIndexedAccess`、`erasableSyntaxOnly` |
| SQLite | 3.53.4 | 經 `bun:sqlite`，並於 `test/db.test.ts` 內做 runtime 斷言 |

## 執行

```bash
bun install
bun test          # 執行完整 Bun 測試
bun run typecheck # tsc --noEmit
bun start         # login server 監聽 0.0.0.0:40200
```

環境變數：`PM_HOST`、`PM_PORT`、`PM_DB`、`PM_ADVERTISE_HOST`、
`PM_CHANNEL_PORT`、`PM_CHANNEL_NAME`、`PM_UDP_HOST`、`PM_UDP_PORT`、
`PM_ADMISSION_TTL_MS`。

## 目錄結構

慣例細節見 [STYLE.md](STYLE.md)。

```
src/packet.ts        完整 wire 格式：header、cipher、reader、writer、分段重組
src/aes.ts           AES-128 + CFB-128 —— 客戶端的加密
src/opcodes.ts       676-opcode 目錄，由 db/packets.tsv 載入
src/store.ts         bun:sqlite 上的帳號
src/admission.ts     登入連線 → 頻道連線的一次性 handoff（IP＋回聲值比對）
src/connection.ts    一條 TCP 連線：分段重組、存活偵測、dispatch、Bun.listen
src/udp.ts           有 native 來源佐證的 private UDP opcode 19 → 回空 20
src/new-skill-catalog.ts  itemdata.pat 生成的原生 new-skill 品項目錄（資料模組；未來 store/quest 層的檢核來源）
src/ops/registry.ts  檔名 → opcode，以及具型別的 build() / handlerFor()
src/ops/c2s/         客戶端送給我們的 packet
src/ops/s2c/         我們送給客戶端的 packet

分層準則（2026-09-19 定案）：s2c 模組【只負責序列化】——零 throw、零驗證，
值域由 packet.ts 的 typed writer（u8/s16/s32/f32/strMax…）在寫入點把關，
其餘一律由上游（c2s handler／store／config）保證；native 文法上限以
strMax 常數或常數本體（固定 arm）呈現，不殘留 require 式守衛。
src/main.ts          進入點
```

**一個 packet 一個檔案，以 opcode 命名；方向看資料夾。** 要從
`docs/PACKETS.md` 找到 packet 對應的程式碼，開啟同名檔案即可：

```
src/ops/c2s/GL_LOGIN_REQ.ts   客戶端送出；我們讀取
src/ops/s2c/GL_LOGIN_ACK.ts   我們送出；由我們組建
```

命名、registry 與 module-load 檢查的完整規則見 [STYLE.md](STYLE.md)。
現行 31 個 packet module 的逐欄完整審計見
[`../docs/SERVER_TS_EVIDENCE.md`](../docs/SERVER_TS_EVIDENCE.md)。

## 本實作涵蓋的協定事實

下列全數引用自 `../docs/PACKETS.md`：

- **Frame** —— `u16 size, u16 opcode, u16 preEncryptSize, u16 preCompressSize`，
  然後是 payload。Little-endian、未對齊、欄位之間無 padding。
- **Cipher** —— AES-128-CFB（128-bit feedback、零 IV）作用於 16-byte 對齊的
  buffer；即使空 payload 也要付一個 block。金鑰為 EUC-KR 字面量
  「트렁크점령전머지」。
- **兩條連線、兩次 handshake** —— 客戶端先連 login server，然後開啟*第二條*
  連線到 login 回覆中讀到的頻道 host 與 port。兩種情況都是 server 先開口：

  ```
  login    GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ    -> GL_LOGIN_ACK
  channel  GL_TCPCONNSUCC     -> PM_UDPSTART_REQ -> PM_UDPSTART_ACK
                                     -> GC_ENTERCHANNEL_REQ -> GC_ENTERCHANNEL_ACK
  ```

  `GL_ACCOUNTCONNSUCC` 必須恰好送一次：它也會觸發客戶端的憑證 builder，
  登入後重送會讓客戶端無止境循環。
- **頻道 handoff 不是身分** —— `PM_UDPSTART_REQ` 攜帶一個 `String[24]`，其
  writer 從未被定位，因此 server 以 source IP 與回聲值對照最近的單次 login
  admission，而不是把它當帳號 key 信任；它也不是憑證。客戶端的二級 handler
  無視 144 的 result，一律送出 195，所以連線會被 gating 直到 196 成功為止。
- **頻道進入是明確的** —— `GC_ENTERCHANNEL_ACK` 有三欄失敗前綴與
  success-only 的 endpoint 尾巴。server 只接受自己廣告的那一組 group/channel，
  並在成功回覆寫出之後才綁定 lobby 權限。type-3 admission 與發射需要明確的
  raw `type3Tail`；語意部署組態與 gameplay 仍不納入範圍。
- **壓縮** —— 客戶端只在值嚴格低於 `0x2580` 時才降低門檻，因此送出
  `0x2580` 會在雙向停用 LZ。TCP LZ stage 因此未實作；若對端真的送出壓縮
  frame，`decodeFrame` 擲出錯誤而不是猜測。private UDP endpoint 同樣無 LZ
  stage，與 `sub_595980`/`sub_595A60` 相符。
- **字串** —— payload 內以 NUL 結尾、無長度前綴。韓文客戶端是 CP949，
  其 WHATWG label 為 `euc-kr`（Bun 拒絕 `cp949`）。
- **憑證** —— 客戶端送出前會驗證 `[0-9A-Za-z@]`，所以 store 也拒絕其餘字元。
- **Keepalive 是倒著跑的** —— server 送 `GT_PING_ACK(102)`，客戶端回
  `GT_PING_REQ(101)`。客戶端的 dispatcher 對 102 催生 101（`sub_58D6F0`），
  沒有 101 的 handler、也沒有 102 的 builder。回應一個收到的 101 會無限循環。
- **回覆依 request 順序** —— handler 是 async，所以 dispatch 按連線串鏈。
  客戶把回覆與請求按位置配對，而併發 dispatch 會讓快的回覆超車慢的回覆。

### 建置途中修正的一項文件錯誤

`docs/PACKETS.md` 過去帶有兩個互相矛盾的 AES-CFB 測試向量：CFB keystream
第一個 block 是 `AES(IV)`，不可能依賴明文，但那兩個暗示的 keystream 在
16 byte 中只重合 1 個。本實作通過 FIPS-197 C.1 與兩個文件中記載的 *ECB*
向量，所以 cipher 與 key 是對的，錯的是 CFB 期望值。文件現已改載重算後的
數值；見 `test/aes.test.ts`。

## 刻意不實作

該服務於 2016 年停止營運，許多遊戲行為只存在於原廠 server。凡筆記標示
UNRESOLVED 之處，本伺服器寧可什麼都不做、也不憑空發明規則 —— 不做傷害
計算、不做經濟、不做任務進度、不做掉落表；private UDP 也不超出有 native
來源佐證的 19 → 空 20 控制交換。為何這些行為無法僅從 client 推出，
見 `docs/WIKI_MECHANICS.md`。
