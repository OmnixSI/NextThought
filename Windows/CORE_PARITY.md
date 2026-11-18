# Core 移植对照（CORE_PARITY）

来源 `Sources/NextThoughtCore`，共 80 个文件、约 17,202 行（含注释与提示词文本）。
状态含义：

- **已移植**：C# 侧逐文件移植，带 xunit 测试，行为与 Swift 对齐。
- **部分移植**：只移植了其它文件依赖的那几个函数，其余随该文件的完整移植补齐。
- **基线简化版**：基线 0.4.0 已有的 C# 实现，只覆盖一部分字段或规则，后续按方案 P1 被逐文件移植取代。
- **未移植**：尚无 C# 对应物。

| Swift 文件 | 行数 | 状态 | C# 对应 / 备注 |
| --- | ---: | --- | --- |
| AnswerEvidence.swift | 57 | 部分移植 | Core/AnswerEvidence.cs（SearchDecision.parse + AnswerGrounding；EvidenceBundle/AnswerState 等 ContextExcerpt/ScreenContext/WebResult 移植） |
| AnswerLanguage.swift | 38 | 已移植 | Core/AnswerLanguage.cs（4 项测试） |
| AnswerPromptBuilder.swift | 328 | 部分移植 | Core/AnswerPromptBuilder.cs（指令原文、预算化 JSON、整份资料、精炼；PreparationPrompt 依赖未移植的 ModeProfile，暂缺） |
| AnswerServiceSupport.swift | 242 | 已移植 | Core/AnswerServiceSupport.cs（默认后端、服务商表、Key 清洗/掩码、失败解释、时间线；8 项测试） |
| AnswerStreaming.swift | 267 | 已移植 | Core/AnswerStreaming.cs（Codex 协议、SSE 解析、模型列表、请求体；11 项测试） |
| AudioTimeline.swift | 119 | 已移植 | Core/AudioTimeline.cs（10 项测试） |
| Backup.swift | 667 | 部分移植 | Core/Backup.cs（清单校验、模板合并、待导入会议、设置快照/密钥过滤；9 项测试。工作区与知识合并、归档写入未移植） |
| BackupArchivePolicy.swift | 33 | 已移植 | Core/BackupContent.cs（路径/符号链接/资源上限预检） |
| BackupContent.swift | 36 | 已移植 | Core/BackupContent.cs（内容指纹与导入状态） |
| ByteLevelBPETokenizer.swift | 219 | 已移植 | Core/ByteLevelBPETokenizer.cs（tokenizer.json、字节表、合并与解码；8 项测试） |
| CancellableTaskValue.swift | 34 | 已移植 | Core/CancellableTaskValue.cs（取消不取消共享任务） |
| CloudTranscription.swift | 293 | 已移植 | Core/CloudTranscription.cs（三家服务商配置、转写消息装配、16→24 kHz 重采样；9 项测试。WebSocket 传输仍在应用层） |
| CoachSummary.swift | 69 | 已移植 | Core/CoachSummary.cs（随 Scene 测试覆盖） |
| CodexEventParser.swift | 31 | 已移植 | Core/CodexEventParser.cs（2 项测试） |
| Companion.swift | 110 | 部分移植 | Platform/CompanionServer.cs（`CompanionSnapshot` / `CompanionEnvelope` / 局域网命令去重）+ Core/CompanionRelayCrypto.cs（`CompanionCommandCache`）；`CompanionHTTP` 的完整动作表仍在 Platform |
| CompanionBLEProtocol.swift | 93 | 已移植 | Core/CompanionBLEProtocol.cs（分片/重组/重发 outbox） |
| CompanionCrypto.swift | 68 | 已移植 | Core/CompanionRelayCrypto.cs（9 项测试；HKDF 用 RFC 5869 向量、AES-GCM 信封、配对链接、命令新鲜度与去重） |
| DocumentClassifier.swift | 74 | 已移植 | Core/DocumentClassifier.cs（文件名优先 + 正文前 2000 字） |
| DocumentFeatureAvailability.swift | 27 | 已移植 | Core/DocumentFeatureAvailability.cs（依赖齐备才可用） |
| DocumentJobs.swift | 466 | 部分移植 | Core/DocumentJobs.cs（限制、任务模型与阶段文案、依赖错误/事件/产物；10 项测试。SQLite 队列与工作区删除日志未移植） |
| DocumentNotice.swift | 53 | 已移植 | Core/DocumentNotice.cs（提示与警告区分、页码汇总） |
| DocumentStructure.swift | 210 | 已移植 | Core/DocumentStructure.cs（标题路径/代码/公式/表格块、HTML 表格分片、检索分块；11 项测试） |
| EchoSuppression.swift | 177 | 已移植 | Core/EchoSuppression.cs（8 项测试；已接入转写回声判定） |
| GazeEstimator.swift | 188 | 已移植 | Core/GazeEstimator.cs（眼部几何、标定中位数、平滑与迟滞、注视比例；8 项测试） |
| Glossary.swift | 572 | 已移植 | Core/Glossary.cs（词条/分类/宽松键/归一/合并/检索展开/规范替换/热词/提示词；17 项测试） |
| IncrementalBackup.swift | 123 | 已移植 | Core/IncrementalBackup.cs（内容寻址恢复点、校验、去重与裁剪；16 项测试） |
| LaTeXMath.swift | 545 | 已移植 | Core/LaTeXMath.cs（Unicode 转换 + Markdown 预处理；17 项测试；接入答案与纪要渲染） |
| LiveInsights.swift | 403 | 部分移植 | Core/AnswerPoints.cs（仅 Split / StripMarkup，供提词使用） |
| LiveTranslationRevisionGate.swift | 76 | 已移植 | Core/LiveTranslation.cs（12 项测试；逐字同传给字幕条与提词岛） |
| LocalModelReuse.swift | 112 | 已移植 | Core/LocalModelReuse.cs（按名字搜索、大小/哈希校验、原子复用；4 项测试） |
| MaterialTerms.swift | 57 | 已移植 | Core/MaterialTerms.cs（3 项测试，含 9 个 IsTerm 用例） |
| MeetingCarry.swift | 38 | 部分移植 | Core/MeetingCarry.cs（材料/复习/经验/转写开关与快照过滤；4 项测试。系列无文档列表，材料开关暂不生效） |
| MeetingEndWatch.swift | 130 | 已移植 | Core/MeetingEndWatch.cs（8 项测试；已接入会议自动收尾） |
| MeetingPreparation.swift | 43 | 部分移植 | Core/MeetingPreparation.cs（MeetingPlan 有效模式、携带选择与快照；3 项测试。风格/文档继承未移植） |
| MeetingType.swift | 168 | 部分移植 | Core.MeetingTypeDefaults（场景 → 纪要模板与实时模式；自定义类型未移植） |
| NotesTemplates.swift | 873 | 部分移植 | Core/NotesTemplates.cs + NotesTime.cs + NotesDates.cs + TranscriptCorrections.cs（模板/解析/渲染/提示/校对/到期日） |
| OnboardingFlow.swift | 55 | 已移植 | Core/OnboardingFlow.cs（步骤/可跳过/待办） |
| OnboardingVersion.swift | 16 | 已移植 | Core/OnboardingVersion.cs（与 TranscriptContextBuilder 同文件） |
| PDFTextLayout.swift | 171 | 已移植 | Core/PDFTextLayout.cs（页眉页脚、页码、换行合并、标题识别；9 项测试；已接入 PDF 读取） |
| ProcessLineReader.swift | 67 | 已移植 | Core/ProcessLineReader.cs（流式行读取 + 进程退出等待；5 项测试） |
| QuestionDetector.swift | 115 | 已移植 | Core/QuestionDetector.cs（片段合并、15 秒去重、问句判定；7 项测试） |
| QuestionEvalCorpus+Extended.swift | 110 | 已移植 | Core/QuestionEvalCorpus.cs（Extended 集；评估器见 QuestionDetectionEvaluator.cs） |
| QuestionEvalCorpus+HeldOut.swift | 49 | 已移植 | Core/QuestionEvalCorpus.cs（HeldOut 集） |
| QuestionEvalCorpus.swift | 8 | 已移植 | Core/QuestionEvalCorpus.cs（139 条标注语料：Extended + HeldOut） |
| QuestionIntent.swift | 532 | 已移植（等价重写） | QuestionIntent.cs + QuestionCandidateTracker.cs + QuestionVocabulary.cs；阈值/追问/候选跟踪/评测器齐备，139 条语料 82/82、误报 0 |
| QuestionTurn.swift | 96 | 已移植 | Core/QuestionTurn.cs（整轮提问、词数、何时该答；14 项测试） |
| RealtimeSetupReadiness.swift | 77 | 已移植 | Core/RealtimeSetupReadiness.cs（录音就绪与可请求启动） |
| Scene.swift | 427 | 已移植 | Core/Scene.cs（11 项测试；场景/提词/教练已接入界面） |
| ScreenContext.swift | 44 | 已移植 | Core/ScreenContext.cs（屏幕上下文、引用检测、文本相似度；10 项测试） |
| ScreenDelta.swift | 23 | 已移植 | Core/ScreenDelta.cs（变化区域与边距） |
| ScrollFollowInputClassifier.swift | 21 | 已移植 | Core/ScrollFollowState.cs（滚动导航键与命中测试） |
| ScrollFollowState.swift | 48 | 已移植 | Core/ScrollFollowState.cs（跟随/读历史/选中文字） |
| SemanticRetrieval.swift | 277 | 已移植 | Core/SemanticRetrieval.cs（指纹、分块、SemanticIndex、RRF、HybridContextIndex.retrieve 与显式页码/文件名优先；11 项测试） |
| SeriesBriefing.swift | 327 | 部分移植 | Core/SeriesBriefing.cs（近期会议/经验/题库/候选限制 + 决定/待办/已完成/过期项；14 项测试。证据引用合并未移植） |
| SessionArchivePersistence.swift | 513 | 部分移植 | Core/SessionArchivePersistence.cs（目录读取结果、内容标记、写入收据、原子追加/替换、残留判定；11 项测试。迁移发布已移植；翻译写队列未移植） |
| SessionContentSearch.swift | 293 | 部分移植 | Core/SessionContentSearch.cs（字段/命中/摘录/去重/按会话首命中；9 项测试。fields 构建器依赖 SessionTextProjection/SessionSummary，未移植） |
| SessionContext.swift | 265 | 部分移植 | Core/SessionContext.cs（词法索引、手动提问、Office XML 文本、AnswerStyle、ContextExcerpt；ContextDocument/SessionMode/SessionPreset 未移植） |
| SessionEvidence.swift | 456 | 部分移植 | Core/SessionEvidence.cs + SessionEvidenceBuilder.cs（评审/依据等级/来源/结构定位/引用 + 引文与时间匹配、评审时效；11 项测试。结构化笔记提取与编辑器未移植） |
| SessionExport.swift | 129 | 部分移植 | Core/SessionExport.cs（SRT/VTT 字幕与时间戳；Markdown/Word/PDF 导出走既有实现） |
| SessionRecord.swift | 740 | 部分移植 | Models.cs + Platform/SessionStore + SessionSummaryParser + SessionMarkdownExporter（会话读写、恢复中断记录、摘要解析、Markdown 导出、汇总/阶段复盘提示词；验收覆盖） |
| SessionSearch.swift | 173 | 部分移植 | Core/SessionSearch.cs（命中标签/打开目标/进度与快照字节数/无缓存检索；8 项测试。磁盘快照加载与字节上限缓存未移植） |
| SessionTextProjection.swift | 497 | 部分移植 | Core/SessionTextProjection.cs + SessionProjectionBuilder.cs（选项/可搜索文本/回答/摘要/新鲜度 + 转写/译文/问题/回答/摘要条目与修订；13 项测试。完整转写行投影与笔记渲染未移植） |
| SessionTranscriptLocator.swift | 31 | 已移植 | Core/SessionTranscriptLocator.cs（按 id 或 3 秒容差定位） |
| SessionUserContent.swift | 143 | 已移植 | Core/SessionUserContent.cs（个人笔记/重点/编辑稿/校对决定/修订存储；7 项测试） |
| ShortcutBinding.swift | 26 | 已移植 | Core/ShortcutBinding.cs（2 项测试） |
| Skill.swift | 84 | 已移植 | Core/Skill.cs（front matter、标题回退、启用/全局筛选） |
| SpeakerClustering.swift | 272 | 已移植 | Core/SpeakerClustering.cs（VoiceClusterer 合并/判定 + SpeakerLabels 编号与提示词；12 项测试；VoiceGroups 委托至此） |
| StructuredNotes.swift | 962 | 已移植 | Core/StructuredNotes.cs + StructuredNotesCleaner.cs（16 项测试；鱼骨图/大纲/观点对照已接入纪要页，含核对与丢弃） |
| SubtitleBar.swift | 295 | 已移植 | Core/SubtitleBar.cs（12 项测试） |
| TranscriptContextBuilder.swift | 8 | 已移植 | Core/OnboardingVersion.cs（TranscriptContextBuilder） |
| TranscriptDeduplicator.swift | 98 | 已移植 | Core/TranscriptDeduplicator.cs（8 项测试） |
| UpdateCatalog.swift | 164 | 部分移植 | Core/UpdateCatalog.cs（7 项测试；设置页「检查更新」已接入） |
| VoiceActivitySegmenter.swift | 153 | 已移植 | Core/VoiceActivitySegmenter.cs（partial/final、12 秒切分 + 0.5 秒重叠、预卷；7 项测试；已接识别器） |
| VoiceProfile.swift | 86 | 已移植 | Core/VoiceProfile.cs（0.55 阈值、3 段起建、离群片段丢弃） |
| WebLookupPromptBuilder.swift | 41 | 已移植 | Core/WebLookupPromptBuilder.cs（字段预算与最近两轮） |
| WebSearchNeed.swift | 129 | 已移植 | Core/WebSearchNeed.cs（17 条标注语料逐条断言；联网搜索提供方仍未接） |
| Workspace.swift | 1460 | 部分移植 | Core/Workspace.cs + WorkspaceKnowledge.cs（系列、经验版本历史、题库、候选与回执、KnowledgeMerger；16 项测试。seriesItems、源删除完整性、SeriesBriefing 未移植） |
| WorkspaceArchiveOwnership.swift | 35 | 部分移植 | Core/WorkspaceArchiveOwnership.cs（系列目录下生成/复用 md + json、原子写、非法 id 拒绝；6 项测试。Windows 资料全局共享，无 markdownPath 回写） |
| WorkspaceDeletionTransaction.swift | 151 | 部分移植 | Core/WorkspaceDeletionTransaction.cs（有序清单、暂存/回滚/清理、授权事务；9 项测试。DocumentJob 与共享原件判定未移植） |
| ZipformerStreaming.swift | 496 | 部分移植 | Core/ZipformerStreaming.cs（模型文件名/在线与离线模型描述/热词清洗与临时文件/错误文案/识别结果；9 项测试。运行时仍由 Platform/SpeechRecognizerSession + sherpa-onnx 承担） |

