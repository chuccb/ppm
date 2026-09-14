# PaperMan 私服 — C# 14 / .NET 10

以 `PaperMan.exe.c` (IDA/Hex-Rays 反編譯) 逐函數重建的伺服端。
資料層為 **SQLite** (`db/paperman.db`, schema 見 `db/schema.sql`)。

```
server-cs/
├── PaperMan.slnx                  # .NET 10 新式 XML solution (sln 已淘汰)
├── tools/gen_opcodes.py            # db/packets.tsv → Opcode.cs (勿手改 Opcode.cs)
└── src/
    ├── PaperMan.Protocol/          # 純協定層 (無 IO 依賴)
    │   ├── Opcode.cs               # 670 opcodes ← sub_9D2050 註冊表
    │   ├── Packet.cs               # 讀寫原語 ← sub_592xxx 家族 (CP949/wstr/blob/內嵌)
    │   ├── LoginWire.cs            # 682/681/693/694 嚴格 wire contract
    │   ├── ChannelBootstrapWire.cs # 142/144/196 + packed calendar contract
    │   ├── PaperLz.cs              # LZSS ← sub_591600 / sub_591900
    │   ├── PaperAes.cs             # AES-128-CFB-128 (IV=0) ← sub_403430/4042A0/404470
    │   └── PacketCodec.cs          # 送收管線 ← sub_593280 / sub_5930C0
    ├── PaperMan.Server/            # TCP 伺服器
    │   ├── Program.cs              # 入口 (top-level, 每連線一 task)
    │   ├── ServerContext.cs        # listener/681/bootstrap 組態 + LoginCode
    │   ├── ChannelAdmissionRegistry.cs # 681→143 IP-bound one-use handoff
    │   ├── Session.cs              # 9600B 緩衝框架, 錯包全丟 (sub_555280 行為)
    │   ├── Router.cs               # FrozenDictionary 路由 (≈ sub_58B010 switch)
    │   ├── Db.cs                   # Microsoft.Data.Sqlite 存取層 (record 模型)
    │   ├── Handlers.Auth.cs        # 682→681, ping (694 is Program greeting)
    │   ├── Handlers.Channel.cs     # 143→144→195→196; 141→142 endpoint confirm
    │   ├── Handlers.Lobby.cs       # 105/107/197/199/210/212
    │   ├── Handlers.Shop.cs        # 356/204/695
    │   ├── Handlers.Stats.cs       # GP_CH*C 戰績家族 (18 REQ + 882 推播)
    │   └── Handlers.BattleObjects.cs # OCC 902–907 權威狀態 + 962 安全拒絕
    └── PaperMan.SelfTest/          # 不需客戶端的 codec 自測
```

## 建置與執行

本沙箱無法安裝 .NET SDK (鏡像全被擋), 程式碼**未經編譯**, 請在有
.NET 10 SDK 的機器上:

```bash
cd server-cs
dotnet build                                   # 只需還原 1 個套件: Microsoft.Data.Sqlite 10.0.12
                                               #   (CP949 編碼已內建於 .NET 10 shared framework,
                                               #    無需 System.Text.Encoding.CodePages — 加了反而 NU1510)
dotnet run --project src/PaperMan.SelfTest     # 先跑自測 (codec + login/channel wire layouts)
dotnet run --project src/PaperMan.Server -- ../db/paperman.db 40200
# 40200 = 登入伺服器 (握手 694); 40201 = 頻道伺服器 (握手 693, 自動 +1)
```

- **AES 金鑰已內建**: 客戶端硬編碼金鑰 = EUC-KR 字串「트렁크점령전머지」
  (`C6AEB7B7 C5A9C1A1 B7C9C0FC B8D3C1F6`), 自反編譯 `sub_403430`
  (Hex-Rays 9.4 重導出直接展開字串來源) 完整還原, 並以獨立 AES 實作
  + FIPS-197 測試向量三重驗證。預設啟用 (`PaperAes.DefaultKey`);
  第三個參數給 `off`/`plain` = 明文模式, 或 32 位 hex = 自訂金鑰。
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
  channel listener 收 682、在 login listener 使用其他 lobby opcode，且在
  channel 143 claim 成功前只允許 ping/143；唯一例外是 native 每個 144 後都會
  送出的 195，未認證時它只會得到無 endpoint tail 的拒絕 196。
- **682 的真實欄位**：`str account, str password_or_token, u64 packed_data_revision,
  u8 fingerprint_source, raw[24] fingerprint`。u64 的高 32 bits 是
  `datarevision.txt ^ 0xB1A9D7C7`，低 32 bits 必為 `0x0000000E`；它不是硬體
  key。`fingerprint_source` 是 2=storage serial、1=fallback adapter MAC、0=none。
  伺服器嚴格要求 NUL、完整 raw24、無 trailing data，保存 source/raw24/revision，
  且不記錄密碼或 fingerprint bytes。
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
- 接收 (`sub_555280`): AES 解密 (驗 w0≥16、16 對齊、==align16(w2)、<0x2578)
  → 若 w3≥門檻且 w0<w3 → LZ 解壓 (結果須==w3 且 <0x2580); 失敗丟整緩衝。
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
2. **交易完整性**: 買道具 = 扣款+入包+記帳一個 transaction (`Db.BuyItem`),
   SQLite 的 ACID 與 `STRICT` 表 + `CHECK` 約束把反編譯得出的
   合法值域 (period 白名單、slot 0..5119、角色槽 0..19) 直接壓進 schema。
3. **零運維**: 一個檔案即全部狀態, 備份 = 複製檔案 (或 `VACUUM INTO`),
   對私服/保存性專案是決定性優勢。
4. **生態現況 (2026)**: SQLite 3.4x+ 系列持續演進 (WAL2、更強的
   query planner), `Microsoft.Data.Sqlite` 在 .NET 10 為第一方支援;
   若未來要上多行程, 換 `Data Source=...;Cache=Shared` 或遷 libSQL/Turso
   都不需改 schema。
