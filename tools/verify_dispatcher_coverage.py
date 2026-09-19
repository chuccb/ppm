#!/usr/bin/env python3
"""Check docs/LAYOUTS.md covers every case in the native packet dispatcher.

Run from any working directory:
    python3 tools/verify_dispatcher_coverage.py

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

ROOT = Path(__file__).resolve().parents[1]
DUMP = ROOT / "PaperMan.exe.c"
LAYOUTS = ROOT / "docs" / "LAYOUTS.md"
LAYOUTS_REQ = ROOT / "docs" / "LAYOUTS.md"  # merged: S2C + C2S inventories in one file
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


SYMBOL = re.compile(r"\b(sub_[0-9A-Fa-f]{4,8})\b")
# PACKETS.md mentions 9 sub_ symbols absent from every dump: 6 genuinely
# unresolved, two kept as before/after examples in the header note, plus
# sub_593260 cited at the send_count++ row and explicitly annotated as
# drifted. 28 more were resolved by following Packet(opcode) builders.
# LAYOUTS.md (merged S2C+C2S) is machine-extracted and must stay at zero.
# See the note at the top of PACKETS.md. (Baseline 8 -> 9 on 2026-09-19:
# the drifted 593260 citation predates this count.)
EXPECTED_STALE = {"docs/PACKETS.md": 9, "docs/LAYOUTS.md": 0}


def check_cited_symbols(text: str) -> None:
    present = set(SYMBOL.findall(text))
    for relative, expected in EXPECTED_STALE.items():
        path = ROOT / relative
        if not path.is_file():
            continue
        cited = set(SYMBOL.findall(path.read_text(encoding="utf-8")))
        stale = sorted(cited - present)
        if len(stale) != expected:
            print(f"dispatcher verification failed: {relative} cites {len(stale)} "
                  f"symbol(s) absent from the dump, expected {expected}")
            print(f"  {stale[:20]}")
            raise SystemExit(1)


UDP_DISPATCHER = "sub_595E80"
# The UDP private opcode space is separate from TCP's 100..1010 and is small
# enough to pin exactly. docs/PACKETS.md "UDP private opcode 空間全圖" depends
# on these three facts, so re-derive them instead of trusting the prose.
UDP_RECEIVE_CASES = [2, 4, 5, 6, 8, 10, 12, 13, 14, 15, 18, 20, 22, 24, 26, 28,
                     29, 31, 33, 34, 154, 158]
UDP_SEND_OPCODES = [1, 5, 6, 9, 13, 14, 15, 17, 19, 21, 23, 27, 30, 32, 35]
# Direct branch identity for sub_595E80. The grouped 8/24 branch is intentional:
# the native dispatcher does not establish separate official names for those two
# numeric cases. Case 26 was decoded from the PE on 2026-09-18: its 16-byte body
# is an empty thiscall (prologue; save ecx; ret 4), so the doc anchors the raw
# byte signature "55 8B EC 51 89 4D FC" — keep unknown_libname_107 in the map
# so a decompiler re-export cannot silently turn case 26 into a guessed layout.
UDP_RECEIVE_HANDLERS = {
    2: "sub_593A60", 4: "sub_593AB0", 5: "sub_593E60", 6: "sub_5940E0",
    10: "sub_594460", 12: "sub_5946C0", 13: "sub_594A10", 14: "sub_594CA0",
    15: "sub_593DF0", 18: "sub_596300", 20: "sub_5968C0", 22: "sub_5964E0",
    26: "unknown_libname_107", 28: "sub_594E80", 29: "sub_593E20",
    31: "sub_594EA0", 33: "sub_594EC0", 34: "sub_594F20",
    154: "sub_5965D0", 158: "sub_596910",
}
UDP_SHARED_RECEIVE_HANDLER = (8, 24, "sub_596940")
UDP_DOC_CASE_MARKERS = [
    "## Appendix B — `sub_595E80` UDP-private dispatcher / every verified receive case",
    "`unknown_libname_107`", "55 8B EC 51 89 4D FC",
    "`154 UDP_ALL_PING_ACK`", "`158 UDP_TCP_DEAD_ACK`",
    "`sub_595980` 為明確位址 AES 送出",
]
UDP_LOW_SEND_MAX_OPCODE = 40  # low private outbound constructors; receive cases also include 154/158
UDP_SHARED_HEADER_BUILDERS = {1: "sub_593830", 9: "sub_594300", 19: "sub_596670",
                              21: "sub_596330", 23: "sub_744450", 27: "sub_6013E0",
                              30: "sub_6065E0", 32: "sub_96BF70", 35: "sub_7463E0"}
UDP_IDENTITY_SOURCES = ("sub_417D00", "byte_EE896D", "dword_EE8CB4")
WRITE_PRIMITIVE = {
    "sub_592920": "u8", "sub_592960": "u8", "sub_5928E0": "s8",
    "sub_5929A0": "u16", "sub_5929E0": "s16",
    "sub_592A20": "s32", "sub_592A60": "u32", "sub_592AA0": "raw4",
    "sub_592AC0": "raw4", "sub_592B20": "f32",
    "sub_592AE0": "u64", "sub_592B60": "u64", "sub_5926F0": "str",
}


def check_udp_opcode_space(text: str) -> None:
    head = re.search(r"\n[A-Za-z_][^\n]*\b" + UDP_DISPATCHER + r"\([^)]*\)\s*\r?\n\{", text)
    if head is None:
        print(f"dispatcher verification failed: cannot locate {UDP_DISPATCHER}")
        raise SystemExit(1)
    start = head.end()
    depth = 1
    index = start
    while index < len(text) and depth:
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
        index += 1

    received = sorted({int(value) for value in CASE.findall(text[start:index])})
    sent = sorted({int(value) for value in
                   re.findall(r"Packet::possible_ctor_or_dtor_0\([^,]+, (\d+)\)", text)
                   if int(value) <= UDP_LOW_SEND_MAX_OPCODE})
    for label, actual, expected in (("receive cases", received, UDP_RECEIVE_CASES),
                                    ("send opcodes", sent, UDP_SEND_OPCODES)):
        if actual != expected:
            print(f"dispatcher verification failed: UDP {label} changed")
            print(f"  expected {expected}")
            print(f"  found    {actual}")
            raise SystemExit(1)

    paired = [opcode for opcode in sent if opcode + 1 in received]
    if len(paired) != 12:
        print(f"dispatcher verification failed: UDP n->n+1 pairs = {len(paired)}, expected 12")
        raise SystemExit(1)

    # Nine UDP senders share a u8/u8/u8/s32 identity header taken from the same
    # three globals. docs/PACKETS.md documents the layout on that basis.
    for opcode, builder in UDP_SHARED_HEADER_BUILDERS.items():
        head = re.search(r"\n[A-Za-z_][^\n]*\b" + builder + r"\([^)]*\)\s*\r?\n\{", text)
        if head is None:
            print(f"dispatcher verification failed: UDP builder {builder} not found")
            raise SystemExit(1)
        start = head.end()
        depth = 1
        index = start
        while index < len(text) and depth:
            if text[index] == "{":
                depth += 1
            elif text[index] == "}":
                depth -= 1
            index += 1
        body = text[start:index - 1]
        after = body[body.find(f", {opcode})"):]
        written = [WRITE_PRIMITIVE[name] for name
                   in re.findall(r"(sub_592[0-9A-F]{3})\(", after)
                   if name in WRITE_PRIMITIVE]
        missing = [source for source in UDP_IDENTITY_SOURCES if source not in body]
        expected_prefix = (["u8", "u8", "s8", "u8", "s32"]
                           if opcode == 19 else ["u8", "u8", "u8", "s32"])
        if written[:len(expected_prefix)] != expected_prefix or missing:
            print(f"dispatcher verification failed: UDP {opcode} ({builder}) header changed")
            print(f"  prefix {written[:len(expected_prefix)]}, missing identity sources {missing}")
            raise SystemExit(1)


def _body_after_dispatcher(text: str) -> str:
    """Return the brace-matched body of sub_595E80."""
    head = re.search(r"\n[A-Za-z_][^\n]*\b" + UDP_DISPATCHER + r"\([^)]*\)\s*\r?\n\{", text)
    if head is None:
        print(f"dispatcher verification failed: cannot locate {UDP_DISPATCHER}")
        raise SystemExit(1)
    start = head.end()
    depth = 1
    index = start
    while index < len(text) and depth:
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
        index += 1
    if depth:
        print(f"dispatcher verification failed: unbalanced body for {UDP_DISPATCHER}")
        raise SystemExit(1)
    return text[start:index]


def check_udp_dispatcher_handlers(text: str) -> None:
    """Ensure each documented private receive case still calls its native callee."""
    body = _body_after_dispatcher(text)
    for opcode, handler in UDP_RECEIVE_HANDLERS.items():
        match = re.search(rf"case {opcode}:\s*(.*?)(?=case \d+:|default:)", body, re.S)
        if match is None or handler not in match.group(1):
            print(f"dispatcher verification failed: UDP case {opcode} no longer calls {handler}")
            raise SystemExit(1)

    first, second, handler = UDP_SHARED_RECEIVE_HANDLER
    grouped = re.search(
        rf"case {first}:\s*case {second}:\s*(.*?)(?=case \d+:|default:)",
        body, re.S)
    if grouped is None or handler not in grouped.group(1):
        print(f"dispatcher verification failed: UDP cases {first}/{second} no longer share {handler}")
        raise SystemExit(1)

    documented = LAYOUTS_REQ.read_text(encoding="utf-8")
    missing = [marker for marker in UDP_DOC_CASE_MARKERS if marker not in documented]
    if missing:
        print("dispatcher verification failed: LAYOUTS.md UDP appendix lost marker(s)")
        print(f"  {missing}")
        raise SystemExit(1)

    appendix_a = documented.split("## Appendix A —", 1)[1].split("## Appendix B —", 1)[0]
    appendix_b = documented.split("## Appendix B —", 1)[1]
    missing_sends = [opcode for opcode in UDP_SEND_OPCODES
                     if f"| {opcode} |" not in appendix_a]
    if missing_sends:
        print("dispatcher verification failed: LAYOUTS.md Appendix A lost UDP outbound row(s)")
        print(f"  {missing_sends}")
        raise SystemExit(1)

    missing_receives = []
    for opcode in UDP_RECEIVE_CASES:
        marker = "| 8 / 24 |" if opcode in (8, 24) else f"| {opcode} |"
        if marker not in appendix_b:
            missing_receives.append(opcode)
    if missing_receives:
        print("dispatcher verification failed: LAYOUTS.md Appendix B lost UDP inbound row(s)")
        print(f"  {missing_receives}")
        raise SystemExit(1)


def main() -> None:
    if not DUMP.is_file():
        print(f"dispatcher check skipped: {DUMP} is not present")
        return

    text = DUMP.read_text(encoding="utf-8", errors="replace")
    check_equivalent_handlers(text)
    check_cited_symbols(text)
    check_udp_opcode_space(text)
    check_udp_dispatcher_handlers(text)
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
