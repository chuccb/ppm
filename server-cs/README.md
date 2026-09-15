# PaperMan 私服 — C# 14 / .NET 10

以 `PaperMan.exe.c` (IDA/Hex-Rays 反編譯) 逐函數重建的伺服端。
資料層為 **SQLite**。首次啟動由 server 自動建立 schema、opcode catalog 與預設
運維設定；開發模式預設檔是 repository 的 `db/paperman.db`。

## 原始碼導覽

本專案按 protocol boundary 與 persistent domain 切分，而不是套用通用 framework。
每條主要資料流都由下表左側開始；handler dispatch 從 `Router.Build()` 的 generated entry
point 追到 canonical source，而不是維護另一份手寫註冊表。

| 區域 | 檔案 / 入口 | 責任與 ownership |
|---|---|---|
| Solution、generated catalog 與 static checks | `PaperMan.slnx`, `tools/gen_opcodes.py`, `tools/verify_server_layout.py`, `tools/verify_server_naming.py`, `src/PaperMan.Protocol/Generated/Opcode.cs` | `db/packets.tsv` 是 opcode source；要改 opcode 名稱或值時執行 generator，不手改 generated output。layout checker 驗證 catalog → generated discovery → canonical handler source graph；naming checker 驗證 native mode vocabulary、Extracted default maps 與 map bit / two-team tables。兩者皆不取代 build。 |
| Protocol | [`src/PaperMan.Protocol/README.md`](src/PaperMan.Protocol/README.md) | byte-exact `Core/`、`Codecs/`、`Contracts/` 與 `Generated/` boundary；不放 socket、DB 或 gameplay policy。 |
| Handler source generator | [`src/PaperMan.HandlerGenerator/README.md`](src/PaperMan.HandlerGenerator/README.md) | compiler-only Roslyn analyzer，從 canonical direct entries 產生 Router method-group table；不做 runtime reflection，僅此 dispatch path 可宣稱 NativeAOT-friendly。 |
| Server source guide | [`src/PaperMan.Server/README.md`](src/PaperMan.Server/README.md) | 由 runtime flow 或 canonical opcode 直接定位 Host、State、Database、compile-time discovered Handler family。 |
| Executable host | `src/PaperMan.Server/Host/` | 零參數 startup、listener configuration、TCP session lifetime、role/state gate、dispatch 與 narrow UDP endpoint。主路徑為 `Program → Session.ReceiveAsync → Router → handler`。 |
| Process-local state | `src/PaperMan.Server/State/` | rooms/seats、room battle state、online-session lookup、one-use 681→143 admission；不是 durable account state。 |
| SQLite ownership | `src/PaperMan.Server/Database/` | bootstrap/connection 與 `Db.*` persisted-domain partials。跨 table atomic change 放在擁有該 operation 的 partial，transaction 必須明確可見。 |
| Canonical handlers | `src/PaperMan.Server/Handlers/<family>/` | direct source basename 與 receive entry 保留 `db/packets.tsv` / `Opcode.cs` canonical token；source generator 於編譯期自動發現並產生 binding。詳見 Server source guide 的完整 family map。 |
| Assembly 與 executable checks | `Properties/AssemblyInfo.cs`, `src/PaperMan.SelfTest/Program.cs` | assembly metadata，以及 byte-level protocol / SQLite bootstrap / loopback checks；SelfTest 不取代 original-service capture。 |

在修改 handler 或 resource-derived value 前，先讀
[`../docs/README.md`](../docs/README.md) 的 evidence hierarchy、文件入口、generated-file
boundary 與 reverse-engineering checklist。

### Handler 檔名與 entry 命名

所有 handler family 均採用下列可由 opcode 反向直接定位的規則；不得另造需要反向映射的泛化業務名稱：

1. 以 `db/packets.tsv` 為 canonical spelling；`tools/gen_opcodes.py` 產生的
   `Opcode.cs` 是 C# 端同一 token 的檢查點。native string/comment 可補充該 family 的
   provenance，但不取代 catalog。
