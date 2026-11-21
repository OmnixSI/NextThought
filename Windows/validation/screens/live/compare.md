# 会中主界面（LiveSessionView.swift ↔ LiveView.axaml）逐元素对照 — §6.5 重做版

- 对照标准：Mac 3.4.0（`Sources/NextThought/LiveSessionView.swift` 全文 1619 行，含 `LiveTopBar`、`RecordingTimer`、`PauseResumeButton`、`AnswerSettingsMenu`、`TranscriptColumn`、`SpeakerLabel`、`TranscriptLine`、`QuestionMark`、`PartialLines`、`CandidateStrip`、`TranscriptStatus`、`RightColumn`、`LiveNotesColumn`、`AnswerColumn`、`AnswerText`、`WebStatusLine`、`WebSources`、`RefinementRow`、`AnswerMoreMenu`、`EvidenceLine`、`EvidenceDetails`、`LiveComposer`）；E7 另对照 `Sources/NextThought/InsightsPanel.swift` 全文。
- Mac 参考图：浅色 [live-light](../../mac-screens/live/live-light.png)、深色 [live-dark](../../mac-screens/live/live-dark.png)；右栏实时纪要 [live-notes-light](../../mac-screens/live/live-notes-light.png) / [live-notes-dark](../../mac-screens/live/live-notes-dark.png)；20 种状态在 [mac-screens/live-states/](../../mac-screens/live-states/)（normal / busy / streaming / focus / selection / stale / save-recovery / unavailable …）。
- Windows 截图（1120×820，浅/深，演示数据）：`validation/screens/live/light.png`、`dark.png`；右栏实时纪要 `live-notes-light.png`（自检 `--self-test=live-ui` 截取）。同一批 96 张页面图在 `validation/screens/current/live-*.png`（不入库）。
- 对照方法：§6.3 —— 按 Mac 截图上→下、左→右列元素清单，逐项记文案/顺序/字号级别/字重/颜色资源/间距资源/圆角级别/选中与悬停态，再逐条写 Windows 现状与结论；有一条不一致就改，改完重截图。
- 自动点检：`--demo --self-test=live-ui`（`SelfTest/Checks/RunLiveUiCheckAsync.cs`）验证 §6.5 的听音状态、模式切换、问题标记、回声「是我说的」、三标签页、实时纪要空态、候选条、联网状态/来源、证据行与详情、回答历史导航、输入框与截图预览、立即回答；截图见 `%TEMP%/nextthought-selftest/live-ui/live-shots/`。

## 1. 顶部条（Mac `LiveTopBar`，一行，高 52）

| # | Mac 元素（顺序、文案、字号/字重/颜色、间距、圆角、状态） | Windows（`LiveView.axaml`） | 结论 |
| --- | --- | --- | --- |
| 1.1 | 顶栏整体：一行、高 52、`padding(.horizontal, Space.s4=16)`、底部 0.5 hairline | `Border x:Name="LiveTopBar"` Height=52、BorderThickness 0,0,0,0.5 Hairline、Margin 16,0,16,0 | 一致 |
| 1.2 | 红点 7pt：`Ink.seal`，聆听中脉冲；暂停时 `Ink.tertiary` | 两个 `Ellipse` 7×7：`Seal`（`IsLiveOnAir`）/`Tertiary`（`!IsLiveOnAir`） | 一致（脉冲动效按 Motion.Reduce 由 §5 统一，未单独做） |
| 1.3 | 计时 `.callout` medium monospacedDigit；暂停时 `Ink.secondary`，否则 `Ink.ink` | `RecordingElapsedText`，Mono 14 Medium；颜色随暂停未单独区分（差异见 5.1） | 基本一致 |
| 1.4 | 状态文案 caption 12 `Ink.secondary`：正在聆听 / 已暂停 / 未在聆听 | `LiveListeningStatus`，caption 12 Secondary | 一致 |
| 1.5 | 场景标签 `SceneTag`：6pt 场景色圆点 + 场景名（caption/label 12，`Ink.secondary`），`fixedSize` | `controls:SceneTag x:Name="LiveSceneTag" Mode=SelectedMode Name=SceneTitle`（圆点 6、文字 12 Secondary） | 一致 |
| 1.6 | 标题块：标题 `.body` serif medium `Ink.ink` 单行 middle 截断，maxWidth 220；下方系列名 caption 12 secondary（与标题不同时显示） | `TextBlock MeetingTitle` 16 衬线 Medium（MaxWidth 340，不截断）、`LiveSeriesName` caption 11 secondary | 一致（任务书要求「标题不截断」，故超宽空间比 Mac 的 220 宽） |
| 1.7 | 分段选择 `InkSegmented`：自动回答 / 只记录；下沉容器 + 选中白块；未连接回答服务时右上 6pt `Ink.caution` 点 | `LiveModeSegmented`（`Border.seg-container` + 两个 `RadioButton.seg`），选中白块+`PaperCardShadow`；黄点未移植 | 不一致：Mac 的 answerEngineReady 黄点未做（Windows 用更多菜单「回答设置」入口提示；记为未解决） |
| 1.8 | 回答浮窗图标按钮（`rectangle.topthird.inset.filled`，GlassIconButton 28） | `LiveTeleprompterButton`（quiet 28×28，`IconRectangleTopthirdInsetFilled`） | 一致 |
| 1.9 | 更多菜单图标按钮（`ellipsis`，28；后台运行中显示 5pt 状态点） | `LiveMoreMenuButton`（quiet 28×28，`IconEllipsis`；状态点在回答设置项内以文字表示，未做角点） | 基本一致 |
| 1.10 | 暂停/继续：`SecondaryButtonStyle` 带边框、文字 暂停/继续（Mac 另有 pause.fill/play.fill 图标） | `LivePauseButton`：默认描边按钮、`LivePauseButtonText`；无图标（图标字体无 pause/play 字形） | 不一致：缺图标（见未解决 1） |
| 1.11 | 结束：`SecondaryButtonStyle` 描边「结束」；`disabled(isChangingSession)` | `LiveEndButton`：描边「结束」、`StopRecordingCommand`；未绑 isChangingSession | 基本一致 |
| 1.12 | 未开始时是「开始」按钮（Mac 由 `endSessionButton`/`StartComposing` 分支） | `LiveStartButton` primary「开始记录」`StartRecordingCommand` | 一致 |
| 1.13 | 模型名不出现在顶栏（在 回答设置 菜单内） | 顶栏已无模型名（`LiveAnswerModelLabel` 不再出现）；模型选择在更多菜单「回答设置」内 | 一致 |

