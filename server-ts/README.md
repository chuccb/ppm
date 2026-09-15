# server-ts — PaperMan server on Bun

A from-scratch server for the PaperMan client, written against the
reverse-engineering notes in [`../docs/`](../docs/).

## Stack

| Component | Version | Notes |
|---|---|---|
| Bun | 1.4.3-canary | `bun:sqlite`, `Bun.listen`, `Bun.password` — no Node shims |
| TypeScript | 7.1.0-dev nightly | strict, plus `exactOptionalPropertyTypes`, `noUncheckedIndexedAccess`, `erasableSyntaxOnly` |
| SQLite | 3.53.4 | via `bun:sqlite`, asserted at runtime in `test/db.test.ts` |

## Run

```bash
bun install
bun test          # 48 tests
bun run typecheck # tsc --noEmit, clean
bun start         # login server on 0.0.0.0:40200
```

Environment: `PM_HOST`, `PM_PORT`, `PM_DB`, `PM_ADVERTISE_HOST`,
`PM_CHANNEL_PORT`.

## Layout

Seven files, no subdirectories. See [STYLE.md](STYLE.md) for the conventions.

```
src/packet.ts    the whole wire format: header, cipher, reader, writer, reassembly
src/aes.ts       AES-128 + CFB-128, the client's cipher
src/opcodes.ts   676-opcode catalogue, loaded from db/packets.tsv
src/store.ts     accounts on bun:sqlite
src/login.ts     GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ -> GL_LOGIN_ACK
src/session.ts   per-connection dispatch, and Bun.listen
src/main.ts      entry point
```

Builders are named after the opcode they produce, so grepping an official name
from `docs/PACKETS.md` lands on the code that implements it.

## Protocol facts this implements

All of these are cited to `../docs/PACKETS.md`:

- **Frame** — `u16 size, u16 opcode, u16 preEncryptSize, u16 preCompressSize`,
  then payload. Little-endian, unaligned, no padding between fields.
- **Cipher** — AES-128-CFB (128-bit feedback, zero IV) over a 16-byte-aligned
  buffer. An empty payload still costs one block. The key is the EUC-KR literal
  「트렁크점령전머지」.
- **Login order** — the client does not send credentials unsolicited. The
  server sends `694` on connect, which both negotiates the compression
  threshold and triggers the client's `682` builder. `694` must be sent exactly
  once: repeating it after login makes the client resend `682` forever.
- **Compression** — the client only lowers its threshold when the value is
  strictly below `0x2580`, so sending `0x2580` disables LZ in both directions.
  The LZ stage is therefore not implemented, and `decodeFrame` throws rather
  than guessing if a peer ever sends a compressed frame.
- **Strings** — NUL-terminated inside the payload, no length prefix. The Korean
  client is CP949, whose WHATWG label is `euc-kr` (Bun rejects `cp949`).
- **Credentials** — the client validates `[0-9A-Za-z@]` before sending, so the
  store rejects anything else too.

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
drop tables. `docs/WIKI_MECHANICS.md` explains why each is unknowable from the
client alone.
