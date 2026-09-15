# Server handler 待辦清單 (廿四輪自動盤點)

> 「client 有 builder、server 尚無 handler」的 REQ 全表 — 附自動抽出
> 的寫入序列, 按此實作 handler 即可。已實作 120+ 個 REQ handler (本輪起以 Router 實際註冊數為準)
> (Auth/Lobby/Shop/Stats/Clan/Quest/Friend/Room/Channel/Voice/
> BattleRelay/Warehouse/Join)。
>
> 本輪 (房間設定/聊天簇) 新增: 139 GG_EXITGAME、167/169/171/173/175
> /177、340/364/712、728 觀戰聊天、990 GR_DAMAGEROOM (db/packets.tsv
> 補 990/991 後命名) — 全數限房主、ACK 同值廣播, 詳 docs/PACKETS.md
> §3.15b2。177/178 為 client 死碼 (無呼叫者/無 dispatcher case)。
>
> 四十輪: 169/170 已鏡像 mode→預設圖 (system/map_StartIndex.xml);
> +146/+150 定案為「wire 送、client 存而不讀」的 mode param (送 0 安全)。
>
> 卌二輪 (隊打散簇落地): 368/369 (u8→mode+13)、894/895 (u8 room_no +
> u8 map; ACK status 1 逐槽重排 u8 slot + u32 uid)、366/367 區域房、
> 969/970 足球開關 — 全數限房主、ACK 同值/逐槽廣播。112 GL_MAKEROOM_ACK
> 補完 15 欄 (尾 9 欄 team_mode + 2×team 資料必送)。
>
> 卌三輪 (Extracted 深挖 + 原語定型): 895 的 u16「讀後丟棄」欄定型為
> 純保留欄 (sub_592A00 讀入 v35[6] 後無引用, 送 0); 366/969 的
> n2_0!=3 定案 = 錦標賽場景 (dword_EA0F30) 不發, 一般房
> (dword_EA10D0) 才發; 原語寬度表定型 (sub_592940/592900/592980=u8,
> sub_592A00/5929C0=u16, sub_592A40/592AC0/592A80=u32,
> sub_592730/5926F0=str) — 895 每槽 (u8 slot, u32 uid) 確認無誤。
> msgtableres.lang 解碼完成 (CP932/LF/+3 偏移, 1346 條) — 895 錯誤碼
> 文字已補 §3.15b2, 全表 851 個 sub_408080 引用可查 RESOURCES.md §8。
>
> 卌四輪 (190/191/192/194 反編譯解碼): lobby channel dispatcher 即
> sub_54D040 (case 193/194 → sub_54E020/sub_54EE10); 194 兩場景兩解讀
> (一般場景 sub_56FE90 5×u8; 戰隊頻道 sub_54EE10 u32 n2)。
>
> 卌五輪 (落地 191/192/193/194 + mode+12 定案):
> ① 190 新房主廣播 (Rooms.RemoveMemberAsync) + 123/124 離房改用共用
>   RemoveMemberAsync (房主離房一併廣播新房主, 否則新房主不戴皇冠);
> ② 191 GR_CALLUSER_REQ (str nick) → 192 (u8 slot + str nick) 落地,
>   新增 SessionRegistry (nick→Session 反查, TryRemove(key,value) 防
>   同名誤刪); 192 依接收者房狀態==2 才帶 body (sub_56FE10);
> ③ 193 GC_CHANNEL_REQ (u32 n2) → 194 回 u32 2 (重置; 私服無戰隊);
> ④ mode+12 定案 =「是否隊伍房」(sub_56A7B0 建房時 sub_438990?1:0),
>   已取代 130/134/114/108 四處原本送 0 的寫法; +146/+150 確認 client
>   存而不讀 (全 exe 無讀者) 送 0 安全。
>
> 卌六輪 (資源盤點 + README 整理): ① mode 枚舉定案 — sub_53FBB0
> factory 15 個 CyGameModes 類 (0..13/15; 14/16 無效), 兩隊制集合
> {0,2,3,4,8,10,11,12,13} 與 maplist `modes` bitmask 的 bit↔mode
> 對照表已更正在 RESOURCES.md §4b; ② 版控新增 msgtableres.lang、
> roommake.xml/gameroom.xml、slanderfilter 過濾詞 (見 README);
> ③ 111 建房/169 改模式的 mode 值現在可用 map_catalog.modes
> (bitmask) 做「該模式可選地圖」驗證, server 尚未做 — 列下一輪。
>
> 卌七輪 (111/169 地圖過濾 + 語音簇定案):
> ① 111/121 依 mode→bit 過濾可選地圖落地 — ModeMapBit (mode→bit,
>    見 RESOURCES.md §4b) + ResolveMap: 建房/換圖時該 mode 不支援的
>    地圖回退 mode 預設圖 (ModeDefaultMap); map_catalog 查無/mode 無
>    規則時原樣放行 (不硬編)。Db.GetMapModes 讀 map_catalog。
> ② 語音 791–796 wire 佈局全定案並更正 docs (792=整塊覆寫、794=u8
>    count 前綴, 非先前誤判的 795 變體鏡像); Handlers.Voice.cs 改為
>    真解析 795 兩變體 (依長度 1780B 判別) 並落地 voice_customize/
>    voice_slots (char_idx 0..14 + 2×base_voice + 27×(item,flag))。
>    792 的 char_idx 由 users.current_char 的 char_type 推出。
>
> 卌八輪 (倉庫簇 855-863 落地): 856/863 的 70B 狀態塊定案為
> 7×10B {s32 count, s32 到期(位元打包日期 sub_48B9A0), u8 loaded, u8
> pad}; 到期欄非秒數而是 (年-2000)<<24|月<<19|日<<13|時<<7|分。
> 859/861 REQ = u8 tab + s32 slot (5B); 860/862 ACK 6B header (u8 err,
> u8 tab, s32 slot) + 28B 物品 + s32 tab_count。862 err 5 = 背包滿
> (0x49E), 1/2/3/4/6/7 = 0x49A。落地 Db.Warehouse.cs (warehouse_items
> /warehouse_lockers, 頁籤預設全持有) + Handlers.Warehouse.cs。
> 855 的 s32 = dword_EE8CB4 自己 uid (驗證用, 私服以 session 為準)。
>
> 卌九輪 (GL_JOIN 簇 260-269 落地): 房單進房流程逐函數定案 — 260/262
> /264 REQ = u8 / u8 str / u8; 265 ACK 10 欄頭 + count×(slot,str,u8),
> 其中 +408/+410/+416 全程無讀者 (送 0 安全), +409 map/+411 time/
> +412 round/+414 item 由 sub_515DE0 顯示; 267 = u8 code u8 flag
> (code 0=玩家/1=觀戰); 268 = u8 room_no u8 flag (0=PLAY/1=OBSERVE);
> 269 code 6=玩家自身快照 / 7=觀戰全房快照。落地 Handlers.Join.cs:
> 260/262/264 → 261/263/265 完整 (房單進房 + 密碼關卡 + 房資訊);
> 266 → 267 依 flag 回 code; 268 → 269 回 code 0 (無遊戲狀態機,
> code 6/7 成功態留待後續, 不硬編未確認欄位)。詳 PACKETS.md §3.15f。
>
> 五十輪 (comm 簇落地 — 437/438、378/379、726/727、836/837、439-442):
> 逐函數定案並實作 — 378/379 radio (u8 team, u8 face 頁*9+項目, u8 slot,
> u8 len≤64, wchar[len]; REQ/ACK 同構原樣轉播全房); 726/727 對戰觀戰聊天
> (str nick + str message, ⚠ ANSI 與 728 的 wstr 不同); 437/438 房廣播
> (u8 flag + s32 len + raw, flag 語意無從確認 — builder 無呼叫者、client
> 無 438 case, 原樣轉播); 836/837 喊話 (s32 uid + s32 strlen + str text →
> u8 flag + s32 uid + s32 timer + str nick + s32 raw_len + raw, timer=1
> 非0 保持可喊, client 3s 牆鐘限流); 439/440 好友聊天 (s32 uid + 3×str →
> u8 status + 2×str [+comment], status 0/1/2/3 = 不存在/離線/訊息/找不到,
> 上線遞送+回聲); 441/442 好友位置 (str nick → u8 status [+3×u8], 私服
> 一律大廳/找不到)。msgtableres 0x1EF/0x1F0/0x1D8/0x1D9/0x21D/0x21E/
> 0x314/0x3AF 文字已補 RESOURCES.md §8。詳 PACKETS.md §3.15c + §3.15g。
>
> 五十一輪 (GG 戰鬥中繼重驗 — 更正廿五輪「slot 前綴」簡化, 修兩處潛伏
> bug): ① **139 GG_EXITGAME 重複註冊** — BattleRelay 與 Room 都 add(139),
> Router.Dictionary.Add 會擲例外 → 啟動即崩 (本機無 dotnet 從未跑過, 潛伏);
> 已自 BattleRelay 移除 (Room 的 ExitGame 才是正主)。② **344/346/348/350
> 聊天 relay 用 REQ opcode 廣播** — client 只對 345/347/349/351 有
> dispatcher case, REQ opcode 會被忽略 → 已改以 ACK opcode 廣播。
> ③ TH 簇 316-331 逐函數定案: REQ 首欄是 team 非 slot; ACK = REQ + 尾附
> slot (321/323 例外); 322 空 REQ → 323 [BombTeam] (Room.BombTeam 只於 318 武裝成功記下)。
> ④ 足球 964/967 空 REQ → ACK [u8 flag=0, u8 slot]。⑤ 奪寶 443/445/447
> ACK 是分數組 (u8,u8,u16×3), 需計分狀態機 → 自轉發器移除 (不註冊,
> 送錯比不送糟)。⑥ BattleRelay 全面改 TryFindRoomSlot 清晰寫法。
> 詳 PACKETS.md §3.15d3。
>
> 五十二輪 (sub_885D00 語音全鏈閉環 + 114/269/765/985 負載尾塊補齊):
> ① **sub_885D00 呼叫全景定案**: 全 exe 共有 6 處呼叫 — 114 (GR_ENTERROOM_ACK)、
>    269 (GL_JOINPLAY_ACK)、765 (GL_CLAN_TNMT_ENTERROOM_ACK)、985
>    (GL_ENTERMATCHINGROOM_ACK)、792 (GL_VOICEITEMSLOT_ACK) 與 796
>    (GI_CHANGE_VOICEITEMSLOT_ACK 失敗回滾)。
> ② **語音塊 wire 佈局**: mode 2 (房間成員) 讀 85B (`s16 base1, s16 base2,
>    27×{s16 voice_item, u8 flag}`); mode 1 (792 ACK) 讀 86B (`u8 char_idx`
>    + 85B 塊); 794 ACK 讀 `u8 count` + count×86B (1291B)。
> ③ **補齊 114 / 269 / 765 / 985 負載尾塊**: Handlers.Room.cs 的
>    WriteMemberLoadout 末尾補上 WriteVoiceBlock (85B); 修正 114 進房
>    解析偏移。
> ④ **269 GL_JOINPLAY_ACK 成功態全落地**: 268 PLAY(flag=0) → code 6
>    (自身完整快照, 含 85B 語音塊); 268 OBSERVE(flag=1) → code 7
>    (全房+全成員快照, 含 85B 語音塊)。
> ⑤ **錦標賽 764/765 與配對房 983/984/988/989 落地**:
>    764 → 765 (與 114 同構, 錦標賽進房); 983 → 984 (配對建立);
>    988 → 989 (配對取消)。
> ⑥ **自測與 DB 測試**: SelfTest 增測 791-796 / 378 / 114 / 269 語音塊;
>    smoke_test.py 增測 voice_customize 與 voice_slots CRUD 及約束。
>
> 五十三輪 (系統/角色/商城/信件/投票/轉蛋 22 個 REQ 全面落地):
> ① **教學與系統限制 (685/686, 689/690, 704/705, 706/707, 787/788, 834/835, 370/371)**:
>    - 685/686: 教學索引查詢 (s32 flags1); 689/690: 教學進度設置;
>    - 704/705: 殺敵上限與經驗倍率 (s32 kill_limit 50, f32 exp 1.0, s32 max_lv 30);
>    - 706/707 & 787/788: 商城計費 Token 與排行榜 Token 頒發;
>    - 834/835: 資料接收完成 ACK; 370/371: 頻道切換 (u8 status 1, u8 ch, ip, port, extra)。
> ② **角色與裝備 (214/215, 218/219, 220/221, 312/313, 466/467, 912/913, 310/311)**:
>    - 214/215: 角色初創 (u8 char_type, 3×s16 equip);
>    - 218/219 & 312/313: 角色槽切換 (u8 slot_no → users.current_char);
>    - 220/221: 武器組 delta 寫入、全量 4×weapon_groups ACK，且驗證 owned/unexpired item 和 `weaponparts.pat` compatibility；
>    - 466/467: NewSkill 五 profile 切換／上一 profile 七 puzzle IDs 持久化；255 snapshot 與 raw packed-minute expiry 見 `PACKETS.md`（不寫入 9-slot `skill_slots`）;
>    - 912/913: **尚未實作**。已確認 op 0 remove / 1 install / 2 replace 和 913 `errorRaw` gate；原服 error values、授權及到期/持久化效果仍需確認，不可回假成功。
>    - 310/311: 購買新角色 (characters 插入, users.game_point 扣款)。
> ③ **商城/背包/信件/任務 (453/454, 802/803, 423/424, 876/877, 878/879)**:
>    - 453/454: REQ/ACK wire and client cache key are recovered, but original gift-state/authority policy is not. Server validates exact `{s32 giftId,s32 itemId}` then returns a non-mutating `result=0` echo; it must not delete SQLite gifts before 298/300/315 are reconciled.
>    - 802/803: request layout remains **UNRESOLVED**. The server deliberately emits only the fully evidenced no-mutation failure `{u8 nonzero_result,u8 raw_code,u8 affected_count=0}`; it does not delete or decrement inventory.
>    - 423/424: 信件標記已讀 (messages.is_read = 1);
>    - 876/877: 每日任務接取 (回傳 13B 任務結構);
>    - 878/879: 榮譽任務完成確認 (榮譽標題與稱號)。
> ④ **轉蛋機與膠囊機 (698-703, 900/901) — 後續證據已推翻當時的成功實作敘述**:
>    - 698/699 僅確認 entry response 的 `u8,s32,s32` consumer；兩個 s32 不是已證實的 coins/cash。
>    - 702/703 的 exact wire 是 `{s32 start,s32 count,(start+count)×s16}`，不是 normal/rare item lists。
>    - 700/701 的 exact request is `{u8 selector,s32 selectedCharacterId}`; success contains a variable reel/prize sequence. No debit/reward policy is recovered.
>    - 900/901 的 exact request is `{u8 paymentSelector,u8 drawCount}`; ACK carries a count-driven variable result plus three trailing state words. No token debit/reward policy is recovered.
>    - Current server only emits client-safe failure arms with no mutation; see `PACKETS.md` §3.98a.
> ⑤ **房間管理與投票 (131/132, 718-722)**:
>    - 131/132: 房主強制踢人 (廣播 132 ACK 並移除 slot 成員);
>    - 718-722: 踢人投票流程 (718 REQ → 719 ACK → 720 全房倒數廣播 → 721 表決 → 722 結算)。
> ⑥ **自測與驗證**: SelfTest 增測 23 項封包編解碼; smoke_test 增測 Step 14 CRUD; 全測試 100% 通過。
>
> 五十五輪 (GM / MASTER、GameCenter 迷你遊戲、PVE / AI 防衛戰 37 個封包全鏈落地):
> ① **GM / MASTER 管理指令簇 (Handlers.Master.cs)**:
>    - 275/276 (MEMO 私訊)、277/278 (MEMOALL 全服廣播)、279/280 (USERCUT 踢線)、
>      281/282 (USERCUT2 依 UID 踢線)、283/284 (ROOMCUT 解散房間)、285/286 (MSET GM旗標)、
>      287/288 (PRINTUSER 在線人數)、289/290 (USERINFO 查玩家)、291/292 (LISTCUT)、
>      293/294 (USERINFODB)、394/395 (ROOMINFO 房成員與IP)、402/403 (EVENTPAGE PG倍率)、
>      404/405 (EVENTEXP EXP倍率)、416/417 (KILLALL 全服維護踢線)、822/823 (CHAT_BAN 禁言)、
>      824/825 (USERLIST 玩家清單)、830/831 (CHAT_FORCE_BAN)、841/842 (SETALL_EXP)、
>      843/844 (SETALL_PAGE)、845/846 (VIEWALL_EVENTSTATE)、883/884 (FIND_USER 查房號)、
>      885/886 (PLAY_WITH 瞬移進房)。
> ② **遊戲中心 GameCenter 迷你遊戲協定 (Handlers.GameCenter.cs + Db.GameCenter.cs)**:
>    - 472/473: 紀錄查詢 (高分/排名/遊玩次數);
>    - 474/475 & 483/484: 迷你遊戲開始與確認;
>    - 476/477: 遊戲結算與高分落庫、PG/EXP 獎勵派發 (32B/44B 結構體對齊);
>    - 478/479: 防作弊心跳檢查;
>    - 480/481: 迷你遊戲 TOP 10 / TOP 3 排行榜查詢 (0x38 條目結構);
>    - 485/486: 戰局進行時間查詢 (sub_56AE30 動態時鐘同步)。
> ③ **AI / PVE 防衛戰模式協定 (Handlers.Ai.cs)**:
>    - 918/919: PVE 結算道具抽取 (8B 抽獎結果同步);
>    - 922/923: 核心防護罩受損廣播 (shield_id, damage, remain);
>    - 924/925: 彈藥補給開始全房廣播;
>    - 926/927: 彈藥補給完成全房廣播;
>    - 928/929: 接關復活 (continue_count 同步);
>    - 935/936: Fever 狂暴狀態觸發廣播;
>    - 939/940: 波次推進 (Wave 切換與計時);
>    - 944/945: 房間槽位重置。
> ④ **自測與 DB 測試**: `Db.GameCenter.cs` 擴充個人紀錄與排行榜查詢; `smoke_test.py` 增測 Step 15-16; 測試全數通過。
>
> 五十六輪 (OCC 902–907 狀態機 + 962 安全拒絕):
> ① 以 builder、`sub_58B010`、parser 三向驗證 902/904/906 的同構 6B
>    REQ（`point_id, claimed_slot, claimed_user_id`）及 903/905/907 ACK。
>    新增 `Handlers.BattleObjects.cs` 和每房 `RoomBattleState`：只接受
>    playing 的 Occupy/OccupyRenewal 房內成員，slot/uid 必須與 session 一致；
>    start→success/fail 在 room lock 內原子轉換，GR_START/GR_END 皆清除狀態。
> ② 962 的 request 13B 序列與 963 的條件式 1B/17B/59B ACK 已確認。
>    959/961 沒有可驗證的 server-side seed 前，handler 只給請求者 `result=1`
>    （client 已證任何非零均不讀 success tail），禁止虛構掉落物成功包。
> ③ 更正 LAYOUTS.md：903 實際為 8B 而非誤列的 12B；959/961 補完整 raw32，
>    960/961 count/0-id early-stop 與 963 optional tail 全記錄。SelfTest 加入
>    902/903/905/907/962/963（含 963 的 1B/59B 分支）wire round-trip。詳
>    PACKETS.md §3.15d3a。
>
> 五十七輪（零參數 SQLite bootstrap / account migration）:
> ① `PaperMan.Server` 將 `db/schema.sql` 與 `db/packets.tsv` 編入 assembly；`Db`
>    第一次開啟時建立 parent directory、SQLite 檔案、idempotent schema、676 筆
>    protocol catalog 與只補不存在 key 的 17 項 `server_config` 預設值。這不是
>    runtime shell-out Python，故 publish 輸出也能自行初始化。
> ② 不再讀取 database / port / cipher command-line arguments：開發時可靠地定位 repo
>    `db/paperman.db`，publish 時用 executable 旁 `data/paperman.db`；只有 operator
>    明確設定 `PAPERMAN_DATABASE_PATH` 才改位置。啟動 banner 顯示解析後路徑與
>    bootstrap 結果，便於排查錯開資料庫。
> ③ 帳密新 row 使用 PBKDF2-SHA256（210,000 iterations、per-account random salt）；
>    遺留 `SHA256(salt + password)` 僅在驗證成功後升級。682 的 raw24 fingerprint
>    legacy columns 會安全 migration，並在 migration 後以 trigger 保留長度不變量。
>    `CreateNick` 改為 user、trigger bootstrap、starter character 同一 transaction，
>    不留下半建好的 identity；unknown opcode 統計以 SQL existence guard 忽略而不破壞
>    router 的 native-like 靜默行為。
> ④ SelfTest 新增 temporary-path first/second-open 自動建庫測試；`smoke_test.py`
>    新增 682 raw24 schema constraint test。仍須在有 .NET 10 SDK 的環境做完整 build
>    與 SelfTest 實跑（本輪文件不得把未跑結果稱作通過）。
>
> 五十八輪（登入／頻道 state boundary 再整理）:
> ① **Fact / HIGH**：重新逐行核對 `CLobbyLogin::sub_43E500`：694 讀完 u16
>    後唯一直接呼叫 `sub_43DF00`（682 builder）；681 low-byte 1 讀完 success
>    tail 後設 `this+131=1` 並呼叫 `sub_43E450`。同一正常 login conversation
>    沒有第二個 682 builder call。
> ② 因此 server 以 **Inference / HIGH** 拒絕成功 681 後的重複 682，防止越序
>    request 覆寫已綁定 account。143 成功現在只建立 channel handoff；195 的
>    成功 196 寫入完成後才設 `Session.ChannelEntryCompleted`，此前 Router 僅允許
>    ping / 143 / 195。這是 state-machine guard，不是宣稱已還原原廠 server code。
> ③ 674 次 `sub_9EAF50` direct call 已用 C export 範圍重新計數；catalog 的 676
>    unique rows 包含未在 name registry 的 990/991 補名。`PACKETS.md` 現分開
>    記錄這兩個數字，避免將 inference 混成 source fact。
> ④ 必須在有 .NET 10 SDK 及真實 client 的環境驗證：正常 143→195→196→107
>    應仍可完整進大廳；錯誤 144 後的 195/196 形狀與重複 682/195 的實際 native
>    error/disconnect 行為仍屬 **UNRESOLVED**，不可虛構 failure response。
>
> 下一輪可做:
> 1. 取得一組已知正常及一組拒絕的 681→143→144→195→196 實包，定位
>    `String[24]` 的 writer（仍不能猜為 account/nickname）、681 extension 的兩個
>    s32、尾端 billing s32×2、144 的兩個 read-but-unused raw4 與 propagated
>    `dword_F2A684` server-domain meaning。144 的 daily PG、rank flag、level/KD
>    restrictions、net-café 4×u8+8×raw4 shape，及 142 calendar 已經 source-verified，
>    不再列為未知。
> 2. 已實作並下發 source-proven AES-only UDP-private 19→empty-20 control
>    endpoint；它不是 relay。後續必須先逐一追完 `sub_595E80` 各 case、371 的
>    secondary socket、`sub_596330` send callers 與 remote-address/correlation
>    data flow，才能判斷是否有可實作的 relay。確認 141 的 packed wall-clock 與
>    部署時區預期；也以實包驗證 684
>    `GL_LOGIN_DUPLICATE` 的方向與 payload（現有 C export 沒有可歸屬的 builder/
>    reader，不能猜測發送）。
> 3. 補 type-3 channel 的 `sub_875680` 196 AI tail；在完整 reader/writer與可重現
>    AI config 前，保持 `ServerConfig` 拒絕 type-3，而不送 truncated success tail。
> 4. 取得 Pulp’n Roll 733/734 initial-state 和 959/961 ground-weapon 實包，建立
>    可重現的 per-room object seed，才實作 730–742 Pulp 與 962 成功交換。
> 5. 精讀並實作戰隊錦標賽進階流程 (756–776)：先對每項 builder/dispatcher/
>    parser 做欄位對照，再決定使用既有 `clan_tournaments`/entries schema 的範圍。
> 6. 以 OCC 實包驗證 `CaptureParticipantCount` 是否可由位置聚合增加到 2，以及
>    908 的可發送條件；目前只有單一已驗證 start actor，不能硬編成 team/slot。

