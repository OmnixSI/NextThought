> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 屏幕与讲稿面板（InsightsPanel.swift ↔ InsightsPanel.axaml）逐元素对照 — §6.5/E7 重做版

- 对照标准：Mac v3.4.0 `Sources/NextThought/InsightsPanel.swift` 全文（`InsightsPanel`、`ScreenWatchOffHint`、`SlideSection`、`ScreenSection`、`AnswerProgressStrip`）；嵌入位置对照 `Sources/NextThought/LiveSessionView.swift` 的 `TranscriptColumn`（`if state.insights.screenWatch && state.typeDefaults.followsSlides { InsightsPanel().frame(height: 240) }`）。
- Windows 对应物：`Views/InsightsPanel.axaml(.cs)`、`AppState.Insights.cs`（含嵌入规则/相对时间/进度文案）、`Views/InsightsPanelWindow.axaml`（「屏幕与讲稿」独立窗口入口）；会中页左栏嵌入见 `Views/LiveView.axaml` 的 `LiveInsightsPanel`。
- 截图：`validation/screens/live/light.png`（会中页整页，含左栏位置）；`insights-embedded-light.png`（`--self-test=live-ui` 打开屏幕感知 + 答辩场景时截取的左栏嵌入版，2026-11-21）。
- 对照方法：§6.3，逐元素（顺序/文案/字号级别/字重/颜色资源/间距资源/圆角/状态）列表比对。

## 元素对照

| # | Mac（InsightsPanel.swift） | Windows（InsightsPanel.axaml / AppState.Insights.cs） | 结论 |
| --- | --- | --- | --- |
| 1 | 面板 `.card()`，VStack spacing 10，`maxHeight .infinity` 顶部对齐 | `Border Classes="card"`（Surface/hairline 0.5/RadiusMedium/PaperCardShadow），Grid 头部 + ScrollViewer 内容，spacing 12 | 一致 |
| 2 | 头部：`Label("屏幕与讲稿", systemImage: insights.symbol)` 13 semibold 单行 + Spacer + `立即识别` ChipButtonStyle（读屏中禁用）+ mini ProgressView + 齿轮 GlassIconButton 26（跳设置） | 同：`IconRectangleOnRectangle` +「屏幕与讲稿」13 SemiBold、`立即识别` pill（`RequestScreenReadCommand`，`!ScreenIsReading` 才可用）、读屏 ProgressBar 36×3、`IconGearshape` 26 → 设置 | 一致 |
| 3 | `ScreenWatchOffHint`：长说明（打开“屏幕感知”后…）+「打开屏幕感知」prominent Chip | 同文案逐字（`{loc:Tr 打开“屏幕感知”后，…}`）+ `打开屏幕感知` pill（`EnableScreenWatch_Click`） | 一致 |
| 4 | 读屏错误：`Note(error, color: .orange)`；读屏状态 `status · screenTargetTitle` | `ScreenErrorText`（Caution）+ `ScreenStatusLine`（状态 · 来源） | 一致 |
| 5 | `SlideSection`：`Brand.accent.opacity(0.06)` 圆角 10 底、padding 10；页位置 `第 N / M 页` 13 semibold monospacedDigit + 文档名 11 secondary；读屏生成中 mini ProgressView；上一页/下一页 ChipButton（到边界禁用）；`重新生成` / `改用 AI 要点` ChipButton（生成中禁用） | 同：`Sunken` 底（4%，见未解决 1）+ `D4CornerRadius_10` + padding 12,10；`SlidePositionText`（`第 %lld / %lld 页`，mono 13 SemiBold）+ `SlideDocumentName`；`PreviousSlideCommand`/`NextSlideCommand`（`SlideHasPrevious/Next`）；`重新生成`/`改用 AI 要点` 按 `SlideNotesAreScript` 切换 | 一致（逐页生成中 spinner 未做，见未解决 2） |
| 6 | 讲稿要点：done `checkmark.circle.fill` 绿 / next `arrow.right.circle.fill` accent（14 semibold）/ pending `circle` secondary；done 文字 secondary | `SlidePointRows`：`IconCheckmark`（`Ready` 绿）/`IconArrowRight`（`InkBrush`）/`IconCircle`（Secondary）；`point.next` 14 SemiBold、`point.done` secondary | 一致 |
| 7 | 过渡：`下一页：…`（讲稿）或 `过渡：…` 12 secondary | `SlideTransitionText`（`下一页：%@` / `过渡：%@`） | 一致 |
| 8 | 听众问题：「听众可能会问（点一下提前准备回答）」11 medium secondary + 每问 `questionmark.bubble` 12 accent，点击 `state.ask` | 同文案 + `IconQuestionmarkBubble` 按钮（`AskSlideQuestion_Click` → `AskSlideQuestionCommand` → `AnswerAsync`） | 一致（问题文字用 `InkBrush`；Mac 是 Brand.accent=Ink.ink，等价） |
| 9 | 无 notes 时：「正在根据这一页和你的资料写讲稿提示…」Note | `!HasSlidePoints` 时显示同文案 | 一致 |
| 10 | `ScreenSection`：display 图标 + `屏幕：<kind.title>` 12 semibold +「已识别 · 是否用于回答见“本次依据”」12 secondary + 「N 秒前」11 mono（TimelineView 每 5 秒）+ 识别文字 12 secondary `lineLimit(expanded ? 30 : 3)` + 展开识别的文字/收起 tapText 11 accent | 同：`ScreenTitleText`（`屏幕：%@`，kind 标题 题目/代码/幻灯片/文档/聊天/视频通话/其他）+ `已识别 · 是否用于回答见“本次依据”` + `ScreenAgeText`（`%lld 秒前`，面板 5 秒 DispatcherTimer 刷新）+ `ScreenText`（`ScreenTextMaxLines` 3↔30）+ 展开识别的文字/收起 | 一致 |
| 11 | `AnswerProgressStrip`：`MetricPill(symbol: checkmark.circle, text: 已讲 x/y, tint: 完成绿/次要)` + 「下一句：…」12 medium accent 单行 + Spacer；只在录音中、非回答中、未回看历史、答案 ≥2 句时显示 | 同：hairline 药丸（`IconCheckmark` +「已讲」+ `AnswerProgressCounterText`，tooltip `AnswerProgressMetricText`）+「下一句：」+ `NextAnswerPointText`；`HasAnswerProgress` 条件与 Mac 相同 | 一致（完成态变绿未做，见未解决 3） |
| 12 | 嵌入：会中页左栏，`screenWatch && followsSlides` 时 240 高、左右 padding 16、上 padding 12 | `LiveView.axaml`：`<views:InsightsPanel x:Name="LiveInsightsPanel" Height="240" Margin=16,12,16,0 IsVisible="{Binding LiveShowsInsightsPanel}"/>`；`LiveShowsInsightsPanel = InsightScreenWatch && SelectedMode==Academic`（Mac `followsSlides` 仅 Academic 为 true） | 一致 |
| 13 | 自动刷新：`insights.slide` / `screen` / `answerProgress` 变化 | 订阅 `SlideStatus`/`CurrentSlidePointsText`/`ScreenWatchStatus`/`ScreenWatchCapturing`/`ScreenContextText`/`ScreenWatchKind`/`AnswerText`/`Answering`/`Recording`/`CurrentSession` → `RefreshInsights()`；我的发言 `RaiseAnswerProgress()`；回答完成再刷新；5 秒定时器刷新相对时间 | 一致 |