2. 有 `*_REQ` 的 handler，檔名是 `Handlers.<token 去掉 _REQ>.cs`，entry method 是完整
   `<TOKEN>_REQ`；例如 `Handlers.GI_CHANGEWP.cs` 的 `GI_CHANGEWP_REQ`。paired packet
   builder/parser 的 method name 也保留完整 `*_REQ` 或 `*_ACK` token。
3. 沒有 `*_REQ` 後綴的單向 token（目前為 `GL_MYINFO_OPEN`）同時作為檔名 family 與
   entry method。不存在以猜測業務語意命名的中介 handler 名稱。
4. `PaperMan.HandlerGenerator` 在編譯期以 top-level public static partial
   `*Handlers` container、exact method signature 與已驗證 C2S canonical token
   （`*_REQ` 加 `GL_MYINFO_OPEN`）產生 binding；不要加入 runtime
   reflection、hand-written registration list 或需要把 protocol token 反向映射的 service
   abstraction。`*.Shared.cs` 僅限已明確記錄的
   跨-family wire / authority support，不含 receive entry。
5. 唯一沒有官方 request token 的已註冊 path 是 raw opcode 206；它保留
   `Handlers.RawOpcode206.cs` / `RawOpcode206_REQ`，以 `[RawOpcodeHandler(206)]`
   明示為 raw evidence boundary，絕不補造 GS request token。
6. 修改 catalog、handler path 或 direct receive entry 後，執行
   `python3 server-cs/tools/verify_server_layout.py`。它會靜態驗證
   `packets.tsv → Opcode.cs → compile-time discovery → canonical source/entry`，但不取代
   C# 編譯、SelfTest 或 real-client capture。

這是導覽規則，不改變 packet header、field order、length gate、state guard、SQLite
ownership 或未知邊界的 fail-closed 行為。

### Native 與 resource 名稱的證據邊界

**Fact/HIGH.** Catalog token、native semantic name 與 resource/UI display name 是三個
不同來源，不能因為字面相近就互相覆蓋：

1. wire handler / filename / direct entry 一律保留 `packets.tsv` / generated `Opcode.cs`
   的完整 canonical token；`verify_server_layout.py` 保障這條路徑。
2. `State/Room.cs` 的 `GameMode` 成員逐字對應 native `sub_53FBB0` 選取的
   `CyGameModes::Cy*ModeLobbyUI` suffix，並以 client payload 的 `modeIndex` 作為
   欄位名。native class semantic 不是 resource 的 UI 別名。
3. `main:Extracted/ui/system/map_StartIndex.xml` 的 `modeName` 是 client UI/resource
   spelling；若它與 native name 不同，兩者都必須在鄰近 comment 或 evidence 文件中
   保留（例如 `TeamMatch` / `TeamDeath`），不能以其中一個改寫另一個。
4. `Extracted/` 只證明 client lookup/display input，不證明 original-service policy。
   local directory/type/SQLite names 只可描述 Server ownership，不能佯稱原服務採用該名。
5. 缺少 catalog、native class/string 或 verified resource 名稱時，保留 `Raw`、`Reserved`、
   `Opaque` 或 `UNRESOLVED` boundary 與 provenance；不可為求好讀而捏造「官方」名。

修改 room `modeIndex`、native mode name、default map、maplist bit 或兩隊模式 predicate
後，執行 `python3 server-cs/tools/verify_server_naming.py`。它會從本 checkout 的
`PaperMan.exe.c` 和不 checkout 的 `origin/main:Extracted/ui/system/map_StartIndex.xml`
（沒有 remote-tracking ref 時退回 `main`）重新核對 C#；它同樣不取代 build、SelfTest
或 real-client capture。

## 建置與執行

本沙箱無法安裝 .NET SDK (鏡像全被擋), 程式碼**未經編譯**, 請在有
.NET 10 SDK 的機器上:

