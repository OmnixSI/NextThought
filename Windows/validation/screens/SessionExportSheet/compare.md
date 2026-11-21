> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



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
| 归档缺失/读取错误提示 + 暂停导出（readIssues） | `SessionExportReadIssue` 显示在警告行，`ExportButton.IsEnabled = readIssue.Length == 0 && 有内容`（有读取问题时禁用导出，与 Mac 一致） | 一致 |
| 派生内容过期提示（stale / unknown 纪要） | 用归档 `summary.json` 的 `transcriptSourceRevision`/`structuredNotesSourceRevision` 与当前 projection 复算 `SessionDerivedContentStatus`；对话框按 Mac 显示 stale=caution 三角「纪要或手记可能已过期；导出预览已附上核对提示。」/ unknown=问号「旧纪要未记录来源版本；导出预览会说明无法确认是否过期。」 | 一致 |
| 字幕估算/过期计数警告 | 已接：`SessionExportSubtitleWarnings` 显示「N 段结束时间为估算值」「N 条译文可能已过期」 | 一致 |
| 分页预览（PDFKit，真实按页渲染） | 文本预览（TextBox，等宽字体，显示与文件相同的投影文本；Word/PDF 不在对话框内渲染页面） | **差异**：分页预览未做（记未解决） |
| 字幕预览（等宽 sourceText） | 同（等宽预览） | 一致 |
| 「导出…」主按钮 + 保存面板（NSSavePanel，自动补扩展名、不截断带点文件名） | 「导出…」+ Avalonia SaveFilePicker（SuggestedFileName 由 `SessionExportContent.SuggestedFileName` 生成：标题中 `/ : \` 换 `-`，默认扩展名按格式） | 一致 |
| 状态行「已导出：文件名」/ 错误到 state.errorMessage | `SessionExportStatus` 状态行 + 顶栏状态 | 一致 |
| 导出使用同一 projection（SessionExportPreviewContent.make + userContentMarkdown 追加） | `SessionExportContent.Build` 同一 projection（预览与文件同源）；用户内容按 Mac `SessionArchive.userContentMarkdown` 追加：`## 我的笔记` + personalNotes、`## 重点标记` + `- [mm:ss] quote`（`SessionExportContent.UserContentMarkdown`） | 一致 |

## 2. 文件渲染（SessionDocumentRenderer ↔ DocxWriter / PdfWriter）

| 项目 | Mac | Windows | 结论 |
|---|---|---|---|
| A4 页面 | 595×842 pt | 595×842 pt | 一致 |
| 页边距 | `x:44, y:44, width:507, height:754` | 44 pt（本次从 56 改为 44），内容宽 507 | 一致 |
| 页脚 | 无 | 已移除页码（原实现底部分页数字） | 一致 |
| 正文字号 | 10.5 pt，行距 +2 | 10.5 pt，行高 size+2 | 一致 |
| 标题字号 | #18 / ##15 / ###12.5 / ####11.5 / h5 11 / h6 10.5，粗体，段前 8/段后 4 | #18 / ##15 / ###12.5 / ####11.5 粗体，段前 8/段后 4 | 一致（h5/h6 已按 Mac styles.xml Heading5=11/Heading6=10.5 写入） |
| 引用 | 斜体 + 左缩进 16 | 左缩进 16 + Skia 斜体倾斜；CJK 字体无真斜体字形（平台差异） | 一致（记录字体平台差异） |
| 列表 | 项目符号 ●/○/□ 或有序编号，每级缩进 14 | 与 Mac 同：`numbering.xml` abstractNum 0（bullet •/◦/▪，Symbol 字体）/1（decimal `%N.`），9 级缩进 720×(level+1)、悬挂 360；PDF 用 • + 14/28 缩进 | 一致 |
| 表格 | 按列宽（CJK 显示宽度）绘制 ┌─┬─┐ 网格，最大行宽 84 显示列；超宽表退化为「列名: 值」行 | 同规则：显示宽度计算、最大 84 列、超宽退化为「列名：值」 | 一致（比例字体下网格对齐不如 Mac 等宽字体，记差异） |
| 代码块 | 9 pt 等宽，缩进 | 9 pt 系统等宽字体（Cascadia Mono/Consolas/Courier New 回退）+ 8 pt 缩进 | 一致（Windows 无 CJK 等宽字形，平台差异） |
| 行内格式 | 粗体/斜体/行内代码/链接（链接显示「文字 (URL)」） | DocxWriter 行内 run：粗体 `<w:b/>`、斜体 `<w:i/>`、删除线、行内代码 Consolas + F3F4F6 底、链接「文字 (URL)」；PdfWriter 同样按 run 绘制（链接显示地址、行内代码等宽、引用/斜体 SkewX） | 一致 |
| Word 文档结构 | `[Content_Types].xml`、styles.xml（标题/正文样式）、numbering.xml（多级列表）、relations、document.xml；原子 rename 写入 | `DocxWriter` 按 Mac `stylesXML`/`numberingXML` 移植：styles 含 Heading1–6/TableGrid、numbering 含多级 bullet/decimal、页边距 1134 twip、表格边框/表头底纹；临时文件+rename 写入 | 一致（字体替换 Menlo→Consolas、PingFang SC→Microsoft YaHei 为平台差异） |
| 写入原子性 | 临时文件 + rename | DocxWriter 与 PdfWriter 均先写临时文件再 `File.Move(overwrite)`，失败不留半成品 | 一致 |

