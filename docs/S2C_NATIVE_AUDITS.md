# S2C native audits：登入／頻道 handshake（681 → 143/144 → 195/196）

> **合併說明（2026-09-17）**：本檔案由 `S2C_NATIVE_AUDIT_681.md`、
> `S2C_NATIVE_AUDIT_144.md`、`S2C_NATIVE_AUDIT_196.md` 三份登入/頻道准入鏈
> 專項審計合併而成；三份為同一 handshake 串鏈（login 681 → channel TCP
> greeting 693 → 143/144 → 195/196）的三個階段，合併後依流程順序排列，
> 內容與原檔逐字一致（僅標題層級整體下移一級：原 H1 成 H2 part banner）。
>
> 每個 part 的自編章節號（原檔的 ## 1.~## 9.）沿用原文、僅層級 +1；
> 跨 part 章節號不連續是刻意的，勿重新編號以免斷掉原審計的內部語序。

---

## Native audit: `GL_LOGIN_ACK` (681)

Cross-check date: **2026-09-17**
Status: **complete for the recovered native path**. This audit re-reads the
681 receiver, its 694/682 login handshake, the server-list projection and all
recovered endpoint/UI/resource consumers before changing the TypeScript writer.
It separates wire facts from native storage layout, UI labels, and private
server deployment policy.

### 1. Direction, dispatch, and adjacent handshake

- `CLobbyLogin::sub_43E500` (`0x43E500`) is the client-side receiver. It gets a
  packet object, calls `sub_591EE0` to read the opcode, and handles `681` and
  `694` in the same function. Therefore 681 is **server to client**.
- `sub_43DF00` (`0x43DF00`) is the adjacent client writer for `GL_LOGIN_REQ`
  (682). It writes two ANSI strings, the 64-bit guarded data-revision word,
  the fingerprint source byte, and 24 raw bytes. It is not part of 681's
  payload, but proves the preceding login request/response boundary.
- On a 694 packet, `sub_43E500` reads a `u16` threshold, updates the global
  threshold when it is below `0x2580`, reloads the login state with
  `sub_43DF00`, and returns. A 694 is a login-flow trigger, not a 681 body.
- On a 681 packet, the login button and close button are disabled while the
  body is consumed. The client first reads the result word, and only the
  low byte is used for the success test. The surrounding login UI is reset or
  populated after the complete branch; it does not introduce a hidden prefix
  or suffix in the packet.
- The successful extension gate is saved in global `dword_231800C`. The
  adjacent `sub_555C60` (143 request builder) later writes that same global as
  its final s32 after the literal-one byte; `n100` is similarly copied from
  the login global. This confirms the gate's cross-connection echo without
  proving an account, billing, or entitlement name for it.
- After successful 681 processing, `sub_43E450` (`0x43E450`) tears down the
  login UI state, resets the login stage, reloads the normal UI resource state,
  and calls the stored login callback when present. The later channel scene
  constructs `CLobbyChannel` and consumes the stored server-list projection.

The resulting flow is:

```text
694 (server -> client, u16 threshold)
  -> client writes 682
  -> server writes 681
  -> client builds the server/channel selector
  -> client selects a positive-capacity channel
  -> client connects to the server-level host/port from 681
```

The available native code does not prove the original service's account,
entitlement, billing, or server-list policy. The TypeScript deployment keeps
those choices outside the recovered wire grammar.

### 2. Primitive width evidence

The relevant packet helpers are direct byte-copy wrappers in
`0x5926F0..0x592AC0`:

| helper | direction | bytes | evidence |
|---|---|---:|---|
| `sub_592920` | write | 1 | `sub_592580(this, &a2, 1)` |
| `sub_592940` | read | 1 | `sub_592500(this, a2, 1)` |
| `sub_5929C0` | read | 2 | `sub_592500(this, a2, 2)` |
| `sub_592A40` | read | 4 | `sub_592500(this, a2, 4)` |
| `sub_592730` | read string | NUL-terminated ANSI | `lstrlenA`/copy of the encoded string including NUL |

The wrapper bodies are byte-copy primitives, so their C parameter declarations
alone do not prove business names. The cross-file helper audit nevertheless
has a stable wire alias mapping from the native helper family and paired direct
consumers: `sub_592940=u8`, `sub_592A40=s32`, and `sub_5929C0=s16`.
Accordingly, the 681 reader's `sub_592A40` calls are **s32 wire fields** and its
`sub_5929C0` calls are **s16 wire fields**; the native consumers may still leave
their domain semantics unresolved. That is different from `sub_592AC0`, whose
helper identity remains generic `raw4` because its direct callsites do not
establish one numeric interpretation.

The destination local type is still not a license to invent a business name:
`s32`/`s16` here are wire aliases, while field meaning and server policy remain
separate evidence questions.

### 3. Exact 681 read order

`CLobbyLogin::sub_43E500` calls `sub_592A40(a2, v146)` first:

```text
s32 result
```

The native code then branches on `*v146 == 1`, which is the first byte of the
four-byte local result storage. On success it reads:

```text
s32 user_no                 v144
s32 n100                    n100
s32 ext_count               v140
```

For the extension arm:

```text
if ext_count <= 0:
  no extension tuple
else:
  s32 extension_first       v120
  s32 extension_second      v141
  u8  extension_feature     v135
```

