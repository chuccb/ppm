# PaperMan 客戶端資源檔案地圖 (十四輪逆向)

> 這份文件回答:「伺服器/工具還需要哪些客戶端資源檔案?」
> 以及每個檔案的容器格式 (全部由 PaperMan.exe.c 逐行讀出)。

## 1. 對私服最有價值的檔案 (若能提供, 可完善 DB 目錄)

| 檔案 | 讀取器 | 內容 | 對私服的價值 |
|---|---|---|---|
| **cfg\ItemData.pat** | @131262 | 物品目錄 — **21,164 條**（檔案 stride **997B**；`1808B/條` 是記憶體結構，見 §2c2） | ★★★ 填 item_catalog 的真實資料: id/名稱/kind/價格/期限/能力值/需求等級 |
| **cfg\Quest.pat** | @602219 | 任務目錄 — **844 條 × 47 欄** CSV | ★★★ 填 quest_catalog: 條件類型/目標值/獎勵 |
| **cfg\weaponparts.pat** | @619191 | 武器改裝件 — **1,108 列 × 81 欄**（第一欄為**完整 item id**） | ★★ 220/221 編組 parts 驗證 |
| **cfg\maplist.pat** | (sub_717E50) | 地圖清單 — **123 張**（stride 836B，見 §5d-5） | ★★ 111 建房 map id 驗證 |
| **cfg\partsability.pat** | `CPartsAbilityListParamCtrl::Load` | 改裝件**效果差分** — **413 列 × 31 欄**彈道模型（**依欄位順序**解析，見 §5d-11） | ★★ |
| **cfg\RecommandItem.pat** | @686031 | 推薦商品 — **1,030 列 × 12 欄**（Concept 1=男性向 531／2=女性向 485／20=特殊 14） | ★ `808`→`809` GS_GET_RECOMMENDSET_INFO 內容 |
| **data.pat** | @225227 | 主資料容器 | ★★ (見 §3 已破解格式) |
| **Data\pmClient.dat** | @415210 | pmFile 打包主檔 | ★★★ 上面所有 cfg\*.pat 都從這打包檔讀出 |
| slanderfilter\FilterWord.{txt,dat} / ExceptionWord.{txt,dat} | `sub_717E50(L"slanderfilter\\", …)` | 聊天過濾詞 — 本 revision 實際隨附的是 **`filterword.txt` (1,227 行)** 與 **`exceptionword.txt` (1,686 行)**，**UTF-8 明文**（非 CP932）。native 先組 `.txt` 再組 `.dat` 路徑，兩種副檔名皆支援 | ○ (伺服器可自備；過濾為客戶端行為) |
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

> **⚠ 座標系澄清（本輪實測補正）。** 本節的 `tail[N]` 與括號中的 `bNNN`
> 是**記憶體結構**（1808B）的位移，**不是檔案位移**。在實際檔案裡，
> record stride 是 997B（見 §2c2），tail 區從 **record + 276** 開始，
> 長度恰為 `997 − 276 = 721` B —— 與本節標題的「尾部 721B」完全吻合。
> 換算公式：**`檔案位移 = record起點 + 276 + tail[N]`**。
> 例：`kind` 在 `tail[1]`，檔案上就是 `+277`；實測該處值為 9 的筆數
> **正好 10,914**，與下表記載一致。先前若直接拿 `b533` 當檔案位移去讀，
> 會取到無意義的位元組。

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
    → **UNRESOLVED:** this Japanese resource revision does not provide a usable
    price sheet. The client does send 358's locally calculated item-price
    pairs, but that alone cannot prove whether the retired service used,
    ignored, or cross-checked them, nor can any 205/209 response establish a
    catalog price policy.
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

### 2c-1. 本節錨點的獨立複驗 (本輪, 以 base=276 重跑全 21,164 筆)

| 本節主張 | 實測 | 結果 |
|---|---|---|
| `kind`(tail[1]) 值 9 = 武器, 10,914 條 | `+277` 值 9 者 **10,914** | ✅ 完全一致 |
| tail 區長度 721B | `997 − 276 = 721` | ✅ |
| tail[705..712] = ASCII 上架日 | **21,164/21,164** 全為 8 位數字 (首筆 `20081101`) | ✅ |
| tail[662] 上/停架旗標 約 71%=1 | 15,022/21,164 = **70%** | ✅（四捨五入差） |
| tail[112] u8 恆為 1 | 21,164 筆**全部**為 1 | ✅ |
| tail[105] u8 幾乎恆為 1 | 21,160 為 1，僅 4 筆為 0 | ✅ |
| 價格承載區日版全 0，僅 23 條有值 | tail[101..114] 中**只有 tail[107]** 非零，**恰 23 筆** | ✅ |

`kind` 完整分布（本輪新增，可用於分類查詢）：
`9`=武器 10,914 · `2`=裝飾 3,128 · `10`=PG/獎勵券 2,126 · `13`=ヘアパズル 1,550 ·
`12`=シャウト等服務 1,012 · `1`=衣裝 743 · `0`=頭部 718 · `8`=兜/頭飾 404 ·
`7`=加護類 204 · `6`=臉部 161 · `11`=パズルベンダー 158 ·
`14`=\[CP\] 變體 29 · `15`=\[CP\] 變體 16 · `16`=COUPON 1。

**tail[107] 的語義修正（Fact / MEDIUM）。** 本節原文把它列在「價格承載區」
並註記「參數非價格」。實測那 23 筆**全部是加裝／改造型武器**：
`INGRAM (Silencer)` 5、`SIG550 (Dot)` 30、`M1014 Benelli(Dot)` 20、
`TOMMY GUN (Custom)` 30、`M4(sopmod)` 15、`FMG-9(Dual Gun)` 30 等，
值域 {3,5,10,15,20,25,30}。其中 **17/23 同時出現在 `weaponparts.pat`**
的可改裝武器清單中。因此它**確定不是價格**，較可能是與加裝件相關的參數；
但 6 筆（含兩把「ルドルフショットガン」與 `INGRAM (Silencer)(SSW)`）
不在 weaponparts 清單內，故**確切語義仍 UNRESOLVED**，不得當作改裝件數量使用。

### 2c-2. id 空間對照表 (本輪彙整 — 避免再誤用)

各資源檔引用武器的方式**不一致**，這是本專案反覆踩到的陷阱：

| 檔案 | 引用形式 | 範例 |
|---|---|---|
| `cfg/weaponparts.pat` | **完整 item id** | `12100419`（全 1,108 筆皆落在 12.1M/12.2M） |
| `ui/system/SpecialWeaponType.xml` | **段內偏移**（需 +12100000） | `Index="2381"` → `12102381` |
| `ui/system/Tutorial_Data.xml` | **段內偏移 + `type` 指定段** | `type='1' weapon='26'` → `12200026` |
| `ui/system/AI/AiMultiCompensation.xml` | **完整 item id** | `itemnumber="15301005"` |
| `ui/cfg/maplist.pat` / `gamecenter_map_info.xml` | **map id**（非 item id） | `81`、`89` |
| `ui/lang/msgtableres.lang` | **lang id**（`entry i = lines[i+3]`） | `1085` |
| `ui/killImgWeapon.xml` | **段內偏移，且不指定段**（見下方 §2c-3） | `<!--2381-->` → `FMG-9(Dual Gun)` |

引用任何數字前，先確認它屬於哪一種 id。

### 2c-3. 四個武器段共用同一個偏移空間（Fact / HIGH，本輪證明）

`ui/killImgWeapon.xml` 是擊殺紀錄的武器圖示表，native 以寫死檔名
`L"killImgWeapon.xml"` 在啟動時載入 UI sprite registry。它用
`<!--N-->` 註解標出 **3,006 筆條目（索引 0..3005，其中 361 與 726 各出現兩次，
相異索引 3,004 個）**，每筆一張圖示，**但完全沒有「段」欄位** —— 只有一個數字。

這之所以可行，是因為**四個武器段的段內偏移互不重疊**。實測整個
`0..3099` 偏移空間對 12.1M／12.2M／12.3M／12.4M 四段做交叉比對：

```
存在於一個段的 (offset, band) 配對：2,076
同時存在於兩個以上段的 offset    ：0
```

**零碰撞。** 因此「偏移 → 武器」是**全域唯一**的，不需要段資訊即可還原。
驗證抽樣（killImgWeapon 註解 ↔ itemdata 名稱）：

| 索引 | XML 註解 | 落在哪一段 | itemdata 名稱 |
|---:|---|---|---|
| 2 | `m3` | primary | `M3 SUPER90` |
| 3 | `deagle` | **secondary** | `DE .50 AE` |
| 4 | `CU_BK7` | **melee** | `CU-BK7` |
| 7 | `BOMB` | **throw** | `HE GRENADE` |
| 27 | `mp5k` | primary | `MP5K` |
| 2381 | `double_fmg9` | primary | `FMG-9(Dual Gun)` |

3,004 個相異索引中 **2,074 個**能對到實際武器
（primary 1,338／melee 280／secondary 228／throw 228），
其餘 **930** 個是圖集中的預留或已移除條目。

**推論。** 這條「無碰撞」性質也解釋了 §5d-8 的 `SpecialWeaponType.xml`
為何敢只寫 `Index`、以及 §5d-10 的 `Tutorial_Data.xml` 為何其 `type` 欄
其實是**多餘的保險**而非必要 —— 偏移本身已足以唯一定位。
但**請勿反過來依賴這一點**：這是本 revision 資料的觀察性質，
若日後加入新武器造成碰撞就會失效；解析時仍應優先使用明示的段資訊。

## 1z. 2026-09 新 IDA 導出：40 個具名 global 取代原本的 `off_` 位址

`main` 分支新上傳的 `PaperMan.exe.c` 與既有版本**函數內容等價**
（17,830 具名函數、19,835 function body，集合完全相同），
但 Hex-Rays 為 40 個先前匿名的字串 global 產生了名稱。
這對交叉驗證有實質幫助：原本讀起來是無語義的 `&off_AE5AE4`，現在是
`&aSkillPresetSlo`。已確認的對照（以 `sub_40F510` 字串比較鏈的順序為準）：

| 新名稱 | 舊 `.c` 的寫法 | 意義 / 已知交叉點 |
|---|---|---|
| `aSkillPresetSlo`, `_0`, `_1`, `_2`, `_3` | `off_AE5AE4`, `off_AE5ABC`, `off_AE5A94`, `off_AE5A6C`, `off_AE5A44` | **恰好 5 個** SkillPresetSlot 按鈕，與 255 NewSkillProfile 的 `ProfileCount = 5` 完全吻合（獨立第二證據）|
| `aItemPlayButton_0`..`_6` | 匿名 | 7 個，與 `ITEM_PLAY_BUTTON_0%d` 格式字串同源 |
| `aVoiceCustomize`, `_0`, `_1`, `_2` | 匿名 | voice customize UI，對應 §5d 的 791–796 |
| `aAttack1`..`aAttack5`, `aMove1`..`aMove4`, `aReserve1` | 匿名 | radio-chat 分類按鈕（攻擊 5 / 移動 4 / 保留 1）|
| `aPresetSlotName`, `aPresetSlotLevD` | 匿名 | preset slot 的名稱與等級顯示欄 |
| `aPinfoWinImage`, `aPinfoLoseImage`, `aPinfoDieEmblem` | 匿名 | 戰績畫面勝/敗/陣亡徽章 |
| `aMyinventory`, `aPaperCode` | 匿名 | 背包與 serial-code 入口（對應 461/464）|

**界線。** 這些是 **UI 控制項名稱**，證明的是畫面上有哪些按鈕與其數量，
**不是** wire 欄位、不是 server 權限、也不是持有狀態。
`aSkillPresetSlo` 的 5 個實例可以獨立支持「5 個 profile」的既有結論，
但 profile 的內容、切換權限與持久化仍以 255 的 native reader 為準。

## 2c2. itemdata.pat record stride = 997B 自證切段 (本輪新增)

§2 的 `1808B/條` 是**記憶體結構**大小 (載入器 @131262 配置的 struct)，
不是檔案上的 record 間距。實測檔案佈局：

```
+0   s32 version   (本 revision = 1)
+4   s32 count     (= 21164)
+8   record[0] ... record[count-1]      每筆固定 997 bytes
     record +0   s32 item_id
     record +20  UTF-16LE NUL-terminated display name
```

**自證。** `8 + 21164 × 997 = 21,100,516`，與
`server/pmfile.py pat-decrypt` 輸出的檔案大小**完全相等**、無剩餘位元組，
因此 stride 與起點都不是猜測。重現：

```bash
python3 server/pmfile.py pat-decrypt Extracted/ui/cfg/itemdata.pat /tmp/itemdata.bin
# 之後以 stride=997、name @ record+20 (UTF-16LE) 逐筆解析
```

名稱欄為 UTF-16LE（非 §2 早期假設的 CP932 變長段），因此日文品名可直接讀出，
例如 `12300051 = チョコスティック`。四武器槽段的實際筆數：
12.1M 主武器 1339、12.2M 副武器 228、12.3M 近戰 280、12.4M 投擲 229，
與 §5a2 的分段語義一致。

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

(mode 5 Practice / 7 ChattingRoom 無專屬地圖; bit 7/8 未用。
⚠ bit 5 (0x20) 只出現在 3 張純 TU 圖 (map 0/101/102 = TU_01..03);
bit 6 (0x40) 則出現在全部 6 張教學可用圖 — 另 3 張為複用圖:
77=TW_13 池袋X-mas、78=PS_11 冬の街、86=ECT_01 WeaponPreview。
故「教學可選」過濾位取 **bit 6** (server ModeIndexMapBit[GameMode.Tutorial]=6)。)
建房時 client 以 `(map.modes >> bit) & 1` 過濾該模式可選地圖；
`modeIndex` 是 room 的 native mode 值 (0..13/15，見 §7)，`bit` 是上表對應的
maplist `modes` 位，兩者**不是同一編號**（如 TeamMatch=0、XML 顯示名
TeamDeath ↔ bit 2）。

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
**改裝件 8 組 (weaponparts.pat 81 欄完整版)**:
```
grp0 = 15.21M バレル (槍管)     grp1 = 15.22M トリガ (扳機)
grp2 = 15.23M フロントサイト    grp3 = 15.24M グリップ (握把)
grp4 = 15.25M ストック (槍托)   grp5 = 15.26M ドットサイト (紅點)
grp6 = 15.278M ペイント弾 (彈藥皮膚)
grp7 = 15.288M 整槍配色 (L96 A1 Color...)
```
**Fact / HIGH (main `Extracted` resource cross-check):** the first row declares
1,108 gun rows. Their 80 part columns produce exactly 10,648 nonzero
`(gun_item_id, grp, part_item_id)` compatibility references for 280 distinct
parts; a part occurs in one `grp` only. These are **compatibility edges**, not
10,648 different items and not starter grants. The native 913 receiver partitions
its IDs by the contiguous 8 intervals `15,210,001..15,220,000` through
`15,280,001..15,290,000`, respectively, and writes that matching 0..7 part
position. Thus the resource `grp` and native part-array index are independently
aligned.

