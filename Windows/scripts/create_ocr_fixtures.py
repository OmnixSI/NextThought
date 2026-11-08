"""Synthetic OCR inputs with known reading order, table cells, and a formula."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json

root = Path(__file__).resolve().parents[1] / 'artifacts/p3/ocr-fixtures'
root.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc', 40)
small = ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc', 32)
image = Image.new('RGB', (1400, 1000), 'white'); draw = ImageDraw.Draw(image)
draw.text((80, 50), '公开合成检测计划', font=font, fill='black')
for row, values in enumerate([('检测项目', '周期（天）'), ('桥面巡检', '7'), ('支座复核', '90')]):
    for col, text in enumerate(values):
        x, y = 80 + col * 600, 170 + row * 140
        draw.rectangle((x, y, x + 600, y + 140), outline='black', width=3)
        draw.text((x + 25, y + 45), text, font=font, fill='black')
draw.text((80, 700), '说明：以上数据仅为软件验收使用。', font=small, fill='black')
image.save(root / 'table.png')
image = Image.new('RGB', (1600, 1100), 'white'); draw = ImageDraw.Draw(image)
draw.text((70, 50), '公开合成施工说明', font=font, fill='black')
left = ['第一部分 施工准备', '进场前完成安全培训。', '检查起重设备制动系统。', '吊装前确认风速满足要求。']
right = ['第二部分 施工验收', '完成支座标高复核。', '检查钢梁连接螺栓。', '资料员保存验收记录。']
for x, lines in [(70, left), (850, right)]:
    for i, text in enumerate(lines): draw.text((x, 210 + i * 130), text, font=small, fill='black')
image.save(root / 'columns.png')
image = Image.new('RGB', (1200, 650), 'white'); draw = ImageDraw.Draw(image)
draw.text((70, 60), '公开合成公式识别', font=font, fill='black')
mathfont = ImageFont.truetype(r'C:\Windows\Fonts\times.ttf', 85)
draw.text((260, 250), 'σ =', font=mathfont, fill='black'); draw.text((540, 170), 'F', font=mathfont, fill='black'); draw.line((490, 285, 670, 285), fill='black', width=5); draw.text((540, 310), 'A', font=mathfont, fill='black')
image.save(root / 'formula.png')
(root / 'expected.json').write_text(json.dumps({'table.png': {'phrases': ['桥面巡检', '支座复核', '90'], 'requiresTable': True}, 'columns.png': {'phrases': ['施工准备', '安全培训', '施工验收', '验收记录'], 'leftBeforeRight': True}, 'formula.png': {'phrases': ['F', 'A'], 'requiresFormula': True}}, ensure_ascii=False, indent=2), encoding='utf-8')
print(root)
