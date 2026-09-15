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

## 6. 本輪瀏覽頁面（來源索引）

本索引記錄已閱讀的主題入口，避免日後把搜尋摘要誤當完整頁面內容；個別頁的 last-modified
時間不同，任何數字都需取該 client/resource revision 的實證。

- 根導航／分類：[首頁](https://wikiwiki.jp/paperman/)、[page index](https://wikiwiki.jp/paperman/::cmd/list)
- 商店與取得：[通常ショップ武器一覧](https://wikiwiki.jp/paperman/通常ショップ武器一覧)、[ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細)、[ペーパチ CASH](https://wikiwiki.jp/paperman/ペーパチCASH詳細)、[ペーパチ PG](https://wikiwiki.jp/paperman/ペーパチPG詳細)、[福袋詳細](https://wikiwiki.jp/paperman/福袋詳細)、[ペーパーガッチャン詳細](https://wikiwiki.jp/paperman/ペーパーガッチャン詳細)、[ペーパダスEX詳細](https://wikiwiki.jp/paperman/ペーパダスEX詳細)、[パッケージ詳細](https://wikiwiki.jp/paperman/パッケージ詳細)、[シリアルコード詳細](https://wikiwiki.jp/paperman/シリアルコード詳細)
- 持久化與選擇：[ひよこ用/ゲーム起動編](https://wikiwiki.jp/paperman/ひよこ用/ゲーム起動編)、[階級関連](https://wikiwiki.jp/paperman/階級関連)、[ペーパースロット詳細](https://wikiwiki.jp/paperman/ペーパースロット詳細)、[称号一覧](https://wikiwiki.jp/paperman/称号一覧)、[ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧)、[ペーパズル](https://wikiwiki.jp/paperman/ペーパズル)、[スキル一覧](https://wikiwiki.jp/paperman/スキル一覧)、[ペーパズル合成](https://wikiwiki.jp/paperman/ペーパズル/合成)、[ペーパズルリスト](https://wikiwiki.jp/paperman/ペーパズル/リスト)、[キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧)、[リサイクルシステム](https://wikiwiki.jp/paperman/リサイクルシステム)、[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報)
- 對戰／社交：[MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細)、[出現アイテム一覧](https://wikiwiki.jp/paperman/出現アイテム一覧)、[名誉ゲージ](https://wikiwiki.jp/paperman/名誉ゲージ)、[クエストシステム](https://wikiwiki.jp/paperman/クエストシステム)、[クラン](https://wikiwiki.jp/paperman/クラン)、[PvEモード](https://wikiwiki.jp/paperman/PvEモード)、[アシストポイント機能](https://wikiwiki.jp/paperman/アシストポイント機能)、[戦闘中のキャラ情報](https://wikiwiki.jp/paperman/戦闘中のキャラ情報)、[武器移動速度](https://wikiwiki.jp/paperman/武器移動速度)

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
