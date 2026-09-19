# server-ts 欄位證據與待辦（原 SERVER_TS_PACKET_FIELDS.md＋TODO_HANDLERS.md）

> **合併說明（2026-09-17）**：本檔由 `SERVER_TS_PACKET_FIELDS.md`（31 個
> TS packet 的逐欄 native meaning／TS use／boundary 審計）與
> `TODO_HANDLERS.md`（未實作 handler 清單、下一步證據順序）合併——兩者
> 同屬「server-ts 目前的實作證據現況」，審計（既成）在前、待辦（future
> work queue）在後，內容逐字保留。歷史 provenance 記錄保留在
> `PACKETS.md`／`RESOURCES.md`／本文 Part 一節，不重複寄存。

---

## Part I — 現行 Packet 欄位審計（原 SERVER_TS_PACKET_FIELDS.md，31 個 module）

---

日期：2026-09-17（Asia/Taipei）

本文件只描述目前 `server-ts/src/ops/c2s` 與 `server-ts/src/ops/s2c` 實際存在的
31 個 Packet。欄位順序以 TS reader/builder 為準，再用 `PaperMan.exe.c`、既有
`docs/PACKETS.md`、`docs/LAYOUTS*.md`、`docs/WIKI_MECHANICS.md`（PaperMan
Wiki 觀察只作歷史/語意交叉線索，不單獨決定 wire 或 server policy）、官方資源
檔與 client consumer 交叉核對。它把三件事分開：

* **wire meaning**：native consumer 已直接讀取、比較、顯示或由 resource/offset
  交叉證實的用途。
* **TS use**：目前 private server 是否使用該值、只保留/echo，或刻意輸出空
  projection。
* **UNRESOLVED**：只有欄位存在或被原樣帶過，沒有足夠證據命名其業務語意。
  這些欄位不因 implementation 變數名、猜測或 UI 位置而改名。

`raw`、`unknown`、`flag`、`extra`、`uid`、`user_no`、`requestContextRaw`、
`ext_count` 等保守名稱是刻意保留的 wire boundary，不代表漏做翻譯。沒有 data
model 的地方，TS 只輸出已確認可被 client 完整消費的空 projection；這不宣稱它
等同歷史服務的成功資料。

## 五分法結論 ledger

下表把後面的逐欄說明再拆成五個互不替代的欄位：**native fact** 只寫
`PaperMan.exe.c` 直接讀寫、branch、caller/callee 或 consumer；**resource fact**
只寫 `Extracted/` 能直接對上的檔案/ID/UI；**inference** 是由前兩者推出但
不是 bytes 本身；**TS projection** 是本 repo 實際送出的保守資料；
**unresolved** 是刻意沒有命名或沒有移植成 server policy 的部分。沒有 resource
證據時明寫「無」，不把歷史 Wiki 或 TS 變數名補進 fact。

