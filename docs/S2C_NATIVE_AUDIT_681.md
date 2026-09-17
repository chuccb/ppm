# Native audit: `GL_LOGIN_ACK` (681)

Cross-check date: **2026-09-17**
Status: **complete for the recovered native path**. This audit re-reads the
681 receiver, its 694/682 login handshake, the server-list projection and all
recovered endpoint/UI/resource consumers before changing the TypeScript writer.
It separates wire facts from native storage layout, UI labels, and private
server deployment policy.

## 1. Direction, dispatch, and adjacent handshake

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

## 2. Primitive width evidence

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

## 3. Exact 681 read order

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

## 4. Fixed-buffer and scalar guardrails

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

## 5. The 132-byte native channel projection

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

## 6. Endpoint consumer: server port is not channel USERS data

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

## 7. Server-list UI and resource branches

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

## 8. Extension and billing conclusions

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

## 9. Cross-check against current TypeScript

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
