#!/bin/bash
# Renders the README screenshots on this Mac from the app's demo mode (sample data,
# no audio, no network, no model calls). The app draws its own window into PNG files,
# so no Screen Recording permission is needed.
#
# Usage: scripts/capture-screenshots.sh [path/to/InterviewHacker.app]
#        (defaults to dist/InterviewHacker.app from scripts/build-app.sh)
# Output: docs/screenshots/v2/<page>-<light|dark>[-en].png, at most 1600 px wide.
#
# The capture runs a copy with a separate bundle identifier and a temporary data
# folder, so the installed app's settings and data are never touched.
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
source_app="${1:-$project_root/dist/InterviewHacker.app}"
[[ -d "$source_app" ]] || { echo "No app at $source_app — run scripts/build-app.sh first" >&2; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
app="$work/InterviewHacker-ci.app"
cp -R "$source_app" "$app"
plutil -replace CFBundleIdentifier -string com.interviewhacker.app.ci "$app/Contents/Info.plist"
codesign --force --sign - --identifier com.interviewhacker.app.ci "$app" >/dev/null 2>&1

defaults delete com.interviewhacker.app.ci >/dev/null 2>&1 || true
out="$project_root/docs/screenshots/v2"
mkdir -p "$out"

# capture <name> <section> <appearance> <language> [defaults]
capture() {
    local name="$1" section="$2" appearance="$3" language="$4" defaults="${5:-}"
    local suffix="" png
    [[ "$language" == en ]] && suffix="-en"
    png="$out/$name-$appearance$suffix.png"
    rm -f "$png"
    local attempt
    for attempt in 1 2 3; do
    # A sleeping display draws nothing; declare the user active to wake it.
    caffeinate -u -t 3 >/dev/null 2>&1 &
    pkill -9 -f "InterviewHacker-ci.app/Contents/MacOS" 2>/dev/null || true
    # Every capture ends with a kill; without this the next launch tries to restore
    # windows after an "unexpected quit" and never draws the demo window.
    rm -rf "$HOME/Library/Saved Application State/com.interviewhacker.app.ci.savedState"
    INTERVIEWHACKER_DEMO=1 \
    INTERVIEWHACKER_DATA_DIR="$work/data" \
    INTERVIEWHACKER_DEMO_SECTION="$section" \
    INTERVIEWHACKER_DEMO_APPEARANCE="$appearance" \
    INTERVIEWHACKER_DEMO_DEFAULTS="$defaults" \
    INTERVIEWHACKER_DEMO_VOICE_READY=1 \
    INTERVIEWHACKER_DEMO_SNAPSHOT="$png" \
    "$app/Contents/MacOS/InterviewHacker" -AppleLanguages "($language)" \
        -ApplePersistenceIgnoreState YES -NSQuitAlwaysKeepsWindows NO >"$work/$name.log" 2>&1 &
    local pid=$!
    for _ in $(seq 1 60); do [[ -s "$png" ]] && break; sleep 0.5; done
    kill "$pid" 2>/dev/null || true
    sleep 0.5
    kill -9 "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    [[ -s "$png" ]] && break
    cp "$work/$name.log" "$project_root/.ih-loop/capture-$name-$appearance$suffix-$attempt.log" 2>/dev/null || true
    sleep 1
    done
    if [[ -s "$png" ]]; then
        sips -Z 1600 "$png" >/dev/null
        echo "ok   $(basename "$png")"
    else
        echo "FAIL $(basename "$png")"; tail -n 20 "$work/$name.log" || true
        report="$(ls -t "$HOME"/Library/Logs/DiagnosticReports/InterviewHacker* 2>/dev/null | head -1 || true)"
        [[ -n "$report" ]] && echo "     latest crash report: $report" && head -n 40 "$report"
        pgrep -fl "InterviewHacker" || true
    fi
    sleep 0.5
}

# README uses the light appearance only; pass APPEARANCES="light dark" for both.
# Keep the display awake for the whole run.
caffeinate -d -u -w $$ >/dev/null 2>&1 &
for appearance in ${APPEARANCES:-light}; do
    capture home        home       "$appearance" zh-Hans
    capture prepare     prepare    "$appearance" zh-Hans
    capture live        live       "$appearance" zh-Hans
    capture live-notes  live-notes "$appearance" zh-Hans
    capture review      meetings   "$appearance" zh-Hans
    capture notes-work  meetings   "$appearance" zh-Hans "demoNotes=work"
    capture library     library    "$appearance" zh-Hans
    capture settings    settings   "$appearance" zh-Hans
    capture settings-notes settings "$appearance" zh-Hans "settingsGroup=纪要"
done
for page in home:home prepare:prepare live:live review:meetings; do
    capture "${page%%:*}" "${page##*:}" light en
done
ls -la "$out"
