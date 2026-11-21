# 会中主界面（LiveSessionView.swift ↔ LiveView.axaml）逐元素对照

- Mac 标准：`Sources/NextThought/LiveSessionView.swift`（LiveTopBar / RecordingTimer / PauseResumeButton / AnswerSettingsMenu / QuestionMark / CandidateStrip / TranscriptStatus / RightColumn / AnswerColumn / WebStatusLine / WebSources / RefinementRow / AnswerMoreMenu / EvidenceLine / EvidenceDetails / LiveComposer）+ `LiveWindowController.swift`（LiveWindowMenu）；对照图 `docs/screenshots/v3/live.png`。
- Windows 截图：`windows-light.png` / `windows-dark.png`（本目录；`--demo --screenshots` 生成）。
- 自动点检：`--demo --self-test=live-ui`（RunLiveUiCheckAsync）逐项验证顶栏状态、模式切换、问题标记、候选条、联网状态/来源、证据行与详情、回答历史导航、输入框与截图预览；结果见文末。

## 元素对照

| Mac（LiveSessionView.swift） | Windows 对应物 | 结论 |
| --- | --- | --- |
| `LiveTopBar`：计时圆点 + 计时 + 正在聆听/已暂停/未在聆听；`PauseResumeButton` | LiveView 顶栏：`IsLiveOnAir` 圆点、`RecordingElapsedText`、`LiveListeningStatus`、暂停/继续按钮（`TogglePauseListeningCommand`） | 一致 |
| `SceneTag` + 标题块（标题 + 系列名） | `SceneTag` + `MeetingTitle` 输入框（20 磅衬线）+ `PageSubtitle` | 一致 |
| `answerModePicker`：自动回答 / 只记录 分段 | 顶栏两个 `RadioButton.chip`（`AnswerModeIsAuto`，点击 `SetAnswerMode`） | 一致；Mac 的「未连接回答服务」黄点未移植（Windows 无 answerEngineReady 状态） |
| `PhoneStatusIcon` / `ScreenStatusIcon`（图标 + 状态点） | 手机/屏幕图标按钮 + 状态圆点（`LiveCompanionOn` / `LiveScreenWatchOn`，tooltip 用 `CompanionInfo` / `LiveScreenWatchHelp`） | 一致（Windows 的点表示「开着」，Mac 区分已连接/等待，已在未解决项注明） |
| `AnswerSettingsMenu`：模型、快速模式、联网、回答长度、从下一个回答开始生效、更多回答设置… | 回答设置 Flyout：模型 `ComboBox`（ModelChoicesList→`Settings.Model`）、快速模式（CodexFast、仅 API/Codex 可用时显示）、联网开关（`LiveWebSearchIsOn`）、回答长度 要点/详细（`CurrentAnswerIsDetailed`，作用于下一次回答的提示词 style）、说明与跳转 | 一致；Mac 联网是「不联网/自动判断/每次都查」三选，Windows 设置层只有联网开关（设置页仍是二态），已在未解决项注明 |
| `moreMenu`：提词岛一直展开、双屏演讲、回答语言/英文对照、同声传译、屏幕感知、手机伴侣、回答浮窗、会中窗口三选、设置… | 工具 Flyout：提词岛一直展开、双屏演讲、回答语言三选、英文回答附中文对照、屏幕感知、手机伴侣、回答浮窗、提词岛、会中窗口（纪要窗口/字幕条/隐藏）、设置… | 大部分一致；同声传译入口未移植（Windows 逐字同传由设置自动启用，无独立开关） |
| `endSessionButton` / 开始记录 | 「结束」（`StopRecordingCommand`）/「开始记录」（`StartRecordingCommand`），按 `Recording` 切换 | 一致 |
| `TranscriptColumn`：实时对话 overline、跟随按钮、行（SpeakerLabel + QuestionMark + 文本 + 译文 + 回答这句/星标） | 实时对话 + 行模板：`Label`、`IsQuestion` 的「问题」胶囊（场景色 13% 底 + 场景色字）、文本、译文、回答这句、高亮 | 一致（跟随/滚动暂停按钮未移植；Mac 的 `ScrollFollowState` 行为留待后续） |
| `QuestionMark`：场景色胶囊 | 同上，`LiveSceneTintSoftBrush`/`LiveSceneTintBrush`（随明暗与场景） | 一致 |
| `CandidateStrip`：可能的问题 + 文本 + 回答这句（+可删除的 suggested） | 转写底部候选条：场景色圆点 + 可能的问题 + `CandidateQuestion` + 回答这句（`ForceAnswerCommand`） | 一致；Windows 无 suggestedQuestions 队列，候选条只显示 tracker 当前候选 |
| `PartialLines`：对方 / 我（现场）两行分别带译文 | 底部两行 partial（对方 / 我），来自 `SystemPartial` / `MicrophonePartial` | 部分一致（译文 partial 行未显示，音频层有 translation 但未暴露成两行） |
| `TranscriptStatus`：音源 · 识别引擎 · 麦克风 · 回声 · 遗漏 · 同传 · 隐私锁 | 状态行：`AudioSourceText` · `LiveAudioEngineLabel` · 隐私锁 | 部分一致（回声/遗漏/麦克风未开等计数 Windows 未跟踪，已在未解决项注明） |
| `RightColumn`：我的笔记 / 问答 分段；回答生成中提示 | 右栏两个 `RadioButton.chip` + NotesPanel/AnswerPanel 切换（默认跟随 `AnswerModeIsAuto`）；`Answering` 显示「回答生成中」 | 一致 |
| `AnswerColumn`：✦ 建议回答/正在回答、上一条/下一条 + 2/2、联网状态行、问题原文、答案卡片、WebSources、RefinementRow、EvidenceLine | 答案栏：`AnswerColumnTitle`、上一/下一条（`PreviousAnswerCommand`/`NextAnswerCommand`）、`LiveWebStatusLine` + 编号来源、`DetectedQuestion`/双语问题、Markdown 答案、空态提示、精修行（更简短/举例/英文版/…/停止生成）、证据行 | 一致；历史位置文本 2/2 未移植（Windows 历史游标是陪伴快照用的，E5 回答历史面板再统一） |
| `WebStatusLine`：正在联网核查… / 联网版 · N 个来源 / 看原回答、失败重试 | `HasWebResults` 时显示 🌐 + `LiveWebStatusLine`；重试按钮走 `LookUpDisplayedAnswerCommand` | 部分一致（Windows 没有「原回答 ↔ 联网版」切换，联网结果直接并入当前回答；未解决项注明） |
| `WebSources`：编号 [1] 标题可点 | `LiveWebSources` 编号列表，点击 `OpenWebSource` 用系统浏览器打开 | 一致 |
| `RefinementRow` + `AnswerMoreMenu` | 更简短/举例/英文版 chip + ⋯ 菜单（展开/STAR/中文版/澄清问题/我可以反问/重新生成/联网查一下/复制回答/独立窗口）+ 回答中「停止生成」 | 一致 |
| `EvidenceLine` + `EvidenceDetails`：依据：资料 N · 屏幕 · 联网 N · M 项限制；详情含检索方式、资料引文/正文、屏幕来源、联网链接、限制 | `EvidenceSummary` + 证据 Flyout：`EvidenceRetrievalText`、`EvidenceMaterialRows`（引文标题 + 正文）、`EvidenceScreenSource`、`EvidenceWebSourceRows`、`EvidenceNoteRows`（含 ⚠） | 一致；`EvidenceBundle` 在真实回答链路里落库（之前只存模型字段），演示回答也带完整证据 |
| `LiveComposer`：截图预览条（取消/分析截图）、输入框（⌘L）、截图菜单（区域/窗口）、发送、立即回答（bolt + 快捷键） | 底部 composer：截图预览条（取消/分析截图）、输入框（`LiveComposerPlaceholder`）、相机菜单（框选区域/选择窗口）、发送、立即回答（`ForceAnswerCommand` + `ForceAnswerShortcut`） | 一致；⌘L 聚焦输入的快捷键未移植（Windows 快捷键表无此项），立即回答显示 Ctrl+Alt+Enter |
| `LiveWindowMenu`：纪要窗口/字幕条/隐藏 | 工具菜单三按钮（纪要窗口跳转、字幕条/隐藏走 `CycleLiveModeCommand`） | 部分一致（Windows 的 LiveMode 是循环切换，三按钮都可用但「隐藏」按钮实际是循环到下一态） |

