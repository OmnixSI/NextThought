#!/bin/bash
set -euo pipefail
ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_fixtures="$ih_root/docs/validation/fixtures"
ih_media="$ih_root/.build/InterviewHackerMedia.app"
mkdir -p "$ih_fixtures" "$ih_media/Contents/MacOS" "$ih_media/Contents/Resources" "$ih_root/.build/fixture-cache"
export CLANG_MODULE_CACHE_PATH="$ih_root/.build/fixture-cache"

swiftc "$ih_root/Tools/Validation/GenerateImages.swift" -o "$ih_root/.build/generate-fixture-images" -framework AppKit
"$ih_root/.build/generate-fixture-images" "$ih_fixtures"
swiftc "$ih_root/Tools/Validation/TestMediaApp.swift" -o "$ih_media/Contents/MacOS/InterviewHackerMedia" -framework AppKit -framework AVFoundation
cp "$ih_root/Tools/Validation/Fixtures/"*.wav "$ih_fixtures/"
cp "$ih_fixtures/"*.wav "$ih_fixtures/"*.png "$ih_media/Contents/Resources/"
cat > "$ih_media/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>InterviewHackerMedia</string>
<key>CFBundleIdentifier</key><string>com.interviewhacker.test-media</string>
<key>CFBundleName</key><string>InterviewHackerMedia</string>
<key>CFBundlePackageType</key><string>APPL</string>
</dict></plist>
PLIST
codesign --force --sign - "$ih_media"
echo "Synthetic test media: $ih_media"
echo "Audio fixtures are synthetic recorded speech samples, not human recordings."