```bash
# 在 repository root 執行；先跑不需 .NET 的 Server static checks。
python3 server-cs/tools/verify_server_layout.py
python3 server-cs/tools/verify_server_naming.py

# 沒有 DB 建置命令、路徑或 port 參數。
dotnet run --project server-cs/src/PaperMan.Server
# 第一次執行：自動建立 db/paperman.db、所有 schema、676 筆 packet catalog、
#              預設 server_config。
# 40200 = 登入伺服器 (握手 694); 40201 = 頻道伺服器 (握手 693, 自動 +1)
# 40202 = UDP-private control endpoint (encrypted 19 → empty 20, auto +2)

# 可選：在有 .NET 10 SDK 的機器先驗證。
dotnet build server-cs/PaperMan.slnx
dotnet run --project server-cs/src/PaperMan.SelfTest
```

- **AES 金鑰已內建**: 客戶端硬編碼金鑰 = EUC-KR 字串「트렁크점령전머지」
  (`C6AEB7B7 C5A9C1A1 B7C9C0FC B8D3C1F6`), 自反編譯 `sub_403430`
  (Hex-Rays 9.4 重導出直接展開字串來源) 完整還原, 並以獨立 AES 實作
  + FIPS-197 測試向量三重驗證。預設啟用 (`PaperAes.DefaultKey`);
  server 預設啟用；目前 zero-configuration launch 不讀 command-line 覆寫。
- **UDP-private 19→20 only (Fact/HIGH)**：successful 196 advertises `UdpHost` /
  `UdpPort`; `Program` binds it before it opens either TCP listener.
  `UdpControlServer` decrypts AES-only private opcode 19, validates every
  source-proven field, then replies to the source address with an **empty but
  still 16-byte-AES-encrypted** private opcode 20. It deliberately does not
  authorize the reported player/slot/name values, does not apply TCP LZ, and
  does not claim P2P, relay, NAT, or the behavior of other private UDP opcodes.
  Evidence and unknowns are maintained in `../docs/PACKETS.md` “UDP private
  transport and the only implemented control exchange”. `PaperMan.SelfTest`
  includes byte-level 19/20 checks plus a loopback source-address response test;
  it still needs execution on a machine with the .NET 10 SDK.
- **零參數、可攜 DB bootstrap**：`schema.sql` 與 `packets.tsv` 是 assembly 的
  embedded resources。`Db` 會建立父目錄、以 WAL/foreign keys 開啟 SQLite、套用
  idempotent schema、seed 676 筆 opcode 與未存在的運維預設值；既有玩家與管理者
  設定不會被覆寫。開發時預設使用 repository `db/paperman.db`；publish 後使用
  executable 旁的 `data/paperman.db`。只有需要自訂持久化位置時才設定環境變數
  `PAPERMAN_DATABASE_PATH`，不需要 command-line argument。
- **帳密、初始玩家與 migration guard**：新帳號使用 PBKDF2-SHA256（210,000
  iterations、per-account random 16-byte salt、32-byte derived hash）；成功登入
  必定有可供 197→198 讀取的 `users` row、stats/groups 與 starter character。
  預設暱稱優先使用合法的登入名稱；若不符合 Client 2..16 CP949-byte 限制則使用
  `P` 加 account id 的 base-36 值。這是讓單機私服新帳號可直接進大廳的 Server
  policy；Client 對 198 的 `success=0` 會顯示 code 17，不可拿它表示「尚未建角」。
  既有的 account-only row 會在密碼驗證後補齊 player identity。有效的舊
  `SHA256(salt + password)` 登入後立即升級。舊 DB 的 682 欄位會 rename/migrate，
  並用 trigger 補強 SQLite 無法以 `ALTER TABLE` 補上的 raw24 fingerprint 長度限制。
