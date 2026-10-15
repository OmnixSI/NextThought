#!/bin/bash
set -euo pipefail

ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_app="${1:-$ih_root/dist/InterviewHacker.app}"
ih_identity="${INTERVIEWHACKER_SIGNING_IDENTITY:-}"
ih_args=()
ih_local=0
ih_entitlements="$ih_root/Resources/InterviewHacker.entitlements"

if [[ ! -d "$ih_app" || ! -f "$ih_app/Contents/MacOS/InterviewHacker" ]]; then
    echo "The InterviewHacker app bundle is incomplete: $ih_app" >&2
    exit 1
fi

# CI builds without a certificate use an ad-hoc signature.
if [[ "$ih_identity" == "-" ]]; then
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

if [[ "${INTERVIEWHACKER_HARDENED_RUNTIME:-0}" == 1 ]]; then
    ih_args+=(--options runtime --timestamp)
fi

if [[ "$ih_local" == 1 ]]; then
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier com.interviewhacker.app \
        --requirements "=designated => identifier \"com.interviewhacker.app\" and certificate leaf = H\"$ih_identity\"" \
        "$ih_app"
elif [[ "${INTERVIEWHACKER_HARDENED_RUNTIME:-0}" == 1 ]]; then
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier com.interviewhacker.app --entitlements "$ih_entitlements" "$ih_app"
else
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier com.interviewhacker.app "$ih_app"
fi

codesign --verify --deep --strict "$ih_app"
ih_requirement="$(codesign -dr - "$ih_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
if [[ -z "$ih_requirement" || "$ih_requirement" == *cdhash* ]]; then
    echo "The app has no stable designated requirement; refusing to package it." >&2
    exit 1
fi
echo "Signature: $ih_identity"
echo "$ih_requirement"
