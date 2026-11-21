> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 会前准备页 · Mac ↔ Windows 逐元素对照（§6.3 / §6.7 / J2）

- Mac 基准：Mac 3.4.0 `Sources/NextThought/PrepareView.swift`（含 3.4.0 的「这场用什么」`FeaturePicker`、`FeatureReset`、`SeriesCarryCard`、`MaterialsBlock`、`CheckCard`、`PreparationStartBar`）、`LiveNotesEngine.swift:144-178`（`meetingFeatures` 每系列沿用）。
- Mac 参考截图：`../../mac-screens/prepare/prepare-light.png`、`prepare-dark.png`（1600×1171，窗口 1120×820，比例 1.4214）。
- Windows 截图：`prepare/prepare-light.png`、`prepare-dark.png`（1120×820，`--demo --screenshots`，数据目录隔离）。
- 结论见文末「结论」。

## 1. 窗口外壳（非本页所有，仅记录）

| 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 标题栏 | 交通灯 + 顶部胶囊导航「首页 / 所有记录 / 系列」+ 右上齿轮 | 自绘标题栏：同款胶囊导航（E1 已对齐）+ 右上齿轮 + Windows 最小化/最大化/关闭 | 导航与齿轮一致；窗口按钮属平台机制，外壳归 MainWindow owner |
| 全局状态条 | 无（只有页面自己的底部开始条） | 页面开始条下方还有 owner 的全局状态条（演示模式提示） | 外壳级差异，不属于 PrepareView |

## 2. 顶部水墨背景

| 元素 | Mac（Swift） | Windows | 结论 |
| --- | --- | --- | --- |
| InkWash | `InkWash(tint: mode.theme.tint, animated: false).frame(height: 300).opacity(0.7)` | `controls:InkWash Height=300 Opacity=0.7 Animated=False`，随 `SelectedMode` 换色 | 一致（组件本体归 D6） |

## 3. 标题与场景卡（`PrepareComposer` + `VenuePicker` + `TypePicker`）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 卡片 | `Ink.surface`、radius 22、contact 1/1 + ambient 20/10 两层阴影、hairline 0.5 描边；整页唯一抬起的卡 | `Surface` + `CornerRadius=22` + `PaperCardShadow` + `Hairline 0.5` | 一致 |
| 2 | 场景图标 `SceneGlyph` | 44×44，随场景图形/颜色 | `SceneGlyph Size=44 Mode={SelectedMode}` | 一致 |
| 3 | 标题输入 | `Typo.display` 30 serif medium、minHeight 44、无边框；占位「这场会议叫什么」 | Serif 30 Medium、`MinHeight=44`、透明无边框、`PlaceholderText`（内边距已归零以对齐行高） | 一致 |
| 4 | 线上/线下 | `InkSegmented`：sunken 轨道 radius 8 padding 2；选中项 `Ink.surfaceRaised` radius 6 + contact 阴影 + hairline、`Ink.ink` 文字；未选中 `Ink.secondary`；字号 13 medium、左右 14、minHeight `max(height,32)-4`=26 | 本地 `Border.venueContainer`（Sunken/radius 8/padding 2）+ `RadioButton.venue`（13 medium、padding 14,0、minHeight 26、radius 6；选中=SurfaceRaised+PaperCardShadow+Hairline 0.5，未选中=透明，悬停=Hover） | 一致 |
| 5 | 「这是什么会」行 | overline 11 semibold secondary + caption 12 tertiary「根据标题选好了，可以改」（仅猜类型时显示） | `overline` + `caption` + `Tertiary`，`IsVisible={DraftGuessNoteVisible}` | 一致 |
| 6 | 场景 chips | `SessionMode.builtIn` 顺序：面试、组会、汇报 / 答辩、小组作业、客户沟通、工作会议、讲座 / 课程、小组讨论、通用，再「+ 自定义…」；label 13 medium；高 30、左右 11、Capsule；选中=场景色 13% 底 + 场景色字；悬停=Hover；按下 scale 0.96 | `ItemsControl DraftSceneChips` + `Button.sceneChip`：高 30、padding 11、radius 15、13 medium；选中=`TintAt13` 底 + `Tint` 字；悬停=Hover；「+ 自定义…」同规格（选中时同色） | 一致（按下 0.96 未做，见差异 1） |
| 7 | 自定义类型行 |「类型名称，例如：导师一对一」+「基于」+ 场景 Picker，仅自定义模式显示 | 同款 TextBox +「基于」+ ComboBox，`IsVisible={DraftIsCustom}`；沿用 `applyingDraft` 防重入 | 一致 |
| 8 | 场景承诺 | `theme.promise`，17 serif medium、`Ink.ink` | Serif 17 Medium | 一致 |
| 9 | 一句说明 | `summary(draft)` caption secondary 可换行：「面试：会中自动回答 · 回答简洁 · 纪要：… · 建议放：…」 | `DraftSummary` caption secondary 自动换行，文案逐字一致 | 一致 |
| 10 | 三条说明 | 场景色 4pt 圆点 + caption，横向 spacing 16 | `DraftMomentRows`：场景 tint 4pt + caption，横向 spacing 16 | 一致 |
| 11 | 卡片内边距 | 标题行 horizontal/top/bottom 24；类型区左右 24、下 24 | 标题 Grid margin 24；类型区 margin 24,0,24,24 | 一致 |
| 12 | 选中/悬停过渡 | chips 选中用 `matchedGeometryEffect` 滑动，悬停 0.14s | chips 选中即时换底/字，悬停 0.14s（主题级） | 选中滑动动画未做，见差异 1 |

