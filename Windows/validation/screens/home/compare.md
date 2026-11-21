> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 首页（HomeView.swift ↔ HomeView.axaml）逐元素对照

- 对照基准：Mac 3.4.0（`f015e8c6`）。Mac 参考图：[home-light](../../mac-screens/home/home-light.png) / [home-dark](../../mac-screens/home/home-dark.png)（2026-11-21，1120×820 pt @2x = 1600×1171 px）。
- Windows 截图：`Windows/validation/screens/home/windows-{light,dark}.png`（首屏）与 `windows-stats-{light,dark}.png`（滚到底的统计区），1120×820，2026-11-21 用隔离数据目录加 `--demo --screenshots` 生成并入库；`current/home*.png` 是同一 harness 的最新产物（不入库）。
- 相关源码：`Sources/NextThought/HomeView.swift`、`MeetingStatsViews.swift`；Windows `Views/HomeView.axaml`、`Views/Home/*`、`AppState.Home.cs`、`MeetingStats.cs`/`MeetingStatsText.cs`。

## 1. 页面骨架（从上到下）

| # | Mac 元素（HomeView.swift） | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 日期 overline（`月份 日 星期`） | `HomeView.axaml` 的 `DateLine`，`TextBlock.overline` | 逐条一致 |
| 2 | hero 两行：问候（`Ink.ink`）+「接下来是什么场合？」（`Ink.tertiary` 0.75），`Typo.hero` 38 衬线中粗 | `HomeGreeting`/`HomeQuestion` 两行，`Classes="hero"`（§5.1 字体任务负责最终字重/衬线） | 逐条一致（字重由 §5.1 统一校准） |
| 3 | `OnboardingResumeRow`：引导「稍后继续」行 | `Views/Home/OnboardingResumeRow.axaml`，`OnboardingResumeVisible` 控制 | 逐条一致（演示数据无 deferred 版本时不可见） |
| 4 | `interruptedNotice` 行：警示图标 + 文案 +「查看」「知道了」 | `HomeView.axaml` 警示行，`HasInterruptedNotice`/`ReviewInterruptedCommand`/`DismissInterruptedNoticeCommand` | 逐条一致 |
| 5 | `InkWash(tint: scene.theme.tint, animated: !reducedEffects)` 高 380，页面背景随场景 | `Views/Controls/InkWash`，`Mode="{Binding SelectedMode}" Height="380" Animated="{Binding HomeInkWashAnimated}"`（`AppState.HomeInkWashAnimated = !Motion.Reduce`，系统动画关闭或应用「减少视觉特效」时为 false；InkWash 每帧也复查 `Motion.Reduce`，切换设置后立即停住） | 逐条一致 |
| 6 | 内容最大宽 760、水平 32、上下 40，居中 | `MaxWidth="760"`+`D4Margin_32_40_32_40`，`HorizontalAlignment="Center"` | 逐条一致（§5.7 若把全局内容列改为 720 需同步本页） |

## 2. StartComposer（起动卡）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 卡片：`Ink.surface`、圆角 22、contact+ambient 阴影、hairline 描边；标题聚焦时描边为场景色 45%（`lineWidth` 0.5→1，`Motion.hover`） | `Border x:Name="ComposerCard" Classes="card"` + `BrushTransition` 0.14s；`TitleBox` 聚焦时 `UpdateTitleFocus()` 把描边换成场景 tint 45%、粗细 1，失焦回 `Ink.hairline` 0.5 | 逐条一致 |
| 2 | 场景 glyph：38 pt 圆角方块，场景色 13% 底 + 场景图标、图标同场景色 | `controls:SceneGlyph Mode="{Binding SelectedMode}" Size="38"`（`SceneGlyph.cs`：13% tint + 图标） | 逐条一致 |
| 3 | 标题输入：19 pt medium、透明无边框；占位「给这场会起个名字，比如“字节二面”」，占位与正文同大、更深 | `TitleBox` 19 medium，透明边框；本地样式 `TextBox /template/ TextBlock#PART_Watermark` 19 pt + `Ink.secondary` | 逐条一致 |
| 4 | 场景胶囊：`SessionMode.builtIn` 顺序 面试→组会→汇报/答辩→小组作业→客户沟通→工作会议→讲座/课程→小组讨论→通用；**只有选中项**有场景色 13% 胶囊底 + 场景色文字，其余纯文字（secondary） | `AppState.Audio.cs` 的 `SceneChoices` 已按该顺序；chips 只负责文字颜色，场景色 13% 胶囊由 `SelectionCapsule` 在 `StartComposer.SlideSelection()` 里用 `Motion.snappy` 在选中 chip 间滑动（Mac `matchedGeometryEffect`） | 逐条一致 |
| 5 | 胶囊悬停 `Ink.hover`、按下 0.96 缩放 | `Button.scene:pointerover` 悬停 Hover；`Button.scene:pressed` 用 `ScaleTransform` 0.96（Mac `SceneChipStyle`） | 逐条一致 |
| 6 | 承诺句：17 pt 衬线 medium，`Ink.ink` | `SelectedScene.Promise`，SerifFamily 17 medium | 逐条一致 |
| 7 | 三条 moments：4 pt 场景色圆点 + caption secondary；场景切换整体 blurReplace | `MomentsList` 圆点取当前场景 tint；场景切换时 `BlurReplacePromise()` 用 `BlurEffect`（8→0）+ 淡入（`Motion.expand`）重放承诺句与 moments，匹配 Mac `.transition(.blurReplace)`；glyph 同时从 0.8 缩放淡入 | 逐条一致 |
| 8 | 左下：`ShortcutHint("⌘⇧N")` +「不挑场合，立即记录」 | 快捷键 chip `Ctrl+Alt+N`（G10 允许的 Windows 修饰键）+「不挑场合，立即记录」 | 逐条一致（修饰键差异属 G10） |
| 9 | 右下：`先准备资料`（secondary 描边）+ `开始`（primary 黑底白字，7 pt seal 红点）；`isChangingSession` 时进度点/禁用 | `先准备资料` 为默认描边按钮（secondary 类）；`开始` `Button.primary` + 7 pt `Seal` 圆点；`CanExecute` 随状态 | 逐条一致（`isChangingSession` 小进度点未加，见 §6） |

