# 实现计划（对照现有代码）

本文把 01–03 的设计落到代码上。**截至本文写成时，代码还没有按新设计修改。** 分支 `claude/design-system-v2` 是早期被否决方向（原生风格 + 紫色主按钮）的实验，已作废，不要在它上面继续。

每一项都写了：改什么、在哪个文件、注意什么。按顺序做，每一节可以单独提交。

## 总原则

- **保持旧数据可读。** `Workspace`、`SessionRecord`、`SessionMode` 都是 Codable 持久化的。只加字段（带默认值），不改 rawValue，不删字段。`SessionMode` 读到未知值会回到 `.general`，这一点要保留。
- **不改语音、回答、资料管线的核心逻辑**，只改它们的入口和呈现。例外是第 7 节的纪要流程。
- **新写或改动的界面文字一律走 String Catalog**（第 1 节），不要再写死中文。
- **每完成一节都要能编译。** 这个仓库在 Linux 上编译不了（AppKit / SwiftUI / sherpa-onnx），需要在 Mac 上执行 `swift build`、`swift test`，或者用 CI 的 `Verify` 工作流。

## 0. 设计变量（纸与墨）

文件：`Sources/NextThought/LiquidGlass.swift`（建议改名为 `DesignSystem.swift`，或者新建文件后让旧名字的类型转调新实现）。

- 按 `design-system/tokens.json` 定义颜色。用动态颜色，浅色、深色各一套，可以用 `NSColor(name:dynamicProvider:)` 包一个 `Color.dynamic(light:dark:)`：
  - `paper` `surface` `surfaceRaised` `sunken` `hover`
  - `ink` `onInk` `inkSecondary` `inkTertiary` `hairline`
  - `ai` `aiSoft` `live` `ready` `caution` `focus`
- **字体**：
  - `display`、`section` 用 `Font.system(size:weight:design: .serif)`，在 macOS 上就是 New York，中文自动回落到系统宋体。
  - 其余用系统字体。
  - 字号表见 01 文档。
- **按钮**：
  - 主按钮：`ink` 底、`onInk` 字，圆角 6，高 32（大号高 40）。
  - 次按钮：`sunken` 底。
  - 无底按钮：悬停时出现 `hover` 底色。
  - 紫色不用在任何按钮上。
- **卡片**：`surface` 底，1px `hairline` 描边，圆角 10，不加阴影。只有浮层（提词器、菜单）用 `shadow-float`。
- **去掉所有玻璃和渐变**：
  - `glassEffect`、`FrostedFallback`、`Brand.gradient`、`AppBackdrop` 的径向光晕都去掉。
  - 窗口背景就是 `paper`。
  - 为了少改调用点，可以保留旧的函数名（`glassSurface`、`glassCapsule`、`cardSurface`、`PrimaryButtonStyle` 等），只换实现。
- **AI 内容**（建议回答、AI 重点、纪要的总结）统一用 `aiSoft` 底，标题前加 `sparkle` 图标并用 `ai` 色。
- **动效**：
  - 悬停 120ms，展开 200ms，阶段转场 320ms，用 easeOut，不回弹。
  - 流式回答逐词淡入（opacity），不位移。
  - 尊重“减少动态效果”。

## 1. 多语言地基（第一步就做）

- `Package.swift` 加 `defaultLocalization: "zh-Hans"`。
- 新建 `Resources/Localization/Localizable.xcstrings`，源语言 zh-Hans，加入 en。
- **打包**：`scripts/build-app.sh` 在拷贝资源后，用 `xcrun xcstringstool compile Resources/Localization/Localizable.xcstrings --output-directory "$app_path/Contents/Resources"` 生成 `zh-Hans.lproj` 和 `en.lproj`。这样 `Bundle.main` 能查到翻译，SwiftUI 的 `Text("…")` 直接生效。需要在 Mac 上验证这个命令和输出目录。
- `Resources/Info.plist`：
  - 加 `CFBundleLocalizations`（zh-Hans、en）。
  - 用 `InfoPlist.xcstrings`，或者在打包时生成 `InfoPlist.strings`，翻译权限说明文字。
