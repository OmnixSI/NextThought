# E2 · 会前准备页点按记录（demo 数据，第三轮）

环境：`--demo`（内置样本数据，不写用户资料）。截图：`prepare-{light,dark}.png`、`prepare-en-{light,dark}.png`。

| 位置 | 操作 | 预期 | 观测/代码路径 |
|---|---|---|---|
| 标题输入 | 输入 | `DraftTitle` 更新 `MeetingTitle`，未选类型时按标题猜场景；猜中时显示「根据标题选好了，可以改」 | `DraftTitle` setter + `MeetingTypeGuesser` |
| 线上/线下 | 点击 | 线上 = 系统声音 + 麦克风，线下 = 仅麦克风；保存到设置；麦克风状态行变化 | `DraftOnline/DraftOffline` → `SetVenueAsync` |
| 场景 chip | 点击 | `SetDraftType`：选中场景、标记 `TypeChosen`，SceneGlyph/Promise/摘要/Moments 更新；tooltip 显示 `defaultsSummary` | `PrepareScene_Click` → `SetDraftType` |
| 「自定义…」 | 点击 | 出现类型名称 + 「基于」下拉；类型卡片摘要使用自定义名称 | `SetDraftCustomTypeCommand`，`SceneSummary.Line` |
| 「基于」下拉 | 选择 | 自定义类型的基础场景更新；不得递归（本轮修复点） | `CustomBase_SelectionChanged`（`applyingDraft` 守卫）+ 缓存 `DraftBaseScenes` |
| 系列列表 / 四个带入开关 | 选择/勾选 | `SelectedSeries` 进入 draft；开关同步 `draft.Carry`，汇总行更新 | `OnSelectedSeriesChanged` → `RefreshPrepareDraft`；`RefreshPrepareSummary` |
| 拖放资料 | 拖文件到资料框 | `ImportDraftDocumentsAsync` → `Library.ImportAsync`，资料行/计数刷新 | `PrepareView.axaml.cs` DragOver/Drop（Avalonia 12 `IAsyncDataTransfer.TryGetFilesAsync`） |
| 导入资料 | 点击 | 文件选择 → 导入 → 资料行刷新 | `ImportPreparedDocumentsCommand` |
| 只记录对方（不用麦克风） | 点击 | 关闭麦克风并保存，状态行「未开启」，出现「重新打开麦克风」 | `ContinueWithoutMicrophoneCommand` |
| 重新打开麦克风 | 点击 | 打开麦克风并保存 | `UseMicrophoneAgainCommand` |
| 检查回答服务 | 点击 | `TestAnswerEngineCommand`，连接检查结果行更新 | `PrepareEngineVerification` |
| 取消 | 点击 | 丢弃 draft、清标题、回首页 | `CancelDraftCommand`（清 `meetingDraft`） |
| 开始记录 | 点击 | draft → `MeetingPlan`（标题/类型/carry/系列 baseline/上一场）→ 开始录音 | `StartPreparedMeetingCommand` → `StartRecordingCommand` → `BeginPreparedMeeting` |

自测：`--demo --self-test prepare`（写 `%TEMP%/nextthought-selftest/prepare`，断言上述命令、draft 状态与 plan 内容）；`--demo --self-test startup` 冒烟。修复前该页面首屏即栈溢出，修复后两项自测实跑通过。
