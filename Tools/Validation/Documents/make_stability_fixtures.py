"""Authored Office fixtures. Run with the bundled document runtime's site-packages."""
import pathlib,sys,json
if '--site-packages' in sys.argv:
 sys.path.insert(0,sys.argv[sys.argv.index('--site-packages')+1])
from pptx import Presentation
from pptx.util import Inches
from pptx.chart.data import CategoryChartData
from pptx.enum.chart import XL_CHART_TYPE
out=pathlib.Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
p=Presentation();s=p.slides.add_slide(p.slide_layouts[6])
t=s.shapes.add_textbox(Inches(.3),Inches(.3),Inches(8),Inches(.5));t.text='Stability audit 2026'
g=s.shapes.add_group_shape()
for i,txt in enumerate(['Group A: 1,234.56 USD','Group B: -7.5%']):
 t=g.shapes.add_textbox(Inches(.5),Inches(1+i*.6),Inches(6),Inches(.5));t.text=txt
table=s.shapes.add_table(3,3,Inches(.5),Inches(3),Inches(7),Inches(1)).table
table.cell(0,0).merge(table.cell(0,1));table.cell(0,0).text='Merged heading';table.cell(0,2).text='Count'
for r in range(1,3):
 table.cell(r,0).text='Row '+str(r);table.cell(r,1).text='A&B';table.cell(r,2).text=str(r*11)
s.notes_slide.notes_text_frame.text='Speaker note: exact sample size 384.'
s2=p.slides.add_slide(p.slide_layouts[6]);data=CategoryChartData();data.categories=['Baseline','Proposed'];data.add_series('Accuracy',[.71,.93]);s2.shapes.add_chart(XL_CHART_TYPE.COLUMN_CLUSTERED, Inches(.5),Inches(1),Inches(8),Inches(4),data)
p.save(out/'structured-deck.pptx')
(out/'reference.json').write_text(json.dumps({'anchors':['1,234.56 USD','-7.5%','Merged heading','Speaker note: exact sample size 384.','Accuracy','0.71','0.93'],'slides':[1,2]},indent=2))
print(out/'structured-deck.pptx')
