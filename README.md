# PaperMan 私服重建 — 協議逆向 + SQLite 伺服器 DB

從 `PaperMan.exe.c` (Hex-Rays 9.4 IDA 導出, 81 萬行) 逆向出完整封包協議，
並據此重建伺服器端 SQLite 資料庫。

## 目錄

| 路徑 | 內容 |
|------|------|
| `docs/PACKETS.md` | **協議完整分析**: Packet 類佈局、wire 格式、序列化原語、checksum/壓縮/加密層、關鍵 payload 結構 (全部附反編譯函數地址) |
| `docs/RESOURCES.md` | **客戶端資源地圖**: maplist/物品/任務/訊息表 `msgtableres.lang`、UI 圖像音效盤點、mode 枚舉正名 |
| `docs/LAYOUTS.md` / `docs/LAYOUTS_REQ.md` | 各封包 dispatcher 讀取序 / REQ builder 寫入序 (欄位級對照) |
| `docs/ARCHITECTURE.md` | 全景架構: 生命週期、資料層、加密、互證鏈 |
| `docs/TODO_HANDLERS.md` | 尚未實作的 server handler 清單與下一輪建議 |
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
python3 db/import_pats.py        # 資源目錄灌 DB (需先以 server/pmfile.py 解密 cfg/*.pat)
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

## 客戶端資源地圖 (`Extracted/`, 詳見 `docs/RESOURCES.md`)

主分支 `main:Extracted/` 有日版 ペーパーマン 的完整資源。已灌入 DB 或
已逆向、對私服最有價值的幾件:

| 資源 | 用途 |
|------|------|
| `ui/cfg/maplist.pat` | **123 張地圖** (id/日文名/檔名/模式 bitmask) → `db/map_catalog` |
| `ui/cfg/itemdata.pat` / `Quest.pat` / `weaponparts.pat` / `partsability.pat` | 物品/任務/改裝件目錄 → `db` 對應表 |
| `ui/lang/msgtableres.lang` | **全 client 唯一訊息表** (CP932, entry i=第 i+3 行) — ACK error code、預設房名(309..325)、895/112 status 的權威文字來源 |
| `ui/roommake.xml` / `gameroom.xml` | 建房/房內 UI 控制項 → 房設定簇 (121/122、167–178、340/341、364/365、712/713、990/991) 語意 |
| `ui/system/map_StartIndex.xml` | mode → 預設 map_id (0→106、1→104、2→14、3→107、4→23、8→51、9→89、12→98; ⚠ soccer 預設 98 實為 TeamSurvival 圖, 真正 soccer 圖是 99/100) |
| `ui/system/voice_customize_contents.xml` / `voice_customize_path.xml` | **語音自訂 791–796 資料源**: 15 角色 × 92 情境 (缺 87) × 27 句 (command/tactics/infomation × 9) + 語音檔路徑 `sound\soundsNN\<codename>` |
| `ui/slanderfilter/filterword.txt` + `exceptionword.txt` | 聊天/暱稱過濾詞 (GM/GS 變體 + 髒話) |
| `ui/system/Total_Package_Index.xml` / `SpecialWeaponType.xml` / `map_BGMSoundNames.xml` | 套裝索引 / 特殊武器型態 / 地圖 BGM |
| `ui/{bot,game,lobby,loadscreen}/` + `ui/sounds/` | DDS/TGA 圖集與音效 — 檔名可佐證各模式 UI (見下) |
| `data.pat` / `convars.pat` / `0.xml` / `ClientDataList.xml` | 資料容器/控制台變數/打包清單 |

### 模式枚舉 (權威, `sub_53FBB0` mode factory)

房 rule 的 mode 值 0..15, 對應 `CyGameModes::Cy*ModeLobbyUI` 類:

```
0 TeamMatch(TeamDeath)   1 IndividualSurvival(FreeForAll)
2 DefuseBomb(爆破)       3 TeamSurvival           4 Steal(スチール)
5 Practice               6 Tutorial               7 ChattingRoom
8 Pulp'n'Roll(PNR)       9 GunShooting           10 Occupy(占領)
11 AIMulti(PvE)         12 TeamSoccer(サッカー)  13 OccupyRenewal(new占領)
14 (無效)               15 WeaponTest            16 (哨兵=不改)
```

- `sub_438990` = 「是否兩隊制」→ mode∈{0,2,3,4,8,10,11,12,13};
  130/134 序列化的 `mode+12` 欄位即此旗標。
- 建房 UI (`roommake.xml`) 只開放 {0,1,2,3,4,5,8,10,11,12,13};
  6/7/9/15 走專用入口 (教學/聊天房/射擊館/武器試射)。
- 地圖過濾 bitmask: maplist 的 `modes` 欄以 `1<<bit` 標記該圖可玩的
  模式, bit↔mode 對照 (0→bit2, 1→bit0, 2→bit3, 3→bit1, 4→bit4,
  8→bit9, 9→bit10, 10→bit12, 11→bit13, 12→bit14, 13→bit15, 15→bit11)
  見 `docs/RESOURCES.md` §4b。

## C# 14 伺服器 (`server-cs/`)

依上述逆向成果重建的可運行伺服端 (net10.0, `LangVersion 14`):

- `PaperMan.Protocol` — 純協定層: `Opcode.cs` (670 opcodes, 由
  `tools/gen_opcodes.py` 從 `db/packets.tsv` 產生)、`Packet.cs` (讀寫原語)、
  `LoginWire.cs` (682/681/693/694 的具名 wire contract)、`PaperLz.cs` /
  `PaperAes.cs` / `PacketCodec.cs` (真實 LZ+AES 管線)。
- `PaperMan.Server` — TCP 伺服器: 9600B 框架 (`Session.cs`)、SQLite 存取層
  (`Db.cs`, 交易式購物/登入/暱稱/背包分頁)、login/channel 雙 listener、
  `ChannelAdmissionRegistry` 的 681→143 單次交接，以及封包 handlers
  (681/694、143/144、195/196、大廳、商店、送禮 296/297、戰隊隧道
  583/584、GP_CH*C 戰績 18 REQ/ACK 對 + 882 推播、房間
  111–194/340–367/712–728、語音 791–796、倉庫 855–863)。AES 原生金鑰已內建。
- `PaperMan.SelfTest` — 不需遊戲客戶端的 codec + login/channel wire layout 自測。
- LZ 演算法另以 Python 逐行移植跑過 310 組 round-trip/fuzz 驗證。

本沙箱無法安裝 .NET SDK (所有鏡像被網路封鎖), 原始碼未經編譯 —
建置/執行方式與 AES 金鑰抽取方法見 `server-cs/README.md`。

### 為何選 SQLite (2026-09 現況)

單行程私服 + WAL 模式 = 每秒數萬寫入輕鬆達標且無網路 round-trip;
`STRICT` 表 + `CHECK` 約束把逆向得到的值域直接壓進 schema;
一檔即全部狀態、零運維; `Microsoft.Data.Sqlite` 是 .NET 10 第一方支援。
詳細論證見 `server-cs/README.md` 末節。
