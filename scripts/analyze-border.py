from PIL import Image
from collections import Counter

im = Image.open(
    r"C:\Users\Administrator\.cursor\projects\d-ShareFloder\assets\c__Users_Administrator_AppData_Roaming_Cursor_User_workspaceStorage_d91fb828144814dfed90043b5215b392_images_image-a91927dd-c280-42a2-aca8-98088cd7e035.png"
).convert("RGB")
print("size", im.size)

cx = Counter()
for y in range(im.height):
    for x in range(im.width):
        r, g, b = im.getpixel((x, y))
        if r > 140 and 40 < b < 160 and g < 100 and r > g + 40:
            cx[x] += 1
print("top pink columns", cx.most_common(15))

for v in [4281873617, 3302044728]:
    print(hex(v), "AABBGGRR", v & 255, (v >> 8) & 255, (v >> 16) & 255)
