#!/usr/bin/env python3
"""Validate PaperMan.Server's catalog-to-source-generator handler topology.

Run from any working directory:
    python3 server-cs/tools/verify_server_layout.py

This is a static structural check, not a replacement for a .NET build or a
captured client/server interoperability test. The source generator discovers
canonical handler methods at compile time and emits direct method-group calls;
the running server does not reflect over types or methods.
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVER = ROOT / "server-cs" / "src" / "PaperMan.Server"
HANDLERS = SERVER / "Handlers"
GENERATOR_PROJECT = ROOT / "server-cs" / "src" / "PaperMan.HandlerGenerator"
TODO_HANDLERS = ROOT / "docs" / "TODO_HANDLERS.md"
SHIPPED_NAME = "AnalyzerReleases.Shipped.md"
UNSHIPPED_NAME = "AnalyzerReleases.Unshipped.md"
OPCODE_SOURCE = ROOT / "db" / "packets.tsv"
OPCODE_GENERATED = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Generated" / "Opcode.cs"

ENUM_VALUE = re.compile(r"^    (?P<token>\w+) = (?P<value>\d+),$", re.MULTILINE)
HANDLER_ENTRY = re.compile(
    r"(?m)^    (?:private|internal|public) static (?:async )?ValueTask "
    r"(?P<entry>\w+)\(Session session, Packet packet, ServerContext context\)")
HANDLER_CLASS = re.compile(r"(?m)^public static partial class (?P<name>\w+Handlers)\s*$")
# DiagnosticDescriptor declarations in the generator, used to keep the analyzer
# release-tracking rows (RS2000/RS2001) in step with the reported rules.
DESCRIPTOR = re.compile(
    r"""id:\s*"(?P<id>PMH\d+)",.*?"""
    r"""category:\s*"(?P<category>[^"]+)",\s*"""
    r"""defaultSeverity:\s*DiagnosticSeverity\.(?P<severity>\w+),\s*"""
    r"""isEnabledByDefault:\s*(?P<enabled>true|false)\)""",
    re.DOTALL)
# Mirrors ReleaseTrackingHelper's parser: only ';' starts a comment, the header
# is two fixed lines, and a New Rules row carries 3 or 4 '|'-separated columns.
RELEASE_TABLE_HEADER = re.compile(r"^\|?\s*Rule ID\s*\|\s*Category\s*\|\s*Severity\s*\|\s*Notes\s*\|?$", re.I)
RELEASE_TABLE_DIVIDER = re.compile(r"^\|?-{3,}\|-{3,}\|-{3,}\|-{3,}\|?$")
RAW_206_ATTRIBUTE = re.compile(
    r"\[RawOpcodeHandler\(206\)\]\s*\n\s*"
    r"private static ValueTask RawOpcode206_REQ\(Session session, Packet packet, ServerContext context\)")
# `sub_556680` sends GL_MYINFO_OPEN (270) as a one-way C2S notification.
# All other direct entries in this client revision are catalog *_REQ tokens.
NON_REQUEST_C2S_TOKENS = frozenset({"GL_MYINFO_OPEN"})


def fail(message: str) -> None:
    raise SystemExit(f"layout verification failed: {message}")


def read_packet_catalog() -> dict[int, str]:
    catalog: dict[int, str] = {}
    tokens: set[str] = set()
    for line_number, line in enumerate(OPCODE_SOURCE.read_text(encoding="utf-8").splitlines(), 1):
        if not line or line.startswith("#") or line.startswith("opcode\t"):
            continue
        fields = line.split("\t")
        if len(fields) < 2:
            fail(f"{OPCODE_SOURCE}:{line_number} is not an opcode/name row")
        opcode, token = int(fields[0]), fields[1]
        if opcode in catalog or token in tokens:
            fail(f"duplicate opcode/token at {OPCODE_SOURCE}:{line_number}")
        catalog[opcode] = token
        tokens.add(token)
    return catalog


def read_generated_opcode_values() -> dict[str, int]:
    values = {
        match["token"]: int(match["value"])
        for match in ENUM_VALUE.finditer(OPCODE_GENERATED.read_text(encoding="utf-8"))
    }
    if not values:
        fail(f"no enum values found in {OPCODE_GENERATED}")
    return values


def canonical_source_for(token: str, directory: Path) -> Path:
    return directory / f"Handlers.{token.removesuffix('_REQ')}.cs"


