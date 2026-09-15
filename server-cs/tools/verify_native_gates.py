#!/usr/bin/env python3
"""Re-derive client-side gates and state names from the binary and resources.

Run from any working directory:
    python3 server-cs/tools/verify_native_gates.py

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
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
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

        # The gate constants themselves.
        check(f"{caller} level floor ({spec['level_global']})",
              dword_initialiser(text, spec["level_global"]), LEVEL_MIN)
        check(f"{caller} present-box cap ({spec['presentbox_global']})",
              dword_initialiser(text, spec["presentbox_global"]), PRESENTBOX_MAX)

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

    # RESOURCES.md 5d-26: scale is shipped on every BotEnemy row but the exe
    # never reads it, and BotEnemy_intelligent.xml is never loaded at all.
    check("exe never reads the bot scale attribute", 'L"scale"' in text, False)
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