The positive branch reads exactly one tuple; it does **not** loop
`ext_count` times. `sub_A1C870(dword_2318008, &v141, &v120, v140)` has no
conversion or validation in its recovered body: it stores `*a2` at native
object DWORD slot `+23` (the second wire word), `*a3` at slot `+24`
(the first wire word), and `a4` at slot `+1` (`dword_231800C`, the gate).
The separately read
`v135` sets `byte_231807D` to a boolean. `sub_44D640(dword_2318008)` only
checks whether the `+1` gate is nonzero; it does not expose a tuple count or
reinterpret either stored s32.

Next, regardless of extension gate, the server list begins:

```text
raw2 server_count            i_1 (__int16 local; loop is i < i_1)
repeat server_count times:
  raw2 server_id             src
  str  server_name           v122, native char[50]
  str  server_host            v124, native char[16]
  raw2 server_port           v125, only the first two bytes are read
  u8   server_flag           v129
  raw2 server_group          v128
  repeat exactly 3 times:
    raw2 max_users           v127
    if max_users > 0:
      u8   channel_type       n3
      str  channel_name       v123, native char[50]
      raw2 current_users      v126
      u8   channel_flag       v132
      if channel_type == 3:
        u8 channel_extra      v131
```

Before reading `server_count`, the receiver calls `sub_58ED80(byte_13242F8)`
to clear the previous 132-byte projection vector. This is state reset, not
wire consumption. After all server rows:

```text
s32 billing_first             v142
s32 billing_second            v137
```

There is no native 681 loop over a variable number of channels inside a group.
The three `max_users` gates are fixed by `for (j = 0; j < 3; ++j)`, and each
positive gate consumes one channel record before the next gate is read.

The signed `__int16 i_1` declaration plus the `i < i_1` loop is why the current
writer caps `server_count` at `0x7fff` and writes it with `s16`. A negative raw
count would make the native loop consume no rows and then reinterpret the next
bytes as the billing tail; it is not a safe server-list configuration.

### 4. Fixed-buffer and scalar guardrails

The native scratch declarations establish these string capacities:

- `v122[50]`: server name, at most 49 ANSI bytes plus NUL.
- `v124[16]`: server host, at most 15 ANSI bytes plus NUL.
- `v123[50]`: channel name, at most 49 ANSI bytes plus NUL.

`Packet::str` is ASCII-only in the server implementation, so its character
length is also the emitted byte length. The writer rejects strings at or above
the native array size rather than letting the native `sub_592730` copy over a
local buffer.

`flag`, `channel_type`, `channel_flag`, `channel_extra`, and the extension
feature byte are each read by `sub_592940`, so they are u8-width fields. The
writer validates before calling the masking `Packet.u8` primitive. It does not
silently turn `0x100` into `0`.

`user_no`, `n100`, `ext_count`, the extension words, and both billing words are
read by `sub_592A40`, so the TypeScript writer validates the signed s32 domain
before calling `Packet.s32`. The raw result is likewise a complete signed s32
word; the low-byte branch does not change its wire width.

The server id, group, max-users, and current-users values are all two-byte
wire fields. The native code proves a positive `max_users` gate and later
compares `max_users` with `current_users`, but it does not provide a universal
unsigned domain for any of these fields. The writer therefore keeps
`serverId` and `group` as raw16 bit patterns (signed notation or `0..0xffff`)
and validates the two operational fields only as signed s16 values. A
non-positive max gate emits no channel body; a positive gate still requires a
body so the following group/tail cannot be misread. Missing group slots are
written as empty gates and extra input slots are ignored, matching the native
fixed three-iteration reader without adding an object-shape restriction.

### 5. The 132-byte native channel projection

For every positive group gate, the receiver fills a stack layout beginning at
`src` and calls `sub_58E690(byte_13242F8, src)`. The local addresses in
`sub_43E500` make the copied 132-byte projection explicit:

| projection offset | scratch source | native role |
|---:|---|---|
| `+0..1` | `src` | server id raw2 |
| `+2..51` | `v122` | server name, 50-byte buffer |
| `+52..101` | `v123` | current channel name, 50-byte buffer |
| `+102..117` | `v124` | server host, 16-byte buffer |
| `+118..119` | `v125` first two bytes | server endpoint raw2 |
| `+120..121` | remaining scratch bytes | no independent wire field proven |
| `+122..123` | `v126` | current-users raw2 |
| `+124..125` | `v127` | max-users raw2 |
| `+126..127` | `v128` | server group raw2 |
| `+128` | `v129` | server flag u8 |
| `+129` | `n3` | channel type u8 |
| `+130` | `v131` | type-3 extra u8 when present |
| `+131` | first byte of `v132` | channel flag u8 |

This projection is native storage, not an additional wire record. The server
row fields preceding each of the three group gates are repeated in the local
scratch used to create each positive-channel projection; the wire itself has
one server header followed by the three gates/conditional records.

`sub_58E690` uses the vector helpers `sub_58F120`, `sub_590160`, and
`sub_58FF80`. `sub_58E670` compares the projection's first byte for one sort,
and `sub_58E640` compares offset 129 for another. `sub_58E970` clears and
rebuilds the auxiliary `this+8` vector from each stored projection's first
word after those sorts; `sub_58E890` is the indexed projection lookup used by
the UI. None of these helpers adds bytes to 681 or turns the 132-byte internal
projection into a second protocol field.

### 6. Endpoint consumer: server port is not channel USERS data

The selected channel path is `CLobbyChannel::sub_4176C0`:

1. It obtains a stored projection with `sub_58E890(byte_13242F8, index)`.
2. It returns the full/unavailable result when projection `+124` is less than
   or equal to projection `+122`.
