# E2 · 会前准备页对照（PrepareView，第三轮缺口收尾）

- 日期：2026-11-21
- Mac 参照：`Sources/NextThought/PrepareView.swift`（PrepareView / PrepareComposer / TypePicker / SeriesCarryCard / SeriesBriefingPanel / CarryRow / MaterialsActions / MaterialsBlock / FileRow / MaterialsInsight / CheckCard / AudioCheckRow / VenuePicker / AudioSourceMenu / AnswerCheckRow / PhoneCheckRow / PreparationStartBar）、`Sources/NextThought/AppState+Meeting.swift`（`MeetingDraft`、`MeetingVenue`、`SessionMode.defaultsSummary/preparationHint`）、`Sources/NextThought/AppState+Prepare.swift`（updateDraftTitle / setDraftType / setDraftCustomType / setDraftCarry / cancelDraft / startPreparedMeeting）
- Windows 实现：`Windows/src/NextThought.Desktop/MeetingDraft.cs`（新）、`AppState.PrepareDraft.cs`（新）、`AppState.Prepare.cs`、`Views/PrepareView.axaml(.cs)`、`AppState.Live.cs` 的 `StartRecordingAsync`
- 截图：`Windows/validation/screens/prepare/prepare-{light,dark,en-light,en-dark}.png`

## 本轮补齐的缺口（对照 Mac）

| Mac 元素 | 本轮实现 | 说明 |
|---|---|---|
| `MeetingDraft`（AppState+Meeting.swift） | `MeetingDraft` 记录：title / mode / customTypeName / customBaseMode / typeChosen / seriesId / carry / baselineDocumentIds / previousSessionID / startedAt；AppState 以 `Draft` 持有，`EnsureDraft`（进入准备页）建立，`CancelDraft` 丢弃 | 内存态，和 Mac 一样不落盘；`DraftMeetingPlan` 直接从 draft 生成 ported `MeetingPlan` |
| `updateDraftTitle` | `DraftTitle` setter：写 `MeetingTitle`，未手动选类型时用 Core `MeetingTypeGuesser` 猜场景 | 输入标题即切换场景与 SceneGlyph，和 Mac 相同 |
| 类型猜测提示 | 「这是什么会」overline + 猜测时显示「根据标题选好了，可以改」（`DraftGuessNoteVisible`） | Mac `showsGuessNote` 的等价 |
| `SessionMode.defaultsSummary` / `preparationHint` | `SceneSummary.Defaults/Hint/Line`：10 个场景的默认值摘要与「建议放：」资料提示；`SceneChoice.DefaultsSummary` 作为场景 chip 的 `ToolTip.Tip`；类型卡片下显示「名称：默认值 · 建议放：资料」 | 快捷键按 G10 改为 `Ctrl+Shift+Enter`（Mac 为 ⌘⇧↩） |
| `TypePicker` 自定义类型 | 「自定义…」chip → `SetDraftCustomType`；自定义面板：类型名称 TextBox（`DraftCustomTypeName`）+ 「基于」下拉（`DraftBaseScenes`） | 修复过一处运行时栈溢出：ItemsSource 每次重建 + SelectionChanged 无守卫会无限递归；现为缓存列表 + 值未变提前返回 + `applyingDraft` 防重入 |
| `VenuePicker` | 标题右侧「线上 / 线下」RadioButton：线上 = 系统声音 + 麦克风，线下 = 仅麦克风（写 `Settings.CaptureSystem/CaptureMicrophone` 并保存） | 与 Mac MeetingVenue 语义一致；转写 `InPerson` 跟随（Live 用 `CaptureSystem` 推断） |
| `AudioCheckRow` | 检查卡「声音」行：状态点 + 当前声音来源 + 麦克风状态（现场/我/未开启）+ 模型状态/下载 + 未就绪提示 | 无 macOS 的 AudioSourceMenu（只窗口/刷新）；Mac 该行本身无电平条 |
| 只记录对方 / 重新打开麦克风 | `ContinueWithoutMicrophoneCommand`、`UseMicrophoneAgainCommand`；页面与开始栏各有入口 | 对应 Mac `continueWithoutMicrophone` / `useMicrophoneAgain` |
| `AnswerCheckRow` | 「AI 可选」行：API/未配置 + 「检查回答服务」（`TestAnswerEngineCommand`）+ 连接结果（`PrepareEngineVerification`）+ 「更换/去连接」 | Mac 打开设置 answers 页；Windows 复用设置页入口 |
| `PhoneCheckRow` | 「手机」行：连接状态/中转地址 | 无 Mac 的 companionEnabled 条件隐藏 |
| `MaterialsBlock` / `FileRow` / `MaterialsActions` | 资料行「名称 + N 段 / 还没有可用的文字 / N 处提示」；拖放目标（`DragDrop.AllowDrop`，`ImportDraftDocumentsAsync`）；「导入资料」按钮；空态提示 | 缺 Mac 的逐条解析进度/失败重试与「这次新加的资料」分组 |
| `SeriesCarryCard` / `CarryRow` | 系列列表 + 四个带入开关 + `PrepareCarrySummary`；开关同步 `draft.Carry` | 缺 Mac 的系列候选建议（SeriesMatcher 接着上一场/不是同一个系列） |
| `PreparationStartBar` / `StartButton` | 状态点 + 就绪说明 + 本场资料句 + 取消 + 只记录对方 + 导入 WAV + 开始记录（`StartPreparedMeetingCommand` → `BeginPreparedMeeting`） | 主流程与 Mac startPreparedMeeting 一致；缺录制同意提示（Windows 无对应步骤） |
| `SeriesBriefingPanel` | 最近会议 / 已确认经验 / 题库 | 与上一轮一致，样式简化 |

## 运行时自测

- `--demo --self-test prepare`（`RunPrepareCheckAsync`）：逐项驱动场景 chip、自定义类型/基础类型、线上/线下、只记录对方/重新打开麦克风、四个带入开关、资料行、`DraftMeetingPlan`、`BeginPreparedMeeting`（断言 plan Title/Mode/Carry），最后取消并断言 draft 清空。
- `--demo --self-test startup`：启动/页面装配冒烟。
- 修复记录：ComboBox `SelectionChanged` 无限递归（栈溢出）已修；修复后 `startup` 与 `prepare` 自测实跑通过。

## 未完成项

- SeriesMatcher 系列建议；AudioSourceMenu 捕获窗口选择；逐条解析进度与重试；录制同意提示；本轮截图待 verify 重跑（`click-through.md` 已更新）。
