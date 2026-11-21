# Face-landmark ONNX assets (E10 看镜头提醒)

The Windows build ships the MediaPipe Face Landmarker models as ONNX so the build machine and the
user's machine never need a Python/TFLite toolchain. They are generated from the pinned upstream
bundle by `Windows/scripts/convert_face_landmarker.py` and checked in so the app can load them
directly through ONNX Runtime.

| File | Source | Size | SHA-256 |
|---|---|---:|---|
| `face_detector.onnx` | BlazeFace short-range detector (converted from `face_detector.tflite`) | 425,659 B | `fd2a4d2133b48a515bbfe248480cbcd7c9efa95d82dbf0d82bb99a5ae57efeb9` |
| `face_landmarks_detector.onnx` | FaceMesh V2 (converted from `face_landmarks_detector.tflite`) | 4,996,125 B | `f03920f259c58ea9695ff53698386f9b1c104bd22cbe8942626be5bda6b01d4c` |

Upstream: <https://huggingface.co/litert-community/MediaPipe-FaceLandmarker>, revision
`cfb66f449dc5cb9f8dada3d52174d4cdae1c9ef1`, Apache-2.0. Source bundle `face_landmarker.task`,
3,758,596 B, SHA-256 `64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff`.
The upstream model cards and license text are committed under `Windows/licenses/`; see
`Windows/THIRD_PARTY_NOTICES.txt` and `Windows/validation/e10/conversion.md`.

## Runtime contract

`face_detector.onnx` (Python wrapper transposes NHWC to NCHW before the run):

- input `input`: float32 NCHW `[1, 3, 128, 128]`, pixel scale 0…255 (same as the TFLite model)
- output `regressors`: float32 `[1, 896, 16]`
- output `classificators`: float32 `[1, 896, 1]`

`face_landmarks_detector.onnx`:

- input `input_12`: float32 NCHW `[1, 3, 256, 256]`，像素标度 **0…255**（FaceMesh 的 MediaPipe
  ImageToTensor 不做 0…1 归一化；喂 0…1 会让 presence 分数塌到 0；检测模型才用 0…1）
- output `Identity`: float32 `[1, 1434, 1, 1]` — 478 landmarks × (x, y, z)，**以 256 像素输入帧为单位**
  （C# 侧除以 256 后再投影回相机画面；flatten 顺序与 TFLite `[1, 1, 1, 1434]` 一致）
- output `Identity_1`: float32 `[1, 1, 1, 1]` — face presence logit；`FaceLandmarker` 按 MediaPipe
  的做法取 sigmoid 并以 0.5 作为门限（低于门限视为没有人脸）
- output `Identity_2`: float32 `[1, 1]` — tongue-out score

The landmark coordinates are the FaceMesh V2 model outputs in the 256-pixel crop frame; the
MediaPipe geometry pipeline (detection → ROI → rotation/normalisation → landmark back-projection)
is reimplemented in C# and covered by its own tests, so these files only carry the neural networks.
Runtime evidence: `Windows/validation/e10/coach-gaze.md`.

## Regenerate / verify

```powershell
py -3.12 -m pip install -r Windows/scripts/face_landmarker_requirements.txt
python Windows/scripts/convert_face_landmarker.py           # downloads, converts and validates
python Windows/scripts/convert_face_landmarker.py --check   # verifies the committed SHA-256 values
```

The script re-executes itself with `PYTHONHASHSEED=0`; two fresh runs produce byte-identical ONNX
files, and a fresh conversion is accepted only when its outputs numerically match the TFLite
originals (see `Windows/validation/e10/numeric-alignment.json`).
