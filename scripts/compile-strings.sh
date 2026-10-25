#!/bin/bash
# Compiles the String Catalogs into <Resources>/<language>.lproj/*.strings so that
# Bundle.main (and SwiftUI's Text("…")) finds the translations in the app bundle.
# Usage: scripts/compile-strings.sh <App>.app/Contents/Resources
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
resources="${1:?usage: compile-strings.sh <App.app/Contents/Resources>}"
mkdir -p "$resources"

for catalog in "$project_root"/Resources/Localization/*.xcstrings; do
    [[ -e "$catalog" ]] || continue
    if ! xcrun xcstringstool compile "$catalog" --output-directory "$resources"; then
        echo "warning: could not compile $(basename "$catalog"); the app will show the Chinese source strings" >&2
    fi
done

# The source language needs its own .lproj even when it has no translated table:
# without it, a Chinese system would fall back to the development region (en).
for language in zh-Hans en; do
    mkdir -p "$resources/$language.lproj"
done
