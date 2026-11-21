# §6.2/§6.5：会中窗口菜单 LiveWindowMenu（对照 LiveWindowController.swift）

Mac 参照：`Sources/NextThought/LiveWindowController.swift:99` 的 `LiveWindowMenu`（菜单：会中窗口 inline picker 三个模式 + 分隔线 + 「按 … 在三者之间切换」；按钮是 28px glass 图标 `captions.bubble`，help「会中窗口 · 快捷键」）。
Windows 实现：`Views/LiveView.axaml` 顶栏新增 `LiveWindowMenuButton`（`IconCaptionsBubble`，28×28，ToolTip 会中窗口）＋ Flyout：会中窗口标题、纪要窗口/字幕条/隐藏 三个 RadioButton（`SetLiveWindowModeCommand` 参数 notes/subtitles/hidden）、0.5px 分隔线、快捷键提示（`ToggleLiveWindowHint`）；同时从「更多」菜单删掉旧的「会中窗口」重复段（Mac moreMenu 中没有这一段）。
状态：`AppState.LiveWindowMenu.cs`（`LiveWindowShortcut` = Ctrl+Alt+Shift+S，`LiveModeIsNotes/Subtitles/Hidden`，`SetLiveWindowMode`）。
测试：`LiveWindowMenuParityTests` 3/3 通过（顶栏图标/三模式/命令参数/分隔线/提示、更多菜单去重、状态属性）。

## 逐元素

| # | Mac 元素 | Windows 现状 | 一致性 |
|---|---|---|---|
| 1 | 28px glass icon button，`captions.bubble` | quiet 28×28 + `IconCaptionsBubble` | 一致（玻璃背景由主题 Surface 近似） |
| 2 | help「会中窗口 · 快捷键」 | ToolTip「会中窗口」+ 菜单内快捷键提示行 | 部分一致（tooltip 未拼快捷键，菜单内有） |
| 3 | inline picker：纪要窗口 / 字幕条 / 隐藏 | 三个 RadioButton + `SetLiveWindowModeCommand` | 一致 |
| 4 | 分隔线 | 0.5px Hairline Border | 一致 |
| 5 | 「按 %@ 在三者之间切换」 | `ToggleLiveWindowHint`（UiText.Format + Ctrl+Alt+Shift+S） | 一致 |
| 6 | 快捷键循环（notes→subtitles→hidden） | `CycleLiveMode` 命令保留（热键路径） | 一致 |
| 7 | moreMenu 无会中窗口段 | 已从更多菜单删除重复段 | 一致 |

## 未解决

1. 弹出层无静态截图（Flyout 不属于窗口渲染树；`--self-test` 截图只截 Window）。如需视觉证据可后续给 LiveWindowMenuButton 点击后截 PopupRoot。
2. Tooltip 文案统一为「会中窗口」，未拼接快捷键（Mac help 是「会中窗口 · Ctrl+Alt+Shift+S」）；菜单内已显示快捷键。
3. 玻璃背景用主题 Surface 近似（Windows 无系统材质）。
