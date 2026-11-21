# §6.7 E25/E30：系列编辑器各标签（WorkspacesView.swift ↔ SeriesEditorWindow）

Mac 参照：`Sources/NextThought/WorkspacesView.swift` 的 `WorkspaceEditor`（562 行起）、`WorkspaceEditorSection`（542）、`WorkspaceBasicTab`（968）、`WorkspaceRequirementsTab`（1113）、`WorkspaceDocumentsTab`（1169）、`KnowledgeTab`（1366）、`WorkspaceSkillsSection`、`ReviewTab`（1747）；Mac 本轮无该窗口截图，按源码逐元素对照。
Windows 实现：`Views/SeriesEditorWindow.axaml(.cs)`、`AppState.SeriesEditor.cs`、`AppState.SeriesEditorHeader.cs`、`AppState.Series.cs`（SeriesSkills/SeriesRecords）。
证据：`series-editor-records-light/dark.png`、`documents-light.png`、`knowledge-light/dark.png`、`skills-light.png`、`basic-light.png`、`requirements-light.png`（760×720，`--self-test=series-editor-tabs` 生成，自检 1/1 通过）。
结论：**以下几条不一致**。

## 1. 头部（WorkspaceEditor.workspaceName + editorActions）

| # | Mac 元素 | Windows 现状 | 一致性 |
|---|---|---|---|
| 1 | `SceneGlyph(mode:size:36)` | `controls:SceneGlyph Mode="{Binding SelectedSeriesMode}" Size="36"` | 一致 |
| 2 | 系列名 `Typo.display`、单行截断 | `TextBlock Classes="display"` + ellipsis/NoWrap | 一致 |
| 3 | `SceneTag(mode:name:)` + `SeriesStatsLine` | `controls:SceneTag` + `SelectedSeriesStatsLine` caption | 部分一致：demo 系列统计行为空时只剩 SceneTag（需回退显示「N 场 · M 份资料」） |
| 4 | `开下一场` PrimaryButtonStyle（isRunning 禁用） | `Button Classes="primary"` 绑 `OpenNextMeetingInSeriesCommand`；未接运行中禁用 | 部分一致（缺 disabled 条件） |
| 5 | `配置` GhostButtonStyle 菜单：基本信息 / 回答要求 / 系列技能 / 分隔线 / 类型选项 | `配置` quiet Button + MenuFlyout：基本信息/回答要求/系列技能/归档系列/删除系列；**类型选项仍在基本信息 ComboBox**，未进配置菜单 | 部分一致（缺菜单内类型选项；归档/删除是 Windows 既有能力） |
| 6 | 无底部工具条 | 底部一行 caption 提示；无保存/取消常驻按钮 | 一致（Mac 也无） |

## 2. 分区导航（WorkspaceEditor section strip）

| # | Mac | Windows | 一致性 |
|---|---|---|---|
| 1 | 仅 3 个 primary：记录 / 资料 / 经验积累（`WorkspaceEditorSection.primary`） | 3 个 `Button.sectionTab`：记录 / 资料 / 经验积累 | 一致 |
| 2 | 每项 `Typo.label`，选中 Ink.ink、未选 Secondary；选中项 2px Ink 胶囊下划线（matchedGeometry） | 13 Medium；选中 Ink + 底边 2px Ink；未选 Secondary；0.5px Hairline 轨道 | 一致（无 matchedGeometry 滑动动画） |
| 3 | 计数：记录 N 场；资料 N 份；经验积累无计数 | 记录 = `SeriesRecords.Count`；资料 = `SeriesEditorDocuments.Count`；经验积累无计数 | 一致 |
| 4 | 分区切换有 `Motion.phase` 过渡 | 直接切换无动画 | 不一致：缺过渡 |

## 3. 各分区内容

### 记录（WorkspaceRecordsTab）
- Mac：系列会议列表（每行同 SessionRow：场景点、标题、时间、细节），空态「还没有记录。从这个系列开始一场会议，记录和资料会保存在一起。」。
- Windows：`ModeTintBrush` 7px 点 + `Title` + `Details`（MeetingOutcome.line）+ 查看按钮；空态同文案；行为一致。部分：Mac 行可打开会议页并带更多操作，Windows 只有「查看」。

