# AGENT_MEMORY.md — 原生審計記憶檔

> **重建說明（2026-09-18）**：本檔前身累積 4253 行、但因從未被追蹤進 git
> 而在 sandbox 重整時遺失；其實質內容早已逐日鏡像進 tracked docs
> （S2C_NATIVE_AUDITS / SERVER_TS_EVIDENCE / WIKI_MECHANICS / PACKETS 各節）。
> 本檔自此**納入 version control**，僅記「登入起逐步實作 walkthrough」的
> 階段錨點與原生定位，詳表一律以 tracked 專項文件為準。

## Walkthrough: Login 起逐步實作（2026-09-18）

每 phase = 原生直接證據 → TS 模組/測試對位 → docs 記錄 → commit。

| Phase | 範疇 | commit | 交付物 |
|---|---|---|---|
| 1 | admission 681 / TCP grooming / login trio | `b9f02d5f` | `SERVER_TS_EVIDENCE.md` 192 行 ledger;681 Result 表; server-list s16 定向;693 區段補正(greeting 型別/ v6 spin-lock) |
| 2 | channel handshake 143/144/195/196 | `3fad207c` | 144 case 108=0x11D/result 0=0x42/3=0xA4+7082;196 result 6→0x3A6/8→0x3A7;`client_identity` String[24]=AccountName(PaperMan.exe.c:2241) |
| 3 | 大廳同步序列（本節詳記） | `8e3c66e7` | PACKETS §3.15pre-3;原生命令行 197→199→834 定序 |
| 48 | **141→142 PM_CONNECT 握手(端點再確認+伺服器鐘)** | 2026-09-19 | builder `sub_556530` 空體、consumer `sub_5565D0`=`str,s32(low u16),u8,u32` 行級、`sub_534F20` 解碼 mask 反式=`(year-2000)<<24|month<<19|day<<13|hour<<7|minute`;142 值全來自 live config(endpoint/udpPort/index)+process-local 鐘(wire 無時區,文件化);141 嚴格空體;wire pin 固定時刻 0x1A4A6A9E byte-exact+decode mask 對拍;registry 63/61;205 測試/908 expects 綠,tsc clean |
| 49 | **s2c 全層純序列化正規化(33 檔零 throw)**+Phase 3.5 事故再現復原 | 2026-09-19 | **準則**:s2c 只負責序列化——模組零 `throw`,值域全交 packet.ts typed writer(u8/s16/s32/f32/strMax),native 文法上限=strMax 常數或固定 arm 常數本體,其餘由上游(c2s handler/store/config)保證;33 檔 59 處 throw 全刪(A 類 writer 重複/B 類能力釘參數→零參數函式+常數本體/C 類行數 cap),198/255 puzzle 目錄驗證刪除(new-skill-catalog 保留為未來 store 層資料),196 改 gate-arm 也可寫;**事故**:分支指標一度被 anchor 回 fcbe08f4(同 Phase 3.5),以「tar 全量備份工作樹→`git fetch+reset --hard FETCH_HEAD`→tar 回蓋」復原,Δ 驗證=純掃盪差 50 檔;wire pins 全保,202 測試/877 expects 綠,tsc clean |
| 50 | **s2c 貼齊 client:strMax/label 機制全撤** | 2026-09-19 | 使用者裁定:client 反序列化 str 只讀到 NUL,native buffer 容量是文件事實非程式事實;14 檔 s2c 的 43 處 strMax/label 全部回歸 `.str(x) // native char[N]`;packet.ts 的 label()/strMax() 機制整體移除;c2s 讀側上限常數 6 件在地化(24B ACK local/stride-21/stride-20,脫離 s2c 交叉匯入);MAX_* 冗餘常數全刪;測試 7 組 stride/cap 斷言改寫或移除,bytes pins 零漂移;196 測試/859 expects 綠,tsc clean |
| 51 | **命名對齊+環境飄移（三度事故）** | 2026-09-19 | 審計結果:store schema/Stats/Result 值本就對齊 RESOURCES schema 欄名與資源 id;落地 `ChannelConfig.index/id/type`→wire 官方名 `activeChannelIndex/channelId/channelType`(142/196 wire 名,c2s×2+main+e2e/channel/ops 測具同步),681 `Channel.type`→`channelType` 收斂同概念;STYLE.md 去掉 15/16 陳舊數字;**三度事故:git HEAD 再被 anchor 回 fcbe08f4+bun 再消失,固化救援(tar 備份→fetch→reset --hard FETCH_HEAD→回蓋),建議每輪 setup guard (git log -1 + bun --version)**;196 測試/859 expects 綠,tsc clean |
| 47 | **C2S 62 件審計+三持久化修復** | 2026-09-19 | ①friends/msg/shop/pepachi 拒絕臂+689 silent 覆核全綠;②**218 假成功消除**:dirty-only builder+type 欄實證,改事务持久化(`applyCharacterData` ROLLBACK⇒回 0);③312 揭穿 `sub_884160` 傳 +88 ⇒ **角色選擇更名+持久化**;④686/689 教程 marker 改 per-account store(哨兵 145 復現);wire 既有 pins 不動,200 測試/892 expects 綠,tsc clean |
| 46 | **S2C 零值總審計(60 件)** | 2026-09-19 | 新增:「936 flag=0 惰性臂鍊證明([16,76) 驗證無效⇒notify 跳)」「705 kind==0 switch 全跳=無防沉迷面板正解」「686 哨兵 145=隱藏 TUTO_NEW→具名 `TUTORIAL_COMPLETED`」「792 `sub_876B00` zero=原生語音」「371/132/467/311/879/454/106/255 行級對齊全綠」;結論:無任何 0 是未經語義驗證的填充;5 件模組註解/命名升級,wire 不變、pins 不動;197 測試綠 |
| 45 | **944→945 RESET_GAMEROOMSLOT(count 文法揭穿)——GR 全段完結** | (本次) | 944 空體 ✓;945 consumer=`sub_435E40`:**count≠0 清註冊群+逐槽 u8+s32,==0 單 B 終止**;舊表 status(1) 誤 ⇒ 恆回 `count=0`(wire `00`);pins 2+2;197 測試綠,c2s 62/s2c 60。GR/AI 法定序 912→945 全段完結 |
| 44 | **939→940 NEXT_WAVE(唯一無脫身臂⇒沉默)** | (本次) | 939 空體 ✓;940 收 5B 即無閘開波(AI3_next.wav+30s timer,無 status 臂)⇒ 無波次編排只能 parse-and-silence,940 不註冊 s2c;pins 2;c2s 61/s2c 59 不動 |
| 43 | **935→936 FEVER(空體+固定 7B 被拒臂)** | (本次) | 935 builder 零欄位 ✓;936 固定讀 7B,成功唯一件=status≠0+duration==客端基準,==0=指定被拒臂 ⇒ 恆回 7B 全零(wire `00000000000000`);pins 2+1;193 測試綠,c2s 60/s2c 59 |
| 42 | **928→929 CONTINUE(字面量 0 + 單 B 終止臂)** | (本次) | 928 builder 恆送 0(gate `sub_67EB70`);929 唯 status==1 走 `u8,s32,str,s32,s32` 復活體 ⇒ 無 PVE 接關模型恆回 `status=0`(wire `00`);舊表 continue_count 誤;pins 3+2;191 測試綠,c2s 59/s2c 58 |
| 41 | **924→925/926→927 magazine(docs 三欄錯置抓到)** | (本次) | 924 真 wire=2B(switch 只改 kind 值);925 三臂=0→+u8+s32/1→+u8+u16/其他=denial 零讀 ⇒ 恆 `status=2`(wire `010002`);926=3B;927 頭 4B≠0 終止 ⇒ 恆 `status=1`(wire `01000001`);pins 2+2 各 + wire 2;189 測試綠,c2s 58/s2c 57 |
| 40 | **922→923 DAMAGE_SHIELD(SLOBYTE 陷阱)** | (本次) | 922 builder:`sub_592B20`(4B)+`SLOBYTE(*a4)`⇒ 只送 float 低 byte;923 鏡像零臂+remain∈{4,7} 才動作 ⇒ TS 逐 byte 恆回聲(wire `0500FDFF070040000000`);pins 3+1;187 測試綠,c2s 56/s2c 55 |
| 39 | **918→919 PVE 結算抽獎(sentinel 終止臂)** | (本次) | 918=無臂 1B;919 `sub_761B20`:status==0=戰利品鏈(slot 型別檢)、!=0 讀 `u8 code` 且**僅 0xFF 哨兵終止**(否則再讀 9B)⇒ 無結算模型恆走 `idx,1,0xFF`(wire `0201FF`);pins 3+2;185 測試綠,c2s 55/s2c 54 |
| 38 | **912→913 weaponparts(errorRaw=0 危險臂實證)** | (本次) | 912 builder 三臂(0/1→9B、2→13B),`sub_592AA0`=4B ✓;913 `sub_95B180`:errorRaw==0 續讀後對末 s32 做 **item 表 bounds-check** ⇒ 無模型不可回 id ⇒ 恆 `errorRaw=1`(wire `01`);非零語義 UNRESOLVED 保持;pins 6+2;183 測試綠,c2s 54/s2c 53 |
| 37 | **718→719 / 721 投票系(+723 補記)** | (本次) | 718 `sub_A191D0`:12B(state==3 gate;房 socket);721 `sub_A192B0`:1B;consumer=共用 `sub_9BF430`:719=`u8`(無臂),720=`4×s32+u8`(`sub_592AC0`=4B 驗證),722=`s32+s8`,**723=`s8+s32` 此前無列→補記**;TS 719 恆 0、721 沉默、720/722/723 push 不發;pins 5+1;181 測試綠,c2s 53/s2c 52 |
| 36 | **485→486 progresstime(n3 臂 + 靜默臂)** | (本次) | 485 `sub_56AD60`:1B ✓;486 `sub_56AE30`:n3∈{0,1,2,4} 才有資料,其他⇒n3=3 全跳渲染 ⇒ TS 恆回 `n3=3`(wire `03`);PACKETS 486 列改臂型記法;pins 4+2;178 測試綠,c2s 51/s2c 51 |
| 35 | **483→484 start-ok(無臂 9B 恆讀)** | (本次) | 483 `sub_584EC0`:2B ✓;484 `sub_584F70` 無臂 9B 恆讀(status 讀而不耗)⇒ TS echo id+status=1+0(wire 9B);pins 3+1;176 測試綠,c2s 50/s2c 50 |
| 34 | **480→481 ranking(481 雙清單補正)** | (本次) | 480 `sub_585320`:3B ✓(state==1 gate);481 `sub_585080`:**top3(≤3)+top10(≤0xA) 雙 0x38 行清單**,舊列單 count ⇒ 補正;TS 恆 echo id+雙 0 計數(wire 11B);pins 3+1;174 測試綠,c2s 49/s2c 49 |
| 33 | **478→479 play-check(479 無 consumer → 沉默)** | (本次) | 478 `sub_564A40`:36B raw ✓;全檔掃 scanner 無 case 479/ctor 479 ⇒ 社群列「u8 status(1)」保留但**無 native 消費點**⇒ TS 解析後沉默(689 政策);pins 4;172 測試綠,c2s 48/s2c 48 |
| 32 | **476→477 gamecenter-end(137B 無臂恆讀)** | (本次) | 476 `sub_564930`:70B(2+0x18+0x2C)✓;477 `sub_76E450`:137B **無臂恆讀**(局部統計/錢包 +=0 惰性)⇒ TS 恆零結算 echo id;dispatcher 僅 GunShooting flow 路由;pins 4+1;170 測試綠,c2s 47/s2c 48 |
| 31 | **474→475 gamecenter-start(零讀取 consumer)** | (本次) | 474 `sub_584DB0`:3B ✓;475 `sub_584E80` 零讀取(同 313 模式)⇒ echo+status=1;pins 3+1;168 測試綠,c2s 46/s2c 47 |
| 30 | **472→473 gamecenter-rec(473 列補正：行 payload+尾部)** | (本次) | 472 `sub_584850`:2B id ✓;473 `sub_584910` 真文法:top3/top10 **各 0x38 B/行**、v35/v23=0x20/0x2C 條件 raw、尾 `v31,v32Raw,v21`——舊列缺漏 ⇒ 補正;TS 恆全零板 echo id(wire 38B);pins 4+2;166 測試綠,c2s 45/s2c 46 |
| 29 | **466→467 skill-item-slot(domain UNRESOLVED 保持)** | (本次) | 466 `sub_5738A0`+`sub_527BA0`(0x1C):`u8 raw0,u8 raw1[,u8 raw2,7×s32]`(2B/31B 條件);467 `sub_573A70`:3 header 恆讀、row 32B 只在 dword_E650B0 存在才讀 ⇒ TS 恆 `00 00 00`;header 語義不杜撰;pins 7+2;164 測試綠,c2s 44/s2c 45 |
| 28 | **453→454 deletegift(status=1 計數污染實證)** | (本次) | 453 `sub_57BC40`:2×s32=8B ✓;454 `sub_57BCF0`:`u8 status,2×s32` **恆讀**;status==1 尋 match 刪 cache——i_1=-1 時 **i_23-- 到 -1 計數污染**⇒ TS 恆 `status=0`(0x308 失敗橫幅,安全誠實);pins 4+2;162 測試綠,c2s 43/s2c 44 |
| 27 | **370→371 changechannel(status 五臂;文件臂型補完)** | (本次) | 370 `sub_570030`:1B(不同 channel 才送)✓;371 `sub_570100` 五臂:1=`{u8 ch,str ip,s32 port,u8 extra}`→`sub_596E60` 副端點+0x163 橫幅;0/2/3=橫幅 0xDA/0x148/0x328+重置 holder;≥4 靜默重置 ⇒ TS 單 channel 部署(main.ts channelGroups 互證)恆 `status=0`(wire `00`);pins 4+2;160 測試綠,c2s 42/s2c 43 |
| 26 | **312→313 changeslot(零消費 consumer)** | (本次) | 312 `sub_573270`:1B `u8 slot_no` ✓;313 `sub_573320` **本體完全不讀 body**(只 `sub_538470` UI 刷新)⇒ TS echo 策略;pins 4+2;158 測試綠,c2s 41/s2c 42 |
| 25 | **310→311 buychar;311 列補正(尾部恆讀)** | (本次) | 310 builder `sub_572790`:6× `sub_592A20`(4B 驗證)=24B ✓。311 `sub_5728A0`:`u8 status`≠0→6×s32 快照(sub_5831F0 混淆表),**無條件再讀 `u8 v26,s32 v33,s32 v29`**(錢包 switch 只在 status≠0)⇒ 舊列漏恆讀尾;TS 恆 `status=0`+三零(wire 10B)= dialog 收合不動錢包;pins 4+2;156 測試綠,c2s 40/s2c 41 |
| 24 | **218→219 changedata(inventory diff 上傳；文件列補正)** | (本次) | 218 真 wire=`u8 char_slot,u8 count(≤0x14),count×26B{u8 slot,u8 flagRaw,12×u16 rawWords}`(builder `sub_572FC0`+serializer `sub_5244E0` 行級;accessor `sub_592920`=1B/`sub_5929E0`=2B 實證;13-word slot table=158..169 words);舊 PACKETS 列「u8 char_slot」**不完整→補正**。219 `sub_573230`→`sub_4BCF00` 狀態機:1=applied(0xC 刷 UI)、0=abort-sync、其他 no-op ⇒ TS 無 slot store 恆回 `status=1`(wire `01`);pins 5+2;154 測試綠,c2s 39/s2c 40 |
| 23 | **131→132 forceout(房主踢人)** | (本次) | 131 builder `sub_56EC10`:`sub_592920` 1B `u8 target_slot` ✓;132 `sub_56ECC0`:先讀 `u8 status`,**全體 if(status!=0) 無 else** ⇒ status=0 全沉默臂(≠0 才再讀 slot+room-view 對)⇒ TS 無房間模型恆回 `status=0`(wire `00`);pins 4+2;152 測試綠,c2s 38/s2c 39 |
| 22 | **876→877 / 878→879 quest 系** | (本次) | 876 `sub_91D730` 空 ✓;877 `sub_91D7E0`:err≠0 only log,==0 先清 3×13B 表→`s32 count`+`13×count`B ⇒ TS 恆 `err=0,count=0`(wire `00,00000000`)。878 builder `sub_91C9D0`:`sub_5928E0(a2!=0)`=1B bool ✓;879 `sub_91CAA0`:err≠0 only log,==0 `str title`+定長 blob(長度來源 +239104 全檔唯讀)⇒ TS 恆 `err=1`(wire `01`)惰性。latch 239676 保持 0;pins 8+4(含範圍守衛);150 測試綠,c2s 37/s2c 38;首跑 4 fail 為環境 flake(連跑兩輪綠) |
| 21 | **787→788 ranking-web token(文檔補正)** | (本次) | 787 `sub_581E40` 空 ✓;**788 真 wire=`u8 hasToken[,str token]`**——唯一 payload reader 是 `sub_407360`(行級重讀),token ≥16 字元被 `strncpy(...,0x10)` 條件丟棄→`byte_EDDE04`;`sub_44BEA0`(只切 tab)/`sub_489AD0`(只動 UI)不讀 payload ⇒ 舊 PACKETS 列「str token」已補正;TS 恆回 `hasToken=0`(wire `00`);pins 4+2;146 測試綠,c2s 35/s2c 36;remote 已推 cc3491c5,bun 1.4.2 經 npm 補回 |
| 20 | **706→707 bill-token** | (本次) | 706 = CHARGE 鈕觸發,builder `sub_460480`@49534(ctor→send 無 writer ⇒ 空 wire、300ms debounce);707 `sub_46AD00` case 707 @52373 只讀 `str token`(reader `sub_592730` NUL 語意本體實證)→this+521173,純 UI 無驗證臂 ⇒ TS 恆回 `""`(wire `00`);模組採 repo 慣用 plain-function 形;pins 4+2;144 測試綠,c2s 34/s2c 35;平台 soft-reset 把全量歷程壓回工作樹(需重新 commit)、bun 1.4.2 經 npm 補回 |
| 19 | **704→705 level-kill-limit** | (本次) | 704 `sub_582570` 空 ✓ → 705 `sub_55C9B0`:`{s32 killLimit, f32 expRate, s32 maxLevelLimit}` 12B;**killLimit==0=靜默臂**(sub_44B880 靜默分支實證;非零才按 mode 彈 notice) ⇒ TS 恆回全零;pins 3+1;140 測試綠,c2s 33/s2c 34 |
| 18 | **685→686 tutorial+689 沉默** | (本次) | 685 builder `sub_55C6F0` 空 ✓ → 686 `sub_55C790`(`s32`→n145_0) ⇒ 恆回 0 白板;689 builder `sub_55C7D0`(`s32`,accessor `sub_592A20`=4B write ✓));dispatcher 無 `case 690`(舊 sub_582530 錨本 dump 不存在)⇒ 689 **刻意沉默**(同 437)。pins 5+1;140 測試綠,c2s 32/s2c 33 |
| 17 | **419→420 add-message** | (本次) | builder `sub_559550`:gate body 1..200B/toNick 1..24B/ownNick=登入 nick 截 24;wire=`u8 raw0, str ownNick, s32 uidCtxRaw, str toNick, str body, str title, s16 iconRaw, u8 soundRaw`(accessor `sub_5929A0`=2B writer ✓)。420 `sub_559810` 只讀 `str toNick,u8 xRaw,u8 resultRaw`,switch xRaw∈{0,1,2,3,4,5,10},default→0x1E2。TS 無信箱 ⇒ **恆回 default 臂 xRaw=6/resultRaw=0**(不造拒收/滿/成功);pins 8;138 測試綠,c2s 30/s2c 32;push 仍待重連 |
| 16 | **783→784＋791→792 空-REQ 對** | (本次) | 783 `sub_5643E0` 空 ✓ → 784 `sub_564480`(`s32`→dword_F0C104,UI `!=0` 切指示) ⇒ TS 恆回 0。791 `sub_885590` 空 ✓(context client-local)→ 792 `sub_885D00`+reader `sub_876B00`(`u8 page,u16,u16,27×{u16 id,u8}` 86B,id≠0→unk_EAFC40) ⇒ TS 全零表 page=0=原生零臂;slot 語義不造。accessor 表補齊:592A00=2B read(依 `sub_592500(..,2u)`)。8 pins;136 測試綠,c2s 29/s2c 31;push 待 GitHub reconnect |
| 15 | **441→442 friend where** | (本次) | 441 `sub_55B940`(恰 `str nick`,無 gate) ✓;442 `sub_55B9F0`(u8 status;==1 才讀 `u8 type,u8 channel,u8 roomNo`:11=0x314 教學、9/10=同房進房/跨房 `sub_570030`、其他=0x21E 大廳;2=0x21D+0x3AF;其他=0x21D 且不讀後續) ✓。TS: 非空/≤20B/trailing 拒絕 → 恆回 status 0(最簡誠實非-1 臂);ACK 不帶條件三欄;6 pins;134 測試綠,c2s 27/s2c 29 |
| 14 | **439→440 friend chat** | (本次) | 439 `sub_55B510`(`strlen(msg)<=180` 閘;`{s32 dword_F2A684 context, str self(sub_537740), str friendNick, str msg}`) ✓;440 `sub_55B660`(u8 status;str 24B nick1;str 24B nick2;status==2 才讀 str 200B comment;status→0x1EF/1F0/1D9/1D8;顯示走全域 sub_401B20,wire nick 只推進游標) ✓。TS: 解析四欄(trailing/180B-cap 拒絕,context parse-only)→ 恆回 status 3(通用「找不到」) + echo (self,friend);不送 0/1/2(不能證實之 claim);5 pins;133 測試綠,c2s 26/s2c 28 |
| 13 | **437 GG_ROOMBROADCAST** | (本次) | builder `sub_55B430` wire=`{u8 flag, s32 len, raw[len]}`,**無直接呼叫者**(訊息表/fn ptr);dispatcher 無 437/438 case ⇒ client 不解析 438 ⇒ **TS 解析驗線後刻意沉默**(不造任何 ACK 行為);4 pins;132 測試綠,c2s 25 |
| 12 | **435→436 friend info CSV 鏈** | (本次) | 435 `sub_55B0A0`(空 table 不送;否則 21B-stride 名單逗號相連入 String[1028]→ 單一 `str csv`) ✓;436 `sub_55B2C0`(`u8 count, rows×{str key(24B local), u8 statusRaw, [==1: str where(20B), u8 raw]}` → `sub_5382D0(key,st,where,raw+1)` + UI(matched,count,1)) ✓;**唯一證明區分是 ==1 帶 context,不造 online 語義**。TS: parser 寬上限(≤1023B csv/每段≤20B/≤100 rows/trailing 拒絕)→ 回 `statusRaw=0` rows echo(無 friend table);pbuilder 用 strMax(20/19);registry guard 拒絕 `.constants.ts` 側車 → 常數改內嵌;8 pins;131 測試綠,c2s 24/s2c 27 |
| 11 | **429→430／431→432 friend key 對** | (本次) | 429 `sub_55A860`(閘: 非空≤23B + self `sub_537740`/dup `sub_538200` 本地攔截)+431 `sub_55AD00`(閘: 非空≤23B): wire 恰 `str key` ✓。430 `sub_55AA90`(status 0 插入表→`0x1E7`;1..4→`0x1E8..0x1EB`;else→`0x1EC`;恆送空 433 via `sub_55AF20`) ✓;432 `sub_55AE10`(0 刪→`sub_538010`+433;1→`0x1ED`;2→`0x1EE`) ✓。TS 四模組(parser: 非空/≤23B/trailing 拒絕;builder `u8+strMax(23)` 以原生 24B ACK local 為界): 無 friend table ⇒ 429 self→1 else→5(原生 else 臂),431 恆→2;6 pins;129 測試綠,c2s 23/s2c 26 |
| 10 | **421→422／423→424 mailbox key 對** | (本次) | 421 `sub_55A1E0`(兩閘: key 非空≤20B + `sub_537DE0` 存在證明)+423 `sub_55A3C0`(mirror, `sub_537E90==0` 未讀才送): wire 恰 `str key` ✓。422 `sub_55A310`(非零→`sub_537A80` 刪+UI,零→resource `0x1E3`)+424 `sub_55A4F0`(非零→`sub_537D20` 寫 89,零→resource `0x1E4`)——ACK 為 `{u8 statusRaw, str key}`。TS 新增四模組(c2s parse nonempty str/trailing 拒絕/key cap=char[20]=19B;s2c emit `u8+strMax(19)`); 空信箱→**statusRaw=0** 唯一誠實回覆,勿造 status enum;6 pins(4 snapshot+2 parser);127 測試綠,c2s 21/s2c 24 |
| 9 | **834/835 data-recv-completed 鏈** | (本次) | 834 builder `sub_583120` 本體=Packet(834)+`sub_592AA0(dword_F2A684)` raw4;anti-陷阱: 144 consumer 結尾 `raw4 v69→dword_F2A684`+`u8 v66≠0→byte_EE8CB1` 一併複驗。dword_EE8CB4/EE8CB1 兩套 self-bit 分家 ✓。835 consumer `sub_5831D0` 本體=`sub_522440(dword_EE3950)` 單行,**不讀任何欄位** — 空 ACK byte-exact。三式零漂移;新增 834 parser pin(s32+trailing 拒絕)+835 空 pin,124 測試綠 |
| 8b | **252/253 shop＋254/255 invenin 重驗** | (本次) | 環境疏漏排整 (sandbox 重置後 fetch+reset 回 b42cf822; bun 以 npm-global 重裝)。252 `sub_574120` 本體(Packet(252)+state:=3)與 call-site SFX `sub_9CCEC0(10)`=trans.wav 補證;253 零 consumer 覆查(dispatcher 174–315+opcode-getter 掃描)零漂移—TS 不需更動。254 `sub_5741C0`=Packet+`sub_592920(u8)`+state:=7 ✓ 對位。255 `sub_574270` 全讀: header `{u8 mode(0/1),s32 uid,u8 contextRaw,u8 unknown}`→mode==0 追讀 swap tuple `{u8,u8,s32}`+`sub_67D870`;`uid==EE8CB4` 才讀 `u8 selectedProfile(<5)`+`sub_592500(0xA0)` blob+`sub_4BDD80`—TS mode1-only self 投影、160B blob 逐 byte 吻合;GL_INVENIN_ACK style-sweep (primitive 域接管的寬度檢查移除) |
| 8 | **codec 驗證根除重構** | (本次) | 根治問題: TS primitives 本來靜截斷 (`v & 0xff`/`v | 0`), 才被迫在 writer 流上堆 requireXxx;改為 packet.ts 型別真化: u8/s8/u16/s16/u32/s32 域全識別+非整數剔除、u64 bigint 域、f32 finite+fround 域, 新增 `strMax(text, maxBytes)` 自帶原生 buffer 上限, 加一次性 `label()` 欄位標籤 (消費後即清除, 失敗時前綴錯誤);8 支 writer 全掃 (681/198/247/200/196/201/202/434/426) requireU8/S16/U16/S32/Raw16/Raw4 全刪, 關係型護欄 (listCount/size 配對、sentinel、catalog 域) 保留;887 測試綠 (123 tests), tsc 乾淨 |
| 7 | **246/247 他人資料** | (本次) | 246 builder `sub_573DE0` 本體重讀 (Packet(246)+str+resource 0xA2) 與 TS `r.str()` 吻合;247 `sub_573EB0` 全讀: ok==1→523BF0(Phase4 亂序修正已同步)+`sub_524360` 索引式單筆外觀 (u8 index, **`>=0x14` 早退不讀**—TS 護欄一致), u8 type+12×u16;TS writer 逐欄吻合零修正;524360 為 247 與 105/108 房間快照共用 reader;§3.15pre2 補重驗注;120 測試綠 |
| 6 | **434/426 好友×信箱全語法** | (本次) | `sub_55AFC0`+`sub_537F60`(434) 與 `sub_55A630`+`sub_5378C0`(426) reader/store 雙函式重讀, 與既有 doc 三式一致零漂移;native store stride/上限一次性定案: friends ≤100、nick char[21], mail ≤10、key20/name21/body201/selector2;raw4→低byte保留(**不窄化 wire**);TS 兩 builder 升級全語法記錄投影(預設仍零筆),四支 snapshot pin+九支上限 pin;120 測試綠 |
| 5 | **200/201/202 分頁背包×PartsUp** | (本次) | `sub_570AB0`+`sub_524B70` 逐欄重驗 (u8 success→[s32 start; 直到 sentinel 的 100/5120 分頁; slot/id/raw4×2/period/u8/u16 每筆]);200 clamp/錯誤 6 arm 補注;**201/202 wire 是 17B/record 非「×20B」**(heap 0x14 的 3B pad 不上線)doc 雙處修正;新增 `GL_MYPARTSUP_ACK`/`GL_EXPIRE_PARTSUP_ACK` s2c builder 共用 `writePartsUpEntry`(s2c 20→22),wire-snapshot 17B 精確 pin;116 測試綠 |
| 4 | **198 GL_MYINFO_ACK 逐欄重驗** | `e658b579` | sub_570550 主體+523BF0/524010/524660/527550/527D00+尾段全函式重讀;語意三式互證(sub_9252D0 條件器/sub_5206F0 標籤直繫/523BF0+523E10 序列化鏡像);**TS 修掉 criticals/doubleKill/tripleKill 三欄亂序**(wire=+180,+184,+176 非遞增;原寫錯序受舊 §3.2 升冪誤表影響);PACKETS §3.2+EVIDENCE 對齊;另注 696 `sub_523A90` 變體序(176 在 172 前,跳 +144 收 +116)TS 未實作 |

