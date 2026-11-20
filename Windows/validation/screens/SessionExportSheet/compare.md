# E19 导出对话框：Mac ↔ Windows 逐元素对照

对照代码：`Sources/NextThought/SessionExportUI.swift`（296 行全文）、`Sources/NextThought/SessionDocumentRenderer.swift`（787 行）、Core `SessionExport.swift`（Windows 已移植为 `SessionExport.cs`）
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无导出对话框截图（`docs/screenshots/` 无 export 图），按源码逐元素对照
Windows 代码：`Windows/src/NextThought.Desktop/Views/SessionExportSheet.axaml(.cs)`（新增）、`AppState.SessionExport.cs`（新增）、`NextThought.Core/SessionExportContent.cs`（新增）、`NextThought.Platform/PdfWriter.cs`（对照 SessionDocumentRenderer 调整）、`Views/NotesView.axaml` / `Views/HistoryView.axaml.cs`（接线）
Windows 截图：`Windows/validation/screens/SessionExportSheet/`（自检 `session-export` 生成：session-export-sheet-light.png + session-export-check.json；exports/ 下为 30 个真实导出样本，未提交）

## 1. 对话框元素（SessionExportSheet）

| Mac | Windows | 结论 |
|---|---|---|
| 标题「导出会议」+ 会议标题 + 关闭按钮 | 同（`SessionExportSheet.axaml`：导出会议 / SessionExportTitle / 关闭） | 一致 |
| 格式选择（SessionExportFormat.allCases，MenuPicker） | ComboBox，选项 Markdown / Word 文档 / PDF / SRT 字幕 / WebVTT 字幕 | 一致 |
| 转写选择（当前校正版本 / 原始记录） | 同（ComboBox） | 一致 |
| 语言选择（原文与译文 / 仅原文 / 译文优先） | 同（ComboBox） | 一致 |
| 说明行：文档格式「页面预览与导出使用同一版校正、译文和纪要内容。」；字幕「预览使用同一版转写投影；字幕时间来自语句起止时间，缺少结束时间时会标明估算。」 | 同文案，按格式切换显示（`HintText`） | 一致 |
| 归档缺失/读取错误提示 + 暂停导出（readIssues） | `SessionExportReadIssue` 显示在警告行；有读取问题时仍允许导出（Mac 禁用导出按钮） | 部分 |
| 派生内容过期提示（stale / unknown 纪要） | 由 `SessionMarkdownExporter` 在 Markdown 内输出「提示：…」引用块；对话框内不单独提示 | 部分 |
| 字幕估算/过期计数警告 | 已接：`SessionExportSubtitleWarnings` 显示「N 段结束时间为估算值」「N 条译文可能已过期」 | 一致 |
| 分页预览（PDFKit，真实按页渲染） | 文本预览（TextBox，等宽字体，显示与文件相同的投影文本；Word/PDF 不在对话框内渲染页面） | **差异**：分页预览未做（记未解决） |
| 字幕预览（等宽 sourceText） | 同（等宽预览） | 一致 |
| 「导出…」主按钮 + 保存面板（NSSavePanel，自动补扩展名、不截断带点文件名） | 「导出…」+ Avalonia SaveFilePicker（SuggestedFileName 由 `SessionExportContent.SuggestedFileName` 生成：标题中 `/ : \` 换 `-`，默认扩展名按格式） | 一致 |
| 状态行「已导出：文件名」/ 错误到 state.errorMessage | `SessionExportStatus` 状态行 + 顶栏状态 | 一致 |
| 导出使用同一 projection（SessionExportPreviewContent.make + userContentMarkdown 追加） | `SessionExportContent.Build` 同一 projection；子文档=SessionMarkdownExporter，字幕=SessionSubtitleExporter.ExportProjected；用户内容 Markdown 追加参数已留（当前传空） | 一致（用户手记 Markdown 追加未接，记未解决） |

## 2. 文件渲染（SessionDocumentRenderer ↔ DocxWriter / PdfWriter）

| 项目 | Mac | Windows | 结论 |
|---|---|---|---|
| A4 页面 | 595×842 pt | 595×842 pt | 一致 |
| 页边距 | `x:44, y:44, width:507, height:754` | 44 pt（本次从 56 改为 44），内容宽 507 | 一致 |
| 页脚 | 无 | 已移除页码（原实现底部分页数字） | 一致 |
| 正文字号 | 10.5 pt，行距 +2 | 10.5 pt，行高 size+2 | 一致 |
| 标题字号 | #18 / ##15 / ###12.5 / ####11.5 / h5 11 / h6 10.5，粗体，段前 8/段后 4 | #18 / ##15 / ###12.5 / ####11.5 粗体，段前 8/段后 4 | 一致（h5/h6 未单列，落到 10.5 粗体） |
| 引用 | 斜体 + 左缩进 16 | 左缩进 16（CJK 字体无斜体字形，斜体未实现） | 部分 |
| 列表 | 项目符号 ●/○/□ 或有序编号，每级缩进 14 | 「• 」+ 缩进 14/28（多级按前两档） | 部分 |
| 表格 | 按列宽（CJK 显示宽度）绘制 ┌─┬─┐ 网格，最大行宽 84 显示列；超宽表退化为「列名: 值」行 | 本次实现同样规则：显示宽度计算、最大 84 列、超宽退化为「列名：值」，网格用 ┌─┐/│ 绘制 | 一致（比例字体下网格对齐不如 Mac 等宽字体，记差异） |
| 代码块 | 9 pt 等宽，缩进 | 9 pt + 8 pt 缩进（字体未用等宽 CJK，记差异） | 部分 |
| 行内格式 | 粗体/斜体/行内代码/链接（链接显示「文字 (URL)」） | 去掉 `**`/`` ` `` 标记、链接只保留文字（不显示 URL） | 部分 |
| Word 文档结构 | `[Content_Types].xml`、styles.xml（标题/正文样式）、numbering.xml（多级列表）、relations、document.xml；原子 rename 写入 | `DocxWriter` 生成同结构 OOXML（既有实现，未改动）；本次未逐样式对照 styles.xml | 部分（未逐项对照 Word 样式） |
| 写入原子性 | 临时文件 + rename | DocxWriter/PdfWriter 直接写目标文件（失败可能留半成品） | 部分 |

