#!/bin/bash
# Build-time only. End users download the preassembled archive.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
export DOCUMENT_LAB_ROOT="${DOCUMENT_BUILD_ROOT:-$root/.local/runtime-build}"
export UV_PYTHON_INSTALL_DIR="$DOCUMENT_LAB_ROOT/python"
export UV_PYTHON_BIN_DIR="$DOCUMENT_LAB_ROOT/bin"
export UV_CACHE_DIR="$DOCUMENT_LAB_ROOT/cache/uv"
export XDG_CACHE_HOME="$DOCUMENT_LAB_ROOT/cache"
export UV_PYTHON_PREFERENCE=only-managed
mkdir -p "$DOCUMENT_LAB_ROOT"
uv python install 3.12.13
uv venv --python 3.12.13 "$DOCUMENT_LAB_ROOT/venv"
uv pip install --python "$DOCUMENT_LAB_ROOT/venv/bin/python" --no-deps --require-hashes -r "$root/Tools/DocumentRuntime/requirements.hashed.txt"
# Host macOS26 otherwise selects an incompatible MLX wheel. Always replace it with the macOS14 build.
cp "$root/Tools/DocumentRuntime/select_macos14_wheels.py" "$DOCUMENT_LAB_ROOT/select_macos14_wheels.py"
"$DOCUMENT_LAB_ROOT/venv/bin/python" "$DOCUMENT_LAB_ROOT/select_macos14_wheels.py"
uv pip install --python "$DOCUMENT_LAB_ROOT/venv/bin/python" --reinstall --no-deps "$DOCUMENT_LAB_ROOT/wheels/"*.whl
"$DOCUMENT_LAB_ROOT/venv/bin/python" - "$DOCUMENT_LAB_ROOT" <<'PY'
import pathlib,sys,shutil,sysconfig
root=pathlib.Path(sys.argv[1]);target=root/'relocatable';target.mkdir(exist_ok=True)
source=pathlib.Path(sys.base_prefix)
shutil.copytree(source,target/'python',symlinks=True,dirs_exist_ok=True)
shutil.copytree(sysconfig.get_path('purelib'),target/'site-packages',symlinks=False,dirs_exist_ok=True)
print(target)
PY
python3 "$root/Tools/DocumentRuntime/package_runtime.py" --source "$DOCUMENT_LAB_ROOT/relocatable" --output "$root/dist"
