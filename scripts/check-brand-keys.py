from pathlib import Path
import re

zh = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx").read_text(
    encoding="utf-8"
)
for key in [
    "ShellView_Menu_Syncthing",
    "ShellView_Menu_Syncthing_KillAll",
    "ConsoleView_Title",
    "ShellView_Menu_View_SyncthingConsole",
]:
    m = re.search(rf'name="{key}".*?<value>(.*?)</value>', zh, re.S)
    print(key, "=>", repr(m.group(1) if m else None))
