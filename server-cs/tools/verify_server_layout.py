#!/usr/bin/env python3
"""Validate PaperMan.Server's catalog-to-registry-to-handler topology.

Run from any working directory:
    python3 server-cs/tools/verify_server_layout.py

This is a structural/static check, not a replacement for a .NET build or a
captured client/server interoperability test. It keeps the intentionally flat
canonical handler token visible while allowing one physical directory per
existing registry owner.
"""
from __future__ import annotations

import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVER = ROOT / "server-cs" / "src" / "PaperMan.Server"
HANDLERS = SERVER / "Handlers"
OPCODE_SOURCE = ROOT / "db" / "packets.tsv"
OPCODE_GENERATED = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Generated" / "Opcode.cs"

STANDARD_BINDING = re.compile(r"add\(Opcode\.(?P<token>\w+),\s*(?P<entry>\w+)\);")
RAW_206_BINDING = re.compile(r"add\(RawOpcode206,\s*(?P<entry>\w+)\);")
ENUM_VALUE = re.compile(r"^    (?P<token>\w+) = (?P<value>\d+),$", re.MULTILINE)
REGISTRY_CLASS = re.compile(r"public static partial class (?P<name>\w+Handlers)")
NESTED_REGISTER = re.compile(r"^        (?P<name>\w+Handlers)\.Register\(add\);$", re.MULTILINE)
ROUTER_REGISTER = re.compile(r"^        (?P<name>\w+Handlers)\.Register\(Add\);$", re.MULTILINE)


def fail(message: str) -> None:
    raise SystemExit(f"layout verification failed: {message}")


def read_packet_catalog() -> dict[int, str]:
    catalog: dict[int, str] = {}
    for line_number, line in enumerate(OPCODE_SOURCE.read_text(encoding="utf-8").splitlines(), 1):
        if not line or line.startswith("#") or line.startswith("opcode\t"):
            continue
        fields = line.split("\t")
        if len(fields) < 2:
            fail(f"{OPCODE_SOURCE}:{line_number} is not an opcode/name row")
        opcode, token = int(fields[0]), fields[1]
        if opcode in catalog:
            fail(f"duplicate opcode {opcode} in {OPCODE_SOURCE}")
        if token in catalog.values():
            fail(f"duplicate canonical token {token} in {OPCODE_SOURCE}")
        catalog[opcode] = token
    return catalog


def read_generated_opcode_values() -> dict[str, int]:
    values = {match["token"]: int(match["value"]) for match in ENUM_VALUE.finditer(
        OPCODE_GENERATED.read_text(encoding="utf-8"))}
    if not values:
        fail(f"no enum values found in {OPCODE_GENERATED}")
    return values


def require_direct_source(registry: Path, token: str, entry: str) -> Path:
    """Verify the canonical basename and direct C# receive entry together."""
    if entry != token:
        fail(f"{registry}: registry entry {entry} does not retain catalog token {token}")

    basename = token.removesuffix("_REQ")
    source = registry.parent / f"Handlers.{basename}.cs"
    if not source.is_file():
        fail(f"{registry}: {token} expects missing canonical source {source}")
    if not re.search(rf"\b{re.escape(entry)}\s*\(", source.read_text(encoding="utf-8")):
        fail(f"{source}: missing direct receive entry {entry}")
    return source


