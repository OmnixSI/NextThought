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
3. 「AI 新版本」行（新草稿提示 + 查看新版/用这一版/继续用我改的）未做。
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
