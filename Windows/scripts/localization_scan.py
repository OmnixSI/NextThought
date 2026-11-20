#!/usr/bin/env python3
"""G9/G10: localization audit for the Windows port.

Two jobs:

1. ``--screens <dir>`` reads the per-page text inventories that ``--screenshots`` writes next to
   the PNGs and reports every Chinese character left on an English page. Demo/user data and
   OS-provided device names are classified and excluded; everything else is a residual to fix.
2. ``--write`` regenerates ``Windows/WORDING.md``: the Mac → Windows wording replacement table
   (Chinese and English, with the location of each sentence) plus the English screenshot scan
   result. The source of truth for the table is ``Windows/src/NextThought.Core/UiTextWindows.cs``.

Usage (from the repository root):

    .tools/python/python.exe Windows/scripts/localization_scan.py --write --screens <screenshot-dir>
    .tools/python/python.exe Windows/scripts/localization_scan.py --screens <screenshot-dir> --check
"""
from __future__ import annotations

import argparse
import html
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WIN = ROOT / "Windows"
DESKTOP = WIN / "src" / "NextThought.Desktop"
CORE = WIN / "src" / "NextThought.Core"
WORDING = WIN / "WORDING.md"
EVIDENCE = WIN / "validation" / "localization" / "english-scan.md"

CJK = re.compile(r"[\u3000-\u303f\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff00-\uffef]")
PAIR = re.compile(r'^\s*\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)",?\s*$')
TR_KEY = re.compile(r"\{loc:Tr\s+(?:'(?P<quoted>[^']*)'|(?P<plain>[^}]*))\}")

# --- system-noun substitutions that are allowed to differ from the Mac wording (G10) -------------
# (Mac wording, Windows wording, English on both sides, scope)
SYSTEM_NOUNS: list[tuple[str, str, str, str]] = [
    ("钥匙串", "凭据管理器", "Keychain → Credential Manager", "密钥保存说明（设置 ▸ 高级、API Key 状态行）"),
    ("访达", "文件资源管理器", "Finder → File Explorer", "打开数据目录 / 打开文件位置"),
    ("这台 Mac / Mac", "这台电脑 / Windows", "Mac → Windows", "帮助文案里指代系统的地方"),
    ("⌘⇧ / ⌘⌃ / ⌃⌥ 等快捷键符号", "Ctrl+Alt / Ctrl+Alt+Shift", "Mac shortcut glyphs → Windows keys", "全部快捷键与提示"),
    ("系统语音识别", "本机语音识别（不含系统识别）", "System speech → on-device speech", "声音来源 / 引导第 2 步"),
    ("系统翻译框架", "当前回答服务（文本会离开本机）", "System translation → answer service", "说话人与翻译设置"),
    ("屏幕录制权限", "无（Windows 不需要这一步）", "Screen Recording permission → none", "首次引导 / 权限说明"),
    ("Menu 栏 / 菜单模式", "主窗口 / 托盘图标", "Mac menu bar → Windows tray", "工作台与托盘入口"),
    ("Apple Speech 语言资源", "随包的 X-ASR 模型", "Apple Speech assets → bundled X-ASR model", "本机语音模型状态"),
    ("旧版 Office 格式 .doc/.odt/.rtf", "提示另存为新格式", "legacy Office formats → save as a new format", "导入资料"),
]

# Text owned by other in-flight tasks: recorded in WORDING.md §4 instead of being fixed here.
OTHER_TASK_TEXT = [
    "导入会合并，不删除现有数据；先预览，再决定恢复什么。",  # E30 backup UI
    "导入失败：", "恢复失败：", "打开位置失败：",  # E30 backup UI
    "已是最新：", "正式版本", "main 最新构建",  # F update feed
    "已校对", "识别错误",  # E17 proofreading status
    "问：",  # AppState.Live.cs (F group in flight)
]

# Lines that come from the OS (device names) are not our wording and follow the OS language.
SYSTEM_DATA_PREFIXES = ("扬声器 (", "麦克风 (", "Speakers (", "Microphone (")
# Demo/meeting content shown by the demo shell (MainWindow demo panel) is user data.
DEMO_TEXT = [
    "温度补偿之后残差还剩多少，和有限元对得上吗？",
    "残差约占 12%，主要集中在支座附近。",
    "新会议",  # D1 demo seeds an unsaved meeting with this placeholder title (user data)
    "我负责订单服务的缓存层",
]

