# Mac 编解码夹具（A15）

由 `swift run --package-path Windows/tools/MacCodecMac MacCodec emit --output Windows/tests/Fixtures/mac` 生成，2026-11-21。

## 怎么生成的

- 每个文件都用 `NextThoughtCore` 的真实类型构造，用应用写盘用的同一个编码器 `JSONLines.encoder()`（ISO-8601 秒级日期、键排序、斜杠不转义）写出；写出后用 `JSONLines.decoder()` 读回，和原值逐字段相等才落盘。
- 修订号全部由 Swift 函数算出：`SessionProjectionBuilder.transcriptRevision / transcriptSourceRevision / structuredNotesRevision`，以及 `SessionEvidenceBuilder.build` 给出的 `claimRevision`、`sourceRevision`。
- 被替换过的系列条目走真实流程：第一场会议的决定经证据复核后由 `WorkspaceSeriesChangeMerger.collectCandidates` 收为候选，`.add` 采纳；第二场会议的新决定再经 `.replace` 替换。条目 revision 2，history 里保留 revision 1，history id 由 Swift `stableID` 派生。
- 固定 UUID、固定日期（2025-11-24T16:00:00Z 起），重复运行输出逐字一致。
- `MacCodec check` 用 Swift 类型逐个解码；`emit` 结束时会自动跑一遍。

## 文件与 Mac 上的位置

| 夹具 | Mac 类型 | Mac 上的文件 |
|---|---|---|
| `session-meta.json` | `SessionMeta` | `Sessions/<id>/meta.json` |
| `transcript.jsonl` | `SessionTranscriptLine`，每行一条 | `Sessions/<id>/transcript.jsonl` |
| `translations.jsonl` | `SessionTranscriptTranslation` | `Sessions/<id>/translations.jsonl` |
| `voices.jsonl` | `SessionVoiceTag` | `Sessions/<id>/voices.jsonl` |
| `questions.jsonl` | `SessionQuestionEvent` | `Sessions/<id>/questions.jsonl` |
| `answers.jsonl` | `SessionAnswerEvent` | `Sessions/<id>/answers.jsonl` |
| `summary.json` | `SessionSummary`（含 `evidenceReviews`） | `Sessions/<id>/summary.json` |
| `structured-notes.json` | `StructuredNotes` | 纪要的结构化结果 |
| `user-content.json` | `SessionUserContent`（笔记、重点、编辑过的纪要、改字决定） | `Sessions/<id>/user-content.json` |
| `workspace.json` | `Workspace` | `Workspaces/<id>.json` |
| `skills.json` | `[Skill]` | 备份里的 `skills.json` |
| `templates.json` | `[NotesTemplate]`（自定义模板） | 备份里的 `note-templates.json` |
| `glossary.json` | `Glossary` | `Glossary/glossary.json`，备份里的 `glossary.json` |
| `backup-manifest.json` | `BackupManifest` | 备份里的 `manifest.json` |
| `revisions.json` | 修订号输入与 Swift 结果 | A11 逐值核对用 |
| `sessions/<id>/…` | 两场会议的 Mac 目录布局 | 跨会议的系列条目要用到第二场 |

两场会议、三个说话人（我、对方声音 1“王老师”、对方声音 2），一条模型改字（Sherpa → Sherpa-ONNX）和一条用户改字决定，全局术语表含术语和人名，系列术语表另有一条。

## 与旧版 emit 的差别

旧版 emit 写的是手写字典，没有经过 Mac 类型。按 Mac 真实格式，下面几处和旧夹具不同，Windows 侧需要按这里改：

- `Workspace` 没有 `sessionIds`：会议靠 `meta.json` 的 `workspaceID` 指向系列。
- `BackupManifest` 记的是 `kind`、`appVersion`、`appBuild`、`workspaceCount`、`sessionCount`，没有 `sessions`、`workspaces` 列表。
- `SessionQuestionEvent` 必须有 `score`；`disposition` 取 `auto` / `suggested` / `manual`。
- `SessionMeta` 必须有 `audioSource`、`recognizer`、`answerModel`、`transcriptCount`、`questionCount`、`answerCount`、`summaryState`。
- `Glossary` 编码为 `{"entries": [...]}`，读取时也接受裸数组。
- `.jsonl` 每行一个紧凑 JSON 对象。

Windows 侧不再有只查键名的 `MacCodecCheck`：A15 的比对改由 Core 测试完成，`Windows/tests/NextThought.Core.Tests/MacFixtureCodecTests.cs` 用程序真实类型逐家族读入、写出并逐键比较（六处旧版差异），`MacFixtureRevisionTests.cs` 用 `revisions.json` 逐值复算修订号；`Windows/verify.ps1` 显式跑这两个过滤。

## 环境

- Mac：Mac14,10（MacBook Pro，Apple M2 Pro），Apple Silicon 原生运行，不经 Rosetta
- macOS 26.5.2（25F84）
- Swift 6.1.2（Command Line Tools）
- `NextThoughtCore` 源码：`windows/parity` 分支 87fd6531（`Resources/Info.plist` 为 3.3.0 build 3304）。这些文件涉及的存储格式和已发布的 3.4.0 build 3400 相同：3.4.0 只新增了不写盘的类型。
