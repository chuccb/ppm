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
> A/B，仍是兩個方向的獨立 native evidence。現有 native evidence 證明的
> `n → n+1` case pairs、AES/raw send lane、secondary sockaddr 與 `UNRESOLVED`
> server boundary 詳見 [`PACKETS.md`](PACKETS.md) §2.5；這裡保留 constructor-level
> inventory，避免把 UDP evidence 從本文件的完整 native builder audit 中遺漏。

| op | direct ctor xref（19 sites） | write order after opcode | native send / caller / boundary |
|---:|---|---|---|
| 1 | `sub_593830`（1 site；shared function 的 `n2!=2` branch） | `u8×3 s32` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；client send fact only |
| 5 | `sub_593AB0` | `u8 raw4` | called by `sub_595E80` case 4；explicit `raw16` destination via `sub_595980`；matching stored member addresses are retried three times；`raw4` is the `sub_592AA0` caller-defined elapsed/context value；server role UNRESOLVED |
| 6 | `sub_593E60` | `u8 raw4` | called by `sub_595E80` case 5；explicit `raw16` destination via `sub_595980`；first matching received source is stored and retried three times；`raw4` is the `sub_592AA0` caller-defined elapsed/context value；server role UNRESOLVED |
| 9 | `sub_594300` | `u8×3 s32` | called by `sub_5942B0`；`sub_595A10` secondary AES lane；periodic client path; no server behavior inferred |
| 13 | `sub_594460`; `sub_5946C0`（2 sites） | each `u8 raw4` | called by `sub_595E80` cases 10/12；explicit stored-address sends via `sub_595980`；the two constructors remain separate native call sites；`raw4` is caller-defined `sub_592AA0` output |
| 14 | `sub_594A10` | `u8 raw4` | called by `sub_595E80` case 13；explicit stored-address sends via `sub_595980`；source/local state branch differs from 13; `raw4` is caller-defined `sub_592AA0` output; domain UNRESOLVED |
| 15 | `sub_593830`（1 site；shared function 的 `n2==2` branch） | `u8×2` | `sub_5937D0` timer/state caller；`sub_595A10` secondary AES lane；same native function as 1, but not the same wire form |
| 17 | `sub_596180`; `sub_596240`（2 sites） | `sub_596180`: empty; `sub_596240`: `str` (ANSI/NUL) | no named direct caller recovered for either global builder; both use primary raw `sub_595900`, bypass AES; empty and string forms must remain separate |
| 19 | `sub_596670` | `u8×2 s8 u8 s32 str` | direct callers: `sub_4070B0`, `sub_407290`, `CLobbyGameStart::sub_43C380`；`sub_595A10` secondary AES lane；offset 2 uses `sub_5928E0` s8；offset 3 uses `sub_592920` u8 and emits `0xFE` for source `-2`；nickname string is native writer output |
| 21 | `sub_596330` | `u8×3 s32 raw4 raw4` | `sub_595D80` active-manager caller；via `sub_595A10`; the two `sub_592AA0` tail values remain caller-defined raw4 |
| 23 | `sub_744450` | `u8×3 s32 raw4 u8 u16×3 u8 u8×8 s32` | direct callers: `sub_600770`, `sub_73E170`；gated through `sub_602D70 → sub_596B90 → sub_595A10`; `sub_592AA0` n0x64 is caller-defined raw4；width is Fact, field meaning UNRESOLVED |
| 27 | `sub_6013E0`; `sub_6036F0`（2 sites） | each `u8×3 s32 u8×2` | direct callers: `sub_73E170`→`sub_6013E0`; `sub_5607C0` / `sub_6013E0`→`sub_6036F0`；`sub_602D70 → sub_596B90 → sub_595A10` gate; two trailing u8 values are native state bytes |
| 30 | `sub_606340`（empty constructor; no send at this site）；`sub_6065E0` | `sub_606340`: empty; `sub_6065E0`: `u8×3 s32 u16 count, count×{s8 status,[u16 if status!=0,[conditional u16 s8 u16 f32×3 s32]]}` | callers: `sub_73E170`→`sub_606340`; `sub_606340` (2 sites) / `sub_6065D1`→`sub_6065E0`; only `sub_6065E0` calls `sub_595A10`; `sub_761500(...)` is a local temporary, not a wire field |
| 32 | `sub_96BF70` | `u8×3 s32 u8 s8 u16×3` | `sub_967E90` caller；object/position path via `sub_595A10`; the three `u16` values have no proven domain names |
| 35 | `sub_7463E0` | `u8×3 s32` | `sub_749B90` (2 direct calls)；`sub_67F380` gate then `sub_595A10` secondary AES lane；send-only evidence, no receiver or gameplay meaning inferred |