## 4. 「这场用什么」（3.4.0 / J2）

| # | 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- | --- |
| 1 | 区块头 | overline「这场用什么」+ caption tertiary「不用的可以关掉」；右侧 `FeatureReset`（仅当开关 ≠ 场景默认）「恢复这个场景的默认」 | `prepareSectionTitle`「这场用什么」+ caption tertiary「不用的可以关掉」+ quiet「恢复这个场景的默认」（`IsVisible={!MeetingFeaturesAreDefault}`，`ResetMeetingFeaturesCommand`） | 一致 |
| 2 | 分隔线 | hairline 0.5，头部下 8 | `Border Height=0.5 Hairline Margin=0,8,0,0` | 一致 |
| 3 | 行 1 实时转写 | `waveform` 14 secondary 宽 24；标题 13 medium；说明 12 secondary「把双方说的话写成文字。回答、纪要和搜索都靠它，一直开着。」；右侧 caption tertiary「始终开启」，无开关 | Fluent `IconWaveform` 14 secondary 宽 24；13 medium；同句；caption tertiary「始终开启」，不写值 | 一致（图标按 D7 允许 Fluent 映射） |
| 4 | 行 2 自动回答 | `sparkles`；「听到对方提问，参考回答出现在摄像头下方的提词岛和右侧。」；小开关读 `meetingFeatures.answers`，无引擎时禁用并换成「需要先连接回答服务（开始前检查里可以连接）。」 | `IconSparkles`、同文案（`MeetingFeatureAnswersDetail`）、`InkSwitch` 34×20 绑 `MeetingFeatureAnswers`，`IsEnabled={MeetingFeatureAnswersEnabled}`；演示模式按 Mac 截图显示引擎已就绪 | 一致 |
| 5 | 行 3 实时纪要 | `list.bullet.rectangle`；「边开边整理议题、决定和待办，大约每 45 秒更新一次；结束后仍会整理完整纪要。」；开关 `liveNotes` | `IconListBullet`；同文案；`MeetingFeatureLiveNotes` | 一致 |
| 6 | 行 4 同声传译 | `character.bubble`；「听到对方说英文时自动出现：每句话下面和提词岛上给出中文。在本机翻译，不联网。」；开关 `translation` | `IconBubbleLeftAndBubbleRight`；同文案；`MeetingFeatureTranslation` | 一致 |
| 7 | 行 5 区分说话人 | `person.2.wave.2`；「按声音分出每个人，可以起名字；纪要里按人来写。认错了随时能改。」；`enabled = modelAvailable && speakers.enabled` | `IconPerson2`；同文案；`IsEnabled={MeetingFeatureSpeakersEnabled}`（Windows 声纹模型需下载；演示模式按 Mac 截图视为可用） | 一致（真机未装模型时禁用，见差异 2） |
| 8 | 行间距/分隔 | 行 `padding(.vertical, 12)`；行间 hairline 0.5、leading 36（区块左边 8+36=44） | 行 `Padding=8,12`；hairline `Margin=44,0,0,0` | 一致 |
| 9 | 开关样式 | 系统 switch small：开=Ink 底白钮；关=tertiary 45% 底 | `InkSwitch`：开=`InkBrush` 底 `OnInk` 钮；关=`Tertiary` 0.45 底 `Surface` 钮 | 一致 |
| 10 | 记忆语义 | 键 `meetingFeatures.<seriesID>` 存 UserDefaults；同一系列下一场沿用；`resetMeetingFeatures` 删键回默认；不进备份 | Core `MeetingFeatureChoices`（`07489234`）sidecar `meeting-features.json`，同键名/同四 Bool/同 `ForSeries` 默认；不写 Workspace、不进 .ntbackup；Reset=Remove | 一致（平台存储机制不同） |
| 11 | 开始时生效 | `startPreparedMeeting`：`automaticAnswers = answerEngineReady && features.answers`、`liveNotes.begin(enabled:)`、`liveTranslation.beginMeeting(allowed:)`、`speakers.beginMeeting(wanted:)` | `BeginPreparedMeeting` 取 `DraftMeetingFeatures` → `ApplyActiveMeetingFeatures()`：`Settings.AutomaticAnswers = AnswerEngineReady && ActiveMeetingFeatures.Answers`、`LiveNotesEnabled`、关掉同传时 `LiveTranslationEnabled=false`；声纹门控 `settings.SeparateSpeakers && ActiveMeetingFeatures.Speakers` | 一致（`--self-test=prepare` 断言四项关掉后开始会时不运行） |
| 12 | 会中仍可开关 | 「⋯」菜单：自动回答/只记录、实时纪要、同声传译 | 会中顶栏「自动回答/只记录」+「⋯」内「实时纪要」「同声传译」（E4/E11 接线） | 一致 |

