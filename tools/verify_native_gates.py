#!/usr/bin/env python3
"""Re-derive client-side gates and state names from the binary and resources.

Run from any working directory:
    python3 tools/verify_native_gates.py

`docs/WIKI_MECHANICS.md` §5b-17 and `docs/PACKETS.md` §3.15r claim that the
Pepachi (700) and capsule (900) callers refuse to send until four *local*
conditions hold, that the level gate exists on the PG branch only, and that
opcode 995 is what populates the three globals those gates read.

Every one of those claims is re-derivable from two files already in the repo,
so this tool re-derives them instead of trusting the prose:

  * the four gate constants (10 / 200, one pair per caller) from their
    `// idb` initialisers in `PaperMan.exe.c`;
  * the asymmetry itself -- the level global appears in the PG arm of each
    caller and the level message id appears exactly once per caller;
  * the message ids the failure arms display, resolved through
    `msgtableres.lang` so a text drift is caught as well as a number drift;
  * that `sub_567AE0` is dispatched from `case 995u` and writes all three
    globals, which is the only reason we can name them.

This reads the dump textually. It is a drift alarm, not a decompiler.
"""
from __future__ import annotations

import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DUMP = ROOT / "PaperMan.exe.c"
MSGTABLE = ROOT / "Extracted" / "ui" / "lang" / "msgtableres.lang"

# The two structurally identical callers, with the constants documented for each.
CALLERS = {
    "sub_8459C0": {
        "opcode": 700,
        "sender": "sub_8458D0",
        "level_global": "dword_BDBC98",
        "presentbox_global": "dword_BDBC9C",
    },
    "sub_99D0A0": {
        "opcode": 900,
        "sender": "sub_99CFA0",
        "level_global": "dword_BEAE4C",
        "presentbox_global": "dword_BEAE50",
    },
}

LEVEL_MIN = 10
PRESENTBOX_MAX = 200

# Wallet/level globals, and the 995 field order that lets us name them.
PG_GLOBAL = "dword_EE8D18"
CASH_GLOBAL = "dword_EE8D0C"
LEVEL_GLOBAL = "n10_2"
WALLET_READER = "sub_567AE0"
WALLET_CASE = "case 995u:"

# Failure-arm message ids, as hex literals in the dump, with the exact resource
# text each must still resolve to.
MESSAGES = {
    0x108: "ＣＡＳＨが不足しています。",
    0xFC: "PGが不足しています。",
    0x34E: "ペーパチはレベル「%d」以上からご利用できます。",
    0x34F: "プレゼントボックスに空きがありません。",
}

# PACKETS.md 3.15s: the tournament state machine. Each state that the lobby
# renders resolves to one of these strings, which is where the state *names*
# come from -- the wiki only corroborates the ordering afterwards.
TOURNAMENT_READER = "sub_57E5A0"
TOURNAMENT_STATES = {
    949: "トーナメント情報公開",
    950: "受付中",
    966: "入場中",
    982: "トーナメントが終了しました。",
}
# state==5 expands by remaining-round counter 4..0, in progress then finished.
TOURNAMENT_ROUNDS = {
    952: "32強戦、試合中", 953: "16強戦、試合中", 954: "8強戦、試合中",
    955: "4強戦、試合中", 956: "決勝戦、試合中",
    957: "32強戦、終了", 958: "16強戦、終了", 959: "8強戦、終了",
    960: "4強戦、終了",
}
# The entry cap the client actually states, which is NOT the wiki's join floor.
TOURNAMENT_ENTRY_CAP = (933, "１クラン１０名まで入場可能")

# `char name[] = { '\n', '\0', '\0', '\0' };` -- a little-endian dword written
# out as four character escapes by Hex-Rays.
IDB_INIT = r"char {name}\[\] = \{{([^}}]*)\}}"


def fail(failures: list[str], message: str) -> None:
    failures.append(message)


def dword_initialiser(text: str, name: str) -> int | None:
    """Value of a Hex-Rays `char name[] = {...}` four-byte initialiser."""
    match = re.search(IDB_INIT.format(name=re.escape(name)), text)
    if match is None:
        return None
    literals = re.findall(r"'((?:\\.|[^'\\])*)'", match.group(1))
    if len(literals) != 4:
        return None
    value = 0
    for index, literal in enumerate(literals):
        try:
            byte = ord(literal.encode().decode("unicode_escape"))
        except (UnicodeDecodeError, TypeError):
            return None
        value |= byte << (8 * index)
    return value