| op | 名稱 | REQ 寫入序列 |
|---|---|---|
| 103 | GE_LOGOUT_REQ | `(空)` |
| 230 | GP_CHLOSSC_REQ | `s32` |
| 232 | GP_CHKILLC_REQ | `s32` |
| 244 | GP_CHTKILLC_REQ | `s32` |
| 298 | GS_TAKEGIFT_REQ | `(空)` |
| 300 | GS_MOVEGIFT_REQ | `(空)` |
| 306 | GG_JJGET_REQ | `u8` |
| 324 | GG_BOMBEND_REQ | `u8` |
| 358 | GS_BUYCASHITEM_REQ | `u8 count, count×{s32 itemId,s32 clientCalculatedPrice}` |
| 374 | GR_GETCRYSTAL_REQ | `u8` |
| 398 | MASTER_SVRCLASS_REQ | `u8` |
| 400 | MASTER_CONNTYPE_REQ | `u8` |
| 410 | MASTER_DISLOGIN_REQ | `(空)` |
| 412 | MASTER_DISGMS_REQ | `str s32` |
| 414 | MASTER_DISLOG_REQ | `str s32` |
| 418 | MASTER_RESETTCPGROUPINFO_REQ | `str s32` |
| 443 | GG_STEALSUCK_REQ | `u8 s16` — ACK 444=u8,u8,u16×3 分數組, 需計分狀態機 (勿轉發) |
| 445 | GG_STEALPUSH_REQ | `u8 s16` — ACK 446=u8,u8,u16×3 分數組, 需計分狀態機 (勿轉發) |
| 457 | GI_CHANGEITEMSLOT_REQ | `9×s32` (36B); ACK needs exact 63B tail producer semantics before implementation |
| 461 | GS_USE_PAPERCODEGIFT_REQ | `str` (native UI sends only a 16-character upper-case ASCII alphanumeric code; validity, lockout, entitlement, and grant policy remain server-side unresolved) |
| 464 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ | `u8 duplicateChoice; choice==1 → str` (GET=1, CANCEL=0; cancel is one byte) |
| 571 | GV_TEST_REQ | `str str` |
| 581 | GC_CLAN_START_REQ | `(空)` |
| 697 | GG_CHEATER_REPORT_REQ | `s16` |
| 708 | GL_CHECKCASHPG_REQ | `s32` |
| 714 | GG_INVALIDWPDATA_REQ | `u8 u8 u8 str s32` |
| 716 | GG_CHANGEWPQUICKSLOT_REQ | `s16 s16 s16 s16` |
| 724 | GL_COMBISKILLITEM_REQ | `s32 s32 s32 s32` |
| 730 | GG_GETPULP_REQ | `u8` |
| 733 | GG_SPAWNPULP_REQ | `(空)` |
| 736 | GG_PULPSTEAL_REQ | `u8` |
| 737 | GG_DESTROY_START_REQ | `u8` |
| 739 | GG_DESTROY_SUCC_REQ | `u8` |
| 741 | GG_DESTROY_FAIL_REQ | `u8` |
| 749 | GG_GIMMICK_DAMAGE_REQ | `(空)` |
| 752 | GG_MAPINFO_RELOAD_REQ | `(空)` |
| 756 | GL_CLAN_TNMT_RECEIPT_REQ | `s32` |
| 758 | GL_CLAN_TNMT_RECEIPT_CANCEL_REQ | `s32` |
| 762 | GL_CLAN_TNMT_CURRENT_STATE_NOTICE_REQ | `(空)` |
| 767 | GL_CLAN_TNTM_AWARD_INFO_REQ | `(空)` |
| 771 | GL_CLAN_TNMT_ALL_INFO_REQ | `s32` |
| 773 | MASTER_RELOAD_TNMT_REQ | `(空)` |
| 776 | GL_CLAN_TNMT_CLANREC_REQ | `(空)` |
| 785 | GL_FRIEND_ADD_PROCESS_REQ | `str` |
| 804 | MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ | `(空)` |
| 806 | GS_HIDDEN_ITEM_LIST_REQ | `s16 category` (native shop callers use 1–24; parts-room initialization uses 25; response production/filter policy unresolved) |
| 808 | GS_GET_RECOMMENDSET_INFO_REQ | `s32 count, count×s32 recommendationId`; native sender emits only when `count>0` |
| 812 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ | `s8` |
| 814 | MASTER_CHECK_BOMB_CHEATER_APPLY_REQ | `s8` |
| 819 | MASTER_CHECK_NPGAMEGUARD_QUERY_REQ | `(空)` |
| 820 | GG_CHATTING_PENALTY_REPORT_REQ | `s32` |
| 831 | MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ | `s32` |
| 838 | GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ | `u8 s32` |
| 849 | MASTER_TNMT_VIEW_STATE_REQ | `(空)` |
| 871 | GQ_QUEST_SUCCESS_REQ | `raw4` |
| 873 | GQ_QUEST_COMPLETE_REQ | `raw4` |
| 887 | GX_XIGNCODE_DATA_REQ | `rawN` |
| 890 | GC_QUERY_CLANRANKING_REQ | `(空)` |
| 892 | MASTER_RELOAD_CLANRANKING_REQ | `(空)` |

