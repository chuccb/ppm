# REQ builder 全 261 opcode 自動寫入序列表 (廿四輪)

> 對每個 `Packet::ctor(N)` 呼叫點抽出寫入原語序列 (多變體以 || 併列)。
> 抽查 682/204/208/210/212/230/585 等與手工版吻合; 並修正 119 (兩變體,
> 完整版=s32+str+wstr 與 120 ACK 同構) 與 585 (尾欄 s32 非 u8)。
> 型別: u8=592920, s8=5928E0, u16=5929A0, s16=5929E0, s32=592A20/AA0,
> u32=592A60, u64=592AE0, f32=592B20, str=5926F0, wstr=592770, rawN=592580。
>
> **登入轉接欄位限制（2026-09 重新交叉確認）**：682 是嚴格的
> `str account, str password_or_token, u64 packed_data_revision, u8 fingerprint_source,
> raw24 fingerprint`，沒有 optional tail。143 的 `str` 源自 native
> `String[24]`（內容最多 23 ANSI bytes），後三欄依序為 681 回送的 `n100`
> （經 signed-char 暫存再以 s32 寫出）、常數 `u8 1`、681 `ext_count`。
> 因此 143 是新 channel TCP connection 對最近 681 的 handoff claim，而不能
> 當作已驗證登入本身。141 是 private UDP op18 觸發的 empty endpoint-confirmation
> request（`sub_556530`）；195 三個 byte 依序為 681 的 group、group-local
> channel index、replay-module availability（`sub_56FF40`）。

