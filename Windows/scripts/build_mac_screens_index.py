#!/usr/bin/env python3
"""Build the Mac -> Windows screenshot index in validation/mac-screens/README.md.

The Mac reference batch (validation/mac-screens/) was produced by the Mac app; the Windows
screenshot batch (validation/screens/current/, untracked) comes from
`NextThought.exe --demo --screenshots=<dir>`; the per-view comparison documents live in
validation/screens/<view>/compare.md.

This script only writes the marked index section of README.md, so the hand-written Mac notes at
the top of the file are preserved. Run it from anywhere:

    python Windows/scripts/build_mac_screens_index.py

Exit code is 0 when the index was written, 1 when a mapping points at a missing file (the index is
still written so the gap is visible).
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAC_ROOT = ROOT / "validation" / "mac-screens"
CURRENT = ROOT / "validation" / "screens" / "current"
README = MAC_ROOT / "README.md"
START = "<!-- mac-win-index:start -->"
END = "<!-- mac-win-index:end -->"

# (Mac directory, Mac base name) -> (Windows current base names, compare doc, note)
# A Windows base name is paired with -light/-dark unless it already carries a suffix.
MAPPING: dict[tuple[str, str], tuple[list[str], str, str]] = {
    ("home", "home"): (["home"], "home/compare.md", ""),
    ("prepare", "prepare"): (["prepare"], "prepare/compare.md", ""),
    ("live", "live"): (["live"], "live/compare.md", ""),
    ("live", "live-notes"): ([], "live/compare.md", "Windows 会中页没有「实时纪要」右栏（E11 之后的独立视图未落地）"),
    ("notes", "review"): ([], "notes/compare.md", "Windows 刷新批次只截了纪要/转写/回答/个人笔记四个标签，回顾页未截"),
    ("notes", "work"): ([], "notes/compare.md", "同上：工作会议纪要模板页未截"),
    ("notes", "transcript"): (["transcript"], "notes/compare.md", ""),
    ("series", "series"): (["series", "library"], "series/compare.md", "Windows 还多一个仅 Windows 的「资料库」页（导入/解析资料，Mac 无直接对应）；Mac 系列页的对应主体是 series-*"),
    ("settings", "window"): (["settings"], "settings-structure/compare.md", ""),
    ("settings", "quick-settings"): (["quick-settings"], "settings-pages/compare.md", ""),
    ("settings-pages", "scenes"): (["settings-scene"], "settings-pages/compare.md", ""),
    ("settings-pages", "answers"): (["settings-answer"], "settings-pages/compare.md", ""),
    ("settings-pages", "presenter"): (["settings-coach"], "settings-pages/compare.md", ""),
    ("settings-pages", "notes"): (["settings-notes"], "settings-pages/compare.md", ""),
    ("settings-pages", "general"): (["settings-general"], "settings-pages/compare.md", ""),
    ("settings-pages", "advanced"): (["settings-advanced"], "settings-pages/compare.md", ""),
    ("onboarding", "step-1"): (["onboarding-step0"], "onboarding/compare.md", ""),
    ("onboarding", "step-2"): (["onboarding-step1"], "onboarding/compare.md", ""),
    ("onboarding", "step-3"): (["onboarding-step2"], "onboarding/compare.md", ""),
    ("onboarding", "step-4"): (["onboarding-step3"], "onboarding/compare.md", ""),
    ("island", "cue"): (["island-cue"], "island/compare.md", ""),
    ("island", "hearing"): (["island-hearing"], "island/compare.md", ""),
    ("island", "deck"): (["island-deck"], "island/compare.md", ""),
    ("island", "launcher"): (["island-launcher"], "island/compare.md", ""),
    ("island", "pinned"): ([], "island/compare.md", "Windows 提词岛没有「钉住展开（双语）」态"),
    ("island", "autoend"): ([], "island/compare.md", "Windows 提词岛没有「自动结束提醒」态"),
}

# Mac view names for the table.
VIEW_NAMES: dict[tuple[str, str], str] = {
    ("home", "home"): "HomeView 首页",
    ("prepare", "prepare"): "PrepareView 准备页",
    ("live", "live"): "LiveSessionView 会中（问答标签）",
    ("live", "live-notes"): "会中右栏实时纪要",
    ("notes", "review"): "NotesPageView 回顾纪要",
    ("notes", "work"): "工作会议纪要",
    ("notes", "transcript"): "会后转写标签",
    ("series", "series"): "WorkspacesView 系列页",
    ("settings", "window"): "主窗口里的设置（通用页）",
    ("settings", "quick-settings"): "齿轮弹出的快速设置",
    ("settings-pages", "scenes"): "设置 · 场景",
    ("settings-pages", "answers"): "设置 · 回答与资料",
    ("settings-pages", "presenter"): "设置 · 提词与教练",
    ("settings-pages", "notes"): "设置 · 纪要",
    ("settings-pages", "general"): "设置 · 通用",
    ("settings-pages", "advanced"): "设置 · 高级",
    ("onboarding", "step-1"): "引导 · 第一步 场合",
    ("onboarding", "step-2"): "引导 · 第二步 声音",
    ("onboarding", "step-3"): "引导 · 第三步 回答服务",
    ("onboarding", "step-4"): "引导 · 第四步 提词与开始",
    ("island", "cue"): "提词岛 · 参考回答",
    ("island", "hearing"): "提词岛 · 对方在说",
    ("island", "deck"): "提词岛 · 会中面板",
    ("island", "launcher"): "提词岛 · 无会议启动器",
    ("island", "pinned"): "提词岛 · 钉住展开（双语）",
    ("island", "autoend"): "提词岛 · 自动结束提醒",
}


def rel(path: Path) -> str:
    return path.relative_to(MAC_ROOT).as_posix()


def link(text: str, path: Path) -> str:
    return f"[{text}]({path.as_posix()})"


def windows_cells(bases: list[str]) -> str:
    cells: list[str] = []
    for base in bases:
        if base in {"island-cue", "island-hearing", "island-deck", "island-launcher"}:
            files = [CURRENT / (base + ".png")]
            if files[0].exists():
                cells.append(link(base + ".png", Path("..") / "screens" / "current" / files[0].name))
                continue
        for theme in ("light", "dark"):
            candidate = CURRENT / f"{base}-{theme}.png"
            if candidate.exists():
                cells.append(link(candidate.name, Path("..") / "screens" / "current" / candidate.name))
    return "<br>".join(cells) if cells else "—"


def mac_cells(directory: str, base: str) -> str:
    cells = []
    for theme in ("light", "dark"):
        candidate = MAC_ROOT / directory / f"{base}-{theme}.png"
        if candidate.exists():
            cells.append(link(candidate.name, Path("..") / "mac-screens" / directory / candidate.name))
    return "<br>".join(cells)


def main() -> int:
    mac_images = sorted(MAC_ROOT.rglob("*.png"))
    if not mac_images:
        print("no Mac screenshots found", file=sys.stderr)
        return 1
    missing: list[str] = []
    rows: list[str] = []
    used_current: set[str] = set()
    for (directory, base), (bases, compare, note) in MAPPING.items():
        mac = mac_cells(directory, base)
        if mac == "":
            continue
        windows = windows_cells(bases)
        for item in bases:
            for suffix in ("-light", "-dark", ""):
                candidate = f"{item}{suffix}.png"
                if (CURRENT / candidate).exists():
                    used_current.add(candidate)
        compare_link = f"[{compare}](../screens/{compare})"
        note_cell = note
        rows.append(f"| {mac} | {VIEW_NAMES.get((directory, base), base)} | {windows} | {compare_link} | {note_cell} |")
        if windows == "—":
            missing.append(f"{directory}/{base}")

    # Mac states that are not screenshots of a Windows page: the live page state matrix is
    # generated by --self-test live-session-ui, so it only maps to the live compare document.
    state_rows = []
    for image in mac_images:
        if image.parent.name != "live-states":
            continue
        theme = "light" if "-light" in image.stem else "dark"
        state_rows.append(
            f"| {link(image.name, Path('..') / 'mac-screens' / 'live-states' / image.name)} | 会中状态 · {image.stem.replace('-light', '').replace('-dark', '')} | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |"
        )
    if state_rows:
        rows.append("")
        rows.append("**会中状态矩阵（live-states，Mac 20 种状态 × 浅/深）：**")
        rows.append("")
        rows.extend(state_rows)

    total = len(mac_images)
    paired = sum(1 for (directory, base) in MAPPING
                 if (MAC_ROOT / directory / f"{base}-light.png").exists())
    current_count = len(list(CURRENT.glob("*.png")))
    extras = sorted(p.name for p in CURRENT.glob("*.png") if p.name not in used_current)

    lines = [START,
             "<!-- 由 Windows/scripts/build_mac_screens_index.py 生成，勿手改此区间。 -->",
             "",
             "## Windows 对照索引",
             "",
             f"Mac 参考图共 **{total}** 张（{paired} 组视图 × 浅/深）；Windows 最新截图批次 "
             f"`validation/screens/current/` 共 **{current_count}** 张（本文件不包含 PNG，"
             "Windows 截图目录不入库）。下表把每张 Mac 图对到 Windows 截图与逐元素对照文档。",
             "",
             "| Mac 参考图（浅 / 深） | Mac 视图 | Windows 截图（`validation/screens/current/`） | 对照文档 | 说明 |",
             "|---|---|---|---|---|",
             *rows,
             "",
             "### 未覆盖",
             ""]
    if missing:
        lines.append("Mac 有、Windows 刷新批次没有对应截图的视图：")
        lines.append("")
        for item in missing:
            lines.append(f"- `{item}`")
    else:
        lines.append("- 无。")
    if extras:
        lines.append("")
        lines.append("Windows 截图批次里没有 Mac 参考的视图（英文界面版本同源，不单列）：")
        lines.append("")
        for name in extras:
            lines.append(f"- `validation/screens/current/{name}`")
    lines.extend(["", "### 使用方法", "",
                  "1. 在 `validation/screens/current/` 跑一次 `NextThought.exe --demo --screenshots=validation/screens/current`，"
                  "Windows 截图不入库，只用于本地对照。",
                  "2. 打开 `validation/screens/<视图>/compare.md`，按「Mac 参考图」一节的链接逐张对照浅/深两版。",
                  "3. Mac 图更新后重跑 `python Windows/scripts/build_mac_screens_index.py` 刷新本索引。",
                  END])

    text = README.read_text(encoding="utf-8")
    if START in text and END in text:
        before = text.split(START, 1)[0]
        after = text.split(END, 1)[1]
        text = before + "\n".join(lines) + after
    else:
        text = text.rstrip() + "\n\n" + "\n".join(lines) + "\n"
    README.write_text(text, encoding="utf-8", newline="\n")
    print(f"index written: {total} Mac images, {current_count} current screenshots, "
          f"{len(missing)} uncovered Mac views")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
