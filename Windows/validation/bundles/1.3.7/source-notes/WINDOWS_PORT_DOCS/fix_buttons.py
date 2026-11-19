import re, io
files = [
 r"D:\NextThought开发\src\NextThought\Windows\src\NextThought.Desktop\Views\LibraryView.axaml",
 r"D:\NextThought开发\src\NextThought\Windows\src\NextThought.Desktop\Views\HomeView.axaml",
 r"D:\NextThought开发\src\NextThought\Windows\src\NextThought.Desktop\Views\LiveView.axaml",
 r"D:\NextThought开发\src\NextThought\Windows\src\NextThought.Desktop\Views\SettingsView.axaml",
]
pattern = re.compile(r'<Button([^>]*Classes="primary"[^>]*?)\s*Content="([^"]+)"([^>]*)/>')
def repl(m):
    attrs = (m.group(1) + m.group(3))
    attrs = re.sub(r'\s*Content="[^"]*"', '', attrs).rstrip()
    label = m.group(2)
    return '<Button%s>\n          <TextBlock Text="%s" Foreground="{DynamicResource OnInk}" />\n        </Button>' % (attrs, label)
for path in files:
    text = io.open(path, encoding="utf-8").read()
    new = pattern.sub(repl, text)
    if new != text:
        io.open(path, "w", encoding="utf-8", newline="\n").write(new)
        print("patched", path)
print("done")
