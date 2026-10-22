#!/bin/bash
# Project-contained, isolated review bundle. Does not replace the installed app.
set -euo pipefail
ih_review_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ih_review_root"
mkdir -p .local/stability dist
swift build --disable-sandbox --scratch-path .build/stability -c release
ih_review_binary="$(swift build --disable-sandbox --scratch-path .build/stability -c release --show-bin-path)/InterviewHacker"
ih_review_stage="$(mktemp -d "$ih_review_root/.local/stability/package-XXXXXX")"
mkdir -p "$ih_review_stage/volume"
ih_review_mount="$ih_review_stage/volume"
cleanup() { hdiutil detach "$ih_review_mount" >/dev/null 2>&1 || true; }
trap cleanup EXIT
# codesign needs space for temporary executable copies; 128 MiB was too small.
hdiutil create -size 512m -type SPARSE -fs APFS -volname IHStabilityReview "$ih_review_stage/review.sparseimage" >/dev/null
hdiutil attach "$ih_review_stage/review.sparseimage" -mountpoint "$ih_review_mount" -nobrowse >/dev/null
ih_review_app="$ih_review_mount/InterviewHacker Stability Review.app"
mkdir -p "$ih_review_app/Contents/MacOS" "$ih_review_app/Contents/Resources"
cp "$ih_review_binary" "$ih_review_app/Contents/MacOS/InterviewHacker"
cp Resources/Info.plist "$ih_review_app/Contents/Info.plist"
cp Resources/ThirdPartyNotices.txt "$ih_review_app/Contents/Resources/"
swift scripts/make-icon.swift "$ih_review_stage/AppIcon.iconset"
iconutil -c icns "$ih_review_stage/AppIcon.iconset" -o "$ih_review_app/Contents/Resources/AppIcon.icns"
cp -R Resources/DocumentRuntime Resources/Companion "$ih_review_app/Contents/Resources/"
if [ -d Resources/Design ]; then cp -R Resources/Design "$ih_review_app/Contents/Resources/"; fi
/usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier com.interviewhacker.app.ci.review' "$ih_review_app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleDisplayName InterviewHacker Stability Review' "$ih_review_app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleShortVersionString 1.8.1' "$ih_review_app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Set :CFBundleVersion 1801' "$ih_review_app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Add :InterviewHackerReviewMode bool true' "$ih_review_app/Contents/Info.plist"
printf 'APPL????' > "$ih_review_app/Contents/PkgInfo"
xattr -cr "$ih_review_app"
codesign --force --sign - --identifier com.interviewhacker.app.ci.review "$ih_review_app"
codesign --verify --deep --strict "$ih_review_app"
lipo "$ih_review_app/Contents/MacOS/InterviewHacker" -verify_arch arm64
rm -f dist/InterviewHacker-Stability-Review-macOS.zip
ditto -c -k --keepParent --norsrc --noextattr "$ih_review_app" dist/InterviewHacker-Stability-Review-macOS.zip
# Keep a usable local app beside its own review data directory.
rm -rf 'dist/InterviewHacker Stability Review.app'
ditto --norsrc --noextattr "$ih_review_app" 'dist/InterviewHacker Stability Review.app'
codesign --verify --deep --strict 'dist/InterviewHacker Stability Review.app'
python3 - <<'PY'
import hashlib,json,subprocess
from pathlib import Path
artifact=Path('dist/InterviewHacker-Stability-Review-macOS.zip')
report={'gitCommit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'filename':artifact.name,'sha256':hashlib.sha256(artifact.read_bytes()).hexdigest(),'bytes':artifact.stat().st_size,'signature':'ad-hoc isolated review identity; not notarized','dataDirectory':'alongside app: .interviewhacker-review','status':'draft; physical device acceptance pending'}
Path('dist/review-build.json').write_text(json.dumps(report,indent=2)+'\n')
PY
printf 'Review bundle: %s/dist/InterviewHacker-Stability-Review-macOS.zip\n' "$ih_review_root"
