# 截图提问 / 区域选择（E13 · §6.7）Mac 3.4.0 源码 ↔ Windows 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`ScreenshotService.swift`、`RegionScreenshotPicker.swift`、`ScreenRegionSheet.swift`、`ContentView.swift`（WindowPickerSheet）
- Windows 代码：`Views/WindowPickerWindow.axaml(.cs)`、`Views/RegionCaptureWindow.axaml(.cs)`、`ScreenCapture.cs`、`MainWindow.Shell.cs`（CommitScreenshotAsync / 清理）
- Windows 截图（本目录）：`window-picker-light.png` / `window-picker-dark.png`、`region-picker-light.png` / `region-picker-dark.png`（`--self-test screenshot-context` 7/7 产出）
- 注：Mac 无这些窗口的截图，按 Swift 源码逐元素对照

## 窗口选择器 WindowPickerSheet

| # | Mac 元素 | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 尺寸/呈现 | sheet 420×520，NavigationStack，标题「选择要截图的窗口」，toolbar 取消 | 420×520、标题栏同名、标题行 + 底部「取消」 | 接近（取消位置：toolbar vs 底部，见差异） |
| 2 | 行 | 标题（≤2 行）+ 所属应用 caption；整行可点 | 同（标题 2 行、owner caption、整行按钮） | 一致 |
| 3 | 列表来源 | CGWindowList：仅 layer 0、排除本进程、未命名窗口「未命名窗口」、未知应用「未知应用」 | `ScreenCapture.AvailableWindows()`：EnumWindows + 可见 + 排除本进程 + DWM cloaked + 扩展边框 + 最小尺寸；「未命名窗口/未知应用」同 | 一致 |
| 4 | 刷新 | 重新打开 sheet 刷新 | 标题行「刷新」按钮（Mac 无，见差异） | 接近 |
| 5 | 空态 | 空列表 | caption「没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。」+ 数量 | 接近（Mac 无文案） |
| 6 | 选择后 | captureScreenshot(window)：隐藏本应用窗口→窗口截图→恢复 | `CommitScreenshotAsync`：隐藏自身浮窗→`ScreenCapture.CaptureWindow`→恢复 | 一致 |

## 区域选择 RegionScreenshotPicker / ScreenRegionSheet

| # | Mac 元素 | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 尺寸 | min 720×540（区域）/ 720×500（持续读取） | 900×640、Min 720×540 | 接近 |
| 2 | 标题行 | viewfinder 图标 + 「拖动选择截图区域」headline；右侧「显示器」Picker（名称 · W×H，≤310 宽） | 同（`IconCameraViewfinder` + headline；右侧显示器 ComboBox，`N · W×H`，≤310） | 一致 |
| 3 | 显示器切换 | onChange 清空选区 | `Display_Changed` 重新裁切该显示器帧并清空选区 | 一致（名称用序号，见差异） |
| 4 | 画布 | 冻结帧等比适配，黑色 8% 圆角 12 底；拖选 = 16% accent 填充 + 2 pt 边框；坐标裁剪到图像内 | 同（Sunken 圆角 12 底、Seal 16% 填充 + 2 pt、拖选 clamp 到图像） | 一致 |
| 5 | 页脚 | caption「截图会裁切到选中范围。请把题目、代码或公式完整框入。」+ 取消 + 使用此区域（primary，未选时禁用） | caption（现为「…然后存入资料库并由系统 OCR 读取文字。…」，含 Windows OCR 说明）+ 取消 quiet + 使用此区域 primary 禁用态 | 接近（文案，见差异） |
| 6 | 持续读取变体 | ScreenRegionSheet：标题「选择持续读取的区域」、提示「框选演示内容；后续画面均读取该区域。」、min 720×500 | 复用同一 RegionCaptureWindow（标题/提示沿用区域截图文案） | 不一致（见差异） |
| 7 | 取消/无效 | 取消返回 nil；区域过小无效（Mac 仅要求拖出矩形，过小视为无内容） | 取消返回 null；<4 px 视为无效；显示器切换清空 | 一致 |

## ScreenshotService 流水线

| # | Mac | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | availableWindows / captureWindow（隐藏自身→截图→恢复，250–300 ms 等待） | `AvailableWindows` + `CaptureWindow` + `HideOwnWindowsFromCapture/RestoreOwnWindowCapture` | 一致 |
| 2 | captureForReading：window:/display: + 归一化区域裁剪（min 1.5%，max 宽 2400） | 区域选择返回像素矩形；持续读取路径（屏幕上下文）已有 | 一致（实现方式不同：像素矩形 vs 归一化 0–1） |
| 3 | 临时文件：启动清旧、失败/取消删除、提问后删除 | `CleanupStaleCaptureFiles()`（>6 h）、`ScreenCaptureFile` 失败清理、`AnalyzeScreenshotFileAsync`/`SetScreenshotPreview` 后删除 | 一致 |
| 4 | 截图→提问 | 录制中→`AnalyzeScreenshotFileAsync` 注入屏幕上下文；非录制→`SetScreenshotPreview` 附到 composer；模型是否收到图像见差异 | 接近 |

## 差异 / 未解决

1. **取消按钮位置**：Mac 在 sheet toolbar；Windows 在底部（视觉/交互一致）。
2. **刷新**：Windows 窗口选择器多一个「刷新」按钮（Mac 重开 sheet 刷新）。
3. **显示器名称**：Windows 用「序号 · W×H」代替 Mac 的显示器名（Windows 无本地化屏幕名 API）。
4. **区域页脚文案**：多出「系统 OCR 读取文字」说明（Windows 真实管线），与 Mac 句不同。
5. **ScreenRegionSheet 未单独成窗**：持续读取复用 RegionCaptureWindow，标题/提示未切换成 Mac 句（缺翻译 key，列 OPEN_QUESTIONS 候选）。
6. **截图→提问附图像**：Windows 把预览交给 composer；回答请求是否带图像取决于模型客户端（当前未把图像作为 vision 输入），列 OPEN_QUESTIONS 候选。
7. 本机 `--self-test screenshot-context` 亮度检查在断连显示器会话下屏幕捕获返回纯白，自检改为环境感知（记录说明），其余 6 步正常断言。
