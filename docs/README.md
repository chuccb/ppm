# 文件導覽與逆向工作規則

本目錄不是一般產品文件：它同時保存 client-revision 證據、wire contract、
資源索引與尚未證實的 server 工作邊界。先從這一頁選擇正確入口，避免把
自動抽取表、Extracted resource 或 Wiki 歷史資料誤當成 original-service policy。

## 先讀哪一份

| 目標 | 先讀 | 再讀 | 不可省略的界線 |
|---|---|---|---|
| 修改或新增 packet handler | [`PACKETS.md`](PACKETS.md) 的對應 family | [`LAYOUTS_REQ.md`](LAYOUTS_REQ.md) 的 writer、[`LAYOUTS.md`](LAYOUTS.md) 的 reader、相關 `Handlers.*.cs` | 必須追到 sender → fields → consumer → state/cache/storage → observable behavior；只有 ACK reader 不足以產生成功 server policy。 |
| 釐清一個欄位或條件分支 | `PACKETS.md` | `PaperMan.exe.c` 的 caller/callee/xref 與 `LAYOUTS*.md` | 自動表只列 primitive read/write sequence；它不表示 optional branch、count loop 或欄位語意。 |
| 使用角色、物品、地圖、語音、parts 或 UI 資料 | [`RESOURCES.md`](RESOURCES.md) | 原始 `main:Extracted/` 檔案、native lookup 與 packet consumer | resource / XML / asset 的存在只能證明 client content，不證明可購、持有、預設、可見或有 entitlement。 |
| 理解 server socket、state、DB ownership | [`ARCHITECTURE.md`](ARCHITECTURE.md) | [`../server-cs/README.md`](../server-cs/README.md)、`Program` → `Router` → `Handlers.*` → `Db.*` | server state guard 是 compatibility inference 時，必須和 native fact 分開記錄。 |
| 選擇下一個未完成 handler | [`TODO_HANDLERS.md`](TODO_HANDLERS.md) 的「Current next evidence」與 inventory | 對應 `PACKETS.md` / `LAYOUTS*.md` | inventory 是工作地圖，不是已確認的 original-server behavior。 |
| 查外部歷史玩法 | [`WIKI_MECHANICS.md`](WIKI_MECHANICS.md) | native / resource / packet evidence | Wiki 只能提供搜尋線索，絕不可獨自補價格、掉落、初始裝備或 response。 |

根目錄 [`README.md`](../README.md) 是首次使用與全專案摘要；
[`server-cs/README.md`](../server-cs/README.md) 是 C# source topology、build 與 runtime
entry point。兩者都不取代欄位級的證據文件。

## 證據與可實作性

| 證據類別 | 可以確認 | 不能自行確認 |
|---|---|---|
| **Native fact / HIGH**：可達 writer、reader、consumer、常數或 state branch | 欄位寬度、順序、已觀察的 client state/cache/UI effect | original service 的 catalog、價格、ownership、grant、拒絕 code 或 transaction policy。 |
| **Resource fact / HIGH**：同 revision 的 `main:Extracted/` 資料 | asset、ID、UI 名稱、local lookup input | 物品的 service visibility、預設持有或回應 record。 |
| **Inference / MEDIUM/HIGH**：多條相容的 native/resource evidence | 有明確範圍與反證的 compatibility guard | 不可將推導文案寫成 original service fact。 |
| **Packet capture / service evidence** | capture 所屬版本與情境下的實際 request/response 行為 | 不可在沒有版本、account state 與重現條件時外推。 |
| **UNRESOLVED** | 已搜尋仍無法判定的界線 | 成功 ACK、inventory/currency mutation、假定零值或填充。 |

對會造成持久化或 client success effect 的改動，最低證據鏈是：

```text
request builder → every caller/state gate → exact parser/consumer
                → field data flow → source/ownership rule
                → mutation and response → next valid client state
```

不能完成此鏈時，保留已證實的 no-op、no-ACK 或 consumer-safe failure arm；在文件中
清楚標成 **UNRESOLVED**，而不是用資源或一般遊戲慣例補洞。

## 文件角色與維護方式

