import urllib.request
import re

html = urllib.request.urlopen("http://127.0.0.1:8384/", timeout=5).read().decode(
    "utf-8", "replace"
)
for pat in [
    r"<title[^>]*>.*?</title>",
    r"rel=[\"'][^\"']*icon[^\"']*[\"'][^>]*>",
    r"favicon[^\"'\s]*",
    r"document\.title|pageTitle|Syncthing",
]:
    print("====", pat)
    for m in re.finditer(pat, html, re.I | re.S):
        print(m.group(0)[:200])

# check controller for title
js = urllib.request.urlopen(
    "http://127.0.0.1:8384/syncthing/core/syncthingController.js", timeout=5
).read().decode("utf-8", "replace")
for m in re.finditer(r".{0,40}document\.title.{0,120}|title\s*=\s*.{0,40}Syncthing.{0,40}", js):
    print("JS:", m.group(0).replace("\n", " ")[:180])
