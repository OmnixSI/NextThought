#!/bin/bash
set -euo pipefail
umask 077

ih_root="$(cd "$(dirname "$0")/.." && pwd)"
ih_dir="$ih_root/.local-signing"
ih_keychain="$ih_dir/signing.keychain-db"
ih_password_file="$ih_dir/keychain-password"
ih_identity_file="$ih_dir/identity-sha1"

ensure_keychain_visible() {
    ih_search=()
    ih_found=0
    while IFS= read -r ih_path; do
        ih_search+=("$ih_path")
        if [[ "$ih_path" == "$ih_keychain" ]]; then ih_found=1; fi
    done < <(security list-keychains -d user | sed 's/^[[:space:]]*"//; s/"[[:space:]]*$//')
    if [[ "$ih_found" == 0 ]]; then
        ih_search+=("$ih_keychain")
        security list-keychains -d user -s "${ih_search[@]}"
    fi
}

ensure_codesign_trust() {
    if ! security find-identity -v -p codesigning "$ih_keychain" | rg -Fq "$ih_identity"; then
        security add-trusted-cert -r trustRoot -p codeSign -k "$ih_keychain" "$ih_dir/certificate.pem"
    fi
    if ! security find-identity -v -p codesigning "$ih_keychain" | rg -Fq "$ih_identity"; then
        echo "macOS has not accepted code signing trust for this local identity." >&2
        exit 1
    fi
}

if [[ -f "$ih_keychain" && -f "$ih_password_file" && -f "$ih_identity_file" ]]; then
    IFS= read -r ih_password < "$ih_password_file"
    IFS= read -r ih_identity < "$ih_identity_file"
    security unlock-keychain -p "$ih_password" "$ih_keychain"
    ensure_keychain_visible
    if ! security find-identity -p codesigning "$ih_keychain" | rg -Fq "$ih_identity"; then
        echo "The saved local signing identity is missing. Restore .local-signing from backup; do not create a new identity for an existing permission grant." >&2
        exit 1
    fi
    ensure_codesign_trust
    echo "Reusing InterviewHacker local development signing identity: $ih_identity"
    exit 0
fi

if [[ -e "$ih_keychain" || -e "$ih_password_file" || -e "$ih_identity_file" ]]; then
    echo "Local signing setup is incomplete. Inspect .local-signing before retrying; the signing identity must not change silently." >&2
    exit 1
fi

mkdir -p "$ih_dir"
chmod 700 "$ih_dir"
ih_tmp="$(mktemp -d "$ih_dir/import.XXXXXX")"
ih_ready=0
cleanup() {
    rm -rf "$ih_tmp"
    if [[ "$ih_ready" == 0 && ! -e "$ih_keychain" ]]; then
        rm -f "$ih_password_file" "$ih_dir/certificate.pem" "$ih_identity_file"
    fi
}
trap cleanup EXIT
/usr/bin/openssl rand -hex 24 > "$ih_password_file"
IFS= read -r ih_password < "$ih_password_file"

cat > "$ih_tmp/openssl.cnf" <<'CONFIG'
[req]
prompt = no
distinguished_name = subject
x509_extensions = codesign
[subject]
CN = InterviewHacker Local Development
O = InterviewHacker
[codesign]
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,codeSigning
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid
CONFIG

/usr/bin/openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 3650 \
    -config "$ih_tmp/openssl.cnf" -keyout "$ih_tmp/key.pem" \
    -out "$ih_dir/certificate.pem" >/dev/null 2>&1
/usr/bin/openssl pkcs12 -export -name "InterviewHacker Local Development" \
    -inkey "$ih_tmp/key.pem" -in "$ih_dir/certificate.pem" \
    -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES \
    -out "$ih_tmp/identity.p12" -passout "file:$ih_password_file"

security create-keychain -p "$ih_password" "$ih_keychain"
ensure_keychain_visible
security unlock-keychain -p "$ih_password" "$ih_keychain"
security import "$ih_tmp/identity.p12" -k "$ih_keychain" -f pkcs12 \
    -P "$ih_password" -x -T /usr/bin/codesign >/dev/null
security set-key-partition-list -S apple-tool: -s -k "$ih_password" "$ih_keychain" >/dev/null

/usr/bin/openssl x509 -in "$ih_dir/certificate.pem" -noout -fingerprint -sha1 \
    | sed 's/.*=//; s/://g' > "$ih_identity_file"
IFS= read -r ih_identity < "$ih_identity_file"
if ! security find-identity -p codesigning "$ih_keychain" | rg -Fq "$ih_identity"; then
    echo "The local certificate and private key did not form a code signing identity." >&2
    exit 1
fi
ensure_codesign_trust

echo "Created InterviewHacker local development signing identity: $ih_identity"
echo "Private signing files stay in .local-signing and are excluded from the app and archive."
ih_ready=1