- **壓縮門檻**: 預設送 `0x2580` (=9600) 給 `GL_ACCOUNTCONNSUCC(694)` → 客戶端
  永不壓縮，與原版預設一致。native 只有收到 **< `0x2580`** 才覆寫門檻；
  因此 `ServerConfig` / `PacketCodec` 都拒絕大於 `0x2580` 的值，避免 client
  以 9600 壓縮、server 卻以較大門檻不解壓的雙向失配。`0` 會規範為 `0x2580`。
- LZ 演算法已用 Python 逐行移植做過 310 組 round-trip 驗證 (含 fuzz)。
- **有狀態戰場物件**: OCC 902/904/906 只接受 playing 的 Occupy 房成員，
  檢查 self-reported slot/uid 後才在每房 `RoomBattleState` lock 中作
  start/success/fail 轉換；962 在缺少經驗證的 959/961 掉落物 seed 前只回
  1-byte rejection，絕不偽造成功 ACK。完整證據與後續工作見
  `../docs/PACKETS.md` §3.15d3a。

## 已實作的登入／頻道 bootstrap

- **兩個角色、兩條 TCP connection**：登入 listener 連線時只發一次
  `694 GL_ACCOUNTCONNSUCC(u16 compression threshold)`；681 清單所列的頻道
  listener 則只發一次空 payload `693 GL_TCPCONNSUCC`。`Router` 會拒絕在
  channel listener 收 682、在 login listener 使用其他 lobby opcode，且拒絕
  成功 681 後的重複 682。143 claim 成功只完成帳號 handoff；直到 195 收到成功
  196、且該 196 已實際寫入 socket，才允許 lobby / room / economy / gameplay
  requests。唯一例外是 native 每個 144 後都會送出的 195，未認證時它只會得到
  無 endpoint tail 的拒絕 196。
- **682 的真實欄位**：`str account, str password_or_token, u64 packed_data_revision,
  u8 fingerprint_source, raw[24] fingerprint`。u64 的高 32 bits 是
  `datarevision.txt ^ 0xB1A9D7C7`，低 32 bits 必為 `0xF1E1AB0E`；它不是硬體
  key。`fingerprint_source` 是 2=storage serial、1=fallback adapter MAC、0=none。
  伺服器嚴格要求 NUL、完整 raw24、無 trailing data，保存 source/raw24/revision。
  Session diagnostics 只記錄 fingerprint source 與固定 24B 長度，絕不輸出 password/token
  或 fingerprint bytes。
- **681 成功 tail**：一個 account/net-café extension（0 或 1 組）、固定三個
  channel groups、s16 port bit pattern、type-3 extension byte 和最後的 billing
  s32×2 都由 `LoginWire` 具名建模與驗證。`ServerConfig` 會在開 listener 前
  檢查 CP949 fixed-buffer 長度與尚未實作 AI type-3 196 tail。144 的完整 optional
  `sNetCafeInfo` (4×u8 + 8×raw4) 則已由 `ChannelBootstrapWire` 具名建模並可設定。
- **142 / 144 / 196 source fixes**：142 的最後 raw4 是
  `(year-2000)<<24 | month<<19 | day<<13 | hour<<7 | minute`，不是 opaque config；
  server 以可注入 clock + 明確 `ProtocolTimeZone` 生成，且 142 的 u8 是 active
  channel index。144 的第一個 s32 是 daily-login PG notice（>0 才顯示），不是
  TCP session id；其 rank flag / level / K/D restriction 與 optional `sNetCafeInfo`
  的 native reader effects 已記錄。196 只有 `result==1` 有 endpoint tail，並把
  第三個 byte 設為 active channel index。詳細欄位與證據見 `../docs/PACKETS.md` §3.15d。
- **143 無法當 credential**：native `String[24]` 的大小可證，但此匯出尚未找到
  寫入者，不能猜它是 account 或 nickname。故 server 不以它當 lookup key；而是
  對同 source IP、billing UI mode、extension count 的「唯一」近期 681 admission
  做一次性 claim。相同 NAT 下有兩個無法區分的 live login 時會安全拒絕 143，直到
  有實包或 writer trace 可建立正確 identity mapping。這是 native handoff 格式本身
  的限制，而非可用零值或別名修補的項目。
