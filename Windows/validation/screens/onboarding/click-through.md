# E3 · 首次引导点按记录（demo 数据）

环境：`--demo --screenshots=<dir>`（内置样本数据，不写用户资料）。

| 位置 | 操作 | 预期 | 观测 |
|---|---|---|---|
| 顶部「稍后继续」 | 点击 | 关闭引导（SkipOnboardingCommand） | 引导覆盖层消失，回到首页 |
| 场合页场景 chip | 点击任一场景 | `SetSceneCommand` 切换 `SelectedMode`/`SelectedScene`，场景名/Promise/Moments 跟着变 | 见 `onboarding-step0-*.png` 两张场合页 |
| 场合页「看一段面试示例」 | 点击 | `OnboardingShowSample` 展开 | 出现示例说明与「看一场示例会议」按钮 |
| 「看一场示例会议」 | 点击 | 完成引导并 `LoadDemo()` | 回到首页、demo 数据可见 |
| 声音页「声音测试」 | 点击 | 打开 `OnboardingVoiceWindow` | 声音录入窗口弹出（OnboardingView.axaml.cs VoiceTest_Click） |
| 声音页「下载/准备」 | 点击 | `PrepareModelsCommand` | 模型准备状态行刷新（模型未安装时显示未准备） |
| 回答页「打开设置」 | 点击 | `NavigateCommand Settings` | 设置覆盖层打开（引导仍在） |
| 回答页「检查回答服务」 | 点击 | `CheckServiceCommand` | 状态行显示连接结果/未配置 |
| 提词与开始页「回到声音」 | 点击 | `GoToOnboardingStepCommand Sound` | 回到声音页 |
| 底部「上一步」 | 点击 | `PreviousOnboardingStepCommand` | 步骤号减一 |
| 底部「下一步」 | 点击 | `NextOnboardingStepCommand` | 步骤号加一，最后一步变「开始第一场」 |
| 回答步「跳过」 | 点击 | 前进到下一步 | 进入提词与开始页 |
| 底部「先看看」 | 点击 | `CompleteOnboardingCommand` | 关闭引导回到首页 |
| 底部「开始第一场」 | 点击 | `BeginFirstMeetingCommand`（引导关闭后 StartRecordingCommand） | 模型就绪时开始录音；未就绪时按钮置灰 |
| 设置清单 | 展示 | 声音/回答/资料库三行，按 `OnboardingChecks` 状态点变化 | 见 `onboarding-step3-*.png` |

说明：截图批按 D1 命名只保留 first/last（本批 `--screenshots` 生成于 E3 视图替换后）；逐步浅/深截图在 `onboarding-step0..3-*.png`（上一轮稳定性问题改由 `RunOnboardingCheckAsync` 点按自测覆盖，见下）。

自测：`--demo --self-test onboarding`（`RunOnboardingCheckAsync`）按同样命令走完四步、示例开关、跳过，并写入 `%TEMP%/nextthought-selftest/onboarding`。
