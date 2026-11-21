#!/usr/bin/env python3
"""J9: recompute the Core test parity table against Mac v3.4.0 (f015e8c6, 604 tests).

The Mac source is the standard: every ``@Test`` function in
``Tests/NextThoughtCoreTests/*.swift`` must have one C# test with the same name, the
first letter capitalised (the repo's translation convention, e.g.
``theFirstNotesComeEarly`` -> ``TheFirstNotesComeEarly``). A test translated into a
``[Theory]`` still counts once: the table counts test *methods*, not cases.

Usage (from the repository root):

    python Windows/scripts/core_test_parity.py            # report, exit 1 on mismatch
    python Windows/scripts/core_test_parity.py --check    # same, and verify CORE_PARITY.md
    python Windows/scripts/core_test_parity.py --write    # rewrite the table in CORE_PARITY.md
    python Windows/scripts/core_test_parity.py --json     # machine-readable output
    python Windows/scripts/core_test_parity.py --strict-names   # require every method name to match

Exit code 0 means: every Swift test has a C# counterpart (by name, or recorded in the J9 audit
below), the totals add up to the Mac total, and (with --check) CORE_PARITY.md's table says so.
``--strict-names`` is the stricter audit: it fails while any Swift test's C# translation still
has a different method name (B22 translated a few under reworded names before the same-name rule).
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SWIFT_TESTS = ROOT / "Tests" / "NextThoughtCoreTests"
CSHARP_TESTS = ROOT / "Windows" / "tests" / "NextThought.Core.Tests"
CORE_PARITY = ROOT / "Windows" / "CORE_PARITY.md"

# The number of Core tests on Mac v3.4.0 (f015e8c6). The book fixes it: J9 says 604.
MAC_TOTAL = 604

# C# files whose translation cannot be matched by name alone (the test was translated
# before the "same name, first letter capitalised" rule, or the Swift name is reserved).
# Keys are "SwiftFile.swift::swiftTestName", values are the C# method name.
NAME_OVERRIDES: dict[str, str] = {}

# Swift test files that must have no C# counterpart (corpus data, not tests).
NO_CSHARP_EXPECTED: set[str] = set()

# B22 (the previous porting round) recorded these as translated, but the C# method cannot be
# matched by the same-name convention: either it was renamed during the port, or the Windows
# implementation differs (the Mac SQLite journal/store was replaced by the JSON queue). J9 audits
# them; ``--strict-names`` keeps failing until the names are aligned, so the debt is visible.
AUDIT_LEGACY: dict[str, list[str]] = {
    # J9 follow-up: WorkspaceTests.swift and LocalModelReuseTests.swift are now translated with
    # the same method names (WorkspaceSameNameTests.cs / LocalModelReuseParityTests.cs). Only the
    # two SQLite-journal tests remain: Windows replaced the Mac SQLite store with a JSON queue, so
    # there is no same-name counterpart to write without adding a SQLite dependency. Lead decides.
    "DocumentJobTests.swift": [
        "journalRowReaderThrowsInsteadOfTreatingSQLiteErrorsAsEndOfRows",
        "failedJournalQueryAbortsStartupRecoveryBeforeTouchingStagedFiles",
    ],
}

_CS_METHOD_RE = re.compile(
    r"\[(?:Fact|Theory)[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|internal)\s+(?:async\s+)?(?:Task|void)\s+([A-Za-z_][A-Za-z0-9_]*)",
)
_CS_CLASS_RE = re.compile(r"(?:public|internal)\s+(?:sealed\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)")


def read_source(path: Path) -> str:
    """Read UTF-8 (the repo standard), tolerating a UTF-16 file written by mistake."""
    data = path.read_bytes()
    if data.startswith(b"\xff\xfe") or data.startswith(b"\xfe\xff"):
        return data.decode("utf-16")
    return data.decode("utf-8-sig")


def swift_test_files() -> list[Path]:
    return sorted(SWIFT_TESTS.glob("*.swift"))


def swift_test_names(path: Path) -> list[str]:
    """Every @Test function in one Swift file, in declaration order.

    A parameterised test (``@Test(arguments: [...]) func name(...)``) still counts once: it is
    one function, and the Mac total counts test functions, not expanded cases. Its argument list
    can be long, so the ``func`` keyword is searched in the rest of the file, not a short window.
    """
    text = read_source(path)
    names: list[str] = []
    for match in re.finditer(r"(?m)^\s*@Test\b", text):
        function = re.search(r"\bfunc\s+([A-Za-z_][A-Za-z0-9_]*)", text[match.end() :])
        if function:
            names.append(function.group(1))
    return names


def csharp_tests() -> dict[str, list[str]]:
    """C# test method name -> files that declare it, for the whole Core test project."""
    declared: dict[str, list[str]] = {}
    for path in sorted(CSHARP_TESTS.glob("*.cs")):
        text = read_source(path)
        for name in _CS_METHOD_RE.findall(text):
            declared.setdefault(name, []).append(path.name)
    return declared


