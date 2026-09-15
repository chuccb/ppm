# PaperMan 私服重建 — 協議逆向 + SQLite 伺服器 DB

從 `PaperMan.exe.c` (Hex-Rays 9.4 IDA 導出, 81 萬行) 逆向出完整封包協議，
並據此重建伺服器端 SQLite 資料庫。

> **IDA 導出物版本說明（2026-09）。** `main` 分支上傳了一份新的 `PaperMan.exe.c`。
> 已逐函數比對過：兩份的**函數集合完全相同**（17,830 個具名函數、19,835 個
> function body，無新增亦無遺失），差異只在 forward-declaration 區塊與
> Hex-Rays 的變數命名；新版少了約 26,000 行純宣告，另有約 247 行是重新命名後
> 的字串常數（如 `PINFO_EMBLEM_%d`、`ITEM_PLAY_BUTTON_0%d`）。
> **結論：兩份在反編譯內容上等價，既有以行號/函數名為錨點的分析全部仍然成立。**
>
> Release 附的 `PaperMan.exe.lst`（1.92 GB，IDA 反組譯清單）在本工作環境**無法取得** —
> GitHub release asset 會轉址到 `release-assets.githubusercontent.com`，該網域在此沙箱
> 被封鎖（`gh api` 走 API 網域可用，但資產下載網域不可用）。因此本輪的所有交叉驗證
> 均以 `PaperMan.exe.c` 與 `Extracted/` 為準；LST 專屬的資訊（精確指令、段位址、
> 完整 xref 圖）**尚未納入**，待可下載時再補。

## 目錄

