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

import csv
import re
import struct
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EXTRACTED = ROOT / "Extracted"
DUMP = ROOT / "PaperMan.exe.c"

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

    # map/maps/*.ini are plaintext spawn tables; crystal slots pair 1:1 with
    # team spawns on the two maps that still carry crystal data.
    maps = EXTRACTED / "map" / "maps"
    if not maps.is_dir():
        skipped.append("map/maps/*.ini")
    else:
        crystal_totals = {}
        for path in sorted(maps.glob("*.ini")):
            text = path.read_text(encoding="utf-8", errors="replace")
            spawns = len(re.findall(r"^\s*team ", text, re.M))
            block = re.search(r"\[CrystalSpawnPoint\](.*?)\n\}", text, re.S)
            slots = re.findall(r"^\s*(none|small|large)\s*$", block.group(1), re.M) if block else []
            crystal_totals[path.name] = (spawns, len(slots))
            if slots:
                check(f"{path.name} crystal slots pair with spawns", len(slots), spawns)
        check("maps carrying crystal data",
              sorted(name for name, (_, slots) in crystal_totals.items() if slots),
              ["TS_14_Stadium.ini", "TS_40_SlumTown2.ini"])

    # killImgWeapon.xml indexes weapons by band offset with no band field,
    # which only works because the four weapon bands never share an offset.
    blob = decrypt(EXTRACTED / "ui" / "killImgWeapon.xml")
    itemdata = decrypt(EXTRACTED / "ui" / "cfg" / "itemdata.pat")
    if blob is None or itemdata is None:
        skipped.append("killImgWeapon.xml")
    else:
        text = blob.decode("utf-8", "replace")
        rows = re.findall(r"<!--(\d+)-->\s*<element [^>]*?/>", text)
        indices = {int(value) for value in rows}
        check("killImgWeapon.xml entries", len(rows), 3006)
        check("killImgWeapon.xml distinct indices", len(indices), 3004)

        count = struct.unpack_from("<i", itemdata, 4)[0]
        item_ids = {struct.unpack_from("<i", itemdata, 8 + i * 997)[0] for i in range(count)}
        bands = (12100000, 12200000, 12300000, 12400000)
        collisions = [offset for offset in range(3100)
                      if sum(1 for band in bands if band + offset in item_ids) > 1]
        check("weapon band offset collisions", collisions, [])
        resolved = sum(1 for offset in indices
                       if any(band + offset in item_ids for band in bands))
        check("killImgWeapon indices resolving to a weapon", resolved, 2074)

    # Projectile physics tables. Rocket entries are named types; the plasma and
    # laser tables key on gunindex, which is a weapon-band offset.
    blob = decrypt(EXTRACTED / "ui" / "RocketProperty.xml")
    if blob is None:
        skipped.append("RocketProperty.xml")
    else:
        text = blob.decode("utf-8", "replace")
        check("RocketProperty.xml named types",
              len(re.findall(r"<([A-Z][A-Z0-9_]*) commonproperty", text)), 26)
        check("RocketProperty.xml commonproperty spread",
              dict(Counter(re.findall(r'commonproperty="(\d+)"', text))),
              {"0": 24, "1": 1, "2": 1})

    if itemdata is None:
        skipped.append("Plasma/LaserProperty.xml")
    else:
        count = struct.unpack_from("<i", itemdata, 4)[0]
        item_ids = {struct.unpack_from("<i", itemdata, 8 + i * 997)[0] for i in range(count)}
        bands = (12100000, 12200000, 12300000, 12400000)
        for filename, entries, resolving in (("PlasmaProperty.xml", 42, 42),
                                             ("LaserProperty.xml", 45, 39)):
            blob = decrypt(EXTRACTED / "ui" / filename)
            if blob is None:
                skipped.append(filename)
                continue
            gunindexes = [int(value) for value in
                          re.findall(r'gunindex="(\d+)"', blob.decode("utf-8", "replace"))]
            check(f"{filename} entries", len(gunindexes), entries)
            check(f"{filename} gunindexes resolving to a weapon",
                  sum(1 for g in gunindexes if any(band + g in item_ids for band in bands)),
                  resolving)

    # Character appearance/feel tables. Cooki and PushChar cover all 15
    # character types; only CharacterFitting stops at 13.
    for filename, sections in (("CharacterFitting.xml", 13),
                               ("CharacterToCooki.xml", 15),
                               ("CharacterToPushChar.xml", 15)):
        blob = decrypt(EXTRACTED / "ui" / "system" / filename)
        if blob is None:
            skipped.append(filename)
            continue
        text = blob.decode("utf-8", "replace")
        check(f"{filename} character sections",
              len(re.findall(r'<\w+ bEnable="\d"', text)), sections)
        if filename == "CharacterToCooki.xml":
            # handTexture runs hand1..hand15 in character order, which is an
            # independent witness that there are exactly 15 character types.
            check("CharacterToCooki.xml handTexture order",
                  [int(v) for v in re.findall(r'handTexture="hand(\d+)\.tga"', text)],
                  list(range(1, 16)))
        if filename == "CharacterToPushChar.xml":
            # damage_aim is per-character; the other three fields are uniform.
            check("CharacterToPushChar.xml damage_aim spread",
                  dict(Counter(re.findall(r'damage_aim="([^"]*)"', text))),
                  {"1f": 8, "0.6f": 1, "0.4f": 1, "0.3f": 1, "0.25f": 2, "0.2f": 2})

    # character/animations/ui: 15 type directories, each carrying every .PAD
    # animation the dump names. Two further files ship but are never named.
    animations = EXTRACTED / "character" / "animations" / "ui"
    if not animations.is_dir() or not DUMP.is_file():
        skipped.append("character/animations/ui")
    else:
        named = {match.lower() for match in
                 re.findall(r'L"([A-Za-z0-9_]+\.PAD)"',
                            DUMP.read_text(encoding="utf-8", errors="replace"))}
        check("native-named .PAD animations", len(named), 13)
        types = [f"type{index}" for index in range(1, 16)]
        present = [name for name in types if (animations / name).is_dir()]
        check("character animation type directories", len(present), 15)
        incomplete = sorted(name for name in present
                            if named - {path.name.lower()
                                        for path in (animations / name).iterdir()})
        check("type directories missing a named animation", incomplete, [])

    # CharacterFitting references 13 pendant folders; only one of them ships,
    # which is why the fitting-room subsystem stays UNRESOLVED.
    fitting = decrypt(EXTRACTED / "ui" / "system" / "CharacterFitting.xml")
    characters = EXTRACTED / "character"
    if fitting is None or not characters.is_dir():
        skipped.append("CharacterFitting pendant folders")
    else:
        wanted = set(re.findall(r'PendantFolderName="([^"]*)"',
                                fitting.decode("utf-8", "replace")))
        check("CharacterFitting pendant folders referenced", len(wanted), 13)
        shipped = {path.name for path in characters.rglob("Angry_*") if path.is_dir()}
        check("pendant folders that ship", sorted(shipped), ["Angry_Type13"])

    # RESOURCES.md 5d-20: the three skill tables. The band count is checked
    # because 5d-4 previously claimed five bands (-2..+2) when there are six,
    # and the axis order is checked because sub_7DBDA0 hard-codes it.
    levtable = decrypt(EXTRACTED / "ui" / "system" / "ItemAbilityLevTable.xml")
    if levtable is None:
        skipped.append("ItemAbilityLevTable.xml")
    else:
        text = levtable.decode("utf-8", "replace")
        bands = re.findall(r'<Lev start="([^"]+)" end="([^"]+)" id="(\d+)" '
                           r'lev_value="([^"]+)">', text)
        check("ItemAbilityLevTable bands", len(bands), 6)
        check("ItemAbilityLevTable band boundaries",
              [(start, end, ident, lev) for start, end, ident, lev in bands],
              [("-1000", "-10", "0", "-2"), ("-9", "-5", "1", "-1"),
               ("-4", "+4", "2", "0"), ("+5", "+9", "3", "+1"),
               ("+10", "+14", "4", "+2"), ("+15", "1000", "5", "+3")])
        # The lev_value=0 band is what makes "1-4 points do nothing" true.
        zero = re.search(r'lev_value="0">(.*?)</Lev>', text, re.S)
        check("ItemAbilityLevTable neutral band is all-none",
              sorted(set(re.findall(r'operation="(\w+)"', zero.group(1))))
              if zero else None,
              ["none"])
        check("ItemAbilityLevTable operations are the four named functors",
              sorted({op for op in re.findall(r'operation="(\w+)"', text)
                      if op != "none"}),
              ["MINUS", "PERCENT_MINUS", "PERCENT_PLUS", "PLUS"])

    colortable = decrypt(EXTRACTED / "ui" / "system"
                         / "ItemAbilityEffectColorTable.xml")
    if colortable is None:
        skipped.append("ItemAbilityEffectColorTable.xml")
    else:
        text = colortable.decode("utf-8", "replace")
        # The colour rows carry id 3/4/5, i.e. the *positive* lev_value bands of
        # the table above: same id space, which is why there are only three.
        check("ItemAbilityEffectColorTable Lev ids",
              re.findall(r'<Lev_\d+ id="(\d+)"', text), ["3", "4", "5"])
        check("ItemAbilityEffectColorTable has a Penalty row",
              "<Penalty" in text, True)

    nametable = decrypt(EXTRACTED / "ui" / "system"
                        / "ItemAbilityEffectNameTable.xml")
    if nametable is None:
        skipped.append("ItemAbilityEffectNameTable.xml")
    else:
        text = nametable.decode("utf-8", "replace")
        rows = re.findall(r'<Lev ThirdPersonView="([^"]*)" '
                          r'FirstPersonView="([^"]*)" LevValue="([^"]*)"', text)
        check("ItemAbilityEffectNameTable rows", len(rows), 15)
        check("ItemAbilityEffectNameTable third-person particle names",
              [third for third, _, _ in rows],
              [f"Ptcl_ItemEffect{index}" for index in range(1, 16)])
        # Every first-person field is empty: the effect is third-person only.
        check("ItemAbilityEffectNameTable first-person fields are all empty",
              sorted({first for _, first, _ in rows}), [""])

    # The 5x5 system-name matrix is symmetric and holds 15 distinct names; two
    # of them differ from the 2015-05 wiki, which is how the snapshot is dated.
    axes = ["speed", "agility", "hp", "defence", "hit"]
    namematrix = decrypt(EXTRACTED / "ui" / "ItemAbilityNameTAble.xml")
    if namematrix is None:
        skipped.append("ItemAbilityNameTAble.xml")
    else:
        text = namematrix.decode("utf-8", "replace")
        matrix: dict[str, dict[str, str]] = {}
        for row in axes:
            found = re.search(r"<" + row + r"\s+((?:\w+=\"[^\"]*\"\s*)+)/?>", text)
            if found is None:
                continue
            matrix[row] = {
                key: value.replace("&#xD;&#xA;", "").replace("&amp;", "&").strip()
                for key, value in re.findall(r'(\w+)="([^"]*)"', found.group(1))}
        check("ItemAbilityNameTAble rows", sorted(matrix), sorted(axes))
        if len(matrix) == len(axes):
            check("ItemAbilityNameTAble is symmetric",
                  [(row, column) for row in axes for column in axes
                   if matrix[row].get(column) != matrix[column].get(row)], [])
            check("ItemAbilityNameTAble distinct names",
                  len({matrix[row][column] for row in axes for column in axes}), 15)
            check("ItemAbilityNameTAble speed x hit (wiki says 速戦系)",
                  matrix["speed"]["hit"], "Hit&Run系")
            check("ItemAbilityNameTAble hp x hit (wiki says 応射系)",
                  matrix["hp"]["hit"], "対応射撃系")

    # RESOURCES.md 5d-21: map/gameobject.dat, the battlefield drop catalogue.
    # Parsed with the exact record layout of pmFile::possible_ctor_or_dtor_49,
    # so a stride or count drift shows up as a non-zero remainder.
    gameobject = EXTRACTED / "map" / "gameobject.dat"
    if not gameobject.is_file():
        skipped.append("gameobject.dat")
    else:
        blob = gameobject.read_bytes()
        version, count = struct.unpack_from("<fI", blob, 0)
        check("gameobject.dat version", version, 1.0)
        check("gameobject.dat count", count, 105)
        check("gameobject.dat consumes the file exactly",
              8 + count * 520, len(blob))

        def wide(offset: int) -> str:
            return blob[offset:offset + 128].decode("utf-16le", "replace").split("\0")[0]

        records = []
        for index in range(count):
            base = 8 + 520 * index
            ident, = struct.unpack_from("<I", blob, base)
            records.append((ident, tuple(blob[base + 4:base + 8]),
                            [wide(base + 8 + 128 * part) for part in range(4)]))

        # The id really is the four classification bytes packed together.
        check("gameobject id equals its classification bytes",
              [ident for ident, tag, _ in records
               if tuple(struct.pack("<I", ident))[::-1] != tag], [])
        check("gameobject third string is always empty",
              sorted({strings[2] for _, _, strings in records}), [""])
        check("gameobject categories",
              sorted(Counter(tag[0] for _, tag, _ in records).items()),
              [(1, 2), (2, 97), (3, 2), (4, 2), (5, 2)])

        # The 7 families x 3 levels matrix the wiki also describes.
        drops = [(tag, strings) for _, tag, strings in records
                 if strings[3].startswith("D_Item") and len(strings[3]) == 10]
        check("D_Item four-digit records", len(drops), 88)
        check("D_Item suffix digits agree with the id bytes",
              [strings[3] for tag, strings in drops
               if not (tag[2] == int(strings[3][8]) * 10
                       and tag[3] == (int(strings[3][6]) * 10 + int(strings[3][7])
                                      if int(strings[3][6]) else 0)
                       and strings[3][9] == "1")], [])
        check("D_Item families", sorted({int(s[3][6]) for _, s in drops}),
              [0, 1, 2, 3, 4, 5, 6, 7])
        check("D_Item levels (families 1-7)",
              sorted({int(s[3][7]) for _, s in drops if int(s[3][6])}), [1, 2, 3])
        textures: dict[str, list[int]] = {}
        for _, strings in drops:
            textures.setdefault(strings[1], [])
            if int(strings[3][6]) not in textures[strings[1]]:
                textures[strings[1]].append(int(strings[3][6]))
        textures = {key: sorted(value) for key, value in textures.items()}
        check("D_Item texture tiers", textures,
              {"models/item/DropItem_001": [0, 1, 2, 3],
               "models/item/DropItem_002": [4, 5, 6],
               "models/item/DropItem_003": [7]})
        check("Q_Item records",
              sorted(s[3] for _, s in
                     [(tag, strings) for _, tag, strings in records
                      if strings[3].startswith("Q_Item")]),
              [f"Q_Item000{index}" for index in range(1, 6)])

    # RESOURCES.md 5d-21: the eight QuestTerm==19 quests are exactly the ones
    # carrying a non-zero HonorMedalPosition.
    quests = decrypt(EXTRACTED / "ui" / "cfg" / "Quest.pat")
    if quests is None:
        skipped.append("Quest.pat")
    else:
        lines = quests.decode("cp932").split("\r\n")
        header = [name.strip() for name in lines[1].split(",")]
        rows = [row for row in csv.reader(lines[2:]) if len(row) == len(header)]
        check("Quest.pat rows", len(rows), 844)
        check("Quest.pat declared total", lines[0].split(",")[0], "844")
        term = header.index("QuestTerm")
        medal = header.index("HonorMedalPosition")
        star = {row[0].strip() for row in rows if row[term].strip() == "19"}
        marked = {row[0].strip() for row in rows if row[medal].strip() != "0"}
        check("QuestTerm==19 quests", sorted(star),
              [str(index) for index in range(40001, 40009)])
        check("QuestTerm==19 is exactly the HonorMedalPosition set",
              sorted(marked), sorted(star))

    # Every datarevision.txt must agree: Extracted/ is one coherent snapshot.
    revisions = {path.read_text(encoding="utf-8", errors="replace").strip()
                 for path in EXTRACTED.rglob("datarevision.txt")}
    if not revisions:
        skipped.append("datarevision.txt")
    else:
        check("datarevision.txt values agree", sorted(revisions), ["811034967"])

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
