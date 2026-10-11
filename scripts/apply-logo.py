from PIL import Image
from pathlib import Path
import shutil

src = Path(r"d:\ShareFloder\SyncTrayzor\logo-white-pink.webp")
icons_dir = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Icons")
res_dir = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Resources")
res_dir.mkdir(exist_ok=True)

img = Image.open(src).convert("RGBA")


def make_square(logo: Image.Image, size: int, pad_ratio: float = 0.88) -> Image.Image:
    canvas = Image.new("RGBA", (size, size), (11, 13, 16, 255))  # #0b0d10
    max_w = int(size * pad_ratio)
    max_h = int(size * 0.42)
    ratio = min(max_w / logo.width, max_h / logo.height)
    nw = max(1, int(logo.width * ratio))
    nh = max(1, int(logo.height * ratio))
    resized = logo.resize((nw, nh), Image.Resampling.LANCZOS)
    canvas.paste(resized, ((size - nw) // 2, (size - nh) // 2), resized)
    return canvas


sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
images = [make_square(img, s) for s in sizes]
ico_path = icons_dir / "default.ico"
# Pillow uses the largest image as base when writing multi-size ICO
images[-1].save(ico_path, format="ICO", sizes=[(s, s) for s in sizes])
shutil.copy2(ico_path, icons_dir / "default_tray.ico")

wide_path = res_dir / "logo-white-pink.png"
img.save(wide_path, format="PNG")

make_square(img, 512).save(Path(r"d:\ShareFloder\SyncTrayzor\SyncTrayzor.png"), format="PNG")

print(f"Wrote {ico_path} ({ico_path.stat().st_size} bytes)")
print(f"Wrote {wide_path} ({wide_path.stat().st_size} bytes)")
