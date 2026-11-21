from PIL import Image
im = Image.open(r"D:\NextThought开发\qa\shots\v06-capture-picker2.png")
# The suspicious box in the preview (1708 wide) around x=860..1165, y=385..505 -> scale to 2560
s = 2560/1708
box = (int(850*s), int(380*s), int(1180*s), int(515*s))
im.crop(box).resize(((box[2]-box[0])*2, (box[3]-box[1])*2), Image.LANCZOS).save(r"D:\NextThought开发\qa\shots\crop-capture-box.png")
print(box)
