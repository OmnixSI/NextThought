#!/bin/bash
# Foreground, project-local, no launchd service; unregisters when it exits.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
exec python3 "$root/Tools/DocumentRuntime/temporary_runner.py" "$@"