## 5. 「接着上一场」（`SeriesCarryCard` + `SeriesBriefingPanel`，首屏下方）

| 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 区块头 | overline「接着上一场」 | 同（`DraftContinuesSeries` 时显示） | 一致 |
| 系列行 | 场景色 7pt 圆点 +「这是「系列名」的第 N 场」body +「上一场：标题 · 日期」（无上一场「这个系列还没有开过会」）+ 右侧 ghost「不是同一个系列」 | `DraftSceneTintBrush` 7pt + `DraftSeriesHeaderTitle` / `DraftSeriesPreviousLine` +「不是同一个系列」→`DeclineDraftSeriesCommand` | 一致 |
| 系列简报 | 标题 +「本机 · 已保存内容」；最近会议（共 N 场）+ 行；已确认的经验；已确认的答法；知识关闭/待确认提示 | 同结构（`SeriesBriefing`）：`DraftBriefingRecentLine`、`DraftBriefingRecentSessions`、`DraftBriefingLessons`、`DraftBriefingQuestionBank`、`DraftBriefingKnowledgeOff`、`DraftBriefingPendingLine` | 一致（「需要重新确认/变化记录/来源」子块未做，见差异 3） |
| 携带行 | WideToggleRow：资料（名称…/份数）、复盘 · 改进后的答法（题数）、经验（引用/条数）、上一场完整转写（分钟）；空系列一行说明 | `WideToggleRow`×4 绑 `CarryMaterials/CarryReview/CarryLessons/CarryTranscript`，文案/数量同；空说明同 | 一致 |
| 折叠动效 | `Motion.expand` + offset -6 过渡 | `IsVisible` 即时切换 | 差异 4 |

