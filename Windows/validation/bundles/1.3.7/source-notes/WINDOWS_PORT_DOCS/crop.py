from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v05-home.png")
im.crop((0,0,460,60)).resize((920,120), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-title.png")
im.crop((600,740,1136,828)).resize((1072,176), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-answer-buttons.png")
print("ok")
