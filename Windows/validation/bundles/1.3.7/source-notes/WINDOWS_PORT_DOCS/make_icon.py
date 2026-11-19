from PIL import Image, ImageDraw
import os
out = r"D:\NextThought开发\src\NextThought\Windows\src\NextThought.Desktop\Assets"
os.makedirs(out, exist_ok=True)
S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# Paper card with ink border, seal mark in cinnabar.
r = 210
d.rounded_rectangle([38, 38, S-38, S-38], radius=r, fill=(247, 245, 240, 255))
d.rounded_rectangle([38, 38, S-38, S-38], radius=r, outline=(28, 27, 25, 255), width=14)
# vertical stroke (thought) and seal square
d.rounded_rectangle([290, 250, 350, 700], radius=30, fill=(28, 27, 25, 255))
d.rounded_rectangle([430, 250, 740, 380], radius=26, fill=(28, 27, 25, 255))
d.rounded_rectangle([430, 430, 640, 560], radius=26, fill=(28, 27, 25, 255))
# seal
d.rounded_rectangle([560, 600, 760, 800], radius=34, fill=(196, 69, 44, 255))
img.save(os.path.join(out, "NextThought.png"))
img.resize((256, 256), Image.LANCZOS).save(os.path.join(out, "NextThought.ico"), sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
print("icons written")