def read_release_rows(path: Path) -> dict[str, tuple[str, str]]:
    """Parse a release-tracking file the way ReleaseTrackingHelper does.

    Returns {rule id: (category, severity)} for 'New Rules' rows. Raises through
    fail() on anything the real parser would report as RS2007.
    """
    rows: dict[str, tuple[str, str]] = {}
    expect = "table-title"
    for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        # The parser only honours ';' comments; '<!-- -->' is parsed as content.
        if not line or line.startswith(";"):
            continue
        if line.startswith("<!--") or line.startswith("#"):
            if line.startswith("## Release") or line.startswith("### "):
                pass
            else:
                fail(f"{path}:{number} is not a ';' comment; the release parser reads it as an entry")
        if line.startswith("## Release"):
            expect = "table-title"
            continue
        if line.startswith("### "):
            if line != "### New Rules":
                fail(f"{path}:{number} only 'New Rules' tables are maintained here, found {line!r}")
            expect = "header"
            continue
        if expect == "header":
            if not RELEASE_TABLE_HEADER.match(line):
                fail(f"{path}:{number} invalid release table header {line!r}")
            expect = "divider"
            continue
        if expect == "divider":
            if not RELEASE_TABLE_DIVIDER.match(line):
                fail(f"{path}:{number} invalid release table divider {line!r}")
            expect = "rows"
            continue
        if expect != "rows":
            fail(f"{path}:{number} unexpected content before a table header: {line!r}")
        parts = [cell.strip() for cell in line.strip("|").split("|")]
        if not 3 <= len(parts) <= 4:
            fail(f"{path}:{number} a New Rules row needs 3 or 4 columns, found {len(parts)}: {line!r}")
        rule_id = parts[0]
        if rule_id in rows:
            fail(f"{path}:{number} duplicate release entry for {rule_id}")
        rows[rule_id] = (parts[1], parts[2])
    return rows


TODO_ROW = re.compile(r"(?m)^\| (?P<opcode>\d+) \| (?P<token>\w+) \|")


def verify_unimplemented_inventory(direct_entries: dict[str, Path], generated: dict[str, int]) -> int:
    """docs/TODO_HANDLERS.md must not list an opcode that already has a handler.

    The inventory is the queue of remaining work. When an opcode ships and its
    row survives, the document overstates what is left and the next reader
    re-researches something that is already done.
    """
    implemented = {generated[token] for token in direct_entries if token in generated}
    rows = [(int(match["opcode"]), match["token"])
            for match in TODO_ROW.finditer(TODO_HANDLERS.read_text(encoding="utf-8"))]
    if not rows:
        fail(f"{TODO_HANDLERS} has no unimplemented-inventory rows to check")

    stale = sorted((opcode, token) for opcode, token in rows if opcode in implemented)
    if stale:
        listed = ", ".join(f"{opcode} {token}" for opcode, token in stale)
        fail(f"{TODO_HANDLERS} lists implemented opcodes as unimplemented: {listed}")

    mislabelled = sorted(
        (opcode, token) for opcode, token in rows
        if token in generated and generated[token] != opcode)
    if mislabelled:
        listed = ", ".join(f"{opcode} {token}" for opcode, token in mislabelled)
        fail(f"{TODO_HANDLERS} rows disagree with the generated catalog: {listed}")
    return len(rows)


def verify_analyzer_release_tracking() -> int:
    """RS1036 / RS2008: the generator must opt in and track every PMH* rule."""
    project = (GENERATOR_PROJECT / "PaperMan.HandlerGenerator.csproj").read_text(encoding="utf-8")
    if "<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>" not in project:
        fail("PaperMan.HandlerGenerator must set EnforceExtendedAnalyzerRules (RS1036)")
    for name in (SHIPPED_NAME, UNSHIPPED_NAME):
        if f'<AdditionalFiles Include="{name}" />' not in project:
            fail(f"PaperMan.HandlerGenerator must pass {name} as an AdditionalFiles item (RS2008)")
        if not (GENERATOR_PROJECT / name).is_file():
            fail(f"missing {name}; release tracking is enabled only when both files exist (RS2008)")

    generator = (GENERATOR_PROJECT / "PacketHandlerRegistryGenerator.cs").read_text(encoding="utf-8")
    declared = {
        match["id"]: (match["category"], match["severity"], match["enabled"])
        for match in DESCRIPTOR.finditer(generator)
    }
    if not declared:
        fail("no PMH DiagnosticDescriptor found in PacketHandlerRegistryGenerator.cs")

    shipped = read_release_rows(GENERATOR_PROJECT / SHIPPED_NAME)
    unshipped = read_release_rows(GENERATOR_PROJECT / UNSHIPPED_NAME)
    if overlap := sorted(shipped.keys() & unshipped.keys()):
        fail(f"rules listed as both shipped and unshipped: {overlap} (RS2006)")

    tracked = {**shipped, **unshipped}
    if missing := sorted(declared.keys() - tracked.keys()):
        fail(f"reported rules missing from the release files: {missing} (RS2000)")
    if stale := sorted(tracked.keys() - declared.keys()):
        fail(f"release entries for rules the generator no longer reports: {stale} (RS2001)")

    for rule_id, (category, severity, enabled) in sorted(declared.items()):
        # A disabled rule is recorded as 'Disabled' rather than its severity.
        expected = severity if enabled == "true" else "Disabled"
        if tracked[rule_id] != (category, expected):
            fail(
                f"{rule_id} release entry {tracked[rule_id]} does not match the descriptor "
                f"{(category, expected)} (RS2001)")
    return len(declared)


