#!/bin/bash
set -euo pipefail

ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_app="${1:-$ih_root/dist/NextThought.app}"
ih_identity="${NEXTTHOUGHT_SIGNING_IDENTITY:-${INTERVIEWHACKER_SIGNING_IDENTITY:-}}"
ih_bundle_id="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$ih_app/Contents/Info.plist" 2>/dev/null || true)"
ih_signing_dir="${HOME}/Library/Application Support/InterviewHacker/Signing"
ih_args=()
ih_local=0
ih_entitlements="$ih_root/Resources/NextThought.entitlements"

if [[ ! -d "$ih_app" || ! -f "$ih_app/Contents/MacOS/NextThought" ]]; then
    echo "The NextThought app bundle is incomplete: $ih_app" >&2
    exit 1
fi
if [[ "$ih_bundle_id" != "com.nextthought.app" ]]; then
    echo "Refusing to sign an unexpected bundle identifier: ${ih_bundle_id:-missing}." >&2
    exit 1
fi

# On this Mac, always use the identity bound in the app. CI may use its
# separately configured release identity, but local overrides must not silently
# change the signature that macOS uses for privacy permissions.
if [[ "${GITHUB_ACTIONS:-}" != "true" && -n "$ih_identity" ]]; then
    ih_identity_file="$ih_signing_dir/identity-sha1"
    if [[ "$ih_identity" == "-" || ! -f "$ih_identity_file" ]]; then
        echo "Local builds must use the app-bound Local Signing identity. Bind the app in Settings before building." >&2
        exit 1
    fi
    ih_saved_identity="$(< "$ih_identity_file")"
    ih_requested_identity="$(printf '%s' "$ih_identity" | tr '[:upper:]' '[:lower:]')"
    ih_saved_identity="$(printf '%s' "$ih_saved_identity" | tr '[:upper:]' '[:lower:]')"
    if [[ "$ih_requested_identity" != "$ih_saved_identity" ]]; then
        echo "Refusing to sign a local build with a different identity. Use the app-bound Local Signing certificate." >&2
        exit 1
    fi
    ih_identity=""
fi

# CI builds without a certificate use an ad-hoc signature.
if [[ "$ih_identity" == "-" ]]; then
    if [[ "${GITHUB_ACTIONS:-}" != "true" ]]; then
        echo "Ad-hoc signing is only allowed in GitHub Actions; local builds must use Local Signing." >&2
        exit 1
    fi
    codesign --force --sign - --identifier "$ih_bundle_id" "$ih_app"
    codesign --verify --deep --strict "$ih_app"
    echo "Signature: ad-hoc (not notarized)"
    exit 0
fi

if [[ -z "$ih_identity" ]]; then
    ih_identity_file="$ih_signing_dir/identity-sha1"
    ih_password_file="$ih_signing_dir/keychain-password"
    ih_keychain="$ih_signing_dir/signing.keychain-db"
    ih_certificate="$ih_signing_dir/certificate.pem"

    if [[ ! -f "$ih_identity_file" || ! -f "$ih_password_file" || ! -f "$ih_keychain" || ! -f "$ih_certificate" ]]; then
        echo "No complete Local Signing identity was found. Bind the app in Settings before building." >&2
        exit 1
    fi

    # LocalSigning writes these files without a trailing newline. Command
    # substitution reads both formats without letting `set -e` exit on EOF.
    ih_identity="$(< "$ih_identity_file")"
    ih_password="$(< "$ih_password_file")"
    if [[ ! "$ih_identity" =~ ^[[:xdigit:]]{40}$ || -z "$ih_password" ]]; then
        echo "The saved Local Signing identity is invalid. Inspect its files in Application Support before retrying." >&2
        exit 1
    fi

    ih_certificate_identity="$(/usr/bin/openssl x509 -in "$ih_certificate" -noout -fingerprint -sha1 | sed 's/.*=//; s/://g' | tr '[:upper:]' '[:lower:]')"
    ih_saved_identity="$(printf '%s' "$ih_identity" | tr '[:upper:]' '[:lower:]')"
    if [[ "$ih_certificate_identity" != "$ih_saved_identity" ]]; then
        echo "The Local Signing certificate does not match its saved identity. Refusing to change the app signature." >&2
        exit 1
    fi

    security unlock-keychain -p "$ih_password" "$ih_keychain"
    ih_identities="$(security find-identity -v -p codesigning "$ih_keychain" 2>&1)"
    if ! printf '%s\n' "$ih_identities" | grep -Fqi "$ih_identity"; then
        echo "The saved Local Signing certificate and private key are not available for signing." >&2
        exit 1
    fi
    ih_args=(--keychain "$ih_keychain" --timestamp=none)
    ih_local=1
elif [[ -n "${NEXTTHOUGHT_SIGNING_KEYCHAIN:-${INTERVIEWHACKER_SIGNING_KEYCHAIN:-}}" ]]; then
    ih_args=(--keychain "${NEXTTHOUGHT_SIGNING_KEYCHAIN:-$INTERVIEWHACKER_SIGNING_KEYCHAIN}")
fi

if [[ "${NEXTTHOUGHT_HARDENED_RUNTIME:-${INTERVIEWHACKER_HARDENED_RUNTIME:-0}}" == 1 ]]; then
    ih_args+=(--options runtime --timestamp)
fi

if [[ "$ih_local" == 1 ]]; then
    ih_requirement_identity="$(printf '%s' "$ih_identity" | tr '[:upper:]' '[:lower:]')"
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier "$ih_bundle_id" \
        --requirements "=designated => identifier \"$ih_bundle_id\" and certificate leaf = H\"$ih_requirement_identity\"" \
        "$ih_app"
elif [[ "${NEXTTHOUGHT_HARDENED_RUNTIME:-${INTERVIEWHACKER_HARDENED_RUNTIME:-0}}" == 1 ]]; then
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier "$ih_bundle_id" --entitlements "$ih_entitlements" "$ih_app"
else
    codesign --force --sign "$ih_identity" "${ih_args[@]}" \
        --identifier "$ih_bundle_id" "$ih_app"
fi

codesign --verify --deep --strict "$ih_app"
ih_requirement="$(codesign -dr - "$ih_app" 2>&1 | sed -n 's/^.*designated => /designated => /p')"
if [[ -z "$ih_requirement" || "$ih_requirement" == *cdhash* ]]; then
    echo "The app has no stable designated requirement; refusing to package it." >&2
    exit 1
fi
echo "Signature: $ih_identity"
echo "$ih_requirement"
