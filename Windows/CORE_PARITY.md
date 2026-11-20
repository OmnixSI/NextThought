# Core 移植对照（CORE_PARITY）

本文件是 **v3.3.0 的 Core 认领表**：把 Mac 侧 `Sources/NextThoughtCore` 的每个文件对应到 Windows 的
C# 目标与测试，作为 P1（Core 移植）的批次登记表。状态列初始一律「未开始」；每移植一个文件，把状态改为
「已移植 / 测试通过」，并补测试数与差异说明。

- 基线：Mac 主分支 `5ddb9e5`（v3.3.0，2026-11-17）。
- 规模：**80 个文件 / 17,202 行 / 68 个测试文件 / 592 项测试**（行数含注释与提示词文本）。
- 旧基线：Mac v2.3.1（`af60a4d`）。旧移植版（0.4.0 / 1.3.7）的对照记录只留在 git 历史与
  `PORTING_STATUS.md`，不作为本表的状态来源；本表按 v3.3.0 重新起算。
- ★ = v2.3.1 之后新增的文件（24 个），没有旧实现可参照，直接按 Swift 源码移植。
- 批次划分：A 算法与规则 39 文件，B 数据模型 22，C 持久化与协议 11，D 回答与检索 7，外加 `ZipformerStreaming` 特例。
- 表中 `QuestionEvalCorpus` 一行合并了 `+Extended`、`+HeldOut` 两个文件，因此表体行数（78 行）比文件总数
  （80 个）少 2；其余行与文件一一对应。

## 批次 A：算法与规则（39 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | 状态 |
|---|---|---|---|---|
| QuestionDetector | 115 |  | NextThought.Core | 未开始 |
| QuestionIntent | 532 |  | 替换基线 144 行版本；另有 RealtimeRegressionTests | 未开始 |
| QuestionTurn | 96 | ★ | 一轮提问合并与修订 | 未开始 |
| QuestionEvalCorpus（含 Extended、HeldOut） | 167 |  | 导出 JSON 夹具，两端共用 | 未开始 |
| TranscriptDeduplicator | 98 |  |  | 未开始 |
| TranscriptContextBuilder | 8 |  |  | 未开始 |
| VoiceActivitySegmenter | 153 |  | 替换基线 66 行版本 | 未开始 |
| AudioTimeline | 119 |  |  | 未开始 |
| EchoSuppression | 177 |  |  | 未开始 |
| CancellableTaskValue | 34 |  |  | 未开始 |
| ProcessLineReader | 67 |  | Darwin 调用改 Process；含 ProcessExitWaiterTests | 未开始 |
| ShortcutBinding | 26 |  | keyCode 仍存 Mac 虚拟键码 | 未开始 |
| AnswerLanguage | 38 | ★ | 回答语言选择 | 未开始 |
| LaTeXMath | 545 |  |  | 未开始 |
| ByteLevelBPETokenizer | 219 |  | 替换 QwenTokenizer.cs；夹具直接复用 | 未开始 |
| PDFTextLayout | 171 | ★ | PDF 文本块按版面排序 | 未开始 |
| ScrollFollowInputClassifier | 69 | ★ | 与 State 合计 69 行 | 未开始 |
| ScrollFollowState |  | ★ | 讲稿跟随状态 | 未开始 |
| ScreenDelta | 67 |  | 与 ScreenContext 合计 | 未开始 |
| ScreenContext |  |  |  | 未开始 |
| GazeEstimator | 188 | ★ | 眼神提醒本版不做，但 Core 逻辑保留 | 未开始 |
| SpeakerClustering | 272 |  | 全程 float | 未开始 |
| VoiceProfile | 86 |  | 我的声音档案 | 未开始 |
| MeetingEndWatch | 130 |  | 无人发言自动结束 | 未开始 |
| CoachSummary | 69 | ★ | 临场教练统计 | 未开始 |
| OnboardingFlow | 71 |  | 与 Version 合计 | 未开始 |
| OnboardingVersion |  |  |  | 未开始 |
| WebSearchNeed | 170 |  | 与 LookupPromptBuilder 合计 | 未开始 |
| WebLookupPromptBuilder |  |  |  | 未开始 |
| Skill | 84 |  | SKILL.md 解析 | 未开始 |
| MaterialTerms | 57 | ★ | 从资料提取术语 | 未开始 |
| DocumentClassifier | 154 |  | 与 FeatureAvailability、Notice 合计 | 未开始 |
| DocumentFeatureAvailability |  |  |  | 未开始 |
| DocumentNotice |  | ★ |  | 未开始 |
| RealtimeSetupReadiness | 77 |  | 权限项改 Windows 麦克风隐私开关 | 未开始 |
| LiveTranslationRevisionGate | 76 |  | 同传修订节流 | 未开始 |
| SubtitleBar | 295 |  | 字幕条行、通道与计时 | 未开始 |

