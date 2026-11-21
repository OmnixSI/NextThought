# Mac / Windows 功能对照与验收基线（v3.4.0）

> **本文件是 v3.4.0 的 37 行验收基线。** 旧 FEATURE_PARITY 的 47 行功能切片表（0.4.0 时代、对照 Mac v2.3.1）
> 已整体作废，不再作为验收口径；旧行中不在这 37 行内的动作按文末「本版不做与旧行归属」处理。

- 对照基线：Mac 主分支 `f015e8c6`（v3.4.0，2026-11-17）。
- 旧基线快照：`af60a4d`（v2.3.1，2026-11-06）；两者之间 86 个提交、`Sources` 约 +2.4 万行。
- 阶段列是 Windows 侧的归属，不是先后承诺（先后按 v2 §16 的 P1→P8）。
- 配套认领表：`CORE_PARITY.md`（Core 80 个文件）、`VIEW_PARITY.md`（主窗口约 257 个视图 + 5 类特殊窗口）。
- 行内验收只核对功能与证据；不把源码结构检查当作 Mac 实机往返结果。

## 验收记录规则

1. 本表取代旧 FEATURE_PARITY.md 的 47 行。每行在 Windows 上验完，补日期、机器、版本、结果、证据路径；没验的写「未做」或「本版不做 + 原因」。
2. 表中「P7（P2 接口先行）」只指联网与技能：接口和提示词在 P2 落地，真实联网与技能导入在 P7 验收。
3. 提词岛（第 12 行）以 v2 §9 八项为前提；八项不过，先走退路（区域裁剪 / 不透明窗口），不换整个界面框架。
4. 第 14 行眼神提醒不在本版验收内；`GazeEstimator` 进入 P1 只是保留 Core 逻辑，不承诺功能。
5. 第 35 行必须在第二台干净机器上做；开发机的结果不算。

> 状态列说明（本轮 0-4）：只按第 4 节的四种状态填写。**没有任何 Windows 实机记录，因此全表没有「已验证」**。
> 「已实现、未验证」= 代码/测试已存在但未在 Windows 实机操作；「未做」= 代码或验收尚未完成；「本版不做」= 第 1 节允许差异。

