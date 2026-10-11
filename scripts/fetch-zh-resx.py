import urllib.request
from pathlib import Path

url = "https://raw.githubusercontent.com/GermanCoding/SyncTrayzor/master/src/SyncTrayzor/Properties/Resources.zh.resx"
out = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx")

with urllib.request.urlopen(url) as resp:
    data = resp.read()

print("downloaded", len(data), "bytes")
print("header", data[:60])
# find TrayIcon_Menu_Restore value bytes
idx = data.find(b"TrayIcon_Menu_Restore")
snippet = data[idx : idx + 120]
print("snippet bytes:", snippet)
print("snippet text:", snippet.decode("utf-8"))
print("contains 打开 utf8", "打开".encode("utf-8") in data)

# write raw bytes unchanged
out.write_bytes(data)

# now replace SyncTrayzor -> irongoo only inside values, preserve bytes via unicode
text = data.decode("utf-8")
import re


def repl(m):
    return m.group(1) + m.group(2).replace("SyncTrayzor", "irongoo") + m.group(3)


new = re.sub(r"(<value[^>]*>)(.*?)(</value>)", repl, text, flags=re.S)
out.write_text(new, encoding="utf-8", newline="\r\n")
check = out.read_text(encoding="utf-8")
m = re.search(r'name="TrayIcon_Menu_Restore".*?<value>(.*?)</value>', check, re.S)
print("final value:", m.group(1) if m else None)
print("final value codepoints:", [hex(ord(c)) for c in (m.group(1) if m else "")])
