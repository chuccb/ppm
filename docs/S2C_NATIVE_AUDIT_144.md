# Native audit: `PM_UDPSTART_ACK` (144)

Cross-check date: **2026-09-17**
Status: **complete for the recovered native path**. This note records the
native dispatcher entry, the complete reader, every recovered cross-packet
consumer, and the resource/UI checks for the channel bootstrap acknowledgement.
It does not promote the two unused words, the propagated raw context, or the
optional NetCafe tail to business names that the executable does not prove.

## 1. Entry, caller, and adjacent handshake

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

## 2. Exact native reader order

`sub_555D50(a1)` reads the following bytes before branching on the first byte.
The helper names are significant: `sub_592940` is a one-byte read,
`sub_592A40` copies four raw bytes, `sub_592730` reads the native NUL string,
and `sub_592B40` reads a native little-endian `f32`.

```text
u8    result                         n108
u8    rank_or_server_flag            v65
raw4  daily_login_value              dword_1D0D23C
str   raw_string_v71                  v71, fixed local 40-byte destination
raw4  post_name_raw_0                v72
raw4  post_name_raw_1                v68
raw4  restriction_value_raw          v70[4]
f32   restriction_value_float        v75
raw4  client_request_context         v69 -> dword_F2A684
u8    has_net_cafe_info              v66
if v66 != 0:
  u8    net_cafe_byte_0              v73
  u8    net_cafe_byte_1              v63
  u8    net_cafe_byte_2              v64
  u8    net_cafe_byte_3              v67
  raw4  net_cafe_slot[0..7]          v62[0..7]
```

The native local declarations sometimes make the fields look narrower than
the wire grammar. In particular, `v70` is `char[4]`, but the reader still calls
`sub_592A40`; it consumes four bytes. The restriction UI later uses only
`*v70`, the low byte, including `*v70 - 1` in two messages. The server must
therefore preserve the four-byte position; it must not replace the field with a
one-byte wire field.

`dword_1D0D23C`, `dword_F2A684`, and the optional `sNetCafeInfo` assignments are
also raw four-byte paths. The current TS writer emits the mandatory prefix and
uses `u32(0)` for the context, which preserves the four bytes even though the
current projection has no context owner.

## 3. Reader-side storage and direct consumers

| wire field | native storage / consumer | what is directly proven |
|---|---|---|
| `result` | `if (n108 != 0)` and the explicit cases `1..10`, `101..108` in `sub_555D50` | raw `u8` result; `0` is the failure/default UI branch; unknown nonzero values use the default account/login-error resource path |
| `rank_or_server_flag` | copied to `byte_132432D`; later channel/lobby UI checks `byte_132432D == 1 && n10_2 > 10` and shows resource `0x11C` | a binary admission/UI flag that participates in the rank-over-10 restriction notice; no broader enum is proven |
| `daily_login_value` | copied to `dword_1D0D23C`; later lobby progress/UI code tests `> 0`, formats resource `0xC9` with the value, displays it, then clears it | a positive login-reward notice value; the executable does not prove a currency name from the bytes alone, although the localized text identifies the displayed unit as PG |
| `raw_string_v71` | `sub_592730` fills the local 40-byte buffer `v71`; no later use of `v71` is recovered in `sub_555D50`, and the nearby `sub_4B56C0` call copies a different buffer returned by `sub_401B20` | a NUL string with a 40-byte native local destination; its channel/display/identity domain is **not proven** by this reader |
| `post_name_raw_0/1` | read into `v72`/`v68`; no recovered use follows in `sub_555D50` or the audited consumer search | exact raw4 fields, conservatively reserved/unknown |
| `restriction_value_raw` | `*v70` is formatted into resources `0x31B`, `0x32D`, `0x321`, `0x334`; messages `0x32D` and `0x334` use `*v70 - 1` | raw4 wire field whose currently recovered UI consumer uses the low byte as a level/value; the field is not proven to be a general signed `s32` domain |
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

## 4. Result branches and resource cross-check

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

## 5. Cross-packet context flow

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

## 6. TS projection and boundary decision

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