`partsability.pat` has 413 parameter rows (49/37/37/58/58/19/20/135 by those
same eight intervals). All 280 parts accepted by `weaponparts.pat` occur there;
133 additional effect rows are not referenced by the current compatibility list.
It is therefore an effect-delta table — recoil/range/damage/shot_delay and
`miJump`/`miSit`/`miStand`/`miWalk`/`miRun` among 31 columns — **not** the
compatibility authority and not a grant source.

### 5a3. Weapon loadout domains and 220/221 authority boundary (current verification)

**Fact / HIGH.** `sub_4C7C00` names the four u16 columns shown by the loadout UI:
three profile rows (group `0..2`) contain `PRIMARYSLOT`, `SECONDARYSLOT`,
`MELEESLOT`, and `THROWSLOT`; group `3` is the primary-only
`SWITCHWEAPONSLOT`. `sub_527DB0` expands their nonzero u16 category offsets with
bases `12,100,000`, `12,200,000`, `12,300,000`, and `12,400,000` respectively.
They are player loadout state, not the 12 u16 character-normal-appearance words.
The old database column names `equipped/sub1/sub2/sub3` are retained only for
migration compatibility; their actual meanings are primary/secondary/melee/throw
offsets.

**Fact / HIGH.** `sub_573340` emits opcode 220 as a delta: `u8 changedCount`,
then only `sub_525680`-different groups, each serialized by `sub_524A50` as
`u8 group, u16 primary, [u16 secondary, melee, throw when group != 3],
[8×s32 parts when primary != 0]`. `sub_5735F0` reads opcode 221 into a fresh
object and copies that object over the current profile; consequently the server
response must contain the full authoritative four-group snapshot, not merely the
request delta. This corrects the older “220 response-only” conclusion.

**Fact / HIGH.** `sub_4C9440` fills the first three profile rows and rejects
nonzero duplicate offsets within each weapon family; it also rejects a primary
already present in the switch-weapon row. A selected primary causes
`sub_9591F0` to materialize its eight current parts, so the 220 wire carries the
parts only with a nonzero primary. This UI filtering is client behavior, not
historical-server authorization evidence.

**Inference / MEDIUM (server policy).** Server-side 220 accepts only items that
are actually owned and unexpired and parts that match the exact imported
`weaponparts.pat` row. A fresh database has an intentionally empty compatibility
table, so it fails closed until the resource importer supplies it. The native
client does not reveal the original server's authorization result code; rejected
220 has no invented success payload or state mutation.

**UNRESOLVED.** Neither client initialization, the static resource tables, nor
any observed packet producer establishes a character-specific starter primary,
secondary, melee, throw weapon, or part. Empty bootstrap loadout containers are
not evidence of an equipment grant.

**Implementation status (not native evidence).** `Database/Db.WeaponLoadout.cs` applies
only the submitted delta in one SQLite transaction, validates the merged
four-row state, and returns it in order for 221. `PaperMan.SelfTest` contains a
routed 220→221 63-byte full-snapshot case plus no-mutation negative cases for a
truncated record, duplicate primary, expired/unowned primary, unowned compatible
part, owned incompatible part, parts with an empty primary, and group-3
secondary/melee/throw words. The checked-in C# test has **not been executed in
this environment** because no `dotnet` SDK/compiler is installed. Separately,
the Python schema smoke test and an in-memory import of the decoded
`weaponparts.pat` have run: the latter inserted 10,648 rows and confirmed a
known `(12100016, grp=0, 15210001)` edge while rejecting its wrong-group and
wrong-gun variants. This only verifies schema/import data, not C# runtime
behavior.

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
(198 `sub_524010` character record 的 u8 char_type 即此編號；basic block 的
`+88` u8 是另一個 CHARSLOT list index) — 第六次互證。

## 5c-1. Native canonical starter appearance (character body template)

This section deliberately separates the native **canonical normal appearance**
from a body item, the 12 persistent appearance slots, temporary visual changes,
and shop recommendations.  The distinction matters: a similarly named XML
record or a PAV thumbnail is not, by itself, starter-state evidence.

### Evidence ledger

| Conclusion | Classification / confidence | Direct provenance |
|---|---|---|
| Each body `19900001..19900015` has one complete head/face/top/bottom/shoes vector. | **Fact / HIGH** | The five switch tables `sub_402FF0`, `sub_4030A0`, `sub_403150`, `sub_403200`, `sub_4032B0` in `PaperMan.exe.c`. |
| The vector is part of the native character-creation visual model, rather than only a shop display. | **Fact / HIGH** | `CLobbyCharMake::sub_41B330` passes body plus all five map results to `sub_4148D0`; `sub_41BDE0` passes mapped face/head offsets with the selected body into `sub_572EB0` (opcode 214). |
| The vector materializes ordinary character state when normal pieces are absent. | **Fact / HIGH** | `sub_522580(mask, a2)` writes mapped components into `a2[2..6]` from body `a2[1]`. `CPaperCtrl::sub_5B40E0` additionally fills mapped head/face/top/bottom/shoes when its normal appearance record has an absent head/body-template prefix. |
| All 75 mapped component records are compatible free normal-avatar resources. | **Fact / HIGH** | Decoded `cfg/ItemData.pat`: matching character type, `kind=6`, price `0`; every item has a matching `origin/main:Extracted/item/avatar/%02d_%05d_%02d.pav` asset. This corroborates the native state mapping; it does not establish it alone. |
| A private server should persist and acknowledge these six values for a newly created canonical character. | **Inference / MEDIUM** | The facts above plus the exact 311 reader establish the client-side canonical creation state and its accepted wire representation. The original server executable that generated historical 311 responses is unavailable. |

### Raw offset map

Offsets below are the u16 category-relative values used by the native normal
appearance record.  Add bases `19900000`, `10000000`, `10100000`, `10200000`,
`10300000`, and `10400000` respectively for body/head/face/top/bottom/shoes.
The table is a direct transcription of the five native switch tables; in
particular type 8 is `face=113, top=177, bottom=119, shoes=125`, and type 11
head is `1096` (full ID `10001096`), not the previously misread `584`.

| type / body offset | head | face | top | bottom | shoes |
|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 1 | 1 | 1 | 1 |
| 2 | 15 | 10 | 22 | 12 | 12 |
| 3 | 28 | 19 | 45 | 25 | 24 |
| 4 | 41 | 28 | 66 | 36 | 41 |
| 5 | 55 | 37 | 90 | 47 | 52 |
| 6 | 123 | 111 | 157 | 99 | 105 |
| 7 | 124 | 112 | 167 | 109 | 115 |
| 8 | 125 | 113 | 177 | 119 | 125 |
| 9 | 126 | 114 | 187 | 129 | 135 |
| 10 | 127 | 115 | 197 | 139 | 145 |
| 11 | 1096 | 839 | 1069 | 974 | 952 |
| 12 | 1428 | 865 | 1205 | 1069 | 1009 |
| 13 | 1600 | 866 | 1213 | 1072 | 1012 |
| 14 | 792 | 385 | 428 | 376 | 360 |
| 15 | 30220 | 920 | 10011 | 10011 | 10114 |

### State domains that must not be conflated

| Domain | What the native evidence says | Classification / confidence |
|---|---|---|
| Character body item (`199xxxxx`) | It selects a character identity/body and indexes the five template maps. It is the first normal appearance word, not a weapon or a recommendation set. | **Fact / HIGH** |
| 198/247 character record | `sub_524010`/`sub_524360` carry `u8 char_type` then 12 u16 normal appearance offsets: body, head, face, top, bottom, shoes, outer/set, eye, hair accessory, face accessory, head accessory, special. | **Fact / HIGH** |
| Canonical starter prefix | It is only the first six normal record words from the table above. The native maps do not name or populate the last six slots, so server bootstrap leaves those unrelated optional slots unchanged/empty. | **Fact / HIGH** for scope; **Inference / MEDIUM** for server persistence policy. |
| Weapons, 9 UI-item slots, and other props | Weapon groups (`sub_524660`), the nine `sub_527550` UI-item ordinals, and the selected NewSkill puzzle record are separate 198 state, not arguments or results of the five body maps. No native/resource dataflow in this corpus establishes a per-character starter weapon, consumable, or last-six-slot item. | **Fact / HIGH** for separation; **UNRESOLVED** for any historical per-character starter-item policy. |
| NewSkill five profiles | `GL_INVENIN_ACK` 255 carries five records of seven `1101…1107` puzzle IDs plus a packed-minute expiry; `sub_4AAB80` loads the selected one to `CClientData+144420`. Its `NEWSKILL_HEAD/CLOTH_UP/CLOTH_DOWN/SHOES/CLOTHOFSET/ACCESSORY1/ACCESSORY2` controls are puzzle preview controls, not 198/247 normal appearance or a shop recommendation. | **Fact / HIGH** for wire/UI separation. **Inference / MEDIUM:** user/account scope, because 255 is self-uid keyed and 466 has no character identity. |
| Fitting XML | `Extracted/ui/system/CharacterFitting.xml` is a `CHANGE_AVATAR_PROPERTY` fitting/preview source. Its hand textures and values are not a persistent starter vector. | **Fact / HIGH** |
| Cooki transformation | `CharacterToCooki.xml` is `CHANGE_AVATAR_TO_COOKI_PROPERTY`; `CCharToCookiProperty::sub_993E40` snapshots normal appearance and `sub_9942D0` restores it. It is temporary override state. | **Fact / HIGH** |
| RecommandItem / Total_Package XML | These are shop recommendation/package presentation data. They have no observed write to the persistent 12-slot character record and are not used as starter-default evidence. | **Fact / HIGH** for their UI/resource role; **UNRESOLVED** for any unobserved original-server pricing/business policy. |

### 5c-2. 9-slot UI items and 457/458 boundary

**Fact / HIGH.** The persistent 198/247 `sub_527550 → sub_522480` block is
exactly nine `s32` item IDs, with no count. `sub_4C4990`/`sub_4C6120` apply the
following control-specific inclusive ranges when a player selects an item:

| ordinal | UI label | accepted full-ID interval |
|---:|---|---|
| 0 | CROSSHAIR | `15305001..15305100` |
| 1 | NAME | `15304001..15305000` |
| 2 | MASTER | `15305301..15305400` |
| 3 | ABILITY | `15305401..15305600` |
| 4 | BOOST_EXP | `15305601..15305700` |
| 5 | BOOST_PG | `15305701..15305800` |
| 6, 7, 8 | EXTRA_ABILITY, EXTRA_ABILITY, VOICE | `15305801..15306000` |

Slot 8's VOICE label and its shared extra-ability range are both direct facts.
The `153051xx` legacy-voice-looking gap and `154xxxxx` voice records are **not**
therefore interchangeable with slot 8; their entitlement/storage relation is
**UNRESOLVED**. These UI IDs are player state and are not body-template,
appearance-slot, NewSkill puzzle, or temporary-transformation values.

**Fact / HIGH.** `sub_573770` sends opcode 457 only after `sub_5274D0` detects a
difference, then `sub_5275A0` serializes the exact 36-byte `9×s32` block.
`sub_573860` reads opcode 458 as those nine `s32` values plus exactly three
`{u8 rawFlag, s32 itemId, s32 itemStateRaw}` records (63 bytes total).
`sub_528C40` ignores `rawFlag`; if `itemId != 0`, it finds the matching local
inventory record by ID and writes `itemStateRaw` at record dword 4 (`+16`).

**UNRESOLVED.** No producer-side 458 trace in this corpus names that dword's
business semantics or establishes which three inventory records must be sent.
The server therefore does **not** implement 457/458 yet: it must not emit a
short ACK, fabricate `rawFlag`, or substitute zero records merely to satisfy the
length. The completed source/parser facts above are intentionally retained for a
future producer/consumer trace.

### Server storage and wire boundary

The existing database column names predate this reconstruction.  Their first
six *ordinal* fields, not their labels, store the native prefix:
`eq_primary..eq_face = body, head, face, top, bottom, shoes`.  This preserves
`GetCharacters` → 198/247 serialization order.  During login repair the server
fills only zero values on rows whose body is absent or already equals their
canonical `char_type`; it never changes a nonzero appearance word and leaves a
noncanonical nonzero body unresolved rather than inventing a replacement.

`GS_BUYCHAR` 310 receives the full body ID plus five native scalar words whose
server-domain semantics remain **UNRESOLVED**.  `GS_BUYCHAR_ACK` 311 has an
independently verified accepted creation vector: success, full IDs in **body,
face, head, top, bottom, shoes** wire order, followed by its always-read
neutral `u8=0, s32=0` account-update tail.  See `PACKETS.md §3.15pre1a` for the
reader-level layout.

### Evidence-bounded next work

1. Capture a real native 310 request (or recover the original server) to name
   its five trailing scalar words and verify character-price/currency policy.
2. Capture original successful and failed 311 responses to replace the bounded
   malformed-request assumption and identify `account_update_target` 1/2/3.
3. Do **not** add a weapon, consumable, recommendation-set, Cooki, fitting, or
   last-six-slot "starter" value until a native field/state write path supports
   it; the current evidence only establishes the six-word normal prefix.
4. NewSkill profile 1..4 expiry is server-authoritative in 255/467, but the
   original grant/renew packet has not yet been traced. Do not manufacture a
   shop/default unlock; retain the packed-minute raw word and leave the source
   operation **UNRESOLVED**.

## 5d. system XML 資料表 (二十輪全掃)

| 檔案 | 內容 | 對應 opcode |
|---|---|---|
| map_StartIndex.xml | **官方模式名表** (modeIndex↔modeName) | 111 rule |
| ItemAbilityLevTable.xml | skill 門檻與效果換算，**6 段** `lev_value` −2..+3 (pmFile 加密; §5d-20) | 205 f32 能力值 |
| ItemAbilityEffectColorTable.xml | skill 特效顏色 (`Lev_1/2/3` 的 id=3/4/5，與上表共用 id 空間) + `AlphaValue` + `Penalty`；明文 | — (純顯示) |
| ItemAbilityEffectNameTable.xml | skill 粒子名 `Ptcl_ItemEffect1..15`，`FirstPersonView` 全空＝僅第三人稱；明文 | — (純顯示) |
| AI/AiMultiCompensation.xml | AI 協力模式過關獎勵 (難度×等級→物品) | AI 模式結算 |
| AI/gamecenter_map_info.xml | 射擊館關卡 (盾 HP/Fever/砲位) | 479 GAMECENTER |
| AI/BotWave/BotEnemy/Scenario | AI 波次/敵人/劇本 (easy/intelligent) | AI 對戰 |
| Total_Package_Index.xml | 角色套裝 UI 索引 (114 套) | 商店套裝頁 |
| URLList_01.xml | 正版端點 (dl.paperman.jp, bill.paperman.jp, hangame.co.jp) | 網頁跳轉 |
| TimeLimit_NotUse_IP.xml | 防沉迷白名單 IP | — |
| netcafe_contents.xml | 網咖特典 (UTF-16) | PopUpNetCafeShop |
| voice_customize_contents.xml | 語音自訂 (UTF-16LE; 15 角色×92 情境×27 句) | 791–796 voice_slots |
| CharacterFitting.xml | 試衣間 temporary fitting/preview state (hand*.tga ← data.pat 快取；非 persistent default) | — |
| face_contents.xml | **聊天表情觸發詞表**（**非**臉型清單 — 舊記「角色創建」有誤，本輪更正）：5 種表情 × 共 122 個關鍵字，載入類別為 `CFaceChatScriptProperty` | 聊天／表情，與角色創建無關（創角用 `CharMakeProcess.xml`） |

