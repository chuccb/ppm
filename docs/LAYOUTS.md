# LAYOUTS：Packet wire-layout 原語總表（S2C 306＋C2S 261＋私人 UDP＋全 op 索引）

> **這份文件是什麼**：`PaperMan.exe` wire 面的 opcode 級中心目錄。每個 packet
> 至少可在其中一處查到 native 事實：**Part I**（S2C 收端讀取原語，dispatcher
> `sub_58B010` 306 case 全覆蓋）、**Part II**（C2S 送端 builder 寫入原語，
> 261 unique op）、**Appendix A/B**（私人 UDP 送端 15 op／收端 22 case，
> 獨立命名空間）、**Part III**（全 opcode 覆蓋總表：官方 676＋無官方名 76，
> 共 752 列索引）。語意、consumer 與 server policy 界線在
> [`PACKETS.md`](PACKETS.md)；本檔只管 native primitive 證據與命名狀態。
>
> **建議讀法**：找「某個 op 在哪裡」→ 直接翻 **Part III**（按數值排）；
> 查「欄位怎麼讀/寫」→ 回 Part I/II/App 該列；查「這個 op 選什麼命名」→
> 各審計節（Part I、Part II、PACKETS §2.6 命名總表）。
>
> **圖例（全文通用）**：
> - 名稱狀態：`官方名`＝tsv catalog（Fact）；`〔推定〕`＝native 證據鏈比照官方
>   風格之推定名（審計節為唯一定義處）；`〔未命名〕`＝**明確保留不命名**
>   （UNRESOLVED，非缺漏；理由見各審計節／PACKETS §2.6）。
> - 型別速記 S2C（讀取 helper）：u8=sub_592940/592980 · s8/bool=592900 ·
>   u16=592A00 · s16=5929C0 · s32=592A40 · u32=592A80 · raw4=592AC0 ·
>   f32=592B40 · u64=592B00/592B80 · str=592730 · wstr=5927B0 · raw16=592C40。
> - 型別速記 C2S（寫入 helper）：u8=592920/592960 · s8=5928E0 · u16=5929A0 ·
>   s16=5929E0 · s32=592A20 · u32=592A60 · f32=592B20 · u64=592AE0/592B60 ·
>   raw4=592AA0/592AC0（caller-defined） · str=5926F0。
> - ⚠ 兩張主表是「原語序列」而非精確語法：條件分支／迴圈／可選尾段會改變
>   實際 wire 形狀；精確語意以 `PACKETS.md` 手工條目為準。
>
> **覆蓋率儀表板**（機器複驗；`python3 tools/verify_dispatcher_coverage.py` 可重跑）：

| 集合 | 數量 | 收錄處 |
|---|---:|---|
| 官方 catalog 名錄（670 binary 註冊＋6 UI 補名） | 676 | 名稱來源（`db/packets.tsv`） |
| S2C dispatcher case 覆蓋 | 306/306 | Part I（307 列＝306＋switch 外 196） |
| C2S TCP builder（unique op；321 direct sites） | 261 | Part II |
| 私人 UDP 送端 builder（19 direct sites） | 15 op | Appendix A |
| 私人 UDP 收端 dispatcher case | 22 | Appendix B |
| 有 native 證據但無官方名 | 76（TCP 46＋UDP 30） | Part III 標〔推定〕/〔未命名〕 |
| 官方有名但無已定位 native endpoint（registry-only） | 150 | Part III 標注 |
| 數值 op 全宇宙 | 752 | Part III 全收錄 |

> **switch 外處理**：`681`／`694` 由 login TCP 層專屬 handler 處理（詳
> `S2C_NATIVE_AUDITS.md` 681 part）；`196` 經 vtable 前置轉發、非
> `sub_58B010` switch case，但仍列於 Part I 表。
>
> **沿革（2026-09-17 合併）**：本檔由原 `LAYOUTS.md`（S2C 收端）與
> `LAYOUTS_REQ.md`（C2S 送端＋UDP Appendix A/B）合併；Appendix A/B 標題與
> 表格行維持 byte-identical，作為 verifier 的 marker 錨定。Part I／Part II
> 命名審計各半份，互不覆蓋。

---

## Part I — S2C 收端：dispatcher 讀取原語清單（306/306 全覆蓋＋dispatch 外 694）

---

> 由自動抽取器產生: 對每個 handler 抽出 sub_592xxx 讀取原語序列。
> 已與 5 個歷輪手工佈局抽查比對全部吻合 (106/118/120/122/142)。
>
> **覆蓋率 (本輪機器複驗)。** 主 dispatcher `sub_58B010` 實際有 **306** 個
> `case`（標題原寫 300，已更正）。本輪逐一比對後補進先前遺漏的三筆 ——
> **417**（`MASTER_KILLALL_ACK`，dispatcher inline 無獨立 handler）、
> **803**（`GS_DESTROYITEM_ACK`）、**882**（`GP_CHPLAYTIMEC_ACK`，inline 差分）
> —— 現為 **306/306 全覆蓋**。可用
> `python3 tools/verify_dispatcher_coverage.py` 重驗。
>
> 本表另含 dispatcher 以外的 S2C（例如走 vtable 前置轉發器者），
> 故列數多於 306。名稱欄原留空之列經 2026-09-17 S2C 未命名列稽核後：
> 367/970/991 先前是對 `db/packets.tsv` 的 stale 空白，已回填官方名；
> 17 列依 native 證據鏈推定命名（標 `〔推定〕`；含 880/914/946/947/949 第二波）；
> 933、1007、1009
> 的 handler 於本 dump 無函式體（`unknown_libname_94/95/105`），
> 489、1010 語義證據不足 —— 此五列以 `〔未命名〕` 明確標示，詳
> 〈S2C 推定命名審計（2026-09-17）〉節，
> 屬正確標示而非缺漏。
>
> 型別對照: u8=sub_592940/592980, s8/bool=592900, u16=592A00,
> s16=5929C0, s32=592A40, u32=592A80, raw4=592AC0 (caller determines
> semantics; 142/144/196 contain both signed/raw4 fields—see bootstrap notes),
> f32=592B40, u64=592B00/592B80,
> str=592730 (NUL ANSI), wstr=5927B0 (UTF-16),
> raw16=592C40。
>
> ⚠ 此表為「讀取序列」非精確佈局: 條件分支/迴圈會使實際 wire 依
> 內容變化 — 精確語意以 PACKETS.md 手工條目為準; 本表用於快速
> 查閱與覆蓋保證（306 個 dispatcher case，另含 6 個非 sub 直呼）。


### Bootstrap 欄位 native 交叉核對（2026-09；自動讀取序列之外的精確語意）

| op | 自動讀取序列之外的精確欄位語意 | Native 證據 |
|---:|---|---|
| 142 | `str endpoint_host`（client `char[20]`）、port 為 raw4/s32（實際只用低 u16）、`u8 active_channel_index`、再一個打包日曆 `u32`：`(year-2000)<<24 \| month<<19 \| day<<13 \| hour<<7 \| minute`。 | `sub_5565D0`, `sub_534F20` |
| 144 | `u8 result`、`u8 rank 限制旗標`、`s32 每日登入 PG 通知`、`str[40] channel`、兩個讀而未用的 `s32`、等級 `s32`、K/D `f32`、透傳的 raw4 request context、`u8 has_net_cafe`，存在時恰為 `u8×4 + s32×8`。 | `sub_555D50`, `sub_A1C800`, CP932 msg table ids 0xC9/0x11C/0x31B… |
| 196 | 前綴恆為 `u8 result, s32 channel_id, u8 active_channel_index`；七欄 endpoint 尾段**僅在 result==1** 存在。 | `CLobbyChannel::sub_4179D0`, `sub_4177B0` |
| 693 | 空 packet；handler 顯示訊息 0xFF 後立即構成並送出 143。 | `sub_57CAE0`, `sub_555C60` |

`694` 由 `CLobbyLogin::sub_43E500` 在本 dispatcher 表之外處理：其 u16 僅在
`<0x2580` 時替換預設 9600 壓縮門檻，達門檻上限即忽略。`681` 的 result 以
raw4 讀取，但分支選擇器取其低位元組。完整佈局見 `PACKETS.md` §1.4 與 §3.15d。

### S2C 推定命名審計（2026-09-17）

> **對象**：2026-09 時點本表 25 個名稱空白列。**方法與規則**比照
> 本檔 Part II〈推定命名審計（2026-09-17）〉節：
> ① dispatcher `sub_58B010` case 本體 → handler 函式體逐案抽取核對
> （brace-match）；原自動抽取誤記者已校正（880 誤取 getter
> `sub_407E00`；203、914、1010 讀取序列見下表與主表）；
> ② 語意錨點＝dev 標籤（`GameNetwork::On…`）、結果分支字串、音效路徑、
> RTTI、韓文 log（EUC-KR/cp949；本 dump 已損毀成 `?` 者不復原、不引證）；
> ③ 對向 C2S 僅引用本檔 Part II 本週已定名之列；
> ④ `db/packets.tsv`（676-catalog）僅用於回填與鄰接檢查，不作命名來源；
> ⑤ handler 本體＋語意錨點＋配對鏈三者齊備才命名，否則保留空白
> （UNRESOLVED 者附行為紀錄）。
>
> **結果**：367／970／991 為 tsv Fact 回填（先前 stale 空白）；
> 17 列推定命名（主表標 `〔推定〕`；對照表實測 17 rows）；**1010 於** **
> 2026-09-19 升格（sub_5680E0 行級 → assist-point 增量 assign）**；489、933、
> 1007、1009 明確保留 unnamed（489=bit0 死路徑級 UNRESOLVED、933/1007/1009=
> lib thunk 無函式體可恢復,屬終局既判）。

**tsv 回填（Fact）**

| op | 名稱（tsv Fact） | 行為核對 |
|---:|---|---|
| 367 | `GR_LOCALROOM_ACK` | handler `sub_586090` 讀 u8 → `sub_437B50` 勾選 GAMEROOM_LOCALROOM；對向 366 `GR_LOCALROOM_REQ`（tsv；下游詳 PACKETS.md §3.15b2） |
| 970 | `GR_SOCCER_ACK` | `sub_586180` → `sub_437D00` 寫 mode rule +14 足球旗標（`sub_74F4D0`）並勾選 GAMEROOM_SOCCER；對向 969 `GR_SOCCER_REQ`（tsv；§3.15b2） |
| 991 | `GR_DAMAGEROOM_ACK` | `sub_56FA00` → `sub_430FD0` 寫 room+128 double_damage＋GAMEROOM_DAMAGEROOM UI；對向 990 `GR_DAMAGEROOM_REQ`（tsv；§3.15b2） |

**推定命名對照表**（對向 REQ 之〔推定〕標記沿用本檔 Part II 審計結果）

