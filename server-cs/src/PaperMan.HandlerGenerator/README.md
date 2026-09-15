# PaperMan.HandlerGenerator

This compiler-only Roslyn source generator removes handwritten packet-handler
registration from `PaperMan.Server`. Its output is compiled into the Server
assembly; the generator assembly itself is referenced as an MSBuild `Analyzer`
and is not a runtime dependency.

## Discovery contract

For each static partial `*Handlers` class in `PaperMan.Server`, the generator
selects a method only when all of these are true:

1. The method name is a verified C2S catalog token: a generated
   `PaperMan.Protocol.Opcode` member ending in `_REQ`, or the source-proven
   one-way `GL_MYINFO_OPEN` token.
2. Its exact signature is `ValueTask (Session, Packet, ServerContext)`.
3. Its containing type is a static partial class.

It then generates direct method-group `Dictionary<ushort, PacketHandler>.Add`
calls and `Router.Build()` calls only the generated entry point. There is no
runtime assembly/type/method reflection or manually maintained registration
list.

`RawOpcodeHandler(206)` on `RawOpcode206_REQ` is the sole explicit exception:
206 has no native/catalog request token, so it cannot be discovered by name.
The attribute preserves that numeric boundary without inventing a GS token.

## Diagnostics and AOT boundary

The generator emits these compile errors rather than silently omitting a path:

| ID | Meaning |
|---|---|
| `PMH001` | required Protocol/raw-marker symbol is unavailable |
| `PMH002` | a canonical-name or raw-marked method has the wrong static handler shape |
| `PMH003` | two methods claim the same numeric opcode |
| `PMH004` | a raw marker is malformed or incorrectly applied to a named opcode |
| `PMH005` | a receive-shaped method uses an ACK/base catalog token rather than an admitted C2S token |

This makes the **dispatch discovery path** trim- and NativeAOT-friendly:
compiled code contains direct references, not unbounded runtime reflection.

That boundary does **not** establish that the complete SQLite-backed server is
NativeAOT publishable. Validate that separately with the .NET 10 publish-time
analyzers and actual target platform/native dependencies.

## Inspect and check

From the repository root:

```bash
# Static catalog/source topology check (does not compile C#).
python3 server-cs/tools/verify_server_layout.py

# Inspect generated source on a machine with the .NET 10 SDK.
dotnet build server-cs/PaperMan.slnx -p:EmitCompilerGeneratedFiles=true
```

Generated files are build artifacts under `obj/`; never edit or commit them.
