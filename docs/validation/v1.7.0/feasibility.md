# Document runtime experiment — 2026-10-21

All experiment files, installs, caches and models are under this lab. No production source was edited. No existing user models were changed. No OpenAI/Codex inference was invoked.

## Reusable runtime

- `runtime-macos14/python-run`: relocatable launcher, bundled CPython 3.12.13 and `site-packages`.
- `runtime-macos14/python/`: standalone interpreter; `runtime-macos14/site-packages/`: installed minimal inference/Office dependencies.
- `requirements.lock.txt`: 61 exact package versions; `requirements.hashed.txt`: same package set with PyPI SHA-256 hashes.
- `installed-freeze.txt`: actual local install metadata; `installed-packages.json`: versions/license fields.
- `install_lab.sh`: reproduce installation, with all uv/Python/HF/XDG caches local.
- `wheels-macos14.json`: exact PyPI URLs, byte sizes and SHA-256 for MLX 0.32.4 and mlx-metal 0.32.4 macOS 14 arm64 wheels. Explicit wheel selection is necessary: uv running on this macOS 26 host initially chose macOS 26 wheels.
- `check_deployment.py` scanned 190 native Python/library files; `results/deployment.json` reports none with minimum deployment version above macOS 14.
- `relocation-macos14.log` verifies that interpreter, MLX, ORT and all imports load from the copied new prefix. Actual OCR and embedding were also rerun from this runtime, not only imported.

The minimal package set deliberately installs mlx-vlm with `--no-deps`; default audio/training/server dependencies were omitted. Paddle OCR and all tested calls work without PyTorch, PaddlePaddle, PaddleX, datasets, mlx-audio or a Python web server. This is a tested feature subset, not a claim that every mlx-vlm command works with this dependency set.

## Verified models

`download_models.py` downloads fixed revisions using resumable `.partial` files. It validates expected byte sizes, SHA-256 against HF LFS metadata, and Git blob SHA-1 for small files. Each directory has `download-manifest.json` including source URL, revision, hashes and file sizes.

- `models/paddle-ocr-q8`: olragon/PaddleOCR-VL-1.6-8bit @ 845ebafa9b6bed8372e4675a40b2bb19ac4c7089.
- `models/qwen-embedding-q8`: mlx-community/Qwen3-Embedding-0.6B-8bit @ 407ad2329cd30702720aafe83f74a1ba30fdfbca.
- `models/layout-onnx`: PaddlePaddle/PP-DocLayoutV3_onnx @ 46bbdf188bb0a772c08aed74882ce7e51a8f1ea6.

No repository Python code is needed for the Paddle model: `probe_regions.py` constructs a second model directory that excludes all four repo `.py` files and AppleDouble files, and successfully loads and infers offline using mlx-vlm's built-in Paddle processor.

## Measured results on this M2 Pro 16 GB, macOS 26.5.2

- ONNX layout: official 800x800 preprocessing, CPU execution, successful region detection. Initial load 0.408 s, one 1100x1000 synthetic page 0.646 s; table score 0.958. Actual model inputs are `im_shape`, `image`, `scale_factor`. Outputs are `[300,7]` boxes (label,score,x1,y1,x2,y2,reading_order), `[1]` count and `[300,200,200]` int32 masks. Code in `probe.py`.
- Paddle Q8 whole page, after selecting macOS 14 MLX wheels: load 1.640 s, prediction 4.282 s, Metal peak memory 2,066,171,526 bytes. Chinese, English, Kubernetes, 1024, and E=mc² were recognized. Whole-page `OCR:` returned column text rather than a structured table; region-specific OCR is necessary for that table.
- Relocated runtime, no repo Python: `Table Recognition:` returned correct 3-row, 2-column **OTSL**, 28 generated tokens in 0.946 s. `Formula Recognition:` returned LaTeX with `E=m c^{2}`, 24 tokens in 0.312 s. Model load 1.516 s. Raw results in `results/regions.json`.
- OTSL tokens present in tokenizer: `<ecel>`, `<fcel>`, `<lcel>`, `<nl>`, `<ucel>`, `<xcel>`. A normalizer limited to HTML/Markdown will fail on real Paddle output.
- Qwen embedding via MIT mlx-lm: `model.model(token_ids)` hidden states, last token pooling, float32 L2 normalization, single unpadded input, no manually appended EOS. Dimension 1024, norms approximately 1. Chinese related/unrelated cosine 0.833063 / 0.090309; English 0.778447 / 0.136106. Relocated load 0.568 s; first query 0.334 s and subsequent samples 0.012–0.019 s. Metal peak memory 931,692,856 bytes. No GPL mlx-embeddings dependency.
- MarkItDown 0.1.8 Office extras: self-generated DOCX/PPTX/XLSX all extracted Chinese/English and Kubernetes text; DOCX/XLSX tables retained. `results/office.json`. Old binary XLS, embedded-image placement/OCR, and complex Office layouts were not part of this probe.

## Reproduce probes

From this repository, source `.local/document-runtime-lab/env.sh`, then set `HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1` for inference. Run:

```sh
"$DOCUMENT_LAB_ROOT/runtime-macos14/python-run" "$DOCUMENT_LAB_ROOT/probe.py" layout
"$DOCUMENT_LAB_ROOT/runtime-macos14/python-run" "$DOCUMENT_LAB_ROOT/probe.py" embedding
"$DOCUMENT_LAB_ROOT/runtime-macos14/python-run" "$DOCUMENT_LAB_ROOT/probe.py" ocr
"$DOCUMENT_LAB_ROOT/runtime-macos14/python-run" "$DOCUMENT_LAB_ROOT/probe_regions.py"
"$DOCUMENT_LAB_ROOT/runtime-macos14/python-run" "$DOCUMENT_LAB_ROOT/probe_office.py"
```

The fixture is locally drawn text/table/formula, not an externally copyrighted page. All successful runtime probes ran in offline mode. The runtime is approximately 628 MiB unpacked. Timing is a smoke measurement on these fixtures, not a general OCR benchmark. A real macOS 14 host, signing/notarization, merged-cell tables, dense scientific pages, polygon crop fidelity and official reference-vector parity remain separate acceptance work.
