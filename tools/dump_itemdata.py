#!/usr/bin/env python3
"""Decode cfg/itemdata.pat and print item id -> display name.

Run from any working directory:
    python3 tools/dump_itemdata.py                 # summary + band counts
    python3 tools/dump_itemdata.py --name MP5K     # exact name lookup
    python3 tools/dump_itemdata.py --id 12100027   # id lookup
    python3 tools/dump_itemdata.py --band 12100000 # list one 100k band

This is a resource reader, not a protocol authority. An entry here proves only
that the shipped client knows an item id and its display string. It does NOT
prove the item is purchasable, grantable, priced, owned by a new account, or
present in any historical pool -- this revision's price region is almost
entirely zero. See docs/RESOURCES.md section 2c and docs/WIKI_MECHANICS.md.

File layout is self-verifying, which is why the stride is trustworthy:

    +0  s32 version    (this revision: 1)
    +4  s32 count      (this revision: 21164)
    +8  count x 997-byte records
        record +0   s32 item_id
        record +20  UTF-16LE NUL-terminated display name

8 + count*997 equals the decrypted file size exactly, with no trailing bytes.
The script asserts that, so a different revision fails loudly instead of
silently mis-parsing.
"""
from __future__ import annotations

import argparse
import struct
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ENCRYPTED = ROOT / "Extracted" / "ui" / "cfg" / "itemdata.pat"

RECORD_STRIDE = 997
NAME_OFFSET = 20
NAME_MAX_BYTES = 120
HEADER_SIZE = 8

# Weapon loadout bands, from docs/RESOURCES.md section 5a2.
WEAPON_BANDS = {
    12100000: "primary",
    12200000: "secondary",
    12300000: "melee",
    12400000: "throw",
}


def decrypt() -> bytes:
    """Reuse the repository's pmFile decryptor rather than duplicating it."""
    sys.path.insert(0, str(ROOT / "server"))
    try:
        from pmfile import pmfile_decrypt
    except ImportError as error:
        raise SystemExit(f"cannot import server/pmfile.py: {error}") from error
    if not ENCRYPTED.is_file():
        raise SystemExit(f"missing {ENCRYPTED}")
    return pmfile_decrypt(ENCRYPTED.read_bytes())


def parse(blob: bytes) -> dict[int, str]:
    if len(blob) < HEADER_SIZE:
        raise SystemExit("itemdata.pat is too short to hold a header")
    version, count = struct.unpack_from("<ii", blob, 0)
    expected = HEADER_SIZE + count * RECORD_STRIDE
    if expected != len(blob):
        raise SystemExit(
            f"itemdata.pat does not match the known layout: version={version} "
            f"count={count} implies {expected} bytes but the file is {len(blob)}. "
            "The record stride differs in this revision; re-derive it before trusting output.")

    items: dict[int, str] = {}
    for index in range(count):
        base = HEADER_SIZE + index * RECORD_STRIDE
        item_id = struct.unpack_from("<i", blob, base)[0]
        raw = blob[base + NAME_OFFSET:base + NAME_OFFSET + NAME_MAX_BYTES]
        name = raw.decode("utf-16-le", "replace").split("\x00")[0].strip()
        items[item_id] = name
    return items


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", help="print every item whose display name matches exactly (case-insensitive)")
    parser.add_argument("--id", type=int, help="print one item id")
    parser.add_argument("--band", type=int, help="list a 100000-wide id band")
    arguments = parser.parse_args()

    items = parse(decrypt())

    if arguments.id is not None:
        name = items.get(arguments.id)
        print(f"{arguments.id}\t{name}" if name is not None else f"{arguments.id} not present")
        return

    if arguments.name:
        wanted = arguments.name.casefold()
        matches = sorted((i, n) for i, n in items.items() if n.casefold() == wanted)
        if not matches:
            print(f"no item named {arguments.name!r}")
        for item_id, name in matches:
            print(f"{item_id}\t{name}")
        return

    if arguments.band is not None:
        low = arguments.band
        high = low + 99999
        rows = sorted((i, n) for i, n in items.items() if low <= i <= high)
        for item_id, name in rows:
            print(f"{item_id}\t{name}")
        print(f"# {len(rows)} items in {low}..{high}", file=sys.stderr)
        return

    bands = Counter(item_id // 100000 * 100000 for item_id in items)
    print(f"itemdata.pat: {len(items)} records, layout self-check passed")
    print("\nweapon loadout bands (docs/RESOURCES.md section 5a2):")
    for band, label in WEAPON_BANDS.items():
        member_ids = [i for i in items if band <= i <= band + 99999]
        if not member_ids:
            continue
        print(f"  {band} {label:10} {len(member_ids):5d} items  "
              f"min={min(member_ids)} max={max(member_ids)}")
    print("\nall bands:")
    for band, total in sorted(bands.items()):
        print(f"  {band:>10} {total:5d}")


if __name__ == "__main__":
    main()
