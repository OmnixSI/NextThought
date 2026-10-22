#!/bin/bash
set -euo pipefail
ih_office_root="$(cd "$(dirname "$0")/../../.." && pwd)"
mkdir -p "$ih_office_root/.local/validation"
ih_office_probe="$(mktemp -d "$ih_office_root/.local/validation/office-XXXXXX")"
cp "$ih_office_root/Tools/Validation/LegacyOffice/fake-soffice.py" "$ih_office_probe/soffice"
chmod +x "$ih_office_probe/soffice"
swiftc -parse-as-library -swift-version 5 "$ih_office_root/Sources/InterviewHacker/LegacyOfficeConverter.swift" "$ih_office_root/Tools/Validation/LegacyOffice/Probe.swift" -o "$ih_office_probe/probe"
IH_LEGACY_TEST_PID="$ih_office_probe/process.pid" "$ih_office_probe/probe" "$ih_office_probe"
