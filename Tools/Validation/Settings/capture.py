"""Render isolated SwiftUI demo states. Does not request devices, downloads or answers."""
import argparse, pathlib, plistlib, os, subprocess, time, json, hashlib
p = argparse.ArgumentParser()
p.add_argument('--app', required=True)
p.add_argument('--output', required=True)
p.add_argument('--data', required=True)
a = p.parse_args()
app = pathlib.Path(a.app).resolve()
info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
assert '.ci.' in info['CFBundleIdentifier'], 'Use an isolated CI bundle identity'
out = pathlib.Path(a.output).resolve(); out.mkdir(parents=True, exist_ok=True)
data = pathlib.Path(a.data).resolve(); data.mkdir(parents=True, exist_ok=True)
cases = []
for index, title in enumerate(['welcome', 'sample', 'sound', 'voice', 'answers', 'materials', 'start']):
    cases.append((f'guide-{title}', 'assistant', f'quickStartStep={index}', False, True, {}))
for title in ['基础', '识别', '回答', '资料', '提词器', '数据', '更多']:
    cases.append((f'basic-{title}', 'settings', f'settingsGroup={title}', False, False, {}))
for title in ['基础', '识别', '回答', '资料', '提词器', '技能', '数据', '更多']:
    cases.append((f'advanced-{title}', 'settings', f'settingsGroup={title}', True, False, {}))
cases += [
    ('documents-installed', 'settings', 'settingsGroup=资料', False, False,
     {'INTERVIEWHACKER_DEMO_DOCUMENTS_READY':'1', 'INTERVIEWHACKER_DEMO_DOCUMENT_PROVIDER':'paddle', 'INTERVIEWHACKER_DEMO_SEMANTIC':'1'}),
    ('basic-api', 'settings', 'settingsGroup=回答', False, False, {'INTERVIEWHACKER_DEMO_API':'1'}),
    ('guide-missing-core', 'assistant', 'quickStartStep=6', False, True, {'INTERVIEWHACKER_DEMO_VOICE_READY':'0'}),
]
results = []
for name, section, defaults, advanced, guide, extra in cases:
    image = out / (name + '.png')
    if image.exists(): image.unlink()
    env = os.environ.copy()
    env.update(INTERVIEWHACKER_DEMO='1', INTERVIEWHACKER_DEMO_IDLE='1', INTERVIEWHACKER_DEMO_HIDDEN='1',
               INTERVIEWHACKER_DATA_DIR=str(data), INTERVIEWHACKER_DEMO_SECTION=section,
               INTERVIEWHACKER_DEMO_DEFAULTS=defaults, INTERVIEWHACKER_DEMO_FRAME='80,80,1120,820',
               INTERVIEWHACKER_DEMO_SNAPSHOT=str(image), INTERVIEWHACKER_DEMO_ONBOARDING='1' if guide else '0',
               INTERVIEWHACKER_DEMO_ADVANCED='1' if advanced else '0', INTERVIEWHACKER_DEMO_DOCUMENT_PROVIDER='system',
               INTERVIEWHACKER_DEMO_SEMANTIC='0', INTERVIEWHACKER_DEMO_VOICE_READY='1')
    env.update(extra)
    with (out / (name + '.log')).open('wb') as log:
        child = subprocess.Popen([str(app / 'Contents/MacOS/InterviewHacker')], env=env, stdout=log, stderr=log)
        try:
            for _ in range(100):
                if image.exists() and image.stat().st_size > 10000: break
                if child.poll() is not None: raise RuntimeError(f'{name}: App exited {child.returncode}')
                time.sleep(.1)
            assert image.exists() and image.read_bytes().startswith(b'\x89PNG\r\n\x1a\n'), name
        finally:
            child.terminate()
            try: child.wait(timeout=15)
            except subprocess.TimeoutExpired: child.kill(); child.wait()
    results.append({'case':name, 'bytes':image.stat().st_size,'sha256':hashlib.file_digest(image.open('rb'),'sha256').hexdigest()})
    print('Captured', name, flush=True)
(out / 'manifest.json').write_text(json.dumps({'states':results, 'demoOnly':True, 'onlineModelCalls':0, 'deviceRequests':0}, ensure_ascii=False, indent=2)+'\n')
