# 首次引导 · Mac ↔ Windows 逐元素对照（§6.3 / §6.7 / E3）

- Mac 基准：Mac 3.4.0 `Sources/NextThought/Onboarding.swift`（`OnboardingView` 四步：header / scenePage+sceneSheet / soundPage / answersPage+`AnswerEngineForm` / startPage+`CueIslandMock` / footer）、`OnboardingSampleMeeting.swift`、Core `OnboardingFlow.swift`、`LiveNotesEngine.swift`（场景即 lastScene）。
- Mac 参考截图：`../../mac-screens/onboarding/step-{1,2,3,4}-{light,dark}.png`（1600×1171，窗口 1120×820，比例 1.4214）。
- Windows 截图：`onboarding/onboarding-step{0,1,2,3}-{light,dark}.png`（1120×820，`--demo --screenshots`，数据目录隔离；step0=场合 … step3=提词与开始）。
- 结论见文末「结论」。

## 1. 框架（header / footer / 动效）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 顶部进度 | 4 条 22×2 capsule：已完成 secondary、当前 `Ink.ink`、未到 hairline；capsule 内 spacing 4 | `OnboardingProgressCapsules`（4 个 Border 22×2 radius 1，颜色按 `GuideStep` 计算，spacing 4） | 一致 |
| 2 | 「1 / 4」 | caption mono digits secondary | `OnboardingPosition`「1 / 4」caption（数字等宽） | 一致 |
| 3 | 步骤名 + 预计时间 | label「场合」+ caption「约 15 秒」/「约 1 分钟」 | `OnboardingStepTitle` label + `OnboardingStepTime` caption；四步「场合/声音/回答/提词与开始」，15/60/90/15 秒 | 一致 |
| 4 | 稍后继续 | 右上 ghost「稍后继续」→`deferOnboarding`（首页留续做入口） | quiet「稍后继续」→`SkipOnboardingCommand`→`DeferOnboarding`（`OnboardingResumeRow` 已由 E1 接首页） | 一致 |
| 5 | header 间距 | 左右 32、上 32、下 12；元素间 12 | `Margin=32,32,32,12`，各元素 margin 12 | 一致 |
| 6 | 内容列 | 最大 720 居中、左右 32、上 24、下 24 | `GuidePageHost Width=720` 居中、`Margin=0,24,0,24` | 一致（窗口 1120 时列左 200） |
| 7 | 页脚 | 左右 32 上下 16、顶部 hairline；左「上一步」（非首步才显示）；右侧按步骤：回答未配置→「跳过」、最后一步→「先看看」+「开始第一场」、其余→「下一步」primary | 同：`Margin=32,16` + hairline；`!OnboardingIsFirst` 显示上一步；`OnboardingCanSkipAnswers`（回答步且未配置）显示跳过；最后一步显示先看看+开始第一场；其余下一步 primary；主按钮高 40 radius 9 | 一致 |
| 8 | 切步动效 | `withAnimation(Motion.phase)`（0.46s spring）+ opacity 过渡 | `GuideMotion.PhaseIn`（460ms cubic-out 淡入），`OnboardingStep` 变化触发 | 一致（时长/曲线为近似，见差异 1） |
| 9 | 首次打开 | 首次启动/功能升级版本出现；`--onboarding` 可强制 | `LoadOnboarding`（版本判断、演示模式不自动弹）+ `--onboarding`；截图批用 `OnboardingVisible` | 一致 |

