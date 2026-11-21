> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# E11 实时翻译界面：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/LiveTranslation.swift`（415 行全文：TranslationDirection.detect、LiveTranslation 状态机、turnOnForForeignSpeech、LiveTranslationToggle、LiveTranslationHosts、TranslationLaneHost）、`Sources/NextThought/LiveSessionView.swift` 的 LiveTranslationToggle/PartialLines/TranscriptLine 译文渲染、`Sources/NextThought/AppState+Live.swift` 的 provisionalTranslation 抄写到定稿行
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无对应截图，按源码逐元素对照
Windows 代码：`Views/LiveView.axaml`（工具行开关/状态、两条未定稿 lane 的译文）、`AppState.LiveTranslation.cs`（新增：开关/状态/自动打开提示/文案）、`AppState.Live.cs`（beginMeeting 挂接、partial 自动打开与开关闸门、LooksForeign 改为方向检测）、`TranscriptRow.cs`（转写行「译文：…」）、`Core/LiveTranslationPolicy.cs`（新增：方向检测 + 自动打开策略）、`Core/Models.cs` + `SettingsViewModel`（`liveTranslation` 记忆选择）
Windows 截图：`Windows/validation/screens/translation/`（本轮共享树被其他队友在途改动阻塞构建，截图/点按改为 verify 阶段补跑）

## 1. 翻译方向与检测（TranslationDirection.detect）

| Mac | Windows | 结论 |
|---|---|---|
| 按 unicode script 计 han/latin：纯中文→中译英、纯英文→英译中；混合按 5:7 比例判主导；再不行用 NLLanguageRecognizer；仍不定则 nil | `Core/LiveTranslationDirectionDetector.Detect` 相同 script 计数与 5:7 主导规则；无 NaturalLanguage 模型，最后按字符数多者定（单测覆盖纯中/纯英/混合/空格/数字） | 一致（混合歧义路径简化为字符数比较） |
| 「四个空格分隔的词」作为自动打开门槛 | `HasEnoughEnglishWords` 同规则（`>= 4`） | 一致 |
| 方向由识别的句子决定，与音频来源无关 | 同（每条 partial/final 文本自己检测） | 一致 |

## 2. 开关与自动打开（LiveTranslationToggle / turnOnForForeignSpeech）

| Mac | Windows | 结论 |
|---|---|---|
| 工具行「开启同声传译 / 关闭同声传译」按钮 + 帮助文案「自动识别我和对方说的中文或英文，并翻译成另一种语言」 | LiveView 工具行新增开关按钮（`LiveTranslationButtonLabel`，文案同 Mac）+ Tooltip 同句 + 开启时状态小字（同声传译 · 译文） | 一致 |
| 用户手动开关会记住选择（UserDefaults `liveTranslation`） | `Settings.LiveTranslation`（`bool?`，JSON `liveTranslation`）+ `SettingsViewModel.LiveTranslation` 映射；开关同时写 `Settings.TranslateSpeech` 与 `Settings.LiveTranslation` 并保存 | 一致 |
| 未选择时：对方系统音说英文（≥4 词、界面中文、两方向语言包已安装）自动开启一次（不记住） | `MaybeAutoOpenLiveTranslation`：录音中、未选择、界面非英文、方向=英译中、≥4 词、translator 可用（Windows 的「语言包已装」等价条件）→ 自动开启并显示提示「听到外语自动翻译 · 同声传译」；一场会议只自动尝试一次 | 一致（提示为 Windows 增加；语言包用 translator 是否存在近似） |
| beginMeeting：上一场的选择保留；开启时状态「自动识别 · 中英互译」，准备中「正在准备中英互译…」 | 开始记录时 `BeginLiveMeetingTranslation` 恢复记忆选择并生成状态（同声传译 · 译文）；无「准备中」中间态（Windows 翻译服务无预加载阶段） | 部分（缺准备中状态/自动识别文案） |
| 关闭时清空两路 partial 译文与 ready 状态 | 关闭开关只停止新请求；已有 partial 译文在下一句 final/清空时移除（`clearPartial` 路径未改） | 部分 |
| macOS 15 以下禁用并提示版本要求 | Windows 无系统翻译 API 下限问题（用回答服务翻译），开关始终可用 | 差异（无禁用态） |

