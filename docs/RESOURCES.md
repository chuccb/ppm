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
| **cfg\partsability.pat** | | 改裝件**效果差分** (31 欄彈道模型) | ★★ |
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
**模式 bitmask 解碼 (卌六輪修正 — sub_53FBB0 枚舉 + map_StartIndex
modeName + maplist 檔名前綴三方互證; 舊表 bit1/2/3/4 的語意已更正)**:

| bit | 檔名前綴 | mode | modeName (map_StartIndex) | CyGameModes:: 類 | 日文語意 |
|---|---|---|---|---|---|
| 0 | PS | 1 | FreeForAll | CyIndividualSurvivalMode | 個人サバイバル |
| 1 | TS | 3 | TeamSurvival | CyTeamSurvivalMode | チームサバイバル |
| 2 | TD | 0 | TeamDeath | CyTeamMatchMode | チーム戦 |
| 3 | TH | 2 | TeamHacking | CyDefuseBombMode | 爆破ミッション |
| 4 | TW | 4 | TeamSteal | CyStealMode | スチール |
| 5/6 | TU | 6 | — | CyTutorialMode | チュートリアル |
| 9 | PNR | 8 | PNR | CyPulpnRollMode | パルプ&ロール |
| 10 | AI | 9 | GunShooting | CyGunShootingMode | ガンシューティング |
| 11 | ECT | 15 | — | CyWeaponTestMode | 武器試射 |
| 12 | OCC | 10 | — | CyOccupyMode | 占領 |
| 13 | PVE | 11 | — | CyAIMultiMode | AI 協力 (PvE) |
| 14 | TS(worldcup) | 12 | SOCCER | CyTeamSoccerMode | サッカー |
| 15 | OCC2 | 13 | — | CyOccupyRenewalMode | new占領 |

(mode 5 Practice / 7 ChattingRoom 無專屬地圖; bit 7/8 未用。)
建房時 client 以 `(map.modes >> bit) & 1` 過濾該模式可選地圖;
`mode` 是 room 的模式值 (0..13/15, 見 §7), `bit` 是上表對應的
maplist `modes` 位 — 兩者**不是同一編號** (如 mode 0=TeamDeath↔bit2)。

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
partsability.pat (413 條) 的 id 段 15.21M..15.28M **正好覆蓋全部 8 組**
(49/37/37/58/58/19/20/135 條) — 它是「改裝件效果差分表」: 每件對
recoil/range/damage/shot_delay/姿勢精度 (miJump/miSit/miStand/miWalk/
miRun) 等 31 項彈道參數的修正 — weaponparts↔partsability 第九次互證。
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
| voice_customize_contents.xml | 語音自訂 (UTF-16LE; 15 角色×92 情境×27 句) | 791–796 voice_slots |
| CharacterFitting.xml | 試衣間預設 (hand*.tga ← data.pat 快取!) | — |
| face_contents.xml | 臉型清單 | 角色創建 |

(CharacterFitting 引用 hand12.tga — 與 data.pat 快取表的 15 個
hand*.tga 互證: 那是「試衣間手部貼圖」快取)

**語音自訂兩 XML (卌七輪全解 — 791..796 資料源)**:
- `voice_customize_contents.xml` (UTF-16LE BOM, root `vcustomizelistTable`):
  15 角色區塊 `<hayate index="2">`…`<devilgirl index="2">`, 每區塊
  `<Voice00>`..`<Voice92>`(缺 87) 共 **92 情境**; 每情境 27 句 speech =
  `command_1..9` / `tactics_1..9` / `infomation_1..9` (client 拼字如此)。
  全檔 40052 tags。語音表的 voice_item 偏移對到 (情境, 類別, 句序)。
- `voice_customize_path.xml` (UTF-8, root `vcustomizepathTable`): 91 個
  `<soundsNN>` 區塊 (sounds..sounds92, 缺 87) — first_path
  `sound\soundsNN` + 15 個 `sub_path_<角色>` (cacao/dallae/hana/lich/
  loki/maru/momo/nari/pero/robotgirl_12/spy_11/wooka/tsunderegirl/
  magicgirl/devilgirl) = 語音檔路徑。
