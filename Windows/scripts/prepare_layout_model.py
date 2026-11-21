import hashlib
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
repository = 'PaddlePaddle/PP-DocLayoutV3'
revision = '241f8bdfc77a7c7bee915a5057aaee58c235a8d3'
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought/models/pp-doclayout-v3'
root.mkdir(parents=True, exist_ok=True)
expected = '70bd316b0582769ec968829fd1feb1a6a58b7c941b938327e551b6b12b45c137'
size = 130806572
target = root / 'inference.pdiparams'
temporary = target.with_suffix('.pdiparams.partial')
offset = temporary.stat().st_size if temporary.exists() else 0
if target.exists() and target.stat().st_size == size:
    with target.open('rb') as stream: existing_hash = hashlib.file_digest(stream, 'sha256').hexdigest()
    if existing_hash == expected:
        print("Layout model already verified", flush=True)
        sys.exit(0)
if offset > size: raise ValueError('Unexpected partial model length')
url = f'https://huggingface.co/{repository}/resolve/{revision}/inference.pdiparams'
while offset < size:
    end = min(size - 1, offset + 8 * 1024 * 1024 - 1)
    for attempt in range(4):
        try:
            request = urllib.request.Request(url + f'?download=true&part={offset}', headers={'Range': f'bytes={offset}-{end}', 'User-Agent': 'NextThought-Windows/0.3'})
            with urllib.request.urlopen(request, timeout=120) as response:
                if response.status != 206 or not response.headers.get('Content-Range', '').startswith(f'bytes {offset}-'):
                    raise ValueError('Server did not honor model range request')
                block = response.read(end - offset + 1)
                if len(block) != end - offset + 1: raise ValueError('Truncated model range')
            break
        except Exception:
            if attempt == 3: raise
            time.sleep(1)
    with temporary.open('ab') as output: output.write(block)
    offset += len(block); print('Verified transport length', offset, '/', size, flush=True)
with (temporary if temporary.exists() else target).open('rb') as stream:
    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
if digest != expected: raise ValueError('Final model SHA-256 mismatch; partial file kept for diagnosis')
if temporary.exists(): os.replace(temporary, target)
files = []
for name in ['inference.json', 'inference.yml', 'inference.pdiparams']:
    file = root / name
    if not file.exists(): file.write_bytes(urllib.request.urlopen(f'https://huggingface.co/{repository}/resolve/{revision}/{name}', timeout=120).read())
    with file.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    files.append({'name': name, 'size': file.stat().st_size, 'sha256': digest})
(root / 'installed.json').write_text(json.dumps({'repository': repository, 'revision': revision, 'files': files}, indent=2), encoding='utf-8')
print('Layout model verified', flush=True)