- **App 内切换语言**：
  - 位置：设置 → 通用 → 语言（跟随系统 / 简体中文 / English）。
  - 实现：写 `UserDefaults.standard.set([code], forKey: "AppleLanguages")`；选“跟随系统”就删掉这个键。然后提示并自动重开 App。
  - 会议进行中禁用这一项。
- **代码约定**：
  - `Text("开始")` 这类字面量会自动作为翻译键。
  - 存在变量里或拼接的字符串用 `String(localized: "…")`。
  - Core 模块里的标题（例如 `WebSearchMode.title`、`SessionMode.title`、`AnswerStyle.title`）用 `String(localized:bundle: .module)`。Core 目标也要加资源，或者把这些标题移到 App 目标里做。
- **这一步只要求**：新写或改动的界面全部本地化。旧界面（设置的大部分）放到第二步迁移。英文文案按 03 文档的术语表来。
- **AI 提示词**：
  - 回答的提示词加一句“用对话所用的语言回答”。
  - 纪要的提示词按界面语言选择中文或英文模板（第 7 节）。

## 2. 导航与会中专注模式

文件：`ContentView.swift`、`Models.swift`（`AppSection`）、`AssistantView.swift`、`AssistantLayout.swift`、`DetachedModules.swift`。

- **顶栏标签改成侧边栏**：首页 / 所有会议 / 资料夹，设置放在侧边栏底部，也能用 ⌘, 打开。
  - `AppSection` 改成 `home`、`meetings`、`library`、`settings`。旧的 `assistant`、`materials`、`history` 的 rawValue 如果有持久化，要做映射。
- **`state.isRunning` 为真时进入专注模式**：
  - 侧边栏隐藏，整个窗口只显示会中页（第 5 节）。
  - ⌘, 打开的设置用独立窗口（`Settings` 场景或 `openWindow`），不切换主窗口内容。
- **会中不再提供**布局预设、模块显示或隐藏、弹出为独立窗口。
  - `AssistantLayout`、`DetachedModules` 可以先保留代码，但入口（`LayoutMenu`）从界面上移除。
  - 窗口固定两栏，宽度小于 760pt 时改成上下排列。

## 3. 会议类型与系列

文件：`Sources/NextThoughtCore/SessionContext.swift`（`SessionMode`）、`Workspace.swift`。

- **`SessionMode` 增加 case**：`labMeeting`（组会）、`client`（客户沟通）、`discussion`（小组讨论）、`custom`（自定义）。
  - 已有的 `academic` 标题改为“汇报 / 答辩”，`meeting` 改为“工作会议”，`lesson` 改为“讲座 / 课程”。
  - rawValue 不变。
- **每种类型的默认值**集中放在一个表里（例如 `MeetingTypeDefaults`），内容见 02 文档“会议类型”表：
  - 会中默认：提词 / 只记录
  - 回答长度
  - 纪要模板
  - 准备时提示放什么
- **自定义类型**：`Workspace` 加可选字段 `customTypeName: String?`，以及基础类型。
- **系列**：在界面上把“场景”叫作“系列”。`Workspace` 已经存着 `documents`、`lessons`、`questionBank`，会话记录也通过 `workspaceID` 关联到它，所以数据不用迁移。
- **“接着上一场”**：
  - 准备新会议时，用标题、公司名、类型找最近的系列。可以先用简单规则：标题去掉“一面 / 二面 / round N”之后做相似度比较。
  - 列出可以带入的内容（资料、上一场复盘、经验、完整转写），默认勾选前三项。
  - 勾选结果决定这场会议用哪些 `documents`、`lessons` 和 `questionBank` 条目。
  - 还要提供“从以前的会议带入…”手动入口。
- **自动猜类型**：日历标题里有关键词（面试 / interview、组会 / group meeting / lab meeting、答辩 / defense、讲座 / lecture、例会 / sync）时预先选好，否则用 `general`。

## 4. 首页与准备页

