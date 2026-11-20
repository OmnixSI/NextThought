#!/usr/bin/env python3
"""Count Windows Core files and how many names are referenced by non-test code.

The heuristic is deliberately simple and reproducible: a Core .cs file is
"used" when its file stem appears in any other Windows/src .cs file.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CORE = ROOT / "Windows" / "src" / "NextThought.Core"
SRC = ROOT / "Windows" / "src"

core = sorted(CORE.glob("*.cs"))
others = []
for path in SRC.rglob("*.cs"):
    if CORE in path.parents:
        continue
    try:
        others.append(path.read_text(encoding="utf-8", errors="ignore"))
    except OSError:
        pass
joined = "\n".join(others)
unused = [path.name for path in core if path.stem not in joined]
print(f"core_files={len(core)} unused={len(unused)}")
for name in unused:
    print(name)
