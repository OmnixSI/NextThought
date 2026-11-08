import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought/models/wespeaker-resnet34-lm'
root.mkdir(parents=True, exist_ok=True)
file = root / 'model.onnx'
expected = '7bb2f06e9df17cdf1ef14ee8a15ab08ed28e8d0ef5054ee135741560df2ec068'
url = 'https://huggingface.co/Wespeaker/wespeaker-resnet34-LM/resolve/f0c48c298fd835726c27956a5d617bad7115627e/voxceleb_resnet34_LM.onnx'
if not file.exists():
    data = urllib.request.urlopen(url + '?download=true', timeout=120).read()
    if len(data) != 26530309 or hashlib.sha256(data).hexdigest() != expected: raise ValueError('Voice model size/hash mismatch')
    temporary = file.with_suffix('.onnx.tmp'); temporary.write_bytes(data); os.replace(temporary, file)
with file.open('rb') as stream:
    if hashlib.file_digest(stream, 'sha256').hexdigest() != expected: raise ValueError('Voice model hash mismatch')
print('Speaker model verified', flush=True)
fixtures = Path(__file__).resolve().parents[1] / 'artifacts/p3/speaker-fixtures'
fixtures.mkdir(parents=True, exist_ok=True)
records = []
for speaker in ['fangjun', 'leijun', 'liudehua']:
    for suffix in ['-sr-1.wav', '-sr-2.wav', '-test-sr-1.wav', '-test-sr-2.wav']:
        name = speaker + suffix
        source = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/' + name
        data = urllib.request.urlopen(source, timeout=120).read()
        (fixtures / name).write_bytes(data)
        records.append({'name': name, 'source': source, 'sha256': hashlib.sha256(data).hexdigest()})
(fixtures / 'sources.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
print('Public speaker fixtures ready', flush=True)
