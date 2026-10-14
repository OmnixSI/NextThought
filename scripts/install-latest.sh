#!/bin/bash
# Installs or updates InterviewHacker from GitHub Releases without the browser
# "downloaded from the internet" quarantine flag, so Gatekeeper's "Open Anyway"
# prompt is not needed on every update. Requires the GitHub CLI (`gh auth login`)
# because the repository is private.
#
#   ./scripts/install-latest.sh            # latest release (including betas)
#   ./scripts/install-latest.sh v0.2.0-beta.3
set -euo pipefail

# Defaults to the GitHub repository of the current clone; override with INTERVIEWHACKER_REPO=owner/name.
repo="${INTERVIEWHACKER_REPO:-$(cd "$(dirname "$0")/.." && gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || true)}"
if [[ -z "$repo" ]]; then
    echo "Run this inside a clone of the repository, or set INTERVIEWHACKER_REPO=owner/InterviewHacker." >&2
    exit 1
fi
tag="${1:-}"
if [[ -z "$tag" ]]; then
    tag="$(gh release list --repo "$repo" --limit 1 --json tagName -q '.[0].tagName')"
fi
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Downloading InterviewHacker $tag…"
gh release download "$tag" --repo "$repo" --pattern '*-macOS-arm64.zip' --pattern 'SHA256SUMS.txt' --dir "$work"
(cd "$work" && grep -- '-macOS-arm64.zip' SHA256SUMS.txt | shasum -a 256 -c -)
ditto -x -k "$work"/InterviewHacker-*-macOS-arm64.zip "$work/app"

if pgrep -xq InterviewHacker; then
    echo "Quitting the running InterviewHacker…"
    osascript -e 'quit app "InterviewHacker"' || true
    sleep 2
fi
rm -rf /Applications/InterviewHacker.app
ditto "$work/app/InterviewHacker.app" /Applications/InterviewHacker.app
xattr -dr com.apple.quarantine /Applications/InterviewHacker.app 2>/dev/null || true
codesign -dv /Applications/InterviewHacker.app 2>&1 | grep -E 'Authority|Signature' || true
echo "Installed $tag. Opening…"
open /Applications/InterviewHacker.app
