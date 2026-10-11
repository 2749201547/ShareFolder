"""Replace user-facing 'Syncthing' with 'irongoo' inside resx <value> nodes."""
from pathlib import Path
import re

files = [
    Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.resx"),
    Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx"),
]


def repl_value(match: re.Match) -> str:
    open_tag, inner, close_tag = match.group(1), match.group(2), match.group(3)
    # Keep executable filename references
    inner = inner.replace("syncthing.exe", "\0EXE\0")
    inner = inner.replace("Syncthing", "irongoo")
    inner = inner.replace("\0EXE\0", "syncthing.exe")
    return open_tag + inner + close_tag


for path in files:
    text = path.read_text(encoding="utf-8")
    new = re.sub(r"(<value[^>]*>)(.*?)(</value>)", repl_value, text, flags=re.S)
    path.write_text(new, encoding="utf-8", newline="\r\n")
    left = sum(1 for line in new.splitlines() if "<value" in line and "Syncthing" in line)
    irongo = sum(1 for line in new.splitlines() if "<value" in line and "irongoo" in line)
    print(f"{path.name}: Syncthing values left={left}, irongoo values={irongo}")
