#!/usr/bin/env python3
"""Task-scoped MCP server. No arbitrary path, command, network or write tools."""
import sys,pathlib,json,hashlib,base64,subprocess,io,zipfile
sys.dont_write_bytecode = True
sys.path.insert(0,str(pathlib.Path(sys.executable).resolve().parents[2]/'site-packages'))
from contextlib import redirect_stdout
MANIFEST=pathlib.Path(sys.argv[1]).resolve(strict=True)
JOB=json.loads(MANIFEST.read_text());SOURCE=pathlib.Path(JOB['source']).resolve(strict=True)
if SOURCE.parent!=MANIFEST.parent/'input' or SOURCE.is_symlink():raise ValueError('invalid manifest source')
if hashlib.sha256(SOURCE.read_bytes()).hexdigest()!=JOB['sha256']:raise ValueError('source changed')
TOOLS=[
 ('file_info','Read current file type, original filename and page count',{}),
 ('read_text','Extract selectable text from PDF, Office or plain text. PDF page is 1-based; omit page to read all.',{'page':{'type':'integer','minimum':1}}),
 ('office_structure','Read Office structure and enumerate embedded image IDs',{}),
 ('view_page','View one PDF page on demand; or the original image',{'page':{'type':'integer','minimum':1}}),
 ('view_embedded_image','View an embedded image ID from office_structure',{'image_id':{'type':'string'}})]

def checked(args):
    # Any proposed file path is rejected unless it is the exact current immutable manifest entry.
    if 'path' in args and pathlib.Path(args['path']).resolve()!=SOURCE:raise ValueError('file outside current job')
    if SOURCE.resolve()!=SOURCE or hashlib.sha256(SOURCE.read_bytes()).hexdigest()!=JOB['sha256']:raise ValueError('source changed')

def native(operation,page=1):
    command=[JOB['app'],'--document-tool',str(MANIFEST),operation,str(page)]
    result=subprocess.run(command,capture_output=True,timeout=45,check=True)
    return json.loads(result.stdout)

def structured():
    import importlib.util
    spec=importlib.util.spec_from_file_location('document_worker',pathlib.Path(__file__).with_name('worker.py'));worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
    # Dedicated job output only. These files are deleted with the temporary job.
    result=worker.office({'path':str(SOURCE),'output_directory':str(MANIFEST.parent/'rendered')})
    indices=JOB.get('partIndices')
    if indices is not None:
        if not isinstance(indices,list) or not all(isinstance(i,int) and 0<=i<len(result['parts']) for i in indices):raise ValueError('invalid assigned parts')
        result['parts']=[result['parts'][i] for i in indices]
        allowed={p['image'] for p in result['parts'] if 'image' in p}
        result['images']=[image for image in result['images'] if image['path'] in allowed]
    return result

def text(value):return {'type':'text','text':value if isinstance(value,str) else json.dumps(value,ensure_ascii=False)}
def image(data,mime):return {'type':'image','mimeType':mime,'data':base64.b64encode(data).decode()}
def call(name,args):
    checked(args);suffix=SOURCE.suffix.lower();page=args.get('page',1)
    if not isinstance(page,int) or page<1:raise ValueError('page must be positive')
    if name=='file_info':
        info={'path':str(SOURCE),'name':JOB['name'],'sha256':JOB['sha256'],'bytes':SOURCE.stat().st_size,'format':suffix}
        if 'partIndices' in JOB:info['assigned_part_indices']=JOB['partIndices']
        if suffix in ('.pdf','.tiff','.heic'):info.update(native('info'))
        else:info['pages']=1
        return [text(info)]
    if name=='read_text':
        if suffix=='.pdf':return [text(native('text',args.get('page',0)))]
        if suffix in ('.doc','.odt','.rtf','.md','.markdown','.csv','.json'):return [text(native('convert'))]
        if suffix in ('.docx','.pptx','.xlsx','.html','.htm','.txt','.csv','.md','.json'):return [text([dict({k:v for k,v in p.items() if k!='image'}, **({'image_id':pathlib.Path(p['image']).name} if 'image' in p else {})) for p in structured()['parts']])]
        return [text('This image needs view_page for visual reading.')]
    if name=='office_structure':
        result=structured();images=result.pop('images',[])
        result['parts']=[dict({k:v for k,v in p.items() if k!='image'}, **({'image_id':pathlib.Path(p['image']).name} if 'image' in p else {})) for p in result['parts']]
        result['images']=[dict({k:v for k,v in x.items() if k!='path'},image_id=pathlib.Path(x['path']).name) for x in images]
        return [text(result)]
    if name=='view_page':
        if suffix in ('.pdf','.heic','.tiff'):return [{'type':'image','mimeType':'image/png','data':native('image',page)['base64']}]
        if page!=1:raise ValueError('single-page image')
        from PIL import Image
        im=Image.open(SOURCE);im.thumbnail((2400,2400));out=io.BytesIO();im.convert('RGB').save(out,format='PNG');return [image(out.getvalue(),'image/png')]
    if name=='view_embedded_image':
        candidates={pathlib.Path(x['path']).name:x['path'] for x in structured()['images']}
        if args.get('image_id') not in candidates:raise ValueError('unknown image ID')
        from PIL import Image
        im=Image.open(candidates[args['image_id']]);im.thumbnail((2400,2400));out=io.BytesIO();im.convert('RGB').save(out,format='PNG');return [image(out.getvalue(),'image/png')]
    raise ValueError('unknown tool')

def main():
    for line in sys.stdin:
        m=None
        try:
            m=json.loads(line);method=m.get('method');ident=m.get('id');result={}
            if method=='initialize':result={'protocolVersion':'2024-11-05','capabilities':{'tools':{}},'serverInfo':{'name':'interviewhacker-document','version':'1.7.0'}}
            elif method=='tools/list':result={'tools':[{'name':n,'description':d,'inputSchema':{'type':'object','properties':p,'additionalProperties':False},'annotations':{'readOnlyHint':True,'destructiveHint':False,'openWorldHint':False}} for n,d,p in TOOLS]}
            elif method=='tools/call':
                p=m['params']
                # Block extra fields, including path attempts and write destinations.
                allowed=next((set(props) for name,_,props in TOOLS if name==p['name']),None)
                if allowed is None or set(p.get('arguments',{}))-allowed:raise ValueError('unsupported argument or file outside task')
                with redirect_stdout(sys.stderr):content=call(p['name'],p.get('arguments',{}))
                result={'content':content}
            elif method=='ping':result={}
            elif ident is None:continue
            else:raise ValueError('unsupported method')
            if ident is not None:print(json.dumps({'jsonrpc':'2.0','id':ident,'result':result},ensure_ascii=False),flush=True)
        except Exception as e:
            if m and m.get('id') is not None:print(json.dumps({'jsonrpc':'2.0','id':m['id'],'error':{'code':-32602,'message':str(e)}}),flush=True)
if __name__=='__main__':main()