## 3. 导出入口

| Mac | Windows | 结论 |
|---|---|---|
| MeetingActionsMenu「导出 Markdown…」→ `state.exportSession` → 对话框 | 历史行右键「导出 Markdown…」→ 选中该会议并打开导出对话框 | 一致 |
| Notes 页无独立导出按钮（导出入口在菜单） | Notes 页原 5 个直接导出按钮（Markdown/Word/PDF/SRT/VTT）改为一个「导出…」按钮 → 打开对话框（`OpenSessionExportCommand`） | 一致（视图接线） |
| 导出前 flush 未落盘内容 | `SaveSessionExportAsync` 先 `await Task.WhenAll(writes)` 再读归档 | 一致 |
| 导出内容包含用户手记 Markdown（detail.userContentMarkdown） | 预留参数，当前未追加手记 | 未做（记未解决） |

## 4. 演示数据点按记录（自检 `session-export`）

隔离数据目录、真实归档会议（含带译文转写、回答、纪要），真实写文件：

| 步骤 | 操作 | 结果 |
|---|---|---|
| 1 | 选中会议并打开导出对话框 | 通过（标题正确） |
| 2 | 5 格式 × 2 转写版本 × 3 语言 = 30 个组合逐一下载 | **通过 30/30**（文件写入 exports/，校验见下） |
| 3 | 原始记录 + 仅原文不含译文；校正 + 双语含译文 | 通过（原始记录无译文=True；双语文有译文=True） |
| 4 | 对话框截图（预览为同一 projection） | session-export-sheet-light.png |
| 5 | PDF 文件头 %PDF、Word 文件头 PK、SRT 含时间轴、VTT 以 WEBVTT 开头、Markdown 以 # 开头 | 通过（30 个文件全部满足；PDF 约 5 MB，内嵌 SimHei 字体） |

## 5. 未解决 / 待补

1. 对话框内**分页预览未做**（Mac 用 PDFKit 渲染真实页；Windows 显示源文本预览）。Word/PDF 仍可在导出后用系统程序查看。
2. PDF 引用斜体、代码等宽字体、行内链接 URL 显示与 Mac 有差异（CJK 字体无斜体/等宽字形，链接只保留文字）。
3. DocxWriter 的 styles.xml/编号样式未与 Mac `stylesXML`/`numberingXML` 逐项对照（结构相同）。
4. 导出包含用户手记 Markdown（Mac detail.userContentMarkdown）未接；Windows 侧字段已留。
5. 归档读取问题（readIssues）时 Mac 禁用导出，Windows 仅提示仍可导出；派生内容过期只在 Markdown 内提示，对话框不单独提示。
6. 无 Mac v3.3 导出对话框实机截图，按源码对照；建议 Mac 补 SessionExportSheet 浅/深色截图（交 OPEN_QUESTIONS）。
7. 未实机点击（无 Mac 真机对照）；Windows 自检在隔离数据目录真实导出文件并校验文件头，状态最多「已实现、未验证」。
