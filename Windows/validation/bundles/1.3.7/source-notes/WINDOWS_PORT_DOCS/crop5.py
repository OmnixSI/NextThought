from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v05i-notes.png")
im.crop((940,80,1136,132)).resize((588,156), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-btn4.png")
