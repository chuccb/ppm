#!/usr/bin/env python3
"""Decode cfg/maplist.pat: map id, mode bitmask and .pmm path.

Run from any working directory:
    python3 server-cs/tools/dump_maplist.py              # full catalog
    python3 server-cs/tools/dump_maplist.py --mode 3     # maps playable in one modeIndex
    python3 server-cs/tools/dump_maplist.py --id 89      # one map
    python3 server-cs/tools/dump_maplist.py --check      # verify against the mode-bit table

`maplist.pat` is the authoritative catalog for this revision; `map/maplist.dat`
is an older plaintext copy (v1.02, 67 maps, 832B records) kept for version
archaeology. See docs/RESOURCES.md section 5e.

Layout, which is self-verifying:

    +0  f32 version    (this revision: 1.03)
    +4  s32 count      (this revision: 123)
    +8  count x 836-byte records
        record +0  s32 mode bitmask   (bit N set => playable in the modeIndex
                                       whose maplist bit is N)
        record +4  s32 map id
        record +8  UTF-16LE NUL-terminated "maps\\<NAME>.pmm"

8 + count*836 equals the decrypted size exactly; the script asserts that so a
revision with a different stride fails loudly rather than mis-parsing.

The bitmask is a set, not a scalar: a map may be listed for several modes, and
the tutorial maps carry both Practice and Tutorial. Deriving a map's mode from
its filename prefix is NOT reliable -- PVE_01_ruins is AIMulti and the worldcup
TS_* maps are TeamSoccer. Always read the bitmask.
"""
from __future__ import annotations

import argparse
import struct
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENCRYPTED = ROOT / "Extracted" / "ui" / "cfg" / "maplist.pat"

HEADER_SIZE = 8
RECORD_STRIDE = 836
PATH_OFFSET = 8
PATH_MAX_BYTES = 200

# modeIndex -> bit position in the maplist mask. Same table as
# verify_server_naming.py MODE_INDEX_MAP_BITS; docs/RESOURCES.md section 4b.
MODE_INDEX_MAP_BITS = {0: 2, 1: 0, 2: 3, 3: 1, 4: 4, 6: 6, 8: 9, 9: 10,
                       10: 12, 11: 13, 12: 14, 13: 15, 15: 11}
MODE_NAMES = {0: "TeamMatch", 1: "IndividualSurvival", 2: "DefuseBomb",
              3: "TeamSurvival", 4: "Steal", 5: "Practice", 6: "Tutorial",
              7: "ChattingRoom", 8: "PulpnRoll", 9: "GunShooting", 10: "Occupy",
              11: "AIMulti", 12: "TeamSoccer", 13: "OccupyRenewal", 15: "WeaponTest"}
BIT_TO_MODE_INDEX = {bit: index for index, bit in MODE_INDEX_MAP_BITS.items()}


def decrypt() -> bytes:
    sys.path.insert(0, str(ROOT / "server"))
    try:
        from pmfile import pmfile_decrypt
    except ImportError as error:
        raise SystemExit(f"cannot import server/pmfile.py: {error}") from error
    if not ENCRYPTED.is_file():
        raise SystemExit(f"missing {ENCRYPTED}")
    return pmfile_decrypt(ENCRYPTED.read_bytes())


def parse(blob: bytes) -> list[tuple[int, int, str]]:
    if len(blob) < HEADER_SIZE:
        raise SystemExit("maplist.pat is too short to hold a header")
    version, count = struct.unpack_from("<fi", blob, 0)
    expected = HEADER_SIZE + count * RECORD_STRIDE
    if expected != len(blob):
        raise SystemExit(
            f"maplist.pat does not match the known layout: version={version:.2f} "
            f"count={count} implies {expected} bytes but the file is {len(blob)}. "
            "Re-derive the record stride before trusting output.")
    rows = []
    for index in range(count):
        base = HEADER_SIZE + index * RECORD_STRIDE
        mask, map_id = struct.unpack_from("<ii", blob, base)
        path = blob[base + PATH_OFFSET:base + PATH_OFFSET + PATH_MAX_BYTES] \
            .decode("utf-16-le", "replace").split("\x00")[0]
        rows.append((map_id, mask, path))
    return rows


def mode_names(mask: int) -> list[str]:
    names = []
    for bit in range(32):
        if not mask >> bit & 1:
            continue
        index = BIT_TO_MODE_INDEX.get(bit)
        names.append(MODE_NAMES.get(index, f"bit{bit}") if index is not None else f"bit{bit}")
    return names


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mode", type=int, help="list maps playable in this modeIndex")
    parser.add_argument("--id", type=int, help="show one map id")
    parser.add_argument("--check", action="store_true",
                        help="assert every map carries at least one known mode bit")
    arguments = parser.parse_args()

    rows = parse(decrypt())

    if arguments.id is not None:
        for map_id, mask, path in rows:
            if map_id == arguments.id:
                print(f"{map_id}\t{mask}\t{','.join(mode_names(mask))}\t{path}")
                return
        print(f"map id {arguments.id} not present")
        return

    if arguments.mode is not None:
        bit = MODE_INDEX_MAP_BITS.get(arguments.mode)
        if bit is None:
            raise SystemExit(f"modeIndex {arguments.mode} has no documented maplist bit")
        hits = [r for r in rows if r[1] >> bit & 1]
        for map_id, mask, path in hits:
            print(f"{map_id}\t{path}")
        print(f"# {len(hits)} maps for modeIndex {arguments.mode} "
              f"({MODE_NAMES.get(arguments.mode, '?')})", file=sys.stderr)
        return

    if arguments.check:
        nomode = [r for r in rows if r[1] == 0]
        if nomode:
            raise SystemExit(f"{len(nomode)} maps carry no mode bit: {nomode[:5]}")
        print(f"maplist.pat OK: {len(rows)} maps, every map carries at least one mode bit")
        counts: Counter[str] = Counter()
        for _, mask, _ in rows:
            for name in mode_names(mask):
                counts[name] += 1
        for name, total in sorted(counts.items(), key=lambda pair: -pair[1]):
            print(f"  {name:22} {total:3d}")
        return

    for map_id, mask, path in rows:
        print(f"{map_id}\t{mask}\t{','.join(mode_names(mask))}\t{path}")


if __name__ == "__main__":
    main()