## 3. 继续一个系列（ContinueSeries）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 标题 overline「继续一个系列」；右上「全部系列」文本按钮 | `ContinueSeries.axaml`：`{loc:Tr 继续一个系列}`、`{loc:Tr 全部系列}`，命令 `NavigateCommand Series` | 逐条一致 |
| 2 | 行上方/下方 0.5 hairline 分隔 | `Border Height=0.5 Hairline` 同样上下分隔 | 逐条一致 |
| 3 | 行：7 pt 场景色圆点、系列名 body ink、场景名 caption secondary、右侧 `N 场 · 时长 · M 份资料`（没开过 `还没开过 · M 份资料`）、右箭头 tertiary | `HomeSeriesRow`：`ModeTint` 圆点、`Name`、`TypeName`、`Counts`（`MeetingStats` 计算的新格式/没开过文案）、`IconArrowRight` | 逐条一致 |
| 4 | 点击：有上一场 → `prepareNextMeeting(after:)`（带上一场转写）；无上一场 → 新 draft 绑定该系列；行 help「在这个系列里开下一场…」 | `ContinueSeries.axaml.cs` → `AppState.PrepareNextMeetingInSeries`：先找该系列最近一场走 `PrepareNextMeetingAfter`；否则 `EnsureDraft(series.EffectiveMode)` 带上系列资料/经验；行 ToolTip 已换 | 逐条一致 |
| 5 | 最多 3 个，按 `lastSessionDate ?? updatedAt` 排序，排除 standing 系列 | `RefreshHomeSeries`：`Take(3)`、`Standing != true`、`OrderByDescending(Last?.StartedAt ?? series.EffectiveUpdatedAt)`；`WorkspaceSeries.updatedAt` 已补进 Core（Mac 字段，解码/编码往返保留），演示数据按 Mac 截图顺序设值（ACL 未开过排最前） | 逐条一致 |

## 4. 最近记录 + 用了多少

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 「最近记录」overline + 右上「全部记录」→ 会议页 | `RecentMeetings.axaml`，`CommandParameter="History"`（本轮从 Live 改为会议列表） | 逐条一致 |
| 2 | 4 场最近会议（排除正在录的）：场景色圆点、标题 body、`MeetingOutcome.line` caption、右侧 `MeetingOutcome.when`、chevron；行分隔线；空态句 | `HomeRecentRows`（4 条）、`Outcome`/`When`（Core `MeetingOutcome`，中英单测）、`IconChevronRight`、空态文案 | 逐条一致 |
| 3 | `HomeStats`「用了多少」+ 右上「本月/累计」`InkSegmented`；4 个 `StatFigure`（20 pt 衬线 medium 数字 + caption 标签）：场会议、记录时长、听到的提问、给出的回答；上下 hairline；底部 caption「时长按每场从开始记录到结束计算；暂停的时间也算在内。」；无会议时整块不显示 | `Views/Home/HomeStats.axaml`：同结构；截图 `home-stats-light/dark.png` 实测本月 2 场会议 / 1 小时 22 分 / 6 听到的提问 / 2 给出的回答，本月/累计白块选中，说明句在底部；`MeetingStats.Of`+`Month`+`MeetingStatsText.Duration` 计算 | 逐条一致（Mac `InkSegmented` 待 §6.1 组件落地后替换本地样式；数字/规则由 `MeetingStatsTests` 与 all-scenarios 手算断言锁定） |
| 4 | 统计规则：场数=开始时间落在区间内；秒数=`endedAt ?? startedAt` − startedAt（未结束 0 秒但计场次）；提问/回答取 meta 计数；本月=当前月区间 | 同上（`MeetingStats.Of` interval 含头不含尾、series 过滤、`HoursAndMinutes` 向下取整） | 逐条一致 |

