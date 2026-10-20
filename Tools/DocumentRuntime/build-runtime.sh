#!/bin/bash
# Assemble from the validated local runtime; all generated data remains in this repository.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
source="${1:?Pass the validated relocatable runtime directory}"
python3 "$root/Tools/DocumentRuntime/package_runtime.py" --source "$source" --output "$root/dist"