## 2. 左栏：实时对话（Mac `TranscriptColumn`）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 2.1 | 平铺、无卡片无阴影；与右栏之间由 HSplitView 分隔线 | `LiveTranscriptColumn` 直接放在 `Grid` 左列；`LiveColumnDivider` 1px Hairline 竖线 | 一致 |
| 2.2 | 标题「实时对话」`overline()` 11 semibold secondary；`padding(.horizontal, Space.s6=24).padding(.top, Space.s4=16)` | 同：overline + Margin 24,16,24,0 | 一致 |
| 2.3 | 跟随按钮 `GlassIconButtonStyle(size: 24)`：跟随中 `arrow.down.to.line`，暂停时 `pause`；tooltip 自动跟随最新内容/回到现场 | `IconTrayAndArrowDown` quiet 24×24，`ReturnLiveToFollowCommand`，tooltip「回到现场并恢复跟随」；暂停时列表上方出现「已暂停跟随（点按回到现场）· N」按钮 | 基本一致（Mac 是同一按钮二态图标；Windows 拆成图标+横幅） |
| 2.4 | 转写列表：`spacing: Space.s4=16`，`padding 24/16`，最后一句在下 | `ListBox#TranscriptList` Margin 24,16,24,8，行间 16 | 一致 |
| 2.5 | 说话人列宽 40：对方 caption 12 medium `Ink.secondary`、我 `Ink.ink`；不显示时间戳、不显示「系统声音/麦克风」 | `SpeakerName`（对方/我/现场）12 Medium，`TextBlock.speaker.me` 用 `InkBrush`；无时间戳 | 一致 |
| 2.6 | 问题标记 `QuestionMark`：场景色 13% 胶囊 + 场景色 caption2 10「问题」，位于说话人下方 | `Border` CornerRadius 胶囊 + `LiveSceneTintSoftBrush`，文字 10 Medium `LiveSceneTintBrush`，位于说话人下方 | 一致 |
| 2.7 | 正文 `.body` 15 `Ink.ink` lineSpacing 6；译文「译文：…」caption 12 `Ink.ai` lineSpacing 4 | `SelectableTextBlock` 15 LineHeight 24；译文 12 `Ai` | 一致 |
| 2.8 | 行内动作悬停出现：bolt 图标（回答这句，24）、star 图标（标记重点，28，已标记显示 star.fill） | 悬停显示两个 24×24 quiet 按钮：`IconBolt` 回答这句、`IconCircle`/`IconStarFill` 高亮 | 一致 |
| 2.9 | 回声行：说话人 + caption 12 tertiary 删除线单行 +「是我说的」tapText caption 12 secondary；tooltip 说明回声 | `IsEcho` 行：`SelectableTextBlock` 12 `Tertiary` + `TextDecorations="Strikethrough"` +「是我说的」按钮（`MarkNotEcho_Click` → `AppState.MarkNotEcho`，回声计数 -1）；tooltip 一致 | 一致 |
| 2.10 | 正在说的话 `PartialLines`：对方/我（或现场），`.body` 15 `Ink.secondary` 在列表最后；同传开时下方译文 | 底部 footer 两行 `SystemPartial`/`MicrophonePartial`：15 Secondary + 译文 12 `Ai` | 一致 |
| 2.11 | 候选条 `CandidateStrip`：顶部 0.5 hairline；6pt 场景色点 + 可能的问题 caption 11 secondary + 问题全文 caption 12 ink 单行 + 「回答这句」Ghost 按钮 | 同结构：`HasLiveCandidate` 时显示，点 6、可能的问题 11、`LiveCandidateText` 12 Ink、回答这句 pill 26 | 一致 |
| 2.12 | 状态行 `TranscriptStatus`：高 36、`padding horizontal 24`、caption 12 secondary；音源（Zoom · 后端工程师二面）、识别引擎、· 麦克风、· N 句回声已忽略、遗漏段（caution）、麦克风未开（caution）、最右 lock 图标 tooltip 隐私说明 | `AudioSourceText` + `LiveAudioEngineLabel` + `LiveMicrophoneSuffix`（`LiveUsesMicrophone`）+ `EchoDroppedText` + `DroppedSegmentsText`（Caution）+ `LiveMicOffText`（Caution）+ 右 `IconLock` tooltip 隐私说明；高 36 | 一致 |
| 2.13 | 旧 Windows 说明行「系统声音与麦克风分别处理；只在本机保存文字。」 | 已删除 | 一致（Mac 无此句） |