(CharacterFitting 引用 hand12.tga — 與 data.pat 快取表的 15 個
hand*.tga 互證: 那是「試衣間手部貼圖」快取)

**語音自訂兩 XML (卌七輪全解 — 791..796 資料源)**:
- `voice_customize_contents.xml` (UTF-16LE BOM, root `vcustomizelistTable`):
  15 角色區塊 `<hayate index="2">`…`<devilgirl index="2">`, 每區塊
  `<Voice00>`..`<Voice92>`(缺 87) 共 **92 情境**; 每情境 27 句 speech =
  `command_1..9` / `tactics_1..9` / `infomation_1..9` (client 拼字如此;
  speech 為屬性 `speech="…"`, 全檔 1380×27 = **37,260 句**)。語音表的
  voice_item 偏移 = 情境編號 (VoiceNN → NN), 對到 (情境, 類別, 句序)。
- `voice_customize_path.xml` (UTF-8, root `vcustomizepathTable`): **92 個**
  `<soundsNN>` 區塊 (sounds..sounds92, 缺 87; 首塊 `<sounds index="0">`
  無尾碼) — first_path `sound\soundsNN` + 每塊 15 個 `sub_path_<角色>`
  (92×15 = 1,380 tags) = 語音檔路徑。
- 15 角色 codename 對照 (sub_8859B0 switch 0..14 與 sub_path 一一對應):
  0 maru 1 nari 2 dallae 3 lich 4 cacao 5 loki 6 hana 7 momo 8 wooka
  9 pero 10 spy_11 11 robotgirl_12 12 tsunderegirl 13 magicgirl 14 devilgirl
  — 等同 ICT_* 角色 (NORMAL_BOY…DEVILGIRL), character/models/type1..15
  = idx+1。base_voice 兩枚 s16 即選 sounds index。

**15 角色身份全對照 (卌八輪彙整 — 語音 codename ↔ 商店顯示名 ↔
char_type ↔ item_id)**: contents.xml 角色區塊序 (0..14) 即 char_idx;
商店 item_id 19,900,001..19,900,015 = char_type 1..15 (§5c), 故
`char_type = char_idx + 1`:

| char_idx | codename (sub_8859B0) | 商店/顯示名 (itemdata) | char_type | item_id |
|---|---|---|---|---|
| 0 | maru | ハヤテ (hayate) | 1 | 19900001 |
| 1 | nari | ティナ (tina) | 2 | 19900002 |
| 2 | dallae | ミリィ (milly) | 3 | 19900003 |
| 3 | lich | サイラス (Cyrus) | 4 | 19900004 |
| 4 | cacao | ドッドン (Doddon) | 5 | 19900005 |
| 5 | loki | ガイ (Guy) | 6 | 19900006 |
| 6 | hana | テリシア (Tericia) | 7 | 19900007 |
| 7 | momo | アルル (alulu) | 8 | 19900008 |
| 8 | wooka | ヴァン (van) | 9 | 19900009 |
| 9 | pero | フッド (hood) | 10 | 19900010 |
| 10 | spy_11 | リカ (rika) | 11 | 19900011 |
| 11 | robotgirl_12 | レム (rem) | 12 | 19900012 |
| 12 | tsunderegirl | エリス (eris) | 13 | 19900013 |
| 13 | magicgirl | ルコット (lucott) | 14 | 19900014 |
| 14 | devilgirl | ルーシー (lucy) | 15 | 19900015 |

⚠ contents.xml 角色區塊 0..9 用**顯示名** (hayate…hood)、10..14 用
**codename** (spy_11…devilgirl); 載入器只比對名稱、不讀 `index="2"`
屬性 (所有區塊都寫 2, 未見用途), **文件順序 = char_idx 0..14**
(CVCustomizeScriptProperty::sub_88B310 依子節點序傳 i 當 char_idx)。

**語音檔實體結構 (卌八輪 — `Extracted/sound/` 1,777 檔實測)**: 每
「情境/套件」是一個資料夾, 內含 15 角色 × 27 句 Radio_Message +
整組 Voice 本嗓:

```
sound\soundsNN\<codename>\Radio_Message\<codename>_command\   <codename>_command_01..09.wav
                              \<codename>_tactics\           <codename>_tactics_01..09.wav
                              \<codename>_information\       <codename>_information_01..09.wav
sound\soundsNN\<codename>\Voice\<codename>_cry|die|drop|jump|kill|...>_NN.wav
```

- `sounds`(無尾碼, index 0)= 角色**原生嗓** (Voice00 → voice_item 0);
  `sounds01`..`sounds92`(缺 87)= 92 個可選套件。
