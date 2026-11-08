"""Prepare the same verified X-ASR assets as the app, plus public test WAVs.

Developer verification only. The app has its own model installer and needs no Python.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import sys
import tarfile
import time
import urllib.request

sys.stdout.reconfigure(encoding='utf-8')
ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = ROOT / '.cache'
RESOURCES = ROOT / 'src/NextThought.Platform/Resources'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--data-root', type=pathlib.Path, required=True)
    args = parser.parse_args()
    CACHE.mkdir(exist_ok=True)
    catalog = json.loads((RESOURCES/'models.json').read_text(encoding='utf-8'))
    for model in [m for m in catalog if m['id'] in ('xasr480','xasrOffline')]:
        archive = CACHE/(model['archive']+'.tar.bz2')
        if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest() != model['sha256']:
            for attempt in range(6):
                try:
                    url='https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/'+model['archive']+'.tar.bz2'
                    print('Downloading',model['id'],flush=True)
                    with urllib.request.urlopen(url,timeout=60) as response, archive.open('wb') as target:
                        total=last=0
                        while block:=response.read(1024*1024):
                            target.write(block);total+=len(block)
                            if total-last>=20*1024*1024:print('Downloaded',total//1024//1024,'MiB',flush=True);last=total
                    if hashlib.sha256(archive.read_bytes()).hexdigest()!=model['sha256']:raise ValueError('Model SHA-256 mismatch')
                    break
                except (OSError,ValueError) as error:
                    print('Retry',attempt+1,type(error).__name__,flush=True)
                    if attempt==5:raise
                    time.sleep(2)
        stage=CACHE/('extract-'+model['id']);stage.mkdir(parents=True,exist_ok=True)
        with tarfile.open(archive,'r:bz2') as package:package.extractall(stage,filter='data')
        extracted=stage/model['archive'];target=args.data_root/'models'/model['archive'];target.mkdir(parents=True,exist_ok=True)
        for name,size in model['files'].items():
            source=extracted/name
            assert source.stat().st_size==size,name
            shutil.copyfile(source,target/name)
        shutil.copyfile(RESOURCES/'xasr-bpe.vocab',target/'bpe.vocab')
        (target/'installed.json').write_text(json.dumps({'archive':model['archive'],'sha256':model['sha256']}),encoding='utf-8')
        sample_root=CACHE/'test-audio';sample_root.mkdir(exist_ok=True)
        for audio in extracted.rglob('*.wav'):
            if audio.stat().st_size < 4*1024*1024:shutil.copyfile(audio,sample_root/(model['id']+'-'+audio.name))
        stage.resolve().relative_to(CACHE.resolve())
        shutil.rmtree(stage)
        print('Verified and installed:',model['id'],'test WAVs:',len(list(sample_root.glob('*.wav'))),flush=True)


if __name__=='__main__':main()
