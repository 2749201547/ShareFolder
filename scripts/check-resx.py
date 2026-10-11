from pathlib import Path

p = Path(r"d:\ShareFloder\SyncTrayzor\src\SyncTrayzor\Properties\Resources.zh.resx")
raw = p.read_bytes()
print("bom", list(raw[:3]), "len", len(raw))
text = raw.decode("utf-8")
for key in [
    "TrayIcon_Menu_Restore",
    "SettingsView_Tab_SyncTrayzor",
    "SettingsView_RestartSyncTrayzor_Title",
    "AboutView_Donate_Text",
]:
    i = text.find(f'name="{key}"')
    print("---", key, "---")
    print(text[i : i + 220].split("</data>")[0] if i >= 0 else "MISSING")
print("has Chinese", any("\u4e00" <= c <= "\u9fff" for c in text))
print(
    "SyncTrayzor left in values",
    sum(1 for line in text.splitlines() if "<value" in line and "SyncTrayzor" in line),
)
print(
    "irongoo in values",
    sum(1 for line in text.splitlines() if "<value" in line and "irongoo" in line),
)