## 动作逐项点检（--demo --self-test=live-ui）

> 结果写入 `%TEMP%\nextthought-selftest\live-ui`（JSON：passed / failures / checks），控制台打印 `live-ui-check: passed`。

1. 载入 Live 演示阶段 → 会中页：计时在走、状态「正在聆听」、场景「面试」、标题与系列名显示。
2. 自动回答 / 只记录 分段切换：`Settings.AutomaticAnswers` 跟随，右栏默认页签跟随（自动→问答，只记录→我的笔记）。
3. 回答设置菜单：模型选择写回 `Settings.Model`；回答长度 要点/详细 改变 `CurrentAnswerStyle`（下一次回答的提示词 style）；联网开关写回设置；快速模式仅 Codex 下显示。
4. 工具菜单：提词岛一直展开、双屏演讲、屏幕感知、回答语言、英文对照逐项切换。
5. 问题标记：演示转写里带问句的行出现「问题」胶囊（场景色）；我方发言永不标记。
6. 候选条：`DetectedQuestion` 非空时出现「可能的问题 + 回答这句」。
7. 答案栏：建议回答标题、上一条/下一条、答案正文；更多菜单逐项可用（精修走 `RefineAsync`）。
8. 联网：写入一条演示 WebResult 后出现联网状态行与编号来源 [1]，点击走系统浏览器。
9. 证据：演示回答显示「依据：资料 2 · 屏幕 · 联网 1 · 1 项限制」；详情列出资料引文、屏幕来源、联网来源、限制。
10. 输入框：空态不可发送；输入文字可发送；截图预览出现后占位文案变为「对截图的补充要求（可留空）」，取消后恢复。

