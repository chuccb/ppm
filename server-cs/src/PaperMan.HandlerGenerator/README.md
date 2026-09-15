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
3. Its containing type is a top-level public static partial class.

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

Because this project ships a source generator, it opts in to the extended
analyzer rules and tracks its own rule set:

* `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>` satisfies
  **RS1036** and turns on **RS1035**, which bans APIs that would affect the
  compiler host (`Console`, `Environment`, `File`/`Directory` IO, `Random`,
  `Assembly.Load`, `CultureInfo.Current*`). The generator uses none of them; it
  reads only the `Compilation` and formats with `CultureInfo.InvariantCulture`.
* `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` are passed
  as `AdditionalFiles` to satisfy **RS2008**. Release tracking only counts as
  enabled when *both* files reach the compiler, which is why the shipped file
  exists while still holding no releases.

Every `PMH*` descriptor needs a row in the unshipped file whose Category and
Severity columns match the descriptor, or **RS2000** (missing) / **RS2001**
(stale or mismatched) fires. Both are warnings that `TreatWarningsAsErrors`
promotes to build errors, so the table is part of the build contract.

When editing those files, note that the release-tracking parser has its own
grammar and is not general Markdown:

* only lines starting with `;` are comments — an `<!-- ... -->` block is read as
  a table entry and fails with **RS2007**;
* the header must be the bare `Rule ID | Category | Severity | Notes` form
  followed by a `---|---|---|---` divider, with no surrounding pipes, because
  analyzer packages older than 4.x reject a leading `|`;
* the Notes column must not contain `|`, which would be parsed as a 5th column.

Releasing this generator under a version number means moving the unshipped rows
into a new `## Release <version>` section in the shipped file.

This makes the **dispatch discovery path** trim- and NativeAOT-friendly:
compiled code contains direct references, not unbounded runtime reflection.

That boundary does **not** establish that the complete SQLite-backed server is
NativeAOT publishable. Validate that separately with the .NET 10 publish-time
analyzers and actual target platform/native dependencies.

## Inspect and check

From the repository root:

```bash
# Static catalog/source topology check, including the RS1036/RS2008 opt-in and
# the PMH* release-tracking rows (does not compile C#).
python3 server-cs/tools/verify_server_layout.py

# Inspect generated source on a machine with the .NET 10 SDK.
dotnet build server-cs/PaperMan.slnx -p:EmitCompilerGeneratedFiles=true
```

Generated files are build artifacts under `obj/`; never edit or commit them.
