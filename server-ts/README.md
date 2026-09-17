# server-ts — PaperMan server on Bun

A from-scratch server for the PaperMan client, written against the
reverse-engineering notes in [`../docs/`](../docs/).

## Stack (2026-09-17 preview baseline)

This is the only server implementation. The runtime and lockfile intentionally target
this date's preview toolchain; do not reintroduce another server language or database
runtime.

| Component | Version | Notes |
|---|---|---|
| Bun | 1.4.3-canary | `bun:sqlite`, `Bun.listen`, `Bun.password` — no Node shims |
| TypeScript | 7.1.0-dev.20260915.1 | strict, plus `exactOptionalPropertyTypes`, `noUncheckedIndexedAccess`, `erasableSyntaxOnly` |
| SQLite | 3.53.4 | via `bun:sqlite`, asserted at runtime in `test/db.test.ts` |

## Run

```bash
bun install
bun test          # 63 tests
bun run typecheck # tsc --noEmit, clean
bun start         # login server on 0.0.0.0:40200
```

Environment: `PM_HOST`, `PM_PORT`, `PM_DB`, `PM_ADVERTISE_HOST`,
`PM_CHANNEL_PORT`, `PM_CHANNEL_NAME`, `PM_UDP_HOST`, `PM_UDP_PORT`,
`PM_ADMISSION_TTL_MS`.

## Layout

See [STYLE.md](STYLE.md) for the conventions.

```
src/packet.ts        the whole wire format: header, cipher, reader, writer, reassembly
src/aes.ts           AES-128 + CFB-128, the client's cipher
src/opcodes.ts       676-opcode catalogue, loaded from db/packets.tsv
src/store.ts         accounts on bun:sqlite
src/connection.ts    one TCP connection: reassembly, liveness, dispatch, Bun.listen
src/udp.ts            source-proven private UDP opcode 19 -> empty 20
src/ops/registry.ts  filename -> opcode, and the typed build() / handlerFor()
src/ops/c2s/         packets the client sends us
src/ops/s2c/         packets we send the client
src/main.ts          entry point
```

**One packet, one file, named after the opcode.** The name appears in the
filename and nowhere else — the module gets its opcode injected, so nothing
inside it repeats the name. To find the code for a packet from
`docs/PACKETS.md`, open the file with that name. The complete field-by-field
review of the 31 current packet modules is in
[`../docs/SERVER_TS_PACKET_FIELDS.md`](../docs/SERVER_TS_PACKET_FIELDS.md):

```
src/ops/c2s/GL_LOGIN_REQ.ts   the client sends it; we read it
src/ops/s2c/GL_LOGIN_ACK.ts   we send it; we build it
```

Direction is the folder, not the `_REQ`/`_ACK` suffix — those describe the
client's view, and `GT_PING_ACK` is an `_ACK` the *server* sends. The registry
discovers these files directly with Bun's `Glob` and `import.meta.require`;
`bun run sync` checks that every filename is in `db/packets.tsv`.

## Protocol facts this implements

All of these are cited to `../docs/PACKETS.md`:

- **Frame** — `u16 size, u16 opcode, u16 preEncryptSize, u16 preCompressSize`,
  then payload. Little-endian, unaligned, no padding between fields.
- **Cipher** — AES-128-CFB (128-bit feedback, zero IV) over a 16-byte-aligned
  buffer. An empty payload still costs one block. The key is the EUC-KR literal
  「트렁크점령전머지」.
- **Two connections, two handshakes** — the client connects to the login
  server, then opens a *second* connection to the channel host and port it
  read from the login reply. In both cases the server speaks first:

  ```
  login    GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ    -> GL_LOGIN_ACK
  channel  GL_TCPCONNSUCC     -> PM_UDPSTART_REQ -> PM_UDPSTART_ACK
                                     -> GC_ENTERCHANNEL_REQ -> GC_ENTERCHANNEL_ACK
  ```

  `GL_ACCOUNTCONNSUCC` must be sent exactly once: it also triggers the client's
  credential builder, so repeating it after login loops the client forever.
- **The channel handoff is not an identity** — `PM_UDPSTART_REQ` carries a
  `String[24]` whose writer has never been located, so it is matched against a
  recent, single-use login admission by source IP and echoed values rather than
  trusted as an account key, and it is not a credential. The client's
  second-level handler ignores the 144 result and sends 195 regardless, so the
  connection stays gated until a successful 196.
- **Channel entry is explicit** — `GC_ENTERCHANNEL_ACK` has a three-field
  failure prefix and a success-only endpoint tail. The server accepts only the
  one group/channel it advertises and binds lobby authority after the success
  reply is written. Type-3 admission and emission require an explicit raw
  `type3Tail`; semantic deployment config and gameplay remain out of scope.
- **Compression** — the client only lowers its threshold when the value is
  strictly below `0x2580`, so sending `0x2580` disables LZ in both directions.
  The TCP LZ stage is therefore not implemented, and `decodeFrame` throws
  rather than guessing if a peer ever sends a compressed frame. The private UDP
  endpoint likewise has no LZ stage, matching `sub_595980`/`sub_595A60`.
- **Strings** — NUL-terminated inside the payload, no length prefix. The Korean
  client is CP949, whose WHATWG label is `euc-kr` (Bun rejects `cp949`).
- **Credentials** — the client validates `[0-9A-Za-z@]` before sending, so the
  store rejects anything else too.
- **Keepalive runs backwards from its names** — the server sends
  `GT_PING_ACK(102)` and the client answers `GT_PING_REQ(101)`. The client's
  dispatcher handles 102 by building 101 (`sub_58D6F0`), has no handler for 101
  and no builder for 102. Replying to an inbound 101 would loop forever.
- **Replies keep request order** — handlers are async, so dispatch is chained
  per connection. The client pairs replies to requests positionally, and
  concurrent dispatch let a fast reply overtake a slow one.

### A correction made while building this

`docs/PACKETS.md` previously carried two AES-CFB test vectors that are mutually
inconsistent: a first-block CFB keystream is `AES(IV)` and cannot depend on the
plaintext, yet the two implied keystreams agreed on 1 of 16 bytes. This
implementation passes FIPS-197 C.1 and both documented *ECB* vectors, so the
cipher and key are right and the CFB expectations were wrong. The doc now
carries the recomputed values; see `test/aes.test.ts`.

## Deliberately not implemented

The service was shut down in 2016 and much of the game's behaviour lived only
there. Where the notes say UNRESOLVED, this server does nothing rather than
invent a rule — no damage calculation, no economy, no quest progression, no
drop tables, and no private UDP behavior beyond the source-proven 19 to empty
20 control exchange. `docs/WIKI_MECHANICS.md` explains why each is unknowable
from the client alone.
