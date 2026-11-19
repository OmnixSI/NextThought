from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v05f-notes.png")
im.crop((940,80,1136,132)).resize((588,156), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-btn3.png")
print("ok")
