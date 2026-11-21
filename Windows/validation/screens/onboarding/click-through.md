# E3 · 首次引导点按记录（§6.7 重做版）

环境：`--demo --onboarding`（截图批里用 `OnboardingVisible`）。截图：`onboarding-step{0..3}-{light,dark}.png`。
Mac 参照：`Onboarding.swift` 四步；逐元素对照见同目录 `compare.md`。

| 位置 | 操作 | 预期 | 观测/代码路径 |
|---|---|---|---|
| header | 查看 | 4 条 22×2 进度胶囊（已完成 secondary/当前 ink/未到 hairline）、「1 / 4」、步骤名、预计时间、「稍后继续」 | `OnboardingProgressCapsules` / `OnboardingPosition` / `OnboardingStepTime` |
| 「稍后继续」 | 点击 | 关闭引导；首页出现续做入口；回放停止 | `SkipOnboardingCommand` → `DeferOnboarding`；`OnboardingResumeRow` |
| 第 1 步 场景 chip | 点「组会」等 | 卡片图标/名称/承诺/三条说明即时切换；场景色选中底/字移动 | `SceneChip_Click` → `SetSceneCommand` + `RefreshGuideScene` |
| 第 1 步 「› 看一段面试示例」 | 点击 | chevron 翻转并展开示例：左侧逐行转写、右侧参考回答（13 秒后）与面试记录、底部进度条；播完出现「重播」 | `OnboardingSampleVisibleLines/Takeaways` / `ReplayOnboardingSampleCommand` |
| 第 2 步 线上/线下 | 切换 | 线下=只用麦克风（同时把麦克风打开）；线上=系统声音+麦克风 | `OnboardingOnlineVenue_Click` / `OnboardingOfflineVenue_Click` |
| 第 2 步 麦克风 | 切「记录我的声音 / 只记录对方」 | 只记录对方关闭麦克风采集，说明行/状态点联动；未授权时「允许/打开设置」打开 Windows 麦克风设置 | `OnboardingMicrophoneRecordsMe` / `OnboardingMicrophonePermissionSettingsCommand` |
| 第 2 步 语音识别 | 「下载」 | 未安装本机模型时下载并显示进度/状态；就绪后该行绿点「已就绪」 | `PrepareModelsCommand` / `OnboardingRecognitionDetail` |
| 第 2 步 「› 换一种识别方式」 | 点击 | 展开一段识别方式说明（无卡片，Mac DisclosureGroup 样式） | `ToggleRecognitionOptionsCommand` |
| 第 3 步 服务商 | 点任一服务商 | 打开设置 → 回答，在完整 `AnswerEngineForm` 里选择服务商并填 Key | `Provider_Click` → `NavigateCommand Settings` |
| 第 3 步 连接状态/模型 | 查看 | 演示数据与 Mac 截图一致：Codex 已登录 (ChatGPT 账号) / Logged in using ChatGPT · codex-cli 0.160.0 / 模型 GPT-5.5；真实连接时显示 API·模型 | `OnboardingAnswerServiceTitle/Detail` / `OnboardingModelLine` |
| 第 3 步 「› 更多选项」 | 点击 | 展开说明 + 「打开设置」 | `ToggleAnswerOptionsCommand` |
| 第 3 步 「测试回答」 | 点击 | 走与会中回答相同的连通性请求；未配置时明确提示 | `TestAnswerEngineCommand` |
| 第 4 步 | 查看 | 提词岛示意（黑块/问题/回答/两条 cue）、两段说明（Windows 用「提词岛」不用「刘海」） | `OnboardingCueIsland*` |
| 第 4 步 未就绪 | 查看 | 显示 readiness 说明 +「回到声音」跳第 2 步 | `OnboardingReadinessMessage` / `GoToOnboardingStepCommand` |
| footer | 上一步/下一步/跳过/先看看/开始第一场 | 页面 460ms 淡入切换；「跳过」仅回答步且未配置时出现；「开始第一场」完成引导并以所选场景开始 | `GuideMotion.PhaseIn` / `BeginFirstMeetingCommand`；`--demo --self-test onboarding` |

自测：`--demo --self-test onboarding`（通过：四步步进/前一步/回声/示例开关/线下切换与恢复/跳过关闭）。
