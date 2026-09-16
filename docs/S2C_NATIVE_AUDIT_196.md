# Native audit: `GC_ENTERCHANNEL_ACK` (196)

Status: **complete for the recovered native path**. This note records the
entire 195/196 channel-selection path that was re-read from `PaperMan.exe.c`,
including the successful type-3 continuation. It intentionally separates wire
facts from UI labels and from unresolved server policy.

## 1. Entry path and direction

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
    only proves that it is a local-option/config-derived byte. Its business
    domain is unresolved.
-  196 is not handled by the main packet switch in `sub_58B010`. The scene
  layer dispatches it to `CLobbyChannel::sub_4179D0` through the lobby object
  vtable. The function begins by checking the opcode with `sub_591EE0`.

## 2. Exact 196 reader grammar

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

## 3. Result branch and UI/resource cross-check

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

## 4. Success side effects and endpoint consumers

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

## 5. Complete type-3 continuation: `sub_875680`

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
  parser defect, not permission to invent a smaller server grammar.
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

### Recovered consumers of the shared tournament object

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

## 6. Cross-check against current TS

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

## 7. Remaining unresolved items

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