## 6. 「这次新加的资料」（`MaterialsActions` + `MaterialsBlock`）

| 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 区块头 | overline「这次新加的资料」+ caption tertiary「可跳过」+ 右侧「从以前的会议带入…」（有其它系列时）+「＋ 添加」 | 同：标题/说明/「从以前的会议带入…」（Flyout 列 `OtherSeriesRows`）+「＋ 添加」→`ImportPreparedDocumentsCommand` | 一致 |
| 空拖放区 | tray 18 light +「把文件拖到这里，或点右上角“添加”」+「建议放：<场景提示>」；虚线 hairline radius 14，拖入 Hover 底 | 同图标/文案 + `PrepareDropHint`；`Sunken` 底 radius 14；整页 `AllowDrop` + `TryGetFilesAsync`（沿用 E2） | 一致（拖入高亮未做，见差异 5） |
| 文件行 | 格式块 32×32 + 文件名 + 进度/阶段/警告/取消/重试 | 文件名 + 段数/警告（失败 Caution）；简化只读行，无逐文件进度/取消 | 差异 6 |
| 讨论准备 · AI 可选 | sparkle + overline；说明/运行中/结果/错误/重试/取消；未配置显示「设置 AI 服务（可选）」 | `AiSoft` 块：`PrepareInsightHeading/Hint/Status/Text/Error` + `PrepareInsightActionCommand` / `CancelMaterialsInsightCommand` + `PrepareInsightActionHint` | 一致（真实模型往返仅状态机验证，沿用 E2 限制） |

## 7. 「开始前检查」（`CheckCard`）

| 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 行结构 | `StatusDot` + label 13 secondary 宽 56 + 内容；minHeight 48；hairline 分隔 | `StatusDot` + `prepareRowTitle` 宽 56 secondary + 内容；MinHeight 48；hairline | 一致 |
| 声音 | 音源菜单（线上）/「麦克风 · 我/现场/未开启」+ 动作；问题/下载进度/模型状态；只记录对方/重新打开麦克风 | `AudioSourceText` + `PrepareMicLine` + 只记录对方/重新打开麦克风/下载/声音测试 + 电平条 + `PrepareAudioProblem` + `ModelStatusText`；`PrepareAudioTone` | 一致（无「只听某个窗口」音源菜单，沿用 E2 限制） |
| AI 可选 | 引擎行 + 更换/去连接 + 检查结果 | `PrepareAnswerEngineLine` + `TestAnswerEngineCommand` + 打开设置 + `PrepareEngineVerification`；`PrepareAiTone` | 一致 |
| 手机 | companionEnabled 时一行 | `PrepareShowPhoneCheck` 时一行（`PreparePhoneLine`/`PreparePhoneTone`） | 一致 |

## 8. 底部开始条（`PreparationStartBar`）

| 元素 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 位置/底 | 贴窗口底、`Ink.paper`、顶部 hairline 0.5、左右 32 上下 12 | 同：Grid 第二行、`Paper` 底、`BorderThickness=0,0.5,0,0`、Padding=32,12 | 一致（下方另有全局状态条，见 §1） |
| 状态 | 6pt `StatusDot` + caption ink：「声音」「AI 可选」（companion 时「手机」） | `StatusDot DotSize=6` + 声音（`PrepareAudioTone`）/AI 可选（`PrepareAiTone`） | 一致 |
| 说明行 | `recordingReadinessMessage` +「  ·  」+ 资料句 | `PrepareStartDetail`：「可以开始记录 · 本场使用 4 份已就绪资料；文字可用即可开始。」（演示数据与截图逐字一致） | 一致 |
| 取消 | ghost「取消」 | quiet「取消」→`CancelDraftCommand` | 一致 |
| 主按钮 | 黑底白字 radius 9、高 40；红点 7pt +「开始」 | `Button.primary` Height 40、Padding 18,0、CornerRadius 9；`Seal` 7pt + `PrepareStartLabel`（开始/准备声音）；`StartPreparedMeetingCommand` | 一致 |

