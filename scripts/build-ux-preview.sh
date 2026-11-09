#!/bin/bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
configuration="${NEXTTHOUGHT_REVIEW_CONFIGURATION:-release}"
swift build --disable-sandbox -c "$configuration"
binary_dir="$(swift build --disable-sandbox -c "$configuration" --show-bin-path)"
preview="$project_root/.local/NextThoughtUXReview.app"
# Recreate the disposable bundle so a renamed executable cannot remain signed inside it.
rm -rf "$preview"
mkdir -p "$preview/Contents/MacOS" "$preview/Contents/Resources"
cp "$binary_dir/NextThought" "$preview/Contents/MacOS/NextThought"
cp Resources/Info.plist "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier com.nextthought.app.ci.ux250' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Add :NextThoughtReviewDemo bool true' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Add :NextThoughtReviewMode bool true' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName NextThought UX Review' "$preview/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleName NextThought UX Review' "$preview/Contents/Info.plist"
for resource in Companion DocumentRuntime models; do
    rsync -a --exclude=__pycache__ --exclude='*.pyc' "Resources/$resource/" "$preview/Contents/Resources/$resource/"
done
cp Resources/ThirdPartyNotices.txt "$preview/Contents/Resources/"
scripts/compile-strings.sh "$preview/Contents/Resources"
codesign --force --sign - --identifier com.nextthought.app.ci.ux250 "$preview"
codesign --verify --deep --strict "$preview"
printf '%s\n' "$preview"