3. Otherwise it calls `sub_58AD90(byte_13242F8, index)`.

`sub_58AD90` passes projection `+102` and projection `+118` to:

```c
sub_554810(&dword_1321D00, host, port)
```

`sub_554810` has the native signature
`(SOCKET *, char *, u_short)`. It creates `socket(AF_INET, SOCK_STREAM,
IPPROTO_TCP)`, writes `inet_addr(host)` and `htons(hostshort)` into a sockaddr,
and calls `WSAConnect`. This independently proves that the server-level
`server_port` field is the selected server's TCP endpoint and that the native
consumer uses its low 16 bits as an unsigned Winsock port.

By contrast, `sub_416DA0` formats projection `+122` and `+124` as
`"%d/%d"` for the `USERS` UI field, and `sub_4176C0` compares the same pair
for fullness. The channel field is therefore `current_users`/USERS numerator;
it is not another network port.

The TypeScript model retains `GameServer.port` for the endpoint and uses one
`ChannelGroup` per fixed group with `maxUsers` plus at most one `Channel`. It
rejects a positive gate without a channel and a channel without a positive
gate, preventing a body from being shifted into the next group.

### 7. Server-list UI and resource branches

The channel scene constructor `CLobbyChannel::sub_415B80` loads
`LOBBYCHANNEL.xml`, constructs `CLobbyServerData`, checks the native net-cafe
state, and calls `sub_4169E0` and `sub_416DA0` to populate the `SERVERS`
control. The extracted `pm_lobbydata.dat` has a `LOBBYCHANNEL` layout and
button/scroll geometry, but no endpoint schema; it cannot rename wire fields.
`Extracted/ui/system/netcafe_contents.xml` is a separate UTF-16 resource
containing three `NetCafe_1..3` entries with `number`, `grade`, `logo`,
`boost_exp`, `pg_exp`, `cash_discount`, `pg_discount`, and `level_limit`
attributes. This is direct evidence for a client NetCafe configuration table
used by the UI/resource layer, not a mapping from those values to the 681
extension words or feature byte; no such byte-to-resource join is recovered.

Recovered consumers include:

- `sub_416DA0`: enumerates the server-data rows, writes server names and
  server-kind UI text, writes `USERS` as current/max, and chooses channel
  state resources. For type 3 it maps projection `+130`—the optional
  type-3 extra byte, not `ch_flag`—through a small native switch to a
  resource/state branch. This is a UI projection, not proof that every value
  is a server business enum.
- `sub_4176C0`: selection/fullness/connect path described above.
- On the next channel handshake, `CLobbyChannel::sub_4179D0` handles 144 and
  calls `sub_56FF40(projection +129, projection +131)`. The 195 writer then
  sends the selected channel type and channel flag, plus a separate local
  option byte. This cross-checks `+129` as `ch_type` and `+131` as `ch_flag`,
  while leaving the type-3-only `+130` byte as a raw UI/state value.
- `sub_416C00`: rebuilds `CLobbyServerData` entries from the stored channel
  projections, aggregates the native row data and channel names, and keeps the
  lobby's server-data vector. The repeated construction/copy helpers
  (`sub_418340`, `sub_4185E0`, `sub_419760`, `sub_4197F0`) operate on native
  objects after wire read; they do not add wire fields.
- `sub_415B80`: enables/disables `CHANNEL_NETCAFE` from the global feature
  gate. The same `dword_231800C` gate is used by the clan and game-room
  `*_NETCAFE` controls, so the extension has a broader UI gate than the
  channel selector alone. This is the consumer chain that makes a positive
  681 extension gate a meaningful UI state, but it does not prove the original
  service's policy.
- `byte_231807D`, loaded from the extension's feature byte, is consumed in
  item/profile filtering and sorting paths (`sub_45EF70` callers,
  `sub_6BE1F0`/`sub_6BE290`, and `sub_6C2E00`) as well as other lobby/game
  branches. It is therefore intentionally kept as a raw feature flag; the
  `CHANNEL_NETCAFE` control itself is gated by `dword_231800C`, not by this
  byte. No business name is assigned to either value.
- `sub_7092C0`: after successful login, the client creates a Tricod argument
  block through `sub_440420`. It puts `user_no`, `billing_first`,
  `billing_second`, constant `5`, and another zero into that block before
  calling `sub_7092C0`. The call is guarded by the local UI path
  (`this+408 == 0`). `sub_7092C0` only forwards the block to the
  Tricod/logging subsystem when that subsystem is enabled; this is client-side
  telemetry/billing context, not proof of server field names or authorization
  semantics.

The failure branch in `sub_43E500` also consumes no body beyond the result word.
Known low-byte UI branches are 2, 0xC8 through 0xD6, with resource/message
lookups and a hard-coded GM-account/IP message for 0xD6. Unknown result words
remain raw; their presence does not authorize a success body.

### 8. Extension and billing conclusions

`ext_count` is a real s32 grammar gate. The positive arm is structurally safe
only when the sender has an official, reproducible tuple configuration because
it changes the native feature object, the `*_NETCAFE` UI gates, and the
separate raw feature-flag state. The TypeScript writer now exposes this as
`Success.rawExtension`, with raw names and exact validation, but leaves it
absent in production login flow so the emitted default remains `s32 0`. A
positive gate emits exactly one `s32,s32,u8` tuple even when the gate is
greater than one.

The final two billing words are fixed s32 values and are consumed by the
Tricod argument block. Their original service semantics remain unresolved.
They remain explicit raw s32s in deployment code rather than being inferred as
cash, account balance, route, or entitlement fields.