| opcode | native fact | resource fact | inference | TS projection | unresolved |
|---|---|---|---|---|---|
| 101 `GT_PING_REQ` | 空 request；client heartbeat path 建立它。 | 無。 | proof-of-life response。 | 只記到達，不回 102。 | interval、timeout、server heartbeat policy。 |
| 105 `GL_USERLIST_REQ` | `sub_56A0F0` accepts unsigned `a2`; only `a2==1` passes the one-second `timeGetTime` gate and writes one byte, while `a2==0` only changes the caller's local transition path. The native wire output is therefore exactly `u8=1`; its status/filter meaning is not proven. | 無。 | refresh trigger byte, not a proven status/filter. | consumes exactly one byte, rejects values other than native `1`, returns 106 zero gate. | rate policy。 |
| 107 `GL_GAMEROOMINFO_REQ` | 空 request；對應 108 reader。 | 無直接 room payload 對應。 | lobby room refresh。 | 嚴格空讀，回 `mode=0,count=0`。 | room model、mode-3 branch 與 policy。 |
| 143 `PM_UDPSTART_REQ` | `str`、`s32`、literal `u8=1`、`s32`；由 681 handoff values 建立。 | 無。 | 是 channel admission claim。 | 以兩個 echoed raw value、source IP claim；identity 只記錄。 | identity、n100、ext tuple 的業務語意。 |
| 195 `GC_ENTERCHANNEL_REQ` | 三個 `u8`；第三 byte 是 native `0`/`1` boolean，來自 local option block。 | 無。 | 前兩 byte 對應 681 的 group/channel 選擇。 | 驗認證與 group/channel；rawFlag 僅驗證 `0`/`1` wire domain，不作放行條件；type 3 只有在完整 raw continuation 已配置時才允許 server success。 | rawFlag business semantics、type-3 server policy。 |
| 197 `GL_MYINFO_REQ` | 空 request，dispatcher 進 198。 | character/NewSkill 資源只供 response projection。 | self MyInfo request。 | 從 authenticated Store 輸出 198 snapshot。 | 未登入時官方 error policy。 |
| 199 `GL_MYITEM_REQ` | `sub_570A00` constructs opcode 199 with no payload; observed callers are the lobby data state machine after its `INFORMATION` step and the scene state machine after `MYINFO` completes. The client displays resource string `0x66` (`載入中`) before sending. | `itemdata.pat` is the catalog consulted only by the 200 reader. | inventory refresh/page request; no native 199 start-index field. | empty success page at start 0 + negative sentinel。 | inventory ownership、catalog/grant policy。 |
| 246 `GL_CLIENTINFO_REQ` | 一個 NUL ANSI string；`sub_573DE0` 直接以 `sub_5926F0` 寫出；`sub_51EFB0` 的 UI-side temporary 是 `CHAR[132]`，只 `memset` 128 bytes 後以 `strlen` 作 `strncpy`，沒有 native 20-byte wire clamp。 | 無。 | lookup key 是 nickname string，而非 numeric uid。 | Store nickname lookup，回單一 247 character；server reader 不添加未證實的 20-byte request limit。 | name uniqueness/authorization policy；UI input limit、encoding/stack safety。 |
| 250 `GL_LOBBYIN_REQ` | 空；目前 recovered path 沒有 251 consumer。 | 無。 | client-local lobby transition。 | 嚴格空讀，不回 packet。 | 其他 build 的 server transition policy。 |
| 252 `GL_SHOPIN_REQ` | 空；client 先切 shop state。 | shop XML 只證 UI layout，非 success payload。 | shop entry notice。 | 回空 253 compatibility ACK。 | 253 官方成功語意。 |
| 254 `GL_INVENIN_REQ` | `sub_5741C0` writes one `u8`; an observed caller derives it from a selected client/server entry and uses `-46` when the 255 entry is absent. The value is not directly named by a native consumer. | NewSkill XML controls corroborate scene/puzzle slots, not context. | inventory/NewSkill entry context is only a bounded UI description. | consumes one byte and echoes it structurally in mode-1 255. | exact context/header join, remote mode, and server policy。 |
| 435 `GL_FRIEND_INFO_REQ` | `str csvNickList` | `sub_55B0A0`: 21B-stride friend table 逗號相連成 String[1028];空表時 client 不送 435。TS 解析單一 `str`(≤1023B、拒絕 trailing)、split(',' 每段非空 ≤20B、rows≤100),回 436 `{u8 count, rows×{str key, u8 0}}`。 | `sub_55B0A0`、`sub_55B2C0`、friend-table stride (Phase 6)；HIGH for wire;UNRESOLVED server-side friend DB |
| 436 `GL_FRIEND_INFO_ACK` | `u8 count, count×{str key, u8 statusRaw, [==1: str where(20B), u8 raw]}` | 每列→`sub_5382D0(key,statusRaw,where,raw+1)`,UI 處函式收 (matched,count,1)。唯一證明之區分: statusRaw==1 才帶 extra context,不造 online/channel 更高語義。 | `sub_55B2C0`、`sub_5382D0`；HIGH for shape, UNRESOLVED semantics beyond the ==1 split |
| 437 `GG_ROOMBROADCAST_REQ` | `u8 flag, s32 len, raw[len]` | `sub_55B430` 經函式指標/訊息表送出;dispatcher 無 437/438 case ⇒ flag/blob 語意 UNRESOLVED,client 不解析 438。TS 解析驗線後刻意不回覆(無房間轉播目標 + 無人消費 438)。 | `sub_55B430` builder、dispatcher case 逐列檢視；HIGH for wire, UNRESOLVED semantics |
| 441 `GL_FRIEND_WHERE_REQ` | `str nick` | `sub_55B940`: builder 無長度閘(friend UI 提供)。TS 以非空+≤20B 朋友表 stride 作防禦上限,拒絕 trailing → 恆回 status 0(0x21D 通用失敗臂,最簡合法)。 | `sub_55B940`、`sub_55B9F0` ACK 臂；HIGH for wire, UNRESOLVED full status enum |
| 442 `GL_FRIEND_WHERE_ACK` | `u8 statusRaw, [==1: u8 where_type, u8 channel, u8 room_no]` | status 1: 11→0x314 教學,9/10→同房或切頻頻進行房/sub_570030,其他→0x21E 大廳;2→0x21D+0x3AF;其他→0x21D;非-1 不讀後續。 | `sub_55B9F0`、`sub_570030`-channel-cross；HIGH |
| 439 `GL_FRIEND_CHAT_REQ` | `s32 rawUidContext, str selfNick, str friendNick, str message` | `sub_55B510` gate `strlen(message) <= 180`;rawUidContext=`dword_F2A684` 與 834 同源 UNRESOLVED → parse-only。TS 無 friend/whisper 系統 → 恆回 status 3(0x1D8 通用「找不到」臂)並回送 nick 對;0/1/2 皆承載不能證實之主張。 | `sub_55B510`、`sub_55B660`(status 臂+24B/24B/200B 讀入槽)、doc §3.15pre-2；HIGH for wire/gates, UNRESOLVED context domain |
| 440 `GL_FRIEND_CHAT_ACK` | `u8 statusRaw, str nick1, str nick2, [==2: str comment]` | status 0→`0x1EF`、1→`0x1F0`、2→`0x1D9`(讀 comment)、3→`0x1D8`;wire nick 僅推進游標(顯示靠全域 `sub_401B20`)→ 結構證明「==2 帶 comment」一分。 | `sub_55B660`、`sub_401B20`/`sub_401A60`；HIGH for shape, UNRESOLVED display internals |
| 429 `GL_FRIEND_ADD_REQ` | `str key` | `sub_55A860`: 非空 `strlen <= 23` + 本地 self(`sub_537740`)/duplicate(`sub_538200`) 送件閘。TS 無 friend table ⇒ 永不回 success 臂;self nickname → status 1,其他 → status 5(原生 else 臂 `0x1EC`)。 | `sub_55A860`、`sub_55AA90` ACK consumer、字串表 `0x1E5..0x1EC`；HIGH for wire gate/arm, UNRESOLVED status enum beyond proven branches |
| 430 `GL_FRIEND_ADD_ACK` | `u8 statusRaw, str key` | status 0 插入 100 行 friend table(`sub_537F60`)+ `0x1E7`;1..4 → `0x1E8..0x1EB`;else → `0x1EC`;任一分支末端皆送空 433 (`sub_55AF20`)。 | `sub_55AA90`、`sub_537F60`、`sub_55AF20`；HIGH |
| 431 `GL_FRIEND_DEL_REQ` | `str key` | `sub_55AD00`: 非空 `strlen <= 23`。TS 恆回 status 2(原生 `0x1EE` failure 臂)。 | `sub_55AD00`、`sub_55AE10` ACK consumer；HIGH for wire gate, UNRESOLVED full status enum |
| 432 `GL_FRIEND_DEL_ACK` | `u8 statusRaw, str key` | status 0 刪(`sub_538010`)+ 433 refresh;status 1 → `0x1ED`,2 → `0x1EE`,皆不動表。 | `sub_55AE10`、`sub_538010`；HIGH |
| 419 `GL_MSG_ADD_REQ` | `raw4 contextRaw, str ownNick, str toNick, str body, str title, u16 iconRaw, u8 soundRaw`（2026-09-19 行級訂正：lead 欄是 `sub_592AA0(dword_F2A684)` 的 4B context——即 834 同源的 shared raw context,舊列 `u8 raw0` 誤） | builder `sub_559550`:body 1..200B / toNick 1..24B / ownNick=登入 nick 截 24;420 reader `sub_559810` 只讀 str+u8+u8,switch xRaw ∈ {0,1,2,3,4,5,10},default→通用 error 資源。TS 無信箱 ⇒ 恆回 default 臂 xRaw=6,resultRaw=0,不回存收件者謊。 | `sub_559550`/`sub_559810`（+accessor `sub_5929A0`=2B write)；HIGH for wire/gates, UNRESOLVED x/result 全 enum |
| 420 `GL_MSG_ADD_ACK` | `str toNick, u8 xRaw, u8 resultRaw` | 對位 420 reader:default 臂 0x1E2;TS 僅 emit default 臂以避任何虛構郵件狀態。 | `sub_559810`；HIGH for shape, UNRESOLVED enum |
| 421 `GL_MSG_DEL_REQ` | `str key` | Native builder `sub_55A1E0` 以兩閘作為送件前提: key 非空且 `strlen <= 20`,且 `sub_537DE0` 證明 key 存在於本地信箱。TS 解一個非空 `str key`(cap=原生 char[20] key 槽 19B)、拒絕 trailing bytes;空信箱伺服器回 `statusRaw=0`(原生失敗臂)並回送 key。 | `sub_55A1E0`、`sub_55A310` ACK consumer；HIGH for wire gate/shape, UNRESOLVED status enum beyond zero/nonzero |
| 422 `GL_MSG_DEL_ACK` | `s8/bool statusRaw, str key` | `sub_55A310` 首讀 `sub_592900`(s8/bool)後接 `sub_592730`: status 非零→`sub_537A80` 移除 key+刪除路徑 UI;零→彈 resource `0x1E3`。builder emit `{s8 statusRaw, strMax(key,19)}`;status enum 不造。 | `sub_55A310`、`sub_537A80`；HIGH |
| 423 `GL_MSG_READ_REQ` | `str key` | `sub_55A3C0` 鏡像 421 兩閘,但 `sub_537E90(...)==0`(未讀才送)。TS 對位與 421 相同。 | `sub_55A3C0`、`sub_55A4F0` ACK consumer；HIGH for wire gate/shape, UNRESOLVED status enum beyond zero/nonzero |
| 424 `GL_MSG_READ_ACK` | `s8/bool statusRaw, str key` | `sub_55A4F0` 首讀 `sub_592900`(s8/bool)後接 `sub_592730`: status 非零→`sub_537D20` 寫 `89` 標記;零→彈 resource `0x1E4`。builder emit `{s8 statusRaw, strMax(key,19)}`;status enum 不造。 | `sub_55A4F0`、`sub_537D20`；HIGH |
| 783 `GL_NEW_MSG_COUNT_REQ` | (空) | builder `sub_5643E0` ✓。784 `sub_564480`:單 `s32`→`dword_F0C104`,`!=0` 切 UI 新信件指標。TS 信箱恆空 ⇒ 回 `s32(0)`。 | `sub_5643E0`/`sub_564480`；HIGH |
| --- **2026-09-19 S2C 零值總審計**(60 件全體,新增 5 件註記) --- | | | |
| 141↔142 `PM_CONNECT` 握手落地 | 141 空體;142=`str host(≤19B), s32 port(low u16), u8 active_channel_index, u32 packed_calendar` | builder `sub_556530` 空體實證、consumer `sub_5565D0` 行級(142 進 `sub_596E60` 次 UDP 地址族)、`sub_534F20` 解碼 mask 對拍;TS 回 config.channel.endpoint/index + process-local wall clock(wire 無時區,zone 明確文件化) | 全值有 live 來源,零亂填;HIGH |
| 218 `GI_CHANGEDATA_REQ` 持久化 | `u8 selected_slot, u8 count(≤20), count×{u8 slot,u8 type,12×u16}` | builder `sub_572FC0` dirty-only;第二欄實證 char_type;store `applyCharacterData` 全有或全無事務;`sub_4BCF00` 0/1 二臂 ⇒ 回 1/0 | 解決「假成功」風險;HIGH |
| 312 `GI_CHANGESLOT_REQ` 更名 | `u8 slot_no` | `sub_884160` 傳 CClientData+88 ⇒ **角色選擇**非背包分頁;persist `current_character`(未擁有槽靜默丟棄,consumer 不讀) | 舊註解「inventory tab」錯名纠正;HIGH |
| 685/689 tutorial | 686=`s32 marker`回讀 store;689 原樣存入 player.tutorial_index | `sub_4422B0` 哨兵 145=隱藏 TUTO_NEW;689 無 ACK 維持沉默 | 689 擲棄⇒重覆讀回 0 的缺口修復;HIGH |
| 936 `GR_AI_FEVER_START_ACK` 三零 | flag=0→`sub_67D7D0` [16,76) 無效 ⇒ notify 跳;duration=0→mismatch 只 log;type=0→status 0 臂不讀 | 皆非亂填字;HIGH |
| 705 `GL_LEVEL_KILL_LIMIT_ACK` 全零 | `n11_0==0` ⇒ `sub_415F90` switch 全跳,rate/cap 是 kind 6..12 專屬死格 | "無防沉迷限制面板"正解;HIGH |
| 686 `GL_TUTORIALINDEX_ACK`=0 | `sub_4422B0` `*n145==145` ⇒ 145=隱 TUTO_NEW 徽、他值顯示;0=新鮮帳態(誠實);哨兵已具名 `TUTORIAL_COMPLETED` | HIGH |
| 792 `GL_VOICEITEMSLOT_ACK` 全零 86B | `sub_876B00`:u16 id 非零才差表替換;0=`vtbl+8(0)`=原生語音;flag 0=未自訂 | 86B shape 與原生逐 byte 一致;HIGH |
| 371/132/467/311/879/454 拒絕臂 | 各 consumer 行級:status 0(≠1)→零續讀或無操作;467 前二頭 B 從未分支;879 err≠0 即返回 | wire 全對齊無 desync 之虞;HIGH |
| 106 `GL_USERLIST_ACK` u16(0) | `sub_56A250` 首位 **u16 閘**,0=閘死零續讀 | 舊註解獲二進位證實;HIGH |
| 255 `GL_INVENIN_ACK` u8(0) | `sub_574270`:v11 在 mode==1 僅 parked;profile/uid 全真值 | HIGH |
| 944 `GR_RESET_GAMEROOMSLOT_REQ` | 空(0B) | builder `sub_585E90` @175969 零寫入 ✓。945 真 consumer `sub_435E40`(585F30 跳線):**count 文法**≠0→清註冊群+逐槽重讀、==0→單 B 終止;舊表 status(1) 誤 ⇒ 恆 `count=0`(wire `00`)。 | consumer 追跳線至底;HIGH |
| 939 `GR_AI_GO_NEXT_WAVE_REQ` | 空(0B) | builder `sub_75CE40` @391020 零寫入 ✓。940 `sub_7613D0` 收 5B 後**無閘**翻波次+播音+定時(唯一無 denial 臂)⇒ parse-and-silence(721 前例),940 永不發、不註冊 s2c。 | 全體行級;HIGH |
| 935 `GR_AI_FEVER_START_REQ` | 空(0B) | builder `sub_7622C0` 零寫入 ✓。936 `sub_7623A0` 固定 7B:status≠0&&duration==`dword_EE8CB4` 才啟動(否則錯誤日誌);==0=被拒臂(state=2+UI 廣播)⇒ 恆 7B 全零(wire `00000000000000`)。 | 全體行級;HIGH |
| 928 `GR_AI_CONTINUE_START_REQ` | `s32`(恆0) | builder `sub_761DB0` 字面 0(`sub_592A20`=4B ✓)。929 `sub_761E90`:唯 ==1 走復活體(u8,s32,str,s32,s32),他值終止 ⇒ 恆 `status=0`(wire `00`);舊表 continue_count 列誤。 | 全體行級+accessor 驗寬;HIGH |
| 924 `GR_AI_RECHARGE_MAGAZINE_START_REQ` | `u8 slot,u8 kind`(2B) | builder `sub_558350` @153683 二寫,**舊表三欄錯置**。925 `sub_558550` 三臂(0→+u8+s32/1→+u8+u16/他→denial 零讀)⇒ 恆 `status=2`(wire `010002`)。 | 四函數全體行級 + `sub_592900/592940/592A00/592A40` 驗寬;HIGH |
| 926 `GR_AI_RECHARGE_MAGAZINE_END_REQ` | `u8,u8,s8`(3B) | builder 三 1B accessor ✓。927 `sub_558880` 頭 4B,status≠0 零收讀 ⇒ 恆 `status=1`(wire `01000001`)。 | 同上;HIGH |
| 922 `GR_AI_DAMAGE_SHIELD_REQ` | `3×s16+s32`(10B) | builder `sub_761580` @393240;**`sub_592B20`=4B 配 `SLOBYTE`⇒ 只送 float 低位元組**(accessor 本體驗寬)。923 `sub_761710` 鏡像 10B 零臂+ obj 查表/remain∈{4,7} 才動 ⇒ TS 逐 byte 恆回聲(wire `0500FDFF070040000000` @ 5,-3,7,0x40)。 | builder/consumer 行級+accessor 驗寬;HIGH |
| 918 `GR_AI_GET_REWARD_ITEM_REQ` | `u8 reward_idx`(1B) | builder `sub_761A70` @393375 無臂 1B。919 `sub_761B20` 雙巨臂:status==0→戰利品鏈(含 `sub_67DF00` slot 型別檢);!=0→`u8 code`,**僅 0xFF 終止**。TS 無結算模型 ⇒ 恆 `idx,1,0xFF`(wire `0201FF`)。 | builder/consumer 行級;HIGH |
| 912 `GL_WEAPONPARTS_EQUIP_CHANGE_REQ` | `u8 raw0,2×s32[,raw0==2:+s32]` | builder `sub_95AEF0` @619208:三臂(0/1→9B、2→13B),`sub_592AA0`=4B 驗證。913 `sub_95B180`:errorRaw==0→續讀 912 身+末 s32 native 表 bounds-check;≠0 只耗 1B ⇒ TS **不可 echo back** ⇒ 恆 `errorRaw=1`(wire `01`),非零語義 UNRESOLVED 保持。 | builder/consumer 雙體行級、accessor 驗證;HIGH |
| 718 `GR_START_VOTING_REQ` | `3×s32`(12B) | builder `sub_A191D0` @701039(state==3 gate;房 socket)。719 consumer = 共用 `sub_9BF430` case 719:`u8 status` vtable+12 無臂 ⇒ TS 恆 `status=0`。720=`4×s32+u8`(sub_592AC0=4B)、722=`s32+s8`、723=`s8+s32`(補記)皆 push,TS 不發。 | builder/consumer/dispatcher 三體行級;HIGH |
| 721 `GR_DO_VOTING` | `u8 vote` | builder `sub_A192B0` @701061:`sub_5928E0`(1B 驗證)。無 active session ⇒ 解析後沉默不發 722(同 689 政策)。 | 本體行級;HIGH |
| 485 `GL_GET_GAMEROOM_PROGRESSTIME_REQ` | `u8 room_no` | builder `sub_56AD60` @163685(1B 驗證)。486 `sub_56AE30`:n3==2→4B;n3∈{0,1,4}→9/11B 行+門檻尾;其他 n3→規格化 3 跳渲染 ⇒ TS 恆 `n3=3`(wire 1B)。 | 本體臂型行級;HIGH;PACKETS 列改臂型記法 |
| 483 `GG_GAMECENTER_GAME_START_OK_REQ` | `s16 game_id` | builder `sub_584EC0` @175469(2B 驗證)。484 `sub_584F70`:無臂 9B 恆讀(status 讀而不耗)⇒ TS echo+status=1+0(wire 9B)。 | 本體行級;HIGH |
| 480 `GG_GAMECENTER_RANKING_REQ` | `s16 game_id, u8 mode` | builder `sub_585320` @175599(state gate;3B 驗證)。481 `sub_585080`:雙清單 top3(≤3)/top10(≤0xA)各 0x38B/行,均收在判定內 ⇒ 舊列補正;TS 恆 echo id+雙 0(wire 11B)。 | 本體行級;HIGH |
| 478 `GG_GAMECENTER_GAME_PLAY_CHECK_REQ` | `raw36` | builder `sub_564A40` @160362(0x24 驗證);全檔無 479 consumer(無 case/ctor)⇒ TS 解析後刻意沉默(同 689)。 | builder 本體 + 全檔 consumer 掃描;HIGH |
| 476 `GG_GAMECENTER_GAME_END_REQ` | `s16 game_id, raw24, raw44` | builder `sub_564930` @160332(2B+0x18+0x2C=70B 驗證)。477 `sub_76E450`:無臂 137B 恆讀(u16,raw32,raw44,u16,s32,raw24,raw8,4×s32,s8,u8,u8,s8,s8)→ sub_8EE1D0/錢包 += 全零惰性 ⇒ TS 恆零結算。 | 兩體行級+寬度掃描;HIGH |
| 474 `GG_GAMECENTER_GAME_START_REQ` | `s16 game_id, u8 stage` | builder `sub_584DB0` @175429(2B+1B 驗證)。475 `sub_584E80` 零讀取(僅 UI 刷新)⇒ TS echo+status=1。 | 本體行級;HIGH |
| 472 `GL_GAMECENTER_REC_REQ` | `u16 game_id` | builder `sub_584850` @175275(2B 驗證;local flag 語義未定)。473 `sub_584910` 行級:top3/top10 各 0x38 B/行、v35=0x20/v23=0x2C 條件 raw、尾 `u8 v31,u16 v32Raw,u16 v21` ⇒ 舊列補正;TS 恆全零板+echo id(wire 38B)。 | 兩函數本體行級+全 accessor 寬度掃描;HIGH |
| 466 `GI_CHANGE_SKILLITEMSLOT_REQ` | `u8 raw0,u8 raw1[,u8 raw2,7×s32 bulk]` | builder `sub_5738A0` @167578 + blob `sub_527BA0`=0x1C 驗證;raw1 條件二分(2B/31B)。467 `sub_573A70`:3 header 恆讀;profile 行 32B 只在 dword_E650B0 存在才讀 ⇒ TS 恆 `count=0` 空盤(wire `000000`)。 | builder/bulk/consumer 三體行級;HIGH;domain 名稱 UNRESOLVED 保持 |
| 453 `GS_DELETEGIFT_REQ` | `s32 gift_uid, s32 item_id` | builder `sub_57BC40` @171264:2× 4B ✓。454 `sub_57BCF0`:`u8 status, 2×s32` 恆讀;==1 刪 cache(無 match ⇒ i_23-- 到 -1 = 污染),否則失敗橫幅 0x308 ⇒ TS 恆 `status=0`(安全/誠實),因 status=1 在無 gift 模型下有計數污染。 | 本體行級、i_1=-1 掃表路徑重讀;HIGH |
| 370 `GL_CHANGECHANNEL_REQ` | `u8 channel_id` | builder `sub_570030` @165825(僅於不同 channel 送出,1B)。371 `sub_570100`:status==1 讀 `{u8 ch, str ip, s32 port, u8 extra}`→`sub_596E60` 副端點;0/2/3=橫幅 0xDA/0x148/0x328;≥4 靜默重置。TS 單一 channel ⇒ 恆 `status=0`。 | 兩函數本體行級、`main.ts` 部署组態互證;HIGH |
| 312 `GI_CHANGESLOT_REQ` | `u8 slot_no` | builder `sub_573270` @167410(1B 驗證)。313 `sub_573320` 本體不讀 packet 任何欄位(只 UI 刷新)⇒ TS echo `u8 slot_no`。 | 本體行級;HIGH |
| 310 `GS_BUYCHAR_REQ` | `6×s32`(24B) | builder `sub_572790` @167049:6× `sub_592A20`(4B 驗證)。311 `sub_5728A0`:`u8 status`≠0→6×s32 快照;**恆讀尾部** `u8,s32,s32`(錢包 switch 只於 status≠0)⇒ TS 恆 `status=0`+三零(wire 10B),`sub_4694B0(...,0)` 收 dialog。 | 兩函數本體行級+accessor 寬度;HIGH;PACKETS 311 列補正 |
| 218 `GI_CHANGEDATA_REQ` | `u8 char_slot, u8 count, count×26B` | builder `sub_572FC0` @167338 + per-slot serializer `sub_5244E0`(13-word slot table 158..169 讀取);`sub_592920`=1B、`sub_5929E0`=2B 本體驗證;count 上限 0x14(builder 同樣中止)。219 `sub_573230`→`sub_4BCF00`:1=applied(0xC case UI 刷新),0=abort-sync,其他 no-op ⇒ TS 恆 `status=1`。 | 三函數本體行級+accessor 寬度;HIGH |
| 131 `GR_FORCEOUT_REQ` | `u8 target_slot` | builder `sub_56EC10` @165201:`sub_592920`=1B 寫入驗證。132 `sub_56ECC0`:body 全包 `if(status!=0)` 無 else ⇒ status=0 全沉默;≠0 才讀 slot(+room-view 成員對)。TS 無房間模型 ⇒ 恆 `status=0`。 | builder/consumer 本體行級 + accessor 1B 驗證;HIGH |
| 876 `GQ_QUEST_ACCEPT_DAILY_REQ` | (空) | builder `sub_91D730` @594466(ctor→send;send log 署名同名)。877 `sub_91D7E0`:`u8 err`;==0 清 3×13B 表→`s32 count`→`13×count`B。TS 恆 `err=0,count=0`(wire `00,00000000`)。 | builder/consumer 本體行級;HIGH |
| 878 `GQ_QUEST_USER_COMPLETE_HONOR_REQ` | `u8 flag` | builder `sub_91C9D0` @594069:`sub_5928E0(a2!=0)`=1B 本體驗證。879 `sub_91CAA0`:`u8 err`≠0 only log;==0 `str title`+定長 blob(+239104 長度全檔唯讀點)。TS 恆 `err=1`(wire `01`)惰性臂。 | builder/consumer/accessor 本體;HIGH |
| 787 `GL_RACKINGWEB_TOKEN_REQ` | (空) | builder `sub_581E40` @174077(ctor→send 無 writer)。788 三訂閱點:唯一 reader `sub_407360`:`u8 hasToken[, str token]`,token `strncpy(...,0x10)` 入 `byte_EDDE04`(≥16 字元丟棄);`sub_44BEA0`/`sub_489AD0` 不讀 payload(僅 UI)。TS 無 ranking-web ⇒ 恆 `hasToken=0`。 | 787 builder 本體、`sub_407360` 行級重讀、accessor `sub_592900`(1B)/`sub_592730`(NUL) 本體；HIGH,並修正 PACKETS「str token」舊列 |
| 706 `GL_BILLTOKEN_REQ` | (空) | builder 位於 `sub_460480` @49534(CHARGE 鈕、300ms debounce);ctor→send 之間無 writer ⇒ 空 wire。707 `sub_46AD00` case 707 @52373 僅讀 `sub_592730`(str)→this+521173,純 UI 無驗證臂 ⇒ TS 恆回空字串。 | ctor/send 原樣、`case 707` 臂 awk 掃描僅 1 個 reader、`sub_592730` 本體 NUL 語意；HIGH |
| 704 `GL_LEVEL_KILL_LIMIT_REQ` | (空) | builder `sub_582570`。705 `sub_55C9B0`:12B `{s32 killLimit, f32 expRate, s32 maxLevelLimit}`,killLimit==0=靜默臂。TS 無限制模型 ⇒ 恆回全零。 | `sub_582570`/`sub_55C9B0`、`sub_44B880` 靜默分支；HIGH |
| 685 `GL_TUTORIALINDEX_REQ` | (空) | builder `sub_55C6F0`;無進度模型 ⇒ 回 `s32 0` 白板。 | `sub_55C6F0`(builder=空 wire);HIGH |
| 689 `GL_TUTORIAL_INDEX_SET_REQ` | `s32 tutorialIndex` | builder `sub_55C7D0`(4B writer `sub_592A20`);dispatcher 無 `case 690` 消費(舊錨不存在)⇒ TS 解析後刻意沉默。 | `sub_55C7D0`、dispatcher case 掃描；HIGH |
| 791 `GL_VOICEITEMSLOT_REQ` | (空) | builder `sub_885590`;context `(+296/+292)` client-local 不送線。792 reader `CMyVoiceCustomize::sub_876B00`:`u8 page, u16, u16, 27×{u16 id, u8 flag}`;id≠0→`unk_EAFC40`。TS 全零表 page=0;slot 語義 UNRESOLVED(只證零非零)。 | `sub_885590`、`sub_876B00`、`sub_885D00`、accessor 補 `sub_592A00`=2B read；HIGH for wire, UNRESOLVED slot domain |
| 425 `GL_MSG_RECVLIST_REQ` | `s32`；`sub_44E480` returns the UI navigation value, `sub_6DC6C0` subtracts 10 with a floor of 1, `sub_6DC760` adds 10, and `sub_55A580` writes the value with the 4-byte writer. | 無 mailbox resource/schema。 | native proves a message-list navigation scalar with initial/step behavior; “page” is a reasonable UI description but total-count/server mailbox policy remains unproven. | 讀取並忽略，回 zero-count 426。 | exact cursor/page domain、total count、mailbox policy。 |
| 433 `GL_FRIEND_LIST_REQ` | 空 request，對應 434。 | 無 friend table/status resource。 | current-account friend-list request。 | zero-count 434 + configured account display string as a bounded compatibility projection；builder 已具全語法記錄投影 (nickname 20B/stateRaw s32/count≤100, 2026-09-18)。 | friend storage/status/policy。 |
| 682 `GL_LOGIN_REQ` | two ANSI strings、guard `u64`、source `u8`、raw24；builder source 0/1/2 的 raw shape 可追。 | `Extracted/datarevision.txt` supplies the revision input；不是 hardware identity。 | guard is a client-data-version gate；fingerprint is separate security material。 | validates guard, source/raw combinations, credentials；does not use revision as Store policy。 | token semantics, fingerprint persistence/auth policy。 |
| 834 `GL_DATA_RECV_COMPLETED_REQ` | one raw `s32` propagated from 144 global；then no fields。 | 無。 | data receive-completion notification。 | read-only raw boundary，回空 835。 | raw context domain、是否需 admission join。 |
| 102 `GT_PING_ACK` | 空 heartbeat packet；client builds 101 after receiving it。 | 無。 | server heartbeat。 | 可發空 102；不由 101 handler 回。 | scheduling policy。 |
| 106 `GL_USERLIST_ACK` | `sub_56A250` reads raw2 gate; nonzero reads `u8 flags`, `u8 recordCount`, then each `raw4 userKey`, nickname, raw4 exp, and for positive key another raw4 texture key plus texture string. `sub_588560` stores the key/name/exp in the waiter list; `sub_403360(exp)` derives the class index. | `Extracted/ui/ui.xml` has the `Class` sprite strip and `EMBLEM` custom-sprite table; this corroborates the two UI lookup destinations, not a server user schema. | first raw2 is a gate, not record count. `userKey` is directly used as a native client-profile/list key; `exp` is a level/class input by direct lookup, but server progression policy is not established. Native flags bit0 clears the common progress object and sets a mode-specific list-state byte; bit2 clears that byte. | writes gate 0, so no optional flags or records are emitted. | gate/flags domains, recordCount limit, identity authority, custom-texture ownership and exp policy。 |
| 108 `GL_GAMEROOMINFO_ACK` | `sub_568CE0` reads `u8 mode`; only `mode!=3` then reads ordinary `u8 count` and room records. `mode==3` immediately enters `sub_580A80`, whose independent grammar is `u8 n4,u8 i1,u8 flags`, reverse stage loop, pair records, tail, raw4 stored through native `int v57`. | `Extracted/ui/gameroom.xml` contains the actual room controls (`GAMEROOM_DAMAGEROOM`, `GAMEROOM_NORMAL_NOSKILL`, `GAMEROOM_CLAN_NOSKILL`, map/mode selectors); `RESOURCES.md` separately ties those controls to native consumers such as `+128` and `+185`. `GAMEROOM_TEAMBALANCE` is a 364/365 UI branch, not proof that every tournament `+186` byte has that meaning. `Extracted/ui/TNMT_Awardproperty.xml` is a tournament award layout only: `award_1..3`, `nomarl_award`, `abnomarl_award`, emblem/present positions. Native UI consumers also reference `tournamentmatchlist.xml`, `tournamentPlayerInfo.xml`, and `tournamentFinalAward.xml`; those files are not in this Extracted snapshot. Room maps remain catalog-only (`maplist.pat`/`Map.dat`). | mode 0 is ordinary room-list; mode 3 is a separate tournament bracket/state projection, not a count variant. `sub_53F9F0` maps pair fields into `CRoomInfo`; `sub_875C20` and local-identity comparison consume the two participant blocks. | TS deliberately emits only `mode=0,count=0`; this is a safe ordinary projection, not mode-3 support. | tournament scheduling/pair semantics, participant block schema, stage raw words, selected-node tail semantics, server policy; native count fields have no visible bounds check in this reader. |
| 144 `PM_UDPSTART_ACK` | fixed prefix is fully read before result branch；name is fixed ANSI buffer；optional net-cafe block is gated。 | localized message resources corroborate displayed restriction text only。 | rank/PG/level/KDR fields feed UI messages。 | full fixed prefix, conservative reserved/raw values, optional gate 0。 | reserved s32, raw context, net-cafe records、admission policy。 |
| 196 `GC_ENTERCHANNEL_ACK` | failure is 3 fields; success appends endpoint/type; type 3 calls gated continuation。 | 無 direct type-3 catalog。 | endpoint is subsequent UDP control handoff。 | success/failure framing and complete type-3 raw tail are preserved；server 不輸出截斷的 false-success prefix。 | endpoint policy、opaque flags、type-3 record semantics。 |
| 681 `GL_LOGIN_ACK` | success prefix, raw2 server/group fields, positive-capacity one-channel body, type-3 extra, billing tail；failure only result。 | `Extracted/ui/system/netcafe_contents.xml` directly proves a three-entry NetCafe UI/config table (`number`, `grade`, `logo`, boost/discount/level fields), but does not map any 681 extension word/byte；仍無 resource authority for billing/flag/group。 | channel field is USERS current numerator, server port is selector endpoint；capacity/current-user raw2 are consumed as signed short by the native path；`sub_A1C870` stores the positive extension gate and the two raw s32 words without conversion, while the feature byte becomes a separate boolean。 | three fixed groups, s16/raw2 width checks, zero gates only for omitted groups, positive gate requires a complete channel body、ext gate 0。 | flag/group/billing/user_no domains、extension tuple/resource join、server policy。 |
| 247 `GL_CLIENTINFO_ACK` | failure one byte；success shares 198 basic block then one slot/type/12 appearance words。 | character/avatar resources corroborate category-relative appearance space。 | slot is serialized character-list index; type is separate char type。 | public lookup emits one selected serialized entry and 12 u16。 | persistent slot policy、appearance item authority。 |
| 253 `GL_SHOPIN_ACK` | no recovered native payload consumer。 | shop XML is UI only。 | no wire-level success schema established。 | empty compatibility ACK only。 | official 253 response semantics。 |
| 255 `GL_INVENIN_ACK` | common 4-field prefix；mode 0 remote branch；mode 1 selected `<5` then 5×32-byte raw profiles。 | `sub_527AF0`/`sub_4AC8F0` identify one native ordinal range `11,010,001..11,070,000` plus a catalog lookup for each nonzero ID; the decoded `itemdata.pat` contributes exactly 1,273 catalog members in that range. The seven UI families and accessory duplicate rule remain resource facts, not per-slot wire ranges。 | uid/profile scope is account-level, not character appearance；duplicate/ownership rule is resource/native-client evidence, not automatically server policy。 | mode 1 only; echo context; five 7-ID + expiry records；TS validates the common range and current `itemdata.pat` catalog membership, while zero remains the empty slot。 | remote fields, header/context, catalog completeness for future resource revisions, duplicate accessory enforcement, expiry/ownership/grant policy。 |
| 426 `GL_MSG_RECVLIST_ACK` | count is `u8`; client stores at most 10 records; one record raw field is 4 bytes and the other is 2 bytes, but the recovered table assignments retain their low bytes; the context string is read into native local `char[21]` (at most 20 ANSI bytes); fixed local record string slots。 | 無 message schema。 | message-list projection, not proven page model。 | raw2/context string/count 0；TS uses an empty context when the compatibility nickname exceeds 20 bytes；builder 另具全語法記錄投影 (key 19B/name 20B/body 200B/selector 1B/count≤10, 2026-09-18)。 | header, record raw fields/domains、mailbox policy。 |
| 198 `GL_MYINFO_ACK` | success basic block, max-20 characters, max-4 weapon groups, 9 UI ids, NewSkill block, tail；all widths follow native helpers。 | `itemdata.pat`, avatar resources, NewSkill resources corroborate separated domains/ID spaces；the current catalog adapter preserves native nonzero-ID membership checks。 | selected NewSkill is not normal appearance；reserved stats remain unnamed。 | Store facts + zero/empty conservative projection；six-word normal appearance repair only fills zero fields for a zero/canonical body；noncanonical nonzero bodies and final six appearance slots remain raw。 | reserved words/flags/raw48/n5、inventory/loadout grant policy、future resource-revision catalog migration。 |
| 200 `GL_MYITEM_ACK` | `sub_570AB0` reads one success byte; if nonzero, `sub_524B70(...,1)` reads start plus up to 100 records in the 5120-slot array, then all paths call `sub_41BF20(byte_BF0724)` (local state 8→9) and set `byte_EE8C05=1`. Record grammar is `s32 slot, s32 item_id, raw4, raw4, s32, u8, u16`; negative slot stops, negative/catalog-missing item id enters native error handling, and `sub_534450` updates the client item catalog's durability words. | `Extracted/ui/cfg/itemdata.pat` is native catalog membership source。 | period is duration-like only from consumer context; 200 completion also advances a local inventory state flag, not a server success policy. | success 1/start 0, optional record API, `-1` terminator。 | f1/f2/extra domains、ownership/service/time policy、native error-code projection。 |
| 434 `GL_FRIEND_LIST_ACK` | raw2/context string/count；the context string is read into native local `char[21]` (at most 20 ANSI bytes); consumer keeps at most 100 record-string/row-field rows, 21-byte record-string slot, wire raw4 row field with local low-byte retention。 | 無。 | context string is read but has no recovered immediate consumer。 | header 0/context string/count 0；TS uses an empty context when the compatibility nickname exceeds 20 bytes。 | header/row-field values、friend policy。 |
| 693 `GL_TCPCONNSUCC` | empty greeting triggers 143 builder on channel connection。 | 無。 | channel-side handshake start。 | sends once as connection trigger。 | server connection sequencing policy。 |
| 694 `GL_ACCOUNTCONNSUCC` | exactly one u16 threshold; `<0x2580` enables compression, otherwise disabled；then 682 builder。 | datarevision resource is consumed by subsequent 682, not by this u16。 | threshold is compression negotiation boundary。 | exact u16, default `0x2580`。 | deployment compression policy。 |
| 835 `GL_DATA_RECV_COMPLETED_ACK` | empty completion response in recovered mapping。 | 無。 | completion ACK。 | empty packet。 | absent alternate consumer/policy。 |