## 5. 动作记录（`--demo`，2026-11-21 隔离数据目录截图批次）

1. 首页渲染：日期、hero、场景卡、继续系列 3 行、最近记录 4 行、用了多少（本月）显示。
2. 输入标题「字节跳动 后端二面」→ 场景自动切「面试」，胶囊场景色底/文字、glyph/承诺/moments/水墨随之变化。
3. 点「组会」胶囊 → pinned，再输入标题不覆盖；场景底只在该胶囊上。
4. 「本月/累计」切换 → 4 个数字重算；截图批次本月 2 场 / 1 小时 22 分 / 6 / 2（演示两场会议 38+44 分钟）；`ImportedAllScenariosStatsMatchTheHandCalculatedMacNumber` 另用 all-scenarios 夹具断言 4 场 / 90 秒 / 2 / 0。
5. 「先准备资料」→ 准备页并带上场景/标题；「开始」→ 立即开始记录。
6. 继续系列行 → 带上一场转写进入准备页；没开过的系列 → 新 draft 绑定该系列。
7. 「全部记录」→ 会议列表；最近记录行 → 打开该会议。
8. 场景胶囊：点「组会」→ 场景色 13% 胶囊从「面试」滑到「组会」（`Motion.snappy`）、glyph 从 0.8 缩放淡入、承诺句 + 三条说明 blurReplace、水墨/圆点换成组会色；按下胶囊时 0.96 缩放。
9. 标题聚焦：点标题输入框 → 卡片描边变成场景色 45%、1 px（0.14s BrushTransition）；点「开始」等其它控件 → 描边回 hairline 0.5。
10. 减少视觉特效：设置里打开后回首页，水墨不再漂移（`HomeInkWashAnimated=false`，InkWash 每帧复查 `Motion.Reduce`）。
11. 继续系列顺序：ACL 2026（还没开过，updatedAt 最新）→ 周一客户例会（昨日）→ 字节后端二面（前日），与 Mac 截图一致。

## 6. 未解决 / 已知差异

- Windows 标题栏（品牌/导航/副标题）按 §5.2 由 settings-parity 重做，本页不再对照；截图核对只比较页面内容。
- 未实现的动效只剩：`isChangingSession` 时「开始」旁的小进度点（`StartComposer` 的 `ProgressView`；Windows 用按钮禁用表达同一状态）。
- 场景胶囊滑动用 `SlideSelection()` 的 Canvas 动画实现（Mac 是 matchedGeometryEffect），最终位置/尺寸/颜色与 Mac 一致；切步/展开曲线来自 D5 的解析弹簧。
- `WorkspaceSeries.updatedAt` 已补进 Core 并随 Mac 文件往返；Windows 侧在编辑系列时暂不推进 updatedAt（Mac 会），因此真实使用中排序仍以最近会议优先，updatedAt 只在「还没开过」的系列之间参与排序（J8 盘点记录）。
- 截图：`windows-{light,dark}.png` 与 `windows-stats-{light,dark}.png` 已核对；`home-stats` 是 harness 滚到底的第二张（820 高首屏看不到统计区），harness 还会在首页截图前刷新 Home 列表、demo 历史为空时重新载入演示数据，保证浅/深数字一致。

## 7. 自动验证

- `Windows/tests/NextThought.Tests/HomePolishTests.cs`（本轮新增：InkWash 绑定 reducedEffects、胶囊滑动/按压缩放、glyph 过渡、blurReplace、聚焦描边、updatedAt 排序）。
- `--demo --self-test=home`（本轮新增：运行时断言 InkWash.Animated 与 `HomeInkWashAnimated` 一致、reducedEffects 关闭动画、选中胶囊与 chip 位置对齐并在换场景后滑动到新 chip、glyph 拿到 ScaleTransform、聚焦描边为场景 tint 45%/失焦回 hairline、继续系列首行为 ACL、共 3 行）。
- `Windows/tests/NextThought.Tests/HomeStage4Tests.cs`（首页统计/系列文案/无「系列」/系列开下一场/场景胶囊/占位/按钮）。
- `Windows/tests/NextThought.Tests/HomeParityTests.cs`（子视图同名、hero、猜场景、场景默认、继续系列排序、最近记录、截图卡）。
- `Windows/tests/NextThought.Core.Tests/MeetingStatsTests.cs`（durationText/SeriesLine 文案与 Mac 值）。
- `Windows/tests/NextThought.Tests/HomeStage4Tests.ImportedAllScenariosStatsMatchTheHandCalculatedMacNumber`：导入 all-scenarios 后 4 场 / 90 秒 / 2 问 / 0 答 → “1 分钟”。
