# 系列页（WorkspacesView.swift ↔ SeriesView.axaml）逐元素对照

- 对照基准：Mac v3.4.0（`f015e8c6`）。Mac 参考图：[series-light](../../mac-screens/series/series-light.png) / [series-dark](../../mac-screens/series/series-dark.png)（左系列列表 + 右详情；2026-11-21，1120×820 pt @2x）。
- Windows 截图：`Windows/validation/screens/current/series-light.png` / `series-dark.png`（1120×820，2026-11-21 隔离数据目录 `--demo --screenshots`，未入库；已按截图核对右详情标题/统计行/开下一场位置）。
- 相关源码：`Sources/NextThought/WorkspacesView.swift`、`WorkspaceActions.swift`、`MeetingStatsViews.swift`；Windows `Views/SeriesView.axaml(.cs)`、`AppState.Series.cs`、`AppState.Home.cs`、`MeetingStats.cs`/`MeetingStatsText.cs`。

## 1. 页面骨架

| # | Mac 元素（WorkspacesView.swift） | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 顶部导航「系列」 | 顶部胶囊导航已由 §5.2 落为「首页/所有记录/系列」，`series-light.png` 可见「系列」选中；页面标题「系列」+ 说明句 + `SeriesSummary` | 逐条一致 |
| 2 | 左栏：标题「系列」+ 计数、`+` 新建；搜索「搜索系列或资料」；「排序 最近活动 ▾」 | 左栏卡：`ListBox SeriesRows`（名称/`Meta`/`Preview`），顶部「新建系列」输入+按钮；无搜索框、无排序下拉 | 不一致：缺少左栏搜索与「排序」下拉（Mac 3.4 新增；记录） |
| 3 | 左列表行：场景图标圆角块、系列名、`M 份资料 · N 题 · K 场会议`、选中行 Sunken 底 | 列表行：Name/Meta/Preview，选中态用 ListBox 默认选中样式（D5 主题资源） | 不一致：缺场景色图标块与 Mac 统计顺序（记录） |
| 4 | 右详情：hero 标题（衬线大字）、场景/统计元信息行 | `SelectedSeriesTitle`（Serif 22 medium）、`SelectedSeriesMeta`（会议/经验/待确认数） | 逐条一致 |
| 5 | 右详情 hero 下方 `SeriesStatsLine`：`共 N 场 · 时长 · 提问 N 次 · 回答 N 次`（0 场不显示） | 本轮新增 `SelectedSeriesStatsLine` + `HasSelectedSeriesStats`（`MeetingStatsText.SeriesLine`） | 逐条一致 |
| 6 | 右上「+ 开下一场」primary 按钮、「配置」按钮 | `{loc:Tr 开下一场}`（`OpenNextMeetingInSeriesCommand`）、`编辑系列`、`把这场景加入`、`移出`。Windows 右详情较窄，本轮把操作行放到标题/统计行下方（Mac 同一行），否则标题会被挤成一字一行 | 不一致：窄宽度下操作行换行（动作/内容一致，布局差异记录在案） |
| 6b | 标题/统计行不换行 | 标题 `NoWrap`+`TextTrimming`，`SelectedSeriesMeta` 与 `SelectedSeriesStatsLine` 同理（截图 `series-light.png` 已恢复单行） | 逐条一致 |
| 7 | 三个标签：记录 / 资料 / 经验积累 | Windows：会议列表（`SeriesMeetings`，带「有纪要」chip）、系列技能勾选、知识/决定待确认卡、经验（`SeriesLessons`）分块展示；没有 Mac 的三段标签容器（内容都在同页分块） | 不一致：标签容器改为分块（内容齐全，信息架构差异；记录） |
| 8 | 记录行：状态点、标题、时间/结果，点击打开 | `SeriesMeetings` 行（标题 + 有纪要 chip）；打开记录用 `SeriesRecords`/`OpenSeriesRecordAsync`（会议页细节） | 部分不一致：列表行没有时间/结果摘要（记录） |
| 9 | 配置/编辑：打开系列编辑器（各标签：基本信息/记录/资料/知识/技能/复核） | `EditSeries_Click` 打开 `SeriesEditorWindow`（E20 逐元素对照已覆盖编辑器标签） | 逐条一致（编辑器本身沿用 E20 compare） |

## 2. J4 系列命名与动作

| # | 要求 | Windows 现状 | 结论 |
|---|---|---|---|
| 1 | 界面里搜不到「资料夹」 | Desktop UI 源（XAML/CS）已无「资料夹」：首页继续系列、备份复核、设置技能说明、系列空态全部改「系列」；旧的 Windows-only 表项保留但不被引用 | 逐条一致（`HomeStage4Tests.NoInterfaceTextUsesTheOldSeriesWord` 断言） |
| 2 | 首页「继续一个系列」每行显示开过几场/累计多久 | `HomeSeriesRow.Counts` = `N 场 · 时长 · M 份资料`；无场次 `还没开过 · M 份资料` | 逐条一致 |
| 3 | 点首页系列 → 在这个系列里开下一场 | `PrepareNextMeetingInSeries`：有上一场带上一场转写；无上一场新 draft 绑定系列 + 资料/经验；`HomeStage4Tests` 结构断言 | 逐条一致 |
| 4 | 系列页「开下一场」 | `SeriesView.axaml` 右上按钮 + `AppState.Series.OpenNextMeetingInSeriesCommand` | 逐条一致 |
| 5 | 系列名下一行统计 | 详情 hero 下 `SelectedSeriesStatsLine`（Mac `SeriesStatsLine` 同文案） | 逐条一致 |

## 3. 动作记录（`--demo`，截图刷新后补实测日期）

1. 首页「继续一个系列」三行：每行场景色圆点/名称/场景名/「N 场 · 时长 · M 份资料」/箭头；点行进入准备页且带该系列（有上一场则带上转写）。
2. 「全部系列」→ 系列页；系列列表选择某系列 → 右侧标题、元信息、统计行（共 N 场 · 时长 · 提问/回答）刷新。
3. 系列页「开下一场」→ 准备页，标题/场景/资料/经验就位。
4. 「编辑系列」→ 编辑器窗口（E20 标签）；「把这场景加入/移出」仍可用。
5. 全局搜索「资料夹」：界面文案无结果（仅生成表历史键保留）。

## 4. 未解决 / 已知差异

- 左栏搜索与「排序」下拉未移植（Mac 3.4 WorkspacesView）；左列表行缺场景图标块与 Mac 统计顺序。
- Mac 三段标签（记录/资料/经验积累）在 Windows 为同页分块，内容齐全但信息架构不同；记录行缺 when/outcome 摘要。
- 系列页入口：Mac 顶部导航有「系列」，Windows 为 Settings 旁独立 Section（§5.2 导航重做后可能调整）。
- 截图：共享树构建稳定后重拍 `current/series-{light,dark}.png`，按本表复核视觉。

## 5. 自动验证

- `Windows/tests/NextThought.Tests/HomeStage4Tests.cs`（系列页「开下一场」、J4 文案/无「资料夹」）。
- `Windows/tests/NextThought.Tests/SeriesTests.cs`、`SeriesEditorTests.cs`（E20 结构/动作）。
- `Windows/tests/NextThought.Core.Tests/MeetingStatsTests.cs`（`SeriesLine` 文案）。
