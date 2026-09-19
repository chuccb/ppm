#!/usr/bin/env python3
"""Verify that db/packets.tsv still equals the client binary's name
registry.

PaperMan.exe.c registers packet names via `sub_9EAF50(obj, OP, name)`
pairs (name literal emitted a few lines above the call). We extract all
pairs and require:
  (1) every registry pair exists in db/packets.tsv with the same name
      (proved 670/670, 0 conflicts on 2026-09-19);
  (2) the only tsv entries missing from the binary registry are the six
      room-mode pairs (tsv's second authoritative channel):
      366/367 GR_LOCALROOM, 969/970 GR_SOCCER, 990/991 GR_DAMAGEROOM.

This makes the STYLE.md authority-hierarchy claim "packets.tsv is the
official catalog" machine-checkable instead of assumed.
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "PaperMan.exe.c"
TSV = ROOT / "db" / "packets.tsv"

EXPECTED_TSV_ONLY = {
    366: "GR_LOCALROOM_REQ", 367: "GR_LOCALROOM_ACK",
    969: "GR_SOCCER_REQ", 970: "GR_SOCCER_ACK",
    990: "GR_DAMAGEROOM_REQ", 991: "GR_DAMAGEROOM_ACK",
}

OP_CALL = re.compile(r"sub_9EAF50\([^,]+,\s*(\d+),")
NAME_RE = re.compile(r'L"([A-Z][A-Z0-9_]+)"')


def registry_pairs() -> dict[int, str]:
    lines = SRC.read_text(encoding="utf-8", errors="ignore").splitlines()
    pairs: dict[int, str] = {}
    for i, line in enumerate(lines):
        m = OP_CALL.search(line)
        if not m:
            continue
        for j in range(i, max(-1, i - 6), -1):
            nm = NAME_RE.search(lines[j])
            if nm:
                pairs[int(m.group(1))] = nm.group(1)
                break
    return pairs


def main() -> int:
    pairs = registry_pairs()
    tsv: dict[int, str] = {}
    for raw in TSV.read_text(encoding="utf-8").splitlines():
        f = raw.split("\t")
        if len(f) >= 2 and f[0].isdigit():
            tsv[int(f[0])] = f[1]

    problems = 0
    for op, name in sorted(pairs.items()):
        if tsv.get(op) != name:
            print(f"conflict: op {op} registry={name} tsv={tsv.get(op)}",
                  file=sys.stderr)
            problems += 1
    extras = {op: n for op, n in tsv.items() if op not in pairs}
    for op, name in sorted(extras.items()):
        if EXPECTED_TSV_ONLY.get(op) != name:
            print(f"unexpected tsv-only entry: {op} {name}", file=sys.stderr)
            problems += 1
    for op in EXPECTED_TSV_ONLY - extras.keys():
        # not an error: a new binary build could move them into the registry
        print(f"note: expected tsv-only {op} now present in registry")

    if problems:
        print(f"verify_tsv_registry: {problems} problem(s)", file=sys.stderr)
        return 1
    print(f"verify_tsv_registry: OK ({len(pairs)} pairs, "
          f"{len(extras)} tsv-only as expected)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
