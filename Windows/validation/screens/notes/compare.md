# E15 会议页：Mac ↔ Windows 逐元素对照

对照代码：`Sources/NextThought/NotesPageView.swift`（1327 行）、`Sources/NextThought/SessionInfoView.swift`（202 行全文）、`Sources/NextThought/OutlineView.swift`（91 行全文）、`Sources/NextThought/NotesRegeneration.swift`（49 行全文）
对照基准：Mac v3.3.0（`5ddb9e5`）+ Mac 3.4.0 参考图（见文末 task-48 节）；Mac 3.4.0 有 [review-light](../../mac-screens/notes/review-light.png) / [review-dark](../../mac-screens/notes/review-dark.png)、[work-light](../../mac-screens/notes/work-light.png) / [work-dark](../../mac-screens/notes/work-dark.png)、[transcript-light](../../mac-screens/notes/transcript-light.png) / [transcript-dark](../../mac-screens/notes/transcript-dark.png)；旧 `docs/screenshots/notes.png` 仅留档
Windows 代码：`Windows/src/NextThought.Desktop/Views/NotesView.axaml(.cs)`、`Views/SessionInfoWindow.axaml(.cs)`、`Views/NotesEditorWindow.axaml`、`AppState.StructuredNotes.cs`（E15 段）、`NotesOutlineRows.cs`、`AppState.MeetingNotes.cs`
Windows 截图：`Windows/validation/screens/notes/`（自检 `notes-ui` 生成：notes-light / notes-outline-light / notes-transcript-light / notes-personal-light / notes-info-light）

## 1. 页面骨架（MeetingPage）

| Mac | Windows | 结论 |
|---|---|---|
| header（标题块：会议标题 + 模式/事实行） | 页头「会后纪要」+ 会议标题/归档事实行（在纪要 card 内） | 一致（位置上移） |
| NotesProgressLine：本会议正在整理时显示阶段/进度 | 纪要 card 顶部进度行（Summarizing 时显示「正在整理纪要…」+ Status + ProgressBar） | 已补 |
| 来源过期提示（stale/unknown） | 未单独提示（Markdown 导出内含提示）；纪要页未显示 → 未解决 | 未做 |
| 读取问题条 + 详情按钮（readIssues） | 未显示 → 未解决 | 未做 |
| 四标签 MeetingTab：纪要/复盘、转写、我的笔记、会中回答（无回答时不显示） | 四标签：纪要/转写/回答/个人笔记；回答标签按 HasSessionAnswers 隐藏 | 一致（标签名：Mac「我的笔记」/Windows「个人笔记」；复盘标题未按模板切换） |
| 证据栏常驻纪要页右侧（ViewThatFits 窄屏改为上下） | 纪要页右侧 300px 证据栏；窄屏未自适应 → 部分 | 已补 |
| 从纪要/提纲/证据跳到转写留在会议页内并高亮行 | 「跳到原话」→ NotesTabIndex=1、高亮最近转写行、提示「已跳到原话 · mm:ss」；滚动到该行 | 已补（精确行定位用行 id/偏移，近似最近行） |
| tab 内容滚动复位（切会议） | 未处理 → 未解决 | 未做 |
| 编辑纪要 sheet / 重新整理确认 / 新 AI 版本提示 | 编辑纪要打开 NotesEditorWindow（E16）；重新整理在改过纪要时先确认（Mac 原句）；「AI 新版本」行未做 → 未解决 | 部分 |

## 2. 纪要标签（NotesBody / NotesFormatBar / StructuredNotesPane）

