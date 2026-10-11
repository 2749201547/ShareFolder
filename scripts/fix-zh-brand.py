from pathlib import Path
import re

p = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx")
text = p.read_text(encoding="utf-8")

# sanity: TrayIcon restore should contain 打开
m = re.search(r'name="TrayIcon_Menu_Restore".*?<value>(.*?)</value>', text, re.S)
print("TrayIcon_Menu_Restore repr:", repr(m.group(1) if m else None))

def repl_value(match: re.Match) -> str:
    return match.group(1) + match.group(2).replace("SyncTrayzor", "irongoo") + match.group(3)


new = re.sub(r"(<value[^>]*>)(.*?)(</value>)", repl_value, text, flags=re.S)
p.write_text(new, encoding="utf-8", newline="\r\n")

text2 = p.read_text(encoding="utf-8")
m2 = re.search(r'name="TrayIcon_Menu_Restore".*?<value>(.*?)</value>', text2, re.S)
print("After repr:", repr(m2.group(1) if m2 else None))
print(
    "values with SyncTrayzor",
    sum(1 for line in text2.splitlines() if "<value" in line and "SyncTrayzor" in line),
)
print(
    "values with irongoo",
    sum(1 for line in text2.splitlines() if "<value" in line and "irongoo" in line),
)

# English resx already updated; verify
en = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.resx").read_text(
    encoding="utf-8"
)
print(
    "EN SyncTrayzor values left",
    sum(1 for line in en.splitlines() if "<value" in line and "SyncTrayzor" in line),
)
print(
    "EN irongoo values",
    sum(1 for line in en.splitlines() if "<value" in line and "irongoo" in line),
)
