"""Recolor logo.webp to irongoo pink and write tray .ico files."""
from PIL import Image
from pathlib import Path

src = Path(r"d:\ShareFloder\SyncTrayzor\logo.webp")
icons_dir = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Icons")
out_preview = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Resources\tray-logo-pink.png")

# irongoo pink from logo-white-pink / favicon
PINK = (231, 63, 145)  # #E73F91


def recolor_to_pink(im: Image.Image, tint=PINK) -> Image.Image:
    """Keep alpha shape; fill with vivid irongoo pink (light anti-alias edges)."""
    im = im.convert("RGBA")
    tr, tg, tb = tint
    out = []
    for r, g, b, a in im.getdata():
        if a == 0:
            out.append((0, 0, 0, 0))
            continue
        # Source icon is flat blue; use coverage (alpha) only, keep hue pure pink
        out.append((tr, tg, tb, a))
    im.putdata(out)
    return im


def make_icon_frame(logo: Image.Image, size: int, bg=None) -> Image.Image:
    """Center logo on transparent (or solid) square canvas."""
    canvas = Image.new("RGBA", (size, size), bg if bg else (0, 0, 0, 0))
    # leave a little padding for tray readability
    target = int(size * 0.88)
    ratio = min(target / logo.width, target / logo.height)
    nw = max(1, int(logo.width * ratio))
    nh = max(1, int(logo.height * ratio))
    resized = logo.resize((nw, nh), Image.Resampling.LANCZOS)
    canvas.paste(resized, ((size - nw) // 2, (size - nh) // 2), resized)
    return canvas


def write_ico(path: Path, logo: Image.Image, sizes=(16, 20, 24, 32, 40, 48, 64, 128, 256)):
    # Build each size from a high-res master for cleaner downscales
    master = make_icon_frame(logo, 256)
    frames = [master.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]
    frames[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes])


def tint_alpha(logo: Image.Image, rgb, alpha_scale=1.0) -> Image.Image:
    im = logo.copy().convert("RGBA")
    out = []
    for r, g, b, a in im.getdata():
        if a == 0:
            out.append((0, 0, 0, 0))
        else:
            out.append((rgb[0], rgb[1], rgb[2], int(a * alpha_scale)))
    im.putdata(out)
    return im


pink = recolor_to_pink(Image.open(src))
out_preview.parent.mkdir(exist_ok=True)
# save a crisp preview at 64px
make_icon_frame(pink, 64).save(out_preview, format="PNG")

# Main tray / idle / syncing frames — same pink mark
write_ico(icons_dir / "default_tray.ico", pink)
write_ico(icons_dir / "syncing_2.ico", pink)
write_ico(icons_dir / "syncing_3.ico", pink)
write_ico(icons_dir / "syncing_4.ico", pink)

# State variants (still the same mark, different tint)
write_ico(icons_dir / "stopped.ico", tint_alpha(pink, (140, 140, 140)))
write_ico(icons_dir / "paused_tray.ico", tint_alpha(pink, (180, 140, 60)))
write_ico(icons_dir / "alert_tray.ico", tint_alpha(pink, (220, 60, 60)))

# Keep app window/exe icon aligned with tray brand
write_ico(icons_dir / "default.ico", pink)

print("Wrote tray icons from", src)
print("Preview:", out_preview)
for name in (
    "default_tray.ico",
    "syncing_2.ico",
    "syncing_3.ico",
    "syncing_4.ico",
    "stopped.ico",
    "paused_tray.ico",
    "alert_tray.ico",
    "default.ico",
):
    p = icons_dir / name
    print(f"  {name}: {p.stat().st_size} bytes")