## 未完成/已知差异

- 跟随/暂停跟随（Mac ScrollFollowState 的「新增 N 句 · 回到现场」）未移植；转写仍自动滚到底。
- `TranscriptStatus` 的回声数、遗漏音频段、麦克风未开/未授权提示：Windows 音频层未跟踪这些计数。
- 联网只有二态开关（设置层无「不联网/自动判断/每次都查」三选），也没有「原回答 ↔ 联网版」切换。
- 同声传译没有会中开关（Windows 逐字同传由设置自动启用）；LiveWindowMenu 的「隐藏」按钮目前是循环切换而非直达。
- 历史位置文本（2/2）与回答历史面板留到 E5；同声传译 partial 译文行留待后补。
- 手机状态点表示「手机伴侣开着」，Mac 表示「已连接」；Windows 无 answerEngineReady 黄点。
- 快捷键：立即回答的提示文本显示 `Ctrl+Alt+Enter`。

## 截图与运行证据

- `windows-light.png` / `windows-dark.png` / `windows-en-light.png` / `windows-en-dark.png`：`--demo --screenshots` 于 2026-11-21 06:31 生成（本目录；同批拷贝到 `validation/screens/current/`）。
- `--demo --self-test=live-ui`：**live-ui-check: passed**，report 里 40 项检查全 ok（顶栏计时/聆听状态、场景与系列名、模式切换、回答设置、问题标记、候选条、联网状态行与编号来源、证据行与详情、回答历史导航、输入框可提交性、截图预览、工具菜单开关）。
- `--demo --self-test=startup`：**startup-check: passed**（2026-11-21 06:35:44，report passed=true）。
- 行内「回答这句 / 高亮」按钮改为悬停才显示（与 Mac TranscriptLine 的 hover 行为一致）；候选条「回答这句」走 `AnswerLiveCandidate`（先把候选提为当前问题再立即回答）。

## E6 回答语言 / 双语回答 / QuestionTurn

### 元素对照

