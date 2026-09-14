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
    │   ├── PaperLz.cs              # LZSS ← sub_591600 / sub_591900
    │   ├── PaperAes.cs             # AES-128-ECB ← sub_403430/4042A0/404470
    │   └── PacketCodec.cs          # 送收管線 ← sub_593280 / sub_5930C0
    ├── PaperMan.Server/            # TCP 伺服器
    │   ├── Program.cs              # 入口 (top-level, 每連線一 task)
    │   ├── ServerContext.cs        # 組態 record + LoginCode enum
    │   ├── Session.cs              # 9600B 緩衝框架, 錯包全丟 (sub_555280 行為)
    │   ├── Router.cs               # FrozenDictionary 路由 (≈ sub_58B010 switch)
    │   ├── Db.cs                   # Microsoft.Data.Sqlite 存取層 (record 模型)
    │   ├── Handlers.Auth.cs        # 682→681+694, ping
    │   ├── Handlers.Lobby.cs       # 105/107/197/199/210/212
    │   ├── Handlers.Shop.cs        # 356/204/695
    │   └── Handlers.Stats.cs       # GP_CH*C 戰績家族 (18 REQ + 882 推播)
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
dotnet run --project src/PaperMan.SelfTest     # 先跑自測 (codec round-trip)
dotnet run --project src/PaperMan.Server -- ../db/paperman.db 40200
# 40200 = 登入伺服器 (握手 694); 40201 = 頻道伺服器 (握手 693, 自動 +1)
```

- **AES 金鑰已內建**: 客戶端硬編碼金鑰 = EUC-KR 字串「트렁크점령전머지」
  (`C6AEB7B7 C5A9C1A1 B7C9C0FC B8D3C1F6`), 自反編譯 `sub_403430`
  (Hex-Rays 9.4 重導出直接展開字串來源) 完整還原, 並以獨立 AES 實作
  + FIPS-197 測試向量三重驗證。預設啟用 (`PaperAes.DefaultKey`);
  第三個參數給 `off`/`plain` = 明文模式, 或 32 位 hex = 自訂金鑰。
- **壓縮門檻**: 預設送 `0x2580` (=9600) 給 `GL_ACCOUNTCONNSUCC(694)` → 客戶端
  永不壓縮, 與原版預設一致, 可簡化除錯。
- LZ 演算法已用 Python 逐行移植做過 310 組 round-trip 驗證 (含 fuzz)。

## 協定要點 (詳見 ../docs/PACKETS.md)

- Header 8B: `[u16 w0=size][u16 w1=opcode][u16 w2][u16 w3]`, frame = w0+8。
- 送出 (`sub_593280`): 首次送出時 w3:=原始大小 → (w0≥門檻時) LZ 壓縮
  (**w3 不變**, w0:=壓縮後) → **一律** AES-128-ECB (補齊 16, **w2:=加密前大小**,
  w0:=對齊後)。⚠ w2 由且僅由 AES 層寫入; 加密失敗原版客戶端 ExitProcess。
- 接收 (`sub_555280`): AES 解密 (驗 w0≥16、16 對齊、==align16(w2)、<0x2578)
  → 若 w3≥門檻且 w0<w3 → LZ 解壓 (結果須==w3 且 <0x2580); 失敗丟整緩衝。
- dispatcher `sub_58B010` 有 365 個 case, 含 **27 個未註冊 opcode**
  (203, 995..1010 等) — 協定實際延伸到 1010; 未知 opcode 靜默忽略。
- popcount+XOR「seal」層 (`sub_5923D0/592420`) 為**死碼**, 無呼叫者, 不實作。
- 字串 = NUL 結尾 CP949 (無長度前綴, `lstrlenA+1`); 寬字串 = 雙 NUL UTF-16LE;
  blob = u16 len 前綴 (`sub_592BA0`); 內嵌 packet = u16 op + u32 size + payload。

## C# 14 / .NET 10 特性使用

- `field` keyword (CompressThreshold 正規化 setter)
- primary constructors (Packet / Session / PacketCodec)
- collection expressions `[...]`、list pattern (`is [1,2,3]`)
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
