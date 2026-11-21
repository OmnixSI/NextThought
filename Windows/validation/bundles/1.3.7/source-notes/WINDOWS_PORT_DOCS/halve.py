from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\capture\shot.png")
print(im.size)
im.resize((im.size[0]//2, im.size[1]//2), Image.LANCZOS).save(r"D:\NextThought开发\qa\capture\shot-half.png")
