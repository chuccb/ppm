# 文件導覽與逆向工作規則

本目錄同時保存三種不同性質的資料：

1. `PaperMan.exe.c` 的 native caller、reader、writer、consumer 與 state evidence。
2. 同 revision `Extracted/` 的資源格式、ID、字串與 UI cross-check。
3. `server-ts/` 的保守 wire projection、runtime boundary 與尚未完成的工作清單。

它們不是同一個證據層。client 有某個欄位、資源或 UI，不等於 original service
會接受、持有、贈送、收費或回傳成功。涉及持久化或成功 ACK 的改動，必須先
完成本頁的 evidence chain。

## 目前基線（2026-09-17）

- 唯一 server implementation：[`../server-ts/README.md`](../server-ts/README.md)。
- Runtime：Bun 1.4.3-canary、TypeScript `7.1.0-dev.20260915.1`、Bun SQLite
  3.53.4；版本以 `server-ts/package.json` 與 lockfile 為準。
- Wire proof：`PaperMan.exe.c`；資源 proof：`Extracted/`；離線 catalog：
  `db/packets.tsv`。
- `server-ts/src/packet.ts` 的 runtime wire validation 與 TypeScript compile-time
  type contract 必須分開；compile-time type 不能取代 field width、fixed-buffer、
  framing、mask/coerce、count limit 或 malformed-input checks。
- 未完成的 service policy 一律保持 `UNRESOLVED`、fail-closed 或 no-mutation；不以
  Wiki、item name、UI label 或一般遊戲慣例補洞。

## 先讀哪一份

| 目標 | 先讀 | 再讀 | 不可省略的界線 |
|---|---|---|---|
| 修改或新增 packet handler | [`PACKETS.md`](PACKETS.md) 的對應 family | [`LAYOUTS.md`](LAYOUTS.md) Part II writer、Part I reader、現有 `server-ts/src/ops/` module | 必須追 sender → fields → consumer → state/cache/storage → observable behavior；只有 ACK reader 不足以產生成功 server policy。 |
| 核對目前 TS packet 欄位 | [`SERVER_TS_EVIDENCE.md`](SERVER_TS_EVIDENCE.md) Part I | 對應 native audit、`PACKETS.md`、實際 `server-ts/src/ops/` 檔案 | TS 欄位名稱只是 projection；raw/unknown/flag/extra 不可擅自改成業務語意。 |
| 釐清一個欄位或條件分支 | [`PACKETS.md`](PACKETS.md) | `PaperMan.exe.c` 的 caller/callee/xref 與兩份 layout | 自動表只有 primitive read/write sequence，不表示 optional branch、count loop 或欄位語意。 |
| 登入／頻道 handshake | [`S2C_NATIVE_AUDITS.md`](S2C_NATIVE_AUDITS.md)（681 part） | 同檔 144、196 parts、`PACKETS.md` §3.15d | 保留 native width、成功/失敗 framing、single-use admission 與 196 success-only tail。 |
| 理解 server socket、state、DB ownership | [`ARCHITECTURE.md`](ARCHITECTURE.md) | [`../server-ts/README.md`](../server-ts/README.md)、`main.ts` → `connection.ts` / `udp.ts` → `ops/` → `store.ts` | server state guard 是 compatibility inference 時，必須和 native fact 分開記錄。 |
| 使用角色、物品、地圖、語音、parts 或 UI 資料 | [`RESOURCES.md`](RESOURCES.md) | 原始 `Extracted/`、native lookup 與 packet consumer | resource/XML/asset 只能證明 client content，不證明可購、持有、預設、可見或 entitlement。 |
| 由 client class、vftable 或 inheritance 定位 native 起點 | [`RTTI_PYCLASSINFORMER.md`](RTTI_PYCLASSINFORMER.md) | `PaperMan.exe.c` xref、`PACKETS.md` / `RESOURCES.md` data flow | RTTI 是 native xref 起點，不是 wire、server policy、ownership 或 storage evidence。 |
| 選擇下一個未完成 handler | [`SERVER_TS_EVIDENCE.md`](SERVER_TS_EVIDENCE.md) Part II | 對應 `PACKETS.md`、layout、resource 與 native chain | inventory 是工作地圖，不是 original-server behavior 的確認。 |
| 查外部歷史玩法 | [`WIKI_MECHANICS.md`](WIKI_MECHANICS.md) | native / resource / packet evidence | Wiki 只能提供搜尋線索，絕不可獨自補價格、掉落、初始裝備或 response。 |

## 文件地圖

