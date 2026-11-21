# E21 知识复核收尾：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/KnowledgeReviewSection.swift`（287 行全文：KnowledgeReviewSection / SeriesChangeCandidateCard / KnowledgeCandidateCard）、`Sources/NextThought/ProjectLibrarySupport.swift`（103 行全文：WorkspaceRecordsTab / MaterialReviewSheet）
对照基准：Mac v3.3.0（`5ddb9e5`）；macOS 截图无对应视图，按源码逐元素对照
Windows 代码：`Views/SeriesView.axaml(.cs)`、`Views/SeriesEditorWindow.axaml(.cs)`（记录标签）、`Views/LibraryView.axaml`、`Views/MaterialReviewWindow.axaml`、`AppState.Series.cs`、`AppState.MaterialReview.cs`、`SeriesChangeRow.cs`、`RecentMeetingRow.cs`（KnowledgeCandidateRow）、`NextThought.Core/MaterialReviewNotes.cs`（新增）
Windows 截图：`Windows/validation/screens/knowledge-review/`（自检 `knowledge-review` 生成：series-change-light / material-review-light；知识候选标签页截图未单独生成——最后一次重跑被共享 obj/bin 文件锁打断，自检 JSON 的 12 步覆盖了候选卡行为，记为待补）

## 1. 知识候选区（KnowledgeReviewSection）

| Mac | Windows | 结论 |
|---|---|---|
| 区块标题「知识候选」+「候选先保留在待审阅区；确认后才会影响后续回答。」 | 系列页「待确认」标签内同名标题与说明 | 一致 |
| 「收集本场候选」按钮（summary.lessons/bank → CollectCandidates） | 已实现 `CollectKnowledgeCandidates`（读 summary.json 的 lessons/bank，调 `KnowledgeMerger.CollectCandidates`），但页面暂未放按钮 → 未接 UI | 部分 |
| 「收集已核实的决定与行动」按钮（SessionEvidenceBuilder 证据 → CollectCandidates） | 已实现 `CollectSeriesChangeCandidates`（用当前会议 `NotesEvidence` 调 `WorkspaceSeriesChangeMerger.CollectCandidates`）+ 变更卡区域内按钮 | 一致（按钮位置在变更区域） |
| 候选卡：卡面 AISoft 背景 + hairline 描边 | 卡片用 `card` 样式，容器为普通区块（无 AISoft 背景） | 视觉近似 |
| 知识候选卡：kind 标签「经验建议 / 答法建议」+ 右侧「忽略」 | 已补：KindLabel（答法建议/经验建议）+ 忽略按钮 | 一致 |
| 问题候选且题库已有相似问题：显示「系列当前问题 / 系列当前答法 / 本次建议」+「保留当前答法」+「采用建议并保留旧版」 | 已补（`KnowledgeCandidateRow.MatchingQuestion` + 三段展示 + 两按钮；`KeepCandidate_Click`/`ReplaceCandidate_Click`） | 一致 |
| 问题候选且是新问题：「建议答法」+「加入题库」 | 已补（ShowAddNewQuestion 分支 + 加入题库） | 一致 |
| 经验候选且已有相近经验（相似度 ≥0.6）：「系列已有相近经验：…」+「保留现有经验并记下来源」 | 已补（MatchingLesson 相似度 ≥0.6 + 该文案/按钮） | 一致 |
| 新经验候选：「加入经验库」 | 已补 | 一致 |

## 2. 决定 / 行动候选卡（SeriesChangeCandidateCard）

| Mac | Windows | 结论 |
|---|---|---|
| kind 标签「决定建议 / 行动建议」+ 右侧「忽略」 | 已补（SeriesChangeRow.KindLabel + 忽略） | 一致 |
| 卡片正文 + 依据「依据：“…” · 位置」 | 已补（SourceLine = 依据：“quote” · location） | 一致 |
| 行动候选：提示「确认负责人和期限；期限会保留原话，不会自动改写相对日期。」+ 负责人输入 + 期限原话输入 | 已补（IsAction 分支 + Owner/Due 双向绑定输入框） | 一致 |
| 接受时把负责人/期限写入系列记录（WorkspaceSeriesChangeReviewDetails） | 已补（`ReviewSeriesChange(row, decision, target)` 传 details；自检验证 Owner=Shiyin、Due 原话、ActionState=Open） | 一致 |
| 内容已过期（证据修订对不上）：「原话或负责人/期限已变化；此建议不能接受。」+「忽略过期建议」 | Windows 无 evidenceReviews 修订链，用 `SourceMissing || TranscriptSourceRevision 为空` 近似判断 → 提示与忽略按钮已补 | 部分（判定近似） |
| 「更新已有内容（保留旧版本）」列表 + 每行「更新」（把旧 revision 写入 history） | 已补（MatchingItems + 更新按钮 → Apply Replace with targetItemId/expectedRevision；自检验证 revision 2=1+1、history 1 条、文本替换） | 一致 |
| 「作为新决定/行动加入系列」主按钮 | 已补（AddLabel 按 kind 切换 + 主按钮；自检行动/决定各加一条） | 一致 |
| 卡内忙碌态（isWorking 禁用按钮） | Windows 未做（操作同步，无网络/长任务） | 差异（可接受） |

