# 首页（HomeView.swift ↔ HomeView.axaml）逐元素对照

- Mac 标准：`Sources/NextThought/HomeView.swift`；Mac 3.4.0 参考图 [home-light](../../mac-screens/home/home-light.png) / [home-dark](../../mac-screens/home/home-dark.png)（2026-11-21 实机渲染；旧 v3 图 `docs/screenshots/v3/home.png` 仅留档）。
- Windows 截图：`validation/screens/current/home-light.png` / `home-dark.png`（task-48 用 `--demo --screenshots` 刷新，未入库；旧 `windows-*.png` 留档）。
- 旧基线：`light.png` / `dark.png` / `en-*.png` 是 D1 之前的 Windows 首页，仅留档，不再作为对照标准。

## 元素对照

| Mac（HomeView.swift） | Windows 对应物 | 结论 |
| --- | --- | --- |
| 日期 overline（`month(.wide).day().weekday(.wide)`） | `HomeView.axaml` 的 `DateLine`，`TextBlock.overline` | 一致（2026/11/21 星期六；英文模式走字符串表） |
| 两行 hero：问候（ink）+「接下来是什么场合？」（tertiary 75%） | `HomeGreeting`（夜深了。/上午好。/下午好。/晚上好。）+ `HomeQuestion`，`Classes=hero` | 一致；问候语按 Mac 五档时间判断，文案来自 `UiText` 表 |
| `OnboardingResumeRow`（引导「稍后继续」后出现） | `Views/Home/OnboardingResumeRow.axaml` | 组件已建、接线已通；Windows 引导（E3）尚未存「稍后继续」步骤，故 `OnboardingResumeVisible` 恒为 false，演示截图不可见 |
| `interruptedNotice` 行（上次被中断时显示「查看/知道了」） | 无 | **Windows 尚无中断提示状态**，本轮未移植；属 A 组恢复逻辑，待后续任务 |
| `StartComposer`：38pt 场景图标、19pt 标题框、场景胶囊（选中 13% 场景色）、17pt 衬线承诺、三条说明（4px 场景色圆点）、`⌘⇧N 不挑场合，立即记录`、先准备资料、开始（7px 印章红点） | `Views/Home/StartComposer.axaml/.axaml.cs` | 一致：图标 38/标题 19/胶囊高 30/选中 13% 场景色随主题明暗/承诺 17 衬线/moments 横排 16px 间距/按钮文案与动作一致；快捷键按 Windows 写成 `Ctrl+Alt+N`（G10 允许的修饰键差异） |
| 标题输入即时猜场景（`MeetingTypeGuesser`，手动选场景后 pin 住） | `AppState.GuessSceneFromTitle` + `StartComposer.Title_Changed/scenePinned` | 一致：输入标题即切换场景高亮，手动点胶囊后不再被标题覆盖，清空标题后恢复猜测 |
| 选场景应用场景默认（自动回答/提词宽度/模板/回答风格） | `AppState.ApplySceneDefaults`，开始/准备前调用；`Settings.AutomaticAnswers`、`SelectedCueWidth`、`TemplateKind` | 一致；回答风格在开始记录时随 `SceneDefaults.AnswerStyle` 进入会话上下文（与 Mac beginNewMeeting 相同） |
| `ContinueSeries`：最新 3 个非 standing 系列，行 = 场景色 7px 圆点、系列名、类型名、`N 场记录 · M 份资料`、右箭头；点击准备下一场 | `Views/Home/ContinueSeries.axaml` + `AppState.RefreshHomeSeries/PrepareNextMeetingInSeries` | 一致（含全部资料夹跳转）。**顺序差异**：Mac 用 `lastSessionDate ?? updatedAt`，Windows 无 updatedAt，未开会系列退化为创建时间，故演示里「ACL 2026」排第二而非第一；真实使用（每次开会都会更新 lastSessionDate）不受影响 |
| `HomeScreenshotCard`：截图卡片（图 + 分析截图/取消），空闲时区域/窗口截图落在这里 | `Views/Home/HomeScreenshotCard.axaml` + `AppState.SetScreenshotPreview/AnalyzeScreenshotAsync`；`MainWindow.CaptureRegionAsync/CaptureWindowAsync` | 结构/动作一致：空闲截图→卡片等待；分析截图→现有 OCR→提问链路；取消→删临时文件。录屏中截图仍立即分析（live 页行为）。演示数据不会产生 `screenshotPreview`，故截图里不出现该卡 |
| `RecentMeetings`：最近 4 场（排除正在录的），场景色圆点、标题、`MeetingOutcome.line`、`MeetingOutcome.when`、chevron；空态文案「开过的会都在这里…」 | `Views/Home/RecentMeetings.axaml` + Core `MeetingOutcome` | 一致；`line`/`when` 逐条对 Mac 规则（正在整理纪要 / 纪要没有整理好 / N 道题待改进 / N 个待办 / 场景 · N 分钟；今天/昨天/周几/M月D日），中英文均有单测 |
| 页面背景：`InkWash(tint: scene.theme.tint, animated: !reducedEffects)`，高 380 | `HomeView.axaml` 的 `InkWash Mode=SelectedMode Height=380` | 一致（动画受 `Motion.Reduce` 控制，见 D6） |
| 内容宽 760、水平 padding 32、上/下 40 | `MaxWidth="760"` + `D4Margin_32_40_32_40` | 一致 |