| 路徑 | 內容 |
|------|------|
| [`docs/README.md`](docs/README.md) | **文件與證據導覽**：先判斷該讀哪份資料、證據等級、generated boundary 與每次改動的最小交叉驗證流程 |
| [`docs/RTTI_PYCLASSINFORMER.md`](docs/RTTI_PYCLASSINFORMER.md) | **Client RTTI 索引**：使用者提供的 PyClassInformer class / vftable / inheritance tranche；用於 native xref 定位，明確不等同 server policy 或 wire evidence |
| `docs/PACKETS.md` | **協議完整分析**: Packet 類佈局、wire 格式、序列化原語、checksum/壓縮/加密層、關鍵 payload 結構 (全部附反編譯函數地址) |
| `docs/RESOURCES.md` | **客戶端資源地圖**: maplist/物品/任務/訊息表 `msgtableres.lang`、UI 圖像音效盤點、mode 枚舉正名 |
| `docs/WIKI_MECHANICS.md` | **Wiki* 歷史機制研究帳本**: 已閱讀主題、版本風險與待由 client/resource/packet 交叉驗證的矩陣；明確不是 service/wire 權威 |
| `docs/LAYOUTS.md` / `docs/LAYOUTS_REQ.md` | 各封包 dispatcher 讀取序 / REQ builder 寫入序 (欄位級對照) |
| `docs/ARCHITECTURE.md` | 全景架構: 生命週期、資料層、加密、互證鏈 |
| `docs/TODO_HANDLERS.md` | 尚未實作的 server handler 清單與下一輪建議 |
| `db/packets.tsv` | 從 `sub_9D2050` 抽出的 **676 筆 opcode ↔ 名稱** 對照表 (100–994) |
| `db/schema.sql` | SQLite schema (33 個 STRICT tables、3 views、4 triggers；C# migration 另補 legacy guards), 每個欄位註明來源封包/函數 |
| `db/build_db.py` | **可選**離線重建／檢查工具；C# server 首次啟動會自行建庫，不必先跑它 |
| `db/smoke_test.py` | 模擬 登入→建角→購物→背包分頁→開房→結算→好友/訊息/任務/公會 全流程的 DB 讀寫測試 |
| `db/paperman.db` | 開發模式的預設 SQLite 資料庫（不存在時由 C# server 自動建立） |
| `server/packet.py` | wire 協議 Packet 參考實作 (Python, 逐函數對應反編譯), 含自測 |
| `server-cs/tools/dump_itemdata.py` | 解出 `Extracted/ui/cfg/itemdata.pat` 的 21,164 筆 item id ↔ 名稱 (stride 997B 自證); 支援 `--name` / `--id` / `--band` 查詢 |
| `server-cs/` | **C# 14 / .NET 10 伺服器** (協定層 + login/channel TCP + source-proven UDP-private 19→20 control + SQLite + 自測), 見 `server-cs/README.md` |

## 快速開始

```bash
# 只要 .NET 10 SDK；不需要 DB 建置命令或任何啟動參數。
dotnet run --project server-cs/src/PaperMan.Server

# 下列是可選的離線工具：
python3 db/build_db.py --fresh   # 明確重建 DB
python3 db/smoke_test.py         # 跑 DB 全流程測試
python3 server/packet.py         # Packet 編解碼自測
python3 db/import_pats.py        # 資源目錄灌 DB (需先以 server/pmfile.py 解密 cfg/*.pat)
```

## 逆向重點摘要

### Wire 格式 (Packet 類 @ 0x591AC0, `sub_591DA0` 初始化)

```
[u16 payload_size][u16 opcode][u16 w2][u16 w3=orig_size][payload ≤9592B]
```
- 傳送 `sub_555090` → `sub_593280`: w3=原始大小 → (w0≥門檻時) 自製 LZ
  壓縮 (`sub_591600`, **w3** 保留壓前大小) → **一律** AES-128-CFB-128
  加密（IV=0；`sub_4042A0`，補齊 16，**w2**=加密前大小） →
  `WSASend(this+24, size+8)`
- 接收 `sub_5930C0`: AES 解密 (驗 16 對齊 + `w0==align16(w2)`；**w2=0
  仍需解出一個 16-byte AES block**) → (w3≥門檻且 w0<w3 時) LZ 解壓
  (`sub_591900`, 結果須==w3), 壞包整緩衝丟棄
- ⚠️ popcount checksum + XOR「seal」層 (`sub_5923D0/sub_592420`) 為
  **死碼** (無呼叫者), 二次深挖後已自管線剔除 — 詳見 `docs/PACKETS.md` §1.4
- 壓縮門檻由 `GL_ACCOUNTCONNSUCC(694)` 的 u16 協商：client **只接受 <0x2580**，
  預設/規範化值 0x2580(9600)=永不壓縮；server 拒絕更大值以避免 LZ 協商失配
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
| 角色槽 ≤20, 12 個 normal-appearance 位 | `sub_524010` | `characters` |
| 武器 loadout 固定 4 組，非空 primary 有 8 parts | `sub_524660` / `sub_4C7C00` | `weapon_groups` + bootstrap trigger |
| 9 個 UI-item + 已選 NewSkill profile 的 7 puzzle IDs | `sub_522480` / `sub_527AF0` (0x1C) | `skill_slots` + `new_skill_profiles` |
| 戰績 19 計數器 | GP_CH*C 家族 | `user_stats` |
| 房間 ≤210, 10 槽 | `sub_568CE0` | `rooms/room_slots` CHECK |
| 期限白名單 1/7/15/30/60/90 天 | `sub_570B00` | `inventory.period_days CHECK` |
| 訊息內文 ≤200 字 | `sub_55A630` (buf 201) | `messages.body CHECK` |

`protocol_packets` 表載入了全部 676 筆 opcode, 伺服器可直接拿來做
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

房 `modeIndex` 值 0..15, 對應 `CyGameModes::Cy*ModeLobbyUI` 類:

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

- `PaperMan.Protocol` — 純協定層: `Opcode.cs` (676 opcodes, 由
  `tools/gen_opcodes.py` 從 `db/packets.tsv` 產生)、`Packet.cs` (讀寫原語)、
  `Contracts/Login/LoginWire.*.cs` (682/681/693/694) 與
  `Contracts/Channel/ChannelBootstrapWire.*.cs` (142/144/196 + packed calendar)
  的 opcode-family split、具名 wire contract、`PaperLz.cs` / `PaperAes.cs` /
  `PacketCodec.cs` (真實 LZ+AES 管線)。
- `PaperMan.Server` — TCP 伺服器: 9600B 框架 (`Session.cs`)、SQLite 存取層
  (`Db.cs` + embedded `DatabaseBootstrapper.cs`: 自動建庫、schema migration、opcode/
  運維預設資料 seed；登入/暱稱/背包分頁)、login/channel 雙 listener、
  `ChannelAdmissionRegistry` 的 681→143 單次交接，以及封包 handlers
  (681/694、143/144、195/196、大廳（含使用者許可的空 252→253）、商店/送禮的
  fail-closed wire 邊界與 zero-record 806→807、`GC_CLAN_PROTOCOL` 583/584 container、GP_CH*C 戰績
  18 REQ/ACK 對 + 882 推播、房間
  111–194/340–367/712–728、語音 791–796、倉庫 855–863)。AES 原生金鑰已內建。
- `PaperMan.SelfTest` — 不需遊戲客戶端的 codec、login/channel wire layout、SQLite first-run bootstrap、credential upgrade/migration 自測。
- LZ 演算法另以 Python 逐行移植跑過 310 組 round-trip/fuzz 驗證。

本沙箱無法安裝 .NET SDK (所有鏡像被網路封鎖), 原始碼未經編譯 —
建置/執行方式與 AES 金鑰抽取方法見 `server-cs/README.md`。

### 為何選 SQLite (2026-09 現況)

單行程私服 + WAL 模式 = 每秒數萬寫入輕鬆達標且無網路 round-trip;
`STRICT` 表 + `CHECK` 約束把逆向得到的值域直接壓進 schema;
一檔即全部狀態、零運維。server 將 schema.sql 與 packets.tsv 編入 assembly，首次
啟動自動建立父目錄、schema、opcode catalog 與非破壞性的預設運維設定；
`Microsoft.Data.Sqlite` 是 .NET 10 第一方支援。
詳細論證見 `server-cs/README.md` 末節。
