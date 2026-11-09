#!/bin/bash
# One-time setup so GitHub release builds use a FIXED signing identity.
# macOS keeps screen-recording / microphone permission across updates only when
# the signing identity stays the same. Run this once on your Mac:
#
#   ./scripts/setup-release-signing.sh
#
# It reuses (or creates) a self-signed release identity in .local-signing/, exports
# it as a password-protected .p12 and stores it as GitHub Actions secrets with the
# GitHub CLI (`gh auth login` first). Local builds use the app-bound Local Signing
# identity in Application Support; its private key stays on this Mac.
set -euo pipefail
umask 077

ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_dir="$ih_root/.local-signing"
"$ih_root/scripts/setup-local-signing.sh"

IFS= read -r ih_identity < "$ih_dir/identity-sha1"
IFS= read -r ih_keychain_password < "$ih_dir/keychain-password"
ih_keychain="$ih_dir/signing.keychain-db"
ih_tmp="$(mktemp -d)"
trap 'rm -rf "$ih_tmp"' EXIT

ih_p12_password="$(/usr/bin/openssl rand -hex 16)"
security unlock-keychain -p "$ih_keychain_password" "$ih_keychain"
# Exports only the release identity from the dedicated signing keychain.
security export -k "$ih_keychain" -t identities -f pkcs12 -P "$ih_p12_password" -o "$ih_tmp/identity.p12"
ih_p12_base64="$(base64 -i "$ih_tmp/identity.p12")"

if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
    repo="${NEXTTHOUGHT_REPO:-${INTERVIEWHACKER_REPO:-$(gh repo view --json nameWithOwner -q .nameWithOwner)}}"
    printf '%s' "$ih_p12_base64" | gh secret set MACOS_CERTIFICATE_P12_BASE64 --repo "$repo"
    printf '%s' "$ih_p12_password" | gh secret set MACOS_CERTIFICATE_PASSWORD --repo "$repo"
    printf '%s' "$ih_identity" | gh secret set MACOS_SIGNING_IDENTITY --repo "$repo"
    echo "Saved signing secrets to $repo. Future releases are signed with $ih_identity."
else
    echo "GitHub CLI not available or not logged in."
    echo "Add these repository secrets manually (Settings → Secrets and variables → Actions):"
    echo "  MACOS_SIGNING_IDENTITY       = $ih_identity"
    echo "  MACOS_CERTIFICATE_PASSWORD   = $ih_p12_password"
    echo "  MACOS_CERTIFICATE_P12_BASE64 = (copied to the clipboard)"
    printf '%s' "$ih_p12_base64" | pbcopy
fi
echo "Keep .local-signing/ backed up: a new identity would make macOS ask for permissions again."
