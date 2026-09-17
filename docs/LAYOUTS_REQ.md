# C2S REQ builder primitive-write inventory（261 TCP opcode rows；321 direct ctor call sites）

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
> （native 讀寫皆為 4B signed value）、常數 `u8 1`、681 `ext_count`。
> 因此 143 是新 channel TCP connection 對最近 681 的 handoff claim，而不能
> 當作已驗證登入本身。141 是 private UDP op18 觸發的 empty endpoint-confirmation
> request（`sub_556530`）；195 三個 byte 依序為 681 的 group、group-local
> channel index、local-option-derived raw flag（domain UNRESOLVED；`sub_56FF40`）。


## 推定命名審計（2026-09-17）

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
  可鎖定）— 保留命令 token。
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


## 主表：C2S REQ builder primitive-write rows

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
> hole-punch 狀態機、19↔20 註冊握手、8/24 移動套用鏈與存活度標注）。**現有 native evidence 證明的
> `n → n+1` case pairs、AES/raw send lane、secondary sockaddr 與 `UNRESOLVED`
> server boundary 詳見 [`PACKETS.md`](PACKETS.md) §2.5；這裡保留 constructor-level
> inventory，避免把 UDP evidence 從本文件的完整 native builder audit 中遺漏。

| op | direct ctor xref（19 sites） | opcode 之後的寫入序列 | native send / caller / 邊界 |
|---:|---|---|---|
| 1 | `sub_593830`（1 site；shared function 的 `n2!=2` branch） | `u8×3 s32` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；僅 client 端送出事實 |
| 5 | `sub_593AB0` | `u8 raw4` | 由 `sub_595E80` case 4 呼叫；經 `sub_595980` 以明確 `raw16` destination 送出；符合之已存 member address 一律重送三次；`raw4` 為 `sub_592AA0` 之 caller-defined elapsed/context 值；server 角色 UNRESOLVED |
| 6 | `sub_593E60` | `u8 raw4` | 由 `sub_595E80` case 5 呼叫；經 `sub_595980` 以明確 `raw16` destination 送出；第一個符合的 received source 會被儲存並重送三次；`raw4` 為 `sub_592AA0` 之 caller-defined elapsed/context 值；server 角色 UNRESOLVED |
| 9 | `sub_594300` | `u8×3 s32` | 由 `sub_5942B0` 呼叫；`sub_595A10` secondary AES lane；週期性 client 路徑；不推斷 server 行為 |
| 13 | `sub_594460`; `sub_5946C0`（2 sites） | 各為 `u8 raw4` | 由 `sub_595E80` cases 10/12 呼叫；經 `sub_595980` 以已存位址送出；兩個 constructor 維持獨立 native call site；`raw4` 為 `sub_592AA0` 之 caller-defined 輸出 |
| 14 | `sub_594A10` | `u8 raw4` | 由 `sub_595E80` case 13 呼叫；經 `sub_595980` 以已存位址送出；source/本地 state 分支與 13 不同；`raw4` 為 `sub_592AA0` 之 caller-defined 輸出；domain UNRESOLVED |
| 15 | `sub_593830`（1 site；shared function 的 `n2==2` branch） | `u8×2` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；與 1 同屬一個 native function，但 wire form 不相同 |
| 17 | `sub_596180`; `sub_596240`（2 sites） | `sub_596180`：空；`sub_596240`：`str`（ANSI/NUL） | 兩個 global builder 均未回收具名 direct caller；兩者皆走 primary raw `sub_595900`，繞過 AES；空形式與字串形式必須維持分開 |
| 19 | `sub_596670` | `u8×2 s8 u8 s32 str` | direct callers：`sub_4070B0`、`sub_407290`、`CLobbyGameStart::sub_43C380`；`sub_595A10` secondary AES lane；offset 2 使用 `sub_5928E0` s8；offset 3 使用 `sub_592920` u8，source 為 `-2` 時送出 `0xFE`；nickname 字串為 native writer 輸出 |
| 21 | `sub_596330` | `u8×3 s32 raw4 raw4` | `sub_595D80` active-manager caller；經 `sub_595A10`；尾端兩個 `sub_592AA0` 值維持 caller-defined raw4 |
| 23 | `sub_744450` | `u8×3 s32 raw4 u8 u16×3 u8 u8×8 s32` | direct callers：`sub_600770`、`sub_73E170`；經 `sub_602D70 → sub_596B90 → sub_595A10` gate；`sub_592AA0` n0x64 為 caller-defined raw4；寬度為 Fact，欄位涵義 UNRESOLVED |
| 27 | `sub_6013E0`; `sub_6036F0`（2 sites） | 各為 `u8×3 s32 u8×2` | direct callers：`sub_73E170`→`sub_6013E0`；`sub_5607C0`／`sub_6013E0`→`sub_6036F0`；`sub_602D70 → sub_596B90 → sub_595A10` gate；尾端兩個 u8 值為 native state byte |
| 30 | `sub_606340`（空 constructor；此 site 無 send）；`sub_6065E0` | `sub_606340`：空；`sub_6065E0`：`u8×3 s32 u16 count, count×{s8 status,[u16 若 status!=0,[條件式 u16 s8 u16 f32×3 s32]]}` | callers：`sub_73E170`→`sub_606340`；`sub_606340`（2 sites）／`sub_6065D1`→`sub_6065E0`；僅 `sub_6065E0` 呼叫 `sub_595A10`；`sub_761500(...)` 為本地暫存，非 wire 欄位 |
| 32 | `sub_96BF70` | `u8×3 s32 u8 s8 u16×3` | `sub_967E90` caller；經 `sub_595A10` 之 object/position 路徑；三個 `u16` 值無已證明之 domain 名稱 |
| 35 | `sub_7463E0` | `u8×3 s32` | `sub_749B90`（2 次 direct call）；`sub_67F380` gate 之後走 `sub_595A10` secondary AES lane；為純送端證據，不推斷接收端或遊戲涵義 |

