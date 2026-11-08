"""Fetch pinned original PaddleOCR weights for a native-Paddle CPU comparison."""
import concurrent.futures
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
repository = 'PaddlePaddle/PaddleOCR-VL-1.6'
revision = '132c45f52c0a26ed5a6b2f6bd8e3985794cc27dd'
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought/models/paddleocr-vl-1.6-native'
root.mkdir(parents=True, exist_ok=True)
names = ['LICENSE', 'config.json', 'generation_config.json', 'inference.yml', 'preprocessor_config.json', 'processor_config.json', 'special_tokens_map.json', 'tokenizer_config.json', 'added_tokens.json', 'chat_template.jinja', 'tokenizer.model', 'tokenizer.json']
assets = json.load(urllib.request.urlopen(f'https://huggingface.co/api/models/{repository}/revision/{revision}?blobs=true'))['siblings']
lookup = {f['rfilename']: f for f in assets}
records = []

def download(name):
    asset = lookup[name]; file = root / name
    url = f'https://huggingface.co/{repository}/resolve/{revision}/{name}'
    if file.exists() and file.stat().st_size == asset['size'] and asset.get('lfs', {}).get('sha256'):
        with file.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if digest == asset['lfs']['sha256']: return {'name': name, 'size': file.stat().st_size, 'sha256': digest}
    if name != 'model.safetensors':
        content = urllib.request.urlopen(url, timeout=120).read()
        expected = asset.get('lfs', {}).get('sha256')
        if len(content) != asset['size'] or expected and hashlib.sha256(content).hexdigest() != expected: raise ValueError('Invalid asset: ' + name)
        file.write_bytes(content)
    else:
        temporary = file.with_suffix('.safetensors.partial'); total = temporary.stat().st_size if temporary.exists() else 0
        while total < asset['size']:
            end = min(asset['size'] - 1, total + 16 * 1024 * 1024 - 1)
            for attempt in range(4):
                try:
                    request = urllib.request.Request(url + f'?download=true&part={total}', headers={'Range': f'bytes={total}-{end}'})
                    with urllib.request.urlopen(request, timeout=120) as response:
                        if response.status != 206 or not response.headers.get('Content-Range', '').startswith(f'bytes {total}-'): raise ValueError('Range unsupported')
                        block = response.read(end - total + 1)
                    if len(block) != end - total + 1: raise ValueError('Truncated model segment')
                    break
                except Exception:
                    if attempt == 3: raise
            with temporary.open('ab') as output: output.write(block)
            total += len(block)
            if total % (128 * 1024 * 1024) < len(block): print('Native OCR model', total // 1024 // 1024, 'MiB', flush=True)
        with temporary.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != asset['lfs']['sha256']: raise ValueError('Native model hash mismatch')
        os.replace(temporary, file)
    with file.open('rb') as stream: digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return {'name': name, 'size': file.stat().st_size, 'sha256': digest}

with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool: records = list(pool.map(download, names + ['model.safetensors']))
(root / 'installed.json').write_text(json.dumps({'repository': repository, 'revision': revision, 'files': records}, indent=2), encoding='utf-8')
print('Native Paddle OCR assets verified', flush=True)
