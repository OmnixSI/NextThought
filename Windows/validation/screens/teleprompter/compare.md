# 回答浮窗（E29 · §6.7）Mac 3.4.0 源码 ↔ Windows TeleprompterWindow 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`Sources/NextThought/TeleprompterController.swift`（TeleprompterView）
- Windows 代码：`Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml(.cs)`
- Windows 截图（本目录）：`teleprompter-camera-light.png`、`teleprompter-camera-dark.png`（窗口自身内容；三位置的屏幕坐标由自检数值断言：camera=800,12 / bottom=800,954 / right=1576,495 @1440p）
- 证据生成：`--self-test windows`

| # | Mac 元素 | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 窗口 | 640×300 起、最小 360×160、可移动/缩放、非激活、不清空空间 | 同（`Width=640 Height=300 MinWidth=360 MinHeight=160`，`WS_EX_NOACTIVATE`） | 一致 |
| 2 | 表面 | `RoundedRectangle(22)` raised + hairline；隐背景时 clear + 1.5 pt ink 边 | 同（`Surface` / `HiddenBackgroundBorder`） | 一致 |
| 3 | 头部 | 7 pt 状态点、12 medium 状态文字（正在听/已暂停/未在听）、12 次要色问题（或候选）、回答中 ProgressView、Spacer | 同（`StatusDot/StatusText/QuestionText/AnsweringProgress`） | 一致 |
| 4 | 头部按钮 | 上一条/位置/下一条、文字缩小/放大（24 pt 玻璃图标按钮）、鼠标穿透（cursorarrow）、关闭 | 同（`tpIcon` 24×24；图标取自共享图标表） | 一致 |
| 5 | 正文 | `MarkdownText`，字号 teleprompterFontSize，行距 45%，跟随最新；滚开显示「回到最新」 | 同（`MarkdownText` 正文、`ScrollFollowState`、`FollowButton`） | 一致 |
| 6 | 空状态 | 「正在生成…」/「回答会显示在这里。你也可以按 {shortcut} 手动请求回答。」size×0.75 次要色 | 同 | 一致 |
| 7 | 底部 | 鼠标穿透时隐藏；否则 更简短/举例/英文版 + 「…」菜单（展开/STAR/中文/重新生成/联网查一下/复制回答/打开主窗口）+ 停止生成/立即回答 | 同（`Footer`），菜单项受限见下 | 大体一致 |
| 8 | 字号 | 14–36，步进 2，自己保存（不跟全局文字缩放） | 同（`Settings.TeleprompterFontSize`，标题栏 ± 按钮） | 一致 |
| 9 | 透明度 | `teleprompterOpacity` 0.35–1 | 同（`SettingsSnapshot.TeleprompterOpacity`） | 一致 |
| 10 | 三种位置 | camera：可见顶+8；bottom：底−24；right：右−16 垂直居中 | 同（`MoveTo`） | 一致 |
| 11 | 鼠标穿透 | 整窗穿透，单独小面板保留还原按钮 | 同（`TeleprompterHandleWindow` 常驻还原） | 一致 |

## 差异 / 未解决

1. Mac 「…」菜单还有「先澄清」「追问」两项：Windows 字符串表没有对应行，未加入（新增表行后补）。
2. Mac 窗口无系统标题栏/红绿灯（玻璃面板自绘）；Windows `WindowDecorations=None` + 自绘头部，等价。
3. 截图使用透明背景窗口捕获，无 Mac 桌面背景。
