# UI 第一批：主窗口 / 提词岛 / 设置 / 历史

按路径优先级，而不是字母顺序；本轮先做前两块，设置页与历史归档保留给下一批。

## 1. 主窗口（核对，不做像素改动）

- Mac `NextThoughtApp.swift`：`MainWindowLayout.ideal = 1120×820`，`minimum = 960×660`。
- Windows `MainWindow.axaml`：`Width=1120 Height=820 MinWidth=960 MinHeight=660`。
- 结论：数值一致，本轮不改主窗口尺寸；逐视图截图对照仍为“未建立”。

## 2. 提词岛（本轮改动）

- 新增设置 `cueWidth`：窄 440 / 标准 540 / 宽 660；`CueIslandWindow` 启动和设置变化时应用。
- 设置页“外观”卡片增加「提示岛宽度 / Cue island width」下拉。
- Windows 没有 Mac 刘海，岛仍固定在主屏顶部居中；默认宽度对齐 Mac regular 540，而不是原来的固定 560。
- QA：`--demo --share-hide-check` 实测 `width=660`（宽档），并验证真实标题为 `Zoom - Screen Sharing` 的窗口被检测到、共享时收回、结束后恢复。

## 3. 设置页 / 历史归档

- 设置页已有搜索、接口预设、外观、字幕条；逐视图差异仍待下一批按清单逐项比对。
- 历史归档未动像素；清单见 `Windows/UI_PARITY_INVENTORY.md`。

## 4. 共享时自动收回（对应第 4 步）

见 `Windows/SCREEN_SHARE_FALLBACK.md`。