| 文件 | 角色 | 維護規則 |
|---|---|---|
| [`PACKETS.md`](PACKETS.md) | 手工整理的 packet、consumer、state evidence 與 implementation boundary | 新結論附 function、field order、consumer、confidence 與 unresolved limit；歷史 section number 保留以維持引用。 |
| [`SERVER_TS_EVIDENCE.md`](SERVER_TS_EVIDENCE.md) | 目前 31 個 TS packet 的逐欄 native meaning、TS use、zero projection 與 unresolved audit（Part I）＋handler 待辦與下一步證據順序（Part II） | 只收錄 `server-ts/src/ops` 現有 modules；不要把保守欄位改成未證實業務名稱。 |
| [`S2C_NATIVE_AUDITS.md`](S2C_NATIVE_AUDITS.md) | 登入／頻道 bootstrap 的 reader、caller、consumer、resource 與 TS boundary（681/144/196 三 parts） | 修改 handshake 前先更新相應 audit；不能只改 builder 或 ACK 表。 |
| [`LAYOUTS.md`](LAYOUTS.md) | S2C dispatcher primitive-read（Part I）＋ C2S builder primitive-write（Part II，含 private UDP Appendix A/B）的自動 inventory | 同 opcode 可有多種 builder form；先回到所有 caller，不可只取第一列；不把線性序列誤讀成完整 payload grammar，重要例外補到 `PACKETS.md`。 |
| [`RESOURCES.md`](RESOURCES.md) | Extracted format、resource-to-client cross-check 與資源界線 | 每個 ID/資產結論標明 display-only、lookup input 或已證實 authority。 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | server lifecycle、runtime ownership、layer boundaries 的全景圖 | 保持高層次；欄位細節連回 `PACKETS.md` 或 audit。 |
| [`SERVER_TS_EVIDENCE.md`](SERVER_TS_EVIDENCE.md) Part II | 目前 evidence gaps、未實作 request inventory、下一步驗證順序 | 只保留 current work queue；歷史 provenance 放回 packet/resource 文件。 |
| [`WIKI_MECHANICS.md`](WIKI_MECHANICS.md) | 外部歷史資料的搜尋索引與反推禁止清單 | 只記錄歷史觀察與驗證問題，不登錄 server policy。 |
| [`RTTI_PYCLASSINFORMER.md`](RTTI_PYCLASSINFORMER.md) | PyClassInformer class、vftable、inheritance 搜尋索引 | 保留匯出值與多重繼承 offset；不把 RTTI root 當成 storage 或 service proof。 |

## Source 與資料入口

| 區域 | 入口 | Ownership / boundary |
|---|---|---|
| `server-ts/src/` | [`server-ts README`](../server-ts/README.md) → `main.ts` → `connection.ts` / `udp.ts` → `ops/` → `store.ts` | Bun runtime、TCP/UDP ownership、session state、per-packet handlers 與 SQLite projection。 |
| `server-ts/src/packet.ts` | frame reader/writer、AES-CFB、packet primitives | 保留 native 9600-byte framing、fixed-buffer bounds、field widths、mask/coerce 與 malformed-input rejection。 |
| `server-ts/test/` | Bun unit/integration tests | 驗證 wire framing、store projection、admission handoff 與 handler boundary；不是 original-service capture。 |
| `db/packets.tsv` | 676-opcode catalog | 是 `server-ts/src/opcodes.ts` 與 runtime registry 的 input；修改前須同步 native catalog evidence。 |
| `db/schema.sql` / `db/build_db.py` / `db/smoke_test.py` | 離線 SQLite schema、建立器與 smoke test | 不等同於 `server-ts/src/store.ts` 的目前最小 runtime projection；測試時明確說明使用哪一條路徑。 |
| `server/packet.py` / `server/pmfile.py` | Python protocol/resource reference tooling | 可做 codec / pmFile cross-check；Bun server 啟動不依賴 Python。 |
| `tools/` | resource、native gate、coverage 與 catalog 離線稽核工具 | 只讀取 `PaperMan.exe.c`、`Extracted/` 或文件，不屬於 runtime server。 |

## Evidence chain

對會造成持久化或 client success effect 的改動，最低鏈是：

```text
request builder → every caller/state gate → exact parser/consumer
                → field data flow → source/ownership rule
                → mutation and response → next valid client state
```

證據分類：

| 類別 | 可以確認 | 不能自行確認 |
|---|---|---|
| **Native fact / HIGH** | 欄位寬度、順序、可達 writer/reader、已觀察的 client state/cache/UI effect | catalog、價格、ownership、grant、拒絕 code 或 transaction policy。 |
| **Resource fact / HIGH** | asset、ID、UI 名稱、local lookup input | service visibility、預設持有、回應 record 或 entitlement。 |
| **Inference / MEDIUM/HIGH** | 範圍明確、由多條相容 evidence 支持的 compatibility guard | original-service fact；不可把推導文案寫成原廠行為。 |
| **Packet capture / service evidence** | 特定版本與 account state 下的 request/response 行為 | 沒有版本、狀態與重現條件時的普遍外推。 |
| **UNRESOLVED** | 已搜尋但仍無法判定的界線 | 成功 ACK、inventory/currency mutation、假定零值或 padding 語意。 |

不能完成完整鏈時，保留已證實的 no-op、no-ACK 或 consumer-safe failure arm，
並明確標為 **UNRESOLVED**；不要用資源或一般遊戲慣例補洞。

## 每次改動前後的最小檢查

1. 先讀本頁、相關 README、packet/resource evidence；把結論分成 Fact / Inference /
   Assumption / UNRESOLVED。
2. Packet 變更核對 header、field order、signedness、optional/count framing、client
   state gate、consumer effect、malformed input behavior 與後續合法 state。
3. TypeScript/Bun 變更保持直接 control flow、domain 名稱、strict 型別不變量、async
   cancellation/resource ownership 與可診斷錯誤路徑；不要順便重寫無關區域。
4. 只更新真正承載新結論的文件；不要把同一份 evidence 複製到每一份 Markdown。
5. 執行：

   ```bash
   git diff --check
   python3 tools/verify_dispatcher_coverage.py
   python3 tools/verify_native_gates.py
   python3 tools/verify_resource_claims.py
   python3 tools/verify_resource_coverage.py
   cd server-ts && bun run typecheck && bun test
   ```

   `Extracted/` 不完整時，resource coverage 允許明確 skip；沒有 Bun 時，必須明確
   記錄 typecheck/test 未執行，不能用 Python smoke test 代替 TypeScript runtime verification。

`PaperMan.exe.c` 是 IDA/Hex-Rays decompile，`Extracted/` 是 client resource
snapshot；兩者都應以小範圍、可重現的 search/excerpt 研究，不能為格式化、命名偏好
或猜測而全檔重寫。
