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
packet under `src/wire/`, named exactly as the catalogue names it:

```
src/wire/GL_LOGIN_REQ.ts   *_REQ  -> inbound handler, default (reader, session)
src/wire/GL_LOGIN_ACK.ts   others -> outbound builder, default (op, ...args)
```

Inside the module the name never appears again — not in a constant, not in a
comment header, not in `new Packet(...)`. The builder receives its own opcode
as the first argument, so there is nothing to repeat and nothing to keep in
sync. To find the code for a packet, open the file with that name.

Everything else is normal camelCase: `frameLength`, `verifyLogin`.

Two guards make the convention enforceable rather than aspirational:

- `wire/index.ts` lists the modules, one `export { default as X } from "./X.ts"`
  per line, so `reply("GL_LOGON_ACK")` is a *compile* error and a builder's
  argument types are checked at each call site. The list is unavoidable: ES
  modules have no glob import, and a dynamic `import(\`./${name}.ts\`)`
  degrades to `any`, which would hand back exactly the runtime surprises the
  naming scheme is meant to remove.
- At startup the registry cross-checks that list against the directory and
  every filename against `db/packets.tsv`. A file that is unlisted, a listing
  with no file, or a name that is not a real opcode all fail immediately.

Opcode families from the catalogue, for orientation:
`GL_` lobby · `GG_` in-game relay · `GR_` room · `GS_` shop · `GP_` play ·
`GC_` clan · `GQ_` quest · `GI_` inventory · `GT_` transport · `MASTER_` GM.

## Structure

- **One concept, one file.** `packet.ts` owns the whole wire format — header,
  cipher, reader, writer — because you never touch one without the others.
- **No interface with a single implementation.** Depend on Bun's `Socket`
  directly rather than inventing a `SessionSink` to wrap it.
- **No wrapper object used once.** If a type exists only to be the parameter of
  one function, pass the fields.
- **Prefer a flat function to a class** unless there is per-instance state.

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
