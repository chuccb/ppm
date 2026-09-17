# Dispatcher S2C primitive-read inventory（306 cases）

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
> 故列數多於 306。名稱欄留空者為**名稱表未註冊**的 opcode，
> 屬正確標示而非缺漏，詳 `PACKETS.md` 的 676-catalog 說明。
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


### Bootstrap fields cross-checked in native source (2026-09)

| op | Exact field meanings beyond the generated read sequence | Native evidence |
|---:|---|---|
| 142 | `str endpoint_host` (client `char[20]`), raw4/s32 port whose low u16 is used, `u8 active_channel_index`, then packed calendar `u32`: `(year-2000)<<24 \| month<<19 \| day<<13 \| hour<<7 \| minute`. | `sub_5565D0`, `sub_534F20` |
| 144 | `u8 result`, `u8 rank-restricted flag`, `s32 daily-login PG notice`, `str[40] channel`, two read-but-unused `s32`, level `s32`, K/D `f32`, propagated raw4 request context, `u8 has_net_cafe`, then exactly `u8×4 + s32×8` when present. | `sub_555D50`, `sub_A1C800`, CP932 msg table ids 0xC9/0x11C/0x31B… |
| 196 | Prefix is always `u8 result, s32 channel_id, u8 active_channel_index`; the seven-field endpoint tail exists **only when result==1**. | `CLobbyChannel::sub_4179D0`, `sub_4177B0` |
| 693 | Empty packet; its handler displays message 0xFF then immediately builds/sends 143. | `sub_57CAE0`, `sub_555C60` |