### 9. Cross-check against current TypeScript

- `server-ts/src/ops/s2c/GL_LOGIN_ACK.ts` writes failure as one complete s32
  and writes all success fields in the native order.
- The success extension default is still the conservative zero-gate arm;
  explicit positive extension data is validated as a raw one-tuple projection.
- Server count uses signed s16-compatible values (`0..0x7fff`) because the
  native local is `__int16` and the loop is signed.
- Server id/group preserve raw16 bit patterns. The endpoint port is written as
  u16 because its consumer is a native Winsock `u_short`; this does not relabel
  the raw read helper globally.
- All three group gates are always written. A positive gate writes exactly one
  channel body, and type 3 alone writes its extra byte; omitted input slots are
  empty and surplus input slots are ignored.
- Fixed ANSI buffer limits and all direct u8/s16/s32 widths are validated
  before masking/coercing writer primitives.
- No deployment semantic is inferred for flag, group, billing words, or the
  raw extension tuple.

The source-audited wire change is limited to making the already-proven
positive extension arm explicitly representable. It does not enable a guessed
feature configuration in `main.ts` or loosen channel/login admission policy.

---

## Native audit: `PM_UDPSTART_ACK` (144)

Cross-check date: **2026-09-17**
Status: **complete for the recovered native path**. This note records the
native dispatcher entry, the complete reader, every recovered cross-packet
consumer, and the resource/UI checks for the channel bootstrap acknowledgement.
It does not promote the two unused words, the propagated raw context, or the
optional NetCafe tail to business names that the executable does not prove.

### 1. Entry, caller, and adjacent handshake

The native channel lifecycle is:

```text
channel TCP connect
  -> GL_TCPCONNSUCC (693)
  -> PM_UDPSTART_REQ (143, sub_555C60)
  -> PM_UDPSTART_ACK (144, sub_555D50)
  -> GC_ENTERCHANNEL_REQ (195, sub_56FF40)
  -> GC_ENTERCHANNEL_ACK (196, CLobbyChannel::sub_4179D0)
```

`sub_555D50` is selected by the main network dispatcher `sub_58B010` when the
incoming opcode is `144`. The request writer `sub_555C60` is reached from the
693 channel-TCP greeting path. It writes:

```text
str String[24]
s32 n100
u8  literal 1
s32 dword_231800C
```

The two integers are copied from native channel/login state, but their service
domain is not proven by the writer. 143/144 therefore remains a handoff and
admission handshake, not a password or token protocol.

### 2. Exact native reader order

`sub_555D50(a1)` reads the following bytes before branching on the first byte.
The helper names are significant: `sub_592940` is a one-byte read,
`sub_592A40` is the audited `s32` reader (the local destination may still be used as domain-raw storage), `sub_592730` reads the native NUL string,
and `sub_592B40` reads a native little-endian `f32`.

```text
u8    result                         n108
u8    rank_or_server_flag            v65
s32   daily_login_value              dword_1D0D23C
str   raw_string_v71                  v71, fixed local 40-byte destination
s32   post_name_raw_0                v72
s32   post_name_raw_1                v68
s32   restriction_value_raw          v70[4] (low-byte consumer)
f32   restriction_value_float        v75
raw4  client_request_context         v69 -> dword_F2A684 (sub_592AC0)
u8    has_net_cafe_info              v66
if v66 != 0:
  u8    net_cafe_byte_0              v73
  u8    net_cafe_byte_1              v63
  u8    net_cafe_byte_2              v64
  u8    net_cafe_byte_3              v67
  s32   net_cafe_slot[0..7]          v62[0..7] (domain-raw slots)
```

The native local declarations sometimes make the fields look narrower than
the wire grammar. In particular, `v70` is `char[4]`, but the reader still calls
`sub_592A40` s32; it consumes four bytes. The restriction UI later uses only
`*v70`, the low byte, including `*v70 - 1` in two messages. The server must
therefore preserve the four-byte position; it must not replace the field with a
one-byte wire field.

`dword_1D0D23C`, the post-name/restriction locals, and the optional `sNetCafeInfo`
word inputs are all four-byte `s32` wire paths; `dword_F2A684` is the separate
`sub_592AC0` generic raw4 context path. The current TS writer emits the mandatory prefix and
uses `u32(0)` for the context, which preserves the four bytes even though the
current projection has no context owner.

### 3. Reader-side storage and direct consumers

