# PaperMan 私服重建 — 協議逆向 + SQLite 伺服器 DB

從 `PaperMan.exe.c` (Hex-Rays 9.4 IDA 導出, 81 萬行) 逆向出完整封包協議，
並據此重建伺服器端 SQLite 資料庫。

## 目錄

| 路徑 | 內容 |
|------|------|
| `docs/PACKETS.md` | **協議完整分析**: Packet 類佈局、wire 格式、序列化原語、checksum/壓縮/加密層、關鍵 payload 結構 (全部附反編譯函數地址) |
| `db/packets.tsv` | 從 `sub_9D2050` 抽出的 **670 個 opcode ↔ 名稱** 對照表 (100–994) |
| `db/schema.sql` | SQLite schema (STRICT tables, 30 表 + 3 視圖 + 4 觸發器), 每個欄位註明來源封包/函數 |
| `db/build_db.py` | 建 DB + 匯入 opcode 註冊表 + 預設運維設定 + 自檢 |
| `db/smoke_test.py` | 模擬 登入→建角→購物→背包分頁→開房→結算→好友/訊息/任務/公會 全流程的 DB 讀寫測試 |
| `db/paperman.db` | 已建好的資料庫 |
| `server/packet.py` | wire 協議 Packet 參考實作 (Python, 逐函數對應反編譯), 含自測 |
| `server-cs/` | **C# 14 / .NET 10 伺服器** (協定層 + TCP 伺服器 + SQLite 存取層 + 自測), 見 `server-cs/README.md` |

## 快速開始

```bash
python3 db/build_db.py --fresh   # 重建 DB
python3 db/smoke_test.py         # 跑全流程測試
python3 server/packet.py         # Packet 編解碼自測
```

## 逆向重點摘要

### Wire 格式 (Packet 類 @ 0x591AC0, `sub_591DA0` 初始化)

```
[u16 payload_size][u16 opcode][u16 w2][u16 w3=orig_size][payload ≤9592B]
```
- 傳送 `sub_555090` → `sub_593280`: w3=原始大小 → (w0≥門檻時) 自製 LZ
  壓縮 (`sub_591600`, w2=壓前大小) → **一律** AES-128-ECB 加密
  (`sub_4042A0`, 補齊 16, w2=加密前大小) → `WSASend(this+24, size+8)`
- 接收 `sub_5930C0`: AES 解密 (驗 16 對齊 + `w0==align16(w2)`) →
  (w3≥門檻且 w0<w3 時) LZ 解壓 (`sub_591900`, 結果須==w3), 壞包整緩衝丟棄
- ⚠️ popcount checksum + XOR「seal」層 (`sub_5923D0/sub_592420`) 為
  **死碼** (無呼叫者), 二次深挖後已自管線剔除 — 詳見 `docs/PACKETS.md` §1.4
- 壓縮門檻由 `GL_ACCOUNTCONNSUCC(694)` 的 u16 協商, 預設 0x2580(9600)=永不壓縮
- AES-128 金鑰**已還原**: EUC-KR 字串「트렁크점령전머지」=
  `C6AEB7B7C5A9C1A1B7C9C0FCB8D3C1F6` (`sub_403430` 字串字面量, 過測試向量)
- 字串: NUL 結尾 ANSI (CP949), 無長度前綴 (`sub_5926F0` = `lstrlenA`+1);
  寬字串: 雙 NUL 結尾 UTF-16LE (`sub_592770`)

### Opcode 註冊表 (`sub_9D2050`)

674 次 `sub_9EAF50(name, id)` 呼叫註冊全部封包名稱; ID 即 opcode。
`GS_BASE=100`, 慣例 `ACK = REQ+1`。前綴分子系統
(GL 大廳 / GR 房間 / GG 戰鬥 / GS 商店 / GP 戰績 / GQ 任務 / MASTER 運維 ...)。
客戶端主 dispatcher: `sub_58B010` 的 `switch(opcode)`。

### DB 設計直接取自封包硬限制

| 事實 | 來源 | Schema 對應 |
|------|------|-------------|
| 背包 5120 格, 100/頁 | `sub_524B70` | `inventory.slot CHECK 0..5119` |
| 角色槽 ≤20, 12 裝備位 | `sub_524010` | `characters` |
| 武器編組固定 4 組, 8 parts | `sub_524660` | `weapon_groups` + bootstrap trigger |
| 技能/快速槽 7 格 | `sub_527AF0` (0x1C) | `skill_slots` |
| 戰績 19 計數器 | GP_CH*C 家族 | `user_stats` |
| 房間 ≤210, 10 槽 | `sub_568CE0` | `rooms/room_slots` CHECK |
| 期限白名單 1/7/15/30/60/90 天 | `sub_570B00` | `inventory.period_days CHECK` |
| 訊息內文 ≤200 字 | `sub_55A630` (buf 201) | `messages.body CHECK` |

`protocol_packets` 表載入了全部 670 個 opcode, 伺服器可直接拿來做
route table / 日誌 / `packet_stats` 監控。

## C# 14 伺服器 (`server-cs/`)

依上述逆向成果重建的可運行伺服端 (net10.0, `LangVersion 14`):

- `PaperMan.Protocol` — 純協定層: `Opcode.cs` (670 opcodes, 由
  `tools/gen_opcodes.py` 從 `db/packets.tsv` 產生)、`Packet.cs` (讀寫原語)、
  `PaperLz.cs` / `PaperAes.cs` / `PacketCodec.cs` (真實 LZ+AES 管線)。
- `PaperMan.Server` — TCP 伺服器: 9600B 框架 (`Session.cs`)、SQLite 存取層
  (`Db.cs`, 交易式購物/登入/暱稱/背包分頁)、封包 handlers
  (登入 681/694、大廳、商店、送禮 296/297、戰隊隧道 583/584、
  GP_CH*C 戰績 18 REQ/ACK 對 + 882 推播)。AES 原生金鑰已內建。
- `PaperMan.SelfTest` — 不需遊戲客戶端的 codec round-trip 自測。
- LZ 演算法另以 Python 逐行移植跑過 310 組 round-trip/fuzz 驗證。

本沙箱無法安裝 .NET SDK (所有鏡像被網路封鎖), 原始碼未經編譯 —
建置/執行方式與 AES 金鑰抽取方法見 `server-cs/README.md`。

### 為何選 SQLite (2026-09 現況)

單行程私服 + WAL 模式 = 每秒數萬寫入輕鬆達標且無網路 round-trip;
`STRICT` 表 + `CHECK` 約束把逆向得到的值域直接壓進 schema;
一檔即全部狀態、零運維; `Microsoft.Data.Sqlite` 是 .NET 10 第一方支援。
詳細論證見 `server-cs/README.md` 末節。
