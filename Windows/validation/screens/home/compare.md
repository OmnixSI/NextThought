# 首页（HomeView.swift ↔ HomeView.axaml）逐元素对照

- 对照基准：Mac v3.4.0（`f015e8c6`）。Mac 参考图：[home-light](../../mac-screens/home/home-light.png) / [home-dark](../../mac-screens/home/home-dark.png)（2026-11-21，1120×820 pt @2x = 1600×1171 px）。
- Windows 截图：`Windows/validation/screens/current/home-light.png` / `home-dark.png`（首屏）与 `home-stats-light.png` / `home-stats-dark.png`（滚到底的统计区）——1120×820，2026-11-21 用隔离数据目录加 `--demo --screenshots` 生成，未入库。
- 相关源码：`Sources/NextThought/HomeView.swift`、`MeetingStatsViews.swift`；Windows `Views/HomeView.axaml`、`Views/Home/*`、`AppState.Home.cs`、`MeetingStats.cs`/`MeetingStatsText.cs`。

## 1. 页面骨架（从上到下）

| # | Mac 元素（HomeView.swift） | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 日期 overline（`月份 日 星期`） | `HomeView.axaml` 的 `DateLine`，`TextBlock.overline` | 逐条一致 |
| 2 | hero 两行：问候（`Ink.ink`）+「接下来是什么场合？」（`Ink.tertiary` 0.75），`Typo.hero` 38 衬线中粗 | `HomeGreeting`/`HomeQuestion` 两行，`Classes="hero"`（§5.1 字体任务负责最终字重/衬线） | 逐条一致（字重由 §5.1 统一校准） |
| 3 | `OnboardingResumeRow`：引导「稍后继续」行 | `Views/Home/OnboardingResumeRow.axaml`，`OnboardingResumeVisible` 控制 | 逐条一致（演示数据无 deferred 版本时不可见） |
| 4 | `interruptedNotice` 行：警示图标 + 文案 +「查看」「知道了」 | `HomeView.axaml` 警示行，`HasInterruptedNotice`/`ReviewInterruptedCommand`/`DismissInterruptedNoticeCommand` | 逐条一致 |
| 5 | `InkWash(tint: scene.theme.tint, animated: !reducedEffects)` 高 380，页面背景随场景 | `Views/Controls/InkWash.axaml.cs`，`Mode="{Binding SelectedMode}" Height="380" Animated="True"` | 不一致：Windows 未按 `reducedEffects` 关闭动画（低优先级，§5 无障碍）；其余（tint/高度/顶部对齐）一致 |
| 6 | 内容最大宽 760、水平 32、上下 40，居中 | `MaxWidth="760"`+`D4Margin_32_40_32_40`，`HorizontalAlignment="Center"` | 逐条一致（§5.7 若把全局内容列改为 720 需同步本页） |

## 2. StartComposer（起动卡）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 卡片：`Ink.surface`、圆角 22、contact+ambient 阴影、hairline 描边；标题聚焦时描边为场景色 45% | `Border Classes="card"`（22/阴影/hairline 由 D5/D6 提供）；聚焦描边尚未随场景色变化 | 不一致：聚焦时 scene tint 描边未实现（记录，属细节动效） |
| 2 | 场景 glyph：38 pt 圆角方块，场景色 13% 底 + 场景图标、图标同场景色 | `controls:SceneGlyph Mode="{Binding SelectedMode}" Size="38"`（`SceneGlyph.cs`：13% tint + 图标） | 逐条一致 |
| 3 | 标题输入：19 pt medium、透明无边框；占位「给这场会起个名字，比如“字节二面”」，占位与正文同大、更深 | `TitleBox` 19 medium，透明边框；本地样式 `TextBox /template/ TextBlock#PART_Watermark` 19 pt + `Ink.secondary` | 逐条一致 |
| 4 | 场景胶囊：`SessionMode.builtIn` 顺序 面试→组会→汇报/答辩→小组作业→客户沟通→工作会议→讲座/课程→小组讨论→通用；**只有选中项**有场景色 13% 胶囊底 + 场景色文字，其余纯文字（secondary） | `AppState.Audio.cs` 的 `SceneChoices` 已按该顺序；`Button.scene` 底色改为 Transparent，选中时代码写 13% tint 底与 tint 文字 | 逐条一致（Mac matchedGeometry 位移与按压 0.96 缩放未做，记录为动效差异） |
| 5 | 胶囊悬停 `Ink.hover`、按下 0.96 缩放 | 悬停已有（`Button.scene:pointerover`），按下缩放无 | 不一致：按压缩放未实现（动效细节，记录） |
| 6 | 承诺句：17 pt 衬线 medium，`Ink.ink` | `SelectedScene.Promise`，SerifFamily 17 medium | 逐条一致 |
| 7 | 三条 moments：4 pt 场景色圆点 + caption secondary；场景切换整体 blurReplace | `MomentsList`，代码把 `Ellipse.Fill` 设为当前场景 tint；无 blurReplace | 逐条一致（切换动画未做，记录） |
| 8 | 左下：`ShortcutHint("⌘⇧N")` +「不挑场合，立即记录」 | 快捷键 chip `Ctrl+Alt+N`（G10 允许的 Windows 修饰键）+「不挑场合，立即记录」 | 逐条一致（修饰键差异属 G10） |
| 9 | 右下：`先准备资料`（secondary 描边）+ `开始`（primary 黑底白字，7 pt seal 红点）；`isChangingSession` 时进度点/禁用 | `先准备资料` 为默认描边按钮（secondary 类）；`开始` `Button.primary` + 7 pt `Seal` 圆点；`CanExecute` 随状态 | 逐条一致（`isChangingSession` 小进度点未加，记录） |

