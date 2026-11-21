# E2 · 会前准备页对照（PrepareView，第四轮 task-33 收尾）

- 日期：2026-11-21
- Mac 参照：`Sources/NextThought/PrepareView.swift`（PrepareComposer / TypePicker / SeriesCarryCard / SeriesBriefingPanel / MaterialsBlock / FileRow / MaterialsInsight / CheckCard / AudioCheckRow / VenuePicker / AnswerCheckRow / PhoneCheckRow / PreparationStartBar）、`AppState+Meeting.swift`（`MeetingDraft` / `MeetingVenue` / `defaultsSummary`）、`AppState+Prepare.swift`（draft 生命周期、`readDraftMaterials`、`startPreparedMeeting`）、Core `PreparationPrompt`
- Windows 实现：`Windows/src/NextThought.Desktop/MeetingDraft.cs`、`AppState.PrepareDraft.cs`、`AppState.Prepare.cs`、`AppState.PrepareInsight.cs`（本轮新增）、`Views/PrepareView.axaml(.cs)`、`AppState.Live.cs`
- 截图：`Windows/validation/screens/prepare/prepare-{light,dark,en-light,en-dark}.png`

## task-33 本轮收尾项

| Mac 元素 | 实现 | 说明 |
|---|---|---|
| `MaterialsInsight`（讨论准备 · AI 可选） | `AppState.PrepareInsight.cs`：`准备InsightAction`（整理讨论重点 / 设置 AI 服务（可选））→ `ReadMaterialsInsightAsync` 用 Core `PreparationPrompt.System(mode)` + `Output(mode)` 与系列资料文本（`WorkspaceSeries.MaterialText()`，回退资料库 blocks，截断 24k）走**真实回答服务往返**（`CurrentClient().AnswerAsync`，流式回填），状态 idle/running/ready/failed；未配置服务、无资料、取消、异常分别有明确文案；「取消生成」可用 | Mac `PreparationPreview.shared.read` 的等价；差异：无跨页面缓存复用（每次请求） |
| CheckCard 采音电平 | `UpdateAudioLevels` 接入引擎 level 回调（`systemLevel/microphoneLevel`），准备页显示「对方 · 系统声音」「我 · 麦克风」两条电平；停止时清零；新增「声音测试」（打开 `OnboardingVoiceWindow`）与未就绪时「去准备」 | Mac 准备页无电平条（电平在 ContentView 的 LevelMeters）；Windows 按任务加在采音行，录音/声音测试时才有读数 |
| `VenuePicker` 完整交互 | 线上/线下单选旁显示 Mac 语义提示（线上听电脑播放的声音 / 线下只用麦克风、转写标「现场」）；切换即写 `Settings.CaptureSystem/CaptureMicrophone` 并保存；线下强制开麦克风；开始录音据此生成 `InPerson` | 与 Mac `setMeetingVenue` + `isOfflineMeeting/usesMicrophone` 语义一致；不引入 Mac 的 `meetingVenue` UserDefaults 键（Windows 用采集设置推导） |
| 「测试回答服务」状态机 | `PrepareEngineStateLabel` / `PrepareEngineActionLabel` / `PrepareEngineActionEnabled` 跟随 `EngineTestState`（idle → 正在检查… → 通过·用时 / 失败原因），按钮 idle「检查回答服务」、running 禁用、结束「重新检查」；**未配置服务时直接给出「到 设置 → 回答 填写 API Key 后再检查」**，不再走泛化连接错误 | 真实模型往返由 `TestAnswerEngineAsync` 发送最短请求；结果文本见 `PrepareEngineStateLabel` |
| 其它 Mac 动作 | 「从以前的会议带入…」未做独立菜单（系列列表已可选）；资料添加支持拖放与「导入资料」按钮（多选由选择器提供单文件）；`开始记录` 走 `StartPreparedMeetingCommand`（draft → MeetingPlan → BeginPreparedMeeting） | 未做逐文件取消/重试（Windows 材料无逐文件作业队列）；`AudioSourceMenu` 的窗口/显示选择仍缺（见准备页遗留差异表） |

