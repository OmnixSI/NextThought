# E2 · 会前准备页点按记录（第四轮 task-33 增补）

环境：`--demo`。截图：`prepare-{light,dark,en-light,en-dark}.png`。

| 位置 | 操作 | 预期 | 观测/代码路径 |
|---|---|---|---|
| 「讨论准备 · AI 可选」 | 点击「整理讨论重点」 | 未配置服务时显示「还没有配置回答服务；整理讨论重点需要它，记录和转写不需要。」，按钮为「设置 AI 服务（可选）」→ 设置页 | `PrepareInsightActionHint` / `PrepareInsightAction` |
| 同上（已配置 AI） | 点击「整理讨论重点」 | 进入 running（「正在读资料…」），流式回填重点；完成后显示结果；失败显示原因并可重试 | `ReadMaterialsInsightAsync`（`PreparationPrompt` + `CurrentClient().AnswerAsync`） |
| 同上（无资料） | 点击 | 显示「还没有可用的资料：先导入资料，再整理讨论重点。」 | `DraftMaterialText` 为空 → failed |
| 「取消生成」 | running 时点击 | 取消请求，状态回 idle，提示「已取消生成。」 | `CancelMaterialsInsightCommand` |
| 采音行电平条 | 查看 | 「对方 · 系统声音」「我 · 麦克风」两条；未录音/未测试时为 0 | `SystemAudioLevelPercent`/`MicrophoneAudioLevelPercent` ← `UpdateAudioLevels` |
| 「声音测试」 | 点击 | 打开声音录入窗口（现有 OnboardingVoiceWindow），录完电平刷新 | `PrepareView.axaml.cs` `VoiceTest_Click` |
| 「去准备」 | 未就绪时点击 | 打开设置准备语音组件 | Navigate Settings |
| 线上/线下 | 切换 | 提示行变化（线上听电脑声音/线下仅麦克风）；设置保存；麦克风行状态联动；开始录音的 InPerson 跟着变 | `DraftVenueHint` + `SetVenueAsync` |
| 「检查回答服务」 | 未配置时点击 | 明确提示「还没有配置回答服务：到 设置 → 回答 填写 API Key 后再检查。」，记录仍可继续 | `TestAnswerEngineAsync` 未配置分支 + `PrepareEngineStateLabel` |
| 同上（已配置） | 点击 | 「正在检查…」按钮禁用 → 「连接检查通过 · 用时 X 秒」或失败原因；再点变「重新检查」 | `EngineTestState` idle/running/passed/failed |
| 取消 / 开始记录 | 点击 | 取消清 draft 回首页；开始走 `StartPreparedMeetingCommand` → `BeginPreparedMeeting` | 自测 `--demo --self-test prepare` 断言 |

自测：`--demo --self-test prepare`、`startup`；截图批（浅/深/英文）由 verify 生成。
