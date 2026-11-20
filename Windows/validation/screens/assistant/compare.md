# 回答区模块（AssistantView / AssistantLayout / DetachedModules ↔ Windows 回答区）逐元素对照

- Mac 标准：`Sources/NextThought/AssistantView.swift`（AnswerHistoryPanel / DetectionStrip / ConfidenceBar / ScreenshotCard / AnswerPanel / AnswerHeader / AnswerBody / WebLookupStrip / WebSourcesList / AnswerFooter / AnswerEvidenceDetails / ConversationPanel / TranscriptStatusLine / ConversationBubble / PartialBubbles / EmptyConversationHint）、`AssistantLayout.swift`（AssistantModule / AssistantLayout / Preset / 槽位操作）、`DetachedModules.swift`（DetachedModuleController / DetachedModuleView）。
- Windows 对应物：`Views/Assistant/AnswerHistoryPanel.axaml`、`DetectionStrip.axaml`、`ScreenshotCard.axaml`；`Views/DetachedModuleWindow.axaml(.cs)`；`Core/AssistantLayout.cs`；`AppState.Assistant.cs`；会中页 `Views/LiveView.axaml` 里的答案栏（E4 已完成 AnswerHeader/AnswerBody/WebLookupStrip/WebSourcesList/RefinementBar/AnswerFooter/AnswerEvidenceDetails 对应物）。
- 截图证据：`module-answer.png` / `module-history.png` / `module-question.png`（`--self-test=assistant-ui` 生成，独立模块窗口）；会中页答案栏见 `../live/windows-light.png`。

## 元素对照

| Mac | Windows | 结论 |
| --- | --- | --- |
| `AnswerHistoryPanel`：标题 回答记录 + 数量 + 回到最新；行 = 时间、联网图标、手动标记、问题、回答预览；最新在下/点行回看；空态说明 | `Views/Assistant/AnswerHistoryPanel`：历史回答 + 数量 + 回到最新；行=时间/联网/手动/问题/回答预览、active 高亮；点击走 `SelectAnswerHistory`，最新行清游标；数据来源 `SessionAnswerHistory`（录制中=内存 `CurrentAnswerHistory`，会后=归档 `SelectedSessionAnswers`） | 一致；空态说明句未显示（该句无 Mac 字符串表键，避免自造文案）；标题用现有「历史回答」而非 Mac「回答记录」 |
| `answerHistory` + `historyCursor`：上一条/下一条/位置 2/2/回到最新 | `AppState.Assistant`：`SessionAnswerHistory`、`AnswerHistoryCursor`、`HistoryPositionText`、`DisplayedAnswerText/DisplayedQuestionText`、`PreviousAnswerCommand/NextAnswerCommand` 改为移动游标、`ShowLatestAnswer` | 一致（这是 E5 补齐的「从归档读取」）：会后打开同场会议时面板读 `SelectedSessionAnswers`，游标回答读到的是归档答案 |
| `DetectionStrip` + `ConfidenceBar`：当前候选（意图、置信度 36×4 胶囊、文本、回答）+ 可能的问题列表 | `Views/Assistant/DetectionStrip`：可能的问题 + 置信度条（`CandidateConfidence`，≥0.6 绿 / ≥0.4 橙在主题语义内）+ 候选文本 + 回答这句（`AnswerLiveCandidate`）；会中页转写底部保留同信息的内联候选条 | 部分一致：Windows 无 suggestedQuestions 队列，只显示当前 tracker 候选；颜色用主题 Ai/Sunken 而非绿/橙 |
| `ScreenshotCard`：截图预览 + 取消/分析截图 + 图（≤140）+ 提示「可在下方输入框补充要求…」 | `Views/Assistant/ScreenshotCard`：截图 + 取消/分析截图 + 图 + 现有「对截图的补充要求（可留空）」；会中页 composer 与独立回答模块共用 | 一致（提示句改用现有译文键） |
| `AssistantLayout`：`AssistantModule` 五模块、`Slot(module,column,weight)`、`detached`、`split`、`Preset` 五套、place/hide/show/detach/move/setWeight/load/save | `Core/AssistantLayout.cs`：同五模块、Slot/Detached/Split、`Standard` + 五 Preset（槽位与权重逐值对 Mac）、Place/Hide/Show/Detach/Dock/Move/WithWeight/WithSplit、`FromJson/ToJson`（重复模块保留首个位置） | 一致；Windows 存 `assistant-layout.json`（Mac 用 UserDefaults "assistantLayout"） |
| `DetachedModuleController`：弹出/合并/关闭回来、面板尺寸（conversation 420×560、question 520×160、answer 560×480、history 380×460、insights 440×520） | `AppState.Assistant.SyncDetachedModuleWindows` + `Views/DetachedModuleWindow`：按模块开/关窗口，关闭窗口自动 merge 回主窗口；尺寸逐值对 Mac（question 520×200，比 Mac 高一点留出候选/文本） | 一致（窗口标题/图标、`关闭` 即合并回主窗口） |
| `DetachedModuleView`：细标题栏 + 模块内容；answer 模块可滚动 | `DetachedModuleWindow`：标题+图标+关闭；内容按模块切换为 DetectionStrip / AnswerHistoryPanel / 转写列表 / 回答（问题+Markdown+证据行）/ InsightsPanel | 一致（answer 模块内容为简化版，完整答案卡在会中页右栏） |
| `ConversationPanel`/`ConversationBubble`/`PartialBubbles`/`TranscriptStatusLine` | 模块窗口的 实时对话：转写列表（时间+说话人、文本、译文）；完整对话区仍为会中页左栏（E4） | 部分一致：独立窗口里为紧凑列表，不带气泡布局 |
| `AnswerPanel`/`AnswerHeader`/`AnswerBody`/`WebLookupStrip`/`WebSourcesList`/`AnswerFooter`/`AnswerEvidenceDetails` | 会中页答案栏（E4）：建议回答标题、答案正文/空态、联网状态行+编号来源、精修行、复制/重新生成/联网查一下、证据行+详情 flyout | 一致（E4 已收口，本任务未重复实现） |

