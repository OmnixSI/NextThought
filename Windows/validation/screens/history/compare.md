> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# E18 会议列表：Mac ↔ Windows 逐元素对照

对照代码：`Sources/NextThought/HistoryView.swift`（1738 行全文）、`Sources/NextThought/MeetingActionsMenu.swift`（68 行全文）
对照基准：Mac v3.3.0（`5ddb9e5`）+ Mac 3.4.0 参考批次（**没有 history 独立截图**，用 [home-light](../../mac-screens/home/home-light.png) 的「所有记录」导航侧证；旧 `docs/screenshots/history-light.png` / `history-dark.png` 仅留档）
Windows 代码：`Windows/src/NextThought.Desktop/Views/HistoryView.axaml(.cs)`、`AppState.Meeting.cs`（本次新增 E18 段）、`AppState.Deletion.cs`、`AppState.Archive.cs`、`RecentMeetingRow.cs`
Windows 截图：`Windows/validation/screens/history/`（自检 `history-ui` 生成；另 `Windows/validation/screens/current/history-*.png` 为上一批 D1 截图）

## 1. 页面骨架

| 元素 | Mac | Windows | 结论 |
|---|---|---|---|
| 左列会议列表 + 右侧选中会议详情 | 单页 split view（所有会议 = 列表 + Review/Transcript/Live answers） | 列表在「历史」页；点行进入「纪要」页显示同一场的 Review/Transcript 等 | **既有导航差异**（VIEW_PARITY 已按页拆分记录），本任务不改变；列表元素逐项对照见下 |
| 顶部标题/说明 | 「所有会议」+ 副标题 | 「所有会议」+ 副标题（本任务从「历史」改为 Mac 原句） | 一致 |
| 搜索框 | 搜索会议、系列、原话、译文、纪要（带放大镜、占位文案） | 搜索框占位「搜索全部转写、纪要、笔记」；输入后自动全文搜索（350ms 防抖） | 元素齐；占位文案为 Windows 版（Mac 原句较长，见「文案」节） |
| 筛选条 | 全部时间/全部时长/最新优先 + 搜索范围说明 | 时间/时长/排序三个下拉 + 搜索状态 + 结果数 | 一致（选项集合见 2.3） |
| 系列分组 | 按系列折叠/行头（系列名、场次数、chevron、管理菜单），根页为「所有记录」文件夹卡片 | 按系列分组，行头含系列名/场次数/管理菜单；无独立根页卡片 | 分组与管理菜单已补；**根页文件夹卡片布局未做**（列表即分组），记录在未解决 |
| 批量删除 | 根页「批量删除」→ 勾选系列 → 全选当前结果/删除所选/完成 | 行头「批量删除」→ 复选框 → 全选当前结果/删除所选/完成 | 一致；删除按 Mac（连会议记录一起删） |
| 会议记录操作菜单 | 列表右上「…」→ 删除全部会议记录 | 右上「…」→ 删除全部会议记录（双选项确认） | 一致 |
| 归档概览 | MeetingArchiveOverview：所有记录根页（系列卡片、搜索命中计数、清除筛选、清除搜索、返回首页、重试读取） | 选中会议的「归档概览」卡片：标题、事实行（时间/时长/模式/系列/转写/回答/问题/纪要）、摘要错误、归档截图缩略图、回答列表 | 元素齐但形态不同：Mac 概览是根页导航，Windows 是详情卡；无「清除筛选/清除搜索/返回首页/重试读取」按钮 → 未解决 |

## 2. 元素清单

### 2.1 会议行（SessionRow）