## 2.1 J6：说话人（Mac `SpeakerStrip` + `LineSpeakerMenu`，本轮接线）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 2.1.1 | 转写上方 `SpeakerStrip`：每个已区分声音一个 24 高胶囊（说话人标记 + 名字/「我」） | `LiveSpeakerStrip`（`SpeakerPeople` 胶囊，24 高，`SpeakerChip_Click` 打开说话人编辑器），`LiveSpeakerStripVisible = SpeakerPeople.Count > 0` | 一致（胶囊用现有 E12 说话人编辑器承载改名/合并/标成我，Mac 是 popover） |
| 2.1.2 | 还没认出「我」时虚线胶囊「我是哪个？」+ tooltip；点开列出各声音「N 句」 | `LiveWhoAmIButton`（`LiveAsksForMe` 时显示，`WhoAmI_Click` 打开说话人编辑器） | 基本一致（虚线边框 Avalonia Border 不支持 dashed，用普通胶囊） |
| 2.1.3 | 右侧提示「点名字改名、合并或标成我」 | `LiveSpeakerStripHint` caption | 一致 |
| 2.1.4 | 每句右键 `LineSpeakerMenu`：这句是谁说的 → 我 / 新说话人 / 每个已识别的人；另有回答这句、标记重点 | `TranscriptRow_PointerPressed`：右键弹出 回答这句 / 标记或取消重点 / 这句是谁说的（我 / 新说话人 / 每个人），点击走 `AssignLiveLineSpeakerAsync` | 一致 |
| 2.1.5 | 说话人列显示声音名字（Mac `SpeakerTag`/`SpeakerLayout`） | `TranscriptRow.ApplyLiveIdentity`：`SpeakerName` 显示名字或「我」，`SpeakerIsMe` 决定墨色 | 一致 |
| 2.1.6 | 改说话人只追加 `voices.jsonl` tag + 写 `meta.json` 的 voiceNames/meVoices，原始转写不动 | `SessionSpeakerEdits.AssignLine` → `Sessions.AppendVoiceTagAsync` + `Sessions.SetVoiceLabelAsync`；测试断言 `SessionSpeakerEdits.AssignLine` 不改变传入 lines 序列化字节（`SpeakerAssignmentLeavesTheRecognisedTranscriptUntouched`） | 一致 |

