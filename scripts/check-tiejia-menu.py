from pathlib import Path
import re

for name in ("Resources.zh.resx", "Resources.resx"):
    t = Path(rf"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\{name}").read_text(
        encoding="utf-8"
    )
    m = re.search(r'name="ShellView_Menu_Syncthing".*?<value>(.*?)</value>', t, re.S)
    val = m.group(1) if m else None
    print(name, repr(val), [hex(ord(c)) for c in val] if val else None)
    # ensure console title still irongoo
    m2 = re.search(r'name="ConsoleView_Title".*?<value>(.*?)</value>', t, re.S)
    print("  ConsoleView_Title", repr(m2.group(1) if m2 else None))
