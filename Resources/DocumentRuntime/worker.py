#!/usr/bin/env python3
"""Version 1 NDJSON document/MLX worker. stdout is exclusively protocol.
No model downloads, remote code, shell execution or network operations.
"""
import sys, pathlib, os
ROOT = pathlib.Path(sys.executable).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'site-packages'))
import json, threading, concurrent.futures, traceback, re, html, zipfile, hashlib
from contextlib import redirect_stdout
from io import StringIO
PROTOCOL_OUT = sys.stdout
LOCK = threading.Lock()
CANCELLED = set()
MODELS = {}
COUNTERS = {"embeddings": 0}
EXECUTOR = concurrent.futures.ThreadPoolExecutor(max_workers=1)

def emit(message):
    with LOCK:
        PROTOCOL_OUT.write(json.dumps(message, ensure_ascii=False, allow_nan=False) + '\n'); PROTOCOL_OUT.flush()

def checkpoint(job):
    if job in CANCELLED: raise InterruptedError('任务已取消')

def otsl(text):
    """Restore spans from Paddle OTSL; emit HTML so merged cells survive."""
    if '<fcel>' not in text and '<ecel>' not in text: return text
    tokens = re.split(r'(<(?:fcel|ecel|lcel|ucel|xcel|nl)>)', text)
    grid, row, pending = [], [], None
    for t in tokens:
        if t == '<nl>':
            if row: grid.append(row); row = []
            pending = None
        elif t.startswith('<') and t.endswith('>') and t[1:-1] in ['fcel','ecel','lcel','ucel','xcel']:
            pending = {'type': t[1:-1], 'text': ''}; row.append(pending)
        elif pending is not None: pending['text'] += t
    if row: grid.append(row)
    cells, owners = [], {}
    for y, r in enumerate(grid):
        cells.append([])
        for x, c in enumerate(r):
            kind = c['type']
            if kind in ('lcel', 'xcel') and (y,x-1) in owners: owner = owners[y,x-1]
            elif kind in ('ucel','xcel') and (y-1,x) in owners: owner = owners[y-1,x]
            else:
                owner = {'text': c['text'].strip(), 'x':x, 'y':y, 'right':x, 'bottom':y}; cells[y].append(owner)
            owners[y,x] = owner; owner['right']=max(owner['right'],x);owner['bottom']=max(owner['bottom'],y)
    output = ['<table>']
    for r in cells:
        output.append('<tr>')
        for c in r:
            attrs = ''
            if c['right']>c['x']: attrs += f' colspan="{c["right"]-c["x"]+1}"'
            if c['bottom']>c['y']: attrs += f' rowspan="{c["bottom"]-c["y"]+1}"'
            output.append('<td'+attrs+'>'+html.escape(c['text']).replace('\n','<br>')+'</td>')
        output.append('</tr>')
    return '\n'.join(output+['</table>'])

def embedding(payload):
    COUNTERS["embeddings"] += 1
    import mlx.core as mx
    from mlx_lm import load
    path = payload['model_directory']
    if ('embed',path) not in MODELS: MODELS['embed',path] = load(path)
    model, tokenizer = MODELS['embed',path]
    text = payload['text']
    if payload.get('query'): text = 'Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: '+text
    ids = tokenizer.encode(text)
    if not ids: raise ValueError('不能为无内容文本生成向量')
    if len(ids)>8192: raise ValueError('单片段超过 8192 token；请重新切片')
    h=model.model(mx.array([ids]));v=h[:,-1,:].astype(mx.float32)
    v /= mx.linalg.norm(v,axis=-1,keepdims=True);mx.eval(v)
    return {'vector': v[0].tolist(), 'dimension':1024}

