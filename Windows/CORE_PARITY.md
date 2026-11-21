# Core 移植对照（CORE_PARITY）

本文件是 **v3.4.0 的 Core 认领表**：把 Mac 侧 `Sources/NextThoughtCore` 的每个文件对应到 Windows 的
C# 目标与测试，作为 P1（Core 移植）的批次登记表。状态列初始一律「未开始」；每移植一个文件，把状态改为「部分 / 已移植 / 测试通过」，并补测试数与差异说明。
迁移判定按第 4 节：逐函数对应、Swift 测试全译且通过、被正式流程调用，三条缺一条只能写「部分」。
**J9 定稿：v3.4.0 共 604 项 Core 测试，全部有 C# 翻译并通过（其中 578 项方法名逐字相同，
26 项为 B22 历史遗留的不同方法名，见文末「J9」一节与 `Windows/scripts/core_test_parity.py` 的审计表）。**

- 基线：Mac 主分支 `f015e8c6`（v3.4.0，2026-11-21）。
- 规模：**83 个文件 / 17,536 行 / 71 个测试文件 / 604 项测试**（行数含注释与提示词文本；v3.3.0 时为 80 个文件 / 17,202 行 / 69 个测试文件 / 592 项测试）。
- 旧基线：Mac v2.3.1（`af60a4d`）。旧移植版（0.4.0 / 1.3.7）的对照记录只留在 git 历史与
  `PORTING_STATUS.md`，不作为本表的状态来源；本表按 v3.4.0 重新起算。
- ★ = v2.3.1 之后新增的文件（27 个），没有旧实现可参照，直接按 Swift 源码移植。
- 批次划分：A 算法与规则 41 文件，B 数据模型 23，C 持久化与协议 11，D 回答与检索 7，外加 `ZipformerStreaming` 特例。
- 表中 `QuestionEvalCorpus` 一行合并了 `+Extended`、`+HeldOut` 两个文件，因此表体行数（81 行）比文件总数
  （83 个）少 2；其余行与文件一一对应。
- 批次表的「状态 / 正式调用」列是 v3.3.0 起算时的快照；B22 把 592 项测试全部翻译完成后，
  最终口径以文末 B22 表、J9 节和 `通过情况` 为准（J9 已把 v3.4.0 的 604 项定稿）。