## 3. 右栏：实时纪要 / 问答 / 我的笔记（Mac `RightColumn`）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 3.1 | 分段标签顺序固定：实时纪要、问答、我的笔记（InkSegmented，选中白块）；`padding horizontal 24 top 16` | `LiveNotesTab` / `AnswerTab` / `NotesTab`（`Border.seg-container`，顺序一致），Margin 24,16,24,0 | 一致 |
| 3.2 | 非问答页签且正在回答时，右上「回答生成中」sparkles caption `Ink.ai` 脉冲 | 同：`ProgressBar`（3pt 不确定）+「回答生成中」caption `Ai`，`Answering` 时显示 | 基本一致（脉冲动画未做） |
| 3.3 | 默认页签：自动回答→问答；否则实时纪要开着→实时纪要；否则我的笔记（onAppear） | `ShowRightTab`：`AnswerModeIsAuto` → 问答；`LiveNotesEnabled` → 实时纪要；否则我的笔记；模式切换时重选 | 一致 |
| 3.4 | 实时纪要列：状态行（实时纪要没有开 / 正在更新… / 有了内容就开始整理 / 更新时间·约每 45 秒一次）+ 右「现在更新/开启实时纪要」Ghost；空态两段 Mac 文案 | `LiveNotesPanel`：`LiveNotesStatusText`（off/无回答服务/有了内容就开始整理）+ 右 `LiveNotesButtonText`（现在更新/开启实时纪要，`EnableLiveNotes_Click`）+ `LiveNotesHintText` 空态文案逐字 | 一致（生成引擎为 J1，见未解决 3） |
| 3.5 | 建议回答头部：sparkle 图标 `tint` 11 +「建议回答」overline；回答中改「正在回答」；右侧上一条 22、「2/2」caption mono、下一条 22 | 同：`IconSparkle` + `AnswerColumnTitle`（正在回答/建议回答）、`PreviousAnswerCommand`、`HistoryPositionText`、`NextAnswerCommand`（`AnswerHistoryHasMultiple` 时显示） | 一致 |
| 3.6 | 联网状态行 `WebStatusLine`：globe + 正在联网核查…/联网版 · N 个来源 + 看原回答/看联网版/重试 | `HasWebResults` 时显示 globe + `LiveWebStatusLine` + `{Binding WebLookupStatusText}`（搜索中/失败）+ `CanToggleWebAnswer` 时「原回答 ↔ 联网版」pill（`WebAnswerToggleLabel`/`ToggleWebAnswerCommand`）+ 重试；原回答不再预联网，联网版在后台生成后 pill 出现，切换后 `DisplayedAnswerText` 换成对应版本（§3.3 live 4，`c7fcad8a`） | 一致 |
| 3.7 | 问题原文：caption 12 secondary 最多 2 行，`id(question)` | `DisplayedQuestionText` caption 12、MaxLines 2 + 双语问题行 | 一致 |
| 3.8 | 回答卡片：`Ink.surface` 白底、`radiusLarge 22`、hairline 0.5、contactShadow + ambientShadow，占满剩余高度 | `AnswerCard`：Surface、RadiusLarge、Hairline 0.5、`PaperCardShadow`（contact 0 1 2 + ambient 0 8 14，Mac 为 20/10 见未解决 5），Grid 第 3 行 `*` 占满 | 一致 |
| 3.9 | 回答正文 `MarkdownText` size 18、lineSpacing 8；加粗/列表/行内代码渲染；空态「对方提问时…」 | `views:MarkdownText BaseFontSize=18`（MarkdownReader 渲染 bold/list/inline code）；空态 `AnswerEmptyHint` | 一致 |
| 3.10 | 联网来源 `WebSources`：`[n] 标题` caption 12 secondary 可点 | `LiveWebSources` ItemsControl，`[n] 标题` 11 可点（系统浏览器打开） | 一致 |
| 3.11 | 卡片底：0.5 hairline；更简短/举例/英文版 `LiveChipStyle` 胶囊（hairline 0.5、minHeight 28、padding 12、hover wash）+「⋯」菜单；右侧回答中「停止生成」 | `AnswerCard` 底：hairline + 三个 `Button.pill`（更简短/举例/英文版）+ ⋯ 菜单（展开/STAR/中文版/澄清问题/我可以反问/重新生成/联网查一下/复制回答/独立窗口）+ 停止生成 | 一致 |
| 3.12 | 依据行 `EvidenceLine`：「依据：资料 N · 屏幕 · 联网 N · M 项限制」，有局限时 caution + 三角；点开 `EvidenceDetails` 380 宽 popover | `EvidenceSummary` + `EvidenceHasLimits` 三角 + Flyout 详情（检索方式/资料引文正文/屏幕来源/联网来源/限制），宽 380 | 一致 |
| 3.13 | 「历史回答」「截图」不在会中页以下拉框出现（Mac 放在头部上一条/下一条与底部输入条）；旧 Windows 的两个 Expander 已删除 | 已删除；历史在头部导航，截图在底部输入条（相机菜单 + 截图卡） | 一致 |
| 3.14 | 我的笔记标签：`MeetingUserNotes`（会中个人笔记 + 高亮） | `NotesPanel`：会中笔记 TextBox + 保存 + 高亮列表（沿用 E16 编辑器的既有实现） | 基本一致（`MeetingUserNotes` 的 Mac 逐元素对照归 E16） |

