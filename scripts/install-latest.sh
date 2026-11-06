#!/bin/bash
# Installs or updates NextThought from GitHub Releases without the browser
# "downloaded from the internet" quarantine flag, so Gatekeeper's "Open Anyway"
# prompt is not needed on every update. Requires the GitHub CLI (`gh auth login`)
# because the repository is private.
#
#   ./scripts/install-latest.sh            # latest release (including betas)
#   ./scripts/install-latest.sh v0.2.0-beta.3
#   ./scripts/install-latest.sh main-latest   # newest build of the main branch
set -euo pipefail
script_dir="$(cd "$(dirname "$0")" && pwd)"
downloaded_app_name="NextThought.app"
installed_app="/Applications/NextThought.app"

# Defaults to the GitHub repository of the current clone; override with NEXTTHOUGHT_REPO=owner/name.
repo="${NEXTTHOUGHT_REPO:-${INTERVIEWHACKER_REPO:-$(cd "$(dirname "$0")/.." && gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || true)}}"
if [[ -z "$repo" ]]; then
    echo "Run this inside a clone of the repository, or set NEXTTHOUGHT_REPO=owner/NextThought." >&2
    exit 1
fi
tag="${1:-}"
if [[ -z "$tag" ]]; then
    # Newest versioned release (betas included); "main-latest" is the rolling build of main.
    tag="$(gh release list --repo "$repo" --limit 20 --json tagName -q '[.[] | select(.tagName != "main-latest")][0].tagName')"
fi
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Downloading NextThought $tag…"
gh release download "$tag" --repo "$repo" --pattern '*-macOS-arm64.zip' --pattern 'SHA256SUMS.txt' --dir "$work"
(cd "$work" && grep -- '-macOS-arm64.zip' SHA256SUMS.txt | shasum -a 256 -c -)
ditto -x -k "$work"/NextThought-*-macOS-arm64.zip "$work/app"

downloaded_app="$work/app/$downloaded_app_name"
if [[ ! -d "$downloaded_app" ]]; then
    echo "The downloaded archive does not contain $downloaded_app_name." >&2
    exit 1
fi

# Release archives can use a different certificate. Re-sign the extracted app
# with this Mac's bound identity before it can replace the installed copy.
"$script_dir/sign-app.sh" "$downloaded_app"
codesign --verify --deep --strict "$downloaded_app"
new_requirement="$(codesign -dr - "$downloaded_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
if [[ -z "$new_requirement" ]]; then
    echo "Could not verify the downloaded app's designated requirement." >&2
    exit 1
fi

if [[ -d "$installed_app" ]]; then
    current_requirement="$(codesign -dr - "$installed_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
    if [[ -z "$current_requirement" || "$current_requirement" != "$new_requirement" ]]; then
        echo "Refusing to replace the installed app because its signing identity differs from this Mac's Local Signing identity." >&2
        echo "Bind the installed app once in Settings, grant the requested permissions, then retry the update." >&2
        exit 1
    fi
fi

if pgrep -xq NextThought; then
    echo "Quitting the running NextThought…"
    osascript -e 'quit app "NextThought"' || true
    sleep 2
fi
rm -rf "$installed_app"
ditto "$downloaded_app" "$installed_app"
codesign --verify --deep --strict "$installed_app"
installed_requirement="$(codesign -dr - "$installed_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
if [[ "$installed_requirement" != "$new_requirement" ]]; then
    echo "Installed app signature does not match the verified Local Signing identity." >&2
    exit 1
fi
codesign -dv "$installed_app" 2>&1 | grep -E 'Authority|Signature' || true
echo "Installed $tag. Opening…"
open "$installed_app"
