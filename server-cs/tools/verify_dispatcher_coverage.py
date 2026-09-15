#!/usr/bin/env python3
"""Check docs/LAYOUTS.md covers every case in the native packet dispatcher.

Run from any working directory:
    python3 server-cs/tools/verify_dispatcher_coverage.py

`docs/LAYOUTS.md` is generated from `PaperMan.exe.c` by an extractor that is not
in this repository, so the table can silently drift from the binary: a case can
be missed and nothing notices. This tool re-derives the case set straight from
the decompiler dump and diffs it against the table.

It also reports, without failing, the opcodes that have a native reader or
writer but no entry in `db/packets.tsv`. That is expected rather than broken:
`sub_9D2050` registers only the *named* opcodes, so the catalog is not an upper
bound on the wire protocol. See the 676-catalog note in `docs/PACKETS.md`.

This reads the dump textually. It is a drift alarm, not a decompiler.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DUMP = ROOT / "PaperMan.exe.c"
LAYOUTS = ROOT / "docs" / "LAYOUTS.md"
LAYOUTS_REQ = ROOT / "docs" / "LAYOUTS_REQ.md"
CATALOG = ROOT / "db" / "packets.tsv"

DISPATCHER = "sub_58B010"
# The dispatcher signature, then its opening brace on the following line.
DISPATCHER_HEAD = re.compile(r"\n[A-Za-z_][^\n]*\b" + DISPATCHER + r"\([^)]*\)\s*\r?\n\{")
CASE = re.compile(r"case (\d+)u?:")
TABLE_ROW = re.compile(r"^\| (\d+) \|", re.M)


def dispatcher_cases(text: str) -> set[int]:
    """Every `case N:` inside the dispatcher body, found by brace matching."""
    head = DISPATCHER_HEAD.search(text)
    if head is None:
        raise SystemExit(
            f"dispatcher verification failed: cannot locate {DISPATCHER} in {DUMP}")
    start = head.end()
    depth = 1
    index = start
    while index < len(text) and depth:
        character = text[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
        index += 1
    if depth:
        raise SystemExit(f"dispatcher verification failed: unbalanced body for {DISPATCHER}")
    return {int(match) for match in CASE.findall(text[start:index])}


def table_opcodes(path: Path) -> set[int]:
    return {int(match) for match in TABLE_ROW.findall(path.read_text(encoding="utf-8"))}


def catalog_opcodes() -> set[int]:
    opcodes = set()
    for line in CATALOG.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith(("#", "opcode")):
            continue
        opcodes.add(int(line.split("\t")[0]))
    return opcodes


FUNCTION_HEAD = re.compile(r"\n[A-Za-z_][^\n]*\b(sub_[0-9A-Fa-f]+)\([^)]*\)\s*\r?\n\{")
TABLE_HANDLER = re.compile(r"^\| (\d+) \| [^|]*\| (sub_[0-9A-Fa-f]+) \|", re.M)

# Exactly three pairs of S2C opcodes decompile to byte-identical handler
# bodies; docs/PACKETS.md 3.15q and 3.15q2 depend on the list being complete,
# so re-derive it rather than trusting the prose.
EQUIVALENT_PAIRS = [(122, 748), (276, 278), (361, 972)]


def handler_bodies(text: str) -> dict[str, str]:
    """Body of every sub_* function in the dump, whitespace-normalised."""
    bodies = {}
    for match in FUNCTION_HEAD.finditer(text):
        start = match.end()
        depth = 1
        index = start
        while index < len(text) and depth:
            character = text[index]
            if character == "{":
                depth += 1
            elif character == "}":
                depth -= 1
            index += 1
        bodies[match.group(1)] = re.sub(r"\s+", " ", text[start:index - 1]).strip()
    return bodies


def check_equivalent_handlers(text: str) -> None:
    bodies = handler_bodies(text)
    table = {int(op): handler for op, handler in TABLE_HANDLER.findall(
        LAYOUTS.read_text(encoding="utf-8"))}

    groups: dict[str, set[tuple[int, str]]] = {}
    for opcode, handler in table.items():
        if handler in bodies:
            groups.setdefault(bodies[handler], set()).add((opcode, handler))
    found = sorted(tuple(sorted(opcode for opcode, _ in members))
                   for members in groups.values()
                   if len({handler for _, handler in members}) > 1)

    expected = sorted(tuple(sorted(pair)) for pair in EQUIVALENT_PAIRS)
    if found != expected:
        print("dispatcher verification failed: identical-handler groups changed")
        print(f"  expected {expected}")
        print(f"  found    {found}")
        raise SystemExit(1)


def main() -> None:
    if not DUMP.is_file():
        print(f"dispatcher check skipped: {DUMP} is not present")
        return

    text = DUMP.read_text(encoding="utf-8", errors="replace")
    check_equivalent_handlers(text)
    cases = dispatcher_cases(text)
    documented = table_opcodes(LAYOUTS)
    missing = sorted(cases - documented)
    if missing:
        print("dispatcher coverage failed:")
        print(f"  {len(missing)} dispatcher case(s) absent from {LAYOUTS.relative_to(ROOT)}:")
        print(f"    {missing}")
        raise SystemExit(1)

    catalog = catalog_opcodes()
    request = table_opcodes(LAYOUTS_REQ)
    unnamed = sorted((documented | request) - catalog)
    beyond = [opcode for opcode in unnamed if opcode > max(catalog)]

    print(
        f"dispatcher coverage OK: {len(cases)} cases in {DISPATCHER}, all present in "
        f"{LAYOUTS.name}; {len(catalog)} named opcodes in packets.tsv; "
        f"{len(unnamed)} opcode(s) have a native reader/writer but no registered name "
        f"({len(beyond)} of them past the catalog's last id {max(catalog)})")


if __name__ == "__main__":
    main()