- 舊 DB 的 `accounts.hw_key` / `security_state` 在首次開啟時自動 rename 成
  `client_data_revision` / `fingerprint_source`，並補入 `client_fingerprint`；
  既有資料會保留，但欄名改為真實 wire 語意。

## 協定要點 (詳見 ../docs/PACKETS.md)

- Header 8B: `[u16 w0=size][u16 w1=opcode][u16 w2][u16 w3]`, frame = w0+8。
- 送出 (`sub_593280`): 首次送出時 w3:=原始大小 → (w0≥門檻時) LZ 壓縮
  (**w3 不變**, w0:=壓縮後) → **一律** AES-128-CFB-128 (IV=0, 補齊 16, **w2:=加密前大小**,
  w0:=對齊後)。⚠ w2 由且僅由 AES 層寫入; 加密失敗原版客戶端 ExitProcess。
- 接收 (`sub_555280`): AES 解密 (驗 w0≥16、16 對齊、==align16(w2)、<0x2578；
  **w2=0 仍是有效的 16-byte encrypted frame**) → 若 w3≥門檻且 w0<w3 → LZ
  解壓 (結果須==w3 且 <0x2580); 失敗丟整緩衝。
- dispatcher `sub_58B010` 有 365 個 case, 含 **27 個未註冊 opcode**
  (203, 995..1010 等) — 協定實際延伸到 1010; 未知 opcode 靜默忽略。
- popcount+XOR「seal」層 (`sub_5923D0/592420`) 為**死碼**, 無呼叫者, 不實作。
- 字串 = NUL 結尾 CP949 (無長度前綴, `lstrlenA+1`); 寬字串 = 雙 NUL UTF-16LE;
  blob = u16 len 前綴 (`sub_592BA0`); 內嵌 packet = u16 op + u32 size + payload。

## C# 14 / .NET 10 特性使用

- immutable `record` 組態 + `init` properties（bootstrap metadata 具名化）
- primary constructors (Packet / Session / PacketCodec)
- collection expressions `[...]`、relational/property patterns
- `System.Threading.Lock`、`FrozenDictionary` 路由表
- span-first API (`ReadOnlySpan<byte>` 貫穿 protocol 層, `stackalloc` 零配置)
- `IAsyncEnumerable` recv 迴圈 + `await foreach`

## 為何 SQLite 仍是 2026 年的正確選擇

1. **單寫者工作負載**: 私服是單行程、低併發 (千人以下) 遊戲大廳。
   SQLite WAL 模式單機可承受每秒數萬次寫入, 遠超此遊戲的封包頻率;
   client/server DB (PostgreSQL 等) 的網路 round-trip 反而更慢。
2. **交易完整性（未來成功路徑的必要條件）**: 若有原始服務或實包證據能實作
   購買，扣款、入包和記帳必須是同一 transaction。現行 `Db.BuyItem` 不是對
   原始經濟政策的證據，也沒有由 `Handlers.Shop` 成功路徑呼叫；商店、送禮、
   福袋和抽獎一律回已驗證的無 mutation failure arm。SQLite 的 ACID 與
   `STRICT`/`CHECK` 可作為未來已證實 policy 的實作工具，不可反過來產生 policy。
3. **零運維**: 一個檔案即全部狀態, 備份 = 複製檔案 (或 `VACUUM INTO`),
   對私服/保存性專案是決定性優勢。
4. **生態現況 (2026)**: SQLite 3.4x+ 系列持續演進 (WAL2、更強的
   query planner), `Microsoft.Data.Sqlite` 在 .NET 10 為第一方支援;
   若未來要上多行程, 換 `Data Source=...;Cache=Shared` 或遷 libSQL/Turso
   都不需改 schema。
