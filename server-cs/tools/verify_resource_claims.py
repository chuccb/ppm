#!/usr/bin/env python3
"""Re-derive the countable claims in docs/RESOURCES.md from Extracted/.

Run from any working directory:
    python3 server-cs/tools/verify_resource_claims.py

The markdown files are written by hand and drift. Several of this project's
conclusions rest on row counts and record strides that are cheap to re-check
against the shipped resources, so this tool recomputes them and fails when a
number stops matching. Every expectation below was verified against the files
at the time it was added; if one fires, trust the resource and fix the doc.

Requires `Extracted/` (present on the `main` branch). When a file is absent the
individual check is skipped rather than failed, so a partial checkout still
runs. Needs no .NET SDK.
"""
from __future__ import annotations

import re
import struct
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXTRACTED = ROOT / "Extracted"

failures: list[str] = []
skipped: list[str] = []
checked = 0


def decrypt(path: Path) -> bytes | None:
    """pmFile-decrypt when needed; plain UTF-8/BOM files are returned as-is."""
    if not path.is_file():
        return None
    blob = path.read_bytes()
    if blob[:3] == b"\xef\xbb\xbf" or blob[:1] == b"<":
        return blob
    sys.path.insert(0, str(ROOT / "server"))
    from pmfile import pmfile_decrypt
    return pmfile_decrypt(blob)


def check(name: str, actual, expected) -> None:
    global checked
    checked += 1
    if actual != expected:
        failures.append(f"{name}: expected {expected!r}, resource says {actual!r}")


def csv_rows(blob: bytes, *, header_lines: int) -> list[list[str]]:
    lines = blob.decode("cp932", "replace").splitlines()
    return [line.split(",") for line in lines[header_lines:]
            if line.strip() and line.split(",")[0].strip().isdigit()]


def main() -> None:
    # itemdata.pat: 21,164 records, 997B stride, self-verifying length.
    blob = decrypt(EXTRACTED / "ui" / "cfg" / "itemdata.pat")
    if blob is None:
        skipped.append("itemdata.pat")
    else:
        version, count = struct.unpack_from("<ii", blob, 0)
        check("itemdata.pat version", version, 1)
        check("itemdata.pat count", count, 21164)
        check("itemdata.pat length", len(blob), 8 + count * 997)
        # kind lives at record+277 in file coordinates (RESOURCES.md 2c note).
        kinds = Counter(blob[8 + i * 997 + 277] for i in range(count))
        check("itemdata.pat kind==9 (weapons)", kinds[9], 10914)
        # The price carrier is empty in this Japanese revision except tail[107].
        nonzero = sum(1 for i in range(count) if blob[8 + i * 997 + 276 + 107])
        check("itemdata.pat tail[107] non-zero rows", nonzero, 23)

    # maplist.pat: 123 maps, 836B stride, every map carries a mode bit.
    blob = decrypt(EXTRACTED / "ui" / "cfg" / "maplist.pat")
    if blob is None:
        skipped.append("maplist.pat")
    else:
        count = struct.unpack_from("<i", blob, 4)[0]
        check("maplist.pat count", count, 123)
        check("maplist.pat length", len(blob), 8 + count * 836)
        modeless = sum(1 for i in range(count)
                       if struct.unpack_from("<i", blob, 8 + i * 836)[0] == 0)
        check("maplist.pat maps with no mode bit", modeless, 0)

    # CSV catalogues: declared count in line 0 must match the parsed rows.
    for filename, header_lines, expected_rows, expected_columns in (
            ("Quest.pat", 2, 844, 47),
            ("weaponparts.pat", 2, 1108, 81),
            ("partsability.pat", 2, 413, 31),
            ("RecommandItem.pat", 3, 1030, 12)):
        blob = decrypt(EXTRACTED / "ui" / "cfg" / filename)
        if blob is None:
            skipped.append(filename)
            continue
        lines = blob.decode("cp932", "replace").splitlines()
        check(f"{filename} declared count", int(lines[0].split(",")[0]), expected_rows)
        check(f"{filename} parsed rows", len(csv_rows(blob, header_lines=header_lines)),
              expected_rows)
        check(f"{filename} columns", len(lines[header_lines - 1].split(",")), expected_columns)

    # msgtableres.lang: entry i is line i+3; 1346 entries.
    path = EXTRACTED / "ui" / "lang" / "msgtableres.lang"
    if not path.is_file():
        skipped.append("msgtableres.lang")
    else:
        lines = path.read_bytes().decode("cp932", "replace").split("\n")
        check("msgtableres.lang entries", len(lines) - 3, 1346)

    # pe-pachi_scenario.xml: four ordered tiers drive the 701 reelC animation.
    blob = decrypt(EXTRACTED / "pepachi" / "pe-pachi_scenario.xml")
    if blob is None:
        skipped.append("pe-pachi_scenario.xml")
    else:
        text = blob.decode("utf-8", "replace")
        tiers = [(match.group(1), len(re.findall(r"<Type\d+", match.group(2))))
                 for match in re.finditer(r"<(Rare|Atari|Zannen|Suka)>(.*?)</\1>", text, re.S)]
        check("pe-pachi_scenario.xml tiers",
              tiers, [("Rare", 3), ("Atari", 11), ("Zannen", 8), ("Suka", 44)])

    # face_contents.xml: chat-triggered expressions, not character creation.
    blob = decrypt(EXTRACTED / "ui" / "system" / "face_contents.xml")
    if blob is None:
        skipped.append("face_contents.xml")
    else:
        text = blob.decode("utf-8", "replace")
        faces = re.findall(r'<face_(\d+) index="\d+">(.*?)</face_\1>', text, re.S)
        check("face_contents.xml faces", len(faces), 5)
        check("face_contents.xml trigger words",
              sum(len(re.findall(r'content="', body)) for _, body in faces), 122)

    # convars.pat: 14 ability blocks; ICT_DEVILGIRL is deliberately absent.
    blob = decrypt(EXTRACTED / "convars.pat")
    if blob is None:
        skipped.append("convars.pat")
    else:
        text = blob.decode("cp932", "replace")
        blocks = set(re.findall(r"m_cAvataAbility\[(\w+)\]", text))
        check("convars.pat ability blocks", len(blocks), 14)
        check("convars.pat has ICT_DEVILGIRL", "ICT_DEVILGIRL" in blocks, False)

    if failures:
        print("resource claim verification failed:")
        for failure in failures:
            print(f"  {failure}")
        raise SystemExit(1)

    summary = f"resource claims OK: {checked} checks re-derived from Extracted/"
    if skipped:
        summary += f"; skipped {len(skipped)} missing file(s): {', '.join(sorted(set(skipped)))}"
    print(summary)


if __name__ == "__main__":
    main()
