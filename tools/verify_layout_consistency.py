#!/usr/bin/env python3
"""Cross-check LAYOUTS.md rows that describe the same opcode in both the
main table (Part I) and the req-resp table (Part II/III).

Each descriptive cell embeds wire grammars in backticks. We extract the
type-token stream (u8/s8 etc.) from every backticked grammar of every row
for the same op and compare the first two tokens. Rows that document
*multiple disjoint native forms* on one line (165 / 583 / 695 / 28 style)
are expected to differ inside one row, so only *cross-row* pairs whose
first two tokens diverge are flagged; those rows are whitelisted after
manual review (see EXPECTED_BENIGN).

Exit 0 = no unexplained divergence. Run after broker doc edits.
"""
import collections
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DOC = ROOT / "docs" / "LAYOUTS.md"

# ops whose single row legitimately enumerates several disjoint native
# forms (multi-form opcodes); divergent tokens inside them are by design.
EXPECTED_BENIGN = {28, 144, 165, 203, 583, 695}

TOKEN_RE = re.compile(
    r"\b(s32|u32|s16|u16|u8|s8/bool|s8|f32|raw4|raw1|str\d*)\b"
)


def grammars_of(cell: str):
    out = []
    for g in re.findall(r"`([^`]+)`", cell):
        g2 = re.sub(r"\(.*?\)|（.*?）", "", g)
        toks = TOKEN_RE.findall(g2)
        if len(toks) >= 2:
            out.append((g[:80], toks))
    return out


def main() -> int:
    text = DOC.read_text(encoding="utf-8")
    byop: dict[int, list[tuple[int, str]]] = collections.defaultdict(list)
    for lineno, line in enumerate(text.splitlines(), 1):
        m = re.match(r"^\| (\d+) \|", line)
        if not m:
            continue
        for cell in line.split("|")[2:-1]:
            for ga, toks in grammars_of(cell.strip()):
                byop[int(m.group(1))].append((lineno, ga, tuple(toks)))

    bad = []
    for op, entries in sorted(byop.items()):
        if op in EXPECTED_BENIGN or len(entries) < 2:
            continue
        base = entries[0][2][:2]
        for lineno, ga, toks in entries[1:]:
            if toks[:2] != base:
                bad.append((op, lineno, entries[0][1], ga))

    for op, lineno, first, other in bad:
        print(f"op {op}: first-2 token divergence", file=sys.stderr)
        print(f"  baseline: {first}", file=sys.stderr)
        print(f"  line {lineno}: {other}", file=sys.stderr)
        print("  -> if the row legitimately documents several native forms,",
              file=sys.stderr)
        print("     add the op to EXPECTED_BENIGN after manual review.",
              file=sys.stderr)
    if bad:
        print(f"verify_layout_consistency: {len(bad)} divergence(s)", file=sys.stderr)
        return 1
    print("verify_layout_consistency: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
