# J8 · Mac 3.4.0 变更文件 → Windows 状态盘点

- 基准：`git diff --name-status 5ddb9e5 f015e8c6 -- Sources Tests Resources`，共 **69** 个文件（new=8，modified=61）。
- 方法：逐个文件取 3.4.0 的实际变更（`git diff --unified=0`），排除纯 `.zoomed(...)` 字号换算后，核对 Windows 是否有对应实现/测试（文件名、方法名、单测与自检均作为证据）。
- 结论分布：**已实现 55**、**等效已实现 5**、**部分/在途 8**、**不适用 1**。逐行见下表；「缺口」列即 FIX_TASKS 可用的行。

## 表：Swift 文件 → Windows 状态

| # | Swift 文件（3.4.0 改动摘要） | Windows 状态 | 证据 / 缺口 | 归属 |
|---:|---|---|---|---|
| 1 | `Resources/Info.plist`（版本 3.4.0 / 3400） | **不适用** | Windows 版本由 `NextThought.Desktop.csproj` 的 `Version`（0.5.145）与 `app.manifest` 管理；无 Info.plist | J8 |
| 2 | `Resources/Localization/Localizable.xcstrings`（+310 条） | **已实现** | `Core/UiTextMac.g.cs` 由 xcstrings 生成；xcstrings 2638 keys ↔ `UiTextMac` `Count = 2638`；`LocalizationSourceScan/Coverage`、`WordingTable` 测试绿 | G8/G10 |
| 3 | `AnswerServiceViews.swift`（图标字号 `.zoomed`） | **已实现** | J7 `TextZoomService`（`Desktop/TextZoomService.cs`，六档、`ApplyAll` 遍历缩放） | J7 |
| 4 | `AppLanguage.swift`（字号 `.zoomed`） | **已实现** | 同上 | J7 |
| 5 | `AppNavigationSidebar.swift`（字号/图标尺寸） | **已实现** | 同上；导航胶囊为 E1 `MainWindow` 实现 | J7 |
| 6 | `AppState+Live.swift`（`liveNotes.tick`） | **已实现** | J1 `AppState.LiveNotes.cs`：`LiveNotesTickAsync` 每 2 s、`LiveNotesSchedule.IsDue` 决定 45 s 节奏 | J1 |
| 7 | `AppState+Notes.swift`（`editLine` 逐句改正） | **已实现** | J6 `AppState.TranscriptEdits.cs`（`EditTranscriptLineAsync`/`SessionSpeakerEdits`）+ `EditTextDialog` | J6 |
| 8 | `AppState.swift`（LiveNotes 引擎、`meetingFeaturesRevision`、开始时落地四项开关） | **已实现** | J1 `AppState.LiveNotes.cs`；J2 `AppState.Prepare.cs`（`MeetingFeatureChoices`、`ActiveMeetingFeatures`、`ApplyActiveMeetingFeatures`）+ `AppState.Live.cs` 门控 | J1/J2 |
| 9 | `AssistantView.swift`（字号 `.zoomed`） | **已实现** | J7 | J7 |
| 10 | `AutoEndViews.swift`（字号） | **已实现** | J7 | J7 |
| 11 | `BackupFixtureSelfTest.swift`（+170：生成四场景 fixture 的自测入口） | **等效已实现** | Windows 用测试侧 fixture 往返替代：`MacBackupFixturesTests`、`MacRevisionParityTests`、`MacBackupParityTests` + `RunBackupManifestCheckAsync`、`verify.ps1` 的 Mac fixture 步骤；**未提供 `--self-test=backup-fixtures` 生成器入口** | J9 |
| 12 | `ContentView.swift`（动态字号环境、共享测试窗口字号） | **已实现** | `TextZoomService.ApplyAll` 覆盖页面；共享窗口文案已本地化（E12） | J7 |
| 13 | `ControlComponents.swift`（按钮/标签字号随 zoom） | **已实现** | `TextZoomService` 缩放 Button/TextBlock；`Controls.axaml` 尺寸 token | J7 |
| 14 | `CueIsland.swift`（`PresenterMode.meetingStarted()`） | **部分/在途** | 提词岛由 C5–C9 重做（`CueIslandWindow`），presenter 会话状态随 §6.5/提词浮窗任务；`PresenterCheck.cs` 已有检查，`presenterPageWanted` 正在 `MainWindow.axaml.cs` 接入 | 提词岛 owner |
| 15 | `DemoMode.swift`（+44：`DEMO_LIVE_NOTES` 演示纪要、`DEMO_CHURN` 渲染计数、`DEMO_VOICES` 现场多人 demo） | **部分** | ✅ 演示纪要已接：`AppState.Demo.cs` 在 `live-notes` 阶段调用 `EnsureDemoLiveNotes()`（本次补）；❌ `NEXTTHOUGHT_DEMO_CHURN` 渲染计数（见 #55）、`NEXTTHOUGHT_DEMO_VOICES=1`（线下多人+`voiceNames` 演示数据）未移植 | J8/D1/J6 |
| 16 | `DesignSystem.swift`（+84：`TextZoom`、`Typo.zoomed`、字号随 zoom） | **已实现** | J7 `Desktop/TextZoomService.cs`（六档 0.9–1.45、`CanGrow/CanShrink/Grow/Shrink/Reset`、主题字体统一缩放） | J7 |
| 17 | `DetachedModules.swift`（字号） | **已实现** | J7 | J7 |
| 18 | `DocumentModelSettings.swift`（字号） | **已实现** | J7 | J7 |
| 19 | `FishboneView.swift`（字号） | **已实现** | J7；`Views/FishboneView.cs` 存在 | J7 |
| 20 | `GlossaryView.swift`（+11：字号、查找框） | **已实现** | J7；术语窗口由 glossary owner 重做（`GlossaryWindow`），查找/添加自检通过 | J7/J6 |
| 21 | `HistoryView.swift`（+19：字号、列表宽 `270*min(factor,1.3)`） | **部分** | 字号 ✅ J7；`HistoryView.axaml` 会话列宽仍固定 `270`，未随 TextZoom 缩放（Mac 3.4.0 会加宽到最多 1.3 倍） | J7/E1 |
| 22 | `HitTargets.swift`（字号） | **已实现** | J7；`HitMin/HitIcon/HitRow` token 在 `Theme.axaml` | J7 |
| 23 | `HomeView.swift`（+13：用了多少、系列行统计/箭头/继续一个系列文案） | **已实现** | J3 `MeetingStats` + `Views/Home/HomeStats.axaml`；J4 `ContinueSeries` 统计行/「继续一个系列」；本会话 E1 补 updatedAt 排序与动效 | J3/J4/E1 |
| 24 | `InsightsPanel.swift`（字号、屏幕与讲稿） | **已实现** | J7；`InsightsPanel.axaml`（E9 重做） | J7/E9 |
| 25 | `LiquidGlass.swift`（字号） | **已实现** | J7；组件在 `DesignSystem.axaml`/`Controls` | J7/D6 |
| 26 | `LiveNotesEngine.swift`（+179 新文件） | **已实现** | J1 `AppState.LiveNotes.cs`（45 s 排程、主题/决定/待办/新内容标记、失败保留旧稿）+ Core `LiveNotes.cs`（prompt/模型）+ `LiveNotesTests` | J1 |
| 27 | `LiveSessionAcceptanceSelfTest.swift`（+51：`LiveRenderCount` 渲染计数与 `churn` 自测） | **部分** | ❌ 未移植 `LiveRenderCount`/`--demo churn` 计数；会中页由 §6.5 重做（`LiveView`）时再补；其余验收自测（pause/flush 等）已有 | E4/J8 |
| 28 | `LiveSessionView.swift`（+162：会中页动态字号、结束按钮首次点击、more 菜单/实时纪要切换、`byUser` 窗口模式） | **部分/在途** | §6.5 会中页由 E4 逐步重做（顶部条/右栏/底栏已提交）；本次变更中的 `实时纪要`开关与`会中窗口`选择已存在（`LiveView.axaml` more 菜单），`LiveRenderCount` 计数缺 | E4 |
| 29 | `LiveTranslation.swift`（`beginMeeting(allowed:)`） | **已实现** | J2：`ApplyActiveMeetingFeatures()` 在开关关闭时置 `LiveTranslationEnabled=false`（不写偏好）；`AppState.LiveTranslation.cs` 保留 Mac remembered-choice 语义 | J2 |
| 30 | `LiveWindowController.swift`（`set(_:byUser:)`） | **部分/在途** | 会中窗口模式 `LiveView` 已有选择；`byUser → PresenterMode.pageModeChosen` 联动随 presenter 任务，`presenterPageWanted` 正在接入 | §6.5 presenter |
| 31 | `MeetingStatsViews.swift`（+77：`durationText`/`SeriesLine`） | **已实现** | J3 `Core/MeetingStats.cs` + `MeetingStatsText.cs` + `Views/Home/HomeStats.axaml`；`MeetingStatsTests`、all-scenarios 手算断言 | J3 |
| 32 | `Models.swift`（library 标题→「系列」） | **已实现** | J4：导航/系列页统一「系列」（`MainWindow.axaml`、`SeriesView`），`HomeStage4Tests.NoInterfaceTextUsesTheOldSeriesWord` 守门 | J4 |
| 33 | `NextThoughtApp.swift`（+67：`⌘+ / ⌘− / ⌘0` 菜单项） | **等效已实现** | J7：Windows 无应用菜单栏，`MainWindow.axaml.cs` `OnTextZoomKeyDown` 用 `Ctrl + +/−/0` 调 `TextZoomService.Grow/Shrink/Reset`（与 Mac 六档一致） | J7 |
| 34 | `NotesPageView.swift`（+87：`DEMO_MEETING_TAB`、逐句编辑 `LineEditSheet`、页签/大纲字号） | **已实现** | J6：`NotesView.axaml` 页签 + `AppState.TranscriptEdits`/`EditTextDialog` 逐句改正；`DEMO_MEETING_TAB` 等价由 `RunScreenshotsAsync`/`ApplyDemoNotesTab` 覆盖 | J6 |
| 35 | `Onboarding.swift`（+6：字号、示例提词岛字号） | **已实现** | E3 本会话重做引导四步（`AppState.Onboarding.cs`、`Views/OnboardingView.axaml`），字号走 J7 | E3/J7 |
| 36 | `OutlineView.swift`（字号） | **已实现** | J7 | J7 |
| 37 | `PaperTextFieldStyle.swift`（字号） | **已实现** | J7 | J7 |
| 38 | `PrepareView.swift`（+74：这场用什么/FeaturePicker/FeatureReset） | **已实现** | J2+E2 本会话提交：五功能开关、每系列沿用、`FeatureReset`、`PrepareView.axaml` 重做；自检 `--self-test=prepare` | J2/E2 |
| 39 | `PresenterMode.swift`（+28：`pageWanted`/`pageModeChosen`/`meetingStarted`） | **部分/在途** | `PresenterCheck.cs` 与 `MainWindow.axaml.cs` 的 `presenterPageWanted` 已有骨架，完整双屏 presenter 行为随 §6.5/提词浮窗任务 | §6.5 presenter |
| 40 | `QuickSettings.swift`（字号） | **已实现** | J7；`QuickSettingsPanel.axaml` | J7 |
| 41 | `RegionScreenshotPicker.swift`（字号） | **已实现** | J7；截图提问窗口 E8 已有 | J7 |
| 42 | `ScreenRegionSheet.swift`（字号） | **已实现** | J7；`RegionCaptureWindow` | J7 |
| 43 | `SelfTest.swift`（+37：CLI `--self-test live-notes` / `backup-fixtures`） | **部分** | Windows 自检走 `--self-test=<name>`（`SelfTestRegistry`，无独立 CLI 进程）；`live-notes` 的“用真实回答服务喂转写→看纪要”通路未作为自检入口（J1 引擎与 demo 纪要已有），`backup-fixtures` 见 #11 | J8/J9 |
| 44 | `SessionExportUI.swift`（字号） | **已实现** | J7；导出单 E21 已重做 | J7/E21 |
| 45 | `SessionInfoView.swift`（字号） | **已实现** | J7 | J7 |
| 46 | `SettingsAdvanced.swift`（字号） | **已实现** | J7；设置页 E10 已重做 | J7/E10 |
| 47 | `SettingsGeneralData.swift`（字号、起步清单字号） | **已实现** | J7 + E3 `SettingsSetupChecklist`（设置→通用清单卡） | J7/E3 |
| 48 | `SettingsPages.swift`（字号） | **已实现** | J7 | J7 |
| 49 | `SettingsPresenter.swift`（字号、日历/校准文案） | **已实现** | J7；`SettingsView.axaml` presenter/coach 分组 | J7 |
| 50 | `SettingsView.swift`（+33：字号、行图标/chevron 尺寸） | **已实现** | J7；设置页 E10 结构与 `SettingsStructureTests` | J7/E10 |
| 51 | `ShortcutSettingsView.swift`（字号） | **已实现** | J7；快捷键设置 E10 | J7 |
| 52 | `SpeakerID.swift`（+67：`LineSpeaker`、`assignLine`、`beginMeeting(wanted:)`） | **已实现** | J6：`AppState.TranscriptEdits.AssignTranscriptLineSpeakerAsync` + `LineSpeaker` + `SessionSpeakerEdits.AssignLine`；声纹启用/可用门控在 `AppState.Prepare.cs`（J2） | J6/J2 |
| 53 | `SpeakerViews.swift`（+63：`asksForMe`、named/voiceNumbers 展示） | **已实现** | `SpeakerLabels.voiceNumbers/named`（J6，`AppState.Speakers.cs`）+ `AskForMe` 类提示在 `AppState.Speakers`/`TranscriptRow`；`SpeakerTests`、`SpeakerClusteringParityTests` | J6 |
| 54 | `Stage.swift`（字号） | **已实现** | J7；InkWash 在 `Views/Controls/InkWash.cs`（D6） | J7/D6 |
| 55 | `StructuredNotesView.swift`（字号） | **已实现** | J7；结构化纪要 E17/E19 | J7 |
| 56 | `SubtitleBarSettingsView.swift`（字号） | **已实现** | J7；字幕条设置 E11 | J7 |
| 57 | `SubtitleBarView.swift`（+6：字号随设置/缩放） | **已实现** | J7 + E11 `SubtitleBarWindow.axaml.cs` 用 `TextZoomService.Scaled` | J7/E11 |
| 58 | `TeleprompterController.swift`（`liveWindow.set(byUser:)`） | **部分/在途** | 提词浮窗 E22/E29 已有窗口与字号；`byUser` presenter 联动随 #30 | §6.5 presenter |
| 59 | `Theme.swift`（+6：zoom 感知、`.zoomed` 辅助） | **已实现** | J7 `TextZoomService`；`Theme.axaml` 的字体资源由 service 统一缩放 | J7 |
| 60 | `ViewpointMatrixView.swift`（字号） | **已实现** | J7；`ViewpointMatrixView.cs` 存在 | J7 |
| 61 | `WorkspacesView.swift`（+67：字号、「系列」、pin、`SceneTag`+`SeriesStatsLine`、开下一场） | **部分** | ✅ 系列页 E12 已重做（`SeriesView.axaml`：系列标题/统计/「开下一场」/搜索）；❌ **系列置顶 pin**（`WorkspacesView` 3.4.0 新增）未移植 | J4 (d-parity) |
| 62 | `ZipformerStreamingTranscriber.swift`（+46：`committedThroughDate`/`PendingUtterance`/watch） | **已实现** | J5 `Core/UtteranceText.cs` + `StreamingUtteranceTrackerTests`；`Platform/SpeechRecognizerSession.cs` 引用 tracker；`UtteranceTextTests` 通过 | J5 |
| 63 | `Sources/NextThoughtCore/LiveNotes.swift`（+192 新文件） | **已实现** | J1 `Core/LiveNotes.cs`（模型/prompt/schedule）+ `LiveNotesTests`（82 行对译） | J1/J9 |
| 64 | `Sources/NextThoughtCore/MeetingStats.swift`（+44 新文件） | **已实现** | J3 `Core/MeetingStats.cs` + `MeetingStatsTests` + all-scenarios 手算断言 | J3/J9 |
| 65 | `Sources/NextThoughtCore/SpeakerClustering.swift`（`voiceNumbers`/`named`） | **已实现** | J5 `Core/SpeakerClustering.cs` + `SpeakerClusteringTests`/`SpeakerClusteringParityTests`（含“手动命名的人即使只说一句也显示”） | J5/J9 |
| 66 | `Sources/NextThoughtCore/UtteranceText.swift`（+79 新文件） | **已实现** | J5 `Core/UtteranceText.cs` + `UtteranceTextTests`；平台侧 `SpeechRecognizerSession` 已接 | J5/J9 |
| 67 | `Tests/NextThoughtCoreTests/LiveNotesTests.swift`（+82） | **已实现** | J9 `Windows/tests/NextThought.Core.Tests/LiveNotesTests.cs` | J9 |
| 68 | `Tests/NextThoughtCoreTests/SpeakerClusteringTests.swift`（+25） | **已实现** | J9 `SpeakerClusteringTests.cs`（含新增 named 用例） | J9 |
| 69 | `Tests/NextThoughtCoreTests/UtteranceTextTests.swift`（+45） | **已实现** | J9 `UtteranceTextTests.cs` + `StreamingUtteranceTrackerTests.cs` | J9 |

