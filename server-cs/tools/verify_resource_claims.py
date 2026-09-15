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
import subprocess
import sys
from collections import Counter
from xml.etree import ElementTree
from pathlib import Path, PurePosixPath

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

    # RESOURCES.md 5d-22: tutorial_contents.xml is the only tutorial file that
    # carries level data, and its 9 missions pair 1:1 with the result sprites
    # in tutorial_image.xml -- two independently maintained files agreeing.
    contents = EXTRACTED / "ui" / "tutorial_contents.xml"
    images = EXTRACTED / "ui" / "tutorial_image.xml"
    if not contents.is_file() or not images.is_file():
        skipped.append("ui/tutorial_contents.xml")
    else:
        root = ElementTree.parse(contents).getroot()
        check("tutorial_contents root", root.tag, "TUTORIALDEFINE")
        check("tutorial missions", [node.tag for node in root],
              ["move_1", "move_2", "move_3", "move_4",
               "attack_1", "attack_2", "attack_3", "attack_4", "attack_5"])
        check("tutorial mission indices",
              [node.get("index") for node in root],
              ["0", "1", "2", "3", "4", "14", "24", "34", "44"])
        # weapon runs 0..3 like the Tutorial_Data.xml segment selector, plus a
        # fifth *lesson* (sniper) that is NOT a fifth weapon segment.
        check("tutorial weapon values",
              [node.find("property").get("weapon") for node in root],
              ["0", "0", "0", "0", "0", "1", "2", "3", "4"])
        check("tutorial time limit is uniform",
              sorted({node.find("property").get("time") for node in root}),
              ["180000"])
        check("tutorial success/fail are plain constants",
              sorted({(node.find("property").get("success"),
                       node.find("property").get("fail")) for node in root}),
              [("SUCCESS", "FAIL")])
        sprites = set(re.findall(r'name="(FINISH_[A-Z_]+)"',
                                 images.read_text("cp932", "replace")))
        check("tutorial result sprites pair 1:1 with the 9 missions",
              sorted(sprites & {
                  "FINISH_DEF_MOVE", "FINISH_ONLYWAY_MOVE", "FINISH_DUCK_MOVE",
                  "FINISH_JUMP_MOVE", "FINISH_MAIN_WEAPON", "FINISH_SUB_WEAPON",
                  "FINISH_NEAR_WEAPON", "FINISH_BOMB_WEAPON",
                  "FINISH_SNIPER_WEAPON"}),
              sorted(["FINISH_DEF_MOVE", "FINISH_ONLYWAY_MOVE",
                      "FINISH_DUCK_MOVE", "FINISH_JUMP_MOVE",
                      "FINISH_MAIN_WEAPON", "FINISH_SUB_WEAPON",
                      "FINISH_NEAR_WEAPON", "FINISH_BOMB_WEAPON",
                      "FINISH_SNIPER_WEAPON"]))

    # RESOURCES.md 5d-23: the durability falloff curve. The wiki's "performance
    # drops from 19%" is really "from 20%", because the accessor index is a
    # truncated (1 - ratio) * 10 -- so the all-zero prefix is the real claim.
    durable = EXTRACTED / "ui" / "durable_ability.xml"
    if not durable.is_file():
        skipped.append("ui/durable_ability.xml")
    else:
        curve = ElementTree.parse(durable).getroot()
        check("durable_ability axis order", [node.tag for node in curve],
              ["aiming", "recoil", "shotvelocity", "reload", "power", "distance"])
        columns = [f"per{step}" for step in range(100, 0, -10)]
        rows = {node.tag: [float(node.get(name)) for name in columns]
                for node in curve}
        check("durable_ability columns per axis",
              sorted({len(values) for values in rows.values()}), [10])
        # No penalty at all above 20% remaining. Pin the zero prefix by its
        # LENGTH too, so shortening the window cannot silently pass.
        check("durable_ability zero-penalty prefix length",
              sorted({next((index for index, value in enumerate(values) if value),
                           len(values)) for values in rows.values()
                      if any(values)}), [8])
        check("durable_ability has no penalty above 20% durability",
              {axis: values[:8] for axis, values in rows.items()
               if any(values[:8])}, {})
        check("durable_ability per20 column",
              {axis: values[8] for axis, values in rows.items()},
              {"aiming": 0.15, "recoil": 0.1, "shotvelocity": 0.15,
               "reload": 0.0, "power": 0.3, "distance": 0.3})
        check("durable_ability per10 column",
              {axis: values[9] for axis, values in rows.items()},
              {"aiming": 0.9, "recoil": 0.12, "shotvelocity": 0.5,
               "reload": 0.0, "power": 0.9, "distance": 0.5})
        # reload is the one axis that never degrades.
        check("durable_ability reload never degrades",
              [axis for axis, values in rows.items() if not any(values)],
              ["reload"])

    # RESOURCES.md 5d-24: commonProperty rows are keyed by position, and the
    # tempting UIWeaponEffectIcon mapping is recorded as disproved -- index 3
    # and 4 would make SPEED_UP slower and SPEED_DOWN faster than the baseline.
    common = EXTRACTED / "ui" / "commonProperty.xml"
    if not common.is_file():
        skipped.append("ui/commonProperty.xml")
    else:
        effects = ElementTree.fromstring(common.read_text("cp932", "replace"))
        check("commonProperty rows", len(effects), 17)
        check("commonProperty baseline row", effects[0].tag, "NONE")
        baseline = int(effects[0].get("speed"))
        check("commonProperty baseline speed", baseline, 1000)
        check("commonProperty index 3 is slower than baseline, not faster",
              int(effects[3].get("speed")) < baseline, True)
        check("commonProperty index 4 is faster than baseline, not slower",
              int(effects[4].get("speed")) > baseline, True)

    # RESOURCES.md 5d-25: the PvE difficulty table. The wiki's "1 to 4 players,
    # three difficulties, one map, four waves" is all checkable here, and the
    # shield column is a live data/code mismatch worth pinning.
    ai_level = EXTRACTED / "ui" / "system" / "AI" / "AiMultiLevel.xml"
    if not ai_level.is_file():
        skipped.append("ui/system/AI/AiMultiLevel.xml")
    else:
        levels = ElementTree.fromstring(
            ai_level.read_bytes().decode("utf-8-sig"))
        check("AiMultiLevel root", levels.tag, "AI_LEVEL")
        # One map only, and it is the AIMulti map 95 from maplist.
        check("AiMultiLevel modes", [node.get("index") for node in levels], ["95"])
        mode = levels[0]
        check("AiMultiLevel waves",
              [node.get("wave_index") for node in mode], ["1", "2", "3", "4"])
        check("AiMultiLevel difficulties per wave",
              sorted({tuple(tier.tag for tier in wave) for wave in mode}),
              [("WAVE_LEVEL_EASY", "WAVE_LEVEL_NORMAL", "WAVE_LEVEL_HARD")])
        check("AiMultiLevel rows per difficulty",
              sorted({len(tier) for wave in mode for tier in wave}), [8])

        axes = ["subtraction_rate", "bothp_rate", "siege_dmg_rate",
                "firstdelay_rate", "shotdelay_rate", "movespeed_rate"]

        def row(tier, number: int) -> list[int]:
            # Missing attributes must surface as a failed check, not a crash.
            return [int(entry.get(name) or -1) for name in axes
                    for entry in tier if entry.get("number") == str(number)]

        # Rows 5..8 are a uniform sentinel: the mode really is 1..4 players.
        check("AiMultiLevel rows 5-8 are a uniform sentinel",
              sorted({tuple(row(tier, number)) for wave in mode
                      for tier in wave for number in range(5, 9)}),
              [(0, 0, 0, 1, 1, 1)])

        # Enemies get weaker as the party grows, in every wave and tier.
        check("AiMultiLevel subtraction_rate falls as players are added",
              [(wave.get("wave_index"), tier.tag)
               for wave in mode for tier in wave
               if not all(
                   row(tier, number)[0] >= row(tier, number + 1)[0]
                   for number in range(1, 4))], [])

        # EASY is never harder than NORMAL, which is never harder than HARD.
        ordered = [name for name in axes if name != "siege_dmg_rate"]
        check("AiMultiLevel difficulty ordering holds on every axis",
              [(wave.get("wave_index"), number, name)
               for wave in mode for number in range(1, 5)
               for index, name in enumerate(axes) if name in ordered
               and not (row(wave[0], number)[index]
                        >= row(wave[1], number)[index]
                        >= row(wave[2], number)[index])], [])

        # The live mismatch: the file says siege_dmg_rate, the exe reads
        # shilddamage_rate, so this column never reaches the engine.
        check("AiMultiLevel ships siege_dmg_rate on every row",
              sorted({name for wave in mode for tier in wave
                      for entry in tier for name in entry.attrib
                      if name.endswith("dmg_rate") or name.endswith("damage_rate")}),
              ["siege_dmg_rate"])

    # RESOURCES.md 5d-26: BotEnemy.xml vs BotEnemy_easy.xml is a same-shape
    # controlled pair, so "what does easy actually change" is checkable.
    bots = EXTRACTED / "ui" / "system" / "AI" / "BotEnemy.xml"
    bots_easy = EXTRACTED / "ui" / "system" / "AI" / "BotEnemy_easy.xml"
    if not bots.is_file() or not bots_easy.is_file():
        skipped.append("ui/system/AI/BotEnemy.xml")
    else:
        def bot_rows(path: Path) -> dict[str, dict[str, str]]:
            root = ElementTree.fromstring(path.read_bytes().decode("utf-8-sig"))
            return {node.get("bot_type_index"): node.attrib
                    for node in root if node.tag == "TYPE"}

        normal, easy = bot_rows(bots), bot_rows(bots_easy)
        check("BotEnemy rows", len(normal), 35)
        check("BotEnemy indices are contiguous",
              sorted(int(key) for key in normal), list(range(35)))
        check("BotEnemy_easy covers the same indices",
              sorted(easy) == sorted(normal), True)

        # Only these attributes differ; the rest are identical in all 35 rows.
        differing = sorted({name for key, row in normal.items()
                            for name in row if row[name] != easy[key].get(name)})
        check("BotEnemy easy/normal differing attributes", differing,
              ["bonus_value", "bot_hp", "bot_type", "game_point", "game_score",
               "instant_pg", "move_speed", "scale"])
        # Attack stats are deliberately untouched by the difficulty split.
        check("BotEnemy difficulty leaves attack stats alone",
              [name for name in ("siege_dmg", "first_delay", "shot_delay")
               if name in differing], [])
        # Easy bots are never tougher or faster: zero counter-examples.
        for name in ("bot_hp", "move_speed"):
            check(f"BotEnemy_easy never raises {name}",
                  [key for key, row in normal.items()
                   if int(easy[key][name]) > int(row[name])], [])
        # scale is present in the file but never read by the engine.
        check("BotEnemy still ships the dead scale attribute",
              [key for source in (normal, easy) for key, row in source.items()
               if "scale" not in row], [])

    # RESOURCES.md 5d-27: every outbound endpoint is data-driven, and
    # ui/URLList.xml is an older copy stranded at a path nothing loads.
    live_urls = EXTRACTED / "ui" / "system" / "URLList_01.xml"
    stale_urls = EXTRACTED / "ui" / "URLList.xml"
    if not live_urls.is_file():
        skipped.append("ui/system/URLList_01.xml")
    else:
        def url_entries(path: Path) -> dict[str, str]:
            root = ElementTree.fromstring(path.read_bytes().decode("utf-8-sig"))
            return {node.get("index"): node.get("url") for node in root}

        live = url_entries(live_urls)
        check("URLList_01 index keys", sorted(live, key=int),
              ["1", "2", "3", "4", "5", "6"])
        check("URLList_01 entries are all enabled",
              sorted({node.get("disable") for node
                      in ElementTree.fromstring(
                          live_urls.read_bytes().decode("utf-8-sig"))}), ["0"])
        # The two templated endpoints, including the token-bearing sign-on.
        check("URLList_01 ranking endpoint", live["2"],
              "http://157.7.172.71:5351/content/mainContent.asp?key=%s")
        check("URLList_01 billing endpoint", live["4"],
              "https://bill.paperman.jp/login.ashx?userid=%s&token=%s")
        if stale_urls.is_file():
            stale = url_entries(stale_urls)
            check("ui/URLList.xml is the shorter, older copy",
                  (len(stale), sorted(stale, key=int)),
                  (4, ["1", "2", "3", "4"]))
            check("ui/URLList.xml still carries the superseded ranking host",
                  stale["2"],
                  "http://202.213.230.237:5351/content/mainContent.asp?key=%s")

    # RESOURCES.md 5d-4b: the reward table's mode_index values are MAP ids for
    # Tutorial and IndividualSurvival -- not the PvE map 95, despite the
    # AiMulti filename. periodType ships on every row but is never read.
    payouts = EXTRACTED / "ui" / "system" / "AI" / "AiMultiCompensation.xml"
    if not payouts.is_file():
        skipped.append("ui/system/AI/AiMultiCompensation.xml")
    else:
        comp = ElementTree.fromstring(payouts.read_bytes().decode("utf-8-sig"))
        check("AiMultiCompensation modes",
              sorted(node.get("mode_index") for node in comp), ["102", "104"])
        check("AiMultiCompensation does not cover the PvE map",
              "95" in {node.get("mode_index") for node in comp}, False)
        check("AiMultiCompensation difficulty tiers",
              sorted({tuple(tier.tag for tier in mode) for mode in comp}),
              [("MODE_LEVEL_EASY", "MODE_LEVEL_NORMAL", "MODE_LEVEL_HARD")])
        # Every tier awards the same four items, ranked 1..4 by score.
        check("AiMultiCompensation payouts are identical across tiers",
              sorted({tuple((row.get("itemnumber"), row.get("level"))
                            for row in tier)
                      for mode in comp for tier in mode}),
              [(("15301005", "1"), ("15301004", "2"),
                ("15200044", "3"), ("15200045", "4"))])
        check("AiMultiCompensation still ships the dead periodType",
              [row.get("level") for mode in comp for tier in mode
               for row in tier if "periodType" not in row.attrib], [])

    # RESOURCES.md 5d-3 + 5d-28: native branches to two score tables, but in
    # this revision they are byte-identical, and both are plaintext.
    score = EXTRACTED / "ui" / "system" / "AI" / "ScoreRatio.xml"
    score_ai = EXTRACTED / "ui" / "system" / "AI" / "AiMultiScoreRatio.xml"
    if not score.is_file() or not score_ai.is_file():
        skipped.append("ui/system/AI/ScoreRatio.xml")
    else:
        plain, multi = score.read_bytes(), score_ai.read_bytes()
        check("ScoreRatio tables are byte-identical", plain == multi, True)
        check("ScoreRatio is plaintext, not pmFile-encrypted",
              plain[:12], b"<SCORERATIO>")
        ratios = ElementTree.fromstring(plain.decode("utf-8-sig"))
        chains: dict[str, list[str]] = {}
        for node in ratios:
            if node.tag == "Kill_Chain":
                chains.setdefault(node.get("index"), []).append(node.get("ratio"))
        check("ScoreRatio chain families", sorted(chains), ["0", "1", "2", "3"])
        check("ScoreRatio tiers per family",
              [len(chains[key]) for key in sorted(chains)], [4, 4, 3, 11])
        single = next(node for node in ratios if node.tag == "Kill_1Time")
        check("ScoreRatio single-kill multipliers",
              [single.get(name) for name in
               ("HeadShotRatio", "HeartShotRatio",
                "CriticalShotRatio", "AirComboRatio")],
              ["2", "1.5", "1.8", "2"])

    # 5d-28: the encryption census. .pat is always encrypted; only three xml
    # files are. First byte '<' or a BOM means plaintext.
    encrypted = []
    for folder in ("system", "cfg"):
        base = EXTRACTED / "ui" / folder
        if not base.is_dir():
            continue
        for path in sorted(base.rglob("*")):
            if path.is_file() and path.suffix.lower() in (".xml", ".pat"):
                if path.read_bytes()[:1] not in (b"<", b"\xef"):
                    encrypted.append(path.name)
    if not encrypted:
        skipped.append("ui/{system,cfg} encryption census")
    else:
        # Every .pat present must be encrypted: none may be missing from the
        # encrypted set. Comparing against the on-disk listing, not itself.
        all_pat = sorted(path.name for folder in ("system", "cfg")
                         for path in (EXTRACTED / "ui" / folder).rglob("*.pat")
                         if (EXTRACTED / "ui" / folder).is_dir())
        check("no .pat ships in plaintext",
              [name for name in all_pat if name not in encrypted], [])
        check("encrypted xml files are the known three",
              sorted(name for name in encrypted if name.endswith(".xml")),
              sorted(name for name in
                     ("ItemAbilityLevTable.xml", "netcafe_contents.xml",
                      "voice_customize_contents.xml")
                     if (EXTRACTED / "ui" / "system" / name).is_file()))

    # RESOURCES.md 5d-11b: move_speed belongs to the stock part group alone,
    # and it is a flag (1 or 3), not the magnitude -- the posture deltas are.
    parts = EXTRACTED / "ui" / "cfg" / "partsability.pat"
    if not parts.is_file():
        skipped.append("ui/cfg/partsability.pat")
    else:
        blob = decrypt(parts)
        lines = blob.decode("cp932", "replace").split("\r\n")
        columns = [name.strip() for name in lines[1].split(",")]
        table = [row for row in csv.reader(lines[2:]) if len(row) == len(columns)]
        check("partsability rows", len(table), 413)
        speed = columns.index("move_speed")
        posture = [columns.index(name) for name in
                   ("miJump", "miSit", "miStand", "miWalk", "miRun")]

        def group(row: list[str]) -> int:
            return int(row[0]) // 10000 * 10000

        STOCK = 15250000
        check("move_speed is set only in the stock part group",
              sorted({group(row) for row in table
                      if row[speed].strip() not in ("", "0")}), [STOCK])
        check("every stock row sets move_speed",
              [row[0] for row in table
               if group(row) == STOCK and row[speed].strip() in ("", "0")], [])
        check("move_speed is a two-valued flag",
              sorted({row[speed].strip() for row in table
                      if group(row) == STOCK}), ["1", "3"])
        # Posture deltas span several groups, which is why move_speed cannot
        # be the magnitude.
        check("posture deltas span more than the stock group",
              sorted({group(row) for row in table
                      if any(row[index].strip() not in ("", "0")
                             for index in posture)}),
              [15220000, 15240000, 15250000, 15270000, 15280000])

    # RESOURCES.md 5d-7b: convars.pat ships the tunables but no debug toggle,
    # and notably not um_gr_maxspeed, so that one always runs at its default.
    convars = ROOT / "Extracted" / "convars.pat"
    if not convars.is_file():
        skipped.append("convars.pat")
    else:
        settings = decrypt(convars).decode("cp932", "replace").replace("\r", "")
        for name in ("um_gr_accel", "um_gr_decel", "gun_caliber",
                     "movespeed", "defence", "jumpheight"):
            check(f"convars.pat ships {name}", name in settings, True)
        for name in ("um_gr_maxspeed", "r_showfps", "r_noui", "d_netrun"):
            check(f"convars.pat omits {name}", name in settings, False)
        # The per-character movespeed band whose mode equals that 90.0 default.
        speeds = sorted({line.split()[2] for line in settings.split("\n")
                         if "movespeed" in line and len(line.split()) > 2})
        check("per-character movespeed values", speeds,
              ["85", "86", "87", "88", "90"])

    # RESOURCES.md 5c-2b: the 153051xx band is voice merchandise, encoded as
    # set-in-the-tens and character-ordinal-in-the-units, and the paper-slot
    # ranges from 5c-2 are checked semantically against the item names.
    itemdata = EXTRACTED / "ui" / "cfg" / "itemdata.pat"
    if not itemdata.is_file():
        skipped.append("ui/cfg/itemdata.pat")
    else:
        blob = decrypt(itemdata)
        total = struct.unpack_from("<I", blob, 4)[0]
        catalog: dict[int, str] = {}
        for index in range(total):
            base = 8 + 997 * index
            ident = struct.unpack_from("<I", blob, base)[0]
            catalog[ident] = blob[base + 20:base + 140].decode(
                "utf-16le", "replace").split("\0")[0]
        check("itemdata self-check", 8 + total * 997, len(blob))

        voices = {ident: name for ident, name in catalog.items()
                  if 15305101 <= ident <= 15305300}
        check("voice merchandise records", len(voices), 85)
        check("every record in the band is a named voice item",
              [ident for ident, name in voices.items()
               if not re.search(r"\(Voice [^)]+\)", name)], [])
        # Units digit is the character ordinal: one name per digit, no clashes.
        by_digit: dict[int, set[str]] = {}
        for ident, name in voices.items():
            by_digit.setdefault(ident % 10, set()).add(name.split("(")[0])
        check("units digit maps to exactly one character each",
              sorted(digit for digit, names in by_digit.items()
                     if len(names) != 1), [])
        check("voice character ordinals",
              [sorted(by_digit[digit])[0] for digit in sorted(by_digit)],
              ["ハヤテ", "ティナ", "ミリィ", "サイラス", "ドッドン",
               "ガイ", "テリシア", "アルル", "ヴァン"])

        def band(low: int, high: int) -> list[int]:
            return sorted(i for i in catalog if low <= i <= high)

        check("CROSSHAIR slot population", len(band(15305001, 15305100)), 43)
        check("NAME slot population", len(band(15304001, 15305000)), 421)
        check("MASTER and ABILITY slots are empty in this revision",
              [len(band(15305301, 15305400)), len(band(15305401, 15305600))],
              [0, 0])
        check("BOOST_EXP items", [catalog[i] for i in band(15305601, 15305700)],
              ["EXP +10%UP", "EXP +30%UP", "EXP +50%UP"])
        check("BOOST_PG items", [catalog[i] for i in band(15305701, 15305800)],
              ["PG +10%UP", "PG +30%UP", "PG +50%UP"])
        check("extra-ability slot population",
              len(band(15305801, 15306000)), 13)

    # RESOURCES.md 5c-2c: the title band. Fourteen character chains of seven
    # contiguous ids each, matching the wiki's unlock ladder, plus the colour
    # markup and its two malformed records.
    if itemdata.is_file():
        titles = {ident: name for ident, name in catalog.items()
                  if 15304001 <= ident <= 15305000}
        check("title band records", len(titles), 421)

        def plain(name: str) -> str:
            return re.sub(r"[\u2019']#[0-9A-Fa-f]{6}\u2019", "", name).strip()

        # Colour markup: well-formed records carry two U+2019 delimiters.
        malformed = sorted(ident for ident, name in titles.items()
                           if "'#" in name)
        check("titles with an ASCII apostrophe delimiter", malformed,
              [15304114, 15304166])
        check("well-formed colour-tagged titles",
              sum(1 for name in titles.values() if name.count("\u2019") == 2),
              413)

        # The fourteen chains, each head..head+6 ending in <character>ラバー.
        lovers = sorted(ident for ident, name in titles.items()
                        if plain(name).endswith("ラバー"))
        # 15 names end in ラバー, but ミクラバー (15304225) is a standalone
        # collaboration title with no chain behind it; 14 are chain tails.
        check("titles ending in ラバー", len(lovers), 15)
        check("the non-chain ラバー is the Miku collaboration",
              [ident for ident in lovers if ident < 15304594], [15304225])
        roster = ["ハヤテ", "ティナ", "ミリィ", "サイラス", "ドッドン", "ガイ",
                  "テリシア", "アルル", "ヴァン", "フッド", "リカ", "レム",
                  "エリス", "ルコット"]
        chains = [ident for ident in lovers if ident >= 15304594]
        check("chain ラバー names follow the character roster",
              [plain(titles[ident])[:-3] for ident in chains], roster)
        check("the first twelve chains are contiguous seven-id blocks",
              [ident for index, ident in enumerate(chains[:12])
               if ident != 15304600 + 7 * index], [])
        # Lucy, character 15, has no chain -- the third file to omit her.
        check("no title chain for ルーシー",
              [ident for ident in lovers
               if plain(titles[ident]).startswith("ルーシー")], [])
        # The wiki's odd second step in the Cyrus chain is real.
        check("Cyrus chain step two breaks the naming pattern",
              plain(titles[15304616]), "包帯I")
        check("Cyrus chain surrounds it normally",
              [plain(titles[i]) for i in (15304615, 15304617, 15304621)],
              ["ストレンジャー", "ストレンジャーII", "サイラスラバー"])

    # RESOURCES.md 5c-2d: every character voice pack is a complete 3x9 radio
    # grid, matching the wiki's Z/X/V key table with no gaps.
    # The wav payloads are far too large to vendor, so the grid is re-derived
    # from the main-branch tree listing rather than from local files.
    sound_root = ROOT / "Extracted" / "sound"
    radio_names: list[str] = []
    if sound_root.is_dir():
        radio_names = [str(path) for path in sound_root.rglob("Radio_Message/*/*.wav")]
    if not radio_names:
        listing = subprocess.run(
            ["git", "ls-tree", "-r", "main", "--name-only"],
            cwd=ROOT, capture_output=True, text=True)
        radio_names = [line for line in listing.stdout.split("\n")
                       if "Radio_Message/" in line and line.endswith(".wav")]
    if not radio_names:
        skipped.append("sound/*/Radio_Message")
    else:
        radio_files = [PurePosixPath(name.replace("\\", "/"))
                       for name in radio_names]
        grid: dict[tuple[str, str], dict[str, set[int]]] = {}
        for path in radio_files:
            pack = path.parents[2].parent.name
            character = path.parents[2].name.lower()
            matched = re.match(r"(.+)_(\d+)\.wav$", path.name, re.I)
            category = matched.group(1).lower().rsplit("_", 1)[-1]
            grid.setdefault((pack, character), {}).setdefault(
                category, set()).add(int(matched.group(2)))
        check("radio wav files", len(radio_files), 1044)
        check("character voice packs", len(grid), 38)
        expected = {"command": set(range(1, 10)),
                    "tactics": set(range(1, 10)),
                    "information": set(range(1, 10))}
        check("every pack is a complete 3x9 grid",
              sorted(key for key, cats in grid.items() if cats != expected), [])

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