## 3. 记录标签（WorkspaceRecordsTab）

| Mac | Windows | 结论 |
|---|---|---|
| 系列编辑器/详情内「记录」区块：这个系列的会议，按时间倒序 | 系列编辑器新增「记录」标签：`SeriesRecords`（按系列成员、当前排序） | 一致 |
| 空状态「还没有记录」+「从这个资料夹开始一场会议，记录和资料会保存在一起。」 | 同文案（`SeriesRecordsEmptyText`） | 一致 |
| 点击一行 → 打开会议页（historySelection + select(.meetings)） | 「查看」按钮 → `OpenSeriesRecordAsync`：选中会议并切到会议页（Section=Notes；自检验证 current session = 该记录） | 一致 |

## 4. 资料复核（MaterialReviewSheet）

| Mac | Windows | 结论 |
|---|---|---|
| 标题资料名 + 「完成」 | 已补 | 一致 |
| 警告块「这份资料需要核对」+ 逐条警告（DocumentNotice.warnings） | 已补（`MaterialReviewWarnings` / HasMaterialReviewWarnings）；库列表行加「需要核对」chip | 一致 |
| 轻提示行（DocumentNotice.note：图片未读取但正文完整） | 已补（`MaterialReviewNoteLine`） | 一致 |
| 左「原件」：QuickLook 预览 + 「用系统应用打开」；原件缺失占位「原件暂不可用」 | Windows 无内嵌 QuickLook；显示原件路径 + 「用系统应用打开」（系统默认应用）+ 缺失占位同文案 | 部分（无内嵌预览） |
| 右「提取内容」：前 80,000 字符预览 + 截断说明 + 「打开完整文本」 | 已补（`MaterialReviewPreview` 截断 80k + 说明 + 打开 document.json）；Mac 打开归档 Markdown，Windows 只有解析 JSON | 部分（完整文本格式不同） |
| 复核备注持久化到 material-review.json | 已补且抽为 `MaterialReviewNotes`（Load/NoteFor/Save，空白备注删除条目），自检验证写入 | 一致 |
| 拖放/复核状态 | 无（Mac 也无显式状态字段） | 一致 |

## 5. 自检点按记录（`knowledge-review`，隔离数据目录 + 真实 workspace/session 文件）

| 步骤 | 操作 | 结果 |
|---|---|---|
| 1 | 系列页加载 3 条变更候选 + 3 条知识候选 + 2 条记录 | 通过 |
| 2 | 行动建议填负责人/期限 → 作为新行动加入 | 通过（Owner=Shiyin、Due 原话、Open） |
| 3 | 决定建议 → 作为新决定加入 | 通过 |
| 4 | 「更新」已有行动 → 保留旧版本 | 通过（revision 2、history 1、文本更新） |
| 5 | 队列消费完，无待确认 | 通过 |
| 6 | 知识候选显示系列当前答法（相似问题匹配） | 通过 |
| 7 | 「采用建议并保留旧版」 | 通过（题库 answer 更新、answerHistory 1 条） |
| 8 | 新经验「加入经验库」 | 通过 |
| 9 | 「忽略」知识候选 | 通过（无 pending） |
| 10 | 记录标签「查看」打开会议页 | 通过（Section=Notes、CurrentSession=该记录） |
| 11 | 资料复核：警告与 80k 提取预览 | 通过（1 条警告、正文含扫描页文本） |
| 12 | 复核备注写入 material-review.json | 通过（字段一致） |

自检报告：`Windows/validation/screens/knowledge-review/knowledge-review-check.json`（12/12、exit 0）。
新增测试 `MaterialReviewTests`（4 个）：备注按资料往返/空白删除、缺失或损坏文件读空、预览带页码定位并按 80k 截断、只有需人工处理的警告才标记 NeedsReview。

## 6. 未解决 / 待补

1. 「收集本场候选」（知识候选）已实现 AppState 方法但页面无按钮（变更区域的「收集已核实的决定与行动」已接）；需确认按钮位置后再放。
2. 过期判定为近似：Windows 没有 Mac 的 evidenceReviews 修订链（origin/claimRevision/transcriptSourceRevision 校验），只用 SourceMissing/TranscriptSourceRevision 缺失近似；「忽略过期建议」文案已按 Mac。
3. 卡片无 Mac 的 AISoft 背景与卡内忙碌态（操作同步完成）；候选区整体容器视觉未完全照 Mac。
4. 资料复核：无 QuickLook 内嵌原件预览；「打开完整文本」打开的是解析 JSON（Windows 无归档 Markdown 路径）；截断说明/80k 预览已对照。
5. 无 Mac 对应视图截图（KnowledgeReviewSection/WorkspaceRecordsTab/MaterialReviewSheet 均无 docs 截图），本轮截图仅 Windows 自检取证；按口径状态最多「已实现、未验证」。
6. 记录标签的排序跟随当前历史排序（Mac 按 startedAt 倒序），差异小但未逐项对齐。