| Mac | Windows | 结论 |
|---|---|---|
| 格式栏：文字纪要 / 鱼骨图 / 要点大纲 / 观点对照（推荐格式带点、未生成时点击即生成） | 格式栏四选项；未生成时提示+「生成这种纪要」按钮 | 一致（推荐格式标记未做） |
| 鱼骨图画成骨图、观点对照画成矩阵 | 既有 FishboneView / ViewpointMatrixView | 一致 |
| **要点大纲用 OutlineView 真视图**：编号章节、要点、子要点、每条时间可点击跳转，关键概念（term+meaning），待复习 | 已补真提纲视图：`NotesOutlineRows`（编号/标题/时间）、要点/子要点（前缀 •/–、时间）、关键概念表、待复习；时间按钮跳转 | 已补 |
| NotesSectionView：结构化纪要分章节条目 + ConfirmBadge/VerdictBadge + 条目编辑 sheet | Windows 结构化纪要仍以 Markdown 渲染（`StructuredMarkdown`）；证据栏可编辑证据行；条目级编辑 sheet 未做 → 未解决 | 部分 |
| 系列知识回看（KnowledgeReviewSection） | 原「回顾」标签内容并入纪要页（已确认经验/题库） | 已补 |
| 教练小结（CoachSummary） | 既有卡片 | 一致 |

## 3. 转写标签（CorrectedTranscript）

| Mac | Windows | 结论 |
|---|---|---|
| 校正/原始版本切换、查找、逐行撤销（E17） | 「校正原话」按钮打开 CorrectedTranscriptView（版本切换/查找/撤销在窗口内，E17 已实现并验证） | 部分（未直接嵌入 tab 内做版本切换） |
| 高亮行定位/滚动 | 跳转后行内「★ 高亮」+ ListBox 滚动到行；跳转提示行 | 已补 |
| 译文显示 | 既有 Translation 行 | 一致 |

## 4. 回答标签（SessionAnswersList）

| Mac | Windows | 结论 |
|---|---|---|
| 问题、回答、自动/手动、模型、时间、首 token、未完成片段、分答截图、编辑回答/评价徽标 | 回答列表仅问答+自动徽标+模型；其余在历史页归档概览（E18）有 | 部分（会议页回答列表未补时间/首字/徽标） |

## 5. 个人笔记标签（MeetingUserNotes）

| Mac | Windows | 结论 |
|---|---|---|
| 个人笔记编辑 + 高亮列表（引用原话、点击跳转） + 选中高亮时证据侧栏 | 个人笔记编辑保存 + 高亮列表（引用+跳到原话）；选中高亮联动证据栏未做 → 未解决 | 部分 |
| 笔记保存在 user-content.json，不覆盖生成纪要 | 既有（SessionUserContentStore） | 一致 |

## 6. 会议信息（SessionInfoView）

| Mac | Windows | 结论 |
|---|---|---|
| 标题 + 模式；分组：会议（开始/时长/发言人）、纪要（状态）、存储（位置/大小）、归档（写入状态、读写提醒、确认缺失/重试保存） | 新增 `SessionInfoWindow`：会议（开始/时长/发言人/模式）、纪要（状态）、存储（位置/大小，真实目录统计）、归档（已读取归档 + N 项待写入） | 已补；Mac 的 unqueued 丢失确认/重试保存未做（Windows 归档写入模型不同）→ 未解决 |
| 不可用状态（会议已删除/读取失败） | 未做空状态 → 未解决 | 未做 |

## 7. 重新整理（NotesRegeneration）

| Mac | Windows | 结论 |
|---|---|---|
| 有用户修订时先确认（标题「重新整理这份纪要？」+ Mac 正文） | 已补：`HasEditedNotesForCurrentSession` → ConfirmDialog（Mac 原句）后执行；无修订直接重新整理 | 一致 |
| 重新生成完成后「AI 新版本」行（查看新版/用这一版/继续用我改的） | 未做 → 未解决 | 未做 |
| 模板/语言的 regeneration 请求经 NotesRegeneration 统一确认 | 模板整理与纪要语言（E18）未走统一确认 → 部分 | 部分 |

## 8. 自检点按记录（`notes-ui`，隔离数据目录 + 真实归档会议 + 真实 summary.json 结构化提纲）

| 步骤 | 操作 | 结果 |
|---|---|---|
| 1 | 选中会议进入会议页 | 通过：四标签（索引 0）、证据 0 条（该摘要无结构化证据行） |
| 2 | 切「要点大纲」 | 通过：真实 OutlineView（章节 1、要点/子要点带时间、关键概念 1、待复习 1） |
| 3 | 点击带时间的条目跳转 | 通过：切到转写标签、高亮「我们决定先更新数据库再删缓存」、提示「已跳到原话 · 00:40」 |
| 4 | 切「个人笔记」，保存笔记并高亮一行 | 通过：高亮 1 条且可跳转 |
| 5 | 打开「详细信息」 | 通过：8 项分组（会议/纪要/存储/归档） |
| 6 | 模拟用户改过纪要后检查重生成确认条件 | 通过：无修订=False（初次）、有修订=True |
| 截图 | notes-light / notes-outline-light / notes-transcript-light / notes-personal-light / notes-info-light | 生成 |

