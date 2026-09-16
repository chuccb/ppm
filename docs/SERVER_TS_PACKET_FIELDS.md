# server-ts 現行 31 個 Packet 欄位審計

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
  這些欄位不因 C# 變數名、猜測或 UI 位置而改名。

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
| 105 `GL_USERLIST_REQ` | builder 傳 `u8`，native 參數是 unsigned，client 目前送 1。 | 無。 | 是 user-list 觸發 byte，但不是已證明的 status/filter。 | 消費一 byte，回 106 的 zero gate。 | byte domain、nonzero 行為。 |
| 107 `GL_GAMEROOMINFO_REQ` | 空 request；對應 108 reader。 | 無直接 room payload 對應。 | lobby room refresh。 | 嚴格空讀，回 `mode=0,count=0`。 | room model、mode-3 branch 與 policy。 |
| 143 `PM_UDPSTART_REQ` | `str`、`s32`、literal `u8=1`、`s32`；由 681 handoff values 建立。 | 無。 | 是 channel admission claim。 | 以兩個 echoed raw value、source IP claim；identity 只記錄。 | identity、n100、ext tuple 的業務語意。 |
| 195 `GC_ENTERCHANNEL_REQ` | 三個 `u8`；第三 byte 來自 local option block。 | 無。 | 前兩 byte 對應 681 的 group/channel 選擇。 | 驗認證與 group/channel；rawFlag 不作放行條件；type 3 只有在完整 raw continuation 已配置時才允許 server success。 | rawFlag domain、type-3 server policy。 |
| 197 `GL_MYINFO_REQ` | 空 request，dispatcher 進 198。 | character/NewSkill 資源只供 response projection。 | self MyInfo request。 | 從 authenticated Store 輸出 198 snapshot。 | 未登入時官方 error policy。 |
| 199 `GL_MYITEM_REQ` | 空 request；client 期待 200 page。 | `itemdata.pat` 是 200 的 catalog lookup 資源。 | inventory refresh。 | empty success page at start 0 + negative sentinel。 | inventory ownership、catalog/grant policy。 |
| 246 `GL_CLIENTINFO_REQ` | 一個 NUL ANSI string；native 246 caller 將其送往 247。 | 無。 | lookup key 是 nickname string，而非 numeric uid。 | Store nickname lookup，回單一 247 character。 | name uniqueness/authorization policy。 |
| 250 `GL_LOBBYIN_REQ` | 空；目前 recovered path 沒有 251 consumer。 | 無。 | client-local lobby transition。 | 嚴格空讀，不回 packet。 | 其他 build 的 server transition policy。 |
| 252 `GL_SHOPIN_REQ` | 空；client 先切 shop state。 | shop XML 只證 UI layout，非 success payload。 | shop entry notice。 | 回空 253 compatibility ACK。 | 253 官方成功語意。 |
| 254 `GL_INVENIN_REQ` | `u8` context，native 254 caller 送後讀 255。 | NewSkill XML controls corroborate scene/puzzle slots, not context. | inventory/NewSkill entry context。 | echo context，按 Store uid 輸出 mode 1。 | context/header、remote mode、server policy。 |
| 425 `GL_MSG_RECVLIST_REQ` | `s32`；`sub_44E480` returns the UI navigation value, `sub_6DC6C0` subtracts 10 with a floor of 1, `sub_6DC760` adds 10, and `sub_55A580` writes the value with the 4-byte writer. | 無 mailbox resource/schema。 | native proves a message-list navigation scalar with initial/step behavior; “page” is a reasonable UI description but total-count/server mailbox policy remains unproven. | 讀取並忽略，回 zero-count 426。 | exact cursor/page domain、total count、mailbox policy。 |
| 433 `GL_FRIEND_LIST_REQ` | 空 request，對應 434。 | 無 friend table/status resource。 | current-account friend-list request。 | zero-count 434 + configured account display string as a bounded compatibility projection。 | friend storage/status/policy。 |
| 682 `GL_LOGIN_REQ` | two ANSI strings、guard `u64`、source `u8`、raw24；builder source 0/1/2 的 raw shape 可追。 | `Extracted/datarevision.txt` supplies the revision input；不是 hardware identity。 | guard is a client-data-version gate；fingerprint is separate security material。 | validates guard, source/raw combinations, credentials；does not use revision as Store policy。 | token semantics, fingerprint persistence/auth policy。 |
| 834 `GL_DATA_RECV_COMPLETED_REQ` | one raw `s32` propagated from 144 global；then no fields。 | 無。 | data receive-completion notification。 | read-only raw boundary，回空 835。 | raw context domain、是否需 admission join。 |
| 102 `GT_PING_ACK` | 空 heartbeat packet；client builds 101 after receiving it。 | 無。 | server heartbeat。 | 可發空 102；不由 101 handler 回。 | scheduling policy。 |
| 106 `GL_USERLIST_ACK` | raw2 gate；nonzero branch then flags/count/records。 | 無。 | gate is not the later record count。 | gate 0，省略 optional tail。 | gate domain、user-list data authority。 |
| 108 `GL_GAMEROOMINFO_ACK` | `sub_568CE0` reads `u8 mode`; only `mode!=3` then reads ordinary `u8 count` and room records. `mode==3` immediately enters `sub_580A80`, whose independent grammar is `u8 n4,u8 i1,u8 flags`, reverse stage loop, pair records, tail, raw4 stored in a native `float`. | `Extracted/ui/gameroom.xml` contains the actual room controls (`GAMEROOM_DAMAGEROOM`, `GAMEROOM_NORMAL_NOSKILL`, `GAMEROOM_CLAN_NOSKILL`, map/mode selectors); `RESOURCES.md` separately ties those controls to native consumers such as `+128` and `+185`. `GAMEROOM_TEAMBALANCE` is a 364/365 UI branch, not proof that every tournament `+186` byte has that meaning. `Extracted/ui/TNMT_Awardproperty.xml` is a tournament award layout only: `award_1..3`, `nomarl_award`, `abnomarl_award`, emblem/present positions. Native UI consumers also reference `tournamentmatchlist.xml`, `tournamentPlayerInfo.xml`, and `tournamentFinalAward.xml`; those files are not in this Extracted snapshot. Room maps remain catalog-only (`maplist.pat`/`Map.dat`). | mode 0 is ordinary room-list; mode 3 is a separate tournament bracket/state projection, not a count variant. `sub_53F9F0` maps pair fields into `CRoomInfo`; `sub_875C20` and local-identity comparison consume the two participant blocks. | TS deliberately emits only `mode=0,count=0`; this is a safe ordinary projection, not mode-3 support. | tournament scheduling/pair semantics, participant block schema, stage raw words, selected-node tail semantics, server policy; native count fields have no visible bounds check in this reader. |
| 144 `PM_UDPSTART_ACK` | fixed prefix is fully read before result branch；name is fixed ANSI buffer；optional net-cafe block is gated。 | localized message resources corroborate displayed restriction text only。 | rank/PG/level/KDR fields feed UI messages。 | full fixed prefix, conservative reserved/raw values, optional gate 0。 | reserved s32, raw context, net-cafe records、admission policy。 |
| 196 `GC_ENTERCHANNEL_ACK` | failure is 3 fields; success appends endpoint/type; type 3 calls gated continuation。 | 無 direct type-3 catalog。 | endpoint is subsequent UDP control handoff。 | success/failure framing and complete type-3 raw tail are preserved；server 不輸出截斷的 false-success prefix。 | endpoint policy、opaque flags、type-3 record semantics。 |
| 681 `GL_LOGIN_ACK` | success prefix, raw2 server/group fields, positive-capacity one-channel body, type-3 extra, billing tail；failure only result。 | `Extracted/ui/system/netcafe_contents.xml` directly proves a three-entry NetCafe UI/config table (`number`, `grade`, `logo`, boost/discount/level fields), but does not map any 681 extension word/byte；仍無 resource authority for billing/flag/group。 | channel field is USERS current numerator, server port is selector endpoint；capacity/current-user raw2 are consumed as signed short by the native path；`sub_A1C870` stores the positive extension gate and the two raw s32 words without conversion, while the feature byte becomes a separate boolean。 | three fixed groups, s16/raw2 width checks, zero gates only for omitted groups, positive gate requires a complete channel body、ext gate 0。 | flag/group/billing/user_no domains、extension tuple/resource join、server policy。 |
| 247 `GL_CLIENTINFO_ACK` | failure one byte；success shares 198 basic block then one slot/type/12 appearance words。 | character/avatar resources corroborate category-relative appearance space。 | slot is serialized character-list index; type is separate char type。 | public lookup emits one selected serialized entry and 12 u16。 | persistent slot policy、appearance item authority。 |
| 253 `GL_SHOPIN_ACK` | no recovered native payload consumer。 | shop XML is UI only。 | no wire-level success schema established。 | empty compatibility ACK only。 | official 253 response semantics。 |
| 255 `GL_INVENIN_ACK` | common 4-field prefix；mode 0 remote branch；mode 1 selected `<5` then 5×32-byte raw profiles。 | `sub_527AF0`/`sub_4AC8F0` plus NewSkill XML identify seven ordinal families: `11010001..11020000`, `11020001..11030000`, `11030001..11040000`, `11040001..11050000`, `11050001..11060000`, and shared accessories `11060001..11070000`; resource message 900 also states the same accessory puzzle cannot occupy both accessory positions。 | uid/profile scope is account-level, not character appearance；duplicate/ownership rule is resource/native-client evidence, not automatically server policy。 | mode 1 only; echo context; five 7-ID + expiry records，range validation only。 | remote fields, header/context, catalog completeness, duplicate accessory enforcement, expiry/ownership/grant policy。 |
| 426 `GL_MSG_RECVLIST_ACK` | count is `u8`; client stores at most 10 records; one record raw field is 4 bytes and the other is 2 bytes, but the recovered table assignments retain their low bytes; fixed local string slots。 | 無 message schema。 | message-list projection, not proven page model。 | raw2/context string/count 0。 | header, record raw fields/domains、mailbox policy。 |
| 198 `GL_MYINFO_ACK` | success basic block, max-20 characters, max-4 weapon groups, 9 UI ids, NewSkill block, tail；all widths follow native helpers。 | `itemdata.pat`, avatar resources, NewSkill resources corroborate separated domains/ID spaces。 | selected NewSkill is not normal appearance；reserved stats remain unnamed。 | Store facts + zero/empty conservative projection；validated puzzle ranges。 | reserved words/flags/raw48/n5、inventory/loadout grant policy。 |
| 200 `GL_MYITEM_ACK` | success gate；start int; 100/page, 5120 slots, negative slot terminator, item catalog check, f32/f32/s32/u8/u16。 | `Extracted/ui/cfg/itemdata.pat` is native catalog membership source。 | period is duration-like only from consumer context。 | success 1/start 0, optional record API, `-1` terminator。 | f1/f2/extra domains、ownership/service/time policy。 |
| 434 `GL_FRIEND_LIST_ACK` | raw2/context string/count；consumer keeps at most 100 record-string/row-field rows, 21-byte record-string slot, wire raw4 row field with local low-byte retention。 | 無。 | context string is read but has no recovered immediate consumer。 | header 0/context string/count 0。 | header/row-field values、friend policy。 |
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
| 105 → 106 | `sub_56A0F0` emits 105 with one unsigned byte only for `a2==1` and a one-second native rate gate; other callers pass 0 to change local flags without sending 105 | `sub_56A250` reads raw2 gate, then optional `flags/count/records`; `sub_588560` consumes rows | first gate is not record count；TS uses zero gate, so optional branch is not entered | 無；gate/status/list authority unresolved |
| 107 → 108 | lobby refresh caller sends empty 107 | `sub_568CE0` reads `mode`; `mode==3` diverts before reading ordinary `count`; otherwise reads `u8 count` and ordinary rows. `sub_580A80` reads its own `u8 n4,u8 i1,u8 flags` header. | ordinary mode 0 + count 0 terminates before room-record consumer；mode 3 stores state `[16],[17],[18],[19],[142],[494]`, builds `CRoomInfo` nodes via `sub_53F9F0`, sends two participant blocks through `sub_875C20`, then selects `sub_47E1B0` or `sub_47E3E0` for tournament UI. | `Extracted/ui/TNMT_Awardproperty.xml` proves only award layout; native UI names `tournamentmatchlist.xml`, `tournamentPlayerInfo.xml`, `tournamentFinalAward.xml` are direct filename references, not available resource contents. Stage/pair semantics remain unresolved. |
| 143 ↔ 144 | `sub_555C60` builds 143 after channel TCP greeting；144 is server result | `sub_555D50` reads entire fixed prefix before checking result | endpoint/admission consumer stores raw4 context for later 834/195 flow；result UI uses rank/PG/level/KDR branches | `msgtableres.lang` only corroborates displayed text；reserved/context/net-cafe policy unresolved |
| 195 → 196 | `sub_56FF40` builds 195 after 144; group/channel are selected from 681 list | `sub_4179D0` reads 196 prefix, then success endpoint; type 3 enters `sub_875680` | endpoint passed into UDP setup (`sub_58ED30`/`sub_595C90`); follow-up 195 is not gated by 144 result in native, so TS admission must gate it | no resource proves rawFlag/type3 business semantics; native optional type3 gate is preserved, TS server config requires complete tail before emitting success |
| 197 → 198 | `sub_570550` dispatcher requests self data | `sub_523BF0` basic block → `sub_524010` characters → `sub_524660` loadouts → `sub_527550` UI IDs → `sub_527D00` NewSkill/tail | CClientData is populated; selected char and profile then drive lobby UI/state | avatar assets and NewSkill XML separate appearance/puzzle domains; reserved/basic tail unresolved |
| 199 → 200 | `sub_570A00` emits empty 199; response enters `sub_570AB0` | success gate then `sub_524B70(...,1)`; inventory slots are scanned and `sub_535020` validates catalog lookup | 5120-slot local array, max-100 page, negative slot terminator; item cache update calls `sub_534450`/`sub_524F70` are downstream consumers | `itemdata.pat` proves shipped-client membership only; no server ownership/grant authority |
| 246 → 247 | `sub_573EB0` builds nickname request | `sub_573EB0`/public reader shares `sub_523BF0`; `sub_524360` consumes one character appearance tail | public profile branch serializes one list index/type/12 appearance words, not private 198 tail | avatar resource categories corroborate 12 relative slots; nickname policy unresolved |
| 250 → (251) | client local lobby transition sends empty 250 | no recovered 251 reader/consumer in this snapshot | state transition is local; TS rejects trailing bytes and intentionally does not reply | no resource payload; alternate build/server policy unresolved |
| 252 → 253 | client shop scene sends empty 252 | no recovered non-empty 253 consumer | TS empty ACK is compatibility projection only; shop UI does not prove server success grammar | `ui/shop.xml` is layout/presentation, not account/shop policy |
| 254 → 255 | `sub_573EB0` inventory entry caller sends one context byte | `sub_574270` reads common prefix; mode 1 copies selected profile and 5×32-byte records; `sub_4AAB80` chooses current profile | local uid/profile branch updates NewSkill client state; mode 0 remote preview is a separate branch not emitted by TS | NewSkill XML/level/color tables corroborate seven slots/five profiles; context/header/expiry authority unresolved |
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
| stage | for `i=n4-1; i>=i1; --i`: `sub_592940` stage byte, `sub_592940` round byte, `sub_592940` mode byte, `sub_592A40` raw4 stage word, `sub_592A40` raw4 stage word, `sub_592940` pair count | reverse order is native fact; stage bytes/words are not business-named here. round byte is copied to state `[18]`; mode byte is passed to `sub_53F9F0`'s mode factory |
| pair base | `sub_592A40` raw4 node/room source; `sub_592940` ×4; `sub_592A00` raw2 word | the four values are passed separately to `sub_53F9F0`; the third byte is first assigned to `+129` but `sub_53FB10` later recomputes `+129` from the raw2 mask |
| round type 4 tail | `sub_592940` one byte, then `sub_592A40` ×4 into two participant/emblem-related blocks | both blocks go to `sub_875C20`; their first dword participates in the local identity comparison. No decompiled body proves a uid, emblem, or score name |
| other round tail | two `sub_592A40` raw4 blocks | both go to `sub_875C20`; the same local identity comparison applies |
| footer | `sub_592940` `hasMy`; when nonzero, `sub_592940` ×2; `sub_592AC0` reads a 4-byte word into native local `float v57` | first optional byte becomes the selected raw node value; second is read but not used by the visible continuation; the 4-byte value is stored at client state `[494]` (native local type is float, helper itself is a raw4 reader) |

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
| `sub_5928E0`、`sub_592920`、`sub_592960` | 寫入 1 byte | `u8`/`s8` | helper 的 `char` 型別不代表業務是 bool/status |
| `sub_592900`、`sub_592940`、`sub_592980` | 讀取 1 byte | `u8`/`s8` | signedness 只在 direct consumer 有比較證據時採用 |
| `sub_5929A0`、`sub_5929E0`（寫）；`sub_592A00`、`sub_5929C0`（讀） | raw copy 2 bytes | `raw2`/caller-defined `u16`/`s16` | 681 channel/server counts、198 appearance 等再由 consumer 判斷 |
| `sub_592A20`、`sub_592A40`、`sub_592A60`、`sub_592A80`、`sub_592AA0`、`sub_592AC0` | raw copy 4 bytes | `raw4`/caller-defined `s32`/`u32`/`f32` | 4-byte width 不等於 user id、page、status 或 float；要看 native consumer |
| `sub_592AE0`、`sub_592B00`、`sub_592B60`、`sub_592B80` | raw copy 8 bytes | `raw8`/caller-defined `u64` | 682 guard 由 low/high direct consumer 定義；其他 raw8 不可重命名 |
| `sub_592B20`、`sub_592B40` | raw copy 4 bytes | `raw4`; native float destination 才可投影為 `f32` | helper 自身只是 copy；144 K/D 是 consumer 造成的 f32 語意 |
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
| 105 `GL_USERLIST_REQ` | `u8 refreshTrigger` | Native `sub_56A0F0` 的參數是 `unsigned __int8`，且只有值 `1` 才建立 105；這證明 wire byte 應按 unsigned 讀取，不證明它是 status/filter。TS 只消費一 byte，直接回 106 zero-count。 | `sub_56A0F0`、`sub_56A250`；HIGH（unsigned trigger；數值 domain 仍不命名） |
| 107 `GL_GAMEROOMINFO_REQ` | empty | 空的 room-list refresh request。TS 嚴格拒絕 trailing bytes，回 108 `mode=0,count=0`；尚未建立 room model。 | `sub_568CE0` / 108 reader；HIGH |
| 143 `PM_UDPSTART_REQ` | `str identity`, `s32 n100`, `u8 literal=1`, `s32 ext_count` | `identity` 是 native `String[24]`（最多 23 ANSI bytes），但 writer/權威來源尚未找到，不能稱 account/nickname。`n100`、`ext_count` 是 681 回送的 handoff claim；literal 必須是 1。TS 只用 `n100 + ext_count + source IP` claim admission；identity 僅 log。 | `sub_555C60`、143/144 C consumer、`PACKETS.md §3.15d`；HIGH for shape, UNRESOLVED for identity |
| 195 `GC_ENTERCHANNEL_REQ` | `u8 group`, `u8 channel`, `u8 rawFlag` | `group` 是 681 廣告的 group 序號，`channel` 是組內 channel index。第三 byte 是由 local option block 產生的 raw flag；`sub_735DE0` 只證明 native 讀取一個設定值，不能直接命名成 replay/module/status。TS 讀取但不以它放行。TS 僅接受已認證且 group/channel 與 config 相符；native 允許 196 type 3 的 gate-only boundary，但 TS server success 只在 config 帶完整 raw continuation 時成立，不把截斷 tail 當成成功。 | `sub_56FF40`、`sub_4179D0`、`sub_7338D0/sub_735DE0`；HIGH for shape, UNRESOLVED for rawFlag domain |
| 197 `GL_MYINFO_REQ` | empty | 請求自己的 198 MyInfo。TS 從 authenticated account 取得 Store identity 與 NewSkill snapshot；未登入時保留 null/失敗形狀。 | `sub_570550` dispatcher case；HIGH |
| 199 `GL_MYITEM_REQ` | empty | 請求 200 背包 page。TS 目前沒有 inventory/catalog model，回 `success=1,start=0` 加負 slot sentinel。 | `sub_570AB0`、`sub_524B70`；HIGH for shape, TS projection policy only |
| 246 `GL_CLIENTINFO_REQ` | `str nickname` | 以 nickname 查 public MyInfo。TS 將此字串交給 `getMyInfoByNickname`，回 247；沒有把它當 numeric user id。 | `sub_573EB0`、247 consumer；HIGH |
| 250 `GL_LOBBYIN_REQ` | empty | Client-local lobby transition notice；client state 由本地流程處理。TS 嚴格讀空 payload，不回 251，因目前沒有 recovered native 251 consumer。 | `PACKETS.md §3.15pre-2`；HIGH for empty/no-consumer boundary |
| 252 `GL_SHOPIN_REQ` | empty | Client 在送出前已進入 shop state 3；TS 回空 253 是 interoperability choice，不宣稱 native shop-success payload。 | `PACKETS.md §3.15pre-2`；HIGH for request, bounded TS policy for reply |
| 254 `GL_INVENIN_REQ` | `u8 requestContextRaw` | Client-local inventory/NewSkill entry context。其 UI/entity/page/tab 語意 **UNRESOLVED**；TS 原樣 echo 到 255，不解讀。TS 以 authenticated account 的 `uid` 取得五個 NewSkill profiles。 | `sub_573EB0`/254 caller、255 mode-1 consumer；HIGH for echo, UNRESOLVED for context |
| 425 `GL_MSG_RECVLIST_REQ` | `s32 rawRequestValue` | Native builder `sub_55A580` writes 4 bytes from `sub_44E480`; `sub_6DC6C0` decrements the UI value by 10 with a floor of 1, and `sub_6DC760` increments it by 10. This proves a message-list navigation scalar and its native step behavior, but not the server mailbox/page policy. TS reads it and ignores it because no mailbox model is recovered. | `sub_55A580`、`sub_44E480`、`sub_6DC6C0/sub_6DC760`、`sub_55A630` ACK consumer；HIGH for width/step behavior, UNRESOLVED total/page policy |
| 433 `GL_FRIEND_LIST_REQ` | empty | 請求 current account 的 friend list。TS 尚無 friend table，回 434 的 zero-count projection，並提供 configured account display string as a bounded compatibility projection。 | `sub_55AFC0`；HIGH for empty request and ACK shape |
| 682 `GL_LOGIN_REQ` | `str account`, `str password_or_token`, `u64 packed_data_revision`, `u8 fingerprint_source`, `raw[24] fingerprint` | `packed_data_revision` 是 guard：low dword 固定 `0xF1E1AB0E`，high dword 為 `datarevision.txt` 讀出的 client data revision XOR `0xB1A9D7C7`，不是 hardware key。`fingerprint_source`/24 bytes 是分離的 device/security material；native 只證明 source byte 由 security probe 選出，不能替它命名成 account/device id。TS 驗 guard、完整長度與 credentials，現在只用 account/password 驗證；revision/source/raw24 尚未成為 Store policy。 | `sub_43DF00`、`sub_592AE0`、682 builder、`PACKETS.md §3.1`；HIGH for wire shape/guard, bounded TS policy |
| 834 `GL_DATA_RECV_COMPLETED_REQ` | `s32 requestContextRaw` | lobby data receive-completion report。native `sub_583120` 直接把 `dword_F2A684` 寫入；該 global 是 144 的 propagated raw4 context，雖也被多個 client request 重用，但其 server-domain/user identity 語意未證實。TS 只讀取、拒絕 trailing bytes，再回空 835；不可命名為 `user_id`。 | `sub_583120`、144 `sub_555D50` raw4 path、419 `sub_559550`/other raw-context builders、834/835 mapping；HIGH for shared raw field, UNRESOLVED domain |