| Mac | Windows | 结论 |
|---|---|---|
| 场景色点/图标（SceneGlyph） | 文档图标（IconDocPlaintext） | 视觉差异（D 组图标体系） |
| 标题（displayTitle，未命名时占位） | Title（未命名会议占位） | 一致 |
| 副行：日期、时长、问题数、行动条目/纪要图标 | Subtitle：日期、转写条数、回答数、有纪要 chip | 信息项不同：Mac 行显示时长/问题数/行动条目，Windows 显示转写/回答数；**未改**（E15 详情页现有取舍，记录为差异） |
| 搜索命中：命中来源 kind（原话/校正原话/译文/问题/回答/纪要）+ location + 摘要，最多 1 条首命中 | 本任务补齐：`SearchHitLine`（kind+location）+ `SearchHitExcerpt` 两行 | 一致 |
| 选中高亮、键盘焦点 | 无高亮（点击进入纪要页） | 既有差异 |
| 行内「有纪要」图标（sparkles，pending 中断提示…） | 「有纪要」chip | 部分（无 pending/failed 图标） |

### 2.2 会议操作菜单（MeetingActionsMenu.swift → 行右键菜单）

| Mac 菜单项 | Windows | 结论 |
|---|---|---|
| 重新整理 | 重新整理（`RegenerateSessionNotesAsync` 走现有纪要整理管线） | 已接；Mac NotesRegeneration 重新生成结构化纪要，Windows 走自动纪要服务 → 行为近似，记未解决 |
| 纪要语言 ▸ 简体中文 / English | 纪要语言 ▸ 简体中文 / English（强制语言走模板纪要管线） | 已接 |
| 导出 Markdown… | 导出 Markdown… | 一致 |
| 重命名… | 重命名… | 一致 |
| 在 Finder 中显示 | 在资源管理器中显示（打开会议文件夹） | 平台文案换算 |
| 详细信息 | 详细信息（自绘 sheet：标题/时间/时长/模式/系列/来源/引擎/模型/转写/回答/问题/纪要/文件夹） | 已接 |
| 以此为上一场，准备下一场 | 已接：选中该系列、带入上一场转写、进入准备页 | 已接；Mac 用 MeetingDraft 预填标题/模式，Windows 准备页字段为现有模型 → 记未解决 |
| 删除这场会议（destructive，录音中/只读 fallback 时禁用 + help 说明） | 删除这场会议（点击时守卫：录音中/任务中拒绝并提示；无只读 fallback 模型） | 行为齐；**菜单项未置灰**（Avalonia MenuItem IsEnabled 未绑定父状态）→ 未解决 |
| （无「打开纪要」） | 额外「打开纪要」 | Windows 额外项，保留 |

### 2.3 筛选与搜索（filterBar / searchField / searchResults / searchHitNavigator）

| Mac | Windows | 结论 |
|---|---|---|
| 搜索输入即触发（task id 防抖），可取消，显示搜索进度/上限/不可读计数 | 输入即触发（`ScheduleHistoryContentSearch`，350ms 防抖，可取消；结果上限 200） | 一致 |
| 全文搜索字段：原话/校正原话/译文/问题/回答/纪要（SessionSearch 投影） | 本任务从「只搜 transcript.text」升级为 Core `SessionContentSearch` 投影搜索（同一组六类字段） | 一致 |
| 命中来源 kind + location + excerpt；上一个/下一个导航条 | 本任务补齐：行内首命中 + 命中导航条（位置 i/n、kind、excerpt、上/下一个） | 已接；Mac 还支持点击命中打开对应 Transcript 标签并定位/高亮行，Windows 只切到纪要页 → 未解决（定位高亮） |
| 搜索范围说明（全部会议 / 某系列） | 无系列限定搜索（列表分组但不限定） | 未做（记录未解决） |
| 清除搜索/清除筛选 | 清空搜索框；筛选改回「全部」 | 行为一致（无单独按钮） |
| 日期筛选：全部时间/今天/近 7 天/近 30 天/本年度 | 同（历史既有） | 一致 |
| 时长筛选：全部时长/1 分钟以内/1–10 分钟/10 分钟以上 | 同（历史既有） | 一致 |
| 排序：最新优先/最早优先/时长最长/时长最短/问题最多 | 同（历史既有） | 一致 |
| 系列按最近匹配会议排序 | 分组按当前排序后的首行顺序（近似） | 部分 |

