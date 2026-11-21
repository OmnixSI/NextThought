# H2 方法重命名与 AppState 拆分报告

对照基准：Mac v3.3.0（`5ddb9e5`）。决策（见 `OPEN_QUESTIONS.md`）：允许重命名，不建映射层；C# 方法名 = Swift 方法名首字母大写；只改 Swift 有对应物的方法；一个文件一个提交；每个提交后全部测试通过；用 AST/Roslyn 级别重命名，不手工替换字符串。

## 1. 重命名扫描（Roslyn 语义级）

对 clean HEAD `eff8f0b7` 的整个 Windows 源码树做语义扫描（`git worktree` 临时干净快照，避免在途 WIP 干扰；工具在 `%TEMP%\h2\tool`，未提交）：

| 项目 | 普通方法 | 与 Swift 大小写不敏感同名 | 已 PascalCase | 需改名 |
|---|---:|---:|---:|---:|
| NextThought.Core | 1144 | 779 | 779 | **0** |
| NextThought.Platform | 391 | 67 | 67 | **0** |
| NextThought.Desktop | 1312 | 101 | 101 | **0** |
| NextThought.Core.Tests | 1686 | 63 | 63 | **0** |
| NextThought.Tests | 591 | 27 | 27 | **0** |

结论：**Windows 源码已满足「C# 方法名 = Swift 方法名首字母大写」**，没有需要重命名的方法，因此 H2 不产生重命名提交。唯二小写开头的普通方法是 `AppState.StructuredNotes.cs` 的 `rowsSpeakerLabels` 与 `KnowledgeMergerTests.cs` 的 `scope`，它们在 Swift 侧没有同名 `func`，按「只改 Swift 有对应物」的规则不动；`local function` 不在 H2 范围。

复算方式：用 Roslyn `CSharpCompilation` + `SemanticModel` 枚举 `MethodDeclarationSyntax`（`MethodKind.Ordinary`），与 `Sources/NextThought`、`Sources/NextThoughtCore` 中 `func` 声明名做 `OrdinalIgnoreCase` 比较。逐条匹配明细（文件名、C# 名、Swift 名、是否已大写）在 `%TEMP%\h2\{core,platform,desktop,core-tests,tests}_matched.json`。

## 2. AppState 拆分

- `AppState.cs` **354 行**（完成条件：< 600）。
- `Windows/src/NextThought.Desktop/AppState.*.cs` 部分类共 **49 个**。
- Mac 的 15 个 `AppState+*.swift` 中 14 个已有同名 `AppState.*.cs`：Archive、Audio、AutoEnd、Glossary、Insights、Launcher、Lifecycle、Live、Meeting、Models、Navigation、Prepare、Resilience、StructuredNotes。
- 第 15 个 `AppState+Notes.swift` 于本轮补 `AppState.Notes.cs`（762 行，`03bcc17e`）：
  - 原 `AppState.Templates.cs` 整体并入（该文件删除）；
  - 从 `AppState.StructuredNotes.cs` 移入 10 个方法：`SetNotesFormatAsync`、`StructuredPath`、`StructuredJsonPath`、`LoadStructuredAsync`、`GenerateStructuredNotesAsync`、`CanGenerateStructured`、`LineOffsets`、`MeetingSeconds`、`BuildNumberedTranscript`、`SummarizeCoreAsync`；
  - 移入纪要页导航整块（`ShowNotesTranscriptAt` 起至 `RebuildNotesOutline`，含相关字段/属性/事件）。
  - 移动为逐字搬移：`AppState.StructuredNotes.cs` diff 为 0 新增 / 451 删除，被删的 `AppState.Templates.cs` 每一行都原样出现在新文件；无改名、无逻辑改动；构建 0 错误、两套测试全绿。
- `AppState+Notes.swift` 的 24 个 `func` 中，12 个在 `AppState.Notes.cs` 有对应物（见下），其余由 Core / 设置模型 / 视图层承担，未造方法：

| Swift（AppState+Notes.swift） | Windows 对应 |
|---|---|
| `allNotesTemplates` / `notesTemplate(id:)` | `TemplateChoices` + `BuildChoices` + `SelectedTemplate`（AppState.Notes.cs） |
| `saveNotesSettings` | `SaveCustomTemplates` |
| `duplicateNotesTemplate` | `NewTemplate`（新建空模板，不做复制） |
| `updateNotesTemplate` | `SaveEditedTemplate` |
| `generateSummary` / `writeNotes` / `saveGeneratedSummary` | `GenerateStructuredNotesAsync`（写 summary.json/notes.json） |
| `generateSummary`（Markdown/模板路径） | `SummarizeCoreAsync`、`SummarizeWithTemplateAsync` |
| `notesRequest` | `CurrentClient().AnswerAsync`（在 `GenerateStructuredNotesAsync` 内） |
| `carry` 结构化读写 | `SetNotesFormatAsync` 等 10 个移入方法 |
| 纪要页跳转/大纲/会议信息 | `ShowNotesTranscriptAt` → `RebuildNotesOutline` |

未覆盖（Swift 有、C# 无同名/对应方法，未造假方法）：`defaultNotesTemplateID(for:)`（由 `AppState.SceneSettings.cs:DefaultTemplateIdFor` 承担）、`notesTemplate(for workspace)`、`setDefaultNotesTemplate`（由设置 `SelectedTemplateId` + 场景设置承担）、`deleteNotesTemplate`、`notesWorkspace(for detail)`（Core `SeriesOfSession`）、`refreshRestorePoint`、`resumeRecoveredSummariesIfNeeded`、`markSummaryFailed`、`properNouns`、`notesSummary(from:template:detail:)`（Core `SummaryService`）、`toggleCorrection`（E17 校对流程）、`copyNotes`（视图层）。

## 3. 状态与验证

- 状态：**已实现、未验证**（重命名结论与拆分已由 Roslyn 扫描 + 构建/两套测试支撑；「已验证」还需要一次独立人工复核扫描）。
- 证据：本轮 `03bcc17e` 提交；Roslyn 匹配明细在 `%TEMP%\h2\*_matched.json`（临时目录，未入库）；构建与测试见 `PORTING_STATUS.md` 对应轮次。