## 2. 第 1 步「场合」（scenePage + sceneSheet）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 标题 | `Typo.display` 30 serif medium「NextThought 按场合帮你开会」 | `Classes=display`（30 serif medium） | 一致 |
| 2 | 说明 | body 15 secondary「先选一个最常用的场合。每个场合有自己的提示和纪要，开会前随时可以换。」 | body secondary 同句 | 一致 |
| 3 | 场景卡 | `Ink.surface` radius 22、contact + ambient(28/16) 阴影、hairline 0.5 | `Surface` radius 22 + hairline + `BoxShadow 0 1 2 …/0 16 28 …` | 一致 |
| 4 | 卡内标题行 | `SceneGlyph` 38 + 场景名 19 medium；左右 24、上 24、下 16 | `SceneGlyph Size=38` + 19 medium；`Margin=24,24,24,16` | 一致 |
| 5 | 场景 chips | `SessionMode.builtIn` 9 个；13 medium、高 30、左右 11、Capsule；选中=场景色 13% 底 + 场景色字；悬停 Hover；FlowLayout spacing 4 | `OnboardingSceneChips` + `Button.guideChip`：同尺寸/字重/圆角；选中 `TintAt13` + `Tint` 字；悬停 Hover；`WrapPanel` spacing 4 | 一致（9 个 chip 一行，与 Mac 相同） |
| 6 | 承诺 + 三条说明 | `theme.promise` 17 serif medium；三条：场景色 4pt 圆点 + caption，纵向 spacing 6；左右/上下 24 | `OnboardingScenePromise` 17 serif medium；`OnboardingSceneMomentRows`：场景 tint 4pt + caption 纵向 6；`Margin=24,16,24,24` | 一致 |
| 7 | 示例展开 | `› 看一段面试示例`（chevron 旋转），展开：一句说明 + `OnboardingSampleMeeting`（左实时转写卡/右参考回答+面试记录卡/底部进度+重播） | 同：`ToggleOnboardingSampleCommand` + chevron 右/下切换；说明句；两栏示例卡（`OnboardingSampleVisibleLines/Takeaways`、参考回答、进度、重播）；关闭引导停止回放 | 一致（无自动滚动跟读/逐字动画，沿用 E3 限制，见差异 2） |
| 8 | 首屏其它 | 无「看一场示例会议」按钮 | 已移除该补充入口（Mac 没有） | 一致 |

## 3. 第 2 步「声音」（soundPage）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 标题/说明 | display 30「让它听得到」；body secondary「只在这里问一次。每一项都要你按下按钮才会请求，不允许也可以先往下看。」 | 同 | 一致 |
| 2 | 会议在哪里 | `GuideRow`：标题 13 medium + 12 secondary「线上：听电脑里会议软件播放的声音。」/「线下：只用麦克风，声音标为“现场”。」；右侧 `InkSegmented` 线上/线下 | `guideRow` + `OnboardingVenueDetail`（同句）+ 本地 InkSegmented（线上/线下 RadioButton，样式同 §3.4 prepare） | 一致 |
| 3 | 系统音频 | dot（ready/caution）+「系统音频」+「已允许 · 能听到会议里对方的声音。」（未允许时右侧「允许」按钮） | dot ready +「系统音频」+「无需授权 · 能听到会议里对方的声音。」（Windows 无 macOS 屏幕录制授权，此行为平台事实文案） | 平台差异（文案按 G10 允许的屏幕录制权限差异，见差异 3） |
| 4 | 麦克风 | dot（ready/idle/caution）+「麦克风」+ 状态句（已允许/未允许/首次询问，线上会给「记录我的声音 / 只记录对方」segmented；未允许给允许按钮） | dot（`OnboardingMicrophoneTone`）+「麦克风」+ `OnboardingMicrophoneDetail`（同 Mac 语义，Windows 设置措辞）；segmented「记录我的声音/只记录对方」；未授权时「允许/打开设置」→`OpenMicrophonePermissionSettingsCommand` | 一致（平台设置入口不同） |
| 5 | 语音识别 | dot（ready/caution）+「语音识别」+「X-ASR 中英模型 · 在这台 Mac 上离线识别，已就绪。」；未安装时「下载」 | dot +「语音识别」+「X-ASR 中英模型 · 在本机离线识别，已就绪。」（演示模式按 Mac 显示已就绪）；未就绪时「下载」→`PrepareModelsCommand` | 一致（Mac→Windows 措辞替换允许） |
| 6 | 换一种识别方式 | `DisclosureGroup`：chevron + caption 行；展开一段说明 | 同：chevron 按钮 + `ToggleRecognitionOptionsCommand` 展开说明句（无边框/无卡片） | 一致 |
| 7 | 底部隐私说明 | caption secondary 两行：`recordingDataRouteSummary`（本机识别/不保存原始录音；使用 AI 时发送范围） | `OnboardingDataRouteSummary` 同两行（这台 Windows） | 一致 |
| 8 | 行样式 | `GuideRow`：HStack top、spacing 16；标题 13 medium + 说明 12 secondary；行上下 12；行间 hairline 0.5；无卡片 | 同：`guideRow` Padding 0,12；行间 hairline；无卡片 | 一致 |

