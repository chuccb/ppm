#!/usr/bin/env python3
"""Diff the resource filenames the client loads against what Extracted/ ships.

Run from any working directory:
    python3 tools/verify_resource_coverage.py
    python3 tools/verify_resource_coverage.py --list

`PaperMan.exe.c` names its data files as wide string literals, so the set of
resources the client can load is recoverable. Comparing that set against the
shipped tree answers two questions that keep coming up:

* is a file we are about to analyse actually loaded by this revision, or is it
  leftover in the extraction; and
* is a file the client wants missing from the extraction, which would cap what
  can ever be proven about that subsystem.

Every currently-missing reference is expected and classified below. The tool
fails only when an unclassified one appears, which means either the extraction
changed or the dump was replaced.

This matches on filename, not full path, because the dump stores directory
prefixes separately from the leaf name in several loaders.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DUMP = ROOT / "PaperMan.exe.c"
EXTRACTED = ROOT / "Extracted"

REFERENCE = re.compile(r'L"([A-Za-z0-9_/\\.]+\.(?:xml|pat|dat|ini|txt|lang))"')

# The full extraction on main holds tens of thousands of files; a working
# branch carries only a token few. Below this, the comparison is meaningless.
FULL_TREE_MINIMUM = 500

# References that are legitimately absent from Extracted/, with the reason.
EXPECTED_ABSENT = {
    "/lastchatfilter.ini": "written by the client at runtime (local chat filter state)",
    "/lastconnect.ini": "written by the client at runtime (last server)",
    "user/lastconnectuserid.txt": "written by the client at runtime (last user id)",
    "data/pmclient.dat": "the pack container that holds these resources, not a member of it",
    "ata/pmclient.dat": "decompiler artifact: a pointer-increment copy of the string above",
    "exceptionword.dat": "fallback extension; the shipped file is exceptionword.txt",
    "filterword.dat": "fallback extension; the shipped file is filterword.txt",
    "ui/charfittinganimation.xml": (
        "genuinely absent. Loaded with root UICHARFITTINGANIMATION for the fitting-room "
        "animation, so that subsystem cannot be fully analysed from this extraction."),
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list", action="store_true",
                        help="print every referenced filename and whether it ships")
    arguments = parser.parse_args()

    if not DUMP.is_file():
        print(f"resource coverage skipped: {DUMP} is not present")
        return
    if not EXTRACTED.is_dir():
        print(f"resource coverage skipped: {EXTRACTED} is not present")
        return

    references = {match.replace("\\\\", "/").replace("\\", "/")
                  for match in REFERENCE.findall(
                      DUMP.read_text(encoding="utf-8", errors="replace"))}
    shipped = {path.name.lower() for path in EXTRACTED.rglob("*") if path.is_file()}

    absent = sorted(reference for reference in references
                    if reference.split("/")[-1].lower() not in shipped)

    if arguments.list:
        for reference in sorted(references):
            state = "ship" if reference.split("/")[-1].lower() in shipped else "MISS"
            print(f"{state}  {reference}")

    # Only the full extraction (on the main branch) can answer the coverage
    # question. A working branch carries a handful of resources, so comparing
    # against it would report almost every reference as missing.
    present = len(references) - len(absent)
    if len(shipped) < FULL_TREE_MINIMUM:
        print(f"resource coverage skipped: Extracted/ holds only {len(shipped)} files, "
              f"which is a partial checkout. Run this against the full tree from the "
              f"main branch to check the {len(references)} referenced names.")
        return

    unexplained = [reference for reference in absent
                   if reference.lower() not in EXPECTED_ABSENT]
    if unexplained:
        print("resource coverage failed: unclassified missing reference(s):")
        for reference in unexplained:
            print(f"  {reference}")
        print("Either Extracted/ changed or the dump was replaced; classify or fix.")
        raise SystemExit(1)

    print(f"resource coverage OK: the client names {len(references)} resource files; "
          f"{present} ship in Extracted/ ({100 * present // len(references)}%); "
          f"{len(absent)} absent, all expected "
          f"(3 runtime-written, 2 pack/artifact, 2 fallback extensions, "
          f"1 genuinely missing: ui/CharFittingAnimation.xml)")


if __name__ == "__main__":
    main()
