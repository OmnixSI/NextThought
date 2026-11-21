#!/usr/bin/env python3
"""H4: report Core files whose declared types are never referenced by Windows code.

Counted across Windows/src and Windows/tests (including other Core files). A partial
class file counts as used when its partial type is referenced. The goal is unused=0.
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CORE = ROOT / "Windows" / "src" / "NextThought.Core"
OTHERS = [ROOT / "Windows" / "src", ROOT / "Windows" / "tests"]
DECLARATION = re.compile(
    r"\b(?:public|internal|private|protected|static|sealed|partial|abstract|readonly|file|new\s+)*"
    r"(?:class|record|enum|struct|interface)\s+([A-Za-z_]\w*)"
)

files: list[tuple[Path, list[str]]] = []
for path in sorted(CORE.glob("*.cs")):
    text = path.read_text(encoding="utf-8", errors="ignore")
    names = sorted(set(DECLARATION.findall(text)))
    files.append((path, names))

sources: dict[Path, str] = {}
for base in OTHERS:
    for path in base.rglob("*.cs"):
        try:
            sources[path] = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            pass

unused = []
for path, names in files:
    if not names:
        unused.append((path.name, "<no type>"))
        continue
    used = False
    for other, text in sources.items():
        if other == path:
            continue
        if any(re.search(r"\b" + re.escape(name) + r"\b", text) for name in names):
            used = True
            break
    if not used:
        unused.append((path.name, ", ".join(names)))

print(f"core_files={len(files)} unused={len(unused)}")
for name, types in unused:
    print(name, "::", types)
raise SystemExit(0 if not unused else 1)