## 4. 第 3 步「回答」（answersPage + AnswerEngineForm）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 标题/说明 | display 30「接上一个回答服务」；body secondary「会中的回答和会后的纪要由它来写。选一个服务商，粘贴 Key，点一下检查。」 | 同 | 一致 |
| 2 | 服务商 | caption「服务商」+ chips：DeepSeek、通义千问、Moonshot Kimi、智谱 GLM、火山方舟 豆包、OpenAI、OpenRouter、Grok、本机 Ollama、本机 LM Studio、其他 | 同列表/顺序，`guideChip`；点击打开设置页选择服务商（完整表单在设置） | 一致（chips 不就地展开表单，见差异 4） |
| 3 | 连接状态 | 折叠标题「使用 Codex (已有 ChatGPT 账号) 当前使用」+ 状态行「Codex 已登录 (ChatGPT 账号) / Logged in using ChatGPT · codex-cli 0.160.0」+ 刷新按钮 | 状态行 dot +「Codex 已登录 (ChatGPT 账号)」+「Logged in using ChatGPT · codex-cli 0.160.0」（演示模式与 Mac 截图一致；真实连接时显示「已连接回答服务 / API · 模型」） | 一致（刷新/折叠外层未做，见差异 4） |
| 4 | 模型 | 「模型」+ `MenuPicker`「GPT-5.5」 | 「模型」+ `OnboardingModelLine`（演示模式 GPT-5.5）；真实连接显示模型名 | 一致（选择器内联未做，见差异 4） |
| 5 | 更多选项 | `› 更多选项` 展开更多设置 | 同：chevron + `ToggleAnswerOptionsCommand` + 说明句 +「打开设置」 | 一致 |
| 6 | 测试回答 | secondary「测试回答」+ caption「发一句测试问题，确认回答服务能正常回答。」 | 同按钮 →`TestAnswerEngineCommand` + 同 hint | 一致 |
| 7 | 底部说明 | caption「不连接也能记录和转写；回答和会后纪要需要它，可以之后再接。」 | 同 | 一致 |

## 5. 第 4 步「提词与开始」（startPage + CueIslandMock）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 标题/说明 | display 30「会中，回答在镜头下方」；body secondary「第一场按“面试”开始。」 | 同（`OnboardingStartHeadline`，场景随选择变化） | 一致 |
| 2 | 提词岛示意 | 浅灰（`Ink.sunken`）圆角容器 radius 14 + hairline；居中 380 宽黑色形状，上方两角贴顶、下两角 26 圆角；内容：白 20% 7pt 圆点、（白 50% 11 medium）「能介绍一下你在这个项目里负责的部分吗？」、（白 16 semibold）「我负责订单服务的缓存层。」、两条 cue（场景色 5pt 圆点 + 白 80% 13 medium）「规模：日均 2 亿次读取，P99 降到 18 ms」「我的角色：方案设计和灰度上线由我主导」 | 同结构（外容器 Sunken radius 14 + hairline + `ClipToBounds`；内黑块宽 380、`CornerRadius=0,0,26,26`、padding 22,8,22,18；同样圆点/三行文字/两条 cue，场景 tint 圆点） | 一致 |
| 3 | 两段说明 | body 15：「被问到时，回答出现在摄像头下方的提词岛，眼睛不用离开镜头。」「鼠标停在刘海上，可以暂停、结束或立即回答。」 | 同第一句；第二句按 G10 平台名词替换为「鼠标移到提词岛上，可以暂停、结束或立即回答。」 | 一致（刘海→提词岛为允许的平台措辞差异） |
| 4 | 不能开始时 | caption caution +「回到声音」ghost | 同（`!OnboardingCanStartFirstMeeting` 时显示，`GoToOnboardingStepCommand` Parameter=Sound） | 一致 |
| 5 | 页脚 | 「先看看」ghost +「开始第一场」primary（默认动作；不可开始时禁用） | 同：`CompleteOnboardingCommand` + `BeginFirstMeetingCommand`，`OnboardingCanStartFirstMeeting` 控制启用；演示模式按 Mac 截图启用 | 一致 |

