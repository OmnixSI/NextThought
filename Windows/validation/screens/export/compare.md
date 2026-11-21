# 导出对话框（E21 · §6.7）Mac 3.4.0 源码 ↔ Windows SessionExportSheet 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`Sources/NextThought/SessionExportUI.swift`（SessionExportSheet）
- Windows 代码：`Windows/src/NextThought.Desktop/Views/SessionExportSheet.axaml(.cs)`、`AppState.SessionExport.cs`
- Windows 截图（本目录）：`export-light.png` / `export-dark.png`（`--self-test windows` 用真实归档会议复现 SRT 源文本预览，浅/深各一张）
- 证据生成：`--self-test windows`

| # | Mac 元素（顺序） | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 尺寸 | min 760×520、ideal 860×620，padding 24，spacing 16 | 同（860×620、Min 760×520、Margin 24） | 一致 |
| 2 | 标题块 | 「导出会议」section + 会议标题 caption 次要色 1 行；右上「关闭」ghost | 同 | 一致 |
| 3 | 选择器 | 格式/转写/语言 三个 MenuPicker，宽 180/180/160，label + 6 间距 | 同（`MenuPicker` 控件） | 一致 |
| 4 | 格式 | Markdown/DOCX/PDF/SRT/VTT | 同（5 项） | 一致 |
| 5 | 转写/语言 | 校正/原始；双语/仅原文/译文 | 同 | 一致 |
| 6 | 说明行 | info 图标 + caption：字幕格式显示源文本、文档格式显示分页预览 | 同（文案按 Windows 预览实现改写，语义一致） | 一致 |
| 7 | 归档错误块 | caution 三角形 +「归档有缺失或读取错误，导出已暂停。」+ 各问题；8% caution 底，圆角 8 | 同（`ReadIssuePanel`，单条问题汇总裁剪） | 一致 |
| 8 | 过期提示 | derived notes 过期 / 旧归档无来源版本，两套文案 + 图标 | 未显示：Windows 归档未跟踪 derived content status（见差异） | 不一致 |
| 9 | 字幕警告 | 估算结束时间 N 段、过期译文 N 条（caution、图标行） | 同（`EstimatedWarning`/`StaleTranslationWarning`） | 一致 |
| 10 | 预览 | 文档格式 → PDF 分页预览；字幕格式 → 源文本，11 等宽，sunken 底 + hairline，min260/max430 | 字幕格式同；文档格式显示源文本而非分页（无 PDF 视图控件，见差异） | 部分 |
| 11 | 底部 | 本地状态文字 + 「导出…」主按钮、默认动作、无内容/错误时禁用 | 同（回车触发、`IsEnabled` 联控） | 一致 |

## 差异 / 未解决

1. 文档格式（Markdown/DOCX/PDF）的**分页预览**未实现：Windows 无 PDF 渲染控件，预览区显示同一投影的源文本；导出文件本身与 Mac 一致（分页由 PdfWriter/DOCX 生成）。
2. Mac 的 derived-content 过期提示依赖 `projection.derivedContentStatus`；Windows 归档数据层暂无该状态，提示块保留但隐藏（`StaleNotice`）。
3. 截图取 SRT（源文本）以避开分页预览差异。