## task-62 / E18 闭环：知识复核与资料复核（2026-11-21，阶段 4 §6.7）

- Mac 参照：`Sources/NextThought/KnowledgeReviewSection.swift`（知识/决定/行动候选卡）、`ProjectLibrarySupport.swift`（WorkspaceRecordsTab / MaterialReviewSheet）；macOS 无对应截图，按源码逐元素。
- 本轮自检：`--self-test=knowledge-review` **passed**（`knowledge-review-check.json` 已更新入库，12 步全过）。
- 截图（本目录，本轮隔离数据目录重拍）：`series-change-light.png`（系列页「待确认」变更卡）、`series-knowledge-light.png`（知识候选标签）、`material-review-light.png` / `material-review-dark.png`（资料复核窗口浅/深）。
- 本轮修掉的 3 个真实问题（E18 阻塞的根因，全部在系列/Workspace 范围）：
  1. **`WorkspaceStore.Save` 丢 `sessionIds`**：`WorkspaceSeries.SessionIds` 是 `[JsonIgnore]`（Windows 分组状态），旧实现直接序列化 series，保存后 `sessionIds` 不落盘，`Load()` 读回 0 条记录 → 系列「记录」标签与知识复核自检都变空。现 Save 显式把 `sessionIds` 写入每个 series 文件与 legacy `Workspace/workspace.json`（`SerializeSeries`/`SerializeData`），Load 原有 `ReadSessionIds` 读回；新增 `WorkspaceSessionRoundTripTests` 2/2 锁定往返。
  2. **demo 切页清空选中系列**：`ApplyDemoStage(Library)` 在自定义 workspace（自检/真实非 demo 数据）里找不到 demo 面试系列时把 `SelectedSeries = null`；现在找到才赋值，不再覆盖用户/自检选择（`AppState.Demo.cs` 3 处）。
  3. **`FocusSeriesForCheck` 选中不触发刷新**：`RefreshSeries()` 已默认选中同一行，再赋同值不触发 `OnSelectedSeriesChanged`，导致记录/变更列表不重建；现在显式刷新 lessons/meetings/skills/changes/stats（`AppState.Series.cs`）。
- 逐元素结论沿用上表：知识候选卡、决定/行动候选卡、匹配问题/近似经验对照、保留旧版、忽略路径均**一致**；仍记为部分/缺口的是「收集本地候选」「收集已核实决定与行动」两个按钮未放到页面（数据层 `CollectKnowledgeCandidates`/`CollectSeriesChangeCandidates` 已实现）、AISoft 卡片底色（视觉近似）、以及真实资料复核窗口的人工确认流程（自检只覆盖警告/预览/备注写入）。这些不影响本轮 E18 验收，已在表中逐条标注。

## task-62 / E18 增量：知识复核按钮与 AISoft 卡片（2026-11-21）

- 按 Mac `KnowledgeReviewSection.swift` 补齐页面元素（此前只有数据层）：
  1. **「收集本场候选」按钮**：在系列页「待确认」标签的「知识候选」区标题下，绑定 `CollectKnowledgeLabel`（Mac 原文「收集本场候选」）与 `CanCollectKnowledgeCandidates`，点击走 `CollectKnowledgeCandidates()`（读本场 summary 的 lessons/bank 调 `KnowledgeMerger.CollectCandidates`）；「收集已核实的决定与行动」按钮此前已有，现两个按钮都在页面上。
  2. **候选卡 AISoft 底色**：决定/行动候选卡与知识候选卡都改为 `Classes="card" Background="{DynamicResource AiSoft}"`，对齐 Mac KnowledgeReviewSection 的 AISoft 卡片背景（浅/深主题由 AiSoft 资源自动切换）。
- 自检：`--self-test=knowledge-review` 仍 **passed 12/12**；新测试 `SeriesKnowledgeStage4Tests` **2/2**（按钮标签/命令接线、两张候选卡 AiSoft）。
- 截图：`material-review-{light,dark}.png` 与 `knowledge-review-check.json` 本轮重拍/更新（1120 窗口位图，自检 passed 12/12）。系列页「变更/知识候选」两张标签截图**待补**：详情列高于 820 视口，直接截 TabControl 得到 1×1 图；已改为滚动到标签后截整窗，但本轮共享树重建后应用未能启动，下一轮重跑自检补这两张（不影响 ②③ 的代码验收）。
- 结论：② 收集按钮与 ③ AISoft 卡片已**逐条一致**；① 结构化纪要逐条编辑 +「我已核实」仍在做（见 notes/compare.md 的 E15/E16 段，完成后另提交）。