def reading_order(regions, width):
    # Remove nested inline formulas/text already represented by an enclosing table or paragraph.
    def inside(a,b):
        x1,y1,x2,y2=a;u1,v1,u2,v2=b
        return x1>=u1-3 and y1>=v1-3 and x2<=u2+3 and y2<=v2+3
    clean=[]
    for r in regions:
        _,label,box=r
        if any(other is not r and inside(box,other[2]) and
               (other[1]==21 and label!=21 or label==15 and other[1]==22) for other in regions):continue
        if any(inside(box,other[2]) and inside(other[2],box) for other in clean):continue
        clean.append(r)
    # A stable whitespace gutter indicates independent columns. Spanning titles divide the page into bands.
    def order(items):
        if len(items)<2:return items
        narrow=[r for r in items if r[2][2]-r[2][0]<width*.65]
        spanning=[r for r in items if r not in narrow]
        intervals=sorted((r[2][0],r[2][2]) for r in narrow)
        gaps=[];right=None
        for left,end in intervals:
            if right is not None and left-right>width*.04 and width*.2<(left+right)/2<width*.8:gaps.append((left-right,(left+right)/2))
            right=max(right or end,end)
        if not gaps:return sorted(items,key=lambda r:(r[0],r[2][1],r[2][0]))
        cut=max(gaps)[1];left=[r for r in narrow if (r[2][0]+r[2][2])/2<cut];right=[r for r in narrow if r not in left]
        if len(left)<2 or len(right)<2:return sorted(items,key=lambda r:r[0])
        result=[];remaining=narrow
        for barrier in sorted(spanning,key=lambda r:r[2][1]):
            above=[r for r in remaining if r[2][1]<barrier[2][1]]
            result += sorted([r for r in above if r in left],key=lambda r:r[2][1])+sorted([r for r in above if r in right],key=lambda r:r[2][1])+[barrier]
            remaining=[r for r in remaining if r not in above]
        return result+sorted([r for r in remaining if r in left],key=lambda r:r[2][1])+sorted([r for r in remaining if r in right],key=lambda r:r[2][1])
    return order(clean)

def recognize(payload, job):
    from PIL import Image
    import mlx.core as mx
    from mlx_vlm import load, generate
    from mlx_vlm.prompt_utils import apply_chat_template
    from mlx_vlm.utils import load_config
    path=payload['model_directory'];image=Image.open(payload['image']).convert('RGB')
    if ('ocr',path) not in MODELS: MODELS['ocr',path]=load(path)
    model,processor=MODELS['ocr',path]
    regions=[]
    if payload.get('layout_directory'):
        import numpy as np, cv2, onnxruntime as ort
        layout=payload['layout_directory']
        if ('layout',layout) not in MODELS: MODELS['layout',layout]=ort.InferenceSession(str(pathlib.Path(layout)/'inference.onnx'),providers=['CPUExecutionProvider'])
        w,h=image.size
        blob=cv2.resize(np.asarray(image),(800,800),interpolation=cv2.INTER_CUBIC).astype('float32')/255
        out=MODELS['layout',layout].run(None,{'image':blob.transpose(2,0,1)[None],'im_shape':np.array([[800,800]],dtype='float32'),'scale_factor':np.array([[800/h,800/w]],dtype='float32')})
        for r in out[0][:int(out[1][0])]:
            if float(r[1])<.35:continue
            label=int(r[0]);x1,y1,x2,y2=map(float,r[2:6])
            box=(max(0,int(x1)-4),max(0,int(y1)-4),min(w,int(x2)+4),min(h,int(y2)+4))
            if box[2]>box[0] and box[3]>box[1]:regions.append((float(r[6]),label,box))
        regions = reading_order(regions, w)

    if not regions:regions=[(0,22,(0,0,*image.size))]
    blocks=[]
    for i,(_,label,box) in enumerate(regions):
        checkpoint(job)
        task='Table Recognition:' if label==21 else 'Formula Recognition:' if label in (5,15) else 'OCR:'
        prompt=apply_chat_template(processor,load_config(path),task,num_images=1)
        crop=image.crop(box)
        # Bound is explicit: reaching the generation limit fails this page, never returns a silently clipped page.
        result=generate(model,processor,prompt,image=[crop],max_tokens=4096,temperature=0.0,verbose=False)
        checkpoint(job)
        if getattr(result,'generation_tokens',0)>=4096:raise ValueError('区域文字超过生成上限，请使用更小的页面或其他接口')
        text=result.text.strip()
        if label==21:text=otsl(text)
        elif label in (5,15):
            text=re.sub(r'^\\\[|\\\]$', '', text).strip()
            text='$$\n'+text+'\n$$'
        elif label in (6,17):text='## '+text
        if text:blocks.append({'markdown':text,'kind':'table' if label==21 else 'formula' if label in (5,15) else 'paragraph','region':list(box)})
        emit({'version':1,'id':job,'event':'progress','completed':i+1,'total':len(regions)})
    return {'markdown':'\n\n'.join(b['markdown'] for b in blocks),'blocks':blocks,'components':{'mlx-vlm':'0.7.9','layout':'PP-DocLayoutV3'}}