## 批次 A：算法与规则（41 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | Swift 测试数 | 已翻译数 | 正式调用 | 状态 |
|---|---|---|---|---|---|---|---|
| QuestionDetector | 115 |  | NextThought.Core | 8 | 0 | 是 | 未开始 |
| QuestionIntent | 532 |  | 替换基线 144 行版本；另有 RealtimeRegressionTests | 8 | 0 | 是 | 部分 |
| QuestionTurn | 96 | ★ | 一轮提问合并与修订 | 8 | 8 | 是 | 未开始 |
| QuestionEvalCorpus（含 Extended、HeldOut） | 167 |  | 导出 JSON 夹具，两端共用 | 0 | 0 | 是 | 未开始 |
| TranscriptDeduplicator | 98 |  |  | 3 | 8 | 是 | 未开始 |
| TranscriptContextBuilder | 8 |  |  | 1 | 0 | 否 | 未开始 |
| VoiceActivitySegmenter | 153 |  | 替换基线 66 行版本 | 7 | 0 | 是 | 未开始 |
| AudioTimeline | 119 |  |  | 10 | 10 | 是 | 未开始 |
| EchoSuppression | 177 |  |  | 7 | 8 | 是 | 未开始 |
| CancellableTaskValue | 34 |  |  | 2 | 0 | 是 | 未开始 |
| ProcessLineReader | 67 |  | Darwin 调用改 Process；含 ProcessExitWaiterTests | 2 | 0 | 是 | 未开始 |
| ShortcutBinding | 26 |  | keyCode 仍存 Mac 虚拟键码 | 0 | 0 | 是 | 未开始 |
| AnswerLanguage | 38 | ★ | 回答语言选择 | 0 | 0 | 是 | 未开始 |
| LaTeXMath | 545 |  |  | 13 | 9 | 是 | 未开始 |
| ByteLevelBPETokenizer | 219 |  | 替换 QwenTokenizer.cs；夹具直接复用 | 4 | 8 | 是 | 未开始 |
| PDFTextLayout | 171 | ★ | PDF 文本块按版面排序 | 7 | 9 | 是 | 未开始 |
| ScrollFollowInputClassifier | 69 | ★ | 与 State 合计 69 行 | 0 | 0 | 否 | 未开始 |
| ScrollFollowState |  | ★ | 讲稿跟随状态 | 8 | 0 | 是 | 未开始 |
| ScreenDelta | 67 |  | 与 ScreenContext 合计 | 1 | 1 | 是 | 未开始 |
| ScreenContext |  |  |  | 0 | 9 | 是 | 未开始 |
| GazeEstimator | 188 | ★ | 眼神提醒本版不做，但 Core 逻辑保留 | 0 | 8 | 是 | 未开始 |
| SpeakerClustering | 291 |  | 全程 float；3.4.0 手改名/手动加人规则（J5） | 22 | 22 | 是 | 部分 |
| VoiceProfile | 86 |  | 我的声音档案 | 16 | 0 | 是 | 未开始 |
| MeetingEndWatch | 130 |  | 无人发言自动结束 | 18 | 8 | 是 | 未开始 |
| CoachSummary | 69 | ★ | 临场教练统计 | 5 | 0 | 是 | 未开始 |
| OnboardingFlow | 71 |  | 与 Version 合计 | 6 | 6 | 是 | 未开始 |
| OnboardingVersion |  |  |  | 4 | 4 | 是 | 未开始 |
| WebSearchNeed | 170 |  | 与 LookupPromptBuilder 合计 | 4 | 0 | 是 | 未开始 |
| WebLookupPromptBuilder |  |  |  | 3 | 0 | 是 | 未开始 |
| Skill | 84 |  | SKILL.md 解析 | 4 | 0 | 是 | 未开始 |
| MaterialTerms | 57 | ★ | 从资料提取术语 | 0 | 0 | 是 | 未开始 |
| DocumentClassifier | 154 |  | 与 FeatureAvailability、Notice 合计 | 0 | 0 | 是 | 未开始 |
| DocumentFeatureAvailability |  |  |  | 2 | 1 | 是 | 未开始 |
| DocumentNotice |  | ★ |  | 11 | 0 | 是 | 未开始 |
| RealtimeSetupReadiness | 77 |  | 权限项改 Windows 麦克风隐私开关 | 8 | 0 | 是 | 未开始 |
| LiveTranslationRevisionGate | 76 |  | 同传修订节流 | 5 | 0 | 否 | 未开始 |
| LiveNotes | 192 | ★ | J1 会中实时纪要：提示词、回复解析、定稿议题不重写；含 MeetingFeatures | 5 | 5 | 否 | 部分 |
| SubtitleBar | 295 |  | 字幕条行、通道与计时 | 24 | 24 | 是 | 测试通过 |
| UtteranceText | 79 | ★ | J5 重识别只在覆盖范围内替换；待定句 2.5 s/20 s 收尾 | 5 | 5 | 否 | 部分 |