## Native traversal closure（caller → reader → consumer → next state）

這一節不是把變數名再抄一次，而是記錄我在 `PaperMan.exe.c` 重新走過的
最短可重現 data-flow closure。`dispatcher` 代表 opcode table 進入點；
`builder` 代表 client 送出的 caller；`reader/consumer` 代表讀完 wire 後
仍會使用該欄位的下一個 callee。只找到 reader、找不到業務 consumer 的地方
明列為 unresolved，而不是把 reader local 變數名當用途。

| packet pair / opcode | caller / entry | reader / immediate callee | consumer、state branch、adjacent handshake | resource cross-check / remaining boundary |
|---|---|---|---|---|
| 101 ↔ 102 | server 102；client heartbeat dispatcher | client 收 102 後 `sub_58D6F0` builds empty 101；server 101 reader is empty | 101 只更新到達/liveness；不回 102，否則形成 loop | 無；interval/timeout unresolved |
| 105 → 106 | `sub_56A0F0` emits 105 with one unsigned byte only for `a2==1` and a one-second native rate gate; callers include the WAIT_REFRESH paths, `sub_4FCDB0(1)`, `sub_4FCDF0(0)`, and `CUIWaiterList::sub_6FE180(a2)`. The zero path does not send 105. | `sub_56A250` reads raw2 gate, then optional `flags/count/records`; bit0 clears the common progress object and sets a mode-specific list-state byte, bit2 clears that byte. Each positive-key row enters `sub_588560`, which derives a Class index from exp and inserts the key/name/exp into the waiter-list collection. | first gate is not record count. The first record raw4 is used as a native client-profile/list key; the exp word is consumed by `sub_403360` for the displayed Class index. TS uses zero gate, so optional branch is not entered. | `Extracted/ui/ui.xml` directly contains the `Class` sprite strip and `EMBLEM` custom-sprite table; these corroborate UI destinations only. Gate/status/list authority, exact flag policy, record limit, identity, texture ownership, and exp policy remain unresolved. |
| 107 → 108 | lobby refresh caller sends empty 107 | `sub_568CE0` reads `mode`; `mode==3` diverts before reading ordinary `count`; otherwise reads `u8 count` and ordinary rows. `sub_580A80` reads its own `u8 n4,u8 i1,u8 flags` header. | ordinary mode 0 + count 0 terminates before room-record consumer；mode 3 stores state `[16],[17],[18],[19],[142],[494]`, builds `CRoomInfo` nodes via `sub_53F9F0`, sends two participant blocks through `sub_875C20`, then selects `sub_47E1B0` or `sub_47E3E0` for tournament UI. | `Extracted/ui/TNMT_Awardproperty.xml` proves only award layout; native UI names `tournamentmatchlist.xml`, `tournamentPlayerInfo.xml`, `tournamentFinalAward.xml` are direct filename references, not available resource contents. Stage/pair semantics remain unresolved. |
| 143 ↔ 144 | `sub_555C60` builds 143 after channel TCP greeting；144 is server result | `sub_555D50` reads entire fixed prefix before checking result；完整欄位/consumer 另見 [`S2C_NATIVE_AUDITS.md`](S2C_NATIVE_AUDITS.md)（144 part） | endpoint/admission consumer stores raw4 context for later 834/195 flow；result UI uses rank/PG/level/KDR branches | `msgtableres.lang` only corroborates displayed text；raw string/reserved/context/net-cafe policy unresolved |
| 195 → 196 | `sub_56FF40` builds 195 after 144; group/channel are selected from 681 list and `rawFlag` is the boolean result of the local option path | `sub_4179D0` reads 196 prefix, then success endpoint; type 3 enters `sub_875680` | endpoint passed into UDP setup (`sub_58ED30`/`sub_595C90`); follow-up 195 is not gated by 144 result in native, so TS admission must gate it | no resource proves rawFlag business semantics or type-3 business semantics; native optional type3 gate is preserved, TS server config requires complete tail before emitting success |
| 197 → 198 | `sub_570550` dispatcher requests self data | `sub_523BF0` basic block → `sub_524010` characters → `sub_524660` loadouts → `sub_527550` UI IDs → `sub_527D00` NewSkill/tail | CClientData is populated; selected char and profile then drive lobby UI/state | avatar assets and NewSkill XML separate appearance/puzzle domains; reserved/basic tail unresolved |
| 199 → 200 | `sub_570A00` emits empty 199 from the lobby `+1905` and scene `+748` state machines; it shows resource string `0x66` before the send | `sub_570AB0` reads success; nonzero enters `sub_524B70(...,1)`; inventory slots are scanned and `sub_535020` validates catalog lookup; completion calls `sub_41BF20` and sets `byte_EE8C05=1` | 5120-slot local array, max-100 page, negative slot terminator; `sub_534450` receives each item/durability pair and updates native client-side durability state | `itemdata.pat` proves shipped-client membership only; no server ownership/grant authority |
| 246 → 247 | `sub_573DE0` builds opcode 246 with one selected nickname string; callers `sub_423C20`, `sub_51EFB0`, and the lobby/friend UI paths avoid the local self-profile path and use this remote lookup | `sub_573EB0` reads the result byte, shares `sub_523BF0` with 198, then `sub_524360` consumes one character appearance tail | public profile branch drives the state-specific `sub_520ED0`/profile UI path and serializes one list index/type/12 appearance words, not private 198 tail | avatar resource categories corroborate 12 relative slots; request-name length/authorization policy unresolved |
| 250 → (251) | client local lobby transition sends empty 250 | no recovered 251 reader/consumer in this snapshot | state transition is local; TS rejects trailing bytes and intentionally does not reply | no resource payload; alternate build/server policy unresolved |
| 252 → 253 | client shop scene sends empty 252 | no recovered non-empty 253 consumer | TS empty ACK is compatibility projection only; shop UI does not prove server success grammar | `ui/shop.xml` is layout/presentation, not account/shop policy |
| 254 → 255 | `sub_5741C0(a1)` writes exactly one request byte; its observed caller derives `a1` from the selected client/server entry and uses `-46` when no 255 entry is present, so the request byte is not proven to be a tab or account field. | `sub_574270` reads `u8 mode, raw4 uid, u8 n16, u8 v11`; mode 0 then reads `u8,u8,raw4` for the remote branch, while mode 1 reads `u8 selectedProfile` and, only when `<5` and `uid==dword_EE8CB4`, 160 raw bytes. `sub_4AAB80` copies five 32-byte profile records and drives the NewSkill UI. | mode 1 is the self snapshot; the first common u8 is a structural context candidate because TS echoes 254, but the native dump does not directly join the two packets. The seven s32 slots/profile and selected `<5` gate are direct; remote lookup and expiry/ownership policy remain separate. | NewSkill XML/level/color tables corroborate seven slots/five profiles; 255 TS emits validated mode 1 only; unresolved: context/header join, mode-0 remote fields, catalog completeness, duplicate accessory rule, expiry/ownership/grant authority |
| 425 → 426 | `sub_55A580` writes a 4-byte value from `sub_44E480`; `sub_6DC6C0`/`sub_6DC760` adjust the UI value by −/+10 (the previous path floors at 1) | `sub_55A630` reads raw2/context-string/count and records; `sub_5378C0` stores at most 10 | 425 is natively a message-list navigation scalar; 426 UI consumes records and the two raw fields, but no server mailbox/page policy is recovered | no Extracted mailbox schema; fixed local capacities are client facts |
| 433 → 434 | empty friend-list request through dispatcher | `sub_55AFC0` reads raw2/context-string/count; `sub_537F60` stores up to 100 rows and assigns wire raw4 row field into a one-byte local slot | the context string is read separately but has no recovered immediate consumer; the row field is 4 bytes on wire but recovered local storage visibly keeps only low 8 bits | no friend resource; header/row-field domain unresolved |
| 681 ↔ 682 ↔ 694 | `sub_43E651` 694 branch reads threshold then invokes 682 builder; 682 response enters same login dispatcher | 681 success reader enters server-list loop; `sub_58AD90` consumes selected host/port; `sub_58E690` stores local server projection | 681 server list supplies next channel TCP target; n100/ext fields flow to 143; billing tail enters `sub_7092C0`; failure stops after result s32 | `datarevision.txt` only feeds 682 guard; no resource establishes flag/group/billing semantics |
| 693 → 143 | channel TCP accept sends empty 693 | `sub_57CAE0` displays greeting and invokes `sub_555C60` | opens channel handoff; 143 claim is later matched by source/echo values | no resource; identity writer/authority unresolved |
| 834 → 835 | `sub_583120` builds 834 with shared `dword_F2A684` | 834 reader consumes exactly one raw s32; 835 is empty | completion branch follows lobby data receive; raw context is not joined to uid by native evidence | same raw4 can be propagated from 144; no resource/business name |

