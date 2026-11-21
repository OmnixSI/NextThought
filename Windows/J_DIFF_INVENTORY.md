# Mac 3.4.0 差异文件清单（任务书 §7 / J8）

由 Lead 用 `git diff --name-status 5ddb9e5 f015e8c6 -- Sources Tests Resources` 生成；对应任务书 §7 的 J 组。J8 的完成条件：本表每个文件在 `FIX_TASKS.md` 里有一行且状态不是「未做」。

- 变更文件总数：**69**
- 涉及新增/修改/删除：new=8、modified=61、deleted=0

| 文件 | 状态 | +行 | -行 | 归属 J 项 |
|---|---|---:|---:|---|
| `Resources/Info.plist` | M | 2 | 2 | J8 |
| `Resources/Localization/Localizable.xcstrings` | M | 310 | 0 | J8 |
| `Sources/NextThought/AnswerServiceViews.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/AppLanguage.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/AppNavigationSidebar.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/AppState+Live.swift` | M | 1 | 0 | J8 |
| `Sources/NextThought/AppState+Notes.swift` | M | 28 | 0 | J6 |
| `Sources/NextThought/AppState.swift` | M | 13 | 5 | J2/J4 |
| `Sources/NextThought/AssistantView.swift` | M | 37 | 37 | J8 |
| `Sources/NextThought/AutoEndViews.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/BackupFixtureSelfTest.swift` | A | 170 | 0 | J8 |
| `Sources/NextThought/ContentView.swift` | M | 6 | 4 | J8 |
| `Sources/NextThought/ControlComponents.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/CueIsland.swift` | M | 1 | 0 | J8 |
| `Sources/NextThought/DemoMode.swift` | M | 44 | 1 | J8 |
| `Sources/NextThought/DesignSystem.swift` | M | 84 | 10 | J8 |
| `Sources/NextThought/DetachedModules.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/DocumentModelSettings.swift` | M | 8 | 8 | J8 |
| `Sources/NextThought/FishboneView.swift` | M | 5 | 5 | J8 |
| `Sources/NextThought/GlossaryView.swift` | M | 11 | 11 | J8 |
| `Sources/NextThought/HistoryView.swift` | M | 19 | 18 | J4 |
| `Sources/NextThought/HitTargets.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/HomeView.swift` | M | 13 | 10 | J3/J4 |
| `Sources/NextThought/InsightsPanel.swift` | M | 14 | 14 | J8 |
| `Sources/NextThought/LiquidGlass.swift` | M | 4 | 4 | J8 |
| `Sources/NextThought/LiveNotesEngine.swift` | A | 179 | 0 | J1 |
| `Sources/NextThought/LiveSessionAcceptanceSelfTest.swift` | M | 51 | 0 | J8 |
| `Sources/NextThought/LiveSessionView.swift` | M | 162 | 34 | J8 |
| `Sources/NextThought/LiveTranslation.swift` | M | 6 | 3 | J8 |
| `Sources/NextThought/LiveWindowController.swift` | M | 6 | 3 | J8 |
| `Sources/NextThought/MeetingStatsViews.swift` | A | 77 | 0 | J3 |
| `Sources/NextThought/Models.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/NextThoughtApp.swift` | M | 67 | 3 | J7 |
| `Sources/NextThought/NotesPageView.swift` | M | 87 | 29 | J6 |
| `Sources/NextThought/Onboarding.swift` | M | 6 | 6 | J8 |
| `Sources/NextThought/OutlineView.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/PaperTextFieldStyle.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/PrepareView.swift` | M | 74 | 7 | J2 |
| `Sources/NextThought/PresenterMode.swift` | M | 28 | 3 | J7 |
| `Sources/NextThought/QuickSettings.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/RegionScreenshotPicker.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/ScreenRegionSheet.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/SelfTest.swift` | M | 37 | 0 | J8 |
| `Sources/NextThought/SessionExportUI.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/SessionInfoView.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/SettingsAdvanced.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/SettingsGeneralData.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/SettingsPages.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/SettingsPresenter.swift` | M | 6 | 6 | J8 |
| `Sources/NextThought/SettingsView.swift` | M | 33 | 33 | J8 |
| `Sources/NextThought/ShortcutSettingsView.swift` | M | 3 | 3 | J8 |
| `Sources/NextThought/SpeakerID.swift` | M | 67 | 2 | J6 |
| `Sources/NextThought/SpeakerViews.swift` | M | 63 | 8 | J6 |
| `Sources/NextThought/Stage.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/StructuredNotesView.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/SubtitleBarSettingsView.swift` | M | 1 | 1 | J8 |
| `Sources/NextThought/SubtitleBarView.swift` | M | 6 | 6 | J8 |
| `Sources/NextThought/TeleprompterController.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/Theme.swift` | M | 6 | 3 | J8 |
| `Sources/NextThought/ViewpointMatrixView.swift` | M | 2 | 2 | J8 |
| `Sources/NextThought/WorkspacesView.swift` | M | 67 | 64 | J4 |
| `Sources/NextThought/ZipformerStreamingTranscriber.swift` | M | 46 | 2 | J5 |
| `Sources/NextThoughtCore/LiveNotes.swift` | A | 192 | 0 | J1 |
| `Sources/NextThoughtCore/MeetingStats.swift` | A | 44 | 0 | J3 |
| `Sources/NextThoughtCore/SpeakerClustering.swift` | M | 27 | 8 | J5 |
| `Sources/NextThoughtCore/UtteranceText.swift` | A | 79 | 0 | J5 |
| `Tests/NextThoughtCoreTests/LiveNotesTests.swift` | A | 82 | 0 | J9 |
| `Tests/NextThoughtCoreTests/SpeakerClusteringTests.swift` | M | 25 | 0 | J9 |
| `Tests/NextThoughtCoreTests/UtteranceTextTests.swift` | A | 45 | 0 | J9 |
