# PaperMan 私服 — C# 14 / .NET 10

以 `PaperMan.exe.c` (IDA/Hex-Rays 反編譯) 逐函數重建的伺服端。
資料層為 **SQLite** (`db/paperman.db`, schema 見 `db/schema.sql`)。

```
server-cs/
├── PaperMan.sln
├── tools/gen_opcodes.py            # db/packets.tsv → Opcode.cs (勿手改 Opcode.cs)
└── src/
    ├── PaperMan.Protocol/          # 純協定層 (無 IO 依賴)
    │   ├── Opcode.cs               # 670 opcodes ← sub_9D2050 註冊表
    │   ├── Packet.cs               # 讀寫原語 ← sub_592xxx 家族 (CP949 字串)
    │   ├── PaperLz.cs              # LZSS ← sub_591600 / sub_591900
    │   ├── PaperAes.cs             # AES-128-ECB ← sub_403430/4042A0/404470
    │   └── PacketCodec.cs          # 送收管線 ← sub_593280 / sub_5930C0
    ├── PaperMan.Server/            # TCP 伺服器
    │   ├── Session.cs              # 9600B 緩衝框架, 錯包全丟 (原版行為)
    │   ├── Db.cs                   # Microsoft.Data.Sqlite 存取層
    │   ├── Handlers.cs             # login/lobby/shop/nick handlers
    │   ├── StatHandlers.cs         # GP_CH*C 戰績家族 (19 計數器)
    │   └── Program.cs
    └── PaperMan.SelfTest/          # 不需客戶端的 codec 自測
```

## 建置與執行

本沙箱無法安裝 .NET SDK (鏡像全被擋), 程式碼**未經編譯**, 請在有
.NET 10 SDK 的機器上:

```bash
cd server-cs
dotnet build                                   # 需要 nuget 還原兩個套件:
                                               #   Microsoft.Data.Sqlite
                                               #   System.Text.Encoding.CodePages
dotnet run --project src/PaperMan.SelfTest     # 先跑自測 (codec round-trip)
dotnet run --project src/PaperMan.Server -- ../db/paperman.db 40200 <AES金鑰hex32>
```

- **AES 金鑰**: IDA 匯出的 `.c` 不含資料段, 必須自原版 `PaperMan.exe`
  的 `.data` VA `0xB69E88` 抽出 16 bytes (例如 IDA 中 `unk_B69E88` 按 Shift+E)。
  不給金鑰 = 明文模式, 僅供自測/代理除錯, 真客戶端無法連。
- **壓縮門檻**: 預設送 `0x2580` (=9600) 給 `GL_ACCOUNTCONNSUCC(694)` → 客戶端
  永不壓縮, 與原版預設一致, 可簡化除錯。
- LZ 演算法已用 Python 逐行移植做過 310 組 round-trip 驗證 (含 fuzz)。

## 協定要點 (詳見 ../docs/PACKETS.md)

- Header 8B: `[u16 size][u16 opcode][u16 w2][u16 w3]`, frame = size+8。
- 送出: w3=原始大小 → (視門檻) LZ 壓縮 (w2=壓前大小) → **一律** AES-128-ECB
  (補齊 16, w2=加密前大小)。
- 接收: AES 解密 (驗 16 對齊 + w0==align16(w2)) → (條件) LZ 解壓 (結果須==w3)。
- popcount+XOR「seal」層 (`sub_5923D0/592420`) 為**死碼**, 無呼叫者, 不實作。
- 字串 = NUL 結尾 CP949 (無長度前綴); 寬字串 = 雙 NUL 結尾 UTF-16LE。

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
