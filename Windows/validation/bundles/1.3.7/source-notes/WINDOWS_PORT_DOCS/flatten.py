from PIL import Image
p = r"D:\NextThought开发\qa\shots\v05-island.png"
im = Image.open(p)
print(im.mode, im.size)
bg = Image.new("RGB", im.size, (247,245,240))
if im.mode in ("RGBA","LA","P"):
    im = im.convert("RGBA")
    bg.paste(im, mask=im.split()[-1])
else:
    bg.paste(im.convert("RGB"))
bg.resize((im.size[0]//2, im.size[1]//2), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\v05-island-flat.png")