## 3. 继续一个系列（ContinueSeries）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 标题 overline「继续一个系列」；右上「全部系列」文本按钮 | `ContinueSeries.axaml`：`{loc:Tr 继续一个系列}`、`{loc:Tr 全部系列}`，命令 `NavigateCommand Series` | 逐条一致 |
| 2 | 行上方/下方 0.5 hairline 分隔 | `Border Height=0.5 Hairline` 同样上下分隔 | 逐条一致 |
| 3 | 行：7 pt 场景色圆点、系列名 body ink、场景名 caption secondary、右侧 `N 场 · 时长 · M 份资料`（没开过 `还没开过 · M 份资料`）、右箭头 tertiary | `HomeSeriesRow`：`ModeTint` 圆点、`Name`、`TypeName`、`Counts`（`MeetingStats` 计算的新格式/没开过文案）、`IconArrowRight` | 逐条一致 |
| 4 | 点击：有上一场 → `prepareNextMeeting(after:)`（带上一场转写）；无上一场 → 新 draft 绑定该系列；行 help「在这个系列里开下一场…」 | `ContinueSeries.axaml.cs` → `AppState.PrepareNextMeetingInSeries`：先找该系列最近一场走 `PrepareNextMeetingAfter`；否则 `EnsureDraft(series.EffectiveMode)` 带上系列资料/经验；行 ToolTip 已换 | 逐条一致 |
| 5 | 最多 3 个，按 `lastSessionDate ?? updatedAt` 排序，排除 standing 系列 | `RefreshHomeSeries`：`Take(3)`、`Standing != true`、`OrderByDescending(Last?.StartedAt ?? CreatedAt)` | 已知差异：无 `updatedAt` 时回退 `CreatedAt`（沿用旧 compare 记录；真实使用每次开会都会更新 lastSessionDate） |

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

## 6. 未解决 / 已知差异

- Windows 标题栏（品牌/导航/副标题）按 §5.2 由 settings-parity 重做，本页不再对照；截图核对只比较页面内容。
- 未实现的动效：场景胶囊 matchedGeometry/按压缩放、glyph 缩放过渡、承诺 blurReplace、标题聚焦场景色描边、`isChangingSession` 进度点（细节动效，记录在案）。
- `InkWash` 未绑定 `reducedEffects`（动画关闭无障碍项）。
- 继续系列无 `lastSessionDate` 时排序回退 `CreatedAt`（旧 compare 已记）。
- 截图：`home-{light,dark}.png` 与 `home-stats-{light,dark}.png` 已核对；`home-stats` 是 harness 新增的滚到底第二张（820 高首屏看不到统计区），harness 还会在首页截图前刷新 Home 列表、demo 历史为空时重新载入演示数据，保证浅/深数字一致。

## 7. 自动验证

- `Windows/tests/NextThought.Tests/HomeStage4Tests.cs`（首页统计/系列文案/无「资料夹」/系列开下一场/场景胶囊/占位/按钮）。
- `Windows/tests/NextThought.Tests/HomeParityTests.cs`（子视图同名、hero、猜场景、场景默认、继续系列、最近记录、截图卡）。
- `Windows/tests/NextThought.Core.Tests/MeetingStatsTests.cs`（durationText/SeriesLine 文案与 Mac 值）。
- `Windows/tests/NextThought.Tests/HomeStage4Tests.ImportedAllScenariosStatsMatchTheHandCalculatedMacNumber`：导入 all-scenarios 后 4 场 / 90 秒 / 2 问 / 0 答 → “1 分钟”。
