# E3 · 首次引导对照（Onboarding）

- 日期：2026-11-21
- Mac 参照：`Sources/NextThought/Onboarding.swift`（OnboardingView / SetupChecklist / SetupRow / GuideRow / CueIslandMock）、`OnboardingSampleMeeting.swift`、`MyVoiceViews.swift` 的 `OnboardingVoicePage`；Core `OnboardingFlow.swift` / `OnboardingVersion.swift`
- Windows 实现：`Windows/src/NextThought.Desktop/Views/OnboardingView.axaml(.cs)`、`AppState.Onboarding.cs`、`MainWindow.axaml` 的引导覆盖层入口
- 截图：`Windows/validation/screens/Onboarding/`（`onboarding-step0..3-{light,dark}.png`，由 `--screenshots` 生成）

## 结构性对照

| Mac 元素 | Mac 行为 | Windows 状态 | 差异 |
|---|---|---|---|
| `OnboardingView` 外框 | 四步（场合/声音/回答/提词与开始），顶部四条进度胶囊 + `1 / 4` + 步骤名 + 预计时间 + 「稍后继续」 | 有：`OnboardingIsScene/Sound/Answers/Start` 控制四条胶囊；`OnboardingPosition`、`OnboardingStepTitle`（场合/声音/回答/提词与开始）、`OnboardingStepTime`（约 15/60/90 秒）、「稍后继续」（SkipOnboardingCommand） | 一致（文案走 Mac 字符串表） |
| 底部栏 | 上一步；下一步/开始第一场；回答步可「跳过」；开始页「先看看」 | 有：上一步、下一步、「跳过」（仅回答步）、「先看看」+「开始第一场」（BeginFirstMeetingCommand） | 「开始第一场」走 `StartRecordingCommand`（无法执行时置灰）；Mac 的 `isChangingSession` 保护未建模 |
| 场合页 `sceneSheet` | SceneGlyph + 大号 serif 场景名 + Promise + Moments 列表；场景 chip 切换；「看一段面试示例」 | 有：`SceneGlyph` + `OnboardingSceneTitle` + `OnboardingScenePromise` + `OnboardingSceneMoments`；Scenes chip（SetSceneCommand）；示例开关（ToggleOnboardingSample → 「看一场示例会议」） | 示例用 Windows 已有的 demo 模式（LoadDemo）而不是 Mac 的内嵌 OnboardingSampleMeeting 时间线；缺 Mac 的 `sampleFinished` 自动收起 |
| 声音页 | 会议在哪里（线上/线下 VenuePicker）、系统音频权限、麦克风、语音识别（本机/云端）、声音测试 | 有：系统音频 CheckBox、麦克风 CheckBox、语音识别（`ModelStatusText` + 下载/准备按钮）、我的声音（`OnboardingVoiceStatus` + 声音测试打开 `OnboardingVoiceWindow`）、只在本机保存文字说明 | 缺 Mac 的「允许系统音频」权限按钮（Windows 无需系统录制权限，属计划允许的差异）；麦克风授权弹窗由系统在首次录音时处理 |
| 回答页 | 回答服务表单（API Key/模型/测试连接），可选 | 有：`Settings.KeyStatus` 状态、「打开设置」、`CheckServiceCommand` 检查连接、可选说明 | Mac 表单内嵌在引导页；Windows 复用设置页（E27 完成能力） |
| 提词与开始页 | CueIslandMock（刘海下的提词岛示意）+ 说明 + 设置清单 + 回到声音 | 有：提词岛示意卡（问题/回答两行）、两句说明（含 Windows 措辞「鼠标移到提词岛上…」）、就绪说明、回到声音、设置清单（`OnboardingChecks` 三行） | 示意卡为静态演示文案，未逐字使用 Mac CueIslandMock 的 `cuePoint` 布局 |
| `SetupChecklist` / `SetupRow` | 设置页「通用」中的可勾选清单（权限/声音/签名等，排除若干项） | 部分：引导开始页展示三项 `OnboardingChecks`；设置页未挂 Checklist | 设置页 Checklist 未接入（E3 后续项） |
| `OnboardingSampleMeeting` | 引导内嵌示例会议（先看一段对话再点开始），`sampleFinished` 后自动收起 | 用 `--demo` 的完整示例（D1 已实现 demo staged entries）；引导页「看一场示例会议」会结束引导并打开 demo | 交互等价，流程不同 |
| `OnboardingVoicePage` | 引导内声音录入页 | 复用现有 `OnboardingVoiceWindow`（MyVoiceViews 对应窗口） | 一致（窗口复用） |
| 屏幕录制权限一步 | Mac 有系统录制权限请求 | 已按任务要求**去掉**（Windows 不需要该权限） | 计划内差异 |
| 版本/持久化 | `quickStartStep`（AppStorage）记录步数；`OnboardingVersion` 判断是否再显示；「稍后继续」进入 checklist 入口 | 有：`OnboardingStep`（AppState）、`OnboardingVersion.NeedsGuide`、`OnboardingCompletedVersion`；「稍后继续」= SkipOnboarding（完成引导） | Mac 的「稍后继续」保留首页入口；Windows 完成即关闭，首页无「继续引导」入口 |

## 点按记录（demo 数据）

见 `Windows/validation/screens/Onboarding/click-through.md`（待构建可用后补齐真实点按结果）。

## 结论

四步引导的页面结构、文案（Mac 字符串表逐句）、切换与退出行为已在独立 `OnboardingView` 中实现，并接回主窗口覆盖层；旧的 MainWindow 内联 40 行引导块已删除。未完成项：内嵌示例会议时间线、设置页 SetupChecklist、切步动画（Motion.phase 逐段出现）。状态最高「已实现、未验证」。