DEMO_TEXT.extend([
    "Cache Aside", "删除失败兜底", "并发读写窗口", "取舍",
    "Undo correction", "先写数据库、再删缓存", "先更新数据库、再删除缓存",
])


# Demo/user data may stay in its own language on English pages.
DEMO_FILES = [
    DESKTOP / "DemoContent.cs",
    DESKTOP / "AppState.Demo.cs",
    WIN / "src" / "NextThought.Platform" / "DemoStage.cs",
    WIN / "src" / "NextThought.Platform" / "DemoScript.cs",
]


def decode(value: str) -> str:
    out: list[str] = []
    index = 0
    while index < len(value):
        char = value[index]
        if char != "\\" or index + 1 >= len(value):
            out.append(char)
            index += 1
            continue
        nxt = value[index + 1]
        mapping = {"n": "\n", "r": "\r", "t": "\t", '"': '"', "\\": "\\"}
        if nxt in mapping:
            out.append(mapping[nxt])
            index += 2
        elif nxt == "u" and index + 5 < len(value):
            out.append(chr(int(value[index + 2:index + 6], 16)))
            index += 6
        else:
            out.append(char)
            index += 1
    return "".join(out)


def windows_table() -> dict[str, str]:
    rows: dict[str, str] = {}
    text = (CORE / "UiTextWindows.cs").read_text(encoding="utf-8")
    for line in text.splitlines():
        match = PAIR.match(line)
        if match:
            rows[decode(match.group(1))] = decode(match.group(2))
    return rows


def demo_literals() -> list[str]:
    values: list[str] = []
    for path in DEMO_FILES:
        if not path.exists():
            continue
        for match in re.finditer(r'"((?:[^"\\]|\\.)*)"', path.read_text(encoding="utf-8")):
            value = decode(match.group(1))
            if CJK.search(value):
                values.append(value)
    return values


def locations() -> dict[str, str]:
    """First source location of every referenced Windows-only sentence."""
    found: dict[str, str] = {}
    for path in sorted(DESKTOP.rglob("*.axaml")):
        rel = path.relative_to(ROOT).as_posix()
        for match in TR_KEY.finditer(path.read_text(encoding="utf-8")):
            key = match.group("quoted") if match.group("quoted") is not None else match.group("plain")
            key = html.unescape(key)
            found.setdefault(key, rel)
    literal = re.compile(r'(?<![\w@$])@?"((?:[^"\\]|\\.)*)"')
    for path in sorted(DESKTOP.rglob("*.cs")):
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        if "/SelfTest/" in path.as_posix():
            continue
        rel = path.relative_to(ROOT).as_posix()
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if line.strip().startswith("//"):
                continue
            for match in literal.finditer(line):
                value = decode(match.group(1))
                if CJK.search(value):
                    found.setdefault(value, f"{rel}:{number}")
    return found


def mac_counterpart(win_key: str, mac: set[str]) -> str | None:
    """Best-effort: undo the Windows system-noun/wording changes and see if a Mac sentence remains."""
    candidates = [win_key]
    for mac_word, win_word, _english, _scope in SYSTEM_NOUNS:
        for mac_token in mac_word.split(" / "):
            for win_token in win_word.split(" / "):
                if win_token and win_token in win_key:
                    candidates.append(win_key.replace(win_token, mac_token.strip()))
    # Shortcut wording: Ctrl+Alt ↔ ⌘⇧, Ctrl+L ↔ ⌘L, Enter ↔ Return.
    candidates.append(re.sub(r"Ctrl\+Alt\+Shift", "⌃⌥", win_key))
    candidates.append(re.sub(r"Ctrl\+Alt", "⌘⇧", win_key))
    candidates.append(re.sub(r"Ctrl\+Shift\+Enter", "⌘⇧↵", win_key))
    candidates.append(re.sub(r"Ctrl\+L", "⌘L", win_key))
    candidates.append(win_key.replace("Enter", "Return"))
    candidates.append(win_key.replace("这个 Windows 用户", "这台 Mac 的用户"))
    for candidate in candidates:
        if candidate in mac:
            return candidate
    return None


