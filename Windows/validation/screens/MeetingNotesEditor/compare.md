> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# E16 纪要编辑器与证据侧栏：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/MeetingNotesEditor.swift`（189 行全文：MeetingNotesEditor、MeetingEvidenceSidebar）
对照基准：Mac v3.3.0（`5ddb9e5`）；无 Mac 编辑器截图，按源码逐元素对照
Windows 代码：`Windows/src/NextThought.Desktop/Views/NotesEditorWindow.axaml(.cs)`、`AppState.NotesEditor.cs`、`Views/NotesView.axaml(.cs)`（入口与证据栏）

## 1. MeetingNotesEditor（编辑器）

| Mac | Windows | 结论 |
|---|---|---|
| 标题「编辑纪要」+ 「查看 AI 原稿」按钮切换 | 同：标题 + ToggleButton「查看 AI 原稿」，切换显示只读 AI 原稿（`NotesEditorOriginalText`） | 一致 |
| 说明「修改保存在独立的用户版本中。重新生成只更新 AI 原稿，时间引用仍然保留。」 | 同文案 | 一致 |
| 结构化纪要（有 sections）时逐节逐条 TextEditor 编辑 + 每条时间 +「我已核实」（不确定条目） | Windows 结构化纪要仍在纪要页以 Markdown 渲染、证据行可编辑（E15）；编辑器内仍是整篇 Markdown 文本框，**无逐条编辑与「我已核实」** | 未做（记未解决） |
| 无结构化纪要时整篇 Markdown TextEditor | 同（整篇 TextEditor，实时显示） | 一致 |
| 脏标记「更改尚未保存 / 没有未保存的更改」 | 已补：`NotesEditorDirty`/`NotesEditorDirtyText`（文本与个人笔记任一变化） | 一致 |
| 取消：脏时先确认「放弃尚未保存的更改？」 | 已补：取消按钮与关闭窗口（Closing 拦截）在脏时 ConfirmDialog 确认；未脏直接关闭 | 基本一致（按钮文案用 Windows 确认框「取消/确认」，Mac 为「继续编辑/放弃更改」） |
| 保存更改 + 忙碌态（保存中禁用、正在保存…） | 已补：`SaveNotesEditorCommand` 异步保存、`NotesEditorSaving` 禁用按钮（按钮文案未变「保存更改」） | 部分（无 busy 文案切换） |
| 保存写入 user-content：无结构化 notes → editedSummary.markdown + editedMarkdown=nil；有 editedNotes → editedMarkdown；basedOnGeneratedAt=AI 稿 generatedAt | 已补：无 EditedNotes 时写 `EditedSummary { Markdown }` 且 `EditedMarkdown=null`；有 EditedNotes 时写 `EditedMarkdown`；个人笔记一并保存；`BasedOnGeneratedAt` 取已有值或当前时间（Windows 摘要读不到 AI generatedAt） | 基本一致（basedOn 时间语义不同） |
| 保存失败显示错误「更改尚未保存：…」且不关窗 | 已补：`NotesEditorError` 显示、不关窗 | 一致 |
| 编辑器尺寸 720×640 | 900×640（右侧证据栏占 300） | 差异（因加入证据侧栏） |
| 只有「查看 AI 原稿」时禁止保存 | 已补：`CanSaveNotesEditor = !Saving && !ShowOriginal` | 一致 |

## 2. MeetingEvidenceSidebar（原话依据侧栏）

| Mac | Windows | 结论 |
|---|---|---|
| 标题「原话依据」+ 关闭按钮 | 编辑器右侧栏「原话依据」；纪要页证据栏（E15）常驻，均无单独关闭按钮（页内切换标签即可） | 部分 |
| 定位到的原话前后各 2 行、显示时间、当前行高亮 | 未做行上下文列表：Windows 侧栏显示证据引用清单（claim/支持徽标）；点击「在完整转写中查看」跳到纪要页转写标签并高亮该行 | 部分（缺 ±2 行上下文与当前行高亮） |
| 该原话相关的不确定纪要条目 +「待确认」+「我已核实」（写 editedNotes） | 未做（Windows 结构化条目编辑未实现，见上） | 未做 |
| 「在完整转写中查看」「标为重点 / 取消重点」（高亮保存） | 「在完整转写中查看」已接（关闭编辑器、跳转并高亮）；「标为重点」在转写页有「高亮」按钮（LiveView/纪要页高亮列表），侧栏内未直接提供 | 部分 |
| 无法定位时显示「此原话无法定位。已有笔记仍保留，请查看完整转写核实。」 | 未做 | 未做 |
| 侧栏 300 宽、左侧分隔线 | 300 宽卡片（无分隔线） | 视觉近似 |

## 3. 与 E15 的衔接

- 纪要页「编辑纪要」→ 打开本编辑器（`LoadNotesEditor` 预填用户版本、AI 原稿另存用于「查看 AI 原稿」）。
- 编辑保存后：`EditedSummary/EditedMarkdown` 写入 user-content.json，`HasEditedNotesForCurrentSession` 为真 → 纪要页「重新整理」先确认（E15）。
- 证据侧栏数据来自 `NotesEvidence`（SessionEvidenceBuilder，E15 已接）；点击引用跳转到转写标签并高亮。

## 4. 验证状态（本轮按 Lead 要求暂停应用启动/截图）

- 代码：编译通过与两套测试数字见提交报告；本轮**未重新启动应用、未截图**（Lead 要独占 verify）。
- 未截图项：编辑器浅/深色截图（Mac 也无对应截图，按源码对照）；「保存后重新打开核对」未实机点按 → 状态「已实现、未验证」。
- 已有行为测试覆盖：`SessionNotesEditorTests`（条目编辑 payload 修订）、`SessionEvidenceTestsParityTests`（摘要条目编辑与旧编辑失效）；本轮新增 dirty/save 语义没有独立单测（依赖窗口，属未验证）。

## 5. 未解决（task-78/§3.4 裁决后更新，2026-11-21）

1. 结构化纪要逐节逐条编辑 +「我已核实」：**已完成**（task-62 ① 的 `f030fce6`：effectiveNotes = editedNotes ?? summary.notes，编辑器逐 section/item TextBox + time caption +「我已核实」，保存写 editedNotes 并重渲染）。本条关闭。
2. 原话依据侧栏：**已完成**——`SessionEvidenceRow` 带 ±2 行转写上下文（当前行前缀 ▸）、`HasMissing`+Mac 原句「此原话无法定位。已有笔记仍保留，请查看完整转写核实。」；侧栏新增「标为重点」按钮（`MarkEvidenceHighlight_Click` → `HighlightLineCommand`）。条目关闭。
3. 取消确认按钮文案与 Mac：**已完成**——`ConfirmDialog` 支持自定义确认/取消文案，编辑器关闭/取消使用「放弃尚未保存的更改？」+「放弃更改 / 继续编辑」；保存中按钮文案改为「正在保存…」（Mac BusyActionLabel）。本条关闭。
4. `basedOnGeneratedAt`：**已完成**——载入时读 summary.json 的 `generatedAt`（AI 原稿时间），保存时 `content.BasedOnGeneratedAt ?? AI generatedAt ?? now`；结构化保存路径同样使用。本条关闭。
5. 截图：本页尚未有最终提交的浅/深截图；§5 统一重拍时补（当前共享树被 runtime-gaps/live-parity 在途文件打红，无法启动应用）。
