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
AI_HANDLERS = ROOT / "server-cs" / "src" / "PaperMan.Server" / "Handlers" / "AI"
LOGIN_HANDLERS = ROOT / "server-cs" / "src" / "PaperMan.Server" / "Handlers" / "Login"
GT_HANDLERS = ROOT / "server-cs" / "src" / "PaperMan.Server" / "Handlers" / "GT"
SCHEMA_SOURCE = ROOT / "db" / "schema.sql"
SMOKE_TEST_SOURCE = ROOT / "db" / "smoke_test.py"
SELF_TEST_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.SelfTest" / "Program.cs"
CLAN_CONTRACT_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Contracts" / "GC_CLAN_PROTOCOL.cs"
UDP_CONTROL_CONTRACT_SOURCE = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Contracts" / "UdpControlWire.cs"

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


def native_clan_ack_sub_opcodes() -> frozenset[int]:
    source = NATIVE_SOURCE.read_text(encoding="utf-8", errors="replace")
    marker = "//----- (0054D040)"
    try:
        start = source.index(marker)
        end = source.index("//-----", start + len(marker))
    except ValueError:
        fail("could not locate the native sub_54D040 clan ACK dispatcher")

    return frozenset(
        int(match["value"])
        for match in re.finditer(r"^    case (?P<value>\d+):$", source[start:end], re.MULTILINE)
    )


def native_clan_request_sub_opcodes() -> frozenset[int]:
    source = NATIVE_SOURCE.read_text(encoding="utf-8", errors="replace")
    builders = list(
        re.finditer(
            r"Packet::possible_ctor_or_dtor_0\((?P<packet>\w+), 583\);",
            source,
        )
    )
    if len(builders) != 24:
        fail(f"expected 24 native 583 builders, found {len(builders)}")

    values: set[int] = set()
    for builder in builders:
        packet = re.escape(builder["packet"])
        sub_op_match = re.search(
            rf"sub_592A20\({packet}, (?P<value>\d+)\);",
            source[builder.end():builder.end() + 800],
        )
        if sub_op_match is None:
            fail("a native 583 builder has no literal leading sub_opcode write")
        values.add(int(sub_op_match["value"]))
    return frozenset(values)


def source_clan_sub_opcodes() -> frozenset[int]:
    text = CLAN_CONTRACT_SOURCE.read_text(encoding="utf-8")
    enum_match = re.search(
        r"public enum GC_CLAN_PROTOCOL_SubOpcode\n\{(?P<body>.*?)\n\}",
        text,
        re.DOTALL,
    )
    if enum_match is None:
        fail("GC_CLAN_PROTOCOL_SubOpcode enum is missing")
    if "public static class GC_CLAN_PROTOCOL_Wire" not in text:
        fail("GC_CLAN_PROTOCOL_Wire contract is missing")

    values: set[int] = set()
    for match in re.finditer(
        r"^    Sub(?P<name>\d+) = (?P<value>\d+),",
        enum_match["body"],
        re.MULTILINE,
    ):
        name_value = int(match["name"])
        wire_value = int(match["value"])
        if name_value != wire_value:
            fail(f"GC_CLAN_PROTOCOL sub-op name/value mismatch: Sub{name_value} = {wire_value}")
        values.add(wire_value)
    if not values:
        fail("GC_CLAN_PROTOCOL_SubOpcode contains no SubNNN values")
    return frozenset(values)


def native_udp_private_opcodes() -> frozenset[int]:
    source = NATIVE_SOURCE.read_text(encoding="utf-8", errors="replace")
    request_marker = "//----- (00596670)"
    completion_marker = "//----- (00595E80)"
    try:
        request_start = source.index(request_marker)
        request_end = source.index("//-----", request_start + len(request_marker))
        completion_start = source.index(completion_marker)
        completion_end = source.index("//-----", completion_start + len(completion_marker))
    except ValueError:
        fail("could not locate native UDP opcode 19/20 paths")

    request_path = source[request_start:request_end]
    completion_path = source[completion_start:completion_end]
    if not re.search(r"Packet::possible_ctor_or_dtor_0\(\w+, 19\);", request_path):
        fail("sub_596670 does not construct private UDP opcode 19")
    if not re.search(r"case 20:\s*\n\s*sub_5968C0\(this, a2\);", completion_path):
        fail("sub_595E80 case 20 no longer dispatches sub_5968C0")
    return frozenset({19, 20})


def source_udp_private_opcodes() -> frozenset[int]:
    text = UDP_CONTROL_CONTRACT_SOURCE.read_text(encoding="utf-8")
    enum_match = re.search(
        r"public enum UdpPrivateOpcode : ushort\n\{(?P<body>.*?)\n\}",
        text,
        re.DOTALL,
    )
    if enum_match is None:
        fail("UdpPrivateOpcode enum is missing")

    values: set[int] = set()
    for match in re.finditer(
        r"^    Opcode(?P<name>\d+) = (?P<value>\d+),",
        enum_match["body"],
        re.MULTILINE,
    ):
        name_value = int(match["name"])
        wire_value = int(match["value"])
        if name_value != wire_value:
            fail(f"UDP private opcode name/value mismatch: Opcode{name_value} = {wire_value}")
        values.add(wire_value)
    if not values:
        fail("UdpPrivateOpcode contains no OpcodeNN values")
    return frozenset(values)


