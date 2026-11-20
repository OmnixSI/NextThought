# 设置对照（SETTINGS_PARITY）

Mac 六页设置共 **113** 个偏好键。本表由 `Windows/scripts/settings_parity.py` 从 Swift 源码（UserDefaults/AppStorage/SceneSettings key/`Sources/NextThoughtCore/Backup.swift` 的 exportedKeys 并集）提取，当前 **113 / 113**；其中「已实现、未验证」**101** 项、「本版不做」**12** 项。

复算命令（在仓库根目录运行）：

```bash
python Windows/scripts/settings_parity.py          # 校验键集、默认值与 Swift 证据；打印当前数字
python Windows/scripts/settings_parity.py --write  # 重新生成 MacPreferences.g.cs 与本表
```

测试锁定这些数字：`Windows/tests/NextThought.Core.Tests/SettingsParityDefaultsTests.cs` 逐个断言类型、默认值与取值范围；`Windows/tests/NextThought.Tests/SettingsParityTableTests.cs` 断言本表 113 行与注册表一致，并断言 `SettingsStore` 写出的 settings.json 使用 Mac 键名且默认值逐个相同。

| 键名 | 默认类型 | 默认值 | Mac 设置视图 | Windows 状态 |
|---|---|---|---|---|
| `AppleLanguages` | Array | nil（删除即跟随系统） | 设置 → 通用 → 语言（AppLanguage.swift LanguageSettingsSection） | 本版不做｜Mac 用系统级 AppleLanguages 覆盖界面语言并重启生效；Windows 语言设置按 G8 用独立键并即时生效，机制不同（平台差异，见 FIX_TASKS G8） |
| `activeAPIPresetID` | String | nil（未设置） | 设置 → 回答与资料 → 回答服务·预设（AnswerServiceViews.swift APIPresetManager） | 已实现、未验证｜MacPreferenceMap↔AppSettings.ActiveApiPresetId（E27：APIPresetLogic.Select/Add/Delete 维护，选中的预设地址/模型写回 apiBaseURL/apiModel）；SettingsStore 读写 |
| `activeWorkspaceID` | String | nil（取第一个系列） | 资料夹列表·当前系列（WorkspacesView.swift WorkspaceList） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 资料夹当前系列未持久化（AppState 只在内存里选），无读写行为；接入任务：E18 |
| `advancedSettingsEnabled` | Bool | false | 内部派生（AppState.swift simplifiedInterface 取反；无设置控件） | 本版不做｜Mac 内部派生键：由 simplifiedInterface 取反写回，没有设置控件，读它只是初始化同一值；Windows 没有简化界面模式，本版不做 |
| `answerBackend` | String | "openAICompatible"（未装 Codex 时；装了为 "codexAppServer"） | 设置 → 回答与资料 → 回答服务（SettingsPages.swift AnswerServiceChooser；AnswerServiceViews.swift CodexEngineSection） | 已实现、未验证｜MacPreferenceMap↔AppSettings.UseCodex（openAICompatible=false，codexAppServer/codexExec=true）；SettingsStore 读写；行为：AppState.Live 按 UseCodex 选择回答服务；缺口：Windows 只区分 API / Codex，不区分 codexAppServer 与 codexExec |
| `answerGloss` | Bool | true | 快速设置 / 会中顶栏（QuickSettings.swift、LiveSessionView.swift） | 已实现、未验证｜AppSettings.AnswerGloss（默认已改为 true）；SettingsStore 读写；行为：AppState.StructuredNotes/AnswerPromptBuilder 生成中文对照 |
| `answerLanguage` | String | "auto" | 快速设置 / 会中顶栏（QuickSettings.swift、LiveSessionView.swift） | 已实现、未验证｜AppSettings.AnswerLanguage；SettingsStore 读写；行为：AnswerPromptBuilder/AnswerLanguage 生成回答语言字段 |
| `answerPreferences` | String | "" | 旧版单场景背景（WorkspaceStore.swift migrateLegacyContext；现为系列文件） | 本版不做｜Mac 旧版单一场景迁移键；Windows 按系列文件直接读写（B1/A7），不做旧 UserDefaults 场景迁移，本版不做 |
| `answerStyle` | String | "concise" | 设置 → 场景「回答」（SettingsPages.swift SceneRow）；旧键见 WorkspaceStore.swift | 本版不做｜Mac 旧版单场景键；Windows 的回答长度按系列文件与场景设置（AppSettings.SceneDefaults、SceneSettingRow）存储，本版不做旧键 |
| `apiBaseURL` | String | "https://api.openai.com/v1" | 设置 → 回答与资料 → 回答服务（AnswerServiceViews.swift APIConnectView） | 已实现、未验证｜MacPreferenceMap↔AppSettings.BaseUrl；SettingsStore 读写；行为：ChatCompletionClient/AnswerStreaming 使用 |
| `apiModel` | String | "" | 设置 → 回答与资料 → 回答服务（AnswerServiceViews.swift APIConnectView） | 已实现、未验证｜MacPreferenceMap↔AppSettings.Model；SettingsStore 读写；行为：AppState.Live → ChatCompletionClient |
| `apiPresets` | Array | [] | 设置 → 高级 → 回答服务·预设（AnswerServiceViews.swift APIPresetManager） | 已实现、未验证｜MacPreferenceMap↔AppSettings.ApiPresets（E27：APIPreset 数组，Mac 字段名 id/name/baseURL/model/effort/keyAccount；APIPresetLogic 增删改选 + 首次运行迁移）；SettingsStore 读写；数据形状与 Mac 相同（数组）；Mac 备份里是 Data，Windows plist 导入/导出按数组处理（已有数据格式差异记录） |
| `apiVisionEnabled` | Bool | false | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 回答请求尚未按该开关附加截图（E4/E13 截图提问独立），无行为消费者；接入任务：E4 |
| `appearance` | String | "system" | 设置 → 通用 → 语言与外观（SettingsGeneralData.swift GeneralSettings） | 已实现、未验证｜AppSettings.Appearance；SettingsStore 读写；行为：AppState 应用浅色/深色/跟随系统 |
| `assistantLayout` | Dict | AssistantLayout.standard（两栏；split 0.44） | 助手页模块布局（AssistantLayout.swift；AssistantView.swift 拖拽） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 AssistantLayout.standard，JSON 原样保存）；缺口：Windows 助手页无模块化布局（只有 ContextSidebarHidden），无行为消费者；接入任务：E15 |
| `autoApplyLessons` | Bool | true | 旧键→autoCollectKnowledgeCandidates（Backup.swift SettingsSnapshot.valuesForRestore） | 已实现、未验证｜SettingsSnapshot.ValuesForRestore（Backup.cs 与 Mac 相同的重命名迁移：autoApplyLessons → autoCollectKnowledgeCandidates）；默认 true 与 Mac 一致 |
| `autoBackup` | Bool | true | 设置 → 高级 → 数据与维护（SettingsGeneralData.swift DataMaintenanceAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 尚未按该开关在会后自动创建恢复点（BackupService 只有手动/导入导出路径）；接入任务：E30 |
| `autoCollectKnowledgeCandidates` | Bool | true | 设置 → 高级 → 纪要（NotesSettingsView.swift NotesAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true；含 autoApplyLessons 迁移）；缺口：Windows 纪要整理尚未按该开关收集经验/答法候选；接入任务：E16 |
| `autoSummarize` | Bool | true | 设置 → 纪要 → 会后整理（NotesSettingsView.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.AutoSummarize；SettingsStore 读写；行为：AppState.StructuredNotes 会议结束后自动整理 |
| `automaticAnswers` | Bool | true | 会中顶栏 / 场景「会中」（AppState.swift；无设置页控件） | 已实现、未验证｜AppSettings.AutomaticAnswers（默认改为 true）；SettingsStore 读写；行为：AppState.Live 自动回答开关（每场会开始时按场景覆盖） |
| `automaticUpdateChecks` | Bool | true | 设置 → 通用 → 更新（SettingsView.swift UpdateSettings） | 已实现、未验证｜MacPreferenceMap↔AppSettings.AutoCheckUpdates；SettingsStore 读写；行为：AppState.Update 启动后/每 6 小时检查 |
| `capturePrivacy` | Bool | true | 设置 → 提词与教练 → 屏幕共享（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.CapturePrivacy；SettingsStore 读写；行为：WindowCaptureHiding 每秒对全部窗口施加/撤销 WDA_EXCLUDEFROMCAPTURE |
| `cloudConsent` | Bool | false | 设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 云端识别界面没有单独的同意开关（设置页以说明文字提示），无行为消费者；接入任务：E25 |
| `cloudModel` | String | "" | 设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CloudSttModel；SettingsStore 读写；行为：CloudSttSession 使用云端模型名 |
| `cloudProvider` | String | "soniox" | 设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CloudStt（soniox/openai/deepgram，与 transcriptionBackend=cloud 配合）；SettingsStore 读写；行为：CloudSttSession |
| `coachEnabled` | Bool | true | 设置 → 提词与教练 → 临场教练（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：E10 未做，等待模型方案答复，Windows 尚无临场教练行为消费者；接入任务：E10 |
| `coachGazeSensitivity` | String | "standard" | 设置 → 提词与教练 → 临场教练（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "standard"，允许值 relaxed/standard/strict 已写入注册表）；缺口：E10 未做，等待模型方案答复；接入任务：E10 |
| `coachWatchesGaze` | Bool | false | 设置 → 提词与教练 → 临场教练（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：E10 未做，等待模型方案答复；接入任务：E10 |
| `codexFastMode` | Bool | nil（跟随模型默认） | 设置 → 回答与资料 → Codex 快速模式（AnswerServiceViews.swift CodexFastModeToggle） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CodexFastMode（E27：设置模型与 Codex 状态/快速模式标签已接入）；缺口：Windows CodexAnswerClient 尚未实现服务档位参数（Mac activeServiceTier），行为待 Codex app-server 协议核对；接入任务：E25 |
| `codexModelID` | String | "" | 设置 → 回答与资料 → Codex（AnswerServiceViews.swift CodexEngineSection） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CodexModel；SettingsStore 读写；行为：CodexAnswerClient |
| `codexOCREffort` | String | "medium" | 设置 → 高级 → 资料组件（DocumentModelSettings.swift DocumentComponentsAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "medium"）；缺口：Windows 资料读取走本机 OCR/文档组件，没有 Codex 代读路径，无行为消费者；接入任务：E2 |
| `codexOCRModel` | String | "gpt-6-luna" | 设置 → 高级 → 资料组件（DocumentModelSettings.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "gpt-6-luna"）；缺口：同上，Windows 无 Codex 文件代读；接入任务：E2 |
| `codexOCRTimeout` | Real | 600 | 设置 → 高级 → 资料组件（DocumentModelSettings.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 600，范围 30–3600 已写入注册表并做归一化）；缺口：同上，Windows 无 Codex 文件代读；接入任务：E2 |
| `codexReasoning` | String | "low" | 设置 → 高级 → 回答服务·Codex（SettingsView.swift CodexReasoningPicker；AnswerServiceViews.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.ReasoningEffort（空值按 Mac 默认 "low"）；SettingsStore 读写；行为：AnswerStreaming 推理强度 |
| `companionPort` | Integer | 8_765 | 设置 → 高级 → 手机伴侣（SettingsView.swift CompanionSection） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 8765）；缺口：Windows CompanionServer 由 G3/F10 绑定固定端口策略选择，未读取该首选端口；接入任务：F10/F12；缺口：首选端口未接 CompanionServer |
| `companionRelayURL` | String | "" | 设置 → 高级 → 手机伴侣（SettingsView.swift CompanionSection） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CompanionRelayUrl；SettingsStore 读写；行为：CompanionRelayClient 中转通道 |
| `cueAlwaysOpen` | Bool | false | 设置 → 提词与教练 → 提词（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.CueAlwaysOpen；SettingsStore 读写；行为：CueIslandWindow 会中保持展开（C4） |
| `cueAvoidsSharing` | Bool | true | 设置 → 提词与教练 → 屏幕共享（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 屏幕共享时按 CapturePrivacy/C19 收回，但没有独立 avoidsSharing 设置，无行为消费者；接入任务：E25 |
| `cueLauncher` | Bool | true | 设置 → 提词与教练 → 提词（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 提词岛 Launcher 阶段始终可用（C5），没有该开关，无行为消费者；接入任务：E25 |
| `cueScreen` | String | "" | 设置 → 提词与教练 → 提词（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.CueScreen；SettingsStore 读写；行为：CueIslandWindow.SelectedScreen 顶部居中（C10） |
| `cueStyle` | String | "scene" | 默认跟随场景；按场景在设置 → 场景「提词」（SettingsPages.swift SceneRow） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "scene"）；Windows 按场景存 SceneDefaults.CueStyle（SceneSettingRow），全局键无行为消费者；接入任务：E25 |
| `cueSurface` | String | "island" | 设置 → 提词与教练 → 提词·显示方式（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "island"）；缺口：Windows 同时提供提词岛与提词浮窗（C14），没有「浮窗/提词岛」二选一开关，无行为消费者；接入任务：E25 |
| `cueWidth` | String | "regular" | 设置 → 提词与教练 → 提词·宽度（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.CueWidth；SettingsStore 读写；行为：CueIslandWindow 宽度 440/540/660 |
| `customNotesTemplates` | Array | [] | 设置 → 纪要 → 自定义模板（NotesSettingsView.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 []，JSON 原样保存）；Windows 自定义模板存 NotesTemplateStore 文件（AppState.Templates），该键无行为消费者；接入任务：E16；缺口：Windows 模板另存文件 |
| `customShortcuts` | Dict | {} | 设置 → 提词与教练 → 快捷键（ShortcutSettingsView.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 {}，Mac 原始 JSON 原样保留）；缺口：Windows 快捷键存 AppSettings.Shortcuts（动作 id→按键文本，Ctrl+Alt 体系，见 G1–G4），Mac 的 ShortcutBinding 码不互通 |
| `darkMode` | Bool | false | 旧外观迁移（AppState.swift appearance 读取） | 本版不做｜Mac 旧版深色开关迁移键（appearance 已取代）；Windows 从未使用该键，外观直接存 appearance，本版不做 |
| `demoNotes` | String | "" | 演示模式（DemoMode.swift） | 本版不做｜Mac 演示/截图模式专用键；Windows 演示与截图入口按 H1 用 --demo/--screenshots 启动参数，不读写这些用户偏好键，本版不做 |
| `demoOnboarding` | Integer | 0 | 演示模式（DemoMode.swift） | 本版不做｜Mac 演示模式专用键（H1：Windows 演示入口为 --demo 启动参数），本版不做 |
| `demoSettings` | Integer | 0 | 演示模式（DemoMode.swift） | 本版不做｜Mac 演示模式专用键（H1：Windows 演示入口为 --demo 启动参数），本版不做 |
| `documentOCRProvider` | String | "system" | 设置 → 回答与资料 → 资料（DocumentModelSettings.swift DocumentReadingSettings） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "system"）；缺口：Windows 资料导入走系统 OCR + 文档组件（DocumentReader/EnhancedOcrService），没有系统/本机/Codex 三选一，无行为消费者；接入任务：E2 |
| `documentProviderWasSelected` | Bool | false | AppState+Models.swift（自动选择后置位，无设置控件） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 没有资料识别方式选择，无行为消费者；接入任务：E2 |
| `echoSuppression` | Bool | true | 设置 → 高级 → 声音与识别（SettingsAdvanced.swift SoundAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows EchoSuppression 逻辑已移植（Core/EchoSuppression.cs）但始终启用，没有该开关的行为消费者；接入任务：E25 |
| `globalSkillIDs` | Array | [] | 技能（SkillLibrary；Backup 导出键，无独立控件） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 []）；缺口：Windows 技能的「所有系列」标记存在 Skill 文件字段（SkillStore），该键无行为消费者；接入任务：E28 |
| `insightAnswerProgress` | Bool | true | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 回答进度勾选（AnswerPoints）尚未按该开关启停，无行为消费者；接入任务：E7 |
| `insightFollowSlides` | Bool | true | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift） | 已实现、未验证｜AppSettings.InsightFollowSlides；SettingsStore 读写；行为：AppState.ScreenWatch 跟页（E9） |
| `insightScreenInterval` | Real | 1 | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift） | 已实现、未验证｜AppSettings.InsightScreenInterval（最小 1 秒）；SettingsStore 读写；行为：AppState.ScreenWatch 定时读屏（E9） |
| `insightScreenRegion` | Array | nil（整屏/整窗） | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift 框选区域） | 已实现、未验证｜MacPreferenceMap↔AppSettings.InsightScreenRegion（Mac [x,y,w,h] ↔ Windows "x,y,w,h"）；SettingsStore 读写；行为：AppState.ScreenWatch 区域截图 |
| `insightScreenTarget` | String | "" | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift 读取来源） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：Windows 读屏来源用 InsightScreenSource + 系统显示器，目标 id 与 Mac capture target 体系不同，无行为消费者；接入任务：E4 |
| `insightScreenWatch` | Bool | false | 设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift） | 已实现、未验证｜AppSettings.InsightScreenWatch；SettingsStore 读写；行为：AppState.ScreenWatch 会中读屏（E9，默认关闭） |
| `keepOnTop` | Bool | true | 设置 → 高级 → 数据与维护（SettingsGeneralData.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 主窗口没有「工作台置顶」开关或置顶行为，无行为消费者；接入任务：E29 |
| `keepRecognizerWarm` | Bool | true | 设置 → 高级 → 声音与识别·后台预加载（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 识别器预热由「准备语音组件」流程决定，没有该开关，无行为消费者；接入任务：E25 |
| `lastScene` | String | "interview" | 首页场景（Onboarding.swift rememberGuideScene；AppState.swift 读取） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "interview"）；缺口：Windows 首页场景取系列/场景选择，不持久化最后场景，无行为消费者；接入任务：E3 |
| `liveTranslation` | Bool | nil（未选择） | 会中翻译开关「记住选择」（LiveTranslation.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 有 AppSettings.AutoTranslateForeign（自动翻译外语）与 TranslateSpeech（逐句互译），但未读写该 Mac「记住会中翻译选择」键；接入任务：E6 |
| `localOCRModelID` | String | "paddleocr-vl-1-6-mlx-8bit"（ModelCatalog.paddleOCR.id） | 设置 → 高级 → 资料组件·本机识别模型（DocumentModelSettings.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ModelCatalog.paddleOCR.id）；缺口：Windows 本机 OCR 模型清单由 EnhancedOcrService/ModelStore 固定，没有该选择，无行为消费者；接入任务：E2 |
| `localStreamingModel` | String | "xasr480" | 设置 → 高级 → 声音与识别·流式模型（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.LocalModel；SettingsStore 读写；行为：AppState.Audio/SpeechRecognizerSession 选择识别模型 |
| `meetingVenue` | String | "online" | 首页/引导 线上·线下（AppState+Meeting.swift；Onboarding.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "online"）；缺口：Windows 每场会议按采集目标选择系统声/麦克风，没有线上/线下持久化选项，无行为消费者；接入任务：E2 |
| `microphoneEnabled` | Bool | true | 设置 → 高级 → 声音与识别·麦克风（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CaptureMicrophone；SettingsStore 读写；行为：AppState.Audio 麦克风采集 |
| `microphoneVoiceProcessing` | Bool | false | 设置 → 高级 → 声音与识别（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 麦克风没有 Apple 语音处理（平台差异），无行为消费者；接入任务：E25 |
| `notesLanguage` | String | "auto" | 设置 → 纪要·纪要语言（NotesSettingsView.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "auto"）；缺口：Windows 纪要生成沿用界面语言，没有独立纪要语言开关，无行为消费者；接入任务：E16 |
| `notesModelID` | String | "" | 设置 → 高级 → 纪要·模型（NotesSettingsView.swift NotesAdvanced） | 已实现、未验证｜MacPreferenceMap↔AppSettings.NotesModel；SettingsStore 读写；行为：SummaryService 纪要模型 |
| `notesReasoning` | String | "high" | 设置 → 高级 → 纪要·推理强度（NotesSettingsView.swift NotesAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "high"）；缺口：Windows 纪要共用 AppSettings.ReasoningEffort，没有独立推理强度，无行为消费者；接入任务：E16 |
| `notesTemplateByType` | Dict | {} | 设置 → 场景「纪要模板」（SettingsPages.swift SceneRow） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 {}）；缺口：Windows 按场景模板选择存 SceneDefaults.NotesTemplate（SceneSettingRow）+ NotesTemplateStore，该键无行为消费者；接入任务：E16 |
| `ocrFallbackToVision` | Bool | true | 设置 → 高级 → 资料组件（DocumentModelSettings.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 文本文档识别失败走 DocumentReader 的降级路径但没有该开关，无行为消费者；接入任务：E2 |
| `onboardingCompleted` | Bool | false | 引导完成（Onboarding.swift finishOnboarding） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导完成版本存 AppSettings.OnboardingCompletedVersion（映射见 onboardingGuideCompletedVersion），旧布尔键无行为消费者；接入任务：E3 |
| `onboardingGuideCompletedVersion` | String | "" | 引导（Onboarding.swift；HomeView.swift @AppStorage） | 已实现、未验证｜MacPreferenceMap↔AppSettings.OnboardingCompletedVersion；SettingsStore 读写；行为：AppState.Onboarding 判定是否重开引导 |
| `onboardingGuideDeferredVersion` | String | "" | 首页继续行（HomeView.swift @AppStorage） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：Windows 引导只有完成版本（OnboardingCompletedVersion），无「稍后继续」版本，无行为消费者；接入任务：E3 |
| `onboardingGuideDismissedVersion` | String | "" | 引导（Onboarding.swift deferOnboarding） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：同上，Windows 无该引导状态；接入任务：E3 |
| `onboardingGuideEnabled` | Bool | false | 引导（Onboarding.swift setOnboardingGuideEnabled） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导开关由 OnboardingVisible/settings 完成版本驱动，无该键行为；接入任务：E3 |
| `onboardingGuideLayout` | Integer | nil（未记录） | 引导（Onboarding.swift；当前 layout=2） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 引导没有 layout 版本检测，无行为消费者；接入任务：E3 |
| `onboardingGuideStartedVersion` | String | nil（未记录） | 引导（Onboarding.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 引导没有开始版本追踪，无行为消费者；接入任务：E3 |
| `onboardingResumeRowDismissedVersion` | String | "" | 首页（HomeView.swift @AppStorage） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：Windows 首页没有引导继续行，无行为消费者；接入任务：E3 |
| `pinnedWorkspaceIDs` | String | "" | 资料夹列表·置顶（WorkspacesView.swift WorkspaceList） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""，"id\|id" 串）；缺口：Windows 资料夹列表没有置顶功能，无行为消费者；接入任务：E18 |
| `presenterMode` | Bool | false | 设置 → 提词与教练 → 双屏演讲（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.PresenterMode；SettingsStore 读写；行为：AppState.Presenter/主窗口收起（C 组双屏演讲） |
| `presenterSlideNotes` | Bool | true | 设置 → 提词与教练 → 双屏演讲（SettingsPresenter.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 双屏演讲的讲稿要点显示尚未按该开关启停（C7 已显示讲稿要点），无独立消费者；接入任务：E25 |
| `presenterSlidesScreen` | String | "" | 设置 → 提词与教练 → 双屏演讲·幻灯片所在屏（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.PresenterSlidesScreen；SettingsStore 读写；行为：PresenterCheck/AppState 双屏演讲选屏 |
| `profile` | String | "" | 旧版单场景背景（WorkspaceStore.swift；现为 Workspace.notes） | 本版不做｜Mac 旧版单一场景背景字段；Windows 按系列文件的 Workspace.Notes 存储，不做旧 UserDefaults 迁移，本版不做 |
| `projectLibrarySort` | String | "recent" | 资料夹列表·排序（WorkspacesView.swift WorkspaceList） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "recent"，允许值 recent/name 已写入注册表）；缺口：E18 资料库排序控件未实现，无行为消费者；接入任务：E18 |
| `quickNotesWorkspaceID` | String | nil（首次创建「随手记录」） | 立即记录·随手记录系列（AppState+Prepare.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows「立即记录」没有固定的随手记录系列，无行为消费者；接入任务：E4 |
| `quickStartStep` | Integer | 0 | 引导步骤（Onboarding.swift；HomeView.swift @AppStorage） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 0）；缺口：Windows 引导没有分步进度，无行为消费者；接入任务：E3 |
| `recognitionLanguage` | String | "auto" | 设置 → 高级 → 声音与识别·识别语言（SettingsView.swift LanguagePicker） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "auto"）；缺口：Windows 本机 X-ASR 固定中英混说，云端语言另有 CloudSttLanguage，无本机识别语言消费者；接入任务：E25 |
| `recognitionVocabulary` | String | "" | 设置 → 高级 → 声音与识别·临时专业词（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.Vocabulary；SettingsStore 读写；行为：AppState.Hotwords 识别热词 |
| `recordSessions` | Bool | true | 设置 → 通用 → 记录每次会议（SettingsGeneralData.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 始终保存会议记录，没有「记录每次会议」开关（设置页也未提供），无行为消费者；接入任务：E25 |
| `recordingNoticeShown` | Bool | false | 录音提示（AppState+Prepare.swift、AppState+Launcher.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导已含录音说明，没有一次性录音提示状态，无行为消费者；接入任务：E3 |
| `reducedEffects` | Bool | false | 设置 → 高级 → 数据与维护·减少视觉特效（SettingsGeneralData.swift） | 已实现、未验证｜AppSettings.ReducedEffects；SettingsViewModel 启动/保存时同步 Motion.AppReducedEffects；行为：Motion.Reduce = 系统关闭动画 或 本设置（MotionModel.ShouldReduce）；设置页开关待 E29；接入任务：E29（开关控件；Motion.Reduce 已接入） |
| `sceneOverrides` | Dict | {} | 设置 → 场景（SettingsPages.swift SceneSettingsPage；SceneSettings.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.SceneDefaults（仅 liveMode/style/cueStyle 三字段，与 Mac 场景覆盖同义；Windows 场景行另存模板名）；SettingsStore 读写；行为：AppState.Live 按场景覆盖 |
| `secondPassEnabled` | Bool | true | 设置 → 高级 → 声音与识别·整句精修（SettingsAdvanced.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.SecondPass；SettingsStore 读写；行为：AppState.Audio 整句精修 |
| `selectedTargetID` | String | "" | 设置 → 高级 → 声音与识别·会议声音来自（SettingsAdvanced.swift SoundAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：Windows 采集目标存进程 id/启动时间（AppState.CaptureProcessId，不落盘）并按输出设备选择，capture target 体系不同，无行为消费者；接入任务：E4 |
| `semanticRetrievalEnabled` | Bool | false | 设置 → 高级 → 资料组件·查找方式（DocumentModelSettings.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.UseSemanticRetrieval；SettingsStore 读写；行为：RetrievalService 语义+关键词混合检索 |
| `sessionMode` | String | "general" | 旧版单场景模式（WorkspaceStore.swift；现为 Workspace.mode） | 本版不做｜Mac 旧版单一场景模式键；Windows 按系列文件 Workspace.Mode 存储（A7/B1），不做旧 UserDefaults 迁移，本版不做 |
| `settingsGroup` | String | "" | 设置页导航记忆（AppState+Navigation.swift） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 ""）；缺口：Windows 设置页的页面切换在界面代码里（不持久化），无该键行为；接入任务：E25 |
| `simplifiedInterface` | Bool | true | 内部派生（AppState+Live.swift 写回；无设置控件） | 本版不做｜Mac 内部派生键：只在 saveAnswerSettings 里按 simplifiedInterface 写回，没有设置控件也没有实际行为；Windows 没有该模式，本版不做 |
| `subtitleBarFontSize` | Real | 24 | 设置 → 提词与教练 → 字幕条·字号（SubtitleBarSettingsView.swift） | 已实现、未验证｜MacPreferenceMap↔AppSettings.SubtitleFontSize（默认改 24、范围 16–44）；SettingsStore 读写；行为：字幕条字号（SettingsViewModel） |
| `teleprompterFontSize` | Real | 22 | 设置 → 提词与教练 → 提词·字号（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.TeleprompterFontSize（Mac 默认 22，范围 14–36）；WindowLayout 启动从 settings 读取、改动回写 settings.json；行为：TeleprompterWindow 字号（Views/TeleprompterWindow.axaml.cs）；设置页控件待 E29；接入任务：E29（设置页控件；提词浮窗已接入） |
| `teleprompterHideBackground` | Bool | false | 设置 → 提词与教练 → 提词（SettingsPresenter.swift） | 已实现、未验证｜AppSettings.TeleprompterHideBackground；SettingsStore 读写；行为：TeleprompterWindow 隐藏背景只留文字/边框/按钮（C15） |
| `teleprompterOpacity` | Real | 1.0 | 设置 → 高级 → 提词·不透明度（SettingsPresenter.swift CueAdvanced） | 已实现、未验证｜AppSettings.TeleprompterOpacity（Mac 默认 1.0，范围 0.35–1）；WindowLayout 启动从 settings 读取、改动回写 settings.json；行为：TeleprompterWindow 不透明度（Views/TeleprompterWindow.axaml.cs 保留 0.35 下限）；设置页控件待 E29；接入任务：E29（设置页控件；提词浮窗已接入） |
| `transcriptionBackend` | String | "zipformer" | 设置 → 高级 → 声音与识别·引擎（SettingsAdvanced.swift SoundAdvanced） | 已实现、未验证｜MacPreferenceMap↔AppSettings.CloudStt（zipformer=本地、cloud=CloudStt 云端；apple 在 Windows 无对应——允许的差异「系统语音识别」不显示）；SettingsStore 读写；行为：AppState.Audio；允许的差异：apple（系统语音识别）在 Windows 不显示 |
| `updateChannel` | String | "stable" | 设置 → 高级 → 更新·更新渠道（SettingsView.swift UpdateAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "stable"）；缺口：Windows 更新渠道在 AppState.Update 的运行时选择（UpdateChannels 列表）里，未落盘读取该键；接入任务：E25 |
| `updateRepository` | String | "OmnixSI/NextThought" | 设置 → 高级 → 更新 → GitHub 访问·仓库（SettingsView.swift GitHubAccessSection） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "OmnixSI/NextThought"）；缺口：Windows 更新源由 UpdateFeedPolicy 固定 GitHub 仓库，没有仓库输入框，无行为消费者；接入任务：E25 |
| `verifyUncertainWithModel` | Bool | false | 设置 → 高级 → 回答服务（SettingsAdvanced.swift AnswerAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 追问判断只走本机规则（QuestionDetector/QuestionIntent），没有二次模型判断开关，无行为消费者 |
| `webSearchMode` | String | "auto" | 设置 → 回答与资料 → 联网搜索（SettingsView.swift WebSearchSettings） | 已实现、未验证｜MacPreferenceMap↔AppSettings.WebSearch（off=false、auto/always=true）；SettingsStore 读写；行为：AppState.Live 联网回答；缺口：Windows 只有开/关两档（always 归为开），无「每次必查」档 |
| `webSearchProvider` | String | "codex" | 设置 → 高级 → 回答服务·联网搜索（SettingsView.swift WebSearchAdvanced） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "codex"）；缺口：Windows 联网搜索固定 Tavily（TavilyKey + WebSearch），没有 Codex/Tavily 选择，无行为消费者；接入任务：E25 |
| `workspaceEditorFocusSection` | String | "records" | 资料夹编辑器（WorkspacesView.swift WorkspaceEditor） | 已实现、未验证｜MacPreferences+SettingsStore（Mac 键名/默认 "records"）；缺口：Windows 资料夹编辑器页面切换不持久化，无行为消费者；接入任务：E18 |
| `workspaceMigrationCompleted` | Bool | false | 旧版迁移标记（WorkspaceStore.swift） | 本版不做｜Mac 旧版单一场景迁移标记；Windows 直接读 Mac 系列文件（B1/A7），不跑旧 UserDefaults 迁移，本版不做 |