## 点按/行为验证

- `--demo --self-test=live-ui`（2026-11-21 13:0x，iso 数据目录）：断言 `LiveShowsInsightsPanel` 规则；把屏幕感知打开、场景切到答辩后，`LiveInsightsPanel.IsVisible=true` 并截 `insights-embedded-light.png`；随后恢复 demo 状态。
- 翻页/重新生成沿用 E9 已实现的 `StepSlide` 与 `SlideNotesCacheStore`（手动翻页暂停自动跟随，屏幕换页恢复；重新生成删缓存写回）。
- 立即识别：`RequestScreenReadCommand` 先确保屏幕感知开启，再走 E9 单帧读屏。
- 答案进度：`AnswerPoints.Split(BilingualAnswer.Spoken(答案))`（≤10 句）+ 近 120 秒我的发言 `HeuristicCovered` + `Next`，说话/回答完成触发刷新。

## 未解决/已知差异

1. 幻灯片区底色用 `Sunken`（ink 4%），Mac `Brand.accent.opacity(0.06)` = 6%。
2. Mac 的 `insights.generatingNotes` 逐页生成中 spinner、`slideNotesError` 橙色 note 未移植（Windows 讲稿提示为本地脚本/要点生成，不调模型）。
3. `MetricPill` 已讲完成态变绿未做（Windows 用次要色）；`MetricPill` 组件由 §6.1 建设计系统组件，落地后替换。
4. `ScreenKind` 的标题由 Windows 本地分类（E9 启发式）给出，Mac 用屏幕上下文模型的 kind；分类结果不同时标题可能不同。
5. 独立「屏幕与讲稿」窗口（`InsightsPanelWindow`）仍保留为工具菜单入口（Mac 会中页不再单开窗口）；会中页左栏为用户主要入口。

## 自动验证

- `tests/NextThought.Tests/InsightsPanelParityTests.cs`（8 项）：头部/幻灯片/屏幕/答案进度/刷新/面板嵌入/ Mac 文案/证据文件。
- `tests/NextThought.Core.Tests/AnswerPointsInsightsTests.cs`（前一轮 6 项）：切句/上限/下一句/中英覆盖/双语不入朗读。