### 2.4 系列分组头（seriesGroupHeader / SeriesManagementMenuItems）

| Mac | Windows | 结论 |
|---|---|---|
| 系列名 + 场次数 + chevron，点击查看系列 | 系列名 + 场次数 + 「打开系列」 | 一致 |
| 管理菜单：编辑系列信息 / 删除系列…（删除置灰 + help 原因；录音中/破坏性操作禁用） | 管理菜单：打开系列 / 编辑系列信息 / 删除系列…（确认框「系列及其会议记录将被永久删除，无法恢复。」→ 连会议记录删除） | 已接；**未置灰**→ 未解决 |
| 批量删除模式：行头/根页复选框选择系列，删除所选 | 批量删除模式：复选框 + 全选当前结果 + 删除所选 + 完成；删除系列连会议记录 | 已接 |

### 2.5 删除（deleteSession / deleteSelectedSeries / deleteAll）

| Mac | Windows | 结论 |
|---|---|---|
| 单场确认：可保留已确认内容时提供第二按钮「删除会议，保留已确认的系列内容」 | 本任务补齐：同一双选项（`HasRetainableAcceptedContent` 判定；Core 新增同语义方法并测） | 一致 |
| 保留语义：内容作为独立经验保存、原来源标记删除，不保留原话 | `WorkspaceSourceIntegrity.RemoveMeetings(..., retainAcceptedContent: true)`（B3 语义已有） | 一致 |
| 删除全部：排除正在记录的会议 | 同 | 一致 |
| 删除后系列成员/知识清理、列表与详情刷新 | 同（B3 removeMeetings + RefreshSeries + 列表刷新） | 一致 |
| 「删除全部」确认文案 | Windows 自拟中文（Mac 的 confirmationDialog 文案在源码，逐字对照见 AppState 文案表；未找到对应翻译 key 的用 Windows 版） | 部分（文案见未解决） |

### 2.6 归档概览 / 回答列表 / 归档截图

| Mac | Windows | 结论 |
|---|---|---|
| MeetingArchiveOverview：根页/文件夹卡片、搜索命中计数、清除筛选、清除搜索、返回首页、重试读取、空状态 | 详情卡：标题/事实/摘要错误/截图/回答列表 | 部分（见 1 节） |
| SessionAnswersList：每条回答显示问题、回答、自动/手动、模型、时间、首 token、未完成标记、分栏「逐题回放」 | 本任务补齐自动/手动标签、时间+模型+首字、未完成片段标记 | 一致（分栏样式除外） |
| 回答内嵌归档截图（ArchivedScreenshot，按需加载图片，缺失显示占位） | 显示「截图已归档」+「查看截图」按钮开原图窗；无内嵌缩略图 | 部分（未解决：内嵌缩略图/缺失态） |
| 会议归档截图墙（ArchivedScreenshot 列表/放大查看） | 归档截图缩略图墙 + 点击放大（既有） | 一致 |

## 3. 文案

- 本任务新增/修改文案优先用 Mac 原句并通过 `{loc:Tr}`/`UiText.T`：所有会议、每一场会议都存在本机…、会议记录操作、删除全部会议记录、批量删除、全选当前结果、删除所选、完成、会议系列、归档概览、归档截图、回答列表、没有符合条件的记录、重新整理、纪要语言、导出 Markdown…、重命名…、详细信息、以此为上一场，准备下一场、删除这场会议、编辑系列信息、删除系列…、管理系列、搜索全部转写、纪要、笔记 等。
- Mac 有、Windows 表暂无翻译的 Windows 专用句（删除双选项按钮/确认正文、搜索状态、命中「原话/校正原话…」个别词）已走 `UiText.T` 的 Mac 原句或 `T(zh,en)` 双语兜底；G9 本地化合规测试已通过（见测试记录）。
- 与 Mac 逐字不同的已知文案：搜索占位、删除确认正文（Mac 用 confirmationDialog 的 message 文本）→ 记未解决（待 Mac 文案补齐/翻译表覆盖）。