## 108 mode-3 native/resource recheck（2026-09-17）

這次把既有 `docs/PACKETS.md` 的 mode-3 grammar 回溯到 `PaperMan.exe.c`，
不再把 `mode=3` 放在 ordinary `count` 的後面解讀。`sub_568CE0` 的實際
分支是：先讀 `mode`；若為 3，立即呼叫 `sub_580A80`；只有其他 mode 才讀
ordinary `count`。因此 `mode=3` 的第二個 byte 是 `n4`，不是 room-list
count。

**writer-side boundary:** 在這份 client `PaperMan.exe.c` 中找不到建立 literal opcode 108 的
client writer；相鄰的 `sub_568C50` 是 opcode 107
request writer，而 108 只出現在 dispatcher/reader `sub_568CE0`。因此不能
用 client writer 反推 server 產生的 108 mode-3 欄位；writer-side 的可證資料
目前只有 TS 的保守 `mode=0,count=0` projection 與上面的 native reader。

### 直接確認的 reader grammar

| wire segment | native read / direct consumer | boundary |
|---|---|---|
| header | `sub_592940` ×3 → `u8 n4`, `u8 i1`, `u8 flags`; flags 寫入 client state `[142]`, n4/i1 寫入 `[16]/[17]` | all three are one-byte fields; native reader visibly has no independent upper-bound check before indexing its local stage storage |
| stage | for `i=n4-1; i>=i1; --i`: `sub_592940` stage byte, `sub_592940` round byte, `sub_592940` mode byte, `sub_592A40` s32 stage word (domain raw), `sub_592A40` s32 stage word (domain raw), `sub_592940` pair count | reverse order is native fact; stage bytes/words are not business-named here. round byte is copied to state `[18]`; mode byte is passed to `sub_53F9F0`'s mode factory |
| pair base | `sub_592A40` s32 node/room source (domain raw); `sub_592940` ×4; `sub_592A00` raw2 word | the four values are passed separately to `sub_53F9F0`; the third byte is first assigned to `+129` but `sub_53FB10` later recomputes `+129` from the raw2 mask |
| round type 4 tail | `sub_592940` one byte, then `sub_592A40` s32 ×4 into two participant/emblem-related blocks | both blocks go to `sub_875C20`; their first dword participates in the local identity comparison. No decompiled body proves a uid, emblem, or score name |
| other round tail | two `sub_592A40` s32 blocks (domain raw) | both go to `sub_875C20`; the same local identity comparison applies |
| footer | `sub_592940` `hasMy`; when nonzero, `sub_592940` ×2; `sub_592AC0` reads a 4-byte word into native local `int v57` | first optional byte becomes the selected raw node value; second is read but not used by the visible continuation; the 4-byte value is stored at client state `[494]` (native local type is int, helper itself is a raw4 reader) |