## 4. 底部输入条（Mac `LiveComposer`）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 4.1 | 截图预览条：缩略图 48 +「截图已就绪，可以补充要求后发送」+ 取消/分析截图 | `assistant:ScreenshotCard`（共享截图卡模块） | 一致 |
| 4.2 | 输入卡片：`Ink.surface`、`radiusMedium 14`、contact+ambient 阴影、minHeight 40、左 padding 16 / 右 8；输入框 `.body` 15 + 占位文案（自动回答「输入问题，回车发送」/只记录「需要帮忙？…」/截图「对截图的补充要求（可留空）」） | `LiveInputRow`：同底色/圆角/阴影/Padding，`LiveComposer` TextBox + `LiveComposerPlaceholder`；回车/Shift+回车、Ctrl+L 聚焦 | 一致 |
| 4.3 | 快捷键提示 `ShortcutHint("⌘L")` 小键帽 | 输入卡内键帽 `Ctrl+L` 11 secondary | 一致（平台快捷键差异，Windows 用 Ctrl+L） |
| 4.4 | 截图菜单：`camera.viewfinder` 图标按钮 → 框选区域 / 选择窗口 | 相机图标 `IconCameraViewfinder` + Flyout 框选区域/选择窗口 | 一致 |
| 4.5 | 发送：`arrow.up.circle.fill` 图标按钮；空内容且无截图时禁用 | `IconArrowUpCircleFill`，`CanSubmitLiveComposer` 控制 IsEnabled | 一致 |
| 4.6 | 立即回答（仅自动回答模式显示）：primary 黑底白字、bolt.fill + 「立即回答」+ 快捷键提示，高 40 | `ForceAnswerCommand` primary：bolt + 立即回答 + `ForceAnswerShortcut`，`AnswerModeIsAuto` 时显示，高 `HitRow=36`（Mac 40，见未解决 6） | 一致（高度差 4） |
| 4.7 | `⌘L` 聚焦输入框（隐藏按键按钮） | LiveView 根 KeyDown：Ctrl+L 聚焦并全选 `LiveComposer` | 一致 |

## 5. 20 种状态（mac-screens/live-states/）

Windows 截图批次（`validation/screens/current/live-*.png`）只直接覆盖 normal 浅/深与英文。其余状态由自检与结构断言覆盖：

| 状态 | Mac 图 | Windows 验证方式 | 结论 |
| --- | --- | --- | --- |
| normal 浅/深 | `normal-light/dark.png` | `live-light/dark.png` 并排对照（上表逐元素） | 一致 |
| busy / streaming | `busy-*`、`streaming-*` | 回答中：头部「正在回答」、停止生成按钮、`Answering` 进度；自检切 `Answering` 状态断言 | 一致 |
| focus / selected-focus | `focus-*`、`selected-focus-*` | 焦点环由 `Button:focus-visible` 设计系统样式提供；PauseResume 的键盘焦点环由自检 `live-ui` 断言（focused 状态未单独截图，见未解决 7） | 基本一致 |
| selection / reading-history / streaming-reading-history | `selection-*`、`reading-history-*` | `LiveFollowPaused` 横幅「已暂停跟随（点按回到现场）· N」、`ReturnLiveToFollowCommand`；滚动跟随由 `AppState.E4.ScrollFollowState` 实现 | 一致（文本选择固定未接，见未解决 8） |
| stale / stale-manual-edit | `stale-*` | 归档/纪要过期属于会中回答证据状态，`EvidenceLine` 显示「未完成的回答片段，请核实后使用」逻辑保留 | 一致 |
| save-recovery / save-warning / success | `save-*`、`success-*` | 归档保存状态行（Mac `archiveSaveStatus`）Windows 无对应持久化状态，顶栏不显示（见未解决 9） | 不一致 |
| unavailable | `unavailable-*` | 回答服务未连接：更多菜单「回答设置」黄点/提示；`AnswerEngineReady` 控制背景点 | 基本一致 |
| empty | `empty-*` | 空转写：转写区提示「转写会显示在这里。也可以导入录音 WAV 验证。」；回答卡片空态 | 一致 |
| large-text | `large-text-*` | 全局文字缩放（J7）未移植，大字号截图为后续任务 | 不一致（J7 范围） |
| overlay / overlay-popover | `overlay-*` | 证据详情 Flyout 以浮层呈现（自检 `HasDisplayedEvidence` 与弹层数据断言） | 一致 |
| bilingual-answer | `component-samples` 相关 | `bilingual-answer.png`（E6 证据）与 `BilingualAnswerText` | 一致 |
| en-light/en-dark | `*-en-*` | 英文批次 `live-en-light/dark.png`，`--screenshots` 英文扫描（G9/G10） | 一致 |