## 9. 动效与交互

| 项目 | Mac | Windows | 结论 |
| --- | --- | --- | --- |
| 进场 | `appear()`：opacity 0→1 + y 8→0，`Motion.phase` 依次 delay 0.05s | 无进场动画 | 差异 7 |
| 区块展开 | `Motion.expand` 0.32s | `IsVisible` 即时 | 差异 4 |
| chips 选中 | matchedGeometry 滑动 + hover/pressed | 即时变色；hover 0.14s | 差异 1 |

## 10. 交互走查（演示数据）

1. 首页「开始」进入准备页：draft 带出标题「字节跳动 · 后端二面」、场景「面试」、线上；chips「面试」选中（场景色底/字）。
2. 点「组会」：图标/承诺/三条说明换成组会；「根据标题选好了，可以改」消失；「恢复这个场景的默认」出现。
3. 「这场用什么」关掉「自动回答」「实时纪要」「同声传译」：开关变关；点「恢复这个场景的默认」：四个开关回场景默认（自动回答开、实时纪要关、同声传译开、区分说话人开），按钮消失。
4. 同系列重开：选择写进 `meeting-features.json` 的 `meetingFeatures.<series id>`；换系列再回来选择仍在（`--self-test=prepare` 断言）。
5. 关掉四项后开始：`ApplyActiveMeetingFeatures` 断言 `Settings.AutomaticAnswers=false`、`LiveNotesEnabled=false`、`LiveTranslationEnabled=false`、`ActiveMeetingFeatures.Speakers=false`（功能不运行）。
6. 「取消」回首页并丢弃 draft；「开始」进入会中（演示模式不写数据）。
7. 向下滚动可见「接着上一场」「这次新加的资料」「开始前检查」（1120×820 首屏外，与 Mac 截图一致）。

## 结论

**以下几条不一致**（其余逐条一致）：

1. **chips 选中滑动动画**：Mac `matchedGeometryEffect` 滑动；Windows 即时换色（最终视觉一致，过程不同）。
2. **区分说话人默认可用性**：Mac 随包声纹模型恒可用（默认开）；Windows 模型需下载，真机未装时该行禁用；演示模式按 Mac 截图呈现可用/开。
3. **系列简报深层内容**：「需要重新确认 / 变化记录 / 来源」子块与来源跳转未移植（沿用 E2 限制）。
4. **展开折叠动效**：Mac `Motion.expand` 0.32s 与 offset 过渡；Windows 即时显隐。
5. **拖入文件高亮态**：Mac Hover 底 + secondary 虚线；Windows 只体现导入结果（拖放可用）。
6. **资料文件行/AI 洞察细节**：Mac 每文件有进度/取消/重试/格式块；Windows 为简化只读行；真实服务往返仍只验证状态机（沿用 E2 限制）。
7. **进场动画**：Mac 分段 `appear()`；Windows 无进场动画。
8. **字体度量导致的纵向偏移**：Windows 中文字体行高比 Mac SF/New York 高约 3–6%，同结构下内容整体下移约 10–19pt（卡片高约 303pt vs Mac 288pt）；字号/字重/顺序/分组/间距 token 均按 Mac 值。
9. **平台外壳**：窗口按钮、下方全局状态条、无 macOS 屏幕录制权限（系统音频直接可采），见 §1 与平台说明，归 MainWindow/其他 owner。