## 3. 导出入口

| Mac | Windows | 结论 |
|---|---|---|
| MeetingActionsMenu「导出 Markdown…」→ `state.exportSession` → 对话框 | 历史行右键「导出 Markdown…」→ 选中该会议并打开导出对话框 | 一致 |
| Notes 页无独立导出按钮（导出入口在菜单） | Notes 页原 5 个直接导出按钮（Markdown/Word/PDF/SRT/VTT）改为一个「导出…」按钮 → 打开对话框（`OpenSessionExportCommand`） | 一致（视图接线） |
| 导出前 flush 未落盘内容 | `SaveSessionExportAsync` 先 `await Task.WhenAll(writes)` 再读归档 | 一致 |
| 导出内容包含用户手记 Markdown（detail.userContentMarkdown） | `SessionExportContent.UserContentMarkdown` 从 user-content.json 追加「我的笔记」「重点标记」；预览与文件同源 | 一致 |

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

1. 对话框内**分页预览未做**（Mac 用 PDFKit 渲染真实页；Windows 显示源文本预览）。按 decisions §3.4 归第二批（`Windows.Data.Pdf` 渲染），导出文件本身不变。
2. ~~PDF 引用斜体/代码等宽/行内链接 URL~~：本轮已修（行内 run 绘制、链接显示「文字 (URL)」、代码块与行内代码用系统等宽字体、引用 SkewX 斜体）；CJK 字体真斜体/等宽字形属平台差异。
3. ~~DocxWriter styles.xml/编号未对照~~：本轮按 Mac `stylesXML`/`numberingXML` 逐项移植（Heading1–6、TableGrid、多级 bullet/decimal、1134 twip 页边距、表格边框/底纹）；字体替换为平台可用字体（Menlo→Consolas、PingFang SC→Microsoft YaHei）。
4. ~~导出包含用户手记 Markdown~~：已接（我的笔记 + 重点标记）。
5. ~~归档读取问题禁用导出/派生内容过期提示~~：读取问题已禁用导出（`ExportButton.IsEnabled` 联控）；派生态用 summary.json 源修订复算 `DerivedContentStatus` 并按 Mac 文案在对话框单独提示（stale=caution、unknown=问号）。
6. 无 Mac v3.3 导出对话框实机截图，按源码对照；建议 Mac 补 SessionExportSheet 浅/深色截图（交 OPEN_QUESTIONS）。
7. 自检 `session-export` 本轮在隔离数据目录真实导出 30/30 并通过（`session-export-check.json`）；仍无 Mac 真机对照，状态记「已实现、未验证」。

## 6. E19 收尾证据（2026-11-21，decisions §3.4）

- 提交：`E19:`（用户笔记 + Word/PDF 渲染按 Mac；原子写；派生内容提示/读取问题禁用）。
- 自检：`--self-test=session-export` passed（30/30 格式组合写盘；JSON 与对话框截图已更新到本目录）。
- 新增测试：Core `SessionExportUserContentTests`（我的笔记/重点标记/过期文案）、`DocxWriterStructureTests`（styles/numbering/页边距/行内 code/link URL）；NextThought.Tests `ExportParityTests`（用户笔记接线、派生态提示、PDF 链接与 code 不抛错）。
