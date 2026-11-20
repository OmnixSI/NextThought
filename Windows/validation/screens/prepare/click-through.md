# E2 · 会前准备页点按记录（demo 数据）

环境：`--demo --screenshots=<dir>`（内置样本数据，不写用户资料）。截图：`prepare-{light,dark}.png`、`prepare-en-{light,dark}.png`（本目录）。

| 位置 | 操作 | 预期 | 观测/代码路径 |
|---|---|---|---|
| 场景 chip | 点击 | `SetSceneCommand` 切换 `SelectedMode`/`SelectedScene`，SceneGlyph/Promise/场次默认值跟着变 | `PrepareScene_Click` → `SetSceneCommand`；`--self-test prepare` 会遍历全部场景断言 |
| 标题输入 | 输入 | 绑定 `MeetingTitle`，空标题时开始时按场景默认标题生成 | `MeetingTitle` 双向绑定；`StartRecordingAsync` 的 `NewMeta` 处理空标题 |
| 系列列表 | 选择系列 | `SelectedSeries` 变化 → `SelectedSeriesBriefing`、`SelectedSeriesTitle/Meta`、`PrepareCarrySummary` 更新 | `SeriesRows`/`SelectedSeries` |
| 带入资料/题库/经验/上一场转写 | 勾选 | 写入 `CarryPlan`（`MeetingCarry`），汇总行 `PrepareCarrySummary` 立即更新 | `OnCarry*Changed` → `RefreshPrepareSummary` |
| 导入资料 | 点击 | 文件选择 → `Library.ImportAsync` → 资料列表/计数/汇总刷新 | `ImportPreparedDocumentsCommand` |
| 打开资料库/打开系列 | 点击 | 导航到资料库/系列页 | `NavigateCommand` |
| 检查卡：采音/场地/回答服务/手机/资料与模型 | 展示 | 状态摘要随设置变化 | `AudioSourceText`、`场地 本机·不依赖网络`、`Settings.KeyStatus`、`Settings.CompanionRelayUrl`、`Library.MaterialCountText`、`ModelStatusText` |
| 开始栏状态点 | 展示 | 声音已就绪/未就绪、AI 可选、手机已连接 | `PrepareAudioReady` / `PrepareAnswerReady` / `PreparePhoneReady` |
| 开始栏说明 | 展示 | 就绪说明 + 「本场使用 N 份已就绪资料；文字可用即可开始。」 | `Status` + `PrepareMaterialsLine`（`PreparedMaterialCount`） |
| 取消 | 点击 | 清空标题、回到首页 | `CancelDraftCommand` |
| 导入 WAV | 点击 | 选择 WAV 验证识别 | `ImportWaveCommand` |
| 开始记录 | 点击 | 走 `StartRecordingAsync`：`BeginPreparedMeeting(planSeries)` 建 MeetingPlan/MeetingCarry，写系列到 meta，资料+上一场转写进上下文，然后开始录音 | `AppState.Live.cs`；`--self-test prepare` 断言 `ActivePlan` 与 carry 一致、`StartRecordingCommand` 可执行性 |

自测：`--demo --self-test prepare`（`RunPrepareCheckAsync`）按上述命令逐项驱动并写入 `%TEMP%/nextthought-selftest/prepare`。