| wire field | native storage / consumer | what is directly proven |
|---|---|---|
| `result` | `if (n108 != 0)` and the explicit cases `1..10`, `101..108` in `sub_555D50` | raw `u8` result; `0` is the failure/default UI branch; unknown nonzero values use the default account/login-error resource path |
| `rank_or_server_flag` | copied to `byte_132432D`; later channel/lobby UI checks `byte_132432D == 1 && n10_2 > 10` and shows resource `0x11C` | a binary admission/UI flag that participates in the rank-over-10 restriction notice; no broader enum is proven |
| `daily_login_value` | copied to `dword_1D0D23C`; later lobby progress/UI code tests `> 0`, formats resource `0xC9` with the value, displays it, then clears it | a positive login-reward notice value; the executable does not prove a currency name from the bytes alone, although the localized text identifies the displayed unit as PG |
| `raw_string_v71` | `sub_592730` fills the local 40-byte buffer `v71`; no later use of `v71` is recovered in `sub_555D50`, and the nearby `sub_4B56C0` call copies a different buffer returned by `sub_401B20` | a NUL string with a 40-byte native local destination; its channel/display/identity domain is **not proven** by this reader |
| `post_name_raw_0/1` | read into `v72`/`v68`; no recovered use follows in `sub_555D50` or the audited consumer search | exact s32 wire fields, conservatively reserved/unknown domain |
| `restriction_value_raw` | `*v70` is formatted into resources `0x31B`, `0x32D`, `0x321`, `0x334`; messages `0x32D` and `0x334` use `*v70 - 1` | s32 wire field whose currently recovered UI consumer uses the low byte as a level/value; the field is not proven to be a general signed `s32` domain |
| `restriction_value_float` | formatted with `%.1f` into resources `0x31C`, `0x32D`, `0x321`, `0x334` | native `f32` and a restriction-message numeric value; the executable does not establish a server-side K/D policy |
| `client_request_context` | copied byte-for-byte to `dword_F2A684`; later builders write that global into 119, 125, 344/346/348/350, 419, 439, 820, and 834 | shared raw4 context propagated across unrelated client requests; no user/account/session semantic owner is proven |
| `has_net_cafe_info` | sets both `byte_EE8CB1` and `byte_EE896C`; these gates are consumed by NetCafe/discount/feature paths and lobby/game UI | presence gate for a native optional feature object; it is not itself a billing result code |
| optional four bytes | `sub_A1C800(dword_2318008, v62, &v67, &v63, &v64, &v73)` stores three values in the native NetCafe object and one in `dword_23180A8`; `v62` is passed to `sub_A1C910` | four independent `u8` wire values; the parameter order is proven, business names are not |
| optional eight raw4 values | `sub_A1C910` stores the eight pointer/handler values in the native NetCafe object and resets its local counters | eight raw4 slots used to initialize client-side NetCafe action/resource state; no wire business names or server policy are proven |

The exact `sub_A1C800` assignments are:

```text
native NetCafe +100 = v64
native NetCafe +104 = v63
native NetCafe +108 = v67
dword_23180A8     = v73
sub_A1C910(v62)   = eight raw4 values
```

These are storage facts, not license to call the values `number`, `grade`,
`logo`, discounts, or limits. The offsets are also native object offsets, not
wire offsets.

### 4. Result branches and resource cross-check

`sub_555D50` reads the entire fixed prefix and the optional gate before
executing the result UI branch. The known native branches are:

| result | native action / resource | evidence level |
|---:|---|---|
| `0` | closes the channel and shows resource `0x42` | HIGH for raw branch; localized business wording remains resource-dependent |
| `1` | success state (`n2_10=2`, `n3_4=3`) | HIGH |
| `2` | alternate success state (`n2_10=2`, `n3_4=0`) | HIGH; no distinct business name beyond the branch |
| `3` | closes channel; resource `0xA4` | HIGH |
| `4` | closes channel; resource `0xCF` | HIGH |
| `5` | closes channel; resource `0x11B` | HIGH |
| `6` | resource `0x31B`, formats the raw restriction value | HIGH |
| `7` | resource `0x31C`, formats the `f32` value | HIGH |
| `8` | resource `0x32D`, formats `f32` and raw-low-byte-minus-one | HIGH |
| `9` | resource `0x321`, formats `f32` and raw-low-byte | HIGH |
| `10` | resource `0x334`, formats `f32` and raw-low-byte-minus-one | HIGH |
| `101..107` | close channel and show resources `0xB6`, `0x10B`, `0x98`, `0xDB`, `0x11E`, `0x3F`, `0xC8` respectively | HIGH for code/resource mapping |
| other nonzero | default account/login-error branch, resource `0x11D`; result remains raw | HIGH |

`Extracted/ui/lang/msgtableres.lang` is a secondary UI check for these resource
indices. It can corroborate the displayed text selected by the executable, but
it cannot turn the two unused words, context, or NetCafe slots into server
policy. `Extracted/ui/system/netcafe_contents.xml` directly proves that the
client ships a three-entry NetCafe UI/configuration table with `number`,
`grade`, `logo`, boost/discount, and level-limit attributes. It does **not**
join any of those attributes to the 144 optional wire positions; the optional
wire slots therefore remain raw.

The rank condition is also deliberately narrower than a protocol enum: the
recovered UI checks the flag together with the separately stored native rank
`n10_2 > 10`. It does not prove that every nonzero flag means "rank
restricted" in every result code.

### 5. Cross-packet context flow

`dword_F2A684` is assigned only by the audited 144 reader in this path. Later
native packet writers copy it unchanged into multiple unrelated requests,
including:

- message/text-family builders 119, 125, 344, 346, 348, 350, 419, and 439;
- the lobby/data builders 820 and 834.

The 834 reader/acknowledgement path sends this same raw word as a completion
request and returns an empty 835. This proves propagation and framing, not a
user id, account id, login ticket, or server-owned completion token. TS keeps it
as a raw fixed-width value and does not join it to `Store` identity.

`byte_EE8CB1` and `byte_EE896C` are aliases/feature gates initialized from the
same 144 byte. Their many later consumers include NetCafe/discount calculations
and feature-specific lobby/game branches. Those consumers establish that the
presence byte affects client feature availability; they do not establish the
historical server admission policy or the semantic names of the optional
payload slots.

After 144, native `CLobbyChannel::sub_4179D0` sends 195 without checking the
144 result itself. A server implementation must therefore gate the 195/196
follow-up using its own 143 admission state; emitting a failure 144 alone does
not grant authenticated channel authority.

