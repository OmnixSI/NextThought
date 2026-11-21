# 洞察面板（InsightsPanel.swift ↔ InsightsPanel.axaml）逐元素对照

- Mac 标准：`Sources/NextThought/InsightsPanel.swift` 的 `InsightsPanel`、`ScreenWatchOffHint`、`SlideSection`、`ScreenSection`、`AnswerProgressStrip`；幻灯片跟随数据由 `AppState+Insights.swift` / `LiveInsights.swift` 维护。
- Windows 对应物：`Views/InsightsPanel.axaml(.cs)`、`Views/InsightsPanelWindow.axaml`（洞察窗口）、`AppState.Insights.cs`（E7 新增，原为占位 partial）、`AppState.SlideFollow.cs`（E9 已有的翻页/缓存/屏幕分类）。
- 截图：本轮收尾要求不再启动应用，`insights/` 浅/深图由 Lead 的全量截图批次补齐后再放进本目录（面板同时出现在“洞察”窗口；会中页嵌入版依赖 E11 对 LiveView 的改动，见未解决项）。

## 元素对照

| Mac | Windows | 结论 |
| --- | --- | --- |
| 头部：「屏幕与讲稿」+「立即识别」按钮（读屏中显示进度）+ 齿轮（设置 → 高级 → 回答服务 → 屏幕与讲稿） | InsightsPanel 头部：「屏幕与讲稿」overline + 「立即识别」（`RequestScreenReadCommand`→`ReadScreenNowAsync`）+ 读屏中 ProgressBar + 齿轮（跳设置） | 一致 |
| `ScreenWatchOffHint`：说明 + 「打开屏幕感知」按钮 | 关闭态卡片：屏幕感知标题 + 说明（复用现有译文句）+ 打开按钮（`EnableScreenWatch`：写设置、保存、初始化状态） | 部分一致：长说明句未逐字采用（避免新增未翻译字符串），打开行为一致；Mac 用眼睛图标，Windows 用文字按钮 |
| `SlideSection`：第 N / M 页 + 文档名 + 上一页/下一页 + 重新生成/改用 AI 要点 + 要点列表（已讲 ✓ / 下一句 → / 未讲 ○）+ 过渡/下一页提示 + 听众可能会问（点击即提问） | 同：`SlidePositionText`（第 N 页 + “ / M”）、`SlideDocumentName`、`PreviousSlideCommand`/`NextSlideCommand`（`SlideHasPrevious/Next` 控制可用）、`RegenerateNotesCommand`（重新生成/改用 AI 要点按 `SlideNotesAreScript` 切换，强制重算并写回 `SlideNotesCacheStore`）、`SlidePointRows`（Done→✓、Next→→、Pending→•，Next 加粗）、`SlideTransitionText`（`下一页：%@` / `过渡：%@`）、`SlideQuestionRows` 点击走 `AskSlideQuestionCommand`（立即回答该问题） | 一致；已讲/下一句由 `AnswerPoints.HeuristicCovered`+`Next` 从我近 120 秒的发言判定（Mac 同样按我这侧 speech 判） |
| `ScreenSection`：屏幕类型（幻灯片/题目/代码）+「已识别 · 是否用于回答见本次依据」+ N 秒前 + 识别文字（3 行/展开 30 行） | 同区域：`ScreenStatusLine`（读屏状态 · 来源：`SelectedInsightSourceLabel`）、`HasScreenError` 警告行、`ScreenText`（`ScreenTextMaxLines` 3 ↔ 30，展开/收起按钮）+ `ScreenIsReading` 进度 | 部分一致：没有「N 秒前」相对时间；屏幕类型标题（Mac `screen.kind.title`）未显示，仅状态/来源行；其余一致 |
| `AnswerProgressStrip`：已讲 x/y（`MetricPill`，说完变绿）+「下一句：…」 | 答案进度卡片：`AnswerProgressCounterText`（x / y）+ `NextAnswerPointText`（下一句原文，箭头图标）；`HasAnswerProgress` 只在录音中、非回答中、有 ≥2 句、且未回看历史时显示 | 一致（Mac 的 MetricPill 绿色完成态未做，用主题色） |
| Mac 会中页左栏在 `screenWatch && followsSlides` 时嵌入 `InsightsPanel`（高 240） | 洞察内容通过「洞察」按钮打开的 `InsightsPanelWindow` 提供；LiveView 主体本轮未嵌入（E11 正在改 LiveView，避免冲突） | 部分一致（嵌入版待 E11 之后接） |
| Mac 面板在 `insights.slide`、`screen`、`answerProgress` 变化时自动刷新 | `InsightsPanel` 监听 `SlideStatus`/`CurrentSlidePointsText`/`ScreenWatchStatus`/`ScreenWatchCapturing`/`AnswerText`/`Answering`/`Recording`/`CurrentSession` 并调用 `RefreshInsights()`；我的发言行进入时 `RaiseAnswerProgress()`，回答完成时再刷新 | 一致 |

## 点按/行为验证（结构测试与可测部分）

- `--self-test`：本轮收尾不允许启动应用，未新增 insights 自检；可测行为由 `InsightsPanelParityTests`（6 项）+ `AnswerPointsInsightsTests`（6 项）锁定：面板各绑定/命令存在、Mac 五段视图都在、翻页与重新生成命令接线、屏幕段字段、答案进度算法（切句/去 Markdown/中英匹配/下一句/双语不计入朗读）。
- 幻灯片翻页：`NextSlideCommand`/`PreviousSlideCommand` 沿用 E9 已实现的 `StepSlide`（手动翻页暂停自动跟随，直到屏幕换页），面板按钮只做呈现。
- 重新生成：删除内存缓存 → 用 `SlideNotes.Script`（有演讲备注）或本页文本重新生成要点/过渡 → 写回缓存文件；面板随即刷新。
- 立即识别：`RequestScreenReadCommand` 先确保屏幕感知开启，再调用 E9 的单帧读屏；读屏中头部显示进度条。
- 答案进度：`AnswerPoints.Split(BilingualAnswer.Spoken(答案))` 切句（≤10 句），用我听过的近 120 秒发言逐句 `HeuristicCovered`，`Next` 找下一句；说话/回答完成会触发刷新。

## 未解决/已知差异

- 会中页左栏的嵌入版 InsightsPanel 未接（需要 LiveView 小改，等 E11 结束后补）。
- 「N 秒前」相对时间、屏幕类型标题（幻灯片/题目/代码）、MetricPill 完成态绿色、Mac `ScreenWatchOffHint` 原句未逐字采用（避免新增未翻译字符串）。
- Mac 的 `insights.generatingNotes` 逐页生成中状态与 `slideNotesError` 提示未移植；Windows 的讲稿提示是“本地脚本/要点”生成，不调用模型。
- 浅/深截图待 Lead 全量截图批次后放入本目录。

## 自动验证

- `tests/NextThought.Tests/InsightsPanelParityTests.cs`：6 项（头部/幻灯片/屏幕/答案进度/刷新/证据）。
- `tests/NextThought.Core.Tests/AnswerPointsInsightsTests.cs`：6 项（切句/上限/下一句/中英覆盖/双语不入朗读）。