## 批次 B：数据模型（22 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | 状态 |
|---|---|---|---|---|
| SessionContext | 265 |  | 自定义编解码 2 处 | 未开始 |
| MeetingType | 168 |  | 会中模式、纪要模板、按标题猜场景 | 未开始 |
| Scene | 427 | ★ | 场景预设、提词样式、教练配置、提词行解析 | 未开始 |
| NotesTemplates | 873 |  | 自定义编解码 5 处；提示词逐字搬 | 未开始 |
| StructuredNotes | 962 |  | 自定义编解码 2 处 | 未开始 |
| SessionRecord | 740 |  | 替换基线 Models.cs；日期 ISO 8601 | 未开始 |
| SessionUserContent | 143 | ★ | 高亮、修订、用户编辑；3 处自定义编解码 | 未开始 |
| SessionTextProjection | 497 | ★ | 原文/修订/译文视图与指纹；SHA-256 | 未开始 |
| SessionEvidence | 456 | ★ | 对按键排序 JSON 字节求哈希；逐字节一致 | 未开始 |
| SessionTranscriptLocator | 31 | ★ | 要点跳回原话 | 未开始 |
| SessionExport | 129 | ★ | 导出格式与字幕 | 未开始 |
| SessionSearch | 466 | ★ | 与 ContentSearch 合计 | 未开始 |
| SessionContentSearch |  | ★ | 搜索覆盖转写/校正/译文/问答/纪要 | 未开始 |
| Glossary | 572 |  | 术语与人名表；4 处自定义编解码 | 未开始 |
| Workspace | 1460 |  | 系列、资料夹、经验、题库、议程；测试 1,259 行 | 未开始 |
| WorkspaceArchiveOwnership | 186 | ★ | 与 DeletionTransaction 合计 | 未开始 |
| WorkspaceDeletionTransaction |  | ★ | 可回滚删除 | 未开始 |
| SeriesBriefing | 327 | ★ | 系列简报与变更合并 | 未开始 |
| MeetingCarry | 81 | ★ | 与 Preparation 合计 | 未开始 |
| MeetingPreparation |  | ★ | 会前计划 | 未开始 |
| DocumentStructure | 210 |  | 文档定位、内容块 | 未开始 |
| LiveInsights | 403 |  | 文本相似度、屏幕类型、幻灯片页匹配 | 未开始 |

## 批次 C：持久化与协议（11 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | 状态 |
|---|---|---|---|---|
| SessionArchivePersistence | 513 | ★ | 替换基线 SessionStore.cs；fsync = Flush(true) | 未开始 |
| Backup | 736 |  | Backup+BackupArchivePolicy+BackupContent 合计；替换 BackupService.cs | 未开始 |
| BackupArchivePolicy |  |  |  | 未开始 |
| BackupContent |  | ★ |  | 未开始 |
| IncrementalBackup | 123 |  | .ntrestore 恢复点 | 未开始 |
| LocalModelReuse | 112 |  | Spotlight 改目录遍历 | 未开始 |
| DocumentJobs | 466 |  | Microsoft.Data.Sqlite；表结构/SQL 不变 | 未开始 |
| Companion | 271 |  | Companion+BLEProtocol+Crypto 合计；AesGcm/HKDF 向量必须过 | 未开始 |
| CompanionBLEProtocol |  |  |  | 未开始 |
| CompanionCrypto |  |  |  | 未开始 |
| UpdateCatalog | 164 |  | 增加 Windows 安装包文件名规则 | 未开始 |

## 批次 D：回答与检索（7 文件）

| Swift 文件 | 行数 | 新增 | C# 目标 / 备注 | 状态 |
|---|---|---|---|---|
| AnswerPromptBuilder | 328 |  | 提示词逐字搬；英文 18 词上限 | 未开始 |
| AnswerStreaming | 267 |  | Codex 应用服务协议、流式片段解析 | 未开始 |
| AnswerServiceSupport | 242 |  | 服务商预设与切换 | 未开始 |
| AnswerEvidence | 57 |  | 回答证据与状态 | 未开始 |
| CodexEventParser | 31 |  |  | 未开始 |
| CloudTranscription | 293 |  | 云端识别服务商与结果拼装 | 未开始 |
| SemanticRetrieval | 277 |  | 替换基线 DocumentChunks.cs、KeywordIndex.cs | 未开始 |

## 特例（1 文件）

| Swift 文件 | 行数 | 新增 | 做法 | 状态 |
|---|---|---|---|---|
| ZipformerStreaming | 496 |  | 模型描述/热词/句尾静音/队列进 Core；C API 进 Platform 的 SherpaSpeechEngine.cs（实现 ISpeechEngine）；集成测试无模型时跳过并报告 | 未开始 |

## 使用规则

1. 每移植一个文件，在本表把状态改成「已移植 / 测试通过」；批次退出条件是测试数量与 Mac 相等（592 项）且六组夹具全过。
2. 8 个「替换基线」的文件（QuestionIntent、VoiceActivitySegmenter、ByteLevelBPETokenizer、SessionRecord、
   SessionArchivePersistence、Backup、SemanticRetrieval、ZipformerStreaming）在替换前先加回归：0.4.0 写出的
   会议目录能被新代码读取。
3. ★ 的 24 个文件在 v2.3.1 基线里没有对应物，不要拿 Windows 0.4.0 的精简版当验收参照；以 Swift 源码和 Mac 夹具为准。
4. 测试文件 68 个、592 项，逐项对齐；ProductArchiveUXTests、StabilityTests、RealtimeRegressionTests 跨类型，
   放在批次 C 结束时统一移植。

## 附：v2.3.1 → v3.3.0 新增的 24 个 Core 文件

AnswerLanguage、BackupContent、CoachSummary、DocumentNotice、GazeEstimator、MaterialTerms、MeetingCarry、
MeetingPreparation、PDFTextLayout、QuestionTurn、Scene、ScrollFollowInputClassifier、ScrollFollowState、
SeriesBriefing、SessionArchivePersistence、SessionContentSearch、SessionEvidence、SessionExport、SessionSearch、
SessionTextProjection、SessionTranscriptLocator、SessionUserContent、WorkspaceArchiveOwnership、
WorkspaceDeletionTransaction。
