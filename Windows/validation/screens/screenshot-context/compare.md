> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# E13 截图提问与屏幕上下文：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/ScreenshotService.swift`（211 行全文：availableWindows/availableDisplays/captureForReading/captureWindow/crop/writePNG/optimizedImageURL/WindowCoordinator）、`Sources/NextThought/ContentView.swift`（WindowPickerSheet 217-239、share/region sheet 挂载 57-70）、`Sources/NextThought/ScreenRegionSheet.swift`（全文）、`Sources/NextThought/ScreenContextService.swift`（全文：QuestionScreenContext/refresh/recognize/enhance/questionImage）
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无对应截图，按源码逐元素对照
Windows 代码：`Views/MainWindow.Shell.cs`（CaptureWindowAsync/CaptureRegionAsync/PickScreenRegionForWatchAsync/CommitScreenshotAsync/清理）、`Views/WindowPickerWindow.axaml(.cs)`（新增）、`Views/RegionCaptureWindow.axaml(.cs)`、`ScreenCapture.cs`（AvailableWindows/CaptureWindow/AverageBrightness 等）、`AppState.Home.cs`（截图预览与分析）、`Platform/CaptureFiles.cs`（新增）
Windows 截图：待 verify 阶段补跑（本轮按 Lead 要求暂停应用启动/截图）

## 1. 截图入口与窗口选择（ScreenshotService.availableWindows / captureWindow；ContentView.WindowPickerSheet）

| Mac | Windows | 结论 |
|---|---|---|
| `availableWindows`：CGWindowList 列出 on-screen 普通层窗口，**排除 ownPID**，标题空则「未命名窗口」，owner 名称 | `WindowPickerWindow` + `ScreenCapture.AvailableWindows()`：EnumWindows 可见窗口、**排除本进程**、跳过 DWM cloaked（其他虚拟桌面/UWP 幽灵）、标题为空跳过、尺寸过小隐藏；行显示标题 + 所属进程 + 尺寸 | 一致（阈值与文本不同，见未解决） |
| WindowPickerSheet：420×520 列表（标题 + owner），点一行即截图，取消按钮 | `WindowPickerWindow` 460×540：标题 + owner·尺寸，点一行返回所选目标；刷新 + 取消；空列表提示「请先打开要截图的窗口」 | 一致 |
| captureWindow：先 `WindowCoordinator.hide()`（隐藏本应用所有窗口），等待 300ms，`screencapture -l <windowID>`，失败提示「窗口…已关闭或无法捕获」 | `CaptureWindowAsync`：先 `HideOwnWindowsFromCapture()`（WDA_EXCLUDEFROMCAPTURE 应用全部窗口）→ 最小化主窗 → 360ms → 整屏抓取并按 DWM 扩展边框裁切；失败提示「窗口…已关闭或无法捕获，请刷新窗口列表后重试。」 | 一致（Win 用整屏抓取+裁切代替单窗口 API；排除自身用显示亲和性） |
| 截完 `WindowCoordinator.show()` 恢复所有窗口 + 激活应用 | `RestoreWindow` + `RestoreOwnWindowCapture`（恢复用户 CapturePrivacy 设置），在 finally 中执行，异常也恢复 | 一致 |
| 显示器列表 availableDisplays + captureDisplay（可指定显示器） | 无显示器选择：窗口选择器按窗口粒度选；区域选择在虚拟屏幕（多显示器）上框选 | 部分（无逐显示器列表） |

## 2. 区域选择（ScreenRegionSheet / RegionSelectionCanvas）

| Mac | Windows | 结论 |
|---|---|---|
| 先截当前显示器，`RegionSelectionCanvas` 拖动框选（归一化矩形），提示「框选演示内容；后续画面均读取该区域。」，取消 / 使用此区域 | `RegionCaptureWindow`：VirtualScreen 整屏图，拖动 `Canvas` 框选（letterbox 适配、像素换算、选区尺寸提示），取消 / 使用此区域；另用于读屏区域选择（保存 InsightScreenRegion） | 一致（均为整屏截图后框选；Win 用虚拟屏幕而非单显示器） |
| `crop(image:normalizedRect:)`：选区 <1.5% 报「选取范围太小…」 | `Crop(capture, PixelRect)`：宽/高 <4px 视为取消；无最小比例提示文案 | 部分（文案/阈值不同） |
| 截图前隐藏本应用窗口（captureForReading 用 SCContentFilter 排除自身 applications） | 区域/窗口截图前 `HideOwnWindowsFromCapture()`；读屏（ScreenWatch）此前用 `ScreenCapture.ExcludeOwnWindowsFromCapture()` | 一致 |

## 3. 屏幕上下文与提问（ScreenContextService.QuestionScreenContext / refresh / questionImage）