The pair-to-`CRoomInfo` mapping is direct in `sub_53F9F0`: `a2` → offset `+4` (the helper signature stores it through a native `char`),
`a3` → `+105`, `a4` initially → `+129`, `a5` → `+110`, `a6` → `+130`,
`thisa` selects the lobby mode object, `a8=0` → `+136`, `a9` → `+144`,
`a10` sets the two mode-rule bits through `sub_74F430/sub_74F450`, `a11` →
`+185`, `a12` → `+186`, and `a13` → `+106`; it also sets `+107=1`,
`+108=0`, `+146=0`, and clears the mode-object `+12` field. This is a
native field-consumer fact, not a claim that every source byte has a tournament
business name. `sub_53FB10` expands the raw2 mask into per-slot flags and replaces
`+129` with its popcount.

The visible continuation then calls `sub_5832A0(n4,n5,selectedRaw)`,
`sub_875B80`, and `sub_480520`; state `[12]` selects either
`sub_47E3E0` (final-award UI path) or `sub_47E1B0` (which creates/updates
`tournamentmatchlist.xml` and `tournamentPlayerInfo.xml`). The final-award path
references `tournamentFinalAward.xml`. These filenames are native UI references;
they are not resource contents in the local `Extracted/` snapshot.

The one directly available tournament resource is
`Extracted/ui/TNMT_Awardproperty.xml`. Its root is `tournamentAwardTable`; it
contains `award_1..3`, `nomarl_award`, and `abnomarl_award`, with emblem/present
positions and sizes. It supports the existence of tournament award layouts only;
it does not define the wire pair blocks, scheduling policy, uid semantics, or
winner calculation. The previous Wiki/UI wording must therefore remain an
inference/background clue, not a replacement for the C reader.

**TS boundary:** current `GL_GAMEROOMINFO_ACK` intentionally remains
`mode=0,count=0`. Implementing mode 3 later requires a separate discriminated
projection and count/framing limits before any user-controlled `n4`, `i1`, stage,
or pair count is serialized; the native reader itself does not supply those
limits.

## 先固定 primitive boundary，再判斷語意

IDA export 的 Packet helper 本身只證明 byte width；不能只看 Hex-Rays 的 C
型別替欄位命名。`PaperMan.exe.c` 的共用 helper 可直接整理為：

| native helper | 實際動作 | TS 對照 | 限制 |
|---|---|---|---|
| `sub_592920`、`sub_592960`（寫）；`sub_592940`、`sub_592980`（讀） | u8，1 byte | `u8` | function identity 已固定 unsigned alias；caller domain 仍可 unresolved |
| `sub_5928E0`（寫）；`sub_592900`（讀） | s8，1 byte | `s8` | `char` prototype 不能覆蓋 native signed alias；bool-like source 仍保留 source evidence |
| `sub_5929A0`（寫）；`sub_592A00`（讀） | u16，2 bytes | `u16` | caller domain 仍須由 consumer 判斷 |
| `sub_5929E0`（寫）；`sub_5929C0`（讀） | s16，2 bytes | `s16` | caller domain 仍須由 consumer 判斷 |
| `sub_592A20`、`sub_592A40` | s32，4 bytes | `s32` | exact signed alias；業務名稱仍不可由 width 自動命名 |
| `sub_592A60`、`sub_592A80` | u32，4 bytes | `u32` | exact unsigned alias；業務名稱仍不可由 width 自動命名 |
| `sub_592AA0`、`sub_592AC0` | raw copy 4 bytes | `raw4`/caller-defined | generic 4-byte alias；不能直接投影成 s32/u32/f32 |
| `sub_592B20`、`sub_592B40` | IEEE-754 f32，4 bytes | `f32` | float semantics follow this helper identity |
| `sub_592AE0`、`sub_592B00`、`sub_592B60`、`sub_592B80` | u64，8 bytes | `u64` | 682 guard 由 low/high direct consumer 定義；其他 u64 不可重命名 |
| `sub_592500`、`sub_592580` | exact raw byte run | `raw`/`zeros` | 48B blob、fingerprint、160B profile block 不能按鄰近欄位猜語意 |
| `sub_5926F0`、`sub_592730` | 寫/讀 NUL-terminated ANSI bytes | `str` | native buffer capacity與encoding另由 caller證明；不存在 wire length prefix |
| `sub_592770`、`sub_5927B0` | 讀 NUL-terminated UTF-16 bytes | `wstr` | 本 31-opcode TS review 沒有把它誤套進 ANSI packet |


`sub_592500`（read）與 `sub_592580`（write）的函數體也確認了 boundary：read
超過 cursor/payload end 時回傳 0 且不 advance；write 超過 allocation end 時
不 copy。這些 helper return 值在 108/426/434 的連續欄位 reader 中沒有逐次
被檢查，所以不能把 helper 的 bounded memcpy 誤寫成 native 整包 fail-closed。
`sub_5926F0/sub_592730` 以 `lstrlenA()+1` 處理 ANSI NUL string；固定 buffer
容量與 malformed-NUL 行為必須由 caller 另行證明。

因此下面的 `s32`、`u32`、`f32` 是欄位 consumer/比較/資料流的結論，不是把
IDA helper 名稱直接翻成業務名。`sub_592A20` 與 `sub_592AA0` 都是四-byte
write，但 834 的 `dword_F2A684` 只能稱 shared raw context；同樣的四 bytes
在 198 stats、200 float、682 guard 中用途完全不同。

## C2S readers（15）

| opcode / TS | wire order and field | wire meaning / current TS use | evidence and confidence |
|---|---|---|---|
| 101 `GT_PING_REQ` | empty | Client 回應 server heartbeat；TS 只記 liveness，不回 ACK，避免 native 兩端互相 loop。 | `PaperMan.exe.c` `sub_58D6F0`；HIGH |
| 105 `GL_USERLIST_REQ` | `u8 refreshTrigger=1` | Native `sub_56A0F0` 的參數是 `unsigned __int8`，且只有值 `1` 才建立 105；這證明合法 native wire output 是 unsigned byte `1`，不證明它是 status/filter。TS 嚴格接受此一 byte，直接回 106 zero-count。 | `sub_56A0F0`、`sub_56A250`；HIGH（native value 1；語意仍 unresolved） |
| 107 `GL_GAMEROOMINFO_REQ` | empty | 空的 room-list refresh request。TS 嚴格拒絕 trailing bytes，回 108 `mode=0,count=0`；尚未建立 room model。 | `sub_568CE0` / 108 reader；HIGH |
| 143 `PM_UDPSTART_REQ` | `str identity`, `s32 n100`, `u8 literal=1`, `s32 ext_count` | `identity` 是 native 全域 `String[24]` identity scratch；全 dump 嚴格掃描後唯一可見 mutator 是 393 `GL_CHANGEID_ACK` handler `sub_58E3B0`（僅 append `'_'`），初始化/正常填值路徑屬 .c 盲區（PACKETS.md §2.6-C）——仍不能稱 account/nickname。`n100`、`ext_count` 是 681 回送的 handoff claim；literal 必須是 1。TS 只用 `n100 + ext_count + source IP` claim admission；identity 僅 log。 | `sub_555C60`、143/144 C consumer、`PACKETS.md §3.15d`；HIGH for shape, UNRESOLVED for identity |
| 195 `GC_ENTERCHANNEL_REQ` | `u8 group`, `u8 channel`, `u8 rawFlag` (`0`/`1` native boolean) | `group` 是 681 廣告的 group 序號，`channel` 是組內 channel index。第三 byte 是 `sub_56FF40` 從 `sub_7338D0()->sub_735DE0` 取得的 bool 結果；這證明 wire value 只有 `0`/`1`，不能直接命名成 replay/module/status。TS 讀取但不以它放行，只驗證其 native wire domain。TS 僅接受已認證且 group/channel 與 config 相符；native 允許 196 type 3 的 gate-only boundary，但 TS server success 只在 config 帶完整 raw continuation 時成立，不把截斷 tail 當成成功。 | `sub_56FF40`、`sub_4179D0`、`sub_7338D0/sub_735DE0`；HIGH for shape/value domain, UNRESOLVED for rawFlag semantics and type-3 server policy |
| 197 `GL_MYINFO_REQ` | empty | 請求自己的 198 MyInfo。TS 從 authenticated account 取得 Store identity 與 NewSkill snapshot；未登入時保留 null/失敗形狀。 | `sub_570550` dispatcher case；HIGH |
| 199 `GL_MYITEM_REQ` | empty | 請求 200 背包 page。TS 目前沒有 inventory/catalog model，回 `success=1,start=0` 加負 slot sentinel。 | `sub_570AB0`、`sub_524B70`；HIGH for shape, TS projection policy only |
| 246 `GL_CLIENTINFO_REQ` | `str nickname` | 以 nickname 查 public MyInfo。TS 將此字串交給 `getMyInfoByNickname`，回 247；沒有把它當 numeric user id。 | `sub_573DE0` writer、`sub_573EB0` reader/247 consumer；HIGH |
| 250 `GL_LOBBYIN_REQ` | empty | Client-local lobby transition notice；client state 由本地流程處理。TS 嚴格讀空 payload，不回 251，因目前沒有 recovered native 251 consumer。 | `PACKETS.md §3.15pre-2`；HIGH for empty/no-consumer boundary |
| 252 `GL_SHOPIN_REQ` | empty | Client 在送出前已進入 shop state 3；TS 回空 253 是 interoperability choice，不宣稱 native shop-success payload。 | `PACKETS.md §3.15pre-2`；HIGH for request, bounded TS policy for reply |
| 254 `GL_INVENIN_REQ` | `u8 requestContextRaw` | Client-local inventory/NewSkill entry context。其 UI/entity/page/tab 語意 **UNRESOLVED**；TS 原樣 echo 到 255，不解讀。TS 以 authenticated account 的 `uid` 取得五個 NewSkill profiles。 | `sub_573EB0`/254 caller、255 mode-1 consumer；HIGH for echo, UNRESOLVED for context |
| 425 `GL_MSG_RECVLIST_REQ` | `s32 rawRequestValue` | Native builder `sub_55A580` writes 4 bytes from `sub_44E480`; `sub_6DC6C0` decrements the UI value by 10 with a floor of 1, and `sub_6DC760` increments it by 10. This proves a message-list navigation scalar and its native step behavior, but not the server mailbox/page policy. TS reads it and ignores it because no mailbox model is recovered. | `sub_55A580`、`sub_44E480`、`sub_6DC6C0/sub_6DC760`、`sub_55A630` ACK consumer；HIGH for width/step behavior, UNRESOLVED total/page policy |
| 433 `GL_FRIEND_LIST_REQ` | empty | 請求 current account 的 friend list。TS 尚無 friend table，回 434 的 zero-count projection，並提供 configured account display string as a bounded compatibility projection。 | `sub_55AFC0`；HIGH for empty request and ACK shape |
| 682 `GL_LOGIN_REQ` | `str account`, `str password_or_token`, `u64 packed_data_revision`, `u8 fingerprint_source`, `raw[24] fingerprint` | `packed_data_revision` 是 guard：low dword 固定 `0xF1E1AB0E`，high dword 為 `datarevision.txt` 讀出的 client data revision XOR `0xB1A9D7C7`，不是 hardware key。`sub_401B50` 的 `Target__7` 已定位為 `WideCharToMultiByte` import；這只證明兩個 scratch credential 是 ANSI conversion output，不證明業務命名或更小 per-field max。`fingerprint_source`/24 bytes 是分離的 device/security material；native 只證明 source byte 由 security probe 選出，不能替它命名成 account/device id。TS 驗 guard、完整長度與 credentials，現在只用 account/password 驗證；revision/source/raw24 尚未成為 Store policy。 | `sub_43DF00`、`sub_401B50`/`Target__7`、`sub_A366EF`、`sub_592AE0`、682 builder、`PACKETS.md §3.1`；HIGH for wire shape/guard, bounded TS policy |
| 834 `GL_DATA_RECV_COMPLETED_REQ` | `raw4 requestContextRaw`(`sub_592AA0` caller-defined;`dword_F2A684`) | lobby data receive-completion report。native `sub_583120` 直接把 `dword_F2A684` 寫入；該 global 是 144 的 propagated raw4 context，雖也被多個 client request 重用，但其 server-domain/user identity 語意未證實。TS 只讀取、拒絕 trailing bytes，再回空 835；不可命名為 `user_id`。 | `sub_583120`、144 `sub_555D50` raw4 path、419 `sub_559550`/other raw-context builders、834/835 mapping；HIGH for shared raw field, UNRESOLVED domain |