## 6. 动效与交互

| 项目 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 切步 | `withAnimation(Motion.phase)` 0.46s spring + `.transition(.opacity)`、`.id(step)` | `GuideMotion.PhaseIn` 460ms 淡入（cubic-out 近似）；页面 `IsVisible` 切换 | 一致（spring 近似，见差异 1） |
| 场景卡切换 | `Motion.snappy` + `blurReplace`/scale | 即时重绘（场景 chip 点击后 `RefreshGuideScene`） | 差异 5 |
| 示例展开 | `Motion.expand` | 即时显隐 | 差异 5 |
| 关闭引导 | `withAnimation(.easeOut(0.2))` + 保留续做入口 | 即时关闭 + `OnboardingResumeRow` | 差异 5 |

## 7. 交互走查（演示数据）

1. 打开引导：第 1 步显示 4 条进度（首条深色）、「1 / 4 场合 约 15 秒」、右上「稍后继续」；场景卡为「面试」+ 场景色选中 chip + 承诺/三条说明。
2. 点「组会」chip：图标/承诺/三条说明、场景名切换；点「下一步」→ 第 2 步（进度第 2 条深色）。
3. 第 2 步：线上/线下（线上选中）、系统音频「无需授权…」、麦克风「记录我的声音」选中、语音识别「已就绪」、「› 换一种识别方式」可展开、底部两行隐私说明；点「线下」切到只用麦克风（自检断言），再切回。
4. 第 3 步：服务商 11 项、连接状态 Codex 已登录 + GPT-5.5、模型行、「› 更多选项」可展开、测试回答按钮+hint、底部「不连接也能记录和转写…」；已配置时页脚无「跳过」。
5. 第 4 步：提词岛示意（黑块/问题/回答/两条 cue）、两段说明、「先看看」+「开始第一场」；不能开始时会显示「回到声音」。
6. 「稍后继续」关闭引导（首页出现续做入口）；「开始第一场」完成引导并以所选场景开始；`--self-test=onboarding` 走四步/前一步/回声/示例开关/跳过并全部通过。

## 结论

**以下几条不一致**（其余逐条一致）：

1. **切步/展开动效**：Mac 用 SwiftUI spring（`Motion.phase` 0.46s、`Motion.expand` 0.32s、`Motion.snappy`）；Windows 用 460ms cubic-out 淡入，展开/场景切换为即时显隐（Avalonia 无 spring API；长度与 Mac 一致）。
2. **示例会议面板**：Mac 自动跟随滚动 + 逐字出现；Windows 用可滚动容器 + 按时间线加载（`OnboardingSample` 数据与文案逐字一致），无逐字动画。
3. **系统音频行文案**：Mac 需要「屏幕与系统音频录制」授权；Windows 采系统输出无需授权，因此该行写「无需授权 · 能听到会议里对方的声音。」，无「允许」按钮（G10 允许的平台机制差异）。
4. **回答页表单深度**：Mac `AnswerEngineForm` 内联折叠 Codex 详情/刷新、模型选择器、更多选项表单；Windows 引导页提供同顺序的服务商 chips/状态行/模型行/更多选项说明/测试回答，真正的表单在设置 → 回答（同一 `TestAnswerEngineCommand` 交互）。演示数据文案与 Mac 截图逐字一致。
5. **字体度量导致纵向偏移**：Windows 中文字体行高比 Mac 大 3–6%，同结构下卡片/行整体下移约 15–20pt；字号级别/字重/顺序/分组/间距 token 均按 Mac 值。
6. **窗口外壳**：Mac 引导是独立窗口（交通灯标题栏）；Windows 是主窗口内的全屏覆盖层（内容一致，左上无交通灯）——外壳差异归 MainWindow owner。