自检报告：`Windows/validation/screens/notes/notes-ui-check.json`（6/6 通过、exit 0）。

## 9. 未解决 / 待补

1. 结构化纪要条目级编辑 sheet（NotesItemEditSheet）与 ConfirmBadge/VerdictBadge 未做：Windows 仍用 Markdown 渲染结构化纪要，证据行可编辑但无条目级编辑/确认/评价徽标。
2. Mac 的来源过期提示（stale/unknown）、读取问题条（readIssues+详情）、归档状态条（saving/blocked/unacked 确认）未在会议页显示。
3. 「AI 新版本」行：**已完成**——会议页标题区 `HasAiNewVersion` 条（Mac 原句「AI 整理出了新版本。现在显示的仍是你改过的版本。」）+「查看新版」（打开编辑器并切 AI 原稿）与「保留我的修订」（`basedOnGeneratedAt` 记到新 AI 时间后关闭提示）；数据比较用 summary.json generatedAt vs basedOnGeneratedAt。
4. 会议页回答列表缺时间/首 token/未完成/分答截图/评价徽标（这些在历史页归档概览有）。
5. 转写 tab 内嵌版本切换/查找/逐行撤销未做（E17 的 CorrectedTranscriptView 窗口已实现，可从按钮进入）；跳转滚动为近似最近行。
6. 个人笔记选中高亮时联动证据侧栏未做；标签名「个人笔记」与 Mac「我的笔记」用词不同。
7. 会议信息缺不可用空状态、unqueued 丢失确认/重试保存；无 Mac v3.3 会议页深色截图（v3 只有 notes.png）。

## Mac 3.4.0 参考图与逐图核对（task-48）

Mac 3.4.0 参考：[review-light](../../mac-screens/notes/review-light.png) / [review-dark](../../mac-screens/notes/review-dark.png)（回顾纪要）、
[work-light](../../mac-screens/notes/work-light.png) / [work-dark](../../mac-screens/notes/work-dark.png)（工作会议纪要）、
[transcript-light](../../mac-screens/notes/transcript-light.png) / [transcript-dark](../../mac-screens/notes/transcript-dark.png)（转写）。
Windows 最新批次：`validation/screens/current/notes-*.png`、`transcript-*.png`；本轮 `--demo --screenshots` 的 notes 标签页只截到 纪要/转写/回答/个人笔记 四个标签，**review（回顾）与 work（工作模板）未截**，已记入 mac-screens README 的「未覆盖」。

