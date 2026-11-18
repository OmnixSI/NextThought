# Core 移植对照（CORE_PARITY）

来源 `Sources/NextThoughtCore`，共 80 个文件、约 17,202 行（含注释与提示词文本）。
状态含义：

- **已移植**：C# 侧逐文件移植，带 xunit 测试，行为与 Swift 对齐。
- **部分移植**：只移植了其它文件依赖的那几个函数，其余随该文件的完整移植补齐。
- **基线简化版**：基线 0.4.0 已有的 C# 实现，只覆盖一部分字段或规则，后续按方案 P1 被逐文件移植取代。
- **未移植**：尚无 C# 对应物。

| Swift 文件 | 行数 | 状态 | C# 对应 / 备注 |
| --- | ---: | --- | --- |
| AnswerEvidence.swift | 57 | 未移植 |  |
| AnswerLanguage.swift | 38 | 未移植 |  |
| AnswerPromptBuilder.swift | 328 | 未移植 |  |
| AnswerServiceSupport.swift | 242 | 未移植 |  |
| AnswerStreaming.swift | 267 | 未移植 |  |
| AudioTimeline.swift | 119 | 已移植 | Core/AudioTimeline.cs（10 项测试） |
| Backup.swift | 667 | 未移植 |  |
| BackupArchivePolicy.swift | 33 | 未移植 |  |
| BackupContent.swift | 36 | 未移植 |  |
| ByteLevelBPETokenizer.swift | 219 | 未移植 |  |
| CancellableTaskValue.swift | 34 | 未移植 |  |
| CloudTranscription.swift | 293 | 未移植 |  |
| CoachSummary.swift | 69 | 已移植 | Core/CoachSummary.cs（随 Scene 测试覆盖） |
| CodexEventParser.swift | 31 | 未移植 |  |
| Companion.swift | 110 | 未移植 |  |
| CompanionBLEProtocol.swift | 93 | 未移植 |  |
| CompanionCrypto.swift | 68 | 未移植 |  |
| DocumentClassifier.swift | 74 | 未移植 |  |
| DocumentFeatureAvailability.swift | 27 | 未移植 |  |
| DocumentJobs.swift | 466 | 未移植 |  |
| DocumentNotice.swift | 53 | 未移植 |  |
| DocumentStructure.swift | 210 | 未移植 |  |
| EchoSuppression.swift | 177 | 已移植 | Core/EchoSuppression.cs（8 项测试；已接入转写回声判定） |
| GazeEstimator.swift | 188 | 未移植 |  |
| Glossary.swift | 572 | 未移植 |  |
| IncrementalBackup.swift | 123 | 未移植 |  |
| LaTeXMath.swift | 545 | 未移植 |  |
| LiveInsights.swift | 403 | 部分移植 | Core/AnswerPoints.cs（仅 Split / StripMarkup，供提词使用） |
| LiveTranslationRevisionGate.swift | 76 | 未移植 |  |
| LocalModelReuse.swift | 112 | 未移植 |  |
| MaterialTerms.swift | 57 | 未移植 |  |
| MeetingCarry.swift | 38 | 未移植 |  |
| MeetingEndWatch.swift | 130 | 已移植 | Core/MeetingEndWatch.cs（8 项测试；已接入会议自动收尾） |
| MeetingPreparation.swift | 43 | 未移植 |  |
| MeetingType.swift | 168 | 未移植 |  |
| NotesTemplates.swift | 873 | 部分移植 | Core/NotesTime.cs（仅 NotesTime、NotesVerifier.IsSupported；模板与校对提示未移植） |
| OnboardingFlow.swift | 55 | 未移植 |  |
| OnboardingVersion.swift | 16 | 未移植 |  |
| PDFTextLayout.swift | 171 | 基线简化版 | LayoutReadingOrder.cs（简化版） |
| ProcessLineReader.swift | 67 | 未移植 |  |
| QuestionDetector.swift | 115 | 未移植 |  |
| QuestionEvalCorpus+Extended.swift | 110 | 未移植 |  |
| QuestionEvalCorpus+HeldOut.swift | 49 | 未移植 |  |
| QuestionEvalCorpus.swift | 8 | 未移植 |  |
| QuestionIntent.swift | 532 | 基线简化版 | QuestionIntent.cs + QuestionCandidateTracker.cs + QuestionVocabulary.cs（精简重写） |
| QuestionTurn.swift | 96 | 基线简化版 | AutomaticAnswerScheduler.cs（部分） |
| RealtimeSetupReadiness.swift | 77 | 未移植 |  |
| Scene.swift | 427 | 已移植 | Core/Scene.cs（11 项测试；场景/提词/教练已接入界面） |
| ScreenContext.swift | 44 | 未移植 |  |
| ScreenDelta.swift | 23 | 未移植 |  |
| ScrollFollowInputClassifier.swift | 21 | 未移植 |  |
| ScrollFollowState.swift | 48 | 未移植 |  |
| SemanticRetrieval.swift | 277 | 基线简化版 | DocumentChunks.cs + KeywordIndex.cs（简化版） |
| SeriesBriefing.swift | 327 | 未移植 |  |
| SessionArchivePersistence.swift | 513 | 未移植 |  |
| SessionContentSearch.swift | 293 | 未移植 |  |
| SessionContext.swift | 265 | 未移植 |  |
| SessionEvidence.swift | 456 | 未移植 |  |
| SessionExport.swift | 129 | 未移植 |  |
| SessionRecord.swift | 740 | 基线简化版 | Models.cs（只覆盖部分字段） |
| SessionSearch.swift | 173 | 未移植 |  |
| SessionTextProjection.swift | 497 | 未移植 |  |
| SessionTranscriptLocator.swift | 31 | 未移植 |  |
| SessionUserContent.swift | 143 | 未移植 |  |
| ShortcutBinding.swift | 26 | 未移植 |  |
| Skill.swift | 84 | 未移植 |  |
| SpeakerClustering.swift | 272 | 基线简化版 | SpeakerFeatures.cs（仅特征） |
| StructuredNotes.swift | 962 | 已移植 | Core/StructuredNotes.cs + StructuredNotesCleaner.cs（16 项测试；鱼骨图/大纲/观点对照已接入纪要页，含核对与丢弃） |
| SubtitleBar.swift | 295 | 已移植 | Core/SubtitleBar.cs（12 项测试） |
| TranscriptContextBuilder.swift | 8 | 未移植 |  |
| TranscriptDeduplicator.swift | 98 | 已移植 | Core/TranscriptDeduplicator.cs（8 项测试） |
| UpdateCatalog.swift | 164 | 未移植 |  |
| VoiceActivitySegmenter.swift | 153 | 基线简化版 | VoiceActivitySegmenter.cs（简化版） |
| VoiceProfile.swift | 86 | 未移植 |  |
| WebLookupPromptBuilder.swift | 41 | 未移植 |  |
| WebSearchNeed.swift | 129 | 未移植 |  |
| Workspace.swift | 1460 | 部分移植 | Core/Workspace.cs（系列、经验与 workspace.json；知识库/候选人/合并未移植） |
| WorkspaceArchiveOwnership.swift | 35 | 未移植 |  |
| WorkspaceDeletionTransaction.swift | 151 | 未移植 |  |
| ZipformerStreaming.swift | 496 | 未移植 |  |

统计：已移植 8，部分移植 3，基线简化版 7，未移植 62。

测试：`tests/NextThought.Core.Tests` 79 项通过（SubtitleBar 12、AudioTimeline 10、
TranscriptDeduplicator 8、EchoSuppression 8、MeetingEndWatch 8、Scene/Coach 11、结构化纪要 16、Workspace 6）。

已接入运行时：回声过滤、会议自动收尾、场景预设与提词、临场教练、结构化纪要
（鱼骨图 / 要点大纲 / 观点对照，含提示词、逐条核对丢弃与鱼骨图、观点矩阵绘制）、
系列与经验（workspace.json；系列页可新建系列、把会议归档进系列、记一句话经验）。

未接入/未移植的重点：`Workspace` 的知识库、变更候选与 `KnowledgeMerger`、
`NotesTemplates`（模板与校对提示）、`Glossary`、`SpeakerClustering`、双屏演讲、同传与自动更新。
