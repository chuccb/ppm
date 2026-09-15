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
OPCODE_SOURCE = ROOT / "db" / "packets.tsv"
OPCODE_GENERATED = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Generated" / "Opcode.cs"

ENUM_VALUE = re.compile(r"^    (?P<token>\w+) = (?P<value>\d+),$", re.MULTILINE)
HANDLER_ENTRY = re.compile(
    r"(?m)^    (?:private|internal|public) static (?:async )?ValueTask "
    r"(?P<entry>\w+)\(Session session, Packet packet, ServerContext context\)")
HANDLER_CLASS = re.compile(r"public static partial class (?P<name>\w+Handlers)")
RAW_206_ATTRIBUTE = re.compile(
    r"\[RawOpcodeHandler\(206\)\]\s*\n\s*"
    r"private static ValueTask RawOpcode206_REQ\(Session session, Packet packet, ServerContext context\)")


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
        family_classes.update(match["name"] for match in HANDLER_CLASS.finditer(text))
        for match in HANDLER_ENTRY.finditer(text):
            entry = match["entry"]
            if entry == "RawOpcode206_REQ":
                if source.name != "Handlers.RawOpcode206.cs" or not RAW_206_ATTRIBUTE.search(text):
                    fail(f"{source}: raw opcode 206 must carry [RawOpcodeHandler(206)]")
                raw_entry_count += 1
            elif entry not in generated:
                fail(f"{source}: receive-shape entry {entry} is not a generated Opcode token")
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
    if len(family_classes) != 17:
        fail(f"expected 17 static partial handler classes, found {len(family_classes)}")

    shared_sources = set(HANDLERS.glob("*/Handlers.*.Shared.cs"))
    explicit_non_entry_sources = {HANDLERS / "Stats" / "Handlers.GP_CHPLAYTIMEC.cs"}
    accounted_sources = direct_sources | shared_sources | explicit_non_entry_sources
    if accounted_sources != all_handler_sources:
        missing = sorted(str(path.relative_to(HANDLERS)) for path in all_handler_sources - accounted_sources)
        extra = sorted(str(path.relative_to(HANDLERS)) for path in accounted_sources - all_handler_sources)
        fail(f"unaccounted handler sources missing={missing}, extra={extra}")
    if direct_sources & shared_sources:
        fail("a handler source has conflicting direct/shared ownership")

    print(
        "server layout OK: "
        f"{len(catalog)} catalog opcodes; generated discovery finds {len(direct_entries)} direct entries "
        f"across {len(family_classes)} handler classes; {len(direct_sources)} direct + "
        f"{len(shared_sources | explicit_non_entry_sources)} support-only = {len(all_handler_sources)} handler sources"
    )


if __name__ == "__main__":
    main()
