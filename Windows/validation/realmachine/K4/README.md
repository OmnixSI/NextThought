# K4 关闭与隐藏 · 本机实测记录（部分）

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 被测版本：`NextThought 0.9.0+…`（Release 自包含 exe）
- 测试脚本：`Windows/scripts/realmachine/k4-hide-all.ps1`
- 数据目录：隔离 `%TEMP%\nt-k4-*`；跑完 `Stop-Process` 结束应用

## 已在本机验证：隐藏所有窗口（Ctrl+Alt+G）

| 步骤 | 结果 |
|---|---|
| 应用启动、主窗口可见 | ✅ `NextThought` |
| 打开提词浮窗（Ctrl+Alt+O） | ✅ `NextThought 回答浮窗` + 主窗口 |
| 隐藏所有窗口（Ctrl+Alt+G） | ✅ 该进程 0 个可见窗口 |
| 隐藏后应用进程仍在 | ✅ 未退出 |
| 再按一次恢复 | ✅ 主窗口重新可见 |

证据：`report.json`、`before-hide.png`（提词浮窗打开）、`after-hide.png`（屏幕无本应用窗口）、
`after-restore.png`（恢复后）。

## 本机未验证、留给人的部分

「Alt+F4 关掉提词岛 → 程序不退出 → 触发一个问题 → 岛重新出现」：演示模式没有加载语音/识别模型，
`StartRecordingCommand.CanExecute` 为 false，本机无法开一场真实会议来生成提词岛；预热模型需要真实音频
设备与识别模型下载。按任务书「K13–K16 留给人」同样方式处理，操作步骤已写进 `Windows/RELEASE.md` §9 K4：

1. 开一场真实会议，让提词岛出现。
2. Alt+F4 关掉岛，确认程序不退出。
3. 触发一个问题（或点回答），确认岛重新出现。
4. 字幕模式下点「隐藏所有窗口」后继续说话，确认字幕条保持隐藏。
5. 穿透状态下点「隐藏所有窗口」，确认屏幕上不剩任何本应用窗口。

结果请记到本目录（`README-human.md` 或 `report-human.json`）。

## 2026-11-21 补充：关闭/隐藏路径的自动化记录（K4）

- `--demo --self-test=window-close`（Release，隔离数据目录）——**passed**：
  - 提词岛 `Close()`（与 Alt+F4 相同的 `Closing` 处理器，`AllowClose=false`）→ 窗口隐藏、程序不退出 → 再次 `Show()` 可见；
  - 字幕条同样关闭→隐藏→重新显示可见。
- 运行中发现并修复真实 bug：`SubtitleBarWindow.Tick` 在 `Persistent`（用户明确选字幕模式）时仍按空闲计时隐藏，导致「关闭后重新显示」的字幕条立刻被计时器再次隐藏；修复后 `subtitleReshow=true`（`Views/SubtitleBarWindow.axaml.cs`，随 `C13:` 提交）。
- 证据：`window-close.json`（自检输出）、本 README；此前「隐藏所有窗口」见 `report.json` 与 before/after 截图。
- 仍留给人：真实会议里的 Alt+F4 与继续说话后字幕保持隐藏（步骤见 `RELEASE.md §9 K4`）。