| # | 功能 | Mac 侧源码位置 | Windows 阶段 | 验收方式 | Windows 实测（日期 / 机器 / 版本 / 结果 / 证据路径） |
|---|---|---|---|---|---|
| 1 | 系统声加麦克风双路 | AudioCaptureService.swift、MicrophoneCaptureService.swift、AppState+Audio.swift | P3 | 六家会议软件各开一场 10 分钟，双方发言分别进入对应通道 | 已实现、未验证 |
| 2 | 指定应用采音 | AudioCaptureService.swift（应用选择与进程树） | P3 | 会议软件与音乐播放器同时发声，只记录会议软件 | 已实现、未验证 |
| 3 | 连续实采稳定性 | AudioCaptureService.swift、AppState+Resilience.swift | P3 | 2 小时真实硬件采音（不是回放），无丢帧、内存不持续增长 | 未做 |
| 4 | 流式识别加整句精修 | ZipformerStreamingTranscriber.swift、LocalASRModels.swift、ZipformerModelStore.swift、Core ZipformerStreaming.swift | P1+P3 | 4 段标准 WAV 输出与 Mac 相同；最终句延迟 p95 ≤ 0.8 秒 | 已实现、未验证 |
| 5 | 回声抑制与「是我说的」恢复 | EchoMonitor.swift、Core EchoSuppression.swift、LiveSessionView.swift | P1+P4 | 不戴耳机开会，对方的话不重复记到我方；手动恢复可用 | 已实现、未验证 |
| 6 | 说话人分组、我的声音 | SpeakerID.swift、MyVoice.swift、MyVoiceViews.swift、Core SpeakerClustering.swift、VoiceProfile.swift | P1+P3 | 三人录音分组正确；录入我的声音后标为「我」 | 已实现、未验证 |
| 7 | 术语与人名表 | GlossaryStore.swift、GlossaryView.swift、Core Glossary.swift、XASRVocabulary.swift | P1+P4 | 加入术语后识别结果和纪要中拼写正确 | 已实现、未验证 |
| 8 | 问题识别与自动回答 | Core QuestionDetector/QuestionIntent/QuestionTurn、AnswerCoordinator.swift、AppState+Live.swift | P1+P2 | 全量评测语料逐条与 Mac 判定一致；实际会议中提问后自动出回答 | 已实现、未验证 |
| 9 | 三种回答后端 | AnswerEngines.swift、CodexBridge.swift、CodexModelCatalog.swift、APIPresets.swift、Core AnswerStreaming.swift、AnswerServiceSupport.swift | P2 | Codex 常驻、Codex exec、兼容接口各完成一次流式回答和一次取消 | 已实现、未验证 |
| 10 | 联网搜索、技能 | WebSearch.swift、SkillLibrary.swift、Core WebSearchNeed/WebLookupPromptBuilder/Skill | P7（P2 接口先行） | 需要最新信息的问题触发联网；导入 SKILL.md 后回答引用技能 | 已实现、未验证 |
| 11 | 截图提问、屏幕感知 | ScreenshotService.swift、RegionScreenshotPicker.swift、ScreenContextService.swift、ScreenRegionSheet.swift、Core ScreenContext/ScreenDelta | P5 | 窗口截图和区域截图各提问一次；屏幕文字进入回答上下文 | 已实现、未验证 |
| 12 | 提词岛 | CueIsland.swift、AppState+Launcher.swift | P5 | v2 §9 验证表八项全过；各状态与 Mac 录屏逐一对照 | 已实现、未验证 |
| 13 | 一直展开、双屏演讲、讲稿跟随 | CueIsland.swift、PresenterMode.swift、ScrollFollowObserver.swift、ReadingScrollMonitor.swift、SlideNotesCache.swift、Core ScrollFollowInputClassifier/ScrollFollowState、LiveInsights.swift | P5 | 外接屏放映幻灯片，翻页后岛上要点跟随；外接屏上无本应用窗口 | 已实现、未验证 |
| 14 | 临场教练 | LiveCoach.swift、CoachSummaryStore.swift、Core CoachSummary.swift、GazeEstimator.swift（眼神本版不做） | P5 | 语速、独白时长、发言占比三种提醒各触发一次；眼神提醒见 v2 §7 | 未做 |
| 15 | 字幕条、提词器浮窗 | SubtitleBarController/SubtitleBarView/SubtitleBarSettingsView.swift、TeleprompterController.swift、Core SubtitleBar.swift | P5 | 显示、拖动、穿透、录屏隐藏各验一次 | 已实现、未验证 |
| 16 | 同声传译 | LiveTranslation.swift、Core LiveTranslationRevisionGate.swift、AnswerLanguage.swift | P2+P5 | 英译中、中译英各一段，译文随修订更新并上岛 | 已实现、未验证 |
| 17 | 九个场景与自定义 | Core Scene.swift、MeetingType.swift、SceneSettings.swift、Onboarding.swift | P1+P4 | 每个场景开一场会，会中模式/回答风格/纪要模板与 Mac 一致；按标题自动选场景 | 已实现、未验证 |
| 18 | 系列、议程、经验库、题库 | WorkspaceStore/WorkspaceActions/WorkspacesView.swift、KnowledgeReviewSection.swift、Core Workspace/SeriesBriefing/MeetingCarry/MeetingPreparation | P2+P6 | 同一系列开两场会，第二场带上第一场确认的经验和常见问题 | 已实现、未验证 |
| 19 | 无人发言自动结束 | Core MeetingEndWatch.swift、AppState+AutoEnd.swift、AutoEndViews.swift | P1+P2 | 静音到达阈值后倒计时并结束，可取消 | 已实现、未验证 |
| 20 | 会后纪要及四种格式 | Core NotesTemplates/StructuredNotes、StructuredNotesView/OutlineView/FishboneView/ViewpointMatrixView、NotesRegeneration.swift | P2+P6 | 纪要、提纲、鱼骨图、观点对照各生成一次；要点能跳回原话 | 已实现、未验证 |
| 21 | 证据核对、高亮、修订 | Core SessionEvidence/SessionUserContent/SessionTextProjection、MeetingNotesEditor/NotesPageView/NotesFormatBar | P2+P4 | 改一句转写后相关要点标记待核对；高亮在重新生成后仍定位正确 | 已实现、未验证 |
| 22 | 导出 | Core SessionExport.swift、SessionFileExporter/SessionExportUI/SessionDocumentRenderer | P7 | Markdown、纯文本、字幕、PDF 各导出一次，内容与界面一致 | 已实现、未验证 |
| 23 | 资料导入 | DocumentPipeline/DocumentPreparation/DocumentImporter/DocumentRuntime/DocumentRuntimeInstaller/DocumentCheckpoint/DocumentFeatureSetup、Resources/DocumentRuntime/worker.py、Core DocumentStructure/DocumentClassifier/DocumentFeatureAvailability/DocumentNotice/DocumentJobs | P2+P7 | 固定样本（PDF、扫描 PDF、Word、PPT、Excel、图片、Markdown）导入结果与 Mac 参考输出对照 | 已实现、未验证 |
| 24 | 检索与引用 | Embedding/SemanticIndexStore、SemanticRetrievalService、SemanticWorkspacePreparation、Core SemanticRetrieval.swift、MaterialArchive.swift | P2+P7 | 固定 38 问命中率不低于基线；引用跳到正确的页、幻灯片或工作表 | 已实现、未验证 |
| 25 | 历史 | HistoryView.swift、SessionArchive.swift、AppState+Archive.swift、Core SessionSearch/SessionContentSearch/SessionArchivePersistence | P2+P4 | 搜索、筛选、批量删除、可回滚删除 | 已实现、未验证 |
| 26 | 备份往返 | DataBackup.swift、BackupRestoreReview.swift、Core Backup/BackupArchivePolicy/BackupContent/IncrementalBackup | P2+P7 | Mac 导出到 Windows 恢复、Windows 导出到 Mac 恢复各一次，逐项核对系列、会议、高亮、模板 | 已实现、未验证 |
| 27 | 自动备份与恢复点 | DataBackup.swift、Core IncrementalBackup/BackupArchivePolicy | P7 | 每日和升级前自动生成；从恢复点恢复 | 已实现、未验证 |
| 28 | 设置与引导 | SettingsView/SettingsPages/SettingsAdvanced/SettingsGeneralData/SettingsPresenter/SettingsStatusRows/SettingsSearch、QuickSettings、Onboarding、AppPreferences | P4+P6 | 六页设置每项可改且重启后保留；设置搜索可用；四步引导走通 | 已实现、未验证 |
| 29 | 全局快捷键 | HotkeyManager.swift、ShortcutSettingsView.swift、Core ShortcutBinding.swift | P5 | 18 个动作逐个触发；改键后生效；冲突时提示 | 已实现、未验证 |
| 30 | 手机伴侣 | CompanionServer/CompanionBluetooth/CompanionRelay、Mobile/、Core Companion/CompanionBLEProtocol/CompanionCrypto | P7 | 局域网、中转、蓝牙各完成配对、状态同步、命令、断线重连 | 未做 |
| 31 | 中英双语界面 | AppLanguage.swift、Resources/Localization/Localizable.xcstrings、InfoPlist.xcstrings、各 View 文案 | P6 | 英文界面下逐页检查无中文残留、无截断 | 已实现、未验证 |
| 32 | 浅色与深色 | Theme.swift、DesignSystem.swift、LiquidGlass.swift、Stage.swift | P4 | 每页两种外观与 Mac 截图对照 | 已实现、未验证 |
| 33 | 缩放与多显示器 | Stage.swift、LiveWindowController.swift、CueIsland.swift（cueScreen） | P4+P6 | 100%–200% 四档；窗口在缩放不同的显示器间拖动 | 已实现、未验证 |
| 34 | 键盘与读屏 | HitTargets.swift、ControlComponents.swift、各视图 accessibility | P6 | 全程键盘操作；讲述人读出主要控件名称和状态 | 未做 |
| 35 | 安装、更新、回退、卸载 | Updater.swift、installer/、AppPaths.swift、SingleInstance.swift | P7 | 干净的第二台机器全流程一次；后台更新一次；回退一次 | 未做 |
| 36 | 性能 | AppState+Lifecycle、AudioCaptureService、AnswerCoordinator、CueIsland | P8 | 冷启动、空闲内存、会中 CPU 占用、最终句延迟各有实测数 | 已实现、未验证 |
| 37 | 异常恢复 | AppState+Resilience.swift、Core SessionArchivePersistence.swift、AppState+Lifecycle.swift、SingleInstance.swift | P2+P8 | 会中强杀进程后重启，会议记录完整可续；休眠唤醒后继续记录 | 已实现、未验证 |

## 本版不做与旧行归属

按验收记录规则 1，旧 47 行中不在上表的功能归入「本版不做」：

- **Apple Speech、云端 ASR**：本版不做。只交付本机 X-ASR；远程回答 API 不等于云端语音识别。
- **旧 Office 格式转换**（.doc/.xls/.ppt 等）：本版不做。只处理 OOXML、文本、PDF 与图片；旧格式需先转换。
- **Windows ARM64 / GPU**：本版不做。仅交付 x64 CPU 版本，无 ARM64 原生构建，也未证明 GPU 加速收益。
- **共享屏幕时排除回答浮窗**：本版不做等价能力。Windows 不承诺浮窗在共享或录屏中隐藏。
- **眼神提醒**：本版不做功能；`GazeEstimator` 只在 Core 保留逻辑（见验收记录规则 4）。
- **每日 / 升级前自动备份策略**：旧行已由第 27 行「自动备份与恢复点」取代，按第 27 行验收，不再单列。

> 说明：旧行里的「默认不保存原始声音」「本机凭据用 Windows DPAPI」等是贯穿所有功能的约束，不是独立验收行，
> 见 `README.md` 的「数据和隐私」一节。
