"""Package a validated relocatable runtime; does not install anything globally.
Usage: python3 package_runtime.py --source DIR --output dist
The source must be built with bootstrap-runtime.sh and macOS14 MLX wheels.
"""
import argparse,pathlib,shutil,subprocess,tarfile,hashlib,json,os
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--output',default='dist');args=p.parse_args()
source=pathlib.Path(args.source).resolve();root=pathlib.Path(__file__).resolve().parents[2];out=pathlib.Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
stage=root/'.local/runtime-package';stage.mkdir(parents=True,exist_ok=True);runtime=stage/'document-runtime'
if runtime.exists():shutil.rmtree(runtime)
shutil.copytree(source,runtime,symlinks=True,ignore=shutil.ignore_patterns('__pycache__','*.pyc','installed.json','cache'))
for file in runtime.rglob('*'):
 if file.is_symlink() and not file.resolve().is_relative_to(runtime):raise SystemExit('External runtime symlink: '+str(file))
# No executable scripts with a build-machine shebang; the app invokes the interpreter directly.
for file in (runtime/'site-packages').rglob('bin/*'):
 if file.is_file() and file.read_bytes().startswith(b'#!') and b'/.local/' in file.read_bytes().splitlines()[0]:file.unlink()
# Mach-O files carry self-verifying ad-hoc signatures; no development private key is copied into an asset.
checked=[]
for file in runtime.rglob('*'):
 if file.is_symlink() or not file.is_file():continue
 magic=file.open('rb').read(4)
 if magic in (b'\xcf\xfa\xed\xfe',b'\xfe\xed\xfa\xcf',b'\xca\xfe\xba\xbe'):
  load=subprocess.check_output(['/usr/bin/otool','-l',str(file)],text=True)
  import re
  versions=re.findall(r'(?:minos|version)\s+(\d+\.\d+(?:\.\d+)?)',load)
  # Only LC_BUILD_VERSION/LC_VERSION_MIN_MACOSX lines. Generic dylib version fields are irrelevant.
  minimum=[]
  for command in load.split('Load command'):
   if 'LC_BUILD_VERSION' in command:minimum += re.findall(r'minos\s+([\d.]+)',command)
   elif 'LC_VERSION_MIN_MACOSX' in command:minimum += re.findall(r'version\s+([\d.]+)',command)
  if any(int(v.split('.')[0])>14 for v in minimum):raise SystemExit('Requires newer macOS: '+str(file))
  subprocess.run(['/usr/bin/codesign','--force','--sign','-','--timestamp=none',str(file)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
  checked.append({'path':str(file.relative_to(runtime)),'minimum':minimum})
shutil.copytree(root/'Resources/DocumentRuntime/licenses',runtime/'licenses',dirs_exist_ok=True)
shutil.copy(root/'Resources/DocumentRuntime/THIRD_PARTY_RUNTIME.json',runtime/'THIRD_PARTY_RUNTIME.json')
shutil.copy(root/'Tools/DocumentRuntime/requirements.lock.txt',runtime/'requirements.lock.txt')
shutil.copy(root/'Tools/DocumentRuntime/requirements.hashed.txt',runtime/'requirements.hashed.txt')
(runtime/'deployment.json').write_text(json.dumps(checked,indent=2))
asset=out/'InterviewHacker-document-runtime-1.7.0-macOS-arm64.tar.gz'
with tarfile.open(asset,'w:gz',compresslevel=9,dereference=False) as tar:tar.add(runtime,arcname='document-runtime')
sha=hashlib.file_digest(asset.open('rb'),'sha256').hexdigest()
manifest={'version':'1.7.0','asset':asset.name,'sha256':sha,'downloadBytes':asset.stat().st_size,'installedBytes':sum(f.stat().st_size for f in runtime.rglob('*') if f.is_file() and not f.is_symlink()),'python':'3.12.13','platform':'macOS 14+ arm64','packages':61,'nativeFilesChecked':len(checked)}
text=json.dumps(manifest,indent=2)+'\n';(out/'runtime-manifest.json').write_text(text);(root/'Resources/DocumentRuntime/runtime-manifest.json').write_text(text)
print(text)