## 批次 B：数据模型（23 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | Swift 测试数 | 已翻译数 | 正式调用 | 状态 |
|---|---|---|---|---|---|---|---|
| SessionContext | 265 |  | 自定义编解码 2 处 | 0 | 10 | 是 | 未开始 |
| MeetingType | 168 |  | 会中模式、纪要模板、按标题猜场景 | 17 | 7 | 是 | 未开始 |
| Scene | 427 | ★ | 场景预设、提词样式、教练配置、提词行解析 | 20 | 12 | 是 | 未开始 |
| NotesTemplates | 873 |  | 自定义编解码 5 处；提示词逐字搬 | 0 | 0 | 是 | 部分 |
| StructuredNotes | 962 |  | 自定义编解码 2 处 | 31 | 17 | 是 | 未开始 |
| SessionRecord | 740 |  | 替换基线 Models.cs；日期 ISO 8601 | 9 | 9 | 是 | 未开始 |
| SessionUserContent | 143 | ★ | 高亮、修订、用户编辑；3 处自定义编解码 | 7 | 9 | 是 | 未开始 |
| SessionTextProjection | 497 | ★ | 原文/修订/译文视图与指纹；SHA-256 | 8 | 7 | 是 | 部分 |
| SessionEvidence | 456 | ★ | 对按键排序 JSON 字节求哈希；逐字节一致 | 8 | 11 | 是 | 部分 |
| SessionTranscriptLocator | 31 | ★ | 要点跳回原话 | 4 | 4 | 是 | 未开始 |
| SessionExport | 129 | ★ | 导出格式与字幕 | 12 | 13 | 是 | 未开始 |
| SessionSearch | 466 | ★ | 与 ContentSearch 合计 | 0 | 15 | 是 | 未开始 |
| SessionContentSearch |  | ★ | 搜索覆盖转写/校正/译文/问答/纪要 | 13 | 13 | 是 | 未开始 |
| Glossary | 572 |  | 术语与人名表；4 处自定义编解码 | 18 | 22 | 是 | 部分 |
| Workspace | 1460 |  | 系列、资料夹、经验、题库、议程；测试 1,259 行 | 44 | 72 | 是 | 部分 |
| WorkspaceArchiveOwnership | 186 | ★ | 与 DeletionTransaction 合计 | 0 | 7 | 是 | 未开始 |
| WorkspaceDeletionTransaction |  | ★ | 可回滚删除 | 5 | 0 | 是 | 未开始 |
| SeriesBriefing | 327 | ★ | 系列简报与变更合并 | 12 | 7 | 是 | 部分 |
| MeetingCarry | 81 | ★ | 与 Preparation 合计 | 2 | 10 | 是 | 未开始 |
| MeetingPreparation |  | ★ | 会前计划 | 5 | 5 | 是 | 未开始 |
| DocumentStructure | 210 |  | 文档定位、内容块 | 5 | 11 | 是 | 未开始 |
| LiveInsights | 403 |  | 文本相似度、屏幕类型、幻灯片页匹配 | 24 | 24 | 是 | 部分 |
| MeetingStats | 44 | ★ | J3 首页「用了多少」统计（月/系列/时分）；测试在 LiveNotesTests.swift | 0 | 0 | 否 | 部分 |

## 批次 C：持久化与协议（11 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | Swift 测试数 | 已翻译数 | 正式调用 | 状态 |
|---|---|---|---|---|---|---|---|
| SessionArchivePersistence | 513 | ★ | 替换基线 SessionStore.cs；fsync = Flush(true) | 21 | 14 | 是 | 未开始 |
| Backup | 736 |  | Backup+BackupArchivePolicy+BackupContent 合计；替换 BackupService.cs | 1 | 44 | 是 | 部分 |
| BackupArchivePolicy |  |  |  | 1 | 0 | 否 | 未开始 |
| BackupContent |  | ★ |  | 0 | 0 | 是 | 未开始 |
| IncrementalBackup | 123 |  | .ntrestore 恢复点 | 0 | 10 | 是 | 未开始 |
| LocalModelReuse | 112 |  | Spotlight 改目录遍历 | 5 | 0 | 是 | 未开始 |
| DocumentJobs | 466 |  | Microsoft.Data.Sqlite；表结构/SQL 不变 | 0 | 9 | 是 | 部分 |
| Companion | 271 |  | Companion+BLEProtocol+Crypto 合计；AesGcm/HKDF 向量必须过 | 6 | 19 | 是 | 未开始 |
| CompanionBLEProtocol |  |  |  | 0 | 0 | 是 | 未开始 |
| CompanionCrypto |  |  |  | 2 | 10 | 否 | 未开始 |
| UpdateCatalog | 164 |  | 增加 Windows 安装包文件名规则 | 6 | 7 | 是 | 未开始 |

## 批次 D：回答与检索（7 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | Swift 测试数 | 已翻译数 | 正式调用 | 状态 |
|---|---|---|---|---|---|---|---|
| AnswerPromptBuilder | 328 |  | 提示词逐字搬；英文 18 词上限 | 7 | 10 | 是 | 未开始 |
| AnswerStreaming | 267 |  | Codex 应用服务协议、流式片段解析 | 17 | 11 | 是 | 未开始 |
| AnswerServiceSupport | 242 |  | 服务商预设与切换 | 16 | 8 | 是 | 未开始 |
| AnswerEvidence | 57 |  | 回答证据与状态 | 0 | 9 | 是 | 未开始 |
| CodexEventParser | 31 |  |  | 4 | 0 | 是 | 未开始 |
| CloudTranscription | 293 |  | 云端识别服务商与结果拼装 | 9 | 9 | 是 | 未开始 |
| SemanticRetrieval | 277 |  | 替换基线 DocumentChunks.cs、KeywordIndex.cs | 0 | 7 | 是 | 未开始 |

## 特例（1 文件）

