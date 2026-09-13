# PaperMan 客戶端資源檔案地圖 (十四輪逆向)

> 這份文件回答:「伺服器/工具還需要哪些客戶端資源檔案?」
> 以及每個檔案的容器格式 (全部由 PaperMan.exe.c 逐行讀出)。

## 1. 對私服最有價值的檔案 (若能提供, 可完善 DB 目錄)

| 檔案 | 讀取器 | 內容 | 對私服的價值 |
|---|---|---|---|
| **cfg\ItemData.pat** | @131262 | 物品目錄 (1808B/條) | ★★★ 填 item_catalog 的真實資料: id/名稱/kind/價格/期限/能力值/需求等級 |
| **cfg\Quest.pat** | @602219 | 任務目錄 | ★★★ 填 quest_catalog: 條件類型/目標值/獎勵 |
| **cfg\weaponparts.pat** | @619191 | 武器改裝件 | ★★ 220/221 編組 parts 驗證 |
| **cfg\maplist.pat** | (sub_717E50) | 地圖清單 | ★★ 111 建房 map id 驗證 |
| **cfg\partsability.pat** | | 改裝件能力 | ★ |
| **cfg\RecommandItem.pat** | @686031 | 推薦商品 (809) | ★ GS_GET_RECOMMENDSET_INFO 內容 |
| **data.pat** | @225227 | 主資料容器 | ★★ (見 §3 已破解格式) |
| **Data\pmClient.dat** | @415210 | pmFile 打包主檔 | ★★★ 上面所有 cfg\*.pat 都從這打包檔讀出 |
| FilterWord.dat / ExceptionWord.dat | | 聊天過濾詞 | ○ (伺服器可自備) |
| Map.dat / map\game*.dat | | 地圖幾何/材質 | ○ (僅戰鬥模擬需要) |
| ui/*.xml (CLAN.xml, SHOP.xml...) | | UI 佈局 | ○ (純客戶端) |

**✅ 已取得 (十五輪): 使用者提供 Extracted/ — itemdata.pat (21,164 條)、
Quest.pat (844 條)、maplist.pat (123 張圖) 已解密並灌入 DB
(db/import_pats.py)。此為日版 ペーパーマン 資料。**

## 2. cfg\ItemData.pat 條目格式 (載入器 @131262, 1808B/條)

容器: pmFile 讀出後 → `[s32 版本?][s32 count]` + count× 變長條目:
```
+0    s32  item_id          ← map 鍵 (sub_535020 查詢用), 武器段判定也用它
+4    s32  (id2/型號)
+8    s32  (類別參數)
+12   s32  (類別參數)
+528  s32  name_len         → +16 起 name[name_len] (變長!)
+532  u8, +533 u8
+534  3×u8                  (壓縮的旗標組)
+540  3×s32
+552.. (中略)
+564..+580  5×s32
+584..+604  6×s32 能力值陣列 (>0 者計數 → +1273 has_ability)
+608..+636  8×s32 能力值第二組
+640  u8, +641 u8
+644  s32  需求等級          ← sub_534FE0 getter; sub_A1CE20 等級 gate
+1208 u8, +1209 u8          (版本 0: 只有 1 個; 版本 1: 2 個 — 檔頭 s32 決定)
+1212..+1252  11×s32        (價格/期限相關區)
+1256 16B                   (4×s32 塊拷貝)
+1272 u8
```
- kind==5/6 的條目另掛消耗品容器 (sub_6C9360)
- item_id 在武器段 (15,304,001..15,305,000) 時解析名稱→ +1276 (sub_535680)

## 2b. cfg\Quest.pat 格式 (載入器 @602219 — 十四輪)

**解密後是 CSV (Shift-JIS 編碼, CRLF 行)** — 十五輪實測: 首行=總數,
第 2 行 = 47 欄標頭 (Index, QuestRepeat, QuestLevel, QuestName,
TermItem1-5, UseWeapon..., GameMode, MapNumber, PeriodType,
QuestTerm(=條件類型 sub_9252D0 cond!), QuestTermData(=目標值),
ClearItem1-3(獎勵), StartDate, EndDate, Hidden)。
「個人サバイバル Kill10」= QuestTerm 3 (cond3=kills)×10 —
與十二輪任務條件對照表互為第四證鏈。
(以下為載入器讀取順序的原始分析, 供參考):
```
行1: quest 總數
行2: (略過)
每條 12432B 記憶體結構, 逐行讀:
  ..., is_active(bool), quest_id(+8), (4 行略), 條件值(+4108),
  (1 行略), 目標值(+5136), 標題字串(+5140, mbstowcs),
  5×s32 (+6164..), 5×s32 (+6184..), ...
```
→ 若拿到 Quest.pat, 先用 pmfile.py pat-decrypt 解密, 得到可讀文字,
再照上面欄位順序灌 quest_catalog (quest_id 編碼 = 類別×10000+序號)。

## 2c. itemdata.pat 尾部 721B 完整切段 (十六輪, 21,164 條統計+錨點定位)

```
tail[0]     u8  b532 (子類)
tail[1]     u8  kind (b533 主分類: 9=武器 10914 條, 2=裝飾...)
tail[2..4]  3×u8 (534..536; 536==5/6 → 消耗品容器)
tail[5..16] 3×s32 (540 區: 參數 — 15304001 的 100 在 [9])
tail[17..36] 5×s32 (564 區: 參數 — 修理% 的量在 [21])
tail[37..60] 6×s32 (584 區: 參數 — PG 面額 600 在 [49] u16)
tail[61..92] 8×s32 (608 區)
tail[93,94] b640,b641
tail[95..98] s32 req_level (644)
tail[99,100] b1208,b1209 (ver==1 讀 2B)
tail[101..114] 價格承載區 (→ 記憶體 1212..1224 檔位0 组) —
    ⚠ 日版資料全 0 (21,141/21,164)! 僅 23 條武器 (INGRAM Silencer 等)
    的 [107] 有 5/25/30 (參數非價格)
    → **價格由伺服器下發** — 這解釋了 358 GS_PRICE_REQ 的存在:
    client 把商店價格快取上報驗證, 實價一律以 205/209 ACK 為準!
tail[105]   u8 =1 (幾乎恆定, 有效旗標)
tail[112]   u8 =1 (恆定)
tail[115..626] 第二個 UTF-16 字串 (256×2B): 顯示名/說明
tail[627..661] 稀疏參數
tail[662]   u8 上/停架旗標 (71%=1)
tail[664..703] 8 個稀疏 s32 (各僅 12 條非零 — 活動物品參數)
tail[705..713] ASCII 日期[8] + \r: 上架日 "20090210"
tail[714..720] padding
```
sub_5359B0 (getter) 證實記憶體 1212..1224/1228..1240 = 兩檔位 4×s32
價格組, +1248/+1252 = 檔位貨幣 id — 結構存在但日版資料未填。

## 2d. itemdata 頭部 16B 定案 (十七輪)
```
+0  s32 item_id
+4  s32 t4  = 稀有連動物品 id (153 條非零)
+8  s32 t8  = 基底物品參照 — 變體→原型 (7,155 條: '赤組帽子' 變體
             → 同名原型; ヘアパズル → 基底髮型 11012201...)
+12 s32 t12 = 改裝件掛載對象 (1,291 條非零)
```

## 3. data.pat 容器格式 (@225227 — 十七輪實測修正)

```
0. ⚠ 檔案本身先過 pmFile 加密 (十七輪實測: 載入器經 pmFile 讀取)
   → 先 pmfile_decrypt 再進下面流程
[u32 size_1 (混淆)] [body...]
1. size_1 = ROL32(讀入的前4B, 9) ^ 0x975E   → 解壓後大小
2. body 逐 byte 解混淆: plain[k] = i ^ ROL8(cipher[k], 3), i 從 len..1
3. zlib 1.2.3 uncompress → 得 size_1 bytes (實測 23,769,904B 精確吻合)
4. 尾 4B = CRC (客戶端自帶表 dword_AFBF28, 與標準 crc32 不同 — 
   實測不匹配, 解包工具改為警告)
內容 (實測): 路徑快取表快照 — [s32 群組數=9][s32 count=15][s32 容量=3007]
+ 3007×128B UTF-16 名稱槽 (只填 15 個 hand*.tga 噴漆/手勢貼圖,
87% 為 0xCD 未初始化填充) → **對私服無用**
```

## 4. pmClient.dat (pmFile 打包系統)

- `pmFile::ctor(path, mode)` → sub_A5BFC0 開檔;
  `ctor_39(name)` = 讀單一子檔進記憶體
- 子檔內容經 **per-byte 滾動 XOR + 位移** 解密 (sub_7117D0):
  ```
  for i = size-1 .. 0:
      out[i] = ROL8(in[i], i) ^ state
      state = ((state ^ 0xFA5387AD) & 0x0F3A94AA)
              ^ ((i | state) + 0x48945DCA) ^ 0x1A68DCCF
  state 初值 = size
  ```
  (加密端 sub_711720 = 先 XOR 再 ROR — 互逆已對照)
- cfg\*.pat 全部走這條路徑 (sub_717E50 組路徑 → pmFile 讀取)

## 4b. cfg\maplist.pat 格式 (十五輪以真實檔案實測修正!)

二進位 (pmFile 解密後): `[4B 版本/f32][s32 count=123]` + count×836B:
```
+0    s32  模式 bitmask   (⚠ 實測: 大量重複 → 是模式不是 id)
+4    s32  map_id         (唯一鍵, 0..122 連續)
+8    128B 檔名 UTF-16    (maps\\*.pmm — ⚠ 與載入器推測對調)
+136  128B 顯示名 UTF-16  (日文: 古城/スタジアム/池袋...)
+264  128B 縮圖
+392/+520  128B×2 說明
+816  f32 ×2 (1.0f)  [+824.. 版本條件]
```
→ 111 GL_MAKEROOM_REQ 的 map id 驗證即對照這張表 (123 張圖已入
map_catalog)。
**模式 bitmask 解碼 (十七輪, 檔名前綴互證)**:
bit0=PS(個人戰) bit1=TS(團隊戰) bit2=TD(爆破) bit3=TH(奪寶?)
bit4=TW(佔領戰) bit5/6=TU(教學) bit9=PNR bit10=AI(協力)
bit11=ECT(武器試射) bit12=OCC(基地建設) bit13=PVE bit14=TS世界盃
bit15=OCC2 — Quest.pat 的 GameMode 欄 (1..10) ≈ bit 位 +1,
建房時 client 以 bitmask 過濾可選地圖。

## 5. .pat 文字/二進位雙軌 (convars)

`sub_712D40("convars.txt", "convars.pat")`: 有 .txt 用 .txt (開發模式),
否則用加密的 .pat — 表示 .pat 內容就是對應 .txt 的加密版。
FilterWord / ExceptionWord / soundprops 同模式。

## 5a2. 武器/改裝件段全圖 (十九輪定案)

**武器 4 槽 (12.xM 段 — 武器編組 sub_524660 用)**:
12.1M=主武器(MP5/M3/L96) 12.2M=副武器(DE/GLOCK) 12.3M=近戰(CUTTER)
12.4M=投擲(GRENADE) — 對應編組條目的 4 種武器位。
**15.xM 服務/衍生段**: 15.2M=PG點數包, 15.300M=服務(修理/改名),
15.301M=福袋, **15.304M=稱號** (キリ番ゲッター — 十二輪誤標「房間
武器顯示段」已更正), 15.4M=戰鬥語音 (ハヤテ戦闘(基本)...)。
**改裝件 8 組 (weaponparts.pat 81 欄完整版, 10,648 條 100% 目錄命中)**:
```
grp0 = 15.21M バレル (槍管)     grp1 = 15.22M トリガ (扳機)
grp2 = 15.23M フロントサイト    grp3 = 15.24M グリップ (握把)
grp4 = 15.25M ストック (槍托)   grp5 = 15.26M ドットサイト (紅點)
grp6 = 15.278M ペイント弾 (彈藥皮膚, 5,089 條)
grp7 = 15.288M 整槍配色 (L96 A1 Color...)
```
→ 220/221 武器編組的 8×u32 parts 欄位即此 8 組!
(RecommandItem 頭兩行: 1030=資料行數, 20=概念類別數)

## 5b. 十六輪補充實測

- **pm_lobbydata.dat = 明文** (LOBBYMAIN/L_MR.DDS + UI 座標表) — 非加密
- **0.xml** = 打包清單 (character.dat/item.dat/map.dat/pepachi.dat 對映)
- **ClientDataList.xml** = 頂層資料清單
- kind 語意與封包白名單互證: kind 13 = ヘアパズル (髮型拼圖,
  11,012,xxx 段) → 正是 204/296 變體尾欄的 kind 12/13/17 「可覆寫」類;
  kind 9 = 武器 (永久型白名單) ✓ sub_570B00 互證
- 日版無 kind 17 條目 (空集), kind 12 = 服務類 (シャウトチャット等)

## 5c. 角色表 (十七輪終驗)
19,900,001..19,900,015 = 15 位角色: ハヤテ/ティナ/ミリィ/サイラス/
ドッドン/ガイ/テリシア/アルル/ヴァン/フッド/リカ/レム/エリス/
ルコット/ルーシー。推薦套裝 char_type 1..14 與角色段序號一一對應
(198 wire 的 u8 char_type 即此編號) — 第六次互證。

## 5d. system XML 資料表 (二十輪全掃)

| 檔案 | 內容 | 對應 opcode |
|---|---|---|
| map_StartIndex.xml | **官方模式名表** (modeIndex↔modeName) | 111 rule |
| ItemAbilityLevTable.xml | 能力 lev 效果換算 (pmFile 加密!) | 205 f32 能力值 |
| AI/AiMultiCompensation.xml | AI 協力模式過關獎勵 (難度×等級→物品) | AI 模式結算 |
| AI/gamecenter_map_info.xml | 射擊館關卡 (盾 HP/Fever/砲位) | 479 GAMECENTER |
| AI/BotWave/BotEnemy/Scenario | AI 波次/敵人/劇本 (easy/intelligent) | AI 對戰 |
| Total_Package_Index.xml | 角色套裝 UI 索引 (114 套) | 商店套裝頁 |
| URLList_01.xml | 正版端點 (dl.paperman.jp, bill.paperman.jp, hangame.co.jp) | 網頁跳轉 |
| TimeLimit_NotUse_IP.xml | 防沉迷白名單 IP | — |
| netcafe_contents.xml | 網咖特典 (UTF-16) | PopUpNetCafeShop |
| voice_customize_contents.xml | 語音自訂 (UTF-16) | 695 kind 語音 |
| CharacterFitting.xml | 試衣間預設 (hand*.tga ← data.pat 快取!) | — |
| face_contents.xml | 臉型清單 | 角色創建 |

(CharacterFitting 引用 hand12.tga — 與 data.pat 快取表的 15 個
hand*.tga 互證: 那是「試衣間手部貼圖」快取)

## 6. 其他已知資源

- `system/map_StartIndex.xml`, `SelectRandomMap.xml`: 地圖選擇
- `ui/system/AI/*.xml`: AI 模式劇本 (BotWave/BotPath/Scenario)
- `Options.cfg`, `CustomMap.cfg`, `LastConnect.ini`: 本機設定 (非資源)
- `TNMT_*.xml`: 錦標賽 UI 資料
- `occupymode.xml` / `occupyrenewalmode.xml`: 佔領模式參數