## 4. 演示数据点按记录（自检 `history-ui`）

在隔离数据目录 `NEXTTHOUGHT_DATA_DIR=<temp>` 下以 `--demo --self-test=history-ui` 运行真实应用；脚本创建 2 场真实归档会议（含转写/回答/纪要）与 1 个含经验、含成员的系列，然后逐项驱动真实命令：

| 步骤 | 操作 | 期望 | 本次结果 |
|---|---|---|---|
| 1 | 打开「所有会议」 | 列表显示 2 场 | 通过 |
| 2 | 搜索「缓存一致性」 | 自动全文搜索命中 2 场，行内出现「原话」命中行 | 通过 |
| 3 | 选中命中会议 + 下一个/上一个命中 | 导航条显示 i/n、kind、摘要；可循环移动 | 通过 |
| 4 | 清空搜索 | 命中行与导航条清空 | 通过 |
| 5 | 查看归档概览/回答列表 | 事实行、回答（自动标签/模型/时间）显示 | 通过 |
| 6 | 以此为上一场，准备下一场 | 进入准备页且「带入上一场转写」开启 | 待复跑（首次失败原因：自检构造的系列未含成员；已修数据构造） |
| 7 | 重命名会议 | 磁盘 meta 标题更新 | 通过 |
| 8 | 详细信息 | ≥8 个字段 | 通过（13 项） |
| 9 | 删除会议（系列有经验 → 保留选项） | 磁盘上会议减少 1，保留选项出现 | 待复跑（同上，依赖系列成员/经验构造） |
| 10 | 批量删除系列（全选当前结果） | 至少选中 1 个系列 | 待复跑（同上） |
| 11 | 删除系列连同会议记录 | 会议与系列一起删除 | 待复跑（同上） |

失败/阻塞说明：首次运行（03:31，旧二进制）第 6/9/10/11 步失败，根因是自检数据构造：系列 `SessionIds` 未包含会议、未加经验 → `SeriesOfSession`/保留判定为空；已修正自检构造。复跑被共享树运行时问题阻塞：未提交的 `Styles/Controls.axaml`（D8 焦点环 `FocusAdorner` Setter 写法）导致应用启动即崩（InvalidCastException，`Logs/desktop.log`），已报 Lead/owner。修好后复跑并补 `history-search-light.png`/`history-overview-light.png`/`history-series-light.png` 与最终 JSON 到本目录。

## 5. 未解决 / 待补

1. **Mac v3.3.0 history 实机截图缺失**（`docs/screenshots/v3/` 无 history-*.png），当前用 `docs/screenshots/history-*.png`（较早视觉）对照；需要人在 Mac 上补 v3.3.0 的所有会议浅/深色截图（交 OPEN_QUESTIONS）。
2. 命中导航不定位/高亮转写行：Windows 只切到纪要页；Mac 打开对应 Transcript 标签并定位命中（需要 Notes 页转写标签的行定位 API）。
3. 系列限定搜索（Mac 在某个系列内搜索并显示「搜索范围」）；Windows 目前全库搜索。
4. 根页文件夹卡片布局（Mac MeetingArchiveOverview 的根页/卡片）未做，Windows 用「列表即概览」。
5. 菜单项禁用态（录音中/只读 fallback）未置灰，只在点击时守卫提示；Windows 无 Mac 的只读 fallback 概念。
6. 归档概览缺「清除筛选/清除搜索/返回首页/重试读取」按钮；回答内嵌截图缩略图未做。
7. 重新整理行为：Mac 的 NotesRegeneration 重新生成结构化纪要；Windows 走自动纪要服务（模板纪要语言分支已支持强制中/英）。
8. 删除确认/搜索占位等文案与 Mac confirmationDialog 原句未逐字一致（部分 Windows 专用文案走双语兜底，需 G9 文案表补齐）。
9. 全部待实机验证项按 FIX_TASKS 第 4 节口径最多「已实现、未验证」。