### C2S 的共同 boundary

所有上表 reader 都拒絕未被 layout 支持的 trailing bytes。這是 frame/parser
完整性檢查，不等於 native server 一定使用相同的錯誤 transport。`rawFlag`、
`fingerprint_source`、fingerprint bytes、`requestContextRaw`、834 的 shared raw
context 目前均不能因為欄位名稱相似而互換。

## S2C builders（16）

| opcode / TS | wire order and field | wire meaning / current TS use | evidence and confidence |
|---|---|---|---|
| 102 `GT_PING_ACK` | empty | Server heartbeat；client 收到後建立 101。TS 可建立空 packet；對 inbound 101 不回 packet。 | `sub_58D6F0`；HIGH |
| 106 `GL_USERLIST_ACK` | `raw2 gate`; if nonzero: `u8 flags`, `u8 recordCount`, each `s32 user_id`, `str nickname`, `s32 exp`, and when `user_id>0`, `s32 custom_tex_id`, `str tex_name` | Native first reads a 2-byte value into `__int16 v23` and only tests zero/nonzero; it is not the record count. The actual loop count is the later `u8 i_1`. Current TS emits the zero gate, so no optional fields follow. Native `flags` controls list UI open/close; the third s32 is exp, which the client converts to a displayed level, not status. | `sub_56A250`, `sub_588560`; HIGH for order/gate, UNRESOLVED gate domain |
| 108 `GL_GAMEROOMINFO_ACK` | `mode!=3`: `u8 mode,u8 count`, then ordinary records. `mode==3`: `u8 mode`, then **no ordinary count**; `sub_580A80` reads `u8 n4,u8 i1,u8 flags`, reverse stage records (`u8,u8,u8,raw4,raw4,u8 pairCount`), pair base (`raw4,u8,u8,u8,u8,raw2`), round-4 extra (`u8` + 4×raw4) or other-round participant blocks (2×raw4), tail `u8 hasMy` + optional 2×u8, then raw4 (native local `float`). | Native direct mapping: `sub_53F9F0` receives each node id, current/max bytes, a raw2 mask, stage-derived bytes/word, mode byte, flags, and participant-related bytes; it writes `CRoomInfo` offsets including `+4,+105,+129,+110,+130,+136,+144,+185,+186`, then normalizes max slots from the mask. `sub_875C20` consumes each of the two participant blocks; the first dword of either block is compared with `sub_54B570(dword_131E238)` to select the local/current node. Tail first byte becomes the selected raw node value; final raw4 is stored at client state `[494]` as the native float local. | `sub_568CE0`/`sub_580A80`/`sub_53F9F0`/`sub_875C20`/`sub_47E1B0`/`sub_47E3E0`; `Extracted/ui/TNMT_Awardproperty.xml`; HIGH for framing/width and state writes, UNRESOLVED for business names |
| 144 `PM_UDPSTART_ACK` | `u8 result`, `u8 rank_restricted_server_flag`, `s32 daily_login_reward_pg`, `str channel_name`, `s32 reserved_after_name_1`, `s32 reserved_after_name_2`, `s32 channel_restriction_level`, `f32 channel_restriction_kdr`, `raw4 client_request_context`, `u8 has_net_cafe_info`, optional `4×u8 + 8×raw4` | Client 在所有 result 上先讀完整固定 prefix。result 是 raw u8；TS 保留已知 UI constants，但未知 code 也原樣可發送。rank flag 在 rank>10 時顯示限制；daily PG 只在正值顯示通知；level/KDR 是 restriction message values。兩個 name 後 s32 維持保守 reserved。`client_request_context` 是 raw4，會被後續 request 原樣帶入但 domain 未證實。TS 預設 zero/empty、驗證兩個 fixed s32 UI values，並 `has_net_cafe_info=0`，所以不發 optional block。 | `sub_555D50`、`sub_592AC0`、`sub_A1C800`；HIGH for shape/consumer, UNRESOLVED reserved/context/net-cafe fields |
| 196 `GC_ENTERCHANNEL_ACK` | `u8 result`, `s32 channel_id`, `u8 channel_index`; only success: `str udp_host`, `s32 udp_port`, `u8 endpoint_opaque`, `u8 channel_type`, `raw4 client_flags`, `u8 client_default`; `channel_type==3` then enters `sub_875680`, whose native gate may stop after `s32 header0` when `header0<=0` (the four fixed 4-byte fields there are `raw4`, not `f32`) | Failure 只有前三欄。result 是 raw u8；TS 保留已知 UI constants，但未知 non-success code 也只寫前三欄。成功 endpoint 是 client 後續 UDP control address；port wire 是完整 s32，native endpoint consumer 另取其 low u16，TS projection 仍要求可用的 `1..65535` endpoint port。`endpoint_opaque`、`client_flags`（bit0 已知）、`client_default` 保持 raw-oriented。Native type 3 可在 header0 後停止，但 TS server 只有在完整 raw continuation 已配置且 header0 positive 時才輸出 success；不輸出截斷的 false-success tail。channel admission 不允許非 type-3 附帶未消費 tail；完整 native grammar 與 lifecycle 見 `docs/S2C_NATIVE_AUDIT_196.md`。 | `sub_4179D0`、`sub_4177B0`、`sub_58ED30`/`sub_596E60`、`sub_875680`；HIGH |
| 681 `GL_LOGIN_ACK` | `s32 result`; only success (`result=1`) continues with `s32 user_no`, `s32 n100`, `s32 ext_count`, `raw2 server_count`, server records (`raw2 server_id`, `str name`, `str host`, `raw2 server_port`, `u8 flag`, `raw2 group`, three groups of `s16 max_users` plus optional channel `{u8 ch_type,str ch_name,raw2 current_users,u8 ch_flag,[u8 extra when type=3]}`), and fixed `s32 billing_first`, `s32 billing_second` | Failure is exactly the result word and the native branch tests its low byte. `user_no` remains the official conservative name; TS supplies verified account row id without claiming it is the later 198 user row. `n100` is opaque charge/billing UI mode and is echoed by 143. `ext_count>0` makes the native reader consume exactly one `{s32,s32,u8}` extension triple and pass it to `sub_A1C870`; TS production still emits 0 by default, while the audited writer can emit an explicitly supplied raw gate plus exactly one raw tuple without assigning semantic names. `server_port` is not duplicated by the channel field: `sub_58AD90` passes server host plus this field to `sub_554810` as a `u_short` TCP endpoint. The channel field is native `USERS` numerator/current-users data; the preceding `max_users` field is the denominator/capacity and positive reader gate. TS retains the existing public `GameServer.port` property for this server-level endpoint, while modeling three `ChannelGroup` objects rather than calling the channel field a port. `flag`, `group`, and billing words remain unresolved. TS validates raw2 server_id/group without assigning signedness, and validates channel/server u8 fields before writing so a masked type cannot accidentally change the type-3 framing. The reader stores a 132-byte internal projection rooted at the local scratch (`sub_58E690`); it is not a second wire field. Positive `maxUsers` without a channel is rejected rather than projected as a zero gate; this preserves the native continuation boundary without silently reinterpreting later groups. | `CLobbyLogin::sub_43E500` (681 branch), `sub_58AD90`/`sub_554810`, `sub_416DA0`/`sub_4176C0`, `sub_4179D0`/`sub_56FF40`, `sub_58E690`/`sub_58F120`/`sub_58E640`/`sub_58E670`, `sub_7092C0`, `PACKETS.md §1.4`; HIGH for order, endpoint/USERS consumer, and the type/channel projection cross-check; wire signedness remains conservative except the endpoint consumer's `u_short`; `flag`/`group`/billing domains UNRESOLVED |
| 247 `GL_CLIENTINFO_ACK` | `u8 ok`; if ok: shared 198 basic block, then `u8 slot` (native 0..19 character-list index), `u8 char_type`, `12×u16 appearance` | `ok=0` 只有一 byte。成功首段與 198 的 `sub_523BF0` 完全共用；尾端是單一 character appearance，不是 198 的 character list、weapon groups 或 NewSkill tail。`slot` 是 serialized character-list slot/index；`char_type` 才是角色類型。TS 以 selected character-list index 取 serialized array entry，並在 247 尾端回寫同一 index；不把 persistent slot id 或 char_type 當成該欄位，然後寫 12 個 category-relative u16。Store 先將 persistent slot key 映射為 compact serialized-list index。若 index 不在 native 0..19 或沒有對應 serialized entry，TS 回 `ok=0`，不以另一筆 character appearance 靜默 fallback。 | `sub_573EB0`、`sub_523BF0`、`sub_524360`、`sub_524010`；HIGH |
| 253 `GL_SHOPIN_ACK` | empty | 沒有 recovered native shop success payload。TS 的空 ACK 是明確標成 interoperability response，不把它寫成官方成功資料。 | `PACKETS.md §3.15pre-2`、未找到 253 consumer；HIGH for current boundary |
| 255 `GL_INVENIN_ACK` | Common prefix `u8 mode`, `s32 uid`, `u8 requestContextRaw`, `u8 unknownHeaderRaw`; native mode 0 then reads two more `u8` values and one `s32` remote lookup value. Native mode 1 then reads `u8 selectedProfile` (`<5`) and 160 raw bytes = 5 profiles × (`7×s32 puzzleItemId`, `s32 expiresAtPackedMinute`). | **Native fact:** `sub_574270` accepts mode 0/1, always reads the common four fields, and in the local-user branch reads selected profile plus 160 raw bytes only when the selected value is below 5; `sub_4AAB80` copies five 32-byte profile records and uses the selected value to choose the current record. **Resource fact:** `Extracted/ui/NewSkill*.xml` and the native `sub_527AF0`/`sub_535020` path identify seven NewSkill puzzle slots and a shipped item-catalog membership check; resource/UI names do not prove grant policy. **Inference:** the uid/profile scope is account-level rather than a character appearance index because 254/255 use a self uid and no character index; packed expiry is profile state, with profile 0 ignored by the native expiry display path. **TS projection:** mode 1 local snapshot only; uid is a positive s32 Store user id, request context is echoed, unknown header remains zero, selected profile is 0..4, and every profile has seven validated s32 IDs plus s32 expiry. **UNRESOLVED:** mode-0 remote fields, context/header meanings, full catalog membership projection, expiry/server-time policy, duplicate accessory policy, and grant/ownership authority. | `sub_574270`、`sub_4AAB80`、`sub_527AF0`/`sub_535020`、Extracted NewSkill resources；HIGH for grammar/width, MEDIUM for profile scope, UNRESOLVED business policy |
| 426 `GL_MSG_RECVLIST_ACK` | `raw2 rawHeader`, `str field_s0`, `u8 count`; each record: `str field_s1`, `u8 field_a3`, `str field_s2`, `raw4 field_a5`, `str field_s3`, `str field_s4`, `raw2 field_a8` | **Native fact:** `sub_55A630` reads the fourth record field into `int v37` and the final record field into `__int16 v35`; `sub_5378C0` retains at most 10 records. Its local `sub_592*` sequence is `str,u8,str,raw4,str,str,raw2`. **Resource fact:** the native table count is at `this+241704`; per-entry storage uses fixed strides of 20 bytes for the first NUL string, a 2-byte stride for the first raw byte slot, 21 bytes for the second NUL string, a one-byte slot for the low byte of raw4, 201 bytes for the third NUL string, 2 bytes for the final NUL string, and a one-byte slot for the low byte of raw2. The copy loops themselves stop at NUL and do not visibly clamp to those strides; `201`/`2` are local storage facts, not wire string limits. The `raw4` fourth-field argument is assigned into a one-byte table slot at `this+60536+index`, and the `raw2` final-field argument is assigned into a one-byte table slot at `this+122107+index`; the recovered list table therefore visibly retains only their low bytes even though the wire reader consumes 4/2 bytes. No Extracted message/catalog resource gives these record fields or a mailbox schema. **Inference:** none beyond the record being a client message-list projection. **TS projection:** count 0 with `rawHeader=0` and the configured account display string as an interoperability projection; native `sub_55A630` reads but does not visibly consume its context string, so this is not a recovered mailbox identity rule. **UNRESOLVED:** rawHeader, context-string/record-string/raw-field domains, mailbox paging/policy. | `sub_55A630`、`sub_5378C0`（10-record cap、fixed storage strides、wire raw4/raw2 → local low-byte assignments）；HIGH for order/width/capacity, UNRESOLVED business fields |
| 198 `GL_MYINFO_ACK` | `u8 success`; if success: `s32 user_id`, shared basic block, character list, weapon groups, 9 UI slots, selected NewSkill raw block, tail | `user_id` 是 MyInfo wire user id；TS 使用 Store player `userId`。basic block：`str nickname`, `u8 selected_char_index`, `s32 level`, `s32 experience`, native derived-level slot, then 18 words in native order: `[reserved34,reserved35,reserved36,wins,losses,kills,deaths,disconnects,hearts,headshots,doubleKill,tripleKill,combos,multiKill,ultraKill,zKill,kKill,ddKill]`, followed by `u8×3 flags`, `s32 cash`, `s32×2 raw`, `48B extra blob`, `u8 slot_current`。`reserved34..36` have no proven task/stat consumer; TS keeps them zero rather than mapping `criticals/playCount/roundCount` by guess. The first dword of the 48B blob is the cumulative play-time task counter (`Stats.playTimeSeconds`); the remaining 44 bytes are still zero because mode counters are not modelled. `derived-level` is recomputed from exp by the client; flags and raw words remain conservative.接著是 `u8 char_count` + 每筆 `u8 char_type + 12×u16`；最多 20。再是最多 4 組 weapon loadout（group 3 只有 primary），9×s32 UI-item slots；再是 raw `u8 n5`（current compatible value 5）+ selected profile 7×s32；最後 `u16 pending_gift_count`, `s32 game_point`, `u8 tutorial_count` 及 records。TS 只填 Store 已有資料，其餘使用已證實的 zero/empty projection；若 caller 提供 NewSkill snapshot，selected profile 與五筆/七槽 shape 先驗證，且非零 puzzle ID 依 `Extracted`/`sub_527AF0` 的七段 native ordinal range 驗證，不以 malformed snapshot 靜默回填 zero。 | `sub_570550`、`sub_523BF0`、`sub_524010`、`sub_524660`、`sub_527550`、`sub_527D00`、`RESOURCES.md §5c-1/§5c-2`；HIGH for order/grammar, UNRESOLVED raw/n5 domains |
| 200 `GL_MYITEM_ACK` | `u8 success`, `s32 start_index`; repeated item: `s32 inv_slot`, `s32 item_id`, `f32 f1`, `f32 f2`, `s32 period`, `u8 extra`, `u16 durability`; terminal negative `s32 inv_slot` | **Native fact:** `sub_570AB0` gates the reader on a nonzero success byte; `sub_524B70(...,1)` reads an `int` start index, scans at most 100 positions within the 5120-slot client array, stops on a negative `inv_slot`, rejects a negative or catalog-missing `item_id` through `sub_535020`, then reads two native floats, one period s32, a u8 `extra`, and a u16 value; the client calls the final s32 in this reader a negative terminator by branch behavior. **Resource fact:** `Extracted/ui/cfg/itemdata.pat` is the client item catalog used by `sub_535020`; this proves membership/lookup, not ownership, grantability, pricing, or the domains of `f1`, `f2`, or `extra`. **Inference:** `period` is a signed remaining-duration word in the inventory projection, but its server time policy is not recovered. **TS projection:** success 1, start 0, optional records, and terminal `-1`; TS rejects negative slots/item IDs and preserves the confirmed f32/s32/u8/u16 widths, but does not silently invent an item catalog authority. **Decision:** do not load `itemdata.pat` in the current runtime. It is a shipped client lookup table (and the local `.pat` is an encoded/resource-format dependency), while `server-ts` has no inventory ownership model; using membership as a grant or shop policy would turn a resource fact into an unsupported server rule. Keep the non-negative s32 boundary now. When a real inventory projection is added, add a separate resource/catalog adapter and validate every emitted ID against it without using the adapter to decide ownership, pricing, or grantability. **UNRESOLVED:** item ownership/service policy, f1/f2 domains, extra, period policy, and the complete future catalog adapter contract. | `sub_570AB0`、`sub_524B70`、`sub_535020`、`Extracted/ui/cfg/itemdata.pat`、`LAYOUTS.md`；HIGH for order/width/framing/caps, UNRESOLVED business semantics |
| 434 `GL_FRIEND_LIST_ACK` | `raw2 rawHeader`, `str contextString`, `u8 count`; each record: `str field_s1`, `raw4 field_a3` (native local table keeps low byte) | **Native fact:** `sub_55AFC0` reads the two-byte header, context string, and unsigned count; `sub_537F60` stores at most 100 string/row-field records. **Resource fact:** the native table count is at `this+244236`; insertion is allowed only while count `< 0x64` (100 entries), each record string uses a 21-byte NUL-string stride at `this+244237+21*index`, the wire `int` record field is assigned into the one-byte slot at `this+61585+index` (only its low byte is visibly retained), and the surrounding delete/shift paths retain two one-byte side fields per slot. `sub_537F60` itself copies until NUL without visibly clamping to the 21-byte stride. `Extracted/` has no friend-table or row-field definition that can name these fields. **Inference:** the context string is read by `sub_55AFC0` but not passed to `sub_537F60` or another recovered consumer; no owner/display semantic is proven. **TS projection:** header 0, configured account display string, count 0; the string is a bounded compatibility projection, not a native owner-id claim. **UNRESOLVED:** header domain, friend record-string/row-field domain, row-field values, side-field meanings, and friend policy. | `sub_55AFC0`、`sub_537F60`（100-entry cap、21-byte key stride、wire raw4 row field→local low byte、side-field shift paths）；HIGH for order/width/capacity, UNRESOLVED business fields |
| 693 `GL_TCPCONNSUCC` | empty | Channel-server greeting. Client shows its greeting and immediately builds 143. TS sends it once as connection trigger; no payload fields exist. | `sub_57CAE0` → `sub_555C60`；HIGH |
| 694 `GL_ACCOUNTCONNSUCC` | `u16 compression_threshold` | Native `sub_43E651` reads exactly one 2-byte value into `n0x2580`; only a value strictly below `0x2580` replaces the local compression threshold, while `0x2580` or larger leaves compression disabled. The same handler then invokes the 682 login builder. TS validates only the native `u16` boundary and sends the value unchanged, including values the client ignores; the default remains `0x2580` so compression is disabled. | `sub_43E651` `n694==694` branch、`sub_592A00`、`sub_592CE0/sub_592E00` compression path；HIGH for wire/client behavior |
| 835 `GL_DATA_RECV_COMPLETED_ACK` | empty | Completion ACK after 834. No payload is consumed by the recovered client path. | 834/835 mapping and completion consumer；HIGH |

