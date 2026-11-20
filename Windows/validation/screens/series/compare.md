# E20 系列编辑器：Mac ↔ Windows 逐元素对照

对照代码：`Sources/NextThought/WorkspacesView.swift` 全文（1877 行）、`Sources/NextThought/WorkspaceActions.swift`
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 截图 `docs/screenshots/materials-light.png` / `materials-dark.png`（v3 目录无 series 单独截图，materials 页含系列列表；编辑器窗口 Mac 截图缺失，按源码逐元素对照）
Windows 代码：`Windows/src/NextThought.Desktop/Views/SeriesView.axaml(.cs)`、`Views/SeriesEditorWindow.axaml(.cs)`、`AppState.Series.cs`、`AppState.SeriesEditor.cs`（本次重写）、`NextThought.Core/SeriesMaterials.cs`（新增）
Windows 截图：`Windows/validation/screens/series/`（自检 `series-editor` 生成：series-light.png / series-editor-light.png；另 series/ 下 D4 批 series 页面 light/dark/en-light/en-dark 为上一批截图）

## 1. 结构

Mac `WorkspacesView` 由 `WorkspaceList`（左侧资料夹列表：搜索、排序、置顶、新建菜单、导入）与 `WorkspaceEditor`（右侧详情：七个 section：记录 records / 资料 documents / 知识 knowledge / 基本信息 basic / 回答要求 requirements / 技能 skills / 复盘 review）组成，`WorkspaceEditController` 管理 draft 的创建/编辑/提交。

Windows 为「系列」页：左列系列列表（现有），右侧选中系列（会议列表、技能、系列变更待确认、经验/待确认/题库三标签、新增经验输入），「编辑系列」打开 `SeriesEditorWindow`。E20 将编辑器窗口扩展为四标签：**基本信息 / 回答要求 / 资料 / 复盘**。

| Mac 元素 | Windows | 结论 |
|---|---|---|
| WorkspaceList：资料夹标题 + N 个系列、搜索「搜索系列或资料」、排序（最近活动/名称）、置顶、新建系列菜单（各模式 + 导入系列或备份） | 系列页左列（现有）：系列列表、新建系列输入框；无搜索/排序/置顶/导入系列菜单 | 部分（非 E20 编辑器范围，记未解决） |
| WorkspaceEditor 七个 section | 编辑器窗口四标签（基本信息/回答要求/资料/复盘）+ 系列页三知识标签 | 元素齐、分组不同；知识（经验/题库/待确认）在系列页标签，可编辑入口在编辑器复盘标签 → 见 2.3 |
| 记录 section（会话统计/阶段复盘/会议列表） | 复盘标签：统计行（会话/问题/经验/题库）、阶段复盘文本、经验列表、题库列表；会议列表在系列页（现有 SeriesMeetings） | 一致（会议列表留在页面） |
| 资料 documents section | 资料标签：准备队列摘要、导入资料、系列资料列表（启用开关/块数/字符数/格式/分类/移动/移除/撤销） | 见 2.2 |
| 知识 knowledge section（经验 + 题库 + 待确认） | 编辑器复盘标签：经验/题库可编辑（编辑行内按钮、删除、保留历史）；待确认候选在系列页「待确认」标签（现有采纳/替换回答/忽略） | 一致 |
| 基本信息 basic section（模式标题、名称、模式字段、其他背景、更多信息） | 基本信息标签：模式标题、系列名称、类型下拉、回答风格、使用系列知识、领域字段（按模式生成 + 未知字段保留） | 一致；「其他背景/更多信息」折叠组未做（字段编辑器已覆盖结构化字段） |
| 回答要求 requirements section（偏好、模式角色/复盘重点） | 回答要求标签：角色、复盘重点提示、要求/偏好多行、复盘/当前阶段多行 | 一致 |
| 技能 skills section | 系列页「附着技能」勾选（现有） | 一致（位置不同） |
| 复盘 review section（统计、阶段复盘生成/重新生成、知识待审阅、会议列表与查看） | 复盘标签：统计（只读）、阶段复盘编辑；无「生成阶段复盘」按钮 | 部分：生成阶段复盘在本轮未接（Windows 无 stageReview 生成服务，见未解决） |
| 归档/删除（WorkspaceActions：archive、deleteWorkspace 事务） | 编辑器底部「归档系列」（立即写 Archived）+「删除系列」（确认「删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。」→ 连会议记录删除，走 DeleteSeriesWithMeetingsAsync） | 一致 |