### C2S 的共同 boundary

所有上表 reader 都拒絕未被 layout 支持的 trailing bytes。這是 frame/parser
完整性檢查，不等於 native server 一定使用相同的錯誤 transport。`rawFlag`、
`fingerprint_source`、fingerprint bytes、`requestContextRaw`、834 的 shared raw
context 目前均不能因為欄位名稱相似而互換。

## S2C builders（16）

| opcode / TS | wire order and field | wire meaning / current TS use | evidence and confidence |
|---|---|---|---|
| 102 `GT_PING_ACK` | empty | Server heartbeat；client 收到後建立 101。TS 可建立空 packet；對 inbound 101 不回 packet。 | `sub_58D6F0`；HIGH |
| 106 `GL_USERLIST_ACK` | `raw2 gate`; if nonzero: `u8 flags`, `u8 recordCount`, each `raw4 userKey`, `str nickname`, `s32 exp`, and when `userKey>0`, `raw4 customTexKey`, `str texName` | Native first reads a 2-byte value into `__int16 v23` and only tests zero/nonzero; it is not the record count. The actual loop count is the later `u8 i_1`. `flags` bit0 calls `sub_408270` and sets the mode-specific state byte; `sub_6FF430` returns that byte to gate waiter-list rendering, while bit2 clears it; each positive-key row is inserted by `sub_588560`, which calls `sub_403360(exp)` and the waiter-list UI uses the resulting Class index. Positive keys also look up the client profile table; a found profile receives the texture key and `EMBLEM` texture lookup. Current TS emits the zero gate, so no optional fields follow. | `sub_56A250`, `sub_588560`, `sub_6FF430`, `CUIWaiterList::sub_6FF470`; `Extracted/ui/ui.xml` `Class`/`EMBLEM`; HIGH for order/width/direct consumers, UNRESOLVED gate/status/identity/texture/exp policy |
| 108 `GL_GAMEROOMINFO_ACK` | `mode!=3`: `u8 mode,u8 count`, then ordinary records. `mode==3`: `u8 mode`, then **no ordinary count**; `sub_580A80` reads `u8 n4,u8 i1,u8 flags`, reverse stage records (`u8,u8,u8,raw4,raw4,u8 pairCount`), pair base (`raw4,u8,u8,u8,u8,raw2`), round-4 extra (`u8` + 4×raw4) or other-round participant blocks (2×raw4), tail `u8 hasMy` + optional 2×u8, then raw4 (native local `int v57`). | Native direct mapping: `sub_53F9F0` receives each node id, current/max bytes, a raw2 mask, stage-derived bytes/word, mode byte, flags, and participant-related bytes; it writes `CRoomInfo` offsets including `+4,+105,+129,+110,+130,+136,+144,+185,+186`, then normalizes max slots from the mask. `sub_875C20` consumes each of the two participant blocks; the first dword of either block is compared with `sub_54B570(dword_131E238)` to select the local/current node. Tail first byte becomes the selected raw node value; final raw4 is stored at client state `[494]` through native `int v57` at state `[494]`. | `sub_568CE0`/`sub_580A80`/`sub_53F9F0`/`sub_875C20`/`sub_47E1B0`/`sub_47E3E0`; `Extracted/ui/TNMT_Awardproperty.xml`; HIGH for framing/width and state writes, UNRESOLVED for business names |
| 144 `PM_UDPSTART_ACK` | `u8 result`, `u8 flag`, `s32 daily_login_value`, `str raw_string_v71`, `s32 post_name_raw_0`, `s32 post_name_raw_1`, `s32 restriction_value`, `f32 restriction_value_float`, `raw4 client_request_context`, `u8 has_net_cafe_info`, optional `4×u8 + 8×s32` | Client 在所有 result 上先讀完整固定 prefix。result 是 raw u8；TS 保留已知 UI constants，但未知 code 也原樣可發送。flag 只有在 native rank `>10` 的共同條件下直接觸發 restriction UI；daily value 只有正值才進 login-reward notice；restriction s32 的低 byte 與 f32 分別進 restriction message。兩個 name 後欄位無 recovered consumer；`client_request_context` 原樣傳入多個後續 request，但 domain 未證實。`has_net_cafe_info` 會初始化 native NetCafe feature object；optional slots 的業務名未證實。TS 預設 raw/empty、保留 fixed prefix、並 `has_net_cafe_info=0`，所以不發 optional block。 | `sub_555D50`/`sub_555C60`、`sub_58B010`、`sub_592AC0`/`sub_592A40`/`sub_592B40`、`sub_A1C800`/`sub_A1C910`、`Extracted/ui/system/netcafe_contents.xml`；HIGH for order/width and recovered consumers, UNRESOLVED policy and optional slot domains |
| 196 `GC_ENTERCHANNEL_ACK` | `u8 result`, `s32 channel_id`, `u8 channel_index`; only success: `str udp_host`, `s32 udp_port`, `u8 endpoint_opaque`, `u8 channel_type`, `raw4 client_flags`, `u8 client_default`; `channel_type==3` then enters `sub_875680`, whose native gate may stop after `s32 header0` when `header0<=0` (the four fixed 4-byte fields there are `raw4`, not `f32`) | Failure 只有前三欄。result 是 raw u8；TS 保留已知 UI constants，但未知 non-success code 也只寫前三欄。成功 endpoint 是 client 後續 UDP control address；port wire 是完整 s32，native endpoint consumer 另取其 low u16，TS projection 仍要求可用的 `1..65535` endpoint port。`endpoint_opaque`、`client_flags`（bit0 已知）、`client_default` 保持 raw-oriented。Native type 3 可在 header0 後停止，但 TS server 只有在完整 raw continuation 已配置且 header0 positive 時才輸出 success；不輸出截斷的 false-success tail。channel admission 不允許非 type-3 附帶未消費 tail；完整 native grammar 與 lifecycle 見 [`S2C_NATIVE_AUDITS.md`](S2C_NATIVE_AUDITS.md)（196 part）。 | `sub_4179D0`、`sub_4177B0`、`sub_58ED30`/`sub_596E60`、`sub_875680`；HIGH |
| 681 `GL_LOGIN_ACK` | `s32 result`; only success (`result=1`) continues with `s32 user_no`, `s32 n100`, `s32 ext_count`, `raw2 server_count`, server records (`raw2 server_id`, `str name`, `str host`, `raw2 server_port`, `u8 flag`, `raw2 group`, three groups of `s16 max_users` plus optional channel `{u8 ch_type,str ch_name,raw2 current_users,u8 ch_flag,[u8 extra when type=3]}`), and fixed `s32 billing_first`, `s32 billing_second` | Failure is exactly the result word and the native branch tests its low byte. `user_no` remains the official conservative name; TS supplies verified account row id without claiming it is the later 198 user row. `n100` is opaque charge/billing UI mode and is echoed by 143. `ext_count>0` makes the native reader consume exactly one `{s32,s32,u8}` extension triple and pass it to `sub_A1C870`; TS production still emits 0 by default, while the audited writer can emit an explicitly supplied raw gate plus exactly one raw tuple without assigning semantic names. `server_port` is not duplicated by the channel field: `sub_58AD90` passes server host plus this field to `sub_554810` as a `u_short` TCP endpoint. The channel field is native `USERS` numerator/current-users data; the preceding `max_users` field is the denominator/capacity and positive reader gate. TS retains the existing public `GameServer.port` property for this server-level endpoint, while modeling three `ChannelGroup` objects rather than calling the channel field a port. `flag`, `group`, and billing words remain unresolved. TS validates raw2 server_id/group without assigning signedness, and validates channel/server u8 fields before writing so a masked type cannot accidentally change the type-3 framing. The reader stores a 132-byte internal projection rooted at the local scratch (`sub_58E690`); it is not a second wire field. Positive `maxUsers` without a channel is rejected rather than projected as a zero gate; this preserves the native continuation boundary without silently reinterpreting later groups. | `CLobbyLogin::sub_43E500` (681 branch), `sub_58AD90`/`sub_554810`, `sub_416DA0`/`sub_4176C0`, `sub_4179D0`/`sub_56FF40`, `sub_58E690`/`sub_58F120`/`sub_58E640`/`sub_58E670`, `sub_7092C0`, `PACKETS.md §1.4`; HIGH for order, endpoint/USERS consumer, and the type/channel projection cross-check; wire signedness remains conservative except the endpoint consumer's `u_short`; `flag`/`group`/billing domains UNRESOLVED |
| 247 `GL_CLIENTINFO_ACK` | `u8 ok`; if ok: shared 198 basic block, then `u8 slot` (native 0..19 character-list index), `u8 char_type`, `12×u16 appearance` | `ok=0` 只有一 byte。成功首段與 198 的 `sub_523BF0` 完全共用；尾端是單一 character appearance，不是 198 的 character list、weapon groups 或 NewSkill tail。`slot` 是 serialized character-list slot/index；`char_type` 才是角色類型。TS 以 selected character-list index 取 serialized array entry，並在 247 尾端回寫同一 index；不把 persistent slot id 或 char_type 當成該欄位，然後寫 12 個 category-relative u16。Store 先將 persistent slot key 映射為 compact serialized-list index。若 index 不在 native 0..19 或沒有對應 serialized entry，TS 回 `ok=0`，不以另一筆 character appearance 靜默 fallback。 | `sub_573EB0`、`sub_523BF0`、`sub_524360`、`sub_524010`；HIGH |
| 253 `GL_SHOPIN_ACK` | empty | 沒有 recovered native shop success payload。TS 的空 ACK 是明確標成 interoperability response，不把它寫成官方成功資料。 | `PACKETS.md §3.15pre-2`、未找到 253 consumer；HIGH for current boundary |
| 255 `GL_INVENIN_ACK` | Common prefix `u8 mode`, `s32 uid`, `u8 requestContextRaw`, `u8 unknownHeaderRaw`; native mode 0 then reads two more `u8` values and one `s32` remote lookup value. Native mode 1 then reads `u8 selectedProfile` (`<5`) and 160 raw bytes = 5 profiles × (`7×s32 puzzleItemId`, `s32 expiresAtPackedMinute`). | **Native fact:** `sub_574270` accepts mode 0/1, always reads the common four fields, and in the local-user branch reads selected profile plus 160 raw bytes only when the selected value is below 5; `sub_4AAB80` copies five 32-byte profile records and uses the selected value to choose the current record. **Resource fact:** `Extracted/ui/NewSkill*.xml` and the native `sub_527AF0`/`sub_535020` path identify seven NewSkill puzzle slots and a shipped item-catalog membership check; resource/UI names do not prove grant policy. **Inference:** the uid/profile scope is account-level rather than a character appearance index because 254/255 use a self uid and no character index; packed expiry is profile state, with profile 0 ignored by the native expiry display path. **TS projection:** mode 1 local snapshot only; uid is a positive s32 Store user id, request context is echoed, unknown header remains zero, selected profile is 0..4, and every profile has seven validated s32 IDs plus s32 expiry. **UNRESOLVED:** mode-0 remote fields, context/header meanings, full catalog membership projection, expiry/server-time policy, duplicate accessory policy, and grant/ownership authority. | `sub_574270`、`sub_4AAB80`、`sub_527AF0`/`sub_535020`、Extracted NewSkill resources；HIGH for grammar/width, MEDIUM for profile scope, UNRESOLVED business policy |
| 426 `GL_MSG_RECVLIST_ACK` | `raw2 rawHeader`, `str field_s0`, `u8 count`; each record: `str field_s1`, `u8 field_a3`, `str field_s2`, `raw4 field_a5`, `str field_s3`, `str field_s4`, `raw2 field_a8` | **Native fact:** `sub_55A630` reads the fourth record field into `int v37` and the final record field into `__int16 v35`; `sub_5378C0` retains at most 10 records. Its local `sub_592*` sequence is `str,u8,str,raw4,str,str,raw2`. **Resource fact:** the native table count is at `this+241704`; per-entry storage uses fixed strides of 20 bytes for the first NUL string, a 2-byte stride for the first raw byte slot, 21 bytes for the second NUL string, a one-byte slot for the low byte of raw4, 201 bytes for the third NUL string, 2 bytes for the final NUL string, and a one-byte slot for the low byte of raw2. The copy loops themselves stop at NUL and do not visibly clamp to those strides; `201`/`2` are local storage facts, not wire string limits. The `raw4` fourth-field argument is assigned into a one-byte table slot at `this+60536+index`, and the `raw2` final-field argument is assigned into a one-byte table slot at `this+122107+index`; the recovered list table therefore visibly retains only their low bytes even though the wire reader consumes 4/2 bytes. No Extracted message/catalog resource gives these record fields or a mailbox schema. **Native consumer fact:** the first per-record string is not inert: `sub_537D20` finds a matching stored string and writes marker byte `89`; `sub_537E90` returns whether that same string is marked `89`; `sub_55A1E0` sends 421 only when the string exists, and `sub_55A3C0` sends 423 only when it is not marked. The 421/423 readers (`sub_55A310`/`sub_55A4F0`) call the mark/unmark helpers. This proves a string-key plus read-state marker path, but not whether the key is a sender, recipient, message id, or mailbox folder. The other stored record strings and the low-byte copies of raw4/raw2 have no recovered semantic consumer in this path. **TS projection:** count 0 with `rawHeader=0` and the configured account display string as an interoperability projection; native `sub_55A630` reads but does not visibly consume its context string, so this is not a recovered mailbox identity rule. **UNRESOLVED:** rawHeader, context-string/record-string domains beyond the key/state fact, raw-field domains, mailbox paging/policy. | `sub_55A630`、`sub_5378C0`（10-record cap、fixed storage strides、wire raw4/raw2 → local low-byte assignments）；HIGH for order/width/capacity, UNRESOLVED business fields |
| 198 `GL_MYINFO_ACK` | `u8 success`; if success: `s32 user_id`, shared basic block, character list, weapon groups, 9 UI slots, selected NewSkill raw block, tail | `user_id` 是 MyInfo wire user id；TS 使用 Store player `userId`。basic block：`str nickname`（native `CClientData` fixed `char[24]`，最多 23 ANSI bytes），`u8 selected_char_index`, `s32 level`, `s32 experience`, one raw/unknown `s32` stored at native `+108` (later consumed by `sub_9252D0` as task condition 1 input; server owner unresolved), then 3 reserved words at `+136..+144`, followed by 15 stat words in native wire order: `[wins,losses,kills,deaths,headshots,combos,hearts,doubleKill,tripleKill,criticals,multiKill,ultraKill,zKill,kKill,ddKill]` ⚠ 非遞增位移序——reader `sub_523BF0` 與 mirror writer `sub_523E10` 都先讀 slot [45](+180) 與 [46](+184) 才讀 [44](+176)；label↔dword 直繫由 `sub_5206F0` 定案（[44]=CRITCALSHOT, [45]=DOUBLEKILL, [46]=TRIPLEKILL），`sub_9252D0` 任務條件對照互證（2026-09-18 三式交叉重驗；此前 TS 依舊 §3.2 表送出順序錯置 criticals/double/triple，已修正）; native `+100` derived class/level is recomputed from experience rather than read from this packet. The runtime does not persist `disconnects`, `playCount`, or `roundCount`: no wire owner is proven and no current handler consumes them. Followed by `u8×3 flags`, `s32 cash`, `s32×2 raw`, `48B extra blob`, `u8 slot_current`。`+108` has a proven native task-condition-1 consumer (`sub_9252D0` → native `[27]`), but no proven Store/stat owner; `reserved34..36` still have no recovered task/stat consumer. TS keeps all four conservative words zero. The first dword of the 48B blob is the cumulative play-time task counter (`Stats.playTimeSeconds`); the remaining 44 bytes are still zero because mode counters are not modelled. `derived-level` is recomputed from exp by the client; flags and raw words remain conservative.接著是 `u8 char_count` + 每筆 `u8 char_type + 12×u16`；最多 20。再是最多 4 組 weapon loadout（group 3 只有 primary），9×s32 UI-item slots；再是 raw `u8 n5`（current compatible value 5）+ selected profile 7×s32；最後 `u16 pending_gift_count`, `s32 game_point`, `u8 tutorial_count` 及 records。TS 只填 Store 已有資料，其餘使用已證實的 zero/empty projection；若 caller 提供 NewSkill snapshot，selected profile 與五筆/七槽 shape 先驗證，且非零 puzzle ID 依 `Extracted`/`sub_527AF0` 的七段 native ordinal range 驗證，不以 malformed snapshot 靜默回填 zero。 | `sub_570550`、`sub_523BF0`、`sub_524010`、`sub_524660`、`sub_527550`、`sub_527D00`、`RESOURCES.md §5c-1/§5c-2`；HIGH for order/grammar, UNRESOLVED raw/n5 domains |
| 200 `GL_MYITEM_ACK` | `u8 success`, `s32 start_index`; repeated item: `s32 inv_slot`, `s32 item_id`, `raw4 f1`, `raw4 f2`, `s32 period`, `u8 extra`, `u16 durability`; terminal negative `s32 inv_slot` | **Native fact:** `sub_570AB0` gates the reader on a nonzero success byte; `sub_524B70(...,1)` reads an `int` start index, scans at most 100 positions within the 5120-slot client array, stops on a negative `inv_slot`, rejects a non-positive or catalog-missing `item_id` through `sub_535020`, then reads two generic four-byte words through `sub_592AC0` into native `int`/byte storage, one period s32, a u8 `extra`, and a u16 value; no direct float local or float consumer is recovered for these two slots. The client calls the final s32 in this reader a negative terminator by branch behavior. **Resource fact:** `Extracted/ui/cfg/itemdata.pat` is the client item catalog used by `sub_535020`; this proves membership/lookup, not ownership, grantability, pricing, or the domains of `f1`, `f2`, or `extra`. **Inference:** `period` is a signed remaining-duration word in the inventory projection, but its server time policy is not recovered. **TS projection:** success 1, start 0, optional records, and terminal `-1`; TS rejects negative slots and non-positive item IDs, preserving the confirmed raw4/s32/u8/u16 widths; the current TS API uses finite f32 values only as a byte-compatible projection, but does not silently invent an item catalog authority. **Decision:** do not load `itemdata.pat` in the current runtime. It is a shipped client lookup table (and the local `.pat` is an encoded/resource-format dependency), while `server-ts` has no inventory ownership model; using membership as a grant or shop policy would turn a resource fact into an unsupported server rule. Keep the native positive-ID boundary now. When a real inventory projection is added, add a separate resource/catalog adapter and validate every emitted ID against it without using the adapter to decide ownership, pricing, or grantability. **UNRESOLVED:** item ownership/service policy, f1/f2 domains, extra, period policy, and the complete future catalog adapter contract. | `sub_570AB0`、`sub_524B70`、`sub_535020`、`Extracted/ui/cfg/itemdata.pat`、`LAYOUTS.md`；HIGH for order/width/framing/caps, UNRESOLVED business semantics |
| 201 `GL_MYPARTSUP_ACK` | `s32 count` + count×`{raw4 key0, raw4 key1, u8 kind, raw4 value, raw4 period}` (server 推送; 無 REQ) | **Native fact:** `sub_95A3B0` fills the `byte_2313148` PartsUp store. 每筆 **wire 17B**;native heap node `operator new(0x14)`=20B,kind(u8) 後 3B pad 不上線。`sub_95A4A0` 以 (key0,key1) pair 為 insert/update key,duplicate 丟棄;loop 尾 `sub_538470` UI 同步。**TS projection:** s2c builder 完整實作 17B/record 線形 (count 由陣列長導出,s32/u8 域驗證);key/value/period 的命名/授權政策 UNRESOLVED,僅接受 catalog-derived 值的呼叫端責任。空 projection = count 0。 | `sub_95A3B0`、`sub_95A4A0`、`sub_538470`；HIGH for order/width, UNRESOLVED business semantics |
| 202 `GL_EXPIRE_PARTSUP_ACK` | 與 201 同構 (server 推送; 無 REQ) | **Native fact:** `sub_95AE40` 讀同 17B records,每筆經 `sub_95A800(this, key1, key0)` 反序 pair 刪除 (callee argument order 是 native fact,不足以命名 `(part,gun)`)。**TS projection:** 與 201 共用 `writePartsUpEntry` (單一線形來源),count 0 為 no-op。 | `sub_95AE40`、`sub_95A800`；HIGH for order/width, UNRESOLVED business semantics |
| 434 `GL_FRIEND_LIST_ACK` | `raw2 rawHeader`, `str contextString`, `u8 count`; each record: `str field_s1`, `raw4 field_a3` (native local table keeps low byte) | **Native fact:** `sub_55AFC0` reads the two-byte header, context string, and unsigned count; `sub_537F60` stores at most 100 string/row-field records. **Resource fact:** the native table count is at `this+244236`; insertion is allowed only while count `< 0x64` (100 entries), each record string uses a 21-byte NUL-string stride at `this+244237+21*index`, the wire `int` record field is assigned into the one-byte slot at `this+61585+index` (only its low byte is visibly retained), and the surrounding delete/shift paths retain two one-byte side fields per slot. `sub_537F60` itself copies until NUL without visibly clamping to the 21-byte stride. `Extracted/` has no friend-table or row-field definition that can name these fields. **Inference:** the context string is read by `sub_55AFC0` but not passed to `sub_537F60` or another recovered consumer; no owner/display semantic is proven. **TS projection:** header 0, configured account display string, count 0; the string is a bounded compatibility projection, not a native owner-id claim. **UNRESOLVED:** header domain, friend record-string/row-field domain, row-field values, side-field meanings, and friend policy. | `sub_55AFC0`、`sub_537F60`（100-entry cap、21-byte key stride、wire raw4 row field→local low byte、side-field shift paths）；HIGH for order/width/capacity, UNRESOLVED business fields |
| 693 `GL_TCPCONNSUCC` | empty | Channel-server greeting. Client shows its greeting and immediately builds 143. TS sends it once as connection trigger; no payload fields exist. | `sub_57CAE0` → `sub_555C60`；HIGH |
| 694 `GL_ACCOUNTCONNSUCC` | `u16 compression_threshold` | Native `sub_43E651` reads exactly one 2-byte value into `n0x2580`; only a value strictly below `0x2580` replaces the local compression threshold, while `0x2580` or larger leaves compression disabled. The same handler then invokes the 682 login builder. TS validates only the native `u16` boundary and sends the value unchanged, including values the client ignores; the default remains `0x2580` so compression is disabled. | `sub_43E651` `n694==694` branch、`sub_592A00`、`sub_592CE0/sub_592E00` compression path；HIGH for wire/client behavior |
| 835 `GL_DATA_RECV_COMPLETED_ACK` | empty | Completion ACK after 834. No payload is consumed by the recovered client path. | 834/835 mapping and completion consumer；HIGH |

