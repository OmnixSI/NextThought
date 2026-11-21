# E3 · 首次引导对照（Onboarding，第四轮 task-34 收尾）

- 日期：2026-11-21
- Mac 参照：`Sources/NextThought/Onboarding.swift`（OnboardingView / SetupChecklist / SetupRow / GuideRow / CueIslandMock）、`OnboardingSampleMeeting.swift`、`MyVoiceViews.swift` 的 `OnboardingVoicePage`；`SettingsGeneralData.swift` 的 `SetupChecklist(exclude:)`；Core `OnboardingFlow.swift` / `OnboardingVersion.swift`
- Windows 实现：`Views/OnboardingView.axaml(.cs)`、`AppState.Onboarding.cs`、`OnboardingSample.cs`、`AppState.OnboardingSample.cs`、`AppState.SetupChecklist.cs`、`Views/SettingsSetupChecklist.axaml(.cs)`、`Views/Motion.cs`（GuideMotion）、`MainWindow.axaml` 的引导覆盖层入口、`SettingsView.axaml`（通用页清单卡）
- 截图：`Windows/validation/screens/onboarding/`（`onboarding-step0..3-{light,dark,en-light}.png` 与 first/last）；设置清单在 `settings-*.png` 的通用页

## task-34 本轮收尾项

| Mac 元素 | 实现 | 说明 |
|---|---|---|
| `SetupChecklist` 位置 | 设置 → **通用** 页的「设置清单」卡（`Tag="General"`），行来自 `AppState.SetupChecklistRows`（语音识别 / 回答引擎 / 当前场景），对应 Mac `setupItems` 排除 `capture/mic/signing` 后的三项 | 第四轮从「高级」页移到 Mac 所在位置；行内状态点、标题、灰色说明、按需动作（下载 / 设置 / 编辑）与 `SetupRow` 一致 |
| `OnboardingSampleMeeting` 面板 | 场景页「看一段面试示例」展开：左侧「实时转写」卡（`示例` 标签、说话人 + 逐行文本，滚动区 280）；右侧「面试官在追问 · 参考回答」块（13 秒后出现；问题 + 参考回答）与「面试记录」卡（`示例` 标签、空态提示、记录/待办类型列宽对齐）；底部进度条 + 播完「重播」 | 时间线来自 `OnboardingSample`（逐字 Mac 数据：7 行对话 / 6 条记录 / 1 条参考回答，34 秒），`ScriptTimeline` 计算可见条数；引导关闭时停止回放 |
| 切步动效 | `GuideMotion.PhaseIn`（460 ms cubic-out 淡入，Mac `Motion.phase` 的 0.46 s spring 近似）在每次 `OnboardingStep` 变化与首次加载时播放；`OnboardingView` 监听 state 的 `OnboardingStep` 触发 | Mac 用 withAnimation(Motion.phase) 包裹 step 切换；Windows 无 spring API，用同长度缓动近似，步进内容不重叠 |
| 其它引导缺口 | 示例会议不再是 demo shell 替代（`OnboardingSample.cs` 与 `AppState.OnboardingSample.cs` 提供脚本化回放）；`看一场示例会议` 仍保留 demo 入口作补充 | 与 Mac 差异：无自动跟随滚动（用可滚动容器 + 最新行加载）；无逐字动画过渡 |

## 逐元素对照（页面）

| Mac 元素 | Windows 状态 | 差异 |
|---|---|---|
| 顶部进度胶囊 / `1 / 4` / 步骤名 / 预计时间 / 「稍后继续」 | 有；`稍后继续` = 延后（首页出现 `OnboardingResumeRow`，见 E1 文件） | — |
| 场合页 sceneSheet + promise + moments + 示例展开 | 有；示例面板见上表 | 例子面板高度按内容，Mac 固定 280 的滚动区 |
| 声音页 系统音频/麦克风/语音识别/我的声音 + 声音测试 | 有；声音测试打开 `OnboardingVoiceWindow` | Windows 无系统录屏权限步（按任务去除） |
| 回答页 Key 状态 / 打开设置 / 检查回答服务 | 有 | 表单在设置页（E27） |
| 提词与开始页 CueIslandMock / 就绪说明 / 设置清单 / 回到声音 | 有 | 提词岛为静态示意 |
| 底部 上一步/下一步/跳过(回答步)/先看看/开始第一场 | 有 | 开始第一场走 `StartRecordingCommand` |

## 点按记录

见 `click-through.md`（更新示例面板与清单行）。

## 验证

- `--demo --self-test onboarding`：四步切换、示例开关、跳过关闭、清单行。
- `--demo --screenshots`：引导 4 步浅/深/英文 + settings 通用页（含清单卡）。
- 本轮已核对：清单卡在通用页、示例面板出现于第 1 步、进度条播放、播完出现重播、关闭引导停止计时。

## 未完成项

- 示例面板无自动滚动跟读与逐字过渡动画；Mac 的 `AIHeading` 图标细节（sparkle）未复刻；清单「本机签名」行按 Windows 实际去掉（无对应能力）。
