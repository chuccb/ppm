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

## 5b. 第二輪 Wiki 閱讀：四項可交叉驗證的新發現

> 本節每一項都先有 Wiki 觀察，再獨立以 `PaperMan.exe.c` 或 `Extracted/` 驗證。
> 凡只有 Wiki 單方敘述者一律留在 UNRESOLVED，不寫進本節。

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
掉落表、Lv1–3 效果值（如武器強化 25 秒 200%）皆無 client-side 權威 reader，
屬 server policy，維持 UNRESOLVED。

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

### 5b-5. 兩項「僅 Wiki、刻意不採用」的記錄

- **房間資訊欄位。** [MAP・ルール詳細](https://wikiwiki.jp/paperman/MAP・ルール詳細)
  列出右鍵房間可見的欄位（鎖、房號、房名、模式、地圖、勝利條件、限時、經過時間／回合數、
  道具戰、平衡、洗牌、local rule、刀戰、crazy play、no-skill）。其中「個人/隊伍生存、
  スチール、パルプ＆ロール、足球顯示經過時間；戰術、爆破、占領、PVE 顯示回合數」是**模式分類**線索，
  與既有 `modeIndex` 對照相容，但房列表 wire 欄位仍以 `LAYOUTS.md` 為準，不據此增欄。
- **経験値表。** [階級関連](https://wikiwiki.jp/paperman/階級関連) 有 Lv1→2 需 1000、
  累計 2500/4000/5500… 的完整表與各級獎勵。**不採用**：這是 2017-03-30 的歷史頁，
  且升級獎勵屬 present grant 政策；本 revision 無 client-side EXP 表 reader 可交叉驗證。

## 6. 本輪瀏覽頁面（來源索引）

本索引記錄已閱讀的主題入口，避免日後把搜尋摘要誤當完整頁面內容；個別頁的 last-modified
時間不同，任何數字都需取該 client/resource revision 的實證。

- 根導航／分類：[首頁](https://wikiwiki.jp/paperman/)、[page index](https://wikiwiki.jp/paperman/::cmd/list)
- 商店與取得：[通常ショップ武器一覧](https://wikiwiki.jp/paperman/通常ショップ武器一覧)、[ペーパチ詳細](https://wikiwiki.jp/paperman/ペーパチ詳細)、[ペーパチ CASH](https://wikiwiki.jp/paperman/ペーパチCASH詳細)、[ペーパチ PG](https://wikiwiki.jp/paperman/ペーパチPG詳細)、[福袋詳細](https://wikiwiki.jp/paperman/福袋詳細)、[ペーパーガッチャン詳細](https://wikiwiki.jp/paperman/ペーパーガッチャン詳細)、[ペーパダスEX詳細](https://wikiwiki.jp/paperman/ペーパダスEX詳細)、[パッケージ詳細](https://wikiwiki.jp/paperman/パッケージ詳細)、[シリアルコード詳細](https://wikiwiki.jp/paperman/シリアルコード詳細)
- 持久化與選擇：[ひよこ用/ゲーム起動編](https://wikiwiki.jp/paperman/ひよこ用/ゲーム起動編)、[階級関連](https://wikiwiki.jp/paperman/階級関連)、[ペーパースロット詳細](https://wikiwiki.jp/paperman/ペーパースロット詳細)、[称号一覧](https://wikiwiki.jp/paperman/称号一覧)、[ラジオチャット一覧](https://wikiwiki.jp/paperman/ラジオチャット一覧)、[ペーパズル](https://wikiwiki.jp/paperman/ペーパズル)、[スキル一覧](https://wikiwiki.jp/paperman/スキル一覧)、[ペーパズル合成](https://wikiwiki.jp/paperman/ペーパズル/合成)、[ペーパズルリスト](https://wikiwiki.jp/paperman/ペーパズル/リスト)、[キャラクター一覧](https://wikiwiki.jp/paperman/キャラクター一覧)、[リサイクルシステム](https://wikiwiki.jp/paperman/リサイクルシステム)、[武器耐久値情報](https://wikiwiki.jp/paperman/武器耐久値情報)
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
