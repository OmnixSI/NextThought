#!/usr/bin/env python3
"""R8 release cleanliness audit.

The formal Windows package must not expose the QA escape hatches, the debug startup switches or the
debug-only sample pages. This scans every shipped file of a published payload for those markers
(direct byte search in UTF-8/ASCII and UTF-16LE, so multi-hundred-MB ReadyToRun assemblies stay
cheap) and exits non-zero with the offending file and marker.

Usage: python scripts/release_cleanliness.py --payload artifacts/release/.stage/0.9.0/payload
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

BANNED = [
    "NEXTTHOUGHT_QA",
    "NEXTTHOUGHT_GPU_RENDER",
    "NEXTTHOUGHT_UPDATE_LAUNCH",
    "--update-check=",
    "--demo-churn",
    "--demo-voices",
    "--voices",
    "--onboarding",
    "ControlSampleWindow",
    "TypographySampleWindow",
    "ComponentSamplesWindow",
    "LiveComponentSamplesWindow",
]

# Debug symbols and documentation are not part of the shipped behaviour and are huge.
SKIP_SUFFIXES = {".pdb", ".xml", ".tar", ".bz2", ".zip", ".nupkg"}


def find_markers(data: bytes) -> list[str]:
    return [marker for marker in BANNED
            if marker.encode("utf-8") in data or marker.encode("utf-16-le") in data]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--payload", required=True, help="published payload directory")
    args = parser.parse_args()
    payload = Path(args.payload)
    if not payload.is_dir():
        print("release cleanliness: payload directory not found: " + str(payload))
        return 2
    problems: list[str] = []
    checked = 0
    for path in sorted(payload.rglob("*")):
        if not path.is_file() or path.suffix.lower() in SKIP_SUFFIXES:
            continue
        checked += 1
        hits = find_markers(path.read_bytes())
        if hits:
            problems.extend(str(path.relative_to(payload)) + " :: " + marker for marker in hits)
    print("release cleanliness: checked " + str(checked) + " payload files")
    if problems:
        for problem in problems:
            print("VIOLATION " + problem)
        print("release cleanliness: FAILED (" + str(len(problems)) + " violations)")
        return 1
    print("release cleanliness: PASS (no QA switch, debug startup switch or sample page in the payload)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
