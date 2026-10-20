"""Six authored fixtures, no personal data. Reference content fixed before OCR runs."""
import pathlib,json
from PIL import Image,ImageDraw,ImageFont
from docx import Document
from docx.shared import Inches
ROOT=pathlib.Path(__file__).resolve().parent/'Fixtures';ROOT.mkdir(exist_ok=True)
FONT='/System/Library/Fonts/STHeiti Medium.ttc'
def canvas():return Image.new('RGB',(1600,1800),'white')
def line(im,x,y,t,size=36):ImageDraw.Draw(im).text((x,y),t,font=ImageFont.truetype(FONT,size),fill='black')
refs=[]
def save(im,name,reference,checks):
 im.save(ROOT/name);refs.append(dict(file=name,reference=reference,checks=checks))
im=canvas();cv=['王明｜后端开发工程师','教育：华南理工大学｜计算机科学｜2020–2024','技能：Python、Go、PostgreSQL、Kubernetes','项目：订单服务重构','将 P95 延迟从 420 ms 降低到 180 ms。','每秒请求从 1,200 提升到 3,600，错误率 0.03%。','经历：星河科技｜2024.07–2026.09','负责支付幂等与库存一致性，带领 4 人小组。','联系方式：wangming@example.test']
for i,t in enumerate(cv):line(im,85,100+130*i,t,46 if i==0 else 36)
save(im,'01-chinese-cv.pdf','\n'.join(cv),{'numbers':['420','180','1,200','3,600','0.03%','2024.07','2026.09'],'text':['王明','支付幂等','库存一致性','Kubernetes']})
# Minimal deterministic digital PDF with selectable Helvetica text.
jd=['Backend Engineer - Platform','Location: Sydney | Hybrid: 3 days per week','Requirements: Go, Python, PostgreSQL, Kubernetes','Design reliable distributed systems and REST APIs.','Own monitoring, incident response and technical documentation.','Target availability: 99.95%; on-call rotation: 1 in 6 weeks.','Salary range: AUD 140,000-170,000 plus superannuation.','Applicants should have 4+ years of production experience.']
stream='BT /F1 16 Tf 50 760 Td 28 TL '+ ' T* '.join('('+s.replace('(','\\(').replace(')','\\)')+') Tj' for s in jd)+' ET'
objs=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 900 850] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',f'<< /Length {len(stream.encode())} >>\nstream\n{stream}\nendstream']
pdf=b'%PDF-1.4\n';offset=[0]
for i,o in enumerate(objs):offset.append(len(pdf));pdf+=f'{i+1} 0 obj\n{o}\nendobj\n'.encode()
xref=len(pdf);pdf+=f'xref\n0 {len(objs)+1}\n0000000000 65535 f \n'.encode()+b''.join(f'{n:010d} 00000 n \n'.encode() for n in offset[1:]);pdf+=f'trailer << /Size {len(objs)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF'.encode();(ROOT/'02-english-jd.pdf').write_bytes(pdf)
refs.append(dict(file='02-english-jd.pdf',reference='\n'.join(jd),checks={'numbers':['99.95%','140,000','170,000'],'text':['incident response','PostgreSQL','superannuation']}))
im=canvas();left=['系统设计 / System Design','缓存策略','使用 Redis 缓存热点数据。','命中率为 92.7%，TTL 为 300 秒。','缓存失效后读取 PostgreSQL。','监控 P99 延迟与错误率。'];right=['Deployment Notes','Use a rolling deployment.','Keep 2 replicas available.','Rollback if errors exceed 0.5%.','CPU budget: 750 millicores.','Release owner: Platform Team.']
for i,t in enumerate(left):line(im,60,110+i*150,t,34)
for i,t in enumerate(right):line(im,850,110+i*150,t,32)
save(im,'03-bilingual-columns.png','\n'.join(left+right),{'numbers':['92.7%','300','0.5%','750'],'text':['缓存失效','PostgreSQL','rolling deployment','Platform Team'],'order':['缓存策略','监控','Deployment Notes','Release owner']})
im=canvas();line(im,80,80,'季度收入表 / Quarterly Revenue',48);d=ImageDraw.Draw(im)
xs=[80,520,970,1500];ys=[250,400,550,700,850,1000]
for y in ys:d.line((80,y,1500,y),fill='black',width=4)
for x in xs:
 if x==970:d.line((x,400,x,1000),fill='black',width=4)
 else:d.line((x,250,x,1000),fill='black',width=4)