## 3. 译文宿主与分区布局（LiveTranslationHosts / TranslationLaneHost / PartialLines）

| Mac | Windows | 结论 |
|---|---|---|
| 两个隐藏的 `TranslationLaneHost`（英→中、中→英）1×1 透明挂在视图里，只负责驱动 translationTask | Windows 无系统翻译会话，两条 lane 由 `LiveTranslationCoordinator`（Core 既有）+ 回答服务翻译在后台驱动；无 UI host 组件（等价物是状态机，见未解决） | 部分（宿主机制不同） |
| 实时页 PartialLines：对方/我两路未定稿原文，各自下面显示「译文：…」 | LiveView 未定稿区改为两条 lane：原文 + `译文：…`（`SystemPartialTranslationLine`/`MicrophonePartialTranslationLine`，仅在有译文时显示），对方/我标签同 Mac | 一致（视觉布局对齐；译文来源为后台翻译） |
| 定稿转写行：原文下「译文：<translation>」（Mac TranscriptLine translatedText） | `TranscriptRow.BuildTranslation` 改为 `译文：<翻译>`（此前是「英译/中译」前缀），字幕条副行仍显示原始译文 | 一致（文案对齐） |
| 译文到达后转发写进定稿行（provisionalTranslation） | 既有：定稿后在 `AppState.Audio` 里调用翻译服务写 `translatedText` 并保存；未定稿译文不转写 | 一致 |
| 字幕条（Mac SubtitleBar 第二行译文） | `SubtitleBarWindow` 第二行显示 `TranslationRaw`（字幕条设置控制显隐） | 一致（本轮未改字幕条） |

## 4. 新增测试

`Windows/tests/NextThought.Tests/LiveTranslationPolicyTests.cs`（6 个）：
- 纯中/纯英/空白/数字的方向检测；混合句按主导 script；
- 「≥4 个英文词」门槛；
- 未选择 + 中文界面 + 语言包可用 + 对方英文 → 自动开启 **一次**；已选择则不自动开启；
- 界面非中文 / 语言包不可用 / 中文语音不触发；
- 手动选择在下一场 beginMeeting 保留。

## 5. 自检点按记录

计划：`--self-test=translation`（开关状态切换、`Settings.LiveTranslation` 落盘、自动打开策略注入、两条 partial 译文绑定、转写行「译文：」前缀、字幕条译文），并截图 Live 页开启/关闭两态。

**本轮运行状态（如实记录）**：共享树正被其他队友在编辑（`AppState.Insights.cs`/`AppState.SlideFollow.cs` 等报错为他人 WIP），Desktop 构建未通过，**自检与截图未执行**；核心策略由单测覆盖。状态「已实现、未验证」。

## 6. 未解决 / 待补

1. 无 Mac 的隐藏 `TranslationLaneHost`（系统 TranslationSession）等价实现：译文由回答服务翻译，无方向专属的长驻会话与「准备中」状态；语言包安装检测用 translator 可用性近似。
2. 关闭开关时不清空已显示的 partial 译文（`clearPartial` 仅在 final/切场时调用）；Mac setEnabled(false) 立即清两路。
3. 自动打开提示为 Windows 增加（Mac 无提示）；Mac 的「自动识别 · 中英互译」状态文案未照搬（用现有已翻译文案）。
4. 无 macOS 版本门槛禁用态；Windows 上开关始终可用。
5. 真实外语识别/翻译未在自动化中执行（需要音频与翻译服务），自检与截图待树恢复后补跑。
6. LiveView 只改了工具行开关/状态与未定稿 lane 的译文（d-parity E6 已提交后再改）；其余 Live 布局归 E6/后续任务。
