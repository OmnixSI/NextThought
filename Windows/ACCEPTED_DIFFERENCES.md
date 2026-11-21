# 已接受的差别（ACCEPTED_DIFFERENCES）

本文件按《收尾决定》（`WINDOWS_FINAL_DECISIONS.md`）第 1 节建立：长期成立的平台差异，不是缺陷。每条格式：**页面 — 差别 — 原因**。

## 全局（所有页面适用）

1. **所有页面 — 圆角曲线的数学形状、阴影渲染方式、玻璃材质** — Windows（Avalonia/Skia）与 Mac（SwiftUI/Core Animation）的绘制管线不同，视觉近似但数学形状不同。
2. **所有页面 — 字体本身的字形与行高度量** — 随包字体在 Windows 与 Mac 上的字体栈/度量不同，行高与字宽有几像素偏移。
3. **所有页面 — 修饰键不同、系统按钮在右上角、没有红绿灯** — 任务书 §2.8 已列的平台差异。
4. **所有页面 — 英文界面为不截断而多换一行** — 英文文案宽度与 Mac 字体度量不同，换行位置可不同。
5. **所有页面 — 密钥存在 Windows 当前用户的加密文件里而不是钥匙串** — 平台凭据存储差异。
6. **对照方法 — 无 Mac 截图、按 Swift 源码对照的条目** — 不是差别，对照方法已写明。
7. **Mac 参考截图 — 现有 93 张 3.4.0 参考图够用，不再等补拍** — 决定文件 §4。
8. **A15 非纯数据夹具 — CryptoKit HKDF/AES-GCM、SQLite DocumentJob、Darwin ProcessLineReader、CoreGraphics ScrollFollow、Sherpa Zipformer 五类按 README 跳过** — Windows 无对应实现；加密部分已有 `CompanionCryptoTests` 固定向量覆盖。
9. **更新通道 — 只有正式版本一个通道** — 任务书 §2.8 允许；`SETTINGS_PARITY.md` 的 12 个内部键同属本版不做。
10. **全局术语 — Mac 的「资料夹」在 Windows 统一改称「系列」** — J4 任务书决定（界面里搜不到「资料夹」），与 Mac 3.4.0 个别原句不同；`HomeStage4Tests` 断言此命名，`BackupRestoreReview` 等句已按 J4 保留「系列」。
10. **提示音与系统通知 — 用 Windows 通知/声音代替 Mac 的 NSUserNotification 等** — 平台机制差异（任务书 §2.8 的 Mac 独有行为）。

## 按页面

| 页面 | 差别 | 原因 |
|---|---|---|
| home | 场景胶囊的滑动用画布动画实现（不是 matchedGeometry） | Avalonia 无 matchedGeometry，动画观感近似 |
| shell | 专注模式保留系统标题栏按钮 | 平台窗口管理差异 |
| zoom | zoom 3（缩放步进外的个别内容近似） | 文字缩放层级在 Avalonia 与 SwiftUI 的继承机制不同 |
| island | 1/2/3/4/10/11：动画曲线、阴影/玻璃材质、部分悬停过渡的近似 | 绘制管线与动画系统差异 |
| teleprompter | 2、3：阴影/玻璃与滚动条样式近似 | 绘制管线差异 |
| live-window-menu | 3：弹出层背景玻璃材质近似 | 绘制管线差异 |
| live | 16 里的说话人编辑用独立窗口而不是气泡 | Avalonia 无 SwiftUI popover 等价控件；窗口形式达到同一目的 |
| translation | 1、4：译文行内阴影与字体度量近似 | 绘制管线与字体度量差异 |
| screenshot-question / screenshot-context | 取消按钮在底部（Mac 在工具栏）；区域页脚多一句「系统 OCR 读取文字」说明 | 窗口框架差异；说明句已记入 `WORDING.md` |
| web-search | 4：联网状态图标用 Windows 风格图形近似 Mac SF Symbol | 图标资源差异 |
| coach | 3：Vision vs MediaPipe 的 yaw/pitch 语义数值略有不同 | 模型实现差异，阈值按 MediaPipe 校准 |
| export / SessionExportSheet | 中文没有斜体字形（用 Windows 字体回退） | 随包中文字体无 italic 字形 |
| knowledge-review | 4 里没有内嵌原件预览，改为用系统默认程序打开原件 | Avalonia 无 QuickLook；用系统默认程序达到同一目的 |
| typography / speakers / all | 字体字形的近似 | 同全局第 2 条 |
