> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# Shell / 会中组件样板（E4 · §6.7）Mac 3.4.0 源码 ↔ Windows 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`ContentView.swift`（ErrorBanner / FocusModeView / LevelMeters）、`LiveWindowController.swift`（LiveWindowMenu）、`LiveSessionAcceptanceComponentSamples.swift`
- Windows 代码：`Views/MainWindow.axaml`（错误横幅/专注态外壳）、`Views/Shell/LevelMeters.cs`、`Views/Shell/LiveComponentSamplesWindow.axaml(.cs)`、`Views/LiveView.axaml`（会中窗口菜单等价物）、`AppState.Shell.cs`
- Windows 截图（本目录）：`shell-{normal,error,meters,focus}-{light,dark}` 组合、`shell-save-blocked-light.png`、`live-window-menu-light.png`、`live-component-samples-{light,dark}.png`、`shell-check.json`（本轮构建）
- 证据生成：`--self-test shell`（隔离数据目录，6/6 passed）

| # | Mac 视图 | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | `ErrorBanner` | 纵向 HStack：caution 三角 + 可选中正文 + 弹性空白 +「重试保存」ghost（有保存错误时）+ 关闭 X（24 玻璃）；padding 12、maxWidth 560、floatingSurface 圆角、顶部 12 | `Views/MainWindow.axaml` 顶层卡片：`IconExclamationmarkTriangleFill` caution + `SelectableTextBlock` + 关闭 `IconXmark` 按钮（`CloseStatusCommand`）；`SurfaceRaised`+`Hairline` 0.5+圆角 12+`FloatShadow`；`IsVisible="{Binding StatusIsError}"`；`SetStatus(error:true)` 驱动 | 一致（重试保存按钮 Windows 无对应 userSaveErrors 队列，见差异 1） |
| 2 | `FocusModeView` | 录制中窗口只属于会议：`LiveSessionView()` 顶替整个 shell | `Views/MainWindow.axaml`：录制时标题胶囊/右侧工具按钮隐藏（`IsVisible="{Binding !Recording}"`）、上下文侧栏自动收起，页面区为会中页；**底部常驻文字条已整体删除** | 一致（接受：系统标题栏保留窗口按钮） |
| 3 | `LevelMeters` | 8 Hz 读 `echoMonitor.levels`：两条 4 pt 胶囊，对方 secondary、我 ink（回声时 caution）；tooltip 对方/我 | `Views/Shell/LevelMeters.cs`（Dispatchers 125 ms，读同一 AudioLevel），放在 **live 顶栏**（Mac expandedControls 位置）；4 pt 胶囊、secondary/ink；tooltip `AudioLevelsText` | 一致（回声着色列第二批） |
| 4 | `LiveWindowMenu` | 玻璃图标菜单：会中窗口 picker（纪要窗口/字幕条/隐藏）+ 分隔 + 快捷键提示 | `Views/LiveView.axaml` 顶栏 `LiveWindowMenuButton`（captions bubble 图标）+ 三项 `RadioButton`（`SetLiveWindowModeCommand` notes/subtitles/hidden）+ `ToggleLiveWindowHint`（按 Ctrl+Alt+Shift+S 循环）；弹出层截图 `live-window-menu-light.png` | 一致（本轮修：三项选择 + 快捷键提示） |
| 5 | `LiveSessionAcceptanceComponentSamplesView` | 1200×1080 调试样张：标题/说明 + 行动按钮、数据状态、来源 chip、转写行、任务行、修订 diff、内联提示、新内容/输入框卡片；合成内容不触达真实管线 | `Views/Shell/LiveComponentSamplesWindow.axaml(.cs)`：同尺寸/标题/说明 + 6 个样张卡（按钮状态、数据状态、来源 chip、转写行、任务行、内联提示）；仅由 `--self-test shell` 打开，正常入口不可达（debug-only） | 一致（卡片从 8 组精简为 6 组，见差异 5） |

## 差异 / 未解决
1. ErrorBanner 的「重试保存」：Mac 依据 `SessionArchive.userSaveErrors` 显示；Windows 写失败重试在写队列内部（`writes`/SetStatus），无对应队列暴露，暂只保留关闭按钮。
2. FocusMode 仍保留系统标题栏最小化/关闭按钮（Mac 窗口常驻交通灯），内容区已无壳元素。
3. LevelMeters 无 macOS `echoMonitor.microphoneIsEcho` 概念（Windows 回声抑制是文本级 backstop），麦克风条恒用 ink。
4. （已修）LiveWindowMenu 三项选择 + 快捷键提示在弹出层内；弹出层截图 `live-window-menu-light.png`。
5. 样张页 8 组 → 6 组合并（revision diff/new content composer 未单列），不影响真实页面组件。