for x,y,t in [(110,290,'地区 / Region'),(720,290,'2026 收入 (万元)'),(110,440,'季度'),(570,440,'第一季度'),(1050,440,'第二季度'),(110,590,'华东'),(570,590,'1,234.56'),(1050,590,'1,789.01'),(110,740,'华南'),(570,740,'987.65'),(1050,740,'1,012.34'),(110,890,'合计'),(570,890,'2,222.21'),(1050,890,'2,801.35')]:line(im,x,y,t,38)
save(im,'04-merged-table.png','季度收入表 / Quarterly Revenue\n地区 / Region | 2026 收入 (万元) [colspan=2]\n季度 | 第一季度 | 第二季度\n华东 | 1,234.56 | 1,789.01\n华南 | 987.65 | 1,012.34\n合计 | 2,222.21 | 2,801.35',{'numbers':['1,234.56','1,789.01','987.65','1,012.34','2,222.21','2,801.35'],'text':['华东','华南','合计'],'table':{'rows':5,'columns':3,'merged':True}})
im=canvas();formula=['模型训练 / Model Training','均方误差 Mean Squared Error','L = (1/n) Σᵢ (yᵢ − ŷᵢ)²','梯度下降 Gradient Descent','θₜ₊₁ = θₜ − η ∇L(θₜ)','学习率 η = 0.001，训练 120 epochs。','验证集准确率为 96.42%。','质量能量关系：E = mc²']
for i,t in enumerate(formula):
 if i in (2,4):
  draw=ImageDraw.Draw(im);x=80;y=120+i*150
  tokens=[('L = (1/n) Σ',50,0),('i',28,30),(' (y',50,0),('i',28,30),(' − ŷ',50,0),('i',28,30),(')',50,0),('2',28,-5)] if i==2 else [('θ',50,0),('t+1',28,30),(' = θ',50,0),('t',28,30),(' − η ∇L(θ',50,0),('t',28,30),(')',50,0)]
  for token,size,dy in tokens:
   font=ImageFont.truetype('/System/Library/Fonts/Supplemental/STIXTwoMath.otf',size)
   draw.text((x,y+dy),token,font=font,fill='black');x+=draw.textlength(token,font=font)
 else:line(im,80,120+i*150,t,42)
save(im,'05-technical-formula.png','\n'.join(formula),{'numbers':['0.001','120','96.42%'],'text':['梯度下降','Mean Squared Error'],'formulas':['L = (1/n) sum_i (y_i-yhat_i)^2','theta_(t+1) = theta_t - eta grad L(theta_t)','E=mc^2']})
# Embed new image with text that does not appear in the OOXML body.
im=Image.new('RGB',(1250,380),'white');embedded=['架构图 / Architecture','Client → Gateway → Worker','吞吐量 2,500 req/s；超时 800 ms；重试 3 次']
for i,t in enumerate(embedded):line(im,30,30+110*i,t,38)
pic=ROOT/'embedded-source.png';im.save(pic)
doc=Document();doc.add_heading('交付说明 / Delivery Notes',0);doc.add_paragraph('版本 v1.7.0，负责人：陈佳。');doc.add_paragraph('下图包含服务链路和容量参数。');doc.add_picture(str(pic),width=Inches(6));doc.add_paragraph('验收日期：2026-10-21。');doc.save(ROOT/'06-office-embedded.docx');pic.unlink()
refs.append(dict(file='06-office-embedded.docx',reference='交付说明 / Delivery Notes\n版本 v1.7.0，负责人：陈佳。\n下图包含服务链路和容量参数。\n'+'\n'.join(embedded)+'\n验收日期：2026-10-21。',checks={'numbers':['1.7.0','2,500','800','2026-10-21'],'text':['陈佳','Client','Gateway','Worker','重试']}))
(ROOT/'reference.json').write_text(json.dumps(refs,ensure_ascii=False,indent=2))