新文件：`HomeView.swift`、`PrepareView.swift`。参考 `previews/HomePage.html`、`previews/PreparePage.html` 和对应截图。

- **首页**：
  - 下一场会议卡片（第二步接日历之前，这里显示“新会议 / 立即记录”）。
  - 新会议、立即记录（⌘⇧N）。
  - 最近 2 场会议，每场写出结果（待办数、待改进题数）。
- **准备页**，四步：
  1. 会议类型：标签平铺加“自定义…”，下面一行写这个类型的默认值。
  2. 接着上一场：有匹配时才显示。
  3. 这次新加的资料：复用现有的资料导入和预处理管线（`DocumentImporter` / `DocumentPreparation`）。显示“读取中 / 已读懂”；读完后显示“AI 读到的重点”2–3 条，以及“可能会问”入口（复用现有的“预测问题”）。
  4. 开始前检查，三行：
     - 声音：线上 / 线下，加电平。
     - 回答：模型名，开着快速模式时标出“快速”。
     - 手机：只在开启了手机伴侣时显示。
- **开始按钮**：始终可以点；资料没读完时按钮上带进度。权限在第一次点“开始”时就地申请，原有的新手引导降级为“设置 → 基础”里的入口。

## 5. 会中页

参考 `previews/LiveSessionPage.html`、`LiveNotesPage.html`、`AnswerSettings.html`、`SessionStatus.html`，以及 02 文档里“每个功能放在哪”的表。

- **顶栏**（从左到右）：
  - 录制计时，点击暂停（合并原来的暂停按钮）。
  - 会议标题。
  - 提词 / 只记录分段控件。
  - 手机图标（带状态点）。
  - 模型标签（快速模式时后面加 ⚡）。
  - 提词器开关。
  - 结束。
- **提词 / 只记录**：取代设置里的 `automaticAnswers` 开关。会中切换就是改这个值，默认值由会议类型决定。设置里的独立开关删掉。
- **模型标签**：点开“回答设置”菜单，包含：
  - 模型列表（`codexModels`，或 API 预设的模型）。
  - 快速模式：只在 `codexFastAvailable` 时显示。
  - 联网：`WebSearchMode` 三档。
  - 回答长度：`AnswerStyle`。
  - 底部写“从下一个回答开始生效”。
- **手机状态**：
  - `CompanionServer` 暴露当前连接数（现有的 `streams.count`），通过回调更新到 `AppState`（例如 `companionClientCount`）。
  - 中转和蓝牙沿用现有的 `companionRelayStatus` 和 `companionBluetoothStatus`。
  - 绿点 = 至少一路已连接，琥珀点 = 已开启但没有连接。
  - 没开启手机伴侣时，图标不出现。
- **右栏（建议回答）**：
  - 联网状态只在发生时出现。
  - 回答下方露 3 个改写，其余加上“重新生成”“联网查一下”放进“…”。
  - 头部 ‹ n/m › 翻看记录（取代“回答记录”模块）。
  - 底部一行写依据。
- **左栏（实时对话）**：底部一行写识别引擎和回声状态。线下会议标“现场”。
- **底部输入栏**：
  - 输入框，⌘L 聚焦。
  - 截图菜单：框选区域 / 选择窗口。
  - 立即回答，⌘⇧↩。这是唯一的主按钮。只记录模式下不显示，靠输入框提示和快捷键。
- **提词器**（`TeleprompterController.swift`）：
  - 视觉换成 `surfaceRaised` 加 `shadow-float`。
  - 底部只放 3 个改写，加一个“回答 / 停止生成”二选一按钮。
  - 复制、打开工作台收进“…”。

## 6. 声音来源：线上 / 线下

文件：`AudioCaptureService.swift`、`AppState.swift`、`AppState+Live.swift`、`MicrophoneCaptureService.swift`、`SessionRecord.swift`（`SessionSpeaker`）。

- **线上（默认）**：
  - 对方 = `display:` 目标，也就是全部系统声音，已经设置了 `excludesCurrentProcessAudio`。在界面上叫“电脑声音”。默认选主显示器，不依赖识别会议软件。
  - “只听某个窗口…”作为次要选项，保留现有的 `window:` 目标。