- 逐图核对：转写标签的说话人行、时间、译文/原文切换、问题徽章与 Mac transcript 图一致（`notes/current/transcript-light.png` 对照）；纪要标签的标题、决策/待办/风险分组沿用原表结论。
- 差异/缺口（未改，记录）：Mac 的「回顾纪要」与「工作会议纪要」两个模板页在 Windows 批次里没有对应截图（Windows 用 `review/work` 两个 Demo 模板页，标签页存在但截图循环未覆盖）；Mac 3.4 的标签顺序与 Windows 四标签（纪要/转写/回答/个人笔记）见 E15/E16 的原表，本轮 mac 图不改变判定。
- Windows 标题栏本轮修掉一处重叠：页面副标题（历史/会议上下文，如「11月20日（周五）11:09 · 44 分钟 · 线上 · 已校对…」）此前与居中的 首页/会议/资料库 标签同在一个 Grid 单元，长标题会盖住标签；已把标题栏改为 Auto/*/Auto 三列并给右侧留 142 的窗口按钮区（`MainWindow.axaml`）。刷新后的 `current/settings-general-light.png` 可见副标题已不与标签重叠。Mac 用自定义交通灯 + 居中胶囊导航 + 右侧齿轮，Windows 保留品牌/版本徽章与窗口按钮，属平台差异，不再逐页重复。

## task-62 / E15：阶段 4 §6.7 Mac 3.4 会后纪要页逐元素复核（2026-11-21）

- Mac 3.4 参考图：[review-light](../../mac-screens/notes/review-light.png) / [review-dark](../../mac-screens/notes/review-dark.png)、[work-light](../../mac-screens/notes/work-light.png) / [work-dark](../../mac-screens/notes/work-dark.png)、[transcript-light](../../mac-screens/notes/transcript-light.png) / [transcript-dark](../../mac-screens/notes/transcript-dark.png)。
- Windows 截图（本目录）：`notes-page-{light,dark}.png`（复盘标签首屏）、`notes-mine-{light,dark}.png`（我的笔记）、`notes-transcript-{light,dark}.png`（转写）、`notes-corrected-{light,dark}.png`（校对结果）。`validation/screens/current/` 的同名产物不入库。
- 本轮按 §6.3 重做页面骨架；结论只能「逐条一致」或逐条列差，下面逐条列出。

| # | Mac 3.4 元素（NotesPageView / 截图） | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 面包屑「所有会议 › 会议标题 › 复盘」 | `NotesBreadcrumb`（本轮新增，`所有会议 › 标题 › 复盘`，`›` 分隔）+ 页头第一行 | 逐条一致 |
| 2 | 右上「复制纪要」primary（复制笔记 Markdown） | `复制纪要` primary + `CopyNotesCommand`（新增，走 shell 的剪贴板 writer） | 逐条一致 |
| 3 | 「编辑纪要」「…」等次级动作 | 编辑纪要/重新整理/导出…/详细信息（+ 默认按钮「整理这场会议」） | 逐条一致（Mac 的「…」更多菜单合并为平铺按钮；记录） |
| 4 | 衬线大标题 + 事实行：日期（本地化长格式）· 时长 · 线上/线下 · 已校对 N 处 | `NotesMeetingTitle`（Serif 30）+ `NotesFactsLine`（`yyyy-MM-dd HH:mm · N min · 线上/线下`）；**未带“已校对 N 处”**，日期是数字格式 | 不一致：事实行缺校对计数、日期格式未本地化（记录） |
| 5 | 旧内容提示「部分旧内容未记录来源版本…」 | 未显示 | 不一致：未做（compare 旧节已记） |
| 6 | 模板下拉 +「模板：…」 | 模板 ComboBox + 按模板整理 + 校对转写 + 结束后自动整理 | 逐条一致（放在第二行，Mac 在面包屑行；记录） |
| 7 | 格式分段「复盘 / 提纲 / 鱼骨图 / 观点对照」（白色块选中） | 同名同序 chip（本轮改名/换序：复盘/提纲/鱼骨图/观点对照） | 逐条一致 |
| 8 | 标签「复盘 / 转写 / 我的笔记 / 会中回答」（会中回答无内容时隐藏） | 同名同序四标签（本轮改名/换序；会中回答按 `HasSessionAnswers` 隐藏） | 逐条一致 |
| 9 | 「一句话印象」AI 卡片（一句话 + 复制按钮） | 卡片保留（本轮改标题 + AI 图标，绑定 `CoachSummaryText`）；演示数据没有教练摘要时整卡隐藏，截图里看不到 | 部分一致：标题/图标一致；演示数据未补，Mac 截图的示例句没有对应 demo 数据（记录） |
| 10 | 「逐题回放」：题号 + 问题 + [时间] + 答得好/待改进 chip + 我答 + 更好的答法卡 | 未实现；当前以 Markdown「问题清单」呈现问题/我的回答要点 | 不一致：逐题回放/反馈 chip 未做（最大剩余差距，记录） |
| 11 | 复盘内容按模板段落渲染（标题/摘要/一句话印象/问题清单等） | Markdown 纪要卡（模板生成）+ 右侧「证据」常驻卡 | 部分不一致：Mac 是结构化段落 + 复盘中不常驻证据；Windows 证据卡常驻（E18 设计），记录 |
| 12 | 顶部标题区无「会后纪要」大页头 | 已移除旧「会后纪要」页头/说明，改成面包屑+标题+事实行 | 逐条一致 |

### 动作记录（2026-11-21，--demo + 隔离数据目录）

1. 打开会议复盘：面包屑/标题/事实行/复制纪要/编辑纪要/重新整理/导出/详细信息/整理这场会议均可见；格式 chip 复盘选中。
2. 切换格式：复盘/提纲/鱼骨图/观点对照 依次切换内容（复用原有 NotesFormat 逻辑）。
3. 标签：复盘/转写/我的笔记/会中回答（无回答时隐藏）；我的笔记为空/有内容时的 MeetingUserNotes 与之前一致。
4. 复制纪要：有 Clipboard writer 时复制 Markdown；shell 未注入时静默返回（自检环境）。
5. 校对结果：`notes-corrected-{light,dark}.png` 为 E17 校对结果视图当前状态（逐元素见 CorrectedTranscript/compare.md，task-62 后续补）。

### 未解决（task-62 后续步骤）

- 逐题回放与答得好/待改进 chip、旧内容提示、事实行校对计数/本地化日期；
- 一句话印象的演示数据（Mac demo 有示例句）；
- 「…」更多菜单合并、模板下拉在面包屑行；
- 复盘结构化段落 vs Markdown 卡、证据卡常驻；
- work（工作会议纪要）演示模板截图的 Mac 对照（Windows 用 `--demo` 默认模板，未单独拍 work 变体）。

## task-62 / E16：转写右键菜单（改说话人 / 改这一句）——逐元素对照（2026-11-21）

- Mac 参照：`Sources/NextThought/SpeakerID.swift`（assignLine/setVoiceName/setMeVoice/mergeVoices）、`Sources/NextThought/NotesPageView.swift` 的 LineSpeakerMenu 与 editLine；数据层由 core-parity J6 落成 `Windows/src/NextThought.Core/SessionLineEdits.cs`（`SessionSpeakerEdits` / `SessionTextEdits`）。
- Windows：`Views/NotesView.axaml` 转写行右键菜单、`Views/NotesView.axaml.cs` 的 Opening/Click、`AppState.TranscriptEdits.cs`；测试 `tests/NextThought.Tests/TranscriptEditsTests.cs`（4 项）。
- 截图：转写标签证据图沿用 `notes-transcript-{light,dark}.png`（菜单为弹出层，未截弹出态，记录）。

| # | Mac 元素（LineSpeakerMenu / editLine） | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 右键行菜单「这句是谁说的」→ 我 / 新说话人 / 每个识别到的人 | 同名菜单：固定「我」「新说话人」+ `SpeakerPeople` 里每个非我的人（`FromVoice`），Opening 时动态生成 | 逐条一致 |
| 2 | assignLine：句子改到目标声音，只追加 voices.jsonl 标签，meta.json 写 voiceNames/meVoices | `AssignTranscriptLineSpeakerAsync` → `SessionSpeakerEdits.AssignLine` → `AppendVoiceTagAsync` + `SetVoiceLabelAsync`；`TranscriptEditsTests` 断言 **transcript.jsonl 字节哈希不变**、meta/行 voice 更新 | 逐条一致 |
| 3 | 标成「我」：复用/新建用户声音并写 meVoices | `LineSpeaker.Me` 分支同 Mac；测试断言 meVoices 含目标声音、字节不变 | 逐条一致 |
| 4 | 改这一句（editLine）：用户改字写入 user-content 的 correctionDecisions，原转写不动 | 右键「改这一句…」→ `EditTextDialog` → `SessionTextEdits.EditLine` → `SessionUserContentStore.Save`；重新按 `EffectiveLines` 装载转写；测试断言字节不变、EffectiveLines 生效/撤销恢复 | 逐条一致 |
| 5 | 校对结果里逐条接受/撤销（E17 窗口） | 本轮只接数据层 `SessionTextEdits.ToggleCorrection` 往返测试；E17 窗口 UI 在 E17 步逐元素对照 | 部分：数据层已覆盖，UI 归 E17 |
| 6 | 菜单其余项：加入术语表… | 保留原有「加入术语表…」 | 逐条一致 |

### 未解决/后续

- 右键菜单弹出态截图未拍（需要真实右键操作；E12 运行环境修复后补）；
- 说话人条/实时页的 assignLine 由 live-parity J6 live 接线（`c7aa185f`），本页只做会后转写标签；
- E17 校对结果窗口的逐条接受/撤销 UI、E18 知识复核待下一步。

## task-62 / ① 结构化纪要逐条编辑 +「我已核实」（2026-11-21）

- Mac 参照：`Sources/NextThought/MeetingNotesEditor.swift`（`ForEach(sections) → ForEach(items)`：每项 `TextEditor`、`time` Label、「我已核实」按钮；`save()` 写 `content.editedNotes`）；数据源 `effectiveNotes = userContent.editedNotes ?? summary.notes`。
- Windows 实现：
  - **持久化**：`summary.json` 新增 Mac 的 `notes` 文档（`SessionSummary.Notes`，模板纪要生成时写入）；`user-content.json` 的 `editedNotes` 恢复为 Mac 语义 `StructuredNotesDoc`，旧的 Windows structured payload 迁到 `editedStructuredViews`（Load 时自动搬移，兼容旧文件）。
  - **编辑器**：`Views/NotesEditorWindow.axaml` 在存在模板文档时显示 sections/items 列表——每 items 一条 TextBox、time caption、未确认项右侧「我已核实」按钮；无文档时保留原 Markdown 文本编辑器。「查看 AI 原稿」、原话依据侧栏、取消/放弃确认、脏状态提示均保留。保存调 `StructuredNotesEditing.WithEditedNotes` 写 `editedNotes`，并按 `NotesMarkdown.Render` 重渲染页面 Markdown；生成结果不被覆盖。
- 逐元素结论（对照 MeetingNotesEditor.swift）：
  | # | Mac | Windows | 结论 |
  |---|---|---|---|
  | 1 | `effectiveNotes = editedNotes ?? summary.notes` | `EffectiveTemplateNotes()` / `LoadStructuredNotesEditor` 同逻辑 | 逐条一致 |
  | 2 | 每 section 标题 + 每 item TextEditor | `NotesEditorSections` → `NotesEditorSectionRow.Title` + `NotesEditorItemRow.Text` TwoWay TextBox | 逐条一致 |
  | 3 | item.time Label | `Time`/`HasTime` caption | 逐条一致 |
  | 4 | uncertain 项「我已核实」清除标记 | `Uncertain` + `ConfirmNoteItem_Click` → `ConfirmNotesEditorItem` | 逐条一致 |
  | 5 | 保存写 `content.editedNotes`，`basedOnGeneratedAt` 保已有值 | `StructuredNotesEditing.WithEditedNotes` + `BasedOnGeneratedAt` | 逐条一致 |
  | 6 | 无 notes 时整篇 Markdown TextEditor | `NotesEditorPlainText`（`!NotesEditorStructured && !ShowOriginal`） | 逐条一致 |
  | 7 | 查看 AI 原稿 / 原话依据 / 脏状态 / 取消确认 | 原实现保留（`NotesEditorShowOriginal`、`NotesEvidence`、`NotesEditorDirty`、`ConfirmDialog`） | 逐条一致 |
- 截图（本目录，1120 窗口位图，`--self-test=notes-ui` 用合成 `StructuredNotesDoc` 捕获）：`notes-editor-light.png` / `notes-editor-dark.png`；`notes-ui-check.json`（自检 passed）。
- 测试：`Windows/tests/NextThought.Core.Tests/StructuredNotesEditingTests.cs`（4 项：编辑保其它项并重渲染、我已核实只清该项、doc/user-content 往返、旧 payload 迁移）、`Windows/tests/NextThought.Tests/StructuredNotesEditorTests.cs`（2 项：窗口 sections/items/time/我已核实 接线、加载/保存路径）；`--self-test=notes-ui` passed。
- 未解决/记录：Mac 的 evidence sidebar 在本页为只读原话依据（与之前一致）；结构化编辑器只覆盖模板纪要（Fishbone/Outline/Viewpoints 的只读展示由 island-parity §6.7 提供，编辑仍走原 structured payload 路径）；「查看 AI 原稿」显示 Markdown 而不是 Mac 的 `NotesMarkdown.render` 只读块（渲染内容等价）。