### 6. TS projection and boundary decision

`server-ts/src/ops/s2c/PM_UDPSTART_ACK.ts` intentionally emits:

```text
u8 result
u8 flag
raw4 daily-login raw value
str raw_string_v71
raw4 0
raw4 0
raw4 restriction raw value
f32 restriction value
raw4 0                  // four-byte raw context
u8 0                   // no optional NetCafe tail
```

This is a conservative interoperability projection, not a claim to reproduce
the original service's rank, restriction, billing, or NetCafe policy. It keeps
the native widths and complete mandatory framing on every result. The TypeScript wire
builder keeps the optional four-byte/eight-raw4 shape for
explicit compatibility tests; server-ts currently leaves that gate disabled because
there is no server-side NetCafe model or evidence-backed value source.

The following remain intentionally unresolved:

- the business domains of `post_name_raw_0/1`;
- the complete result policy and the distinction between success code 1 and 2;
- whether the flag has any server meaning beyond the recovered rank/UI branch;
- server ownership of the propagated raw context;
- the four optional bytes and eight optional raw4 slots;
- NetCafe entitlement, discount, level, and persistence policy.

---

## Native audit: `GC_ENTERCHANNEL_ACK` (196)

Cross-check date: **2026-09-17**
Status: **complete for the recovered native path**. This note records the
entire 195/196 channel-selection path that was re-read from `PaperMan.exe.c`,
including the successful type-3 continuation. It intentionally separates wire
facts from UI labels and from unresolved server policy.

### 1. Entry path and direction

- `GL_TCPCONNSUCC` (693) is received by `sub_57CAE0`. It shows the native
  greeting resource `0xFF`, then calls `sub_555C60`.
- `sub_555C60` is the native **client writer** for `PM_UDPSTART_REQ` (143):
  `str String`, `s32 n100`, `u8 1`, `s32 dword_231800C`.
- `PM_UDPSTART_ACK` (144) is handled by `sub_555D50`. The second lobby-stage
  path in `CLobbyChannel::sub_4179D0` receives 144 and unconditionally calls
  `sub_56FF40(group, channel)`, even when 144 reported a failure.
- `sub_56FF40` is the native **client writer** for `GC_ENTERCHANNEL_REQ` (195):
  `u8 group`, `u8 channel`, `u8 rawFlag`.
  - `group` comes from `CLobbyChannel` server-list state at `+129`.
  - `channel` comes from the selected server-list channel at `+131`.
  - `rawFlag` is `sub_7338D0()` followed by `sub_735DE0`; the recovered code
    proves that the writer stores the boolean result as wire byte `0` or `1`.
    Its business domain is unresolved.
-  196 is not handled by the main packet switch in `sub_58B010`. The scene
  layer dispatches it to `CLobbyChannel::sub_4179D0` through the lobby object
  vtable. The function begins by checking the opcode with `sub_591EE0`.

### 2. Exact 196 reader grammar

Primitive calls are the authoritative width evidence:

- `sub_592940` = `u8`
- `sub_592A40` = native `s32`
- `sub_592730` = NUL-terminated ANSI string
- `sub_592AC0` = four copied bytes (`raw4` when no independent type evidence exists)
- `sub_592B40` = the same four-byte copy width; the helper itself does not
  establish signedness or float encoding (not used by the fixed 196 prefix)

`CLobbyChannel::sub_4179D0` reads:

```text
u8   result                 v16
s32  channel_id             v15
u8   channel_index          v17

only when result == 1:
  str  endpoint_host        cp[20] local destination buffer
  s32  endpoint_port        read into u_short[2], then only low u16 is used
  u8   endpoint_opaque      stored at unk_1D0CFE4; no recovered consumer gives it a name
  u8   channel_type         n2
  raw4 client_flags         v11; bit 0 is the only consumed bit
  u8   client_default       n5, initialized to 5 before reading
  if channel_type == 3:
    type-3 continuation parsed by sub_875680
```

The fixed prefix is consumed before any result UI branch. For any non-1
result, the reader consumes exactly the first three fields and then calls
`sub_4177B0(result, channel_index)`.

The native `endpoint_port` variable is declared as `u_short[2]`, but the
reader call is `sub_592A40`, so four bytes are consumed. The subsequent
consumers use only `hostshort[0]` as a Winsock `u_short`:

- `sub_58ED30(cp, hostshort[0])` initializes the client UDP manager.
- `sub_596E60(&unk_1326908, cp, hostshort[0])` stores a second UDP sockaddr.

Therefore the server wire field is `s32`; the practical endpoint value is the
low unsigned 16 bits. TS deliberately requires a normal `1..65535` endpoint
value rather than advertising the native truncation as a server policy.

The endpoint host is copied through `cp[20]`. The native code does not expose a
separate length field; the string primitive is NUL-terminated. The TS builder
limits the ASCII host to at most 19 bytes so the native 20-byte local buffer
has room for the terminator.

### 3. Result branch and UI/resource cross-check

`sub_4177B0` first calls `sub_522440(dword_EE3950)` and then branches on the
raw `u8 result`:

