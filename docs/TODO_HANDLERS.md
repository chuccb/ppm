# Server handler 待辦清單 (廿四輪自動盤點)

> 「client 有 builder、server 尚無 handler」的 REQ 全表 — 附自動抽出
> 的寫入序列, 按此實作 handler 即可。已實作 67 個 REQ handler
> (Auth/Lobby/Shop/Stats/Clan/Quest/Friend/Room/Channel/Voice)。
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
> 下一輪可做: GM/MASTER 群 (275-299/394-416/822-831/883-885, 需權限
> 分級); matching room 群 (983/986/988); AI 模式群 (918-944);
> 130/134 的 +146/+150 原服語意 (client 存而不讀, 送 0 已安全)。

| op | 名稱 | REQ 寫入序列 |
|---|---|---|
| 103 | GE_LOGOUT_REQ | `(空)` |
| 131 | GR_FORCEOUT_REQ | `u8` |
| 214 | GM_CREATECHAR_REQ | `u8 s16 s16 s16` |
| 218 | GI_CHANGEDATA_REQ | `u8` |
| 220 | GI_CHANGEWP_REQ | `u8` |
| 230 | GP_CHLOSSC_REQ | `s32` |
| 232 | GP_CHKILLC_REQ | `s32` |
| 244 | GP_CHTKILLC_REQ | `s32` |
| 260 | GL_JOIN_REQ | `u8` |
| 262 | GL_JOINPASS_REQ | `u8 str` |
| 264 | GL_JOININFO_REQ | `u8` |
| 266 | GL_JOINGAME_REQ | `u8 u8` |
| 268 | GL_JOINPLAY_REQ | `u8 u8` |
| 275 | MASTER_MEMO_REQ | `wstr` |
| 277 | MASTER_MEMOALL_REQ | `wstr` |
| 279 | MASTER_USERCUT_REQ | `u8 str` |
| 281 | MASTER_USERCUT2_REQ | `s32` |
| 283 | MASTER_ROOMCUT_REQ | `u8` |
| 285 | MASTER_MSET_REQ | `u8` |
| 287 | MASTER_PRINTUSER_REQ | `(空)` |
| 289 | MASTER_USERINFO_REQ | `str` |
| 291 | MASTER_LISTCUT_REQ | `str` |
| 293 | MASTER_USERINFODB_REQ | `str` |
| 298 | GS_TAKEGIFT_REQ | `(空)` |
| 300 | GS_MOVEGIFT_REQ | `(空)` |
| 306 | GG_JJGET_REQ | `u8` |
| 310 | GS_BUYCHAR_REQ | `s32 s32 s32 s32 s32 s32` |
| 312 | GI_CHANGESLOT_REQ | `u8` |
| 316 | GG_HACKSTART_REQ | `u8` |
| 318 | GG_HACKSUCC_REQ | `u8 f32 f32 f32 f32 f32 f32` |
| 320 | GG_HACKFAIL_REQ | `u8` |
| 322 | GG_BOMBSUCC_REQ | `(空)` |
| 324 | GG_BOMBEND_REQ | `u8` |
| 326 | GG_UNHACKSTART_REQ | `u8` |
| 328 | GG_UNHACKSUCC_REQ | `u8` |
| 330 | GG_UNHACKFAIL_REQ | `u8` |
| 342 | GG_SOLORESPON_REQ | `s32` |
| 344 | GG_LIVECHAT_REQ | `s32 u8 str` |
| 346 | GG_TEAMCHAT_REQ | `s32 u8 str` |
| 348 | GG_DEADCHAT_REQ | `s32 u8 str` |
| 350 | GG_TEAMDEADCHAT_REQ | `s32 u8 str` |
| 358 | GS_BUYCASHITEM_REQ | `u8 s32 s32` |
| 360 | GG_TSURRESPON_REQ | `s32` |
| 370 | GL_CHANGECHANNEL_REQ | `u8` |
| 374 | GR_GETCRYSTAL_REQ | `u8` |
| 378 | GR_RADIOMSG_REQ | `u8 u8 u8 u8 rawN` |
| 394 | MASTER_ROOMINFO_REQ | `u8` |
| 398 | MASTER_SVRCLASS_REQ | `u8` |
| 400 | MASTER_CONNTYPE_REQ | `u8` |
| 402 | MASTER_EVENTPAGE_REQ | `f32` |
| 404 | MASTER_EVENTEXP_REQ | `f32` |
| 410 | MASTER_DISLOGIN_REQ | `(空)` |
| 412 | MASTER_DISGMS_REQ | `str s32` |
| 414 | MASTER_DISLOG_REQ | `str s32` |
| 416 | MASTER_KILLALL_REQ | `(空)` |
| 418 | MASTER_RESETTCPGROUPINFO_REQ | `str s32` |
| 423 | GL_MSG_READ_REQ | `str` |
| 437 | GG_ROOMBROADCAST_REQ | `u8 s32 rawN` |
| 439 | GL_FRIEND_CHAT_REQ | `s32 str str str` |
| 441 | GL_FRIEND_WHERE_REQ | `str` |
| 443 | GG_STEALSUCK_REQ | `u8 s16` |
| 445 | GG_STEALPUSH_REQ | `u8 s16` |
| 453 | GS_DELETEGIFT_REQ | `s32 s32` |
| 455 | GG_EXERCISERESPON_REQ | `s32 s8` |
| 457 | GI_CHANGEITEMSLOT_REQ | `(空)` |
| 461 | GS_USE_PAPERCODEGIFT_REQ | `str` |
| 464 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ | `u8 str` |
| 466 | GI_CHANGE_SKILLITEMSLOT_REQ | `u8 u8 u8` |
| 472 | GL_GAMECENTER_REC_REQ | `s16` |
| 474 | GG_GAMECENTER_GAME_START_REQ | `s16 u8` |
| 476 | GG_GAMECENTER_GAME_END_REQ | `s16 raw24 raw44` |
| 478 | GG_GAMECENTER_GAME_PLAY_CHECK_REQ | `raw36` |
| 480 | GG_GAMECENTER_RANKING_REQ | `s16 u8` |
| 483 | GG_GAMECENTER_GAME_START_OK_REQ | `s16` |
| 485 | GL_GET_GAMEROOM_PROGRESSTIME_REQ | `u8` |
| 571 | GV_TEST_REQ | `str str` |
| 581 | GC_CLAN_START_REQ | `(空)` |
| 685 | GL_TUTORIALINDEX_REQ | `(空)` |
| 689 | GL_TUTORIAL_INDEX_SET_REQ | `s32` |
| 697 | GG_CHEATER_REPORT_REQ | `s16` |
| 698 | GP_ENTER_PEPACHI_REQ | `(空)` |
| 700 | GP_START_GAME_REQ | `u8 s32` |
| 702 | GP_PEPACHI_LIST_REQ | `(空)` |
| 704 | GL_LEVEL_KILL_LIMIT_REQ | `(空)` |
| 706 | GL_BILLTOKEN_REQ | `(空)` |
| 708 | GL_CHECKCASHPG_REQ | `s32` |
| 714 | GG_INVALIDWPDATA_REQ | `u8 u8 u8 str s32` |
| 716 | GG_CHANGEWPQUICKSLOT_REQ | `s16 s16 s16 s16` |
| 718 | GR_START_VOTING_REQ | `s32 s32 s32` |
| 724 | GL_COMBISKILLITEM_REQ | `s32 s32 s32 s32` |
| 726 | GG_OBSERVERCHAT_REQ | `str str` |
| 730 | GG_GETPULP_REQ | `u8` |
| 733 | GG_SPAWNPULP_REQ | `(空)` |
| 736 | GG_PULPSTEAL_REQ | `u8` |
| 737 | GG_DESTROY_START_REQ | `u8` |
| 739 | GG_DESTROY_SUCC_REQ | `u8` |
| 741 | GG_DESTROY_FAIL_REQ | `u8` |
| 746 | GG_PNR_RESPON_REQ | `s32` |
| 749 | GG_GIMMICK_DAMAGE_REQ | `(空)` |
| 752 | GG_MAPINFO_RELOAD_REQ | `(空)` |
| 756 | GL_CLAN_TNMT_RECEIPT_REQ | `s32` |
| 758 | GL_CLAN_TNMT_RECEIPT_CANCEL_REQ | `s32` |
| 762 | GL_CLAN_TNMT_CURRENT_STATE_NOTICE_REQ | `(空)` |
| 764 | GL_CLAN_TNMT_ENTERROOM_REQ | `u8 s32` |
| 767 | GL_CLAN_TNTM_AWARD_INFO_REQ | `(空)` |
| 771 | GL_CLAN_TNMT_ALL_INFO_REQ | `s32` |
| 773 | MASTER_RELOAD_TNMT_REQ | `(空)` |
| 776 | GL_CLAN_TNMT_CLANREC_REQ | `(空)` |
| 785 | GL_FRIEND_ADD_PROCESS_REQ | `str` |
| 787 | GL_RACKINGWEB_TOKEN_REQ | `(空)` |
| 802 | GS_DESTROYITEM_REQ | `s32 s32 u8 s32 s32` |
| 804 | MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ | `(空)` |
| 806 | GS_HIDDEN_ITEM_LIST_REQ | `s16` |
| 808 | GS_GET_RECOMMENDSET_INFO_REQ | `s32 s32 s32 s32 s32 s32 s32 s32 s32 s32` |
| 812 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ | `s8` |
| 814 | MASTER_CHECK_BOMB_CHEATER_APPLY_REQ | `s8` |
| 819 | MASTER_CHECK_NPGAMEGUARD_QUERY_REQ | `(空)` |
| 820 | GG_CHATTING_PENALTY_REPORT_REQ | `s32` |
| 822 | MASTER_CHAT_BAN_REQ | `u8 u8 str` |
| 824 | MASTER_USERLIST_REQ | `u8 s32` |
| 830 | MASTER_CHAT_FORCE_BAN_REQ | `u8 str s32` |
| 831 | MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ | `s32` |
| 834 | GL_DATA_RECV_COMPLETED_REQ | `s32` |
| 836 | GL_SHOUTCHAT_REQ | `s32 s32 str` |
| 838 | GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ | `u8 s32` |
| 841 | MASTER_SETALL_EVENTEXP_REQ | `f32` |
| 843 | MASTER_SETALL_EVENTPAGE_REQ | `f32` |
| 845 | MASTER_VIEWALL_EVENTSTATE_REQ | `(空)` |
| 849 | MASTER_TNMT_VIEW_STATE_REQ | `(空)` |
| 871 | GQ_QUEST_SUCCESS_REQ | `raw4` |
| 873 | GQ_QUEST_COMPLETE_REQ | `raw4` |
| 876 | GQ_QUEST_ACCEPT_DAILY_REQ | `(空)` |
| 878 | GQ_QUEST_USER_COMPLETE_HONOR_REQ | `s8` |
| 883 | MASTER_FIND_USER_REQ | `s32` |
| 885 | MASTER_PLAY_WITH_REQ | `s32` |
| 887 | GX_XIGNCODE_DATA_REQ | `rawN` |
| 890 | GC_QUERY_CLANRANKING_REQ | `(空)` |
| 892 | MASTER_RELOAD_CLANRANKING_REQ | `(空)` |
| 900 | GS_CAPSULEMACHINE_START_REQ | `u8 s32` |
| 902 | GG_OCC_START_REQ | `u8 u8 s32` |
| 904 | GG_OCC_SUCC_REQ | `u8 u8 s32` |
| 906 | GG_OCC_FAIL_REQ | `u8 u8 s32` |
| 909 | GG_OCC_RESPON_REQ | `s32` |
| 912 | GL_WEAPONPARTS_EQUIP_CHANGE_REQ | `u8 s32 s32 s32 || u8 s32 s32` |
| 918 | GR_AI_GET_REWARD_ITEM_REQ | `u8` |
| 922 | GR_AI_DAMAGE_SHIELD_REQ | `s16 s16 s16 f32` |
| 924 | GR_AI_RECHARGE_MAGAZINE_START_REQ | `u8 u8 u8` |
| 926 | GR_AI_RECHARGE_MAGAZINE_END_REQ | `u8 u8 s8` |
| 928 | GR_AI_CONTINUE_START_REQ | `s32` |
| 935 | GR_AI_FEVER_START_REQ | `(空)` |
| 939 | GR_AI_GO_NEXT_WAVE_REQ | `(空)` |
| 944 | GR_RESET_GAMEROOMSLOT_REQ | `(空)` |
| 962 | GG_DROPWEAPON_GET_AND_DROP_REQ | `s16 s16 u8 s16 s16 f32` |
| 964 | GG_GET_BALL_REQ | `(空)` |
| 967 | GG_GET_GOAL_REQ | `(空)` |
| 971 | GG_SOCCER_RESPON_REQ | `s32` |
| 983 | GL_MATCHINGROOM_MAKE_REQ | `u8 str s32 u8 u8 u8 u8 u8 u8 u8 u8 u8` |
| 988 | GL_MATCHINGROOM_CANCLE_REQ | `(空)` |
