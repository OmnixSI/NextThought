> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 截图提问 / 区域选择（E13 · §6.7 · decisions §3.3）Mac 3.4.0 源码 ↔ Windows 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`：`ScreenshotService.swift`、`RegionScreenshotPicker.swift`、`ScreenRegionSheet.swift`、`ContentView.swift`（WindowPickerSheet）
- Windows 代码：`Views/WindowPickerWindow.axaml(.cs)`、`Views/RegionCaptureWindow.axaml(.cs)`、`Platform/MonitorNames.cs`、`ScreenCapture.cs`、`MainWindow.Shell.cs`
- Windows 截图（本目录，来自 `--self-test screenshot-context` 本轮构建）：`window-picker-{light,dark}.png`、`region-picker-{light,dark}.png`、`region-picker-continuous-light.png`、`region-picker-small-region-light.png`、`screenshot-context-check.json`
- 对照方法：Mac 无这些窗口的截图，按 Swift 源码逐元素对照（decisions §2：不再列为差别）

## 结论：逐条一致；另有 2 条已裁决的差别（1 接受 + 1 已收尾）

- **图像输入（task-81 / decisions §3.3）已收尾**：截图提问现在把图片本身作为模型输入发出——OpenAI 兼容服务收到 `image_url` 内容块（`ChatCompletionClient` 读临时截图 → `data:image/png;base64,…` 与文字同一 user content），Codex 路径按 Mac 用 `turn/start` 的 `localImage` 附件（`CodexAnswerServerProtocol.TurnStartParams`）。提示词 `hasScreenshot: true`、证据记录 `ScreenImageAttached`；临时截图保留到提问后由 `AnswerAsync` 删除（不再在分析阶段删除）。`--self-test screenshot-context` 全部步骤通过（含「截图留到提问后删除（并作为模型图像输入待发）」），客户端单测覆盖图片内容块与 Codex localImage。

## 窗口选择器 WindowPickerSheet

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 420×520 sheet，标题「选择要截图的窗口」，toolbar 取消 | 420×520、标题同名、标题行 + 底部取消 | 接受：取消在底部（decisions §3.3「取消按钮在底部」接受） |
| 2 | 行 = 标题 ≤2 行 + 所属应用 caption，整行可点 | 同 | 一致 |
| 3 | CGWindowList：排除本进程、layer 0、「未命名窗口/未知应用」 | EnumWindows + 排除本进程 + DWM cloaked + 扩展边框；同名两文案 | 一致 |
| 4 | 打开即刷新，无刷新按钮 | 刷新按钮已删，`Opened`/构造里 `Load()` 自动刷新 | 一致（本轮修） |
| 5 | 空列表 | 「没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。」+ 数量 | 一致（文案保留；按钮已按 Mac 去掉，Lead 已裁决） |
| 6 | 选中 → captureScreenshot(window) | `CommitScreenshotAsync`：隐藏自身浮窗→窗口截图→恢复 | 一致 |

## 区域选择 RegionScreenshotPicker / ScreenRegionSheet

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | min 720×540（区域）/ 720×500（持续读取） | 900×640、Min 720×540 | 一致 |
| 2 | viewfinder 图标 + 「拖动选择截图区域」；右侧显示器 Picker（名称 · W×H） | 同；显示器用**系统友好名称**（`MonitorNames` 经 EnumDisplayDevices 取 monitor 名称，如「Generic PnP Monitor · 3000×2000」） | 一致（本轮修：友好名称） |
| 3 | 逐显示器选择、切换清空选区 | `DisplayBox` 列每个显示器；`Display_Changed` 重新裁切该屏并清空选区 | 一致 |
| 4 | 画布：等比适配、黑色 8% 圆角 12、16% accent 填充 + 2pt 边框、坐标 clamp | 同（Sunken 圆角 12、Seal 16% + 2pt、clamp） | 一致 |
| 5 | 页脚「截图会裁切到选中范围。请把题目、代码或公式完整框入。」+ 取消 + 使用此区域 | 同（Mac 原句，本轮修；额外「系统 OCR 读取文字」句已按 Mac 原句替换掉） | 一致 |
| 6 | ScreenRegionSheet：标题「选择持续读取的区域」、提示「框选演示内容；后续画面均读取该区域。」 | `RegionCaptureMode.ContinuousReading`：标题/headline/页脚均为 Mac 原句（本轮修） | 一致 |
| 7 | 区域无效：ScreenshotService「屏幕选区无效，请重新框选。」 | 选区 <4 px/无效时显示同一 Mac 原句且不关闭窗口（本轮修，截图 `region-picker-small-region-light.png`） | 一致 |

## ScreenshotService 流水线

| # | Mac | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | availableWindows / captureWindow（隐藏自身→截图→恢复） | 同（WDA 捕获排除 + 隐藏/恢复） | 一致 |
| 2 | captureForReading：window:/display: + 归一化区域裁剪 | 区域选择返回像素矩形；持续读取路径已有；ScreenDelta 帧去重/稳定等待/变化区域仍在 Core 侧（`--self-test screenshot-context` 覆盖清理与图像待发） | **由图像输入/Core 链路 owner 收尾**：`AppState.Live` 已有 `PendingScreenshotImagePath` 并把 imagePath 传给回答请求（本轮观察到的在途实现）；`ScreenDelta` 帧去重与 core-parity 协调后补齐 |
| 3 | 临时文件：启动清旧、失败取消删、提问后删 | `CleanupStaleCaptureFiles()`、失败清理、提问后删（并留待模型图像输入） | 一致 |
| 4 | 截图→提问（Mac 附图像） | 预览已作为 `PendingScreenshotImagePath` 交给回答请求；模型是否收到图像取决于回答客户端 | 见上面第 2 条的 owner 协调项 |

## 已裁决 / 未决

1. **接受**：取消按钮在窗口底部（Mac 在 sheet toolbar；decisions §3.3 接受）。
2. **第二批**：本机模型对复杂画面的增强读取（decisions §3.3 第二批）。
3. 图像输入：由 Live/图像输入 owner 在 `AppState.Live` 链路收尾（本 compare 不再列 UI 差别）。