## 2. 元素清单

### 2.1 基本信息 / 字段编辑器（WorkspaceBasicTab / FieldEditor）

| Mac | Windows | 结论 |
|---|---|---|
| 模式标题（`profile` 显示名 / 自定义类型名） | `SeriesEditorModeTitle` 显示当前模式名；类型下拉列出所有 `MeetingMode` 原始值（general/meeting/interview/…） | 一致（Mac 用显示名 + custom 类型名；Windows 类型存原始值） |
| 系列名称单行（仅编辑态） | 系列名称 TextBox（始终可编辑，保存提交） | 一致 |
| 核心字段（模式 profile 前两个） | 字段编辑器按 `ModeProfile.Fields` 逐项生成（label + placeholder + 单行/多行高度 `Long`） | 一致 |
| 其他背景（notes 长文本）、更多信息（其余字段，折叠 + N/M 已填写） | 字段编辑器包含全部 profile 字段，无「其他背景」独立长文本；Windows 无 series.notes 字段 | 部分（Windows 数据模型无 notes 字段，记未解决） |
| 未知/自定义字段保留 | 打开时把 `Fields` 中 profile 未覆盖的键追加为自定义行，保存时原样写回 | 一致 |
| 编辑/完成按钮切换编辑态 | 保存按钮统一提交 | 一致（Mac 即时 draft，Windows 保存提交） |

### 2.2 资料（WorkspaceDocumentsTab + PreparationQueueSummary）

| Mac | Windows | 结论 |
|---|---|---|
| 「资料 N」标题 + 导入菜单（自动归类 + 导入到各分类） | 「导入资料」按钮（多选文件选择器，支持 pdf/docx/pptx/xlsx/md/txt/图片/rtf/html/csv）；无按分类导入（Windows 未实现自动归类） | 部分：分类导入与自动归类未做（导入后分类为空=其他，可「移动」切换） |
| 导入进度/状态文字 | `SeriesEditorImportStatus` + 不确定进度条 | 一致 |
| PreparationQueueSummary：失败/部分导入常驻、「N 份已就绪/待完成/需处理/已取消」汇总、逐任务进度/消息、重试/取消、错误行 | 已实现：读 `DocumentJobs/document-jobs.json`（DocumentJobQueue）按系列过滤；汇总同款；失败/警告/取消行显示 重试/取消；无任务时显示「这个系列已有 N 份资料，均在本机解析」 | 一致（队列目前仅在任务存在时可显示；Windows 导入同步完成，尚未写入队列 → 见未解决） |
| 分类小节（profile 分类 + 已有分类）、每份资料：用于回答开关、图标、名称、N 块/字符、格式标签、警告「需要核对」、归档 Markdown 打开、移动分类菜单、移除（可撤销） | 资料列表：启用开关、名称、N 块/字符、格式、分类文字、移动（按 profile 分类循环）、移除、撤销移除；无「需要核对」警告、无归档 Markdown 打开 | 部分（警告/归档打开未做） |
| 拖放文件/文件夹导入 | 仅文件选择器（无拖放、无文件夹展开） | 部分 |
| 最近回答用到的资料片段（selectedExcerpts） | Windows 在纪要页「来源」现有显示 | 一致（位置不同） |

### 2.3 知识：经验 / 题库（KnowledgeTab / ExperienceLessonRow / BankRow）

| Mac | Windows | 结论 |
|---|---|---|
| 经验行：文本编辑、来源标记、修订历史/恢复、删除 | 编辑器复盘标签：经验文本、编辑（RenameSessionDialog）、删除；`WorkspaceLessonHistory.EditText` 保留历史修订（自检验证历史 ≥1 条）；无历史恢复 UI | 部分（恢复历史未做） |
| 题库行：问题/回答可编辑、历史、删除 | 编辑器复盘标签：题目/答案双框编辑（`KnowledgeMerger.EditQuestionBankItem` 保留回答修订）、删除 | 一致（历史恢复 UI 未做） |
| 待确认知识（候选采纳/替换/忽略、来源引用） | 系列页「待确认」标签（现有）：采纳 / 替换回答 / 忽略 | 一致 |

