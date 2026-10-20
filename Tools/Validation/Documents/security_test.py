"""Offline probes of the real MCP server. Requires packaged app and explicit runtime."""
import argparse,pathlib,subprocess,tempfile,json,hashlib,shutil,time,sys,os
p=argparse.ArgumentParser();p.add_argument('--runtime',required=True);p.add_argument('--app',required=True);args=p.parse_args()
ROOT=pathlib.Path(__file__).resolve().parents[3];runtime=pathlib.Path(args.runtime).resolve();app=pathlib.Path(args.app).resolve();python=runtime/'python/bin/python3'
with tempfile.TemporaryDirectory(dir=ROOT/'.local',prefix='document-security-') as td:
 job=pathlib.Path(td);(job/'input').mkdir();source=job/'input/source.pdf';shutil.copy(ROOT/'Tools/Validation/Documents/Fixtures/02-english-jd.pdf',source)
 manifest=job/'manifest.json';manifest.write_text(json.dumps({'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'name':'test.pdf','app':str(app/'Contents/MacOS/InterviewHacker')}))
 proc=subprocess.Popen([str(python),'-I',str(app/'Contents/Resources/DocumentRuntime/document_mcp.py'),str(manifest)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
 def request(method,params={}):
  request.i+=1;proc.stdin.write(json.dumps({'jsonrpc':'2.0','id':request.i,'method':method,'params':params})+'\n');proc.stdin.flush();return json.loads(proc.stdout.readline())
 request.i=0
 assert 'result' in request('initialize')
 names={t['name'] for t in request('tools/list')['result']['tools']}
 assert names=={'file_info','read_text','office_structure','view_page','view_embedded_image'}
 assert 'result' in request('tools/call',{'name':'file_info','arguments':{}})
 assert 'Backend Engineer' in str(request('tools/call',{'name':'read_text','arguments':{}}))
 for args2 in [{'path':'/etc/passwd'},{'path':str(job/'input/../manifest.json')},{'destination':str(job/'write.txt')},{'url':'https://example.com'}]:
  assert 'error' in request('tools/call',{'name':'read_text','arguments':args2})
 assert 'error' in request('tools/call',{'name':'write_file','arguments':{'text':'bad'}})
 assert not (job/'write.txt').exists()
 assert 'error' in request('tools/call',{'name':'read_text','arguments':{'page':9999}})
 source.write_bytes(b'changed')
 assert 'error' in request('tools/call',{'name':'read_text','arguments':{}})
 proc.terminate();proc.wait(timeout=5)
 # Separate job fails closed for a replaced symlink to an unrelated file.
 source.unlink();source.symlink_to(ROOT/'Package.swift')
 r=subprocess.run([str(python),'-I',str(app/'Contents/Resources/DocumentRuntime/document_mcp.py'),str(manifest)],input='',capture_output=True,text=True)
 assert r.returncode!=0
 # Exercise Office's dynamic worker import against a signed App, including image IDs and slide provenance.
 source.unlink();source=job/'input/source.pptx'
 code="""import sys,pathlib,io
sys.path.insert(0,sys.argv[1])
from pptx import Presentation
from pptx.util import Inches
from PIL import Image
presentation=Presentation()
for color in ['red','blue']:
 slide=presentation.slides.add_slide(presentation.slide_layouts[6]);out=io.BytesIO();Image.new('RGB',(40,40),color).save(out,format='PNG');out.seek(0);slide.shapes.add_picture(out,Inches(1),Inches(1))
presentation.save(sys.argv[2])
"""
 subprocess.run([str(python),'-I','-c',code,str(runtime/'site-packages'),str(source)],check=True)
 manifest.write_text(json.dumps({'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'name':'test.pptx','app':str(app/'Contents/MacOS/InterviewHacker')}))
 proc=subprocess.Popen([str(python),'-I',str(app/'Contents/Resources/DocumentRuntime/document_mcp.py'),str(manifest)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
 request('initialize')
 response=request('tools/call',{'name':'office_structure','arguments':{}})
 structure=json.loads(response['result']['content'][0]['text'])
 image_parts=[p for p in structure['parts'] if 'image_id' in p]
 assert len(image_parts)==2 and {p['locator']['slide'] for p in image_parts}=={1,2}
 assert len({p['image_id'] for p in image_parts})==2
 assert {x['slide'] for x in structure['images']}=={1,2}
 for part in image_parts:
  response=request('tools/call',{'name':'view_embedded_image','arguments':{'image_id':part['image_id']}})
  assert response['result']['content'][0]['type']=='image'
 proc.terminate();proc.wait(timeout=5)
 # Native compatibility formats: original HEIC/TIFF and legacy RTF, without a model call.
 rtf=job/'input/source.rtf';rtf.write_text(r"{\rtf1\ansi Native RTF original file\par exact 1234}")
 heic=job/'input/source.heic'
 subprocess.run(['/usr/bin/sips','-s','format','heic',str(ROOT/'Tools/Validation/Documents/Fixtures/03-bilingual-columns.png'),'--out',str(heic)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
 tiff=job/'input/source.tiff'
 code="import sys;sys.path.insert(0,sys.argv[1]);from PIL import Image;a=Image.new('RGB',(40,40),'white');b=Image.new('RGB',(40,40),'black');a.save(sys.argv[2],save_all=True,append_images=[b])"
 subprocess.run([str(python),'-I','-c',code,str(runtime/'site-packages'),str(tiff)],check=True)
 for path,tool,arguments,expected in [(rtf,'read_text',{},'Native RTF original file'),(heic,'view_page',{'page':1},None),(tiff,'view_page',{'page':2},None)]:
  manifest.write_text(json.dumps({'source':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'name':path.name,'app':str(app/'Contents/MacOS/InterviewHacker')}))
  proc=subprocess.Popen([str(python),'-I',str(app/'Contents/Resources/DocumentRuntime/document_mcp.py'),str(manifest)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
  request('initialize');response=request('tools/call',{'name':tool,'arguments':arguments})
  assert 'result' in response,response
  if expected:assert expected in str(response)
  else:assert response['result']['content'][0]['type']=='image'
  if path==tiff:
   info=request('tools/call',{'name':'file_info','arguments':{}})
   assert json.loads(info['result']['content'][0]['text'])['pages']==2
  proc.terminate();proc.wait(timeout=5)
 env=dict(os.environ,INTERVIEWHACKER_DATA_DIR=str(job/'isolated-data'))
 output=job/'tiff-output'
 subprocess.run([str(app/'Contents/MacOS/InterviewHacker'),'--self-test','documents','--provider','system','--input',str(tiff),'--output',str(output)],env=env,check=True,stdout=subprocess.DEVNULL)
 converted=json.loads((output/'source.tiff.json').read_text())
 assert {block['locator']['page'] for block in converted['blocks']}=={1,2}
 subprocess.run(['/usr/bin/codesign','--verify','--deep','--strict',str(app)],check=True)
 assert not list((app/'Contents/Resources/DocumentRuntime').glob('__pycache__'))
print('PASS real MCP allowlist, read-only tools, path/traversal/network/write rejection, page bounds, mutation and symlink rejection; Office image IDs/slide association; HEIC/multi-page TIFF/RTF native reads; signed App stays intact')