| Mac | Windows | 结论 |
|---|---|---|
| QuestionScreenContext = { ScreenContext（frameID/regions/captureRegion）、image URL、note、slide excerpt } | 无同构对象：截图 →`AnalyzeScreenshotFileAsync`：归档截图 → 资料库导入（Windows.Media.Ocr 解析）→ `PromptAboutScreenshot(Library.SelectedPreview)`（把 OCR 文字注入提问）→ `FollowSlideFromCapture` | 部分（没有结构化 ScreenContext/变化区域；上下文以 OCR 文本注入） |
| refresh：抓帧 → SHA-256 frameID 去重 → 0.5s 稳定等待 → 变化区域 delta → OCR regions → 可选本机模型增强（复杂公式/表格）→ status「画面已变化，等待稳定…/正在识别当前画面…/已读取当前画面」 | `ScreenWatchTickAsync`：定时截屏 → OCR → 文本哈希去重 → 跟随幻灯片；无「稳定等待/变化区域/本机增强」 | 部分（读屏轮询更简单） |
| questionImage(frameID)：把当前钉住的帧复制为临时 jpg 附在提问请求上（后续画面变化不影响已附图片） | 提问走 OCR 文本，不附图；截图文件在分析后删除 | 差异（未附图给模型，记未解决） |
| 截图后直接提问（answerScreenshot：分析截图→自动填入提问→发送） | 记录中截图：`CommitScreenshotAsync` → `AnalyzeScreenshotFileAsync` → `PromptAboutScreenshot`（写入 DetectedQuestion/prompt）；未记录：截图挂到首页「截图卡」，「分析截图」再走同一路径 | 一致（Windows 分会议中/空闲两条路径，Mac 同样用 home 截图卡） |

## 4. 临时文件清理与隐藏主窗后弹窗（修复项）

| 问题 | Mac | Windows 修复 |
|---|---|---|
| 临时图片不删除 | `FileManager.temporaryDirectory` 写入，`captureForReading` 的帧按需 `questionImage` 复制，`optimizedImageURL` 临时 jpg；截图卡清除时删预览 | 记录中：`AnalyzeScreenshotFileAsync` 在 finally 删除（失败也删）；空闲：文件由首页截图卡持有，`ClearScreenshotPreview`/分析后删除；取消/失败的截图在 finally 删除；启动时 `CleanupStaleCaptureFiles()` 清 >6h 的 `nextthought-shot-/window-*.png`（`CaptureFiles` 纯函数可测） |
| 隐藏主窗口后弹窗失败 | `WindowCoordinator.show()` 后才弹 sheet | 恢复窗口后再弹 `RegionCaptureWindow`/`WindowPickerWindow`；`RestoreWindow` 在 finally 执行，异常也恢复；不再在最小化状态下弹 dialog |
| 窗口截图截到自己的浮窗 | captureForReading 排除本应用 applications；captureWindow 先隐藏所有本应用窗口 | 截图前 `WindowCaptureHiding.ApplyAll(true)`（WDA_EXCLUDEFROMCAPTURE），截完恢复用户设置；自检用同一屏幕区域亮度对比验证 |

## 5. 自检点按记录（`screenshot-context`，真实桌面）

自检 `RunScreenshotContextCheckAsync` 已加入（6 步：窗口列表排除自身窗口、窗口选择器 UI、区域选择 UI、同一屏幕区域排除前后亮度对比、旧临时截图清理、提问后删除临时截图），每一步都会写入 `screenshot-context-check.json`。

**本轮运行状态（如实记录）**：在共享工作区尝试运行时，进程在自检早期被并发应用启动/清理打断（exit -1，无托管异常、无 JSON 落盘；同一私有构建的 `--self-test=startup` 可通过）。按 Lead 要求本轮结束前暂停应用启动与截图，**E13 的逐项点按与截图未完成取证**，列为 verify 阶段补跑事项；状态按口径最多「已实现、未验证」。

自检报告（待补）：`Windows/validation/screens/screenshot-context/screenshot-context-check.json`。

## 6. 未解决 / 待补

1. 无 Mac 的 `ScreenContextService` 同构实现：没有 frameID 去重/稳定等待/变化区域 delta/本机模型增强；读屏轮询只做 OCR + 哈希去重。提问时把 OCR 文本注入（`PromptAboutScreenshot`），**不把截图图像附给模型**（Mac questionImage 附 jpg）。
2. 无逐显示器选择/显示器名列表；区域选择覆盖虚拟屏幕（多显示器时整块）。
3. 窗口选择器没有缩略图/应用图标（Mac 也无缩略图，只有标题+owner，一致）；窗口标题为空时 Mac 显示「未命名窗口」，Windows 直接跳过空标题窗口。
4. 区域过小提示文案不同（Mac「选取范围太小，请拖动选择完整题目或内容。」；Windows 仅取消/4px 阈值）。
5. 无 Mac 对应截图；真实截图路径的部分交互（跨进程窗口被关闭、权限失败）需实机验证。状态最多「已实现、未验证」。