- **线下**：
  - 不启动系统声音采集，只开麦克风。
  - `SessionSpeaker` 增加 `.room`（“现场”），线下时麦克风的转写标为 `.room`。
  - 关闭回声抑制。会中默认“只记录”。
- **线上 / 线下按会议保存**；“立即记录”沿用上次的选择。

## 7. 会后纪要流程

文件：`AppState+Archive.swift`（`generateSummary`、`applyReview`）、`SessionArchive.swift`，以及新文件 `NotesTemplates.swift`、`NotesSettingsView.swift`。参考 `previews/SummaryPage.html`、`ReviewPage.html`、`NotesProgress.html`、`AcademicNotes.html`、`NotesSettings.html`。

- **纪要模型单独配置**：设置 → 纪要，包含：
  - 模型和推理强度，默认最强模型、推理“高”、不开快速模式。
  - 结束后自动整理（默认开）。
  - 纪要语言（默认跟界面语言）。
  - 模板管理。
  不再复用会中回答的模型设置。
- **流程**（在后台跑，页面上显示三步进度）：
  1. **校对转写**：完整转写，加上本场资料里的专有名词（人名、公司、术语、专业词表），交给纪要模型纠错。输出“原文 → 校正”的列表，保存在会话里。界面上用虚下划线标出改过的地方，可以撤销。不重新识别，不保存录音。
  2. **按模板整理**：模板由若干部分组成，每部分包括名字和一句要求。输出要求结构化（JSON），每一条必须带原话的时间点。
  3. **核对**：去掉没有时间点依据的条目；相对日期按会议日期换算成具体日期；不确定的条目标“待确认”。
  长会议沿用现有的分段（chunks）再合并。
- **内置模板**（内容见 02 文档“按场景的模板”）：
  - 面试复盘
  - 学术
  - 工作会议
  - 客户沟通
  - 讲座 / 课程
  - 小组讨论
  - 通用
  会议类型决定默认用哪个模板。纪要页可以“换个模板重新整理”。自定义模板可以复制内置模板再改。
- **存回系列**：沿用 `applyReview`。
  - 面试：待改进的题和更好的答法写进 `questionBank`，改进点写进 `lessons`。
  - 学术、工作会议：未决问题和未完成的待办写进系列，供下一场“接着上一场”使用。可以作为 `lessons` 的一种类型，或者新增字段。
- **纪要页**：
  - 主按钮是“复制纪要”（Markdown）。
  - “…”里放导出、换模板、换纪要语言。
  - 面试复盘的主按钮是“存进题库 · n 题”。

## 8. 收尾

- **删掉旧入口**：
  - 顶栏的 `GlassTabBar`。
  - 旧助手页的 SetupCard、LayoutMenu。
  - 设置里的“自动回答”开关。
  - 重复的“新手引导”按钮。
- **演示模式**：`DemoMode.swift` 里的演示数据要跟上新页面，`screenshots.yml` 依赖它生成截图。
- **测试**：Core 的单元测试要覆盖：
  - `SessionMode` 新 case 的编解码，以及旧值兼容。
  - 类型默认值表。
  - 日期换算。
  - 模板解析。
- **更新文档**：更新 `README.md` 的截图和“界面导览”，以及 `docs/SETTINGS-DESIGN.md`。

## 第二步及以后（本轮不做）

- **接日历**：EventKit，`requestFullAccessToEvents()`，`Info.plist` 加 `NSCalendarsFullAccessUsageDescription`。只读标题、时间、是否线上会议。首页显示“下一场会议”，开会前 10 分钟通知（可关）。
- **迁移剩余旧界面的文字**，完成整个 App 的英文版。
- **“一场会议 = 一页”的数据合并**：会话记录和准备合并；资料夹从系列里拆出来，可复用。
- **以后**：
  - 连接邮箱，自动找资料。
  - 读取参会人。
  - 会中区分说话人。
  - 检测到通话时提醒开始记录。