> **UDP framing boundary（Fact / HIGH）**：ops 1/5/6/9/13/14/15/19/21/23/27/30/32/35
> use the observed AES send lane unless the row says otherwise; op 17 uses the separate
> raw primary lane. `sub_595940` exists as a secondary raw-send wrapper but has no
> direct caller recovered in this dump, so it is not an additional op-17 variant.
> The `n+1` inbound cases are reader evidence, not proof of server acceptance or of an
> identical reverse-direction layout. Except for the separately documented 19→20
> client/server exchange, server behavior remains `UNRESOLVED`; this appendix does not
> authorize implementing UDP server behavior.


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

### B.1 Dispatcher case matrix

| inbound op | native branch / parser | exact consumed body | directly observed effect / failure boundary | naming status |
|---:|---|---|---|---|
| 2 | `sub_593A60` | no body read | If global `n0x3E8==0`, sets it to 1, stores `timeGetTime()-dword_F2563C` in the shared elapsed value, and sets `byte_1324330=2`; later receipts only increment the global counter. No payload-empty validation. | private unnamed; do not call it a generic `PING` without server proof |
| 4 | `sub_593AB0` | `u8 entryCount`; repeat `u8 memberKey + raw16 addressBlob` | Looks up each key in the 16-entry `dword_F6DCF4` table. Unknown key returns immediately and earlier entries remain mutated. Known entries store raw16 at `unk_F6D584+240780*i`, set `byte_F6D5B0[i]=1`; then builds op 5 (`u8 + raw4 elapsed`) and sends three AES datagrams to every stored non-local address. Sets local state byte to 4. | private unnamed; address distribution/peer role remains UNRESOLVED |
| 5 | `sub_593E60` | `u8 memberKey + raw4` | The raw4 is read into a 4-byte local and not used in the recovered state transition. For a known key, a repeated receipt only increments `byte_F6D5A4[i]`; first receipt sets `byte_F6D5A4/A5`, stores the current `recvfrom` source sockaddr in `unk_F6D594+240780*i`, builds op 6 (`u8 + raw4 elapsed`) and sends it three times to that source. | private unnamed; not proven a `HOLE` or `PING` field |
| 6 | `sub_5940E0` | `u8 memberKey + raw4` | Raw4 is read but not consumed. If the known key has `byte_F6D5A5[i]==0`, sets `byte_F6D5A4/A5` and stores the current source sockaddr; no response is built. | private unnamed |
| 8 / 24 | `sub_596940 → sub_593750` only when `n15==13`; otherwise bug/report path | no field is read by the dispatcher wrapper; `sub_593750` enqueues/copies the Packet under a critical section | The literal error path names the consumer `OnY_UDP_S_MOVE_INF`; the queue/callee path, not this dispatcher wrapper, owns any later parse. If `n15!=13`, it logs `BUGCUDPNetworkManager::OnY_UDP_S_MOVE_INF` and calls local debug/report helpers. | shared handler label `Y_UDP_S_MOVE_INF` is native string evidence; exact op-to-name mapping for 8 versus 24 is not proven |
| 10 | `sub_594460` | `u8 memberKey + raw16 addressBlob` | Finds the key (the decompiled loop waits until a match), and if `byte_F6D5B0[i]==0` stores the raw16 address and sets the flag. Builds op 13 (`u8 + raw4 elapsed`) and sends it three times to the stored address; sets local state `this+1=2`. | private unnamed |
| 12 | `sub_5946C0` | `u8 entryCount`; repeat `u8 memberKey + raw16 addressBlob` | Stores every known entry; an unknown key returns with prior entries retained. Sets the shared elapsed value from `timeGetTime()-dword_F2563C`, builds op 13 (`u8 + raw4 elapsed`), and sends it three times to each stored non-local key; sets local state `this+1=4`. | private unnamed |
| 13 | `sub_594A10` | `u8 memberKey + raw4` | Raw4 is read but unused. Known key: repeated `byte_F6D5A4[i]` increments; first receipt sets `byte_F6D5A4/A5`, stores current source sockaddr, builds op 14 (`u8 + raw4 elapsed`) and sends it three times; sets state `this+1=4`. | private unnamed |
| 14 | `sub_594CA0` | `u8 memberKey + raw4` | Raw4 is unused. If known key has `byte_F6D5A5[i]==0`, sets `byte_F6D5A4/A5`, stores current source sockaddr, and sets state `this+1=4`; no response. | private unnamed |
| 15 | `sub_593DF0` | `raw16` | One-shot latch `byte_F25646`: first packet copies raw16 to `unk_F25648`; later packets are ignored. No response. This is distinct from outbound op 15 (`u8,u8`). | private unnamed; do not merge directions |
| 18 | `sub_596300` | no body read | One-shot latch `n0x3E8_1`; first packet calls `sub_556530`, which constructs and sends catalog/TCP opcode `141 PM_CONNECT_REQ` on the TCP socket; later packets do nothing. No UDP body consumer is recovered. | private UDP trigger remains source-oriented; nested TCP opcode 141 has official `PM_CONNECT_REQ` evidence |
| 20 | `sub_5968C0` | no body read | Sets `byte_1D0CFE7=1`, clears manager retry/state words `+44,+8,+4`, updates `+24=timeGetTime()`, and clears `byte_1324331` through `sub_594F00`. No identity or gameplay field is read. | private completion for outbound op 19; official name not recovered |
| 22 | `sub_5964E0` | `u8 updateFlag`; if `==1`: `u8 count`, repeat `u8 memberKey + raw4 value` | Updates the timer/network-manager local state, then writes `raw4 value` to `dword_F6D9E8[i]` for known keys only. No response. Non-1 flag stops after the first byte. | private unnamed |
| 26 | `unknown_libname_107` | not recoverable from the exported C body | Dispatcher call is verified, but the callee body/name is absent from this dump; no field order or effect may be invented. | explicitly UNRESOLVED |
| 28 | `sub_594E80 → sub_74D130 → sub_9FA000` | gated reader: `u8×3, u8, u8, raw2, u8 count`; for selected rules, `count×{u8 index, raw4 value, raw2 state}` | Only enters in the recovered gameplay/object gates. Valid records can update an internal object flag/timestamp through `sub_9FA860` and queue insertion; local gate failure can mean no body read. No response. | shared consumer is unnamed; do not reuse outbound op 27 name |
| 29 | `sub_593E20` | no body read | Closes the TCP socket via `sub_555030(&dword_1321D00)`, loads resource `0xA8`, and calls local notice `sub_9A7DE0(...,37,1)`. No packet-derived field. | private local-notice trigger; unnamed |
| 31 | `sub_594EA0 → sub_606AD0` | gated header `u8×3, raw4 gateValue, s16 recordCount`; each record starts `u8 active`, with object-dependent tails | Valid object branch consumes object/member keys, status, raw2 state, `f32×3` position-like values, and raw4; fallback branches consume different tails without using the values. Updates client object state only; no response. `recordCount` is native `s16`, not a proven unsigned count. | shared consumer unnamed |
| 33 | `sub_594EC0 → sub_96C1E0` when global `n2_24!=0` | gated `u8,u8,u8,raw4,u8,u8,s16×3` | When its local gates pass, divides the three signed 16-bit values by 3 and writes object `+60/+64/+68`; writes adjacent state bytes and clears `+84`. If `n2_24==0`, wrapper reads nothing. No response. | shared consumer unnamed |
| 34 | `sub_594F20` | exactly three records `{u8×4, raw2, u8}` then two `raw2` values | Calls `sub_778BC0` with each record; its consumer uses selected record bytes but not the record raw2. Final two raw2 values write current-member offsets `+156/+160` when lookup succeeds. No response. | shared consumer unnamed |
| 154 | `sub_5965D0` | `u8 count`; repeat `u8 memberKey + u8 value` | Writes known-key values to `dword_F6D9E8[i]`; unknown keys are ignored; no response. | numeric/catalog overlap with official `UDP_ALL_PING_ACK`; behavior is compatible with that label, but the private dispatcher remains the authoritative xref |
| 158 | `sub_596910` | no body read | Loads resource `0x127` and calls `sub_9A7DE0(...,65,1)`; no packet-derived state or response. | numeric/catalog overlap with official `UDP_TCP_DEAD_ACK`; native private handler is still `sub_596910` |