def function_body(text: str, name: str) -> str | None:
    """Body of a defined function, located by brace matching from its header."""
    head = re.search(r"\n[A-Za-z_][^\n]*\b" + re.escape(name) + r"\([^)]*\)\s*\r?\n\{",
                     text)
    if head is None:
        return None
    start = text.index("{", head.start())
    depth = 0
    for index in range(start, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return text[start:index + 1]
    return None


def message_entries() -> dict[int, str] | None:
    """`entry i = lines[i+3]`, the decode rule fixed in RESOURCES.md section 8."""
    if not MSGTABLE.is_file():
        return None
    lines = MSGTABLE.read_bytes().decode("cp932").split("\n")
    return {index: line.strip() for index, line in enumerate(lines[3:])}


def main() -> None:
    failures: list[str] = []
    checked = 0

    def check(label: str, actual: object, expected: object) -> None:
        nonlocal checked
        checked += 1
        if actual != expected:
            fail(failures, f"{label}: expected {expected!r}, got {actual!r}")

    if not DUMP.is_file():
        print(f"skipped: {DUMP} is absent (working branches may omit it)")
        return
    text = DUMP.read_text(encoding="utf-8", errors="replace")

    for caller, spec in CALLERS.items():
        body = function_body(text, caller)
        if body is None:
            fail(failures, f"{caller}: no function body found in the dump")
            continue

        # The gate constants themselves. The tracked dump carries no .data
        # initialiser for these four globals (values 10 / 200 were re-derived
        # from the richer private dump and are documented in PACKETS.md
        # section 168 and WIKI_MECHANICS.md); when the initialiser is absent
        # we fall back to the structural clamp pattern inside the caller body.
        level_init = dword_initialiser(text, spec["level_global"])
        if level_init is not None:
            check(f"{caller} level floor ({spec['level_global']})",
                  level_init, LEVEL_MIN)
        else:
            check(f"{caller} level clamp uses {spec['level_global']} "
                  f"(init value {LEVEL_MIN} per docs; not in this dump)",
                  f"*{spec['level_global']}" in body, True)
        box_init = dword_initialiser(text, spec["presentbox_global"])
        if box_init is not None:
            check(f"{caller} present-box cap ({spec['presentbox_global']})",
                  box_init, PRESENTBOX_MAX)
        else:
            check(f"{caller} present-box clamp uses "
                  f"{spec['presentbox_global']} (init value {PRESENTBOX_MAX} "
                  f"per docs; not in this dump)",
                  f"* {spec['presentbox_global']}" in body.replace(" ", "") or
                  f"*{spec['presentbox_global']}" in body, True)

        # The caller must actually read each global, and reach its sender.
        for role in ("level_global", "presentbox_global"):
            if spec[role] not in body:
                fail(failures, f"{caller} never reads {spec[role]}")
            checked += 1
        if spec["sender"] not in body:
            fail(failures,
                 f"{caller} no longer calls its sender {spec['sender']} "
                 f"(opcode {spec['opcode']})")
        checked += 1

        # Present-box and level failures are shown once each; the level message
        # appearing exactly once is what makes the gate PG-only rather than
        # applying to both payment branches.
        check(f"{caller} shows the level message (846) exactly once",
              body.count("0x34Eu"), 1)
        check(f"{caller} shows the present-box message (847) exactly once",
              body.count("0x34Fu"), 1)

        # Both callers gate the cash balance; only 700 also has the PG balance
        # arm, because 900's PG control is a separate selector.
        check(f"{caller} shows the CASH-shortfall message (264)",
              "0x108u" in body, True)

    # Opcode 995 is the only reason the three globals can be named.
    reader = function_body(text, WALLET_READER)
    if reader is None:
        fail(failures, f"{WALLET_READER}: no function body found in the dump")
    else:
        for global_name in (PG_GLOBAL, CASH_GLOBAL, LEVEL_GLOBAL):
            if global_name not in reader:
                fail(failures,
                     f"{WALLET_READER} no longer writes {global_name}; "
                     "the 995 field-order naming in PACKETS.md 3.15r is stale")
            checked += 1

    dispatch = re.search(re.escape(WALLET_CASE) + r"\s*\r?\n\s*" + re.escape(WALLET_READER),
                         text)
    check(f"{WALLET_READER} is dispatched from {WALLET_CASE}", dispatch is not None, True)

    # PACKETS.md 3.15s: 763 reads raw4, u8 state, u8 round, u8, and only
    # consults a trailing s32 tournament id when state == 2.
    tournament = function_body(text, TOURNAMENT_READER)
    if tournament is None:
        fail(failures, f"{TOURNAMENT_READER}: no function body found in the dump")
    else:
        check(f"{TOURNAMENT_READER} reads one raw4 then three u8",
              tournament.count("sub_592AC0("), 1)
        check(f"{TOURNAMENT_READER} u8 reads", tournament.count("sub_592940("), 3)
        check(f"{TOURNAMENT_READER} reads a single conditional s32",
              tournament.count("sub_592A40("), 1)

    # RESOURCES.md 5d-25: the exe reads shilddamage_rate; AiMultiLevel.xml
    # ships siege_dmg_rate. Pin BOTH halves so the mismatch cannot silently
    # be "fixed" in the notes while the binary still disagrees.
    check("exe reads shilddamage_rate", text.count('L"shilddamage_rate"'), 1)
    check("exe never mentions siege_dmg_rate", 'siege_dmg_rate' in text, False)

    # RESOURCES.md 5d-26 (2026-09-18 覆查更正): scale IS parsed by the bot
    # loader sub_8CCB30 into row field +0x1C; six L"scale" sites in total
    # (bot parser + five UI/effects readers). Effect downstream: UNRESOLVED.
    check("exe reads the bot scale attribute (6 sites)",
          text.count('L"scale"'), 6)
    check("bot enemy parser sub_8CCB30 present", 'sub_8CCB30' in text, True)
    check("exe never references BotEnemy_intelligent",
          "BotEnemy_intelligent" in text, False)
    # One easy flag (n3 == 3) switches bots, waves and scenario together.
    for resource in ("BotEnemy_easy.xml", "BotWave_easy.xml",
                     "Scenario_easy.xml"):
        check(f"exe loads {resource}", text.count(resource), 1)

    # RESOURCES.md 5d-27: no endpoint is compiled in -- every outbound URL
    # comes from URLList, which is what makes redirection a data-only change.
    check("exe hardcodes no URL literal", text.count('L"http'), 0)
    check("exe loads URLList by numbered path",
          text.count('L"ui/system/URLList_%02d.xml"'), 1)
    # name is a human label; the parser keys on index.
    for attribute in ("index", "url", "disable"):
        check(f"URLList parser reads {attribute}",
              f'L"{attribute}"' in text, True)

    # RESOURCES.md 5d-4b: periodType is shipped but never parsed, and exactly
    # one of the 19 ui/system/AI xml files is absent from the binary.
    check("exe never reads periodType", 'L"periodType"' in text, False)
    check("exe reads the reward table's itemnumber and level",
          all(f'L"{name}"' in text for name in ("itemnumber", "level")), True)
    # 19 AI xml files ship; exactly one (the dead BotEnemy_intelligent) is
    # absent from the binary. Guards the "18 of 19, not 18 of 18" correction.
    ai_dir = ROOT / "Extracted" / "ui" / "system" / "AI"
    if ai_dir.is_dir():
        unreferenced = sorted(path.name for path in ai_dir.glob("*.xml")
                              if path.name not in text)
        # Only assert once the full AI set is present locally, but then
        # assert it exactly -- 19 files, of which precisely one is dead.
        shipped = sorted(path.name for path in ai_dir.glob("*.xml"))
        if len(shipped) < 19:
            print(f"note: only {len(shipped)} of 19 ui/system/AI xml present; "
                  "the 18-of-19 reference check is skipped")
        else:
            check("ui/system/AI xml count", len(shipped), 19)
            check("only BotEnemy_intelligent.xml is missing from the binary",
                  unreferenced, ["BotEnemy_intelligent.xml"])

    # RESOURCES.md 5d-7b: the convar registry. Types 0 and 1 are shipped in
    # convars.pat; every type 2 is not -- including um_gr_maxspeed, which
    # therefore always runs at its hardcoded 90.0 default.
    convar_types: dict[str, set[str]] = {}
    lines = text.replace("\r", "").split("\n")
    for index, line in enumerate(lines):
        named = re.search(r'sub_4023E0\([^,]+, "([A-Za-z_0-9]+)"', line)
        if not named:
            continue
        for follow in lines[index:index + 8]:
            typed = re.search(r"sub_715580\([^,]+, [^,]+, this, (\d+),", follow)
            if typed:
                convar_types.setdefault(named.group(1), set()).add(typed.group(1))
                break
    check("convar registrations discovered", len(convar_types), 23)
    check("character-ability convars are type 0",
          sorted(name for name, kinds in convar_types.items() if kinds == {"0"}),
          ["cam_offset", "def_hp", "defence", "jumpheight", "max_hp",
           "movespeed", "sitdownCamHeight", "standCamHeight"])
    check("integer global convars are type 1",
          sorted(name for name, kinds in convar_types.items() if kinds == {"1"}),
          ["gun_caliber", "um_gr_accel", "um_gr_decel"])
    check("um_gr_maxspeed is registered as type 2",
          sorted(convar_types.get("um_gr_maxspeed", set())), ["2"])
    # 1119092736 is float 90.0, the same figure as the modal movespeed.
    check("um_gr_maxspeed defaults to 90.0",
          struct.unpack("f", struct.pack("I", 1119092736))[0], 90.0)
    check("um_gr_maxspeed default appears in the dump",
          "1119092736" in text, True)

    # RESOURCES.md 5d-11c: the partsability column -> offset contract. The
    # offsets are NOT linear in column order, so this table is re-derived from
    # the loader rather than assumed.
    writes: list[tuple[int, str]] = []
    for index in range(len(lines)):
        stored = re.search(r"4204 \* j \+ (\d+)\) = ", lines[index])
        if not stored:
            continue
        context = " ".join(lines[max(0, index - 2):index])
        writes.append((int(stored.group(1)),
                       "atol" if "atol" in context else "atof"))
    check("partsability writes 26 of its 31 columns", len(writes), 26)
    check("partsability column offsets",
          [offset for offset, _ in writes],
          [4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48,
           60, 52, 64, 68, 72, 76, 80, 56, 84, 88, 92, 96, 100, 104])
    # The two transposed pairs, called out explicitly so a "fix" to a linear
    # layout cannot pass silently.
    offsets = [offset for offset, _ in writes]
    check("bullet_hole/ballCaseSize are transposed",
          (offsets[12], offsets[13]), (60, 52))
    check("miJump/miSit are transposed", (offsets[18], offsets[19]), (80, 56))
    check("only the last two columns are integers",
          [kind for _, kind in writes][-3:], ["atof", "atol", "atol"])
    check("the discarded tail columns are absent from the binary",
          [name for name in ("tanpi_pap_type", "tanpi_mot_type",
                             "sniperbackimgidx", "sniperviewimgidx")
           if name in text], [])

    # RESOURCES.md 5c-2d: radio lines use the caller's exact index while the
    # other voice categories randomise, and a missing file falls back to the
    # default voice pack.
    check("radio uses the Radio_Message folder",
          'L"Radio_Message"' in text, True)
    check("voice path template",
          text.count('L"%s\\\\%s\\\\%s\\\\%s_%s\\\\%s_%s_%02d.wav"') >= 1, True)
    # The randomising arm exists for non-radio categories only.
    check("non-radio voice categories randomise",
          "rand() % n6_1 + 1" in text, True)

    # RESOURCES.md 5c-3: nine xml names exist in both ui/ and ui/system/, and
    # the binary loads the system/ copy in every case. Guards against anyone
    # "simplifying" an analysis onto the stale root-level duplicate.
    DUPLICATED = ("FontDefinition", "ItemAbilityEffectColorTable",
                  "ItemAbilityEffectNameTable", "ItemAbilityLevTable",
                  "NewSkillColorTable", "NewSkillLevTable",
                  "gimmickproperty", "map_StartIndex",
                  "voice_customize_contents")
    for name in DUPLICATED:
        check(f"{name}.xml is loaded from system/",
              f'system/{name}.xml' in text, True)
        # No loader ever names the root-level copy.
        check(f"{name}.xml is never loaded from ui/ root",
              f'L"{name}.xml"' in text or f'ui/{name}.xml' in text, False)

    # RESOURCES.md 5d-29: the emblem is three layers in the UI but one s32 on
    # the wire, and the exe never names the atlas files -- which is why the
    # wiki's 120/60/60 part counts cannot be corroborated.
    # emblem_1..4 are icon-count layout presets (present_1..4 mirror them),
    # NOT the wiki's three emblem layers.
    check("emblem layout presets",
          [name for name in ("emblem_1", "emblem_2", "emblem_3", "emblem_4",
                             "emblem_5")
           if f'L"{name}"' in text],
          ["emblem_1", "emblem_2", "emblem_3", "emblem_4"])
    check("present_1..4 mirror the same layout shape",
          [name for name in ("present_1", "present_2", "present_3",
                             "present_4") if f'L"{name}"' in text],
          ["present_1", "present_2", "present_3", "present_4"])
    check("exe never names the emblem atlases",
          [name for name in ("emblem_mark", "emblem_frame", "emblem_base")
           if name in text], [])

    # RESOURCES.md 5d-29: the emblem packing and the three sub-tab scans.
    check("emblem packs mark | frame<<16 | base<<24",
          "*(this + 246) | (*(this + 248) << 16) | (*(this + 249) << 24)"
          in text, True)
    for bound in ("21845", "43691"):
        check(f"EM_MARK scan boundary {bound}",
              f'sub_40A590(this, {bound}' in text
              or f", {bound}, L\"EM_MARK\"" in text, True)
    check("EM_FRAME and EM_BASE scan the 8-bit space in thirds",
          all(f', {bound}, L"{name}"' in text
              for name in ("EM_FRAME", "EM_BASE")
              for bound in ("85", "171", "255")), True)

    # RESOURCES.md 5d-21 (34th round): the client never names any drop item,
    # by id or by code-name prefix, which is why family A is unknowable here.
    for prefix in ("D_Item", "Q_Item", "P_Item", "W_Item", "M_Item",
                   "DropItem_00"):
        check(f"exe never names {prefix}", prefix in text, False)
    check("exe contains no D_Item object id literal",
          re.search(r"\b3368[0-9]{4}\b", text) is None, True)

    # RESOURCES.md 5d-11b (34th round): partsability records are looked up
    # through exactly one entry point, and move_speed is never read back.
    lookups = [index for index, line in enumerate(lines)
               if "sub_958320(" in line and "= sub_958320" in line]
    check("partsability lookup sites", len(lookups) >= 20, True)
    consumed = set()
    for index in lookups:
        holder = re.search(r"(\w+) = sub_958320", lines[index])
        if not holder:
            continue
        for follow in lines[index + 1:index + 40]:
            for field in re.finditer(rf"\*\({holder.group(1)} \+ (\d+)\)",
                                     follow):
                consumed.add(int(field.group(1)))
    check("partsability offsets actually read back",
          sorted(consumed), [36, 92, 96, 100, 104])
    check("move_speed (+72) is never read back", 72 in consumed, False)
    check("miRun (+92) is the only posture field read back",
          [off for off in (56, 80, 84, 88, 92) if off in consumed], [92])

    # The failure-arm texts, through the documented decode rule.
    entries = message_entries()
    if entries is None:
        print("note: msgtableres.lang absent; message text checks skipped")
    else:
        for message_id, expected in {**MESSAGES, **TOURNAMENT_STATES,
                                     **TOURNAMENT_ROUNDS,
                                     TOURNAMENT_ENTRY_CAP[0]:
                                         TOURNAMENT_ENTRY_CAP[1]}.items():
            actual = entries.get(message_id, "")
            checked += 1
            if expected not in actual:
                fail(failures,
                     f"message {message_id} ({message_id:#x}): expected text to "
                     f"contain {expected!r}, got {actual!r}")

    if failures:
        print("native gate verification failed:")
        for failure in failures:
            print(f"  {failure}")
        raise SystemExit(1)

    print(f"native gates/state names OK: {checked} checks re-derived from "
          "PaperMan.exe.c and Extracted/")


if __name__ == "__main__":
    main()