## 425–434 native caller / UI / adjacent-handshake supplement

This supplement supersedes any older field label in the index above. The packet
names are retained as dispatcher identifiers, but field domains stay raw unless
the native consumer below proves more.

### 425 → 426

- `sub_55A580` is called by the native UI path, not only by an isolated builder.
  The `REFRESH_INBOX` UI command resets the messenger object's `+65` flag through
  `sub_44E4A0`, reads `+63` through `sub_44E480`, and sends that value. The
  `CUIMessageList::sub_6D9990` setup path also initializes the messenger `+63`
  value to `1` before sending it.
- `CUIMessageList::sub_6DC6C0` is the previous-step caller: it requires the
  request-in-flight flag at `+260` to be clear and `+252 != 1`, saves `+252` at
  `+256`, records mode `1`, records a timer at `+264`, subtracts `10`, floors
  the result at `1`, and calls `sub_55A580`. `sub_6DC760` is the next-step
  caller: it requires `+260 == 0`, saves the old value/mode/timer, adds `10`,
  and calls `sub_55A580`. These are native UI step facts; they do not prove a
  total-count, server cursor, or mailbox policy.
- Dispatcher case 426 calls `sub_55A630`. The reader first consumes raw2 and
  the context string, then a u8 count. The context string is not passed to the
  table insertion helper, so no account/page-token meaning is recovered.

### 426 record fields

The following are consumer facts, not reconstructed business names.

- `field_s1` is a row key in the local 20-byte slots. `sub_537DE0` searches it,
  `sub_537A80` removes and compacts a matching row, and `sub_537D20` marks a
  matching row with byte `89`. `sub_55A1E0` builds 421 only when the key exists;
  `sub_55A3C0` builds 423 only when the key is not already marked `89`;
  nonzero 422 calls the remove helper and nonzero 424 calls the `89` marker
  helper. The key is therefore directly evidenced, but sender/recipient/message
  id/folder semantics are not.
- `field_a3` is stored at the 2-byte-stride state area beginning at
  `byte_F23A60`. The UI tests the corresponding byte against ASCII `N` (`78`)
  in `CUIMessageColumn::sub_6D84A0`; the 424 success path writes ASCII `Y`
  (`89`). This proves the observed one-byte state branch and its `N`/`Y`
  values. It does not justify a database `is_read` projection without a server
  join.
- `field_s2` is copied to the 21-byte-stride area beginning at
  `byte_F23A74`. The message-list UI passes the matching slot to its `MSG_NAME`
  display path, and its reply path (`sub_6D95C0`) uses the same slot when it
  constructs a reply. This proves a displayed/action-associated string, not
  whether it is a sender or recipient.
- `field_a5` remains wire `raw4`. The recovered list insertion assigns it to
  `this + 60536 + index`, visibly retaining only one byte. The UI separately
  formats a dword local array at `dword_F23B48[index]` as a date/time, but the
  dump does not show a direct write from this reader's `field_a5` to that dword
  array. Do not rename `field_a5` to timestamp.
- `field_s3` is copied into a 201-byte local slot. The message-list UI has a
  `MESSAGE` control, but the recovered dump does not provide a direct address
  join from this wire string to that control's source; keep it raw.
- `field_s4` is copied into a 2-byte-stride area beginning at `0xF2434A`.
  Native UI code compares each slot with the literal `F` and with `M`: an `M`
  slot selects the reply path using `field_s2`, while an `F` slot participates
  in the friend-apply/refuse UI branch when a separate local marker is `1`.
  This proves a small type/control string domain with observed values `F` and
  `M`, but not a general enum or the meaning of the other values.
- `field_a8` remains wire `raw2`; its recovered table assignment retains only
  the low byte at `this + 122107 + index`, and no semantic consumer was found.
  The header raw2 and context string likewise remain unresolved. The shipped
  `Extracted/` tree contains no message-record schema that joins the unresolved
  fields to a catalog or mailbox model.

### 421/422 and 423/424 adjacent operations