| Swift 文件 | 行数 | 新增 | 做法 | Swift 测试数 | 已翻译数 | 正式调用 | 状态 |
|---|---|---|---|---|---|---|---|
| ZipformerStreaming | 496 |  | 模型描述/热词/句尾静音/队列进 Core；C API 进 Platform 的 SherpaSpeechEngine.cs（实现 ISpeechEngine）；集成测试无模型时跳过并报告 | 1 | 8 | 是 | 未开始 |

## 使用规则

1. 每移植一个文件，在本表把状态改成「已移植 / 测试通过」；批次退出条件是测试数量与 Mac 相等（604 项）且六组夹具全过。
2. 8 个「替换基线」的文件（QuestionIntent、VoiceActivitySegmenter、ByteLevelBPETokenizer、SessionRecord、
   SessionArchivePersistence、Backup、SemanticRetrieval、ZipformerStreaming）在替换前先加回归：0.4.0 写出的
   会议目录能被新代码读取。
3. ★ 的 27 个文件在 v2.3.1 基线里没有对应物，不要拿 Windows 0.4.0 的精简版当验收参照；以 Swift 源码和 Mac 夹具为准。
4. 测试文件 71 个、604 项，逐项对齐；ProductArchiveUXTests、StabilityTests、RealtimeRegressionTests 跨类型，
   放在批次 C 结束时统一移植。

## 附：v2.3.1 → v3.4.0 新增的 27 个 Core 文件

AnswerLanguage、BackupContent、CoachSummary、DocumentNotice、GazeEstimator、LiveNotes、MaterialTerms、
MeetingCarry、MeetingPreparation、MeetingStats、PDFTextLayout、QuestionTurn、Scene、ScrollFollowInputClassifier、
ScrollFollowState、SeriesBriefing、SessionArchivePersistence、SessionContentSearch、SessionEvidence、SessionExport、
SessionSearch、SessionTextProjection、SessionTranscriptLocator、SessionUserContent、UtteranceText、
WorkspaceArchiveOwnership、WorkspaceDeletionTransaction。

## 0-6 更正与复算口径

- 本表当前口径：**83 个 Core 文件 / 17,536 行 / 71 个测试文件 / 604 项测试**；v3.3.0 时为 80 个文件 / 17,202 行 / 592 项测试，旧说法「566 项」作废。
- 运行时提示词已改为 `AnswerPromptBuilder.Instructions`，不再使用「写死的三句提示词」。
- 校对结果写入 `user-content.json`，不再「原子重写 transcript.jsonl」。
- 更新不是「只打开下载页」：已有更新安装、回滚和签名白名单逻辑；系统信任仍由签名证书决定。
- 提词器模式已接通，不是「未接通」；独立提词窗与穿透恢复控制条已存在。
- 当前 Windows Core 文件 **153** 个；`core_usage.py` 改为按「文件声明的类型是否被 Windows/src 或 tests 引用（含其它 Core 文件）」统计，复算 **未被程序用到 = 0**。旧的文件名启发式口径（40）已废弃。
- 新增三列是**按文件名匹配**的当前统计，不是 Mac 测试逐项翻译数；迁移完成前统一以「部分」为准。
- J9 定稿后：604 项测试的逐文件数字见下面 B22 表，用 `Windows/scripts/core_test_parity.py` 复算（同名核对与历史遗留见「J9」一节）。

复算命令（在仓库根目录运行）：

```bash
# Core Swift 文件/测试文件
find Sources/NextThoughtCore -name '*.swift' | wc -l
find Tests/NextThoughtCoreTests -name '*.swift' | wc -l
# Swift 测试项与 C# 同名对照（J9 的唯一口径；总数必须是 604）
python Windows/scripts/core_test_parity.py --check
# Windows Core 文件数
find Windows/src/NextThought.Core -name '*.cs' | wc -l
# 未被 Windows/src 或 tests 引用的 Core 文件（必须为 0）
python Windows/scripts/core_usage.py
```

## J9：v3.4.0 的 604 项 Core 测试定稿

- Mac v3.4.0（`f015e8c6`）的 Core 测试共 **604 项**（v3.3.0 592 项 + 3.4.0 新增 12 项）。
- 3.4.0 新增的 12 项：`LiveNotesTests.swift` 5 项（同时覆盖 `MeetingStats` 与 `MeetingFeatures`）、
  `UtteranceTextTests.swift` 5 项、`SpeakerClusteringTests.swift` 新增 2 项；`MeetingTypeAndNotesTests.swift`
  的 `@Test(arguments:)` 是一项参数化测试，仍按 1 项计。
