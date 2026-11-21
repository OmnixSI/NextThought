#!/usr/bin/env python3
"""Recompute the four-state counts used in the Windows parity reports.

Reads, from the repository root:
  Windows/FIX_TASKS.md       (execution-status table at the top)
  Windows/VIEW_PARITY.md     (273 Mac view rows)
  Windows/SETTINGS_PARITY.md (113 preference keys)
  Windows/CORE_PARITY.md     (B22 Swift-test translation table)

Prints one summary block plus the identifiers that are still "未做".
No file is modified.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

STATES = ["未做", "已实现、未验证", "已验证", "本版不做"]
ROOT = Path(__file__).resolve().parents[2]


def split_row(line: str) -> list[str] | None:
    s = line.strip()
    if not s.startswith("|") or not s.endswith("|"):
        return None
    cells = [c.strip() for c in s[1:-1].split("|")]
    if all(re.fullmatch(r":?-{2,}:?", c or "-") for c in cells):
        return None
    return cells


def state_of(text: str) -> str | None:
    """Match a state exactly; longest first so 已实现、未验证 wins over 未做."""
    for state in sorted(STATES, key=len, reverse=True):
        if text == state or text.startswith(state):
            return state
    return None


def count_fix_tasks() -> tuple[dict[str, int], list[str]]:
    path = ROOT / "Windows" / "FIX_TASKS.md"
    counts = {s: 0 for s in STATES}
    todo: list[str] = []
    in_table = False
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## 执行状态"):
            in_table = True
            continue
        if in_table and line.startswith("## "):
            break
        if not in_table:
            continue
        cells = split_row(line)
        if not cells or len(cells) < 2:
            continue
        task = cells[0].strip()
        if not re.fullmatch(r"[0-9A-Za-z\-]+", task):
            continue
        state = state_of(cells[1])
        if state is None:
            continue
        counts[state] += 1
        if state == "未做":
            todo.append(task)
    counts["total"] = sum(counts[s] for s in STATES)
    return counts, todo


def count_markdown_table(path: Path, status_index: int, label_index: int) -> tuple[dict[str, int], list[str]]:
    counts = {s: 0 for s in STATES}
    todo: list[str] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        cells = split_row(line)
        if not cells or len(cells) <= status_index:
            continue
        state = state_of(cells[status_index])
        if state is None:
            continue
        counts[state] += 1
        if state == "未做":
            todo.append(cells[label_index])
    counts["total"] = sum(counts[s] for s in STATES)
    return counts, todo


def count_core_parity() -> tuple[int, int, int]:
    path = ROOT / "Windows" / "CORE_PARITY.md"
    total = translated = files = 0
    in_table = False
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## B22"):
            in_table = True
            continue
        if in_table and line.startswith("## "):
            break
        if not in_table:
            continue
        cells = split_row(line)
        if not cells or len(cells) != 3:
            continue
        try:
            swift_n = int(cells[1])
            done_n = int(cells[2])
        except ValueError:
            continue
        total += swift_n
        translated += done_n
        files += 1
    return files, translated, total


def main() -> int:
    fix, todo = count_fix_tasks()
    views, view_todo = count_markdown_table(ROOT / "Windows" / "VIEW_PARITY.md", 4, 1)
    keys, key_todo = count_markdown_table(ROOT / "Windows" / "SETTINGS_PARITY.md", 4, 0)
    core_files, core_done, core_total = count_core_parity()

    def line(label: str, counts: dict[str, int]) -> str:
        parts = " ".join(f"{s}={counts[s]}" for s in STATES)
        return f"{label}: total={counts['total']} {parts}"

    print(line("FIX_TASKS", fix))
    print(f"  FIX_TASKS 未做: {' '.join(todo) if todo else '-'}")
    print(line("VIEW_PARITY", views))
    print(line("SETTINGS_PARITY", keys))
    print(f"CORE_PARITY: swift_test_files={core_files} translated={core_done}/{core_total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
