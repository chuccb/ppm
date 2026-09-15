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

| Thing | Convention | Example |
|---|---|---|
| Opcode constant | official name, verbatim | `GL_LOGIN_REQ` |
| Handler for a REQ | `on` + official name | `onGL_LOGIN_REQ` |
| Builder for an ACK | official name, verbatim | `GL_LOGIN_ACK({ ... })` |
| Everything else | normal camelCase | `frameLength`, `verifyLogin` |

`SCREAMING_SNAKE` in an otherwise camelCase codebase looks odd for about five
seconds, then pays for itself every time you cross-reference the docs. The
shouty names mark exactly the boundary where our code meets the wire.

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
