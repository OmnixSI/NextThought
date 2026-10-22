#!/bin/bash
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
build_root="$project_root/.build"
app_path="$project_root/dist/InterviewHacker.app"
archive_path="$project_root/dist/InterviewHacker-macOS.zip"
clang_cache="$build_root/clang-module-cache"
swift_cache="$build_root/swift-module-cache"

mkdir -p "$clang_cache" "$swift_cache" "$project_root/dist"
export CLANG_MODULE_CACHE_PATH="$clang_cache"
export SWIFT_MODULE_CACHE_PATH="$swift_cache"

cd "$project_root"
swift build --disable-sandbox -c release --scratch-path "$build_root/swift"
binary_path="$(swift build --disable-sandbox --show-bin-path -c release --scratch-path "$build_root/swift")/InterviewHacker"
if [[ ! -x "$binary_path" ]]; then
    echo "Could not find the compiled InterviewHacker executable." >&2
    exit 1
fi

rm -rf "$app_path"
mkdir -p "$app_path/Contents/MacOS" "$app_path/Contents/Resources"
cp "$binary_path" "$app_path/Contents/MacOS/InterviewHacker"
cp "$project_root/Resources/Info.plist" "$app_path/Contents/Info.plist"
cp -R "$project_root/Resources/Companion" "$app_path/Contents/Resources/Companion"
cp "$project_root/Resources/ThirdPartyNotices.txt" "$app_path/Contents/Resources/ThirdPartyNotices.txt"
rsync -a --exclude=__pycache__ --exclude='*.pyc' "$project_root/Resources/DocumentRuntime/" "$app_path/Contents/Resources/DocumentRuntime/"
printf 'APPL????' > "$app_path/Contents/PkgInfo"

icon_set="$build_root/AppIcon.iconset"
rm -rf "$icon_set"
if swift "$project_root/scripts/make-icon.swift" "$icon_set" && iconutil -c icns "$icon_set" -o "$app_path/Contents/Resources/AppIcon.icns"; then
    echo "Generated app icon"
else
    echo "warning: could not generate the app icon" >&2
fi

if [[ -n "${INTERVIEWHACKER_VERSION:-}" ]]; then
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $INTERVIEWHACKER_VERSION" "$app_path/Contents/Info.plist"
fi
if [[ -n "${INTERVIEWHACKER_BUILD_NUMBER:-}" ]]; then
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $INTERVIEWHACKER_BUILD_NUMBER" "$app_path/Contents/Info.plist"
fi

xattr -cr "$app_path"
"$project_root/scripts/sign-app.sh" "$app_path"
codesign --verify --deep --strict "$app_path"
lipo "$app_path/Contents/MacOS/InterviewHacker" -verify_arch arm64

if find "$app_path" -type f -iname '*whisper*' -print -quit | grep -q .; then
    echo "The application bundle still contains a Whisper-named file." >&2
    exit 1
fi

rm -f "$archive_path"
ditto -c -k --sequesterRsrc --keepParent "$app_path" "$archive_path"
echo "Built arm64 macOS 14+ app: $app_path"
echo "Created archive: $archive_path"
if [[ "${INTERVIEWHACKER_MAKE_DMG:-0}" == 1 ]]; then
    dmg_root="$build_root/dmg-root"
    rm -rf "$dmg_root" && mkdir -p "$dmg_root"
    cp -R "$app_path" "$dmg_root/"
    ln -s /Applications "$dmg_root/Applications"
    rm -f "$project_root/dist/InterviewHacker.dmg"
    hdiutil create -volname "InterviewHacker" -srcfolder "$dmg_root" -ov -format UDZO "$project_root/dist/InterviewHacker.dmg"
    echo "Created disk image: $project_root/dist/InterviewHacker.dmg"
fi
echo "Distribution: ${INTERVIEWHACKER_SIGNING_IDENTITY:+signed with $INTERVIEWHACKER_SIGNING_IDENTITY}${INTERVIEWHACKER_SIGNING_IDENTITY:-local development build}; not notarized unless the release workflow notarized it."