| 檔案 | 角色 | 維護規則 |
|---|---|---|
| `PACKETS.md` | 手工整理的 packet / consumer / state evidence 與 implementation boundaries | 新結論要附 function、field order、consumer 與 confidence。歷輪 section number 是穩定引用，勿為美觀大幅重排。 |
| `LAYOUTS.md` | S2C dispatcher 的自動 primitive-read inventory | 不把表內線性序列誤讀為完整 payload grammar；重要例外補到 `PACKETS.md`。 |
| `LAYOUTS_REQ.md` | C2S builder 的自動 primitive-write inventory | 同一 opcode 可有多種 builder form；先回到所有 caller，不可只取第一列。 |
| `RESOURCES.md` | Extracted resource format、resource-to-client cross-check 與資源界線 | 加入 ID/資產結論時同時標明是 display-only、lookup input 或已證實 authority。 |
| `ARCHITECTURE.md` | server lifecycle、runtime ownership、layer boundaries 的全景圖 | 保持高層次；欄位細節連回 `PACKETS.md`。 |
| `TODO_HANDLERS.md` | 現在的 evidence gaps、未實作 request inventory、歷輪背景 | 新工作先更新 current section；已完成項目的關鍵 provenance 移入對應 packet/resource 文件。 |
| `WIKI_MECHANICS.md` | 外部歷史資料的搜尋索引與反推禁止清單 | 只登錄歷史觀察與下一步驗證問題，不登錄 server policy。 |

`PaperMan.exe.c` 是由 IDA/Hex-Rays 匯出的巨大 decompile，`Extracted/` 是 client
resource snapshot；兩者都應以小範圍、可重現的 search/excerpt 研究，不能為格式化、
命名偏好或猜測而全檔重寫。

## Source 與資料入口

| 區域 | 入口 | Ownership / generated boundary |
|---|---|---|
| `server-cs/src/PaperMan.Protocol/` | `Packet`, `*Wire`, `PacketCodec`, `UdpPacketCodec` | wire primitives、named protocol contracts、TCP/UDP codecs；不含 socket、DB 或 gameplay policy。 |
| `server-cs/src/PaperMan.Server/` | `Program` → `Router` → `Handlers.*` | TCP/UDP ownership、session state、per-domain handlers 與 SQLite access；詳細導航見 [`server-cs/README.md`](../server-cs/README.md)。 |
| `server-cs/src/PaperMan.SelfTest/Program.cs` | executable, dependency-free wire/bootstrap integration checks | 強化改動過的 wire/state boundary；它不是 original-service capture。 |
| `db/schema.sql` / `db/packets.tsv` | schema 與 opcode catalog | Server assembly 的 embedded bootstrap inputs；schema 改動需兼顧 `DatabaseBootstrapper` migration。 |
| `server/packet.py` / `server/pmfile.py` | Python protocol/resource reference tooling | 可用於 codec / pmFile cross-check；C# server 啟動不依賴 Python。 |
| `server-cs/tools/gen_opcodes.py` | `db/packets.tsv` → `PaperMan.Protocol/Opcode.cs` | `Opcode.cs` 是 generated output；修改 opcode 名稱或值時由 source TSV / generator 處理，不手改 output。 |

## 每次改動前後的最小檢查

1. 先讀本頁與相關 `README`、packet/resource evidence；確認每個結論的 Fact / Inference /
   Assumption / UNRESOLVED 分類。
2. 對 packet 變更核對 header、field order、signedness、optional/count framing、client
   state gate、consumer effect、malformed input behavior 和後續合法 state。
3. 對 C# 變更保留直接的 control flow、具 domain 意義的名稱、nullable invariant、async
   cancellation/resource ownership 與可診斷的錯誤路徑；不要為了抽象或新語法改寫無關區域。
4. 更新最常被使用且真正承載新結論的 Markdown；避免重複貼相同證據到每份文件。
5. 執行可用的 format/whitespace、Python 或 .NET tests。若當前環境沒有 .NET SDK，明確
   記錄 build / `PaperMan.SelfTest` 尚未執行，不能宣稱 compiler-backed success。

目前 Arena sandbox 沒有 `dotnet`、`csc` 或 `mcs`；C# runtime verification 必須在具
.NET 10 SDK 的環境補做。