`694` is handled by `CLobbyLogin::sub_43E500` outside this dispatcher table: its
u16 replaces the initial 9600 compression threshold only when `<0x2580`; at or
above the ceiling it is ignored. `681` result is read as raw4 but its branch
selector is the low byte. See `PACKETS.md` §1.4 and §3.15d for the full layout.

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
| 200 | GL_MYITEM_ACK | sub_570AB0 | `u8 success, s32 start, repeat≤100 {s32 slot, s32 item, raw4 f1, raw4 f2, s32 period, u8 extra, u16 durability}, s32 negative-slot sentinel` |
| 201 | GL_MYPARTSUP_ACK | sub_95A3B0 | `s32 count, repeat {raw4 raw4 raw1 raw4 raw4}` |
| 202 | GL_EXPIRE_PARTSUP_ACK | sub_95AE40 | `s32 count, repeat {raw4 raw4 raw1 raw4 raw4}` |
| 203 |  | sub_571D50 | `(無直接讀取/轉發)` |
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
| 367 |  | sub_586090 | `s8/bool` |
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
| 422 | GL_MSG_DEL_ACK | sub_55A310 | `u8 statusRaw, str key` |
| 424 | GL_MSG_READ_ACK | sub_55A4F0 | `u8 statusRaw, str key` |
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
| 488 |  | sub_5861C0 | `u8 u8` |
| 489 |  | sub_57C230 | `u8` |
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
| 852 |  | sub_5854C0 | `s8/bool` |
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
| 877 | GQ_QUEST_ACCEPT_DAILY_ACK | sub_91D7E0 | `(無直接讀取/轉發)` |
| 879 | GQ_QUEST_USER_COMPLETE_HONOR_ACK | sub_91CAA0 | `u8 str` |
| 880 |  | sub_407E00 | `(無直接讀取/轉發)` |
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
| 914 |  | sub_565A00 | `u8` |
| 919 | GR_AI_GET_REWARD_ITEM_ACK | sub_761B20 | `u8 u8 s32 u8 s32 u8 u8 s32 u8 s32` |
| 921 | GR_AI_APPEARED_BOT_NOTIFY | sub_6061A0 | `u16 u16 u16 u16 u16` |
| 923 | GR_AI_DAMAGE_SHIELD_ACK | sub_761710 | `u16 u16 u16 f32` |
| 925 | GR_AI_RECHARGE_MAGAZINE_START_ACK | sub_558550 | `u8 u8 u8 u8 u16 u8 s32` |
| 927 | GR_AI_RECHARGE_MAGAZINE_END_ACK | sub_558880 | `u8 u8 s8/bool u8 u8 s32` |
| 929 | GR_AI_CONTINUE_START_ACK | sub_761E90 | `u8 u8 s32 str s32 s32` |
| 931 |  | sub_762170 | `u8 u8` |
| 933 |  | unknown_libname_105 | `(非 sub 直呼)` |
| 934 | GR_AI_TEAMSCORE_NOTIFY | sub_762630 | `s32 u8 s32 u8 s32 s32` |
| 936 | GR_AI_FEVER_START_ACK | sub_7623A0 | `u8 u8 s32 u8` |
| 937 | GR_AI_FEVER_END_NOTIFY | sub_762560 | `u8` |
| 940 | GR_AI_GO_NEXT_WAVE_ACK | sub_7613D0 | `u8 s32` |
| 942 | GR_AI_REWARDITEM_SELECT_START_NOTIFY | sub_761830 | `u8 s32 u8 u8 s32 s32` |
| 945 | GR_RESET_GAMEROOMSLOT_ACK | sub_585F30 | `(無直接讀取/轉發)` |
| 946 |  | sub_565AA0 | `u8 s32` |
| 947 |  | sub_565BB0 | `u8 u8 s32` |
| 949 |  | sub_58EF00 | `u8` |
| 954 |  | sub_57DA20 | `s8/bool` |
| 958 |  | sub_565E00 | `(無直接讀取/轉發)` |
| 959 | GG_DROPWEAPON_CREATE_NOTIFY | sub_5666D0 | `u16 u8 s32 u16 s16 s16 s16 u16 u16 f32 raw32` |
| 960 | GG_DROPWEAPON_DESTROY_NOTIFY | sub_566B30 | `u8 count, count×u16` (0 id stops early) |
| 961 | GG_DROPWEAPON_INFO_NOTIFY | sub_566BF0 | `u8 count, count×(u16 u8 s32 u16 s16 s16 s16 u16 u16 f32 raw32)` (0 id stops early) |
| 963 | GG_DROPWEAPON_GET_AND_DROP_ACK | sub_5672E0 | `u8 result, [result==0: u8 s32 u16 u8 u16 s16 s16 s16, [weapon!=0: u16 u16 u16 f32 raw32]]` |
| 965 | GG_GET_BALL_ACK | sub_566040 | `u8 u8` |
| 966 | GG_RESPAWN_BALL_ACK | sub_565EF0 | `(無直接讀取/轉發)` |
| 968 | GG_GET_GOAL_ACK | sub_566200 | `u8 u8` |
| 970 |  | sub_586180 | `s8/bool` |
| 972 | GG_SOCCER_RESPON_ACK | sub_566400 | `u8 u8 s16 s16 s16` |
| 974 | GG_SOCCERBALL_HAVE_INCREASE_PG_ACK | sub_566650 | `u8` |
| 976 |  | sub_57D660 | `s8/bool` |
| 984 | GL_MATCHINGROOM_MAKE_ACK | sub_5865A0 | `u8` |
| 985 | GL_ENTERMATCHINGROOM_ACK | sub_586610 | `u8 s32 u8 str s32 s8/bool s32 s32 str u16 u16 u16 u16 f32/s32 u8 f32/s32 u8 s32 str s32 str u8 u8 f32/s32 ...` |
| 986 | GR_MATCHINGROOM_START_ACK | sub_5880A0 | `u8 s8/bool f32/s32 u8 u8 u8 u16 u8 u8 u16 u8 s8/bool s8/bool s8/bool u8 s8/bool f32/s32` |
| 987 | GR_MATCHINGSUCCESS_ACK | unknown_libname_104 | `(非 sub 直呼)` |
| 989 | GL_MATCHINGROOM_CANCLE_ACK | sub_588020 | `s8/bool` |
| 991 |  | sub_56FA00 | `s8/bool` |
| 994 | GG_ASSISTPOINT_NOTIFY | sub_5676D0 | `u8 u8 s32 u8 u8 s32 s32 s32` |
| 995 | (未註冊; 錢包/等級推播 → PACKETS.md §3.15r) | sub_567AE0 | `s32 pg, s32 cash, s32 level` |
| 997 |  | sub_567F20 | `u8` |
| 999 |  | sub_568170 | `u8 str` |
| 1001 |  | sub_567D50 | `u16 str s32 s32 str` |
| 1003 |  | sub_567BD0 | `u16 str s32 str` |
| 1005 |  | sub_5884C0 | `u8 u8 u8` |
| 1007 |  | unknown_libname_94 | `(非 sub 直呼)` |
| 1009 |  | unknown_libname_95 | `(非 sub 直呼)` |
| 1010 |  | sub_5680E0 | `u8 u8 s32` |