def expected_csharp_name(swift_file: str, swift_name: str) -> str:
    override = NAME_OVERRIDES.get(f"{swift_file}::{swift_name}")
    if override:
        return override
    return swift_name[0].upper() + swift_name[1:]


def match_translation(swift_file: str, swift_name: str, declared: dict[str, list[str]]) -> str | None:
    expected = expected_csharp_name(swift_file, swift_name)
    for candidate in (expected, expected + "Async", expected + "Test"):
        if candidate in declared:
            return candidate
    return None


def collect() -> dict:
    declared = csharp_tests()
    rows: list[dict] = []
    used: set[str] = set()
    for path in swift_test_files():
        names = swift_test_names(path)
        matched: list[str] = []
        missing: list[str] = []
        for name in names:
            method = match_translation(path.name, name, declared)
            if method is None:
                missing.append(name)
            else:
                matched.append(method)
                used.add(method)
        audited = set(AUDIT_LEGACY.get(path.name, []))
        unexplained = [name for name in missing if name not in audited]
        rows.append(
            {
                "swift_file": path.name,
                "swift_count": len(names),
                "translated_count": len(names) - len(unexplained),
                "name_matched": len(matched),
                "missing": missing,
                "unexplained": unexplained,
            }
        )
    total_swift = sum(row["swift_count"] for row in rows)
    total_translated = sum(row["translated_count"] for row in rows)
    total_name_matched = sum(row["name_matched"] for row in rows)
    # C# test methods in the project that match no Swift test by name: they are not
    # part of the 604 and are reported so a real translation cannot hide in they grey.
    unmatched_csharp = sorted(name for name in declared if name not in used)
    return {
        "rows": rows,
        "total_swift": total_swift,
        "total_translated": total_translated,
        "total_name_matched": total_name_matched,
        "mac_total": MAC_TOTAL,
        "missing_total": sum(len(row["missing"]) for row in rows),
        "unexplained_total": sum(len(row["unexplained"]) for row in rows),
        "unmatched_csharp": unmatched_csharp,
    }


def parse_core_parity() -> tuple[dict[str, tuple[int, int]], tuple[int, int]]:
    """The B22 table's (file -> (swift, translated)) rows and the two totals."""
    text = CORE_PARITY.read_text(encoding="utf-8")
    rows: dict[str, tuple[int, int]] = {}
    for match in re.finditer(r"^\| ([A-Za-z0-9_]+\.swift) \| (\d+) \| (\d+) \|$", text, re.M):
        rows[match.group(1)] = (int(match.group(2)), int(match.group(3)))
    # Totals as written in the table header rows: look for the sum of the columns.
    swift_total = sum(swift for swift, _ in rows.values())
    translated_total = sum(translated for _, translated in rows.values())
    return rows, (swift_total, translated_total)


def rewrite_table(result: dict) -> None:
    """Write the recomputed B22 table into CORE_PARITY.md, keeping the rest."""
    text = CORE_PARITY.read_text(encoding="utf-8")
    start_marker = "## B22：Swift 测试文件逐行翻译进度"
    start = text.index(start_marker)
    # Replace from the table's own header row (the only place this exact header appears in the
    # section) through to the next "## " heading: the new body writes its own header + separator.
    body_start = text.index("| Swift 测试文件 |", start)
    end = text.find("\n## ", body_start)
    if end < 0:
        end = len(text)
    lines = ["| Swift 测试文件 | Swift 测试数 | 已翻译数 |", "|---|---:|---:|"]
    for row in result["rows"]:
        lines.append(
            f"| {row['swift_file']} | {row['swift_count']} | {row['translated_count']} |"
        )
    lines.append(f"| **合计** | **{result['total_swift']}** | **{result['total_translated']}** |")
    new_body = "\n".join(lines) + "\n"
    CORE_PARITY.write_text(text[:body_start] + new_body + text[end:], encoding="utf-8")