## 6. E7：屏幕与讲稿面板（InsightsPanel.swift，嵌入左栏）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 6.1 | `InsightsPanel`：`.card()`，VStack spacing 10；头部 `屏幕与讲稿` label 13 semibold + `立即识别` chip（读屏中禁用）+ mini ProgressView + 齿轮（跳设置） | `InsightsPanel.axaml`：card、`grid` 头部：icon + 屏幕与讲稿 13 semibold、立即识别 pill、读屏 ProgressBar 36×3、`IconGearshape` → 设置 | 一致 |
| 6.2 | `ScreenWatchOffHint`：长说明 + 「打开屏幕感知」prominent chip | 同文案逐字 + `打开屏幕感知` pill（`EnableScreenWatch_Click`） | 一致 |
| 6.3 | 屏幕关闭提示、错误 note（橙色） | `HasScreenError` → `ScreenErrorText`（Caution） | 一致 |
| 6.4 | `SlideSection`：accent 6% 圆角 10 底；页位置 `第 N / M 页` mono/semibold + 文档名 11 secondary + 页间箭头 + 重新生成/改用 AI 要点 | 同：`Sunken` 底 + `D4CornerRadius_10`、`SlidePositionText`（`第 %lld / %lld 页`）、`SlideDocumentName`、上一页/下一页、两个生成按钮 | 一致（底色用 Sunken 4%，Mac 6%，见未解决 10） |
| 6.5 | 讲稿要点：每条 done 绿勾 / next 箭头（黑，14 semibold）/ pending 圆圈（secondary），done 文字 secondary | `SlidePointRows`：`IconCheckmark` Ready 绿、`IconArrowRight` Ink、`IconCircle` Secondary；`point.next` 14 semibold、`point.done` secondary | 一致 |
| 6.6 | 过渡：「下一页：…」/「过渡：…」caption 12 secondary | `SlideTransitionText` | 一致 |
| 6.7 | 「听众可能会问（点一下提前准备回答）」+ 每问 questionmark.bubble + 点击即问 | 同文案 11 Medium + `IconQuestionmarkBubble` 按钮（`AskSlideQuestion_Click` → `AskSlideQuestionCommand`） | 一致 |
| 6.8 | 生成中：「正在根据这一页和你的资料写讲稿提示…」 | `!HasSlidePoints` 时显示同文案 | 一致 |
| 6.9 | `ScreenSection`：display 图标 +「屏幕：<kind title>」12 semibold +「已识别 · 是否用于回答见“本次依据”」+ 相对时间「N 秒前」mono；识别文字 3↔30 行 + 展开识别的文字/收起 | 同：`ScreenTitleText`（题目/代码/幻灯片/文档/聊天/视频通话/其他）、`已识别 · …`、`ScreenAgeText`（5 秒定时器刷新）、`ScreenText` MaxLines 3/30、展开/收起 | 一致 |
| 6.10 | `AnswerProgressStrip`：`MetricPill` 已讲 x/y + 「下一句：…」accent 12 medium | 同：hairline 药丸「已讲」+ `AnswerProgressCounterText`，右侧「下一句：」+ `NextAnswerPointText` | 一致 |
| 6.11 | 嵌入位置：`LiveSessionView` 左栏，`screenWatch && followsSlides` 时高 240 | `LiveInsightsPanel` 左栏第 1 行 Height=240，`LiveShowsInsightsPanel` = `InsightScreenWatch && SelectedMode==Academic`（Mac `followsSlides` 只有答辩/汇报=Academic） | 一致 |
| 6.12 | 面板随 `SlideStatus`/`CurrentScreen`/`AnswerText` 刷新 | `InsightsPanel.axaml.cs` 订阅 `SlideStatus`、`ScreenWatchStatus`、`ScreenContextText`、`ScreenWatchKind`、`AnswerText`、`Answering`、`Recording`、`CurrentSession` → `RefreshInsights()`；5 秒定时器刷新相对时间 | 一致 |

## 7. 自检与动作点检（`--demo --self-test=live-ui`）

1. 载入 Live 演示阶段：`Recording=true`、计时在走、状态「正在聆听」、场景「面试」、标题「字节跳动 · 后端二面」+ 系列名「字节后端二面」。
2. 顶栏：自动回答/只记录分段切换写回 `Settings.AutomaticAnswers`，右栏默认页签跟随（自动→问答，只记录→我的笔记/实时纪要）。
3. 左栏：说话人列只显示 对方 / 我（无时间戳）；演示转写的问题句出现「问题」胶囊；回声句为删除线 +「是我说的」，点击后 `IsEcho=false`、回声计数 -1、`currentLines` 同步去掉 `suspectedEcho`。
4. 候选条：`LiveCandidateText` 非空时显示 可能的问题 + 回答这句，点击先提为当前问题再立即回答。
5. 右栏三标签：实时纪要（Mac 状态文案 + 开启实时纪要/现在更新 + 空态文案，自检截 `live-notes-light.png`）、问答（建议回答头部 + 2/2 导航 + 问题原文 + 白色大圆角卡片 + 更简短/举例/英文版/⋯ + 依据行）、我的笔记（会中笔记编辑器）。
6. 更多菜单（回答设置）：模型、快速模式、联网三态（不联网/自动判断/每次都查）、回答长度、说明与跳设置；提词（提词岛一直展开、双屏演讲）；回答（回答语言三选、英文对照）；工具（实时纪要、屏幕感知、手机伴侣、同声传译、回答浮窗、屏幕与讲稿、说话人、补译、会中窗口、设置…）。
7. 输入条：空内容不可发送；输入文字可发送；截图预览出现后占位文案变为「对截图的补充要求（可留空）」，取消后恢复；立即回答显示 `Ctrl+Alt+Enter`。
8. 证据：演示回答显示「依据：资料 2 · 屏幕 · 联网 1 · 1 项限制」；详情列出资料引文、屏幕来源、联网来源、限制。
9. E7：`LiveShowsInsightsPanel` 与 Mac 规则一致（screenWatch && Academic）；面板头部、立即识别、齿轮、幻灯片区、屏幕区、回答进度全部在页内（非独立窗口）。

