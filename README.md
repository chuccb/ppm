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
| `server/packet.py` | wire 協議 Packet 參考實作 (逐函數對應反編譯), 含自測 |

## 快速開始

```bash
python3 db/build_db.py --fresh   # 重建 DB
python3 db/smoke_test.py         # 跑全流程測試
python3 server/packet.py         # Packet 編解碼自測
```

## 逆向重點摘要

### Wire 格式 (Packet 類 @ 0x591AC0, `sub_591DA0` 初始化)

```
[u16 payload_size][u16 opcode][u16 checksum][u16 orig_size][payload ≤9592B]
```
- 傳送 `sub_555090`: `WSASend(this+24, size+8)`
- 密封 `sub_5923D0`: checksum = payload 逐 byte popcount 和 (`sub_592220`)，
  之後 payload 逐 byte XOR checksum 低 8 位 (`sub_592470`)
- 大包再經自製 LZ (`sub_591600`) + 16-byte 區塊加密 (`sub_4042A0`)
- 字串: NUL 結尾 ANSI (CP949), 無長度前綴

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
