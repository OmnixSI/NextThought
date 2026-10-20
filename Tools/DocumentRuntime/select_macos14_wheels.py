import hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parent;D=ROOT/'wheels';D.mkdir(exist_ok=True);out=[]
for pkg in ['mlx','mlx-metal']:
 d=json.loads(subprocess.check_output(['curl','-fLsS',f'https://pypi.org/pypi/{pkg}/0.32.4/json'],text=True))
 a=next(x for x in d['urls'] if 'macosx_14_0_arm64' in x['filename'] and ('cp312' in x['filename'] or pkg=='mlx-metal'))
 p=D/a['filename'];subprocess.run(['curl','-fLsS','-o',str(p),a['url']],check=True)
 assert hashlib.sha256(p.read_bytes()).hexdigest()==a['digests']['sha256']
 out.append(dict(package=pkg,file=str(p),url=a['url'],sha256=a['digests']['sha256'],bytes=a['size']))
(ROOT/'wheels-macos14.json').write_text(json.dumps(out,indent=2))
