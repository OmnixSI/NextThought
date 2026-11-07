"""Generate public, synthetic input documents; bundled Python is used only for QA."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from docx import Document
from pptx import Presentation
from openpyxl import Workbook

root = Path(__file__).resolve().parents[1] / 'artifacts/p2/fixtures'
root.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(TTFont('TestChinese', r'C:\Windows\Fonts\simhei.ttf'))
pdf = canvas.Canvas(str(root / 'project.pdf'), pagesize=(595, 842))
for heading, lines in [('Public bridge fixture', ['Project Aurora bridge inspection.', 'Inspection interval is 180 days.']),
                       ('合成项目资料', ['曙光桥计划于十一月十五日完成复核。', '安全复核负责人是林工。'])]:
    pdf.setFont('TestChinese', 22); pdf.drawString(50, 775, heading)
    pdf.setFont('TestChinese', 17)
    for i, line in enumerate(lines): pdf.drawString(50, 710 - i * 42, line)
    pdf.showPage()
pdf.save()
image = Image.new('RGB', (1600, 900), 'white'); draw = ImageDraw.Draw(image)
font = ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc', 54)
for i, text in enumerate(['合成扫描资料', '质量验收负责人是王工。', '材料复检周期为三十天。']): draw.text((90, 130 + 140*i), text, fill='black', font=font)
image.save(root / 'inspection.png')
pdf = canvas.Canvas(str(root / 'scanned.pdf'), pagesize=(800, 450))
pdf.drawImage(str(root / 'inspection.png'), 0, 0, width=800, height=450); pdf.save()
doc = Document(); doc.add_paragraph('合成施工组织资料'); doc.add_paragraph('混凝土养护时间不少于十四天。')
table = doc.add_table(rows=2, cols=2)
for row, values in zip(table.rows, [('工序', '负责人'), ('支座复核', '陈工')]):
    for cell, text in zip(row.cells, values): cell.text = text
doc.save(root / 'construction.docx')
pres = Presentation()
for title, body in [('合成进度汇报', '钢梁吊装分为四个阶段。'), ('复核安排', '第二阶段完成后复核支座标高。')]:
    slide = pres.slides.add_slide(pres.slide_layouts[1]); slide.shapes.title.text=title; slide.placeholders[1].text=body
pres.save(root / 'schedule.pptx')
book = Workbook(); sheet = book.active; sheet.title = '检测计划'
sheet.append(['项目', '周期（天）']); sheet.append(['桥面巡检', 7]); sheet.append(['支座复核', 90]); book.save(root / 'checks.xlsx')
(root / 'readme.md').write_text('公开合成资料\n现场例会固定在每周三下午三点召开。\n', encoding='utf-8')
expected = {'project.pdf': ['Inspection interval is 180 days.', '安全复核负责人是林工'],
            'scanned.pdf': ['质量验收负责人是王工', '材料复检周期为三十天'],
            'inspection.png': ['质量验收负责人是王工'], 'construction.docx': ['混凝土养护时间不少于十四天', '陈工'],
            'schedule.pptx': ['钢梁吊装分为四个阶段', '第二阶段完成后复核支座标高'], 'checks.xlsx': ['桥面巡检', '90'],
            'readme.md': ['每周三下午三点']}
(root / 'expected.json').write_text(json.dumps(expected, ensure_ascii=False, indent=2), encoding='utf-8')
print(root)