> **Evidence-grade reading of the naming column**：direct `case → callee`, native reader
> helper identity, fixed width, and client state/send call are **HIGH native facts**.
> `private unnamed` means that this native fact is strong but its protocol purpose/name
> is still **UNRESOLVED**. `Y_UDP_S_MOVE_INF` is **HIGH** evidence for the shared
> consumer string but not for assigning 8 or 24 individually. `UDP_ALL_PING_ACK` and
> `UDP_TCP_DEAD_ACK` are **HIGH catalog-name/value facts** plus private case xrefs, but
> the dump does not prove that the private handlers are aliases of the catalog
> protocol. `unknown_libname_107` is a **HIGH unresolved-boundary fact**: the call is
> present and the callee body is absent, so no layout/name is promoted.
>
> **Reader failure boundary**：`sub_592500`/the native Packet reader may fail without
> rolling back already-mutated caller state. Count loops in cases 4/12/22/28/31/33/34
> are not a server-side schema or permission check. In particular, raw16 address blobs,
> member keys, and `recvfrom` source sockaddr are client-side state/selection evidence;
> they do not prove relay, NAT, authentication, ownership, or server acceptance.

### B.2 Direction pairs and non-pairs

The following are only numeric adjacency and native control-flow relations, not a
universal request/response contract:

