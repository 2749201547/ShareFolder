"""Replace display brand 'irongoo' with '铁甲' in resx values (not property keys)."""
from pathlib import Path
import re

files = [
    Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.resx"),
    Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx"),
]


def repl_value(match: re.Match) -> str:
    return match.group(1) + match.group(2).replace("irongoo", "铁甲") + match.group(3)


for path in files:
    text = path.read_text(encoding="utf-8")
    new = re.sub(r"(<value[^>]*>)(.*?)(</value>)", repl_value, text, flags=re.S)
    path.write_text(new, encoding="utf-8", newline="\r\n")
    print(
        path.name,
        "irongoo left in values=",
        sum(1 for line in new.splitlines() if "<value" in line and "irongoo" in line),
        "铁甲 values=",
        sum(1 for line in new.splitlines() if "<value" in line and "铁甲" in line),
    )
