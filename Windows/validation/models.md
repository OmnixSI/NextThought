# First-run and optional model downloads

This file records every model NextThought for Windows downloads after installation: where it comes
from, its pinned size and SHA-256, where the hash is enforced, and the result of the verification run
on 2026-11-21 (release 0.9.0). Models are never shipped inside the installer; they are downloaded to
`%LOCALAPPDATA%\NextThought\models` (speech, voiceprint, face) or the document/embedding cache under
the same data directory, and every download is checked against the hash below before use.

## Required for speech recognition (first recording / "下载" in the setup checklist)

| Model | URL | Pinned SHA-256 | Enforced by | Verified |
| --- | --- | --- | --- | --- |
| X-ASR 480ms streaming zh-en (archive) | `https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-x-asr-480ms-streaming-zipformer-transducer-zh-en-punct-int8-2026-06-05.tar.bz2` | `fa5f63d618e5a01526e275a358bb7772e403f84808a4769fba52cffd8160bf74` | `ModelStore.InstallAsync` (archive SHA-256, then per-file sizes + `bpe.vocab` hash) | HTTP 200, 133,895,136 B; archive not fully downloaded in this run (see note) |
| X-ASR 160ms streaming zh-en (archive) | `.../sherpa-onnx-x-asr-160ms-streaming-zipformer-transducer-zh-en-punct-int8-2026-06-05.tar.bz2` | `8a6fca056e1a342546edd78be4d50274e2c01898e7b8ae8fc336f6410319c399` | same | HTTP 200, 133,898,007 B; same note |
| X-ASR offline refinement (archive) | `.../sherpa-onnx-x-asr-zipformer-transducer-zh-en-punct-int8-2026-06-03.tar.bz2` | `5d02c36d7b44e886b7c8f0d8e051f8713acab96c264bb6ef9e718be39a6a2224` | same | HTTP 200, 136,396,739 B; same note |

The archives unpack to `encoder/decoder/joiner` ONNX files plus `tokens.txt`; the sizes in
`Windows/src/NextThought.Platform/Resources/models.json` are checked after extraction
(encoder 155,278,641 B / 155,278,641 B / 161,015,713 B for the three packages, decoder 11,309,084 B,
joiner 2,581,422 B, tokens 58,806 B). `bpe.vocab` SHA-256
`a6f0b22ef6c0e6d766c41f5d8e888355c2d1a6ca394711fbc18b0aa0424315dc` is bundled in the app and also
checked.

**Note on the three archives:** they are 128–130 MB each; in this environment only the HTTP
reachability and the pinned sizes in `models.json` were verified (HTTP 200, correct content-length).
The archive hashes are enforced at download time by the app, so a normal install on the release
machine performs the full hash check; run the setup checklist's "下载" once on the release machine
before publishing and record the result here.

## Required for semantic retrieval (installed on first document import)

Source repository `onnx-community/Qwen3-Embedding-0.6B-ONNX`, revision
`c25a394dd583836952667c12f008335071b3f43d` (`Resources/embedding-model.json`).

| File | URL path | Size | Pinned SHA-256 (verified) |
| --- | --- | --- | --- |
| `config.json` | `resolve/<revision>/config.json` | 1,576 B | `66a10929782f3c9a3cd5dec90e2a95c60e05736134a63cd54479eeae80bed175` — **downloaded, SHA OK** |
| `tokenizer.json` | `resolve/<revision>/tokenizer.json` | 11,423,705 B | `def76fb086971c7867b829c23a26261e38d9d74e02139253b38aeb9df8b4b50a` — **downloaded, SHA OK** |
| `model_quantized.onnx` | `resolve/<revision>/onnx/model_quantized.onnx` | 613,527,631 B | `87cd124e0ef1fd1f223ebc283efccbaeac386d0b08344701c46975d0657b591f` — HTTP 200, content-length matches; not fully downloaded in this run |

`EmbeddingModelStore` checks size and SHA-256 for each file before installing and re-checks an
existing cache before moving a new copy in.

## Optional: voiceprint (区分说话人)

| Model | URL | Size | SHA-256 (verified) |
| --- | --- | --- | --- |
| WeSpeaker VoxCeleb ResNet34-LM ONNX | `https://huggingface.co/Wespeaker/wespeaker-resnet34-LM/resolve/f0c48c298fd835726c27956a5d617bad7115627e/voxceleb_resnet34_LM.onnx?download=true` | 26,530,309 B | `7bb2f06e9df17cdf1ef14ee8a15ab08ed28e8d0ef5054ee135741560df2ec068` — **downloaded, SHA OK** |

`SpeakerModelStore` rejects a file whose length or SHA-256 differs; the model is CC BY 4.0
(`licenses/WeSpeaker-CC-BY-4.0.txt`).

## Optional: gaze reminder (E10, off by default)

| Asset | Source | Size | SHA-256 | Verified |
| --- | --- | --- | --- | --- |
| `face_landmarker.task` (BlazeFace short-range + FaceMesh V2 + blendshape) | `https://huggingface.co/litert-community/MediaPipe-FaceLandmarker/resolve/cfb66f449dc5cb9f8dada3d52174d4cdae1c9ef1/face_landmarker.task` | 3,758,596 B | `64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff` — **downloaded, SHA OK** | converted ONNX assets are bundled (Apache-2.0, see `THIRD_PARTY_NOTICES.txt`) and numerically checked by `validation/e10/numeric-alignment.json` |

The face model is only downloaded when the user turns on "留意眼神是否离开镜头（使用摄像头）"; frames
are processed in memory and never saved.

## Optional: enhanced document components

- Office/PDF enhanced reading: installed from the Windows document-component package described in
  `validation/e23/` (files and Shas in `Resources/ocr-native-assets.json`; the 1.9 GB
  `model.safetensors` and 130 MB `pp-doclayout-v3/inference.pdiparams` are part of the optional
  native component package, not a first-run download).
- Document components are only needed for Word/Excel/PowerPoint import; the default reader needs no
  download.
- Verified in this run: the package manifest and SHA-256 checks in `EnhancedOcrService` /
  `DocumentRuntimeInstaller` are present; the optional end-to-end download was not repeated here
  because it needs the external component release (recorded in `validation/e23/`).

## Verification log

Run 2026-11-21 on the development machine (Windows 11, x64, release 0.9.0 + `Windows/release.ps1`):

- `curl -I` reachability for all 8 URLs above: **HTTP 200** each, content-length matches the pinned
  sizes (the three ASR archives, embedding config/tokenizer/model, speaker, face).
- Full download + `Get-FileHash` for the four small/medium files: **all SHA-256 match**.
- The three large ASR archives and the 613 MB embedding ONNX were not fully downloaded in this run;
  their hashes are pinned in `models.json`/`embedding-model.json` and enforced by the app, and the
  setup checklist completes the check on a normal install.

Re-run this file's checks when a model URL, revision or hash changes; update the table and the
release notes together.