## 动作点检（--demo --self-test=assistant-ui）

结果写入 `%TEMP%\nextthought-selftest\assistant-ui`（passed / failures / checks / screenshots），控制台打印 `assistant-ui-check: passed`。

1. 录制中：回答历史面板列出本场 2 条回答，行序最新在前。
2. 点第一行：游标=0，答案栏改显示该条答案与对应问题，位置显示 `1 / N`；回到最新清游标并显示当前回答。
3. 会后（Recording=false 且选中归档场次）：面板改读 `SelectedSessionAnswers`，首条与归档一致。
4. 布局：standard 两栏 → answer-only 预设只剩问题+回答 → standard；弹出回答模块（layout.detached 记录、窗口出现）→ 合并回主窗口 → 再弹出/合并历史模块。
5. 布局 JSON：`<data>/assistant-layout.json` 写入并可读回（槽位/列/权重/分离列表）。
6. 检测条：演示候选存在；截图卡：塞入预览图后显示，取消后消失。
7. 独立窗口截图：回答 / 历史 / 问题三个模块窗口各一张。

## 未完成/已知差异

- 空态说明句（"回答过的问题会按时间列在这里…"）、模块标题「回答记录」、预设菜单的中文名未使用 Mac 原文案（避免新增未翻译字符串），预设菜单暂用英文短标签（standard / answer-first / presenting / stacked / answer-only）。
- 检测条的颜色语义（绿/橙/灰）未按阈值换色；Windows 无 suggestedQuestions 队列，只有当前候选。
- 独立窗口里的「实时对话」是紧凑列表，不是 Mac 的气泡布局；窗口尺寸 question 比 Mac 高 40。
- Mac 的「合并回主窗口」按钮文案在 Windows 用「关闭」（关闭即合并，行为一致）。
- 模块布局的拖动改列/拖拽调权重未做（模型支持 place/weight，UI 只提供预设与弹出/合并）。
