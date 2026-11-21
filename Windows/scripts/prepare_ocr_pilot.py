"""Pinned optional OCR feasibility assets; no machine-level install or PATH changes."""
import concurrent.futures
import hashlib
import json
import os
import sys
import urllib.request
import zipfile
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought/models/paddleocr-vl-1.6-gguf'
root.mkdir(parents=True, exist_ok=True)
base = 'https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6-GGUF/resolve/511b09642bb324401f15f97cc23bc67e8f0a291d/'
assets = [
    ('PaddleOCR-VL-1.6-GGUF.gguf', base + 'PaddleOCR-VL-1.6-GGUF.gguf', 935769056, 'f3ae46ec885050acf4b3d31944431e1fd90d50664fb09126af4a3c050ba14ee8'),
    ('PaddleOCR-VL-1.6-GGUF-mmproj.gguf', base + 'PaddleOCR-VL-1.6-GGUF-mmproj.gguf', 881770560, '204d757d7610d9b3faab10d506d69e5b244e32bf765e2bab2d0167e65e0a058a'),
    ('llama-b12189-bin-win-cpu-x64.zip', 'https://github.com/ggml-org/llama.cpp/releases/download/b12189/llama-b12189-bin-win-cpu-x64.zip', 24749596, 'f912ea5e3e397c63405c81e732131ab0fa6e72411c80546bb5ad643f7dd82aac'),
]

def download(asset):
    name, url, size, expected = asset
    file = root / name
    if file.exists() and file.stat().st_size == size:
        with file.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() == expected:
                print('Verified existing', name, flush=True); return
    temporary = file.with_suffix(file.suffix + '.partial')
    total = temporary.stat().st_size if temporary.exists() else 0
    if total > size: raise ValueError('Unexpected partial file length')
    while total < size:
        end = min(size - 1, total + 8 * 1024 * 1024 - 1)
        for attempt in range(4):
            try:
                request = urllib.request.Request(url + f'?download=true&part={total}', headers={'Range': f'bytes={total}-{end}', 'User-Agent': 'NextThought-Windows/0.3'})
                with urllib.request.urlopen(request, timeout=120) as response:
                    if response.status == 206:
                        if not response.headers.get('Content-Range', '').startswith(f'bytes {total}-'): raise ValueError('Unexpected range')
                        block = response.read(end - total + 1)
                    elif total == 0:
                        block = response.read()  # Small GitHub runtime zip fallback.
                    else: raise ValueError('Server did not honor range request')
                if len(block) != end - total + 1 and not (total == 0 and len(block) == size): raise ValueError('Truncated model range')
                break
            except Exception:
                if attempt == 3: raise
        with temporary.open('ab') as output: output.write(block)
        total += len(block); print(name, total // 1024 // 1024, 'MiB', flush=True)
    with temporary.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    if total != size or digest != expected: raise ValueError('Size/hash mismatch: ' + name)
    os.replace(temporary, file); print('Verified', name, flush=True)

with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
    list(pool.map(download, assets))
runtime = root / 'llama-b12189-verified'
runtime.mkdir(exist_ok=True)
with zipfile.ZipFile(root / assets[2][0]) as archive:
    for entry in archive.infolist():
        destination = (runtime / entry.filename).resolve()
        if not destination.is_relative_to(runtime.resolve()): raise ValueError('Unexpected runtime archive path')
    for entry in archive.infolist():
        if entry.is_dir(): continue
        destination = (runtime / entry.filename).resolve()
        destination.parent.mkdir(parents=True, exist_ok=True)
        content = archive.read(entry)
        temporary = destination.with_name(destination.name + '.tmp')
        temporary.write_bytes(content)
        if temporary.read_bytes() != content: raise ValueError('Runtime extraction write verification failed')
        os.replace(temporary, destination)
(root / 'pilot-assets.json').write_text(json.dumps(assets, indent=2), encoding='utf-8')
print('OCR pilot assets ready:', root, flush=True)
