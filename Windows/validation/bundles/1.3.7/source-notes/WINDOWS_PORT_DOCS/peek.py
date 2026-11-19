from PIL import Image
p = r"D:\NextThought开发\qa\shots\v05-island.png"
with open(p,"rb") as f: head=f.read(16)
print(head[:8].hex())