### 资料（WorkspaceDocumentsTab + PreparationQueueSummary）
- Mac：`资料 N` 标题、导入菜单（自动归类/按分类导入）、导入进度、准备队列（失败/部分重试）、分类徽标、移除/撤销移除、支持格式说明。
- Windows：准备摘要 + 导入资料按钮 + 进度条、准备队列行（文件名/进度/消息/重试/取消）、资料列表（启用勾选/名称/大小/格式/分类/移动/移除）、撤销移除、格式说明。一致（导入菜单只有单一「导入资料」，缺自动归类/按分类导入）。

### 经验积累（KnowledgeTab + ReviewTab）
- Mac：`经验库与题库` 标题 + 编辑条目模式、搜索、经验列表（编辑/删除）、题库列表（编辑/删除）、阶段复盘（ReviewTab 统计与复盘）。
- Windows：阶段复盘 TextBox + 经验列表（编辑/删除）+ 题库列表（编辑/删除）+ 统计 caption；缺「编辑条目模式」「搜索框」；阶段复盘未做统计结构。部分一致。

### 系列技能（WorkspaceSkillsSection，配置进入）
- Mac：`技能 N` 标题 + 导入并用于此系列 / 管理技能、空态说明、每个技能 WideToggle（含「所有系列」「已停用」徽标）。
- Windows：技能列表 CheckBox（IsAttached 即存）；缺导入/管理按钮、缺「所有系列/已停用」徽标。部分一致。

### 基本信息（WorkspaceBasicTab，配置进入）
- Mac：模式标题 + 编辑按钮、名称/类型/风格、领域字段（前 2 核心字段、可展开更多、结构化字段折叠）、背景笔记、使用知识开关；编辑态有保存/取消操作条。
- Windows：模式标题、名称、类型 ComboBox、风格、使用知识、角色、全部领域字段（无折叠）、底部说明；通过 `EditActionsBar` 显示「正在编辑系列信息 + 保存/取消」。部分一致（缺字段折叠/背景笔记/自定义类型改名）。

### 回答要求（WorkspaceRequirementsTab，配置进入）
- Mac：角色/复盘重点说明、要求偏好多行、阶段复盘；编辑态保存/取消。
- Windows：同上，通过操作条保存/取消。一致（缺 Mac 的示例占位细节）。

## 4. 交互

| 交互 | Mac | Windows | 一致性 |
|---|---|---|---|
| 配置菜单进入 basic/requirements/skills | `requestEditorPage` 切换分区 + 草稿 | 菜单 Click → `ShowSection` 切面板 + 操作条 | 一致 |
| 保存/取消草稿 | `editor.save/cancelDraft`（basic/requirements） | `SaveSeriesEditorCommand` / `LoadSeriesEditor()` 重载 | 一致 |
| 开下一场 | `requestNewMeeting`（运行中禁用） | `OpenNextMeetingInSeriesCommand` | 部分一致（缺运行中禁用） |
| 删除/归档 | Mac 在系列页菜单 | 配置菜单内 | 一致（位置不同，功能保留） |
| 未保存提示 | `showUnsavedPrompt` | 未实现 | 不一致 |

## 5. 未解决（明确列差）

1. 头部 `SeriesStatsLine` 对无统计的系列显示空（应回退「N 场 · M 份资料」）。
2. 配置菜单缺「类型选项」子菜单（仍在基本信息 ComboBox）；开下一场缺运行中禁用。
3. 资料导入缺「自动归类 / 按分类导入」菜单；准备队列已有重试/取消。
4. 经验积累缺编辑条目模式与搜索框；阶段复盘只保留文本框。
5. 技能分区缺导入/管理按钮与「所有系列/已停用」徽标；缺 Mac 的导入并用于此系列。
6. 基本信息缺字段折叠/背景笔记/自定义类型改名；缺未保存修改提示。
7. 分区切换过渡动画（Motion.phase）未做。
8. Mac 无参考截图，本对照为 Swift 源码逐元素；截图来自 demo 数据（记录为空，资料/经验/技能来自演示系列）。