def validate_office_archive(archive):
    entries=archive.infolist()
    if len(entries)>100000:raise ValueError('Office 包含过多内容项')
    if sum(x.file_size for x in entries)>2*1024*1024*1024:raise ValueError('Office 解压内容超过2GiB，未导入')
    for entry in entries:
        path=pathlib.PurePosixPath(entry.filename)
        if path.is_absolute() or '..' in path.parts:raise ValueError('Office 含有不安全路径')
        if entry.file_size>512*1024*1024:raise ValueError('Office 单个内容项超过512MiB')
        if entry.file_size>16*1024*1024 and entry.file_size/max(1,entry.compress_size)>1000:raise ValueError('Office 解压比例过高')

def slide_table(table):
    import html
    rows=[]
    for row in table.rows:
        cells=[]
        for cell in row.cells:
            if getattr(cell,'is_spanned',False):continue
            cols=getattr(cell,'span_width',1);span_rows=getattr(cell,'span_height',1)
            attrs=(' colspan="'+str(cols)+'"' if cols>1 else '')+(' rowspan="'+str(span_rows)+'"' if span_rows>1 else '')
            cells.append('<td'+attrs+'>'+html.escape(cell.text).replace('\n','<br>')+'</td>')
        rows.append('<tr>'+''.join(cells)+'</tr>')
    return '<table>\n'+'\n'.join(rows)+'\n</table>'

def read_slide_shapes(shapes,locator,parts,save_image,warnings,job=None):
    for shape in shapes:
        checkpoint(job)
        if int(shape.shape_type)==6:
            read_slide_shapes(shape.shapes,locator,parts,save_image,warnings,job);continue
        if getattr(shape,'has_text_frame',False) and shape.text.strip():parts.append(dict(markdown=shape.text,locator=locator))
        if getattr(shape,'has_table',False):parts.append(dict(markdown=slide_table(shape.table),locator=locator))
        if getattr(shape,'has_chart',False):
            chart=shape.chart;lines=['### 图表']
            if chart.has_title:lines.append(chart.chart_title.text_frame.text)
            try:
                categories=[str(c.label) for c in chart.plots[0].categories]
                for series in chart.series:
                    values=list(series.values)
                    lines.append('系列：'+str(series.name))
                    lines.append(mdtable([['类别','数值']]+[[categories[n] if n<len(categories) else str(n+1),str(v)] for n,v in enumerate(values)]))
                parts.append(dict(markdown='\n'.join(lines),locator=locator))
            except Exception as error:warnings.append('幻灯片 '+str(locator['slide'])+'：图表数据无法完整提取：'+str(error))
        if int(shape.shape_type)==13:save_image(shape.image.blob,'image.'+shape.image.ext,locator)
        # SmartArt/diagram relationships need rendering; do not label extraction complete.
        xml=getattr(shape,'_element',None)
        if xml is not None and 'drawingml/2006/diagram' in str(getattr(xml,'xml','')):
            warnings.append('幻灯片 '+str(locator['slide'])+'：包含 SmartArt，请核对图形关系；文字已尽量提取。')

