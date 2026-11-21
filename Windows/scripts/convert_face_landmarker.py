#!/usr/bin/env python3
"""Regenerate the Windows face-landmark ONNX models used by E10 (看镜头提醒).

Source: MediaPipe Face Landmarker (Apache-2.0), the `face_landmarker.task` bundle from
https://huggingface.co/litert-community/MediaPipe-FaceLandmarker (pinned revision below).
The bundle contains BlazeFace short-range detection (`face_detector.tflite`) and FaceMesh V2
(`face_landmarks_detector.tflite`); this script converts both to ONNX with tflite2onnx and
proves numerically that the ONNX models behave like the TFLite originals.

Why the monkey patches: tflite2onnx 0.4.1 crashes on this bundle because the FaceMesh TFLite
graph is not plain NHWC: the per-channel PRelu `alpha` tensors are [1,1,C] (no batch axis), so
layout propagation hands them an NHWC->NCHW perm of length 4 and the shape/weight transform
raises IndexError/ValueError. The channel-last parameter is re-shaped to the channel-first
broadcast shape [1,C,1,1] ONNX expects next to an NCHW input; any other tensor whose rank does
not match the propagated layout is left untouched and reported (the numeric check below is the
gate: a wrong layout makes the check fail).

One command:
    py -3.12 -m pip install -r Windows/scripts/face_landmarker_requirements.txt
    python Windows/scripts/convert_face_landmarker.py            # regenerate + validate
    python Windows/scripts/convert_face_landmarker.py --check    # verify committed files only

Outputs (relative to the repository root):
    Windows/assets/face-landmarker/face_detector.onnx
    Windows/assets/face-landmarker/face_landmarks_detector.onnx
    Windows/assets/face-landmarker/SHA256SUMS
    Windows/validation/e10/numeric-alignment.json
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import tempfile
import urllib.request
import zipfile
from pathlib import Path

# tflite2onnx walks Python sets while building the ONNX graph, so the serialized file depends on
# string hash randomization. Re-exec with a fixed seed before anything is imported so a fresh
# checkout regenerates byte-identical models (verified against SHA256SUMS).
if os.environ.get("PYTHONHASHSEED") != "0":
    os.environ["PYTHONHASHSEED"] = "0"
    os.execv(sys.executable, [sys.executable, *sys.argv])

ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / "Windows" / "assets" / "face-landmarker"
EVIDENCE_DIR = ROOT / "Windows" / "validation" / "e10"

# Pinned upstream (Apache-2.0 model card; see Windows/licenses/ and THIRD_PARTY_NOTICES.txt).
REVISION = "cfb66f449dc5cb9f8dada3d52174d4cdae1c9ef1"
SOURCE_URL = (
    "https://huggingface.co/litert-community/MediaPipe-FaceLandmarker/resolve/"
    + REVISION
    + "/face_landmarker.task"
)
SOURCE_SHA256 = "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff"
SOURCE_BYTES = 3_758_596

# Deterministic validation input: the same image for both runtimes.
RNG_SEED = 7
SIZES = {"face_detector": 128, "face_landmarks_detector": 256}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def download_source(destination: Path) -> Path:
    if destination.exists() and sha256(destination) == SOURCE_SHA256:
        return destination
    print(f"downloading {SOURCE_URL}")
    request = urllib.request.Request(SOURCE_URL, headers={"User-Agent": "NextThought-port/1.0"})
    with urllib.request.urlopen(request, timeout=180) as response, destination.open("wb") as handle:
        handle.write(response.read())
    actual = sha256(destination)
    if actual != SOURCE_SHA256:
        raise SystemExit(f"source SHA-256 mismatch: {actual} != {SOURCE_SHA256}")
    if destination.stat().st_size != SOURCE_BYTES:
        raise SystemExit(f"source size mismatch: {destination.stat().st_size} != {SOURCE_BYTES}")
    return destination


def extract(bundle: Path, directory: Path) -> dict[str, Path]:
    files = {}
    with zipfile.ZipFile(bundle) as archive:
        for name in archive.namelist():
            target = directory / Path(name).name
            target.write_bytes(archive.read(name))
            files[Path(name).name] = target
    required = ["face_detector.tflite", "face_landmarks_detector.tflite"]
    missing = [name for name in required if name not in files]
    if missing:
        raise SystemExit(f"bundle is missing {missing}")
    return files


def install_tflite2onnx_patches() -> None:
    """Documented workaround for tflite2onnx 0.4.1 on the MediaPipe FaceMesh graph."""
    import numpy as np
    import tflite2onnx.tensor as tensor_module

    original = tensor_module.Tensor.transform
    stats = {"pr_elu_alpha": 0, "left_untouched": []}

    def patched(self):
        if self.layout is None:
            return
        if len(self.shape) != len(self.layout.source):
            if self.layout.source == "NHWC" and self.data is not None and len(self.shape) <= 3:
                # Channel-last per-channel parameter (PRelu alpha [1,1,C]): ONNX expects a
                # channel-first broadcast shape next to the NCHW graph.
                channels = int(self.shape[-1])
                self.data = np.asarray(self.data).reshape([1, channels, 1, 1])
                self.shape = [1, channels, 1, 1]
                self.layout = None
                stats["pr_elu_alpha"] += 1
                return
            stats["left_untouched"].append((self.name, list(self.shape)))
            self.layout = None
            return
        original(self)

    tensor_module.Tensor.transform = patched
    globals()["_patch_stats"] = stats


def convert(tflite_path: Path, onnx_path: Path) -> None:
    import tflite2onnx

    tflite2onnx.convert(str(tflite_path), str(onnx_path), explicit_layouts=False)
    if not onnx_path.is_file():
        raise SystemExit(f"conversion produced no {onnx_path}")


def validate(tflite_path: Path, onnx_path: Path, name: str, size: int) -> list[dict]:
    """Compare TFLite and ONNX outputs on one deterministic image."""
    import numpy as np
    import onnxruntime as ort
    import tensorflow as tf

    image = np.random.default_rng(RNG_SEED).random((1, size, size, 3), dtype=np.float32) * 255.0
    interpreter = tf.lite.Interpreter(model_path=str(tflite_path))
    interpreter.allocate_tensors()
    input_detail = interpreter.get_input_details()[0]
    interpreter.set_tensor(input_detail["index"], image.astype(input_detail["dtype"]))
    interpreter.invoke()
    tflite_outputs = [
        np.asarray(interpreter.get_tensor(detail["index"])).reshape(-1)
        for detail in interpreter.get_output_details()
    ]
    session = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    nchw = np.transpose(image, (0, 3, 1, 2)).astype(np.float32)
    onnx_outputs = [np.asarray(value).reshape(-1) for value in session.run(None, {session.get_inputs()[0].name: nchw})]

    optional_names = ["landmarks", "face_presence", "tongue_out"]
    if name == "face_detector":
        optional_names = ["regressors", "classificators"]
    rows = []
    for index, (reference, produced) in enumerate(zip(tflite_outputs, onnx_outputs)):
        output_name = optional_names[index] if index < len(optional_names) else f"output_{index}"
        if reference.size != produced.size:
            raise SystemExit(f"{name}/{output_name}: size {reference.size} != {produced.size}")
        difference = np.abs(reference - produced)
        scale = max(1.0, float(np.abs(reference).max()))
        row = {
            "model": name,
            "output": output_name,
            "values": int(reference.size),
            "max_abs": float(difference.max()),
            "mean_abs": float(difference.mean()),
            "max_rel": float((difference / np.maximum(np.abs(reference), 1e-6)).max()),
            "tflite_range": [float(reference.min()), float(reference.max())],
            "onnx_range": [float(produced.min()), float(produced.max())],
            # fp16 weights in the source bundle; sqrt-sized drift is expected, layout mistakes are not.
            "tolerance_abs": scale * 1e-4,
        }
        if row["max_abs"] > row["tolerance_abs"]:
            raise SystemExit(f"{name}/{output_name}: max_abs {row['max_abs']} > {row['tolerance_abs']}")
        rows.append(row)
    return rows


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify the committed files only")
    parser.add_argument("--source", help="use this face_landmarker.task instead of downloading")
    parser.add_argument("--skip-validation", action="store_true", help="convert without the TFLite reference run")
    args = parser.parse_args()

    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    sum_path = ASSET_DIR / "SHA256SUMS"
    artifacts = [ASSET_DIR / "face_detector.onnx", ASSET_DIR / "face_landmarks_detector.onnx"]

    if args.check:
        for artifact in artifacts:
            if not artifact.is_file():
                raise SystemExit(f"missing {artifact}")
        sums = {}
        for line in sum_path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                digest, name = line.split(maxsplit=1)
                sums[name.strip()] = digest
        for artifact in artifacts:
            actual = sha256(artifact)
            if sums.get(artifact.name) != actual:
                raise SystemExit(f"{artifact.name}: SHA-256 mismatch ({actual})")
        print("face-landmarker ONNX assets match SHA256SUMS")
        return 0

    with tempfile.TemporaryDirectory(prefix="nt-face-landmarker-") as temporary:
        directory = Path(temporary)
        bundle = Path(args.source) if args.source else directory / "face_landmarker.task"
        if args.source:
            if sha256(bundle) != SOURCE_SHA256:
                raise SystemExit("given source bundle SHA-256 does not match the pinned value")
        else:
            bundle = download_source(bundle)
        files = extract(bundle, directory)
        submodel_sha = {name: sha256(path) for name, path in files.items()}

        install_tflite2onnx_patches()
        converted = {}
        for name in SIZES:
            tflite = files[name + ".tflite"]
            onnx = directory / (name + ".onnx")
            convert(tflite, onnx)
            converted[name] = onnx
        stats = globals().get("_patch_stats", {})

        rows: list[dict] = []
        if not args.skip_validation:
            for name, size in SIZES.items():
                rows.extend(validate(files[name + ".tflite"], converted[name], name, size))
            for row in rows:
                print("{model}/{output}: max_abs={max_abs:.6g} max_rel={max_rel:.6g} tolerance={tolerance_abs:.6g}".format(**row))
        else:
            print("validation skipped")

        for name, onnx in converted.items():
            target = ASSET_DIR / (name + ".onnx")
            target.write_bytes(onnx.read_bytes())
        lines = [f"{sha256(target)}  {target.name}" for target in artifacts]
        sum_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        manifest = {
            "source": SOURCE_URL,
            "source_sha256": SOURCE_SHA256,
            "source_bytes": SOURCE_BYTES,
            "revision": REVISION,
            "submodels_sha256": submodel_sha,
            "converted_sha256": {target.name: sha256(target) for target in artifacts},
            "tflite2onnx_patch": {
                "pr_elu_alpha_reshaped": stats.get("pr_elu_alpha", 0),
                "left_untouched": stats.get("left_untouched", []),
                "reason": "tflite2onnx 0.4.1 layout propagation vs channel-last PRelu alpha",
            },
        }
        EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)
        (EVIDENCE_DIR / "numeric-alignment.json").write_text(
            json.dumps({"manifest": manifest, "alignment": rows}, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        (ASSET_DIR / "model-manifest.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        print("wrote", ", ".join(str(target.relative_to(ROOT)) for target in artifacts))
        print("wrote", (EVIDENCE_DIR / "numeric-alignment.json").relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