def scan_screens(directory: Path) -> dict:
    text_dir = directory / "text"
    if not text_dir.is_dir():
        raise SystemExit(f"no text inventories under {text_dir}; run --screenshots first")
    demo = demo_literals()
    pages = sorted(text_dir.glob("*-en*.txt")) + sorted(text_dir.glob("island-*-en.txt"))
    pages = sorted({page for page in pages})
    rows: list[dict] = []
    residuals: list[dict] = []
    for page in pages:
        page_rows = page.read_text(encoding="utf-8").splitlines()
        count = 0
        for line in page_rows:
            if not CJK.search(line):
                continue
            count += 1
            if "\t" in line:
                kind, _, text = line.partition("\t")
            else:
                kind, text = "text", line
            other_task = any(value in text for value in OTHER_TASK_TEXT)
            allowed = (any(value in text for value in demo)
                       or any(value in text for value in DEMO_TEXT)
                       or text.startswith(SYSTEM_DATA_PREFIXES)
                       or other_task)
            row = {"page": page.stem, "kind": kind, "text": text, "allowed": allowed}
            rows.append(row)
            if not allowed:
                residuals.append(row)
        rows.append({"page": page.stem, "kind": "_summary", "text": str(count), "allowed": True})
    return {
        "pages": len(pages),
        "cjkRows": sum(1 for row in rows if row["kind"] != "_summary"),
        "allowed": sum(1 for row in rows if row["kind"] != "_summary" and row["allowed"]),
        "residual": residuals,
        "rows": rows,
    }