### 2.4 复盘（ReviewTab）

| Mac | Windows | 结论 |
|---|---|---|
| 统计：会话/问题/经验/题库 | `SeriesEditorStatistics` 统计行同四项（会话按系列成员 + 历史记录并集，问题数来自会议 meta） | 一致 |
| 「生成阶段复盘 / 重新生成」按钮 + 条件（至少一场有摘要）+ 生成中状态 + Markdown 展示 | 阶段复盘文本框可读写（可直接编辑）；无生成按钮/生成状态 | 部分：**生成阶段复盘未接**（Windows 未有 stageReview 生成服务；Mac 由 AppState.generateStageReview 调模型）→ 未解决 |
| 阶段复盘时间 | 保存时写 `StageReviewAt`（有内容时） | 一致 |
| 知识待审阅 + 会议列表（查看按钮） | 知识在系列页；会议列表在系列页（现有） | 一致（位置不同） |

## 3. 演示数据点按记录（自检 `series-editor`）

隔离数据目录 + 真实文件，`--self-test=series-editor` 12 个动作全部真实驱动 AppState 命令：

| 步骤 | 操作 | 结果 |
|---|---|---|
| 1 | 打开系列页并进入编辑器 | 通过（字段 2 项、资料 1 份） |
| 2 | 字段编辑器改值并保存 | 通过（domain=高并发交易系统 写回 workspace.json） |
| 3 | 准备队列摘要显示失败任务并重试 | 通过（「会前准备 · 0 份已就绪 · 1 份需处理」→ 重试后 Queued） |
| 4 | 导入真实 Markdown 到系列 | 通过（文档 1→2 份，DocumentIds 同步） |
| 5 | 移除资料 + 撤销 | 通过（移除干净、撤销恢复） |
| 6 | 编辑经验（保留历史） | 通过（历史 1 条） |
| 7 | 编辑题库问题/答案 | 通过 |
| 8 | 截图（系列页 + 编辑器窗口） | series-light.png / series-editor-light.png |
| 9 | 归档系列 | 通过（Archived=True） |
| 10 | 删除系列连同会议记录 | 通过（系列剩余 0；会议 2→0） |

自检报告：`Windows/validation/screens/series/series-editor-check.json`（9/9 通过、exit 0）。

## 4. 测试

新增 `Windows/tests/NextThought.Tests/SeriesMaterialsTests.cs`（5 个测试，均通过）：
- 资料附加同时写 ContextDocument + DocumentIds（id 相同、Pages 来自解析块、格式标签）；
- 同一资料重复导入幂等（文档/ID 各 1 份）；
- Detach 同时清空嵌入副本与库 ID（无悬空 ID）；
- 未知分类回退「其他」；
- EnabledCount 只统计可用资料。

## 5. 未解决 / 待补

1. 「生成阶段复盘」按钮与生成流程未接（Windows 无 stageReview 生成服务；Mac 由模型生成后写入 series.stageReview）——按口径「已实现、未验证」之外的部分如实记未解决。
2. 资料导入分类：Mac 可按分类导入、按内容自动归类；Windows 单一「导入资料」，分类为空（其他），可手动「移动」。
3. 导入任务未写入 DocumentJobQueue：Windows 导入同步完成，准备队列摘要仅在已有队列任务（或历史任务）时展示；Mac 为后台队列 + 重试/取消。
4. 资料行缺「需要核对」警告与「打开归档 Markdown」；无拖放/文件夹导入。
5. 经验/题库缺修订历史查看与恢复 UI（底层历史已保留并可恢复）。
6. 基本信息缺 Mac「其他背景」notes 字段（Windows series 模型无该字段）与「更多信息」折叠分组；类型下拉用原始值而非 Mac 显示名 + 自定义类型名。
7. Mac v3 截图目录没有 series 编辑器实机截图，当前用 materials 页 + 源码对照；需要 Mac 上补 WorkspaceEditor 四标签截图（交 OPEN_QUESTIONS）。
8. `SeriesView` 的经验/题库行仍为只读（编辑入口在编辑器复盘标签）；如要求页内直接编辑需再加行内编辑。