## 7.1 J1：实时纪要内容（Mac `LiveNotesColumn` + `LiveNotesEngine`，本轮接线）

| # | Mac 元素 | Windows | 结论 |
| --- | --- | --- | --- |
| 7.1.1 | 状态行：实时纪要没有开 / 正在更新… / 这次没更新成功，稍后会再试 / 还没有连接回答服务，写不了实时纪要 / 更新于 h:mm · 约每 45 秒一次；更新中转圈 | `LiveNotesStatusText` + `LiveNotesIsUpdating` 进度条 + 失败/无服务时 caution 色（`LiveNotesStatusIsWarning`） | 一致 |
| 7.1.2 | 右上 现在更新 Ghost（更新中或无回答服务时禁用；没开时是 开启实时纪要） | `LiveNotesButtonText` / `LiveNotesCanUpdate` / `EnableLiveNotes_Click` | 一致 |
| 7.1.3 | 整场在谈什么：`Ink.sunken` 圆角块里的 summary | 同：Sunken + `D4CornerRadius_10` + padding 16 的 summary 块 | 一致 |
| 7.1.4 | 议题 + 要点：last-update 新增的点用场景色 | `LiveNotesTopicRows`：标题 13 semibold，要点 15；`Fresh` 的点用 `LiveSceneTintBrush`，旧点 tertiary | 一致 |
| 7.1.5 | 定下来的：overline + ✓；要做的：overline + □ +「（负责人）」 | `定下来的`/`要做的` overline + ✓/○ 行；todo 文案后接「（我）」（`LiveNotesTodoRow.DisplayText`） | 一致（方框图标用 circle，图标表无 square） |
| 7.1.6 | 调度：45s；首 3 行；首更 20s；最近 2 个议题可改写；最多 12 个议题；失败保留原纪要 | `LiveNotesSchedule` 的常量与 `IsDue` 直接使用；`LiveNotesPrompt.User/Parse`；解析失败（`next is null`）不改 `notes`、状态 Failed | 一致 |
| 7.1.7 | 结束后仍整理完整纪要 | 结束走既有会后纪要（`generateSummary`），本引擎 `EndLiveNotes` 只停 ticker | 一致 |
| 7.1.8 | 演示数据（DemoMode installDemo） | `InstallDemoLiveNotes()`：与 Mac 演示逐字（summary/2 议题/决定/待办/fresh 两条）；`--self-test=live-ui` 安装后截 live-notes 浅/深，内容与 `mac-screens/live/live-notes-light.png` 一致 | 一致 |



## 8. 未解决差异（逐条，不能写成「逐条一致」）

