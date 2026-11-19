from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v05c-live.png")
im.crop((700,600,1110,790)).resize((820,380), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-answer.png")
print("ok")