## 已修正的 TS 行為

這次逐欄核對發現一個會被全零 fixture 掩蓋的實作問題：198/247 共用的
`writeMyInfoBasicData` 原先把 `criticals, playCount, roundCount` 寫進 native
`[34..36]` 三個沒有已證實 stat consumer 的保留 word。`PaperMan.exe.c` 的
`sub_523BF0` 只證明這三個 word 會被讀入 CClientData，既有 task/resource
追蹤則把它們標為 reserved；TS 現在保守輸出 zero，不把 Store 欄位硬套進去。
後面的 confirmed wire order 是：

```text
reserved34, reserved35, reserved36,
wins, losses, kills, deaths, disconnects, hearts,
headshots, doubleKill, tripleKill, combos,
multiKill, ultraKill, zKill, kKill, ddKill
```

第三個 level/exp word 仍是 native derived-level slot 的 zero projection，因
client 會由 experience 重算它。這次只修正有直接證據支持的 projection，不新增
server policy，也不改 frame bytes 的欄位數。

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
| 246/247 | `sub_573EB0`, `sub_523BF0`, `sub_524360` | TS public lookup and single-character builder | no 198 character-list tail copied into 247 |
| 254/255, 834/835 | native fixed grammar; 834 `sub_583120` writes shared `dword_F2A684` | current handler/store | 254 context and 834 shared raw context are read/echoed only; no unproven user join |

若未來補上 mailbox/friend/room/inventory/catalog/policy data model，應先在本文件
把相應 `UNRESOLVED` 轉為 direct evidence，再新增欄位用途；不可先以推測命名
取代現有保守欄位。
