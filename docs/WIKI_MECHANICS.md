# PaperMan Wiki* 機制研究帳本（非協定權威）

> **用途。** 這是對 [PaperMan - ペーパーマン - Wiki*](https://wikiwiki.jp/paperman/)
> 的定向閱讀索引與「待交叉驗證」矩陣，於 2026-09-15 建立。它讓後續的
> `PaperMan.exe.c`、封包、實際封包擷取和 `main:Extracted/` 資源分析知道該找
> 什麼，**不是**用來補造原版 service 規則、價格、掉落率、預設裝備或 response。
>
> Wiki 是社群維護的歷史資料，頁面涵蓋的時間跨度很長，且有明確的改版、活動、
> 已停止更新的內容。因此本文件的「Wiki 觀察」最多是歷史性事實；沒有 native
> reader/writer、resource、或封包證據時，server 行為一律是 **UNRESOLVED**。
> 特別是「目前」或「當時」等頁面措辭，不可被投射到所提供的日版 client revision。

## 1. 證據標籤與採用門檻

| 標籤 | 意義 | 可以直接用於 server 嗎？ |
|---|---|---|
| **Wiki 觀察** | 此 Wiki 頁面明確敘述的歷史遊戲內容。 | 不可；僅是搜尋與互證線索。 |
| **Native fact** | `PaperMan.exe.c` 的可達 reader/writer/consumer 或所提供資源直接證明。 | 僅在 field、route、或純客戶端行為範圍內採用。 |
| **Inference** | 至少兩條相容證據導出的有限結論。 | 需把政策與 wire 分開，且保留反證/不確定性。 |
| **UNRESOLVED** | 所有已搜尋證據仍不足以分辨候選解釋。 | 不可虛構 ACK、資產、扣款或 mutation；維持 fail-closed。 |

採用任何 Wiki 線索前，至少完成下列資料流：

```text
Wiki observation → native sender / native receiver → field layout
                 → client state gate / consumer → Extracted ID or data table
                 → original-service evidence (or explicitly retained UNRESOLVED)
```

`ItemData.pat` 的類別或一個 UI 名稱，只能證實資源存在，不能單獨證實該物可購、
可送禮、可回收、預設持有、或在任何特定版本的 pool 中。價格尤其不能以 Wiki
價目表回填：本 revision 的 `itemdata.pat` 價格區幾乎全為零，詳見
[`RESOURCES.md` §2c](RESOURCES.md#2c-itemdata-pat-尾部-721b-完整切段-十六輪21164-條統計錨點定位)。

## 2. 本輪已瀏覽的主題與可確定的分界

| 主題 | Wiki 觀察（歷史、非 service fact） | 已有 native/resource 交叉點 | 不可越過的界線 |
|---|---|---|---|
| 普通商店 | [通常ショップ武器一覧](https://wikiwiki.jp/paperman/通常ショップ武器一覧) 將主武器按 SG/SMG/AR/SR/LMG/特殊武器列出，並按 1/7/30 日與無限期價格列舉；頁面稱 PG 有等級限制、CASH 沒有，受贈者也受限制。 | `itemdata.pat` 可分辨物品與大量 weapon item；native loadout 明確分為 primary/secondary/melee/throw 四域。 | 不以這些歷史價格或等級限制實作 204/205、206/207、208/209 等購物交易。 |
| Pepachi | [ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細)、[CASH](https://wikiwiki.jp/paperman/ペーパチCASH詳細)、[PG](https://wikiwiki.jp/paperman/ペーパチPG詳細) 區分 CASH/PG 機台、各自 pool 與輪替；11 抽價格在歷史頁面中另列。 | `pepachi` UI 資產與獨立 opcode family 僅證明 client 有分離畫面/通道。 | 不能由動畫、稀有度、歷史 pool 或單抽/11 抽價格衍生 RNG、保底、餘額或 award packet。 |
| 福袋／選擇式包 | [福袋詳細](https://wikiwiki.jp/paperman/福袋詳細) 將 skin、select、soul、Mebius、weapon、costume、voice 等不同袋型分開，並描述選袋→角色、5/10 個批購等歷史變化。 | 204 與 468 有 native 同形 request、以 Hukubukuro ID range 路由的事實；470/780 是帶 item/negative variant 的 picker boundary。 | 不把所有袋當普通 item；range 只可用於目前的 route/framing guard，不能證明池、選擇權、批量、grant 或價格。 |
| ペーパーガッチャン／ペーパダス | [ペーパーガッチャン詳細](https://wikiwiki.jp/paperman/ペーパーガッチャン詳細) 是歷史上的獨立 rerun gacha/點數交換活動；[ペーパダスEX詳細](https://wikiwiki.jp/paperman/ペーパダスEX詳細) 更把 match-point、銀/金/EX 機台、票券、每日計數器與獎池區分。 | client 存在不同 UI assets；已知 shop/capsule request 仍未取得完整權威 service trace。 | 不把它們合併成 Pepachi，也不可執行 point/counter/獎池邏輯。 |
| package、serial、present | [パッケージ詳細](https://wikiwiki.jp/paperman/パッケージ詳細) 列出包含 title、武器、support 的組合包；[シリアルコード詳細](https://wikiwiki.jp/paperman/シリアルコード詳細) 顯示 serial 曾對應實體包、活動與特典。 | 463 是 exact-empty 的本地 activation gate；298/299、300/301、314/315、453/454 的 client 消費面僅部分已復原。 | 僅有「外部序號會送物」的歷史背景，不足以決定 code verification、重複兌換、present lifecycle 或 454 成功。 |
| recycle／耐久 | [リサイクルシステム](https://wikiwiki.jp/paperman/リサイクルシステム) 描述 CP 作為另一種交換經濟、可回收/不可回收類別與歷史換算；[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報) 描述無限期武器損耗/修理。 | 資源有服務項目段與武器項目；現有 storage/packet evidence 尚未構成回收或修理的完整交易。 | 不從價格、item name、持有期限、或 Wiki 的 CP 比率推導「可刪 inventory」或資產變動。 |
| Paper Slot | [ペーパースロット詳細](https://wikiwiki.jp/paperman/ペーパースロット詳細) 將 support、crosshair、特殊能力三類列開：改名／戰績重置／shout、EXP/PG boosts、switch weapon、crosshair、彈藥/耐久/respawn/PvE/攻防 buffs 等。 | `sub_527550` 的 9 個 persistent UI-item ordinal、457 request 與 458 full 63-byte reader 已確立；`RESOURCES.md` §5c-2 有精確 range。 | slot 的「用途名稱」不等於能隨意裝任何 range item；458 尾端三筆 record 的 service semantics 仍未明，不能用零填補。 |
| title、voice | [称号一覧](https://wikiwiki.jp/paperman/称号一覧) 顯示 title 可來自 login、quest、活動、clan 等來源；[ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧) 區分 base／voice 套件與 Z/X/V 的各 9 個指揮、戰術、報告字串。 | Title = `15304xxx` resource segment；voice customize 的 15 characters × 92 contexts × 27 lines、791–796 UI/wire source 已在 `RESOURCES.md` §5d。 | title/voice 是裝備、解鎖與發話三個可能不同域；不可從範例來源推斷 ownership、slot-8 range 意義、或 795 成功回覆。 |
| Puzzle／skills | [スキル一覧](https://wikiwiki.jp/paperman/スキル一覧) 說明 迅/敏/根/防/集五軸、5-point threshold、multiple skills、No Skill room gate；[ペーパズル/合成](https://wikiwiki.jp/paperman/ペーパズル/合成) 與 [リスト](https://wikiwiki.jp/paperman/ペーパズル/リスト) 描述素材、rarity、body slot 與歷史合成規則。 | 255 的 5 profiles × 7 IDs、選擇 profile consumer，與 198 的 12 normal appearance words 已被 native trace 分離。 | Wiki 的合成公式與數值效果不是 client/service 權威；不得把 PAV 外觀、fitting preview、推薦套裝、或 puzzle profile 當成新角的 default appearance。 |
| 角色／avatar | [キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧) 將早期四角（Hayate/Tina/Guy/Tericia）標成 initial choices，並歷史性地列角色能力/售價；各角色 avatar 頁把物品按 body part 列出。 | 15 body IDs、五個 native template maps、6-word canonical normal appearance prefix，以及 12-slot record 已在 `RESOURCES.md` §5c-1 由 client+PAV 互證。 | 「initial choice」不證明該 client 只可選四人，也不證明 starter weapons、boost、voice、last-six appearance slots 或 persistent inventory。 |

### Starter account grant 與 character appearance 必須分離

**Wiki 觀察。** 2021-03-27 的 [ひよこ用/ゲーム起動編](https://wikiwiki.jp/paperman/ひよこ用/ゲーム起動編)
把 MP5K 稱為「registration 時持有的 initial weapon」，且將 tutorial／level-up 的武器、
PG、boost、Puzzle pack 描述為要從 My Character 的 Present 介面領取；同頁也只說命名後
會進入 character selection。2017-03-30 的 [階級関連](https://wikiwiki.jp/paperman/階級関連)
列出 level 2 以後的歷史性 award。這些資訊可作為三個不同候選來源的提醒：

1. account-registration starter inventory；
2. character creation 的 normal-appearance template；
3. tutorial／level-up／operation present grant。

它們不可合併成「每個新 character 預設裝著這些武器」。已知 native 只直接證明第二項的
六個 appearance values；目前語料沒有 direct creator、period、inventory row、loadout group
或 present ACK 的可重現證據來確認第一／三項是否存在於此 revision。因此 MP5K、任何 Wiki
award，以及同頁的初期角色圖片都仍是 **UNRESOLVED** service policy，不能直接寫入 bootstrap
inventory 或 character weapon fields。

#### MP5K：已找到的 resource candidate，仍非 grant 證明

對 `main:Extracted/ui/cfg/itemdata.pat` 以 native `pmFile` 解密法完整解析（header
`version=1,count=21164`，stream 無剩餘）後，ID `12100027` 的 display name 是 `MP5K`。
它的三個頭部 reference words 都是零，`req_level=0`，其原始 `kind` byte 是 `6`；同一 ID
family 中可一般購買的 MP5 SD6、M3、G36 等 sample 都是 raw kind `9`，且第一個 raw
parameter word 會有像 17,000 或 32,000 的數值，MP5K 該位置為零。這些是 **resource facts /
HIGH**，不是對 raw kind、零值、可購性或初期授與語意的命名。

`main` 也含有 `mp5k` 的 FPV model/sound/animation，及 type `1..15` 全部的 TPV mp5k
animations。這是 **resource fact / HIGH**：本 revision 有可播放的 MP5K content，且資產並
不只附屬於某一個 character。完整 `PaperMan.exe.c` 文字搜尋沒有 `12100027` literal；已知
character template 和 weapon-group materialization trace 也沒有把它寫為 default。這個負面結果
僅限現有 decompile/搜尋方法，不能排除 retired server 在 registration 或 present 畫面授與它。

結論：Wiki 的「MP5K 是 initial weapon」和此 item/resource candidate 相容，但未形成
`registration → inventory → loadout → response` 證據鏈；其 ID、無期限或任何 ownership policy
都不可寫進 server。這也是 resource existence 與 persistent default 必須分離的反例。

## 3. 對戰系統：歷史模型與 native mode 編號的對照

下表用 Wiki 理解 gameplay，但 **mode number、地圖 bit 和建房 availability 只以 native/resource
資料為準**。權威 0..15 枚舉與 map filter 在 [`RESOURCES.md` §4b](RESOURCES.md#4b-cfgmaplistpat-格式-十五輪以真實檔案實測修正)；房間控制項與 packet cluster 在 §7。

| Native mode | 名稱 | Wiki 觀察（可供後續 simulator/consumer 對照） | 需要補的非 Wiki 證據 |
|---:|---|---|---|
| 0 | TeamMatch | 基本兩隊擊殺賽；規則與結算不能由一般 FPS 習慣補全。 | 逐一復原 match start/end、kill、score、reconnect packets。 |
| 1 | IndividualSurvival | [MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細) 稱高 kill 或時間結束決勝，且有死亡後短暫無敵等歷史描述。 | spawn、無敵時間、kill-score 與配置必須由戰鬥 state/UDP trace 確認。 |
| 2 | DefuseBomb | Wiki 描述交替攻/守、plant/defuse/bomb timing 與 round win。 | objective state machine、時間單位、誰擁有 bomb、斷線和 award packet。 |
| 3 | TeamSurvival | Wiki 描述 target kills/timeout，及 historical draw handling。 | 不能將 bug/特定 server 的 draw/自殺規則複製到本 client。 |
| 4 | Steal | Wiki 將其比為染料運輸；雙方數值變動與冷卻勝利。 | item carrier、score mutation、drop/return 的 client messaging。 |
| 5 | Practice | Wiki 稱 1 人可開始、戰績/耐久不變、0 reward。 | client state gate、服務端戰績抑制與 reward path；不要只因 mode 名稱關閉所有 persistence。 |
| 6 | Tutorial | 專用入口/專用地圖的歷史背景。 | tutorial progression、reward、mode entry wire。 |
| 7 | ChattingRoom | Wiki 稱無武器且 TPS 的聊天專用房。 | room override、loadout suppression、chat packet behaviour。 |
| 8 | Pulp'n'Roll | Wiki 說攻守輪替、pulp 運送/破壞和 temporary frenzy，與標準 deathmatch 顯著不同。 | carrier/frenzy timers、weapon override、score/EXP authority。 |
| 9 | GunShooting | UI 和 resource 明確另有 shooting-gallery content。 | wave/machine/mode routing 與 reward。 |
| 10 | Occupy | Wiki 描述舊式 A/B/C sequential objective 與攻守雙半場。 | 2014 replacement 前後的 client revision match，不能混用 mode 10/13。 |
| 11 | AIMulti (PvE) | [PvEモード](https://wikiwiki.jp/paperman/PvEモード) 描述 1–4 人、防 shield、wave/boss、難度與隊內 reward selection。 | `AI/*` resource scripts、AI spawn protocol、score ordering、award/present flow。 |
| 12 | TeamSoccer | Wiki 顯示 goal target、knife/item/No Skill 等特殊 room options。 | ball ownership/goal/state event wire。 |
| 13 | OccupyRenewal | Wiki 的 new占領稱三點持續佔領、score-per-second 和被全佔時 respawn modifier。 | objective tick、respawn and score authority；不可回填舊 mode 10 規則。 |
| 15 | WeaponTest | client resource 對應 `ECT` / weapon test。 | entry eligibility、temporary equipment、退出與持久化界線。 |

所有 Wiki 的「可選回合／分鐘／kill／goal」表僅提示需要檢查 `roommake.xml` 的選項值、
111/112/121–178/340–341/712–713/969–970/990–991 的 serializer 和 mode factory；
不等於原服務一定接受每個列出的值。

## 4. 戰鬥數值與掉落：為何目前只能列為驗證問題

以下閱讀結果很有用，因為它推翻了「戰鬥只是擊殺／死亡」或「任何裝飾物都純 cosmetic」
的簡化模型；但沒有一項可直接生成 server state。

| 系統 | Wiki 觀察 | 後續要驗證的 native/resource 問題 |
|---|---|---|
| 角色差異 | 角色列表歷史上列防禦率、移動速度與 demolition 值；[武器移動速度](https://wikiwiki.jp/paperman/武器移動速度) 說實際速度由持有武器、角色、skill、道具共同作用。 | 從 character record、weapon stats、skill result 和 movement event 精確找出運算順序。禁止將 Wiki 百分比寫死。 |
| Skill | 5 軸的正／負門檻，No Skill gate，以及由 Paper Puzzle 組合啟動。 | 255/466/467 的 ownership、profile selection、expiry 和 battle-spawn payload；檢驗是否 client authoritative 或 server checked。 |
| weapon parts | [武器パーツアップシステム](https://wikiwiki.jp/paperman/武器パーツアップシステム) 強調 main weapons only、依槍/slot 的 compatibility，且 visual ratings 不等於真實效果。 | 已有 10,648 compatibility edges 和 eight-part group mapping；仍需戰鬥數值 layer 與 parts purchase entitlement。 |
| drop／honour | [出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧) 與 [名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ) 把 item-mode drops、kill/death reset、rare/drop effects 分開。 | drop RNG、honour level、pickup effect/timer，及其是否走 UDP/private packet；不要由 Wiki 時間/效果製造 event。 |
| assist | [アシストポイント機能](https://wikiwiki.jp/paperman/アシストポイント機能) 區分傷害、air, healing 和各 mode objective 之 assist。 | damage attribution window、threshold、battle result aggregation、account-stat writer。 |
| PvE | wave/boss、shield gauge、ammunition boxes、score-order reward selection。 | `AI/AiMultiCompensation.xml`、BotWave/Scenario、PvE game state packets 和 present award transaction。 |

## 5. 商店／持久化工作項目矩陣

「已看過」並不代表「已實作」。此矩陣按產生不可逆 mutation 的風險排序，後續完成一列
前必須保存 direct provenance（function、field order、consumer、resource ID/version）並找反證。

| 優先 | 流程 | 現有結論 | 下一個必須取得的證據 |
|---:|---|---|---|
| 1 | ordinary/cash/once/parts purchase | request boundaries有多個已確認，但 ItemData 價格不可用，且無 original success policy。 | 每種 direct builder→ACK consumer→inventory/account mutation；server price/catalog source；拒絕 path。 |
| 2 | gift/present/package/serial | client cache effects只局部恢復；Wiki 證明歷史上有多種送物來源。 | 298/299、300/301、314/315、453/454 的 producer/consumer 全路徑及 persistent lifecycle。 |
| 3 | 福袋/hidden/recommend picker | 204/468、470/780 的 frame/range route 已確定，推薦 XML 是 presentation。 | chosen item provenance、bag entitlement、pool snapshot/expiry、multi-buy response。 |
| 4 | Pepachi/gacha/point systems | distinct historical systems，服務 policy 未復原。 | spin request/ACK full wire，RNG/pool/duplicate/point bookkeeping，失敗和重試。 |
| 5 | sell/destroy/recycle/durability repair | Wiki 顯示有多種不可逆經濟行為；native full service route 尚缺。 | exact request grammar、eligibility、value/currency account update、failure ACK、atomic transaction. |
| 6 | title/boost/ability/crosshair/voice/puzzle | 多個持久化域已在 client 資源分離；部分 response 固定長度但 semantic 不足。 | item entitlement、slot exclusivity、expiry、battle application，以及 full response producer traces。 |

在任何一項有足夠證據前，direct Shop request-family handlers 的可驗 frame 只回已知的客戶端安全失敗臂，
或保持 no-ACK；它**不得**扣款、發物、刪 present，或虛構 award/cache refresh。

## 5b. Wiki × 反編譯 × Extracted 三方對照（逐輪紀錄）

> 本節每一項都先有 Wiki 觀察，再獨立以 `PaperMan.exe.c` 或 `Extracted/` 驗證。
> 凡只有 Wiki 單方敘述者一律留在 UNRESOLVED，不寫進本節。

### 5b-0. 逐輪索引（三十輪索引，本輪整理）

§5b-1..5b-5 為主題式（第二輪），5b-6 起依輪次遞增。

| § | 輪 | 主題 | 本輪最該記住的一句 |
|---|---:|---|---|
| 5b-1 | 2 | Assist 事由碼 | wire 值已定，服務端計分仍 UNRESOLVED |
| 5b-2 | 2 | 名誉ゲージ | 9 級為 native 定值（掉落**表**已於 5b-20 更正） |
| 5b-3 | 2 | Clan rank | wire → S/A/B/C 對照確定 |
| 5b-4 | 2 | 試し撃ち 裝備 | 四個確切 item ID |
| 5b-5 | 2 | 刻意不採用 | 房間欄位／経験値表 |
| 5b-6..5b-8 | 3–5 | 三方對照、角色/武器、文件自稽 | 以三來源反查 md |
| 5b-9..5b-13 | 6–10 | Pepachi、出生點、武器偏移、傷害、角色維度 | 一次宣稱被複驗推翻 |
| 5b-14 | 11 | **方法論轉向** | 改問「完備性」而非「再讀一個檔」 |
| 5b-15..5b-16 | 12–13 | 角色動畫、地圖縮圖 | 命名漂移 ≠ 缺檔 |
| 5b-17..5b-18 | 14 | 抽獎前置 gate | PG 需 Lv10 與 Wiki 吻合 |
| 5b-19 | 15 | skill 三表 | 符號相反 ≠ 矛盾 |
| 5b-20 | 16 | 掉落物總表 | **收窄過寬的 UNRESOLVED** |
| 5b-21 | 17 | 錦標賽狀態機 | 入場 10 ≠ 報名 5 |
| 5b-22 | 18 | 教學腳本 | Wiki **缺頁**，全靠 native |
| 5b-23 | 19 | 耐久衰減 | 修掉 Wiki 的 off-by-one（19%→20%） |
| 5b-24 | 20 | PvE 難度表 | 資料／程式不一致（`siege_dmg_rate`） |
| 5b-25 | 21 | BotEnemy | 死欄位 `scale`、死檔 `_intelligent` |
| 5b-26 | 22 | URLList | exe 內**零個 URL 字面值** |
| 5b-27 | 23 | AI 獎勵表 | **檔名會騙人**（不是 PvE） |
| 5b-28 | 24 | ScoreRatio | **程式分支 ≠ 資料分歧** |
| 5b-29 | 25 | 移動速度 | 只有槍托改速度；缺口明確化 |
| 5b-30 | 26 | convar 全表 | 註冊了卻沒隨附的 `um_gr_maxspeed` |
| 5b-31 | 27 | 位移表 | **欄序契約非線性**（兩對交換） |
| 5b-32 | 28 | 語音商品 | 把一句 UNRESOLVED 拆成兩半 |
| 5b-33 | 29 | 稱號鏈 | Wiki 的「疑似筆誤」被逐字證實 |
| 5b-34 | 30 | 無線對話 | 1,044 檔排成 3×9 按鍵表 |
| 5b-35 | 31 | 任務分類 | 資料列**自己寫著**分類名；第四次缺 ルーシー |

**跨輪累積的通則**（各輪獨立觀察到、彼此印證）：

1. **資源檔普遍帶有 parser 不讀的欄位**，且多位於記錄末尾 ——
   五類：`siege_dmg_rate`(5b-24)／`scale`(5b-25)／`name`(5b-26)／
   `periodType`(5b-27)／partsability 末四欄(5b-31)。
2. **鍵是位置或數字 index，人類可讀的名字只是註解**（5b-25/26/27）。
3. **資源缺席 = 跑硬編碼 fallback，不等於功能不存在**（5d-7／5b-30／5b-34）。
4. **Wiki 對單人／PvE 內容系統性缺頁**（5b-22/24/25 共三例），
   故不可用 Wiki 當覆蓋率指標。

### 5b-1. Assist 事由碼：wire 值已定, 服務端計分規則仍 UNRESOLVED

**Wiki 觀察。** [アシストポイント機能](https://wikiwiki.jp/paperman/アシストポイント機能)
（2014-09-17 實裝、頁面 last-modified 2015-02-22）列出可獲得 assist 的行為，並區分
「練習／PVE／單人模式除外」，另記 爆破解除者 2 點、隊友 1 點、占領參與者 2 點、隊友 1 點。

**Native fact / HIGH。** `sub_6750B0`（`PaperMan.exe.c` 約 315689 行起）以
`*(v123 + 110)` 作 switch，且外層以 `!= 0 && < 0x6Du` 夾住取值範圍，逐一對應到
UI 字串。這把 Wiki 的文字類別變成**精確的數值編碼**：

| 事由碼 | UI 字串 | Wiki 對應行為 |
|---|---|---|
| `1` | `ASSIST_DAMAGE` | 造成 ≥55% 最大 HP 傷害後由他人補刀 |
| `2` | `ASSIST_AIRSHOT` | 投擲浮空後隊友 air-shot 擊殺 |
| `3` | `ASSIST_HP` | 治療隊友 ≥20% 最大 HP |
| `101` (`0x65`) | `ASSIST_BOMB_PLANT` | 設置炸彈 |
| `102` (`0x66`) | `ASSIST_BOMB_EXPLO` | 爆破成功 |
| `103` (`0x67`) | `ASSIST_BOMB_DESTROY` | 拆彈成功 |
| `104` (`0x68`) | `ASSIST_DYE` | スチールモード 染料運送成功 |
| `105` (`0x69`) | `ASSIST_PULP` | パルプ＆ロール 運送成功 |
| `106` (`0x6A`) | `ASSIST_PULP_DESTROY` | パルプ＆ロール 解體成功 |
| `107` (`0x6B`) | `ASSIST_OCCUPY` | 占領據點 |
| `108` (`0x6C`) | `ASSIST_GOAL` | 足球進球 |

注意編碼**不連續**：1–3 是「跨模式通用」事由，101 起才是模式專屬事由，正好對上 Wiki
把「全モード」與各模式分節書寫的結構。上界 `< 0x6D` 表示 108 是本 revision 的最後一個
合法值。

**界線。** 這些只證明 `994 GG_ASSISTPOINT_NOTIFY` 攜帶的事由碼字彙與顯示層行為。
Wiki 的點數值（2 點／1 點）、55%／20% 門檻、「治療 50% 以上重置 assist 狀態」等
**是歷史服務規則，不是 client 可證事實**；本 revision 的 client 只顯示事由，不自行計分。
在取得原服 trace 前不得實作 994 的發送或任何 PG/EXP 結算。

### 5b-2. 名誉ゲージ：9 級為 native 定值

**Wiki 觀察。** [名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ) 稱 gauge 越高、
戰鬥不能時掉落的道具品質越好，但明言「運氣影響很大」。
[出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧) 另記掉落物分
Lv1–3、僅「アイテム戦」勾選時出現、且需該玩家至少 1 kill。

**Native fact / HIGH。** 繪製 gauge 的函式以 `L"HonorGauge%d"` 組出貼圖名，
並有 `if ( n9 == 9 && timeGetTime() % 0x2BC < 0x190 )` 一段：**等級上限就是 9**，
且第 9 級每 700ms 週期中有 400ms 疊加 `HonorGaugeBlink` 閃爍。
每一級的 y 座標為 `671 - 32 * (n9 - 1)`，即 9 格等距 32px。

**界線。** 「掉落率隨 gauge 提高」是 Wiki 歷史敘述；native 這段只負責**顯示**。
掉落**率**與 Lv1–3 **效果值**（如武器強化 25 秒 200%）皆無 client-side 權威 reader，
屬 server policy，維持 UNRESOLVED。

> ⚠ **第十六輪更正。** 本節原寫「掉落**表**…無 client-side 權威 reader」，
> 這句過寬。`map/gameobject.dat` 就是掉落物**目錄**，且 native 有完整 reader
> （`sub_7FBB90` → `pmFile::possible_ctor_or_dtor_49`），88 個 `D_Item` 恰好構成
> Wiki 所述的 **7 族 × Lv1–3** 矩陣。已改為只對「率」與「效果值」宣告 UNRESOLVED。
> 詳 [`RESOURCES.md` §5d-21](RESOURCES.md)。

### 5b-3. Clan rank：wire 值 → S/A/B/C 的對照已確定

**Wiki 觀察。** [トーナメント](https://wikiwiki.jp/paperman/トーナメント) 描述 clan 對抗賽，
並稱需 5 名以上成員才能參加、每隊最多 5 名出賽。

**Native fact / HIGH。** clan 資訊繪製函式以 `*(this + n2 + 60)` 決定 rank 貼圖，
分支是明確的等值比較，因此 wire 上的 rank 欄位編碼可定案：

| 欄位值 | 貼圖 | 意義 |
|---|---|---|
| `6` | `CLAN_RANK_S` | S |
| `5` | `CLAN_RANK_A` | A |
| `4` | `CLAN_RANK_B` | B |
| `3` | `CLAN_RANK_C` | C |
| 其他 | `CLAN_RANK_HYPHEN` | 無／未評級 |

**界線。** 值 0–2 落入 hyphen 分支，但**不代表**它們沒有其他服務端意義；
評級升降條件、賽程（Wiki 記平日 19/21/23 時、假日 14/21 時）、5 人門檻都屬
original-service policy。22 個 `*_TNMT_*` opcode 中目前只有 764 落地，
其餘維持不註冊。

### 5b-4. 試し撃ち 預設裝備 → itemdata.pat 四個確切 item ID

**Wiki 觀察。** [試し撃ちシステム](https://wikiwiki.jp/paperman/試し撃ちシステム)
（2012-08-16 實裝）記載：試射場限時 2 分、子彈不可補充，且
**「選擇的武器以外一律回到初期裝備（MP5K・USP9・CU-BK7・HE GRENADE）」**。

**Resource fact / HIGH。** 本輪重新解出 `Extracted/ui/cfg/itemdata.pat`
（`server/pmfile.py pat-decrypt`；header `version=1, count=21164`）。
實測 record **stride = 997 bytes**、name 為 record 內 `+20` 起的 UTF-16LE
NUL-terminated 字串；以此解析 21,164 筆後，總長 `8 + 21164×997 = 21,100,516`
**恰等於檔案大小**，無剩餘位元組，故切段可自證。四個名稱各自唯一命中：

| Wiki 名稱 | item ID | 段 | 段內位序 |
|---|---|---|---|
| `MP5K` | `12100027` | 12.1M 主武器 | 第 17／1339 |
| `USP9` | `12200026` | 12.2M 副武器 | 第 3／228 |
| `CU-BK7` | `12300004` | 12.3M 近戰 | **第 1**／280 |
| `HE GRENADE` | `12400007` | 12.4M 投擲 | **第 1**／229 |

四者精準落在 [`RESOURCES.md` §5a2](RESOURCES.md#5a2-武器改裝件段全圖-十九輪定案)
既有的四武器槽分段上，形成 Wiki 名稱 → item ID → 段語義的三方互證，也與
§5c-1 既有的 `MP5K = 12100027` 結論一致。

**明確的反證，必須保留。** 近戰與投擲的預設值剛好是段內最小 ID，但
主武器段最小是 `12100001 MP5 SD6`、副武器段最小是 `12200003 DE .50 AE`，
**都不是**預設值。因此「段內最小 ID 即預設裝備」這條看似漂亮的規則**不成立**，
不可用來推導其他槽位的預設值或新帳號 grant。

**界線。** 這四個 ID 證明的是「試射場把未選武器重設為這組」——一個
**client-side 場景行為**。它不證明新帳號 inventory、不證明 grant、
不證明這四件在本 revision 可購買或有價格（本 revision 價格區近乎全零，
見 [`RESOURCES.md` §2c](RESOURCES.md#2c-itemdata-pat-尾部-721b-完整切段-十六輪21164-條統計錨點定位)）。
§5-starter grant 的三來源分離結論不因本節改變。

### 5b-5. 兩項「僅 Wiki、刻意不採用」的記錄

- **房間資訊欄位。** [MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細)
  列出右鍵房間可見的欄位（鎖、房號、房名、模式、地圖、勝利條件、限時、經過時間／回合數、
  道具戰、平衡、洗牌、local rule、刀戰、crazy play、no-skill）。其中「個人/隊伍生存、
  スチール、パルプ＆ロール、足球顯示經過時間；戰術、爆破、占領、PVE 顯示回合數」是**模式分類**線索，
  與既有 `modeIndex` 對照相容，但房列表 wire 欄位仍以 `LAYOUTS.md` 為準，不據此增欄。
- **経験値表。** [階級関連](https://wikiwiki.jp/paperman/階級関連) 有 Lv1→2 需 1000、
  累計 2500/4000/5500… 的完整表與各級獎勵。**不採用**：這是 2017-03-30 的歷史頁，
  且升級獎勵屬 present grant 政策；本 revision 無 client-side EXP 表 reader 可交叉驗證。

### 5b-6. 第三輪：Wiki × 反編譯 × Extracted 三方對照的結果

本輪把 `main:Extracted/` 的 **429 個文字型資源**全部取出並解密
（201 個原本就是明文、228 個經 `pmFile` 解密後可讀），與 Wiki 敘述、
`PaperMan.exe.c` 三方比對。完整技術結論寫在 `RESOURCES.md`，此處只記
**與 Wiki 敘述直接相關**的部分。

| Wiki 敘述 | 三方比對結果 |
|---|---|
| 連續 kill「7 kill=キリングマシーン、8 kill 以後全算ディアブロ」 | **部分修正**。native 與 `ui/information.xml` 一致給出 **7 階**連段（DOUBLE→TRIPLE→MULTI→ULTRA→**GENOCIDE**→KILLINGMACHINE→DIABLO）。自 2 kill 起算則第 7 階正好是 7 kill，與 Wiki 自洽；但 **Wiki 漏列 `GENOCIDE` 這一階**。詳 [`RESOURCES.md` §5d-2](RESOURCES.md#5d-2-戰績面板-11-欄三來源一致解開-zkdd-三個縮寫-本輪)。 |
| 掉落道具「アイテム戦」限定、Lv1–3、名誉ゲージ越高品質越好 | 掉落**倍率表**在 `ui/system/AI/ScoreRatio.xml`（PvE）中有精確數值，但那是 **PvE 專用**表；PvP 掉落率無 client-side 表可證，仍 UNRESOLVED。 |
| ペーパズル 合成有素材、稀有度、body slot 規則 | **大幅補強**。`ui/NewSkillLevTable.xml` 未加密且保留韓文開發註解，含 `COMBILIMIT=3`、五軸稀有度倍率 `15/9/12/10/11`、六部位（Hair/Jacket/Pants/Shoes/Set/Accessory）的 `COMBI`＋`STRENGTH` 修正與 `Lev_1..9` 區間。詳 §5d-4。 |
| PvE 過關有獎勵 | **可解析**。`AiMultiCompensation.xml` 的 `itemnumber` 解出為 `福袋(☆☆)`／`福袋(☆)`／`報奨金 5,000PG`／`報奨金 3,000PG`，名次 1–4。詳 §5d-4b。 |
| MAP・ルール詳細 的模式與地圖清單 | **可機器驗證**。`maplist.pat` 解出 123 張圖與 mode bitmask，全部符合既有 `MODE_INDEX_MAP_BITS`。GunShooting 恰為 map 81/89，與 `gamecenter_map_info.xml` 閉環。詳 §5d-5。 |

**一個必須記住的反面教訓。** 地圖檔名前綴**不能**用來推導模式：
`PVE_01_ruins.pmm` 實際是 AIMulti、`TS_31/32_worldcup.pmm` 實際是 TeamSoccer。
我最初以前綴做交叉驗證時得到「3 個不符」，追查後發現**錯的是我的啟發式、
不是資料**。任何模式歸屬一律以 bitmask 為準。

### 5b-7. 第四輪：角色能力值與武器特性的三方對照

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧) 歷史性地列出各角色能力差異 | **找到權威表**。`Extracted/convars.pat` 的 14 組 `m_cAvataAbility[ICT_*]` 是 client 端能力值來源：`def_hp`/`max_hp` 全 100、`jumpheight` 全 350，差異只在 **defence（−14…+15）、movespeed（85–90）與攝影機高度**。詳 [`RESOURCES.md` §5d-7](RESOURCES.md#5d-7-convarspat14-個角色能力值且第-15-個-ルーシー-刻意缺席)。 |
| ルーシー(Lucy) 是服務末期（2016）才加入的角色 | **資源側強力佐證**。native 依序查詢 **15** 個 ICT，但 convars 只定義 **14** 個，缺的正是 `ICT_DEVILGIRL`；該角色因此吃 native 硬編碼 fallback（defence = 0）。`CharacterFitting.xml` 也只到第 13 個、`item/avatar/*.pav` 只到角色型別 `09`。越晚加入的角色資源覆蓋越少，形成清楚的分級。 |
| 武器清單含「FMG-9(Dual Gun)」與「M1 Garand」等特殊武器 | **機制已定位**。`ui/system/SpecialWeaponType.xml` 以 `1=DUAL_GUN` / `2=EMPTY_RELOAD` 標記這 8 個 id，語義與武器本身完全吻合（M1 Garand 的 en-bloc 彈夾＝打空退夾）。同時揭示**第四種 id 空間**：其 `Index` 是 12.1M 段內偏移。詳 §5d-8。 |
| 「Winchester(CP)」等變體另立條目 | itemdata 的 `t12` 在武器段的實際語義是**變體→基底武器**（`WINCHESTER [CP]` → `WINCHESTER`）。1,291 筆非零 t12 **全部**落在武器段，其中 97 筆為具名變體。 |
| [MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細) 提到油桶等場景機關會造成傷害 | **確認為共用武器模型**。`gimmickproperty.xml` 的 7 個機關各自指定一個 `ReferenceWeapon`（油桶→`FIRE_BOMB`、瓦斯桶→`HE_BOMB`…），且與一般武器查詢**共用同一個名稱登錄表** `dword_1CC95A0`。ordinal 0..6 由讀取順序決定，已由 `GimmickProperties` 類的解析迴圈證實。詳 §5d-9。 |

**§5b-4 的基礎四件組已升級為交叉印證。** 當時只有 Wiki
[試し撃ちシステム](https://wikiwiki.jp/paperman/試し撃ちシステム) 一方說
「未選武器回到 MP5K・USP9・CU-BK7・HE GRENADE」。本輪在
`ui/system/Tutorial_Data.xml` 這個**完全無關的子系統**中，發現教學關卡
以 `type0=27 / type1=26 / type2=4 / type3=7` 配置玩家，
解出來正是同樣那四件。兩個獨立 client 子系統選用同一組基礎裝備，
該結論不再依賴單一 Wiki 頁面。詳 [`RESOURCES.md` §5d-10](RESOURCES.md#5d-10-tutorial_dataxmltype-欄即武器段選擇器並二度印證基礎四件組)。

### 5b-8. 第五輪：文件自我稽核（以三來源為權威反查 md）

本輪把 md **當成待驗物**而非依據，逐條回推。多數數字完全站得住：
itemdata 21,164、maplist 123、msgtable 1,346、quest 844、
weaponparts 1,108、partsability 413，以及
「851 個傳給 `sub_408080` 的字面訊息 id」**全部逐位重現**。
但也找到兩處真正的錯誤，已修正：

1. **座標系混淆**（`RESOURCES.md` §2c）。該節的 `tail[N]`／`bNNN` 是
   **1808B 記憶體結構**的位移，不是檔案位移；照著它去讀 997B 的檔案
   record 會取到雜訊。實際 tail 起點是 `record+276`，
   而 `997−276 = 721` 正好等於該節標題自稱的「尾部 721B」。
   以 base=276 重跑後，該節每一個錨點都完全命中。
2. **dispatcher 覆蓋缺三筆**（`LAYOUTS.md`）。標題寫「300 case」，
   實際 `sub_58B010` 有 **306** 個。補上 417／803／882 後為全覆蓋，
   並新增 `verify_dispatcher_coverage.py` 防止再次悄悄漂移。

**額外釐清一個會誤導後續工作的觀念**：676 是**具名** opcode 數，
**不是 opcode 空間的上界**。有 46 個 opcode 具備 native reader/writer
卻未在名稱表註冊，其中 16 個甚至超過目錄末端 994（995–1010）。
「不在 `packets.tsv` 就不存在」是錯的。

另外，[各種ゲージ詳細](https://wikiwiki.jp/paperman/各種ゲージ詳細) 自陳
「非官方說明、為推測」的武器儀表，已能對到 `partsability.pat` 的實際欄位；
連該頁明講「無圖表的隱藏屬性、詳細不明」的**初弾命中**，
都對應到確實存在的 `first_shot_wide`／`first_shot_angle`。詳
[`RESOURCES.md` §5d-11](RESOURCES.md#5d-11-武器-ui-儀表--partsabilitypat-引擎欄位對照)。

### 5b-9. 第六輪：Pepachi 演出級別，以及兩處 md 更正

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細) 描述抽獎有「大當／小當／槓龜」的演出差異，並有 11 連抽 | **機制已完整定位，且證明結果由伺服器決定**。701 每筆獎品三元組的第三欄 `reelC` 就是演出級別，經 `sub_84A320 → sub_842A30 → sub_8433E0` 三段重映射後選中 `pe-pachi_scenario.xml` 的四個區段之一：`Rare`(3 種演出)／`Atari`(11)／`Zannen`(8)／`Suka`(44)。客戶端唯一的 `rand()` 只在**同級別內**挑第幾種演出。`p_n11 >= 11` 分支即 11 連抽。詳 [`PACKETS.md` §3.15p](PACKETS.md)。 |
| — | **`face_contents.xml` 舊記有誤**。它不是「臉型清單／角色創建」，而是**聊天表情觸發詞表**：5 種表情共 122 個關鍵字，載入類別名為 `CFaceChatScriptProperty`，以 `wcsstr` 對聊天字串做子字串比對。純客戶端行為，伺服器不參與。詳 `RESOURCES.md` §5d-12。 |
| — | **一次「我誤判文件有錯」的自我更正。** 我原先以為 `RESOURCES.md` §1 表中 `RecommandItem.pat` 的「(809)」是筆數寫錯（實際 1,030 列）。再查 `db/packets.tsv` 後確認 **809 是 opcode**（`GS_GET_RECOMMENDSET_INFO_ACK`），與該列自己的備註一致 —— **原文沒錯，是我誤讀**。已改以「1,030 列 × 12 欄」明確標示筆數、並把 opcode 寫成 `808`→`809`，消除同一欄位既可讀成筆數又可讀成 opcode 的歧義。 |

**抽獎的界線仍未鬆動。** 上述只證明「級別由 701 指定、客戶端照演」。
中獎率、獎池內容、保底與扣款**仍無任何 client 可證事實**，維持 UNRESOLVED，
現行 fail-closed 的 700→701 回覆不得改為成功。

### 5b-10. 第七輪：出生點表浮現，與一個自己造成的方法論缺陷

**先講缺陷。** 前幾輪的批次解密用「BOM 或 `<` 開頭」判斷明文，
導致 **21 個本來就是明文**的檔案被錯誤地「解密」成亂碼而長期被略過。
改用「前 512 B 可列印位元組 > 90%」後重跑：明文 222 / 解密 207，
**新增 20 個可讀檔、0 個回歸**。這說明**工具的判準本身也要交叉驗證** ——
先前「這些檔看不懂」的結論其實是我自己造成的。

| 新讀出的內容 | 三方比對結果 |
|---|---|
| `map/maps/*.ini`（7 個） | **每張地圖的出生點表**。native 以 `_stricmp` 比對 6 個模式區段（`[FreeForAll]`…`[Practice]`，**等距 1072 B**）加 `[CrystalSpawnPoint]`；每筆出生點為 `angle`／`team a\|b`／`origin x y z`。詳 [`RESOURCES.md` §5d-13](RESOURCES.md)。 |
| 水晶槽 token | native 寫死 `none=0 / small=1 / large=2`。**槽數與出生點數 1:1**（`TS_14_Stadium` 16:16、`TS_40_SlumTown2` 16:16），其餘 5 張圖為空 —— 與 `PACKETS.md` 把 372–377 `GR_*CRYSTAL_*` 標為「棄用模式」相容。 |
| `slanderfilter` | 實際隨附 **`filterword.txt` 1,227 行**與 **`exceptionword.txt` 1,686 行**、**UTF-8**（非 CP932）。native 先組 `.txt` 再組 `.dat`，兩種副檔名皆支援；`RESOURCES.md` 舊記只寫 `.dat`，已補齊。 |
| `GameInOption.ini` | 雖已可讀（內含 `SoccerMoveData = 137` 等），但**所有鍵名與檔名在 exe 中都找不到字串**，故**不能**當成生效參數，標為 UNRESOLVED。這是「可讀 ≠ 有用」的實例。 |
| 8 個 `datarevision.txt` | 值**全部相同 = `811034967`**，證實 `Extracted/` 是同一次 patch 的一致快照。 |

### 5b-11. 第八輪：武器偏移空間的「無碰撞」性質，與第三方印證計分表

| 主題 | 三方比對結果 |
|---|---|
| [キルログアイコン一覧](https://wikiwiki.jp/paperman/キルログアイコン一覧) 列出擊殺紀錄的武器圖示 | **找到資料來源**。`ui/killImgWeapon.xml` 有 3,006 筆條目（相異索引 3,004），native 以寫死檔名在啟動時載入。它**只用一個數字索引武器、不指定段** —— 因為實測四個武器段的段內偏移**零碰撞**（0..3099 共 2,076 個 (offset,band) 配對，無一重複）。詳 [`RESOURCES.md` §2c-3](RESOURCES.md)。 |
| [各種ゲージ詳細](https://wikiwiki.jp/paperman/各種ゲージ詳細) 之外的 PvE 連段倍率 | **取得第三個獨立證據**。`ui/system/UIActor.xml` 開頭有 **CP949 韓文開發註解**，其 `EFF19..EFF22` 分別標為 피버／래피드 킬／약점 킬／킬 콤보 文字特效，正好對上 `ScoreRatio.xml` 的 `Fever`／`Quick`／`Weakness`／`Combo` 四族；`EFF23..EFF32 = LV 1..LV 10` 對上 `Combo1..Combo10`。`EFF5..EFF8` 亦依序對應 `HeadShot`／`HeartShot`／`CriticalShot`／`AirCombo` 四個倍率欄。詳 §5d-3b。 |
| [パッケージ詳細](https://wikiwiki.jp/paperman/パッケージ詳細) 描述套裝包內含多件物品 | **展開表已解出**。`Total_Package_Index.xml` 有 114 個包（檔頭 `num="114"` 相符），父 id `15306001..15307095` 中 **113/114** 可解出名稱；子成員 1,596 列中 282 列為 `0` 佔位、實際 1,314 個 id 有 **1,300 個**可解出。詳 §5d-15。 |

**一次被自我複驗抓到的計數錯誤。** 我最初把 killImgWeapon 記為 3,005 筆，
複驗時發現正則貪婪吃掉了索引 1 的條目，實際是 **3,006 筆**（且 361、726 各重複一次，
相異 3,004）。已更正，並把四個數字全部寫進 `verify_resource_claims.py`
（現 34 項檢查）以免再次漂移。

### 5b-12. 第九輪：找到成套傷害數值，但仍不採用

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [威力一覧](https://wikiwiki.jp/paperman/威力一覧) 等頁列出各武器威力（社群量測） | **首次找到原廠成套數值**，但**刻意不採用**。`ui/RocketProperty.xml` 以 26 個具名彈頭型別給出 `splashMaxDamage`／`splashRatio`／`MaxnuckBack`／`bulletMoveSpeed` 等 12 欄（部分型別另有 `LifeTime`／`ExploredMine`），native 以具名類別 `CRocketProperty` 逐筆存成 14 dword 記錄。詳 [`RESOURCES.md` §5d-16](RESOURCES.md)。 |
| 電漿槍／雷射槍等特殊武器有獨立表現 | **確認為獨立資料表**。`PlasmaProperty.xml`（42 筆**全部**命中）與 `LaserProperty.xml`（45 筆中 39 筆命中）以 `gunindex`＝武器段內偏移索引，解出來分別是 `プラズマガン` 與 `L-1012` —— 語義完全自洽，也再次印證 §2c-3 的無碰撞偏移空間。 |

**為何找到數值卻不採用。** 這些是**客戶端投射物模擬參數**。
命中判定與實際扣血是否由伺服器覆核，**沒有任何 client 端證據**；
Wiki 的威力一覧則是歷史社群量測。兩者即使相符也不構成 service 事實，
因此維持 UNRESOLVED，不得據此實作伺服器傷害計算。
這與 §5d-11 對武器儀表的處理一致：**可讀出 ≠ 有權威**。

### 5b-13. 第十輪：角色差異化的第二個維度，與一次被複驗推翻的宣稱

| 主題 | 三方比對結果 |
|---|---|
| [キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧) 稱各角色有能力差異 | **找到第二個差異化維度**。除 §5d-7 的 `convars`（defence／movespeed）外，`ui/system/CharacterToPushChar.xml` 的 **`damage_aim` 依角色不同**：8 人為 `1f`（無減免），其餘自 `0.6f` 遞減至 `0.2f`（hood 與 magicgirl 最低）。數值越小＝受擊時準心偏移越輕。詳 [`RESOURCES.md` §5d-17](RESOURCES.md)。 |
| 角色變身／特殊外觀 | **`CharacterToCooki.xml` 完整解出**。15 個角色段全部 `bEnable=1`，`acc2` 依序為 `10760001..10760014`，名稱**全是「クッキーアクセ」**，與檔名 `ToCooki` 完全吻合；`head` 借用既有髮型 item 而非另造資產。 |
| 角色共 15 種 | **再獲一條獨立證據**。`CharacterToCooki` 的 `handTexture` 依角色順序恰為 `hand1.tga`..`hand15.tga`，與 `character/textures/` 的 15 張手部貼圖 1:1 對應。 |

**兩次被自我複驗抓到的問題，都已更正：**

1. **過度概括。** 我先前由「convars 缺 devilgirl、CharacterFitting 只到 13」
   推出「越晚的角色資源越少」。本輪查完發現
   `CharacterToCooki` 與 `CharacterToPushChar` **都完整含 15 個角色**，
   通則不成立 —— devilgirl 其實只缺那兩項。§5d-7 的表已改為逐檔案列出。
2. **誤讀為一致。** 我原本寫 `CharacterToPushChar` 的四個參數「15 個角色完全相同」，
   複驗時發現只有前三項一致，`damage_aim` 有 6 種取值。已更正，
   並把分布寫進 `verify_resource_claims.py`（現 45 項檢查）——
   這條檢查正好就能擋下我當初那個錯誤宣稱。

### 5b-14. 第十一輪：改問「完備性」而非「再讀一個檔」

前十輪都是挑檔案讀。本輪換一個方向，問一個能**界定整體研究上限**的問題：
**client 到底會載入哪些資源檔？我們手上缺了哪些？**

`PaperMan.exe.c` 把檔名寫成寬字串字面值，可以完整列舉：**170 個**相異資源檔名。
與 `main` 的 71,464 檔完整樹比對後 **162 個有、8 個沒有**，
而這 8 個**全部可解釋**：3 個是 client 執行期自己寫出的本機狀態、
2 個是打包容器 `pmClient.dat` 本身（其中一筆還是反編譯產生的假字串）、
2 個是 `.dat` 副檔名 fallback（實際隨附 `.txt`）。

**唯一真正缺少的是 `ui/CharFittingAnimation.xml`**（以根標籤
`UICHARFITTINGANIMATION` 載入的試衣間動畫表）。這個缺口有分析意義：
它與 §5d-17 觀察到的「`CharacterFitting.xml` 只有 13 段、且僅 `hayate` 啟用」
互相呼應 —— **試衣間子系統本來就無法從現有 extraction 完整還原**，
相關結論應停在 UNRESOLVED。

**這 95% 對研究方法的意義。** 先前遇到「查不到某個檔」時，
九成五的情況是**我沒找對地方，而不是檔案不存在**（第七輪把 21 個明文檔
誤判為加密就是典型）。日後再遇到類似情形，應先跑
`verify_resource_coverage.py` 確認它是否真的缺席，而不是直接下結論。

### 5b-15. 第十二輪：角色動畫全備、試衣間確認殘缺

| 主題 | 三方比對結果 |
|---|---|
| 角色共 15 種 | **第四條獨立證據**。`character/animations/ui/` 有 `type1`..`type15` 共 15 個目錄（繼 `hand1..15.tga`、`CharacterToCooki` 的 handTexture、`convars`+native 的 15 次查詢之後）。 |
| 角色動畫完整度 | **15/15 全備**。native 以 13 個 `.PAD` 字面值指名它要的動畫（`base_29`/`base_69`/`base_full`/`crazy`/`damege1`/`dead`/`defeat`/`escape`/`loop`/`shot`/`uiNormalF`/`uiResultLF`/`win`），**每個 type 目錄都齊備、無一缺漏**。 |
| 先前疑似的「type12/13 少檔」 | **不是缺漏**。13 個目錄多出 `uibreath.pad`、`uiresultrf.pad` 兩個 **native 從未指名**的殘留檔；type12/13 沒有這兩個，反而才是剛好 13 個。 |
| 試衣間（CharacterFitting） | **確認殘缺**。`CharacterFitting.xml` 引用 13 個 `PendantFolderName`（`Angry_Type1..13`），實測**只有 `Angry_Type13` 一個目錄存在**；它引用的兩個 `SoundFolderName`（`Angry_Voice`／`Voice_angry`）**也都不存在**。 |

**三條獨立證據指向同一結論。** 試衣間子系統在本 extraction 中殘缺：
① §5d-18 的唯一真缺檔正是 `ui/CharFittingAnimation.xml`；
② 本輪 13 個 pendant 目錄只有 1 個、2 個 sound 目錄全無；
③ §5d-17 的 `CharacterFitting.xml` 只有 13 段且僅 `hayate` 啟用。
因此**任何試衣間相關結論都不可能從現有資料完整還原**，一律停在 UNRESOLVED。
反過來說，角色動畫資產則是**可信且完整**的 —— 這就是界定可分析範圍的實益。

### 5b-16. 第十三輪：地圖縮圖／立繪對照，與 748 的真實語義

| 主題 | 三方比對結果 |
|---|---|
| [MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細) 的地圖清單 | **資產面已對齊**。`map/minimaps/` 有 123 張 `Minimap_*.dds`、`map/portraits/` 有 122 張 `Port_*.dds`，與 maplist.pat 的 123 張圖同量級。忽略大小寫後仍有 10–11 張對不上，原因是**原廠命名漂移**（`TS_03_Port`↔`TS_03_Fort`、`TD_01_Cemetery`↔`Cemetry`、`TS_09_SlumTown`↔`TS_09_Slum Town`），不是缺檔。 |
| 「今すぐプレイ」／隨機地圖 | **找到非實體地圖的縮圖**：`Port_RANDOM_MAP.dds` 與 `Port_HOTRANDOM_MAP.dds` 在 maplist 中沒有對應地圖 —— 它們是 UI 上的「隨機」選項圖示，對應 `SelectRandomMap.xml`。 |
| — | **748 的語義定案（對私服有直接用處）**。`748 GR_SELECTRANDOMMAP_ACK` 是少數「有 ACK 無 REQ」的 opcode。逐字元比對後，它的 handler `sub_564090` 與 `122 GR_MAPCHANGE_ACK` 的 `sub_56E530` **函式本體完全相同**：讀 1 個 `u8`，交給同一個地圖設定器 `sub_42FC50`、寫進同一個房間全域物件。該設定器全檔僅這兩處被呼叫。詳 [`PACKETS.md` §3.15q](PACKETS.md)。 |

**實務結論。** 實作隨機選圖**不需要新的狀態機** —— 沿用既有
`room.MapId` 廣播路徑、改用 opcode 748 即可，客戶端處理完全一樣。
但**選圖規則**（可選池、是否排除當前圖、誰能觸發）仍無 client 證據，
維持 UNRESOLVED，現在不應主動發送 748。

### 5b-17. 第十四輪：抽獎的**前置條件**全部找到了 —— 三個 client gate 與 Wiki 的「PG 需 Lv10」完全吻合

前十三輪反覆確認「抽獎**結果**（獎池、機率、扣款）無 client 證據，維持 UNRESOLVED」。
本輪改問一個**先前沒問過**的問題：**client 在送出 700/900 之前，自己檢查了什麼？**
這是可以完全閉合的，因為 gate 全在 client 端、而且會拿 `msgtableres.lang` 的
訊息 id 出來顯示 —— 訊息文字本身就是這些常數的語意標籤。

**兩個 caller 的結構完全同構**（各自獨立一份常數，非共用）：

| | Pepachi (700) | Capsule／ペーパーガッチャン (900) |
|---|---|---|
| caller | `sub_8459C0` | `sub_99D0A0` |
| sender | `sub_8458D0` | `sub_99CFA0` |
| 等級下限 global | `dword_BDBC98` = **10** | `dword_BEAE4C` = **10** |
| 禮物盒上限 global | `dword_BDBC9C` = **200** | `dword_BEAE50` = **200** |

四個 gate，依 caller 內的求值順序：

| # | 條件 | 失敗顯示 msg id | 訊息原文 |
|---|---|---|---|
| 1 | CASH 餘額 `*ArgList > 0` | **264** | `ＣＡＳＨが不足しています。` |
| 2 | PG 餘額 `*dword_EE8D18 > 0`（PG-ten 另要 `≥ 10000`、CASH-ten 要 `≥ 300`）| **252** | `PGが不足しています。` |
| 3 | 禮物盒 `i_23 < 200` | **847** | `プレゼントボックスに空きがありません。（…%d個まで保管できます。）` |
| 4 | **僅 PG 路徑**：等級 `n10_2 >= 10` | **846** | `ペーパチはレベル「%d」以上からご利用できます。` |

**三個 global 的身分，由 995 的 reader 一次全部定案（Fact / HIGH）。**
`sub_567AE0`（dispatcher `case 995u`，`LAYOUTS.md` 記為 `s32 s32 s32`）
就是錢包/等級推播，三個欄位依序寫進：

```
995 field[0] → *dword_EE8D18   = PG        （§3.2 已知 198 的 GP 欄同樣寫這裡，sub_5392A0）
995 field[1] → *dword_EE8D0C   = CASH      （= 反編譯器誤命名的 `ArgList`，B0F0xx 非堆疊變數）
995 field[2] →  n10_2          = 等級      （EE8D10）
```

`n10_2` 是等級的獨立佐證有三條：① `sub_92EF00(18, 23, n10_2, 0)`；
② 大廳以 `n10_2 - 1` 索引 `Class` 資源表取階級圖示（`sub_44EB50`）；
③ 它同時是 `itemdata.pat +644`「需求等級」的比較對象
（`sub_534FE0(...) > n10_2` → 顯示 msg **922** `レベル制限のあるアイテムです。%dレベル以上、購入可能です。`），
與 `RESOURCES.md` §2 的欄位定義自洽。
`i_23` 是禮物盒待領數也有三條：① 198 (`sub_570550`) 尾段的 `u16` 就寫它
（`PACKETS.md` §3.2 早已記為「禮物盒 pending 數」）；② 299 寫入時 `++i_23`；
③ 301 收下/刪除時由 `sub_57AFE0` 遞減。

**這是本專案第一次把一條 Wiki 數值敘述升級為 Fact。**
[ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細) 寫「ペーパチCASHにレベル制限はありませんが、
ペーパチPGはレベル10から」。native 的 gate 4 **只掛在 PG 分支上、CASH 分支沒有**，
且常數就是 `10`。Wiki 的**定性規則與具體數值同時被 client 二進位證實** ——
注意這仍只是 **client-side gate**：原服是否在伺服端覆核同一條件，依舊無證據。

**同頁的「1回30CASH／1000PG」則仍然 UNRESOLVED。** 本輪找到的
`>0` / `≥300` / `≥10000` 是**餘額門檻**，不是價格：`≥300` 出現在 CASH-ten、
`≥10000` 出現在 PG-ten，若 Wiki 的 30CASH／1000PG 為真則十連正好是 300／10,000，
**數值相容**；但 client 從未把這些常數當作扣款額，扣款一律由 995 推播覆寫本地錢包。
因此價格不得寫入 server。

**900 的 selector↔drawCount 配對本輪完全閉合（更正 §2576 的 MEDIUM 標記）。**
`sub_99D0A0` 先依控制項把 `this+148` 設為 1/2/3，再以「控制項不是那三個單抽名」
決定 drawCount 傳 10 還是 1，故實際只可能送出四組：
`{1,10}` START_TEN_CASH、`{1,1}` START_CASH、`{2,1}` START_PG、`{3,1}` START_CP。
即 **selector 1=CASH、2=PG、3=CP**，這現在是 **Fact / HIGH**（先前因
`Source__240/241` 兩個寬字串字面值被反編譯器丟失而只能標 Inference）——
定案依據是 gate 的掛法：`Source__240` 分支獨佔等級檢查＋`dword_BEAE4C`，
與 Wiki「只有 PG 有 Lv10 限制」對齊，故 `Source__240` = `START_PG`(selector 2)、
`Source__241` = `START_CASH`(selector 1)。同理 700 的 `Source__242/243` 對應
PG-ten/CASH-ten，四個 raw selector 1/2/4/5 的 cash/PG 歸屬也隨之確定。

**對 server 的可操作結論（僅此一項）。** 若日後實作 700/900 的成功路徑，
**必須先發 995 建立客戶端的 PG/CASH/等級**，否則 client 會在本地 gate 就擋下請求、
封包根本不會送出。這是 wire ordering 事實，不是獎池政策。獎池、機率、
保底、扣款金額一律維持 UNRESOLVED，fail-closed 不變。

### 5b-18. 第十四輪副產物：三個先前未登錄的 UI 資源檔

`verify_resource_coverage.py` 的 170 檔清單是以**寬字串字面值**列舉的；
本輪順帶核對 `Extracted/ui/*.xml` 中尚未被任何 md 引用的檔案，得到三筆：

| 檔案 | native 載入點 | 結論 |
|---|---|---|
| `TNMT_Awardproperty.xml` | `sub_717E50(L"TNMT_Awardproperty.xml")` → `sub_701BD0(..., L"tournamentAwardTable")` | **純版面座標表**：`award_1..3` + `nomarl_award`／`abnomarl_award`（原廠拼字如此），各含 `emblem_N`／`present_N` 的 `pos_N` 與 `size`。只證明錦標賽頒獎畫面最多排 4 個 emblem／4 個 present，**不含獎品內容**；與 756–777 的 22 個 `*_TNMT_*` opcode 尚未接上。 |
| `gameroom_teamShuffle.xml` | `sub_717E50(L"gameroom_teamShuffle.xml")` | 隊伍洗牌的**等待動畫**版面（`SHUFFLEING` msprite + `SHUFFLE_WAITING`）。佐證洗牌是一個有可見過渡狀態的流程，但時長／觸發／結果全由伺服端決定，無 client 證據。 |
| `PopUpMedalOfHonor.xml` | **exe 中查無檔名字串** | 與 `GameInOption.ini` 同類：**可讀 ≠ 生效**。其 `QUESTDESC`／`QUESTNAME`／`QUESTDETAIL` 三欄暗示名誉ゲージ彈窗曾與任務系統共用版面，但本 revision 無載入證據，標 **UNRESOLVED**。 |

`NewSkillColorTable.xml`（5 段 × 迅/敏/根/防/集 的 RGB）亦屬先前未登錄者，
但它只是 §5d-4 `NewSkillLevTable` 的**配色伴隨表**，純顯示用途，
不影響任何數值推導，此處僅備案。

### 5b-19. 第十五輪：skill 三表閉環，一處 md 錯誤，與「符號相反不等於矛盾」

本輪延續上輪的**完備性**作法：不挑檔案讀，而是機械式列出
`Extracted/ui/system/` 中**沒有任何 md 引用過**的檔案，得到 5 個。
其中兩個 —— `ItemAbilityEffectNameTable.xml` 與 `ItemAbilityEffectColorTable.xml`
—— 正好補完 skill 子系統的最後兩張表。詳 [`RESOURCES.md` §5d-20](RESOURCES.md)。

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [スキル一覧](https://wikiwiki.jp/paperman/スキル一覧)（2015-05-04）稱「スキルは**5ポイント毎**に発動、1～4ポイントでは一切の効果はありません」，並列出六段門檻 | **六段邊界逐格證實，本輪第二條被二進位確認的 Wiki 敘述**。`ItemAbilityLevTable.xml` 解密後為 `id=0..5` / `lev_value` −2..+3，區間 `−1000..−10 / −9..−5 / −4..+4 / +5..+9 / +10..+14 / +15..1000`，**與 Wiki 六段完全相同**，且 `lev_value=0` 段五軸全為 `none`（即 1–4 點無效果）。 |
| 同頁「スキル名称」表的括號數值（如 `紙鶴(+8%)`、`厚紙(+10%)`） | **必須分三類，不可籠統說「相符」**。19 個可比對格中：**8 格完全一致**（speed/hp/hit 的多數）、**5 格同絕對值但正負號相反**（整條 defence）、**6 格數值不同**（整條 agility 與 hit −1）。 |
| 同頁「敏捷／集中は**数値が小さいほど**早い」 | **這條註解解釋了上面的符號差，使它不再是矛盾**。檔案存的是**引擎參數增減**，Wiki 寫的是**對玩家的利弊**；「越小越好」的軸自然符號相反。可驗證的推論：只有 speed/hp 兩個「越大越好」的軸應該符號一致 —— **實測正是只有這兩軸完全吻合**。剩下的 agility 與 hit −1 的**數值差則是真正的版本差異**，無法用這個解釋消掉。 |
| 同頁「スキル系統」5×5 表 | **13/15 相同，2 處是可定年的改名**。本 revision 的 `ItemAbilityNameTAble.xml` 為 `Hit&Run系`／`対応射撃系`，Wiki 為 `速戦系`／`応射系`。兩處都是外來語→漢語／縮寫方向，一致地**指向本 extraction 早於 2015-05 的 Wiki 版本**。這比單一檔案時間戳更可靠，因為是 15 個名稱的集合比對。 |

**一處 md 錯誤已更正。** `RESOURCES.md` §5d-4 原寫 `ItemAbilityLevTable`
「以 `lev_value` −2…+2 分段」，實際是**六段 −2…+3**。先前顯然只掃了前幾段。
已改正並把完整分段移入新的 §5d-20。

**兩條原本可能被誤用的結構事實，改由 native 確立。**
① 五軸不是「資源檔剛好五列」——`sub_7DBDA0` 寫死
`speed=0/agility=1/hp=2/defence=3/hit=4`，parser 有 `i[0] <= 4u` 上限，
物件以 `eh vector constructor iterator(..., 5, ...)` 配置恰好 5 個容器。
② 顏色表的 `Lev_1/2/3` 其 `id` 是 **3/4/5**，與效果表的 `lev_value +1/+2/+3` **共用同一個 id 空間**
（parser 以 `this + 4*id + ...` 定址）—— 所以「顏色只有三筆」不是缺漏，
而是**只有正向三段才有特效顏色**，負向段走 `Penalty` 節點。
若不看 native 定址，很容易把這誤判為資源殘缺。

**界線不變。** 三張表的消費端都是顏色、alpha 與 `Ptcl_*` 粒子名
（且 `FirstPersonView` 全空＝特效只在第三人稱顯示）。**戰鬥數值是否由 server
覆核仍無任何 client 證據**，比照 §5b-12 對 Rocket/Plasma/Laser 的處理：
**可讀出 ≠ 有權威**，不得據此實作伺服器端能力值計算。

### 5b-20. 第十六輪：掉落物總表浮現，推翻自己上一輪寫過的一句話

本輪繼續用完備性作法，但把範圍從 `ui/` 換到**先前完全沒人碰過的 `map/`**。
`map/gameobject.dat`（54,608 B）**沒有任何 md 引用過**，解出後直接推翻了
§5b-2 我自己寫的「掉落**表**無 client-side reader」。詳
[`RESOURCES.md` §5d-21](RESOURCES.md)。

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧)（2015-10-14）與 [名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ)（2013-04-17）都列出**恰好 7 種**掉落物、**每種 Lv1–3** | **結構被資源檔證實**。`gameobject.dat` 的 88 個 `D_Item` 記錄構成 **7 族 × 3 級**矩陣（另 4 筆 A=0），`D_ItemABCD` 的四位數字與 objectId 的四個位元組**完全同構**（88/88 無例外）。貼圖亦分三階（族 1–3／4–6／7）。繼 skill 三表之後，第三個「Wiki 分類結構被資源證實」的案例。 |
| 同頁「☆付きのアイテムは、クエストシステムの一部のクエストクリア条件に設定されている物」 | **找到兩端**。`Q_Item0001..0005` 五個模型（Star1／Star2／goldcard／mochi_bomb／ghost）對上 `Quest.pat` 中 `QuestTerm==19` 的**恰好 8 條**任務，且**只有這 8 條**的 `HonorMedalPosition` 非 0。`pos=3`↔goldcard（「マネーカード回収」）、`pos=4`↔mochi_bomb（麻糬＝新年↔「Happy New Year!」）語義自洽。 |
| — | **但刻意停在 Inference / MEDIUM。** 40001–40006 六條**共用 `pos=1`**，所以 `HonorMedalPosition` **不可能**是 Q_Item 序號的一對一映射；較保守的讀法是「勳章圖示槽位」恰在兩個節慶任務上與序號重合。在找到 native 消費者前不升級。這是「兩格對得上就想宣告映射成立」的典型陷阱。 |

**一句自己寫錯的話，已更正。** §5b-2 原文把「掉落表」「效果值」「掉落率」
一起宣告為無 client reader。實際上**目錄有**（且有完整 native reader 與
105/105 齊備的模型資產），**沒有的是率與效果值**。已把該句收窄。
這說明 UNRESOLVED 也要寫得精確 —— **宣告範圍過寬，本身就是一種錯誤**，
而且它會讓人不再去找那個其實存在的檔案。

**再次出現「命名漂移而非缺檔」。** 17 個貼圖路徑有 3 個查無同名 `.dds`，
但 `star` 實際隨附 `Star_1.dds` —— 與 §5b-16 地圖縮圖同一種原廠命名漂移。
宣告的 **105 個 `.NAO` 模型則 0 缺**。

**界線。** 族序號 A（1..7）對應哪一種道具**無法確定**：codeName 與 mesh 都是
純編號，exe 全文也搜不到 `D_Item` 字面值（表以 id 查詢）。`C`（1..4）語義同樣未知。
掉落率、名誉 Lv→權重、效果數值全部維持 UNRESOLVED。

### 5b-21. 第十七輪：錦標賽狀態機由 client 字串表自行定名，與兩個不可混用的人數

前一輪走 `map/`，本輪回到 **opcode 家族**：756–779 共 22 個 `*_TNMT_*`
在 `PACKETS.md` 只有零散幾行，`763` 更只寫了欄位型別、**沒有 state 值域**。
本輪補完，且**名稱不是從 Wiki 推的** —— 是 client 自己的 `msgtableres.lang`。
詳 [`PACKETS.md` §3.15s](PACKETS.md)。

| Wiki 敘述 | 三方比對結果 |
|---|---|
| [トーナメント](https://wikiwiki.jp/paperman/トーナメント)（2013-10-18）稱「開催時間の5分後に**受付開始**、10分後に**参加者の入場開始**、15分後に**トーナメント開始**」 | **三階段順序被 client 字串逐字證實**。大廳 UI 依 `[12]`(state) 取訊息：state3=**`受付中`**(950)、state4=**`入場中`**(966)、state5=`NN強戦、試合中`(952–956)、state6=`トーナメントが終了しました`(982)、state2=`トーナメント情報公開`(949)。Wiki 用詞與 client 字串**完全相同**。 |
| 同頁的 +5／+10／+15 分鐘 | **刻意不採用**。client 中**沒有任何對應常數**；狀態推進完全由伺服器以 763 推播。這是「順序可證、時距不可證」的典型分界。 |
| 同頁賽制「1回戦…準決勝…決勝」 | **輪次結構已解出**。state==5 時 `+17` 是**剩餘輪次倒數 4→0**，配合 `+18`(roundType) 選「試合中／終了」兩套文案，展開為 **32強→16強→8強→4強→決勝，最多 5 輪**。 |
| 同頁「クランメンバーが**5名以上**いないと参加が出来ない」 | **找到一個人數，但不是這個 —— 兩者不可混用**。msg **933** 寫「予備メンバーを含めて**１クラン１０名まで入場可能**」，是 state4 的**入場上限 10**；Wiki 的 5 是**報名下限**。client 只證實前者，**報名下限 5 維持 UNRESOLVED**。 |
| 同頁平手裁決「キル数＞デス数＞参加人数＞特殊ショット」 | **無 client 證據**。純 server 判定，維持 UNRESOLVED。 |

**方法論上值得記的一點。** 這一輪的 state 名稱**完全沒有依賴 Wiki**：
先從 `sub_57E5A0` 找到 `[12]` 這個持久欄位，再反查所有讀它的分支，
發現大廳 UI 把每個值映到一個 `msgtableres.lang` id，於是**名稱由 client 自證**。
Wiki 的角色只是**事後確認順序吻合**。這比「先看 Wiki 再去找對應」穩健得多 ——
也正因如此，才能乾淨地切出「順序可證、時距不可證」「入場 10 可證、報名 5 不可證」
這兩條界線；若反過來做，很容易把 5 和 10 混為一談。

**界線。** 以上皆為 client 顯示與本地狀態機。誰能推進 state、輪次配對、
勝敗與平手判定、賽程表、獎品發放全屬 server policy，維持 UNRESOLVED。
唯一可操作的結論：763 的 state 必須落在 **1..6**，且 `state==2` 的 `s32`
必須是收訊者自己的 tournament id，否則 client 靜默丟棄整個封包。

### 5b-22. 第十八輪：教學關卡腳本 —— 一個 **Wiki 沒有寫** 的子系統

本輪的結果與前幾輪相反，值得單獨記一筆：
[`wikiwiki.jp/paperman/チュートリアル`](https://wikiwiki.jp/paperman/チュートリアル)
**回傳「ページが存在しません」** —— 這個 Wiki **從未替教學模式建頁**。
（推測是攻略 Wiki 的編者只寫「有人會查的東西」，而教學只玩一次。）

所以本輪**沒有任何 Wiki 敘述可以比對**，全部結論只能由資源檔與 native 自證。
這正好示範：Wiki 是**不完整的**索引，不能拿「Wiki 沒提到」當作「系統不存在」。
反過來，先前幾輪「Wiki 結構被資源證實」的成果也因此更有價值 ——
兩者是**互補**而非互相取代。詳 [`RESOURCES.md` §5d-22](RESOURCES.md)。

**本輪實得（純 native + resource 互證）。**

- `ui/` 下 15 個 `tutorial*` 檔，逐一檢視後**只有 `tutorial_contents.xml`
  是關卡資料**，其餘 14 個是純版面。先確認「哪些檔沒有資訊」本身就是結果，
  可避免後人重複翻找。
- 該檔由 `CTUTPackage` 的三個 parser 讀取，**每個 XML 屬性都對得上一個記憶體位移**
  （`time`→`+0` 且**除以 1000**＝檔案毫秒、內部秒；`startpoint`→`+4`；
  `limit_action` 每筆 **88 B**…）。這是 Fact / HIGH。
- 9 個任務 = **4 移動 + 5 攻擊**，且 `ui/tutorial_image.xml` 的 9 個
  `FINISH_*` 結算精靈與之 **1:1、無多餘無缺漏**。兩個獨立維護的檔互證，
  所以這個分類是設計定案。

**一個容易踩的陷阱，已明確標註。** 本檔的 `weapon` 是 **0..4**，
而 §5d-12 `Tutorial_Data.xml` 的 `type` 是 **0..3**（四個武器段）。
兩者前四值語義相同，但 `weapon=4`＝**狙擊課程**，狙擊槍本身仍屬主武器段 ——
**不是第五個武器段**。若不查 §5a2 的段定義就直接對齊，會多造出一個不存在的 id 段。

**維持 UNRESOLVED 的兩項（都可查證但查不到，不臆測）。**
觸發 token（`A-1`/`E-5`/`G-8` 等）實測在三個 `TU_*.pmm` 中**找不到完整集合**，
綁定方式不明；44 個 `<message>` id（101–145）**不是** `msgtableres.lang` 的
同號字串（該表那段是資料庫錯誤訊息，語義完全不符），屬教學專用命名空間，
本 extraction 未隨附其字串來源。

**界線。** 全屬單機教學的客戶端腳本，過關由 client 自判，
`success`/`fail` 僅為字串常數，**不得推斷伺服器驗證教學進度**。

### 5b-23. 第十九輪：耐久衰減曲線 —— 首次在**數值門檻**層級證實 Wiki，並修掉它的 off-by-one

前三次「Wiki 被資源證實」都是**結構性**的（skill 六段門檻、掉落物 7×3、
錦標賽階段順序）。本輪是**第一次連啟動門檻的數值都對上**，而且反過來
**修正了 Wiki 自身的矛盾**。詳 [`RESOURCES.md` §5d-23](RESOURCES.md)。

| Wiki 敘述（[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報)，2016-03-19） | 三方比對結果 |
|---|---|
| 「耐久値が**19％**の時点から性能低下が始まります」／「ただし**20％まで**なら武器の性能は変わりません」 | **門檻吻合，但 Wiki 自我矛盾。** `durable_ability.xml` 六軸的 `per100..per30` **全為 0**，首個非 0 是 `per20`。native 以 `n10 = (1.0f - ratio) * 10.0f` **截斷**取索引，於是 **21% 仍取 `per30`（無衰減）、20% 起取 `per20`（開始衰減）**。正確說法是「**20% 起**」；Wiki 的「19%」低了一個百分點。這種 off-by-one **只有整數截斷算得出來**。 |
| 性能低下的項目是「**威力、精度、連射速度**」 | **吻合且可排序。** 檔案六軸中非 0 且數值最大的正是 `power`／`aiming`／`shotvelocity`（per10 = 0.9／0.9／0.5）；`recoil` 僅 0.12，`reload` **恆為 0**。Wiki 只列玩家感受得到的三項，與檔案權重排序一致。 |
| 「修理費 **50PG / 3CASH**」「武器種別基準耐久 **C–SS**」「中途退出扣主武器約 **1%**、副武器 **0.5%**」 | **全部無 client 證據，維持 UNRESOLVED。** 耐久值本身由伺服器下發（§3.12c 的 `u16 dura/dura_max`），本表只負責把耐久比例換成客戶端的彈道衰減。 |

**方法論：一條刻意記錄下來的死路。** 同輪解出的 `commonProperty.xml`
（狀態效果參數，17 筆 × 44 B，native 容量上限 30）與
`UIWeaponEffectIcon.xml` 的 `key='0..21'` 註解名（`FREEZE`/`FIRE`/`SPEED_UP`…）
索引對得上，且有兩處**極具說服力**的吻合：`index 5` 是唯一設
`keyboard_reverse`/`mouse_reverse` 的一筆（↔`FREEZE`）、`index 8` 是唯一設
`jump` 的一筆（↔`FIRE`）。

但它**通不過自己的檢驗**：`index 3`↔`SPEED_UP` 的 `speed=400` 比基準 1000
**慢**，`index 4`↔`SPEED_DOWN` 的 `speed=2500` 比基準**快**，兩格恰好反向。
共用列舉不可能如此。因此**不寫入對照表、維持 UNRESOLVED** ——
並把這個否證記進 md，讓後人不必重走。
「兩個強吻合」在「一個硬矛盾」面前不足以成立，這與 §5b-20 的
`HonorMedalPosition` 是同一種克制。

### 5b-24. 第二十輪：PvE 難度表四項全中，外加一個**正在生效的資料／程式不一致**

`ui/system/AI/` 的 16 個檔先前只被一行「AI 模式劇本」帶過。
其中 `AiMultiLevel.xml` 是**唯一的數值平衡表**，96 列，三個維度全部對上 Wiki。
詳 [`RESOURCES.md` §5d-25](RESOURCES.md)。

| Wiki 敘述（[PvEモード](https://wikiwiki.jp/paperman/PvEモード)，實裝 2013-11-27） | 比對 |
|---|---|
| 「1人からスタート可能。**最大4人**」 | **吻合**。96 列中 `number>=5` 的 **48 列數值完全相同**（`0,0,0,1,1,1`），12 個區塊無一例外＝5..8 是哨兵佔位，實際只支援 1..4 人。 |
| 「難易度を**イージー、ノーマル，ハード**より決定」 | **吻合**，且可量化：5 個有效軸 × 4 波 × 4 人數共 **80 組比較，`EASY>=NORMAL>=HARD` 零違反**。 |
| 「マップは**ロボットセンターのみ**」 | **吻合**。全檔僅 `MODE index="95"`，而 `dump_maplist.py` 第 95 筆正是 `PVE_01_ruins.pmm`。 |
| 攻略分 **1WAVE–4WAVE** | **吻合**。恰好 `wave_index` 1..4。 |
| 「2013-12-11／12-25 に難易度の**下方調整**」 | **無法驗證**。只有單一版本快照，UNRESOLVED。 |

另外量到一條 Wiki 沒寫、但明顯是設計意圖的規律：**人數越多敵人越弱** ——
`subtraction_rate` 對人數 1→4 在 **12/12 區塊全部單調遞減**。
兩條單調性合起來證明這是**經過調校**的表，不是佔位資料。

**本輪最有價值的其實是一個 bug。** native 在 `0x593208` 讀的欄位叫
**`shilddamage_rate`**，但檔案 96 列寫的全是 **`siege_dmg_rate`**；
exe 全文從未出現後者。兩名不符 ⇒ **該欄永遠讀不到，96 列一律退回預設
`1065353216`（float 1.0）**。這是一個**正在生效的資料／程式不一致**
（欄位改名時只改了一邊）。

實務含意很直接：**私服若照抄 `siege_dmg_rate` 的數值，行為會與原版不同**；
原版的等效語義是「**無護盾傷害縮放**」。這也提醒一件事 ——
**讀資源檔不等於讀到生效值**，中間那層 parser 才是權威。
本輪若只解析 XML 而不看 native，會得出一個完全錯誤的平衡模型。

（同場加映：`number` 屬性 native 也不讀，列序才是鍵，與 §5d-24
`commonProperty` 同一模式；本檔還是 **UTF-8 with BOM**，與 `ui/` 多數 CP932 檔不同。）

### 5b-25. 第廿一輪：又一個死欄位、一個死檔，與「Wiki 缺頁」第三例

延續上輪清點 `ui/system/AI/`，本輪處理四個 `BotEnemy*` 檔。
[`ガンシューティング`](https://wikiwiki.jp/paperman/ガンシューティング) **頁面不存在** ——
這是本 session 第三個 Wiki 缺頁（另見 §5b-22 チュートリアル、§5b-24 AIマルチ）。
**三個遊戲模式的 Wiki 條目全缺**，而它們的共同點是**單人／PvE 內容**：
攻略 Wiki 的編者顯然只替「對戰」寫條目。這個偏差本身值得記下來 ——
**用 Wiki 當覆蓋率指標會系統性低估單人系統**。
詳 [`RESOURCES.md` §5d-26](RESOURCES.md)。

**本輪唯一能與 Wiki 對上的，是上輪已確立的 modeIndex。**
載入器用 `modeIndex==11`(AIMulti) / `==9`(GunShooting) 分流三個檔，
其中 `n3 == 3` 是 easy 旗標 —— 而**同一個 `n3 == 3` 也控制
`BotWave_easy.xml` 與 `Scenario_easy.xml`**，所以 GunShooting 的難度切換是
「怪物表＋波次表＋劇本」三者同時換。`ui/gs_popup_start_easy.xml` 與
`gs_popup_start.xml` 成對存在，是第二條獨立證據。

**受控比較的價值。** `BotEnemy.xml` 與 `BotEnemy_easy.xml` 是**同形狀的 35 列
對照組**，可以精確回答「easy 到底改了什麼」：24 個屬性中**只有 8 個**有差異，
且兩條**零反例**的單調性 —— `bot_hp` 與 `move_speed` 在 easy 中**從不高於** normal。
反直覺的是 `siege_dmg`／`first_delay`／`shot_delay`（攻擊力與反應速度）
**35 列完全未動**：難度調校只碰血量與移速，不碰攻擊。

**又抓到一個死欄位，而且這次有旁證。** `scale` 出現在每一列，
但 parser 的 30 個屬性名裡沒有它，**exe 全文 `L"scale"` 出現 0 次**。
有趣的是 normal 與 easy 之間**有 7 列的 `scale` 不同**（1.8 vs 2）——
有人**刻意調過這個值**，想讓 easy 的怪看起來更大，**但它從未生效**。
這是繼上輪 `siege_dmg_rate` 之後第二例，愈發說明
**「資源檔寫了什麼」與「引擎讀了什麼」必須分開查證**。

**外加一個死檔。** `BotEnemy_intelligent.xml` 在 exe 中**零引用**，
且其主鍵屬性叫 `index` 而 parser 讀的是 `bot_type_index` ——
**即使被載入也解析不出主鍵**。它獨有 `bot_type=8`（其餘三檔只有 1..7）。
判定為開發殘留，**不可據以推測 AI 行為**。

### 5b-26. 第廿二輪：exe 內**零個 URL 字面值**，與第三個「寫了但不生效」的檔

本輪處理 `Extracted/ui/URLList.xml`（先前無 md 引用）。
結論與前兩輪形成一組互補的對照，值得並排記住：

| 輪次 | 教訓 |
|---|---|
| §5b-24 | 資源寫了 `siege_dmg_rate`，**引擎讀的是別的名字** ⇒ 寫了不生效 |
| §5b-25 | 資源每列都有 `scale`，**引擎完全不讀**；還有一個零引用的死檔 |
| **本輪** | 反過來：**生效的全部在資源裡** —— exe 全文 `L"http` 出現 **0 次** |

**可直接利用的結論。** 客戶端沒有任何硬編碼的主機名、IP 或路徑，
所有對外連線目標都來自 `ui/system/URLList_01.xml`。
私服只要改這張表，就能把橫幅、排行榜、金流頁導向自架服務，
**完全不必修改二進位**。這是本專案少數「直接可操作」的發現之一。

**第三個錯置／死檔。** native 載入的是
`ui/system/URLList_%02d.xml`（帶兩位數序號，失敗時記
`Can't find urllist.` 到 `LOGINLOGMESSEGE`），
而 `Extracted/ui/URLList.xml` **不在該路徑上，永遠不會被載入**。
它是較舊的修訂：`jpn_ranking` 指向**舊 IP** `202.213.230.237`
（生效版為 `157.7.172.71`），且**少了 `jpn_eventing` 與 `gaccha` 兩筆**。
分析一律以 `ui/system/URLList_01.xml` 為準。

**又一次「標籤不是鍵」。** parser 只讀 `index`／`url`／`disable`，
**從不讀 `name`** —— 與 §5d-26 的 `index`、§5d-24 的節點名同屬一類。
這已是本 session 第三次遇到，可以當成通則：
**這套引擎的資源檔普遍用「位置或數字 index」當鍵，人類可讀的名字只是註解。**

**刻意留白。** `jpn_bill` 是
`https://bill.paperman.jp/login.ashx?userid=%s&token=%s`，
一個帶 token 的單一登入跳轉；但本輪**未找到填入這兩個 `%s` 的呼叫點**，
故 token 來源維持 UNRESOLVED，不臆測它就是登入階段的既有欄位。

（Wiki 方面：金流與排行榜屬外部 Web 服務，Wiki 只有操作說明、
無協定層資訊，本輪無可比對項。）

### 5b-27. 第廿三輪：兩處舊記述更正，與「檔名會騙人」

本輪沒有解出新檔，而是回頭複查 `ui/system/AI/`，結果修掉**自己文件裡的兩個錯誤**。
這類結果不如新發現漂亮，但更該記 —— 錯誤的既有記述會**主動誤導**後續分析。
詳 [`RESOURCES.md` §5d-4b](RESOURCES.md)。

**更正一：`AiMultiCompensation.xml` 不是 PvE 獎勵表。**
檔案放在 `ui/system/AI/`、檔名冠 `AiMulti`，§5d file 表因此記作
「AI 協力模式過關獎勵」。但把 `mode_index` 拿去 `dump_maplist.py` 一查，
**102 = `TU_03_new_tutorial_mode.pmm`（Tutorial）、
104 = `PS_12_castle_horror.pmm`（IndividualSurvival）**，
而 PvE 的 AIMulti 地圖是 **95**（§5d-25 剛確立），**根本不在本檔裡**。

所以它覆蓋的是**教學與個人生存**。這也順帶說明上輪那條
「Wiki 的 PvE 報酬＝称号と福袋」**無法**用本檔佐證 —— 兩者根本不是同一個模式。
**教訓：檔名與目錄是作者的分類，不是證據；鍵值查表才是。**

**更正二：「18 個 AI xml 全部有寫死路徑」的說法不準確。**
實際是 **19 個 xml 中的 18 個**；唯一例外正是上輪判定為死檔的
`BotEnemy_intelligent.xml`（0 次引用）。原句寫成「18 個全部」，
剛好把分母寫成了分子，於是**掩蓋了那個例外**。

**第四個死欄位。** `AiMultiCompensation.xml` 每列都有 `periodType`，
但 parser 只讀 `itemnumber` 與 `level`，**exe 全文 `L"periodType"` 出現 0 次**。
原文把它列為「時效語義未證實」，語氣過弱 ——
正確說法是**根本不生效**。累計到本輪，同類案例已有四個
（`siege_dmg_rate` / `scale` / `name` / `periodType`），
足以當成這套引擎的常態：**資源檔普遍帶有 parser 不讀的欄位**。

### 5b-28. 第廿四輪：「機制上分流」≠「資料上不同」，與一次加密普查

延續上輪的自我複查。本輪查 `ScoreRatio.xml`，又修掉兩類錯誤。
詳 [`RESOURCES.md` §5d-3](RESOURCES.md)、[§5d-28](RESOURCES.md)。

**更正一：兩張計分表目前內容完全相同。**
§5d-3 寫「AI 多人協力模式與一般 PvE 用**兩張不同的倍率表**」。
native 確實依 `sub_67EB70()`（＝`modeIndex==11`）分流兩個路徑，
這部分沒錯；但實測 `ScoreRatio.xml` 與 `AiMultiScoreRatio.xml`
**位元組完全一致**（各 2,603 B、SHA-256 相同）。

所以正確的說法要把兩件事分開：**機制上有分流，資料上此刻毫無差異**。
原句讓人以為 AIMulti 的計分與一般模式不同，實際上**完全一樣** ——
分流只是保留調校空間，本 revision 尚未使用。
私服若「因為有兩個檔就各填各的」，會製造出原版沒有的行為差異。

這個區分值得一般化：**看到程式有分支，不等於資料真的分歧**；
兩者要分別查證，否則會把「預留的擴充點」誤讀成「已實作的差異」。

**更正二：把明文檔記成加密。** `ScoreRatio.xml` 與 `information.xml`
都被記為「pmFile 加密，已解出」，但兩者首位元組就是 `<`，**根本是明文**。
為根絕此類錯誤，本輪對 `ui/system/` 與 `ui/cfg/` 做了**機械式普查**
（讀首位元組判定），結果：**加密者只有 9 個**，其餘 30 幾個全是明文。
副產物是一條可用的經驗規則：**`.pat` 一律加密（6/6）、`.xml` 幾乎都是明文
（僅 3 例外）**，但我把它標為 Inference 而非 Fact —— 3 個樣本不足以定規則，
新檔仍應實測，勿依副檔名假設。

（同一子系統內加密狀態也可能不一致：`ItemAbilityLevTable.xml` 加密，
但同目錄的另外兩張 ItemAbility 表是明文。）

### 5b-29. 第廿五輪：移動速度 —— 補上一塊，並說清楚**還缺哪一塊**

§1 表列有一條長期開放項目：「從 character record、weapon stats、skill result
精確找出移動速度的運算順序，**禁止將 Wiki 百分比寫死**」。
本輪推進了一格，但**沒有**收掉它 —— 重點在於現在能說清楚缺口在哪。
詳 [`RESOURCES.md` §5d-11b](RESOURCES.md)。

**新結果：改裝件之中只有「槍托」會動到移動速度。**
把 `partsability.pat` 413 列依 8 個改裝件組別分群後，結果是雙向排他的：
**15.25M ストック（槍托）58 列全部設定 `move_speed`，其餘 7 組共 355 列全部為 0**。
這符合直覺（槍托決定持槍姿態），但更重要的是它是**資源檔自己分出來的**。

**一個容易誤讀的欄位。** `move_speed` 只有 `1` 與 `3` 兩個值，
很像「速度等級」或倍率。但同列還有五個姿勢差分
`miJump`/`miSit`/`miStand`/`miWalk`/`miRun`（0.01–0.15 的小數），
而**姿勢差分出現在 5 個組別、`move_speed` 只出現在槍托組** ——
若 `move_speed` 是速度量值，兩者不可能如此不同步。
故判定：**`move_speed` 是分類旗標，真正的量值是姿勢差分**。

**Wiki 對照：定性全中，定量不可重建。**
[武器移動速度](https://wikiwiki.jp/paperman/武器移動速度)（2023-07-10）說速度受
「武器重量 ＋ 角色固有補正 ＋ skill 補正 ＋ 道具」共同影響。
前三項本專案都已各自獨立證實（角色＝`convars.pat` 的 movespeed 85–90、
skill＝§5d-20 的 speed 軸、改裝件＝本輪）。**定性描述完全站得住。**

但 Wiki 那張相對速度表（暴走側 164、近戰 104–111…）**無法由現有資源重建**：
`partsability.pat` 只有改裝件**差分**，而**每把武器的基礎速度**
在 `itemdata.pat` 已完整切段的 721B 尾段裡**沒有對應欄位**，
`weaponparts.pat` 也只是相容性邊。所以那些數字**來源不明**，
維持 UNRESOLVED，§1 的「禁止寫死」依然有效。

**明確記下還缺什麼**（比模糊地說「待查」有用）：
1. 每把武器的**基礎**移動速度存在哪裡 —— 三個已解檔案都不含；
2. 角色／skill／武器／道具四路的**合流計算點**尚未定位，
   相加或相乘、先後次序全未證實。

在這兩點補上之前，任何移動速度公式都不該進 server 實作。

### 5b-30. 第廿六輪：convar 全表 —— 一個註冊了卻沒隨附的速度上限

延續上輪。上輪把移動速度的缺口明確化成兩點，本輪針對第二點
（「四路合流的計算點」）縮小範圍，方法是**把 exe 的 convar 註冊慣用法整個掃一遍**。
詳 [`RESOURCES.md` §5d-7b](RESOURCES.md)。

**23 個 convar，三種型別，規律乾淨得可以當判準：**

| type | 個數 | 內容 | 隨附於 `convars.pat`？ |
|---:|---:|---|---|
| 0 | 8 | 角色能力值 | **全 YES** |
| 1 | 3 | 整數全域 | **全 YES** |
| 2 | 12 | 11 個 `r_*`／`d_netrun` 偵錯旗標 ＋ `um_gr_maxspeed` | **全 NO** |

type 2 全數不隨附很合理 —— `r_showfps`／`r_noui`／`r_drawworld` 顯然是**開發開關**。

**但 `um_gr_maxspeed` 混在裡面，這是本輪的實得。**
它的硬編碼預設是 float **90.0**，而 `convars.pat` 裡 14 個角色的
`movespeed` 值域是 **85..90、眾數正好 90**。數值一致，命名也叫 `maxspeed`，
看起來就是全域速度基準。

**但我沒有把它寫成結論。** 「90.0 是上限、角色值在其下取值」是
**數值巧合＋命名推測**；本輪**找不到同時讀取兩者的計算點**，
所以兩者關係維持 UNRESOLVED。能確定的只有三點：它是 convar、預設 90.0、
**本 revision 未隨附覆寫值，所以執行時必然就是 90.0**。
（這與 §5d-7 `ICT_DEVILGIRL` 走 fallback 是同一種情形 ——
**「資源沒給」不等於「功能不存在」，而是「跑硬編碼預設」**。）

**對缺口的實際推進。** 現在可以斷言：移動相關的引擎全域常數**就這四個**
（accel 1250、decel 333、maxspeed 90.0，加 per-character `movespeed`），
**不存在第五個未發現的旋鈕**。反過來也確定了
**「每把武器的基礎速度」不在 convar 空間裡** —— 上輪的第一個缺口
因此從「不知道在哪」收窄為「已排除 convar、itemdata 尾段、
partsability、weaponparts 四處」。

### 5b-31. 第廿七輪：位移表 —— 非線性的欄序契約，與第五類死欄位

追 §5d-11b 剩下的缺口時，順手把 `CPartsAbilityListParamCtrl::Load`
的 26 個寫入點逐句抽出來，結果比原本的目標更有價值。
詳 [`RESOURCES.md` §5d-11c](RESOURCES.md)。

§5d-11 早已寫明這個檔「依位置、不依欄名」解析，但**沒有給出實際位移**，
於是留下一個看似安全的假設：位移應該隨欄序等距遞增。**它不成立。**

**四個欄位是交換的：** `bullet_hole`→+60 / `ballCaseSize`→+52、
`miJump`→+80 / `miSit`→+56。若按「位移 = 4×(欄號+1)」回推，
會把**彈匣容量與彈孔**、**跳躍與蹲下修正**兩兩對調 ——
而且**不會有任何錯誤訊息**，數值型別相同、範圍相近，測試很難察覺。
這正是「依位置解析」最危險的地方：它同時要求**位置正確**與**位移表正確**，
前者文件寫了，後者原本沒寫。

**第五類死欄位。** 31 欄中 native 只消費 **26 欄**。
最後一欄 `first_shot_angle` 以「掃描到 CRLF」結束整列，
其後的 `tanpi_pap_type`／`tanpi_mot_type`／`sniperbackimgidx`／`sniperviewimgidx`
**從未被寫入**，四個名稱在 exe 中出現 **0 次**。
原文把 `sniper*imgidx` 與其他活欄位並列敘述，已就地更正。

累計五類（`siege_dmg_rate`／`scale`／`name`／`periodType`／本輪四欄），
可以把先前的觀察再收緊一句：**這套引擎的資源檔普遍留有 parser 不讀的尾欄，
而「位於檔案末尾」是它們共同的特徵** —— 欄位是往後加的，程式沒跟上。

**另一個細節：型別不一致。** 前 24 欄走 `atof`，
最後兩欄 `first_shot_wide`／`first_shot_angle` 走 **`atol`**（整數）。
重新實作時若一律當浮點讀，會在這兩欄產生偏差。

（本輪無 Wiki 對照項 —— 這層是實作契約，Wiki 不可能記載。
但它直接影響「能否正確重建 Wiki 那些武器數值」，所以仍屬同一條線。）

### 5b-32. 第廿八輪：把一句 UNRESOLVED 拆成兩半，只收掉能證的那半

回到資源側。§5c-2 的九槽表在結尾有一句混合敘述：
「`153051xx` **看起來像**舊語音…其 entitlement/storage 關係 **UNRESOLVED**」。
本輪解出該區間的**品名**後發現：這句話把**兩個獨立問題**綁在一起了，
其中一個其實可以定案。詳 [`RESOURCES.md` §5c-2b](RESOURCES.md)。

**能證的那半：它不是「看起來像」語音，就是語音。**
`15305101..15305208` 共 **85 筆，品名 100% 符合 `<角色名>(Voice <套組>)`**，
零例外。編碼也完全規則：**十位以上＝語音套組（11 組）、個位＝角色序號**。

**一條乾淨的獨立佐證。** 把 85 筆依個位數分群，每群的角色名**唯一**
（9 群 1:1、零衝突），且順序與 §5d-6 的角色表前九位**逐項相同**：
`1=ハヤテ 2=ティナ 3=ミリィ 4=サイラス 5=ドッドン 6=ガイ 7=テリシア 8=アルル 9=ヴァン`。
角色表原本由 `.pav`／convars 推得，本表由 itemdata 品名推得 ——
**兩條互不相干的路徑得到同一組編號**，所以 1-based 角色序號不是巧合。

**不能證的那半，維持原判。** 語音**商品**（`153051xx`）與語音**槽位狀態**
（slot 8 `15305801..16000`）確認是**兩個不同 id 空間**，
但「購買後如何成為 slot 8 的可選值」仍無 client 證據 —— 維持 UNRESOLVED。

把原本一句話拆成「可證／不可證」兩半，比整句掛著 UNRESOLVED 更有用：
**後者會讓人以為整個區間都還沒查，於是重複勞動。**
這與 §5b-20 收窄過寬 UNRESOLVED 是同一類修正。

**順帶用品名獨立驗證了 §5c-2 的九槽區間表。**
七個區間中五個非空，品名語義**逐槽吻合**，`BOOST_EXP`/`BOOST_PG`
尤其精確 —— 各恰好 3 筆 `+10%/+30%/+50%UP`。
slot 2(MASTER)/3(ABILITY) 在本 revision **無任何商品**，
但這是**資源側缺席，不是區間定義錯誤**（native 區間仍成立）。

**另一個觀察：只有前 9 個角色有語音商品**，序號 10..15 完全沒有。
與 §5d-7 `ICT_DEVILGIRL` 缺 convars 同屬「後期角色周邊資源逐步缺席」，
但依 §5d-7 自己的告誡，這是**逐檔案**觀察，**不推廣成通則**。

### 5b-33. 第廿九輪：稱號鏈 —— Wiki 的一處「疑似筆誤」被資源檔逐字證實

接上輪的 NAME 段（421 筆），本輪以
[称号一覧](https://wikiwiki.jp/paperman/称号一覧)（2018-08-29）逐項對照。
這是本專案目前**雙向印證最密**的一次：Wiki 補足了資源檔沒有的取得條件，
資源檔則證實了 Wiki 一處看起來像錯字的記載。
詳 [`RESOURCES.md` §5c-2c](RESOURCES.md)。

| Wiki 敘述 | 比對結果 |
|---|---|
| 「キャラクター限定」鏈式解鎖：`base →(持有前者) I→II→III→IV→V → ○○ラバー`，各階 1日/7日/30日 | **結構完全吻合**。資源側有 **14 條鏈、每條恰好 7 個連續 id**，且順序與 §5d-6 角色表**逐項相同**；前 12 條更是等距區塊 `15304594..15304677` 無間斷。 |
| サイラス 鏈第 2 階寫作 **`包帯I`**（而非 `ストレンジャーI`） | **逐字印證**。`15304616` 的品名正是 `包帯I`。若只看命名規律會判定 Wiki 筆誤 —— 但檔案同樣破格。⇒ **鏈以 id 位置定義，名稱可破格**，不可用名稱樣式反推結構。 |
| イベント称号 13 個 | **12/13 命中**（`1周年記念` 檔案作全形 `１周年記念`）。唯一差異 `アイ・カフェ特務賞` 檔案作 `アイカフェ特務章`（無中點、賞→章），屬 §5b-19 同類的原廠用字差異。 |
| 取得條件（戰鬥 1000 回、排名前 10/100、來場特典）與各階天數 | **client 無任何證據**，全屬 server policy，維持 UNRESOLVED。 |

**Wiki 沒有、資源檔獨有的一項：顯示色。**
413/421 筆品名格式為 `<標題>’#RRGGBB’`，共 **141 種顏色**。
Wiki 只貼稱號圖片、**完全未記色碼** —— 這是資源側單方面的補充。

**兩筆原廠瑕疵，但我沒有宣稱後果。**
`15304114` 與 `15304166` 的色彩標記**開頭誤用 ASCII `'` 而非 U+2019**
（結尾仍正確），全段僅此 2 筆不對稱。
本輪**未能在 exe 中定位該標記的 parser**，所以
**「是否會顯示成字面字元」維持 UNRESOLVED** —— 只記錄資源側事實。
（對照 §5b-24：找不到消費端時，就不要替它猜行為。）

**第三次指向同一個角色。** ルーシー(devilgirl，序號 15) **沒有稱號鏈**，
加上 §5d-7 缺 convars、§5c-2b 缺語音商品 —— **三個互不相干的檔案一致缺席**，
比單一檔案缺漏有力得多。但仍依 §5d-7 的告誡**不推廣成通則**：
エリス／ルコット 實裝更晚，卻都有完整的 7-id 鏈。

### 5b-34. 第三十輪：無線對話 —— 1,044 個音檔排成 Wiki 的按鍵表

`Extracted/sound/sounds*/` 下 **1,044 個 `Radio_Message/*.wav`** 先前無 md 引用。
對照 [ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧)（2016-07-31）
與子頁 [／初期](https://wikiwiki.jp/paperman/ラジオチャット一覧/初期)（2013-03-13）。
詳 [`RESOURCES.md` §5c-2d](RESOURCES.md)。

| Wiki 敘述 | 比對結果 |
|---|---|
| 每角色一張表：**Z（指揮）／X（戦術）／V（報告）× キー 1..9** | **38/38 零例外**。全部 1,044 檔依 `(音源根, 角色)` 分成 **38 組**，每組**恰好**是 `command:9(01..09)`／`tactics:9(01..09)`／`information:9(01..09)`。三個目錄名與三個按鍵一一對應。 |
| 表尾另列一條「ボム」台詞 | **`Radio_Message/` 下沒有對應檔案**，來源未定，UNRESOLVED。（不硬湊。） |
| ボイスアイテムは ペーパチCASH 1回 **30CASH** / 福袋のボイス袋 | 與 §5b-17 已證的 gacha 流程一致，本輪**無新證據**，不重複主張。 |

**兩個只有讀 native 才知道的行為（`0x556252` 一帶）：**

1. **無線是確定的，其他語音是隨機的。** 同一個路徑組裝函式對非無線類別
   先設上限再 `rand() % n + 1`；**唯獨 `n8 == 9`（無線）直接用傳入索引**。
   所以玩家按 Z/X/V + 數字聽到的**必定**是那一句。
2. **缺檔會回退到預設語音包。** 組好路徑後以檔案存在檢查判斷，
   不存在就改用預設角色名重組。這解釋了 `sounds01` 只含 9 個角色卻不出錯。
   —— 又一次印證「**資源缺席不等於功能壞掉**」（同 §5d-7 的 fallback）。

**一個我刻意沒有連起來的關係。** 本輪見到三個音源根
`sounds00`(14 角色)／`sounds01`(9)／`sounds80`(15)，
而 §5c-2b 的語音**商品**是 11 個套組（`Voice F`/`A`/`S`/…）。
**3 對 11，維度與數量都不合**，本輪沒有任何證據能把某個根對應到某個套組。
雖然「音源根＝可購買的語音包」聽起來很順，但**沒有證據就不寫**，維持 UNRESOLVED。
（同 §5b-20 `HonorMedalPosition`、§5b-23 `commonProperty` 的克制。）


### 5b-35. 第三十一輪：任務分類 —— 資料列自己寫出了 Wiki 的分類名

§2b 早已解出 `Quest.pat` 的 47 欄與「quest_id = 類別×10000+序號」，
但**類別的語義一直空著**。本輪以
[クエストシステム](https://wikiwiki.jp/paperman/クエストシステム)（2016-02-19）對照後補上。
詳 [`RESOURCES.md` §2b2](RESOURCES.md)。

| Wiki 敘述 | 比對結果 |
|---|---|
| 任務分 **デイリー**（每日 3 條、全服共通）與 **フリー**（玩家自選最多 3 條），另有イベント／キャラクター限定 | **前綴完全對應**：`3xxxx`=每日(246)、`2xxxx`=自選(578)、`4xxxx`=活動(20)。 |
| — | **不是推測**：每列的 `QuestTermDescription` 首段**自己寫著分類名**，`3xxxx` 群 **246/246 全部**是 `デイリークエスト`，零例外。 |
| 「毎日午前4時に更新、期限は次の午前4時まで」 | **相容且解釋了一個異常**：`3xxxx` 的 `LimitDate` **全為 0**。原本看起來像缺資料，實際是**期限由伺服器每日重置決定，不寫在列裡**。對照之下 `2xxxx` 的 `LimitDate` 分散（180/1200/1440…）＝每條自帶時限。 |
| キャラクター限定クエスト | `CharacterType` 非 0 的 238 條**全部落在 `2xxxx`**，值域恰為 **1..14**，與 §5d-6 角色表一致。 |
| 每日 3 條／自選最多 3 條的**數量上限**、凌晨 4 時重置、獎勵進 present box | **client 無證據**，全屬 server policy，維持 UNRESOLVED。 |

**描述文字與旗標互相獨立地一致。** `2xxxx` 中標為 `一回タイプ` 的
374/392 條 `QuestRepeat=0`，標為 `連続タイプ` 的 165/166 條 `=1`。
兩個欄位由不同人維護卻高度吻合，所以分類不是巧合。

**第四次指向同一個角色。** `CharacterType` 的值域**沒有 15（ルーシー）**。
連同 §5d-7 缺 convars、§5c-2b 缺語音商品、§5c-2c 缺稱號鏈，
**四個互不相干的檔案一致缺席** —— 這是本專案對單一角色最強的一組交叉證據。
仍依 §5d-7 告誡**不推廣成「越晚的角色資源越少」**（エリス／ルコット 反例仍成立）。

## 6. 本輪瀏覽頁面（來源索引）

本索引記錄已閱讀的主題入口，避免日後把搜尋摘要誤當完整頁面內容；個別頁的 last-modified
時間不同，任何數字都需取該 client/resource revision 的實證。

- 根導航／分類：[首頁](https://wikiwiki.jp/paperman/)、[page index](https://wikiwiki.jp/paperman/::cmd/list)
- 商店與取得：[通常ショップ武器一覧](https://wikiwiki.jp/paperman/通常ショップ武器一覧)、[ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細)、[ペーパチ CASH](https://wikiwiki.jp/paperman/ペーパチCASH詳細)、[ペーパチ PG](https://wikiwiki.jp/paperman/ペーパチPG詳細)、[福袋詳細](https://wikiwiki.jp/paperman/福袋詳細)、[ペーパーガッチャン詳細](https://wikiwiki.jp/paperman/ペーパーガッチャン詳細)、[ペーパダスEX詳細](https://wikiwiki.jp/paperman/ペーパダスEX詳細)、[パッケージ詳細](https://wikiwiki.jp/paperman/パッケージ詳細)、[シリアルコード詳細](https://wikiwiki.jp/paperman/シリアルコード詳細)
- 持久化與選擇：[ひよこ用/ゲーム起動編](https://wikiwiki.jp/paperman/ひよこ用/ゲーム起動編)、[階級関連](https://wikiwiki.jp/paperman/階級関連)、[ペーパースロット詳細](https://wikiwiki.jp/paperman/ペーパースロット詳細)、[称号一覧](https://wikiwiki.jp/paperman/称号一覧)、[ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧)、[ペーパズル](https://wikiwiki.jp/paperman/ペーパズル)、[スキル一覧](https://wikiwiki.jp/paperman/スキル一覧)、[ペーパズル合成](https://wikiwiki.jp/paperman/ペーパズル/合成)、[ペーパズルリスト](https://wikiwiki.jp/paperman/ペーパズル/リスト)、[キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧)、[リサイクルシステム](https://wikiwiki.jp/paperman/リサイクルシステム)、[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報)
- **第廿九輪新增**：[称号一覧](https://wikiwiki.jp/paperman/称号一覧)（2018-08-29）—
  活動／購入／クエスト／キャラクター限定 四類稱號，含鏈式解鎖與各階天數；
  對應 `itemdata.pat` NAME 段 `15304001..15305000`（見 §5b-33）。
- **第三十輪新增**：[ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧)（2016-07-31）
  與子頁 [／初期](https://wikiwiki.jp/paperman/ラジオチャット一覧/初期)（2013-03-13）—
  每角色 Z(指揮)/X(戦術)/V(報告) × キー1..9 的台詞表、聲優名單、取得方式；
  對應 `Extracted/sound/sounds*/*/Radio_Message/`（見 §5b-34）。
- **第三十一輪新增**：[クエストシステム](https://wikiwiki.jp/paperman/クエストシステム)（2016-02-19）—
  デイリー（每日 3 條）／フリー（自選 3 條）／イベント／キャラクター限定 四類、
  凌晨 4 時重置、獎勵進 present box；對應 `Quest.pat` 的 `2/3/4xxxx` 前綴（見 §5b-35）。
- 對戰／社交：[MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細)、[出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧)、[名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ)、[クエストシステム](https://wikiwiki.jp/paperman/クエストシステム)、[クラン](https://wikiwiki.jp/paperman/クラン)、[PvEモード](https://wikiwiki.jp/paperman/PvEモード)、[アシストポイント機能](https://wikiwiki.jp/paperman/アシストポイント機能)、[戦闘中のキャラ情報](https://wikiwiki.jp/paperman/戦闘中のキャラ情報)、[武器移動速度](https://wikiwiki.jp/paperman/武器移動速度)

**第二輪（本節 §5b 的來源）。** 服務已於 2016-12-26 12:00 終止（首頁公告），
故全站均為歷史資料，日期差異必須逐頁檢查：

- [トーナメント](https://wikiwiki.jp/paperman/トーナメント)（2013-10-18）— clan 對抗賽賽制、
  5 人門檻、各曜日模式與賽程；對應 756–777 的 22 個 `*_TNMT_*` opcode。
- [試し撃ちシステム](https://wikiwiki.jp/paperman/試し撃ちシステム)（2022-04-15 編輯，
  描述 2012-08-16 實裝）— 2 分限時、不可補彈、預設裝備四件組。
- [アシストポイント機能](https://wikiwiki.jp/paperman/アシストポイント機能)（2015-02-22）—
  assist 行為分類，對應 `sub_6750B0` 的事由碼。
- [よくある質問や答え](https://wikiwiki.jp/paperman/よくある質問や答え)（2026-09-06 仍在編輯）—
  PG 取得規則（擊殺 12PG、拾取 8PG）、連續 kill 10 秒判定、解析度固定 1024×768、
  無墜落傷害、友軍傷害為零但特殊效果仍作用。**全屬歷史 service/client 敘述**，
  僅列為後續查證線索，未採用。
- [用語集](https://wikiwiki.jp/paperman/用語集)、[MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細)（2015-10-23）—
  房間資訊欄位與模式抽出／過濾器行為（見 §5b-5）。

## 7. 下一輪的精確交叉驗證順序

1. 對每個尚未完成的 shop request，先找**所有** `Packet::possible_ctor_or_dtor_0` writers、
   UI caller、state gate、ACK reader及其 cache/inventory/account consumer；將每個候選 frame
   及版本差異登錄到 `LAYOUTS_REQ.md`／`PACKETS.md`，不先寫 parser。
2. 對 `main:Extracted/`，建立可重現的 item-ID proof chain：`ItemData` category、date/flag、
   asset existence、shop/recommend/package XML references、weaponparts compatibility、以及 native
   lookup。這些需逐一標示「display only」或「authority candidate」。
3. 對 gift、present、serial、quest、PvE reward 建立同一個 lifecycle matrix：建立者、owner、
   inventory materialization 時點、cache response、重放/duplicate、expiry、錯誤結果。
4. 在 server 實作前，捕捉/重建一個原版成功與一個拒絕互動，確認 response 長度、status 值、
   account update 和後續 refresh。不足時保留 fail-closed，不能以本文件補洞。
5. UDP 仍受既有 scope gate：約束為先徹底解析 `sub_596670` 與其關聯 state/transport；本 Wiki
   的 pickup、objective、timing 文字不構成 UDP 格式或 authority 證據。
