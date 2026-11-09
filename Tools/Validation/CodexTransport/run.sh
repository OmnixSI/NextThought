#!/bin/bash
# Exercise the real transport implementations against a local fake CLI: no login,
# model calls, user settings or credentials. Unrelated app dependencies are stubbed.
set -euo pipefail
root="$(cd "$(dirname "$0")/../../.." && pwd)"
probe="$(mktemp -d "${TMPDIR:-/tmp}/ih-codex-transport.XXXXXX")"
trap 'rm -rf "$probe"' EXIT
for file in AnswerEngines CodexBridge; do
  sed '/^import NextThoughtCore$/d' "$root/Sources/NextThought/$file.swift" > "$probe/$file.swift"
done
for file in AnswerStreaming CodexEventParser ProcessLineReader CancellableTaskValue; do
  cp "$root/Sources/NextThoughtCore/$file.swift" "$probe/$file.swift"
done
cp "$root/Sources/NextThought/AppIdentity.swift" "$probe/AppIdentity.swift"
cp "$root/Sources/NextThought/AppEnvironment.swift" "$probe/AppEnvironment.swift"
cp "$root/Tools/Validation/CodexTransport/Probe.swift" "$probe/Probe.swift"
cp "$root/Tools/Validation/CodexTransport/fake-codex.py" "$probe/codex"
chmod +x "$probe/codex"
swiftc -parse-as-library -swift-version 5 "$probe/"*.swift -o "$probe/probe"
CODEX_CLI_PATH="$probe/codex" PROBE_INTERRUPT_LOG="$probe/interrupt.log" "$probe/probe"
