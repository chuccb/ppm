# server-ts 現行 31 個 Packet 欄位審計

日期：2026-09-16（Asia/Taipei）

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

## 先固定 primitive boundary，再判斷語意

IDA export 的 Packet helper 本身只證明 byte width；不能只看 Hex-Rays 的 C
型別替欄位命名。`PaperMan.exe.c` 的共用 helper 可直接整理為：

| native helper | 實際動作 | TS 對照 | 限制 |
|---|---|---|---|
| `sub_5928E0`、`sub_592920`、`sub_592960` | 寫入 1 byte | `u8`/`s8` | helper 的 `char` 型別不代表業務是 bool/status |
| `sub_592900`、`sub_592940`、`sub_592980` | 讀取 1 byte | `u8`/`s8` | signedness 只在 direct consumer 有比較證據時採用 |
| `sub_5929A0`、`sub_5929C0`、`sub_5929E0` | 寫/讀 2 bytes | `u16`/`s16` | 681 channel/server counts、198 appearance 等再由 consumer 判斷 |
| `sub_592A20`、`sub_592A40`、`sub_592A60`、`sub_592A80`、`sub_592AA0`、`sub_592AC0` | 寫/讀 4 bytes | `s32`/`u32`/`f32`/raw4 | 4-byte width 不等於 user id、page、status 或 float；要看 native consumer |
| `sub_592AE0`、`sub_592B00`、`sub_592B60`、`sub_592B80` | 寫/讀 8 bytes | `u64`/raw8 | 682 guard 由 low/high direct consumer 定義；其他 raw8 不可重命名 |
| `sub_592B40` | 讀 4 bytes | `f32` only where destination is native `float` | helper 自身只是 copy；144 K/D 是 consumer 造成的 f32 語意 |
| `sub_592500`、`sub_592580` | exact raw byte run | `raw`/`zeros` | 48B blob、fingerprint、160B profile block 不能按鄰近欄位猜語意 |
| `sub_5926F0`、`sub_592730` | 寫/讀 NUL-terminated ANSI bytes | `str` | native buffer capacity與encoding另由 caller證明；不存在 wire length prefix |
| `sub_592770`、`sub_5927B0` | 讀 NUL-terminated UTF-16 bytes | `wstr` | 本 31-opcode TS review 沒有把它誤套進 ANSI packet |


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
| 195 `GC_ENTERCHANNEL_REQ` | `u8 group`, `u8 channel`, `u8 rawFlag` | `group` 是 681 廣告的 group 序號，`channel` 是組內 channel index。第三 byte 是由 local option block 產生的 raw flag；`sub_735DE0` 只證明 native 讀取一個設定值，不能直接命名成 replay/module/status。TS 讀取但不以它放行。TS 僅接受已認證且 group/channel 與 config 相符、且拒絕未實作的 `channelType=3`。 | `sub_56FF40`、`sub_4179D0`、`sub_7338D0/sub_735DE0`；HIGH for shape, UNRESOLVED for rawFlag domain |
| 197 `GL_MYINFO_REQ` | empty | 請求自己的 198 MyInfo。TS 從 authenticated account 取得 Store identity 與 NewSkill snapshot；未登入時保留 null/失敗形狀。 | `sub_570550` dispatcher case；HIGH |
| 199 `GL_MYITEM_REQ` | empty | 請求 200 背包 page。TS 目前沒有 inventory/catalog model，回 `success=1,start=0` 加負 slot sentinel。 | `sub_570AB0`、`sub_524B70`；HIGH for shape, TS projection policy only |
| 246 `GL_CLIENTINFO_REQ` | `str nickname` | 以 nickname 查 public MyInfo。TS 將此字串交給 `getMyInfoByNickname`，回 247；沒有把它當 numeric user id。 | `sub_573EB0`、247 consumer；HIGH |
| 250 `GL_LOBBYIN_REQ` | empty | Client-local lobby transition notice；client state 由本地流程處理。TS 嚴格讀空 payload，不回 251，因目前沒有 recovered native 251 consumer。 | `PACKETS.md §3.15pre-2`；HIGH for empty/no-consumer boundary |
| 252 `GL_SHOPIN_REQ` | empty | Client 在送出前已進入 shop state 3；TS 回空 253 是 interoperability choice，不宣稱 native shop-success payload。 | `PACKETS.md §3.15pre-2`；HIGH for request, bounded TS policy for reply |
| 254 `GL_INVENIN_REQ` | `u8 requestContextRaw` | Client-local inventory/NewSkill entry context。其 UI/entity/page/tab 語意 **UNRESOLVED**；TS 原樣 echo 到 255，不解讀。TS 以 authenticated account 的 `uid` 取得五個 NewSkill profiles。 | `sub_573EB0`/254 caller、255 mode-1 consumer；HIGH for echo, UNRESOLVED for context |
| 425 `GL_MSG_RECVLIST_REQ` | `s32 rawRequestValue` | Native builder `sub_55A580` writes 4 bytes from the integer returned by `sub_44E480`; the UI sends the initial value `1` and also values adjusted by `±10` in `sub_6DC6C0/sub_6DC760`. This proves a four-byte integer request value, but not mailbox/page semantics. TS reads it and ignores it because no mailbox/page model is recovered. | `sub_55A580`、`sub_44E480`、`sub_6DC6C0/sub_6DC760`、`sub_55A630` ACK consumer；HIGH for width/data flow, UNRESOLVED semantics |
| 433 `GL_FRIEND_LIST_REQ` | empty | 請求 current account 的 friend list。TS 尚無 friend table，回 434 的 zero-count projection，並提供 request-account nickname。 | `sub_55AFC0`；HIGH for empty request and ACK shape |
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
| 108 `GL_GAMEROOMINFO_ACK` | `u8 mode`, `u8 count`, then `count` room records | `mode=0` 是 ordinary room-list branch，`count=0` 使 client 不讀任何 room fields。TS 不捏造 room title/map/player flags。 | `sub_568CE0`、`LAYOUTS.md`；HIGH |
| 144 `PM_UDPSTART_ACK` | `u8 result`, `u8 rank_restricted_server_flag`, `s32 daily_login_reward_pg`, `str channel_name`, `s32 reserved_after_name_1`, `s32 reserved_after_name_2`, `s32 channel_restriction_level`, `f32 channel_restriction_kdr`, `raw4 client_request_context`, `u8 has_net_cafe_info`, optional `4×u8 + 8×raw4` | Client 在所有 result 上先讀完整固定 prefix。result 是 raw u8；TS 保留已知 UI constants，但未知 code 也原樣可發送。rank flag 在 rank>10 時顯示限制；daily PG 只在正值顯示通知；level/KDR 是 restriction message values。兩個 name 後 s32 維持保守 reserved。`client_request_context` 是 raw4，會被後續 request 原樣帶入但 domain 未證實。TS 預設 zero/empty 並 `has_net_cafe_info=0`，所以不發 optional block。 | `sub_555D50`、`sub_592AC0`、`sub_A1C800`；HIGH for shape/consumer, UNRESOLVED reserved/context/net-cafe fields |
| 196 `GC_ENTERCHANNEL_ACK` | `u8 result`, `s32 channel_id`, `u8 channel_index`; only success: `str udp_host`, `s32 udp_port`, `u8 endpoint_opaque`, `u8 channel_type`, `raw4 client_flags`, `u8 client_default` | Failure 只有前三欄。result 是 raw u8；TS 保留已知 UI constants，但未知 non-success code 也只寫前三欄。成功 endpoint 是 client 後續 private UDP control address；port wire 是 s32，但 native 取 low u16。`endpoint_opaque`、`client_flags`（bit0 已知）、`client_default` 保持 raw-oriented。TS 只有成功才寫 tail；channel type 3 需要尚未實作的 AI tail，因此 current TS 不接受 type 3 config。 | `sub_4179D0`、`sub_58ED30`/`sub_596E60`、`sub_875680`；HIGH |
| 681 `GL_LOGIN_ACK` | `s32 result`; only success (`result=1`) continues with `s32 user_no`, `s32 n100`, `s32 ext_count`, `raw2 server_count`, server records (`raw2 server_id`, `str name`, `str host`, `raw2 port`, `u8 flag`, `raw2 group`, three groups of `raw2 ch_count` plus optional channel `{u8 ch_type,str ch_name,raw2 ch_port,u8 ch_flag,[u8 extra when type=3]}`), and fixed `s32 billing_first`, `s32 billing_second` | Failure is exactly the result word and the native branch tests its low byte. `user_no` remains the official conservative name; TS supplies verified account row id without claiming it is the later 198 user row. `n100` is opaque charge/billing UI mode and is echoed by 143. `ext_count>0` makes the native reader consume exactly one `{s32,s32,u8}` extension triple and pass it to `sub_A1C870`; TS deliberately emits 0. The server-list fields are all consumed in the read loop, but the recovered handler does not prove endpoint/flag/group domains. The reader stores a 132-byte internal projection rooted at the local `server_id` scratch (`sub_58E690`); this is a stack-layout copy used by native sorting/lookup and is not a second wire field or proof of additional field semantics. TS requires exactly three channel groups. The native reader consumes one channel record when a group count is positive and then advances to the next group, so TS rejects more than one per group to preserve framing. The two billing words are copied into the later Tricod argument block as raw values; their business names remain unresolved. | `sub_43E651` 681 branch、`sub_58E690`/`sub_58F120`/`sub_58E640`/`sub_58E670`、`sub_7092C0`、`PACKETS.md §1.4`；HIGH for shape/reader condition and internal-copy fact, UNRESOLVED 2-byte field signedness and flag/group/billing domains |
| 247 `GL_CLIENTINFO_ACK` | `u8 ok`; if ok: shared 198 basic block, then `u8 slot`, `u8 char_type`, `12×u16 appearance` | `ok=0` 只有一 byte。成功首段與 198 的 `sub_523BF0` 完全共用；尾端是單一 character appearance，不是 198 的 character list、weapon groups 或 NewSkill tail。`slot` 是 serialized character-list slot/index；`char_type` 才是角色類型。TS 從 selected slot 選一筆並寫 12 個 category-relative u16。 | `sub_573EB0`、`sub_523BF0`、`sub_524360`、`sub_524010`；HIGH |
| 253 `GL_SHOPIN_ACK` | empty | 沒有 recovered native shop success payload。TS 的空 ACK 是明確標成 interoperability response，不把它寫成官方成功資料。 | `PACKETS.md §3.15pre-2`、未找到 253 consumer；HIGH for current boundary |
| 255 `GL_INVENIN_ACK` | Common prefix `u8 mode`, `s32 uid`, `u8 requestContextRaw`, `u8 unknownHeaderRaw`; native mode 0 then reads two more `u8` values and one `s32` remote lookup value. Native mode 1 then reads `u8 selectedProfile` (`<5`) and 160 raw bytes = 5 profiles × (`7×s32 puzzleItemId`, `s32 expiresAtPackedMinute`). | TS 只產生 mode 1 local-user snapshot。`uid` 是 profile/account-level addressing，沒有 character index；不可與 198/247 appearance record 混用。`requestContextRaw` 只結構性 echo；`unknownHeaderRaw` 仍 0；expiry 是 server-owned packed-minute value。Profiles 的 puzzle IDs 是目前 NewSkill persistence projection；資源 XML 只作顯示/合成/配色參考，不是 grant authority。Mode 0 is not emitted because 254 never selects it. | `sub_574270`、255 fixed reader、`sub_527D00`/NewSkill resources；HIGH for grammar, MEDIUM for profile domain, UNRESOLVED context/header/remote fields |
| 426 `GL_MSG_RECVLIST_ACK` | `raw2 rawHeader`, `str self`, `u8 count`; each record: `str from`, `u8 raw`, `str title`, `s32 msg_id`, `str body`, `str raw`, `s16 date` | Native `sub_55A630` reads the message id into `int v37` and the date into `__int16 v35`; those signed widths are kept even though TS currently emits count 0. `rawHeader` remains a conservative 2-byte field and cannot be called page/tab/status; record raw fields are also not given invented meanings. | `sub_55A630`、`sub_5378C0` (`int a5`, `__int16 a8`)；HIGH for order/signedness, UNRESOLVED raw fields |
| 198 `GL_MYINFO_ACK` | `u8 success`; if success: `s32 user_id`, shared basic block, character list, weapon groups, 9 UI slots, selected NewSkill raw block, tail | `user_id` 是 MyInfo wire user id；TS 使用 Store player `userId`。basic block：`str nickname`, `u8 selected_char_index`, `s32 level`, `s32 experience`, native derived-level slot, then 18 words in native order: `[reserved34,reserved35,reserved36,wins,losses,kills,deaths,disconnects,hearts,headshots,doubleKill,tripleKill,combos,multiKill,ultraKill,zKill,kKill,ddKill]`, followed by `u8×3 flags`, `s32 cash`, `s32×2 raw`, `48B extra blob`, `u8 slot_current`。`reserved34..36` have no proven task/stat consumer; TS keeps them zero rather than mapping `criticals/playCount/roundCount` by guess. The first dword of the 48B blob is the cumulative play-time task counter (`Stats.playTimeSeconds`); the remaining 44 bytes are still zero because mode counters are not modelled. `derived-level` is recomputed from exp by the client; flags and raw words remain conservative.接著是 `u8 char_count` + 每筆 `u8 char_type + 12×u16`；最多 20。再是最多 4 組 weapon loadout（group 3 只有 primary），9×s32 UI-item slots；再是 raw `u8 n5`（current compatible value 5）+ selected profile 7×s32；最後 `u16 pending_gift_count`, `s32 game_point`, `u8 tutorial_count` 及 records。TS 只填 Store 已有資料，其餘使用已證實的 zero/empty projection。 | `sub_570550`、`sub_523BF0`、`sub_524010`、`sub_524660`、`sub_527550`、`sub_527D00`、`RESOURCES.md §5c-1/§5c-2`；HIGH for order/grammar, UNRESOLVED raw/n5 domains |
| 200 `GL_MYITEM_ACK` | `u8 success`, `s32 start_index`; repeated item: `s32 inv_slot`, `s32 item_id`, `f32 f1`, `f32 f2`, `s32 period`, `u8 extra`, `u16 durability`; terminal negative `s32 inv_slot` | `start_index=0` is current empty-page projection. `f1/f2` are the two native float item values; `sub_524B70` stores them without establishing an item-domain name. Exact ability/roll interpretation is resource/client data, not a reason to rename wire fields. `period` is remaining days in this reader. `extra` is deliberately unresolved (not guessed as kind/status). TS now exposes optional `extra` and defaults it to 0; each page allows at most 100 records and always writes `-1` sentinel. | `sub_570AB0`、`sub_524B70`、`LAYOUTS.md`、`RESOURCES.md §5d-4`；HIGH for shape, UNRESOLVED `f1/f2` domains, `extra`, and server item authority |
| 434 `GL_FRIEND_LIST_ACK` | `raw2 rawHeader`, `str self`, `u8 count`; each record: `str nickname`, `s32 status` | TS zero projection writes unresolved header 0, request-account display nickname, and count 0. `self` is not renamed friend-owner ID; no friend records are fabricated. | `sub_55AFC0`；HIGH for order, UNRESOLVED header/status |
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
| 433/434 | `sub_55AFC0` | TS output | self string + count confirmed; header/status remain raw |
| 246/247 | `sub_573EB0`, `sub_523BF0`, `sub_524360` | TS public lookup and single-character builder | no 198 character-list tail copied into 247 |
| 254/255, 834/835 | native fixed grammar; 834 `sub_583120` writes shared `dword_F2A684` | current handler/store | 254 context and 834 shared raw context are read/echoed only; no unproven user join |

若未來補上 mailbox/friend/room/inventory/catalog/policy data model，應先在本文件
把相應 `UNRESOLVED` 轉為 direct evidence，再新增欄位用途；不可先以推測命名
取代現有保守欄位。