- 播放器 `sub_888220` (00888220) 的拼裝格式
  `%s\%s\Radio_Message\%s_%s\%s_%s_%02d.wav` (base\codename\codename\
  category\codename\category\NN) 與上面目錄完全吻合; 另 `Voice\`
  子夾是 base_voice (cry/die/drop/jump/kill…)。這把 **27 句 = command/
  tactics/information 三類 × 9 槽** 釘死在磁碟上。
- **UI 五頁籤 → 協定對照** (VoiceCustomizeMain.xml tab):
  `VOICE_CUSTOMIZE_FIGHT`=base_voice1(戦闘)、`_EMOTION`=base_voice2
  (感情)、`_COMMAND`=command、`_STRATEGY`=tactics、`_STATEMENT`=
  infomation — 即語音塊 `s16 base_voice1, s16 base_voice2,
  3×9×(s16 voice_item, u8 flag)` 的 2+3 組成來源。對應 UI 檔:
  `VoiceCustomize_Fight_Emotion.xml`(WAR+EMOTION 兩 select)、
  `VoiceCustomize_zxv.xml`(COMMAND/STRATEGY/STATEMENT 共用, 9 列),
  `VoiceCustomize_zxvPopup.xml`(改選彈窗)。

## 5d-2. 戰績面板 11 欄：三來源一致，解開 z/k/dd 三個縮寫 (本輪)

`db/schema.sql` 的 `player_stats` 有三個長期無法解釋的欄位 —
`z_kill` / `k_kill` / `dd_kill`，只知道對應 384/386/388，不知道語義。
本輪由 **UI XML + 反編譯 + opcode 順序** 三方獨立對上，可以定案。

**來源 1（resource）**：`Extracted/ui/information.xml`（pmFile 加密，已解出）
的戰績面板依文件順序有 11 個具名欄位。
**來源 2（native）**：`PaperMan.exe.c` 有**三處**互不相同的面板繪製碼
（約 67930 / 156900 / 157296 行），各自以 `sub_6A8D80(..., L"<名稱>", ...)`
依序填格，三處順序**完全相同**，且與 information.xml 逐格吻合。
第一處另帶 UI ordinal `n2 = 2..12`。
**來源 3（protocol）**：`db/packets.tsv` 的 GP_CH* 計數器 opcode。

| # | 面板名稱 | opcode | schema 欄位 |
|---|---|---|---|
| 1 | `HEADSHOT` | 236 `GP_CHHEADSC` | `headshots` |
| 2 | `HEARTBREAK` | 240 `GP_CHHEARTC` | `hearts` |
| 3 | `CRITCALSHOT`（原廠拼字如此） | 362 `GP_CHCRITICALC` | `criticals` |
| 4 | `AIRCOMBO` | 238 `GP_CHACOMBOC` | `combos` |
| 5 | `DOUBLEKILL` | 242 `GP_CHDKILLC` | `double_kill` |
| 6 | `TRIPLEKILL` | 244 `GP_CHTKILLC` | `triple_kill` |
| 7 | `MULTIKILL` | 380 `GP_CHMKILLC` | `multi_kill` |
| 8 | `ULTRAKILL` | 382 `GP_CHUKILLC` | `ultra_kill` |
| 9 | **`GENOCIDE`** | 384 `GP_CHZKILLC` | **`z_kill`** |
| 10 | **`KILLINGMACHINE`** | 386 `GP_CHKKILLC` | **`k_kill`** |
| 11 | **`DIABLO`** | 388 `GP_CHDDKILLC` | **`dd_kill`** |

**決定性的對齊。** 380→382→384→386→388 這**連號五個** opcode，
其升冪順序與面板第 7–11 格順序**完全一致**；因此
`Z`=Ge**n**ocide（Z 為原廠對該階的代號，全檔僅 `L"GENOCIDE"` 一處拼法）、
`K`=**K**illingMachine、`DD`=**D**ia**b**lo 可以定案。
schema 註解已補上對應名稱。

**與 Wiki 的交叉點（含一項修正）。**
[よくある質問や答え](https://wikiwiki.jp/paperman/よくある質問や答え) 說
「7 kill 目 キリングマシーン +1；8 kill 目以後全部算ディアブロ」。
native 的連段共 **7 階**（DOUBLE→TRIPLE→MULTI→ULTRA→GENOCIDE→
KILLINGMACHINE→DIABLO），若自 2 kill 起算，第 7 階正好落在 7 kill，
與 Wiki 敘述自洽。**但 Wiki 未提及 `GENOCIDE` 這一階**（該頁只列舉兩端），
本表以 resource + native 為準。上限行為（8 kill 以後是否全記 DIABLO）
是計分規則，屬 server policy，維持 UNRESOLVED。

## 5d-3. PvE 計分表 ScoreRatio.xml：native 明確指名兩個檔案路徑

`Extracted/ui/system/AI/ScoreRatio.xml`（pmFile 加密，已解出）是
**倍率表**，且 native 解析器把檔案路徑寫死在程式碼裡：

```c
if ( sub_67EB70() )  thisa = sub_701BD0(&v41, L"ui/system/AI/AiMultiScoreRatio.xml", L"SCORERATIO");
else                 thisa_1 = sub_701BD0(&v40, L"ui/system/AI/ScoreRatio.xml",      L"SCORERATIO");
```

即 **AI 多人協力模式與一般 PvE 用兩張不同的倍率表**，由 `sub_67EB70()` 切換。
XML 內每個標籤都能在 native 找到對應的 reader 字串
（`SCORERATIO`／`Kill_1Time`／`Kill_Chain`／`HeadShotRatio`／`HeartShotRatio`／
`CriticalShotRatio`／`AirComboRatio`／`active_value`／`spend_time`／`miss_shot`），
因此欄位語義不是猜測。

* 單次擊殺倍率 `Kill_1Time`：HeadShot ×2、HeartShot ×1.5、CriticalShot ×1.8、AirCombo ×2。
* 連段族 `Kill_Chain` 以 `index` 分四族：`0=Quick`（4 階）、`1=Weakness`(4 階)、
  `2=Fever`（3 階）、`3=Combo`（0..10 共 11 階）。每階有
  `active_value`（門檻）、`spend_time`（毫秒時窗）、`ratio`（倍率）。
* `Combo` 族的時窗自 12000ms 逐階收緊到 6000ms，倍率 1.1→2.2；
  `Combo0` 的 `spend_time=480000`（8 分鐘）明顯是「不設限」的哨兵值。

**界線。** 這是 **PvE／AI 模式**的倍率表，和 §5d-2 的 PvP 連段面板是**兩套不同系統**
（後者的 UI 名稱 DOUBLEKILL…DIABLO 不出現在本表）。
倍率如何換算成最終 PG／EXP 屬 server 結算政策，本表不足以推導，維持 UNRESOLVED。

### 5d-3b. `UIActor.xml` 的韓文開發註解獨立印證上表（本輪）

`Extracted/ui/system/UIActor.xml`（native 以寫死路徑
`L"ui/system/UIActor.xml"` 搭配根標籤 `L"UIACTOR"` 載入）在開頭留有
**CP949 韓文開發註解**，逐項說明 42 個 `EFF*` 特效槽的用途。
其中四條與 `ScoreRatio.xml` 的四個 `Kill_Chain` 族**一一對應**：

| UIActor 註解 | 中譯 | ScoreRatio 族 |
|---|---|---|
| `EFF20 배율 이펙트 래피드 킬 텍스트` | 倍率特效／**快速擊殺**文字 | `index 0 = Quick`（4 階） |
| `EFF21 배율 이펙트 약점 킬 텍스트` | 倍率特效／**弱點擊殺**文字 | `index 1 = Weakness`（4 階） |
| `EFF19 배율 이펙트 피버 텍스트` | 倍率特效／**Fever** 文字 | `index 2 = Fever`（3 階） |
| `EFF22 배율 이펙트 킬 콤보 텍스트` | 倍率特效／**連段**文字 | `index 3 = Combo`（11 階） |

更直接的是 `EFF23`..`EFF32` 標為 **`LV 1`..`LV 10`**，恰好對上
`Combo1`..`Combo10` 這 10 個等級（`Combo0` 是門檻 1 的基準階，無等級特效）。

註解也確認 `EFF5`/`EFF6`/`EFF7`/`EFF8` 依序是
**헤드샷／하트샷／크리티컬샷／에어샷**（head／heart／critical／air shot）顯示，
與 `Kill_1Time` 的四個倍率欄位
`HeadShotRatio`／`HeartShotRatio`／`CriticalShotRatio`／`AirComboRatio`
**同序對應**。另 `EFF14` 明確標注「실드가 데미지를 입을 때 → 사용안하는 사양으로 바뀜」
（護盾受損時 → 已改為不使用的規格），是原廠自述的**廢棄功能**。

這是**第三個獨立來源**（XML 資料＋韓文註解＋native reader）指向同一組語義，
`ScoreRatio.xml` 的欄位解讀因此不再是單一推斷。

## 5d-4. NewSkillLevTable.xml：ペーパズル 合成公式的原廠常數

`Extracted/ui/NewSkillLevTable.xml` **未加密**（UTF-8 BOM 開頭），
且保留了開發期的韓文註解，可直接讀出合成系統的參數：

* `<COMBILIMIT DATA="3">`（기본조합한계치＝基本組合上限）。
* `<SKILLPOINT ONE=15 TWO=9 THREE=12 FOUR=10 FIVE=11>`
  （능력치별레어도배율＝五條能力軸各自的稀有度倍率）。五個值對應
  迅速／敏捷／根性／防禦／集中五軸，與 `ItemAbilityNameTAble.xml`
  的 5×5 複合名稱矩陣（迅速系／乱戦系／鎮圧系／運搬系／Hit&Run系…）同軸。
* 六個部位段 `Hair` / `Jacket` / `Pants` / `Shoes` / `Set` / `Accessory`，
  各有 `COMBI`（파츠보정값＝部位修正）與 `STRENGTH data_1..3`（강화보정값＝強化修正）。
  `Set` 的 COMBI=23、STRENGTH=3/6/9 明顯高於單件（COMBI=8），
  `Accessory` 全為 0。
* 每部位有 `Lev_1..Lev_9` 的 min/max 區間（레어도등급표），
  以及 `ValueRevision_<軸>_<等級>` 的逐軸數值修正。

搭配同目錄的 `ItemAbilityLevTable.xml`（pmFile 加密，已解出）可得
**最終效果換算**。⚠ **本節先前寫「以 `lev_value` −2…+2 分段」是錯的**：
實際檔案有**六**段 `id=0..5` / `lev_value` **−2…+3**（含 `0` 的無效果段）。
完整分段與 native 讀取語義見 §5d-20。

**界線。** 這些是 **client 端顯示與預覽**用的換算表。實際生效的
能力值、是否由 server 覆核、以及 255 NewSkillProfile 的持久化內容，
仍以 native reader 與封包為準；不得用本表回推 server 應發的數值。

## 5d-20. Skill 三表閉環：門檻、效果、系統名與特效（本輪全解）

`ItemAbilityLevTable.xml`（pmFile 加密）、`ItemAbilityEffectColorTable.xml`、
`ItemAbilityEffectNameTable.xml`（後兩者**明文**）是同一個 skill 子系統的三張表，
本輪三者的 native reader 全部定位，且彼此的索引空間可互相印證。

### 門檻與效果（`CItemAbilityLevTable`，root `SkillAbilityTable`）

載入：`CItemAbilityLevTable::possible_ctor_or_dtor` @ `0x7DC090`
→ `sub_717E50(L"system/ItemAbilityLevTable.xml")` → `pmFile` 解密
→ `sub_704680(..., L"SkillAbilityTable")` → parser `sub_7DC2F0` @ `0x7DC2F0`。

parser 逐 `<Lev>` 讀 `start` / `end` / `id` / `lev_value`，再逐子節點讀
`value` / `nickname` / `name` / `operation`。**六段**（Fact / HIGH）：

| id | lev_value | start..end（技能點區間） |
|---:|---:|---|
| 0 | −2 | −1000 .. −10 |
| 1 | −1 | −9 .. −5 |
| 2 | 0 | −4 .. +4（五軸全為 `none`＝無效果） |
| 3 | +1 | +5 .. +9 |
| 4 | +2 | +10 .. +14 |
| 5 | +3 | +15 .. 1000 |

**`operation` 是具名 functor，不是字串比較。** `sub_7DC6D0` 把它映為
`PERCENT_MINUS→1 / PERCENT_PLUS→0 / MINUS→3 / PLUS→5 / 其他→2`，
對應 RTTI 中確實存在的 `CAbilityOperatorPersentPlus` (`sub_7DCDB0`)、
`CAbilityOperatorPersentMinus` (`sub_7DCDE0`)、`CAbilityOperatorPlus` (`sub_7DCE10`)、
`CAbilityOperatorMinus` (`sub_7DCE30`) 四個 `CAbilityOperator` 子類。

**軸索引由 `sub_7DBDA0` 固定為 `speed=0 / agility=1 / hp=2 / defence=3 / hit=4`**
（不認得的軸回傳 6）。parser 的 `if (i[0] <= 4u)` 只接受 0..4，
且物件以 `eh vector constructor iterator(this+2, 0x10u, 5, ...)` 配置**恰好 5 個**
容器 —— **五軸是 native 寫死的結構事實**，不是由資源檔筆數推得。

### 顏色表與效果表共用同一個 `id` 空間（本輪關鍵交叉點）

`CItemAbilityEffectColorTable::possible_ctor_or_dtor` @ `0x7D9D60` 讀
`system/ItemAbilityEffectColorTable.xml`，root 是 **`ItemEffectColorTable`**
（與檔名不同，native 以 root 名取節點）；parser `sub_7D9D90` 分三種節點：

* `Lev_*`：讀 `id`，再以 **`this + 4*id + 8/32/56/80/104`** 寫入五軸顏色。
  檔案的 `Lev_1/2/3` 其 `id` 分別是 **3/4/5** —— 正好是上表
  `lev_value +1/+2/+3` 的 id。**兩張表是同一個 id 索引空間**，
  這解釋了為何顏色只有三筆：**只有正向三段才有特效顏色**。
* `AlphaValue`：固定迴圈 `j < 7` 讀 7 個值，但檔案只提供 `Lev1..Lev6`；
  取值器 `sub_7DA150` 對 `n7 >= 7` 回 0、`n7 < 0` 回 `this+131`（即 Penalty alpha）。
* `Penalty`：`color` + `alpha`，供負向段（懲罰）使用。

取色器 `sub_7DA0D0` 有 `if (n5 >= 5) return false` 的軸上限，
與五軸結構一致；並以 `r+g+b > 0.01` 判斷「這一格是否真的有顏色」。

`ItemAbilityEffectNameTable.xml`（root 同名）由 `sub_7DA190` 之後的段落讀取，
取 `ThirdPersonView` / `FirstPersonView` / `LevValue`：**15 筆
`Ptcl_ItemEffect1..15`，`FirstPersonView` 全為空字串** ——
即 skill 粒子特效**只有第三人稱視角會顯示**（Fact / HIGH，第一人稱欄確實空白）。

### 與 Wiki [スキル一覧](https://wikiwiki.jp/paperman/スキル一覧) 的逐格比對

Wiki 該頁 last-modified **2015-05-04**。比對結果**不是簡單的「相符」**，
必須分三類陳述（這正是為何不可直接採信任一方）：

| 類別 | 筆數 | 內容 |
|---|---:|---|
| **完全一致** | 8/19 | `speed` −1/+1/+2、`hp` −1/+1/+2、`hit` +1/+2。Wiki 的 `紙鶴(+8%)`、`糊(+8)`、`童画本(-30%)` 等括號數值與檔案逐格相同。 |
| **同絕對值、正負號相反** | 5/19 | 整條 `defence` 軸。檔案 `+1` 段是 `PERCENT_MINUS 10`，Wiki 寫 `厚紙(+10%)`。 |
| **數值不同** | 6/19 | 整條 `agility` 軸（檔 `+1`=15%，Wiki=10%；`+3` 檔 35% vs Wiki 22%），加 `hit` −1（檔 60% vs Wiki 50%）。 |

**符號差異有一個可驗證的解釋，不是矛盾。** Wiki 明確定義「敏捷／集中
**数値が小さいほど**早く／ブレ幅が小さい」，即這兩軸的原始參數是
**越小越好**；檔案存的是**引擎參數的增減**，Wiki 寫的是**對玩家的利弊**。
`defence` 同理（檔案存的是「受傷倍率」而非「防禦力」）。
`speed`/`hp` 兩軸「越大越好」，於是兩邊符號一致 —— 這正好**只有這兩軸完全吻合**，
自洽。剩下的**數值差（agility、hit −1）則是真正的版本差異**，不可調和。

**技能點門檻是本輪第二條被二進位證實的 Wiki 敘述。** Wiki 寫
「スキルは**5ポイント毎**に発動、1～4ポイントでは一切の効果はありません」，
並列出 `～-10 / -9～-5 / -4～0～+4 / +5～+9 / +10～+14 / +15～` 六段。
檔案的六段邊界 **逐格完全相同**（含 −4..+4 為無效果段）。
這是**定性規則與全部六組邊界同時吻合**，可視為 Fact（客戶端顯示層）。

### 系統名矩陣：另一個可定年的版本差

`ui/ItemAbilityNameTAble.xml`（root `AbilityNameSystem_JP`，注意原廠檔名
`TAble` 拼寫）是 5×5 複合系統名矩陣，實測**完全對稱**（25 格、15 個相異名稱）。
與 Wiki 的「スキル系統」表比對：**13/15 相同**，兩處不同：

| 座標 | 本 revision 檔案 | Wiki (2015-05-04) |
|---|---|---|
| speed×hit | **`Hit&Run系`** | `速戦系` |
| hp×hit | **`対応射撃系`** | `応射系` |

兩處都是「同義改名」（`Hit&Run系`→`速戦系` 是外來語改為漢語、
`対応射撃系`→`応射系` 是縮寫），方向一致，**指向本 extraction 早於 2015-05 的 Wiki 版本**。
這與 §5f 的版本考古互相參照，且**比對 15 個名稱比對單一檔案時間戳更可靠**。

**界線（不變）。** 以上全部是 **client 端顯示／預覽／特效**用表。
`sub_7DA0D0` / `sub_7DA150` 的消費端是顏色與 alpha，`Ptcl_*` 是粒子名。
**實際戰鬥數值是否由 server 覆核，仍無任何 client 證據**，
比照 §5d-16 的 Rocket/Plasma/Laser 處理：**可讀出 ≠ 有權威**，
不得據此實作伺服器端的能力值計算或 255 profile 的效果發放。

## 5d-4b. AI/PvE 過關獎勵：itemnumber 可解析為具名獎品

`Extracted/ui/system/AI/AiMultiCompensation.xml`（已解出）給出
AI 協力模式的過關獎勵表，結構為
`MODE_PRESENT[mode_index] → MODE_LEVEL_{EASY,NORMAL,HARD}[index] → DATA[]`，
每筆 `DATA` 有 `itemnumber` / `level`（名次）/ `periodType`。
本 revision 只出現 `mode_index` 102 與 104 兩組，且三個難度的獎品**完全相同**。

以 `dump_itemdata.py` 解出 `itemnumber` 後，四個獎品都是可讀名稱，
並精準落在 §5a2 既有的 15.xM 分段語義上：

| itemnumber | 名稱 | 段語義 | 名次 |
|---|---|---|---|
| `15301005` | `福袋(☆☆)` | 15.301M 福袋 | 1 |
| `15301004` | `福袋(☆)` | 15.301M 福袋 | 2 |
| `15200044` | `報奨金 5,000PG` | 15.2M PG 點數包 | 3 |
| `15200045` | `報奨金 3,000PG` | 15.2M PG 點數包 | 4 |

這同時**反向驗證**了 `dump_itemdata.py` 的切段正確（若 stride 錯，
這四個 ID 不可能同時解出語義自洽的名稱）與 15.2M／15.301M 的段定義。

**界線。** 這是 client 端的**獎勵顯示表**。實際發放由 server 決定，
名次判定、是否可重複領取、periodType 的時效語義均未證實，維持 UNRESOLVED。
`AiMultiCompensation.xml` 等 **18 個 `ui/system/AI/*.xml` 全部**都能在
`PaperMan.exe.c` 找到寫死的路徑字串，故都是本 revision 實際會載入的檔案，
不是殘留資產。

## 5d-5. maplist.pat 全解：123 圖 × mode bitmask，與既有 bit 表 100% 相符

`Extracted/ui/cfg/maplist.pat`（pmFile 加密，已解出）佈局同樣**自證**：

```
+0  f32 version      (本 revision = 1.03)
+4  s32 count        (= 123)
+8  count × 836B record
    record +0  s32 mode bitmask
    record +4  s32 map id
    record +8  UTF-16LE NUL-terminated "maps\<NAME>.pmm"
```

`8 + 123 × 836 = 102,836` 恰等於解密後檔案大小，零剩餘位元組。
工具：`python3 server-cs/tools/dump_maplist.py [--mode N|--id N|--check]`。

**獨立驗證既有的 modeIndex→bit 表。** 用
`verify_server_naming.py` 的 `MODE_INDEX_MAP_BITS` 去解這 123 張圖的 bitmask，
**123 張全部至少帶一個已知 mode bit，無一例外**。各模式可用圖數：

| mode | 圖數 | | mode | 圖數 |
|---|---|---|---|---|
| TeamSurvival | 37 | | Tutorial | 6 |
| TeamMatch | 29 | | GunShooting | 2 |
| DefuseBomb | 14 | | Occupy | 2 |
| Steal | 13 | | TeamSoccer | 2 |
| IndividualSurvival | 12 | | WeaponTest | 1 |
| PulpnRoll | 7 | | AIMulti | 1 |
| （bit5＝Practice，僅 3 張 TU_* 與 Tutorial 並存） | 3 | | OccupyRenewal | 1 |

**閉環交叉點。** GunShooting（modeIndex 9）在 maplist 中**恰好**是
map id 81 `AI_01_Monster.pmm` 與 89 `AI_02_Monster.pmm`，
而 `ui/system/AI/gamecenter_map_info.xml` 的兩筆
`GUNSHOOTING_MAP_INFO index="81"` / `index="89"` 完全對上 ——
**兩個獨立資源檔互為印證**，同時證明 `gamecenter_map_info.xml` 的
`index` 就是 maplist 的 map id（不是 modeIndex）。

**必須避開的陷阱。** bitmask 是**集合**而非純量，且
**檔名前綴不能用來推導模式**：`PVE_01_ruins.pmm` 實際是 AIMulti(11)，
`TS_31/32_worldcup.pmm` 實際是 TeamSoccer(12)，
三張 `TU_*` 同時帶 Practice 與 Tutorial 兩個 bit。
一律讀 bitmask，不要讀檔名。

## 5d-6. 一條完全閉合的四來源引用鏈（射擊館 GunShooting）

本輪最有價值的不是單一數值，而是**證明整套交叉引用機制可以走通**。
以射擊館為例，四個彼此獨立的來源串成一條無斷點的鏈：

```
maplist.pat            map id 81  → maps\AI_01_Monster.pmm，bitmask 1024 = GunShooting(9)
   ↓ 同一個 id
gamecenter_map_info.xml  <GUNSHOOTING_MAP_INFO index="81" ...
                          mapname="ロボットたちの反乱" shieldhp="1000" feverTime="3000"
                          langScenarioID="1085" langDialogueID="1084" langClearID="1128">
   ↓ 同一個 lang id
msgtableres.lang       entry 1085 = 「最新鋭のロボット工場で、原因不明のトラブル発生！…」
                       entry 1084 = 開場對話、entry 1128 = 過關台詞
   ↓ 同一個 modeIndex
map_StartIndex.xml     modeName="GunShooting" modeIndex=9 modeStartIndex=89
```

* `maplist.pat` 中 GunShooting 的**全部**地圖恰為 81 與 89，
  而 `gamecenter_map_info.xml` 也**恰有**這兩筆 —— 兩份獨立資源互相印證，
  且證明該 XML 的 `index` 欄是 **maplist 的 map id**，不是 modeIndex。
* langID 解出的劇情文字與 XML 的 `mapname` 語意一致
  （81＝「ロボットたちの反乱」↔ 訊息提到ロボット工場；
  89＝「記憶の手掛かり」↔ 訊息講尋找記憶的魔法石），
  確認 `msgtableres.lang` 的 `entry i = lines[i+3]` 解碼規則正確。
* 第二張圖的 `modeStartIndex=89` 與 map id 89 同值，說明
  `map_StartIndex.xml` 的 `modeStartIndex` 是「該模式的預設起始地圖 id」。

**方法論結論。** 資源檔之間以 **id 而非名稱**互相引用：
map id、item id、lang id 三類 id 各自貫穿多個檔案。
任何新分析都應先確定手上的數字是哪一類 id，再跨檔解析；
本輪的三個反例（檔名前綴推模式、`index` 誤認為 modeIndex、
1808B 記憶體結構誤當檔案 stride）都源自跳過這一步。

## 5d-7. convars.pat：14 個角色能力值，且第 15 個 (ルーシー) 刻意缺席

`Extracted/convars.pat`（pmFile 加密，已解出；228 行）是**角色能力值的權威表**。
除三個全域常數（`um_gr_accel 1250`、`um_gr_decel 333`、`gun_caliber 50`）外，
其餘全是 `m_cAvataAbility[ICT_*]` 與 `m_cDmg2MultiplyAvataAbility[ICT_*]` 兩組。

| ICT type | defence | movespeed | cam_offset | sitdownCam |
|---|---:|---:|---:|---:|
| `ICT_NORMAL_BOY` / `ICT_NORMAL_GIRL` | 0 | 90 | 0 | 47 |
| `ICT_YOUNG_GIRL` | −10 | 88 | −8 | 47 |
| `ICT_MUMMY` | −11 | 86 | 0 | **43** |
| `ICT_TRUMP` / `ICT_ROKI` / `ICT_HANA` / `ICT_MOMO` / `ICT_PERO` | +15 | 90 | 0 或 −8 | 47 |
| `ICT_UKA` | +12 | 90 | 0 | 47 |
| `ICT_SPYGIRL` | −12 | **85** | 0 | **43** |
| `ICT_ROBOTGIRL` | −14 | 87 | −8 | 47 |
| `ICT_TSUNDEREGIRL` | −10 | 88 | 0 | 47 |
| `ICT_MAGICGIRL` | +10 | 87 | −8 | **46**（standCam 亦為 61） |

`def_hp` / `max_hp` 全為 100、`jumpheight` 全為 350 —— 差異只在
**defence / movespeed / 攝影機高度**三組，這正好對應 Wiki
[キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧) 所述
「各角色有能力差異」的實際落點。`defence` 為負者（SPYGIRL −12、ROBOTGIRL −14）
移動較慢或體型較小，是「脆但靈活」的設計取捨。

**關鍵不對稱（Fact / HIGH）。** native 的載入函式**依序查詢 15 個** ICT
（順序與 convars 前 14 筆**完全一致**，第 15 個是 `ICT_DEVILGIRL`），
但 **convars.pat 只定義了 14 筆，沒有 `ICT_DEVILGIRL`**。
native 對每個 key 都傳入硬編碼 fallback，`ICT_DEVILGIRL` 的 fallback 是
`(100, 100, 0, 90, 350, -8, 62, 47)` —— 即 **defence = 0**。
因此本 revision 中 ルーシー(devilgirl, 角色型別 15) 實際以「無防禦修正」運作。

這種「缺項」是**逐檔案**的，**不是**「越晚的角色資源越少」的通則 ——
本輪重查三個角色屬性檔後必須修正先前的過度概括：

| 角色 | convars 能力值 | CharacterFitting | CharacterToCooki | CharacterToPushChar | avatar `.pav` |
|---|---|---|---|---|---|
| 型別 1–10（hayate…hood） | ✅ | ✅ | ✅ | ✅ | ✅（前綴 `00`–`09`） |
| 11 spygirl / 12 robotgirl / 13 tsunderegirl | ✅ | ✅ | ✅ | ✅ | ❌ |
| 14 magicgirl (ルコット) | ✅ | ❌ | ✅ | ✅ | ❌ |
| 15 devilgirl (ルーシー) | ❌（用 fallback） | ❌ | ✅ | ✅ | ❌ |

`CharacterToCooki.xml` 與 `CharacterToPushChar.xml` **都完整含 15 個角色段
且 `bEnable` 全為 1**；`CharacterFitting.xml` 才是只到第 13 個
（且 13 段中只有 `hayate` 的 `bEnable=1`）。
因此正確的說法是：**devilgirl 缺的是 `convars` 能力值與 `CharacterFitting` 兩項**，
而非全面缺席。

`item/avatar/` 的 20,193 個檔中，符合 `%02d_%05d_%02d.pav` 命名的 20,189 個，
其角色欄**只出現 `00`–`09`**（另 4 個是 `01_00239_07,pav` 逗號錯字、
`05_01749_2.pav` 位數不足、兩個 `10500201_*.pav` 非該命名族）。
`character/textures/` 則有完整的 `hand1..hand15.tga` 15 張。
換言之**第 11 個角色之後就不再走 `.pav` 頭像族、改以模組化方式掛載**，
這與 Wiki 把 ルーシー 記為 2016 年（服務終止前半年）新增相容，
但**「資源覆蓋遞減」只在 convars／Fitting 兩處成立**，不可推廣。

**界線。** 以上是 **client 端能力值與資產覆蓋**。伺服器是否也套用這些
defence/movespeed、以及 devilgirl 的 0 防禦是有意或疏漏，
皆無 server 證據，維持 UNRESOLVED。

## 5d-8. SpecialWeaponType.xml：weapon index = item id − 12100000

`Extracted/ui/system/SpecialWeaponType.xml` 只有 8 列，但示範了
**第四種 id 空間**：它的 `Index` 既不是 item id 也不是 map id，而是
**12.1M 主武器段的段內偏移**。加上 `12100000` 後全部命中且語義自洽：

| Index | item id | 名稱 | SpecialType |
|---|---|---|---|
| 2381–2384 | 12102381–84 | `FMG-9(Dual Gun)` | `1 = DUAL_GUN` |
| 2377–2380 | 12102377–80 | `M1 Garand` | `2 = EMPTY_RELOAD` |

XML 自帶的列舉註解 `NONE=0, DUAL_GUN=1, EMPTY_RELOAD=2` 與名稱**完全吻合**：
雙槍武器標 1、使用 en-bloc 彈夾（打空自動退夾）的 M1 Garand 標 2。
8/8 全對，且兩者都在 Wiki 的武器清單中（FMG-9 在 SMG 類、M1 Garand 在 AR 類）。
native 有 `L"SPECIALWEAPON"` 與 `L"SpecialType"` 兩個 reader 字串。

同一名稱各佔 4 個連號 id，對應 §2d 的 `t8`/`t12` 變體鏈：
`12102377` 為原型，`78/79/80` 以 `t8`／`t12` 指回原型。
在武器段中 `t12` 的實際語義是**變體→基底武器**，例如
`12100363 WINCHESTER [CP]` → `t12 = 12100013 WINCHESTER`
（正是 Wiki 另立條目的「Winchester(CP)」），
`12100364 P90 [CP]` → `12100024 P90` 等；1,291 筆非零中武器段佔 1,291 之多數，
其中 1,194 筆與基底同名、97 筆為 `[CP]`／改色等具名變體。

**順帶驗證。** 獨立重解 itemdata 後，`t4`/`t8`/`t12` 的非零筆數為
**153 / 7,155 / 1,291**，與 §2d（十七輪）記載的數字**完全相同** ——
這同時反向證明本輪的 997B stride 切段與當年的頭部欄位定義都正確。

## 5d-9. gimmickproperty.xml：地圖機關 ordinal 0..6 → 參照武器

`Extracted/ui/system/gimmickproperty.xml`（已解出，根標籤誤用
`TUTORIALDEFINE`）只有 7 列，卻直接給出 749/750/751/753 這組
`GG_GIMMICK_*` 封包所指的**機關型別表**。XML 自帶 `<!--0-->`..`<!--6-->` 序號：

| ordinal | 機關 | ReferenceWeapon（傷害模型借用的投擲武器） |
|---:|---|---|
| 0 | `AirBomb` | `AIR_BOMB` |
| 1 | `LPG`（瓦斯桶） | `HE_BOMB` |
| 2 | `Barrel`（油桶） | `FIRE_BOMB` |
| 3 | `StreetLamp`（路燈） | `FLASH_BOMB` |
| 4 | `Smoke` | `steam_bomb` |
| 5 | `Heal` | `Hill_BOMB`（原廠拼字，非 Heal_） |
| 6 | `water` | `Liquid_Bomb` |

**ordinal 由讀取順序決定，不是 XML 屬性（Fact / HIGH）。**
native 有具名類別 `GimmickProperties`，其 `sub_9A6810` 以寫死的
`L"system/gimmickproperty.xml"` 載入本檔，`sub_9A6860` 則以
`for (j = 0; j < count; ++j)` 逐筆解析並存成 12 byte 的三元組
`{v12, v10, j}` —— 第三欄**就是迴圈索引**，與 XML 註解的 0..6 完全一致。
存取器 `sub_9A69A0` / `sub_9A6A50` 以 `12 * a2 + base` 定址並做邊界檢查，
證實 stride 為 12 bytes、索引即 ordinal。

**設計意涵。** 地圖機關（油桶、瓦斯桶、路燈…）不各自定義傷害，而是
**借用既有投擲武器的傷害模型**。這解釋了為何 Wiki
[MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細) 描述的
「打爆油桶造成火焰傷害」與 FIRE BOMB 效果相同 —— 它們字面上共用同一個武器條目。

**界線。** 這確立了 749/753 所傳 ordinal 的**字彙**（0..6）與其客戶端視覺／
傷害來源。實際傷害值、誰有權宣告機關損毀、以及 750 的
`s32 s32 u8` 三欄語義，仍需 server／封包證據，維持 UNRESOLVED。

## 5d-10. Tutorial_Data.xml：`type` 欄即武器段選擇器，並二度印證基礎四件組

`Extracted/ui/system/Tutorial_Data.xml`（已解出；native 以
`L"system\\Tutorial_Data.xml"` 寫死路徑載入，且 `TutorialDataPath`／
`TUTO_TECH`／`maxmagazine`／`clearArrow`／`botangle` 都有對應 reader 字串）
描述教學關卡：每個 `<mission>` 有 `delaytime`、玩家 `angle`／`position`、
`<weapon slot type weapon magazine maxmagazine>`，以及 bot 的
`position`／`weapon`／`avata body hair face set hp`。

**`type` 是武器段選擇器（Fact / HIGH）。** 檔內出現的 9 組
`slot`/`type`/`weapon` 三元組，以 `type` 決定要加哪個段基底，**9/9 全部命中**：

| type | 段基底 | 範例 |
|---:|---|---|
| 0 | `12100000` 主武器 | `27`→MP5K、`28`→PSG-1、`24`→P90、`501`→クッションガトリング |
| 1 | `12200000` 副武器 | `26`→USP9、`79`→M202 |
| 2 | `12300000` 近戰 | `4`→CU-BK7 |
| 3 | `12400000` 投擲 | `7`→HE GRENADE、`12`→AIR BOMB |

這是繼 `SpecialWeaponType.xml`（§5d-8）之後**第二個**使用「段內偏移」而非
完整 item id 的資源檔，且此處還多一個 `type` 欄明示要用哪個段 ——
等於資源檔自己把 §5a2 的四武器槽分段**寫了出來**。
（唯一未命中的 `weapon='3001'` 不在四個武器段內，屬另一類 id，維持 UNRESOLVED。）

**基礎四件組的第二條獨立證據。** §5b-4 由 Wiki
[試し撃ちシステム](https://wikiwiki.jp/paperman/試し撃ちシステム) 得知
「未選武器一律回到 MP5K・USP9・CU-BK7・HE GRENADE」，當時只有 itemdata 佐證。
本檔的教學關卡在**完全無關的子系統**中，用同一組
`type0=27 / type1=26 / type2=4 / type3=7` 配置玩家 ——
解出來正是 **MP5K / USP9 / CU-BK7 / HE GRENADE**。
兩個互不相干的 client 子系統選擇同一組基礎裝備，
把「這四件是本 revision 的 base kit」從單一來源提升為**交叉印證的事實**。

**界線不變。** 這仍只證明 **client 端場景配置**。它不證明新帳號 inventory、
不證明 grant、也不證明這四件的擁有權由 server 發放（§5-starter grant
的三來源分離結論維持不變）。

## 5d-11. 武器 UI 儀表 ↔ partsability.pat 引擎欄位對照

Wiki [各種ゲージ詳細](https://wikiwiki.jp/paperman/各種ゲージ詳細)（2013-08-03）
說明武器頁上 6 條可見儀表與 1 個「隱藏屬性」，並自陳是**社群推測、非官方說明**。
把它對到 `cfg/partsability.pat` 的 31 個實際欄位後，推測可以落地：

| Wiki 儀表 | 對應引擎欄位 |
|---|---|
| 攻撃 / DMG | `effective_damage`（另有 `limit_damage` 作遠距衰減） |
| 精度 / A.C | `shoot_Wide`（散佈角） |
| 連射 / C.R | `shot_delay`（射擊間隔，**數值越小越快**，與儀表方向相反） |
| 射程 / O.L | `effective_range` / `limit_range` |
| 反動 / R.C | `recoil` |
| 移動 / M.S | `move_speed`（另有 `miJump`/`miSit`/`miStand`/`miWalk`/`miRun` 姿勢別修正） |
| **初弾命中（無儀表的隱藏屬性）** | `first_shot_wide` / `first_shot_angle` |

最後一列特別有價值：Wiki 明說「グラフ表示のない隠し属性…詳細は不明」，
而資源檔裡確實存在**專屬的首發彈參數**，正好解釋該欄位的存在。
留言區長年爭論的 `R.C` 究竟是 Recoil control 還是 charge，
資源檔給出的名稱就是單純的 **`recoil`**。

`partsability.pat` 另含 Wiki 未提及的欄位：`shots_per_fire`（霰彈一次發數）、
`ballCaseSize`（彈匣）、`damage_repeat`、`fov_level_min/max`（瞄準鏡倍率級距）、
`sniperbackimgidx`/`sniperviewimgidx`（狙擊鏡圖）、`Dot IG`/`Dot TG`（紅點照門）。

**解析方式（Fact / HIGH）。** native 以具名類別
`CPartsAbilityListParamCtrl::Load` 載入 `cfg\partsability.pat`：
先逐字元讀到第一個 CRLF 取得**筆數**（`j__atol`），**接著整行跳過標頭**，
之後按固定欄序解析 —— 也就是**依位置、不依欄名**。
這解釋了為何 31 個欄名中只有 `shot_delay`／`move_speed`／`recoil`
在 exe 中以字串出現（那是別處的 XML 屬性查詢），
其餘欄名在二進位中完全不存在卻仍被正確讀取。
**因此欄位順序本身就是契約，改動 CSV 欄序會直接錯位。**

## 5d-12. face_contents.xml：聊天表情觸發詞（更正舊記「臉型清單」）

舊版 §5d 表把 `Extracted/ui/system/face_contents.xml` 記為
「臉型清單 / 角色創建」——**這是錯的**，本輪已更正。它實際是
**依聊天內容自動切換表情**的關鍵字表：

| 表情 index | 觸發詞數 | 範例 |
|---:|---:|---|
| 1 | 2 | `basic`, `基本`（預設／重置） |
| 2 | 30 | `ｗｗｗ`, `(笑)`, `あはは`, `楽しい`（笑） |
| 3 | 30 | `いやだ`, `怒った`, `(怒)`, `きれた`（怒） |
| 4 | 30 | `悲しい`, `憂鬱`, `凹む`, `ため息`（鬱） |
| 5 | 30 | `泣く`, `(泣)`, `涙`, `；∀；`（泣） |

合計 **122** 個關鍵字。

**證據（Fact / HIGH）。** native 以寫死路徑
`L"system\\face_contents.xml"` 搭配根標籤 `L"facemakelistTable"` 載入，
而**載入類別的名字就是 `CFaceChatScriptProperty`**（face *chat*，非 face make）。
比對函式以 `wcsstr(聊天字串, 關鍵字)` 做**子字串比對**，
命中即切換表情 —— 是**純客戶端的本地呈現**，不經任何封包。
角色創建走的是另一條路徑（`CharMakeProcess.xml`／`charmakebackground.xml`），
與本檔無關。

**界線。** 這完全是 client 端行為，伺服器**不需要也不應該**參與；
聊天封包照原樣轉發即可，不得因表情而改寫內容。

## 5d-13. map\maps\*.ini：出生點與水晶槽（本輪新解，先前誤判為加密檔）

`Extracted/map/maps/*.ini`（7 個）**本來就是明文**，先前被誤當加密檔而解成亂碼。
它們是**每張地圖的出生點表**，由具名解析流程以 `_stricmp` 比對中括號區段：

| 區段 | 結構位移 | 說明 |
|---|---:|---|
| `[FreeForAll]` | `this+2424` | 個人生存出生點 |
| `[TeamSurvival]` | `this+3496` | |
| `[TeamDeath]` | `this+4568` | |
| `[TeamHacking]` | `this+5640` | 爆破 |
| `[TeamSteal]` | `this+6712` | |
| `[Practice]` | `this+7784` | |
| `[CrystalSpawnPoint]` | `this+8856` | **水晶槽**（見下） |

六個模式區段**等距 1072 bytes**，由 `sub_541520` 解析；每筆出生點是
`{ angle <deg>, team <a|b>, origin <x> <y> <z> }` 三元組。

**水晶槽是獨立的列舉表（Fact / HIGH）。** `[CrystalSpawnPoint]` 改由
`sub_541720(this+8856, this+8857, …)` 解析 —— 第一參數是**計數**、
第二參數是**位元組陣列**，內容為每槽一個 token，對照表在 native 中寫死：

| token | 存入值 |
|---|---:|
| `none` | 0 |
| `small` | 1 |
| `large` | 2 |

**槽數與出生點數 1:1 對應。** `TS_14_Stadium` 有 16 個出生點、
`[CrystalSpawnPoint]` 恰有 16 個 token（9 `large` + 7 `small`）；
`TS_40_SlumTown2` 同為 16 對 16（全 `small`）。
其餘 5 張圖（含兩張 `OCC_*`、`TD_33`、`TS_41`、`TS_42`）的
`[CrystalSpawnPoint]` **為空**，與 `PACKETS.md` 把
372–377 `GR_*CRYSTAL_*` 標為「棄用模式」相容 —— 本 revision 只有
兩張圖仍帶水晶資料。

**界線。** 這確立了 372/374/376 所指槽位的**資料來源與型別字彙**，
但水晶的分數、重生時間與擁有權判定仍無 client 可證事實，維持 UNRESOLVED。

## 5d-14. 其餘小型設定檔（本輪補完，含一個方法論教訓）

**`ui/system/GameInOption.ini`**（明文 INI）：

```ini
[TipOption]        ChangeSecond = 5 · CurrentTipCount = 10 · ShowCount = 10
[TimeLimitOption]  GenerateElapsedTime = 10
[SoccerMove]       SoccerMoveData = 137 · SoccerFallSpeedData = 1.7f
```

⚠ 這些鍵名在 `PaperMan.exe.c` 中**完全找不到字串**（`L""` 與 ANSI 皆 0 筆），
`GameInOption.ini` 這個檔名也找不到。因此**無法證明本 revision 會載入它**，
更不能把 `SoccerMoveData = 137` 當成生效中的足球物理參數。
歸類為 **UNRESOLVED / 可能為殘留或由外部工具讀取**。

**`ui/cfg/pm_lobbydata.dat`**（明文 TSV，23,606 B）：首行 `LOBBYMAIN`、
次行貼圖 `L_MR.DDS`，其後為大量 `l t r b` 四元組 —— 是**大廳 UI 的矩形座標表**，
無 native 檔名引用，純版面資料，對伺服器無價值。

**`datarevision.txt`**：根目錄與 `item/`、`character/`、`map/`、`pepachi/`、
`sound/sounds{,01,80}/` 共 **8 個檔案值全部相同 = `811034967`**，
證實整個 `Extracted/` 是**同一次 patch 的一致快照**（§5e 既有記載已複驗）。

### ⚠ 方法論教訓：不要用「開頭位元組」判斷是否加密

本輪發現先前幾輪的批次解密有瑕疵：我用「BOM 或 `<` 開頭」判斷明文，
導致 **21 個本來就是明文**的檔案（7 個 `map/maps/*.ini`、8 個
`datarevision.txt`、`pm_lobbydata.dat`、`GameInOption.ini`、
`ui/cfg/Map.dat`、`QuestAnimation.xml`、`tutorial.xml` 等）被錯誤地
「解密」成亂碼，因而長期被當成無法解讀。

正確判準應為**可列印位元組比例**（取前 512 B，>90% 為明文）。
以此重跑後：明文 222 / 解密 207，**新增 20 個可讀檔、0 個回歸**。
`map/maps/*.ini` 的出生點表（§5d-13）就是這樣才浮現的。

## 5d-15. Total_Package_Index.xml：套裝包 → 成員物品的展開表

`Extracted/ui/system/Total_Package_Index.xml`（已解出）是**商店套裝包的內容表**，
結構為 `total_package[index] → type_1..type_N[index]`：父節點是「包」的 item id，
子節點是該包展開後的成員 item id。

* 檔頭 `<total_idx_count num="114" index="14"/>`；實測 `total_package` 節點
  **恰為 114 個**，與 `num` 相符。
* 父 id 範圍 `15306001..15307095`。以 `dump_itemdata.py` 反查，
  **113/114 能解出名稱**（如 `15306001 アニメパッケージ`、
  `15306003 MP7(Chess) パック`、`15307095 ボイス袋 関西/博多袋`），
  唯一解不出的是 `15306014`。
* 子節點共 **1,596 列**，其中 **282 列為 `0` 佔位**、實際 id **1,314 個**，
  **1,300 個**可在 itemdata 解出（99.0%）；解不出的只有 **14 個相異 id**
  （`15303376..15303385` 等連號一段）。

這條「包 → 成員」關係同時**反向驗證**了 §5c-1 對 `15.30xM` 服務／衍生段的
分段判讀：父 id 落在 15.306M／15.307M，成員 id 幾乎全部落在 15.3M 段內。

**界線不變。** 本表證明**客戶端如何展示一個包的內容**，
不證明價格、購買資格、發放方式或該包在任一時期是否在售
（本 revision 的價格區近乎全零，見 §2c）。
少數解不出的 id 也**不得**視為「不存在」——
它們可能屬於本 revision 未隨附的資料，維持 UNRESOLVED。

## 5d-16. Rocket / Plasma / Laser Property：彈道與爆風的原廠數值表

三個 `ui/*Property.xml` 是**投射物物理與傷害模型**，native 各有具名類別
（`CRocketProperty`、`CLaserProperty` 等）並以寫死檔名載入
（`L"RocketProperty.xml"`／`L"PlasmaProperty.xml"`／`L"LaserProperty.xml"`）。
本專案先前只在 RTTI 清單提過 `CLaserProperty`，資料內容從未解讀。

### RocketProperty.xml — 26 種彈頭行為

以 **26 個具名標籤**（`NONE`/`FIRE`/`WATER`/`WIND`/`GLUE`/`OODUTSU`/`TYPE01`..`TYPE20`）
分類，**非 gunindex**。native 逐筆存成 **14 dword 的記錄**
（`*(this + 14*a3 + N)`），欄位順序即 XML 屬性順序：

| # | 欄位 | 意義 |
|---:|---|---|
| 0 | `commonproperty` | 特殊行為類別（實測只有 `0`×24、`1`×1(`WATER`)、`2`×1(`GLUE`)） |
| 1–2 | `splashRatio` / `splashDist` | 爆風比率／距離 |
| 3–4 | `splashMaxDamage` / `splashDamageRange` | **爆風最大傷害**／衰減範圍 |
| 5–6 | `splashMaxHeight` / `splashHeightRange` | 擊飛高度／範圍 |
| 7–8 | `MaxnuckBack` / `nuckBackRange` | 擊退量／範圍（原廠拼字 `nuck`） |
| 9 | `bulletMoveSpeed` | 彈速 |
| 10–11 | `TailBaseScale` / `TailTransScale` | 拖尾視覺 |
| (+) | `LifeTime` / `ExploredMine` | 額外欄，僅部分型別有（`TYPE05` 為地雷：`LifeTime=10`、`ExploredMine=1`） |

具體數值例：`NONE` 爆風 40 傷害／擊飛 250；`FIRE` 僅 10 傷害但 `splashDist=70`；
`WIND` 擊飛高度 360 而擊退為 0；`TYPE02` 是 `splashMaxDamage=300` 且其餘全 0
（近乎「必殺、無爆風」的特例）。

### PlasmaProperty / LaserProperty — 以 gunindex 定位

兩者改用 **`gunindex`＝武器段內偏移**（與 §2c-3 同一空間）。
以 `dump_itemdata.py` 反查：

* `PlasmaProperty.xml` **42 筆全部命中**，`1404..1407` 皆為 `プラズマガン`。
* `LaserProperty.xml` **45 筆中 39 筆命中**，`1400/1401` 為 `L-1012`（雷射武器）。
  未命中的 6 筆屬本 revision 未隨附的資料，維持 UNRESOLVED。

語義完全自洽：Plasma 表對到電漿槍、Laser 表對到雷射槍。
`LaserProperty.xml` 另有 CP949 韓文註解說明欄位
（`gunindex`＝武器索引、`guntype` 0=基本/1=特殊、中心/中間/外側光束長度與貼圖）。

**界線（重要）。** 這是本專案首次找到成套的**傷害數值**，但它們是
**客戶端的投射物模擬參數**：命中判定、實際扣血與權威結算是否由伺服器覆核，
**沒有任何 client 端證據**。不得據此實作伺服器傷害計算，維持 UNRESOLVED。
`Wiki` 的威力一覧同屬歷史社群量測，兩者相符與否都不構成 service 事實。

## 5d-17. 三個「角色暫時外觀／手感」屬性檔（本輪解讀，並修正先前概括）

`ui/system/` 有三個同族的角色屬性檔，native 各以寫死路徑載入，
根標籤皆可在 `PaperMan.exe.c` 找到：

| 檔案 | 根標籤 | 角色段 | 內容 |
|---|---|---:|---|
| `CharacterFitting.xml` | `CHANGE_AVATAR_PROPERTY` | 13 | 試衣間暫時預覽（僅 `hayate` 的 `bEnable=1`） |
| `CharacterToCooki.xml` | `CHANGE_AVATAR_TO_COOKI_PROPERTY` | **15** | 「餅乾化」變身外觀（全部 `bEnable=1`） |
| `CharacterToPushChar.xml` | `CHARACTER_TO_PUSHCHAR_PROPERTY` | **15** | 被推擠／受擊時的鏡頭晃動參數（全部 `bEnable=1`） |

### CharacterToCooki：15 個角色的變身外觀，item id 全可解

每段是 `handTexture` / `head` / `face` / `set` / `acc1..acc4`。
以 `dump_itemdata.py` 反查，`head` 與 `acc2` **全部解得出名稱**，語義完全自洽：

* `acc2` 依序為 `10760001..10760014`，名稱**全是「クッキーアクセ」**（餅乾飾品）
  —— 與檔名 `ToCooki` 吻合，證實這是一套變身用飾品。
* `handTexture` 依角色順序恰為 **`hand1.tga` … `hand15.tga`**，
  與 `character/textures/` 的 15 張手部貼圖 **1:1 對應**，
  也獨立佐證「角色型別共 15 個」。
* `head` 借用既有髮型 item（如 `10000204 Vカットヘア`、
  `10001780 ひなまつり(2014)ヘア`），而非另造資產。
  devilgirl 與 milly 共用 `10001780` 且 `acc2` 亦重用 `10760003`，
  是本檔唯一的重複組。

### CharacterToPushChar：受擊鏡頭晃動，且 `damage_aim` **有角色差異**

每段四個參數，`damage_aim`／`pos_sin`／`shake_yaw` 三個鍵名都能在 exe 找到 reader。
前三項 15 個角色**完全一致**（`pos_width=20.0f`、`pos_sin=5.0f`、`shake_yaw=5.0f`），
但 **`damage_aim` 依角色不同**：

| `damage_aim` | 角色 |
|---|---|
| `1f`（8 人） | hayate, tina, milly, Cyrus, spygirl, robotgirl, tsunderegirl, devilgirl |
| `0.6f` | Doddon |
| `0.4f` | van |
| `0.3f` | alulu |
| `0.25f`（2 人） | Guy, Tericia |
| `0.2f`（2 人） | hood, magicgirl |

數值越小代表受擊時準心偏移越輕微。這與 §5d-7 的 `convars` 能力值是
**兩套獨立的角色差異化維度**：`convars` 管 defence／movespeed，
本檔管受擊手感。值得注意的是 **devilgirl 在此為 `1f`（無減免）**，
與它在 `convars` 缺席而吃 `defence=0` fallback 的情形方向一致。

### ⚠ 修正 §5d-7 的過度概括

先前我由「convars 缺 devilgirl、CharacterFitting 只到 13」推論出
「越晚加入的角色資源越少」。本輪查完這兩個檔案後**這個通則不成立**：
`CharacterToCooki` 與 `CharacterToPushChar` **都完整含 15 個角色**。
正確說法是 **devilgirl 只缺 `convars` 能力值與 `CharacterFitting` 兩項**。
§5d-7 的表格已改為逐檔案列出，不再作跨檔案的趨勢宣稱。

**界線。** 這三個檔都是**客戶端外觀／鏡頭表現**。變身觸發條件、
誰有權讓角色進入 Cooki 狀態、以及是否經由封包同步，
皆無 client 可證事實，維持 UNRESOLVED。

## 5d-18. 資源覆蓋率：client 要求的 170 個檔，Extracted 提供了 162 個 (95%)

前面各節都是「挑一個檔來讀」。本節反過來問一個**完備性**問題：
**client 到底會載入哪些資源檔？其中有多少是我們手上沒有的？**

`PaperMan.exe.c` 把資料檔名寫成寬字串字面值，因此可以全部列舉。
掃出 **170 個**相異資源檔名（`.xml`/`.pat`/`.dat`/`.ini`/`.txt`/`.lang`），
與 `main` 分支的 71,464 檔完整樹比對後：**162 個有、8 個沒有**。
這 8 個**全部可以解釋**，沒有一個是「不明遺失」：

| 類別 | 檔案 | 說明 |
|---|---|---|
| 執行期產生（3） | `LastChatFilter.ini`、`LastConnect.ini`、`User\lastconnectuserid.txt` | client 自己寫出的本機狀態，本就不會隨安裝檔附帶 |
| 打包容器本身（2） | `Data\pmClient.dat` | **就是裝著上述資源的 pack**，不是它的成員。另一筆 `ata\pmClient.dat` 是反編譯產物（指標遞增比對的副本字串），非真實檔案 |
| 副檔名 fallback（2） | `FilterWord.dat`、`ExceptionWord.dat` | 載入器先組 `.txt` 再組 `.dat`；本 revision 隨附的是 `.txt`（見 §1 表） |
| **真正缺少（1）** | **`ui/CharFittingAnimation.xml`** | 以根標籤 `UICHARFITTINGANIMATION` 載入的試衣間動畫表 |

**唯一真正的缺口具有分析意義。** `CharFittingAnimation.xml` 不在提供的
extraction 中，因此**試衣間動畫子系統無法從現有資料完整還原** ——
這正好與 §5d-17 觀察到的「`CharacterFitting.xml` 只有 13 段、且僅
`hayate` 的 `bEnable=1`」互相呼應：試衣間相關資料本就不完整。
任何關於試衣間的結論都應停在 UNRESOLVED。

**可重跑。** `python3 server-cs/tools/verify_resource_coverage.py`
（需完整 `Extracted/`；工作分支上會自動跳過並說明原因）。
出現未分類的缺檔即失敗，代表 extraction 或 dump 換版，需要重新確認。

**這個數字的用途。** 95% 覆蓋率意味著先前各節「查不到某檔」時，
**九成五的情況是我沒找對地方，而不是檔案不存在** ——
第七輪那次把 21 個明文檔誤判為加密就是典型。
日後若再遇到「這個檔好像沒有」，應先跑本工具確認它是否真的缺席。

## 5d-19. 角色 UI 動畫：15 個 type 全備，但試衣間 pendant 幾乎全缺

`Extracted/character/animations/ui/` 下有 **`type1`..`type15` 共 15 個目錄**
（另有一個 `Angry_Type13`），每個裝 `.pad` 動畫檔。這是繼
`hand1..hand15.tga`、`CharacterToCooki` 的 handTexture 之後，
**第四條獨立證明「角色型別恰為 15 種」**的資源側證據。

### 15 個 type 全部完整（Fact / HIGH）

native 以 **13 個** `.PAD` 字面值指名它要載入的動畫：
`base_29` / `base_69` / `base_full` / `crazy` / `damege1`（原廠拼字）/
`dead` / `defeat` / `escape` / `loop` / `shot` / `uiNormalF` / `uiResultLF` / `win`。

實測 **15 個 type 目錄全部備齊這 13 個檔，無一缺漏**。
其中 13 個目錄另多出 `uibreath.pad` 與 `uiresultrf.pad` 兩個
**native 從未指名**的檔案 —— 只有 `type12`、`type13` 沒有這兩個「多餘」檔，
所以先前看到的「type12/13 只有 13 個檔」**不是缺漏，反而它們才是剛好**。
這兩個額外檔屬未使用的殘留資產。

### 試衣間 pendant：13 個引用，只有 1 個隨附

`CharacterFitting.xml` 的每個角色段都指定
`PendantFolderName`（`Angry_Type1`..`Angry_Type13`，共 **13 個**）與
`SoundFolderName`（`Angry_Voice` / `Voice_angry`）。實測：

* `character/` 下**只有 `Angry_Type13` 這一個目錄**，
  其餘 **12 個 pendant 目錄全部不存在**；
* `sound/` 下 **`Angry_Voice` 與 `Voice_angry` 兩個都不存在**。

這與 §5d-18 找到的唯一真缺檔 `ui/CharFittingAnimation.xml`、
以及 §5d-17 的「`CharacterFitting.xml` 只有 13 段且僅 `hayate` 的
`bEnable=1`」構成**三條互相獨立、方向一致的證據**：
**試衣間（CharacterFitting）子系統在本 extraction 中是殘缺的**，
不可能從現有資料完整還原。相關結論一律維持 UNRESOLVED。

**界線。** 動畫檔本身是客戶端播放資產，與伺服器無關。
本節的價值在於**界定可分析範圍**：角色動畫齊全可信，試衣間則否。

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

## 5d-21. `map/gameobject.dat`：戰場掉落物總表（本輪首解，零殘餘）

此檔**先前完全沒有任何 md 引用過**，但它是 §5b-2「名誉ゲージ」一節裡
「掉落表無 client-side reader」這句話的直接反例 —— **掉落物的目錄確實隨附**
（效果數值與機率仍然沒有，見文末界線）。

### 格式（native `pmFile::possible_ctor_or_dtor_49` @ `0x7FBC40` 逐欄對應）

載入者 `sub_7FBB90` 以 `L"map\\gameobject.dat"` 讀入，**明文、非 pmFile 加密**：

```
[f32 version = 1.0][u32 count = 105]
count × 520B:
  +0    u32  objectId          （小端四位元組 = 下列四個分類位元組，見下）
  +8    128B wchar  modelPath  （`models\item\*.NAO`）
  +136  128B wchar  texturePath（`models/item/*` → 實際檔案為 `.dds`）
  +264  128B wchar  (全 105 筆皆空字串)
  +392  128B wchar  codeName   （`D_Item1111` 等）
```

`8 + 105 × 520 = 54,608` **＝檔案大小，零殘餘**（自證切段）。
native 另以 `hash = hash*33 + ch`（**djb2**，種子 5381）對 modelPath 算雜湊存入
`+16`，並在載入後 `sub_7FD000` 對整份表排序 —— 即這是個**可依 id 查詢的目錄**。

### objectId 的四個位元組就是分類鍵（Fact / HIGH）

實測 `struct.pack('<I', objectId)[::-1]` **完全等於**反編譯中逐一寫入的四個位元組，
即 id 是 `(category, subtype, param2, param3)` 打包而成。105 筆分為五類：

| category | 筆數 | codeName 前綴 | 內容 |
|---:|---:|---|---|
| 1 | 2 | `M_Item` | 金錢／彈藥掉落（`DropItem_02` 貼圖） |
| 2 | 97 | `D_Item`(90) / `Q_Item`(5) / `KNIFE_`(2) | **名誉ゲージ掉落物**＋任務道具＋刀 |
| 3 | 2 | `P_Item` | Pulp'n'Roll 的搬運物 A/B |
| 4 | 2 | `NewTuto` | 教學箭頭指示物 |
| 5 | 2 | `W_Item` | 彈匣（`magazineplus`） |

### 88 個 `D_Item` 構成完整的 7 族 × Lv1–3 矩陣（Fact / HIGH）

`D_ItemABCD` 四位數字與 id 位元組**完全同構**（實測 88/88 無例外）：
`param2 = C×10`、`param3 = A×10 + B`、`D` 恆為 1。
其中 **A = 道具族 1..7、B = 等級 1..3、C = 1..4**（另有 A=0 的四筆，`param3=0`）。

7 族 × 3 級 × 4 個 C 值 = 84，加上 A=0 的 4 筆 = **88**，數量完全閉合。
貼圖亦分三階：族 1–3 用 `DropItem_001`、4–6 用 `DropItem_002`、7 用 `DropItem_003`。

**與 Wiki 的對照結果。** [出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧)
與 [名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ) 都列出**恰好 7 種**掉落物
（武器強化／命中強化／體力回復／投擲武器／迅速移動／無限發射／無敵），
**每種 Lv1–3** —— 與檔案的 7 族 × 3 級**結構完全吻合**。這是繼 skill 三表之後
第三個「Wiki 的分類結構被資源檔證實」的案例。

**但族序號 A 對應哪一種道具，本輪無法確定，維持 UNRESOLVED。**
codeName 與 mesh 名都是純編號（`D_Item1111` / `DropItem_001`），
沒有任何語意字串；exe 全文搜尋也找不到 `D_Item` 字面值（表是以 id 查詢的）。
Wiki 兩張表的欄位順序不一致，不足以定序。
`C`（1..4）的語義同樣**未知** —— 它不是等級（等級是 B），
也不是 Wiki 任何一張表的維度。**不得臆測**。

### 資產覆蓋率：105/105，並再次出現「命名漂移而非缺檔」

宣告的 105 個 `.NAO` **全部存在於 `main` 樹中**（0 缺）。
17 個相異 texture 路徑中有 3 個查無同名 `.dds`
（`star`／`PulpItem_A`／`PulpItem_B`），但 `star` 實際隨附
`Star_1.dds` —— 與 §5b-16 的地圖縮圖同一種**原廠命名漂移**，
不是缺檔。這也再次印證 §5d-18 的方法論：先查是否真的缺席。

### 5 個 `Q_Item` ↔ 8 個 `QuestTerm==19` 任務（Inference / MEDIUM）

`Q_Item0001..0005` 的 mesh 依序是 `Star1`／`Star2`／`goldcard`／`mochi_bomb`／`ghost`。
`Quest.pat` 中 `QuestTerm==19`（§3.12b 已定為「星星事件型」）**恰好 8 條**，
且**只有這 8 條**的 `HonorMedalPosition` 非 0：

| quest idx | TermData | HonorMedalPosition | 任務名 |
|---|---:|---:|---|
| 40001–40006 | 5/6/8/10/15/20 | **1** | 星集める者たち ほか |
| 40007 | 10 | **3** | マネーカード回収 |
| 40008 | 10 | **4** | Happy New Year! |

**語義自洽的部分：** `pos=3` ↔ `Q_Item0003 goldcard`（「マネーカード回収」＝回收金卡）、
`pos=4` ↔ `Q_Item0004 mochi_bomb`（麻糬＝新年，對上「Happy New Year!」）。

**但不能升級為 Fact：** 40001–40006 六條**共用 `pos=1`**，
所以 `HonorMedalPosition` 不可能是 `Q_Item` 序號的一對一映射。
較保守的讀法是它是**勳章圖示槽位**，恰好在兩個節慶任務上與 Q_Item 序號重合。
在找到 native 消費者之前維持 **Inference / MEDIUM**。
（`ui/PopUpMedalOfHonor.xml` 有 `QUESTDESC`/`QUESTNAME`/`QUESTDETAIL` 三欄，
與此相容；但 §5b-18 已確認該檔**在 exe 中查無檔名字串**，故不能用它佐證。）

### 界線

本表是**模型／貼圖目錄**。它證實了「有哪些掉落物、分幾族幾級」，
**但完全不含**：掉落機率、名誉 Lv → 掉落權重、效果數值（如 Wiki 的
「武器強化 Lv3 = 25 秒 200%」）、持續時間、或誰有權生成掉落。
這些仍**無任何 client 證據**，維持 UNRESOLVED，
比照 §5d-16 Rocket/Plasma/Laser 的處理：**可讀出 ≠ 有權威**。

## 5d-22. `ui/tutorial_contents.xml`：教學關卡的**觸發腳本**（本輪首解）

`ui/` 下 15 個 `tutorial*` / `Tutorial*` 檔先前**全無 md 引用**。逐一檢視後，
其中 14 個是純版面（`msprite`/`button`/座標），**只有 `tutorial_contents.xml`
是真正的關卡資料**。本節只寫這一個；其餘 14 個歸類為版面檔（見 §6）。

注意它與 §5d-12 的 `system/Tutorial_Data.xml` **是兩個不同的檔**：
後者給**初始狀態**（出生點、bot、要發哪把槍），本檔給**過關判定**。

### 格式（`CTUTPackage` 三個 parser，Fact / HIGH）

根節點 `TUTORIALDEFINE`，9 個任務節點，各含 `<property>` 與 `<event>`。
`CTUTPackage::sub_78B430` @ `0x78B430` 走訪任務並讀 `index`；
`sub_78B880` @ `0x78B880` 讀 `<property>`；`sub_78BFE0` @ `0x78BFE0` 讀 `<event>`。
欄位與**記憶體位移一一對應**：

| XML | native 寫入位移 | 備註 |
|---|---|---|
| `time` | `+0` (f32) | 讀入後 **`*a2 = *a2 / 1000.0`** → 檔案是**毫秒**，內部存**秒** |
| `startpoint` | `+4` | 4 個 float：`x,y,z,angle` |
| `weapon` | `+20` (s32) | 見下 |
| `numball` | `+24` (s32) | 全 9 筆皆 0 |
| `success` / `fail` | `+28` / `+56` | 字串，全 9 筆為 `SUCCESS` / `FAIL` |
| `<success_triggers>` | `+84` | 最多 `type_1..3`，逐一 `sub_413090` 附加 |
| `<deadzone_triggers>` | `+100` | 同上 |
| `<limit_triggers>` | `+116` | 同上 |
| `<attack_triggers>` | `+132` | **固定讀 3 組** `type_N` + `hp_N`（無條件讀滿三次） |
| `<next_condition>` | `+164` | `type`/`value` |
| `<limit_action>` | `+168` + `88*(i-1)` | **每筆 88 B**，`type`/`value` |

`attack_triggers` 與其他三種的**解析方式不同**：前者無條件讀滿 3 組，
後者以回傳值判斷是否存在才附加。這是 native 層的結構事實，不是檔案內容的巧合。

### 9 個任務 = 4 移動 + 5 攻擊（Fact / HIGH）

`time` 全為 `180000`（＝**3 分鐘**上限），`index` 為 `0,1,2,3` 後跳 `4,14,24,34,44`。

| 節點 | index | `weapon` | comment（原廠拼字） |
|---|---:|---:|---|
| `move_1` | 0 | 0 | `keyboard action.` |
| `move_2` | 1 | 0 | `unforked road action.` |
| `move_3` | 2 | 0 | `duck action.` |
| `move_4` | 3 | 0 | `jump action.` |
| `attack_1` | 4 | 0 | `defalut main gun attack action.` |
| `attack_2` | 14 | 1 | `defalut sub gun attack action.` |
| `attack_3` | 24 | 2 | `nife attack action.` |
| `attack_4` | 34 | 3 | `bomb attack action.` |
| `attack_5` | 44 | 4 | `sniper attack action.` |

**`weapon` 0..3 與 §5d-12 的 `type` 段選擇器語義相同**
（0=主 1=副 2=近戰 3=投擲），但**本檔多出 `weapon=4`＝狙擊**。
這是重要差異：四武器**槽**（§5a2 的 12.1M/12.2M/12.3M/12.4M 四段）
與教學的五個**課程**不是同一個列舉 —— 狙擊槍本身屬主武器段，
在此被獨立成第五課。**不可把 `weapon=4` 當成第五個武器段。**

### 第二個資源檔獨立證實同一組分類（Fact / HIGH）

`ui/tutorial_image.xml` 的結算畫面精靈與上表**恰好 1:1**，且**無多餘、無缺漏**：

```
FINISH_DEF_MOVE / ONLYWAY_MOVE / DUCK_MOVE / JUMP_MOVE      <- move_1..4
FINISH_MAIN / SUB / NEAR / BOMB / SNIPER _WEAPON            <- attack_1..5
```

兩個檔由不同團隊維護（一個是腳本、一個是貼圖切片），
**9 對 9 完全吻合**，因此「4 移動 + 5 攻擊」是設計定案而非偶然。

### 未解：觸發 token 與訊息 id

- `limit/success/deadzone/attack_triggers` 的值形如 `A-1`、`E-5`、`G-8`
  （字母 A–G ＝ 課程分組，數字 1–8 ＝ 用途）。實測**三個 `TU_*.pmm`
  地圖檔中都找不到這些 token 的完整集合**（只有零星位元組巧合），
  故它們**如何綁定到地圖實體，維持 UNRESOLVED**，不臆測。
- `<message>` 共用到 **44 個 id，範圍 101–145、全部相異**。
  這些**不是** `msgtableres.lang` 的 id（該表 101–145 是資料庫錯誤訊息，語義完全不符），
  屬**教學專用的獨立字串命名空間**；本 extraction 未隨附該字串來源，維持 UNRESOLVED。

### 界線

全檔是**單機教學關卡的客戶端腳本**。過關與否由 client 自行判定，
`success`/`fail` 只是字串常數。無任何伺服器欄位，
**不得據此推斷伺服器對教學進度有驗證**。

## 5d-23. `ui/durable_ability.xml`：武器耐久度的**性能衰減曲線**（本輪首解）

這是目前為止**與 Wiki 吻合度最高**的一張表：不只結構，連**啟動門檻的數值**都對得上。

### 格式（`CDurableAbility`，Fact / HIGH）

載入者 `sub_9999E0` @ `0x9999E0` 以 `L"durable_ability.xml"` 讀入（明文）。
`CDurableAbility::sub_999A70` @ `0x999A70` **白名單**六個節點名
（`aiming`／`recoil`／`shotvelocity`／`reload`／`power`／`distance`），
其餘一律忽略；`sub_999EB0` @ `0x999EB0` 逐一讀 `per100..per10` **十欄**
存入 `this + 10*axis + 2..11`，並以 `if (n6 >= 6) return 0` 硬限**六軸**。

**軸索引由呼叫端固定（Fact / HIGH）**：`0x320264` 起的 `switch` 依序以
`0=aiming 1=recoil 2=shotvelocity 3=reload 4=power 5=distance` 呼叫，
與檔案節點順序**一致**。

### 存取函式揭露了真正的語義（Fact / HIGH）

```c
double sub_999A30(float *this, int axis, int n10) {
  if (this == nullptr) return 1.0;      // 表缺失 → 中性值 1.0
  if (n10 < 10) return tbl[axis][n10];  // per100=slot0 … per10=slot9
  return tbl[axis][9];                  // n10>=10 一律夾到 per10
}
```

呼叫端一律傳 `n10 = (1.0f - ratio) * 10.0f`，`ratio` = **剩餘耐久比例**。
因此屬性名 `per<N>` 就是**剩餘耐久百分比**，而回傳值是**衰減量**（非乘數）：

| 耐久 | `(1-r)*10` | 索引 | 取用欄 |
|---:|---:|---:|---|
| 100% | 0.0 | 0 | `per100` |
| 30% | 7.0 | 7 | `per30` |
| **21%** | 7.9 | **7** | **`per30`（＝0，無衰減）** |
| **20%** | 8.0 | **8** | **`per20`（首次非 0）** |
| 19% | 8.1 | 8 | `per20` |
| 10% | 9.0 | 9 | `per10` |
| 0% | 10.0 | 10 | 夾到 `per10` |

### 表值：前八欄全 0，衰減只在最後兩檔

| 軸 | per100…per30 | per20 | per10 |
|---|---|---:|---:|
| `aiming` | 全 **0** | 0.15 | 0.90 |
| `recoil` | 全 **0** | 0.1 | 0.12 |
| `shotvelocity` | 全 **0** | 0.15 | 0.5 |
| `reload` | 全 **0** | **0** | **0** |
| `power` | 全 **0** | 0.3 | 0.9 |
| `distance` | 全 **0** | 0.3 | 0.5 |

### 與 Wiki 的比對：**數值級的吻合**

[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報)（2016-03-19）寫：
「**耐久値が19％の時点から性能低下（威力、精度、連射速度）が始まります**」
「**ただし20％までなら武器の性能は変わりません**」。

- **門檻吻合。** 檔案 `per100..per30` **六軸全為 0**，首個非 0 是 `per20` ——
  即「約 20% 以上無任何衰減」。這是本專案第四次、也是**第一次在「數值門檻」
  層級**證實 Wiki（前三次為 skill 門檻、掉落物分類、錦標賽階段，皆為結構性）。
- **衰減項目吻合。** Wiki 點名「威力、精度、連射速度」三項，
  檔案的 `power`／`aiming`／`shotvelocity` 恰為**非 0 且數值最大**的三軸
  （per10 = 0.9／0.9／0.5），而 `recoil` 僅 0.12、`reload` **恆為 0**。
  換言之 Wiki 只列了「玩家感受得到」的三項，與檔案的權重排序一致。
- **一處 off-by-one，Wiki 略有出入。** Wiki 同時寫「19% 開始」與
  「20% までなら変わらない」，兩句本身互相矛盾。native 的截斷計算給出
  **精確答案：21% 仍取 `per30`（無衰減），20% 起取 `per20`（開始衰減）**。
  所以正確說法是「**20% 起**衰減」，Wiki 的「19%」偏低 1 個百分點，
  「20%までなら変わらない」若解為「>20% 才不變」則正確。
  這類 off-by-one 只有靠 native 的整數截斷才能定案。

### 界線

`sub_999A30` 回傳值只在**客戶端的彈道／傷害顯示計算**中使用。
Wiki 所述的「修理費 50PG / 3CASH」「武器種別基準耐久 C–SS」
「中途退出扣主武器 ~1%／副武器 ~0.5%」**全部無 client 證據**
（耐久值本身由伺服器下發，見 §3.12c 庫存條目的 `u16 dura/dura_max`），
維持 UNRESOLVED。**不得據本表推導伺服器的耐久扣減或修理定價。**

## 5d-24. `ui/commonProperty.xml`：狀態效果參數表（結構已解，**對應關係刻意未定案**）

載入者 `sub_995500` @ `0x995500`；`CCommonProperty::sub_995640` @ `0x995640`
以 `if (n0x1E >= 0x1E) return 0` 限制**最多 30 筆**，每筆 **44 B**
（`this + 11*i + 4..14`），欄位依序
`speed`／`mouse`／`armor`／`time`／`keyboard_reverse`／`mouse_reverse`／
`jump`／`GunFacilityLimit`／`duplicate`(u8)／`senser`／`senser_shadow`。
**容量 30，檔案只給 17 筆** —— 與 §5d-20 的 alpha 迴圈（讀 7 給 6）同一種
「預留容量大於實際資料」的模式，不代表缺檔。

節點名是**序數英文字**（`NONE`/`ONE`/…/`SIXTEEN`），parser **不比對名稱**，
純以出現順序當索引（`sub_995560` 傳 `i`）。因此**節點名只是註解，順序才是鍵**。

**一個看似成立、實測不成立的對應（刻意標為 UNRESOLVED）。**
`ui/UIWeaponEffectIcon.xml` 的 `key='0..21'` 帶有註解名
（`NONE`/`NORMAL_DAMAGE`/`HEAL`/`SPEED_UP`/`SPEED_DOWN`/`FREEZE`/…），
索引 0..16 與本檔筆數對得上，且有兩處強力吻合：

- `index 5` 是**唯一**設 `keyboard_reverse`/`mouse_reverse`(1700) 的一筆 ↔ `FREEZE`
- `index 8` 是**唯一**設 `jump=1000` 且 `speed=mouse=0` 的一筆 ↔ `FIRE`

**但它通不過自己的檢驗**：`index 3` ↔ `SPEED_UP` 的 `speed=400`
**低於** `NONE` 基準 1000（＝變慢），而 `index 4` ↔ `SPEED_DOWN`
的 `speed=2500` **高於**基準（＝變快）—— 兩者**恰好相反**。
若兩檔共用同一列舉，這兩格不可能反向。故**對應關係不成立或 `speed`
並非所設想的方向**，兩種可能都未被排除，**維持 UNRESOLVED，不寫入對照表**。
（記錄此否證，是為了讓後人不必重做這條死路。）

### 界線

本檔僅供客戶端套用狀態效果的移動／視角／護甲參數。
哪個 opcode 指派哪個 index、效果由誰裁決，**無 client 證據**，維持 UNRESOLVED。

## 6. 其他已知資源

- `system/map_StartIndex.xml`, `SelectRandomMap.xml`: 地圖選擇
- `ui/system/AI/*.xml`: AI 模式劇本 (BotWave/BotPath/Scenario)
- `Options.cfg`, `CustomMap.cfg`, `LastConnect.ini`: 本機設定 (非資源)
- `map/gameobject.dat`: **戰場掉落物總表** (105 筆, 明文; 見 §5d-21)
- `ui/durable_ability.xml`: **武器耐久衰減曲線** (6 軸 × per100..per10; 見 §5d-23)
- `ui/commonProperty.xml`: **狀態效果參數表** (17 筆 × 44B, native 容量 30; 見 §5d-24)
- `ui/UIWeaponEffectIcon.xml`: 狀態效果圖示切片 (key 0..21 附註解名)
- `ui/tutorial*.xml` / `Tutorial_*.xml`: 教學版面檔 (15 個中 14 個純 msprite/button;
  唯一含關卡資料的是 `tutorial_contents.xml`, 見 §5d-22)
- `TNMT_*.xml`: 錦標賽 UI 資料
- `occupymode.xml` / `occupyrenewalmode.xml`: 佔領模式參數

## 7. 房/模式 UI 資源互證 (四十二輪 — ui/*.xml 對照房設定簇)

本輪逐檔比對 `main` 分支 `Extracted/ui/*.xml`, 把房設定簇 (121/122、
167–178、340/341、364/365、712/713、990/991) 的語意釘死到控制項:

**gameroom.xml** (房內 UI) 控制項 → 協定對照:
| 控制項 | 語意 | 協定 |
|---|---|---|
| GAMEROOM_SCROLL_MAP | 地圖選擇 | 121/122 (u8 map_id → room+130) |
| GAMEROOM_SCROLL_RULE | 模式選擇 | 169/170 (u8 modeIndex) |
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
  教學/聊天/射擊館/武器試射不走一般建房; 7 由 CHKBTN_CHATROOM 勾出;
  ⚠ 足球=12 **有**在建房下拉內, 另 GAMEROOM_SOCCER checkbox 是獨立的
  房規則旗標 (969/970 → mode rule +14), 兩者不同)
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
(maplist 0..122) — 169/170 改 modeIndex 時 client `sub_426930(modeIndex)` 回推
此值寫 room+130，server 已鏡像 (RoomHandlers.ModeIndexDefaultMap)。

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
| 0x3F8 | 選択されているボイスセットに一括変更します。よろしいですか？ | 語音套件批次變更確認 |
| 0x3F9 | 戦闘/感情ボイスは個別設定ができません。選択したボイスに一括変更しますか？ | 語音 base 語音批次變更確認 |
| 0x3FA | 選択したボイスに変更しますか？ | 語音變更確認 |
| 0x3FB | ボイスカスタマイズ設定保存に失敗しました… | 795 儲存失敗 (796 回退告警) |

**好友聊天/位置/喊話簇 (本輪 439-442/836-837 精讀佐證)**:

| id | 文字 | 出處 |
|---|---|---|
| 0x1EF | %s というキャラクター名は存在しません | 440 status 0 (好友聊天名稱不存在) |
| 0x1F0 | %s さんはオフラインです | 440 status 1 (離線) |
| 0x1D9 | ← %s さんのコメント | 440 status 2 (訊息本文前綴) |
| 0x1D8 | %s さんを見つけることが出来ませんでした | 440 status 3 (找不到) |
| 0x21E | %sさんはロビーで待機中です | 442 status 1 大廳待機 |
| 0x21D | %sさんの情報が見つかりませんでした。\r\nリトライしてください | 442 status 2 (找不到資訊) |
| 0x314 | %sさんはチュートリアル中です | 442 where_type 11 (教學中) |
| 0x3AF | 対象者がトーナメントに参加しています…「一緒にプレイ」不可 | 442 status 2 同發提示音 |

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

---

## 9. 客戶端字串解密與 UI 槽位/改裝/倉庫定名對照 (五十四輪更新)

五十四輪對 `PaperMan.exe.c` 進行了反編譯字串修復，揭露了大量先前為 `&off_XXXXXX` 偏移的 UI 標籤、技能槽、改裝件與倉庫頁籤字串：

### 9.1 sub_527550 的 9 個技能/能力槽（Ability Slots）正式名稱
對應 `sub_4C4990` 與 `sub_4C4E70` 的 9 個槽位陣列：
1. `Crosshair` (0): 準心自訂
2. `NAME` (1): 名稱/暱稱卡
3. `MASTER` (2): 大師/稱號
4. `ABILITY` (3): 主能力
5. `BOOST_EXP` (4): 經驗值加成
6. `BOOST_PG` (5): PG (Game Point) 加成
7. `EXTRA_ABILITY` (6): 額外能力 1
8. `EXTRA_ABILITY` (7): 額外能力 2
9. `VOICE` (8): 角色語音槽

### 9.2 武器零件改裝槽（Weapon Parts Slots）與標記
對應 `sub_4C50A0`、`sub_95B180`：
- 改裝槽位：`PARTS_01` 至 `PARTS_07` (7 個改裝槽)
- 零件設定：`PARTS_SET_%d`, `PARTS_SET_MOUSE_%d`, `PARTS_SET_EMPTY_%d`
- 操作按鈕：`PARTS_EQUIP`, `PARTS_CLEAR`
- 狀態標籤：`COUPON_MARK`, `ONLY_NETCAFE_MARK`, `RECYCLE_OUTLINE`, `PARTS_WAITING`

### 9.3 倉庫頁籤（Warehouse Tabs）
對應 `sub_4F0240`：
- 頁籤 ID：`WAREHOUSE_1` 至 `WAREHOUSE_6`
- 格式字串：`L"WAREHOUSE_%d"`、`L"WARE_TAB_%d"`

