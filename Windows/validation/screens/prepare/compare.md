# E2 · 会前准备页对照（PrepareView）

- 日期：2026-11-21
- Mac 参照：`Sources/NextThought/PrepareView.swift`（PrepareView / PrepareComposer / TypePicker / SeriesCarryCard / SeriesBriefingPanel / CarryRow / MaterialsActions / MaterialsBlock / FileRow / MaterialsInsight / CheckCard / AudioCheckRow / VenuePicker / AudioSourceMenu / AnswerCheckRow / PhoneCheckRow / PreparationStartBar）与 `AppState+Prepare.swift`（beginNewMeeting / setDraftCarry / importDraftDocuments / startPreparedMeeting）
- Windows 实现：`Windows/src/NextThought.Desktop/Views/PrepareView.axaml(.cs)`、`AppState.Prepare.cs`、`AppState.Live.cs` 的 `StartRecordingAsync`
- 截图：`Windows/validation/screens/PrepareView/prepare-light.png`、`prepare-dark.png`、`prepare-en-light.png`、`prepare-en-dark.png`（由 `--demo --screenshots` 生成后拷贝；`-en-` 为英文模式）

## 逐元素对照

| Mac 元素 | Mac 行为 | Windows 状态 | 差异 |
|---|---|---|---|
| `PrepareView` 页面 | Ink 水墨背景（animated: false）、标题「准备」+ 一句说明 | 有：`InkWash Animated="False"`、`准备`、`先选场合、补资料、做检查，再开始这场会。` | 一致；Windows 卡片间距沿用 D4 资源 |
| `PrepareComposer` / `TypePicker` | SceneGlyph 44 + 大标题输入；场景 chip 横排，tooltip 为 `defaultsSummary` | 有：SceneGlyph 44、`MeetingTitle` 无边框大输入、`Scenes` chip（`PrepareScene_Click`）；tooltip 用 Promise 文案 | Windows 场景 chip 未带 `defaultsSummary` tooltip（缺 Mac 的默认值摘要） |
| 类型猜测提示 | 标题输入可猜场景并提示 | 无（Windows 未做 `showsGuessNote`） | 待补（不阻塞开始流程） |
| `SeriesCarryCard` / `CarryRow` | 系列选择 + 「带入资料/题库/经验/上一场转写」四行，每行带条数与来源说明 | 有：`SeriesRows` 列表、四个 CheckBox、`PrepareCarrySummary`（本次新增：资料/题库/经验/上一场转写条数汇总） | 行内来源/条数说明简化为一行汇总；`setDraftCarry` 的状态保存由 `Carry*` 属性直接驱动 `CarryPlan` |
| `SeriesBriefingPanel` | 最近会议 / 已确认经验 / 题库，可展开来源 | 有：`SelectedSeriesBriefing` 的 `LatestSessions` / `Lessons` / `QuestionBank` 三块 | Windows 无「来源」按钮与展开详情；列表样式简化 |
| `MaterialsActions` / `MaterialsBlock` / `FileRow` | 拖放 + 导入按钮；每条材料显示解析状态/页数/失败原因与重试 | 有：材料名称列表、`Library.MaterialCountText`、`Library.MaterialsInsight`；**本次新增**「导入资料」按钮（`ImportPreparedDocumentsCommand` → 文件选择 → `Library.ImportAsync`） | 缺拖放、逐条状态/重试；资料洞察显示为一句摘要 |
| `MaterialsInsight` | 已就绪/处理中/需处理数量与展开列表 | 部分：`MaterialsInsight` 文本 + 检查卡里的资料与模型行 | 未逐条列缺页/需处理项 |
| `CheckCard` / `AudioCheckRow` / `AudioSourceMenu` / `VenuePicker` / `AnswerCheckRow` / `PhoneCheckRow` | 四块检查卡：采音（电平/设备菜单/场地）、回答服务（连接/测试）、手机、资料与模型；每块带状态点与操作 | 有：检查卡 2×2 + 资料与模型行（`AudioSourceText`、`场地 本机·不依赖网络`、`Settings.KeyStatus`、手机中转地址、`ModelStatusText`），`Status` 行与「打开设置」 | Windows 无采音电平条、无「测试回答服务」按钮、无 Mac 的 VenuePicker（线上/线下）独立控件；状态点用文字表示 |
| `PreparationStartBar` | 状态点（声音 / AI 可选 / 手机）+ 就绪说明 + 本场资料句 + 取消 / 开始 | **本次重写**：状态点（`PrepareAudioReady`/`PrepareAnswerReady`/`PreparePhoneReady`）+ `Status`/`PrepareMaterialsLine`（「本场使用 N 份已就绪资料；文字可用即可开始。」）+「取消」（`CancelDraftCommand`）+ 导入 WAV + 开始记录 | 缺「只记录对方」按钮（Mac 在麦克风拒绝时显示）；开始按钮直接 `StartRecordingCommand`，与 Mac 的 startPreparedMeeting 对齐见下 |
| 开始流程 | `startPreparedMeeting`：draft → MeetingPlan → 归档 meta 带系列 → 开始录音 | `StartRecordingAsync` 已调用 `BeginPreparedMeeting(planSeries)`（`AppState.Live.cs`），`WorkSpace.Assign` 写 meta 的 `workspaceID/workspaceName`，`PreparedDocuments()` 把系列资料 + 上一场转写放入上下文 | 流程行为已接；draft 的独立编辑态（Mac MeetingDraft）Windows 未建模，直接以页面当前值开始 |
| 取消 | `cancelDraft()` 回到首页 | **本次新增** `CancelDraftCommand`：清标题、回首页 | 一致 |

## 点按记录（demo 数据）

环境：`--demo`，见 `Windows/validation/screens/PrepareView/click-through.md`（待构建可用后补齐真实点按结果）。

## 结论

准备页的核心流程（选类型 → 选系列/带入项 → 看简报 → 补资料 → 看检查 → 开始）已与 Mac 对齐，开始会走 `BeginPreparedMeeting` + 系列归档，资料/系列/简报/检查各块均有实现。逐元素差异集中在：场景 chip tooltip、类型猜测提示、资料拖放与逐条状态、采音电平与回答服务测试按钮、`只记录对方` 入口。以上未完成项等 Mac 截图/实机权限后继续对照，故状态最高为「已实现、未验证」。