| op | 名稱 | 出現處 | 寫入序列 (變體) |
|---|---|---|---|
| 101 | GT_PING_REQ | 1處 | `(空)` |
| 103 | GE_LOGOUT_REQ | 1處 | `(空)` |
| 105 | GL_USERLIST_REQ | 1處 | `s8` |
| 107 | GL_GAMEROOMINFO_REQ | 1處 | `(空)` |
| 111 | GL_MAKEROOM_REQ | 1處 | `u8 s8 u8 s8 str str u8 u8 u8 u8 u8 s8 u8 s8 str u8 u8 u8 u8` |
| 113 | GL_ENTERROOM_REQ | 1處 | `u8` |
| 119 | GL_CHATTING_REQ | 2處 | `s32 str wstr \|\| str` |
| 121 | GR_MAPCHANGE_REQ | 1處 | `u8` |
| 123 | GR_LEAVE_REQ | 1處 | `(空)` |
| 125 | GR_CHATTING_REQ | 2處 | `s32 u8 wstr \|\| s32 u8 str` |
| 127 | GR_READY_REQ | 1處 | `(空)` |
| 129 | GR_START_REQ | 1處 | `u8` |
| 131 | GR_FORCEOUT_REQ | 1處 | `u8` |
| 133 | GR_END_REQ | 1處 | `(空)` |
| 135 | GR_CHANGESLOT_REQ | 1處 | `u8 u8` |
| 139 | GG_EXITGAME_REQ | 2處 | `(空)` |
| 141 | PM_CONNECT_REQ | 1處 | `(空)` |
| 143 | PM_UDPSTART_REQ | 1處 | `str s32 u8(固定 1) s32` |
| 165 | Y_TCP_INF_REQ | 17處 | `u8 u8 s32 u16 u16 u16 u16 u16 u16 u16 u8 u8 u8 u8 s16 u8 s16 s16 f32 u16 \|\| u8 u8 u8 u8 u8 s16 f32 f32 f32 f32 s32 s32 u8 u8 u8 u8 s32 u8 \|\| u8 u8 u8 u8 s16 f32 f32 u8 u8 u8 s32 s32 u8 u8 u8 u8 s32` |
| 167 | GR_CHANGEUSER_REQ | 1處 | `s16` |
| 169 | GR_RULECHANGE_REQ | 1處 | `u8` |
| 171 | GR_WINCHANGE_REQ | 1處 | `s16` |
| 173 | GR_TIMECHANGE_REQ | 1處 | `u8` |
| 175 | GR_ITEMCHANGE_REQ | 1處 | `u8` |
| 177 | GR_AUTOCHANGE_REQ | 1處 | `s8` |
| 183 | GR_ENDLOADING_REQ | 1處 | `(空)` |
| 187 | GG_STARTGAME_REQ | 1處 | `(空)` |
| 191 | GR_CALLUSER_REQ | 1處 | `str` |
| 195 | GC_ENTERCHANNEL_REQ | 1處 | `u8 u8 u8` |
| 197 | GL_MYINFO_REQ | 1處 | `(空)` |
| 199 | GL_MYITEM_REQ | 1處 | `(空)` |
| 204 | GS_BUYITEM_REQ | 1處 | `u8 s32 u8 s16 s16` |
| 206 |  | 1處 | `s32 s32 u8 s32` |
| 208 | GS_SELLITEM_REQ | 1處 | `s32` |
| 210 | GM_CHECKNICK_REQ | 1處 | `str` |
| 212 | GM_CREATENICK_REQ | 1處 | `str` |
| 214 | GM_CREATECHAR_REQ | 1處 | `u8 s16 s16 s16` |
| 216 | GL_ENTERROOMPASS_REQ | 1處 | `u8 str` |
| 218 | GI_CHANGEDATA_REQ | 1處 | `u8` |
| 220 | GI_CHANGEWP_REQ | 3處 | `u8 count, count×weaponGroup`; group = `u8 group, u16 primary, [3×u16 when group!=3], [8×s32 parts when primary!=0]` |
| 230 | GP_CHLOSSC_REQ | 1處 | `s32` |
| 232 | GP_CHKILLC_REQ | 1處 | `s32` |
| 244 | GP_CHTKILLC_REQ | 1處 | `s32` |
| 246 | GL_CLIENTINFO_REQ | 1處 | `str` |
| 250 | GL_LOBBYIN_REQ | 2處 | `(空)` |
| 252 | GL_SHOPIN_REQ | 1處 | `(空)` |
| 254 | GL_INVENIN_REQ | 1處 | `u8` |
| 260 | GL_JOIN_REQ | 1處 | `u8` |
| 262 | GL_JOINPASS_REQ | 1處 | `u8 str` |
| 264 | GL_JOININFO_REQ | 1處 | `u8` |
| 266 | GL_JOINGAME_REQ | 1處 | `u8 u8` |
| 268 | GL_JOINPLAY_REQ | 1處 | `u8 u8` |
| 270 | GL_MYINFO_OPEN | 1處 | `s8` |
| 271 | PM_TSPOSUPDATE_REQ | 1處 | `u8 str u8 u16 u16 u16 s32 u8` |
| 275 | MASTER_MEMO_REQ | 1處 | `wstr` |
| 277 | MASTER_MEMOALL_REQ | 1處 | `wstr` |
| 279 | MASTER_USERCUT_REQ | 1處 | `u8 str` |
| 281 | MASTER_USERCUT2_REQ | 1處 | `s32` |
| 283 | MASTER_ROOMCUT_REQ | 1處 | `u8` |
| 285 | MASTER_MSET_REQ | 1處 | `u8` |
| 287 | MASTER_PRINTUSER_REQ | 1處 | `(空)` |
| 289 | MASTER_USERINFO_REQ | 1處 | `str` |
| 291 | MASTER_LISTCUT_REQ | 1處 | `str` |
| 293 | MASTER_USERINFODB_REQ | 1處 | `str` |
| 295 |  | 1處 | `(空)` |
| 296 | GS_GIVEGIFT_REQ | 2處 | `str u8 str u8 s32 u8 u8 u16 \|\| s32 u8 u8` |
| 298 | GS_TAKEGIFT_REQ | 1處 | `(空)` |
| 300 | GS_MOVEGIFT_REQ | 1處 | `(空)` |
| 306 | GG_JJGET_REQ | 1處 | `u8` |
| 310 | GS_BUYCHAR_REQ | 1處 | `s32 s32 s32 s32 s32 s32` |
| 312 | GI_CHANGESLOT_REQ | 1處 | `u8` |
| 316 | GG_HACKSTART_REQ | 1處 | `u8` |
| 318 | GG_HACKSUCC_REQ | 1處 | `u8 f32 f32 f32 f32 f32 f32` |
| 320 | GG_HACKFAIL_REQ | 1處 | `u8` |
| 322 | GG_BOMBSUCC_REQ | 1處 | `(空)` |
| 324 | GG_BOMBEND_REQ | 1處 | `u8` |
| 326 | GG_UNHACKSTART_REQ | 1處 | `u8` |
| 328 | GG_UNHACKSUCC_REQ | 1處 | `u8` |
| 330 | GG_UNHACKFAIL_REQ | 1處 | `u8` |
| 340 | GR_KILLCHANGE_REQ | 1處 | `s16` |
| 342 | GG_SOLORESPON_REQ | 1處 | `s32` |
| 344 | GG_LIVECHAT_REQ | 2處 | `s32 u8 str` |
| 346 | GG_TEAMCHAT_REQ | 2處 | `s32 u8 str` |
| 348 | GG_DEADCHAT_REQ | 2處 | `s32 u8 str` |
| 350 | GG_TEAMDEADCHAT_REQ | 2處 | `s32 u8 str` |
| 356 | GS_CASH_REQ | 1處 | `(空)` |
| 358 | GS_BUYCASHITEM_REQ | 1處 | `u8 s32 s32` |
| 360 | GG_TSURRESPON_REQ | 1處 | `s32` |
| 364 | GR_BALANCECHANGE_REQ | 1處 | `s8` |
| 366 |  | 1處 | `s8` |
| 368 | GR_TEAMSHUFFLECHANGE_REQ | 1處 | `s8` |
| 370 | GL_CHANGECHANNEL_REQ | 1處 | `u8` |
| 374 | GR_GETCRYSTAL_REQ | 1處 | `u8` |
| 378 | GR_RADIOMSG_REQ | 1處 | `u8 u8 u8 u8 rawN` |
| 394 | MASTER_ROOMINFO_REQ | 1處 | `u8` |
| 398 | MASTER_SVRCLASS_REQ | 1處 | `u8` |
| 400 | MASTER_CONNTYPE_REQ | 1處 | `u8` |
| 402 | MASTER_EVENTPAGE_REQ | 1處 | `f32` |
| 404 | MASTER_EVENTEXP_REQ | 1處 | `f32` |
| 406 | MASTER_ENABLE_LOGIN | 1處 | `(空)` |
| 407 | MASTER_DISABLE_LOGIN | 1處 | `(空)` |
| 410 | MASTER_DISLOGIN_REQ | 1處 | `(空)` |
| 412 | MASTER_DISGMS_REQ | 1處 | `str s32` |
| 414 | MASTER_DISLOG_REQ | 1處 | `str s32` |
| 416 | MASTER_KILLALL_REQ | 1處 | `(空)` |
| 418 | MASTER_RESETTCPGROUPINFO_REQ | 1處 | `str s32` |
| 419 | GL_MSG_ADD_REQ | 1處 | `s32 str str str str u16 u8` |
| 421 | GL_MSG_DEL_REQ | 1處 | `str` |
| 423 | GL_MSG_READ_REQ | 1處 | `str` |
| 425 | GL_MSG_RECVLIST_REQ | 1處 | `s32` |
| 429 | GL_FRIEND_ADD_REQ | 1處 | `str` |
| 431 | GL_FRIEND_DEL_REQ | 1處 | `str` |
| 433 | GL_FRIEND_LIST_REQ | 1處 | `(空)` |
| 435 | GL_FRIEND_INFO_REQ | 1處 | `str` |
| 437 | GG_ROOMBROADCAST_REQ | 1處 | `u8 s32 rawN` |
| 439 | GL_FRIEND_CHAT_REQ | 1處 | `s32 str str str` |
| 441 | GL_FRIEND_WHERE_REQ | 1處 | `str` |
| 443 | GG_STEALSUCK_REQ | 1處 | `u8 s16` |
| 445 | GG_STEALPUSH_REQ | 1處 | `u8 s16` |
| 453 | GS_DELETEGIFT_REQ | 1處 | `s32 s32` |
| 455 | GG_EXERCISERESPON_REQ | 1處 | `s32 s8` |
| 457 | GI_CHANGEITEMSLOT_REQ | 1處 | `9×s32` (exact 36B UI-item IDs; sub_5275A0) |
| 461 | GS_USE_PAPERCODEGIFT_REQ | 1處 | `str` |
| 463 | GS_ENTERPAPERCODEGIFT_NOTIFY | 1處 | `(空)` |
| 464 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ | 1處 | `u8 str` |
| 466 | GI_CHANGE_SKILLITEMSLOT_REQ | 1處 | `u8 target_profile, u8 previous_update_raw, [u8 previous_profile, 7×s32]` (raw==0 → 2B; raw!=0 → 31B; sub_5738A0) |
| 472 | GL_GAMECENTER_REC_REQ | 1處 | `s16` |
| 474 | GG_GAMECENTER_GAME_START_REQ | 1處 | `s16 u8` |
| 476 | GG_GAMECENTER_GAME_END_REQ | 1處 | `s16 raw24 raw44` |
| 478 | GG_GAMECENTER_GAME_PLAY_CHECK_REQ | 1處 | `raw36` |
| 480 | GG_GAMECENTER_RANKING_REQ | 1處 | `s16 u8` |
| 483 | GG_GAMECENTER_GAME_START_OK_REQ | 1處 | `s16` |
| 485 | GL_GET_GAMEROOM_PROGRESSTIME_REQ | 1處 | `u8` |
| 487 |  | 1處 | `u8` |
| 571 | GV_TEST_REQ | 1處 | `str str` |
| 581 | GC_CLAN_START_REQ | 1處 | `(空)` |
| 583 | GC_CLAN_PROTOCOL_REQ | 24處 | `s32 s32 s32 s32 s32 \|\| s32 str str str \|\| s32 s32 rawN` |
| 585 | GC_CLAN_CREATE_REQ | 1處 | `str str str s32` |
| 682 | GL_LOGIN_REQ | 1處 | `str str u64 u8 raw24` |
| 685 | GL_TUTORIALINDEX_REQ | 1處 | `(空)` |
| 687 | GL_TUTORIAL_START | 1處 | `(空)` |
| 688 | GL_TUTORIAL_END | 1處 | `(空)` |
| 689 | GL_TUTORIAL_INDEX_SET_REQ | 1處 | `s32` |
| 695 | GS_BUY_ONCEITEM_REQ | 3處 | `s32 u8 u8 u16 \|\| (空)` |
| 697 | GG_CHEATER_REPORT_REQ | 1處 | `s16` |
| 698 | GP_ENTER_PEPACHI_REQ | 1處 | `(空)` |
| 700 | GP_START_GAME_REQ | 1處 | `u8 s32` |
| 702 | GP_PEPACHI_LIST_REQ | 1處 | `(空)` |
| 704 | GL_LEVEL_KILL_LIMIT_REQ | 1處 | `(空)` |
| 706 | GL_BILLTOKEN_REQ | 1處 | `(空)` |
| 708 | GL_CHECKCASHPG_REQ | 4處 | `s32` |
| 712 | GR_NOSKILL_REQ | 1處 | `s8` |
| 714 | GG_INVALIDWPDATA_REQ | 1處 | `u8 u8 u8 str s32` |
| 716 | GG_CHANGEWPQUICKSLOT_REQ | 1處 | `s16 s16 s16 s16` |
| 718 | GR_START_VOTING_REQ | 1處 | `s32 s32 s32` |
| 721 | GR_DO_VOTING | 1處 | `s8` |
| 724 | GL_COMBISKILLITEM_REQ | 1處 | `s32 s32 s32 s32` |
| 726 | GG_OBSERVERCHAT_REQ | 1處 | `str str` |
| 728 | GR_OBSERVERCHAT_REQ | 1處 | `wstr wstr` |
| 730 | GG_GETPULP_REQ | 1處 | `u8` |
| 733 | GG_SPAWNPULP_REQ | 1處 | `(空)` |
| 736 | GG_PULPSTEAL_REQ | 1處 | `u8` |
| 737 | GG_DESTROY_START_REQ | 1處 | `u8` |
| 739 | GG_DESTROY_SUCC_REQ | 1處 | `u8` |
| 741 | GG_DESTROY_FAIL_REQ | 1處 | `u8` |
| 746 | GG_PNR_RESPON_REQ | 1處 | `s32` |
| 749 | GG_GIMMICK_DAMAGE_REQ | 1處 | `(空)` |
| 752 | GG_MAPINFO_RELOAD_REQ | 1處 | `(空)` |
| 756 | GL_CLAN_TNMT_RECEIPT_REQ | 1處 | `s32` |
| 758 | GL_CLAN_TNMT_RECEIPT_CANCEL_REQ | 1處 | `s32` |
| 762 | GL_CLAN_TNMT_CURRENT_STATE_NOTICE_REQ | 1處 | `(空)` |
| 764 | GL_CLAN_TNMT_ENTERROOM_REQ | 1處 | `u8 s32` |
| 767 | GL_CLAN_TNTM_AWARD_INFO_REQ | 1處 | `(空)` |
| 771 | GL_CLAN_TNMT_ALL_INFO_REQ | 1處 | `s32` |
| 773 | MASTER_RELOAD_TNMT_REQ | 1處 | `(空)` |
| 776 | GL_CLAN_TNMT_CLANREC_REQ | 1處 | `(空)` |
| 783 | GL_NEW_MSG_COUNT_REQ | 1處 | `(空)` |
| 785 | GL_FRIEND_ADD_PROCESS_REQ | 1處 | `str` |
| 787 | GL_RACKINGWEB_TOKEN_REQ | 1處 | `(空)` |
| 791 | GL_VOICEITEMSLOT_REQ | 1處 | `(空)` |
| 793 | GI_VOICEITEMSLOT_ALL_REQ | 1處 | `(空)` |
| 795 | GI_CHANGE_VOICEITEMSLOT_REQ | 2處 | `u8 u8 s16 s16 u8 u8 s16 u8 \|\| s32 s16 s16 s16 u8` |
| 799 | GT_CRITICAL_ERROR_REPORT | 1處 | `s32 s16 s32 s32 s32 u8 u8 s32 u8 u8 s32 u8 u8 s32 u8 s32 u8 s32` |
| 800 | MASTER_XTRAP_RELOAD | 1處 | `(空)` |
| 802 | GS_DESTROYITEM_REQ | 1處 | `s32 s32 u8 s32 s32` |
| 804 | MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ | 1處 | `(空)` |
| 806 | GS_HIDDEN_ITEM_LIST_REQ | 2處 | `s16` |
| 808 | GS_GET_RECOMMENDSET_INFO_REQ | 1處 | `s32 s32 s32 s32 s32 s32 s32 s32 s32 s32` |
| 812 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ | 1處 | `s8` |
| 814 | MASTER_CHECK_BOMB_CHEATER_APPLY_REQ | 1處 | `s8` |
| 819 | MASTER_CHECK_NPGAMEGUARD_QUERY_REQ | 1處 | `(空)` |
| 820 | GG_CHATTING_PENALTY_REPORT_REQ | 1處 | `s32` |
| 822 | MASTER_CHAT_BAN_REQ | 1處 | `u8 u8 str` |
| 824 | MASTER_USERLIST_REQ | 1處 | `u8 s32` |
| 828 |  | 1處 | `s32` |
| 830 | MASTER_CHAT_FORCE_BAN_REQ | 1處 | `u8 str s32` |
| 831 | MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ | 1處 | `s32` |
| 834 | GL_DATA_RECV_COMPLETED_REQ | 1處 | `s32` |
| 836 | GL_SHOUTCHAT_REQ | 1處 | `s32 s32 str` |
| 838 | GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ | 1處 | `u8 s32` |
| 841 | MASTER_SETALL_EVENTEXP_REQ | 1處 | `f32` |
| 843 | MASTER_SETALL_EVENTPAGE_REQ | 1處 | `f32` |
| 845 | MASTER_VIEWALL_EVENTSTATE_REQ | 1處 | `(空)` |
| 849 | MASTER_TNMT_VIEW_STATE_REQ | 1處 | `(空)` |
| 851 |  | 1處 | `(空)` |
| 853 |  | 1處 | `(空)` |
| 855 | GL_MYWAREHOUSEINFO_REQ | 1處 | `s32` |
| 857 | GL_MYWAREHOUSEITEMLIST_REQ | 1處 | `raw1` |
| 859 | GL_PUSH_TO_WAREHOUSE_REQ | 1處 | `raw5` |
| 861 | GL_POP_TO_WAREHOSUE_REQ | 1處 | `raw5` |
| 864 | GL_SERVER_DATETIME_REQ | 2處 | `(空)` |
| 867 | GQ_QUEST_ACCEPT_REQ | 1處 | `raw4` |
| 869 | GQ_QUEST_CANCEL_REQ | 1處 | `raw4` |
| 871 | GQ_QUEST_SUCCESS_REQ | 1處 | `raw4` |
| 873 | GQ_QUEST_COMPLETE_REQ | 1處 | `raw4` |
| 876 | GQ_QUEST_ACCEPT_DAILY_REQ | 1處 | `(空)` |
| 878 | GQ_QUEST_USER_COMPLETE_HONOR_REQ | 1處 | `s8` |
| 883 | MASTER_FIND_USER_REQ | 1處 | `s32` |
| 885 | MASTER_PLAY_WITH_REQ | 1處 | `s32` |
| 887 | GX_XIGNCODE_DATA_REQ | 1處 | `rawN` |
| 890 | GC_QUERY_CLANRANKING_REQ | 1處 | `(空)` |
| 892 | MASTER_RELOAD_CLANRANKING_REQ | 1處 | `(空)` |
| 894 | GR_TEAMSHUFFLE_REQ | 1處 | `u8 u8` |
| 896 |  | 1處 | `s32` |
| 898 |  | 1處 | `s32` |
| 900 | GS_CAPSULEMACHINE_START_REQ | 1處 | `u8 s32` |
| 902 | GG_OCC_START_REQ | 1處 | `u8 u8 s32` |
| 904 | GG_OCC_SUCC_REQ | 1處 | `u8 u8 s32` |
| 906 | GG_OCC_FAIL_REQ | 1處 | `u8 u8 s32` |
| 909 | GG_OCC_RESPON_REQ | 1處 | `s32` |
| 912 | GL_WEAPONPARTS_EQUIP_CHANGE_REQ | 3處 | op 0/1: `u8 op,s32 weapon,s32 part`; op 2: plus `s32 oldPart` |
| 918 | GR_AI_GET_REWARD_ITEM_REQ | 1處 | `u8` |
| 922 | GR_AI_DAMAGE_SHIELD_REQ | 1處 | `s16 s16 s16 f32` |
| 924 | GR_AI_RECHARGE_MAGAZINE_START_REQ | 1處 | `u8 u8 u8` |
| 926 | GR_AI_RECHARGE_MAGAZINE_END_REQ | 1處 | `u8 u8 s8` |
| 928 | GR_AI_CONTINUE_START_REQ | 1處 | `s32` |
| 930 |  | 1處 | `(空)` |
| 932 |  | 1處 | `(空)` |
| 935 | GR_AI_FEVER_START_REQ | 1處 | `(空)` |
| 939 | GR_AI_GO_NEXT_WAVE_REQ | 1處 | `(空)` |
| 944 | GR_RESET_GAMEROOMSLOT_REQ | 1處 | `(空)` |
| 953 |  | 1處 | `s8` |
| 957 |  | 1處 | `s8` |
| 962 | GG_DROPWEAPON_GET_AND_DROP_REQ | 1處 | `s16 s16 u8 s16 s16 f32` |
| 964 | GG_GET_BALL_REQ | 1處 | `(空)` |
| 967 | GG_GET_GOAL_REQ | 1處 | `(空)` |
| 969 |  | 1處 | `s8` |
| 971 | GG_SOCCER_RESPON_REQ | 1處 | `s32` |
| 973 |  | 1處 | `s32` |
| 975 |  | 1處 | `u8 f32` |
| 983 | GL_MATCHINGROOM_MAKE_REQ | 1處 | `u8 str s32 u8 u8 u8 u8 u8 u8 u8 u8 u8` |
| 988 | GL_MATCHINGROOM_CANCLE_REQ | 1處 | `(空)` |
| 990 |  | 1處 | `s8` |
| 992 |  | 1處 | `(空)` |
| 996 |  | 1處 | `str` |
| 998 |  | 1處 | `str` |
| 1000 |  | 1處 | `(空)` |
| 1002 |  | 1處 | `(空)` |
| 1004 |  | 1處 | `(空)` |
| 1006 |  | 1處 | `u8` |
| 1008 |  | 1處 | `u8` |