## 动作逐一点过（演示数据）

1. 首页渲染：问候两行、场景卡、接着上一场 3 行、最近记录 4 行。
2. 输入标题「字节跳动 后端二面」→ 场景自动切到「面试」，图标/承诺/三条说明/水墨随场景变。
3. 点「汇报 / 答辩」胶囊 → 场景 pin 住，再输入标题不再被猜场景覆盖。
4. 「先准备资料」→ 进入准备页（`AppSection.Prepare`），场景与标题带过去。
5. 「开始」→ 走 `StartRecordingCommand`（演示模式下进入会中状态，见 live 截图）。
6. 「接着上一场」某行 → 准备页带出该系列场景；「全部资料夹」→ 资料夹页。
7. 「最近记录」某行 → 打开该场会议（会议页）；「全部记录」→ 会议页。
8. 截图卡：真实截图（空闲）→ 首页出现卡片；「分析截图」→ 现有 OCR 问题链路；「取消」→ 卡片消失、临时文件删除。

## 未完成/已知差异

- `OnboardingResumeRow` 恒隐藏（等 E3 引导流程存「稍后继续」步骤）。
- Mac `interruptedNotice` 行未移植（Windows 无对应中断状态）。
- 截图卡的「分析截图」未在演示数据下点过（演示不产生 screenshotPreview），只做了结构 + 真实截图路径验证。
- Mac 的场景胶囊 matchedGeometry/按压缩放与标题框 focus 场景色描边未移植（Windows 用底色与焦点环近似）。
- 快捷键修饰键按 Windows 写为 `Ctrl+Alt+N`（G10 允许）。
- 序列顺序差异见上表 ContinueSeries 行。

## 自动验证

- `tests/NextThought.Core.Tests/MeetingOutcomeTests.cs`：5 项（状态、计数、场景+分钟、when 中英）。
- `tests/NextThought.Tests/HomeParityTests.cs`：8 项（子视图同名/顺序、hero、猜场景、场景默认、继续系列、最近记录、截图卡、OnboardingResume 与 demo 刷新）。
- 真实启动：`--demo --self-test=startup` → `startup-check: passed`。

## Mac 3.4.0 参考图与逐图核对（task-48）

Mac 3.4.0 参考（2026-11-21，macOS 26.5.2，1120×820 pt @2x = 1600×1171 px）：
[home-light](../../mac-screens/home/home-light.png) / [home-dark](../../mac-screens/home/home-dark.png)。
Windows 最新批次（`--demo --screenshots`，未入库）：`validation/screens/current/home-light.png` / `home-dark.png`（1120×820）。

- 逐图核对（浅/深各看一遍）：日期 overline、两行 hero、场景卡图标/标题/胶囊/承诺/三条 moments、`不挑场合，立即记录`、先准备资料/开始按钮、背景水墨、内容宽 760 与上下留白一致；场景胶囊**顺序**本轮已按 Mac `SessionMode.builtIn` 修正为 面试、组会、汇报 / 答辩、小组作业、客户沟通、工作会议、讲座 / 课程、小组讨论、通用（`AppState.Audio.cs` 的 `SceneChoices`，旧顺序把「汇报 / 答辩」排在「组会」前）。
- 差异（未改，记录）：Mac 顶部是 首页/所有记录/系列 胶囊导航 + 交通灯，Windows 是 首页/会议/资料库；区块标题 Mac「继续一个系列 / 全部系列」↔ Windows「接着上一场 / 全部资料夹」（Windows 文案来自旧字符串表，需 localization 统一）；Mac 图含第二屏「最近记录」首行，Windows 820 高截到同一区块之前，滚动后内容一致。
- Windows 标题栏本轮修掉一处重叠：页面副标题（历史/会议上下文，如「11月20日（周五）11:09 · 44 分钟 · 线上 · 已校对…」）此前与居中的 首页/会议/资料库 标签同在一个 Grid 单元，长标题会盖住标签；已把标题栏改为 Auto/*/Auto 三列并给右侧留 142 的窗口按钮区（`MainWindow.axaml`）。刷新后的 `current/settings-general-light.png` 可见副标题已不与标签重叠。Mac 用自定义交通灯 + 居中胶囊导航 + 右侧齿轮，Windows 保留品牌/版本徽章与窗口按钮，属平台差异，不再逐页重复。
- 其它元素（猜场景、场景默认、ContinueSeries 顺序、RecentMeetings、ScreenshotCard、OnboardingResume）沿用上表的结论，Mac 图不改变判定。