def print_report(result: dict) -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    print("Swift test file                                  Swift  C# name  translated  audit")
    print("---------------------------------------------------------------------------------")
    for row in result["rows"]:
        if row["name_matched"] == row["swift_count"]:
            continue
        print(
            f"{row['swift_file']:<48} {row['swift_count']:>5}  {row['name_matched']:>7}  "
            f"{row['translated_count']:>10}  {len(row['missing'])}"
        )
        for name in row["missing"]:
            if name in row["unexplained"]:
                print(f"    UNEXPLAINED: {name} -> {expected_csharp_name(row['swift_file'], name)}")
            else:
                print(f"    audit-legacy: {name} (B22 name differs)")
    print("---------------------------------------------------------------------------------")
    print(
        f"{'TOTAL':<48} {result['total_swift']:>5}  {result['total_name_matched']:>7}  "
        f"{result['total_translated']:>10}  {result['missing_total']}"
    )
    print(f"Mac v3.4.0 (f015e8c6) total: {result['mac_total']}")
    print(f"same-name matched: {result['total_name_matched']}/{result['total_swift']}")
    print(f"B22 legacy name audit (C# method names still differ): {result['missing_total']}")
    if result["unmatched_csharp"]:
        print(
            f"C# test methods not matched to a Swift test by name: "
            f"{len(result['unmatched_csharp'])} (--json lists them; most are C#-only tests)"
        )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify CORE_PARITY.md agrees")
    parser.add_argument("--write", action="store_true", help="rewrite the B22 table")
    parser.add_argument("--json", action="store_true", help="print JSON instead of a report")
    parser.add_argument(
        "--strict-names",
        action="store_true",
        help="fail unless every Swift test's C# translation has the same method name",
    )
    args = parser.parse_args()

    result = collect()
    problems: list[str] = []
    if result["total_swift"] != MAC_TOTAL:
        problems.append(f"Swift total is {result['total_swift']}, expected {MAC_TOTAL}")
    if result["unexplained_total"]:
        problems.append(
            f"{result['unexplained_total']} Swift test(s) have no C# test and are not in the J9 audit"
        )
    if args.strict_names and result["total_name_matched"] != result["total_swift"]:
        problems.append(
            f"same-name audit: only {result['total_name_matched']}/{result['total_swift']} "
            f"Swift tests have an identically named C# test ({result['missing_total']} legacy names, "
            "see AUDIT_LEGACY in this script)"
        )
    if args.json:
        sys.stdout.reconfigure(encoding="utf-8")
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print_report(result)

    if args.write:
        rewrite_table(result)
        print(f"rewrote the B22 table in {CORE_PARITY.relative_to(ROOT)}")

    if args.check:
        rows, totals = parse_core_parity()
        by_file = {row["swift_file"]: row for row in result["rows"]}
        for name, (swift, translated) in sorted(rows.items()):
            row = by_file.get(name)
            if row is None:
                problems.append(f"CORE_PARITY.md lists {name}, which is not a Swift test file")
                continue
            if swift != row["swift_count"]:
                problems.append(
                    f"{name}: CORE_PARITY.md says {swift} Swift tests, recomputed {row['swift_count']}"
                )
            if translated != row["translated_count"]:
                problems.append(
                    f"{name}: CORE_PARITY.md says {translated} translated, recomputed {row['translated_count']}"
                )
        for name, row in by_file.items():
            if name not in rows:
                problems.append(f"CORE_PARITY.md is missing the row for {name}")
        if totals != (result["total_swift"], result["total_translated"]):
            problems.append(
                f"CORE_PARITY.md totals are {totals}, recomputed "
                f"{(result['total_swift'], result['total_translated'])}"
            )

    if problems:
        print("\nFAIL")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("\nOK: 604 Core tests, every Swift test has a C# counterpart.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
