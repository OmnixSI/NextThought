#!/usr/bin/env python3
"""Compile a String Catalog (.xcstrings) into <lang>.lproj/<Table>.strings files.

Fallback for machines with only the Command Line Tools, where Xcode's
`xcstringstool` is not available. Handles plain stringUnit entries, which is all
this project's catalogs use; entries with variations (plurals, devices) are
reported and skipped.

Usage: xcstrings_to_strings.py <Catalog.xcstrings> <output Resources directory>
"""
import json
import pathlib
import sys


def escape(text: str) -> str:
    return (text.replace("\\", "\\\\").replace('"', '\\"')
            .replace("\n", "\\n").replace("\t", "\\t"))


def main() -> int:
    catalog_path, output = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    table = catalog_path.stem
    per_language: dict[str, list[str]] = {}
    skipped = 0
    for key, entry in sorted(catalog.get("strings", {}).items()):
        for language, localization in entry.get("localizations", {}).items():
            unit = localization.get("stringUnit")
            if unit is None:
                skipped += 1
                continue
            if unit.get("state") not in (None, "translated", "needs_review"):
                continue
            per_language.setdefault(language, []).append(
                f'"{escape(key)}" = "{escape(unit.get("value", ""))}";')
    for language, lines in per_language.items():
        folder = output / f"{language}.lproj"
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{table}.strings").write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"{table}: {len(lines)} strings -> {language}.lproj")
    if skipped:
        print(f"warning: {table}: skipped {skipped} entries with variations", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
