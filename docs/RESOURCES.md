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

**➡ 如果你手上有 PaperMan 客戶端資料夾, 最想要的是:
`Data\pmClient.dat`(或已解出的 `cfg\ItemData.pat` + `cfg\Quest.pat` +
`cfg\maplist.pat`)。給我這幾個檔就能把 item_catalog / quest_catalog
從「種子示意」升級成完整真實目錄。**

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

## 3. data.pat 容器格式 (@225227 — 已完整破解, 可直接寫解包器)

```
[u32 size_1 (混淆)] [body...]
1. size_1 = ROL32(讀入的前4B, 9) ^ 0x975E   → 解壓後大小
2. body 逐 byte 解混淆 (i 從 len..1 遞減):
     plain[k] = i ^ ROL8(cipher[k], 3)
3. zlib 1.2.3 uncompress → 得 size_1 bytes
4. 尾 4B = ~CRC32(前面內容) 校驗 (查表 dword_AFBF28, 多項式標準)
```

## 4. pmClient.dat (pmFile 打包系統)

- `pmFile::ctor(path, mode)` → sub_A5BFC0 開檔;
  `ctor_39(name)` = 讀單一子檔進記憶體
- 子檔內容經 **per-byte 滾動 XOR + 位移** 解密 (sub_7117D0):
  ```
  for i = size-1 .. 0:
      out[i] = ROL8(in[i], i) ^ state
      state = ((state ^ 0xFA5387AD) & 0x0F3A94AA)
              ^ ((i | state) + 0x48945B4A) ^ 0x1A68DCCF
  state 初值 = size
  ```
  (加密端 sub_711720 = 先 XOR 再 ROR — 互逆已對照)
- cfg\*.pat 全部走這條路徑 (sub_717E50 組路徑 → pmFile 讀取)

## 5. .pat 文字/二進位雙軌 (convars)

`sub_712D40("convars.txt", "convars.pat")`: 有 .txt 用 .txt (開發模式),
否則用加密的 .pat — 表示 .pat 內容就是對應 .txt 的加密版。
FilterWord / ExceptionWord / soundprops 同模式。

## 6. 其他已知資源

- `system/map_StartIndex.xml`, `SelectRandomMap.xml`: 地圖選擇
- `ui/system/AI/*.xml`: AI 模式劇本 (BotWave/BotPath/Scenario)
- `Options.cfg`, `CustomMap.cfg`, `LastConnect.ini`: 本機設定 (非資源)
- `TNMT_*.xml`: 錦標賽 UI 資料
- `occupymode.xml` / `occupyrenewalmode.xml`: 佔領模式參數