| result | Native action | Resource evidence | Confidence |
|---:|---|---|---|
| 0 | show resource `0xDA`, then reset channel/network state via `sub_417780` | `docs/RESOURCES.md`: channel full / capacity exceeded | HIGH |
| 1 | set `sub_417D00()[0] = channel_index`, set channel state `+119 = 2`; no error popup | success continuation | HIGH |
| 2 | show resource `0x148`, then reset state | rank restriction branch | HIGH |
| 3 | show resource `0x328`, then reset state | clan-required branch | HIGH |
| 4, 5, 7, 9 | show resource `0x1A5`, then reset state | shared generic-error branch | HIGH |
| 6 | show resource `0x3A6`, then reset state | dedicated native resource | HIGH |
| 8 | show resource `0x3A7`, then reset state | dedicated native resource | HIGH |
| other | no `switch` case; returns without one of the above UI mappings | unknown result remains raw | HIGH |

The names `ChannelFull`, `RankRestricted`, and `ClanRequired` are only used
for the directly evidenced UI branches. Unknown result bytes remain raw u8 in
TS; no broader result policy is inferred.

`sub_417780` performs the common failure reset:

1. `sub_58AEB0(byte_13242F8)` calls `sub_58AF90`, which closes the current TCP
   channel connection and clears its active flag.
2. `sub_58AD60(byte_13242F8)` checks the main lobby socket state.
3. `CLobbyChannel +119` becomes 0 and `+113` becomes 3.

After the 196 case, `sub_4179D0` always calls `sub_88DC20(&dword_1D3841C)`
and sets `CLobbyChannel +140 = -1`, regardless of result.

### 4. Success side effects and endpoint consumers

For result 1, `sub_4179D0` performs these operations in this order:

1. Reads all fixed success fields described above.
2. Saves `client_default` to `sub_417D00()[8]`.
3. Sets `byte_1D0D21B = (client_flags & 1) != 0`.
   - Later lobby UI construction checks this byte to include or omit a
     particular UI item (`0x4D8`, seen in room/lobby control construction).
   - No other `client_flags` bit has a recovered consumer in this path.
4. Saves `channel_id` to `sub_417D00()[1]`.
5. Saves `channel_index` only through result-1 handling to
   `sub_417D00()[0]`.
6. Stores `channel_type` in the global `n2_0` used by later game/lobby code.
7. When `channel_type == 3`, calls `sub_875680` before opening the UDP
   endpoint. This is a real additional grammar, not an optional server-side
   decoration.
8. Calls `sub_58ED30(cp, hostshort[0])`:
   - clears/initializes the `CUDPNetworkManager` state;
   - creates and binds its UDP socket through `sub_595730` → `sub_596D60`
     and `sub_595760` → `sub_596DA0`;
   - starts the UDP receive thread with `sub_5957B0`;
   - waits one second;
   - marks the manager active and initializes its timing object.
9. Calls `sub_596E60(&unk_1326908, cp, hostshort[0])`, which writes an IPv4
   sockaddr (`AF_INET`, `inet_addr(cp)`, `htons(hostshort[0])`) into the second
   UDP destination object.

`sub_595A60` receives UDP packets and dispatches their packet opcodes through
`sub_595E80`; this confirms that the successful 196 endpoint is a native UDP
control endpoint. The recovered code does not establish that it is a P2P,
NAT, gameplay, or public-server endpoint, so TS keeps the name `endpoint`.

### 5. Complete type-3 continuation: `sub_875680`

The earlier shorthand `f32×4` was incorrect. `sub_875680` calls
`sub_592AC0` for these four fields, so they are **raw4**, not typed f32.
The full continuation is:

```text
s32   type3_header_0
if type3_header_0 <= 0:
  parser returns 0 and consumes no more type-3 fields

s32   type3_header_1
str   type3_name
raw4  type3_raw4_0
raw4  type3_raw4_1
raw4  type3_raw4_2
raw4  type3_raw4_3
u8    type3_u8_0
u8    type3_u8_1
u8    type3_u8_2
s32   list_count
repeat list_count times:
  s32 list_value                 // native loop has no local cap
u8    small_record_count
u8    small_record_mode
repeat j = 0 .. min(small_record_count, 5)-1:
  u8    small_u8_0
  u8    small_u8_1
  u8    small_u8_2
  u8    small_u8_3
  s32   small_s32_0
  s32   small_s32_1
  raw4  small_raw4_0
  raw4  small_raw4_1
  u8    small_u8_4
u8    type3_u8_3
u8    stage_count
repeat k = 0 .. min(stage_count, 32)-1:
  s32   stage_s32_0
  raw4  stage_raw4_0
  u8    has_stage_name_0
  if has_stage_name_0 != 0:
    str   stage_name_0       // copied by native strncpy(..., 0x20)
  s32   stage_s32_1
  s32   stage_s32_2
  s32   stage_s32_3
  s32   stage_s32_4
  s32   stage_s32_5
  s32   stage_s32_6
  u8    has_stage_name_1
  if has_stage_name_1 != 0:
    str   stage_name_1       // copied by native strncpy(..., 0x1A)
raw4  type3_raw4_final
```

Important parser-boundary facts:

- `list_count` is used by `for (i = 0; i < *(this + 38); ++i)` without the
  `j < 5` / `k < 32` caps used by the later arrays. A malformed or hostile
  type-3 packet can therefore overrun the native object; this is a client
  parser defect, not permission to invent a tournament-record cardinality.
  The TS writer nevertheless rejects a list whose encoded bytes cannot fit
  `Packet`'s 9592-byte payload budget; that is a transport/framing guard only.
- The later two arrays are capped at 5 and 32 records respectively, but their
  count bytes are still consumed before the capped loops. Extra records beyond
  those caps are not skipped; the parser proceeds to the following fields, so
  those extra bytes are reinterpreted as the following fields and can desync
  the enclosing packet path.