| Mac | Windows | 结论 |
| --- | --- | --- |
| `AnswerLanguage.spoken(in:)`：英文问题夹中文术语仍是英文，中文问题带 "XL-MIMO" 仍是中文；`fields(choice:gloss:question:)` 生成 answer/gloss 语言 | `Core/AnswerLanguage.cs` 逐行移植；`AppState.Live.BuildAnswerPrompt` 已通过 `Settings.AnswerLanguage`/`Settings.AnswerGloss` 调用 `AnswerLanguage.Fields`，把 answer_language/gloss_language 放进 JSON | 一致（E4 已有菜单入口，本轮补测试与显示） |
| 顶栏/工具菜单的「回答语言」（跟随问题/中文/English）+「英文回答附中文对照」 | LiveView 工具菜单 radio 按钮 + 复选框，`AnswerLanguage_Click`/`AnswerGloss_Click` 写设置并保存 | 一致 |
| 双语回答格式：`> 问：`（被问的是什么，gloss 语言）+ 每句回答一行、紧随 `> 译：` | `AppState.LiveUi.IsBilingualAnswer` 检测 `BilingualAnswer`；答案区在有对照时改用 `Views/BilingualAnswerText`：顶部小字显示「问」的 gloss，之后每句正文下面用小一号次要色显示该句译文；无对照时仍用 MarkdownText | 一致（Mac 页面用 Markdown 原文引行显示；Windows 用逐句对照分组版式，和任务要求的逐句并排/分组一致） |
| 提词岛双语：`CueRow.withGloss` 在每句下方小字显示译文；有对照时少显示几行 | CueIslandWindow 台词下方新增 `IslandHeadlineGloss` / `IslandDetailGloss` 小字行（`HasIsland*Gloss` 控制显隐）；`CueScript` 已把 gloss 挂到对应 CueLine | 一致；「有对照时少显示几行」的窗口裁剪未做 |
| `QuestionTurn`：对方一轮发言（多句/陈述式疑问）合并为整轮回答；`worthAsking`/`isDue`/`asked`/`spoke` 结束轮次；`answersWholeTurns` 只在汇报/答辩、线上、演讲模式或我说够 300 词时开启；答案请求时 `questionTurn.asked(with: partialTranscript)` | 新增 `AppState.QuestionTurn.cs`：`questionTurn` 字段 + `AnswersWholeTurns`、`NoteQuestionTurn`（remote→Heard，我→Spoke+词数）、`TryAnswerQuestionTurn`（候选定时器每 tick 检查 IsDue，静音 ≥2s 或我开始回应，Requests<4、词数≤250）、`QuestionTurnAnswerText`（自动回答改用整轮文本）、`MarkQuestionTurnAsked`（每次 Ask 标记已覆盖词数）、开始/结束会议 Reset；`AppState.Live` 的 ReceiveTranscript/OnCandidateTimer/AnswerAsync/StartRecording/StopRecording 对应接线 | 一致（Mac 的 answerTurn 先记录 question 事件/Diagnostics 的完整链路未逐字复制，保留现有 Windows 候选与诊断） |

### 点检记录（--demo --self-test=live-ui）

1. `回答语言=English` + 对照 → `AnswerLanguage.Fields` 返回（English，简体中文）；`=中文` 关闭对照；`跟随问题` 由问题语言决定。
2. 写入一条双语回答（英文句子 + `> 译：` 行）→ `IsBilingualAnswer=true`；`BilingualAnswer.Spoken` 去掉对照行；「问」gloss 可解析；提词岛小字译文取到第一条/第二条 CueLine 的 gloss。
3. 双语页面截图：`bilingual-answer.png`（自检把主窗口当前答案区截到 `live-shots/`，可拷入本目录）。
4. QuestionTurn：Core 单测覆盖 heard/spoke/gap/patience/worthAsking/asked/reset/limit/词数；AppState 接线由 AnswerLanguageParityTests 结构锁。

### 未解决/已知差异

- QuestionTurn 的整轮回答只在「汇报 / 答辩 + 线上 + 演讲模式或本场我说满 300 词」时启用（与 Mac answersWholeTurns 相同）；其他场景仍按候选句回答。
- Mac 在 answerTurn 里额外记录 question 事件与 Diagnostics 明细，Windows 保留现有候选事件，「整轮」标记未写入归档 question 事件。
- 提词岛有译文时少显示行数的裁剪未做；岛上「被问到的 gloss（BilingualAnswer.question）」当问题行已有双语回答时未单独展示。