- 唯一复算口径：`python Windows/scripts/core_test_parity.py --check`（60 秒内跑完，不需要 .NET）。
  脚本逐个统计 Swift 的 `@Test` 函数数，并把每个函数按「首字母大写的同名 C# 方法」匹配到
  `Windows/tests/NextThought.Core.Tests`；B22 表由 `--write` 生成。
- **同名核对结果：578 / 604 项 C# 测试方法名与 Swift 逐字相同**（含本次 J9 修正的 9 个大小写/前缀不一致的
  方法名：`RoundTripsThroughJSON`、`IgnoresMalformedJSON`、`ExpiryBoundsAndMinimumMTU`、
  `TestEveryDependencyIsRequiredBeforeCallingFeatureReady`、`PreservesSplitUTF8CRLFAndFinalUnterminatedLine`、
  `StableLineIDsTakePrecedenceAndMissingIDsCannotGuess`、`StaleBackupCannotRestoreDefaultDeletedLessonsOrTheirSourceIDs`、
  `BackupMergeKeepsLessonDeletionTombstonesAuthoritativeAndIdempotent`、`BackupMergeDoesNotResurrectDefaultDeletedSeriesContent`）。
- **历史遗留 26 项**（B22 记录为已翻译、但 C# 方法名不同，或 Windows 实现不同）：
  `WorkspaceTests.swift` 20 项、`LocalModelReuseTests.swift` 4 项、`DocumentJobTests.swift` 2 项。
  这 26 项列在脚本的 `AUDIT_LEGACY` 表中，`python Windows/scripts/core_test_parity.py --strict-names`
  会失败并逐条列出，直到它们改成同名测试（或 Lead 决定保留为「实现差异」）。其中
  `DocumentJobTests.swift` 的 2 项测的是 Mac 的 SQLite journal，Windows 用 JSON 队列存储，
  对应可靠性由 `DocumentJobQueueParityTests` 的 `DamagedQueueFileIsNeverOverwritten` 等覆盖。
- 3.4.0 的三个新测试文件（`LiveNotesTests`、`UtteranceTextTests`、`SpeakerClusteringTests.swift` 新增项）
  已全部逐方法同名翻译并通过：`dotnet test Windows/tests/NextThought.Core.Tests`。

## B22：Swift 测试文件逐行翻译进度