def source_ai_handler_group() -> int:
    legacy_directory = AI_HANDLERS.with_name("Ai")
    if legacy_directory.exists():
        fail("legacy Handlers/Ai directory remains; canonical native/resource spelling is AI")
    sources = sorted(AI_HANDLERS.glob("Handlers.*.cs"))
    if not sources:
        fail("Handlers/AI contains no canonical AI handler sources")
    for source in sources:
        if "public static partial class AIHandlers" not in source.read_text(encoding="utf-8"):
            fail(f"{source}: AI family must use the canonical AIHandlers spelling")
    return len(sources)


def source_login_handler_group() -> int:
    legacy_directory = LOGIN_HANDLERS.with_name("Auth")
    if legacy_directory.exists():
        fail("legacy Handlers/Auth directory remains; native/resource spelling is Login")
    sources = sorted(LOGIN_HANDLERS.glob("Handlers.*.cs"))
    if {source.name for source in sources} != {"Handlers.GL_LOGIN.cs"}:
        fail("Handlers/Login must contain only the native login handler")
    for source in sources:
        if "public static partial class LoginHandlers" not in source.read_text(encoding="utf-8"):
            fail(f"{source}: Login family must use the canonical LoginHandlers spelling")
    return len(sources)


def source_gt_handler_group() -> int:
    sources = sorted(GT_HANDLERS.glob("Handlers.*.cs"))
    if {source.name for source in sources} != {"Handlers.GT_PING.cs"}:
        fail("Handlers/GT must contain only the canonical GT_PING handler")
    source = sources[0]
    if "public static partial class GTHandlers" not in source.read_text(encoding="utf-8"):
        fail(f"{source}: GT family must use the canonical GTHandlers spelling")
    return len(sources)


def source_room_mode_index_persistence() -> None:
    schema = SCHEMA_SOURCE.read_text(encoding="utf-8")
    for table in ("rooms", "match_results"):
        table_match = re.search(
            rf"CREATE TABLE IF NOT EXISTS {table} \((?P<body>.*?)\n\) STRICT",
            schema,
            re.DOTALL,
        )
        if table_match is None or not re.search(r"^    mode_index\s+INTEGER", table_match["body"], re.MULTILINE):
            fail(f"{table} must persist the client modeIndex as local mode_index")
        if re.search(r"^    rule\s+INTEGER", table_match["body"], re.MULTILINE):
            fail(f"{table}.rule remains as a current schema column")

    if "Rule:" in SELF_TEST_SOURCE.read_text(encoding="utf-8"):
        fail("SelfTest still asserts the obsolete Room.Rule property")
    smoke_test = SMOKE_TEST_SOURCE.read_text(encoding="utf-8")
    if "map_id,rule," in smoke_test:
        fail("DB smoke test still writes the obsolete rule column")


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

    native_clan_ack_values = native_clan_ack_sub_opcodes()
    native_clan_request_values = native_clan_request_sub_opcodes()
    expected_clan_values = native_clan_ack_values | native_clan_request_values
    if source_clan_sub_opcodes() != expected_clan_values:
        fail(
            "GC_CLAN_PROTOCOL_SubOpcode must be the exact union of "
            "native 584 switch values and native 583 builder values"
        )

    if source_udp_private_opcodes() != native_udp_private_opcodes():
        fail("UdpPrivateOpcode must preserve the native numeric-only 19/20 values")

    ai_handler_count = source_ai_handler_group()
    # Both native CyAIMultiModeLobbyUI and this revision's resource directory
    # spell the initialism AI. Read a concrete resource to make path/case drift
    # fail without checking out main.
    git_show_main("Extracted/ui/system/AI/AiMultiLevel.xml")

    login_handler_count = source_login_handler_group()
    gt_handler_count = source_gt_handler_group()
    if "CLobbyLogin" not in NATIVE_SOURCE.read_text(encoding="utf-8", errors="replace"):
        fail("native CLobbyLogin evidence is missing")
    git_show_main("Extracted/ui/login.xml")

    source_room_mode_index_persistence()

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

    source_root = ROOT / "server-cs" / "src"
    for token in (
        "ClanTunnel",
        "ClanSubOp",
        "UdpPrivateOpcode.ControlRequest",
        "UdpPrivateOpcode.ControlCompletion",
        "AuthHandlers",
    ):
        if any(token in path.read_text(encoding="utf-8") for path in source_root.rglob("*.cs")):
            fail(f"stale numeric-only protocol alias remains: {token}")

    print(
        "server naming OK: "
        f"{len(enum_values)} native GameMode values; "
        f"{len(resource_default_maps)} Extracted default maps; "
        f"{len(MODE_INDEX_MAP_BITS)} modeIndex→maplist-bit entries; "
        f"{len(expected_clan_values)} numeric GC_CLAN_PROTOCOL sub-ops; "
        "2 numeric private UDP opcodes; "
        f"{ai_handler_count} canonical AI handler sources; "
        f"{login_handler_count} canonical Login handler sources; "
        f"{gt_handler_count} canonical GT handler source; "
        "modeIndex persistence names"
    )


if __name__ == "__main__":
    main()