> **UDP framing 邊界（Fact / HIGH）**：除非該 row 另有說明，ops
> 1/5/6/9/13/14/15/19/21/23/27/30/32/35 使用已觀察到的 AES send lane；op 17
> 使用獨立的 raw primary lane。`sub_595940` 雖存在為另一個 secondary
> raw-send wrapper，但本 dump 未回收其任何 direct caller，故不構成 op 17 的
> 額外變體。`n+1` 的 inbound case 是 reader 端證據，不能證明 server 接受，
> 也不能證明存在相同的反方向 layout。除另行文件化的 19→20 client/server
> 交換外，server 行為維持 `UNRESOLVED`；本 Appendix 不授權實作任何 UDP
> server 行為。


## Appendix B — `sub_595E80` UDP-private dispatcher / every verified receive case

> **Dispatcher boundary（Fact / HIGH）**：UDP receive 的唯一已定位主要 dispatcher 是
> `sub_595E80(CUDPNetworkManager, packet)`。呼叫鏈是
> `sub_595A60 → sub_596F90(recvfrom, 9600) → sub_591FB0` 填入 Packet buffer，
> `sub_591D50` framing check，`sub_5930C0` AES decrypt，最後才進
> `sub_595E80`。dispatcher 額外要求 manager `+52 != 0`、`this_10 != nullptr`
> 且 `sub_67EAC0()==0`；否則不讀 body、不回覆。switch 使用
> `sub_591EE0(packet)` 的 UDP Packet opcode；`default` 靜默忽略。這個 opcode
> 空間和 `sub_58B010` 的 TCP/catalog dispatcher 分離，即使數字或官方 catalog
> 名稱相同，也不能直接合併 layout 或方向。
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
| 2 | `sub_593A60` | 未讀 body | 若 global `n0x3E8==0`，設為 1，將 `timeGetTime()-dword_F2563C` 存入共享 elapsed 值，並設 `byte_1324330=2`；後續收到僅遞增該 global counter。不做 payload-empty 驗證。 | private unnamed；無 server 端證明前不得稱其為泛用 `PING` |
| 4 | `sub_593AB0` | `u8 entryCount`；重複 `u8 memberKey + raw16 addressBlob` | 於 16 項 `dword_F6DCF4` 表逐一查 key；未知 key 立即 return，先前已修改之 entry 保留。已知 entry 將 raw16 存至 `unk_F6D584+240780*i`、設 `byte_F6D5B0[i]=1`；再構成 op 5（`u8 + raw4 elapsed`），對每個已存非本地 address 送出三個 AES datagram。本地 state byte 設為 4。 | private unnamed；address 分發／peer 角色維持 UNRESOLVED |
| 5 | `sub_593E60` | `u8 memberKey + raw4` | raw4 被讀入 4-byte 本地變數，但在已回收的狀態轉移中未被使用。已知 key：重複收到僅遞增 `byte_F6D5A4[i]`；首次收到設 `byte_F6D5A4/A5`，將當前 `recvfrom` source sockaddr 存入 `unk_F6D594+240780*i`，構成 op 6（`u8 + raw4 elapsed`）並對該 source 送三次。 | private unnamed；未證明是 `HOLE` 或 `PING` 欄位 |
| 6 | `sub_5940E0` | `u8 memberKey + raw4` | raw4 被讀但未消費。若已知 key 的 `byte_F6D5A5[i]==0`，設 `byte_F6D5A4/A5` 並存當前 source sockaddr；不構成回應。 | private unnamed |
| 8 / 24 | `sub_596940 → sub_593750`，僅當 `n15==13`；否則走 bug/report 路徑 | dispatcher wrapper 不讀任何欄位；`sub_593750` 在 critical section 內 enqueue／複製 Packet | 字面 error 路徑指名消費者為 `OnY_UDP_S_MOVE_INF`；後續 parse 由 queue/callee 路徑擁有，非本 dispatcher wrapper。若 `n15!=13`，log `BUGCUDPNetworkManager::OnY_UDP_S_MOVE_INF` 並呼叫本地 debug/report helper。 | shared handler 標籤 `Y_UDP_S_MOVE_INF` 為 native 字串證據；8 與 24 各自的精確 op-to-name 對應未被證明 |
| 10 | `sub_594460` | `u8 memberKey + raw16 addressBlob` | 找 key（decompiler 迴圈呈現等待至符合）；`byte_F6D5B0[i]==0` 時存 raw16 address 並設 flag。構成 op 13（`u8 + raw4 elapsed`），對已存 address 送三次；本地 state `this+1=2`。 | private unnamed |
| 12 | `sub_5946C0` | `u8 entryCount`；重複 `u8 memberKey + raw16 addressBlob` | 存每一筆已知 entry；未知 key 直接 return、先前 entry 保留。以 `timeGetTime()-dword_F2563C` 設共享 elapsed 值，構成 op 13（`u8 + raw4 elapsed`），對每個已存非本地 key 送三次；本地 state `this+1=4`。 | private unnamed |
| 13 | `sub_594A10` | `u8 memberKey + raw4` | raw4 被讀但未使用。已知 key：重複收到遞增 `byte_F6D5A4[i]`；首次收到設 `byte_F6D5A4/A5`、存當前 source sockaddr，構成 op 14（`u8 + raw4 elapsed`）送三次；本地 state `this+1=4`。 | private unnamed |
| 14 | `sub_594CA0` | `u8 memberKey + raw4` | raw4 未使用。若已知 key `byte_F6D5A5[i]==0`，設 `byte_F6D5A4/A5`、存當前 source sockaddr，state `this+1=4`；無回應。 | private unnamed |
| 15 | `sub_593DF0` | `raw16` | 一次性 latch `byte_F25646`：首個 packet 將 raw16 複製至 `unk_F25648`；後續忽略。無回應。與 outbound op 15（`u8,u8`）為不同形式。 | private unnamed；不得合併方向 |
| 18 | `sub_596300` | 未讀 body | 一次性 latch `n0x3E8_1`；首個 packet 呼叫 `sub_556530`，於 TCP socket 構成並送出 catalog/TCP opcode `141 PM_CONNECT_REQ`；後續 packet 無作用。未回收任何 UDP body 消費者。 | private UDP trigger 維持 source-oriented 描述；內層 TCP opcode 141 有官方 `PM_CONNECT_REQ` 證據 |
| 20 | `sub_5968C0` | 未讀 body | 設 `byte_1D0CFE7=1`，清 manager retry/state word `+44,+8,+4`，更新 `+24=timeGetTime()`，並經 `sub_594F00` 清 `byte_1324331`。不讀任何 identity 或 gameplay 欄位。 | 為 outbound op 19 之 private completion；官方名未回收 |
| 22 | `sub_5964E0` | `u8 updateFlag`；若 `==1`：`u8 count`，重複 `u8 memberKey + raw4 value` | 更新 timer/network-manager 本地 state，再僅對已知 key 將 `raw4 value` 寫入 `dword_F6D9E8[i]`。無回應。flag 非 1 時讀完第一個 byte 即停止。 | private unnamed |
| 26 | `unknown_libname_107` | 無法從匯出之 C body 回收 | dispatcher 呼叫已驗證，但 callee 函式體／名稱不在本 dump；不得臆造任何欄位順序或效果。 | 明確 UNRESOLVED |
| 28 | `sub_594E80 → sub_74D130 → sub_9FA000` | gated reader：`u8×3, u8, u8, raw2, u8 count`；選定規則下 `count×{u8 index, raw4 value, raw2 state}` | 僅在已回收之 gameplay/object gate 成立時進入。合法 record 可經 `sub_9FA860` 更新內部 object flag/timestamp 並插入 queue；本地 gate 失敗可能不讀任何 body。無回應。 | shared consumer 無名；不得沿用 outbound op 27 之名 |
| 29 | `sub_593E20` | 未讀 body | 經 `sub_555030(&dword_1321D00)` 關閉 TCP socket，載入 resource `0xA8`，呼叫本地 notice `sub_9A7DE0(...,37,1)`。無 packet 衍生欄位。 | private 本地 notice 觸發；unnamed |
| 31 | `sub_594EA0 → sub_606AD0` | gated header `u8×3, raw4 gateValue, s16 recordCount`；每筆 record 以 `u8 active` 起頭，尾段依 object 而異 | 合法 object 分支消費 object/member key、status、raw2 state、`f32×3` position 類值與 raw4；fallback 分支消費不同尾段但不使用其值。僅更新 client object state；無回應。`recordCount` 為 native `s16`，非已證明之 unsigned count。 | shared consumer unnamed |
| 33 | `sub_594EC0 → sub_96C1E0`，當 global `n2_24!=0` | gated `u8,u8,u8,raw4,u8,u8,s16×3` | 本地 gate 通過時，將三個有號 16-bit 值除以 3 寫入 object `+60/+64/+68`；寫相鄰 state byte 並清 `+84`。若 `n2_24==0`，wrapper 不讀任何內容。無回應。 | shared consumer unnamed |
| 34 | `sub_594F20` | 恰三筆 record `{u8×4, raw2, u8}`，再接兩個 `raw2` 值 | 對每筆 record 呼叫 `sub_778BC0`；其消費者使用 record 之部分 byte，但不使用 record raw2。查表成功時末尾兩個 raw2 寫入 current-member offset `+156/+160`。無回應。 | shared consumer unnamed |
| 154 | `sub_5965D0` | `u8 count`；重複 `u8 memberKey + u8 value` | 將已知 key 之值寫入 `dword_F6D9E8[i]`；未知 key 忽略；無回應。 | 與官方 `UDP_ALL_PING_ACK` 在數值／catalog 上重疊；行為與該標籤相容，但 private dispatcher 仍是權威 xref |
| 158 | `sub_596910` | 未讀 body | 載入 resource `0x127` 並呼叫 `sub_9A7DE0(...,65,1)`；無 packet 衍生 state 或回應。 | 與官方 `UDP_TCP_DEAD_ACK` 在數值／catalog 上重疊；native private handler 仍為 `sub_596910` |

> **命名欄的證據等級讀法**：direct `case → callee`、native reader helper
> 身份、固定寬度與 client state/send 呼叫是 **HIGH native fact**。
> `private unnamed` 表示此 native fact 很強、但其 protocol 用途／名稱仍為
> **UNRESOLVED**。`Y_UDP_S_MOVE_INF` 對 shared consumer 字串是 **HIGH** 證據，
> 但不含 8 與 24 各自的歸屬判定。`UDP_ALL_PING_ACK`、`UDP_TCP_DEAD_ACK` 是
> **HIGH catalog-name/value fact** 加 private case xref，但 dump 未證明
> private handler 是 catalog 協議的別名。`unknown_libname_107` 是 **HIGH 的
> 未解決邊界 fact**：呼叫存在但 callee 函式體缺席，所以不升格任何
> layout／名稱。
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