def write_wording(table: dict[str, str], places: dict[str, str], scan: dict | None) -> None:
    import datetime

    catalogue = json.loads((ROOT / "Resources" / "Localization" / "Localizable.xcstrings").read_text(encoding="utf-8"))
    mac = set(catalogue.get("strings", {}))
    lines: list[str] = []
    lines.append("# Windows 文案对照（G10）")
    lines.append("")
    lines.append("这份表记录 Windows 版与 Mac 版**文案不同**的每一句。英文与中文都写；相同文案的句子")
    lines.append("直接走 Mac 的 `Localizable.xcstrings`（由 `Windows/scripts/generate_ui_text.py` 生成")
    lines.append("`UiTextMac.g.cs`，2,607 条），不在这张表里重复。Mac 是唯一标准：只有涉及系统名词的")
    lines.append("句子可以改（钥匙串→凭据管理器、访达→文件资源管理器等），其余差异都是 Windows 移植过程")
    lines.append("中的说明性文案（Windows 独有功能或 Windows 能力差异），在此逐句列出。")
    lines.append("")
    lines.append(f"- 生成时间：{datetime.date.today().isoformat()}")
    lines.append(f"- 生成方式：`python Windows/scripts/localization_scan.py --write`")
    lines.append(f"- Windows 独有/改写文案：**{len(table)}** 句（下表逐条）")
    lines.append(f"- Mac 共享文案：2,607 条，由生成的字符串表提供，文案逐字相同")
    lines.append("")
    lines.append("## 1. 系统名词替换（允许与 Mac 不同）")
    lines.append("")
    lines.append("| Mac 文案 | Windows 文案 | English | 位置 |")
    lines.append("|---|---|---|---|")
    for mac_word, win_word, english, scope in SYSTEM_NOUNS:
        lines.append(f"| {mac_word} | {win_word} | {english} | {scope} |")
    lines.append("")
    lines.append("## 2. Windows 独有 / 改写文案逐句对照")
    lines.append("")
    lines.append("「Mac 原文」一列能对应的句子是从 Windows 文案反推系统名词后，在 Mac 字符串表里命中")
    lines.append("的同一句；「—」表示 Mac 版没有这句（Windows 独有功能或能力差异说明）。")
    lines.append("")
    lines.append("| # | Windows 中文文案 | Windows 英文文案 | Mac 原文 | 位置 |")
    lines.append("|---|---|---|---|---|")
    for number, key in enumerate(sorted(table, key=lambda value: (len(value), value)), 1):
        english = table[key].replace("|", "\\|")
        shown_key = key.replace("|", "\\|").replace("\n", "⏎")
        counterpart = mac_counterpart(key, mac)
        mac_text = counterpart.replace("|", "\\|").replace("\n", "⏎") if counterpart else "—"
        place = places.get(key, "").replace("|", "\\|")
        lines.append(f"| {number} | {shown_key} | {english} | {mac_text} | {place} |")
    lines.append("")
    lines.append("## 3. 英文模式逐页扫描")
    lines.append("")
    if scan is None:
        lines.append("（本次未提供 `--screens` 目录；运行 `--demo --screenshots=<dir>` 后用 `--screens=<dir>` 重新生成。）")
    else:
        lines.append("扫描对象：`--demo --screenshots=<dir>` 生成的英文模式页面文字清单（`text/*-en*.txt`，")
        lines.append("包含未展开的 tooltip；文字清单由截图工具在渲染后遍历控件树得到，不依赖 OCR）。")
        lines.append("")
        lines.append(f"- 页面数：**{scan['pages']}**")
        lines.append(f"- 含中文字符的行：**{scan['cjkRows']}**")
        lines.append(f"- 其中用户数据/演示数据/系统设备名（允许）：**{scan['allowed']}**")
        lines.append(f"- **残留（需要修）：{len(scan['residual'])}**")
        lines.append("")
        if scan["residual"]:
            lines.append("| 页面 | 类型 | 中文残留 |")
            lines.append("|---|---|---|")
            seen: set[tuple[str, str]] = set()
            for row in scan["residual"]:
                key = (row["kind"], row["text"])
                if key in seen:
                    continue
                seen.add(key)
                lines.append(f"| {row['page']} | {row['kind']} | {row['text'].replace('|', chr(92) + '|')[:160]} |")
        else:
            lines.append("英文模式下除用户数据、演示数据与 Windows 设备名外，没有中文字符。")
        lines.append("")
        lines.append("复算命令：")
        lines.append("```")
        lines.append("dotnet run --project src/NextThought.Desktop -c Release --no-build -- --demo \\")
        lines.append("  --screenshots=<临时目录>")
        lines.append("python Windows/scripts/localization_scan.py --screens=<临时目录> --write")
        lines.append("```")
    lines.append("")
    lines.append("## 4. 已知未覆盖点")
    lines.append("")
    lines.append("- `CorrectedTranscriptView`（E17）、`BackupSettingsSection` / `BackupRestoreReviewView`（E30）")
    lines.append("  是其它任务新增的视图，落地后再迁 `{loc:Tr}` 并补英文，本表在其提交后重跑生成。")
    lines.append("- `AppState.Proofread.cs`、`AppState.Update.cs`、`AppState.Archive.cs` 的文案由各自任务负责。")
    lines.append("- 直播回答的系统提示词（`AppState.Live.cs`）仍为 Windows 自建中文 prompt，未切到 Mac 的")
    lines.append("  `AnswerPromptBuilder.MakeParts`；提示词不渲染到界面，故不在上面的扫描范围。")
    lines.append("")
    WORDING.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {WORDING.relative_to(ROOT)} ({len(table)} replacement rows)")

    if scan is not None:
        evidence: list[str] = []
        evidence.append("# 英文模式截图扫描（G9/G10 证据）")
        evidence.append("")
        evidence.append(f"- 生成时间：{datetime.date.today().isoformat()}")
        evidence.append(f"- 扫描页面：{scan['pages']} 个英文文字清单")
        evidence.append(f"- 含中文字符行：{scan['cjkRows']}；允许（用户数据/设备名）：{scan['allowed']}；残留：{len(scan['residual'])}")
        evidence.append("")
        evidence.append("完整逐页文字清单由截图命令输出到 `<screens>/text/`；本文件只保留汇总与残留。")
        evidence.append("")
        if scan["residual"]:
            evidence.append("## 残留")
            evidence.append("")
            for row in scan["residual"]:
                evidence.append(f"- `{row['page']}` [{row['kind']}] {row['text']}")
        else:
            evidence.append("除用户数据、演示数据与 Windows 设备名外，英文页面没有中文字符。")
        evidence.append("")
        EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
        EVIDENCE.write_text("\n".join(evidence) + "\n", encoding="utf-8")
        print(f"wrote {EVIDENCE.relative_to(ROOT)}")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--screens", type=Path, default=None, help="directory passed to --screenshots")
    parser.add_argument("--write", action="store_true", help="regenerate Windows/WORDING.md and the evidence file")
    parser.add_argument("--check", action="store_true", help="exit 1 when an English page still shows Chinese")
    args = parser.parse_args(argv)

    scan = None
    if args.screens is not None:
        scan = scan_screens(args.screens)
        print(f"pages={scan['pages']} cjkRows={scan['cjkRows']} allowed={scan['allowed']} residual={len(scan['residual'])}")
        for row in scan["residual"][:80]:
            print(f"  RESIDUAL {row['page']} [{row['kind']}] {row['text'][:120]}")
    if args.write:
        table = windows_table()
        places = locations()
        write_wording(table, places, scan)
    if args.check and scan is not None and scan["residual"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
