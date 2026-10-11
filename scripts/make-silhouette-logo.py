"""Make logo-white-pink a clean transparent silhouette (no dark plate)."""
from PIL import Image
from pathlib import Path

src = Path(r"d:\ShareFloder\SyncTrayzor\logo-white-pink.webp")
out = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Resources\logo-white-pink.png")

im = Image.open(src).convert("RGBA")
pixels = []
for r, g, b, a in im.getdata():
    if a < 12:
        pixels.append((0, 0, 0, 0))
        continue
    # Drop any dark plate / charcoal fill — keep only bright brand colors
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    # pink channel: high R, mid B, lower G
    is_pink = r > 160 and b > 80 and g < 160 and r > g
    is_white = r > 200 and g > 200 and b > 200
    is_pinkish_aa = r > 140 and b > 70 and g < 180 and r > g + 20
    if not (is_white or is_pink or is_pinkish_aa) or lum < 70:
        pixels.append((0, 0, 0, 0))
        continue
    pixels.append((r, g, b, a))

im.putdata(pixels)
bbox = im.getbbox()
if bbox:
    im = im.crop(bbox)
pad = 2
canvas = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
canvas.paste(im, (pad, pad), im)
canvas.save(out, format="PNG")
print("saved", out, canvas.size, "bbox", bbox)

# preview on #333 navbar
prev = Image.new("RGBA", (canvas.width + 40, max(40, canvas.height + 20)), (51, 51, 51, 255))
ratio = 28 / canvas.height
rsz = canvas.resize((max(1, int(canvas.width * ratio)), 28), Image.Resampling.LANCZOS)
prev.paste(rsz, (20, (prev.height - 28) // 2), rsz)
prev.save(Path(r"d:\ShareFloder\SyncTrayzor\preview-silhouette.png"))
print("preview written")
