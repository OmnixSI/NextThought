# 界面移植对照（VIEW_PARITY）

本文件是 **v3.3.0 的界面认领表**：把 Mac 侧 SwiftUI 视图对应到 Windows 的 `NextThought.Desktop/Views/`，
作为 P4（设计系统与主流程）和 P6（其余界面）的登记表。状态列初始一律「未开始」。

- 基线：Mac 主分支 `5ddb9e5`（v3.3.0，2026-11-17）。
- 规模：主窗口 **约 257 个视图**，行数与视图数来自 v3.3.0 清单；另有 5 类特殊窗口（提词岛、字幕条、
  提词器、区域截图、双屏演讲）不计入 257。
- 对照口径：只要求布局、间距、颜色、圆角、元素有无和顺序一致；字体不同，文字边缘不要求像素一致。
- ★ = v2.3.1 之后新增的界面文件（21 个，见文末列表），没有旧截图基线，对照对象是 v3.3.0 的
  `docs/screenshots/v3/`（`scripts/capture-screenshots.sh` 用演示模式生成）。
- 逐视图行在该文件被 P4/P6 认领时登记（状态：未做 / 已做 / 已对照截图 / 本版不做 + 原因）；
  文件级视图数以登记结果为准，总量仍按约 257 控制。

## 主窗口区域

| 区域 | Swift 文件 | 行数 | 视图数 | 阶段 | 状态 |
|---|---|---|---|---|---|
| 设计系统与通用控件 | DesignSystem、LiquidGlass、Theme、Stage★、HitTargets、ControlComponents★、PaperTextFieldStyle★、UnderlineTabs★、WrappingHStack、OperationFeedback★ | 1,674 | 26 | P4 | 未开始 |
| 外壳与导航 | NextThoughtApp、ContentView、AppNavigationSidebar★ | 770 | 13 | P4 | 未开始 |
| 首页 | HomeView | 424 | 7 | P4 | 未开始 |
| 准备 | PrepareView | 1,347 | 21 | P4 | 未开始 |
| 会中 | LiveSessionView、AssistantView、AssistantLayout、DetachedModules、InsightsPanel、LiveTranslation、SpeakerViews、AutoEndViews、MeetingActionsMenu、MeetingUserNotes★ | 3,531 | 62 | P4/P5 | 未开始 |
| 纪要 | NotesPageView、NotesFormatBar、MeetingNotesEditor★、StructuredNotesView、OutlineView、FishboneView、ViewpointMatrixView、SessionInfoView、SessionExportUI★、KnowledgeReviewSection★ | 3,047 | 29 | P4/P6 | 未开始 |
| 历史 | HistoryView | 1,738 | 7 | P4 | 未开始 |
| 资料库与系列 | WorkspacesView、ProjectLibrarySupport★、GlossaryView、BackupRestoreReview★ | 2,627 | 24 | P6 | 未开始 |
| 设置 | SettingsView、SettingsPages★、SettingsAdvanced★、SettingsGeneralData★、SettingsPresenter★、SettingsStatusRows、QuickSettings★、NotesSettingsView、InsightSettingsView、DocumentModelSettings、AnswerServiceViews、ShortcutSettingsView、SubtitleBarSettingsView、MyVoiceViews | 3,586 | 57 | P6 | 未开始 |
| 引导 | Onboarding、OnboardingSampleMeeting | 1,163 | 9 | P6 | 未开始 |
| 合计 | — | 19,907 | 约 257 | — | — |

## 特殊窗口（不计入 257）

| 窗口 | Mac 源码 | 规模 / 状态 | 阶段 | 状态 |
|---|---|---|---|---|
| 提词岛 | CueIsland.swift、AppState+Launcher.swift | 1,390 行；待机、听到问题、回答要点、悬停全文、会议面板、讲稿要点、教练提醒、自动结束倒计时 | P5 | 未开始 |
| 字幕条 | SubtitleBarController/SubtitleBarView.swift | Core SubtitleBar 295 行；显示、拖动、穿透、录屏隐藏 | P5 | 未开始 |
| 提词器浮窗 | TeleprompterController.swift | 640×300，最小 360×160；透明度、字号、隐藏背景、穿透 | P5 | 未开始 |
| 区域截图 | RegionScreenshotPicker.swift | 全屏遮罩拖矩形，返回物理像素，Esc 取消 | P5 | 未开始 |
| 双屏演讲 | PresenterMode.swift、ReadingScrollMonitor.swift | 外接屏共享、主窗隐藏、按页截图匹配讲稿 | P5 | 未开始 |

## v2.3.1 之后新增的界面文件（21 个）

ControlComponents、CueIsland、KnowledgeReviewSection、MeetingNotesEditor、MeetingUserNotes、OperationFeedback、
PaperTextFieldStyle、PresenterMode、QuickSettings、SceneSettings、SettingsAdvanced、SettingsGeneralData、
SettingsPages、SettingsPresenter、SettingsSearch、Stage、UnderlineTabs、BackupRestoreReview、
ProjectLibrarySupport、SessionExportUI、LiveCoach。

这 21 个文件里的视图在 v2.3.1 中全部不存在，没有旧截图基线；对照对象是 v3.3.0 的
`docs/screenshots/v3/`。逐视图行在该文件被认领时登记；文件级视图数以登记结果为准。

## 使用规则

1. 先移植 `DemoMode.swift`（596 行）和 `DemoFilm.swift`，接 `Platform.Stub`；没有它，P4 无法在 Mac 或
   Windows 上打开任意页面状态做对照。
2. 每个 Swift 文件对应 `NextThought.Desktop/Views/` 下同名 `.axaml` + `.axaml.cs`；文件内私有子视图放进同名子目录。
3. 对照只要求布局、间距、颜色、圆角、元素有无和顺序一致；字体不同，文字边缘不要求像素一致。
4. 特殊窗口的五类先过 S0 的窗口验证，再谈视觉；S0 不过先走退路。
5. 全部状态为「已做」且无「本版不做」遗漏，P6 才算退出。

## 附：v2.3.1 → v3.3.0 App 文件差集

新增 41 个文件：AppState+Launcher、AppState+Lifecycle、ArchiveUXSelfTest、BackupRestoreReview、
CoachSummaryStore、ControlComponents、CueIsland、DemoFilm、KnowledgeReviewSection、LargePDFSelfTest、LiveCoach、
LiveSessionAcceptanceComponentSamples、LiveSessionAcceptanceScenario、LiveSessionAcceptanceSelfTest、
MeetingContentSelfTest、MeetingNotesEditor、MeetingUserNotes、NotesRegeneration、OperationFeedback、
PaperTextFieldStyle、PreparationActions、PreparationPreview、PresenterMode、ProjectLibrarySupport、QuickSettings、
ReadingScrollMonitor、SceneSettings、ScrollFollowObserver、SessionDocumentRenderer、SessionExportUI、
SessionFileExporter、SettingsAdvanced、SettingsGeneralData、SettingsNavigationSelfTest、SettingsPages、
SettingsPresenter、SettingsSearch、SingleInstance、SlideNotesCache、Stage、UnderlineTabs。

删除 1 个：OnboardingMaterials.swift。

其中 21 个是界面文件（见上文列表）；其余为应用状态、自检、导出等支撑文件，随对应阶段一起移植。