def main() -> None:
    catalog = read_packet_catalog()
    generated = read_generated_opcode_values()
    if generated != {token: opcode for opcode, token in catalog.items()}:
        fail("generated Opcode.cs diverges from packets.tsv; run tools/gen_opcodes.py")

    if list(HANDLERS.glob("*.cs")):
        fail("flat C# handler source remains directly under Handlers/")
    if list(HANDLERS.glob("*/Handlers.*.Registry.cs")):
        fail("manual handler registry source remains")

    server_project = (SERVER / "PaperMan.Server.csproj").read_text(encoding="utf-8")
    required_analyzer_reference = """<ProjectReference Include=\"../PaperMan.HandlerGenerator/PaperMan.HandlerGenerator.csproj\"
                      OutputItemType=\"Analyzer\"
                      ReferenceOutputAssembly=\"false\" />"""
    if required_analyzer_reference not in server_project:
        fail("PaperMan.Server is not wired to the compile-time handler generator as an analyzer")
    if not (GENERATOR_PROJECT / "PacketHandlerRegistryGenerator.cs").is_file():
        fail("missing PacketHandlerRegistryGenerator source")
    if not (SERVER / "Host" / "RawOpcodeHandlerAttribute.cs").is_file():
        fail("missing RawOpcodeHandlerAttribute source")
    tracked_rules = verify_analyzer_release_tracking()

    router = (SERVER / "Host" / "Router.cs").read_text(encoding="utf-8")
    if "GeneratedPacketHandlerRegistration.AddTo(table);" not in router:
        fail("Router.Build does not call generated handler registration")
    if ".Register(Add)" in router or "Registrar" in router:
        fail("Router still has a manual handler registration path")

    all_handler_sources = set(HANDLERS.rglob("Handlers.*.cs"))
    direct_sources: set[Path] = set()
    direct_entries: dict[str, Path] = {}
    family_classes: set[str] = set()
    raw_entry_count = 0

    for source in sorted(all_handler_sources):
        text = source.read_text(encoding="utf-8")
        declared_classes = [match["name"] for match in HANDLER_CLASS.finditer(text)]
        if len(declared_classes) != 1:
            fail(f"{source}: every handler source must declare exactly one top-level public static partial *Handlers class")
        family_classes.add(declared_classes[0])
        for match in HANDLER_ENTRY.finditer(text):
            entry = match["entry"]
            if entry == "RawOpcode206_REQ":
                if source.name != "Handlers.RawOpcode206.cs" or not RAW_206_ATTRIBUTE.search(text):
                    fail(f"{source}: raw opcode 206 must carry [RawOpcodeHandler(206)]")
                raw_entry_count += 1
            elif entry not in generated:
                fail(f"{source}: receive-shape entry {entry} is not a generated Opcode token")
            elif not (entry.endswith("_REQ") or entry in NON_REQUEST_C2S_TOKENS):
                fail(f"{source}: {entry} is not a verified C2S handler token")
            elif source != canonical_source_for(entry, source.parent):
                fail(f"{source}: {entry} must use canonical basename {canonical_source_for(entry, source.parent).name}")

            if entry in direct_entries:
                fail(f"duplicate direct receive entry {entry}: {direct_entries[entry]} and {source}")
            direct_entries[entry] = source
            direct_sources.add(source)

    if raw_entry_count != 1:
        fail(f"expected exactly one raw opcode 206 handler, found {raw_entry_count}")
    if len(direct_entries) != 194:
        fail(f"expected 194 direct handler entries, found {len(direct_entries)}")
    if len(family_classes) != 18:
        fail(f"expected 18 static partial handler classes, found {len(family_classes)}")

    shared_sources = set(HANDLERS.glob("*/Handlers.*.Shared.cs"))
    explicit_non_entry_sources = {HANDLERS / "Stats" / "Handlers.GP_CHPLAYTIMEC_ACK.cs"}
    for source in explicit_non_entry_sources:
        token = source.stem.removeprefix("Handlers.")
        if token not in generated:
            fail(f"{source}: non-entry helper basename must retain its exact catalog token")
    accounted_sources = direct_sources | shared_sources | explicit_non_entry_sources
    if accounted_sources != all_handler_sources:
        missing = sorted(str(path.relative_to(HANDLERS)) for path in all_handler_sources - accounted_sources)
        extra = sorted(str(path.relative_to(HANDLERS)) for path in accounted_sources - all_handler_sources)
        fail(f"unaccounted handler sources missing={missing}, extra={extra}")
    if direct_sources & shared_sources:
        fail("a handler source has conflicting direct/shared ownership")

    pending = verify_unimplemented_inventory(direct_entries, generated)

    print(
        "server layout OK: "
        f"{len(catalog)} catalog opcodes; generated discovery finds {len(direct_entries)} direct entries "
        f"across {len(family_classes)} handler classes; {len(direct_sources)} direct + "
        f"{len(shared_sources | explicit_non_entry_sources)} support-only = {len(all_handler_sources)} handler sources; "
        f"{tracked_rules} analyzer rules release-tracked; "
        f"{pending} opcodes still queued in TODO_HANDLERS.md"
    )


if __name__ == "__main__":
    main()
