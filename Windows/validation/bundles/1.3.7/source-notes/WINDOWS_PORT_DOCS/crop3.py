from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v05d-notes.png")
im.crop((700,80,1136,132)).resize((1308,156), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-btn.png")
im2 = Image.open(r"D:\NextThought开发\qa\shots\v05d-live.png")
im2.crop((960,80,1136,132)).resize((528,156), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-btn2.png")
print("ok")
