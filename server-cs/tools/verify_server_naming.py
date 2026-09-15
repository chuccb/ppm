#!/usr/bin/env python3
"""Validate source-proven PaperMan.Server naming boundaries.

Run from any working directory:
    python3 server-cs/tools/verify_server_naming.py

This complements verify_server_layout.py. That tool verifies canonical packet
catalog names and handler paths. This tool verifies the room mode vocabulary
where native type names and main:Extracted UI labels intentionally differ.

It is a static source/resource check, not a replacement for a .NET build or a
captured client/server interoperability test.
"""
from __future__ import annotations

import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
NATIVE_SOURCE = ROOT / "PaperMan.exe.c"
ROOM_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.Server" / "State" / "Room.cs"
ROOM_SHARED_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.Server" / "Handlers" / "Room" / "Handlers.Room.Shared.cs"
LOBBY_ROOM_LIST_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.Server" / "Handlers" / "Lobby" / "Handlers.GL_GAMEROOMINFO.cs"

# These are the suffixes selected by sub_53FBB0's switch. They deliberately
# follow the native Cy*ModeLobbyUI classes, rather than map_StartIndex.xml's
# UI-facing modeName values (for example TeamMatch vs TeamDeath).
NATIVE_GAME_MODES = {
    "TeamMatch": 0,
    "IndividualSurvival": 1,
    "DefuseBomb": 2,
    "TeamSurvival": 3,
    "Steal": 4,
    "Practice": 5,
    "Tutorial": 6,
    "ChattingRoom": 7,
    "PulpnRoll": 8,
    "GunShooting": 9,
    "Occupy": 10,
    "AIMulti": 11,
    "TeamSoccer": 12,
    "OccupyRenewal": 13,
    "WeaponTest": 15,
}

# The maplist.pat bit relationships are source/resource cross-checked in
# docs/RESOURCES.md §4b. Keep the code table tied to the same mode names.
MODE_INDEX_MAP_BITS = {
    0: 2,
    1: 0,
    2: 3,
    3: 1,
    4: 4,
    6: 6,
    8: 9,
    9: 10,
    10: 12,
    11: 13,
    12: 14,
    13: 15,
    15: 11,
}

# This is the exact predicate in sub_438990, expressed as modeIndex values.
NATIVE_TWO_TEAM_MODE_NAMES = frozenset({
    "TeamMatch",
    "DefuseBomb",
    "TeamSurvival",
    "Steal",
    "PulpnRoll",
    "Occupy",
    "AIMulti",
    "TeamSoccer",
    "OccupyRenewal",
})


def fail(message: str) -> None:
    raise SystemExit(f"naming verification failed: {message}")


def git_show_main(path: str) -> str:
    """Read the current main branch's Extracted evidence without checkout."""
    for ref in ("origin/main", "main"):
        result = subprocess.run(
            ["git", "show", f"{ref}:{path}"],
            cwd=ROOT,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        if result.returncode == 0:
            return result.stdout
    fail(f"cannot read main: {path}; fetch main with its Extracted resource tree")


def native_factory_modes() -> dict[str, int]:
    source = NATIVE_SOURCE.read_text(encoding="utf-8", errors="replace")
    marker = "//----- (0053FBB0)"
    try:
        start = source.index(marker)
        end = source.index("//-----", start + len(marker))
    except ValueError:
        fail("could not locate the native sub_53FBB0 mode factory")

    factory = source[start:end]
    matches = re.finditer(
        r"case (?P<label>\d+|0x[0-9A-F]+)u:\s*"
        r"(?P<body>.*?)(?=\n      case |\n      default:)",
        factory,
        re.DOTALL,
    )
    result: dict[str, int] = {}
    for match in matches:
        name_match = re.search(r"CyGameModes::Cy(?P<name>\w+)ModeLobbyUI", match["body"])
        if name_match is not None:
            result[name_match["name"]] = int(match["label"], 0)
    return result


def source_enum_modes() -> dict[str, int]:
    text = ROOM_SOURCE.read_text(encoding="utf-8")
    values = {
        match["name"]: int(match["value"])
        for match in re.finditer(
            r"^    (?P<name>\w+) = (?P<value>\d+),(?:\s*//.*)?$",
            text,
            re.MULTILINE,
        )
    }
    if not values:
        fail("no GameMode enum values found")
    return values


def source_mode_map(path: Path, marker: str, enum_values: dict[str, int]) -> dict[int, int]:
    text = path.read_text(encoding="utf-8")
    try:
        start = text.index(marker)
        body = text[start:text.index("}.ToFrozenDictionary();", start)]
    except ValueError:
        fail(f"{path}: cannot read {marker} table")

    result = {
        enum_values[match["name"]]: int(match["value"])
        for match in re.finditer(
            r"\[\(byte\)GameMode\.(?P<name>\w+)\] = (?P<value>\d+),",
            body,
        )
    }
    return result


def mode_names_in_two_team_predicate(path: Path) -> frozenset[str]:
    text = path.read_text(encoding="utf-8")
    marker = "private static bool IsNativeTwoTeamMode"
    try:
        body = text[text.index(marker):text.index(";", text.index(marker))]
    except ValueError:
        fail(f"{path}: missing IsNativeTwoTeamMode predicate")
    return frozenset(re.findall(r"\(byte\)GameMode\.(\w+)", body))


def main() -> None:
    enum_values = source_enum_modes()
    if enum_values != NATIVE_GAME_MODES:
        fail(f"GameMode does not match expected native names/values: {enum_values}")

    native_modes = native_factory_modes()
    if native_modes != NATIVE_GAME_MODES:
        fail(f"sub_53FBB0 mode factory differs: {native_modes}")

    extracted_xml = ET.fromstring(git_show_main("Extracted/ui/system/map_StartIndex.xml"))
    resource_default_maps = {
        int(node.attrib["modeIndex"]): int(node.attrib["modeStartIndex"])
        for node in extracted_xml.findall("INDEX")
    }
    if source_mode_map(ROOM_SHARED_SOURCE, "ModeIndexDefaultMap", enum_values) != resource_default_maps:
        fail("ModeIndexDefaultMap diverges from main:Extracted/ui/system/map_StartIndex.xml")
    if source_mode_map(ROOM_SHARED_SOURCE, "ModeIndexMapBit", enum_values) != MODE_INDEX_MAP_BITS:
        fail("ModeIndexMapBit diverges from the documented source/resource bit mapping")

    for source in (ROOM_SHARED_SOURCE, LOBBY_ROOM_LIST_SOURCE):
        if mode_names_in_two_team_predicate(source) != NATIVE_TWO_TEAM_MODE_NAMES:
            fail(f"{source}: IsNativeTwoTeamMode diverges from native sub_438990")

    server_source = (ROOT / "server-cs" / "src" / "PaperMan.Server")
    stale = [
        "room.Rule",
        "request.Rule",
        "GameMode.TeamDeath",
        "GameMode.FreeForAll",
        "GameMode.Chatting",
        "GameMode.PulpNRoll",
        "GameMode.AiMulti",
        "GameMode.Soccer",
    ]
    for token in stale:
        if any(token in path.read_text(encoding="utf-8") for path in server_source.rglob("*.cs")):
            fail(f"stale native/resource naming alias remains: {token}")

    print(
        "server naming OK: "
        f"{len(enum_values)} native GameMode values; "
        f"{len(resource_default_maps)} Extracted default maps; "
        f"{len(MODE_INDEX_MAP_BITS)} modeIndex→maplist-bit entries"
    )


if __name__ == "__main__":
    main()
