# Code style

Optimised for a human reading this months from now, with an LLM as the second
audience. Two rules dominate everything else:

1. **Name things what the protocol calls them.** If the client calls it
   `GL_LOGIN_ACK`, so do we. A reader holding `docs/PACKETS.md` should be able
   to grep an official name and land on the code.
2. **Don't add a layer to a one-layer problem.** An indirection has to earn its
   place by removing more confusion than it adds.

## Naming

Follow `db/packets.tsv`, which is the reverse-engineered source of truth.

**The opcode name lives in the filename, and nowhere else.** One module per
packet, in the folder for its direction:

```
src/ops/c2s/GL_LOGIN_REQ.ts   the client sends it; we read it
src/ops/s2c/GL_LOGIN_ACK.ts   we send it; we build it
```

Inside the module the name appears in exactly one more place: the exported
function is named after the opcode too.

```ts
export default function GL_LOGIN_ACK(op: number, outcome: Result | Success): Packet
```

That is a deliberate exception to "write it once", and it buys two things: the
packet is identifiable when you land mid-file from a grep, and stack traces say
`at GL_LOGIN_ACK` rather than `at GL_LOGIN_ACK_default`. It is safe because the
registry asserts `fn.name` matches the filename at startup, so a rename that
touches only one of them fails immediately instead of drifting.

Nothing else repeats the name — no constant, no `new Packet(...)` argument. A
builder receives its own opcode as the first parameter. To find the code for a
packet, open the file with that name.

**Direction comes from the folder, not the `_REQ`/`_ACK` suffix.** Those
suffixes describe the client's view and do not always match ours: `GT_PING_ACK`
is an `_ACK` the *server* sends, and `GT_PING_REQ` is a `_REQ` it *receives*.
A suffix rule gets that pair backwards; a folder cannot.

Everything else is normal camelCase: `frameLength`, `verifyLogin`.

Two guards make the convention enforceable rather than aspirational:

- The registry discovers each folder with Bun's `Glob` and loads modules with
  `import.meta.require`. There are no generated barrel files to keep in sync.
  `bun run sync` remains a cheap filename/catalogue check before tests.
- Runtime module values remain `unknown` until the registry validates their
  default export and, for s2c, the returned `Packet`; they are never widened
  silently to `any`. A type-only builder map in `registry.ts` preserves the
  literal outbound-name union and every builder's argument tuple even though
  the implementation is discovered at runtime.
- At startup the registry checks every filename against `db/packets.tsv` and
  every default export against its filename. A missing, renamed, or unknown
  operation fails immediately; the wire layout and module implementations do
  not change.

Opcode families from the catalogue, for orientation:
`GL_` lobby · `GG_` in-game relay · `GR_` room · `GS_` shop · `GP_` play ·
`GC_` clan · `GQ_` quest · `GI_` inventory · `GT_` transport · `MASTER_` GM.

## Structure

- **One concept, one file.** `packet.ts` owns the whole wire format — header,
  cipher, reader, writer — because you never touch one without the others.
- **One word, one meaning.** "wire" means the byte format and nothing else, so
  it belongs to `packet.ts` alone. The per-opcode modules live in `src/ops/`
  because each file *is* one opcode, and `opcode` is the vocabulary
  `docs/PACKETS.md` uses most.
- **No interface with a single implementation.** Depend on Bun's `Socket`
  directly rather than inventing a `SessionSink` to wrap it.
- **No wrapper object used once.** If a type exists only to be the parameter of
  one function, pass the fields.
- **Prefer a flat function to a class** unless there is per-instance state.
  `Connection` is a class because each socket has its own buffer and timer; the
  registry is plain functions because there is only ever one of it.
- **A packet's module is the whole story for that packet.** `GL_LOGIN_REQ.ts`
  parses *and* authenticates *and* replies. It must not parse and then call a
  method on `Connection` — that splits one simple thing across two files and
  makes `Connection` grow a method per opcode.

## Comments

Comment *why*, and cite evidence. The valuable comment is the one that stops
someone "fixing" a deliberate oddity:

```ts
// 694 must be sent exactly once. It carries the compression threshold and also
// triggers the client's 682 builder, so resending it after login makes the
// client resend credentials forever. (docs/PACKETS.md §1.4)
```

Cite `sub_XXXXXX` or a doc section for anything non-obvious. Never restate the
code in prose.

## Honesty about what is unknown

The service shut down in 2016 and much of the game's behaviour only ever lived
there. Where `docs/` says UNRESOLVED, do nothing rather than invent a plausible
rule, and say so in a comment. A missing feature is debuggable; a fabricated
rule silently diverges from the original forever.

## TypeScript

Strict, plus `exactOptionalPropertyTypes`, `noUncheckedIndexedAccess` and
`erasableSyntaxOnly`. `tsc --noEmit` stays clean — the settings have already
caught one real aliasing bug.

Bun-native APIs by default: `Bun.listen`, `bun:sqlite`, `Bun.password`,
`bun test`. Reach for `node:` only when Bun has no equivalent.