- 15 角色 codename 對照 (sub_8859B0 switch 0..14 與 sub_path 一一對應):
  0 maru 1 nari 2 dallae 3 lich 4 cacao 5 loki 6 hana 7 momo 8 wooka
  9 pero 10 spy_11 11 robotgirl_12 12 tsunderegirl 13 magicgirl 14 devilgirl
  — 等同 ICT_* 角色 (NORMAL_BOY…DEVILGIRL), character/models/type1..15
  = idx+1。base_voice 兩枚 s16 即選 sounds index。

## 5e. 版本考古 (廿一輪)
- 根 datarevision.txt = 811034967 (patch 版本號)
- map/maplist.dat = **舊版明文** (head f32 v1.02, 67 圖, 832B/條,
  無 +824/+828 版本條件欄) vs ui/cfg/maplist.pat = 新版加密
  (123 圖, 836B/條) — 載入器的 [+824 版本條件] 即此演進痕跡;
  **.pat 為權威來源**
- map/game*.dat (material/object/sfx/shader) = 地圖渲染資源索引
  (戰鬥模擬才需要, 私服可略)

## 5f. 收尾細項 (廿一輪終)
- convars 的 m_cDmg2MultiplyAvataAbility = 第二套角色參數 (2x 傷害
  模式), 值與基本組相同 — 模式共用參數
- RecommandItem Concept: 1=男性向 (531) / 2=女性向 (485) / 20=特殊
  (14) — 頭行第二值 "20" 即最大 concept 編號
- ItemAbilityNameTAble_JP: 能力顯示名 (速度系/機動系/鎮壓系...)
- 全資料最終計數: **7 表 37,044 條**, 全部測試綠

## 6. 其他已知資源

- `system/map_StartIndex.xml`, `SelectRandomMap.xml`: 地圖選擇
- `ui/system/AI/*.xml`: AI 模式劇本 (BotWave/BotPath/Scenario)
- `Options.cfg`, `CustomMap.cfg`, `LastConnect.ini`: 本機設定 (非資源)
- `TNMT_*.xml`: 錦標賽 UI 資料
- `occupymode.xml` / `occupyrenewalmode.xml`: 佔領模式參數

## 7. 房/模式 UI 資源互證 (四十二輪 — ui/*.xml 對照房設定簇)

本輪逐檔比對 `main` 分支 `Extracted/ui/*.xml`, 把房設定簇 (121/122、
167–178、340/341、364/365、712/713、990/991) 的語意釘死到控制項:

**gameroom.xml** (房內 UI) 控制項 → 協定對照:
| 控制項 | 語意 | 協定 |
|---|---|---|
| GAMEROOM_SCROLL_MAP | 地圖選擇 | 121/122 (u8 map_id → room+130) |
| GAMEROOM_SCROLL_RULE | 模式選擇 | 169/170 (u8 mode) |
| GAMEROOM_SCROLL_TIME | 遊戲時間 | 173/174 (u8 → room+136) |
| GAMEROOM_SCROLL_OBJECT | 擊殺/目標數 | 340/341 (u16 → room+148) |
| GAMEROOM_ITEM (checkbox) | 道具開關 | 175/176 (bit0/bit1 → mode+4/+8) |
| GAMEROOM_DAMAGEROOM (checkbox) | 雙倍傷害 | 990/991 (u8 → room+128) |
| GAMEROOM_TEAMBALANCE (checkbox) | 隊伍平衡 | 364/365 (僅切 UI) |
| GAMEROOM_TEAMSHUFFLE (checkbox) | 隊伍打散 | 368/369 (u8 → mode+13)、894/895 (u8 room_no + u8 map; ACK 逐槽重排) |
| GAMEROOM_NORMAL_NOSKILL / CLAN_NOSKILL | 無技背景 | 712/713 (u8 → room+185) |
| GAMEROOM_LOCALROOM (checkbox) | 區域限定房 | 366/367 (u8 → 房內旗標) |
| GAMEROOM_SOCCER (checkbox) | 足球模式開關 | 969/970 (u8 → mode rule +14) |
| CUSTOMMAP_ON_BTN | 自訂地圖 | SelectRandomMap 點陣 |