def office(payload, job=None):
    from markitdown import MarkItDown
    path=pathlib.Path(payload['path']);parts=[];images=[];warnings=[]
    if path.suffix.lower() in ('.pptx','.docx','.xlsx'):
        with zipfile.ZipFile(path) as archive:
            validate_office_archive(archive)
    checkpoint(job)
    target=pathlib.Path(payload.get('output_directory') or path.parent/'rendered')
    def save_image(data,name,locator):
        if len(data)>128*1024*1024:raise ValueError('嵌图超过128MiB，未静默跳过')
        target.mkdir(exist_ok=True)
        dest=target/(hashlib.sha256(data).hexdigest()+pathlib.Path(name).suffix.lower())
        dest.write_bytes(data)
        info=dict(path=str(dest),name=name,**locator);images.append(info)
        parts.append(dict(markdown='',image=str(dest),name=name,locator=locator))
    if path.suffix.lower()=='.pptx':
        from pptx import Presentation
        presentation=Presentation(path)
        for i,slide in enumerate(presentation.slides):
            checkpoint(job);loc={'slide':i+1};start=len(parts)
            read_slide_shapes(slide.shapes,loc,parts,save_image,warnings,job)
            if slide.has_notes_slide:
                notes=slide.notes_slide.notes_text_frame
                if notes is not None and notes.text.strip():parts.append(dict(markdown='### 演讲备注\n'+notes.text,locator=loc))
            if len(parts)==start:parts.append(dict(markdown='[空白幻灯片或未提取到内容]',locator=loc));warnings.append('幻灯片 '+str(i+1)+'：请核对空白页或不可提取图形。')
            emit({'version':1,'id':job,'event':'progress','completed':i+1,'total':len(presentation.slides)})
    elif path.suffix.lower()=='.xlsx':
        import openpyxl
        workbook=openpyxl.load_workbook(path,data_only=False,read_only=False)
        for sheet in workbook:
            if sheet.max_row*sheet.max_column>200000:raise ValueError('工作表展开区域超过200000单元格，请清除远端格式或拆分')
            rows=[]
            for i,row in enumerate(sheet):
                if i%50==0:checkpoint(job)
                rows.append([str(c.value) if c.value is not None else '' for c in row])
            parts.append(dict(markdown='## '+sheet.title+'\n\n'+mdtable(rows),locator={'sheet':sheet.title}))
            for image in sheet._images:save_image(image._data(),'image.'+image.format,{'sheet':sheet.title})
    elif path.suffix.lower()=='.docx':
        import xml.etree.ElementTree as ET
        with zipfile.ZipFile(path) as archive:
            ns={'r':'http://schemas.openxmlformats.org/officeDocument/2006/relationships','a':'http://schemas.openxmlformats.org/drawingml/2006/main'}
            rels=ET.fromstring(archive.read('word/_rels/document.xml.rels'))
            mapping={r.attrib['Id']:r.attrib['Target'] for r in rels if r.attrib.get('TargetMode')!='External'}
            body=ET.fromstring(archive.read('word/document.xml'))
            ordered=[mapping.get(e.attrib.get('{'+ns['r']+'}embed')) for e in body.findall('.//a:blip',ns)]
            markdown=MarkItDown(enable_plugins=False).convert(str(path)).text_content
            chunks=re.split(r'!\[[^\]]*\]\([^)]*\)',markdown)
            for i,chunk in enumerate(chunks):
                if chunk.strip():parts.append(dict(markdown=chunk,locator={}))
                if i<len(ordered) and ordered[i]:
                    member=str(pathlib.PurePosixPath('word')/ordered[i])
                    if '..' in pathlib.PurePosixPath(member).parts:raise ValueError('unsafe embedded image path')
                    save_image(archive.read(member),pathlib.Path(member).name,{})
    else:parts=[dict(markdown=MarkItDown(enable_plugins=False).convert(str(path)).text_content,locator={})]
    return {'markdown':'\n\n'.join(p['markdown'] for p in parts),'parts':parts,'images':images,'warnings':warnings,'components':{'markitdown':'0.1.8'}}

def mdtable(rows):
    if not rows:return ''
    width=max(map(len,rows))
    def line(row):return '| '+' | '.join(str(c).replace('|','\\|').replace('\n','<br>') for c in row+['']*(width-len(row)))+' |'
    return '\n'.join([line(rows[0]),'| '+' | '.join(['---']*width)+' |']+[line(r) for r in rows[1:]])

def run(message):
    job=message['id']
    try:
        checkpoint(job);payload=message.get('payload',{});op=message['operation']
        with redirect_stdout(sys.stderr):
            if op=='health':result={'protocol':1,'python':sys.version,'runtime':'1.7.0','counters':dict(COUNTERS)}
            elif op=='embed':result=embedding(payload)
            elif op=='prepare_embedding':result=embedding(dict(payload,text='health',query=False));result={'ready':True}
            elif op=='ocr':result=recognize(payload,job)
            elif op=='convert':result=office(payload,job)
            else:raise ValueError('未知操作 '+op)
        checkpoint(job);emit({'version':1,'id':job,'result':result})
    except Exception as e:emit({'version':1,'id':job,'error':type(e).__name__+': '+str(e)})
    finally:CANCELLED.discard(job)

def main():
    for line in sys.stdin:
        try:
            m=json.loads(line)
            if m.get('version')!=1:raise ValueError('不支持的协议版本')
            if m.get('operation')=='cancel':CANCELLED.add(m['payload']['id']);continue
            EXECUTOR.submit(run,m)
        except Exception as e:emit({'version':1,'id':'protocol','error':str(e)})
    EXECUTOR.shutdown(wait=False,cancel_futures=True)
if __name__=='__main__':main()
