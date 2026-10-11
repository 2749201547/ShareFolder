from pathlib import Path
import base64

png = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Resources\logo-white-pink.png").read_bytes()
b64 = base64.b64encode(png).decode("ascii")
svg = (
    '<?xml version="1.0" encoding="UTF-8"?>\n'
    '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
    'width="200" height="42" viewBox="0 0 200 42" role="img" aria-label="irongoo">\n'
    '  <image width="200" height="42" preserveAspectRatio="xMidYMid meet" '
    f'xlink:href="data:image/png;base64,{b64}"/>\n'
    "</svg>\n"
)
home = Path(
    r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\bin\Debug\net10.0-windows10.0.17763.0\win-x64\data\syncthing"
)
for theme in ("default", "dark", "black", "light"):
    img = home / "gui" / theme / "assets" / "img"
    img.mkdir(parents=True, exist_ok=True)
    path = img / "logo-horizontal.svg"
    path.write_text(svg, encoding="utf-8")
    print("wrote", path, path.stat().st_size)
