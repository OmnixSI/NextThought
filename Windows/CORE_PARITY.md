# Swift Core 测试翻译进度（B22）

Swift 测试总数：592。按同名 C# 测试方法统计的已翻译数：34。

| Swift 文件 | Swift 测试数 | 已翻译数 |
|---|---:|---:|
| AnswerPromptBuilderTests.swift | 7 | 0 |
| AnswerServiceSupportTests.swift | 16 | 0 |
| AnswerStreamingTests.swift | 17 | 0 |
| AudioTimelineTests.swift | 10 | 1 |
| BackupArchivePolicyTests.swift | 1 | 0 |
| ByteLevelBPETokenizerTests.swift | 4 | 0 |
| CancellableTaskValueTests.swift | 2 | 0 |
| CloudTranscriptionTests.swift | 9 | 0 |
| CoachSummaryTests.swift | 5 | 0 |
| CodexEventParserTests.swift | 4 | 0 |
| CompanionBLETests.swift | 4 | 0 |
| CompanionCryptoTests.swift | 2 | 0 |
| DocumentDependencyTests.swift | 2 | 1 |
| DocumentFeatureAvailabilityTests.swift | 1 | 0 |
| DocumentJobTests.swift | 6 | 0 |
| DocumentNoticeTests.swift | 11 | 0 |
| DocumentStructureTests.swift | 5 | 0 |
| EchoSuppressionTests.swift | 7 | 0 |
| GlossaryTests.swift | 18 | 0 |
| LaTeXMathTests.swift | 13 | 0 |
| LiveInsightsTests.swift | 24 | 1 |
| LiveTranslationRevisionGateTests.swift | 5 | 0 |
| LocalModelReuseTests.swift | 5 | 0 |
| MeetingCarryTests.swift | 2 | 0 |
| MeetingEndWatchTests.swift | 18 | 1 |
| MeetingPreparationTests.swift | 5 | 5 |
| MeetingTypeAndNotesTests.swift | 17 | 2 |
| OnboardingFlowTests.swift | 6 | 6 |
| OnboardingVersionTests.swift | 4 | 4 |
| PDFTextLayoutTests.swift | 7 | 0 |
| ProcessExitWaiterTests.swift | 1 | 0 |
| ProcessLineReaderTests.swift | 2 | 0 |
| ProductArchiveUXTests.swift | 8 | 0 |
| QuestionCorpusExtended.swift | 0 | 0 |
| QuestionCorpusHeldOut.swift | 0 | 0 |
| QuestionCorpusReportTests.swift | 5 | 0 |
| QuestionDetectorTests.swift | 8 | 0 |
| QuestionIntentTests.swift | 8 | 1 |
| QuestionTurnTests.swift | 8 | 0 |
| RealtimeRegressionTests.swift | 9 | 0 |
| RealtimeSetupReadinessTests.swift | 8 | 0 |
| SceneAndGazeTests.swift | 20 | 1 |
| ScreenDeltaTests.swift | 1 | 1 |
| ScreenEvidenceTests.swift | 5 | 1 |
| ScrollFollowStateTests.swift | 8 | 0 |
| SeriesBriefingTests.swift | 12 | 0 |
| SessionArchivePersistenceTests.swift | 21 | 0 |
| SessionContentSearchTests.swift | 13 | 0 |
| SessionEvidenceTests.swift | 8 | 1 |
| SessionExportTests.swift | 12 | 0 |
| SessionRecordTests.swift | 9 | 3 |
| SessionTextProjectionTests.swift | 8 | 1 |
| SessionTranscriptLocatorTests.swift | 4 | 3 |
| SessionUserContentTests.swift | 7 | 0 |
| SkillTests.swift | 4 | 0 |
| SpeakerClusteringTests.swift | 20 | 0 |
| StabilityTests.swift | 6 | 0 |
| StructuredNotesTests.swift | 31 | 0 |
| SubtitleBarTests.swift | 24 | 0 |
| TranscriptContextBuilderTests.swift | 1 | 0 |
| TranscriptDeduplicatorTests.swift | 3 | 0 |
| UpdateCatalogTests.swift | 6 | 0 |
| VoiceActivitySegmenterTests.swift | 7 | 0 |
| VoiceProfileTests.swift | 16 | 0 |
| WebLookupPromptBuilderTests.swift | 3 | 0 |
| WebSearchNeedTests.swift | 4 | 0 |
| WorkspaceDeletionTransactionTests.swift | 5 | 0 |
| WorkspaceTests.swift | 39 | 1 |
| ZipformerStreamingIntegrationTests.swift | 1 | 0 |

复算命令（PowerShell）：

```powershell
$b='D:\NextThought开发\src\NextThought'
# Swift 测试声明数（排除 @testable import）
Select-String -Path "$b\Tests\NextThoughtCoreTests\*.swift" -Pattern '^\s*@Test' | Measure-Object
# 已翻译数按测试方法名与 C# 测试方法名（首字母大写）匹配估算
Get-ChildItem "$b\Windows\tests\NextThought.Core.Tests" -Filter '*Tests.cs' | Measure-Object
```