## 可直接写进 FIX_TASKS 的缺口（8 条 + 1 条本次已补）

| 优先级 | 缺口 | 具体位置 / 验收 |
|---|---|---|
| ✅ 本次补 | `DemoMode NEXTTHOUGHT_DEMO_LIVE_NOTES` 演示纪要未接 | `AppState.Demo.cs` 在 `live-notes` 阶段调 `EnsureDemoLiveNotes()`（已改；`EnsureDemoLiveNotes`/`InstallDemoLiveNotes` 原本无调用方） |
| P1 | `NEXTTHOUGHT_DEMO_VOICES=1` 线下多人演示数据 | `DemoScript` 增加 12 句、3 个 voice、`voiceNames={"1":"老师"}`、`audioSource="线下 · 麦克风"` 的演示会议；供发言人截图/走查（Mac DemoMode.swift） |
| P1 | 系列置顶 pin | `WorkspaceSeries`/`SeriesView` 增加 pin 状态与列表置顶（Mac `WorkspacesView.pinned`），备份往返保留 |
| P2 | 会中页渲染计数 `LiveRenderCount` + churn 自检 | `LiveView` 各重部件加计数、`--demo churn=<file>` 等价入口；对应 Mac `LiveSessionAcceptanceSelfTest` |
| P2 | `--self-test live-notes` / `backup-fixtures` 入口 | `SelfTestRegistry` 增加 live-notes（用转写喂 LiveNotes 引擎）与 backup-fixtures（生成四场景 fixture）两个 app 自检；无 CLI 进程，可用 `--self-test=` 形式 |
| P2 | PresenterMode `pageWanted` / `LiveWindowController.set(byUser:)` 完整联动 | 双屏演示：用户把页调回后，显示器变化不再重启 presenter；`PresenterCheck` 扩展断言 |
| P3 | 历史列表宽随文字大小缩放 | `HistoryView.axaml` 270 → `270 * min(TextZoomService.Factor, 1.3)`（Mac `HistoryView` 3.4.0） |
| P3 | `DEMO_CHURN` 环境变量 | 并入上条 churn 自检（仅 walkthrough 用，不是发布入口） |

## 说明

- 61 个 modified 文件里约 45 个只做 `.zoomed(...)` 字号换算（Mac 3.4.0 的 TextZoom 统一化），Windows 由 J7 `TextZoomService`（六档 + `ApplyAll` + 每窗口 `Scaled`）统一实现，不再逐文件列缺口。
- 无「未做」的文件；8 条缺口均为 3.4.0 新增的演示数据/自检/双屏细节，不影响页面逐元素对照。
- 本表不修改 Lead 的跟踪文件；FIX_TASKS 由 Lead 写入。
- 顺带发现（非本表内）：`--demo --self-test=demo` 现在是红的（`RunDemoCheckAsync` 期望 demo 已载入 lines≥10/history≥3/有回答，实际 6/0/false）——`--demo` 启动不再自动 `LoadDemo()`（E1 `6482e532` 后 demo 历史只在截图批内保留），`RunDemoCheckAsync` 应自己先 `state.LoadDemo()` 或在 `--demo` 启动时统一载入；属 D1/E1 自检入口，未改。