The dispatcher routes 422 to `sub_55A310` and 424 to `sub_55A4F0`. Each reader
consumes an `s8/bool statusRaw` (`sub_592900`) followed by one string. For 422, a nonzero status calls
`sub_537A80` with the string key; for 424, a nonzero status calls
`sub_537D20`. Zero status selects a localized error path. The C2S builders
validate a nonempty string of at most 20 bytes and use the same local key table
as 426. This proves the key/remove and key/marker handshakes, while the status
values and server persistence remain raw.

### 429–434 friend-list-adjacent operations

- 429 validates a nonempty string of at most 23 bytes, rejects the current local
  account and an already-present local friend-table key, then sends one string.
  430 reads `u8 statusRaw, str`; status branches select localized resources
  `0x1E8` through `0x1EC`, while status 0 inserts the returned string into
  the 100-entry table and then sends an empty 433 refresh request. The resource
  strings and the native `friend` UI establish the feature context, but do not
  establish a complete server result-code policy.
- 431 validates a nonempty string of at most 23 bytes and sends it. 432 reads
  `u8 statusRaw, str`; status 0 removes the string from the local 100-entry
  table and sends 433, while nonzero branches only select localized paths in
  the recovered handler. Keep the status and string as raw/key fields.
- 433 is an empty request. Its builder sends opcode 433, and 434 dispatches to
  `sub_55AFC0`. That reader consumes raw2, a context string, u8 count, and
  repeated `str, raw4`; `sub_537F60` stores at most 100 rows, keeps only the
  low byte of the raw4 in its local side slot, and copies the string without a
  visible stride clamp.
- After 434 insertion, `sub_55B0A0` uses the locally accumulated strings to
  optionally build 435 as a comma-separated string request. The 436 reader
  consumes the 435/436 follow-up and calls `sub_5382D0` with each key, online
  byte, optional location string, and channel byte. This is direct adjacent
  handshake evidence for the 434 row strings; it does not give the 434 raw4 a
  meaning, because the recovered 436 fields are read from a different packet.
  The 434 header/context and raw4 remain raw, and `Extracted/` has no friend-row
  schema that changes that conclusion.

### 834 → 835 shared-context and completion path

- `sub_583120` is called from at least two native state-machine paths: from
  `sub_488050` after its room/data progression, and from `sub_449F90` where the
  lobby/session progression sets local state `+1905` to 6. It constructs
  834 with exactly one raw4 written from `dword_F2A684`. The dispatcher routes
  835 to `sub_5831D0`, which consumes no payload and calls `sub_522440` to
  clear the common native progress object.
- `dword_F2A684` is assigned by the 144 reader (`sub_555D50`) from one raw4
  field. It is then reused by multiple unrelated native builders, including
  the 419 message-add builder, 820, 834, and the 344/346/348/350 text-related
  builders. This reuse is direct evidence that the 834 word is a shared raw
  context value, not evidence for a user id, account id, or completion token.
  The TS handler therefore validates one signed 32-bit wire field and does not
  join it to Store identity; 835 stays empty.

The resource cross-check is limited to the strings selected by the native
resource IDs. In `Extracted/ui/lang/msgtableres.lang` (CP932), the relevant
entries are: `0x1E3` recipient-character-name check, `0x1E4` message-send
failure, `0x1E7` message-receive failure, `0x1E8` self cannot be registered as a
friend, `0x1E9` character already registered, `0x1EA` friend-list registration
success, `0x1EB` reconnect-after-disconnect notice, `0x1EC` account not
registered, `0x1ED` already-registered-user text, and `0x1EE` friend-list
registration failure. These are UI/resource facts about the branches; the
presence of a generic or apparently mismatched localized string does not prove
a server result-code meaning or alter any wire width.

## 已修正的 TS 行為

這次逐欄核對發現一個會被全零 fixture 掩蓋的實作問題：198/247 共用的
早期 TS projection 曾把未證實的 `disconnects` 放進 native `+164`，並把
`combos`/`criticals` 寫到錯誤的統計位置。`sub_5206F0` 直接以 UI labels
`TOTAL_WIN`、`TOTAL_LOSE`、`MY_KILL`、`MY_DEATH`、`HEADSHOT`、`AIRCOMBO`、
`HEARTBREAK`、`CRITCALSHOT`、`DOUBLEKILL`、`TRIPLEKILL`、`MULTIKILL`、
`ULTRAKILL`、`GENOCIDE`、`KILLINGMACHINE`、`DIABLO` 消費 native offsets
`+148..+204`，所以 TS 現在依 native wire order 投影 confirmed counters；
`+108` 會被 `sub_9252D0` 作為 task condition 1 threshold input 消費，但沒有證據
可把它等同 `playCount`；`disconnects`、`roundCount` 也沒有找到可直接對應的
wire owner，所以目前 runtime 不建立這三個欄位。

後面的 confirmed wire order 是：

```text
raw/unknown(+108; task condition 1 consumer), reserved(+136,+140,+144),
wins, losses, kills, deaths, headshots, combos, hearts, criticals,
doubleKill, tripleKill, multiKill, ultraKill, zKill, kKill, ddKill
```

Native `+100` 是由 experience 重算的 derived class/level，不是另讀的 wire word。
這次只修正有直接 evidence 支持的 projection，不新增 server policy，也不改
frame bytes 的欄位數。

另外將 200 的第 6 個 item 欄位明確建模為保守名稱 `extra`（default 0），並把
105 refresh byte、425 unresolved s32、834 shared raw context、434/426 unresolved header
與 253 compatibility boundary 寫進實作註解，避免將未證實語意寫死。

## 證據交叉表

| area | primary evidence | secondary check | result |
|---|---|---|---|
| 198/247 basic data | `PaperMan.exe.c` `sub_523BF0` | `docs/PACKETS.md §3.2`、shared TS builder | stats order corrected; 247 stops after one appearance |
| 255 | fixed mode-1 reader and 254 caller | `server-ts/src/store.ts`, NewSkill resource notes | uid/profile snapshot kept separate from appearance; context/unknown conservative |
| 105/106 | `sub_56A0F0`, `sub_56A250` | `LAYOUTS*.md`, TS zero projection | u8 trigger and count-zero tail confirmed |
| 425/426 | `sub_55A630` | `LAYOUTS.md`, TS zero projection | first raw2 header stays unresolved; no invented page semantics |
| 433/434 | `sub_55AFC0` | TS output | context string + count are read; context/header/row field remain raw |
| 246/247 | `sub_573DE0`, `sub_573EB0`, `sub_523BF0`, `sub_524360` | TS public lookup and single-character builder | no 198 character-list tail copied into 247 |
| 254/255, 834/835 | native fixed grammar; 834 `sub_583120` writes shared `dword_F2A684` | current handler/store | 254 context and 834 shared raw context are read/echoed only; no unproven user join |

若未來補上 mailbox/friend/room/inventory/catalog/policy data model，應先在本文件
把相應 `UNRESOLVED` 轉為 direct evidence，再新增欄位用途；不可先以推測命名
取代現有保守欄位。

---

## Part II — Server handler 待辦清單（原 TODO_HANDLERS.md）

---

Last reviewed: **2026-09-17**
`server-ts/` 是唯一的 server implementation。這份文件只描述目前 Bun runtime
已註冊的 packet modules、尚未實作的高價值 boundary，以及下一步需要補的
native/resource evidence；它不把 client reader/writer 自動推導成 service policy。

## Current implementation surface

`server-ts/src/ops/registry.ts` 明確列出 packet modules，並以 directory check
防止新增檔案遺漏；啟動時將每個 filename 對到 `db/packets.tsv`。
目前有 15 個 C2S modules 與 16 個 S2C modules：

- Login/channel: `GL_LOGIN_REQ`、`PM_UDPSTART_REQ`、`GC_ENTERCHANNEL_REQ`，以及
  694、681、693、144、196 的 handshake replies。
- Lobby bootstrap: `GL_LOBBYIN_REQ`、`GL_MYINFO_REQ`、`GL_MYITEM_REQ`、
  `GL_INVENIN_REQ`、`GL_GAMEROOMINFO_REQ`、`GL_USERLIST_REQ`、
  `GL_FRIEND_LIST_REQ`、`GL_MSG_RECVLIST_REQ`、`GL_SHOPIN_REQ`、
  `GL_CLIENTINFO_REQ`、`GL_DATA_RECV_COMPLETED_REQ`。
- Transport: `GT_PING_REQ` / `GT_PING_ACK`。client 的方向命名與 server 的
  send/receive 方向不同，請以 `c2s/` 或 `s2c/` 目錄為準。

每個 module 只負責自己的 wire reader 或 builder；connection ordering、admission
state 與 SQLite projection 分別位於 `connection.ts`、`admission.ts`、`store.ts`。
TypeScript signatures 是 compile-time contract，不取代 reader 的 runtime
width、fixed-buffer、mask/coerce、count limit 或 malformed-input rejection。

## Current next evidence

1. **Channel admission 143→144→195→196**：取得一組成功與一組拒絕的同 revision
   capture，補出 681 extension values、144 raw fields 與
   196 type-3 continuation。`String[24]` 的來源鏈已由 native 側解到結構極限
   （全域 identity scratch；唯一可見 mutator＝393 `GL_CHANGEID_ACK` 的 `'_'`
   append；初始化屬 .c 盲區——PACKETS.md §2.6-C）。未有 capture evidence 前，
   TS 只保留 exact raw shape，不能把 raw bytes 命名成 account、rank、billing
   或 entitlement。
2. **Private UDP**：目前只實作 source-proven encrypted 19→empty-20 control。
   `sub_595E80` 全部 cases、secondary socket、send lanes、remote address 與
   correlation data flow 的 client-side 追蹤已定案於 [`PACKETS.md`](PACKETS.md)
   §2.6（33/37 完全定案；A/B=C2P hole-punch、1=任務邀請、17=殘留設計、6=證據
   上限四項除外）；relay 可實作性由 (B) 位址鏈給出 client-side fact。仍不能因
   opcode 存在就接受 P2P/NAT/gameplay datagrams；其餘語義的 server 實作決策
   仍待 runtime evidence。
3. **Room and battle state**：取得 111–194、730–742、902–963 的可重現 captures，
   先逐欄核對 builder、dispatcher、parser、room/session consumer，再建立 state
   machine。沒有 owner、timer、duplicate/cancel 與 success-tail evidence 時，回
   fail-closed/no-mutation，不送固定成功 ACK。
4. **Clan/tournament and matching**：先完成 756–776、718/721、983/988 的
   caller/callee、membership、queue、timeout 與 result-state audit，再決定是否
   擴充 `Store`；目前的 schema 或 UI 名稱不能當作 ownership proof。
5. **Economy and grants**：shop、gift、reward、weapon、skill、drop 與 pricing
   僅在有 native consumer、resource lookup、持有狀態與 response mutation 的完整
   evidence chain 後實作。資源存在本身不是 entitlement。

## Unimplemented request inventory

下表是下一輪優先處理的 C2S opcode。其 framing 是 native evidence；response、
mutation、ownership 與 service policy 仍未獲得授權。

| Opcode | Name | Current safe boundary |
|---:|---|---|
| 103 | `GE_LOGOUT_REQ` | 解綁 connection/session；不發未證實 ACK |
| 298 | `GS_TAKEGIFT_REQ` | 保留 request shape；不刪 gift、不回成功 |
| 300 | `GS_MOVEGIFT_REQ` | 保留 request shape；不改 inventory |
| 306 | `GG_JJGET_REQ` | 先補 reward/ownership state |
| 324 | `GG_BOMBEND_REQ` | 先補 room battle state |
| 374 | `GR_GETCRYSTAL_REQ` | 先補 room/object owner 與 result policy |
| 398 | `MASTER_SVRCLASS_REQ` | operator-only boundary 未定義，拒絕 |
| 400 | `MASTER_CONNTYPE_REQ` | operator-only boundary 未定義，拒絕 |
| 410 | `MASTER_DISLOGIN_REQ` | operator-only boundary 未定義，拒絕 |
| 412 | `MASTER_DISGMS_REQ` | operator-only boundary 未定義，拒絕 |
| 414 | `MASTER_DISLOG_REQ` | operator-only boundary 未定義，拒絕 |
| 418 | `MASTER_RESETTCPGROUPINFO_REQ` | operator-only boundary 未定義，拒絕 |
| 443 | `GG_STEALSUCK_REQ` | ACK 是 score state；未有計分狀態機，不轉發 |
| 445 | `GG_STEALPUSH_REQ` | ACK 是 score state；未有計分狀態機，不轉發 |
| 718 / 721 | room vote family | 未有 voter、timer、cancel、result owner，不回成功 |
| 730–742 | Pulp’n’Roll family | 需要 object seed、room state 與可重現 captures |
| 756–776 | clan/tournament family | 需要 membership、round、entry 與 billing/state evidence |
| 902–963 | Occupy / ground-object family | 保留已知 raw framing；不虛構 reward、seed 或成功 tail |

新增 module 前，必須在 `docs/PACKETS.md`、`docs/LAYOUTS.md`（Part I/II）與 `PaperMan.exe.c` 中追完：

```text
request builder → every caller and state gate → exact reader/consumer
                → field data flow → ownership/source rule
                → mutation and response → next valid client state
```

## Verification

```bash
python3 tools/verify_dispatcher_coverage.py
python3 tools/verify_native_gates.py
python3 tools/verify_resource_claims.py
python3 tools/verify_resource_coverage.py
cd server-ts
bun run typecheck
bun test
```

若環境沒有 Bun，必須明確記錄 typecheck/test 未執行；不能用離線 Python schema
smoke test 代替 TypeScript runtime verification。