**Popup_Room_Identity.xml** (大廳房單滑鼠懸停的身份卡) 以另一組字串把同簇
房旗再現: PRIVATE(密碼)、LOCAL/LOCALECHECK(區域房 366/367)、
ITEMCHECK(175/176)、TEAMBALANCECHECK(364/365)、SHUFFLECHECK(368/369)、
DOUBLEDAMAGE(990/991)、NOSKILL_CHECK(712/713)、AI_CHECK(mode 11)、
KNIFECHECK(近戰限定房)、ALLMODE/LOCALMODE_SECRET(顯示用) —
與 gameroom.xml 控制項互為第二證鏈。

**roommake.xml** (建房 UI) 下拉值域:
- GAMEMODE 可選 mode = **{0,1,2,3,4,5,8,10,11,12,13}** (無 6/7/9/14/15 —
  教學/聊天/射擊館/足球/武器試射不走一般建房; 7 由 CHKBTN_CHATROOM 勾出)
- USERS 可選人數 = **{2,4,6,8,10,12,14,16}**
- CHKBTN_CHATROOM (勾選=聊天房 mode 7) / CHKBTN_NOSKILL (無技背景)
- ROOMNAME textlimit=120, PASSWORD_INPUT textlimit=8

**SelectRandomMap.xml** (自訂隨機圖): CUSTOM_MAP_LIST 8 列, 點陣列
`MAP_NUM_0..7` 的 x 值 = **1,2,4,8,16,512,4096,16384** — 即 16-bit
自訂地圖 bitmask 的 bit {0,1,2,3,4,9,12,14} (對齊 maplist.pat +0 的
模式 bitmask 位定義)。

**map_StartIndex.xml** (官方模式名 + 預設圖): ⚠ 有兩份且值不同 —
client 實際載入的是 `system/map_StartIndex.xml` (sub_717E50 路徑),
`ui/` 根目錄那份是舊版 (0→5/1→1/3→15 已廢)。權威表
`modeIndex→modeStartIndex` = 0→106(TeamDeath) 1→104(FreeForAll)
2→14(TeamHacking) 3→107(TeamSurvival) 4→23(TeamSteal) 8→51(PNR)
9→89(GunShooting) 12→98(SOCCER)。modeStartIndex 即該模式預設 map_id
(maplist 0..122) — 169/170 改模式時 client `sub_426930(mode)` 回推
此值寫 room+130, server 已鏡像 (RoomHandlers.ModeDefaultMap)。

**mode 枚舉正名 (sub_53FBB0 factory)**: 0=TeamMatch 1=IndividualSurvival
2=DefuseBomb(駭入) 3=TeamSurvival 4=Steal 5=Practice 6=Tutorial
7=ChattingRoom 8=Pulp'n'Roll 9=GunShooting 10=Occupy 11=AIMulti
12=TeamSoccer 13=OccupyRenewal 15=WeaponTest — 14 無 (default→null),
16=「不改」哨兵 (預設 ctor 用)。

**mode rule 物件 (+132, 16B 含 vtable) 欄位總圖** — 房設定簇落地處:
`+4=item bit0 (sub_74F450, 175/176)`、`+8=item bit1 (sub_74F430)`、
`+12=rule param (wire 直寫; 112 本地初始化以 sub_438990 隊伍房寫 1/0)`、
`+13=隊打散 (368/369)`、`+14=足球 (sub_74F4D0 寫/sub_74F4B0 讀, 969/970)`。
`sub_438990` = 「是否兩隊制」: mode∈{0,2,3,4,8,10,11,12,13} → true。

## 8. msgtableres.lang — 訊息表 (卌三輪解碼, ACK error code 權威來源)

`main:Extracted/ui/lang/msgtableres.lang` 是全 client 唯一訊息檔
(53,021 B, CyLangPackTool generated include file, 2014/11/26 15.54.51)。
**格式** (⚠ 別再誤判為 NUL 分隔 — 實為 CP932 + LF):

```
line 0   註解 (工具輸出頭)
line 1   註解 (時間戳)
line 2   空行
line i+3 = entry i   (0-based; 1346 條, 末條 "TEXT_END" 哨兵)
```

解碼: 讀檔為 bytes → `decode('cp932')` → `split('\n')` → entry i =
`lines[i+3]`。格式碼如 `%d`/`%s`/`\\n`(換行)/`\\r\\n`。**entry id 即
sub_408080(table, id) 的第二參數** (每條 28B, 回傳 UTF-16 字串;
sub_408140/684190 載入本檔) — 全 exe 共 851 個唯一「字面常數」id
可對照 (變數傳入的 id 未計入)。

