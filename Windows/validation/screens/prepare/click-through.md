# E2 · 会前准备页点按记录（§6.7 重做版）

环境：`--demo --screenshots`（隔离数据目录）。截图：`prepare-{light,dark,en-light,en-dark}.png`。
Mac 参照：`PrepareView.swift` 3.4.0；逐元素对照见同目录 `compare.md`。

| 位置 | 操作 | 预期 | 观测/代码路径 |
|---|---|---|---|
| 标题输入 | 输入标题 | 未手动选类型时按标题猜场景（chips/承诺/说明联动）；「根据标题选好了，可以改」在猜中时显示 | `DraftTitle` → `DraftGuessNoteVisible`；`MeetingTypeGuesser` |
| 这是什么会 | 点场景 chip（如「组会」） | 场景色选中底/字移动；图标、承诺、三条说明、类型说明即时换；猜类型提示消失 | `DraftSceneChips` / `SetDraftType` / `RaisePrepareDraft` |
| 「+ 自定义…」 | 点击 | 出现「类型名称」输入 +「基于」场景选择；确认后承诺/说明按基础场景显示 | `DraftIsCustom` / `CustomBaseBox`（`applyingDraft` 防重入） |
| 线上/线下 | 切换 | 线上=系统声音+麦克风；线下=只用麦克风并标「现场」；设置保存 | `OnlineVenue_Click` / `OfflineVenue_Click` → `SetVenueAsync` |
| 这场用什么 · 自动回答 | 开/关 | 开关即时生效并写 `meeting-features.json` 的 `meetingFeatures.<seriesID>`；与场景默认不同时右上出现「恢复这个场景的默认」 | `MeetingFeatureAnswers` → `DraftMeetingFeatures`（Core `MeetingFeatureChoices`） |
| 这场用什么 · 实时纪要/同声传译/区分说话人 | 开/关 | 同上；无回答引擎时自动回答/实时纪要行禁用并换成「需要先连接回答服务…」；声纹模型未安装时区分说话人禁用 | `MeetingFeature*Enabled` / `*Detail` |
| 恢复这个场景的默认 | 点击 | 四个开关回场景默认（自动回答=场景是否主动回答、实时纪要=反向、同声传译=开、区分说话人=可用且开），按钮消失 | `ResetMeetingFeaturesCommand`（删 sidecar 键） |
| 接着上一场 | 查看 | 系列色点 +「这是「系列」的第 N 场」+「上一场：…」；系列简报（最近会议/经验/答法/待确认）；携带行 WideToggleRow | `DraftSeriesHeaderTitle` / `DraftBriefing*` / `Carry*` |
| 「不是同一个系列」 | 点击 | 解绑系列、清空携带选择，区块收起 | `DeclineDraftSeriesCommand` |
| 从以前的会议带入… | 打开菜单选系列 | 绑定该系列并带入资料/题库/经验基线 | `OtherSeriesRows` / `ContinueDraftSeries` |
| 这次新加的资料 | 拖入文件 / 点「添加」 | 文件行出现（名称/段数/警告），资料洞察块可用；文件解析状态由现有 E2 流程更新 | `TryGetFilesAsync` / `ImportPreparedDocumentsCommand` / `PrepareMaterialsRows` |
| 讨论准备 · AI 可选 | 「整理讨论重点」 | 未配置→「设置 AI 服务（可选）」；已配置→running/流式结果/失败重试/取消 | `PrepareInsightActionCommand` / `CancelMaterialsInsightCommand` |
| 开始前检查 · 声音 | 查看/「声音测试」/「只记录对方」/「重新打开麦克风」 | 状态点、音源、两条电平、问题行；声音测试打开录入窗口 | `PrepareAudioTone` / `VoiceTest_Click` / `ContinueWithoutMicrophoneCommand` |
| 开始前检查 · AI 可选 | 「检查回答服务」 | 未配置给明确提示；已配置 running→通过/失败原因，记录仍可继续 | `TestAnswerEngineCommand` / `PrepareEngineVerification` |
| 底部开始条 | 「开始」 | `BeginPreparedMeeting` + `ApplyActiveMeetingFeatures`：四项开关落地，关掉的功能开始后不运行 | `StartPreparedMeetingCommand`；`--demo --self-test prepare` 断言 |
| 底部开始条 | 「取消」 | 丢弃 draft、回首页 | `CancelDraftCommand` |

自测：`--demo --self-test prepare`（通过：场景/自定义/线上线下/麦克风/携带/资料/四个功能开关的每系列沿用与开始时生效/清除默认）。