def main() -> None:
    catalog = read_packet_catalog()
    generated = read_generated_opcode_values()

    generated_mismatches = [
        f"{token}={value} (catalog={catalog.get(value)!r})"
        for token, value in generated.items()
        if catalog.get(value) != token
    ]
    if generated_mismatches:
        fail("generated Opcode.cs diverges from packets.tsv: " + "; ".join(generated_mismatches[:5]))
    if len(generated) != len(catalog):
        fail(f"generated/catalog size differs: {len(generated)} != {len(catalog)}")

    if list(HANDLERS.glob("*.cs")):
        fail("flat C# handler source remains directly under Handlers/")

    registries = sorted(HANDLERS.glob("*/Handlers.*.Registry.cs"))
    if not registries:
        fail("no registry files found")

    direct_sources: set[Path] = set()
    registry_classes: dict[str, Path] = {}
    nested_registry_classes: list[str] = []
    standard_binding_count = 0
    raw_206_binding_count = 0

    for registry in registries:
        registry_text = registry.read_text(encoding="utf-8")
        class_match = REGISTRY_CLASS.search(registry_text)
        if class_match is None:
            fail(f"{registry}: missing public static partial *Handlers registry class")
        registry_class = class_match["name"]
        if registry_class in registry_classes:
            fail(f"duplicate registry class {registry_class}")
        registry_classes[registry_class] = registry
        nested_registry_classes.extend(match["name"] for match in NESTED_REGISTER.finditer(registry_text))

        for binding in STANDARD_BINDING.finditer(registry_text):
            token, entry = binding["token"], binding["entry"]
            if token not in generated:
                fail(f"{registry}: {token} is absent from generated Opcode.cs")
            opcode = generated[token]
            if catalog.get(opcode) != token:
                fail(f"{registry}: {token} does not agree with packets.tsv opcode {opcode}")
            direct_sources.add(require_direct_source(registry, token, entry))
            standard_binding_count += 1

        # Opcode 206 has no catalog request token. The source intentionally
        # gives its local entry a Raw name while preserving the raw boundary.
        for binding in RAW_206_BINDING.finditer(registry_text):
            entry = binding["entry"]
            if registry.parent.name != "Shop" or entry != "RawOpcode206_REQ":
                fail(f"{registry}: unexpected non-catalog raw binding {entry}")
            source = registry.parent / "Handlers.RawOpcode206.cs"
            if not source.is_file() or not re.search(r"\bRawOpcode206_REQ\s*\(", source.read_text(encoding="utf-8")):
                fail(f"{registry}: raw opcode 206 has no matching direct source/entry")
            direct_sources.add(source)
            raw_206_binding_count += 1

    if raw_206_binding_count != 1:
        fail(f"expected exactly one documented raw-206 binding, found {raw_206_binding_count}")
    if not set(nested_registry_classes) <= registry_classes.keys():
        fail("registry invokes an unknown nested registry: " + ", ".join(
            sorted(set(nested_registry_classes) - registry_classes.keys())))

    router_text = (SERVER / "Host" / "Router.cs").read_text(encoding="utf-8")
    router_classes = [match["name"] for match in ROUTER_REGISTER.finditer(router_text)]
    top_level_classes = set(registry_classes) - set(nested_registry_classes)
    if Counter(router_classes) != Counter(top_level_classes):
        fail("Router top-level registry calls do not match registry graph")

    all_handler_sources = set(HANDLERS.rglob("Handlers.*.cs"))
    registry_sources = set(registries)
    shared_sources = set(HANDLERS.glob("*/Handlers.*.Shared.cs"))
    # This source emits the server-side play-time push and is deliberately not
    # a receive entry or generic Shared file; keep it an explicit exception.
    explicit_non_entry_sources = {HANDLERS / "Stats" / "Handlers.GP_CHPLAYTIMEC.cs"}
    accounted_sources = direct_sources | registry_sources | shared_sources | explicit_non_entry_sources
    if accounted_sources != all_handler_sources:
        missing = sorted(str(path.relative_to(HANDLERS)) for path in all_handler_sources - accounted_sources)
        extra = sorted(str(path.relative_to(HANDLERS)) for path in accounted_sources - all_handler_sources)
        fail(f"unaccounted handler sources missing={missing}, extra={extra}")
    if direct_sources & registry_sources or direct_sources & shared_sources or registry_sources & shared_sources:
        fail("a handler source has conflicting direct/registry/shared ownership")

    print(
        "server layout OK: "
        f"{len(catalog)} catalog opcodes; {standard_binding_count} catalog bindings + raw 206; "
        f"{len(router_classes)} top-level registries + {len(nested_registry_classes)} nested; "
        f"{len(direct_sources)} direct + {len(registry_sources)} registries + "
        f"{len(shared_sources | explicit_non_entry_sources)} support-only = {len(all_handler_sources)} handler sources"
    )


if __name__ == "__main__":
    main()