统计：已移植 56，部分移植 24，基线简化版 0，未移植 0。

测试：`tests/NextThought.Core.Tests` 566 项通过（SubtitleBar 12、AudioTimeline 10、
TranscriptDeduplicator 8、EchoSuppression 8、MeetingEndWatch 8、Scene/Coach 11、结构化纪要 16、Workspace 6、
Glossary 9、知识库与 KnowledgeMerger 16、逐字同传门控 12、双屏布局 10、伴侣中转加密 9、小文件批次 25、小文件批次二 18、字幕导出 11、小文件批次三 25、音频与模型 11、说话人 12、公式 17、小文件批次四 14、增量备份 16、文档结构 11、提问轮次 14、其余 28）。

已接入运行时：回声过滤、会议自动收尾、场景预设与提词、临场教练、结构化纪要
（鱼骨图 / 要点大纲 / 观点对照，含提示词、逐条核对丢弃与鱼骨图、观点矩阵绘制）、
系列与经验（workspace.json；系列页可新建系列、把会议归档进系列、记一句话经验）、
系列知识库（模板里 NotesCarry 的章节变成待确认候选，系列页逐条采纳/替换/忽略，
确认后的经验与题库进入回答提示词；离线自检 `--knowledge-check`）、
逐字同传（`LiveTranslationCoordinator` 限流 + 过期结果丢弃；译文进字幕条与提词岛）、
伴侣中转（`CompanionRelayClient` 与 `CompanionCrypto` 的 HKDF/AES-GCM；设置里的中转地址）、
术语表（正确写法作为识别热词，错写法在转写里被改回）、
检查更新（正式/main 频道、语义版本比较、只提示并打开下载页）、
模板纪要（九个模板、按场景默认、按模板整理并保存 summary.md）、
转写校对（按行定位、修订指纹、原子重写 transcript.jsonl 并留 corrections.json）。

未接入/未移植的重点：`Workspace` 的决定/待办（`WorkspaceSeriesItem`）与源删除完整性、`SeriesBriefing`、
`SpeakerClustering`、双屏演讲、应用内自动替换安装包。
