"""Repository-local foreground Actions runner, always unregistered on exit. No service."""
import pathlib,subprocess,json,hashlib,tarfile,os,sys,signal
ROOT=pathlib.Path(__file__).resolve().parents[2];DIR=ROOT/'.local/actions-runner';REPO='OmnixSI/InterviewHacker'
def api(path,post=False):
 args=['gh','api']+(['-X','POST'] if post else [])+[path]
 return json.loads(subprocess.check_output(args))
def remove():
 if (DIR/'.runner').exists():
  token=api(f'repos/{REPO}/actions/runners/remove-token',True)['token']
  subprocess.run([str(DIR/'config.sh'),'remove','--token',token],cwd=DIR,check=True)
if '--remove' in sys.argv:remove();print('Temporary runner unregistered');sys.exit(0)
if '--status' in sys.argv:
 for runner in api(f'repos/{REPO}/actions/runners')['runners']:print(runner['name'],runner['status'],'busy='+str(runner['busy']))
 sys.exit(0)
DIR.mkdir(parents=True,exist_ok=True)
if not (DIR/'config.sh').exists():
 release=api('repos/actions/runner/releases/latest');asset=next(x for x in release['assets'] if 'osx-arm64' in x['name'] and x['name'].endswith('.tar.gz'))
 archive=DIR/'runner.tar.gz';subprocess.run(['curl','-fLsS','--retry','3','-o',str(archive),asset['browser_download_url']],check=True)
 sha=hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()
 if asset.get('digest') and asset['digest']!='sha256:'+sha:raise SystemExit('Runner SHA-256 mismatch')
 with tarfile.open(archive) as tar:tar.extractall(DIR,filter='data')
 (DIR/'release.json').write_text(json.dumps({'tag':release['tag_name'],'sha256':sha},indent=2))
if not (DIR/'.runner').exists():
 token=api(f'repos/{REPO}/actions/runners/registration-token',True)['token']
 subprocess.run([str(DIR/'config.sh'),'--url',f'https://github.com/{REPO}','--token',token,'--name','interviewhacker-document-validation','--labels','interviewhacker-documents','--work','_work','--unattended','--disableupdate'],cwd=DIR,check=True)
process=None
try:
 process=subprocess.Popen([str(DIR/'run.sh')],cwd=DIR,start_new_session=True);process.wait()
except KeyboardInterrupt:
 if process:
  os.killpg(process.pid,signal.SIGINT)
  try:process.wait(timeout=30)
  except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGTERM);process.wait(timeout=10)
finally:remove()