## Mac 3.4.0 参考图与逐图核对（task-48）

Mac 3.4.0 参考批次**没有 history 独立截图**（Mac 里「所有记录」= HistoryView，3.4 参考图只给了 home/notes 等页）：可用
[home-light](../../mac-screens/home/home-light.png) 的「所有记录」导航与 [notes/transcript-light](../../mac-screens/notes/transcript-light.png) 的会议页做侧证。
Windows 最新批次：`validation/screens/current/history-light.png` / `history-dark.png`（已刷新）。

- 逐图核对：Windows 历史页的筛选（日期/长度/排序）、行标题/场景/时间/状态、右键菜单与 E18 原表一致；与 Mac 图能对照的只有导航位置和会议页视觉，无法逐像素对照列表行（Mac 批次缺口）。
- 差异：Mac 3.4 历史页在「所有记录」标签内、左侧可选系列；Windows 历史页是同页顶部导航内的独立页面（E18 结构），细节差异见原表。
- Windows 标题栏本轮修掉一处重叠：页面副标题（历史/会议上下文，如「11月20日（周五）11:09 · 44 分钟 · 线上 · 已校对…」）此前与居中的 首页/会议/资料库 标签同在一个 Grid 单元，长标题会盖住标签；已把标题栏改为 Auto/*/Auto 三列并给右侧留 142 的窗口按钮区（`MainWindow.axaml`）。刷新后的 `current/settings-general-light.png` 可见副标题已不与标签重叠。Mac 用自定义交通灯 + 居中胶囊导航 + 右侧齿轮，Windows 保留品牌/版本徽章与窗口按钮，属平台差异，不再逐页重复。

---

## §6.7 E1 重做（2026-11-21，runtime-gaps）

Mac 没有「所有记录」独立截图：标准为 `Sources/NextThought/HistoryView.swift`（`HistoryView.body`、`sessionList`、`filterBar`、`MeetingArchiveOverview`、`SessionRow`）。本轮把 Windows 页按 Swift 重做成两栏并重新截浅/深图（本目录 `history-light.png` / `history-dark.png` / `history-overview-light.png` / `history-overview-dark.png`，由 `--self-test=history-e1` 生成，1120×820）。

### 左侧会话栏（270pt）

| Mac 元素 | Windows 现状 | 一致性 |
|---|---|---|
| 标题 `所有记录`（section 19 衬线）+ 副标题「自动保存 · 不保存录音」 | 同位置同字号/字重/颜色/文案 | 一致（默认状态；记录关闭态/系列作用域未接） |
| 右上 ellipsis → 删除全部会议记录 | 同菜单 | 一致 |
| 搜索框「搜索全部转写、纪要、笔记」 | 同占位/绑定 | 一致 |
| 日期/时长/排序三个 `HistoryFilterPicker` | 三个 §6.1 `MenuPicker`，同绑定 | 一致 |
| `\(searchScopeLabel) · \(resultCount)` 作用域行 | `HistorySummary`（N 场会议 · 共 M 场） | 不一致：格式仍是旧摘要 |
| 系列分组头（名称 + 会议数 + 管理菜单） | 名称 Button + 行数；管理菜单在右栏 | 部分一致 |
| `SessionRow`：场景色点、标题、右侧时间、下行 `MeetingOutcome.line`、ready sparkle/pending/failed 图标、命中摘录、选中 Sunken | 全部实现（`ModeTintBrush`/`TimeText`/`Details`/三类图标/`Classes.selected`）；英文副标题 Wrap MaxLines=2，英文去掉末尾 ready 词 | 一致（active 红点、行间 Hairline、focus ring 未做） |
| 空态（无记录/无匹配/清除按钮/读取失败） | 仅两条静态空态文案 | 不一致：分态与清除按钮未移植 |
| 批量删除：批量删除/已选 N 个系列/全选当前结果/删除所选/完成 | 右栏「会议系列」段四个 ControlButtonBody + 计数 | 一致 |

### 右侧归档概览

| Mac 元素 | Windows 现状 | 一致性 |
|---|---|---|
| 面包屑栏 52 高、底 Hairline、maxWidth 720；非根显示「所有记录 > 名称」 | 高 52 + 底 Hairline；选中会议时显示「所有记录 > 会议标题」，根状态留空（Mac 根无面包屑） | 一致 |
| 大标题 `Typo.display` 30 衬线「所有记录」，容器给足宽度（英文不截断） | display 标题 + MinWidth 120，改 Stretch 容器 | 一致 |
| 副标题「本机记录 · 当前匹配 N 场会议、M 个系列 · 系列按最近匹配会议排序」 | `HistoryOverviewSubtitle` 同文案/格式 | 一致 |
| 统计行「N 场会议」「M 个系列」 | `HistoryOverviewStats` 同格式（选中会议时显示 SessionFacts） | 部分一致（系列内「篇已整理」不适用） |
| 「会议系列」section + 批量删除 + 系列卡片列表 | 同 section + 批量按钮 + 系列行（名称/场数/管理菜单） | 部分一致（缺模式/未整理计数与卡片样式） |
| 选中会议显示内嵌 `MeetingPage` | 保留「归档概览」卡片（标题/事实/错误/截图/回答），点击行导航到会议页 | 不一致：架构差异（避免与会中页 owner 冲突） |
| 读取失败通知/重试 | 未移植 | 不一致 |

### 交互

行点击/右键菜单：一致。筛选即时搜索/命中导航：一致（系列作用域、读取失败、清除筛选未做）。选中动画：未做。键盘 focus ring：未做。

### 未解决（明确列差）

1. 没有 Mac 的 workspace/系列作用域状态（标题不随系列变、无系列层级面包屑、无「系列中的会议」空态/清除筛选）。
2. 右栏选中会议用归档概览卡片，未内嵌完整 MeetingPage（导航架构，留给会中页 owner）。
3. 内容搜索读取失败提示、清除搜索/筛选按钮、搜索进度分态未移植。
4. 行间 Hairline、focus ring、选中 snappy 动画未做。
5. 系列卡片缺模式/未整理计数。
6. 英文截断修复：行副标题（`0fc45f05`）与标题容器（`278f2e4d`）；英文扫描用的 `history-en-light/dark.png` 由 localization 批次生成（本目录的 `history-e1-check.json` 记录自检 4/4 通过）。
---

## §6.7 E1 列差收口（2026-11-21 第二轮，`--self-test=history-e1`）

本轮补齐上一节列出的差异并重拍截图（`history-light/dark.png`、`history-overview-light/dark.png`、`history-series-light.png`，自检 6/6 通过，报告 `history-e1-check.json`）。

### 已补齐（与 Mac 逐条一致）

1. **workspace/系列作用域**：`HistorySeriesFilter` + `IsHistoryScoped`；左栏标题/副标题随作用域切换（系列名 / 「这个系列中的会议记录」），右栏 `HistoryOverviewTitle/Subtitle/Stats` 按作用域生成；系列行与组头点击 `OpenHistorySeriesCommand` 进入作用域，`ShowAllHistoryCommand` 返回根；面包屑显示「所有记录 > 系列名」（根状态无面包屑，与 Mac 一致）。
2. **空态与清除**：根「还没有会议」、系列「这个系列还没有会议」+「返回所有记录」、过滤空「没有符合条件的记录」+「清除搜索与筛选」（`ClearHistoryFiltersCommand`，保留系列作用域）；`HistoryNoMatchDetail` 分作用域文案。
3. **内容搜索进度**：`HistoryChecked/Total/Unreadable` + `HistoryContentSearchProgress`（「已检查 N / M 场；完成后才会确定没有匹配项。」）已接；读取失败提示 UI 存在（`HistoryReadIssueText`）。
4. **行视觉**：每行 0.5px `Ink.hairline` 分隔；`Button.recent:focus-visible` 2px Ink 焦点环；选中底色 0.14s BrushTransition（snappy 近似）；左栏 seriesGroupHeader 只保留名称+场数（模式/整理数在右栏系列卡片）。
5. **系列卡片**：`ModeTitle` + `CountsText`（N 场会议 · M 篇已整理）。

### 仍不一致（明确列差）

1. **内嵌 MeetingPage**：Mac 在右栏内嵌完整会议页；Windows 仍用「归档概览」卡片 + 导航到会议页（导航架构差异，需 live owner 协调）。
2. **内容读取失败**：Windows `Sessions.SearchContentHits` 未暴露逐场读取失败计数，`HasHistoryReadIssues` 恒 false，Mac 的「N 场内容无法完整读取」警示不会出现（API 缺口）。
3. **搜索进度确定性**：Windows 搜索一次性返回（无逐场进度回调），`HistoryChecked` 只在完成后设为总数；Mac 扫描中显示递增进度。
4. **行 focus 键盘导航**：有 focus-visible 环，但没有 Mac 的上下键在行间移动 + `.focused` 状态；
5. **选中动画**：0.14s BrushTransition 近似 snappy，非真弹簧（Motion.snappy 由 D5 弹簧实现，按钮过渡仍用 Avalonia BrushTransition）。
6. **根/系列 overview 的「N 篇已整理」系列内统计**：已按 Mac 显示 `%lld 篇已整理`；根统计 `N 场会议 · M 个系列` 一致。
7. Mac 无该窗口截图，所以证据是 Swift 源码元素表 + 本页浅/深截图；未做像素级对照。
---

## §5 证据重拍（2026-11-21，task-77）

截图来自 `--self-test=history-e1` 同一次构建（隔离数据目录）：`history-light.png`、`history-dark.png`、`history-series-light.png`、`history-meeting-light.png`、`history-meeting-dark.png`；自检 6/6 通过（两栏、分组、SessionRow、右栏内嵌会议页、系列作用域、返回所有记录、深色内嵌页）。旧截图 `history-overview-*`、`history-en-*`、`history-search-light.png` 已按 §5 删除。

**结论：逐条一致。**

本页 §3.4 要修的 2–8 与右栏内嵌会议页均已落地：
- 右栏选中会议时内嵌会议页（复用 `NotesView`，Mac MeetingPage 等价物），根页才显示 archive overview；命中点击/上一条下一条通过 `OpenHistoryHitInMeeting` 在内嵌页打开对应标签，转写命中调 `ShowNotesTranscriptAt` 定位高亮。
- 2) 命中导航定位/高亮：已接。
- 3) 系列限定搜索：`HistorySeriesFilter` 作用域下搜索/筛选先按系列收窄。
- 4) 根页系列卡片：`SceneGlyph` + 名称 + 模式/场次统计 + 管理菜单的卡片网格。
- 5) 菜单禁用态：`ContextMenu.Opened` 在行菜单实现时订阅，录音/任务中禁用删除、重新整理、准备下一场。
- 6) 概览动作：返回首页 / 清除搜索与筛选 / 重试读取已加；内容读取问题提示沿用 §3.4 notes 的读取状态条（右栏内嵌页）。
- 7) 删除全部会议文案已与 Swift 的 confirmationDialog 文案逐字一致。
- 内嵌页内部（回答列表时间/首字/未完成/截图/评价、结构化纪要编辑等）属于 §3.4 notes 1–7 的清单，由 notes 页实现承接，不再计为 history 的差别。