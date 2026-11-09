#!/bin/bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
swift build --disable-sandbox -c release
binary_dir="$(swift build --disable-sandbox -c release --show-bin-path)"
preview="$project_root/.local/AcademicUXReview.app"
mkdir -p "$preview/Contents/MacOS" "$preview/Contents/Resources"
cp "$binary_dir/InterviewHacker" "$preview/Contents/MacOS/InterviewHacker"
cp Resources/Info.plist "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier com.interviewhacker.app.ci.academicux' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Add :InterviewHackerReviewDemo bool true' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Add :InterviewHackerReviewMode bool true' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleName Academic UX Review' "$preview/Contents/Info.plist"
for resource in Companion DocumentRuntime models; do
    rsync -a --exclude=__pycache__ --exclude='*.pyc' "Resources/$resource/" "$preview/Contents/Resources/$resource/"
done
cp Resources/ThirdPartyNotices.txt "$preview/Contents/Resources/"
scripts/compile-strings.sh "$preview/Contents/Resources"
codesign --force --sign - --identifier com.interviewhacker.app.ci.academicux "$preview"
codesign --verify --deep --strict "$preview"
printf '%s\n' "$preview"
