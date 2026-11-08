"""Development helper. Uses the same pinned manifest as the Windows installer."""
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'src/NextThought.Platform/Resources/embedding-model.json').read_text())
target = Path(os.environ['LOCALAPPDATA']) / 'NextThought/models' / manifest['id']
target.mkdir(parents=True, exist_ok=True)
for asset in manifest['files']:
    file = target / asset['name']
    if file.exists() and file.stat().st_size == asset['size'] and hashlib.file_digest(file.open('rb'), 'sha256').hexdigest() == asset['sha256']:
        print('Verified existing', asset['name'], flush=True)
        continue
    url = f"https://huggingface.co/{manifest['repository']}/resolve/{manifest['revision']}/{asset.get('remote', asset['name'])}"
    temporary = file.with_suffix(file.suffix + '.partial')
    request = urllib.request.Request(url, headers={'User-Agent': 'NextThought-Windows/0.3'})
    digest = hashlib.sha256(); total = 0; report = 0
    with urllib.request.urlopen(request, timeout=120) as response, temporary.open('wb') as output:
        while chunk := response.read(1024 * 1024):
            output.write(chunk); digest.update(chunk); total += len(chunk)
            if total - report >= 40 * 1024 * 1024:
                print(asset['name'], total // 1024 // 1024, 'MiB', flush=True); report = total
    if total != asset['size'] or digest.hexdigest() != asset['sha256']:
        raise ValueError('Model size or hash mismatch: ' + asset['name'])
    os.replace(temporary, file)
    print('Verified', asset['name'], total, flush=True)
(target / 'installed.json').write_text(json.dumps(manifest), encoding='utf-8')
print('Ready:', target, flush=True)
