#!/bin/bash
# Development preview: builds a debug binary, wraps it in a disposable bundle with
# its own bundle identifier (ad-hoc signed, separate preferences and TCC entries),
# and optionally renders demo pages into PNG files.
#
#   scripts/dev/preview.sh                       build the bundle only
#   scripts/dev/preview.sh shot <section> <light|dark> <out.png> [key=value;…] [language]
#   scripts/dev/preview.sh run [section] [light|dark]     launch the demo interactively
#
# The official app, its data folder and its signing identity are never touched.
set -euo pipefail
project_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$project_root"
# Each checkout (git worktrees included) gets its own bundle identifier, so previews
# running side by side never share preferences or kill each other.
bundle_id="com.nextthought.app.ci.dev$(printf '%s' "$project_root" | cksum | cut -d' ' -f1)"
app="$project_root/.local/NextThoughtDev.app"
data="$project_root/.local/dev-data"
scratch="$project_root/.build/swift"

build() {
    swift build --disable-sandbox --scratch-path "$scratch" 2>&1 | grep -E "error|warning: unre|Compiling|Build complete|Linking" | grep -vE "^\[[0-9]+/[0-9]+\] Compiling" | tail -40
    [[ "${PIPESTATUS[0]}" == 0 ]] || { echo "BUILD FAILED" >&2; exit 1; }
    local binary; binary="$(swift build --disable-sandbox --scratch-path "$scratch" --show-bin-path)/NextThought"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp "$binary" "$app/Contents/MacOS/NextThought"
    cp Resources/Info.plist "$app/Contents/Info.plist"
    /usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $bundle_id" "$app/Contents/Info.plist"
    /usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName NextThought Dev' "$app/Contents/Info.plist"
    /usr/libexec/PlistBuddy -c 'Set :CFBundleName NextThought Dev' "$app/Contents/Info.plist"
    if [[ ! -d "$app/Contents/Resources/models" || "${PREVIEW_RESOURCES:-0}" == 1 ]]; then
        for resource in Companion models; do
            rsync -a "Resources/$resource/" "$app/Contents/Resources/$resource/"
        done
    fi
    scripts/compile-strings.sh "$app/Contents/Resources" >/dev/null
    codesign --force --sign - --identifier "$bundle_id" "$app" >/dev/null 2>&1
}

# Renders off screen: the app is launched hidden, never activated, and draws its demo
# window without showing it, so nothing appears on any display while you work.
shot() {
    local section="$1" appearance="$2" png="$3" defaults="${4:-}" language="${5:-zh-Hans}"
    rm -f "$png"
    pkill -9 -f "$app/Contents/MacOS" 2>/dev/null || true
    rm -rf "$HOME/Library/Saved Application State/$bundle_id.savedState"
    local extra=()
    for pair in ${PREVIEW_ENV:-}; do extra+=(--env "$pair"); done
    open -j -g -n "$app" \
        --env NEXTTHOUGHT_DEMO=1 --env "NEXTTHOUGHT_DATA_DIR=$data" --env "NEXTTHOUGHT_DEMO_SECTION=$section" \
        --env "NEXTTHOUGHT_DEMO_APPEARANCE=$appearance" --env "NEXTTHOUGHT_DEMO_DEFAULTS=$defaults" \
        --env NEXTTHOUGHT_DEMO_VOICE_READY=1 --env "NEXTTHOUGHT_DEMO_SNAPSHOT=$png" \
        --env "NEXTTHOUGHT_DEMO_FRAME=${PREVIEW_FRAME:-0,0,1120,820}" --env NEXTTHOUGHT_DEMO_HIDDEN=1 \
        ${extra[@]+"${extra[@]}"} \
        --args -AppleLanguages "($language)" -ApplePersistenceIgnoreState YES -NSQuitAlwaysKeepsWindows NO
    for _ in $(seq 1 48); do [[ -s "$png" ]] && break; sleep 0.25; done
    sleep 0.6
    pkill -9 -f "$app/Contents/MacOS" 2>/dev/null || true
    [[ -s "$png" ]] && echo "ok $png" || echo "FAIL $png"
}

case "${1:-build}" in
    build) build ;;
    shot) shift; shot "$@" ;;
    run)
        pkill -9 -f "$app/Contents/MacOS" 2>/dev/null || true
        # shellcheck disable=SC2086
        env NEXTTHOUGHT_DEMO=1 NEXTTHOUGHT_DATA_DIR="$data" NEXTTHOUGHT_DEMO_SECTION="${2:-home}" \
        NEXTTHOUGHT_DEMO_APPEARANCE="${3:-light}" NEXTTHOUGHT_DEMO_VOICE_READY=1 ${PREVIEW_ENV:-} \
        "$app/Contents/MacOS/NextThought" -ApplePersistenceIgnoreState YES >"$project_root/.local/dev-run.log" 2>&1 &
        echo "pid $!" ;;
    *) echo "usage: preview.sh [build|shot|run]" >&2; exit 2 ;;
esac