**對房/大廳簇最有用的碼** (server 寫 ACK/告警時照此語意選碼):

| id | 文字 | 出處 |
|---|---|---|
| 0x4B5 | チームシャッフルに失敗しました | 895 status 3/4/9/11 |
| 0x4B6 | チームシャッフルは3人以上のプレイヤーが必要です | 895 status 6 |
| 0x4B7 | 支援しないモードです | 895 status 8/15/16 |
| 0x178 | 権限がないため、このメニューは利用できません | 895 status 7 |
| 0x87 | 全員がレディー状態になってからスタート可能です | 895 status 5/10 |
| 0x2CE | 移動する事が出来ません | 895 status 12 |
| 0x111 | ２つのチームに分かれてください | 895 status 13 |
| 0xD9 | 定員オーバーです | 895 status 14; 進房滿 |
| 0x70 | データベース接続に障害が発生しました… | 198/130 連線失敗 |
| 0xCC | 使用可能なキャラクターがいません… | 198 無角色 |
| 0x66 | データ更新中 | 199 |
| 0x1B | %sさんがゲームから退場しました | 563430 退場播報 |
| 0x538 | 該当モードにランダムに設定されているマップが存在しません | 22243 隨機圖缺 |
| 0x145 | ルームが存在しません | 進房失敗 |
| 0x146 | ルーム入室失敗 | 進房失敗 |
| 0x90 | ゲームルームに接続しています…防火牆提示 | 進房等待 |
| 0xDA | 定員オーバーのため…チャンネル接続不可 | 頻道滿 |
| 0x107 | キャラクターはプレゼントできません | 送禮 |

**108 房名預設表 = entry 309..325** (sub_568CE0 `state+309` 查表 —
state 0..16 即「預設房名」, state<0 才送自訂房名 inline):

```
309 私達はペラペラだ！         310 日々の努力が実力になる
311 神聖な紙の王国            312 俺が最強！君も最強！
313 君だけいればいい！        314 ひらひら舞うぜ！
315 蜂の巣にしてやるぜ！      316 バンバン撃ち抜こう！
317 お手柔らかにお願いします  318 躊躇しないでヘッドショット
319 空中に浮かせてエアコンボ  320 熱き炎で燃やしてやるぜ！
321 日々の訓練が上達のコツ    322 サクサク始めようぜ
323 とにかく撃ちまくってやるぜ！ 324 スコープ覗いて隠れて狙撃
325 敵の背後をとることが重要
```

其餘高價值區段: 88=暱稱長度規則、98=「%03d番ルームに招待しました」、
144=進房等待提示、195-203=稱號語錄、271/272=チーム戦術/サバイバル、
280=爆破ミッション、326=スチールモード、356/357=アイテム/一般モード、
1221=占領モード、1240=PVE、1263=サッカーモード、1338=new占領。
(全 851 個 code 的文字對照可用本表離線重現, 不落盤。)

**server 側取用方式**: 訊息文字留在 client (server 只送 code, 不送
文字) — 895 的 status、112 的 err 碼都只是一個 u8, client 自行查表。
server 選錯 code 只會顯示錯誤文字, 不會當機。

## 9. UI 圖像/音效資產盤點 (卌六輪 — 圖片檔佐證各模式/區塊)

`main:Extracted/ui/` 下 4 個圖像資料夾 + 1 個音效資料夾, 全部是
DDS (DXT 壓縮) / TGA / JPG 貼圖, 主流 1024×1024 (少數 512×512,
Room_Identity 1024×512), 為 UI **紋理圖集 (atlas)** — 各控制項以
XML 的 l/t/r/b 像素矩形切圖, 不逐一列出。用途依檔名即可判讀,
以下只列對協議/模式有佐證價值的:

