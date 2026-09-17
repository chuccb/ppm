# C2S REQ builder primitive-write inventory（261 TCP opcode rows；321 direct ctor call sites）

> **範圍與計數口徑（Fact；2026-09-17 native scan）**：本表只收錄
> `PaperMan.exe.c` 中 `Packet::possible_ctor_or_dtor_0(buffer, N)` 的 TCP/C2S
> writer，按 **unique opcode row** 計數：261 rows、261 個不重複 opcode、無重複列。
> 這不是完整 native constructor inventory：同一 opcode 可能有互斥 branch 或多個
> caller。261 rows 合計 321 個 direct constructor call sites；全檔 native scan 則為
> 340 個 sites、276 個 unique opcode，另有 19 個 sites／15 個 low private-UDP
> opcode（`1,5,6,9,13,14,15,17,19,21,23,27,30,32,35`），它們刻意不在本
> TCP/C2S 表內。因此標題的 261 是 row/unique-TCP-opcode 口徑，不應解讀成
> 261 個 constructor variants，也不應解讀成 276 個完整 native forms。
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
> exact byte width 的保守寫法，`rawN` 則是 runtime-sized raw/bulk write。native
> helper xref 為 `sub_592920/8E0/960`→raw1、`sub_5929A0/9E0`→raw2、
> `sub_592A20/60/AA0`→raw4、`sub_592AE0`→raw8、`sub_592B20`→raw4；
> `str`、`wstr` 是 native string writer call，沒有在此表臆測 encoding、terminator
> 或最大長度。`||` 代表同一 builder 的互斥或
> 條件變體，不是把它們串成一個可線性消費的 packet。count-prefixed record、
> optional tail、固定 buffer 與 opcode substitution 只在 native branch 已觀察到時
> 才寫出；`raw24`、`raw36` 等固定值是 source copy/array boundary 的 Fact；常見
> `_DWORD packet[4817]` 則是 native Packet 暫存容量，不是宣稱 wire 有 4817 個
> dword。這張表不是 server policy 或 server-ts handler specification。
>
> **Fact / Inference / UNRESOLVED**：opcode literal、direct ctor xref、helper
> call order、分支條件、copy count 與固定陣列邊界是 native Fact。具名 row 只沿用
> native/catalog 的官方 symbol；`db/packets.tsv` 明確補出的 366=`GR_LOCALROOM_REQ`、
> 969=`GR_SOCCER_REQ`、990=`GR_DAMAGEROOM_REQ` 亦屬 catalog Fact。其餘空名稱
> 保留為未註冊 native request，不以 Wiki、舊文件、TS type 或鄰近 opcode 臆造名稱。
> 字段的 domain 語意、server 接受規則、登入／權限／計價等若沒有 native consumer
> 或 protocol handshake 證據，維持 `UNRESOLVED`；raw 欄位不得因有熟悉的數值而升格。
>
> **native/catalog cross-check**：目前仍未註冊的 table opcode 為
> `295,487,828,851,853,896,898,930,932,953,957,973,975,992,996,998,1000,1002,1004,1006,1008`
> （另有 native ctor 但 catalog 未註冊的 206，表內已明示 unnamed）。這些空名稱是
> 有意保留的證據狀態，不是遺漏。`LAYOUTS.md` 的 S2C/dispatcher coverage 是
> 獨立 inventory，不能拿來替換本表的 C2S writer order；`PACKETS.md` 的 named
> protocol facts 也不能把沒有 native writer 的 catalog entry 變成本表 row。
>
> **111 exception (Fact/HIGH):** this inventory mechanically flattens mutually
> exclusive branches in `sub_56A5A0`; its row is **not a linear 111 layout**.
> The sole reachable caller, `sub_449320`, emits
> `u8 0xFF, s8 has_password, str title, [str password], u8 max_players,
> u8 modeIndex, u8 requested_map, u8 no_skill_bg`. See `PACKETS.md` §3.15 before
> implementing or changing 111.
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
| 165 | Y_TCP_INF_REQ | sub_55C9F0; sub_55CAB0; sub_55D090; sub_55D440; sub_55D530; sub_5658B0; sub_5AF880; sub_5DF7F0; sub_5E5ED0; sub_5E6040; sub_5E6170; sub_5E6DF0; sub_744D80; sub_7452D0; sub_745D60; sub_745E80; sub_98E920 | 17 site-specific native forms (xref order; `rawK` is width): `sub_55C9F0 raw1×2 raw4`; `sub_55CAB0 raw1×2`; `sub_55D090 raw1×4 raw2 raw4×2 raw1×3 raw4×2 raw1×4 raw4`; `sub_55D440 raw1×3 raw2 raw1×2`; `sub_55D530 raw1×5 raw2 raw4×6 raw1×4 raw4 raw1`; `sub_5658B0 raw1×2 raw2×3`; `sub_5AF880 raw1×2 raw4 raw2 raw4×8 raw1`; `sub_5DF7F0 raw1×3`; `sub_5E5ED0 raw1×5 raw4`; `sub_5E6040 raw1×5 raw4`; `sub_5E6170 raw1×3`; `sub_5E6DF0 raw1×5 raw4×5`; `sub_744D80 raw1×2 raw4 raw2 raw4×7 raw1`; `sub_7452D0 raw1×2 raw4 raw2×7 raw1×3 raw2 raw1 raw2×2 raw4 raw2`; `sub_745D60 raw1×3`; `sub_745E80 raw1×2`; `sub_98E920 raw1×2 raw4 raw2 raw4×5 raw1`. Do not flatten these into one linear layout; native branch/state predicates remain source-only. |
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
| 204 | GS_BUYITEM_REQ | sub_571100 | `u8 count, count×{s32 raw0,u8 raw1,raw2,[raw2 when raw1 is 12/13/17]}`; native range check may substitute opcode 468 before the records; field/domain meanings remain UNRESOLVED |
| 206 | *(unnamed native request; paired with 207)* | sub_571620 | `s32 raw0,s32 raw1,u8 raw2,s32 raw3` |
| 208 | GS_SELLITEM_REQ | sub_572AD0 | `s32` |
| 210 | GM_CHECKNICK_REQ | sub_572CD0 | `str` |
| 212 | GM_CREATENICK_REQ | sub_572DC0 | `str` |
| 214 | GM_CREATECHAR_REQ | sub_572EB0 | `u8 s16 s16 s16` |
| 216 | GL_ENTERROOMPASS_REQ | sub_56B180 | `u8 str` |
| 218 | GI_CHANGEDATA_REQ | sub_572FC0 | `u8` |
| 220 | GI_CHANGEWP_REQ | CLobbyTournamentGameRoom::sub_47AA40; sub_573340; sub_57C270 | `u8 count, count×record`; record = `u8 raw0,raw2 raw1,[3×raw2 when raw0!=3],[8×raw4 when raw1!=0]`; branch predicates are native raw values, field/domain meanings remain UNRESOLVED |
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
| 295 |  | sub_57D830 | `(空)` |
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
| 421 | GL_MSG_DEL_REQ | sub_55A1E0 | `str raw0` (native caller uses it as the local message key; service meaning UNRESOLVED) |
| 423 | GL_MSG_READ_REQ | sub_55A3C0 | `str raw0` (native caller uses it as the local message key; service meaning UNRESOLVED) |
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
| 457 | GI_CHANGEITEMSLOT_REQ | sub_573770 | `9×raw4` (exact 36B bulk payload via `sub_5275A0`; field/domain meanings remain UNRESOLVED) |
| 461 | GS_USE_PAPERCODEGIFT_REQ | sub_57CBC0 | `str` |
| 463 | GS_ENTERPAPERCODEGIFT_NOTIFY | sub_57CB20 | `(空)` |
| 464 | GS_USE_PAPERCODEGIFT_IGNORE_DUPLICATED_ITEM_REQ | sub_57CCE0 (direct; caller sub_4C4000) | `u8 raw0; raw0==1 → str`; observed UI callback literals are 1 and 0, so the 0 branch is exactly one byte; domain meaning remains UNRESOLVED |
| 466 | GI_CHANGE_SKILLITEMSLOT_REQ | sub_5738A0 | `u8 raw0,u8 raw1,[u8 raw2,7×raw4]` (raw1==0 → 2B; raw1!=0 → 31B; appended bulk is `sub_527BA0`; domain meanings remain UNRESOLVED) |
| 472 | GL_GAMECENTER_REC_REQ | sub_584850 | `s16` |
| 474 | GG_GAMECENTER_GAME_START_REQ | sub_584DB0 | `s16 u8` |
| 476 | GG_GAMECENTER_GAME_END_REQ | sub_564930 | `s16 raw24 raw44` |
| 478 | GG_GAMECENTER_GAME_PLAY_CHECK_REQ | sub_564A40 | `raw36` |
| 480 | GG_GAMECENTER_RANKING_REQ | sub_585320 | `s16 u8` |
| 483 | GG_GAMECENTER_GAME_START_OK_REQ | sub_584EC0 | `s16` |
| 485 | GL_GET_GAMEROOM_PROGRESSTIME_REQ | sub_56AD60 | `u8` |
| 487 |  | sub_57C450 | `u8` |
| 571 | GV_TEST_REQ | sub_58E4D0 | `str str` |
| 581 | GC_CLAN_START_REQ | sub_550560 | `(空)` |
| 583 | GC_CLAN_PROTOCOL_REQ | sub_54BA90; sub_54BC30; sub_5506C0; sub_550790; sub_550840; sub_550960; sub_550A10; sub_550B10; sub_550C00; sub_550CF0; sub_550DD0; sub_550E80; sub_550F80; sub_551210; sub_5512C0; sub_551390; sub_551460; sub_551500; sub_5515A0; sub_551650; sub_5517E0; sub_551970; sub_551B00; sub_551C90 | multiple native control forms, not one linear layout: `s32`; `s32 s32`; `s32 s32 rawN`; `s32 s32 str`; `s32 str`; `s32 str str str`; `2×s32`; `3×s32`; `5×s32` |
| 585 | GC_CLAN_CREATE_REQ | sub_5505F0 | `str str str s32` |
| 682 | GL_LOGIN_REQ | sub_43DF00 | `str str u64 u8 raw24` |
| 685 | GL_TUTORIALINDEX_REQ | sub_55C6F0 | `(空)` |
| 687 | GL_TUTORIAL_START | sub_5624D0 | `(空)` |
| 688 | GL_TUTORIAL_END | sub_562570 | `(空)` |
| 689 | GL_TUTORIAL_INDEX_SET_REQ | sub_55C7D0 | `s32` |
| 695 | GS_BUY_ONCEITEM_REQ | CPopupGunShootingStart::sub_5115E0; sub_570B00; sub_8DE9C0 | multiple native forms; `sub_5115E0`/`sub_8DE9C0`: `raw4 raw1 raw1 raw2`; `sub_570B00`: conditional `raw4 str raw1 raw1` or `raw4 raw1 raw1` or `raw4 raw4` or `raw4 raw4 raw1 raw1`; all item-family predicates and field meanings remain UNRESOLVED |
| 697 | GG_CHEATER_REPORT_REQ | sub_593510 | `s16` |
| 698 | GP_ENTER_PEPACHI_REQ | sub_46E080 | `(空)` |
| 700 | GP_START_GAME_REQ | sub_8458D0 (direct; caller sub_8459C0) | `u8 raw0,s32 raw1` (exact 5B; observed raw0 selectors 1/2/4/5; native computes raw1 from selected-character state; domain meanings UNRESOLVED) |
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
| 802 | GS_DESTROYITEM_REQ | sub_895B90 (direct; caller sub_894070) | `s32 raw0,s32 raw1,u8 count,count×raw4 raw2`; each record may append a native-object-bounded run of raw4 values; the run bound is taken from client state, not emitted as a separate count. Field/domain meanings remain UNRESOLVED. |
| 804 | MASTER_RELOAD_HIDDEN_ITEM_LIST_REQ | sub_581F80 | `(空)` |
| 806 | GS_HIDDEN_ITEM_LIST_REQ | CLobbyPartsUpRoom::sub_9C1DD0; sub_46C760 | `s16` |
| 808 | GS_GET_RECOMMENDSET_INFO_REQ | sub_46E140 | `s32 count,count×raw4`; sender only constructs/sends it when count>0; each optional positive native state slot contributes one raw4 |
| 812 | MASTER_SPECIAL_ABILITY_ITEMSLOT_PROBABILITY_APPLY_REQ | sub_582390 | `s8` |
| 814 | MASTER_CHECK_BOMB_CHEATER_APPLY_REQ | sub_5822E0 | `s8` |
| 819 | MASTER_CHECK_NPGAMEGUARD_QUERY_REQ | sub_582600 | `(空)` |
| 820 | GG_CHATTING_PENALTY_REPORT_REQ | sub_5826A0 | `s32` |
| 822 | MASTER_CHAT_BAN_REQ | sub_582020 | `u8 u8 str` |
| 824 | MASTER_USERLIST_REQ | sub_5821A0 | `u8 s32` |
| 828 |  | sub_57D430 | `s32` |
| 830 | MASTER_CHAT_FORCE_BAN_REQ | sub_5820E0 | `u8 str s32` |
| 831 | MASTER_RESET_PACKET_DELAY_ALLOW_TIME_SEC_REQ | sub_583070 | `s32` |
| 834 | GL_DATA_RECV_COMPLETED_REQ | sub_583120 | `s32` |
| 836 | GL_SHOUTCHAT_REQ | sub_583370 | `s32 s32 str` |
| 838 | GR_CLAN_JOIN_RECOMMAND_REQUEST_REQ | sub_584500 | `u8 s32` |
| 841 | MASTER_SETALL_EVENTEXP_REQ | sub_5840B0 | `f32` |
| 843 | MASTER_SETALL_EVENTPAGE_REQ | sub_584160 | `f32` |
| 845 | MASTER_VIEWALL_EVENTSTATE_REQ | sub_584210 | `(空)` |
| 849 | MASTER_TNMT_VIEW_STATE_REQ | sub_57DD30 | `(空)` |
| 851 |  | sub_57DA90 | `(空)` |
| 853 |  | sub_57DB30 | `(空)` |
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
| 896 |  | sub_57DBD0 | `s32` |
| 898 |  | sub_57DC80 | `s32` |
| 900 | GS_CAPSULEMACHINE_START_REQ | sub_99CFA0 (direct; related caller sub_99D0A0) | `u8 raw0,s32 raw1` (exact 5B; observed raw pairs include `{3,1}` and `{1,10}`; `sub_99D0A0` is related state path, not a second direct ctor) |
| 902 | GG_OCC_START_REQ | sub_564CF0 | `u8 u8 s32` |
| 904 | GG_OCC_SUCC_REQ | sub_565120 | `u8 u8 s32` |
| 906 | GG_OCC_FAIL_REQ | sub_565470 | `u8 u8 s32` |
| 909 | GG_OCC_RESPON_REQ | sub_559020 | `s32` |
| 912 | GL_WEAPONPARTS_EQUIP_CHANGE_REQ | sub_95AEF0×3 | `u8 raw0,s32 raw1,s32 raw2`; raw0==2 appends `s32 raw3`; the three direct sites are branch variants in `sub_95AEF0`, field/domain meanings remain UNRESOLVED |
| 918 | GR_AI_GET_REWARD_ITEM_REQ | sub_761A70 | `u8` |
| 922 | GR_AI_DAMAGE_SHIELD_REQ | sub_761580 | `s16 s16 s16 f32` |
| 924 | GR_AI_RECHARGE_MAGAZINE_START_REQ | sub_558350 | `u8 u8 u8` |
| 926 | GR_AI_RECHARGE_MAGAZINE_END_REQ | sub_5586B0 | `u8 u8 s8` |
| 928 | GR_AI_CONTINUE_START_REQ | sub_761DB0 | `s32` |
| 930 |  | sub_7620A0 | `(空)` |
| 932 |  | sub_582440 | `(空)` |
| 935 | GR_AI_FEVER_START_REQ | sub_7622C0 | `(空)` |
| 939 | GR_AI_GO_NEXT_WAVE_REQ | sub_75CE40 | `(空)` |
| 944 | GR_RESET_GAMEROOMSLOT_REQ | sub_585E90 | `(空)` |
| 953 |  | sub_57D970 | `s8` |
| 957 |  | sub_565E00 | `s8` |
| 962 | GG_DROPWEAPON_GET_AND_DROP_REQ | sub_566F50 | `s16 s16 u8 s16 s16 f32` |
| 964 | GG_GET_BALL_REQ | sub_565F60 | `(空)` |
| 967 | GG_GET_GOAL_REQ | sub_566120 | `(空)` |
| 969 | GR_SOCCER_REQ | sub_5860C0 | `s8` |
| 971 | GG_SOCCER_RESPON_REQ | sub_566350 | `s32` |
| 973 |  | sub_57D4F0 | `s32` |
| 975 |  | sub_57D5A0 | `u8 f32` |
| 983 | GL_MATCHINGROOM_MAKE_REQ | sub_586220 | `u8 str s32 u8 u8 u8 u8 u8 u8 u8 u8 u8` |
| 988 | GL_MATCHINGROOM_CANCLE_REQ | sub_587F70 | `(空)` |
| 990 | GR_DAMAGEROOM_REQ | sub_56F950 | `s8` |
| 992 |  | CLobbyQuest::sub_9C7F30 | `(空)` |
| 996 |  | sub_567E70 | `str` |
| 998 |  | sub_568030 | `str` |
| 1000 |  | sub_567CB0 | `(空)` |
| 1002 |  | sub_567B30 | `(空)` |
| 1004 |  | sub_588420 | `(空)` |
| 1006 |  | sub_564B70 | `u8` |
| 1008 |  | sub_564C30 | `u8` |