1. 暂停/继续按钮只有文字，缺 Mac 的 pause.fill / play.fill 图标：随包 Fluent 图标集里没有这两个字形（D7 图标对照表未含），需要补图标资源后加。
2. 顶栏「自动回答/只记录」的 answerEngineReady 黄点未做（Windows 用更多菜单文字提示代替）。
3. 实时纪要引擎已接（J1）；演示数据下 `现在更新` 因没有回答服务而禁用（Mac 演示有服务），`更新于` 之外的真实增量更新需配置回答服务后实测（脚本化响应用 `LiveNotesPrompt`/`LiveNotesSchedule` 测试覆盖）。
4. 已修（§3.3 live 4，`c7fcad8a`）：原回答 ↔ 联网版切换已接 core-parity 的 web-search 状态层，`WebStatusLine` 显示状态行与「看原回答/看联网版」pill，不再是差别（见 3.6）。
5. 回答卡片阴影用共享 `PaperCardShadow`（contact 0 1 2 + ambient 0 8 14），Mac 是 contact 0 1 1 + ambient 0 10 20；§5.7 的 float/contact/ambient 资源落地后替换。
6. 立即回答按钮高度 36（`HitRow`）而 Mac 40（`Space.controlLarge`）；主题的命中 token 测试要求页面主按钮用 36，取 36 记录。
7. focus / selected-focus 状态没有单独截图：焦点环由设计系统 `Button:focus-visible` 统一提供，自检只断言存在；需要人工点按留证。
8. 滚动跟随的「文本选择固定」未接（只按滚动位置判定）。
9. 归档保存状态行（Mac `archiveSaveStatus`：保存中/保存受阻/未入队计数/确认缺失/重试）Windows 没有对应持久化状态，顶栏不显示。
10. 幻灯片区底色用 `Sunken`（ink 4%），Mac `Brand.accent.opacity(0.06)` 为 6%。
11. 回答设置菜单里模型仍用设计系统样式的 `ComboBox`（Mac 是 inline picker）；等 §6.1 `MenuPicker` 组件落地后替换。
12. 我的笔记标签沿用 E16 的编辑器，`MeetingUserNotes` 的 Mac 逐元素对照不在本轮。
13. 深色模式下回答正文偏暗（几乎与卡片同色）：`Views/MarkdownText.cs` 的 `ResourceLookup.ByKey` 用 `Application.Current.TryFindResource` 解析 `InkBrush`，取到浅色主题值，没随 `ActualThemeVariant` 切换（实测 `dark.png` 正文像素 (35,35,34)、卡片底 (26,26,24)、左栏普通文字 (247,247,247)）。这是 §6.1 `MarkdownText` 组件缺陷，已报 Lead/§6.1 owner 修；本页用 `views:MarkdownText BaseFontSize=18`，组件修好后即正确。
14. 本轮浅/深截图在独立 worktree（HEAD `7b7f8202` + 本任务文件）构建，因为共享工作区当时被提词岛任务的 WIP 文件卡住；截图里的标题栏/底部状态条是当时 HEAD 的 shell（§5.2/§5.4 由其他任务继续重做），不属于会中页元素。会中页本身来自本任务代码。
15. 演示数据与 Mac 截图的数据差异（非布局差异）：Mac 截图底部有一句未定稿「如果删除缓存失败了怎么办」（Windows demo 的 partial 为空、显示在候选条），Mac 状态行音源是「Zoom · 后端工程师二面」而 demo 没有会议软件源（显示「所选系统输出设备混音 · 麦克风 · 在设置中调整」），回声计数 demo 为 0（Mac 1）。partial 行/候选条渲染已实现（自检断言 `SystemPartial`/`MicrophonePartial` 绑定存在）。
16. J6 说话人：Mac 用 popover 编辑一个人，Windows 打开既有 `SpeakerEditorWindow`（E12 实现）；「我是哪个？」的虚线胶囊用普通胶囊（Avalonia Border 无 dashed 边），其余一致。演示数据没有 voice tag，所以截图里不出现说话人条（与 Mac live-light 截图相同），接线由结构测试与自检断言覆盖。

## 9. 截图与运行证据

- `light.png` / `dark.png`：`dotnet run --project src/NextThought.Desktop -c Release --no-build -- --demo --screenshots=<临时目录>`，1120×820、100% 缩放、演示数据；生成后拷贝到本目录（同 `en-light.png` / `en-dark.png` 英文批次）；`validation/screens/current/` 里另有同一批页面图（不入库）。
- `live-notes-light.png` / `live-notes-dark.png`：同一 demo 会话下把右栏切到实时纪要，由 `--self-test=live-ui` 在 `live-shots/` 截取后拷入。
- `--demo --self-test=live-ui`：**live-ui-check: passed**（2026-11-21 13:02，iso `NEXTTHOUGHT_DATA_DIR` 临时目录）；除原有 E4 项外新增 §6.5 断言：说话人列只有 对方/我、无时间戳；回声「是我说的」恢复且计数 -1；右栏三标签顺序与可选；实时纪要 Mac 状态/空态/按钮；`LiveShowsInsightsPanel` 规则；屏幕与讲稿面板嵌入左栏并截图 `insights-embedded-light.png`（E7 证据，拷入 `validation/screens/insights/`）；最后恢复 demo 状态。
- 测试：`Windows/tests/NextThought.Tests` 的 `LiveSessionStage4Tests`（§6.5 结构 7+ 项）、`LivePageParityTests`（E4 9 项）、`InsightsPanelParityTests`（E7 8 项）与 `ThemeResourceTests`/`SceneComponentTests`/`AnswerLanguageParityTests`/`SelfTestStructureTests` 的相应断言；见提交说明。

---

## §3.3 live 9（归档保存状态行）— 2026-11-21

**已修**（`F14:` `866161fc`、`672f22a6`）：`SessionStore.AppendRawAsync` + `IncrementMetaCountAsync`；`AppState.SaveStatus.cs` 接 `Core.SessionArchiveWriteQueue`，500 ms 定时逐条 flush，`Acknowledge`/`Fail` 与 `SaveStatus/SaveStatusText/SaveUnqueuedCount/CanAcknowledgeSaveLoss/RetryPersistence/AcknowledgeSaveLoss`；会议中转写/问题/回答/声纹四类写入改为 `EnqueueArchiveWrite`（JSONL 序列化 + `\n`），Stop/End/三种导出前 `FlushArchiveQueueAsync()`；LiveView 顶栏计时后显示「保存中」，受阻时警示文案 + 「重试保存」/「确认缺失并继续保存」。
证据：`LiveSaveStatusTests` 结构断言；`live-ui` 截图待 §5 统一重拍批次（本轮不单独出图）。