| op | 推定名 | 等級 | 關鍵證據（摘要） |
|---:|---|---|---|
| 946 | `GR_AI_UDPSENDER_CHANGE_START_NOTIFY` | HIGH | handler 內 dev 標籤 ASCII 完好：`GameNetwork::OnGRAiUDPSenderChangeStartNotify`；`u8 v4(預設 -1,KR 標籤損毀→**僅診斷 log 消費**,無狀態依賴), s32 playno(預設 -1,log 本名)`；本機玩家 `v3=*(n0x10_0+12)` 存在則 `sub_75D550(v3,1)` + `sub_764910(v3,1)` 兩 setter 置位(UDP sender change 進入) |
| 947 | `GR_AI_UDPSENDER_CHANGE_END_NOTIFY` | HIGH | dev 標籤 `…EndNotify`;`u8 oldSlot(預設 -1), u8 newSlot(預設 -1), s32 v9(讀後未用=協議保留)`：舊 sender 經 `sub_67D7D0(oldSlot)` 取玩家記錄(`byte_F33120+240780×v`;名欄於 +64,log 「?UDP SENDER?」用之)→`sub_548AE0(row,0)` 除位，新 sender →`(row,1)` 置位；本機玩家兩 setter 歸 0;**`sub_6054A0` 重綁有閘**:`sub_67EB70()` 且 `newSlot==sub_67D110()`（本機成為 sender）才執行 |
| 949 | `GR_AI_MULTI_SHIELD_NOTIFY` | HIGH | cp949 字串完好 `[%s] AI Multi Shield 무적 %s!!`（무적＝無敵）系統列廣播；u8 旗標：bit0=On/Off、`n2>=2`→`All_Channel` 否則 `This_Room`(sub_58EF00 行級，僅此一讀、無狀態寫入——**純 GM 廣播訊息組字串 echo**)；本端無對向 C2S（GM／伺服器觸發）；`AI_MULTI` 詞彙依 941 `MASTER_GO_AI_MULTI_WAVE_DIRECTLY_REQ`（tsv），`GR_AI_…SHIELD` 家族先例 922/923 `GR_AI_DAMAGE_SHIELD_REQ/ACK`（tsv）；全頻道廣播不改 GR 前綴有 911 `GL_SCHEDULED_GM_NOTICE_NOTIFY`（tsv）先例 |
| 931 | `GR_AI_CONTINUE_FAIL_ACK` | HIGH | `u8 result,[u8 slot if result==1]`（sub_762170 行級，僅 0/1 兩分派):**result==0**→PvE mgr vtbl+88=2＋播 `pve_01_sounds\AI3_continue_fail.wav`＋`sub_8EED70` 繼續 UI 關閉（1,1);**result==1**→讀 `u8 slot`→`sub_67D8F0(slot,0)` 取玩家列（與 1010/913 同 slot accessor 家族），當 `sub_67DD90(player)==sub_67D1D0()`（該列掛於全域 PvE mgr）時設 continue 狀態 2 並驅動 `sub_8DEFF0`；其餘值無操作；handler log 為 KR 接收用語（continue 취소)，名仍鏡像對向 930 `GR_AI_CONTINUE_FAIL_REQ`〔推定〕 |
| 954 | `MASTER_PVE_ACK` | HIGH | `s8 pveOn`(0=Off，非 0=On;sub_57DA20 行級：0/!=0 兩分支）→**全域鏡像 `byte_1D0D21B`（暫名 PVE_ON bool)**,log `PVE On/Off Succ!!`；同全域亦由 `CLobbyChannel::sub_4179D0`（頻道封包 u32 bit0）寫入 ⇒ 頻道原生 PvE 旗標；消費者=`sub_4292B0` 與 `CUIComplexControl::sub_50D820` 兩 UI 清單組建器：==1 時追加 **msg 1240**「ふっ...もうあらゆることが終わったよ...屋上のヘリコプターだけ乗れば...」(PvE 劇情風味行，msgtableres.lang)；對向 953 `MASTER_PVE_REQ`〔推定〕（`/pveon`、`/pveoff`) |
| 976 | `MASTER_SETMULTIPLYDAMAGE_ACK` | HIGH | `s8 result`(sub_57D660 行級，僅 0/≠0 兩分派):≠0→`SET DAMAGE SUCCESS!!`(3000ms),==0→`SET DAMAGE FAILED!! INVALID SERVER INDEX!!`(3000ms)——**純系統列 echo,無狀態寫入**;失敗文案坐實 975 首參=server index；對向 975〔推定〕（`/setmultiplydamage <int> <float>`) |
| 852 | `MASTER_RELOAD_GAMECENTER_RANKING_ACK` | HIGH | u8==1→`GAME CENTER RANK RELOAD SUCCESS`，否則 FAIL（系統列 `sub_541BF0`）；對向 851〔推定〕（`/reloadgcrank`） |
| 997 | `GL_BLOCK_ADD_ACK` | HIGH | u8 結果驅動訊息：0→**1320**「ブラックリストに登録しました。」+重送 1000 / 1→**1323**（重複）/ 2→**282**（查無此人）/ 3→**1322**（名額滿）/ 4→**1330**（不能封自己）；對向 996〔推定〕（msgtableres.lang 行級，2026-09-19) |
| 999 | `GL_BLOCK_DEL_ACK` | HIGH | `u8 結果, str nick`：0→真刪(`sub_539B60/539680`)+重送 1000+**1325**「%sさんをブラックリストから解除しました。」 / 1→**1326**「ブラックリストから解除できません。(24時間）」**=24h 冷卻** / 3→**182**「ID不存在」；對向 998〔推定〕(msgtableres.lang 行級) |
| 1001 | `GL_BLOCK_LIST_ACK` | HIGH | `u16 v23, str v14, s32 count, count×{s32 v5, str nick}`(sub_567D50 行級： 先 `sub_53A460(dword_EE8C90)` 清表再逐筆 `sub_5395E0(mydata, nick, v5)` 寫入，尾 `dword_EA131C+132` UI 刷新）；對向 1000〔推定〕 |
| 1003 | `GL_BLOCKME_LIST_ACK` | MEDIUM-HIGH | `u16, str, s32 count, count×str nick`(sub_567BD0 行級： `sub_53A6E0` 清→`sub_539360` 逐筆）；「別人封鎖我」方向語義為 Inference，功能面確定；對向 1002〔推定〕 |
| 998 | `GL_BLOCK_DEL_REQ`〔推定〕 | HIGH | `{str nick}`(sub_568030,5926F0)；對向 999 |
| 999 | `GL_BLOCK_DEL_ACK` | HIGH | `{u8 code, str nick}`(sub_568170):0→**1325**「%sを解除」+`sub_539B60/539680` 真刪除+重送 1000 / 1→**1326**「24時間は解除できません。'（**24h 冷卻**)/3→**182**（查無） |
| 1010 | assist-point 增量 assign 推送〔推定〕 | HIGH | `u8 count, count×{u8 slot, s32 value}`(sub_5680E0 行級： `sub_67F380()` 存在閘→`sub_67D8F0(slot,0)` 取列→`v3[60194]=value`);+60194 與 **994 `GG_ASSISTPOINT_NOTIFY`**(tsv Fact）同槽，且 272965 送至 `sub_65FD40`(MY_RESULT_OCCUPYPOINT) ⇒ 增量式 assist/occupy-point 指派 |
| 1005 | `GL_RANDOMMAP_LIST_ACK` | HIGH | `u8 count`×{u8 mode, u8 mapId} 隨機地圖清單；對向 1004〔推定〕；官方 RANDOMMAP token 先例 748 `GR_SELECTRANDOMMAP_ACK`（tsv） |
| 488 | `GL_MYROOMCHANGE_ACK` | MEDIUM-HIGH | u8 結果：==1→再讀 u8 slot 寫入 `*sub_417D00()`+0（`CLobbyChannel` 狀態位元組）；!=11→大廳 UI 還原 `sub_44C1D0`；對向 487〔推定〕 |
| 958 | `GR_TIMEOVER_ONGAME_ACK` | HIGH | dev 標籤 `GameNetwork::OnGRTimeOverOnGameACK`（REQ 審計已錄）字尾 ACK；handler 不讀 payload，收到即回送 957 `GR_TIMEOVER_ONGAME_RESPON_REQ`〔推定〕（s8＝剩餘秒數歸零與否） |
| 203 | `GL_MYAVATARINFO_ACK` | MEDIUM-HIGH | 讀取序列修正：`u8 count(≤4)`，每筆 `{u8 tag, u16, [3×u16 if tag!=3], [8×raw4 if u16!=0]}`，經 `sub_571D50→sub_524660` 灌入全域 4 槽×44B `p_p_p_p_p_n1189`；消費者＝`GAMEROOM_AVATAR` 3D 預覽 `sub_6A9950` 與 `GAMEROOM_MAIN_GUN_%d0` 面板（12 個 accessor）；無 userKey＋自角色情境 → 自身 avatar／裝備資料推送（server push，無對向 REQ） |
| 880 | `GQ_QUEST_ACCEPT_DAILY_NOTIFY` | MEDIUM-HIGH | handler 更正＝`sub_91DC50`（case 本體為 `sub_407E00(); sub_91DC50(packet);`，原表誤取 getter）；不讀 payload，KR log（已損毀）後立即送出 876 `GQ_QUEST_ACCEPT_DAILY_REQ`（tsv Fact）；與官方 877 `GQ_QUEST_ACCEPT_DAILY_ACK`（handler `sub_91D7E0`、讀取空）為不同 case／handler，非別名；876 其他觸發點＝登入大廳流程、任務窗刷新、866 清單 <3 項自動補齊 → server 端每日任務接取提示 |
| 914 | `GG_MISSILE_INFO_NOTIFY` | MEDIUM-HIGH | 讀取序列修正：`repeat {u8 idx(1..0x4F), raw32}`，idx==0 終止、上限 80 筆；`sub_71FBC0` 對 `MissileObjMgr`（RTTI Fact，容器 `dword_1D12414`）活體物件逐 idx 廣播虛擬更新；後綴仿 961 `GG_DROPWEAPON_INFO_NOTIFY`（tsv）；records 後續用途不透明（stack buffer、caller-defined） |

**明確保留 unnamed（行為紀錄，語義 UNRESOLVED）**

- **489**（`sub_57C230`，`u8`）：bit0→清全域 `this_5`（game-room 武器面板
  refresh 閘門）；全 dump 僅兩處寫 0、無任何非零寫入 → 死路徑級，證據不足。
- **933**（`unknown_libname_105`）：本 dump 無函式體，不可回收。
- **1007／1009**（`unknown_libname_94/95`）：16-byte 微 thunk 落於 decompile
  空白區，不可回收；僅與 1006／1008 數字相鄰，依「禁止鄰接臆名」規則不命名。
- **1010**（`sub_5680E0`，`u8 u8 s32`）：count 迴圈 ×{u8 playerKey, s32 v}，
  經玩家表查詢 `sub_67D8F0(playerKey,0)` 寫入 240780-byte 玩家 row 尾端
  +240776（鄰近 +240764 為足球進球數、+240761/62 為持球旗標）；全 dump
  僅初始化與本 handler 寫入、無任何讀取者 → 語義不可得，保留 unnamed。

| op | 名稱 | handler | 讀取序列 |
|---|---|---|---|
| 102 | GT_PING_ACK | sub_58D6F0 | `(無直接讀取/轉發)` |
| 106 | GL_USERLIST_ACK | sub_56A250 | `raw2 gate; if nonzero: u8 flags, u8 count, repeat {raw4 userKey, str nick, s32 exp, if userKey>0: raw4 customTexKey, str texName}` |
| 108 | GL_GAMEROOMINFO_ACK | sub_568CE0 | `u8 u8 u8 s8 u8 s8/bool u8 u16 u8 s8/bool s8/bool s8/bool s8/bool u8 u8 u8 str u8 s8/bool u8 u16 u8 s8/bool s8/bool ...` |
| 110 | GL_ROOMINFOCHANGE_ACK | sub_569240 | `s8/bool u8 u8 u8 u8 u8 s32 s32 str u8 s32 s32 str u8 s32 u8 s32 u8 u8 s8/bool s8 u8 s8/bool u8 ...` |
| 112 | GL_MAKEROOM_ACK | sub_56A7B0 | `u8 u8 u16 f32/s32 u8 s8/bool u8 s32 s32 str u8 s32 s32 str u8` |
| 114 | GL_ENTERROOM_ACK | sub_56B360 | `u8 s32 u8 str s32 s8/bool s32 s32 str u16 u16 u16 u16 f32/s32 u8 f32/s32 u8 s32 str s32 str f32/s32 u8 u8 ...` |
| 116 | GL_ADDUSER_ACK | sub_56A4D0 | `s32 str` |
| 118 | GL_DELETEUSER_ACK | sub_56A550 | `str` |
| 120 | GL_CHATTING_ACK | sub_56E300 | `f32/s32 str wstr` |
| 122 | GR_MAPCHANGE_ACK | sub_56E530 | `u8` |
| 124 | GR_LEAVE_ACK | sub_5607C0 | `u8 u8 s32 str s32 str u8 u8 s32 str s32 str` |
| 126 | GR_CHATTING_ACK | sub_56EA80 | `f32/s32 u8 wstr` |
| 128 | GR_READY_ACK | sub_5626D0 | `s8/bool u8` |
| 130 | GR_START_ACK | sub_562870 | `u8 s8/bool f32/s32 u8 u8 u8 u16 u8 u8 u16 u8 s8/bool s8/bool s8/bool u8 s8/bool f32/s32` |
| 132 | GR_FORCEOUT_ACK | sub_56ECC0 | `s8/bool u8 s32 str s32 str` |
| 134 | GR_END_ACK | sub_562EA0 | `u8 u8 u8 u8 u8 u16 u8 u8 u16 u8 s8/bool u16 s8/bool s8/bool s8/bool s8/bool u8 u8` |
| 136 | GR_CHANGESLOT_ACK | sub_56EF40 | `u8 u8 u8 f32/s32 s32 u8 u8 s32` |
| 140 | GG_EXITGAME_ACK | sub_563430 | `u8 u8` |
| 142 | PM_CONNECT_ACK | sub_5565D0 | `str s32 u8 u32` |
| 144 | PM_UDPSTART_ACK | sub_555D50 | `u8 u8 s32 str s32 s32 s32 f32 raw4 u8 [u8 u8 u8 u8 s32×8]` |
| 160 | TCP_UDP_DEAD_ACK | sub_58D790 | `(無直接讀取/轉發)` |
| 166 | Y_TCP_INF_ACK | sub_58D820 | `(無直接讀取/轉發)` |
| 168 | GR_CHANGEUSER_ACK | sub_56F410 | `u16` |
| 170 | GR_RULECHANGE_ACK | sub_56F4F0 | `u8` |
| 172 | GR_WINCHANGE_ACK | sub_56F5D0 | `u16` |
| 174 | GR_TIMECHANGE_ACK | sub_56F6B0 | `u8` |
| 176 | GR_ITEMCHANGE_ACK | sub_56F790 | `u8` |
| 184 | GR_ENDLOADING_ACK | sub_563B00 | `u8 u8 u8 u8` |
| 188 | GG_STARTGAME_ACK | sub_563D60 | `u8 u8 u8` |
| 190 | GR_CHANGEMASTER_ACK | sub_56FBF0 | `u8` |
| 192 | GR_CALLUSER_ACK | sub_56FE10 | `u8 str` |
| 194 | GC_CHANNEL_ACK | sub_56FE90 | `u8` |
| 196 | GC_ENTERCHANNEL_ACK | sub_4179D0 | `u8 s32 u8 [str s32 u8 u8 u32 u8]` |
| 198 | GL_MYINFO_ACK | sub_570550 | `s8/bool s32 u16 s32 u8 u8` |
| 200 | GL_MYITEM_ACK | sub_570AB0 | `s8/bool success, s32 start, repeat≤100 {s32 slot, s32 item, raw4 f1, raw4 f2, s32 period, u8 extra, u16 durability}, s32 negative-slot sentinel` |
| 201 | GL_MYPARTSUP_ACK | sub_95A3B0 | `s32 count, repeat {raw4 raw4 raw1 raw4 raw4}` |
| 202 | GL_EXPIRE_PARTSUP_ACK | sub_95AE40 | `s32 count, repeat {raw4 raw4 raw1 raw4 raw4}` |
| 203 | GL_MYAVATARINFO_ACK〔推定〕 | sub_571D50→sub_524660 | `u8 count(≤4); repeat {u8 tag, u16, [3×u16 if tag!=3], [8×raw4 if u16!=0]}`（詳審計節） |
| 205 | GS_BUYITEM_ACK | sub_571910 | `u8 s8/bool s32 f32/s32 f32/s32 s32 u8 u16 s8/bool u8 s32 s32 s32 s32 s32 s32 s32` |
| 207 | GS_BUY_WEAPONPARTS_ACK | sub_571B60 | `u8 rawResult; rawResult==0 → 21B part record + 6×s32 wallet tail; nonzero → no tail` |
| 209 | GS_SELLITEM_ACK | sub_572B80 | `s8/bool s32 s32 s32` |
| 211 | GM_CHECKNICK_ACK | sub_572D80 | `u8` |
| 213 | GM_CREATENICK_ACK | sub_572E70 | `u8` |
| 215 | GM_CREATECHAR_ACK | sub_572F80 | `u8` |
| 219 | GI_CHANGEDATA_ACK | sub_573230 | `u8` |
| 221 | GI_CHANGEWP_ACK | sub_5735F0 | `u8 count, count×{u8 group,u16 primary,[3×u16 group!=3],[8×s32 primary!=0]}` |
| 223 | GP_CHPLAYC_ACK | sub_556730 | `s32` |
| 225 | GP_CHROUNDC_ACK | sub_556780 | `s32` |
| 227 | GP_CHDISC_ACK | sub_5567A0 | `s32` |
| 229 | GP_CHWINC_ACK | sub_5567C0 | `s32` |
| 231 | GP_CHLOSSC_ACK | sub_5568B0 | `s32` |
| 233 | GP_CHKILLC_ACK | sub_5569A0 | `s32` |
| 235 | GP_CHDEADC_ACK | sub_5569E0 | `s32` |
| 237 | GP_CHHEADSC_ACK | sub_556A30 | `(無直接讀取/轉發)` |
| 239 | GP_CHACOMBOC_ACK | sub_556A70 | `(無直接讀取/轉發)` |
| 241 | GP_CHHEARTC_ACK | sub_556AF0 | `(無直接讀取/轉發)` |
| 243 | GP_CHDKILLC_ACK | sub_556B30 | `(無直接讀取/轉發)` |
| 245 | GP_CHTKILLC_ACK | sub_556C50 | `(無直接讀取/轉發)` |
| 247 | GL_CLIENTINFO_ACK | sub_573EB0 | `u8` |
| 255 | GL_INVENIN_ACK | sub_574270 | `u8 mode, s32 uid, u8 contextRaw, u8 unknown; self uid→u8 selected(<5), 5×raw32 NewSkill profile` |
| 257 | GL_ENTERROOMOB_ACK | sub_56D420 | `u8 u8 u8 u8 u8 u8 u16 u8 u8 u16 u8 s8/bool u16 s8/bool u8 str s8/bool s8/bool s32` |
| 261 | GL_JOIN_ACK | sub_56DA70 | `u8` |
| 263 | GL_JOINPASS_ACK | sub_56B2E0 | `s8/bool` |
| 265 | GL_JOININFO_ACK | sub_574550 | `s8/bool u8 u8 u8 u16 u8 u8 u16 u8 u16 u8 str s8/bool u8 u8 u8 u16 u8 u8 u16 u8 u16 u8 str ...` |
| 267 | GL_JOINGAME_ACK | sub_5749E0 | `u8 u8` |
| 269 | GL_JOINPLAY_ACK | sub_574B20 | `u8 s32 u8 str u8 s16 s16 s16 s32 s32 s32 str u16 u16 u16 u16 f32/s32 u8 f32/s32 f32/s32 f32/s32 u8 u8 u8 ...` |
| 274 | GR_TSTARTPOS_ACK | sub_563650 | `s8/bool f32/s32 u8 u8 u8 u8 s16 s16 s16` |
| 276 | MASTER_MEMO_ACK | sub_578920 | `wstr` |
| 278 | MASTER_MEMOALL_ACK | sub_578BC0 | `wstr` |
| 280 | MASTER_USERCUT_ACK | sub_578FB0 | `(無直接讀取/轉發)` |
| 284 | MASTER_ROOMCUT_ACK | unknown_libname_96 | `(非 sub 直呼)` |
| 286 | MASTER_MSET_ACK | sub_578D20 | `u8` |
| 288 | MASTER_PRINTUSER_ACK | unknown_libname_99 | `(非 sub 直呼)` |
| 290 | MASTER_USERINFO_ACK | sub_579830 | `str str u8 u8 u8 u8` |
| 292 | MASTER_LISTCUT_ACK | unknown_libname_100 | `(非 sub 直呼)` |
| 294 | MASTER_USERINFODB_ACK | sub_57A540 | `u8 str` |
| 297 | GS_GIVEGIFT_ACK | sub_57AA50 | `u8 s32 s32 s32 s32 s32` |
| 299 | GS_TAKEGIFT_ACK | sub_57AEF0 | `(無直接讀取/轉發)` |
| 301 | GS_MOVEGIFT_ACK | sub_57AFE0 | `u8 u8 s32 s32` |
| 303 | GG_JJCREATE_ACK | sub_5616A0 | `u8 u8 u8 s16 s16 s16 u8` |
| 305 | GG_JJCHANGE_ACK | sub_5618B0 | `u8` |
| 307 | GG_JJGET_ACK | sub_561AC0 | `u8 u8 u8 s32 u8 s32 s32 s32` |
| 309 | GG_JJGAMEEND_ACK | sub_561FB0 | `u8 u8 s32 s32 s8/bool` |
| 311 | GS_BUYCHAR_ACK | sub_5728A0 | `s8/bool s32 s32 s32 s32 s32 s32 u8 s32 s32` |
| 313 | GI_CHANGESLOT_ACK | sub_573320 | `(無直接讀取/轉發)` |
| 315 | GS_MOVEONEGIFT_ACK | sub_57B500 | `u8 u8 s32 s32 str f32/s32 f32/s32 s32 u8 u16 str` |
| 317 | GG_HACKSTART_ACK | sub_557040 | `u8 u8` |
| 319 | GG_HACKSUCC_ACK | sub_557400 | `u8 f32 f32 f32 f32 f32 f32 u8` |
| 321 | GG_HACKFAIL_ACK | sub_557730 | `u8` |
| 323 | GG_BOMBSUCC_ACK | sub_5579A0 | `u8` |
| 325 | GG_BOMBEND_ACK | unknown_libname_88 | `(非 sub 直呼)` |
| 327 | GG_UNHACKSTART_ACK | sub_557D30 | `u8 u8` |
| 329 | GG_UNHACKSUCC_ACK | sub_557F90 | `u8 u8` |
| 331 | GG_UNHACKFAIL_ACK | sub_558250 | `u8` |
| 333 | GG_KILLJJ_ACK | sub_561E40 | `u8 u8 u8 s32` |
| 341 | GR_KILLCHANGE_ACK | sub_56F870 | `u16` |
| 343 | GG_SOLORESPON_ACK | sub_558AB0 | `u8 u8 s16 s16 s16` |
| 345 | GG_LIVECHAT_ACK | sub_58D870 | `(無直接讀取/轉發)` |
| 347 | GG_TEAMCHAT_ACK | sub_58D8A0 | `(無直接讀取/轉發)` |
| 349 | GG_DEADCHAT_ACK | sub_58D8D0 | `(無直接讀取/轉發)` |
| 351 | GG_TEAMDEADCHAT_ACK | sub_58D900 | `(無直接讀取/轉發)` |
| 353 | GG_LEVELJJ_ACK | sub_562310 | `u8 s32` |
| 357 | GS_CASH_ACK | sub_572420 | `u8 rawStatus s32 rawCash` |
| 359 | GS_BUYCASHITEM_ACK | sub_5725D0 | `u8 resultCount s32 rawHeader; resultCount×{u8 itemResult,[itemResult!=0:s32 itemId,s32 rawA,s32 rawB,s32 rawC]}` |
| 361 | GG_TSURRESPON_ACK | sub_558DD0 | `u8 u8 s16 s16 s16` |
| 363 | GP_CHCRITICALC_ACK | sub_556AB0 | `(無直接讀取/轉發)` |
| 365 | GR_BALANCECHANGE_ACK | sub_56FAE0 | `s8/bool` |
| 367 | GR_LOCALROOM_ACK | sub_586090 | `s8/bool` |
| 369 | GR_TEAMSHUFFLECHANGE_ACK | sub_585D90 | `s8/bool` |
| 371 | GL_CHANGECHANNEL_ACK | sub_570100 | `u8 u8 str s32 u8` |
| 379 | GR_RADIOMSG_ACK | sub_559510 (sub_74C500) | `u8 u8 u8 u8 wstr` |
| 381 | GP_CHMKILLC_ACK | sub_556CB0 | `(無直接讀取/轉發)` |
| 383 | GP_CHUKILLC_ACK | sub_556D10 | `(無直接讀取/轉發)` |
| 385 | GP_CHZKILLC_ACK | sub_556D70 | `(無直接讀取/轉發)` |
| 387 | GP_CHKKILLC_ACK | sub_556DD0 | `(無直接讀取/轉發)` |
| 389 | GP_CHDDKILLC_ACK | sub_556E30 | `(無直接讀取/轉發)` |
| 391 | GL_DELETEITEM_ACK | sub_57C270 | `u8 s32 s8/bool` |
| 393 | GL_CHANGEID_ACK | sub_58E3B0 | `(無直接讀取/轉發)` |
| 395 | MASTER_ROOMINFO_ACK | sub_579160 | `u8 u8 str str` |
| 397 | PM_KICKUSER_ACK | sub_58E410 | `u8` |
| 403 | MASTER_EVENTPAGE_ACK | sub_579500 | `f32` |
| 405 | MASTER_EVENTEXP_ACK | sub_579650 | `f32` |
| 417 | MASTER_KILLALL_ACK | *(dispatcher inline)* | `(空)` — 不讀 payload; 顯示 msg 0xA5 後斷線提示 |
| 420 | GL_MSG_ADD_ACK | sub_559810 | `str u8 u8` |
| 422 | GL_MSG_DEL_ACK | sub_55A310 | `s8/bool statusRaw, str key` |
| 424 | GL_MSG_READ_ACK | sub_55A4F0 | `s8/bool statusRaw, str key` |
| 426 | GL_MSG_RECVLIST_ACK | sub_55A630 | `raw2 header, str context, u8 count, repeat {str key, u8 stateRaw, str rawString2, raw4 raw4, str rawString3, str typeString, raw2 raw2}` |
| 430 | GL_FRIEND_ADD_ACK | sub_55AA90 | `u8 statusRaw, str characterName/key` |
| 432 | GL_FRIEND_DEL_ACK | sub_55AE10 | `u8 statusRaw, str characterName/key` |
| 434 | GL_FRIEND_LIST_ACK | sub_55AFC0 | `raw2 header, str context, u8 count, repeat {str characterName/key, raw4 rowRaw4}` |
| 436 | GL_FRIEND_INFO_ACK | sub_55B2C0 | `u8 count, repeat {str key, u8 online, if online: str where, u8 channel}` |
| 440 | GL_FRIEND_CHAT_ACK | sub_55B660 | `u8 str str str` |
| 442 | GL_FRIEND_WHERE_ACK | sub_55B9F0 | `u8 u8 u8 u8` |
| 444 | GG_STEALSUCK_ACK | sub_55BE80 | `u8 u8 u16 u16 u16` |
| 446 | GG_STEALPUSH_ACK | sub_55C120 | `u8 u8 u16 u16 u16` |
| 448 | GG_STEALCOLORS_ACK | sub_55C220 | `u8 u8 u16 u16 u16` |
| 452 | GS_NEWGIFT_ACK | sub_57BE30 | `u16 s32 s32 s32 str` |
| 454 | GS_DELETEGIFT_ACK | sub_57BCF0 | `u8 s32 s32` |
| 456 | GG_EXERCISERESPON_ACK | sub_55C340 | `u8 u8 s16 s16 s16` |
| 458 | GI_CHANGEITEMSLOT_ACK | sub_573860 | `9×s32 UI ids + 3×{u8 rawFlag,s32 itemId,s32 itemStateRaw}` (exact 63B) |
| 462 | GS_USE_PAPERCODEGIFT_ACK | sub_57CC90 → sub_4C4270 | `u8 outcome`; outcome 1 additionally reads `u8 failedAttemptCount`; outcome 4 delegates a duplicate-item popup which consumes a request/context-dependent tail; other outcomes have no direct tail in this consumer |
| 465 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_ACK | sub_57CDD0 → sub_4C44C0 | `u8 outcome` (0=receive success UI, 13=cancel UI, all other values=generic failure UI) |
| 467 | GI_CHANGE_SKILLITEMSLOT_ACK | sub_573A70 | `u8 resultRaw, u8 unknown, u8 count, count×{u8 profile, raw32}` |
| 469 | GS_BUY_HUKUBUKURO_ACK | sub_57CE30 | `u8 status; status==0 → 6×s32; nonzero → no tail` |
| 471 | GS_GET_HUKUBUKURO_ACK | sub_57D210 | `u8 status; status==0 → s32 count, count×{s32 itemId,u8 rawValue}; nonzero → no tail` |
| 473 | GL_GAMECENTER_REC_ACK | sub_584910 | `u16 s32 u8 u8 u8 u8 u16 s32 u8 u8 u16 u16` |
| 475 | GG_GAMECENTER_GAME_START_ACK | sub_584E80 | `(無直接讀取/轉發)` |
| 477 | GG_GAMECENTER_GAME_END_ACK | sub_564A00 (mode gate) → sub_76E450 | `u16, raw32, raw44, raw4, raw24, raw8, 4×raw4, multiple u8 flags` |
| 481 | GG_GAMECENTER_RANKING_ACK | sub_585080 | `u16 u8 u16 s32, u8 count1 + count1×raw56 (max 3), u8 count2 + count2×raw56 (max 10)` |
| 482 | GL_GAMECENTER_COIN_CHANGED_ACK | sub_585F50 | `u16 u16` |
| 484 | GG_GAMECENTER_GAME_START_OK_ACK | sub_584F70 | `u16 u8 u16 s32` |
| 486 | GL_GET_GAMEROOM_PROGRESSTIME_ACK | sub_56AE30 | `u8 u8 s8/bool u8 s8/bool u16 u8 s32 u8 s8/bool u16 u8 s32 u8 s8/bool u8 s8/bool u8 u8 s8/bool s8/bool u8 s8/bool` |
| 488 | GL_MYROOMCHANGE_ACK〔推定〕 | sub_5861C0 | `u8 u8` |
| 489 | 〔未命名〕 | sub_57C230 | `u8`（bit0→清全域 `this_5`；UNRESOLVED，見審計節） |
| 572 | GV_TEST_ACK | sub_58E5E0 | `(無直接讀取/轉發)` |
| 584 | GC_CLAN_PROTOCOL_ACK | sub_54D040 | `s32` |
| 586 | GC_CLAN_CREATE_ACK | sub_54CB90 | `s8` |
| 587 | GC_CLAN_GAMEEND_RESULT_NOTIFY | sub_550410 | `s32 s32 s32 u16 u16 s32 s32 s32 u16 u16` |
| 686 | GL_TUTORIALINDEX_ACK | sub_55C790 | `s32` |
| 691 | GL_ITEM_MODIFY_NOTIFIER | sub_55C880 | `s32 f32/s32 s32 s32 s32 s32` |
| 692 | GG_CP_TERMINATE_APP | sub_55C960 | `s32` |
| 693 | GL_TCPCONNSUCC | sub_57CAE0 | `(無直接讀取/轉發)` |
| 696 | GS_BUY_ONCEITEM_ACK | sub_571D70 | `u8 rawResult s32 rawItemOrClass; rawResult==0 → raw s32; later fields are item-family conditional` |
| 705 | GL_LEVEL_KILL_LIMIT_ACK | sub_55C9B0 | `s32 f32 s32` |
| 709 | GL_CHECKCASHPG_ACK | sub_57C0B0 | `u8 s32 s32 s32` |
| 713 | GR_NOSKILL_ACK | sub_56FBC0 | `s8/bool` |
| 715 | GG_INVALIDWPDATA_ACK | sub_55D990 | `(無直接讀取/轉發)` |
| 725 | GL_COMBISKILLITEM_ACK | sub_573B70 | `u8 s32 s32 s32 s32 s32 s32 s32 s32 s32 s32 f32/s32 f32/s32 u8 s8/bool` |
| 727 | GG_OBSERVERCHAT_ACK | sub_58D840 | `(無直接讀取/轉發)` |
| 729 | GR_OBSERVERCHAT_ACK | sub_56E610 | `wstr wstr` |
| 731 | GG_GETPULP_ACK | sub_55E760 | `s8/bool u8 u8 u8 u8` |
| 732 | GG_DROPPULP_ACK | sub_55EB60 | `u8 u8 u8 u8 s16 s16 s16` |
| 734 | GG_SPAWNPULP_ACK | sub_55DAE0 | `s32 u8 u8 f32/s32 u8 u8 u8 u8 u8 u16 u16 u8 u8 u8 u8 f32/s32 u16 u16 u8 u8 u8 u8 f32 f32 ...` |
| 735 | GG_RESPAWNPULP_ACK | sub_55EE90 | `u8 u8 u8 u8 u8` |
| 738 | GG_DESTROY_START_ACK | sub_55F390 | `u8 u8 u8 u8` |
| 740 | GG_DESTROY_SUCC_ACK | sub_55F5C0 | `u8 u8 u8 s32 f32/s32 u8 u8 u8 u16 u16 s8/bool s8/bool s8/bool u16 u16 s8/bool u8 u8 u8 u8 u16 u16 u16 u16` |
| 742 | GG_DESTROY_FAIL_ACK | sub_55FF60 | `u8 u8 u8 u8` |
| 743 | GG_TIMEOVER_CHANGE | sub_5600E0 | `f32/s32` |
| 744 | GG_MAGIC_GAUGE_CHANGE | sub_5601A0 | `u16 u16` |
| 745 | GG_DEFENSE_REWARD_NOTICE | sub_560260 | `u8 u16 u16` |
| 747 | GG_PNR_RESPON_ACK | sub_5604E0 | `u8 u8 s16 s16 s16 s8/bool` |
| 748 | GR_SELECTRANDOMMAP_ACK | sub_564090 | `u8` |
| 750 | GG_GIMMICK_DAMAGE_ACK | sub_5640C0 | `s32 s32 u8` |
| 751 | GG_GIMMICK_RESPON_ACK | sub_5641D0 | `s32 s32` |
| 753 | GG_GIMMICKINFO | sub_564290 | `u8 s32 s32` |
| 754 | GG_PNR_OB_MODE_END | sub_564320 | `s32 u8` |
| 755 | GG_SYNC_TIME | sub_564260 | `f32/s32 f32/s32` |
| 757 | GL_CLAN_TNMT_RECEIPT_ACK | sub_57E030 | `u8` |
| 759 | GL_CLAN_TNMT_RECEIPT_CANCEL_ACK | sub_57E270 | `u8` |
| 760 | GC_CLAN_TNMT_RECEIPT_OK_NOTICE_ACK | sub_57E070 | `s32 f32/s32` |
| 761 | GC_CLAN_TNMT_RECEIPT_CANCEL_NOTICE_ACK | sub_57E2B0 | `s32` |
| 763 | GL_CLAN_TNMT_CURRENT_STATE_NOTICE_ACK | sub_57E5A0 | `f32/s32 u8 u8 u8 s32` |
| 765 | GL_CLAN_TNMT_ENTERROOM_ACK | sub_57E9A0 | `u8 s32 u8 str s32 s8/bool s32 s32 str u16 u16 u16 u16 f32/s32 u8 f32/s32 u8 s32 str s32 str f32/s32 u8 u8 ...` |
| 766 | GL_CLAN_TNMT_ROUND_END_NOTIFY | sub_581330 | `(無直接讀取/轉發)` |
| 768 | GL_CLAN_TNTM_AWARD_INFO_ACK | sub_581450 | `s32 u8 u8 s32 s32 u8 s32` |
| 769 | GR_CLAN_TNMT_ROUND_START_COUNTER | sub_581760 | `u8 u8` |
| 770 | GL_CLAN_TNMT_CHANGE_STATE_START_COUNTER | sub_5817F0 | `u8 u8` |
| 772 | GL_CLAN_TNMT_ALL_INFO_ACK | sub_57E550 | `(無直接讀取/轉發)` |
| 775 | GL_CLAN_TNMT_BROADCAST_TNMT_STATE_NOTIFY | sub_581980 | `u8 str s32 u8 str` |
| 777 | GL_CLAN_TNMT_CLANREC_ACK | sub_581D00 | `(無直接讀取/轉發)` |
| 778 | GR_CLAN_TNMT_PREVENT_ENTER_ROOM_NOTIFY | sub_581D20 | `(無直接讀取/轉發)` |
| 779 | GL_CLAN_TNMT_CHANGE_CLAN_INFO_NOTIFY | sub_581D60 | `u8 s32 s32 u8 str` |
| 781 | GS_GET_PRESENTPACKAGE_ACK | sub_57D6B0 | `u8 status; status==0 → s32 count, count×{s32 itemId,s32 rawValue}; nonzero → no tail` |
| 782 | GL_RECEIVE_NEW_MSG | sub_5643C0 | `(無直接讀取/轉發)` |
| 784 | GL_NEW_MSG_COUNT_ACK | sub_564480 | `s32` |
| 786 | GL_FRIEND_ADD_PROCESS_ACK | sub_5645B0 | `u8 str u8` |
| 792 | GL_VOICEITEMSLOT_ACK | sub_885D00 (vtbl+12 sub_876B00) | `u8 s16 s16 27×(s16 u8)` |
| 794 | GI_VOICEITEMSLOT_ALL_ACK | sub_885DA0 (vtbl+16 sub_876C90) | `u8 count×(u8 s16 s16 27×(s16 u8))` |
| 796 | GI_CHANGE_VOICEITEMSLOT_ACK | sub_885E40 | `u8 u8` |
| 803 | GS_DESTROYITEM_ACK | sub_895EE0 | *(dispatcher 呼叫 `sub_895EE0(v7, a4)`，成功時再 `sub_893DB0`；欄位分歧見 PACKETS.md §3.15 803 列)* |
| 810 | GS_HIDDENMAP_LIST_ACK | sub_582250 | `s32 s32` |
| 811 | GR_PROBABILITY_APPLY_NOTIFY | sub_58EE30 | `(無直接讀取/轉發)` |
| 813 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_ACK | sub_58EE60 | `s8/bool` |
| 815 | MASTER_CHECK_BOMB_CHEATER_APPLY_ACK | sub_58EDA0 | `s8/bool` |
| 816 | GL_ADDICTION_PREVENT_ALARM | sub_582500 | `u8 u8 f32/s32` |
| 821 | GG_CHATTING_PENALTY_REPORT_ACK | sub_582750 | `u8 f32/s32 u8` |
| 823 | MASTER_CHAT_BAN_ACK | sub_5827C0 | `u8` |
| 825 | MASTER_LOBBY_USERLIST_ACK | sub_582890 | `s32 s32 str str` |
| 826 | MASTER_ROOM_USERLIST_ACK | sub_582BE0 | `s32 s32 s32 str str` |
| 833 | GG_NETWORKERROR_NOTIFY | sub_583030 | `(無直接讀取/轉發)` |
| 835 | GL_DATA_RECV_COMPLETED_ACK | sub_5831D0 | `(無直接讀取/轉發)` |
| 837 | GL_SHOUTCHAT_ACK | sub_583C20 | `u8 s32 s32 str s32` |
| 840 | GR_CLAN_JOIN_RECOMMAND_REQUESTED_REQ | sub_5845F0 | `str s32 str` |
| 842 | MASTER_SETALL_EVENTEXP_ACK | sub_5842A0 | `f32 s32` |
| 844 | MASTER_SETALL_EVENTPAGE_ACK | sub_584350 | `f32 s32` |
| 846 | MASTER_VIEWALL_EVENTSTATE_ACK | sub_584400 | `str s32 f32 f32` |
| 847 | GR_NETCAFEWEAPONINFO_NOTIFY | sub_584770 | `s32` |
| 848 | GR_NETCAFEWEAPON_DISABLE_NOTIFY | sub_5847E0 | `u16` |
| 850 | MASTER_TNMT_VIEW_STATE_ACK | sub_57DDC0 | `s32 s32 f32/s32` |
| 852 | MASTER_RELOAD_GAMECENTER_RANKING_ACK〔推定〕 | sub_5854C0 | `s8/bool` |
| 856 | GL_MYWAREHOUSEINFO_ACK | sub_585920 | `(無直接讀取/轉發)` |
| 858 | GL_MYWAREHOUSEITEMLIST_ACK | sub_585950 | `(無直接讀取/轉發)` |
| 860 | GL_PUSH_TO_WAREHOUSE_ACK | sub_585980 | `(無直接讀取/轉發)` |
| 862 | GL_POP_TO_WAREHOSUE_ACK | sub_5859B0 | `(無直接讀取/轉發)` |
| 863 | GL_CHANGED_WAREHOUSEINFO_ACK | sub_5859E0 | `(無直接讀取/轉發)` |
| 865 | GL_SERVER_DATETIME_ACK | sub_585AB0 | `(無直接讀取/轉發)` |
| 866 | GQ_QUEST_LIST_ACK | sub_91C7B0 | `(無直接讀取/轉發)` |
| 868 | GQ_QUEST_ACCEPT_ACK | sub_91CC70 | `u8` |
| 870 | GQ_QUEST_CANCEL_ACK | sub_91D290 | `(無直接讀取/轉發)` |
| 872 | GQ_QUEST_SUCCESS_ACK | sub_91C6F0 | `(無直接讀取/轉發)` |
| 874 | GQ_QUEST_COMPLETE_ACK | sub_91C1E0 | `(無直接讀取/轉發)` |
| 875 | GQ_QUEST_CHANGEDSTATE_ACK | sub_91D690 | `(無直接讀取/轉發)` |
| 877 | GQ_QUEST_ACCEPT_DAILY_ACK | sub_91D7E0 | `raw1 err(0), raw4 count(0), count×13B rows` |
| 879 | GQ_QUEST_USER_COMPLETE_HONOR_ACK | sub_91CAA0 | `u8 str` |
| 880 | GQ_QUEST_ACCEPT_DAILY_NOTIFY〔推定〕 | sub_91DC50（case 先呼 getter sub_407E00；原表誤記） | `（無讀取；KR log 後立即送出 876）` |
| 881 | GQ_QUEST_CURRENTITEMQUEST_ACK | sub_407E00 | `(無直接讀取/轉發)` |
| 882 | GP_CHPLAYTIMEC_ACK | *(dispatcher inline)* | `s32` 總秒數 — 直接 `sub_592A40(a4,&v)`，以差分 `v - dword_EE8D7C` 推進 `sub_92EF00(20, 23, Δ, 0)` 後覆寫累計值 |
| 884 | MASTER_FIND_USER_ACK | sub_579BC0 | `s8/bool s32 str str s32 u8 u8 u8 u8` |
| 888 | GX_XIGNCODE_DATA_ACK | sub_88DB50 | `(無直接讀取/轉發)` |
| 889 | GX_XIGNCODE_DATA_BAN_NOTIFY | sub_88DB90 | `(無直接讀取/轉發)` |
| 891 | GC_QUERY_CLANRANKING_ACK | sub_424620 | `u8 s32` |
| 893 | MASTER_RELOAD_CLANRANKING_ACK | sub_585CB0 | `(無直接讀取/轉發)` |
| 895 | GR_TEAMSHUFFLE_ACK | sub_585E70 | `(無直接讀取/轉發)` |
| 903 | GG_OCC_START_ACK | sub_564E30 | `u8 u8 u8 u8 s32` |
| 905 | GG_OCC_SUCC_ACK | sub_565230 | `u8 u8 u8 u8` |
| 907 | GG_OCC_FAIL_ACK | sub_565560 | `u8 u8 u8 u8 s32` |
| 908 | GG_OCC_AB_SUCC_NOTIFY | sub_565850 | `(無直接讀取/轉發)` |
| 910 | GG_OCC_RESPON_ACK | sub_559150 | `u8 u8 s16 s16 s16` |
| 911 | GL_SCHEDULED_GM_NOTICE_NOTIFY | sub_578AC0 | `str` |
| 913 | GL_WEAPONPARTS_EQUIP_CHANGE_ACK | sub_95B180 | `u8 errorRaw`; only zero → `u8 op,s32 weapon,s32 part,[s32 oldPart for op2]` |
| 914 | GG_MISSILE_INFO_NOTIFY〔推定〕 | sub_565A00 | `repeat {u8 idx(1..0x4F), raw32}, idx==0 終止，≤80 筆` |
| 919 | GR_AI_GET_REWARD_ITEM_ACK | sub_761B20 | `u8 u8 s32 u8 s32 u8 u8 s32 u8 s32` |
| 921 | GR_AI_APPEARED_BOT_NOTIFY | sub_6061A0 | `u16 u16 u16 u16 u16` |
| 923 | GR_AI_DAMAGE_SHIELD_ACK | sub_761710 | `u16 u16 u16 f32` |
| 925 | GR_AI_RECHARGE_MAGAZINE_START_ACK | sub_558550 | `u8 u8 u8 u8 u16 u8 s32` |
| 927 | GR_AI_RECHARGE_MAGAZINE_END_ACK | sub_558880 | `u8 u8 s8/bool u8 u8 s32` |
| 929 | GR_AI_CONTINUE_START_ACK | sub_761E90 | `u8 u8 s32 str s32 s32` |
| 931 | GR_AI_CONTINUE_FAIL_ACK〔推定〕 | sub_762170 | `u8 result(0/1),[u8 slot if result==1]` |
| 933 | 〔未命名〕 | unknown_libname_105 | `(非 sub 直呼；無函式體，見審計節)` |
| 934 | GR_AI_TEAMSCORE_NOTIFY | sub_762630 | `s32 u8 s32 u8 s32 s32` |
| 936 | GR_AI_FEVER_START_ACK | sub_7623A0 | `u8 u8 s32 u8` |
| 937 | GR_AI_FEVER_END_NOTIFY | sub_762560 | `u8` |
| 940 | GR_AI_GO_NEXT_WAVE_ACK | sub_7613D0 | `u8 s32` |
| 942 | GR_AI_REWARDITEM_SELECT_START_NOTIFY | sub_761830 | `u8 s32 u8 u8 s32 s32` |
| 945 | GR_RESET_GAMEROOMSLOT_ACK | sub_585F30 | `(無直接讀取/轉發)` |
| 946 | GR_AI_UDPSENDER_CHANGE_START_NOTIFY〔推定〕 | sub_565AA0 | `u8 v4(診斷用), s32 playno` |
| 947 | GR_AI_UDPSENDER_CHANGE_END_NOTIFY〔推定〕 | sub_565BB0 | `u8 oldSlot, u8 newSlot, s32 v9(保留，讀後未用)` |
| 949 | GR_AI_MULTI_SHIELD_NOTIFY〔推定〕 | sub_58EF00 | `u8 n2`(bit0=On/Off;n2>=2→All_Channel 否則 This_Room) |
| 954 | MASTER_PVE_ACK〔推定〕 | sub_57DA20 | `s8 pveOn`(0/!=0→`byte_1D0D21B`;另寫點 CLobbyChannel::sub_4179D0 u32 bit0) |
| 958 | GR_TIMEOVER_ONGAME_ACK〔推定〕 | sub_565E00 | `（無讀取；收到即回送 957 RESPON_REQ）` |
| 959 | GG_DROPWEAPON_CREATE_NOTIFY | sub_5666D0 | `u16 u8 s32 u16 s16 s16 s16 u16 u16 f32 raw32` |
| 960 | GG_DROPWEAPON_DESTROY_NOTIFY | sub_566B30 | `u8 count, count×u16` (0 id stops early) |
| 961 | GG_DROPWEAPON_INFO_NOTIFY | sub_566BF0 | `u8 count, count×(u16 u8 s32 u16 s16 s16 s16 u16 u16 f32 raw32)` (0 id stops early) |
| 963 | GG_DROPWEAPON_GET_AND_DROP_ACK | sub_5672E0 | `u8 result, [result==0: u8 s32 u16 u8 u16 s16 s16 s16, [weapon!=0: u16 u16 u16 f32 raw32]]` |
| 965 | GG_GET_BALL_ACK | sub_566040 | `u8 u8` |
| 966 | GG_RESPAWN_BALL_ACK | sub_565EF0 | `(無直接讀取/轉發)` |
| 968 | GG_GET_GOAL_ACK | sub_566200 | `u8 u8` |
| 970 | GR_SOCCER_ACK | sub_586180 | `s8/bool` |
| 972 | GG_SOCCER_RESPON_ACK | sub_566400 | `u8 u8 s16 s16 s16` |
| 974 | GG_SOCCERBALL_HAVE_INCREASE_PG_ACK | sub_566650 | `u8` |
| 976 | MASTER_SETMULTIPLYDAMAGE_ACK〔推定〕 | sub_57D660 | `s8 result`(0=FAIL INVALID SERVER INDEX/≠0=SUCCESS;純 echo) |
| 984 | GL_MATCHINGROOM_MAKE_ACK | sub_5865A0 | `u8` |
| 985 | GL_ENTERMATCHINGROOM_ACK | sub_586610 | `u8 s32 u8 str s32 s8/bool s32 s32 str u16 u16 u16 u16 f32/s32 u8 f32/s32 u8 s32 str s32 str u8 u8 f32/s32 ...` |
| 986 | GR_MATCHINGROOM_START_ACK | sub_5880A0 | `u8 s8/bool f32/s32 u8 u8 u8 u16 u8 u8 u16 u8 s8/bool s8/bool s8/bool u8 s8/bool f32/s32` |
| 987 | GR_MATCHINGSUCCESS_ACK | unknown_libname_104 | `(非 sub 直呼)` |
| 989 | GL_MATCHINGROOM_CANCLE_ACK | sub_588020 | `s8/bool` |
| 991 | GR_DAMAGEROOM_ACK | sub_56FA00 | `s8/bool` |
| 994 | GG_ASSISTPOINT_NOTIFY | sub_5676D0 | `u8 u8 s32 u8 u8 s32 s32 s32` |
| 995 | (未註冊; 錢包/等級推播 → PACKETS.md §3.15r) | sub_567AE0 | `s32 pg, s32 cash, s32 level` |
| 997 | GL_BLOCK_ADD_ACK〔推定〕 | sub_567F20 | `u8` |
| 999 | GL_BLOCK_DEL_ACK〔推定〕 | sub_568170 | `u8 str` |
| 1001 | GL_BLOCK_LIST_ACK〔推定〕 | sub_567D50 | `u16 str s32 s32 str` |
| 1003 | GL_BLOCKME_LIST_ACK〔推定〕 | sub_567BD0 | `u16 str s32 str` |
| 1005 | GL_RANDOMMAP_LIST_ACK〔推定〕 | sub_5884C0 | `u8 u8 u8` |
| 1007 | 〔未命名〕 | unknown_libname_94 | `(非 sub 直呼；16-byte 微 thunk 無函式體，見審計節)` |
| 1009 | 〔未命名〕 | unknown_libname_95 | `(非 sub 直呼；16-byte 微 thunk 無函式體，見審計節)` |
| 1010 | 〔未命名〕 | sub_5680E0 | `u8 u8 s32`（count×{u8 playerKey, s32}→玩家 row+240776；無讀者，UNRESOLVED，見審計節） |

---

## Part II — C2S 送端：builder 寫入原語清單（261 unique op／321 direct sites）

---

> **範圍與計數口徑（Fact；2026-09-17 native scan）**：本表只收錄
> `PaperMan.exe.c` 中 `Packet::possible_ctor_or_dtor_0(buffer, N)` 的 TCP/C2S
> writer，按 **unique opcode row** 計數：261 rows、261 個不重複 opcode、無重複列。
> 這不是完整 native constructor inventory：同一 opcode 可能有互斥 branch 或多個
> caller。261 rows 合計 321 個 direct constructor call sites；全檔 native scan 則為
> 340 個 sites、276 個 unique opcode，另有 19 個 sites／15 個 low private-UDP
> opcode（`1,5,6,9,13,14,15,17,19,21,23,27,30,32,35`），它們刻意不在本
> TCP/C2S 表內。因此標題的 261 是 row/unique-TCP-opcode 口徑，不應解讀成
> 261 個 constructor variants，也不應解讀成 276 個完整 native forms。表後的
> **Appendix A** 另收錄這 15 個 private-UDP low-opcode builders；Appendix A 不加入
> 261/321 的 TCP row/site 計數。**Appendix B** 再以 `sub_595E80` 為中心收錄
> 所有 verified UDP receive cases、reader order、callee/state effect、方向配對與命名
> 邊界；UDP receive case 也不加入 TCP row/site 計數。
>
> **`direct ctor xref` 的意義（Fact）**：欄位列出每一個 direct constructor site
> 所在的 native function symbol；`×N` 表示同一 symbol 內有 N 個 direct sites。
> class-qualified symbol 保留 native 原名。這是可重現的 constructor-to-enclosing-
> function xref，不是把 opcode 名稱反推成 domain caller；同一 row 的每個 xref
> 都必須視為獨立 call site／可能的 branch variant，不能因寫入寬度相同而合併。
> `Packet::possible_ctor_or_dtor_2` 是 teardown，不另計 constructor form。
>
> **序列讀法與證據界線**：表中的 `u8/s8/u16/s16/s32/u32/u64/f32` 首先是
> wire width/type-shaped writer 的 native shorthand：分別為 1/1/2/2/4/4/8/4
> bytes；相同 width 不代表相同 domain semantics。`raw1/raw2/raw4/raw8` 是只宣稱
> exact byte width 的保守寫法，`rawN` 則是 runtime-sized raw/bulk write。這裡的
> primitive xref 先記錄 byte width；native helper identity audit 再細分 wire type：
> `592920/592960=u8`、`5928E0=s8`、`5929A0=u16`、`5929E0=s16`、
> `592A20=s32`、`592A60=u32`、`592B20=f32`、`592AE0/592B60=u64`，而
> `592AA0/592AC0` 明確保留為 caller-defined generic `raw4`。因此不能把所有
> 4-byte helper 都自動壓成同一 `raw4`，也不能把 `592AA0/AC0` 反向命名成
> numeric type。`str`、`wstr` 是 native string writer call，沒有在此表臆測
> encoding、terminator 或最大長度。`||` 代表同一 builder 的互斥或
> 條件變體，不是把它們串成一個可線性消費的 packet。count-prefixed record、
>
> **signedness audit（Fact / boundary）**：`rawN` 是 wire width label；但本輪
> 已恢復 native primitive alias mapping，不能把所有欄位退回 raw：
> `sub_592920/592960=u8`、`sub_5928E0=s8`、`sub_5929A0=u16`、
> `sub_5929E0=s16`、`sub_592A20=s32`、`sub_592A60=u32`、
> `sub_592B20=f32`、`sub_592AE0/592B60=u64`。`sub_592AA0/592AC0` 是
> caller-defined generic `raw4` alias。故 low UDP 的 `n0x10` 即使 source branch
> 寫入 `-2`，只要 call 是 `sub_592920`，wire type 仍是 `u8`、byte 為 `0xFE`；
> 同理 opcode 23 的 `sub_592960` movement tail 是 `u8`，不能只因 source delta
> clamp 到 `-127` 就標成 `s8`。Hex-Rays 的 helper parameter 普遍顯示 `char`，
> 只能作為 width/decompiler evidence，不能覆蓋已確認的 helper identity。
>
> optional tail、固定 buffer 與 opcode substitution 只在 native branch 已觀察到時
> 才寫出；`raw24`、`raw36` 等固定值是 source copy/array boundary 的 Fact；常見
> `_DWORD packet[4817]` 則是 native Packet 暫存容量，不是宣稱 wire 有 4817 個
> dword。這張表不是 server policy 或 server-ts handler specification。
>
> **Fact / Inference / UNRESOLVED／推定命名**：opcode literal、direct ctor
> xref、helper call order、分支條件、copy count 與固定陣列邊界是 native Fact。
> 具名 row 只沿用 native/catalog 的官方 symbol；`db/packets.tsv` 明確補出的
> 366=`GR_LOCALROOM_REQ`、969=`GR_SOCCER_REQ`、990=`GR_DAMAGEROOM_REQ` 亦屬
> catalog Fact。未註冊 opcode 不以 Wiki、舊文件、TS type 或鄰近 opcode 臆造
> 名稱；經 2026-09-17 多輪 native 證據鏈稽核，22 個未註冊 opcode 已依官方
> 命名風格給出**推定名**（名稱欄標 `〔推定〕`，證據見下方〈推定命名審計〉
> 節；無充分證據者不命名）。推定名不是 catalog Fact，也不寫入
> `db/packets.tsv`（676 個唯一 id＝670 筆 binary 直接註冊＋6 筆 UI 字串
> 補名，口徑詳 `PACKETS.md` §2）。字段的 domain 語意、server 接受規則、登入／權限／計價等若沒有
> native consumer 或 protocol handshake 證據，維持 `UNRESOLVED`；raw 欄位不得
> 因有熟悉的數值而升格。
>
> **native/catalog cross-check（2026-09-17 命名審計後）**：catalog 未註冊的
> table opcode 共 22 個：`206,295,487,828,851,853,896,898,930,932,953,957,973,975,992,996,998,1000,1002,1004,1006,1008`，
> 現已全部依 native 證據鏈推定命名（見下節；名稱欄標 `〔推定〕`）。推定名是
> 帶證據等級標記的稽核結論，不是 catalog Fact。`LAYOUTS.md` 的 S2C/dispatcher
> coverage 是獨立 inventory，不能拿來替換本表的 C2S writer order；`PACKETS.md`
> 的 named protocol facts 也不能把沒有 native writer 的 catalog entry 變成本表
> row。
>
> **111 例外（Fact/HIGH）**：本 inventory 將 `sub_56A5A0` 內互斥分支機械化
> 壓平；該 row **不是線性的 111 layout**。唯一可達 caller `sub_449320`
> 實際送出 `u8 0xFF, s8 has_password, str title, [str password], u8 max_players,
> u8 modeIndex, u8 requested_map, u8 no_skill_bg`。實作或修改 111 之前務必
> 先讀 `PACKETS.md` §3.15。
>
> **登入轉接欄位限制（2026-09 重新交叉確認）**：682 是嚴格的
> `str account, str password_or_token, u64 packed_data_revision, u8 fingerprint_source,
> raw24 fingerprint`，沒有 optional tail。143 的 `str` 源自 native
> `String[24]`（內容最多 23 ANSI bytes），後三欄依序為 681 回送的 `n100`
> （native 讀寫皆為 4B signed value）、常數 `s8 1`(sub_5928E0 accessor;2026-09-19 行級重讀 sub_555C60 訂正)、681 `ext_count`。
> 因此 143 是新 channel TCP connection 對最近 681 的 handoff claim，而不能
> 當作已驗證登入本身。141 是 private UDP op18 觸發的 empty endpoint-confirmation
> request（`sub_556530`）；195 三個 byte 依序為 681 的 group、group-local
> channel index、local-option-derived raw flag（domain UNRESOLVED；`sub_56FF40`）。


## C2S 推定命名審計（2026-09-17）

> 主表中 22 個 **catalog 未註冊**的 row（206 加原 unnamed 21 個）經多輪
> native 證據鏈稽核後，依官方命名風格推定命名。所有證據出自
> `PaperMan.exe.c`（ctor 函式體、caller、KR/JP log 字串、msgtableres.lang
> 訊息碼、UI 類名／視窗名、catalog 既有配對與命名詞彙、dispatcher case
> 存在性稽核）。每個 unnamed op 恰有 1 個 ctor site（全檔
> `possible_ctor_or_dtor_0(…, N)` 掃描：340 sites / 276 opcodes）。

### 命名方法與官方風格錨點

1. **`On<X>` 開發者標籤慣例（對照組驗證）**：已知配對反查 — 964
   `GG_GET_BALL_REQ`（catalog）的 sender `sub_565F60` 自記 log
   `GameNetwork::OnGLUserPickFootballREQ`，967 的 sender `sub_566120` 自記
   `GameNetwork::OnGLUserGoalFootballREQ`，證明 `On<X>` 是**訊息本身**的
   開發命名，RESPON/REQ/ACK 尾綴反映訊息的方向角色。同時注意：標籤只保證
   **語義域與角色**，不保證官方 token 相同（964 標籤
   `GLUserPickFootball` ↔ 官方 `GG_GET_BALL`，前綴與用詞皆異）。
2. **逆向配對與 `RESPON_REQ` 慣例**：官方已有 n → n+1 反向編號先例（681
   `GL_LOGIN_ACK`（S2C）／682 `GL_LOGIN_REQ`（C2S））。958（S2C unnamed）
   為 server push，957 為其 client 回應；C2S 單向回覆的官方慣例是
   `*_RESPON_REQ`（342、360、455、746、909、971），故 957 採
   `GR_TIMEOVER_ONGAME_RESPON_REQ`（開發標籤之 `…ACK` 尾綴於下表註記）。
3. **MASTER_* GM 指令字串 = 命名錨點**：擴寫先例（`/reloadclanrank` →
   892 `MASTER_RELOAD_CLANRANKING_REQ`（補 ING、對齊子系統詞）、
   `/reloadhiddenlist` → 804、`/resetudpdelaytime` → 831），保留命令 token
   先例（`/printuser` → 287 `MASTER_PRINTUSER_REQ`、`/resettcpgroupinfo` →
   418 `MASTER_RESETTCPGROUPINFO_REQ`）；子系統既有專詞優先（`TNMT`、
   `XTRAP`、`NPGAMEGUARD`、480 `GG_GAMECENTER_RANKING_REQ` 之
   `GAMECENTER_RANKING`、911 `GL_SCHEDULED_GM_NOTICE_NOTIFY` 之 `GM_NOTICE`）。
4. **S2C 對端存在性稽核（dispatcher `sub_58B010` 全 case 掃描）**：
   case 852 →`sub_5854C0`、488→`sub_5861C0`、914→`sub_565A00`（S2C 審計
   定為 `GG_MISSILE_INFO_NOTIFY`〔推定〕，MissileObjMgr RTTI，詳
   `LAYOUTS.md` 審計節）、931→`sub_762170`、954→`sub_57DA20`、958→`sub_565E00`、
   976→`sub_57D660`、997→`sub_567F20`、999→`sub_568170`、1001→`sub_567D50`、
   1003→`sub_567BD0`、1005→`sub_5884C0`；case 854、897、899 **不存在** →
   853／896／898 為 fire-and-forget，無專屬回應；case 974 屬既有
   `GG_SOCCERBALL_HAVE_INCREASE_PG_ACK`，與 973 語義無關。1007／1009 的
   handler 為 16-byte 微 thunk（`unknown_libname_94/95`），落於 decompile
   空白區，本 dump 不可回收；933（`unknown_libname_105`）同不可解析 —
   不得臆造其內容。
5. **UI／資源詞彙**：黑名單 UI 原生類名 `CUIBlockList`、`CUIBlockColumn`、
   `CBlockListPopup`、視窗 `BLOCK_NICK`、按鈕 `BLOCK_DELETE` → 官方詞彙為
   `BLOCK`（比照 friend 族 `GL_FRIEND_ADD/DEL/LIST_REQ` 切詞）。

### 推定命名對照表

| op | 推定名 | 寫入摘要 | 對向 S2C（不屬本表） | 等級 | 關鍵證據 |
|---:|---|---|---|---|---|
| 206 | `GS_BUY_WEAPONPARTS_REQ` | s32, s32, u8, s32 | 207 `GS_BUY_WEAPONPARTS_ACK`（catalog） | HIGH | 207 handler 將零件資料寫入武器零件容器並扣款；912/913 為購後之裝備變更鏈；ctor 前置驗證 item kind 與期間 |
| 295 | `MASTER_RELOAD_GM_NOTICE_REQ` | （空） | 無（無 case 296） | HIGH | GM `/reloadgmnotice` 觸發空包；子系統詞見 911 `GL_SCHEDULED_GM_NOTICE_NOTIFY` |
| 487 | `GL_MYROOMCHANGE_REQ` | u8（0..4） | 488 `GL_MYROOMCHANGE_ACK`（`LAYOUTS.md` 〔推定〕；u8 結果+u8 slot，成功寫入 `CLobbyChannel` 狀態位元組，失敗還原 UI） | HIGH | 大廳視窗 `MYROOM_%d`（slot+1）選擇器 `sub_401A60`；GM `/clby N`（1..5→0..4）；資源與訊息表無獨立 MYROOM 文字（共用通用回饋） |
| 828 | `MASTER_RESETCOINTIME_REQ` | s32 | 無 | HIGH | `/resetcointime <int>`；msg 0x4C0 顯示銀幣每小時充 1 枚、上限 5 枚之充值計時機制 |
| 851 | `MASTER_RELOAD_GAMECENTER_RANKING_REQ` | （空） | 852 `MASTER_RELOAD_GAMECENTER_RANKING_ACK`（`LAYOUTS.md` 〔推定〕；u8 成敗==1 印 `GAME CENTER RANK RELOAD SUCCESS`，否則 `GAME CENTER RANK RELOAD FAIL`） | HIGH | `/reloadgcrank`；892 `/reloadclanrank`→`CLANRANKING` 同型擴寫；480 `GG_GAMECENTER_RANKING_REQ` 為子系統詞 |
| 853 | `MASTER_PRINTGCRANK_REQ` | （空） | 無（無 case 854） | HIGH | `/printgcrank`；287 `MASTER_PRINTUSER_REQ` 同 `/print*` 保留 token 先例 |
| 896 | `MASTER_RSHUFFLEWT_REQ` | s32（`a1!=0` 才送出） | 無（無 case 897） | HIGH（`WT` 涵義 UNRESOLVED） | `/rshufflewt <int>`；隊伍洗牌族之時間參數（894 `GR_TEAMSHUFFLE_REQ`、368 `GR_TEAMSHUFFLECHANGE_REQ`） |
| 898 | `MASTER_RSHUFFLEVT_REQ` | s32（同護欄） | 無（無 case 899） | HIGH（`VT` 涵義 UNRESOLVED） | `/rshufflevt <int>` |
| 930 | `GR_AI_CONTINUE_FAIL_REQ` | （空；`sub_67EB70()!=0` 才送出） | 931 `GR_AI_CONTINUE_FAIL_ACK`（`LAYOUTS.md` 〔推定〕；u8 結果，==1 讀 u8 並切換 PvE manager 狀態，==0 播 `pve_01_sounds\AI3_continue_fail.wav`） | HIGH | sender 韓 log 原文（EUC-KR 解碼）「continue 실패 패킷을 서버로 보냄」（＝送出 continue 失敗封包）；931 handler log「continue 취소 패킷을 서버로부터 받음. result: %d」；與 928 `GR_AI_CONTINUE_START_REQ` 對仗 |
| 932 | `MASTER_RELOAD_AIXML_REQ` | （空） | 無 | HIGH | `/reloadaixml`；773 `MASTER_RELOAD_TNMT_REQ` 切詞先例 |
| 953 | `MASTER_PVE_REQ` | s8（1=on, 0=off） | 954 `MASTER_PVE_ACK`（`LAYOUTS.md` 〔推定〕；印 `PVE On Succ!!`／`PVE Off Succ!!`） | HIGH | `/pveon` → `sub_57D970(1)`、`/pveoff` → `sub_57D970(0)` |
| 957 | `GR_TIMEOVER_ONGAME_RESPON_REQ` | s8 | ←958 `GR_TIMEOVER_ONGAME_ACK`（`LAYOUTS.md` 〔推定〕；server push；handler＝`sub_565E00`＝957 的 sender） | HIGH（前綴／用詞依標籤；官方 token 可能略異） | 依規則 1–2 命名；958 handler 內組包送 957：s8 = (`sub_67E390`()==0)，該函式為剩餘遊戲秒數（`CyAIMULTIMode::sub_75D660` 以 `/60`、`%60` 組 mm:ss）；log 標籤 `GameNetwork::OnGRTimeOverOnGameACK`；dispatcher case 958 → `sub_565E00` 已驗證 |
| 973 | `MASTER_RESETSOCCERBALLRESPAWNTIME_REQ` | s32 | 無（974 屬既有他訊） | HIGH | `/resetsoccerballrespawntime <int>`；足球模式（964/967/969/971 族）之球重生計時 |
| 975 | `MASTER_SETMULTIPLYDAMAGE_REQ` | u8, f32 | 976 `MASTER_SETMULTIPLYDAMAGE_ACK`（`LAYOUTS.md` 〔推定〕；印 `SET DAMAGE SUCCESS!!`／`SET DAMAGE FAILED!! INVALID SERVER INDEX!!`） | HIGH | `/setmultiplydamage <int> <float>`（atol→u8、atof→f32）；976 失敗訊息指出首參為 server index |
| 992 | `GQ_QUEST_LIST_REQ` | （空） | 866 `GQ_QUEST_LIST_ACK`（catalog） | HIGH | `CLobbyQuest::sub_9C7F30`（任務視窗開啟）無條件送出（與 864 `GL_SERVER_DATETIME_REQ` 同批）；866 handler 消費任務清單 |
| 996 | `GL_BLOCK_ADD_REQ` | str nick | 997 `GL_BLOCK_ADD_ACK`（`LAYOUTS.md` 〔推定〕；u8 結果：0=成功自動重送 1000+msg 0x528，1=0x52B，2=0x11A，3=0x52A，4=0x532） | HIGH | 見〈黑名單家族〉 |
| 998 | `GL_BLOCK_DEL_REQ` | str nick | 999 `GL_BLOCK_DEL_ACK`（`LAYOUTS.md` 〔推定〕；u8 結果+str nick：0=本地移除+0x52D，1=0x52E，3=0xB6） | HIGH | 見〈黑名單家族〉 |
| 1000 | `GL_BLOCK_LIST_REQ` | （空） | 1001 `GL_BLOCK_LIST_ACK`（`LAYOUTS.md` 〔推定〕；u16 + str + s32 count ×{s32, str nick}） | HIGH | 見〈黑名單家族〉 |
| 1002 | `GL_BLOCKME_LIST_REQ` | （空） | 1003 `GL_BLOCKME_LIST_ACK`（`LAYOUTS.md` 〔推定〕；u16 + str + s32 count ×{str nick}） | MEDIUM-HIGH | 見〈黑名單家族〉（方向語義為 Inference/HIGH，功能面確定） |
| 1004 | `GL_RANDOMMAP_LIST_REQ` | （空） | 1005 `GL_RANDOMMAP_LIST_ACK`（`LAYOUTS.md` 〔推定〕；u8 count ×{u8 mode, u8 mapId}） | HIGH | 見〈隨機地圖〉 |
| 1006 | `GG_OCC_ZONE_ENTER_REQ` | u8（zone/flag index+1） | 1007（handler 本 dump 不可回收） | HIGH | sender 韓 log 原文「내가 깃발(인덱스=%d)에 점령가능지역으로 들어왔습니다.」（我進入旗子 index 之可佔領區域），位置檢查 `sub_76AF20` 狀態 0→1 觸發；occupy 族 902/904/906/908/909 |
| 1008 | `GG_OCC_ZONE_LEAVE_REQ` | u8（同） | 1009（handler 本 dump 不可回收） | HIGH | 韓 log「내가 깃발(인덱스=%d)에 점령가능지역에서 빠져나왔습니다.」（我離開旗子 index 之可佔領區域），狀態 1→0 觸發 |

### 黑名單家族（996–1003）

> - **功能語意（Fact：msgtableres.lang + UI）**：0x52C 確認對話框
>   「フレンド解除をしてブラックリストに追加しますか？」（解除好友並加入
>   黑名單？）；黑名單對象的私訊（0x535）、送禮（0x533）、對話（0x534）、
>   公會邀請（0x537）、好友加入（0x535 路由）被各自阻擋訊息覆蓋。
> - **UI**：`CUIBlockList`（列表）、`CUIBlockColumn`、`CBlockListPopup`、
>   視窗 `BLOCK_NICK`、按鈕 `BLOCK_DELETE`；RTTI 無其他 Block 相關 server
>   類名可回收。
> - **觸發**：996 於「加入黑名單」確認後送出（對象是好友時依 0x52C 先送
>   431 `GL_FRIEND_DEL_REQ` 再送 996）；998 由列表 `BLOCK_DELETE` 觸發；
>   1000 於 `CUIBlockList` 開啟、登入批次（[1000, 1002, 433]）與 997 成功
>   回應後自動刷新；1002 於登入批次與入房／狀態刷新（`sub_44A6A0` 之
>   state==1 分支，與 107 `GL_GAMEROOMINFO_REQ` 同批）。
> - **本地狀態**：黑名單本體快取於 `+202` 集合（1001 下載覆寫）；`+205`
>   集合僅 1003 下載寫入（cover 後逐筆 push_back），998 解除**不清**它
>   → server-authoritative、本地不可解除的第二名單。
> - **+205（1002/1003）消費者（Inference/HIGH）**：①私語送出靜默阻擋
>   ②好友加入失敗走 0x15B 泛用訊息（黑名單本體則走專用 0x535）
>   ③大廳 USERSTATE 顯示強制=4 ④遊戲內／大廳聊天對 +205∪+202 之來源
>   發言壓制 ⑤私語選單顯示 0x220「情報を取得できませんでした」。
> - **命名方向**：+205 各消費語義（對方發言被隱、我無法對其私語／加
>   好友）與「對方將我加入黑名單」模型一致 → 推定 `GL_BLOCKME_LIST_REQ`；
>   方向解讀為 Inference/HIGH，「server 管理的 nick 黑單、client 同步
>   快取」之功能面為 Fact。

### 隨機地圖（1004–1005）

> - 1004 於大廳主 UI 初始化末段**無條件**送出一次（`sub_588420`，行
>   33357；先前可疑的 `+4264` gate 經原始碼複讀確認為 xref 工具偽影）。
> - 1005 handler `sub_5884C0`：u8 count ×{u8 mode, u8 mapId}，每筆
>   push_back 至 `dword_EA134C` 管理者內的 mode→vector 桶。
> - 消費：`sub_9B73E0` 回傳 `vector[rand()%size]`（無可用時 -1）；caller
>   為 GAMEROOM_BACK 點擊（無隨機圖→-1）與 FASTROOM／mode-17 路徑
>   （`+384`＝該管理者）。
> - 佐證：msg 0x539「READY中にはランダムマップ設定ができません。」
>   （READY 中無法設定隨機地圖）— 遊戲房設定含隨機地圖；748
>   `GR_SELECTRANDOMMAP_ACK`（catalog）之 `RANDOMMAP` 詞彙直接沿用。

### 殘留 UNRESOLVED（命名後仍不升格者）

- 896/898 之 `WT`／`VT` 縮寫涵義（wait-time? vote/victory? 無 native 字串
  可鎖定）— 保留命令 token。**2026-09-19 補錨**:896(=`sub_57DBD0`)/898(=`sub_57DC80`)
  皆 `/r` 前綴 GM 命令族——命令實錘為 `/rshufflewt`→896、
  `/rshufflevt`→898，皆 `j__atol` 參數→非零才送的 raw4;`/r` = reshuffle
  命令首碼（同群 `/reloadtnmt`→**773 MASTER_RELOAD_TNMT_REQ(sub_57D8D0)**、
  `/reloadgcrank`→**851(sub_57DA90，裸 opcode 請求）**、`/printgcrank`→
  **853(sub_57DB30)**、`/print_tnmt_state`=無 wire(local only));852=**純名錄
  空隙**(全檔 `ctor(852)` 零匹配)——**但 852 ≠ 死 op**:它是
  851 的 S2C ACK(sub_5854C0,'GAME CENTER RANK RELOAD SUCCESS/FAIL');方向別遮描教訓紀錄於此。**852/853 之 `GCRANK` 推定
  = GAMECENTER_RANKING(與 892/480 平行案一致)。WT/VT 之字母仍屬
  server-policy(reshuffle wire 語義已定,字母擴展 server 保留)。
- 1002 方向語義（block-ME 模型）Inference/HIGH，見上。
- 851/853 之 GCRANK 縮寫：依 892／480 平行案推定為 `GAMECENTER_RANKING`；
  命令 token 原型為 `/reloadgcrank`、`/printgcrank`。
- 957 前綴／用詞依開發標籤語義推定；official token 可能略異（964 先例）。
  尾綴採 catalog C2S 慣例 `RESPON_REQ`（開發標籤記為 `…ACK`，兩者並存）。
- S2C 對端 pairs 之命名屬 `LAYOUTS.md` 範圍，本表不做該側命名宣告；
  2026-09-17 S2C 審計（`LAYOUTS.md`〈S2C 推定命名審計（2026-09-17）〉）
  已完成：488、852、931、954、958、976、997、999、1001、1003、1005
  推定命名（203、880、914、946、947、949 同輪補齊），933、1007／1009
  （微 thunk `unknown_libname_105/94/95`）與 489、1010 維持 unnamed ——
  無函式體／無讀取者，明確標記不回收。

### 本輪驗證 invariant（2026-09-17）

- 主表仍為 261 rows／261 unique TCP opcode；321 direct ctor sites 計數不變
  （本次未增刪 row/site，僅更名 22 欄）。
- 22 個推定名與 catalog 676 名零衝突（`db/packets.tsv` 全表機檢）。
- `python3 tools/verify_dispatcher_coverage.py` 維持通過（opcode 集合未變）。
- 828/896/898/973 之 ctor 用 `sub_592AA0`（4-byte 寫入；文件慣例上寬度
  保守標 raw4、caller-defined）；此四列值源皆為 `/cmd` 之 `j__atol()`，故
  序列欄維持 `s32` 作為 domain 註記，wire 寬度 4B 不因命名改變。419
  （named）首欄同此 helper，其 `s32` 亦為 domain 註記。
- 953/957 之 1-byte 寫入 helper 同為 `sub_5928E0`（文件慣例 `s8`）；其值
  域 {0,1}，signedness 不影響 wire。
- 1006/1008 寫入 helper 為 `sub_592920`（`u8`）。


## 主表：C2S builder 寫入原語（261 列）

| op | 名稱 | direct ctor xref | 寫入序列 (變體) |
|---|---|---|---|
| 101 | GT_PING_REQ | sub_58D6F0 | `(空)` |
| 103 | GE_LOGOUT_REQ | sub_58D660 | `(空)` |
| 105 | GL_USERLIST_REQ | sub_56A0F0 | `u8` |
| 107 | GL_GAMEROOMINFO_REQ | sub_568C50 | `(空)` |
| 111 | GL_MAKEROOM_REQ | sub_56A5A0 | `u8 s8 u8 s8 str str u8 u8 u8 u8 u8 s8 u8 s8 str u8 u8 u8 u8` |
| 113 | GL_ENTERROOM_REQ | sub_56B0D0 | `u8` |
| 119 | GL_CHATTING_REQ | sub_56DAC0; sub_56DB90 | `s32 str wstr \|\| str` |
| 121 | GR_MAPCHANGE_REQ | sub_56E480 | `u8` |
| 123 | GR_LEAVE_REQ | sub_562D70 | `(空)` |
| 125 | GR_CHATTING_REQ | sub_56E6C0; sub_56E860 | `s32 u8 wstr \|\| s32 u8 str` |
| 127 | GR_READY_REQ | sub_562640 | `(空)` |
| 129 | GR_START_REQ | sub_5627C0 | `u8` |
| 131 | GR_FORCEOUT_REQ | sub_56EC10 | `u8` |
| 133 | GR_END_REQ | sub_562E00 | `(空)` |
| 135 | GR_CHANGESLOT_REQ | sub_56EE90 | `u8 u8` |
| 139 | GG_EXITGAME_REQ | VoterMgr::sub_A19A70; sub_560720 | `(空)` |
| 141 | PM_CONNECT_REQ | sub_556530 | `(空)` |
| 143 | PM_UDPSTART_REQ | sub_555C60 | `str s32 u8(固定 1) s32` |
| 165 | Y_TCP_INF_REQ | sub_55C9F0; sub_55CAB0; sub_55D090; sub_55D440; sub_55D530; sub_5658B0; sub_5AF880; sub_5DF7F0; sub_5E5ED0; sub_5E6040; sub_5E6170; sub_5E6DF0; sub_744D80; sub_7452D0; sub_745D60; sub_745E80; sub_98E920 | 17 個 site-specific native form（依 xref 順序；`rawK` 表寬度）：`sub_55C9F0 raw1×2 raw4`；`sub_55CAB0 raw1×2`；`sub_55D090 raw1×4 raw2 raw4×2 raw1×3 raw4×2 raw1×4 raw4`；`sub_55D440 raw1×3 raw2 raw1×2`；`sub_55D530 raw1×5 raw2 raw4×6 raw1×4 raw4 raw1`；`sub_5658B0 raw1×2 raw2×3`；`sub_5AF880 raw1×2 raw4 raw2 raw4×8 raw1`；`sub_5DF7F0 raw1×3`；`sub_5E5ED0 raw1×5 raw4`；`sub_5E6040 raw1×5 raw4`；`sub_5E6170 raw1×3`；`sub_5E6DF0 raw1×5 raw4×5`；`sub_744D80 raw1×2 raw4 raw2 raw4×7 raw1`；`sub_7452D0 raw1×2 raw4 raw2×7 raw1×3 raw2 raw1 raw2×2 raw4 raw2`；`sub_745D60 raw1×3`；`sub_745E80 raw1×2`；`sub_98E920 raw1×2 raw4 raw2 raw4×5 raw1`。不得壓平為單一線性 layout；native 分支／狀態條件仍僅見於 source。 |
| 167 | GR_CHANGEUSER_REQ | sub_56F360 | `s16` |
| 169 | GR_RULECHANGE_REQ | sub_56F440 | `u8` |
| 171 | GR_WINCHANGE_REQ | sub_56F520 | `s16` |
| 173 | GR_TIMECHANGE_REQ | sub_56F600 | `u8` |
| 175 | GR_ITEMCHANGE_REQ | sub_56F6E0 | `u8` |
| 177 | GR_AUTOCHANGE_REQ | sub_56F8A0 | `s8` |
| 183 | GR_ENDLOADING_REQ | sub_563A60 | `(空)` |
| 187 | GG_STARTGAME_REQ | sub_563CC0 | `(空)` |
| 191 | GR_CALLUSER_REQ | sub_56FD60 | `str` |
| 195 | GC_ENTERCHANNEL_REQ | sub_56FF40 | `u8 u8 u8` |
| 197 | GL_MYINFO_REQ | sub_5704B0 | `(空)` |
| 199 | GL_MYITEM_REQ | sub_570A00 | `(空)` |
| 204 | GS_BUYITEM_REQ | sub_571100 | `u8 count, count×{s32 raw0,u8 raw1,raw2,[raw2 當 raw1 為 12/13/17]}`；native range check 可能在記錄之前將 opcode 置換為 468；欄位／domain 涵義維持 UNRESOLVED |
| 206 | GS_BUY_WEAPONPARTS_REQ〔推定〕 | sub_571620 | `s32 raw0,s32 raw1,u8 raw2,s32 raw3` |
| 208 | GS_SELLITEM_REQ | sub_572AD0 | `s32` |
| 210 | GM_CHECKNICK_REQ | sub_572CD0 | `str` |
| 212 | GM_CREATENICK_REQ | sub_572DC0 | `str` |
| 214 | GM_CREATECHAR_REQ | sub_572EB0 | `u8 s16 s16 s16` |
| 216 | GL_ENTERROOMPASS_REQ | sub_56B180 | `u8 str` |
| 218 | GI_CHANGEDATA_REQ | sub_572FC0 | `u8` |
| 220 | GI_CHANGEWP_REQ | CLobbyTournamentGameRoom::sub_47AA40; sub_573340; sub_57C270 | `u8 count, count×record`；record = `u8 raw0,raw2 raw1,[3×raw2 當 raw0!=3],[8×raw4 當 raw1!=0]`；分支條件為 native raw 值，欄位／domain 涵義維持 UNRESOLVED |
| 230 | GP_CHLOSSC_REQ | sub_5567F0 | `s32` |
| 232 | GP_CHKILLC_REQ | sub_5568E0 | `s32` |
| 244 | GP_CHTKILLC_REQ | sub_556B90 | `s32` |
| 246 | GL_CLIENTINFO_REQ | sub_573DE0 | `str` |
| 250 | GL_LOBBYIN_REQ | sub_574080; sub_584FE0 | `(空)` |
| 252 | GL_SHOPIN_REQ | sub_574120 | `(空)` |
| 254 | GL_INVENIN_REQ | sub_5741C0 | `u8` |
| 260 | GL_JOIN_REQ | sub_56D9C0 | `u8` |
| 262 | GL_JOINPASS_REQ | sub_56B230 | `u8 str` |
| 264 | GL_JOININFO_REQ | sub_5744A0 | `u8` |
| 266 | GL_JOINGAME_REQ | sub_574910 | `u8 u8` |
| 268 | GL_JOINPLAY_REQ | sub_574A60 | `u8 u8` |
| 270 | GL_MYINFO_OPEN | sub_556680 | `s8` |
| 271 | PM_TSPOSUPDATE_REQ | sub_5785A0 | `u8 str u8 u16 u16 u16 s32 u8` |
| 275 | MASTER_MEMO_REQ | sub_578830 | `wstr` |
| 277 | MASTER_MEMOALL_REQ | sub_5789D0 | `wstr` |
| 279 | MASTER_USERCUT_REQ | sub_578E50 | `u8 str` |
| 281 | MASTER_USERCUT2_REQ | sub_578F00 | `s32` |
| 283 | MASTER_ROOMCUT_REQ | sub_578FF0 | `u8` |
| 285 | MASTER_MSET_REQ | sub_578C70 | `u8` |
| 287 | MASTER_PRINTUSER_REQ | sub_5796D0 | `(空)` |
| 289 | MASTER_USERINFO_REQ | sub_579780 | `str` |
| 291 | MASTER_LISTCUT_REQ | sub_579960 | `str` |
| 293 | MASTER_USERINFODB_REQ | sub_579A20 | `str` |
| 295 | MASTER_RELOAD_GM_NOTICE_REQ〔推定〕 | sub_57D830 | `(空)` |
| 296 | GS_GIVEGIFT_REQ | sub_57A690; sub_57A770 | `str u8 str u8 s32 u8 u8 u16 \|\| s32 u8 u8` |
| 298 | GS_TAKEGIFT_REQ | sub_57AE50 | `(空)` |
| 300 | GS_MOVEGIFT_REQ | sub_57AF40 | `(空)` |
| 306 | GG_JJGET_REQ | sub_5619F0 | `u8` |
| 310 | GS_BUYCHAR_REQ | sub_572790 | `s32 s32 s32 s32 s32 s32` |
| 312 | GI_CHANGESLOT_REQ | sub_573270 | `u8` |
| 316 | GG_HACKSTART_REQ | sub_556E90 | `u8` |
| 318 | GG_HACKSUCC_REQ | sub_5571E0 | `u8 f32 f32 f32 f32 f32 f32` |
| 320 | GG_HACKFAIL_REQ | sub_557580 | `u8` |
| 322 | GG_BOMBSUCC_REQ | sub_557830 | `(空)` |
| 324 | GG_BOMBEND_REQ | sub_557A40 | `u8` |
| 326 | GG_UNHACKSTART_REQ | sub_557B80 | `u8` |
| 328 | GG_UNHACKSUCC_REQ | sub_557DE0 | `u8` |
| 330 | GG_UNHACKFAIL_REQ | sub_5580A0 | `u8` |
| 340 | GR_KILLCHANGE_REQ | sub_56F7C0 | `s16` |
| 342 | GG_SOLORESPON_REQ | sub_558A00 | `s32` |
| 344 | GG_LIVECHAT_REQ | sub_61D750; sub_798F90 | `s32 u8 str` |
| 346 | GG_TEAMCHAT_REQ | sub_61D750; sub_798F90 | `s32 u8 str` |
| 348 | GG_DEADCHAT_REQ | sub_61D750; sub_798F90 | `s32 u8 str` |
| 350 | GG_TEAMDEADCHAT_REQ | sub_61D750; sub_798F90 | `s32 u8 str` |
| 356 | GS_CASH_REQ | sub_572380 | `(空)` |
| 358 | GS_BUYCASHITEM_REQ | sub_572450 | `u8 count, count×{s32 raw0,s32 raw1}` |
| 360 | GG_TSURRESPON_REQ | sub_558D20 | `s32` |
| 364 | GR_BALANCECHANGE_REQ | sub_56FA30 | `s8` |
| 366 | GR_LOCALROOM_REQ | sub_585FD0 | `s8` |
| 368 | GR_TEAMSHUFFLECHANGE_REQ | sub_585CE0 | `s8` |
| 370 | GL_CHANGECHANNEL_REQ | sub_570030 | `u8` |
| 374 | GR_GETCRYSTAL_REQ | sub_562410 | `u8` |
| 378 | GR_RADIOMSG_REQ | sub_5593A0 | `u8 u8 u8 u8 rawN` |
| 394 | MASTER_ROOMINFO_REQ | sub_5790B0 | `u8` |
| 398 | MASTER_SVRCLASS_REQ | sub_579270 | `u8` |
| 400 | MASTER_CONNTYPE_REQ | sub_579350 | `u8` |
| 402 | MASTER_EVENTPAGE_REQ | sub_579430 | `f32` |
| 404 | MASTER_EVENTEXP_REQ | sub_579580 | `f32` |
| 406 | MASTER_ENABLE_LOGIN | sub_57C640 | `(空)` |
| 407 | MASTER_DISABLE_LOGIN | sub_57C6E0 | `(空)` |
| 410 | MASTER_DISLOGIN_REQ | sub_57C5A0 | `(空)` |
| 412 | MASTER_DISGMS_REQ | sub_57C780 | `str s32` |
| 414 | MASTER_DISLOG_REQ | sub_57C8A0 | `str s32` |
| 416 | MASTER_KILLALL_REQ | sub_57C500 | `(空)` |
| 418 | MASTER_RESETTCPGROUPINFO_REQ | sub_57C9C0 | `str s32` |
| 419 | GL_MSG_ADD_REQ | sub_559550 | `s32 str str str str u16 u8` |
| 421 | GL_MSG_DEL_REQ | sub_55A1E0 | `str raw0`（native caller 以其為本地訊息 key；service 涵義 UNRESOLVED） |
| 423 | GL_MSG_READ_REQ | sub_55A3C0 | `str raw0`（native caller 以其為本地訊息 key；service 涵義 UNRESOLVED） |
| 425 | GL_MSG_RECVLIST_REQ | sub_55A580 | `s32` |
| 429 | GL_FRIEND_ADD_REQ | sub_55A860 | `str` |
| 431 | GL_FRIEND_DEL_REQ | sub_55AD00 | `str` |
| 433 | GL_FRIEND_LIST_REQ | sub_55AF20 | `(空)` |
| 435 | GL_FRIEND_INFO_REQ | sub_55B0A0 | `str` |
| 437 | GG_ROOMBROADCAST_REQ | sub_55B430 | `u8 s32 rawN` |
| 439 | GL_FRIEND_CHAT_REQ | sub_55B510 | `s32 str str str` |
| 441 | GL_FRIEND_WHERE_REQ | sub_55B940 | `str` |
| 443 | GG_STEALSUCK_REQ | sub_55BDC0 | `u8 s16` |
| 445 | GG_STEALPUSH_REQ | sub_55C060 | `u8 s16` |
| 453 | GS_DELETEGIFT_REQ | sub_57BC40 | `s32 s32` |
| 455 | GG_EXERCISERESPON_REQ | sub_55C620 | `s32 s8` |
| 457 | GI_CHANGEITEMSLOT_REQ | sub_573770 | `9×raw4`（經 `sub_5275A0` 之精確 36B bulk payload；欄位／domain 涵義維持 UNRESOLVED） |
| 461 | GS_USE_PAPERCODEGIFT_REQ | sub_57CBC0 | `str` |
| 463 | GS_ENTERPAPERCODEGIFT_NOTIFY | sub_57CB20 | `(空)` |
| 464 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ | sub_57CCE0（direct；caller sub_4C4000） | `u8 raw0; raw0==1 → str`；觀察到之 UI callback 字面量為 1 與 0，故 0 分支恰為一個 byte；domain 涵義維持 UNRESOLVED |
| 466 | GI_CHANGE_SKILLITEMSLOT_REQ | sub_5738A0 | `u8 raw0,u8 raw1,[u8 raw2,7×raw4]`（raw1==0 → 2B；raw1!=0 → 31B；附加 bulk 為 `sub_527BA0`；domain 涵義維持 UNRESOLVED） |
| 472 | GL_GAMECENTER_REC_REQ | sub_584850 | `s16` |
| 474 | GG_GAMECENTER_GAME_START_REQ | sub_584DB0 | `s16 u8` |
| 476 | GG_GAMECENTER_GAME_END_REQ | sub_564930 | `s16 raw24 raw44` |
| 478 | GG_GAMECENTER_GAME_PLAY_CHECK_REQ | sub_564A40 | `raw36` |
| 480 | GG_GAMECENTER_RANKING_REQ | sub_585320 | `s16 u8` |
| 483 | GG_GAMECENTER_GAME_START_OK_REQ | sub_584EC0 | `s16` |
| 485 | GL_GET_GAMEROOM_PROGRESSTIME_REQ | sub_56AD60 | `u8` |
| 487 | GL_MYROOMCHANGE_REQ〔推定〕 | sub_57C450 | `u8` |
| 571 | GV_TEST_REQ | sub_58E4D0 | `str str` |
| 581 | GC_CLAN_START_REQ | sub_550560 | `(空)` |
| 583 | GC_CLAN_PROTOCOL_REQ | sub_54BA90; sub_54BC30; sub_5506C0; sub_550790; sub_550840; sub_550960; sub_550A10; sub_550B10; sub_550C00; sub_550CF0; sub_550DD0; sub_550E80; sub_550F80; sub_551210; sub_5512C0; sub_551390; sub_551460; sub_551500; sub_5515A0; sub_551650; sub_5517E0; sub_551970; sub_551B00; sub_551C90 | 多個 native 控制形式，非單一線性 layout：`s32`；`s32 s32`；`s32 s32 rawN`；`s32 s32 str`；`s32 str`；`s32 str str str`；`2×s32`；`3×s32`；`5×s32` |
| 585 | GC_CLAN_CREATE_REQ | sub_5505F0 | `str str str s32` |
| 682 | GL_LOGIN_REQ | sub_43DF00 | `str str u64 u8 raw24` |
| 685 | GL_TUTORIALINDEX_REQ | sub_55C6F0 | `(空)` |
| 687 | GL_TUTORIAL_START | sub_5624D0 | `(空)` |
| 688 | GL_TUTORIAL_END | sub_562570 | `(空)` |
| 689 | GL_TUTORIAL_INDEX_SET_REQ | sub_55C7D0 | `s32` |
| 695 | GS_BUY_ONCEITEM_REQ | CPopupGunShootingStart::sub_5115E0; sub_570B00; sub_8DE9C0 | 多個 native form；`sub_5115E0`／`sub_8DE9C0`：`raw4 raw1 raw1 raw2`；`sub_570B00`：條件式 `raw4 str raw1 raw1` 或 `raw4 raw1 raw1` 或 `raw4 raw4` 或 `raw4 raw4 raw1 raw1`；所有 item-family 條件與欄位涵義維持 UNRESOLVED |
| 697 | GG_CHEATER_REPORT_REQ | sub_593510 | `s16` |
| 698 | GP_ENTER_PEPACHI_REQ | sub_46E080 | `(空)` |
| 700 | GP_START_GAME_REQ | sub_8458D0（direct；caller sub_8459C0） | `u8 raw0,s32 raw1`（精確 5B；觀察到 raw0 選擇子 1/2/4/5；native 由所選角色狀態計算 raw1；domain 涵義維持 UNRESOLVED） |
| 702 | GP_PEPACHI_LIST_REQ | sub_45C9B0 | `(空)` |
| 704 | GL_LEVEL_KILL_LIMIT_REQ | sub_582570 | `(空)` |
| 706 | GL_BILLTOKEN_REQ | sub_460480 | `(空)` |
| 708 | GL_CHECKCASHPG_REQ | sub_460480×2; sub_57C000; sub_57C0B0 | `s32` |
| 712 | GR_NOSKILL_REQ | sub_56FB10 | `s8` |
| 714 | GG_INVALIDWPDATA_REQ | sub_548E80 | `u8 u8 u8 str s32` |
| 716 | GG_CHANGEWPQUICKSLOT_REQ | sub_563EE0 | `s16 s16 s16 s16` |
| 718 | GR_START_VOTING_REQ | IVotingNetwork::sub_A191D0 | `s32 s32 s32` |
| 721 | GR_DO_VOTING | IVotingNetwork::sub_A192B0 | `s8` |
| 724 | GL_COMBISKILLITEM_REQ | sub_573980 | `s32 s32 s32 s32` |
| 726 | GG_OBSERVERCHAT_REQ | sub_798F90 | `str str` |
| 728 | GR_OBSERVERCHAT_REQ | sub_56E560 | `wstr wstr` |
| 730 | GG_GETPULP_REQ | sub_55E630 | `u8` |
| 733 | GG_SPAWNPULP_REQ | sub_55D9D0 | `(空)` |
| 736 | GG_PULPSTEAL_REQ | sub_55E530 | `u8` |
| 737 | GG_DESTROY_START_REQ | sub_55F240 | `u8` |
| 739 | GG_DESTROY_SUCC_REQ | sub_55F510 | `u8` |
| 741 | GG_DESTROY_FAIL_REQ | sub_55FE30 | `u8` |
| 746 | GG_PNR_RESPON_REQ | sub_5603A0 | `s32` |
| 749 | GG_GIMMICK_DAMAGE_REQ | MapGimmickSystem::sub_9CE1A0 | `(空)` |
| 752 | GG_MAPINFO_RELOAD_REQ | sub_57D390 | `(空)` |
| 756 | GL_CLAN_TNMT_RECEIPT_REQ | sub_57DF80 | `s32` |
| 758 | GL_CLAN_TNMT_RECEIPT_CANCEL_REQ | sub_57E1C0 | `s32` |
| 762 | GL_CLAN_TNMT_CURRENT_STATE_NOTICE_REQ | sub_57E3F0 | `(空)` |
| 764 | GL_CLAN_TNMT_ENTERROOM_REQ | sub_57E8F0 | `u8 s32` |
| 767 | GL_CLAN_TNTM_AWARD_INFO_REQ | sub_5813B0 | `(空)` |
| 771 | GL_CLAN_TNMT_ALL_INFO_REQ | sub_57E490 | `s32` |
| 773 | MASTER_RELOAD_TNMT_REQ | sub_57D8D0 | `(空)` |
| 776 | GL_CLAN_TNMT_CLANREC_REQ | sub_581C60 | `(空)` |
| 783 | GL_NEW_MSG_COUNT_REQ | sub_5643E0 | `(空)` |
| 785 | GL_FRIEND_ADD_PROCESS_REQ | sub_5644D0 | `str` |
| 787 | GL_RACKINGWEB_TOKEN_REQ | sub_581E40 | `(空)` |
| 791 | GL_VOICEITEMSLOT_REQ | sub_885590 | `(空)` |
| 793 | GI_VOICEITEMSLOT_ALL_REQ | sub_885C00 | `(空)` |
| 795 | GI_CHANGE_VOICEITEMSLOT_REQ | sub_885F10; sub_886330 | `u8 u8 s16 s16 u8 u8 s16 u8 \|\| s32 s16 s16 s16 u8` |
| 799 | GT_CRITICAL_ERROR_REPORT | sub_528960 | `s32 s16 s32 s32 s32 u8 u8 s32 u8 u8 s32 u8 u8 s32 u8 s32 u8 s32` |
| 800 | MASTER_XTRAP_RELOAD | sub_581EE0 | `(空)` |
| 802 | GS_DESTROYITEM_REQ | sub_895B90（direct；caller sub_894070） | `s32 raw0,s32 raw1,u8 count,count×raw4 raw2`；每筆記錄可附加一段以 native-object 為界的 raw4 值串；串長取自 client 狀態，不以獨立 count 送出。欄位／domain 涵義維持 UNRESOLVED。 |
| 804 | MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ | sub_581F80 | `(空)` |
| 806 | GS_HIDDEN_ITEM_LIST_REQ | CLobbyPartsUpRoom::sub_9C1DD0; sub_46C760 | `s16` |
| 808 | GS_GET_RECOMMENDSET_INFO_REQ | sub_46E140 | `s32 count,count×raw4`；sender 僅在 count>0 時組包送出；每個 optional 正值 native 狀態 slot 貢獻一個 raw4 |
| 812 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ | sub_582390 | `s8` |
| 814 | MASTER_CHECK_BOMB_CHEATER_APPLY_REQ | sub_5822E0 | `s8` |
| 819 | MASTER_CHECK_NPGAMEGUARD_QUERY_REQ | sub_582600 | `(空)` |
| 820 | GG_CHATTING_PENALTY_REPORT_REQ | sub_5826A0 | `s32` |
| 822 | MASTER_CHAT_BAN_REQ | sub_582020 | `u8 u8 str` |
| 824 | MASTER_USERLIST_REQ | sub_5821A0 | `u8 s32` |
| 828 | MASTER_RESETCOINTIME_REQ〔推定〕 | sub_57D430 | `s32` |
| 830 | MASTER_CHAT_FORCE_BAN_REQ | sub_5820E0 | `u8 str s32` |
| 831 | MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ | sub_583070 | `s32` |
| 834 | GL_DATA_RECV_COMPLETED_REQ | sub_583120 | `s32` |
| 836 | GL_SHOUTCHAT_REQ | sub_583370 | `s32 s32 str` |
| 838 | GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ | sub_584500 | `u8 s32` |
| 841 | MASTER_SETALL_EVENTEXP_REQ | sub_5840B0 | `f32` |
| 843 | MASTER_SETALL_EVENTPAGE_REQ | sub_584160 | `f32` |
| 845 | MASTER_VIEWALL_EVENTSTATE_REQ | sub_584210 | `(空)` |
| 849 | MASTER_TNMT_VIEW_STATE_REQ | sub_57DD30 | `(空)` |
| 851 | MASTER_RELOAD_GAMECENTER_RANKING_REQ〔推定〕 | sub_57DA90 | `(空)` |
| 853 | MASTER_PRINTGCRANK_REQ〔推定〕 | sub_57DB30 | `(空)` |
| 855 | GL_MYWAREHOUSEINFO_REQ | sub_585550 | `s32` |
| 857 | GL_MYWAREHOUSEITEMLIST_REQ | sub_585630 | `raw1` |
| 859 | GL_PUSH_TO_WAREHOUSE_REQ | sub_585720 | `raw5` |
| 861 | GL_POP_TO_WAREHOSUE_REQ | sub_585820 | `raw5` |
| 864 | GL_SERVER_DATETIME_REQ | sub_585A10; sub_91CC70 | `(空)` |
| 867 | GQ_QUEST_ACCEPT_REQ | sub_91CB80 | `raw4` |
| 869 | GQ_QUEST_CANCEL_REQ | sub_91D1A0 | `raw4` |
| 871 | GQ_QUEST_SUCCESS_REQ | sub_91C610 | `raw4` |
| 873 | GQ_QUEST_COMPLETE_REQ | sub_91C060 | `raw4` |
| 876 | GQ_QUEST_ACCEPT_DAILY_REQ | sub_91D730 | `(空)` |
| 878 | GQ_QUEST_USER_COMPLETE_HONOR_REQ | sub_91C9D0 | `s8` |
| 883 | MASTER_FIND_USER_REQ | sub_579AD0 | `s32` |
| 885 | MASTER_PLAY_WITH_REQ | sub_57A450 | `s32` |
| 887 | GX_XIGNCODE_DATA_REQ | sub_918100 | `rawN` |
| 890 | GC_QUERY_CLANRANKING_REQ | sub_585AF0 | `(空)` |
| 892 | MASTER_RELOAD_CLANRANKING_REQ | sub_585C10 | `(空)` |
| 894 | GR_TEAMSHUFFLE_REQ | sub_585DC0 | `u8 u8` |
| 896 | MASTER_RSHUFFLEWT_REQ〔推定〕 | sub_57DBD0 | `s32` |
| 898 | MASTER_RSHUFFLEVT_REQ〔推定〕 | sub_57DC80 | `s32` |
| 900 | GS_CAPSULEMACHINE_START_REQ | sub_99CFA0（direct；相關 caller sub_99D0A0） | `u8 raw0,s32 raw1`（精確 5B；觀察到之 raw pair 含 `{3,1}` 與 `{1,10}`；`sub_99D0A0` 為相關狀態路徑，非第二個 direct ctor） |
| 902 | GG_OCC_START_REQ | sub_564CF0 | `u8 u8 s32` |
| 904 | GG_OCC_SUCC_REQ | sub_565120 | `u8 u8 s32` |
| 906 | GG_OCC_FAIL_REQ | sub_565470 | `u8 u8 s32` |
| 909 | GG_OCC_RESPON_REQ | sub_559020 | `s32` |
| 912 | GL_WEAPONPARTS_EQUIP_CHANGE_REQ | sub_95AEF0×3 | `u8 raw0,s32 raw1,s32 raw2`；raw0==2 追加 `s32 raw3`；三個 direct site 為 `sub_95AEF0` 之分支變體，欄位／domain 涵義維持 UNRESOLVED |
| 918 | GR_AI_GET_REWARD_ITEM_REQ | sub_761A70 | `u8` |
| 922 | GR_AI_DAMAGE_SHIELD_REQ | sub_761580 | `s16 s16 s16 f32` |
| 924 | GR_AI_RECHARGE_MAGAZINE_START_REQ | sub_558350 | `u8 u8 u8` |
| 926 | GR_AI_RECHARGE_MAGAZINE_END_REQ | sub_5586B0 | `u8 u8 s8` |
| 928 | GR_AI_CONTINUE_START_REQ | sub_761DB0 | `s32` |
| 930 | GR_AI_CONTINUE_FAIL_REQ〔推定〕 | sub_7620A0 | `(空)` |
| 932 | MASTER_RELOAD_AIXML_REQ〔推定〕 | sub_582440 | `(空)` |
| 935 | GR_AI_FEVER_START_REQ | sub_7622C0 | `(空)` |
| 939 | GR_AI_GO_NEXT_WAVE_REQ | sub_75CE40 | `(空)` |
| 944 | GR_RESET_GAMEROOMSLOT_REQ | sub_585E90 | `(空)` |
| 953 | MASTER_PVE_REQ〔推定〕 | sub_57D970 | `s8` |
| 957 | GR_TIMEOVER_ONGAME_RESPON_REQ〔推定〕 | sub_565E00 | `s8` |
| 962 | GG_DROPWEAPON_GET_AND_DROP_REQ | sub_566F50 | `s16 s16 u8 s16 s16 f32` |
| 964 | GG_GET_BALL_REQ | sub_565F60 | `(空)` |
| 967 | GG_GET_GOAL_REQ | sub_566120 | `(空)` |
| 969 | GR_SOCCER_REQ | sub_5860C0 | `s8` |
| 971 | GG_SOCCER_RESPON_REQ | sub_566350 | `s32` |
| 973 | MASTER_RESETSOCCERBALLRESPAWNTIME_REQ〔推定〕 | sub_57D4F0 | `s32` |
| 975 | MASTER_SETMULTIPLYDAMAGE_REQ〔推定〕 | sub_57D5A0 | `u8 f32` |
| 983 | GL_MATCHINGROOM_MAKE_REQ | sub_586220 | `u8 str s32 u8 u8 u8 u8 u8 u8 u8 u8 u8` |
| 988 | GL_MATCHINGROOM_CANCLE_REQ | sub_587F70 | `(空)` |
| 990 | GR_DAMAGEROOM_REQ | sub_56F950 | `s8` |
| 992 | GQ_QUEST_LIST_REQ〔推定〕 | CLobbyQuest::sub_9C7F30 | `(空)` |
| 996 | GL_BLOCK_ADD_REQ〔推定〕 | sub_567E70 | `str` |
| 998 | GL_BLOCK_DEL_REQ〔推定〕 | sub_568030 | `str` |
| 1000 | GL_BLOCK_LIST_REQ〔推定〕 | sub_567CB0 | `(空)` |
| 1002 | GL_BLOCKME_LIST_REQ〔推定〕 | sub_567B30 | `(空)` |
| 1004 | GL_RANDOMMAP_LIST_REQ〔推定〕 | sub_588420 | `(空)` |
| 1006 | GG_OCC_ZONE_ENTER_REQ〔推定〕 | sub_564B70 | `u8` |
| 1008 | GG_OCC_ZONE_LEAVE_REQ〔推定〕 | sub_564C30 | `u8` |


## Appendix A — low private-UDP Packet constructors excluded from the TCP count

> **中文導讀**：本節＝私人 UDP **送端** builder 清單（15 op／19 direct sites）。
> 這些低 op 刻意不計入 Part II 的 261 TCP 口徑；方向恆為 client → UDP 目的端。

> **Scope / count（Fact；current `PaperMan.exe.c`）**：本輪 source snapshot 是
> 23,357,375 bytes、SHA-256 `e40df3d16efd7340810de8cfed7a46e270f854d04cf1b16b3f6b1d88b1791125`。
> native scan 找到 19 個 `Packet::possible_ctor_or_dtor_0(..., N)` low-opcode direct sites、15 個 unique
> opcode：`1,5,6,9,13,14,15,17,19,21,23,27,30,32,35`。它們不屬於上方
> `101+` TCP/C2S row inventory，也不應被加回成 TCP forms。以下 sequence 是
> constructor opcode 之後的 native write order；不包含 UDP private 的 8-byte
> Packet/framing header、AES ciphertext 或 `sendto` framing。
>
> `sub_595E80` 是另一個由 `recvfrom` 進入的 UDP-private dispatcher；其 inbound
> case/layout 不能因 opcode 數字相鄰而取代 outbound builder。**Appendix A 的每一列
> direction 都是 outbound（client → native-selected UDP destination）；Appendix B
> 的每一列 direction 都是 inbound（UDP source → client）。**同一數字若同時出現在
> A/B，仍是兩個方向的獨立 native evidence。**2026-09-17 深潛稽核已把每個 op 的
> 觸發點、state 消費者與生命週期定案，見 `PACKETS.md` §2.6（含 A/B 雙通道
> hole-punch 狀態機、19↔20 註冊握手、8/24 移動套用鏈與存活度標注）。**
> **2026-09-18：用途定案者已比照官方 catalog 風格賜予推定名，唯一定義處為
> `PACKETS.md` §2.6〈命名總表〉；本表各列僅記名稱句柄。**
> **2026-09-18 傳輸層定案（詳 PACKETS §2.6）：catalog ≥100 的 UDP 命名帶屬
> TCP/catalog 命名空間——`sub_58B010` 調度、且無一站經 `sub_595A10`／
> `sub_595980` private lane；唯 154/158 在 `sub_595E80` 上與本表私人線
> 數值重疊雙棲。**現有 native evidence 證明的
> `n → n+1` case pairs、AES/raw send lane、secondary sockaddr 與 `UNRESOLVED`
> server boundary 詳見 [`PACKETS.md`](PACKETS.md) §2.5；這裡保留 constructor-level
> inventory，避免把 UDP evidence 從本文件的完整 native builder audit 中遺漏。

| op | direct ctor xref（19 sites） | opcode 之後的寫入序列 | native send / caller / 邊界 |
|---:|---|---|---|
| 1 | `sub_593830`（1 site；shared function 的 `n2!=2` branch） | `u8×3 s32` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；僅 client 端送出事實；推定名 `Y_UDP_C_AHOLE_INF` |
| 5 | `sub_593AB0` | `u8 raw4` | 由 `sub_595E80` case 4 呼叫；經 `sub_595980` 以明確 `raw16` destination 送出；符合之已存 member address 一律重送三次；`raw4` 為 `sub_592AA0` 之 caller-defined elapsed/context 值；server 角色 UNRESOLVED；推定名 `Y_UDP_C_APUNCH_INF`（client-authored；C2C 半邊） |
| 6 | `sub_593E60` | `u8 raw4` | 由 `sub_595E80` case 5 呼叫；經 `sub_595980` 以明確 `raw16` destination 送出；第一個符合的 received source 會被儲存並重送三次；`raw4` 為 `sub_592AA0` 之 caller-defined elapsed/context 值；server 角色 UNRESOLVED；推定名 `Y_UDP_C_APUNCH_ACK`（C2C） |
| 9 | `sub_594300` | `u8×3 s32` | 由 `sub_5942B0` 呼叫；`sub_595A10` secondary AES lane；週期性 client 路徑；不推斷 server 行為；推定名 `Y_UDP_C_BHOLE_INF` |
| 13 | `sub_594460`; `sub_5946C0`（2 sites） | 各為 `u8 raw4` | 由 `sub_595E80` cases 10/12 呼叫；經 `sub_595980` 以已存位址送出；兩個 constructor 維持獨立 native call site；`raw4` 為 `sub_592AA0` 之 caller-defined 輸出；推定名 `Y_UDP_C_BPUNCH_INF`（C2C） |
| 14 | `sub_594A10` | `u8 raw4` | 由 `sub_595E80` case 13 呼叫；經 `sub_595980` 以已存位址送出；source/本地 state 分支與 13 不同；`raw4` 為 `sub_592AA0` 之 caller-defined 輸出；domain UNRESOLVED；推定名 `Y_UDP_C_BPUNCH_ACK`（C2C） |
| 15 | `sub_593830`（1 site；shared function 的 `n2==2` branch） | `u8×2` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；與 1 同屬一個 native function，但 wire form 不相同；不賜名＝mode-2 語理未證 |
| 17 | `sub_596180`; `sub_596240`（2 sites） | `sub_596180`：空；`sub_596240`：`str`（ANSI/NUL） | 兩個 global builder 2026-09-18 經 `PaperMan.exe` 三式掃描確證**零進入邊＝死鏈**（詳 PACKETS §2.6 存活度註記）；兩者皆走 primary raw `sub_595900`，繞過 AES；空形式與字串形式必須維持分開；推定名 `UDP_PROBE_REQ`〔推定；死鏈〕 |
| 19 | `sub_596670` | `u8×2 s8 u8 s32 str` | direct callers：`sub_4070B0`、`sub_407290`、`CLobbyGameStart::sub_43C380`；`sub_595A10` secondary AES lane；offset 2 使用 `sub_5928E0` s8；offset 3 使用 `sub_592920` u8，source 為 `-2` 時送出 `0xFE`；nickname 字串為 native writer 輸出；推定名 `UDP_REGISTER_REQ` |
| 21 | `sub_596330` | `u8×3 s32 raw4 raw4` | `sub_595D80` active-manager caller；經 `sub_595A10`；尾端兩個 `sub_592AA0` 值維持 caller-defined raw4；推定名 `UDP_KEEPALIVE_REQ` |
| 23 | `sub_744450` | `u8×3 s32 raw4 u8 u16×3 u8 u8×8 s32` | direct callers：`sub_600770`、`sub_73E170`；經 `sub_602D70 → sub_596B90 → sub_595A10` gate；`sub_592AA0` n0x64 為 caller-defined raw4；寬度為 Fact，欄位涵義 UNRESOLVED；推定名 `Y_UDP_C_MOVE_INF` |
| 27 | `sub_6013E0`; `sub_6036F0`（2 sites） | 各為 `u8×3 s32 u8×2` | direct callers：`sub_73E170`→`sub_6013E0`；`sub_5607C0`／`sub_6013E0`→`sub_6036F0`；`sub_602D70 → sub_596B90 → sub_595A10` gate；尾端兩個 u8 值為 native state byte；推定名 `Y_UDP_C_HIT_INF` |
| 30 | `sub_606340`（空 constructor；此 site 無 send）；`sub_6065E0` | `sub_606340`：空；`sub_6065E0`：`u8×3 s32 u16 count, count×{s8 status,[u16 若 status!=0,[條件式 u16 s8 u16 f32×3 s32]]}` | callers：`sub_73E170`→`sub_606340`；`sub_606340`（2 sites）／`sub_6065D1`→`sub_6065E0`；僅 `sub_6065E0` 呼叫 `sub_595A10`；`sub_761500(...)` 為本地暫存，非 wire 欄位；推定名 `Y_UDP_C_BOT_INF` |
| 32 | `sub_96BF70` | `u8×3 s32 u8 s8 u16×3` | `sub_967E90` caller；經 `sub_595A10` 之 object/position 路徑；三個 `u16` 值無已證明之 domain 名稱；推定名 `Y_UDP_C_OBJPOS_INF` |
| 35 | `sub_7463E0` | `u8×3 s32` | `sub_749B90`（2 次 direct call）；`sub_67F380` gate 之後走 `sub_595A10` secondary AES lane；為純送端證據，不推斷接收端或遊戲涵義；推定名 `Y_UDP_C_TCPINF_ACK` |

> **UDP framing 邊界（Fact / HIGH）**：除非該 row 另有說明，ops
> 1/5/6/9/13/14/15/19/21/23/27/30/32/35 使用已觀察到的 AES send lane；op 17
> 使用獨立的 raw primary lane。`sub_595940` 雖存在為另一個 secondary
> raw-send wrapper，但本 dump 未回收其任何 direct caller，故不構成 op 17 的
> 額外變體。`n+1` 的 inbound case 是 reader 端證據，不能證明 server 接受，
> 也不能證明存在相同的反方向 layout。除另行文件化的 19→20 client/server
> 交換外，server 行為維持 `UNRESOLVED`；本 Appendix 不授權實作任何 UDP
> server 行為。


## Appendix B — `sub_595E80` UDP-private dispatcher / every verified receive case

> **中文導讀**：本節＝私人 UDP **收端** dispatcher 全 22 case。`sub_595E80` 與
> TCP 主 dispatcher `sub_58B010` 是兩個獨立命名空間；數值相同也不共用 layout。

> **Dispatcher boundary（Fact / HIGH）**：UDP receive 的唯一已定位主要 dispatcher 是
> `sub_595E80(CUDPNetworkManager, packet)`。呼叫鏈是
> `sub_595A60 → sub_596F90(recvfrom, 9600) → sub_591FB0` 填入 Packet buffer，
> `sub_591D50` framing check，`sub_5930C0` AES decrypt，最後才進
> `sub_595E80`。dispatcher 額外要求 manager `+52 != 0`、`this_10 != nullptr`
> 且 `sub_67EAC0()==0`；否則不讀 body、不回覆。switch 使用
> `sub_591EE0(packet)` 的 UDP Packet opcode；`default` 靜默忽略。這個 opcode
> 空間和 `sub_58B010` 的 TCP/catalog dispatcher 分離，即使數字或官方 catalog
> 名稱相同，也不能直接合併 layout 或方向。**命名狀態欄之「推定名」統一定義於
> `PACKETS.md` §2.6〈命名總表〉（2026-09-18，比照官方風格之推定命名）**。
>
> **Native receive case set（Fact）**：`2,4,5,6,8,10,12,13,14,15,18,20,22,24,26,28,29,31,33,34,154,158`。
> `8/24` 共用同一 handler；其餘 case 各有直接 branch。這些 inbound rows 都先
> 經 native Packet 的 8-byte header、`sub_591D50` framing check 與
> `sub_5930C0` AES receive/decrypt lane；本 dump 沒有證明 inbound private raw-17
> exception。以下的 `raw2/raw4/raw16` 僅表示 native reader 的固定 byte width；沒有
> 獨立 signedness/domain evidence 時不升格成 numeric type。`source sockaddr` 是
> `recvfrom` 的 out-parameter projection（`dword_1326944..dword_1326950`），不是
> packet body 欄位。凡 row 寫明 response/retry，其 destination、`sub_595980`/
> `sub_595A10` lane 與次數均是 native send fact；其餘 row 是 no response/send-only
> 或 local consumer，不添加推測 destination。
>
> **2026-09-17 深潛稽核**：每個 receive case 的 state 用途、消費者與生命週期定位
> 已在 `PACKETS.md` §2.6 定案（含 A/B 狀態機 state byte 由哪個 case 設為 2/4/7、
> 8/24 的佇列套用鏈 `sub_593750→sub_593510→sub_602E30`、22/154 的
> `SOLO_RESULT_PING` 消費者、29/158 的 lang 原文錨）；本 matrix 維持 wire fact 層。

### B.1 Dispatcher case matrix

| inbound op | native 分支 / parser | 精確消耗的 body | 直接觀察到的效果 / 失敗邊界 | 命名狀態 |
|---:|---|---|---|---|
| 2 | `sub_593A60` | 未讀 body | 若 global `n0x3E8==0`，設為 1，將 `timeGetTime()-dword_F2563C` 存入共享 elapsed 值，並設 `byte_1324330=2`；後續收到僅遞增該 global counter。不做 payload-empty 驗證。 | 推定名 `Y_UDP_S_AHOLE_ACK`〔推定〕；無 server 端證明前仍不得稱其為泛用 `PING` |
| 4 | `sub_593AB0` | `u8 entryCount`；重複 `u8 memberKey + raw16 addressBlob` | 於 16 項 `dword_F6DCF4` 表逐一查 key；未知 key 立即 return，先前已修改之 entry 保留。已知 entry 將 raw16 存至 `unk_F6D584+240780*i`、設 `byte_F6D5B0[i]=1`；再構成 op 5（`u8 + raw4 elapsed`），對每個已存非本地 address 送出三個 AES datagram。本地 state byte 設為 4。 | 推定名 `Y_UDP_S_AHOLE_LIST_INF`〔推定〕；address 分發／peer 角色維持 UNRESOLVED |
| 5 | `sub_593E60` | `u8 memberKey + raw4` | raw4 被讀入 4-byte 本地變數，但在已回收的狀態轉移中未被使用。已知 key：重複收到僅遞增 `byte_F6D5A4[i]`；首次收到設 `byte_F6D5A4/A5`，將當前 `recvfrom` source sockaddr 存入 `unk_F6D594+240780*i`，構成 op 6（`u8 + raw4 elapsed`）並對該 source 送三次。 | 推定名 `Y_UDP_C_APUNCH_INF`〔推定〕（client-authored；C2C 半邊）；raw4 欄位仍未證明是 `HOLE` 或 `PING` 語義 |
| 6 | `sub_5940E0` | `u8 memberKey + raw4` | raw4 被讀但未消費。若已知 key 的 `byte_F6D5A5[i]==0`，設 `byte_F6D5A4/A5` 並存當前 source sockaddr；不構成回應。 | 推定名 `Y_UDP_C_APUNCH_ACK`〔推定〕（C2C） |
| 8 / 24 | `sub_596940 → sub_593750`，僅當 `n15==13`；否則走 bug/report 路徑 | dispatcher wrapper 不讀任何欄位；`sub_593750` 在 critical section 內 enqueue／複製 Packet | 字面 error 路徑指名消費者為 `OnY_UDP_S_MOVE_INF`；後續 parse 由 queue/callee 路徑擁有，非本 dispatcher wrapper。若 `n15!=13`，log `BUGCUDPNetworkManager::OnY_UDP_S_MOVE_INF` 並呼叫本地 debug/report helper。 | shared handler 標籤 `Y_UDP_S_MOVE_INF` 為 native 字串證據；8 與 24 各自的精確 op-to-name 對應未被證明 |
| 10 | `sub_594460` | `u8 memberKey + raw16 addressBlob` | 找 key（decompiler 迴圈呈現等待至符合）；`byte_F6D5B0[i]==0` 時存 raw16 address 並設 flag。構成 op 13（`u8 + raw4 elapsed`），對已存 address 送三次；本地 state `this+1=2`。 | 推定名 `Y_UDP_S_BHOLE_ONE_INF`〔推定〕 |
| 12 | `sub_5946C0` | `u8 entryCount`；重複 `u8 memberKey + raw16 addressBlob` | 存每一筆已知 entry；未知 key 直接 return、先前 entry 保留。以 `timeGetTime()-dword_F2563C` 設共享 elapsed 值，構成 op 13（`u8 + raw4 elapsed`），對每個已存非本地 key 送三次；本地 state `this+1=4`。 | 推定名 `Y_UDP_S_BHOLE_LIST_INF`〔推定〕 |
| 13 | `sub_594A10` | `u8 memberKey + raw4` | raw4 被讀但未使用。已知 key：重複收到遞增 `byte_F6D5A4[i]`；首次收到設 `byte_F6D5A4/A5`、存當前 source sockaddr，構成 op 14（`u8 + raw4 elapsed`）送三次；本地 state `this+1=4`。 | 推定名 `Y_UDP_C_BPUNCH_INF`〔推定〕（C2C） |
| 14 | `sub_594CA0` | `u8 memberKey + raw4` | raw4 未使用。若已知 key `byte_F6D5A5[i]==0`，設 `byte_F6D5A4/A5`、存當前 source sockaddr，state `this+1=4`；無回應。 | 推定名 `Y_UDP_C_BPUNCH_ACK`〔推定〕（C2C） |
| 15 | `sub_593DF0` | `raw16` | 一次性 latch `byte_F25646`：首個 packet 將 raw16 複製至 `unk_F25648`；後續忽略。無回應。與 outbound op 15（`u8,u8`）為不同形式。 | private unnamed；不得合併方向 |
| 18 | `sub_596300` | 未讀 body | 一次性 latch `n0x3E8_1`；首個 packet 呼叫 `sub_556530`，於 TCP socket 構成並送出 catalog/TCP opcode `141 PM_CONNECT_REQ`；後續 packet 無作用。未回收任何 UDP body 消費者。 | 推定名 `UDP_PROBE_ACK`〔推定〕；private UDP trigger 維持 source-oriented 描述；內層 TCP opcode 141 有官方 `PM_CONNECT_REQ` 證據 |
| 20 | `sub_5968C0` | 未讀 body | 設 `byte_1D0CFE7=1`，清 manager retry/state word `+44,+8,+4`，更新 `+24=timeGetTime()`，並經 `sub_594F00` 清 `byte_1324331`。不讀任何 identity 或 gameplay 欄位。 | 推定名 `UDP_REGISTER_ACK`〔推定〕（`UDP_REGISTER_REQ` 之完成端）；官方名未回收 |
| 22 | `sub_5964E0` | `u8 updateFlag`；若 `==1`：`u8 count`，重複 `u8 memberKey + raw4 value` | 更新 timer/network-manager 本地 state，再僅對已知 key 將 `raw4 value` 寫入 `dword_F6D9E8[i]`。無回應。flag 非 1 時讀完第一個 byte 即停止。 | 推定名 `UDP_MEMBERPING_INF`〔推定〕 |
| 26 | `unknown_libname_107` | 無（不讀 body） | 2026-09-18 由 `PaperMan.exe` 直讀 16-byte 本體（`55 8B EC 51 89 4D FC` `8B E5 5D C2 04 00` + `CC×3`）＝**空 thiscall**：存 this 後即 `ret 4` 吞掉唯一 packet 參數；不讀、不寫、不回應——效果確定為零。原「函式體不在 dump」僅是 IDA 未反編譯 | 行為 Fact（PE 直讀）；名稱因無原生字串仍維持 IDA 標籤；server 端送出理由 UNRESOLVED |
| 28 | `sub_594E80 → sub_74D130 → sub_9FA000` | gated reader：`u8×3, u8, u8, raw2, u8 count`；選定規則下 `count×{u8 index, raw4 value, raw2 state}` | 僅在已回收之 gameplay/object gate 成立時進入。合法 record 可經 `sub_9FA860` 更新內部 object flag/timestamp 並插入 queue；本地 gate 失敗可能不讀任何 body。無回應。 | shared consumer 無名；不得沿用 outbound op 27 之名 |
| 29 | `sub_593E20` | 未讀 body | 經 `sub_555030(&dword_1321D00)` 關閉 TCP socket，載入 resource `0xA8`，呼叫本地 notice `sub_9A7DE0(...,37,1)`。無 packet 衍生欄位。 | 推定名 `UDP_DISCONNECT_INF`〔推定〕；private 本地 notice 觸發 |
| 31 | `sub_594EA0 → sub_606AD0` | gated header `u8×3, raw4 gateValue, s16 recordCount`；每筆 record 以 `u8 active` 起頭，尾段依 object 而異 | 合法 object 分支消費 object/member key、status、raw2 state、`f32×3` position 類值與 raw4；fallback 分支消費不同尾段但不使用其值。僅更新 client object state；無回應。`recordCount` 為 native `s16`，非已證明之 unsigned count。 | 推定名 `Y_UDP_S_BOT_INF`〔推定〕（op30 之對向家族） |
| 33 | `sub_594EC0 → sub_96C1E0`，當 global `n2_24!=0` | gated `u8,u8,u8,raw4,u8,u8,s16×3` | 本地 gate 通過時，將三個有號 16-bit 值除以 3 寫入 object `+60/+64/+68`；寫相鄰 state byte 並清 `+84`。若 `n2_24==0`，wrapper 不讀任何內容。無回應。 | 推定名 `Y_UDP_S_OBJPOS_INF`〔推定〕（op32 之對向家族） |
| 34 | `sub_594F20` | 恰三筆 record `{u8×4, raw2, u8}`，再接兩個 `raw2` 值 | 對每筆 record 呼叫 `sub_778BC0`；其消費者使用 record 之部分 byte，但不使用 record raw2。查表成功時末尾兩個 raw2 寫入 current-member offset `+156/+160`。無回應。 | shared consumer unnamed |
| 154 | `sub_5965D0` | `u8 count`；重複 `u8 memberKey + u8 value` | 將已知 key 之值寫入 `dword_F6D9E8[i]`；未知 key 忽略；無回應。 | 與官方 `UDP_ALL_PING_ACK` 在數值／catalog 上重疊；行為與該標籤相容，但 private dispatcher 仍是權威 xref |
| 158 | `sub_596910` | 未讀 body | 載入 resource `0x127` 並呼叫 `sub_9A7DE0(...,65,1)`；無 packet 衍生 state 或回應。 | 與官方 `UDP_TCP_DEAD_ACK` 在數值／catalog 上重疊；native private handler 仍為 `sub_596910` |

> **命名欄的證據等級讀法**：direct `case → callee`、native reader helper
> 身份、固定寬度與 client state/send 呼叫是 **HIGH native fact**。
> `private unnamed` 表示此 native fact 很強、但其 protocol 用途／名稱仍為
> **UNRESOLVED**。`Y_UDP_S_MOVE_INF` 對 shared consumer 字串是 **HIGH** 證據，
> 但不含 8 與 24 各自的歸屬判定。`UDP_ALL_PING_ACK`、`UDP_TCP_DEAD_ACK` 是
> **HIGH catalog-name/value fact** 加 private case xref，但 dump 未證明
> private handler 是 catalog 協議的別名。`unknown_libname_107` 原為
> **HIGH 的未解決邊界 fact**；2026-09-18 由 `PaperMan.exe` 直讀其
> 16-byte 本體證明＝空函式（行為 Fact），邊界解除——layout 升格為
> 「無（不讀 body）」，名稱因無原生字串仍維持 IDA 標籤。
>
> **Reader 失敗邊界**：`sub_592500`／native Packet reader 可能失敗且不回滾
> 已修改的 caller state。case 4/12/22/28/31/33/34 的 count 迴圈不是 server
> 端 schema 或權限檢查；raw16 address blob、member key 與 `recvfrom` source
> sockaddr 皆為 client 端 state／選擇證據，不能證明 relay、NAT、認證、
> 擁有權或 server 接受。

### B.2 Direction pairs and non-pairs

下列僅為數值相鄰與 native 控制流關係，不是泛用的 request/response 契約：

| outbound builder | inbound case | native 關係 | layout 關係 |
|---:|---:|---|---|
| 1 | 2 | op 1 timer/state 路徑；case 2 更新同一共享 retry state | builder `u8×3+s32`；case 2 不讀 body |
| 5 | 6 | case 4 發出 5；case 5 發出 6 | outbound/inbound 均為 `u8+raw4`，但 raw4 未被證明是共同語義欄位 |
| 9 | 10 | 週期性 op 9；case 10 在 address 分發後啟動 op 13 | 無相同之反向 layout |
| 13 | 14 | case 10/12 發出 13；case 13 發出 14 | 均為 `u8+raw4`，但方向／狀態角色不同 |
| 14 | 15 | case 13 發出 14；case 15 消費 raw16 | 無相同之反向 layout |
| 17 | 18 | op 17 raw-primary 送出；case 18 一次性 TCP-connect 觸發 | op 17 為空或 ANSI/NUL 字串；case 18 為空 |
| 19 | 20 | `sub_596670` 構成 19；`sub_5968C0` 完成其 retry state | 精確已證明的控制交換；case 20 無 body |
| 21 | 22 | op 21 攜帶兩個 raw4 值；case 22 為更新列表 | 無相同之反向 layout |
| 23 | 24 | 移動／遊戲狀態送出與 shared move handler | case 24 wrapper 不讀 body；後續 queue parse 不在此證明 |
| 27 | 28 | 僅 native 數值相鄰 | outbound `u8×2` 尾端不等於 inbound case 28 header/records |
| 30 | 31 | 僅 native 數值相鄰 | 可變 bot record 對上 gated object state reader |
| 32 | 33 | 僅 native 數值相鄰 | 欄位順序與消費者不同 |
| 6,15,35 | 無獨立 `n+1` 證明 | 純送端或 reader 形狀衝突 | 不得推斷 server 回應 |

### B.3 Official/catalog naming cross-check

出貨 catalog（`db/packets.tsv`）與 native 字串註冊表（`PaperMan.exe.c` 行
`674468..674624` 附近）為獨立的 153..164 TCP/catalog band 提供官方名：

`153 UDP_ALL_PING_REQ`, `154 UDP_ALL_PING_ACK`, `155 Y_UDP_C_HOLE_INF`,
`156 Y_UDP_S_HOLE_INF`, `157 UDP_TCP_DEAD_REQ`, `158 UDP_TCP_DEAD_ACK`,
`159 TCP_UDP_DEAD_REQ`, `160 TCP_UDP_DEAD_ACK`, `161 UDP_TCP_LIVE_REQ`,
`162 UDP_TCP_LIVE_ACK`, `163 TCP_UDP_LIVE_REQ`, `164 TCP_UDP_LIVE_ACK`.

只有 154、158 同時是 `sub_595E80` 的 case；其 native private handler 與
layout 即上方各列。case 153,155,156,157,159..164 **不是**本 dispatcher 的
case，故其 catalog 名不得僅因數字相同而貼到 private op 上。字串
`OnY_UDP_S_MOVE_INF` 是 shared 8/24 consumer 標籤的直接 native 證據，但不
證明該標籤的官方 catalog 值是 8 還是 24。其餘 private 值在有更強的
native 名稱或雙向協議證據之前，維持 unnamed/source-oriented。

### B.4 Validation invariants for this appendix

本 dispatcher 稽核刻意做成可執行驗證，而非僅文字描述：

- `sub_595E80` 之 case 集合必須恰為 B.1 的 22 個值。
- low outbound constructor opcode 必須恰為 Appendix A 的 15 個值。
- 九個 shared-prefix builder 必須保有各自 native 身份來源與 `u8,u8,u8,s32`
  前綴；op 19 保留其第三純量 `s8` 與字串尾。
- `sub_595980` 為明確位址 AES 送出；`sub_595900` 為 raw primary 送出；
  `sub_595A10` 使用 secondary sockaddr。三者不可互換。
- 對不在 private switch 中的 opcode，不升格任何官方 catalog 名。

`python3 tools/verify_dispatcher_coverage.py` 檢查 opcode 集合與 shared
builder 前綴。native helper 函式體與 direct-caller 稽核仍為獨立檢查；
通過本 Appendix 不會把 UNRESOLVED 的 client state 升格為 server 權限。

---

## Part III — 全 opcode 覆蓋總表（752 個數值 op 索引；2026-09-18 生成）

> **這張表收錄所有已知 packet 數值**：官方 catalog 676＋有 native 證據但無官方名
> 76（TCP 46＋私人 UDP 30；`verify_dispatcher_coverage.py` 的 74 口徑因
> `| 8 / 24 |` 合體列而不含 8 與 24）＝**752 列，無缺漏**。它只是**索引**：欄位細節請回 Part I（S2C 讀取）、
> Part II（C2S 寫入）、Appendix A/B（私人 UDP）各列；語意與 consumer 請跳
> `PACKETS.md`。
>
> **欄位讀法**：
> - `S2C`/`C2S`/`UDP出`/`UDP入`：`✓`＝本檔有對應列；`✗`＝client build 無此方向
>   native endpoint；`—`＝命名空間不適用（私人 UDP op 不屬 TCP 兩表；TCP op
>   不屬 UDP 兩表）。
> - 名稱狀態：`官方 catalog`＝`db/packets.tsv` 註冊名（Fact，唯一定義處）；
>   `推定〔S2C 審計〕`／`推定〔C2S 審計〕`＝Part I/II 〈推定命名審計〉之推定名；
>   `推定〔UDP 命名總表〕`＝`PACKETS.md` §2.6 之推定名；`未命名`＝有 native
>   endpoint 但證據不足以命名（明確保留，非缺漏）。
> - **registry-only**（官方有名錄、但四個 lane 全 `✗`）：共 150 op——本檔
>   無任何已定位 native endpoint，可能是舊版遺留或純 server 內部用途；不得因
>   名字存在而推斷 client 行為。`681`／`694` 雖無表列但屬 login TCP 專屬處理
>   （`S2C_NATIVE_AUDITS.md`），已自本類除外；十二帶 153..164 的完整傳輸層
>   定案見 `PACKETS.md` §2.6。
> - **私人 UDP 帶（1–99）是獨立命名空間**（`sub_595E80`／`sub_595A10` lane）：
>   數值與 catalog 重複時互不通用；3/7/11/16/25/36..39 從未觀察到，不列。
> - 唯二雙棲＝**154／158**：官方名同時落在私人收端 case（詳 Appendix B.3）。

#### 1–99（私人 UDP 命名空間；數值與 catalog 重複亦不通用）

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 1 | `Y_UDP_C_AHOLE_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 2 | `Y_UDP_S_AHOLE_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 4 | `Y_UDP_S_AHOLE_LIST_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 5 | `Y_UDP_C_APUNCH_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✓ | C2C 打孔（client-authored） |
| 6 | `Y_UDP_C_APUNCH_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✓ | C2C 打孔 |
| 8 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✗ | ✓ | 與 24 共用 handler `sub_596940`；native 字串標籤 `Y_UDP_S_MOVE_INF` 之 8/24 歸屬未定 |
| 9 | `Y_UDP_C_BHOLE_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 10 | `Y_UDP_S_BHOLE_ONE_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 12 | `Y_UDP_S_BHOLE_LIST_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 13 | `Y_UDP_C_BPUNCH_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✓ | C2C 打孔 |
| 14 | `Y_UDP_C_BPUNCH_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✓ | C2C 打孔 |
| 15 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✓ | ✓ | 不賜名：mode-2 語理未證（PACKETS §2.6） |
| 17 | `UDP_PROBE_REQ`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ | 死鏈：兩 builder 零進入邊（PACKETS §2.6 存活度註記） |
| 18 | `UDP_PROBE_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 19 | `UDP_REGISTER_REQ`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 20 | `UDP_REGISTER_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 21 | `UDP_KEEPALIVE_REQ`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 22 | `UDP_MEMBERPING_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 23 | `Y_UDP_C_MOVE_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 24 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✗ | ✓ | 與 8 共用 handler（同上） |
| 26 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✗ | ✓ | PE 直讀＝空 thiscall（16B 本體）；`unknown_libname_107` |
| 27 | `Y_UDP_C_HIT_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 28 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✗ | ✓ | 不賜名：consumer 無名（PACKETS §2.6） |
| 29 | `UDP_DISCONNECT_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 30 | `Y_UDP_C_BOT_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 31 | `Y_UDP_S_BOT_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 32 | `Y_UDP_C_OBJPOS_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |
| 33 | `Y_UDP_S_OBJPOS_INF`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✗ | ✓ |  |
| 34 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | — | — | ✗ | ✓ | 不賜名：consumer 無名（PACKETS §2.6） |
| 35 | `Y_UDP_C_TCPINF_ACK`〔推定〕 | 推定〔UDP 命名總表〕 | — | — | ✓ | ✗ |  |

#### 100–199

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 100 | `GS_BASE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 101 | `GT_PING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 102 | `GT_PING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 103 | `GE_LOGOUT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 104 | `GE_LOGOUT_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 105 | `GL_USERLIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 106 | `GL_USERLIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 107 | `GL_GAMEROOMINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 108 | `GL_GAMEROOMINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 109 | `GL_ROOMINFOCHANGE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 110 | `GL_ROOMINFOCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 111 | `GL_MAKEROOM_REQ` | 官方 catalog | ✗ | ✓ | — | — | `PACKETS.md` §3.15：線性序列為機械壓平，真正 wire 以該節為準 |
| 112 | `GL_MAKEROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 113 | `GL_ENTERROOM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 114 | `GL_ENTERROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 115 | `GL_ADDUSER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 116 | `GL_ADDUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 117 | `GL_DELETEUSER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 118 | `GL_DELETEUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 119 | `GL_CHATTING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 120 | `GL_CHATTING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 121 | `GR_MAPCHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 122 | `GR_MAPCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 123 | `GR_LEAVE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 124 | `GR_LEAVE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 125 | `GR_CHATTING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 126 | `GR_CHATTING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 127 | `GR_READY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 128 | `GR_READY_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 129 | `GR_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 130 | `GR_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 131 | `GR_FORCEOUT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 132 | `GR_FORCEOUT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 133 | `GR_END_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 134 | `GR_END_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 135 | `GR_CHANGESLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 136 | `GR_CHANGESLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 137 | `GR_STARTTIME_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 138 | `GR_STARTTIME_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 139 | `GG_EXITGAME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 140 | `GG_EXITGAME_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 141 | `PM_CONNECT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 142 | `PM_CONNECT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 143 | `PM_UDPSTART_REQ` | 官方 catalog | ✗ | ✓ | — | — | 十二帶 TCP-live（PACKETS §2.6 傳輸層定案） |
| 144 | `PM_UDPSTART_ACK` | 官方 catalog | ✓ | ✗ | — | — | 十二帶 TCP-live（同上） |
| 145 | `PM_MASTER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 146 | `PM_MASTER_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 147 | `PM_ID_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 148 | `PM_ID_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 149 | `PM_LOGOUT_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 150 | `PM_LOGOUT_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 151 | `PM_CH_SERVER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 152 | `PM_CH_SERVER_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 153 | `UDP_ALL_PING_REQ` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 154 | `UDP_ALL_PING_ACK` | 官方 catalog | ✗ | ✗ | — | ✓ | 雙棲：官方 catalog 名＋私人 UDP 收端 case（`sub_5965D0`） |
| 155 | `Y_UDP_C_HOLE_INF` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 156 | `Y_UDP_S_HOLE_INF` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 157 | `UDP_TCP_DEAD_REQ` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 158 | `UDP_TCP_DEAD_ACK` | 官方 catalog | ✗ | ✗ | — | ✓ | 雙棲：官方 catalog 名＋私人 UDP 收端 case（`sub_596910`） |
| 159 | `TCP_UDP_DEAD_REQ` | 官方 catalog | ✗ | ✗ | — | — | 十二帶：僅名錄＋`sub_58D940` logger，無 case/builder |
| 160 | `TCP_UDP_DEAD_ACK` | 官方 catalog | ✓ | ✗ | — | — | 十二帶 TCP-live（同上） |
| 161 | `UDP_TCP_LIVE_REQ` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 162 | `UDP_TCP_LIVE_ACK` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 163 | `TCP_UDP_LIVE_REQ` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 164 | `TCP_UDP_LIVE_ACK` | 官方 catalog | ✗ | ✗ | — | — | 十二帶 registry-only（PACKETS §2.6 傳輸層定案） |
| 165 | `Y_TCP_INF_REQ` | 官方 catalog | ✗ | ✓ | — | — | 十二帶 TCP-live（17 builder 站；同上） |
| 166 | `Y_TCP_INF_ACK` | 官方 catalog | ✓ | ✗ | — | — | 十二帶 TCP-live（同上） |
| 167 | `GR_CHANGEUSER_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 168 | `GR_CHANGEUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 169 | `GR_RULECHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 170 | `GR_RULECHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 171 | `GR_WINCHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 172 | `GR_WINCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 173 | `GR_TIMECHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 174 | `GR_TIMECHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 175 | `GR_ITEMCHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 176 | `GR_ITEMCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 177 | `GR_AUTOCHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 178 | `GR_AUTOCHANGE_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 179 | `GS_STOREOK_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 180 | `GS_STOREOK_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 181 | `GL_ENTERSTARTROOM_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 182 | `GL_ENTERSTARTROOM_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 183 | `GR_ENDLOADING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 184 | `GR_ENDLOADING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 185 | `GR_JOINGAME_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 186 | `GR_JOINGAME_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 187 | `GG_STARTGAME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 188 | `GG_STARTGAME_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 189 | `GR_CHANGEMASTER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 190 | `GR_CHANGEMASTER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 191 | `GR_CALLUSER_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 192 | `GR_CALLUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 193 | `GC_CHANNEL_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 194 | `GC_CHANNEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 195 | `GC_ENTERCHANNEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 196 | `GC_ENTERCHANNEL_ACK` | 官方 catalog | ✓ | ✗ | — | — | switch 外：vtable 前置轉發處理（見 Bootstrap 段）；七欄尾段僅 result==1 |
| 197 | `GL_MYINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 198 | `GL_MYINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 199 | `GL_MYITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |

#### 200–299

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 200 | `GL_MYITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 201 | `GL_MYPARTSUP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 202 | `GL_EXPIRE_PARTSUP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 203 | `GL_MYAVATARINFO_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 204 | `GS_BUYITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 205 | `GS_BUYITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 206 | `GS_BUY_WEAPONPARTS_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 207 | `GS_BUY_WEAPONPARTS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 208 | `GS_SELLITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 209 | `GS_SELLITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 210 | `GM_CHECKNICK_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 211 | `GM_CHECKNICK_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 212 | `GM_CREATENICK_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 213 | `GM_CREATENICK_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 214 | `GM_CREATECHAR_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 215 | `GM_CREATECHAR_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 216 | `GL_ENTERROOMPASS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 217 | `GL_ENTERROOMPASS_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 218 | `GI_CHANGEDATA_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 219 | `GI_CHANGEDATA_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 220 | `GI_CHANGEWP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 221 | `GI_CHANGEWP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 222 | `GP_CHPLAYC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 223 | `GP_CHPLAYC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 224 | `GP_CHROUNDC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 225 | `GP_CHROUNDC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 226 | `GP_CHDISC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 227 | `GP_CHDISC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 228 | `GP_CHWINC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 229 | `GP_CHWINC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 230 | `GP_CHLOSSC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 231 | `GP_CHLOSSC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 232 | `GP_CHKILLC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 233 | `GP_CHKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 234 | `GP_CHDEADC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 235 | `GP_CHDEADC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 236 | `GP_CHHEADSC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 237 | `GP_CHHEADSC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 238 | `GP_CHACOMBOC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 239 | `GP_CHACOMBOC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 240 | `GP_CHHEARTC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 241 | `GP_CHHEARTC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 242 | `GP_CHDKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 243 | `GP_CHDKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 244 | `GP_CHTKILLC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 245 | `GP_CHTKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 246 | `GL_CLIENTINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 247 | `GL_CLIENTINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 248 | `GR_CLIENTINFO_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 249 | `GR_CLIENTINFO_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 250 | `GL_LOBBYIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 251 | `GL_LOBBYIN_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 252 | `GL_SHOPIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 253 | `GL_SHOPIN_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 254 | `GL_INVENIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 255 | `GL_INVENIN_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 256 | `GL_ENTERROOMOB_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 257 | `GL_ENTERROOMOB_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 258 | `GR_LEAVEOB_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 259 | `GR_LEAVEOB_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 260 | `GL_JOIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 261 | `GL_JOIN_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 262 | `GL_JOINPASS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 263 | `GL_JOINPASS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 264 | `GL_JOININFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 265 | `GL_JOININFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 266 | `GL_JOINGAME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 267 | `GL_JOINGAME_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 268 | `GL_JOINPLAY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 269 | `GL_JOINPLAY_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 270 | `GL_MYINFO_OPEN` | 官方 catalog | ✗ | ✓ | — | — |  |
| 271 | `PM_TSPOSUPDATE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 272 | `PM_TSPOSUPDATE_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 273 | `GR_TSTARTPOS_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 274 | `GR_TSTARTPOS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 275 | `MASTER_MEMO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 276 | `MASTER_MEMO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 277 | `MASTER_MEMOALL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 278 | `MASTER_MEMOALL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 279 | `MASTER_USERCUT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 280 | `MASTER_USERCUT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 281 | `MASTER_USERCUT2_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 282 | `MASTER_USERCUT2_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 283 | `MASTER_ROOMCUT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 284 | `MASTER_ROOMCUT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 285 | `MASTER_MSET_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 286 | `MASTER_MSET_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 287 | `MASTER_PRINTUSER_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 288 | `MASTER_PRINTUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 289 | `MASTER_USERINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 290 | `MASTER_USERINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 291 | `MASTER_LISTCUT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 292 | `MASTER_LISTCUT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 293 | `MASTER_USERINFODB_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 294 | `MASTER_USERINFODB_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 295 | `MASTER_RELOAD_GM_NOTICE_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 296 | `GS_GIVEGIFT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 297 | `GS_GIVEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 298 | `GS_TAKEGIFT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 299 | `GS_TAKEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |

#### 300–399

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 300 | `GS_MOVEGIFT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 301 | `GS_MOVEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 302 | `GG_JJCREATE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 303 | `GG_JJCREATE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 304 | `GG_JJCHANGE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 305 | `GG_JJCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 306 | `GG_JJGET_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 307 | `GG_JJGET_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 308 | `GG_JJGAMEEND_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 309 | `GG_JJGAMEEND_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 310 | `GS_BUYCHAR_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 311 | `GS_BUYCHAR_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 312 | `GI_CHANGESLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 313 | `GI_CHANGESLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 314 | `GS_MOVEONEGIFT_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 315 | `GS_MOVEONEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 316 | `GG_HACKSTART_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 317 | `GG_HACKSTART_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 318 | `GG_HACKSUCC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 319 | `GG_HACKSUCC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 320 | `GG_HACKFAIL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 321 | `GG_HACKFAIL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 322 | `GG_BOMBSUCC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 323 | `GG_BOMBSUCC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 324 | `GG_BOMBEND_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 325 | `GG_BOMBEND_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 326 | `GG_UNHACKSTART_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 327 | `GG_UNHACKSTART_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 328 | `GG_UNHACKSUCC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 329 | `GG_UNHACKSUCC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 330 | `GG_UNHACKFAIL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 331 | `GG_UNHACKFAIL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 332 | `GG_KILLJJ_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 333 | `GG_KILLJJ_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 334 | `GG_SEEDKEY_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 335 | `GG_SEEDKEY_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 336 | `GG_UNIQUEKEY_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 337 | `GG_UNIQUEKEY_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 338 | `GG_DETECTCRACK_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 339 | `GG_DETECTCRACK_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 340 | `GR_KILLCHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 341 | `GR_KILLCHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 342 | `GG_SOLORESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 343 | `GG_SOLORESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 344 | `GG_LIVECHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 345 | `GG_LIVECHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 346 | `GG_TEAMCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 347 | `GG_TEAMCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 348 | `GG_DEADCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 349 | `GG_DEADCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 350 | `GG_TEAMDEADCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 351 | `GG_TEAMDEADCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 352 | `GG_LEVELJJ_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 353 | `GG_LEVELJJ_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 354 | `MASTER_UPITEM_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 355 | `MASTER_UPITEM_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 356 | `GS_CASH_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 357 | `GS_CASH_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 358 | `GS_BUYCASHITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 359 | `GS_BUYCASHITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 360 | `GG_TSURRESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 361 | `GG_TSURRESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 362 | `GP_CHCRITICALC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 363 | `GP_CHCRITICALC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 364 | `GR_BALANCECHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 365 | `GR_BALANCECHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 366 | `GR_LOCALROOM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 367 | `GR_LOCALROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 368 | `GR_TEAMSHUFFLECHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 369 | `GR_TEAMSHUFFLECHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 370 | `GL_CHANGECHANNEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 371 | `GL_CHANGECHANNEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 372 | `GR_ALLCRYSTAL_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 373 | `GR_ALLCRYSTAL_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 374 | `GR_GETCRYSTAL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 375 | `GR_GETCRYSTAL_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 376 | `GR_RECRYSTAL_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 377 | `GR_RECRYSTAL_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 378 | `GR_RADIOMSG_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 379 | `GR_RADIOMSG_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 380 | `GP_CHMKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 381 | `GP_CHMKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 382 | `GP_CHUKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 383 | `GP_CHUKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 384 | `GP_CHZKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 385 | `GP_CHZKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 386 | `GP_CHKKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 387 | `GP_CHKKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 388 | `GP_CHDDKILLC_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 389 | `GP_CHDDKILLC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 390 | `GL_DELETEITEM_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 391 | `GL_DELETEITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 392 | `GL_CHANGEID_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 393 | `GL_CHANGEID_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 394 | `MASTER_ROOMINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 395 | `MASTER_ROOMINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 396 | `PM_KICKUSER_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 397 | `PM_KICKUSER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 398 | `MASTER_SVRCLASS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 399 | `MASTER_SVRCLASS_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |

#### 400–499

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 400 | `MASTER_CONNTYPE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 401 | `MASTER_CONNTYPE_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 402 | `MASTER_EVENTPAGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 403 | `MASTER_EVENTPAGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 404 | `MASTER_EVENTEXP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 405 | `MASTER_EVENTEXP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 406 | `MASTER_ENABLE_LOGIN` | 官方 catalog | ✗ | ✓ | — | — |  |
| 407 | `MASTER_DISABLE_LOGIN` | 官方 catalog | ✗ | ✓ | — | — |  |
| 408 | `MASTER_DISBILL_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 409 | `MASTER_DISBILL_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 410 | `MASTER_DISLOGIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 411 | `MASTER_DISLOGIN_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 412 | `MASTER_DISGMS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 413 | `MASTER_DISGMS_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 414 | `MASTER_DISLOG_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 415 | `MASTER_DISLOG_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 416 | `MASTER_KILLALL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 417 | `MASTER_KILLALL_ACK`〔推定〕 | 推定〔PACKETS §3.15i〕 | ✓ | ✗ | — | — | dispatcher inline 處理，無獨立 handler |
| 418 | `MASTER_RESETTCPGROUPINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 419 | `GL_MSG_ADD_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 420 | `GL_MSG_ADD_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 421 | `GL_MSG_DEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 422 | `GL_MSG_DEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 423 | `GL_MSG_READ_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 424 | `GL_MSG_READ_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 425 | `GL_MSG_RECVLIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 426 | `GL_MSG_RECVLIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 427 | `GL_MSG_SENDLIST_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 428 | `GL_MSG_SENDLIST_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 429 | `GL_FRIEND_ADD_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 430 | `GL_FRIEND_ADD_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 431 | `GL_FRIEND_DEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 432 | `GL_FRIEND_DEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 433 | `GL_FRIEND_LIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 434 | `GL_FRIEND_LIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 435 | `GL_FRIEND_INFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 436 | `GL_FRIEND_INFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 437 | `GG_ROOMBROADCAST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 438 | `GG_ROOMBROADCAST_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 439 | `GL_FRIEND_CHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 440 | `GL_FRIEND_CHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 441 | `GL_FRIEND_WHERE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 442 | `GL_FRIEND_WHERE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 443 | `GG_STEALSUCK_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 444 | `GG_STEALSUCK_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 445 | `GG_STEALPUSH_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 446 | `GG_STEALPUSH_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 447 | `GG_STEALCOLORS_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 448 | `GG_STEALCOLORS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 449 | `GG_STEALRESPON_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 450 | `GG_STEALRESPON_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 451 | `GS_NEWGIFT_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 452 | `GS_NEWGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 453 | `GS_DELETEGIFT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 454 | `GS_DELETEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 455 | `GG_EXERCISERESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 456 | `GG_EXERCISERESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 457 | `GI_CHANGEITEMSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 458 | `GI_CHANGEITEMSLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 459 | `GC_CHANGECHANNEL_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 460 | `GC_CHANGECHANNEL_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 461 | `GS_USE_PAPERCODEGIFT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 462 | `GS_USE_PAPERCODEGIFT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 463 | `GS_ENTERPAPERCODEGIFT_NOTIFY` | 官方 catalog | ✗ | ✓ | — | — |  |
| 464 | `GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 465 | `GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 466 | `GI_CHANGE_SKILLITEMSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 467 | `GI_CHANGE_SKILLITEMSLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 468 | `GS_BUY_HUKUBUKURO_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 469 | `GS_BUY_HUKUBUKURO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 470 | `GS_GET_HUKUBUKURO_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 471 | `GS_GET_HUKUBUKURO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 472 | `GL_GAMECENTER_REC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 473 | `GL_GAMECENTER_REC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 474 | `GG_GAMECENTER_GAME_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 475 | `GG_GAMECENTER_GAME_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 476 | `GG_GAMECENTER_GAME_END_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 477 | `GG_GAMECENTER_GAME_END_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 478 | `GG_GAMECENTER_GAME_PLAY_CHECK_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 479 | `GG_GAMECENTER_GAME_PLAY_CHECK_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 480 | `GG_GAMECENTER_RANKING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 481 | `GG_GAMECENTER_RANKING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 482 | `GL_GAMECENTER_COIN_CHANGED_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 483 | `GG_GAMECENTER_GAME_START_OK_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 484 | `GG_GAMECENTER_GAME_START_OK_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 485 | `GL_GET_GAMEROOM_PROGRESSTIME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 486 | `GL_GET_GAMEROOM_PROGRESSTIME_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 487 | `GL_MYROOMCHANGE_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 488 | `GL_MYROOMCHANGE_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 489 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | ✓ | ✗ | — | — | 行為紀錄見 Part I 審計節（bit0 死路徑級） |

#### 500–599

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 560 | `GV_VIEWER_BASE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 561 | `GV_ROOMDUMP_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 562 | `GV_ROOMDUMP_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 563 | `GV_USERDUMP_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 564 | `GV_USERDUMP_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 565 | `GV_AUTHORIZE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 566 | `GV_AUTHORIZE_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 567 | `GV_HACKBLOCK_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 568 | `GV_HACKBLOCK_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 569 | `GV_HACKCLEAR_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 570 | `GV_HACKCLEAR_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 571 | `GV_TEST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 572 | `GV_TEST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 580 | `GC_CLAN_BASE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 581 | `GC_CLAN_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 582 | `GC_CLAN_START_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 583 | `GC_CLAN_PROTOCOL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 584 | `GC_CLAN_PROTOCOL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 585 | `GC_CLAN_CREATE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 586 | `GC_CLAN_CREATE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 587 | `GC_CLAN_GAMEEND_RESULT_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |

#### 600–699

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 680 | `GL_LOGIN_BASE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 681 | `GL_LOGIN_ACK` | 官方 catalog | ✓ | ✗ | — | — | switch 外：login TCP 專屬 reader（詳 `S2C_NATIVE_AUDITS.md` 681 part） |
| 682 | `GL_LOGIN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 683 | `GL_SERVERLIST_NOTICE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 684 | `GL_LOGIN_DUPLICATE` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 685 | `GL_TUTORIALINDEX_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 686 | `GL_TUTORIALINDEX_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 687 | `GL_TUTORIAL_START` | 官方 catalog | ✗ | ✓ | — | — |  |
| 688 | `GL_TUTORIAL_END` | 官方 catalog | ✗ | ✓ | — | — |  |
| 689 | `GL_TUTORIAL_INDEX_SET_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 690 | `GL_TUTORIAL_INDEX_SET_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 691 | `GL_ITEM_MODIFY_NOTIFIER` | 官方 catalog | ✓ | ✗ | — | — |  |
| 692 | `GG_CP_TERMINATE_APP` | 官方 catalog | ✓ | ✗ | — | — |  |
| 693 | `GL_TCPCONNSUCC` | 官方 catalog | ✓ | ✗ | — | — |  |
| 694 | `GL_ACCOUNTCONNSUCC` | 官方 catalog | ✓ | ✗ | — | — | dispatch 外專屬 handler `CLobbyLogin::sub_43E500`；u16<0x2580 才替換壓縮門檻 |
| 695 | `GS_BUY_ONCEITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 696 | `GS_BUY_ONCEITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 697 | `GG_CHEATER_REPORT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 698 | `GP_ENTER_PEPACHI_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 699 | `GP_ENTER_PEPACHI_ACK` | 官方 catalog | ✗ | ✗ | — | — | UDP dispatcher 之外：consumer＝`CLobbyShop::sub_46AD00` case 699（行級：`s8/bool status, s32, s32`;status 1→`sub_469CF0`） |

#### 700–799

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 700 | `GP_START_GAME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 701 | `GP_START_GAME_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 702 | `GP_PEPACHI_LIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 703 | `GP_PEPACHI_LIST_ACK` | 官方 catalog | ✗ | ✗ | — | — | UDP dispatcher 之外：consumer＝`CLobbyShop::sub_46AD00` case 703（行級：`s32 countA, s32 countB, (a+b)×raw4 entry`） |
| 704 | `GL_LEVEL_KILL_LIMIT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 705 | `GL_LEVEL_KILL_LIMIT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 706 | `GL_BILLTOKEN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 707 | `GL_BILLTOKEN_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 708 | `GL_CHECKCASHPG_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 709 | `GL_CHECKCASHPG_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 710 | `GL_RESERVECHANGENICK_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 711 | `GL_RESERVECHANGENICK_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 712 | `GR_NOSKILL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 713 | `GR_NOSKILL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 714 | `GG_INVALIDWPDATA_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 715 | `GG_INVALIDWPDATA_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 716 | `GG_CHANGEWPQUICKSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 717 | `GG_CHANGEWPQUICKSLOT_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 718 | `GR_START_VOTING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 719 | `GR_START_VOTING_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 720 | `GR_START_VOTING` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 721 | `GR_DO_VOTING` | 官方 catalog | ✗ | ✓ | — | — |  |
| 722 | `GR_VOTING_RESULT` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 723 | `GR_END_RESULT` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 724 | `GL_COMBISKILLITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 725 | `GL_COMBISKILLITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 726 | `GG_OBSERVERCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 727 | `GG_OBSERVERCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 728 | `GR_OBSERVERCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 729 | `GR_OBSERVERCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 730 | `GG_GETPULP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 731 | `GG_GETPULP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 732 | `GG_DROPPULP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 733 | `GG_SPAWNPULP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 734 | `GG_SPAWNPULP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 735 | `GG_RESPAWNPULP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 736 | `GG_PULPSTEAL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 737 | `GG_DESTROY_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 738 | `GG_DESTROY_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 739 | `GG_DESTROY_SUCC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 740 | `GG_DESTROY_SUCC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 741 | `GG_DESTROY_FAIL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 742 | `GG_DESTROY_FAIL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 743 | `GG_TIMEOVER_CHANGE` | 官方 catalog | ✓ | ✗ | — | — |  |
| 744 | `GG_MAGIC_GAUGE_CHANGE` | 官方 catalog | ✓ | ✗ | — | — |  |
| 745 | `GG_DEFENSE_REWARD_NOTICE` | 官方 catalog | ✓ | ✗ | — | — |  |
| 746 | `GG_PNR_RESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 747 | `GG_PNR_RESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 748 | `GR_SELECTRANDOMMAP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 749 | `GG_GIMMICK_DAMAGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 750 | `GG_GIMMICK_DAMAGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 751 | `GG_GIMMICK_RESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 752 | `GG_MAPINFO_RELOAD_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 753 | `GG_GIMMICKINFO` | 官方 catalog | ✓ | ✗ | — | — |  |
| 754 | `GG_PNR_OB_MODE_END` | 官方 catalog | ✓ | ✗ | — | — |  |
| 755 | `GG_SYNC_TIME` | 官方 catalog | ✓ | ✗ | — | — |  |
| 756 | `GL_CLAN_TNMT_RECEIPT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 757 | `GL_CLAN_TNMT_RECEIPT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 758 | `GL_CLAN_TNMT_RECEIPT_CANCEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 759 | `GL_CLAN_TNMT_RECEIPT_CANCEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 760 | `GC_CLAN_TNMT_RECEIPT_OK_NOTICE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 761 | `GC_CLAN_TNMT_RECEIPT_CANCEL_NOTICE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 762 | `GL_CLAN_TNMT_CURRENT_STATE_NOTICE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 763 | `GL_CLAN_TNMT_CURRENT_STATE_NOTICE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 764 | `GL_CLAN_TNMT_ENTERROOM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 765 | `GL_CLAN_TNMT_ENTERROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 766 | `GL_CLAN_TNMT_ROUND_END_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 767 | `GL_CLAN_TNTM_AWARD_INFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 768 | `GL_CLAN_TNTM_AWARD_INFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 769 | `GR_CLAN_TNMT_ROUND_START_COUNTER` | 官方 catalog | ✓ | ✗ | — | — |  |
| 770 | `GL_CLAN_TNMT_CHANGE_STATE_START_COUNTER` | 官方 catalog | ✓ | ✗ | — | — |  |
| 771 | `GL_CLAN_TNMT_ALL_INFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 772 | `GL_CLAN_TNMT_ALL_INFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 773 | `MASTER_RELOAD_TNMT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 774 | `MASTER_RELOAD_TNMT_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 775 | `GL_CLAN_TNMT_BROADCAST_TNMT_STATE_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 776 | `GL_CLAN_TNMT_CLANREC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 777 | `GL_CLAN_TNMT_CLANREC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 778 | `GR_CLAN_TNMT_PREVENT_ENTER_ROOM_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 779 | `GL_CLAN_TNMT_CHANGE_CLAN_INFO_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 780 | `GS_GET_PRESENTPACKAGE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 781 | `GS_GET_PRESENTPACKAGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 782 | `GL_RECEIVE_NEW_MSG` | 官方 catalog | ✓ | ✗ | — | — |  |
| 783 | `GL_NEW_MSG_COUNT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 784 | `GL_NEW_MSG_COUNT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 785 | `GL_FRIEND_ADD_PROCESS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 786 | `GL_FRIEND_ADD_PROCESS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 787 | `GL_RACKINGWEB_TOKEN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 788 | `GL_RACKINGWEB_TOKEN_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 789 | `MASTER_TEST_COMMAND_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 790 | `MASTER_TEST_COMMAND_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 791 | `GL_VOICEITEMSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 792 | `GL_VOICEITEMSLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 793 | `GI_VOICEITEMSLOT_ALL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 794 | `GI_VOICEITEMSLOT_ALL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 795 | `GI_CHANGE_VOICEITEMSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 796 | `GI_CHANGE_VOICEITEMSLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 797 | `SECURITY_AHNLAB_RESPONSE_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 798 | `SECURITY_AHNLAB_RESPONSE_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 799 | `GT_CRITICAL_ERROR_REPORT` | 官方 catalog | ✗ | ✓ | — | — |  |

#### 800–899

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 800 | `MASTER_XTRAP_RELOAD` | 官方 catalog | ✗ | ✓ | — | — |  |
| 801 | `GT_WEAPON_ERROR_REPORT` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 802 | `GS_DESTROYITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 803 | `GS_DESTROYITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 804 | `MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 805 | `MASTER_RELOAD_HIDDEN_ITEM_LIST_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 806 | `GS_HIDDEN_ITEM_LIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 807 | `GS_HIDDEN_ITEM_LIST_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 808 | `GS_GET_RECOMMENDSET_INFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 809 | `GS_GET_RECOMMENDSET_INFO_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 810 | `GS_HIDDENMAP_LIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 811 | `GR_PROBABILITY_APPLY_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 812 | `MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 813 | `MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 814 | `MASTER_CHECK_BOMB_CHEATER_APPLY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 815 | `MASTER_CHECK_BOMB_CHEATER_APPLY_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 816 | `GL_ADDICTION_PREVENT_ALARM` | 官方 catalog | ✓ | ✗ | — | — |  |
| 817 | `GC_NPGAMEGUARD_QUERY_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 818 | `GC_NPGAMEGUARD_QUERY_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 819 | `MASTER_CHECK_NPGAMEGUARD_QUERY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 820 | `GG_CHATTING_PENALTY_REPORT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 821 | `GG_CHATTING_PENALTY_REPORT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 822 | `MASTER_CHAT_BAN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 823 | `MASTER_CHAT_BAN_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 824 | `MASTER_USERLIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 825 | `MASTER_LOBBY_USERLIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 826 | `MASTER_ROOM_USERLIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 828 | `MASTER_RESETCOINTIME_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 830 | `MASTER_CHAT_FORCE_BAN_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 831 | `MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 832 | `MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 833 | `GG_NETWORKERROR_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 834 | `GL_DATA_RECV_COMPLETED_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 835 | `GL_DATA_RECV_COMPLETED_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 836 | `GL_SHOUTCHAT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 837 | `GL_SHOUTCHAT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 838 | `GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 839 | `GR_CLAN_JOIN_RECOMMAND_REQUEST_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 840 | `GR_CLAN_JOIN_RECOMMAND_REQUESTED_REQ` | 官方 catalog | ✓ | ✗ | — | — |  |
| 841 | `MASTER_SETALL_EVENTEXP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 842 | `MASTER_SETALL_EVENTEXP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 843 | `MASTER_SETALL_EVENTPAGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 844 | `MASTER_SETALL_EVENTPAGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 845 | `MASTER_VIEWALL_EVENTSTATE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 846 | `MASTER_VIEWALL_EVENTSTATE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 847 | `GR_NETCAFEWEAPONINFO_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 848 | `GR_NETCAFEWEAPON_DISABLE_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 849 | `MASTER_TNMT_VIEW_STATE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 850 | `MASTER_TNMT_VIEW_STATE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 851 | `MASTER_RELOAD_GAMECENTER_RANKING_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 852 | `MASTER_RELOAD_GAMECENTER_RANKING_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 853 | `MASTER_PRINTGCRANK_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 855 | `GL_MYWAREHOUSEINFO_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 856 | `GL_MYWAREHOUSEINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 857 | `GL_MYWAREHOUSEITEMLIST_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 858 | `GL_MYWAREHOUSEITEMLIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 859 | `GL_PUSH_TO_WAREHOUSE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 860 | `GL_PUSH_TO_WAREHOUSE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 861 | `GL_POP_TO_WAREHOSUE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 862 | `GL_POP_TO_WAREHOSUE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 863 | `GL_CHANGED_WAREHOUSEINFO_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 864 | `GL_SERVER_DATETIME_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 865 | `GL_SERVER_DATETIME_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 866 | `GQ_QUEST_LIST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 867 | `GQ_QUEST_ACCEPT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 868 | `GQ_QUEST_ACCEPT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 869 | `GQ_QUEST_CANCEL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 870 | `GQ_QUEST_CANCEL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 871 | `GQ_QUEST_SUCCESS_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 872 | `GQ_QUEST_SUCCESS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 873 | `GQ_QUEST_COMPLETE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 874 | `GQ_QUEST_COMPLETE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 875 | `GQ_QUEST_CHANGEDSTATE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 876 | `GQ_QUEST_ACCEPT_DAILY_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 877 | `GQ_QUEST_ACCEPT_DAILY_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 878 | `GQ_QUEST_USER_COMPLETE_HONOR_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 879 | `GQ_QUEST_USER_COMPLETE_HONOR_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 880 | `GQ_QUEST_ACCEPT_DAILY_NOTIFY`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 881 | `GQ_QUEST_CURRENTITEMQUEST_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 882 | `GP_CHPLAYTIMEC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 883 | `MASTER_FIND_USER_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 884 | `MASTER_FIND_USER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 885 | `MASTER_PLAY_WITH_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 886 | `MASTER_PLAY_WITH_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 887 | `GX_XIGNCODE_DATA_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 888 | `GX_XIGNCODE_DATA_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 889 | `GX_XIGNCODE_DATA_BAN_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 890 | `GC_QUERY_CLANRANKING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 891 | `GC_QUERY_CLANRANKING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 892 | `MASTER_RELOAD_CLANRANKING_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 893 | `MASTER_RELOAD_CLANRANKING_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 894 | `GR_TEAMSHUFFLE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 895 | `GR_TEAMSHUFFLE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 896 | `MASTER_RSHUFFLEWT_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 898 | `MASTER_RSHUFFLEVT_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |

#### 900–999

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 900 | `GS_CAPSULEMACHINE_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 901 | `GS_CAPSULEMACHINE_START_ACK` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 902 | `GG_OCC_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 903 | `GG_OCC_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 904 | `GG_OCC_SUCC_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 905 | `GG_OCC_SUCC_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 906 | `GG_OCC_FAIL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 907 | `GG_OCC_FAIL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 908 | `GG_OCC_AB_SUCC_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 909 | `GG_OCC_RESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 910 | `GG_OCC_RESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 911 | `GL_SCHEDULED_GM_NOTICE_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 912 | `GL_WEAPONPARTS_EQUIP_CHANGE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 913 | `GL_WEAPONPARTS_EQUIP_CHANGE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 914 | `GG_MISSILE_INFO_NOTIFY`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 917 | `GR_AI_NEXTWAVE_NOTIFY` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 918 | `GR_AI_GET_REWARD_ITEM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 919 | `GR_AI_GET_REWARD_ITEM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 920 | `GR_AI_GET_REWARD_ITEM_RESULT_NOTIFY` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 921 | `GR_AI_APPEARED_BOT_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 922 | `GR_AI_DAMAGE_SHIELD_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 923 | `GR_AI_DAMAGE_SHIELD_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 924 | `GR_AI_RECHARGE_MAGAZINE_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 925 | `GR_AI_RECHARGE_MAGAZINE_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 926 | `GR_AI_RECHARGE_MAGAZINE_END_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 927 | `GR_AI_RECHARGE_MAGAZINE_END_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 928 | `GR_AI_CONTINUE_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 929 | `GR_AI_CONTINUE_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 930 | `GR_AI_CONTINUE_FAIL_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 931 | `GR_AI_CONTINUE_FAIL_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 932 | `MASTER_RELOAD_AIXML_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 933 | 〔未命名〕 | 未命名〔handler 不可回收〕 | ✓ | ✗ | — | — | handler 本體不可回收（lib thunk）；保留未命名 |
| 934 | `GR_AI_TEAMSCORE_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 935 | `GR_AI_FEVER_START_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 936 | `GR_AI_FEVER_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 937 | `GR_AI_FEVER_END_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 938 | `GR_AI_WAVE_END_NOTIFY` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 939 | `GR_AI_GO_NEXT_WAVE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 940 | `GR_AI_GO_NEXT_WAVE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 941 | `MASTER_GO_AI_MULTI_WAVE_DIRECTLY_REQ` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 942 | `GR_AI_REWARDITEM_SELECT_START_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 943 | `GR_AI_REWARDITEM_NEXTTURN_NOTIFY` | 官方 catalog | ✗ | ✗ | — | — | registry-only：官方有名錄，client build 無 native endpoint |
| 944 | `GR_RESET_GAMEROOMSLOT_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 945 | `GR_RESET_GAMEROOMSLOT_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 946 | `GR_AI_UDPSENDER_CHANGE_START_NOTIFY`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 947 | `GR_AI_UDPSENDER_CHANGE_END_NOTIFY`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 949 | `GR_AI_MULTI_SHIELD_NOTIFY`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 953 | `MASTER_PVE_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 954 | `MASTER_PVE_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 957 | `GR_TIMEOVER_ONGAME_RESPON_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 958 | `GR_TIMEOVER_ONGAME_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 959 | `GG_DROPWEAPON_CREATE_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 960 | `GG_DROPWEAPON_DESTROY_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 961 | `GG_DROPWEAPON_INFO_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 962 | `GG_DROPWEAPON_GET_AND_DROP_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 963 | `GG_DROPWEAPON_GET_AND_DROP_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 964 | `GG_GET_BALL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 965 | `GG_GET_BALL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 966 | `GG_RESPAWN_BALL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 967 | `GG_GET_GOAL_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 968 | `GG_GET_GOAL_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 969 | `GR_SOCCER_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 970 | `GR_SOCCER_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 971 | `GG_SOCCER_RESPON_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 972 | `GG_SOCCER_RESPON_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 973 | `MASTER_RESETSOCCERBALLRESPAWNTIME_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 974 | `GG_SOCCERBALL_HAVE_INCREASE_PG_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 975 | `MASTER_SETMULTIPLYDAMAGE_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 976 | `MASTER_SETMULTIPLYDAMAGE_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 983 | `GL_MATCHINGROOM_MAKE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 984 | `GL_MATCHINGROOM_MAKE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 985 | `GL_ENTERMATCHINGROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 986 | `GR_MATCHINGROOM_START_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 987 | `GR_MATCHINGSUCCESS_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 988 | `GL_MATCHINGROOM_CANCLE_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 989 | `GL_MATCHINGROOM_CANCLE_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 990 | `GR_DAMAGEROOM_REQ` | 官方 catalog | ✗ | ✓ | — | — |  |
| 991 | `GR_DAMAGEROOM_ACK` | 官方 catalog | ✓ | ✗ | — | — |  |
| 992 | `GQ_QUEST_LIST_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 994 | `GG_ASSISTPOINT_NOTIFY` | 官方 catalog | ✓ | ✗ | — | — |  |
| 995 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | ✓ | ✗ | — | — | 錢包/等級推播 → PACKETS.md §3.15r |
| 996 | `GL_BLOCK_ADD_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 997 | `GL_BLOCK_ADD_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 998 | `GL_BLOCK_DEL_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 999 | `GL_BLOCK_DEL_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |

#### 1000–1010

| op | 名稱 | 名稱狀態 | S2C | C2S | UDP出 | UDP入 | 備註 |
|---:|---|---|:-:|:-:|:-:|:-:|---|
| 1000 | `GL_BLOCK_LIST_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 1001 | `GL_BLOCK_LIST_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 1002 | `GL_BLOCKME_LIST_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 1003 | `GL_BLOCKME_LIST_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 1004 | `GL_RANDOMMAP_LIST_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 1005 | `GL_RANDOMMAP_LIST_ACK`〔推定〕 | 推定〔S2C 審計〕 | ✓ | ✗ | — | — |  |
| 1006 | `GG_OCC_ZONE_ENTER_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 1007 | 〔未命名〕 | 未命名〔handler 不可回收〕 | ✓ | ✗ | — | — | handler 為 16-byte 微 thunk，不可回收；禁止鄰接臆名 |
| 1008 | `GG_OCC_ZONE_LEAVE_REQ`〔推定〕 | 推定〔C2S 審計〕 | ✗ | ✓ | — | — |  |
| 1009 | 〔未命名〕 | 未命名〔handler 不可回收〕 | ✓ | ✗ | — | — | 同上（16-byte 微 thunk） |
| 1010 | 〔未命名〕 | 未命名〔UNRESOLVED〕 | ✓ | ✗ | — | — | 行為紀錄見 Part I 審計節（無讀者，UNRESOLVED） |

