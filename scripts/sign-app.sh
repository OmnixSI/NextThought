#!/bin/bash
set -euo pipefail

ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_app="${1:-$ih_root/dist/InterviewHacker.app}"
ih_runtime="$ih_app/Contents/Resources/whisper-server"
if [[ -x "$ih_app/Contents/Helpers/whisper-server" ]]; then
    ih_runtime="$ih_app/Contents/Helpers/whisper-server"
fi
ih_identity="${INTERVIEWHACKER_SIGNING_IDENTITY:-}"
ih_args=()
ih_local=0
ih_entitlements="$ih_root/Resources/InterviewHacker.entitlements"

# Ad-hoc signing for CI release builds without a certificate. macOS will ask the
# user to approve the app once ("Open Anyway"), and privacy permissions must be
# granted again after each update because the code hash changes.
if [[ "$ih_identity" == "-" ]]; then
    codesign --force --sign - --identifier com.interviewhacker.whisper-server "$ih_app/Contents/Helpers/whisper-server"
    codesign --force --sign - --identifier com.interviewhacker.app "$ih_app"
    codesign --verify --deep --strict "$ih_app"
    echo "Signature: ad-hoc (not notarized)"
    exit 0
fi

if [[ -z "$ih_identity" ]]; then
    "$ih_root/scripts/setup-local-signing.sh"
    IFS= read -r ih_identity < "$ih_root/.local-signing/identity-sha1"
    IFS= read -r ih_password < "$ih_root/.local-signing/keychain-password"
    ih_keychain="$ih_root/.local-signing/signing.keychain-db"
    security unlock-keychain -p "$ih_password" "$ih_keychain"
    ih_args=(--keychain "$ih_keychain" --timestamp=none)
    ih_local=1
elif [[ -n "${INTERVIEWHACKER_SIGNING_KEYCHAIN:-}" ]]; then
    ih_args=(--keychain "$INTERVIEWHACKER_SIGNING_KEYCHAIN")
fi
# Developer ID builds use the hardened runtime (required for notarization).
if [[ "${INTERVIEWHACKER_HARDENED_RUNTIME:-0}" == 1 ]]; then
    ih_args+=(--options runtime --timestamp)
fi

if [[ ! -x "$ih_runtime" ]]; then
    echo "The bundled Whisper runtime is missing: $ih_runtime" >&2
    exit 1
fi

if [[ "$ih_local" == 1 ]]; then
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier com.interviewhacker.whisper-server \
        --requirements "=designated => identifier \"com.interviewhacker.whisper-server\" and certificate leaf = H\"$ih_identity\"" \
        "$ih_runtime"
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --requirements "=designated => identifier \"com.interviewhacker.app\" and certificate leaf = H\"$ih_identity\"" \
        "$ih_app"
else
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier com.interviewhacker.whisper-server "$ih_runtime"
    if [[ "${INTERVIEWHACKER_HARDENED_RUNTIME:-0}" == 1 ]]; then
        codesign --force --sign "$ih_identity" "${ih_args[@]}" --entitlements "$ih_entitlements" "$ih_app"
    else
        codesign --force --sign "$ih_identity" "${ih_args[@]}" "$ih_app"
    fi
fi

codesign --verify --strict "$ih_runtime"
codesign --verify --deep --strict "$ih_app"
ih_requirement="$(codesign -dr - "$ih_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
if [[ -z "$ih_requirement" || "$ih_requirement" == *cdhash* ]]; then
    echo "The app has no stable designated requirement; refusing to package it." >&2
    exit 1
fi
echo "Signature: $ih_identity"
echo "$ih_requirement"
