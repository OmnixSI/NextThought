import argparse,pathlib,json,hashlib,subprocess,datetime,shutil
p=argparse.ArgumentParser();p.add_argument('--tag',required=True);p.add_argument('--commit',required=True);args=p.parse_args()
ROOT=pathlib.Path(__file__).resolve().parents[2];dist=ROOT/'dist';version=args.tag.removeprefix('v')
for source,extension in [('InterviewHacker.dmg','dmg'),('InterviewHacker-macOS.zip','zip')]:
 target=dist/f'InterviewHacker-{version}-macOS-arm64.{extension}'
 if (dist/source).exists():shutil.copyfile(dist/source,target)
 assert target.stat().st_size>1_000_000
m=json.load(open(ROOT/'Resources/DocumentRuntime/runtime-manifest.json'));runtime=dist/m['asset']
def sha(p):return hashlib.file_digest(p.open('rb'),'sha256').hexdigest()
assert sha(runtime)==m['sha256'];assert runtime.stat().st_size==m['downloadBytes']
shutil.copyfile(ROOT/'Resources/DocumentRuntime/runtime-manifest.json',dist/'runtime-manifest.json')
zipname=f'InterviewHacker-{version}-macOS-arm64.zip'
plist=dist/'InterviewHacker.app/Contents/Info.plist'
bundle=subprocess.check_output(['/usr/libexec/PlistBuddy','-c','Print :CFBundleShortVersionString',str(plist)],text=True).strip();assert bundle==version
build=subprocess.check_output(['/usr/libexec/PlistBuddy','-c','Print :CFBundleVersion',str(plist)],text=True).strip()
update={'tag':args.tag,'version':version,'build':int(build),'zip':zipname,'sha256':sha(dist/zipname),'commit':args.commit,'publishedAt':datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')}
(dist/'update.json').write_text(json.dumps(update,indent=2)+'\n')
files=[dist/f'InterviewHacker-{version}-macOS-arm64.dmg',dist/zipname,runtime,dist/'runtime-manifest.json',dist/'update.json']
(dist/'SHA256SUMS.txt').write_text(''.join(f'{sha(f)}  {f.name}\n' for f in files))
print('Validated:',', '.join(f.name for f in files))
