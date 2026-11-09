#!/bin/bash
# Maintainer helper: lets a remote assistant iterate on builds and cut a release
# on this Mac without anyone typing further commands.
#
# Run once from a dedicated clone:  bash scripts/dev/build-loop.sh
# It then waits (Ctrl+C to stop) for trigger files in .ih-loop/:
#   .ih-loop/rebuild         pull the current branch, then build, test and package
#   .ih-loop/release         contains a tag (e.g. v2.0.0): check out main, build,
#                            package and publish that GitHub release
#   .ih-loop/screenshots     pull, build the app, render the README screenshots into
#                            docs/screenshots/v2 (scripts/capture-screenshots.sh)
# Output: .ih-loop/<build|release|screenshots>.log + .status
# (status: "running", or the exit code when finished).
set -u
cd "$(dirname "$0")/../.." || exit 1
grep -qx '.ih-loop/' .git/info/exclude 2>/dev/null || echo '.ih-loop/' >> .git/info/exclude
grep -qx 'dist/' .git/info/exclude 2>/dev/null || echo 'dist/' >> .git/info/exclude
mkdir -p .ih-loop
touch .ih-loop/rebuild
echo "Waiting for triggers in $(pwd)/.ih-loop (Ctrl+C to stop)"

build() {
    git pull -q --ff-only &&
    swift build --disable-sandbox &&
    swift test --disable-sandbox &&
    scripts/build-app.sh
}

release() {
    local tag="$1" version="${1#v}" asset
    git fetch -q origin main --tags &&
    git checkout -q main && git pull -q --ff-only &&
    test -f "docs/releases/$tag.md" &&
    swift test --disable-sandbox &&
    rm -rf dist && mkdir -p dist &&
    asset=$(python3 -c 'import json; print(json.load(open("Resources/DocumentRuntime/runtime-manifest.json"))["asset"])') &&
    gh release download v1.7.0 --pattern "$asset" --dir dist --clobber &&
    cp Resources/DocumentRuntime/runtime-manifest.json dist/runtime-manifest.json &&
    NEXTTHOUGHT_VERSION="$version" NEXTTHOUGHT_MAKE_DMG=1 scripts/build-app.sh &&
    python3 Tools/DocumentRuntime/release_assets.py --tag "$tag" --commit "$(git rev-parse HEAD)" &&
    { git rev-parse -q --verify "refs/tags/$tag" >/dev/null || git tag "$tag"; } &&
    git push -q origin "$tag" &&
    { gh release view "$tag" >/dev/null 2>&1 ||
      gh release create "$tag" --draft --target "$(git rev-parse HEAD)" --title "NextThought $tag" --notes-file "docs/releases/$tag.md"; } &&
    gh release upload "$tag" dist/NextThought-*-macOS-arm64.* dist/SHA256SUMS.txt dist/update.json dist/runtime-manifest.json --clobber &&
    rm -rf .ih-loop/verify && mkdir -p .ih-loop/verify &&
    gh release download "$tag" --dir .ih-loop/verify --clobber &&
    (cd .ih-loop/verify && shasum -a 256 -c SHA256SUMS.txt) &&
    gh release edit "$tag" --draft=false --latest
}

screenshots() {
    git pull -q --ff-only &&
    scripts/build-app.sh &&
    scripts/capture-screenshots.sh
}

while true; do
    if [ -f .ih-loop/rebuild ]; then
        rm -f .ih-loop/rebuild
        echo running > .ih-loop/build.status
        build > .ih-loop/build.log 2>&1
        echo $? > .ih-loop/build.status
        echo "$(date '+%H:%M:%S') build finished: $(cat .ih-loop/build.status)"
    fi
    if [ -f .ih-loop/release ]; then
        tag=$(tr -d '[:space:]' < .ih-loop/release)
        rm -f .ih-loop/release
        echo running > .ih-loop/release.status
        release "$tag" > .ih-loop/release.log 2>&1
        echo $? > .ih-loop/release.status
        echo "$(date '+%H:%M:%S') release $tag finished: $(cat .ih-loop/release.status)"
    fi
    if [ -f .ih-loop/screenshots ]; then
        rm -f .ih-loop/screenshots
        echo running > .ih-loop/screenshots.status
        screenshots > .ih-loop/screenshots.log 2>&1
        echo $? > .ih-loop/screenshots.status
        echo "$(date '+%H:%M:%S') screenshots finished: $(cat .ih-loop/screenshots.status)"
    fi
    sleep 5
done