## 逐元素对照（延续第三轮表）

| Mac 元素 | Windows 状态 | 差异 |
|---|---|---|
| `PrepareComposer`/`TypePicker` | 有：标题、线上/线下、这是什么会、猜测提示、自定义类型 + 基于、promise/summary/moments、chip tooltip `defaultsSummary` | — |
| `SeriesCarryCard`/`CarryRow` | 有：系列列表、四个带入开关、`PrepareCarrySummary`、draft carry 同步 | 缺 SeriesMatcher「接着上一场/不是同一个系列」建议 |
| `SeriesBriefingPanel` | 有：最近会议/经验/题库 | 样式简化 |
| `MaterialsBlock`/`FileRow` | 有：资料行（N 段/提示/无文字）、拖放、导入、空态、`MaterialsInsight` | 缺逐条解析进度/取消/重试 |
| `CheckCard` | 有：声音（电平/设备状态/模型/下载/声音测试/去准备）、AI 可选（状态机）、手机、资料与模型 | 缺 AudioSourceMenu 的窗口级声音来源 |
| `PreparationStartBar` | 有：状态点 + 就绪说明 + 本场资料句 + 取消 / 只记录对方 / 导入 WAV / 开始记录 | 缺录制同意提示（Windows 无该步骤） |

## 点按记录

见 `click-through.md`（新增洞察、电平、场地、测试回答服务条目）。

## 验证

- `--demo --self-test prepare`：draft/类型/场地/麦克风/资料行/plan；`startup` 冒烟。
- `--demo --screenshots`：准备页浅/深/英文。
- 本轮已核对：洞察块按钮在 idle/未配置两种文案；回答服务测试在未配置时给出明确步骤；场地切换与麦克风状态联动；电平条在未录音时为 0。

## 未完成项

- 资料逐条解析进度/取消/重试、AudioSourceMenu 窗口选择、SeriesMatcher 建议、录制同意提示；洞察结果无缓存复用。

## Mac 3.4.0 参考图与逐图核对（task-48）

Mac 3.4.0 参考：[prepare-light](../../mac-screens/prepare/prepare-light.png) / [prepare-dark](../../mac-screens/prepare/prepare-dark.png)。
Windows 最新批次：`validation/screens/current/prepare-light.png` / `prepare-dark.png`。

- 逐图核对：标题卡（图标、标题、线上/线下分段、`这是什么会` 提示）、场景胶囊（顺序已按 Mac 修正，见 home 页同一改动）、承诺与三条 moments、底部开始条（声音/AI 状态、取消、开始）一致。
- 差异（未改，记录）：Mac 3.4 把「这场用什么 不用的可以关掉」五开关（实时转写 始终开启、自动回答、实时纪要、同声传译、区分说话人）直接放在标题卡下方同屏；Windows 准备页同屏先出现「系列与资料」卡，这五个开关在页面下方/其它位置，未与 Mac 同屏。
- Windows 额外：底部 `记录对方`、`导入 WAV`（E2 的导入/离线路径），Mac 没有；Mac 顶部没有「准备」页头，Windows 有页头 + 说明句（平台/信息架构差异）。
- Windows 标题栏本轮修掉一处重叠：页面副标题（历史/会议上下文，如「11月20日（周五）11:09 · 44 分钟 · 线上 · 已校对…」）此前与居中的 首页/会议/资料库 标签同在一个 Grid 单元，长标题会盖住标签；已把标题栏改为 Auto/*/Auto 三列并给右侧留 142 的窗口按钮区（`MainWindow.axaml`）。刷新后的 `current/settings-general-light.png` 可见副标题已不与标签重叠。Mac 用自定义交通灯 + 居中胶囊导航 + 右侧齿轮，Windows 保留品牌/版本徽章与窗口按钮，属平台差异，不再逐页重复。
