import pymupdf, sys
p = r"C:\Users\Thunderobot\Desktop\NextThought Windows 移植方案 v2.pdf"
d = pymupdf.open(p)
print("PAGES:", d.page_count)
out=[]
for i,pg in enumerate(d):
    out.append(f"\n\n===== PAGE {i+1} =====\n")
    out.append(pg.get_text("text"))
txt="".join(out)
open(r"D:\NextThought开发\docs\plan_v2.txt","w",encoding="utf-8").write(txt)
print("CHARS:", len(txt))