| outbound builder | inbound case | native relation | layout relation |
|---:|---:|---|---|
| 1 | 2 | op 1 timer/state path; case 2 updates the same shared retry state | builder `u8×3+s32`; case 2 reads no body |
| 5 | 6 | case 4 emits 5; case 5 emits 6 | both outbound/inbound use `u8+raw4`, but the raw4 is not proven a common semantic field |
| 9 | 10 | periodic op 9; case 10 starts op 13 after address distribution | no identical reverse layout |
| 13 | 14 | case 10/12 emit 13; case 13 emits 14 | both `u8+raw4`, but direction/state roles differ |
| 14 | 15 | case 13 emits 14; case 15 consumes raw16 | no identical reverse layout |
| 17 | 18 | op 17 raw-primary send; case 18 one-shot TCP-connect trigger | op 17 is empty or ANSI/NUL string; case 18 empty |
| 19 | 20 | `sub_596670` builds 19; `sub_5968C0` completes its retry state | exact proven control exchange; case 20 has empty body |
| 21 | 22 | op 21 carries two raw4 values; case 22 is an update list | no identical reverse layout |
| 23 | 24 | movement/game state send and shared move handler | case 24 wrapper does not read body; downstream queue parse not proven here |
| 27 | 28 | native numeric adjacency only | outbound `u8×2` tail is not inbound case 28 header/records |
| 30 | 31 | native numeric adjacency only | variable bot records versus gated object state reader |
| 32 | 33 | native numeric adjacency only | different field order and consumer |
| 6,15,35 | no unique `n+1` proof | send-only or reader-shape conflict | no server response may be inferred |

### B.3 Official/catalog naming cross-check

The shipped catalog (`db/packets.tsv`) and native string-registration table
(`PaperMan.exe.c` around `674468..674624`) give official names to the separate
153..164 TCP/catalog band:

`153 UDP_ALL_PING_REQ`, `154 UDP_ALL_PING_ACK`, `155 Y_UDP_C_HOLE_INF`,
`156 Y_UDP_S_HOLE_INF`, `157 UDP_TCP_DEAD_REQ`, `158 UDP_TCP_DEAD_ACK`,
`159 TCP_UDP_DEAD_REQ`, `160 TCP_UDP_DEAD_ACK`, `161 UDP_TCP_LIVE_REQ`,
`162 UDP_TCP_LIVE_ACK`, `163 TCP_UDP_LIVE_REQ`, `164 TCP_UDP_LIVE_ACK`.

Only numeric 154 and 158 also occur as cases in `sub_595E80`; their native private
handlers and layouts are the rows above. Cases 153,155,156,157,159..164 are **not**
cases in this dispatcher, so their catalog names must not be pasted onto private
ops by number alone. The string `OnY_UDP_S_MOVE_INF` is direct native evidence for the
shared 8/24 consumer label, but it does not prove whether 8 or 24 is the official
catalog value for that label. All other private values remain unnamed/source-oriented
until a stronger native name or bidirectional protocol evidence is found.

### B.4 Validation invariants for this appendix

The dispatcher audit is intentionally executable, not prose-only:

- `sub_595E80` cases must remain exactly the 22-value set in B.1.
- Low outbound constructor opcodes must remain exactly the 15-value set in Appendix A.
- The nine shared-prefix builders must retain their native identity sources and
  `u8,u8,u8,s32` prefix; op 19 retains its `s8` third scalar and string tail.
- `sub_595980` is explicit-address AES send; `sub_595900` is raw primary send;
  `sub_595A10` uses the secondary sockaddr. The three wrappers are not interchangeable.
- No official catalog name is promoted for an opcode absent from the private switch.

`python3 tools/verify_dispatcher_coverage.py` checks the opcode sets and shared
builder prefix. Native helper-body and direct-caller audits remain separate checks;
passing this appendix does not turn unresolved client-side state into server authority.
