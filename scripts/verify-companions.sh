#!/bin/bash
# Protocol checks are runnable with CLT; release/CI also requires a real iOS SDK.
set -euo pipefail
ih_companion_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ih_companion_root"
ih_companion_mode="${1:---require-ios}"
case "$ih_companion_mode" in
  --require-ios|--protocols-only) ;;
  *) echo 'Usage: scripts/verify-companions.sh [--require-ios|--protocols-only]' >&2; exit 2 ;;
esac
npm ci --prefix CompanionRelay
npm --prefix CompanionRelay test
node Tools/Validation/Companion/browser.test.mjs
git diff --exit-code -- Resources/Companion/phone.js
swift test --package-path Mobile/Shared --disable-sandbox
if [[ "$ih_companion_mode" == --require-ios ]]; then
  xcodebuild -version
  xcodebuild -project Mobile/iOS/NextThoughtCompanion.xcodeproj -scheme NextThoughtCompanion \
    -destination 'generic/platform=iOS Simulator' -derivedDataPath .build/companion-ios \
    CODE_SIGNING_ALLOWED=NO build
else
  echo 'Protocols passed; iOS UI build was explicitly skipped and is NOT verified.'
fi