**ui/game/** (87 張 — 遊戲中 HUD/模式 UI):
- `main.dds` / `main02.dds` 遊戲主 HUD; `result.dds` / `result2.dds`
  結算畫面 (對 134 GR_END_ACK); `observer_01.dds` 觀戰 UI;
  `message.dds` / `message02.dds` 局內訊息; `kill_image.dds` 擊殺圖示;
  `level_exp.dds` (512) 經驗條; `ui_radar.dds` / `mapicon.dds` (512)
  雷達與地圖圖示; `crosshair.dds` 準星; `recStatus.dds` 錄影中。
- **模式專屬 UI** (與 §7 mode 枚舉互證): `soccer.dds`(12 足球)、
  `pulpnroll.dds`(8 PNR)、`ocuppy_01/02.dds`(10 佔領)、
  `pnr_crazy_ui.dds` + `pnr_crazy_reenter_{red,yellow}.dds`(PNR crazy)、
  `weaponEffectIcon.dds`(15 武器試射特效)。
- `emblem_base/frame/mark_{101..302}.dds` 徽章三層 (底/框/紋 — 對
  583/584 戰隊徽章 CRC 上傳); `SniperOfScope/` 狙擊鏡 14 張;
  `c_emicon.dds` 表情圖示; `tu_t_1..3` / `tuto_*` 教學步驟圖。

**ui/bot/** (13 張 — 槍械射擊館): `gunshooting*.dds`、`gs_ui/gs_ui02`、
`gs_popup/gs_popup02`、`ai2.dds`、`specialabilityslot.dds` — 全屬
mode 9 GunShooting / AI 協力 UI, 佐證「mode 9 不走一般建房」的
roommake 結論。

**ui/lobby/** (96 張 — 大廳/商店/角色/公會):
- `lobby_01..03.dds` 大廳背景; `l_gr.dds`/`l_gr_02`/`l_mr*` 遊戲房/
  電影房清單列; `l_notice.dds` 公告; `l_tournament_*` 錦標賽(對 108
  mode==3 的 sub_580A80); `Room_Identity.dds`(1024×512) 房單身份卡
  (Popup_Room_Identity.xml 的貼圖, 對 §7 房旗字串簇);
  `c_main/c_menu/c_cl/c_em/c_ge/c_ji/c_ms/c_se` 公會(戰隊)介面;
  `shop_01..03` / `store_01..06` / `store_NSkillInventory_1..3` 商店;
  `warehouse.dds` 倉庫; `quest.dds` 任務; `pepachi.dds`/`pepagacha.dds`
  轉蛋; `tuto_*`/`tutorial_openning` 新手教學; `noparts.dds`(改裝件
  未裝提示); `weapontest_back.dds` 武器試射背景。
- TGA: `Back.tga`/`BackGround.tga` 大廳底圖、`L_CharMake(.02).tga`
  角色創建、`Mouse_00/01.tga` 游標、`n_p{f,g,h,l,s,o,j,gg,..}` 商店
  角色立繪/物品預覽、`p_pui.tga`、`n_call01/N_CALL02/N_PMSG/N_PG/N_PEN`
  呼叫/訊息視窗。

**ui/loadscreen/** (55 張): `loading*.dds/jpg/png` 讀取畫面、
`mainloading_bg/spr0/spr1` 主讀取、`tips_001..010` 讀取小提示、
`SDCount_0..14` (**S**udden **D**eath 倒數 — 平手延長賽倒數數字,
0..14 共 15 個數字貼圖)、`companylogo/studiologo` 廠商標。

**ui/sounds/** (非圖像, 但檔名是最佳模式佐證):
- `soccer_sounds/spe_soccer_*`(12 足球)、`occupy_sounds/occ*` +
  `occ2_*`(10/13 佔領)、`pve_01_sounds/AI3_*` + `stage_clear/spy_1..16`
  (11 AI 協力 16 波)、`bomb_tolerance_1st/3rd`(2 爆破)、
  `ai2_boss_emergence`/`boss_emergence`(9/11 魔王)、
  `charCut_crazy`/`crazy*`/`fever_*`/`gage_*`(PNR crazy/fever)、
  `paper_horror.wav`(map_BGMSoundNames 104/105 城堡恐怖 BGM)。

> 註: 這些是二進位貼圖, 不入版控 (參考 .gitignore 慣例只收小文字
> 資源)。尺寸/格式可用 `PIL.Image.open` 直接讀 DDS/TGA 頭確認;
> 本環境無視覺檢視器, 上述判讀依據 = 檔名 + XML 引用 + 模式枚舉互證。