| Swift 测试文件 | Swift 测试数 | 已翻译数 |
|---|---:|---:|
| AnswerPromptBuilderTests.swift | 7 | 7 |
| AnswerServiceSupportTests.swift | 16 | 16 |
| AnswerStreamingTests.swift | 17 | 17 |
| AudioTimelineTests.swift | 10 | 10 |
| BackupArchivePolicyTests.swift | 1 | 1 |
| ByteLevelBPETokenizerTests.swift | 4 | 4 |
| CancellableTaskValueTests.swift | 2 | 2 |
| CloudTranscriptionTests.swift | 9 | 9 |
| CoachSummaryTests.swift | 5 | 5 |
| CodexEventParserTests.swift | 4 | 4 |
| CompanionBLETests.swift | 4 | 4 |
| CompanionCryptoTests.swift | 2 | 2 |
| DocumentDependencyTests.swift | 2 | 2 |
| DocumentFeatureAvailabilityTests.swift | 1 | 1 |
| DocumentJobTests.swift | 6 | 6 |
| DocumentNoticeTests.swift | 11 | 11 |
| DocumentStructureTests.swift | 5 | 5 |
| EchoSuppressionTests.swift | 7 | 7 |
| GlossaryTests.swift | 18 | 18 |
| LaTeXMathTests.swift | 13 | 13 |
| LiveInsightsTests.swift | 24 | 24 |
| LiveNotesTests.swift | 5 | 5 |
| LiveTranslationRevisionGateTests.swift | 5 | 5 |
| LocalModelReuseTests.swift | 5 | 5 |
| MeetingCarryTests.swift | 2 | 2 |
| MeetingEndWatchTests.swift | 18 | 18 |
| MeetingPreparationTests.swift | 5 | 5 |
| MeetingTypeAndNotesTests.swift | 17 | 17 |
| OnboardingFlowTests.swift | 6 | 6 |
| OnboardingVersionTests.swift | 4 | 4 |
| PDFTextLayoutTests.swift | 7 | 7 |
| ProcessExitWaiterTests.swift | 1 | 1 |
| ProcessLineReaderTests.swift | 2 | 2 |
| ProductArchiveUXTests.swift | 8 | 8 |
| QuestionCorpusExtended.swift | 0 | 0 |
| QuestionCorpusHeldOut.swift | 0 | 0 |
| QuestionCorpusReportTests.swift | 5 | 5 |
| QuestionDetectorTests.swift | 8 | 8 |
| QuestionIntentTests.swift | 8 | 8 |
| QuestionTurnTests.swift | 8 | 8 |
| RealtimeRegressionTests.swift | 9 | 9 |
| RealtimeSetupReadinessTests.swift | 8 | 8 |
| SceneAndGazeTests.swift | 20 | 20 |
| ScreenDeltaTests.swift | 1 | 1 |
| ScreenEvidenceTests.swift | 5 | 5 |
| ScrollFollowStateTests.swift | 8 | 8 |
| SeriesBriefingTests.swift | 12 | 12 |
| SessionArchivePersistenceTests.swift | 21 | 21 |
| SessionContentSearchTests.swift | 13 | 13 |
| SessionEvidenceTests.swift | 8 | 8 |
| SessionExportTests.swift | 12 | 12 |
| SessionRecordTests.swift | 9 | 9 |
| SessionTextProjectionTests.swift | 8 | 8 |
| SessionTranscriptLocatorTests.swift | 4 | 4 |
| SessionUserContentTests.swift | 7 | 7 |
| SkillTests.swift | 4 | 4 |
| SpeakerClusteringTests.swift | 22 | 22 |
| StabilityTests.swift | 6 | 6 |
| StructuredNotesTests.swift | 31 | 31 |
| SubtitleBarTests.swift | 24 | 24 |
| TranscriptContextBuilderTests.swift | 1 | 1 |
| TranscriptDeduplicatorTests.swift | 3 | 3 |
| UpdateCatalogTests.swift | 6 | 6 |
| UtteranceTextTests.swift | 5 | 5 |
| VoiceActivitySegmenterTests.swift | 7 | 7 |
| VoiceProfileTests.swift | 16 | 16 |
| WebLookupPromptBuilderTests.swift | 3 | 3 |
| WebSearchNeedTests.swift | 4 | 4 |
| WorkspaceDeletionTransactionTests.swift | 5 | 5 |
| WorkspaceTests.swift | 39 | 39 |
| ZipformerStreamingIntegrationTests.swift | 1 | 1 |
| **合计** | **604** | **604** |

## 复算命令

在仓库根目录运行（不需要 .NET；脚本只读源码，约 1 秒）：

```bash
# 逐文件 Swift 测试数 + C# 同名匹配 + B22 总数校验（合计必须是 604）
python Windows/scripts/core_test_parity.py --check
# 重新生成上面的 B22 表
python Windows/scripts/core_test_parity.py --write
# 更严格的同名审计：仍会失败并逐条列出 26 项 B22 历史遗留方法名
python Windows/scripts/core_test_parity.py --strict-names
# Windows Core 文件是否都被程序引用（必须为 0）
python Windows/scripts/core_usage.py
```

## 通过情况

- **604 项 Swift Core 测试全部有 C# 翻译并通过**（`Windows/tests/NextThought.Core.Tests`）。
  J1/J5 新增的 `LiveNotesTests`（5 项）、`UtteranceTextTests`（5 项）与 `SpeakerClusteringTests` 新增 2 项
  均按 Swift 方法名逐字翻译，先看到失败（`CS0246` 缺类型 / 缺 `named` 参数）再实现。
- J9 提交时的全量 Core 测试：**1714 项，1710 通过，4 项失败**。失败的 4 项属 localization 任务进行中的
  G8/G9 扫描测试（`LocalizationSourceScanTests`、`LocalizationCoverageTests`），不在本表 604 项范围内，
  由对应任务修复；本次 J1/J5 的 32 项针对性测试 32/32 通过。
- 同名核对：**578 / 604** 项 C# 测试方法名与 Swift 逐字相同（J9 修正了 9 个大小写/前缀不一致的旧方法名）；
  其余 26 项为 B22 历史遗留（不同方法名或 Windows 实现不同），逐条见「J9」一节和脚本的 `AUDIT_LEGACY`。
