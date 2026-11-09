#!/bin/bash
# Builds the unsigned executable and renders synthetic LiveSessionView evidence
# in-process through AppKit. This does not create/sign/install an app bundle.
set -euo pipefail

project_root="$(cd "$(dirname "$0")/.." && pwd)"
output_directory="${1:-$project_root/.local/validation/live-session-ui}"
tracked_evidence="$project_root/docs/validation/v2.4.1/live-session-ui"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

cd "$project_root"
swift build --disable-sandbox -c release
binary_path="$(swift build --disable-sandbox -c release --show-bin-path)/NextThought"
[[ -x "$binary_path" ]] || { echo "Could not find $binary_path" >&2; exit 1; }

mkdir -p "$scratch/data" "$output_directory"
revision="$(git rev-parse --short HEAD 2>/dev/null || printf working-tree)"
if [[ -n "$(git status --porcelain --untracked-files=no 2>/dev/null)" ]]; then revision="$revision+worktree"; fi

NEXTTHOUGHT_DATA_DIR="$scratch/data" \
NEXTTHOUGHT_DEMO=1 \
NEXTTHOUGHT_T07_COMMIT="$revision" \
"$binary_path" -AppleLanguages "(zh-Hans)" -AppleLocale "zh-Hans" \
  --self-test live-session-ui --output "$output_directory"

python3 - "$output_directory/manifest.json" "$tracked_evidence" <<'PY'
import json
import pathlib
import shutil
import sys

source = pathlib.Path(sys.argv[1])
destination = pathlib.Path(sys.argv[2])
manifest = json.loads(source.read_text(encoding="utf-8"))
by_key = {(row["scenario"], row["appearance"], row["widthPoints"]): row
          for row in manifest["captures"]}
scenario_count = len({row["scenario"] for row in manifest["captures"]})
if manifest.get("scenarioCount") != scenario_count:
    raise SystemExit(f"manifest scenario count mismatch: {manifest.get('scenarioCount')} != {scenario_count}")
if manifest.get("matrixCaptureCount") != len(manifest["captures"]):
    raise SystemExit("manifest matrix capture count does not match captures")
selected = [
    ("normal", "light", 1200), ("normal", "dark", 960), ("normal", "light", 900),
    ("streaming", "light", 900), ("streaming", "dark", 1200),
    ("busy", "dark", 960), ("success", "light", 1200),
    ("save-warning", "light", 960), ("save-warning", "dark", 1200),
    ("save-recovery", "dark", 960), ("unavailable", "dark", 1200),
    ("stale", "light", 960), ("empty", "dark", 900),
    ("reading-history", "light", 1200), ("selection", "dark", 960),
    ("focus", "light", 960), ("overlay", "dark", 1200),
    ("large-text", "light", 960), ("large-text", "dark", 1200),
    ("focus-busy", "light", 960), ("stale-manual-edit", "dark", 1200),
    ("streaming-reading-history", "light", 1200), ("selected-focus", "dark", 960),
    ("cancel-late-callback", "dark", 1200),
]
component_sample = manifest["componentSample"]
component_source = source.parent / component_sample["path"]
if not component_source.is_file():
    raise SystemExit(f"component sample image is missing: {component_source}")
destination.mkdir(parents=True, exist_ok=True)
for old in destination.glob("synthetic-*.png"):
    old.unlink()
representatives = []
for key in selected:
    row = by_key.get(key)
    if row is None:
        raise SystemExit(f"missing representative capture: {key}")
    shutil.copy2(source.parent / row["path"], destination / row["path"])
    if row.get("overlayPath"):
        shutil.copy2(source.parent / row["overlayPath"], destination / row["overlayPath"])
    representatives.append({
        "scenario": row["scenario"], "appearance": row["appearance"],
        "widthPoints": row["widthPoints"], "heightPoints": row["heightPoints"],
        "dynamicTypeSize": row["dynamicTypeSize"], "locale": row["locale"],
        "synthetic": row["synthetic"], "path": row["path"],
        "overlayPath": row.get("overlayPath"), "notes": row["notes"],
    })

tracked = {
    "schemaVersion": 1,
    "generatedAt": manifest["generatedAt"],
    "sourceRevision": manifest["sourceRevision"],
    "syntheticState": True,
    "fullMatrixCaptureCount": len(manifest["captures"]),
    "fullMatrix": {"appearances": ["light", "dark"], "widthsPt": [1200, 960, 900],
                    "heightPt": 780, "scenarioCount": scenario_count, "locale": "zh-Hans"},
    "componentSample": component_sample,
    "representativeCaptures": representatives,
    "interactions": manifest["interactions"],
    "limitations": manifest["limitations"],
}
(destination / "manifest.json").write_text(
    json.dumps(tracked, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
shutil.copy2(component_source, destination / component_sample["path"])

images = []
for item in representatives:
    alt = f"{item['scenario']} · {item['appearance']} · {item['widthPoints']} pt"
    images.append(f"### {alt}\n\n![{alt}]({item['path']})")
    if item.get("overlayPath"):
        images.append(f"#### Evidence details popover\n\n![Evidence details popover]({item['overlayPath']})")
limitations = "\n".join(f"- {item}" for item in manifest["limitations"])
checks = "\n".join(
    f"| `{item['id']}` | {item['result']} | {item['evidence']} |"
    for item in manifest["interactions"])
report = f"""# T07 Live Session Acceptance Evidence

- Generated at: {manifest['generatedAt']}
- Source revision: `{manifest['sourceRevision']}`
Tracked evidence includes {len(representatives)} representative scenario PNGs, one evidence popover PNG, and one component sample sheet PNG. The reproducible run produced {len(manifest['captures'])} native matrix captures for both appearances, three widths, and all {scenario_count} scenarios. `manifest.json` maps every tracked matrix image and records the full dimensions.

All transcript, answer, and persistence states are synthetic. Captures use the actual SwiftUI live page rendered through AppKit with locale `zh-Hans`; they use no real audio, ASR, model call, network request, or archive write. The full local matrix is at the requested output directory; run `scripts/capture-live-session-acceptance.sh` to regenerate it and refresh these representative files.

## Interaction checks

| Check | Result | Evidence |
|---|---|---|
{checks}

## Known acceptance gaps and limits

{limitations}

## Component sample sheet

![Native component state samples]({component_sample['path']})

## Representative screenshots

{chr(10).join(images)}
"""
(destination / "report.md").write_text(report, encoding="utf-8")
print(f"Tracked {len(representatives)} representative captures and one component sample sheet in {destination}")
PY

echo "Evidence written to $output_directory"