- The four fields frequently mistaken for floats are raw4 because the C calls
  are `sub_592AC0`, not `sub_592B40`.
- No recovered caller, resource, or UI field gives reliable business names to
  these type-3 fields. They remain raw/unknown here.
- `sub_875680` is also called from tournament-related handlers such as
  `sub_57E550`; its shared object is a tournament/AI state container, but that
  does not prove a wire business name for the 196 fields.

#### Recovered consumers of the shared tournament object

`sub_417E30()` lazily constructs the one global `CTournamentManager` object.
The 196 type-3 branch calls `sub_875680` on this object. A second recovered
caller, `sub_57E550` (the tournament-all-info receive path), calls the same
reader and then refreshes `CLobbyTournamentMainRoom`; this is why the type-3
fields must not be renamed as if they were exclusive to 196.

The direct consumer audit of fields populated by `sub_875680` is:

| parser storage | recovered consumer | what is actually proven |
|---|---|---|
| `this+11` | `sub_875680` gate | positive value permits the continuation; no domain name proven |
| `this+12` | many `sub_417E30()[12]` branches in lobby/tournament UI | a tournament mode/state discriminator used for UI and room transitions; not a server admission policy |
| `this+13` | `sub_401B20()` in `CLobbyTournamentMainRoom` → `TMENT_NAME` | displayed tournament name string |
| `this+30..32` | no direct consumer recovered in the audited call graph | remain raw4 |
| `this+33` | `sub_48B9A0` → `TMENT_TIME` | packed calendar/time value; the wire field is still only proven as four bytes |
| `this+35` | no direct consumer recovered | raw u8 |
| `this+36` | stored as shared tournament state; no independent 196 semantic label recovered | raw u8 |
| `this+38` and `this+39+i` | `sub_48A510` loop → `sub_531FB0` | count plus repeated s32 values used to draw the tournament/map-side marker list; element domain remains unresolved |
| `this+141` | no direct consumer recovered | raw u8 |
| `this+142` | lobby UI checks it, including the `==4` damage-mark branch; other tournament update code tests bits | raw u8/bit flags; only these consumers are proven |
| `this+143`, small records | tournament-room UI loops; `sub_48B7F0` maps a record's mode byte to display flags; `+44` is formatted as a round/count value, `+45` is passed to `sub_728F40` for a map name, and `+48` is tested as a record flag | these consumer roles are real, but the server field names and all mode-code meanings remain unresolved |
| small-record raw4 at the `+46/+47` slots | selected record's `+47` is decoded by `sub_48B9A0` for `TMENT_TIME`; no independent type proof for the other raw4 | packed time is evidenced for the selected record; other raw4 stays raw |
| `this+491` | no direct consumer recovered | raw u8 |
| `this+145` and the stage-entry block | `sub_875BA0` bounds lookup to 32 entries; `sub_875C20` resolves an entry and copies its native name/emblem projection; tournament room code uses the resulting lookup | stage count is capped at 32; ID/name/emblem-like uses are evidenced, but the wire field names are not |
| `this+494` | written by 196 and by later tournament update readers (`sub_57E5A0`/related path); no 196-specific branch recovered | raw4 shared tournament value |

This table is intentionally narrower than the native object layout: fields not
read by a recovered consumer are not given a business meaning merely because a
UI label or a C local happens to look suggestive. The two `strncpy` limits in
`sub_875680` are the only direct size evidence for the optional stage names;
they do not establish their encoding beyond the surrounding `sub_592730` ANSI
string reader.

The TS builder now writes this continuation as an explicit raw projection,
with the native count caps represented and validated. Channel admission only
accepts type 3 when the connection config supplies a complete raw tail; without
that reproducible configuration it remains rejected. The builder must not emit
a fixed prefix plus only the six success bytes when type 3 is selected.

### 6. Cross-check against current TS

- `server-ts/src/ops/s2c/GC_ENTERCHANNEL_ACK.ts` writes the fixed prefix and
  the non-type-3 success tail in the exact native order.
- It writes `endpoint_port` as `s32`, not `u16`, matching `sub_592A40`.
- It keeps `endpointOpaque`, `clientFlags`, and `clientDefault` raw-oriented;
  only the proven `client_flags & 1` consumer is documented.
- It emits type 3 only with an explicit complete `type3Tail`; missing tails,
  count mismatches, and capped-array overflows are rejected instead of creating
  a false-success packet with a truncated grammar.
- `server-ts/src/ops/c2s/GC_ENTERCHANNEL_REQ.ts` separately validates the
  three-byte 195 request and rejects unauthenticated/mismatched selection;
  this is necessary because native 144 → 195 does not gate on 144's result.
  It also refuses type-3 admission unless the connection config supplies the
  complete raw tail.
- `server-ts/test/channel.test.ts` covers failure-prefix framing, unknown
  failure result preservation, success tail order, numeric widths, and the
  type-3 rejection.

### 7. Remaining unresolved items

These are deliberately not renamed or modeled as server policy:

- `endpoint_opaque` (`unk_1D0CFE4`), because no recovered consumer gives it a
  domain name.
- `client_default` (`sub_417D00()[8]`), beyond the native initialization value
  5 and storage location.
- `client_flags` bits other than bit 0.
- The business semantics of all type-3 continuation fields, including the
  raw4 values and the two optional name blocks; the TS writer deliberately
  exposes them as raw/unknown fields.
- The server-side admission policy that chooses result 0/2/3/4/5/6/7/8/9.