### Phase 3 native 錨點（+748 scene 狀態機）

- 十個 boot/on-demand C2S builder→fn 對照:
  250=`sub_574080`、252=`sub_574120`、254=`sub_5741C0`、197=`sub_5704B0`、
  199=`sub_570A00`、246=`sub_573DE0`、425=`sub_55A580`、433=`sub_55AF20`、
  834=`sub_583120`。
- caller 反查出土 **`sub_488C60` = lobby scene `*(this+748)` 狀態機**
  (每 tick 一動作、UI-ready gate `sub_522460(dword_EE3950)` at 748∈{2,3}):
  748=1 印 L"MYINFO"→**197**; 748=2 印 L"MYITEM"→**199**; 748=3 戰績快取
  (`dword_EE8D40/44`→`sub_417E30()[7..10]`; [12]>=4 記 ROOMINFO 旗標)+
  `sub_57E3F0()`; 748=4 →**834**; 748=5 `sub_407E00()`+`sub_91D730()`
  (本地、無封包)→748=6 idle。**另路徑 `sub_49F90` 也送 834+199**
  (資料重拉分支)。
- 250 由 lobby scene UI 建構函數 (載 `ui\LOBBYMAIN.XML`、
  `sub_537710(byte_EE8968, 2)`) 尾段送出 → 必為序列第一 wire 包;
  251 死協定。198 consumer `sub_570550` 尾段**無條件送 433**。
- 834 payload = raw4 `dword_F2A684` (144 之 client_request_context 原樣);
  835 空 ACK,consumer `sub_5831D0` 僅 `sub_522440` UI 刷新。
- on-demand (非 boot 鏈): 246(str 看他人)、425(s32 信箱)、252(scene 轉場
  state:=3)、254(u8 requestContextRaw, state:=7)。
- server-ts 對位: 七個 c2s + ACK 模組線形全部吻合,113 測試綠。

### Phase 3.5 git 事故教訓

sandbox 一度把分支 anchor 回 `fcbe08f4`(起點)而 worktree 保有 sprint
內容 → commit 掛錯親代被拒。救援:`git reset --hard <remote tip>` 由遠端
重建基線(先用 `git diff <tip> --name-status` 驗證差異=僅遺失檔案)、
重放單一編輯再推。**開工先 `git log origin/<branch> -1` 核對親代鏈**;
`node_modules` 在 reset 後要 `bun install` 才跑得動 tsc。
