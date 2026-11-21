NextThought Windows 修复任务清单 

# NextThought Windows 修复任务清单 

Nov 20, 2026 · @Homing 

这份清单把两轮审查和最新代码的复核结果整理成可执⾏的任务,⽬标是让 Windows 版的功 能、⾏为和界⾯与 Mac v3.4.0 (主分⽀ `f015e8c6` )⼀致。每个任务都写明了改哪⾥、照 Mac 的哪个⽂件改、怎么算完成。把第 3 节的提⽰词和这份清单⼀起交给 Agent 即可开⼯。 


## 执行状态（windows/parity，2026-11-21；对照基准 Mac v3.4.0 `f015e8c6`）

> 状态规则按 `C:\Users\Thunderobot\Downloads\WINDOWS_FINAL_DECISIONS.md` §1 更新：只剩「接受」/「第二批」差别的任务可标「已实现、未验证」；「已验证」需本机实测记录。对照文档结论见 `ACCEPTED_DIFFERENCES.md`、`KNOWN_GAPS.md`。
| 任务 | 状态 | 证据 | 未解决 |
|---|---|---|---|
| 0-1 | 已实现、未验证 | 分支 `windows/parity`、本文件 | 待逐元素对照或实机验证 |
| 0-2 | 已实现、未验证 | 本机循环台账：`PORTING_STATUS.md` 顶部逐轮记录；当前 Core 872/872、NextThought.Tests 257/257 | 需 Mac 侧数据/截图/实机项按 OPEN_QUESTIONS 补 |
| 0-3 | 已实现、未验证 | `Windows/validation/bundles/1.3.7/`，无二进制 | 待逐元素对照或实机验证 |
| 0-4 | 已实现、未验证 | `FEATURE_PARITY.md`（v3.4.0）、`CORE_PARITY.md`、`VIEW_PARITY.md`、`SETTINGS_PARITY.md` 已按 v3.4.0 重建；J9 定稿 604 项 Core 测试 | 阶段 4/5 完成后复核 |
| 0-5 | 已实现、未验证 | `VIEW_PARITY.md` 按 Mac v3.4.0 重新生成：**281** 行；生成器 `Windows/scripts/scan_views.py`；当前 150 已实现、131 未做 | 阶段 4 逐页对照清零「未做」 |
| 0-6 | 已实现、未验证 | `CORE_PARITY.md` 已加三列、九个「部分」和复算命令；清单 41 与脚本复算 40 的差异已注明 | 待逐元素对照或实机验证 |
| 0-7 | 已实现、未验证 | `REVIEW_RESPONSE_1370.md` 更正节；`README.md` 首行 | 待逐元素对照或实机验证 |
| 0-8 | 已实现、未验证 | 根目录 9 份跟踪文件 + 允许的 5 份；旧文档在 `Windows/archive/` | 待逐元素对照或实机验证 |
| 0-9 | 已实现、未验证 | `Windows/UPSTREAM` | 待逐元素对照或实机验证 |
| A1 | 已实现、未验证 | 归档日期统一为 UTC 秒 + Z；ArchiveDateTests | 待逐元素对照或实机验证 |
| A2 | 已实现、未验证 | editedSummary 未知字段保留；字段级测试 | 待逐元素对照或实机验证 |
| A3 | 已实现、未验证 | Mac translations 按 id 读取/显示；写出 inputRevision | 待逐元素对照或实机验证 |
| A4 | 已实现、未验证 | 系列条目历史快照补齐 17 个 Mac 字段 | 待逐元素对照或实机验证 |
| A5 | 已实现、未验证 | summary 完整、notes.json/structuredAll 读写；有字段级限制 | 待逐元素对照或实机验证 |
| A6 | 已实现、未验证 | UUID 大小写往返测试通过（MacFixtureRoundTripTests，Core 1795/1795 内） | 「已验证」需在本机再走一次真实备份导入导出并保留记录 |
| A7 | 已实现、未验证 | `SessionMeta.workspaceID/workspaceName`；`WorkspaceStore.Load` 从 meta 建系列成员并从旧 `sessionIds`/`sessionIDs` 首次迁移写回 meta；`Save` 为每个成员写 meta；系列文件不再写 `sessionIds`；新建会议把选中系列写入 meta。新增 `WorkspaceMembershipTests` 两项 | 需导入真实 Mac 备份确认每场会议回到原系列；旧数据迁移需真机数据 |
| A8 | 已实现、未验证 | `ReadMeta` 记录原始键集合，重写时缺失的 `title/platform/inPerson` 不再补默认值；`AppendQuestionAsync` 同步 `questionCount`；纪要状态统一用 `ready`（最近会议行、模板整理、演示数据）。新增 `SessionToleranceTests` 一项验证 Mac meta 重写不新增键、提问数正确 | 需用一份真实 Mac `meta.json` 再保存核对键集合；Windows 新建会议仍会写 `platform`（Mac 会议重写不写） |
| A9 | 已实现、未验证 | `ReadLines` 跳过任意损坏行并计数，`SessionData.SkippedLines` 在界面提示；答案行损坏时去掉 `evidence` 再解一次；说话人上限从 16 改为 32；转写/译文/答案/语音/旧译文行都计入。新增损坏中间行与旧 evidence 两项测试 | 需用真实 Mac 会议文件和损坏样本实测；证据逐字段对照仍待夹具 |
| A10 | 已实现、未验证 | `SkillStore.PreserveIdentity`：文本编辑后按名称保留原 `Id/ImportedAt/Global/Enabled`；`NotesTemplateStore.PreserveIdentity`：按 Id/名称保留模板 `Id/Base` 与各段 `Id/Kind/IsGap/Carry/CarryPrefix`，只更新段名/说明 | 需在技能与模板文本框实测编辑后系列引用仍有效、其余字段不变 |
| A11 | 已实现、未验证 | 修订号逐行一致（MacRevisionParityTests 4/4） | 「已验证」需本机实测导入一场 Mac 会议后纪要不被判「过期」并留记录 |
| A12 | 已实现、未验证 | `MacSettingsPlist.Write` 按 `SettingsSnapshot.ExportedKeys` 全量写出（67+ 键，bool/real/integer/array/dict 分型），带 XML 声明与 DOCTYPE，`webSearchMode` 用 `off`/`auto`；导出过滤 `structured-*.md/json`、`corrections.json`、`summary-job.json`、`*.bak`；plist 键集合与导出排除两项测试。**2026-11-21 真实 Mac settings.plist 夹具核对（0077cb61）**：修复 `<data>` apiPresets 解码/编码、已知未映射键保留原值、未知 `<data>` 用 `{"$data":…}` 哨兵（G6）、导入导出过滤密钥；逐键往返一致（activeWorkspaceID 保留、apiPresets JSON 一致）；MacSettingsPlist 过滤 7/7、相关 41/41；证据 validation/a12/compare.md | `webSearchMode` 的 `always` 导出仍归一成 `auto` 且未写 AppSettings.WebSearchMode（需改 MacPreferenceMap）；Windows 导出键集合比 Mac「仅写存在键」多；Windows plist 导回真实 Mac 打开仍需人工/A16 复核 |
| A13 | 已实现、未验证 | 导入设置按备份键覆盖、缺键保留现值；术语表/技能/模板按 canonical/id/name 合并；四样写入前记录原文件，失败时一起回滚；支持带凭据备份（不导入密钥，保留在 Migration/）；`Originals/` 校验后恢复；导入资料用系列文件里的真实名称；导入大小/数量上限改为 Mac 的 100000 文件/64 GB；导入成功后界面重载设置、技能、术语、模板。新增 3 项测试 | 需人为制造第四步失败验证四样回滚；需 Mac 备份真实样本；界面重载需实机确认 |
| A14 | 已实现、未验证 | `IncrementalBackupStore.Prune` 现在从读不出的清单原文里提取 64 位哈希并加入保护集，删除对象前先合并，损坏清单引用的对象不再被删；新增 `PruneKeepsObjectsReferencedByAnUnreadableManifest`；`BackupArchive` 的 `settings.json` 仍走 `SettingsSnapshot.Filter`（与 A12 的 exportedKeys 同一列表） | 需与 A12 夹具一起验证 settings.json 与 Mac 备份内容一致；增量备份/恢复点尚未接到自动备份界面（E 组） |
| A15 | 已实现、未验证 | Mac 夹具校验 40/40 通过（verify.ps1 的 A15 步） |MacFixtureCodecTests|MacRevisionParityTests|MacFixtureRevisionTests`（当前 40 项全绿）；夹具 README 与 MAC_FIXTURE_REQUEST 更新为测试命令 | 五类非纯数据家族（CryptoKit/SQLite/Darwin/CoreGraphics/Sherpa）按 README 原因跳过；「已验证」需人确认 |
| A16 | 已实现、未验证 | win-export .ntbackup 已提交并与原备份逐文件比较 0 差异（SHA-256 6e8d961f…，31,869 B）；Mac 侧导入留给人 | Mac 侧导入这个 Windows 导出仍需人做；manifest createdAt/版本属新导出元数据；settings.plist 多写 75 个默认键与 3 个 Windows 扩展文件属设计扩展（Mac 忽略） |
| B1 | 已实现、未验证 | Workspace 读取失败保留、原子写；回归测试 | 待逐元素对照或实机验证 |
| B2 | 已实现、未验证 | Glossary 导出/解析按 Mac；往返测试 | 待逐元素对照或实机验证 |
| B3 | 已实现、未验证 | `WorkspaceSourceIntegrity.RemoveMeetings` 完整移植：经验/历史/题库/系列条目/知识候选/变更候选/墓碑；开会删除接入该路径；翻译 Swift 删除测试 | 未实机删除真实会议；B4 备份合并、B6 证据构建仍待做 |
| B4 | 已实现、未验证 | `BackupMerge` 增加 `mergeLessons/mergeQuestionBank/mergeSeriesItems/mergeSeriesCandidates/mergeCandidates` 与系列条目墓碑；三个 Swift 合并测试已翻译通过 | 其余备份合并 Swift 测试未逐项翻译；B22 时补齐 |
| B5 | 已实现、未验证 | `SeriesBriefingBuilder.HasCurrentEvidence` 增加 `origin == textNotes`、`lineID != null` 与证据修订比对；翻译两个证据测试 | 其余简报 Swift 测试未逐项翻译；B22 补齐 |
| B6 | 已实现、未验证 | 新增 `SessionEvidenceBuilder.Build`：文本笔记引用、引文/时间匹配、原/修订版本、claimRevision、review 当前/过期、三种结构化视图引用；翻译两个 Swift 证据测试 | 会议页尚未改为使用该构建器（需 B9 文本笔记模型）；其余 Swift 证据测试未逐项翻译 |
| B7 | 已实现、未验证 | `SessionProjectionBuilder` 移植：transcriptSourceRevision、原文/修订/译文/问题/答案/笔记/纪要检索项、speaker label 参与来源指纹、译文/笔记/纪要过期状态；翻译三个投影测试 | Markdown 导出的过期提示依赖 DerivedContentStatus，未逐字核对 Mac 文案；其余投影 Swift 测试留给 B22 |
| B8 | 已实现、未验证 | `NotesPrompt.OrganizeSystem` 系统提示词已从 `NotesTemplates.swift:582-623` 逐字移植（会议日期、8 条规则、章节形状、JSON schema）；`OrganizeUser` 增加 answers，新增 `PartialUser`；翻译两个提示词测试 | 会议页生成时尚未传入会中参考回答；其余模板 Swift 测试留给 B22 |
| B9 | 已实现、未验证 | 模板纪要不再截断 12000 字：`NotesPrompt.Chunks` 按行切 60,000 字、分段 JSON 请求、`MergeUser` 合并；翻译两个分段测试 | 未实机跑两小时会议；未分段校对（proofread）流程；其余笔记 Swift 测试留给 B22 |
| B10 | 已实现、未验证 | `NotesShape.tidy` 只对要点段限 14/6 条并按 topic 分组；JSON 解码接受字符串布尔值、数字时间/字段；Markdown 改为 # 标题 + 事实行、## 章节、待改进/答得好/待确认标记、问题回答与更好答法；旧冲突测试已替换 | 未实机核对英文页；`StructuredNotesMarkdown` 的三种结构化视图仍用旧渲染；其余笔记 Swift 测试留给 B22 |
| B11 | 已实现、未验证 | 补齐纪要模型：`ActiveCorrections/ActionItemCount/QuestionCount/ImproveCount/BankEntries/Lessons(template)/Named`、`NotesTemplate.CustomCopy`；`NotesKnowledge.Extract` 改用这些方法；模板纪要传入会中参考回答；翻译 `reviewFeedsTheQuestionBank` | 未逐项核对 `named` 与 Mac 在真实姓名上的每个边界；其余纪 Swift 测试留给 B22 |
| B12 | 已实现、未验证 | `NotesDates.Resolve` 增加时区参数，默认按本地日历日计算；全角数字/冒号先归一化，数字解析不再抛异常；翻译 `relativeDatesBecomeDates` 并增加东八区 07:00/UTC 25 日 23:00 的「明天」测试 | 未在非东八区机器实机核对；其余日期 Swift 测试留给 B22 |
| B13 | 已实现、未验证 | 旧记录过时：B22 提交 7cf5f44/07c67fc 已把 `SessionRecordTests` 全部 9 项翻译进 `Windows/tests/NextThought.Core.Tests/SessionRecordTestsParityTests.cs`（JSONLines 撕尾行、录制恢复、deferred notes、standing 系列、设置快照、Markdown 导出），所需存储 API（JSONLines/RecoverInterruptedRecording/NotesDeferred/WorkspaceSeries.Standing/SettingsSnapshot.ExportedKeys/SessionMarkdownExporter）均已存在；逐项对照 Swift 输入/期望一致，过滤运行 SessionRecord/SessionSummaryPrompt 29/29 通过 | 无；待随全量测试确认 |
| B14 | 已实现、未验证 | `strictKey` 改为小写保留标点/单空格；`looseKey` 按 NFKC、空白/格式字符/标点规则；`tidy` 合并空白；热词 80 条、误听优先、2–40 字、排除冒号/逗号、仅规范写法；新增 `HotwordString`；拉丁扩展字符算词字符；提示省略标记改为「（另有 N 条未列出）」；4 个对齐测试通过 | `GlossaryTests` 其余 14 项尚未翻译；未实机用真实术语表核对热词 |
| B15 | 已实现、未验证 | 小组作业/组会阈值统一 0.55/0.3；候选跟踪器新增 `Config`、`Reset`、`NoteAnswered`、`Confirm`，稳定/提前触发/线索/片段/去重/最小间隔均由 Config 控制；4 个对齐测试通过 | `QuestionIntentTests` 8 项、`QuestionCorpusReportTests` 5 项、`RealtimeRegressionTests` 9 项尚未全部翻译；可空句子键尚未加入 |
| B16 | 已实现、未验证 | 新增投影字幕导出：用校对后文本、说话人名字、原文/译文/双语三种模式、无译文回退原文、译文过期标记与计数；SRT/VTT 时间戳；2 个对齐测试通过 | `SessionExportTests` 其余 10 项尚未翻译；会议页导出按钮尚未改接投影字幕导出 |
| B17 | 已实现、未验证 | 新增 `KnowledgeMerger.MatchesCurrentSummary`（来源会议、摘要/实时两个修订号、重新生成候选 id 必须命中）；`CollectCandidates` 增加 `originId`；来源清单时间改用 Unix 秒 double 位模式，保留亚毫秒精度；3 个对齐测试通过 | `WorkspaceTests` 其余 39 项尚未全部翻译；会议页调用尚未改用 `MatchesCurrentSummary` |
| B18 | 已实现、未验证 | 系列新增 `EffectiveMode`（custom 归一到基础类型、嵌套 custom→general）、`Profile`、无参 `MaterialText()`（结构化字段+遗留字段+自由笔记）、`RetrievalDocuments`（题库作为一篇可检索文档）、`ModeProfile.MatchesDefaultTemplatePreferences`；4 个对齐测试通过 | Mac 旧版本对 meeting/client/general 的历史默认偏好字符串未逐字补入；`WorkspaceTests` 其余项留给 B22 |
| B19 | 已实现、未验证 | 讲稿提示词的相关资料改为 `ContextExcerpt.Budget(4000, 5)`（只取完整片段），引用改为 Mac 的 `[来源 定位]`（无 locator 用「第 N 段」）；新增 2 个对齐测试 | `LiveInsightsTests` 其余 11 项尚未翻译；未实机核对真实幻灯片导出 |
| B20 | 已实现、未验证 | 文档任务队列：重开时未完成阶段回到 Queued 并保留进度，失败/取消状态与消息保留；删除记录墓碑、迟到保存不能复活、可恢复；损坏 JSON 拒绝覆盖；容量只淘汰已结束任务，不再静默丢未完成任务；4 个测试通过 | 尚未移植 SQLite 行读取器异常测试与工作区删除日志的完整事务；未在正式导入流程接入恢复调用 |
| B21 | 已实现、未验证 | 追问判断按 Mac 去掉首尾所有标点；摘要去重只去标点、保留符号；说话人名按字素簇截断 24；表意文字范围补 CJK 扩展 A/兼容区（连字假名/谚文已有）；4 个边界测试通过 | `QuestionDetectorTests`、`TranscriptDeduplicatorTests`(3)、`SpeakerClusteringTests`(20) 尚未全部逐条翻译 |
| B22 | 已实现、未验证 | `Windows/CORE_PARITY.md` 592/592 项已翻译；Core 测试 1514 通过、0 失败、0 跳过；各测试文件与 Swift 同名逐项对应 | 无跳过；B22 的「已验证」需要一次对 Mac 原始测试数字的独立复算或实机确认，暂按未验证 |
| B23 | 已实现、未验证 | B22 全量对齐时发现两处旧 C# 测试期望与 Swift 矛盾并按 Swift 改正：`WorkspaceSeriesChangeTests.ACandidateCanOnlyBeReviewedOnce`（Swift 二次复核返回原回执，不是 null）、`KnowledgeMergerTests.ShortLessonsAndAnswersLessQuestionsAreDropped`（知识候选缺失与空列表区分）；Core 1514 项全部通过 | 尚未用独立脚本逐条比对全部旧测试与 Swift 行号，`已验证` 还缺一次人工复核记录 |
| B24 | 已实现、未验证 | 测试不依赖时区；UTC 全量 1795/1795 通过 | 其它界面日期显示仍直接 `ToLocalTime()`（当前 UTC 全量无失败）；「已验证」需实机记录 |
| C1 | 未做 | `CueIslandWindow.axaml.cs`：`ShowActivated=false`、`Focusable=false`，窗口打开后加 `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`；提供 `IsNonActivating` 供自检读取 | 需在记事本打字时点岛上按钮，确认焦点/输入不中断（实机记录未做） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C2 | 已实现、未验证 | `CueIslandWindow.axaml.cs`：80 ms `DispatcherTimer` 读取光标与窗口矩形；岛收起时加 `WS_EX_TRANSPARENT`，进入区域后去掉；`ClickThrough` 供自检读回 | 需实机确认收起时能点到下方浏览器标签页 |
| C3 | 已实现、未验证 | `CueIslandWindow.axaml.cs`：指针停留 0.22 秒展开，离开 0.2 秒收起；展开后恢复回答正文/详情 | 需实机确认 0.22/0.2 秒节奏与动画 |
| C4 | 已实现、未验证 | `Settings.CueAlwaysOpen`（键 `cueAlwaysOpen`）与设置页「会中一直展开提词岛，不用把鼠标移上去」；窗口打开/录音时按该值保持展开并禁用穿透 | 需实机勾选后确认岛不再自动收起 |
| C5 | 未做 | `CueIslandWindow.UpdatePhase`：按录音/回答/听到/问题/空闲判为 `Launcher / Cue / Hearing / Listening / Compact`，各阶段 360/440/480/选中宽度/260 宽，显示问题、听到原文、「正在听…」、收起提示或启动器文案；状态切换走动画 | 需与 `island-cue.png`、`island-launcher.png` 并排对照并补细节 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C6 | 未做 | `CueIslandWindow` 展开时显示按钮条：上一条/下一条、重新生成、改短、复制、取消，分别绑定 `PreviousAnswerCommand`、`NextAnswerCommand`、`RegenerateAnswerCommand`、`ShorterAnswerCommand`、`CopyAnswerCommand`、`CancelCommand`；收起时隐藏 | 需与 `island-deck.png` 对照；Mac 是按钮区可点、其余区域仍穿透，Windows 当前展开时整窗可点，逐元素对齐待补 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C7 | 未做 | 提词岛绑定三类附加内容：`CurrentSlidePointsText`（讲稿当前页要点）、`CoachHint`（教练提示）、`AutoEndNotice`+「继续记录」（`KeepRecordingCommand`）；各自按可见性显示 | 需演示模式确认三类都出现并与 `island-slide.png` 对照 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C8 | 未做 | `CueIslandWindow` 增加 `BilingualQuestionText` 行（`BilingualAnswer` 的问句标记），翻译行继续用 `IslandTranslation`；`HasBilingualQuestion` 控制可见性 | 需 `island-bilingual.png` 并排对照 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C9 | 未做 | 岛边框颜色取 `SceneTheme.For(mode).TintDark`；展开/收起宽度用 `Motion.morph` 阻尼弹簧（response 0.5、dampingFraction 0.82）逐帧积分；未接系统「减少动画」检测 | 需录展开/收起视频放到 `Windows/validation/island/`；`reducedEffects` 系统开关未接 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C10 | 已实现、未验证 | `Settings.CueScreen`（Mac 键 `cueScreen`）与设置页「提示岛所在屏」下拉框，列出主屏与全部显示器；`CueIslandWindow.SelectedScreen` 按名字选屏，未选时用主屏/内置屏，位置按 `screen.Bounds` 与 `Scaling` 顶部居中，切换设置即时重定位 | 需实机在 150% 缩放副屏确认水平居中、贴顶 |
| C11 | 已实现、未验证 | 浮窗关闭改为隐藏；QA passed | 待逐元素对照或实机验证 |
| C12 | 未做 | 用户隐藏停定时器；QA passed | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| C13 | 已实现、未验证 | `SubtitleBarWindow.axaml.cs`：指针移入降到 0.15 透明度、静止 0.6 秒进入可拖动、拖动后保存位置并在重启时恢复（`subtitleBar.position` 对应 `SubtitleBarX/Y`）、双击重置到底部居中、窗口非激活（`WS_EX_NOACTIVATE|TOOLWINDOW`）；可跨屏保存 | 需实机拖动、重启、双击重置并截图与 Mac 对照；重置入口是否与 Mac 用户路径完全一致待确认 |
| C14 | 已实现、未验证 | `TeleprompterWindow` 增加页眉（状态点/问题原文/字号/穿透/关闭）与页脚（改短/举例/英文/复制/工作台/停止生成/立即回答）；页脚按钮走 `AnswerRefinements.QuestionFor`；设置页「移到」三个按钮调用 `MoveTeleprompterTo(camera/bottom/right)`，按屏居中/贴底/贴右并记住位置；工作台按钮打开主窗口 | 需三位置各截一张与 Mac 对照；工作台在 Mac 是菜单模式，这里是打开主窗口，逐元素补差 |
| C15 | 已实现、未验证 | `TeleprompterWindow`：不透明度下限 0.35；`teleprompterHideBackground` 开启时窗口保持 1、只把 Shell 背景变透明并加实线边框；主窗淡入淡出不再把提词器 Opacity 改成 1，也不再存 1；设置页「隐藏背景，只保留文字、边框与按钮」 | 需实机调到 0.5、隐藏再显示、重启后仍 0.5；背景隐藏时文字与按钮始终不透明 |
| C16 | 已实现、未验证 | `TeleprompterWindow` 接入 `ScrollFollowState`：新回答在跟随态滚到末尾；手动滚动/键盘导航暂停跟随；回到末尾恢复；「回到最新」按钮；`ScrollFollowStateTests` 8 项通过 | 需实机手动滚动后确认暂停与恢复 |
| C17 | 已实现、未验证 | 「隐藏所有窗口」改用 `TeleprompterWindow.HideForAllWindows()`，同时隐藏提词器与穿透控制小条；提词器与小条加 `WS_EX_NOACTIVATE|TOOLWINDOW` | 需实机在穿透状态按隐藏全部，确认屏幕无残留小条 |
| C18 | 已实现、未验证 | `Settings.CapturePrivacy`（默认 true，键 `capturePrivacy`）与设置页「尽力从屏幕共享中排除本应用窗口」；`WindowCaptureHiding.ApplyAll` 每秒扫描所有窗口并施加 `WDA_EXCLUDEFROMCAPTURE`，关闭时恢复 `WDA_NONE`；自检读回主窗、岛、字幕条、提词器四个窗口及开关前后 affinity | 需实机在会议共享中确认主窗口与后续新开窗口都不可见 |
| C19 | 已实现、未验证 | `EvaluateScreenSharing` 仅在 `CapturePrivacy` 关闭或窗口无 WDA 保护时才用窗口标题检测兜底；自检先关闭隐私再验证兜底收回 | 需实机确认隐私开启时屏幕共享不再无谓收回岛，且系统不支持 WDA 时才走兜底 |
| D1 | 未做 | `Platform/DemoScript.cs` 逐字移植 DemoMode.swift/DemoFilm.swift（3 系列、3 技能、2 场归档会议与会中直播、准备 draft、6 份资料、引导示例；含日期/更正/回声行等细节），`DemoContent.cs` 变薄适配层、`AppState.Demo.cs` 承载 LoadDemo/分页演示；演示/截图入口按 Mac `NEXTTHOUGHT_DEMO_SECTION` 语义切页；删掉旧自造演示数据；`DemoScriptTests` 16 项逐字断言 + 96 张截图重跑。**2026-11-21 Mac 参考截图接入（ab1570f1）**：93 张 Mac 3.4.0 浅/深参考图入库并生成 `validation/mac-screens/README.md` 索引（Mac 图 ↔ Windows 截图 ↔ compare.md，脚本 `scripts/build_mac_screens_index.py` 可重跑）；home/prepare/live/notes/history/series/onboarding/settings-structure 与新增 settings-pages/island/library 的 compare.md 逐图核对；顺带修场景胶囊顺序（Mac SessionMode.builtIn）、标题栏 Auto/*/Auto 三列防副标题压标签、截图 harness 引导 4 步×浅/深；`MacScreensIndexTests` 7 项锁定索引完整性；current/ 95 张 Windows 截图重跑 | 逐元素对照已完成、剩余视觉差异记录在各 compare.md（设置全高页 vs 内嵌页、场景表数据/顺序、回答服务/联网卡片位置、快速设置项集、首页区块标题文案、会中实时纪要标签、review/work 与 history 的 Mac 图缺口、引导首步布局、岛 pinned/autoend）；「已验证」仍需人逐页过一遍 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D2 | 未做 | 四套字体+许可随包；嵌入 avares，截图正常；干净机未验 | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D3 | 未做 | 全部 34 个颜色值有测试；逐页截图未做 | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D4 | 未做 | 新增 HitMin=32 / HitIcon=36 / HitRow=36 三 token（Mac HitTargets.swift）；Controls.axaml 全部点击尺寸 token 化（按钮/输入/CheckBox→Control 32，nav→HitRow 36，chip/scene→Control），Views 主按钮与标题输入框点击尺寸收口，剩余例外仅 TeleprompterHandle 24 与 live 提问框 58（测试白名单写明理由）；ThemeResourceTests 加三 token 断言与「可点击数字高度只允许白名单」断言，Views 数字间距/圆角扫描 0 违规 | 实机点按观感未逐项验证；布局限宽 220/720 等属 D10 范围 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D5 | 未做 | 五组 Mac 运动 token 真实现：hover 0.14 秒 cubic-bezier(0.2,0.8,0.2,1)；expand 0.32/0.9、phase 0.46/0.9、snappy 0.28/0.78、morph 0.5/0.82 用 SwiftUI spring 的欠阻尼解析解（mass=1、stiffness=(2π/response)²、damping=2ζ√stiffness），时长取稳定时间；`Platform/MotionModel.cs` 承载数学，`Desktop/Motion.cs` 提供 Avalonia Easing 与逐帧 Progress；提词岛 morph 改真弹簧逐帧；`MotionSpringTests` 验证解析解 vs RK4、超调、时长与 Reduce；侧栏 hover 改 0.14 秒 | 未在实机看动画流畅度/录屏；系统「减少动画」与 `reducedEffects` 应用设置都已接，实机观感待验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D6 | 未做 | SceneTheme 十场景四组颜色/符号/承诺逐值对 Stage.swift，moments 十条改为 Mac 原文三条；InkWash 按 Mac 重写（0.44w×250 / 0.32w×230、opacity 0.20/0.09、blur 80、sin(t*0.18)*18、20fps、Reduce 停、上下渐变遮罩、Animated 开关）；SceneGlyph 按主题取色（深色 TintDark）、Home 44→38、Prepare 静水洗 44；SceneTag 接 Live 状态行；SceneThemeTests 13 项 + SceneComponentTests 6 项；home/prepare/live 截图核对 | 真机动画观感（漂移/Reduce）未验；SeriesView 的 SceneTag 未接（Mac WorkspacesView.swift:680，待补） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D7 | 未做 | 图标全部走资源：`Styles/Icons.axaml` 73 个 Fluent System Icons 资源（57 个 Mac SF 符号 + 10 个 Stage 场景符号 + 6 个 Windows 专用），随包 `FluentSystemIcons-Regular.ttf`（MIT）+ 许可与 THIRD_PARTY_NOTICES；Views 43 处 `&#xE…;`+Segoe 全部换 `{StaticResource IconXxx}`/`IconFont`；SceneGlyph 统一读资源；`ICON_MAP.md` 每行加 Fluent 名称/码点/未用登记；`IconResourceTests` 6 项（字体随包、禁止字形字面量、资源被引用或登记、对照表一致/覆盖） | 12 处按语义取近似（字体无同名 glyph，已逐条写进 ICON_MAP）；44 个资源暂未被界面引用（留给 E 组）；逐图标与 Mac 并排对照待实机 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D8 | 未做 | CheckBox / RadioButton.nav / RadioButton.chip / ListBoxItem 全部 2px Ai 焦点环（Avalonia 12 用 FocusAdornerTemplate）；nav/chip 加 :pressed，ListBoxItem hover 改 Hover、加 :pressed，CheckBox 加 hover/pressed token、MinHeight 28→32（Mac Hit.minHeight）；ControlSampleWindow 文案改回 Mac 原句；新增 --self-test=controls（真实启动校验焦点环实例化）与 ControlStateTests 6 项 | 键盘 Tab 走查与按压态观感待实机；CheckBox 背景 hover 是否被 Fluent 模板渲染需实机确认 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D9 | 未做 | 恢复默认 GPU 渲染 | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D10 | 未做 | 胶囊、覆盖层、snappy、专注、上下文侧栏持久化完成；品牌图标未逐像素 | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| D11 | 未做 | 2026-11-20 0.5.145 Windows 11 七页深色截图逐页无白块/读不清；证据 validation/screens | — 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E1 | 未做 | 首页按 HomeView.swift 重组（StartComposer/ContinueSeries/HomeScreenshotCard/RecentMeetings/OnboardingResumeRow 同名子视图）；标题实时猜场景+pin、选场景应用默认；继续系列（最近 3 个）、最近 4 场（Core MeetingOutcome）；截图卡双态（空闲落首页/录制中即时分析）；InkWash 380；OnboardingResumeRow 接「稍后继续/恢复行」与 interruptedNotice；`--self-test=startup` passed；HomeParityTests 8 + compare.md + 浅深英截图 | 未完成：ContinueSeries 无 updatedAt 用创建时间；matchedGeometry/按压缩放/标题框 focus 场景色描边未移植；演示数据下截图卡无预览；E3 引导状态联动仅单测 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E2 | 未做 | 准备页按 PrepareView.swift 补齐：MeetingDraft（标题猜场景/自定义类型+基于/取消/带 draft 开始）；PreparationStartBar（声音/AI/手机/资料与模型状态、就绪说明、取消/导入 WAV/开始记录）；线上/线下 VenuePicker 语义与采集设置；CheckCard（声音电平/回答服务测试/麦克风重开）；资料行状态+拖放导入、资料洞察 MaterialsInsight（Core PreparationPrompt + 真实回答服务流式往返，未配置/无资料/取消/失败状态机）；只记录对方；SceneSummary tooltip；BeginPreparedMeeting(planSeries)；修复 Prepare 页 ComboBox 递归栈溢出；`--self-test=startup/prepare/onboarding` passed；compare/click-through + 浅深英截图 | 资料洞察无跨页缓存/逐文件进度；AudioSourceMenu 窗口选择与 SeriesMatcher 建议未做；真实模型往返仅状态机验证；实机点按未做 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E3 | 未做 | 首次引导按 Onboarding.swift：Views/OnboardingView 四步（场景/声音/回答/提词与开始）+ 进度/稍后继续；示例会议时间线 OnboardingSample（OnboardingSampleMeeting.swift 逐字，滚动区/13s 参考回答/空态/进度+重播/关闭停止回放）；SetupChecklist 数据与设置「通用」页清单卡；GuideMotion 切步动效（PhaseIn 460ms）；录屏权限步去掉；`--self-test=onboarding` passed；compare/click-through + 逐步浅深英截图 | 示例面板无自动滚动跟读/逐字动画；设置清单实机走查未做；引导整体视觉对照待人工 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E4 | 未做 | 会中页按 LiveSessionView.swift：顶栏（计时/聆听/暂停继续、场景+标题+系列、自动回答↔只记录、手机/屏幕状态、回答设置菜单、工具菜单、结束/开始）；三态联网（不联网/自动判断/每次都查，WebSearchMode+WebSearchModes.ShouldSearch）；转写区（问题标记、候选条+置信度、行内动作、partial、状态行含回声/遗漏/麦克风未开计数）；滚动跟随+「回到现场」；答案栏（建议回答/上下条/联网状态+来源/精修菜单/证据行+详情）；底部 LiveComposer；背景状态点（answerEngineReady 黄点）；Ctrl+L 聚焦；回答链路落盘 EvidenceBundle；live-ui 自检 40+ 项、LivePageParityTests 9、WebSearchModesTests 5；compare.md + 截图 | 「原回答↔联网版」两阶段切换未做（Windows 单次回答架构，需大改 AnswerAsync）；滚动跟随未接文本选择固定；遗漏音频段无计数来源；三态/跟随/快捷键未实机点按 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E5 | 未做 | 回答区按 AssistantView/AssistantLayout/DetachedModules：回答历史从归档读取（录制=内存/会后=SelectedSessionAnswers、游标/位置/回到最新/回看）+空态 Mac 句；DetectionStrip+置信度条（≥0.6 绿/≥0.4 橙/否则灰+ToolTip）；ScreenshotCard；Core AssistantLayout 五模块/槽位/预设/JSON；预设菜单五个 Mac 原句+模块标题；独立窗口实时对话四视图（ConversationPanel/ConversationBubble/EmptyConversationHint/PartialBubbles，跟随/暂停/空态/partial/问题徽标/回声灰显）；question 窗口 520×160；--self-test=assistant-ui 22 项；AssistantLayout 7+AssistantParity 7+AssistantE5Gap；compare.md | 「合并回主窗口」仍走窗口 Close-合并；气泡色用现有 token 近似；回声行无 markNotEcho API（不显示「是我说的」）；实时对话数据源为会话行而非独立会话线程 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E6 | 未做 | 回答语言与双语：新增 Views/BilingualAnswerText（逐句正文+次要色小一号译文分组，无对照回退 Markdown）；提词岛 headline/detail gloss（CueRow.withGloss）；AppState.QuestionTurn.cs 移植 Mac 整轮提问合并（Heard/Spoke、词数、AnswersWholeTurns 条件、静音 2s/patience 30s/≤250 词/≤4 次、Asked 标记、Reset），回答链路改整轮文本；工具菜单回答语言（跟随问题/中文/English）与英文附中文对照接设置；Core BilingualAnswerPairsTests 6、NextThought AnswerLanguageParityTests 5、live-ui 自检 47 项；compare 追加 E6 段 | Mac answerTurn 的 question 事件/Diagnostics 明细未复制；提词岛译文行数裁剪未做；对照显示 Windows 用逐句分组（比 Mac 原样 Markdown 引行更清晰，已在 compare 说明） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E7 | 未做 | 洞察面板按 InsightsPanel.swift 重写：头部（屏幕与讲稿/立即识别/齿轮跳设置）、屏幕感知关闭提示、幻灯片区（页位置/文档名/上下页/重新生成·改用 AI 要点/要点已讲·下一句·未讲/过渡/听众问题点击即问）、屏幕区（状态·来源/错误/识别文字 3↔30 行）、回答进度条（已讲 x/y+下一句，随发言与回答完成刷新）；面板随 SlideStatus/CurrentScreen/AnswerText 自动刷新；InsightsPanelParityTests 6、AnswerPointsInsightsTests 6；compare.md + 截图 | 会中页左栏嵌入版未接（E11 占 LiveView，下一轮）；相对时间/屏幕类型标题/MetricPill 完成态/逐页生成中未做；浅深截图待 verify 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E8 | 未做 | 会中笔记按 MeetingUserNotes.swift：新增 Views/MeetingUserNotes（唯一草稿+保存状态+说明句；「重点 · N」分段合并展示——Core MeetingHighlightSegments.Join 相邻 ≤30 秒合成段、段首时间范围、每条 ★+[mm:ss]+原话、点击跳回原话行；空态）；HighlightLine 改为真正开关（Mac toggleHighlight）；笔记与高亮写入 user-content.json 的 PersonalNotes/Highlights；NotesView 个人笔记页承载新视图；MeetingHighlightSegmentsTests 6、MeetingNotesParityTests 7；compare.md | debounced 自动保存与 onDisappear 持久化未做；读取失败重试行未做；分段合并规则为 30s 间隔（Mac 源无专门规则，按任务落成可测合并）；实机点按未做 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E9 | 未做 | 讲稿与屏幕观察：Core SlideFollowController（屏幕排名决定页；手动翻页成 anchor 暂停自动跟随直到屏幕换页；无匹配宽限 20s/presenting 120s；本人讲稿 ≥16 字符 + 候选页 +1/+2 跟读）；SlideNotesCacheStore（指纹=模式+页文本、按文档 JSON 落盘、损坏/不符忽略）；ScreenClassification（Mac ScreenKind 七类本地启发式）；Desktop 本人最终句走 FollowSpeechForLine、每次读屏分类+跟页、上下页走 controller、讲稿要点按指纹缓存并显示编号+下一页标题、观察行 `slides · 0.60`；`--self-test=slide-follow` 扩断言；compare.md + 8 张截图 | 真实读屏/跟页未实机点过；窗口来源为前台窗口近似（无目标窗口/显示器选择）；屏幕分类未接入回答上下文；无 generatingNotes/缓存清理；暂停跟随即新增 N 句属 E4 已补 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E10 | 已实现、未验证 | MediaPipe ONNX（许可/转换/数值对齐/双模型推理）+ 设置卡/会议驱动/岛提示；coach-gaze 自检 6/6（录图三态 + 真机开相机）；真人摄像头前未验证（见 OPEN_QUESTIONS） | 等待 ONNX 关键点模型方案答复（来源/许可证/大小/关键点/分发） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E11 | 未做 | 实时翻译界面：Core LiveTranslationPolicy（Mac TranslationDirection.detect 脚本计数 5:7 规则、≥4 英文词门槛、自动打开四条件）；AppState.LiveTranslation（开关/状态/自动打开提示、记忆选择 liveTranslation、BeginLiveMeetingTranslation、MaybeAutoOpen 创建翻译服务、两路 partial「译文：」）；Live 工具行同声传译按钮+状态；两条未定稿 lane 显示译文；转写行改 Mac「译文：」样式；LiveTranslationPolicyTests 6/6；compare.md | 无系统 TranslationSession 等价宿主（译文由回答服务翻译）；关闭开关不清已显示 partial 译文；无「自动识别 · 中英互译」状态/版本禁用态；真实外语识别翻译未自动化 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E12 | 未做 | 说话人与我的声音：Notes 转写说话人条/编辑与 MeEditor；名字与「这是我」/合并写入 Mac 兼容 meta.json（voiceNames/meVoices，SessionStore.SetVoiceLabelAsync/MergeVoicesAsync）；MyVoiceSettings 摘要与删除确认；四句录入面板状态与我的声音引导页；复用 SpeakerEmbedder、声纹只在本机；SpeakerMetaPersistenceTests 3/3；compare.md + 截图 | 真实麦克风录三段未执行；SpeakerTag「说话人变化处才显示」折叠未做；音色向量与 Mac 一致性未验证（决策不假设互通） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E13 | 未做 | 截图提问与屏幕上下文：新增窗口选择器（EnumWindows 列其他应用窗口、排除本进程/DWM cloaked）；选中窗口截图（隐藏自身→整屏抓取按 DWM 扩展边框裁切）；区域/窗口截图前后排除自身浮窗并恢复；临时图清理（启动清旧/失败取消删/提问后删）；截图→提问统一 CommitScreenshotAsync；CaptureFilesTests 2 项；compare.md | 窗口选择器无缩略图/图标、无逐显示器列表、区域过小无 Mac 文案；无结构化 ScreenContextService（frameID 稳定等待/变化区域/本机增强），提问只注入 OCR 文本不附图；自检未取证；实机未验 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E14 | 未做 | 联网搜索：新增 Core WebVerificationCache（30 分钟/provider/新密钥失效、空结果不缓存、40 条容量淘汰、AgeLabel）；自动联网先查缓存并显示「使用 N 分钟前的查证结果」；手动「联网查一下」始终新请求并回填；密钥保存即时重建 provider 并刷新状态；WebVerificationCacheTests 6 项；compare.md | 无 Mac 三态联网模式/Codex 搜索 provider/模型复核/原回答↔联网版切换；真实 Tavily 联网未验证；自检未执行 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E15 | 未做 | 四标签（纪要/转写/回答/个人笔记）、证据栏常驻、真提纲视图（编号/时间/概念/待复习，不再是 Markdown）、页内跳转高亮、会议信息窗口、改过纪要的重生成确认；--self-test=notes-ui 6/6；compare.md + 截图 | 视觉逐元素对照仍待人工；部分徽标/条目编辑细节见 compare.md 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E16 | 未做 | 纪要编辑器：用户版本脏标记、取消/关窗脏确认、查看 AI 原稿（只读）、按 Mac 规则写 user-content（无 EditedNotes→EditedSummary.Markdown，有→EditedMarkdown）、个人笔记一并保存、BasedOnGeneratedAt；右侧「原话依据」栏（NotesEvidence 引用、跳转会议页转写并高亮）；compare.md | 结构化纪要逐节逐条编辑+「我已核实」未做；侧栏 ±2 行上下文/当前行高亮/无法定位提示未做；保存/取消文案与 Mac 有差异；无实机截图 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E17 | 未做 | 新增 `Views/CorrectedTranscriptView.axaml/.cs`（类型名 CorrectedTranscript，与 Swift 同名）：校正转写/原始转写切换、查找（Mac 文案「在转写里查找（听错的写法也会找出来）」+「N 句」）、逐条「撤销校对/恢复校对」只改 user-content.json 不改 transcript.jsonl；Notes 转写标签加「校正原话」入口；`AppState.Proofread.cs` 补投影/命令/demo 决定；`CorrectedTranscriptViewTests` 5 项；compare.md 已写 | 未在实机操作；Mac 是 Notes 详情 pane、Windows 用独立窗口（已在 compare.md 注明）；高亮/术语表/说话人条未移植 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E18 | 未做 | 内容搜索升级为 Core SessionSearch 投影搜索（六类 kind + location/excerpt、350ms 防抖、可取消、行内首命中、命中导航 i/n+kind+摘要+上/下循环）；MeetingActionsMenu 全量（打开纪要/重新整理/纪要语言/导出 Markdown/重命名/资源管理器/详细信息/以…准备下一场/删除，录音与任务守卫）；删除对话框 Mac 双选项（HasRetainableAcceptedContent）、删除全部、系列批量删除；系列管理菜单（打开/编辑/删除连会议记录）；归档概览+回答列表+归档截图墙；--self-test=history-ui 12/12；HistorySearchTests 2/2；compare.md | 命中导航不定位高亮转写行；无系列限定搜索；根页文件夹卡片布局未做；菜单禁用态未置灰；归档概览缺清除筛选/返回首页/重试；回答内嵌缩略图未做 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E19 | 未做 | 新增 Views/SessionExportSheet.axaml(.cs)（格式 Markdown/Word/PDF/SRT/VTT、转写 当前校正/原始记录、语言 双语/仅原文/译文优先，Mac 原句；字幕估算与过期警告；同源预览；SaveFilePicker 文件名清洗）；AppState.SessionExport.cs 导出管线（先 flush writes；Word DocxWriter、PDF PdfWriter）；Core SessionExportContent 纯函数；PdfWriter 对齐 SessionDocumentRenderer（44pt 边距/内容宽 507/去页码/标题字号段距/引用缩进/表格 CJK 宽度网格/代码块 9pt）；历史行与 Notes「导出…」入口；--self-test=session-export 30/30 组合；SessionExportContentTests 6/6；compare.md | 对话框内分页预览未做；PDF 斜体/等宽/行内链接与 Mac 有差异；Docx styles/numbering 未逐项对照；用户手记 Markdown 追加未接；读取失败仅提示不禁用；无 Mac v3 导出截图 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E20 | 未做 | 系列编辑器四标签（基本信息/回答要求/资料/复盘）、Mac 按模式字段编辑器、资料导入/移除/撤销（真实 Markdown 导入）、准备队列摘要+失败任务重试/取消、经验与题库可编辑（保留历史）、系列归档/删除连会议记录、资料删除；--self-test=series-editor 9/9；SeriesMaterialsTests 5/5；compare.md | 原生「生成阶段复盘」按钮未接（Windows 无生成服务）；导入无自动归类/未入 DocumentJobQueue（同步）；资料行缺「需要核对」/打开归档 Markdown；经验题库历史恢复 UI 未做 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E21 | 未做 | 知识复核：SeriesChangeCandidateCard（决定/行动 kind、负责人+期限写入记录、依据 quote+location、过期提示、更新保留旧版 revision/history、作为新决定/行动加入）；知识候选对照卡（系列当前问题/答法/本次建议、保留当前答法、采用建议保留旧版、加入题库/经验库）；系列编辑器「记录」标签；资料复核框（警告/原件路径+打开/80k 提取预览/打开完整文本/复核备注→material-review.json）；库列表「需要核对」chip；--self-test=knowledge-review 12/12；MaterialReviewTests 4/4；compare.md | 知识候选「收集本场候选」UI 未放按钮；过期判定近似（无 evidenceReviews 修订链）；资料复核无内嵌 QuickLook；无 Mac 对应截图 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E22 | 未做 | 术语表：术语窗口双作用域（全局/系列）+查找过滤+条目列表/人名/误听 chip+编辑器（类型/听错写法/备注/删除）+批量粘贴/导入/导出/复制/整理重复；TranscriptFindField（术语窗口与会议页转写，匹配 N 处）；MisheardMark（「匹配到听错的写法：…」+tooltip）；AddToGlossarySheet（来源行/规范写法/听错写法/类型/仅当前系列）；EffectiveGlossary=全局+会议系列+资料术语，统一用于回答提示/识别热词/转写查找；--self-test=glossary-ui 9/9；GlossaryMaterialTermsTests 6/6；compare.md | 剪贴板自动预填未做（Avalonia 12 文本读取 API 不可用，改为查找命中预填）；纪要提示未注入 glossary 块；会议页查找命中不滚动定位；无 tag 流式组件/无 Mac 截图 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E23 | 未做 | 文档处理：资料页「读取资料的方式」（系统识别/本机增强/Codex 代读+Mac 帮助句+未就绪警告+安装支持）；高级资料组件（组件行：运行包/PaddleOCR-VL 1.6+PP-DocLayoutV3/Qwen3-Embedding，安装·校验·删除；回退开关；Codex 模型/推理强度/超时；语义检索并入）；**四个 Mac 同名视图** Views/DocumentReadingSettings/DocumentComponentsAdvanced/DocumentChoiceSummary/ManagedModelRow；Platform DocumentAssets/DocumentRuntimeInstaller/CodexDocumentReader/DocumentPreparation（隔离 venv、固定 revision SHA-256 下载、按页/段 Codex 代读、持久 DocumentJob、回退）；`--self-test=codex-document`/`settings-presets` document 步骤；compare.md + 16 张截图 | 运行时/模型真实下载未实测（需联网+本机 64 位 Python 3.12）；无取消下载/扫描本机组件/License 链接/多档模型/重建索引；Mac document MCP 无对应（Codex 读本机提取文本，图片走系统 OCR） 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E24 | 未做 | 系列范围 + 学术模式整份资料 prompt 已接；未跑真实模型 | 待逐元素对照或实机验证 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E25 | 未做 | 设置结构按 Mac：六页侧栏（场景/回答与资料/提词与教练/纪要/通用/高级）；外观下拉移入通用→界面语言与外观；Coach 卡改「提词」+「显示方式」；自动检查更新移入通用→更新；设置清单移入高级；搜索跨全部页面匹配、结果「卡片标题 · 页名」、点击跳页并清空、无结果「没有匹配的设置项」；SettingsStructureTests 4；validation/screens/settings-structure/compare.md | 遗留：记录每次会议开关缺失、说话人与翻译暂留 Coach、语义检索两处并存、Mac 搜索关键词表未移植；截图待重拍 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E26 | 未做 | 场景设置与快速设置：Core SceneDefaults.For(mode) 按 Mac 表逐字（liveMode/style/template+ScenePreset.cueStyle）、IsDefault/IsCustomised/IsEmpty；sceneOverrides 只写差异、notesTemplateByType 独立键（修复 typed 往返）；SceneSettingRow per-mode 默认+全部模板+恢复默认；快速面板补提词字号/一直展开/双屏演讲/提词 岛↔浮窗/双屏讲稿开关（cueSurface/presenterSlideNotes 类型化+映射+消费，MainWindow.Shell 按 cueSurface 切岛/浮窗）；截图 runner 支持 quick-settings 浅深英；CuePresentationSettingsTests 2、CuePresentationPanelTests 4、Core 3+App 3 测试 | 实机双屏/岛-浮窗观感未核对；设置页滚动下方截图未单独出 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E27 | 未做 | 回答服务设置：预设管理（选择/命名/新建/重命名/删除、删除确认 Mac 句、仅剩 1 禁删）、模型列表获取、API 推理强度、引擎测试行（检查连接 + Mac 状态句）、Codex 推理强度/快速模式/GitHub 访问（仓库/令牌状态/保存并检查/清除）；更新客户端读 updateRepository（feed URL）与 github.token（DPAPI Bearer 用于 release feed/manifest/checksums/下载）；--self-test=settings-presets 11 步；settings-pages 截图与 compare | Codex Fast 服务档位行为未验证（Windows CodexAnswerClient 无 service-tier 参数，仅键/模型）；页面截图只截首屏，预设/GitHub 卡在下方未进画面 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E28 | 未做 | 技能设置按 Mac SkillSettings/SkillEditor：Skill.Global 默认 false；Core Mac starterTemplate 与三条 demo 技能逐字；设置页技能卡 Mac 文案（技能/系列说明/SKILL.md 说明/空态）+「导入 .md 文件…」；编辑器 Mac 标签（名称/一句话说明/启用/所有系列都使用这个技能）+实时字数句；保存保留 id/导入时间；删除走 Mac 确认句；新技能用 Mac 模板、默认非所有系列；安装示例技能补装三条 demo；--self-test=settings-presets 含技能点按步骤；SkillDemoTests 3 + SkillSettingsTests 4；validation/screens/skills/compare.md | 技能列表缺 Mac 图标/只读摘要/系列使用计数；编辑器为常开内联卡无「取消」；无硬性 6000 字截断；截图证据待 verify 批次入库 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E29 | 未做 | 洞察「屏幕与讲稿」并入高级页；会议自动收尾移到纪要页（Mac 句）；数据与关于拆出「备份与恢复」卡（主体 E30）；新增「权限」卡（麦克风/摄像头 + 打开系统设置，无录屏授权行）；Platform WindowsPermissions（ConsentStore + NonPackaged 桌面项，Deny 优先）；--self-test=settings-presets 覆盖权限/命令；settings-pages 截图证据 | 权限真实系统状态与授予流程需实机；设置页滚动下方内容未进截图 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E30 | 未做 | 新增 `Views/BackupSettingsSection.axaml`（自动备份开关、恢复点创建/删除、备份列表、立即备份）与 `Views/BackupRestoreReviewView.axaml`（导入前复核：内容数量/警告→确认后才应用，两阶段 `PrepareImport/ApplyPreparedImport`）；`IncrementalBackup` 增 List/Delete、`BackupService` 增恢复点创建与导入复核；设置页数据/关于挂载；`RunBackupUiCheckAsync` 自检 8 步（隔离数据目录）；`BackupRecoveryTests`；--demo --self-test=backup-ui 通过 | 未用真实 Mac 备份走完整导入复核；自动备份触发时机的实机验证待做；新视图已用 `{loc:Tr}` 但 LocalizationCoverage 排除列表待移除 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| E31 | 未做 | `Platform/DemoStage.cs` + `DemoScript.cs` 移植 Mac `NEXTTHOUGHT_DEMO_SECTION` 7 阶段（home/prepare/live/live-notes/meetings/library/settings 及旧别名解析）、各阶段 apply 差异（isRunning/ClearsAnswerState/draft/归档视图）、demoNotes=work 变体、9 个岛 phase 数据化、DemoFilm 两段脚本（island 20fps/12.6s、home 15fps/7.6s）；`DemoStageTests` 10 项 | Mac 的 DemoFilm 帧录制器未移植（开发工具，不新增启动参数）；settings 阶段选页由截图工具负责；需实机看各阶段观感 复核（任务书 §3.4）：完成条件未满足，需按 Mac v3.4.0 重做。 |
| F1 | 已实现、未验证 | 旧采集管道停止并释放；显式设备不被覆盖 | 待逐元素对照或实机验证 |
| F2 | 已实现、未验证 | 卡顿不再结束会议；实机睡眠未验证 | 待逐元素对照或实机验证 |
| F3 | 已实现、未验证 | 停止排空至空/5 秒；实机尾句未验证 | 待逐元素对照或实机验证 |
| F4 | 已实现、未验证 | 先发结束/等最终结果再取消 | 待逐元素对照或实机验证 |
| F5 | 已实现、未验证 | 云端实时识别已建立两路会话：`cloudStt`（系统声音）与 `cloudSttMicrophone`（麦克风），帧按来源分别发送；两路结果各自按 `generating` 代次写入转写，失败分别提示；停止时两路都释放 | 需云端模式同时开系统声+麦克风实机验证双方说话都出现且说话人正确 |
| F6 | 已实现、未验证 | 接收端按 `EndOfMessage` 拼接分片；发送端按 Mac 上限 80 in-flight（`CloudSttSession`），超限/发送异常用 Mac 原句一次性报错；收到 Close 完成握手后退出，停止中不把关闭误报为识别失败；`StopAsync` 先发结束消息、等最终结果≤2s、再发 close 帧（超时也不再跳过关闭）；新增测试：40 KiB 分三片解析为一条 final、>512 KiB 报错、服务器不读时 400×1600 帧触发 80 上限且内存不无界增长 | 需真实云端服务长会话实测；内存增长目前只在回环测试观测 |
| F7 | 已实现、未验证 | wss+白名单；读密钥失败不覆盖 | 待逐元素对照或实机验证 |
| F8 | 已实现、未验证 | 文件转写超时由 30 秒改为 30 分钟，超时无结果时抛明确中文提示；连接中断且无转写时提示，已收到 final 后断连不再误报；单声道化支持任意 >2 声道（逐帧平均）；新增三声道 WAV 混音测试 | 需用 10 分钟真实录音与 Soniox/OpenAI/Deepgram 各跑一次；失败提示文案需与 Mac 对照 |
| F9 | 已实现、未验证 | 设置页云端识别说明改为「实时记录与导入 WAV 都走云端识别」；双屏演讲说明改为已支持讲话/截图跟页 | 实机确认选云端后实时录音确实发往所选服务，关闭时无音频离开本机 |
| F10 | 已实现、未验证 | `CompanionServer` 对动作先去重（`commands` 按动作编号）、记录后立即返回 204/409，再在后台执行；相同编号重试不会重复执行；`Allowed` 含 9 个动作 | 需实机手机点一次「回答」，确认电脑只生成一次；8 秒期限内应答需实测 |
| F11 | 已实现、未验证 | 快照已填 `Position`（第几条/共几条）、`AnswerID`（会议:回答数）、`Phase`（generating/recording/idle）；回答开始/结束都通过 `OnAnsweringChanged` 推送；`regenerate` 已是“重新生成不同版本”而不是普通 answer；`CompanionBLETests`/`CompanionCryptoTests` 已翻译通过 | 需实机手机看状态、第几条、上一条/下一条与电脑一致 |
| F12 | 已实现、未验证 | 逐项对照表写入 `Windows/validation/companion/SCOPE_LIFETIME.md`：监听范围仅 RFC 1918+回环（100.64/10 已移除）、配对 fragment 令牌每次轮换、无 8 小时自动停止、配对链接字段与 Mac 相同、中转通道接口相同；`CompanionCryptoTests`/`CompanionBLETests` 已翻译通过 | 需手机实机扫码、跨网段与中转通道验证；对照表基于源码核对，非实机 |
| F13 | 本版不做 | OPEN_QUESTIONS 已记录；不修改 Mobile/，不实现 Windows 蓝牙外设 | 本版不做；事实更正：Mobile/ 在本仓库，手机端已按服务标识扫描但发现后仍检查 IH- 名字，Mac 广播 NT- 同样过不了，留给以后 |
| F14 | 已实现、未验证 | 更新检查移入 `Platform/UpdateCheck.cs`（`UpdateReport` 一并迁移），滚动版按发布 `update.json` 清单的 build 比较，并补上 Swift `Updater.check` 的「清单必须描述本发布」校验（tag/zip 不符报「更新清单与发布不一致。」）；Desktop `UpdateFeed.cs` 变薄包装并传本程序集版本；`UpdateFeedParityTests` 2 项用本地假发布目录（build 41>40 能发现、同 build 不发现、tag 不符报错）+ B22 的 `UpdateCatalogParityTests` | 需真实发布通道/联网实测；假发布目录之外未跑 |
| F15 | 已实现、未验证 | 更新状态改到 UI 线程；断网日志未验证 | 待逐元素对照或实机验证 |
| F16 | 已实现、未验证 | 检查到更新后自动后台预下载（`StartInstallerPrefetch`）；新增 `Platform/UpdateInstaller.PrepareAsync/VerifyAsync`（https 流式下载或 QA 本地路径、SHA-256 校验、坏包删除）；`UpdateInstallerReady` 为真才在标题栏显示「重启以更新」；自检改读应用数据目录 `update-launch.json` 并等预下载；`UpdateInstallerTests` 2 项 | 真实 Inno 安装包执行+重启后版本号变化、标题栏按钮出现时机需实机验证 |
| F17 | 已实现、未验证 | 移除 `NEXTTHOUGHT_UPDATE_LAUNCH` 与 `--update-check`；自检写应用数据目录 `update-launch.json` | 正式发布包扫描确认不含两个字符串 |
| F18 | 已实现、未验证 | `App.axaml.cs` 的 `ShutdownRequested` 从 async void 改为：先取消退出→后台等 `ShutdownAsync` ≤15 秒→再退出；录音中关窗继续托盘录制，真正退出时等待所有写入与 `Sessions.EndAsync`、吞关停异常后关闭浮窗再 Shutdown；`ExitWaitsForShutdownWithATimeout` 通过 | 实机录音中关窗/托盘退出后的 meta.endedAt 与转写完整性待验 |
| F19 | 已实现、未验证 | 全局兜底+各视图 async void try/catch；实机恢复未验证 | 待逐元素对照或实机验证 |
| F20 | 已实现、未验证 | 历史列表、选中会议读取、截图列举、准备会议会话读取、`context.json` 构建全部移出 UI 线程；`ContextDocument.CharacterCount` 加 `[JsonIgnore]`，`context.json` 字段集合与 Mac SessionContextSnapshot 一致（顶层 mode/style/preferences/profile/documents/effectiveMode，文档层 id/name/pages/enabled/category）；`SessionContextPersistenceTests` | 资料库 50 篇时点开始到开始录音 <1 秒、界面不卡顿需实机测量 |
| F21 | 已实现、未验证 | Codex 客户端 `initialize.clientInfo.version` 改用 `ProductVersion.Current`（入口程序集版本，去掉 +sha）；模型解压改用 `Environment.SpecialFolder.System` 下 `tar.exe` 的完整路径，缺失时抛 `FileNotFoundException`，不再从 PATH 找 | 未实跑 Codex 初始化与模型下载；需联网/账号实测 |
| G1 | 已实现、未验证 | 保存设置带快捷键 | 待逐元素对照或实机验证 |
| G2 | 已实现、未验证 | `ShortcutKeys.Actions` 改为 Mac 的 18 个动作（标识按 Mac 顺序，名称用 Mac 文案），默认键按换算表：⌘⇧/⌘⌃→Ctrl+Alt，⌃⌥→Ctrl+Alt+Shift；去掉 Windows 独有「显示主窗口」，原「显示/隐藏提示岛」改为 Mac 的 `toggleCueOpen`（会中一直展开/恢复）；主窗口注册表同步更新；`ShortcutKeysTests` 逐个断言标识/按键/修饰键 | 需实机按 18 个快捷键逐个触发；与 Mac 名称/顺序的并排核对待做 |
| G3 | 已实现、未验证 | 新增 `ShortcutKeys.Validate`：必须带修饰键、两个动作不得同键，错误文案用 Mac 原句；保存快捷键时先校验；`AppState.ShortcutConflicts` 记录被其他程序占用的动作并在设置页显示；`ShortcutKeysTests` 新增校验用例 | 需实机占用一个键（如被其他程序注册 Ctrl+Alt+M）确认设置页标出该动作；重复键保存被拒的界面提示待看 |
| G4 | 已实现、未验证 | 设置页快捷键区改为按动作生成的录制控件：点按键位后直接按下组合键录入（Esc 取消，必须带 Ctrl/Alt/Shift/Win），每个动作有「清除」「默认」；录入后立即写入 `ShortcutText` 并调用保存/重新注册；文本框编辑器移除 | 需与 Mac `ShortcutSettingsView` 截图对照；「清除」目前与「默认」同效（全局热键无法表示禁用），差异待确认 |
| G5 | 已实现、未验证 | `Windows/SETTINGS_PARITY.md` **113/113**（101「已实现、未验证」+12「本版不做」），每键列类型/默认值/范围/Mac 视图/Windows 状态/接入任务，由 `Windows/scripts/settings_parity.py` 从 Swift 源码复算；新增 `MacPreferences.g.cs` 注册表与 `MacPreferenceMap`，`SettingsParityDefaultsTests` 逐个断言默认值/类型/枚举/范围，`SettingsParityTableTests` 锁定 113 行与 settings.json/plist 的 Mac 键名往返；已接行为：teleprompterOpacity/FontSize 改为 Settings 键（默认 1.0/22、下限 0.35、旧 win-ui.json 一次性迁移）、reducedEffects→Motion.Reduce | 约 40 个键目前只有存储层与读 API，需 E2/E3/E4/E6/E7/E10/E14/E15/E16/E18/E25/E28/E30/F10 接入消费者后才算真正生效；E29 补设置页控件；Windows 专有映射与数据格式差异逐键写在 SETTINGS_PARITY |
| G6 | 已实现、未验证 | `AppSettings.AdditionalProperties` 保存不认识的 JSON 键；`SettingsStore` 读写原样往返；`MacSettingsPlist.Apply` 把 plist 里非导出键按 bool/string/integer/real/array/dict 转成 JSON 保存，`Write` 再写回 plist；新增未知键往返测试 | 需用真实 Mac 备份 plist（含快捷键码等 Mac 独有键）导入再导出逐键核对一个不少 |
| G7 | 已实现、未验证 | 新增 `Windows/scripts/generate_ui_text.py`，从 `Resources/Localization/Localizable.xcstrings` 自动生成 `UiTextMac.g.cs`，全部 2607 条英文文案（含占位符、复数 `other` 分支、C# 转义）；`UiText.MacStringCount` 与 `UiTextCoverageTests` 锁定 2607；重新生成后两套 Core/App 测试通过 | 需在英文模式逐页截图扫描中文残留（G10）；占位符调用点是否正确替换仍需逐控件检查 |
| G8 | 已实现、未验证 | 全部界面文字改显式调用：XAML 用 `{loc:Tr Mac中文原句}` 标记扩展、代码用 `UiText.T/Format`；删除 `UiTranslation.cs` 与整棵控件树遍历；`UiTextSource` 索引器 + INotifyPropertyChanged 实现切换语言即时刷新（`--self-test ui-language` 实测 dialog 标题「确认」→「Confirm」原地变）；默认跟随系统语言（Mac `appLanguagePreference` system/zh-Hans/en），设置页三项；`--screenshots` 额外输出 `text/<页面>.txt` 文字清单供 G9/G10 扫描；新增 UiTextSourceTests 13 项、LocalizationSourceScanTests、LocalizationCoverageTests | E17/E30 新视图尚未迁 `{loc:Tr}`（提交时临时排除，落地后补）；未在英文系统实机验证首启英文；仍有无硬编码文字待 G9 清 |
| G9 | 已实现、未验证 | 提示词走 Core AnswerPromptBuilder.MakeParts（answer/gloss 语言用 Mac JSON 字段），删除自建中文 prompt；运行时文案入表并随语言切换刷新；demo 语言切换回归修复；UiTextWindows 673→1171 行（本轮补 E4/E5/gllossary/speaker/window-picker/assistant/live-conversation 等）；WORDING 重生成；英文扫描 50 页、192 含中文行、192 允许、残留 0；`--self-test ui-language` passed；Core 定位测试 16/16 | 英文系统实机首启未验证；语言切换后代码生成列表需实机抽查 |
| G10 | 已实现、未验证 | `Windows/WORDING.md`：系统名词替换表（钥匙串→凭据管理器、访达→文件资源管理器、⌘→Ctrl/Alt、系统语音/翻译/权限等）+ 905 行逐句替换表（中文、英文、可对应的 Mac 原文、位置）+ 英文逐页扫描结果与复算命令；生成/扫描器 `Windows/scripts/localization_scan.py`；证据 `Windows/validation/localization/english-scan.md`（48 个英文页面、166 条含中文行、残留 0，允许项=演示/用户数据与 Windows 设备名）；`WordingTableTests` 锁定每条 Windows-only 文案都在表里 | 需人在英文系统实机逐页抽查；Mac 侧系统名词替换是否可接受由人确认 |
| H1 | 已实现、未验证 | MainWindow 258 行、56 检查各一文件、旧参数删除、--self-test 实跑通过 | 待逐元素对照或实机验证 |
| H2 | 已实现、未验证 | 方法重命名与拆分：Roslyn 语义扫描 clean HEAD 全树——Core 779/Platform 67/Desktop 101/Core.Tests 63/Tests 27 个与 Swift 大小写不敏感同名方法，**全部已是 PascalCase，需改名 0**（2 个小写开头普通方法无 Swift 对应物，不动）；AppState.cs 354 行（<600）、49 个 AppState.*.cs partial；15 个 Mac AppState+*.swift 已有 14 个同名 partial，新增 `AppState.Notes.cs`（762 行，由 Templates.cs 整体并入 + StructuredNotes 移入 10 个纪要方法与纪要页导航块，逐字校验 0 新增/451 删除、无逻辑改动），删除 AppState.Templates.cs；桌面构建 0 错误、NextThought 512/512 通过 | 未用 IDE/Roslyn 自动重命名提交（结论是 0 个需改）；18 个 Mac AppState+Notes func 中 12 个有 C# 对应物（其余由 Core/设置/视图承担，已在报告列明）；「已验证」需独立复核一次扫描 |
| H3 | 已实现、未验证 | 新增 `Platform/Win32Native.cs` 集中所有 P/Invoke（指针/窗口样式/防截屏/热键消息泵/GDI 截图/系统动画开关）及结构体；Desktop 的 `GlobalHotkeys`、`Motion`、`ScreenCapture`、`WindowCaptureHiding`、提词岛/字幕条/提词器/小条全部改为调用 `Win32Native`，删除本地 `DllImport`；新增源码扫描测试断言 Desktop 无 `DllImport`/`LibraryImport` | `Platform.Stub` 替身尚未建；截图/热键/非激活样式需实机回归 |
| H4 | 已实现、未验证 | `Windows/scripts/core_usage.py` 改为按「Core 文件声明的类型是否被 Windows/src 或 tests（含其它 Core 文件）引用」统计；复算 **core_files=129, unused=0**；`CORE_PARITY.md` 口径与复算命令同步更新，旧的文件名启发式 40 已废弃 | 统计仍是类型名文本匹配，非编译器级；需最终验收时再跑一次确认 0 |
| H5 | 已实现、未验证 | 旧工程 NextThought.Windows/Cli 与旧安装器/脚本已删（22b7f2eb）；sln/CI/build_installer 切 Desktop；verify 全绿 | 等待是否删除旧工程答复 |
| H6 | 已实现、未验证 | 现有 Native AOT JSON 警告清单 `Windows/AOT_JSON_WARNINGS.csv` 保持 161 条；新增 `AotJsonBudgetTests` 锁定不超过 161；本轮新增代码（ProductVersion、Win32Native、字符串生成、备份合并）未增加清单行 | 需真实 AOT 发布重新生成清单确认仍为 161；`AOT_REPORT.md` 记录 AOT 备份 JSON 仍失败 |
| H7 | 已实现、未验证 | `verify.ps1`：锁定还原、Release 构建、两套测试、UTC 时区 Core 一遍、Mac 夹具比较套件（40 项，替代已删除的只查键名 MacCodecCheck）、D1 截图（`-SkipScreenshots` 可跳）；任一步失败即停。阶段 1 结束全量运行 **PASS**：Core 1700/1700、Core UTC 1700/1700、Mac fixture 40/40、NextThought.Tests 551/551 | 干净机器上的首次运行仍需人验证 |
| J1 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：实时纪要：会议中约每 45 秒整理，右栏「实时纪要」显示整场/议题要点/定了什么/要做什么；较早议题定稿后不再重写；回复失败保留原纪要；新增内容场景色；会后仍整理完整纪要。核心 LiveNotes.swift、LiveNotesEngine.swift、LiveSessionView.swift；完成条件：翻译 LiveNotesTests.swift 并通过；截图与 live-notes-*.png 逐条一致 |
| J2 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：开会前选功能：准备页「这场用什么」五开关+说明；选择记在系列上、下一场沿用；会中「⋯」可开关。PrepareView.swift、AppState.swift；完成条件：关掉后该功能不运行；下一场沿用；截图与 prepare-*.png 逐条一致 |
| J3 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：统计：首页「用了多少」本月/累计场数、时长、提问、回答；每个系列名下一行；时长按每场开始到结束。MeetingStats.swift、MeetingStatsViews.swift、HomeView.swift；完成条件：导入 all-scenarios 后数字与按 Mac 规则手算相同 |
| J4 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：系列：「资料夹」改叫「系列」；首页继续系列每行场数/累计时长、点进去开下一场；系列页「开下一场」。WorkspacesView.swift、HomeView.swift、HistoryView.swift；完成条件：界面搜不到「资料夹」；截图与 series-*.png 逐条一致 |
| J5 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：转写不丢字：重识别只在覆盖范围内替换；UtteranceText.swift、SpeakerClustering.swift、ZipformerStreamingTranscriber.swift；完成条件：翻译 UtteranceTextTests.swift 与 SpeakerClusteringTests.swift 新增部分并通过 |
| J6 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：说话人和文字可改：转写上方「我是哪个？」；右键改说话人/另一个人；手动起名的人少说也显示；会后右键改这一句文字（原始转写不动、重新整理用改过的）。SpeakerViews.swift、SpeakerID.swift、NotesPageView.swift、AppState+Notes.swift；完成条件：transcript.jsonl 字节不变，改动写入 Mac 同一文件，往返测试通过 |
| J7 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：文字缩放：Ctrl +/−/0 全局缩放；提词岛与回答浮窗自己的大小按钮；显示器稳定 1.5 秒再处理；叫回主窗口后本场不再自动隐藏。NextThoughtApp.swift、PresenterMode.swift；完成条件：重启保留；三档缩放无截断 |
| J8 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：其余 3.4.0 差异：DesignSystem.swift 新增部分、DemoMode.swift 演示数据、新增字符串、diff 里其余每个视图文件。完成条件：FIX_TASKS 里 diff 的每个文件一行，状态不是「未做」 |
| J9 | 已实现、未验证 | 已实现并自检/截图入库；见 Jn 提交与 J8_SWIFT_TO_WINDOWS_STATUS.md（J1 LiveNotes、J2 功能开关、J3 统计、J4 系列文案、J5 UtteranceText/聚类、J6 逐句改正、J7 文字缩放、J8 盘点+缺口、J9 Core 测试逐字对译） | 任务书 §7：测试：Mac 现为 604 项 Core 测试；完成条件：CORE_PARITY.md 合计 604，每行 Swift 测试数 = 已翻译数 |
| K1 | 已验证 | 本机实测通过 2026-11-21：validation/realmachine/K1（GDI+WGC 两种截屏、开/关设置前后对比、SHA-256、复现脚本） | 任务书 §9：防截屏：主窗口/提词岛/字幕条/提词浮窗在 GDI 与 Windows.Graphics.Capture 下显示后方内容；关开关后出现；两种截图存证 |
| K2 | 已验证 | 本机实测通过 2026-11-21：validation/realmachine/K2（记事本前台 + SendInput 点击提词岛三点 + 键入回读） | 任务书 §9：不抢焦点：记事本前台，模拟点击提词岛按钮，前台仍是记事本，键入进入记事本 |
| K3 | 未做 | 未做：演示模式无语音模型、真实会议岛不可得；步骤见 RELEASE.md §9 K3，留给人 | 任务书 §9：鼠标穿透：收起时点击穿过；展开后按钮可点 |
| K4 | 已验证 | 本机实测 2026-11-21：validation/realmachine/K4（隐藏所有窗口 report.json + 关闭/隐藏/重现 window-close.json；真实会议 Alt+F4 留给人见 RELEASE.md §9） | 任务书 §9：关闭与隐藏：Alt+F4 关岛后提问岛重现且程序不退出；字幕/穿透下隐藏所有窗口行为 |
| K5 | 已验证 | 本机实测 2026-11-21：validation/realmachine/K5（快捷键/侧栏/提词透明度 0.5/文字缩放 1.2 写入→重启读回全部一致，演示会话代替真实会议；k5-set-run.json + k5-verify-run.json） | 任务书 §9：设置保存：快捷键/提词透明度 0.5/上下文侧栏/文字缩放，开会结束、重启后保留 |
| K6 | 未做 | 未做：需默认扬声器播真实语音 60s（无回放设备链路），留给人 | 任务书 §9：采音与识别：默认扬声器播已知语音 60 秒，转写出现预期句；最后一句停止后仍在转写里 |
| K7 | 未做 | 未做：需 2 小时真实采音与内存采样，留给人 | 任务书 §9：长会议：循环语音录 2 小时，每分钟记内存；转写不中断、内存不持续增长、会后能打开；有回答服务顺便验纪要生成 |
| K8 | 已验证 | 本机实测通过 2026-11-21：Windows/validation/realmachine/K8（无头 Edge + CDP 渲染真实手机页面，13/13；含更新推送、动作点击、真实 TCP 去重/重连/轮换/关闭） | 任务书 §9：手机页面：无头浏览器打开配对链接，问题/回答/状态/第几条一致；同一动作编号发两次只执行一次 |
| K9 | 已验证 | release.ps1 生命周期实跑 16/16 PASS：静默安装/--self-test/覆盖升级/数据保留/卸载/重装（artifacts/release/0.9.0 + RELEASE.md §9） | 任务书 §9：安装生命周期：见 §10.5（静默安装/写数据/覆盖升级/卸载留数据/重装数据在） |
| K10 | 未做 | 未做：需先发布真实更新版本验证应用内更新，留给人 | 任务书 §9：应用内更新：见 §10.5（假发布源 windows-v0.9.1、发现/下载/校验/重启更新、SHA-256 错误拒绝） |
| K11 | 未做 | 未做：需干净 Windows 沙盒系统，留给人 | 任务书 §9：干净系统：有 Windows 沙盒则安装并走完首次引导，检查衬线标题/中文字体；没有则写明留给人 |
| K12 | 已验证 | 本机实测 2026-11-21：validation/realmachine/K12/audio-switch.json（8 设备中选择第二台，切换后 23 帧，error 空）；拔出/睡眠留给人（RELEASE.md §9） | 任务书 §9：切换与拔出音频设备、睡眠唤醒：有两台以上设备做切换；拔出和睡眠留给人 |
| K13 | 未做 | 留给人：需第二台设备，在真实会议软件（Zoom/Teams/腾讯会议）共享整屏，确认对方看不到主窗口/岛/字幕条/提词浮窗 | 任务书 §9：会议软件里对方是否看得到窗口：需要第二台设备（人做） |
| K14 | 未做 | 留给人：真实手机在同一局域网与中转两种方式配对，验证问题/回答/状态同步 | 任务书 §9：真实手机：局域网与中转（人做） |
| K15 | 未做 | 留给人：多屏与不同缩放比例（主屏 100%、副屏 150%），岛/字幕条/提词浮窗位置与大小正确 | 任务书 §9：多屏与不同缩放比例（人做） |
| K16 | 未做 | 留给人：真实云端识别三家、联网搜索与回答服务账号的端到端验证 | 任务书 §9：真实云端识别三家/联网搜索/回答服务账号（人做） |
| K17 | 未做 | — | 任务书决定文件 §6：PPTX 读屏跟页与讲稿要点：导入演示 PPTX，打开读屏并开始记录，前台翻页，确认跟页和讲稿要点随之变化（本机实测） |
| K18 | 未做 | — | 任务书决定文件 §6：文档组件真实下载与校验：成功、取消、网络中断三种情况（本机实测） |
| R1 | 已实现、未验证 | ReleaseIdentityTests 6/6；Desktop.csproj Version=0.9.0，安装标识 NextThought.Desktop | 任务书 §10：版本和产品标识：版本只在 NextThought.Desktop.csproj；构建号单调递增；产品名 NextThought；安装标识 NextThought.Desktop；安装到 %LOCALAPPDATA%\Programs\NextThought；界面不显示版本徽标；旧预览版标识检测/接管；用户数据不丢 |
| R2 | 已实现、未验证 | release.ps1 -Ci 端到端 exit 0；payload 清洁审计 247 文件 PASS；安装包 82.6 MB | 任务书 §10：一条发布命令：Windows/release.ps1 -Version，整合 build_desktop/build_installer/run_release_checks/test_installer_lifecycle/test_release_smoke/write_release_manifest；干净工作区+分支+版本校验；verify 全量；发布 x64 自包含 ReadyToRun；生成安装包与 SHA256SUMS/update.json；跑安装与更新测试；产物放 artifacts/release/<版本>/；最后打印人的发布命令 |
| R3 | 已实现、未验证 | artifacts/release/0.9.0 四件套（setup/RELEASE_NOTES/SHA256SUMS/update.json）；SHA-256 见最终回复 | 任务书 §10：产物：安装包 NextThought-0.9.0-windows-x64-setup.exe、SHA256SUMS.txt、update.json（标签/版本/构建号/文件名/SHA-256/提交/发布时间）、RELEASE_NOTES.md（中英） |
| R4 | 已实现、未验证 | windows-v* 标签 + installer/update.json 双资产规则；Update 过滤 22/22 | 任务书 §10：应用内更新不影响 Mac：Windows 标签 windows-v<版本>；只认带安装包+update.json 且标签一致的发布；UpdateCatalog.cs 注释 Windows 差异并加测试（候选列表断言选 windows-v0.9.1）；隐藏更新通道选择并标本版不做；下载校验 SHA-256；按 Mac 后台检查/下载/重启以更新 |
| R5 | 已验证 | test_installer_lifecycle.ps1 在 0.9.0 安装包上 16/16 PASS（release.ps1 实跑） | 任务书 §10：安装与更新自动测试（同 K9/K10）：独立标识与数据目录；安装→写数据→覆盖升级→卸载→重装；假发布源 0.9.1 升级、SHA-256 错误拒绝 |
| R6 | 已实现、未验证 | Windows/RELEASE.md 已写（人照做即可发布，含 --latest=false 命令与 K13–K16 步骤） | 任务书 §10：给人的发布步骤：Windows/RELEASE.md 前提/构建/发布命令（gh release create --latest=false）/发布后检查/下一版怎么发/留给人实机测试步骤/建议 README 文字 |
| R7 | 已实现、未验证 | README/RELEASE_NOTES/THIRD_PARTY_NOTICES/licenses/models.md 已写；8 个模型 URL 可达、4 个整包 SHA 校验通过 | 任务书 §10：文档和许可：Windows/README.md 面向用户；RELEASE_NOTES.md；THIRD_PARTY_NOTICES.txt 与 licenses 覆盖全部随包第三方；程序内入口可见；首次运行下载模型的地址/SHA-256 固定并逐个下载验证（validation/models.md） |
| R8 | 已实现、未验证 | release cleanliness PASS（247 files）、日志无密钥（0 violations）、正式包三入口、样张页仅 Debug、TODO/FIXME/HACK 0 | 任务书 §10：发布前清理：正式包无 QA/GPU/UPDATE_LAUNCH/--update-check 等；样张页只在调试构建；日志无密钥；占位/TODO/FIXME/HACK 清零或记 OPEN_QUESTIONS；未使用 Core 文件 0、未引用界面文件 0；默认设置与 Mac 相同；更新 PR #32 说明 |

## 1. ⽬标与范围 

### 完成的定义 

- 「与 Mac ⼀样」按下⾯四条判定,四条都满⾜才算完成: 

1. 功能: Mac 的每个⻚⾯、每个设置项、每个快捷键动作在 Windows 上都有,⾏为相同。系 统做不到的只有第 1 节「允许的差异」⾥列出的⼏项。 

2. 界⾯: Mac 的 273 个视图逐个有对应物;同⼀组演⽰数据下,每个⻚⾯的浅⾊和深⾊截图 与 Mac 的截图并排对照,布局、间距、颜⾊、圆⾓、字号、元素的有⽆和顺序⼀致;动画 按 Mac 的五组参数实现。 

3. 数据:⼀份真实的 Mac 备份能在 Windows 恢复,再从 Windows 导出后能在 Mac 恢复, 逐项核对⽆字段丢失。 

4. 逻辑: Mac 的 592 项 Core 测试逐项翻译到 C# ,输⼊和期望值不改,全部通过。 

### 允许的差异 

只有这些地⽅可以与 Mac 不同,其余⼀律按 Mac 做: 

|项⽬|Mac|Windows|
|---|---|---|
|提词岛形态|从刘海⻓出|⽆刘海的圆⾓药丸(Mac代码⾥已有这个分<br>⽀);不做「停在刘海上开始会议」|
|快捷键修饰键|⌘⇧|Ctrl+Alt,动作和默认键位⼀⼀对应|
|窗⼝按钮|左上⾓红绿灯|右上⾓系统按钮|
|字体|SF Pro、苹⽅、New York|随包的Inter、思源⿊体、Source Serif 4、思源<br>宋体|
|图标|SF Symbols|Fluent System Icons,逐个对应|
|系统语⾳识别|有|没有,选项不显⽰|
|翻译|系统离线翻译|⾛回答服务,界⾯写明⽂本会发出本机|
|权限引导|屏幕录制权限|没有这⼀步|



Page 1 of 42 

NextThought Windows 修复任务清单 

|项⽬|Mac|Windows|
|---|---|---|
|⽂案|「Mac」「钥匙串」「访达」<br>等|换成Windows的说法,其余⽂案逐字相同|
|旧版Office格<br>式|⽀持<br>`.doc`、<br>`.odt`、<br>`.rtf`|提⽰另存为新格式|



### 本轮不做 

- 代码签名、证书、安装包的系统信任。 Native AOT 。继续⽤ ReadyToRun 发布,但不得新增妨碍 AOT 的写法。 

- ARM64 、 GPU 加速识别。 

- 眼神矫正( Mac 没有这个功能)。 

- ⼿机端和中转服务的改动,除了任务组 F ⾥点名的那⼀处。 

Mac 侧的任何改动。 `Sources/` 、 `Tests/` 、 `scripts/` 、 `Resources/` 只读。 

## 2. 当前基线 

起点是分⽀ `fx/ed39491-mac-data` 的提交 `f5278f8` (版本 0.5.145 ),对照基准是 Mac 主分 ⽀的 `5ddb9e5` ( v3.3.0 )。在这个提交上我重新跑了测试和构建,并把前两轮审查的每⼀条逐条 对照了代码。 差距有多⼤ 

|项⽬|Mac v3.3.0|Windows 0.5.145|
|---|---|---|
|界⾯视图|273个|13个界⾯⽂件加3个⾃绘控件|
|Core测试|592项|⾃⼰写的866项全部通过;Mac的592项⾥约75项<br>有对应|
|界⾯⽂案|2,607条|字典⾥有2,356条,实际⽤到261条|
|快捷键动作|18个|对上17个,改过的键位保存不住|
|设置|6⻚,113个偏好键|1个⻓⻚⾯|
|整⻚缺失|—|⾸次引导、会前准备、我的声⾳、会中笔记、洞察⾯<br>板、快速设置|



Page 2 of 42 

NextThought Windows 修复任务清单 

### 上两轮问题的复核结果 

|领域|上次条数|已修|部分修|没动|这⼏轮新引⼊|
|---|---|---|---|---|---|
|Core逻辑|35|2|8|25|5|
|数据格式|23|3|14|6|12|
|平台与运⾏时|47|4|17|26|13|



数据格式的「部分修」⾥有 3 条是原问题修了、但修法带来了新问题。界⾯⽅⾯没有可⽐的条 数:四个主要界⾯⽂件与 1.3.7 逐字节相同,新增的⾏为⼤多只能⽤快捷键触发。 

### 已经修好、不⽤再动的 

- 转写⾏修订摘要的算法、伴侣加密的测试向量,与 Mac ⼀致。 重新⽣成纪要时保留 Mac 写的字段;校对不再改写转写原⽂。 

- 系列按 Mac 的⽅式⼀个系列⼀个⽂件;能读⼊ Mac 的资料⽽不崩溃。 

- 回答服务的密钥不再被误删。 

- 提词浮窗的⿏标穿透可以恢复,位置会检查是否在屏幕内。 

- 四个浮窗有防截屏;外观能跟随系统;会议列表的⽇期、时⻓、排序筛选与 Mac ⼀致。 

### 任务数量与顺序 

共 148 个任务:组 0 有 9 个, A 16 个, B 24 个, C 19 个, D 11 个, E 31 个, F 21 个, G 10 个, H 7 个。 E 组的每个任务对应⼀个完整⻚⾯或功能,⼯作量远⼤于其他组的单个任务。 

按这个顺序做: 

1. 整理:任务组 0 。 

2. ⽌损:会丢数据、会崩溃、会让 Mac 拒收备份的。 B1 、 B2 、 A1 ⾄ A5 、 C11 、 C12 、 F1 、 F4 、 F7 、 G1 。 

3. 地基: H1 、 H2 、 A15 、 D 组全部。 

4. 数据与逻辑: A 组和 B 组剩下的。 

5. 界⾯: C 组,然后 E 组。 E 组建议的先后: E25 、 E2 、 E4 、 E15 、 E18 、 E20 、 E3 ,再做其 余。 

6. 运⾏时与设置: F 组和 G 组剩下的, H 组剩下的。 

7. 验收:第 14 节。 

第 2 步做完之前,不要把 Windows 导出的备份交给 Mac ⽤户使⽤。 

Page 3 of 42 

NextThought Windows 修复任务清单 

## 3. 给 Agent 的提⽰词 

### 怎么⽤ 

- 先把这份清单导出为 Markdown ,存成⼯作分⽀上的 `Windows/FIX_TASKS.md` (任务 0- 1 )。 Agent 以仓库⾥的这个⽂件为准,并在⾥⾯更新状态。 

- ⼀次会话只交⼀批任务: A 、 B 、 F 、 G 组⼀次 3 到 8 个; D 组⼀次 2 到 3 个; C 组和 E 组⼀ 次⼀个编号。把编号填进提⽰词⾥「本次要做的任务」那⼀⾏。 C 、 D 、 E 组和 F 组的实机项要在 Windows 电脑上跑。在 Linux 上只能构建和跑测试,界 ⾯跑不起来,截图和实机验证做不了。 每批做完,⽤后⾯的复核提⽰词另开⼀个会话检查,再交下⼀批。 

### 执⾏提⽰词 

你要在 `OmnixSI/NextThought` 仓库⾥把 `Windows` 版修到与 `Mac` 版⼀致。 

##### 【⽬标】 

`Windows` 版( `Windows/` ⽬录, `C#` 加 `Avalonia` )的功能、⾏为、界⾯与 `Mac v3.3.0` (提交 `5ddb9e5` , `Sources/` ⽬录)⼀致。 

本轮不做:代码签名、 `Native AOT` 、 `ARM64` 、识别的 `GPU` 加速。 

只读⽬录: `Sources/` 、 `Tests/` 、 `scripts/` 、 `Resources/` 、 `CompanionRelay/` 。 `Mobile/` 只允许 任务 `F13` 点名的那⼀处改动。 

##### 【从哪⾥开始】 

⼯作分⽀是 `windows/parity` (没有就从 `fx/ed39491-mac-data` 新建)。只向这个分⽀提交和推 送。 任务清单是 `Windows/FIX_TASKS.md` 。先完整读第 `1` 节(范围)和第 `4` 节(⼯作规则),再读本次 任务所在的任务组。 

##### 【本次要做的任务】 

`<` 在这⾥填任务编号,例如:任务组 `0` 全部,然后 `B1` 、 `B2` 、 `A1` ⾄ `A5>` 

##### 【每个任务的做法】 

`1.` 打开该任务「 `Mac` 参照」列的 `Swift` ⽂件,从头读完。 `Swift` 源码是唯⼀标准:阈值、提⽰词、 ⽂案、字段名逐字相同;不重新设计,不简化,不合并。 

`2. Swift` 侧有对应测试的,先逐项翻译成 `C#` (同名⽂件、同名⽅法,输⼊和期望值⼀字不改),确认 它失败。 

`3.` 改 `C#` 代码直到测试通过。测试不过时改代码,不改测试。 

`4.` 界⾯任务:⽤ `--screenshots` ⽣成浅⾊和深⾊截图,与 `docs/screenshots/` ⾥的 `Mac` 截图 并排放进 `Windows/validation/screens/<` 视图名 `>/` ,逐项⽐对元素的有⽆、顺序、⽂案、间距、 颜⾊,有差别就继续改。 

`5.` 运⾏ `Windows/verify.ps1` 。这个脚本做出来之前:按锁定⽂件还原依赖、构建、两个测试⼯程全 部通过。 

`6.` ⼀个任务⼀组提交,提交说明以任务编号开头,例如 `A3: read Mac translations.jsonl` 。 

Page 4 of 42 

NextThought Windows 修复任务清单 

`7.` 在 `Windows/FIX_TASKS.md` 的对应⾏和相关跟踪⽂件⾥更新状态。 

##### 【状态怎么写】 

只有四种:未做;已实现、未验证;已验证;本版不做。 

- 「已验证」必须有 `Windows` 实机记录:⽇期、版本、操作步骤、结果、证据⽂件路径。程序内的⾃检 和单元测试通过,只能写「已实现、未验证」。 没有代码改动就不能改状态。不使⽤「可⽤」「已接通」「完备」「达标」这类词。 

##### 【硬性规则】 

- 不删除⽤户数据,不覆盖读不懂的⽂件:读失败时保留原⽂件并报错,不当成空值继续。 

- `-` 不写断⾔「与 `Mac` 不同的⾏为」的测试。发现这样的旧测试,删掉或改成 `Mac` 的期望值,并在提交 说明⾥写明。 

- 不为了指标关功能。不新增能改变正式⾏为的环境变量和启动参数。 

- 版本号保持 `0.x` 。 

- 清单没写⽽ `Mac` 有明确做法的,按 `Mac` 做。 

- `Windows` 做不到、清单也没给做法的,不要⾃⼰发明替代⽅案:在 `Windows/OPEN_QUESTIONS.md` ⾥写清楚卡在哪、 `Mac` 怎么做、有哪些可选做法,然后做下⼀个任 

##### 务。 

- 不要因为任务⼤就缩⼩范围,也不要做了⼀部分就标成完成。没做完的如实写进「未解决」。 

- `-` 如果运⾏环境不是 `Windows` :构建时加 `-p:EnableWindowsTargeting=true` ,测试照常跑;界 ⾯任务照做,截图和实机验证记为待办,状态写「已实现、未验证」。 

##### 【结束时】 

在 `Windows/PORTING_STATUS.md` 顶部按第 `4` 节的格式写本轮汇报。 最后的回复⾥列出:完成了哪些任务编号、每个任务的状态、测试数字(通过数和总数)、新翻译的 `Swift` 测试数、未解决的问题、需要⼈在 `Windows` 实机或 `Mac` 上做的事。 

Page 5 of 42 

NextThought Windows 修复任务清单 

### 复核提⽰词 

你要复核 `OmnixSI/NextThought` 仓库 `windows/parity` 分⽀上刚完成的⼀批任务。只检查,不改 代码。 

【要复核的任务】 

`<` 在这⾥填任务编号 `>` 

##### 【做法】 

任务清单是 `Windows/FIX_TASKS.md` 。对每个任务: `1.` 找出提交说明以该任务编号开头的提交,读完整的改动。 `2.` 打开「 `Mac` 参照」列的 `Swift` ⽂件,逐个函数对照 `C#` 的实现,列出每⼀处不同:阈值、⽂案、 字段名、分⽀条件、边界情况。 `3.` 检查「完成条件」是否逐字满⾜。要求翻译的 `Swift` 测试,逐项核对输⼊和期望值有没有被改动、 有没有漏掉的。 

`4.` 运⾏构建和全部测试,记下数字。 `5.` 检查有没有违反第 `4` 节的规则:删除或覆盖⽤户⽂件、断⾔偏离 `Mac` ⾏为的测试、新增的环境变 量或启动参数、没有实机记录却写「已验证」。 

`6.` 界⾯任务:打开 `Windows/validation/screens/` 下的截图,与 `Mac` 截图逐项对⽐。 

【输出】 

每个任务⼀段:结论(通过、不通过)、不通过的每⼀条写明⽂件和⾏号、 `Mac` 的做法、 `Windows` 现在 的做法。 最后给出需要返⼯的任务编号列表。不要写总体评价。 

## 4. ⼯作规则 

这些规则针对上两轮实际出过的问题,每⼀条都要遵守。提⽰词⾥已经包含它们的要点,这⾥是 完整版。 怎么做 

1. **Swift** 源码是唯⼀标准。 每个任务先打开「 Mac 参照」列的 Swift ⽂件通读,再改 C# 。照 着搬,不重新设计,不简化,不合并。阈值、提⽰词、⽂案、字段名逐字相同。 

2. ⼀个任务⼀组提交。 提交说明以任务编号开头,例如 `A3: read Mac` 

   - `translations.jsonl` 。不要把多个任务打包成⼀个⼤提交。 

3. 先写测试。 Swift 侧有对应测试的,先把那些测试逐项翻译过来(输⼊和期望值⼀字不 改),看到它失败,再改代码到它通过。测试不过时改代码,不改测试。 

4. 界⾯任务要对照截图。 ⽤演⽰数据、窗⼝ 1120×820 、 100% 缩放,浅⾊和深⾊各截⼀张, 与 Mac 的截图并排放进 `Windows/validation/screens/<` 视图名 `>/` 。 Mac 的截图在 `docs/screenshots/v3/` ,没有的⻚⾯列⼊第 15 节请⼈在 Mac 上补。 

Page 6 of 42 

NextThought Windows 修复任务清单 

5. 遇到清单没写的情况: Mac 有明确做法的,按 Mac ; Windows 没有对应能⼒、清单也没给 做法的,停下这个任务,记到 `Windows/OPEN_QUESTIONS.md` ,继续做下⼀个。不⾃⾏发 明替代⽅案。 

### 怎么算完成 

每个任务只有四种状态,记在对应的跟踪⽂件⾥: 

|状态|含义|
|---|---|
|未做|还没开始|
|已实现、未验证|代码合⼊,测试通过,但没有在Windows上实际操作<br>过|
|已验证|有实机记录:⽇期、版本、操作步骤、结果、证据⽂<br>件路径|
|本版不做|属于第1节「允许的差异」,并写明理由|



- ⽂档⾥不⽤「可⽤」「已接通」「完备」「达标」这类词,只⽤上表的四个状态。 状态只能因为代码或验证⽽改变。禁⽌「⼝径校准」:没有代码改动就不能改状态。 程序内的 `--xxx-check` ⾃检不算验证,只算「已实现、未验证」的依据。⾃检必须⾛真实 代码路径,不能⽤假的回调或写死的结果。 

- 「 Core ⽂件已移植」的条件是:与 Swift 逐函数对应、该⽂件的 Swift 测试全部翻译并通 过、并且已经被正式流程调⽤。三条缺⼀条都只能写「部分」。 

### 不许做的事 

- 不改 `Sources/` 、 `Tests/` 、 `scripts/` 、 `Resources/` 、 `Mobile/` 、 `CompanionRelay/` ,除⾮任务明确点名。 

- 不写断⾔「偏离 Mac 的⾏为」的测试;发现这样的旧测试就删掉或改成 Mac 的期望值。 不为了指标关功能(例如为了内存数字关掉 GPU 渲染)。 不在正式包⾥保留能改变⾏为的环境变量和启动参数;⾃检、演⽰、截图三个⼊⼝除外(⻅ H1 )。 

- 不把版本号改到 1.x 。对等验收通过前⼀直⽤ 0.x 。 不删除⽤户数据,不覆盖读不懂的⽂件:读失败时保留原⽂件并报错,不当成空值继续。 

### 每轮的汇报格式 

每完成⼀组任务,在 `Windows/PORTING_STATUS.md` 顶部加⼀节,只写这些: 

Page 7 of 42 

NextThought Windows 修复任务清单 

`## <` ⽇期 `> · <` 版本 `> ·` 任务 `<` 编号列表 `>` 改动: `<` 每个任务⼀⾏,写改了哪些⽂件 `>` 

测试: `Core <` 通过 `>/<` 总数 `>` , `NextThought.Tests <` 通过 `>/<` 总数 `>` ;新翻译的 `Swift` 测试 `<` 数量 `>` 

状态: `<` 每个任务的状态,四选⼀ `>` 

证据: `<` 证据⽂件路径 `>` 未解决: `<` 没做完的部分和原因,没有就写「⽆」 `>` 

5. 任务组 0 :分⽀与⽂档整理 代码和⽂档现在分散在三个分⽀上,彼此都不完整。开⼯前先合到⼀处,并把跟踪⽂件改到能⽤ 的状态。这⼀组不改任何程序⾏为。 

|分⽀|提交|有什么|缺什么|
|---|---|---|---|
|`fx/ed39491-mac-`|`f5278f8`|最新代码(版本0.5.145),307个提交的完整历|验收证据|
|`data`||史,开发者对上次审查的答复|⽂件;下<br>⾯两个分<br>⽀上的⽂<br>档|
|`port/windows-`|`a4dd248`|1.3.7的源码快照和验收证据|1.3.7之后|
|`native`(PR #30)||(<br>`Windows/validation/bundles/1.3.7/`)|的全部改<br>动;PR说<br>明还是早<br>期的内容|
|`fx/78-parity-`|`e30049c`|按Mac v3.3.0重写的<br>`FEATURE_PARITY.md`|没有代码|
|`baseline`(PR||(37⾏)、<br>`CORE_PARITY.md`,新增的|改动|
|#31)||`VIEW_PARITY.md`||



|编|任务|完成条件|
|---|---|---|
|号|||
|0-1|从<br>`fx/ed39491-mac-data` 建⼯作分⽀|分⽀存在,清单⽂件在分⽀⾥|
||`windows/parity`,之后所有任务都提交到这||
||⾥。把这份清单导出为Markdown,存成||
||`Windows/FIX_TASKS.md`||



Page 8 of 42 

NextThought Windows 修复任务清单 

|编<br>号|任务|完成条件|
|---|---|---|
|0-2|先跑⼀遍基线并记录:还原依赖、构建、两个测<br>试⼯程。预期Core测试866项、<br>`NextThought.Tests`122项全部通过|结果记⼊<br>`Windows/PORTING_STATUS.md`;之<br>后每个任务都以这个数字为起点,只<br>增不减(按B23删掉的错误测试除<br>外,删时注明)|
|0-3|从<br>`port/windows-native` 取回<br>`Windows/validation/bundles/1.3.7/` 下的⽂<br>档(含<br>`source-`<br>`notes/WINDOWS_PORT_DOCS/DECISIONS.md`)。<br>⼆进制⽂件不进仓库,它们在发布<br>`windows-`<br>`1.3.7-evidence` ⾥|⼯作分⽀⾥有这些⽂档,没有新增⼆<br>进制⽂件|
|0-4|从<br>`fx/78-parity-baseline` 取回三份⽂档。<br>`FEATURE_PARITY.md` ⽤那边的37⾏版本替换<br>(⼯作分⽀上的版本还在和Mac v2.3.1⽐),每⾏<br>的状态按第4节的四种状态重新填:没有实机记<br>录的⼀律不能写「已验证」|三份⽂档在⼯作分⽀;<br>`FEATURE_PARITY.md` 顶部写明对照<br>的是Mac v3.3.0(<br>`5ddb9e5`)|
|0-5|重新⽣成视图清单,合并成⼀份<br>`VIEW_PARITY.md`。⽤脚本从<br>`Sources/NextThought/` 扫出所有视图类型(现<br>在的<br>`UI_PARITY_INVENTORY.md` 有257⾏,漏<br>了16个带泛型参数的,实际是273个)。每⾏:<br>Swift⽂件、视图名、所属任务编号、Windows<br>对应⽂件、状态、截图路径。现有清单⾥「可映<br>射199」只是按⽂件名归类,整组被归到没有这些<br>内容的⻚⾯(引导归到⾸⻚、我的声⾳归到设<br>置、术语表归到资料库等),这些映射全部清空重<br>填|清单⾏数等于脚本扫出的视图数;每<br>⾏都有任务编号;状态列开始时全部<br>是「未做」,已有对应物的少数⼏个写<br>「已实现、未验证」|
|0-6|改正<br>`CORE_PARITY.md`:加三列(Swift测试<br>数、已翻译数、是否被正式流程调⽤);9个⽂件<br>改回「部分」(⻅B组末尾);改掉已经过时的说<br>法:测试数566、「运⾏时仍⽤写死的提⽰词」、<br>「原⼦重写transcript.jsonl」、「更新只打开下载<br>⻚」、「讲稿模式未接通」、「48个未引⽤」(现在<br>120个Core⽂件⾥有41个没有被程序⽤到)|⽂件⾥的每个数字都能⽤⼀条命令复<br>算出来,命令写在⽂件末尾|



Page 9 of 42 

NextThought Windows 修复任务清单 

|编<br>号|任务|完成条件|
|---|---|---|
|0-7|在<br>`REVIEW_RESPONSE_1370.md` 末尾加⼀节「更<br>正」,不改原⽂。要更正的说法:删除会议的清理<br>「已修」(这段代码只有⾃检和测试在调⽤,程序<br>⾥没有删除会议的⼊⼝);「Mac的19个动作都有<br>对应键」(Mac是18个,其中⼀个没有对应);<br>「会前准备的系列设置现在真正应⽤」(只在⾃检<br>⾥);测试数写的是863和114,实际是866和<br>122;字典条数前后两处不⼀致。<br>`Windows/README.md` 第⼀⾏还是「P4验收预览<br>· 0.4.0」,改成当前状态|「更正」⼀节存在;<br>`README.md` 第⼀<br>⾏与当前版本⼀致|
|0-8|跟踪⽂件定为这⼏份,其余的旧⽂档<br>(<br>`P1_VALIDATION.md` 到<br>`P4_VALIDATION.md`、<br>`P4_SECOND_PC.md`、<br>`UI_PARITY_INVENTORY.md`、<br>`UI_FIRST_BATCH.md`)移到<br>`Windows/archive/`:<br>`FIX_TASKS.md`(任务状<br>态)、<br>`PORTING_STATUS.md`(每轮汇报)、<br>`CORE_PARITY.md`、<br>`VIEW_PARITY.md`、<br>`FEATURE_PARITY.md`、<br>`SETTINGS_PARITY.md`、<br>`OPEN_QUESTIONS.md`、<br>`ICON_MAP.md`、<br>`WORDING.md`|`Windows/` 根⽬录下的⽂档只有这九<br>份加<br>`README.md`、<br>`REVIEW_RESPONSE_1370.md`、<br>`MAC_FIXTURE_REQUEST.md`、<br>`AOT_REPORT.md`、<br>`SCREEN_SHARE_FALLBACK.md`|
|0-9|新建<br>`Windows/UPSTREAM`,记下对照的Mac提<br>交<br>`5ddb9e5` 和版本v3.3.0。对照基准在这轮⼯作<br>中不变|⽂件存在|



## 6. 任务组 A :数据层与 Mac 互通 

上次审查的 23 条数据差异⾥,键名、枚举和系列的⽬录结构⼤多已经对上,但最新代码新增了 三条写⼊路径,写出的⽇期 Mac 读不了,其中两条会让 Mac 拒收整个备份( A1 )。这⼀组做完 之前, Windows 写出的数据不能交给 Mac ⽤户。路径省略前缀: `Core/` 、 `Platform/` 、 `Desktop/` 分别指 `Windows/src/NextThought.Core/` 、 `.Platform/` 、 `.Desktop/` ; Swift ⽂件不带⽬录的在 `Sources/NextThoughtCore/` ,带 `App/` 的在 `Sources/NextThought/` 。 

Page 10 of 42 

NextThought Windows 修复任务清单 

### 先做:会让 Mac 拒收的写⼊ 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|A1|所有进备份的⽂件⽤同⼀<br>套⽇期写法:ISO-8601、<br>UTC、不带⼩数秒、以<br>`Z`<br>结尾。现在有四处写出带<br>⼩数秒的⽇期:纪要的<br>`generatedAt`(直接把<br>`DateTimeOffset` 塞进<br>JSON节点,绕过了转换<br>器)、<br>`user-`<br>`content.json`、<br>`skills.json`、<br>`note-`<br>`templates.json`,以及<br>结构化纪要的载荷|`Platform/SessionStore.cs:118`;<br>`Core/SessionUserContent.cs:129`;<br>`Core/Skill.cs:56`;<br>`Core/NotesTemplateStore.cs`;<br>`Desktop/AppState.cs:327`|`SessionRecord.swift:325` ⾄<br>`:337`(<br>`JSONLines`);<br>`App/DataBackup.swift:355` ⾄<br>`:358`(读不了就整份拒收)|新增⼀项测试:跑<br>完⼀场演⽰会议、<br>校对⼀次、新建⼀<br>个技能、改⼀次模<br>板后,扫描<br>`Sessions/`、<br>`Workspaces/`、<br>`skills.json`、<br>`note-`<br>`templates.json`、<br>`glossary.json` ⾥<br>的每个⽇期字符<br>串,全部符合<br>`yyyy-MM-`<br>`ddTHH:mm:ssZ`|
|A2|`user-content.json` 不<br>得丢Mac的字段。<br>`editedNotes` 和<br>`editedSummary` 按原始<br>JSON保留,不映射成<br>Windows⾃⼰的类型;<br>保存⼀份带<br>`editedSummary` 的Mac<br>⽂件时不得去掉标题、问<br>题、模型|`Core/SessionUserContent.cs:9` ⾄<br>`:14`、<br>`:80`|`SessionUserContent.swift:45`<br>⾄<br>`:97`|⼀份含全部字段的<br>Mac⽂件,读⼊再<br>保存,除<br>`revision` 和<br>`updatedAt` 外逐字<br>节相同|
|A3|读Mac的<br>`translations.jsonl`。<br>Mac的⾏是<br>`{id, text,`<br>`inputRevision}`,现在<br>按Windows⾃⼰的<br>`SourceHash` 去匹配,永<br>远对不上;<br>`translatedText` 读进<br>来了但不显⽰;<br>Windows写出的⾏没有<br>`inputRevision`|`Core/Models.cs:22` ⾄<br>`:35`;<br>`Platform/SessionStore.cs:237`;<br>`Desktop/TranscriptRow.cs:15`|`SessionRecord.swift:49` ⾄<br>`:56`;<br>`App/SessionArchive.swift:502`<br>⾄<br>`:508`|导⼊⼀场带译⽂的<br>Mac会议,转写⻚<br>逐⾏显⽰译⽂;<br>Windows新写的⾏<br>带<br>`inputRevision`,<br>Swift能解码|
|A4|系列条⽬的历史记录按<br>Mac的17个字段写。现<br>在「替换」⼀条系列条⽬<br>时只写7个字段,缺的都<br>是Mac的必填项,该系<br>列在Mac上读不出来,<br>备份被拒收|`Core/WorkspaceSeriesItem.cs:69`;<br>`Core/WorkspaceSeriesChange.cs:191`<br>⾄<br>`:195`|`Workspace.swift:432` ⾄<br>`:451`|在Windows上替换<br>⼀条系列条⽬后,<br>系列⽂件能被Swift<br>的<br>`Workspace` 解码<br>(⻅A15)|
|A5|`summary.json` 任何时候<br>都是完整的。先⽣成结构<br>化纪要、后⽣成⽂字纪要<br>时,现在只写标题、正⽂<br>和结构化三项;补上<br>`questions`(空数组)、<br>`generatedAt`、<br>`model`。同时读写<br>`notes.json`,并读取<br>`structuredAll` ⾥的其<br>他格式|`Desktop/AppState.cs:234` ⾄<br>`:240`、<br>`:316` ⾄<br>`:340`;<br>`Platform/SessionStore.cs:106` ⾄<br>`:148`|`SessionRecord.swift:239` ⾄<br>`:244`|任何顺序⽣成纪要<br>后,<br>`summary.json` 都<br>能被Swift的<br>`SessionSummary`<br>解码|



Page 11 of 42 

NextThought Windows 修复任务清单 

### 标识与归属 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|A6|标识统⼀。写出时UUID⼀律<br>⼤写带连字符、键按字⺟排<br>序;⽐较时不区分⼤⼩写(解<br>析成<br>`Guid` 再⽐)。现在按原<br>样字符串⽐较,Mac把标识改<br>成⼤写后再导回,整个系列和<br>其中每条经验、每道题都会重<br>复⼀份,并且两个系列抢同⼀<br>个⽂件名。会议上下⽂⾥的资<br>料标识现在是32位⽆连字符,<br>Mac不认|`Core/Backup.cs:102`、<br>`:11`<br>`4` ⾄<br>`:120`、<br>`:189`;<br>`Core/WorkspaceSessionDel`<br>`etion.cs`;<br>`Desktop/AppState.Series.`<br>`cs`;<br>`Desktop/AppState.cs:909`<br>;<br>`Core/Models.cs:162`|`SessionContext.swift:84`|测试:Windows导出,把所<br>有标识改成⼤写后再导⼊,系<br>列数、经验数、题⽬数不变|
|A7|会议属于哪个系列按Mac记:<br>读写会议<br>`meta` ⾥的<br>`workspaceID` 和<br>`workspaceName`。现在⽤<br>Windows独有的<br>`sessionIds` 列表,导⼊的<br>Mac会议不属于任何系列,<br>Windows的会议在Mac上也<br>不属于任何系列。已有的<br>`sessionIds` 在⾸次读取时迁<br>移到<br>`meta`,之后不再写。系<br>列有改动时更新<br>`updatedAt`|`Core/Workspace.cs:104` ⾄<br>`:152`、<br>`:316` ⾄<br>`:369`;<br>`Core/Models.cs`|`SessionRecord.swift:160`<br>⾄<br>`:161`|导⼊Mac备份后,每场会议出<br>现在它原来的系列下;<br>Windows新建的会议写出的<br>`meta` 带这两个字段|
|A8|重写Mac的会议<br>`meta` 时不<br>得添加Windows的默认值<br>(现在会多出<br>`title:`新会<br>议、<br>`platform: windows`、<br>`inPerson: false`);追加提<br>问时同步更新<br>`meta.questionCount`;界⾯<br>判断纪要状态⽤<br>`ready`,现在<br>判断的是磁盘上不存在的<br>`saved` 和<br>`complete`|`Core/Models.cs:58`、<br>`:70`<br>、<br>`:71`;<br>`Platform/SessionStore.cs`<br>`:66` ⾄<br>`:71`;<br>`Desktop/RecentMeetingRow`<br>`.cs:25`、<br>`:30`;<br>`Desktop/AppState.Templat`<br>`es.cs:211`|`App/SessionArchive.swift`<br>`:520`|⼀份Mac的<br>`meta.json` 读⼊<br>再保存,没有新增键;提问数<br>与<br>`questions.jsonl` ⾏数相<br>等|
|A9|读转写时的容错按Mac:现在<br>只容忍最后⼀⾏损坏,中间任<br>何⼀⾏解析失败整场会议打不<br>开;说话⼈编号16及以上被丢<br>掉。<br>`evidence` 改成类型化读<br>取后,嵌套结构与Mac有⼀处<br>不同就会触发前⼀条,需要逐<br>字段对照|`Platform/SessionStore.cs`<br>`:235`、<br>`:296` ⾄<br>`:297`;<br>`Core/Models.cs:36` ⾄<br>`:54`|`App/SessionArchive.swift`<br>(读取部分);<br>`AnswerEvidence.swift`|⼀场中间有⼀⾏损坏的会议能<br>打开,损坏⾏被跳过并提⽰;<br>说话⼈0到31都保留|



Page 12 of 42 

NextThought Windows 修复任务清单 

### 技能、模板与修订号 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|A10|编辑技能或模板不得换标识、<br>不得丢字段。现在在⽂本框⾥<br>改技能会给每个技能⼀个新标<br>识,系列⾥引⽤的<br>`skillIDs`<br>全部失效;改模板会重新⽣成<br>标识并丢掉<br>`base`、<br>`kind`、<br>`isGap`、<br>`carry`。E组会把这<br>两个⽂本框换成Mac的编辑界<br>⾯,这⾥先保证存储层按标识<br>更新|`Desktop/AppState.Skills.`<br>`cs:56` ⾄<br>`:58`;<br>`Core/Skill.cs`;<br>`Core/NotesTemplateStore.`<br>`cs:46`、<br>`:53`|`Skill.swift`;<br>`NotesTemplates.swift`|改⼀个技能的正⽂后,它的标<br>识和<br>`importedAt` 不变;改⼀<br>个模板的段名后,其余字段不<br>变|
|A11|移植Mac写进磁盘的三个修订<br>号:<br>`transcriptSourceRevision`<br>、|`Core/SessionNotesEditor.`<br>`cs:14` ⾄<br>`:21`;<br>`Core/SessionProjectionBu`<br>`ilder.cs`;|`SessionTextProjection.sw`<br>`ift`;<br>`SessionEvidence.swift`|⽤A15的夹具:同⼀份转写,<br>C#和Swift算出的三个值相同|
||`structuredNotesRevision`<br>、<br>`claimFingerprint`。现在<br>没有移植,Mac会议在<br>Windows上重新⽣成纪要后<br>仍带着旧的Mac修订号。与<br>B6、B7⼀起做|`Core/SessionEvidenceBuil`<br>`der.cs`|||



### 备份 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|A12|导出的<br>`.ntbackup` 与Mac完<br>全同构。逐项:设置plist写出<br>`SettingsSnapshot.exporte`<br>`dKeys` 的全部键(现在只有11<br>个),<br>`webSearchMode` 的取值<br>⽤Mac的<br>`off`、<br>`auto`、<br>`always`,⽂件带XML声明和<br>DOCTYPE;资料按Mac的⽬<br>录结构写出,让Mac看得到<br>Windows的资料;<br>`sessionCount` 按Mac的算<br>法数;Windows独有的<br>`structured-*.md`、<br>`structured-*.json`、<br>`corrections.json`、<br>`summary-job.json`、<br>`*.bak`<br>不进备份|`Platform/BackupService.c`<br>`s:38`、<br>`:47` ⾄<br>`:54`;<br>`Platform/MacSettingsPlis`<br>`t.cs:35`;<br>`Desktop/AppState.cs:341`<br>⾄<br>`:343`;<br>`Desktop/AppState.Proofre`<br>`ad.cs:70`;<br>`Platform/SummaryService.`<br>`cs:31`|`Backup.swift:622`<br>(<br>`exportedKeys`);<br>`App/DataBackup.swift:62`<br>⾄<br>`:80`、<br>`:457`|A15的Swift校验⼯具对<br>Windows导出的备份逐⽂件<br>解码成功;压缩包⾥没有Mac<br>不认识的⽂件|



Page 13 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|A13|导⼊按Mac做合并。现在设<br>置、术语表、技能、模板四样<br>是直接覆盖,⽽且不在回滚范<br>围⾥,界⾯也不重新载⼊,下<br>⼀次保存⼜把刚恢复的设置冲<br>掉。改为:按Mac的规则合<br>并;失败时四样⼀起回滚;成<br>功后界⾯重新载⼊。⽀持带凭<br>据的备份(现在直接拒绝);恢<br>复<br>`Originals/`(现在只校验<br>不恢复);⼤⼩和数量上限与<br>Mac相同;导⼊的资料⽤系列<br>⽂件⾥的真实名称,不⽤<br>「Mac资料xxxx」|`Platform/BackupService.c`<br>`s:76`、<br>`:84`、<br>`:95`、<br>`:116`<br>⾄<br>`:160`、<br>`:179` ⾄<br>`:244`|`App/DataBackup.swift:182`<br>⾄<br>`:221`;<br>`BackupArchivePolicy.swif`<br>`t`|导⼊后不重启就能看到恢复的<br>设置和术语;⼈为让第三步失<br>败,四样都回到导⼊前;带凭<br>据的Mac备份能导⼊|
|A14|把已经写好但没接上的两个模<br>块接进正式流程并修掉遗留问<br>题:增量备份的<br>`Prune` 遇到<br>读不了的清单会删掉它引⽤的<br>对象,改为保留;<br>`BackupArchive` 现在写<br>`settings.json`,改为与A12<br>相同的内容。⾃动备份和恢复<br>点的界⾯在E组|`Core/IncrementalBackup.c`<br>`s:199` ⾄<br>`:202`;<br>`Core/BackupArchive.cs:31`|`IncrementalBackup.swift`<br>;<br>`BackupArchivePolicy.swif`<br>`t`;<br>`BackupContent.swift`|翻译Swift侧这三个⽂件的测<br>试并通过;<br>`Prune` 的测试⾥放<br>⼀个损坏的清单,对象不被删<br>除|



### 验收⼯具 

|编|任务|完成条件|
|---|---|---|
|号|||
|A15|做⼀个不依赖Mac的双向校验⼯具。Swift 6<br>的⼯具链可以在Windows上编译<br>`NextThoughtCore` 的⼤部分⽂件:开发记<br>录⾥已实测,80个⽂件有70个直接编译通<br>过;7个⽤到CryptoKit的⽂件换成swift-<br>crypto的同名模块即可;另外4个绑定平台<br>的⽂件(SQLite、Darwin、<br>CoreGraphics、语⾳识别)这个⼯具⽤不<br>到,排除。在<br>`Windows/tools/MacCodec/`<br>新建⼀个SwiftPM可执⾏程序,在它⾃⼰的<br>清单⽂件⾥按路径引⽤仓库⾥的|`emit` 的产物提交到<br>`Windows/tests/Fixtures/mac/`,C#测<br>试逐个读⼊再写出并与原⽂件⽐较;<br>`check` 对Windows跑完演⽰流程后的数<br>据⽬录和导出的备份全部通过,输出存到<br>`Windows/validation/data/`|
||`NextThoughtCore`,提供两个命令:<br>`emit`<br>`<`⽬录`>` ⽤Swift的类型和编码器写出每种⽂<br>件的样本(会议<br>`meta`、转写、译⽂、提||
||问、纪要、<br>`user-content.json`、系列、技||
||能、模板、术语表、备份清单);<br>`check <`⽬<br>录或备份`>` ⽤Swift的类型逐个解码并报告失<br>败的⽂件和字段。不改<br>`Sources/` 和根⽬录<br>的<br>`Package.swift`||



Page 14 of 42 

NextThought Windows 修复任务清单 

编 任务 完成条件 号 A16 ⽤真实的 Mac 备份做往返。夹具由⼈在 Mac Mac 导出 → Windows 导⼊ → Windows 上导出(第 15 节),请求清单已有: 导出 → Mac 导⼊, Mac 上会议数、系列 `Windows/MAC_FIXTURE_REQUEST.md` ,在其 数、经验数、题⽬数、术语数与最初相 中补三种情况:校对过的会议、 Windows 上 同,逐场会议能打开 建的技能、替换过的系列条⽬ 

7. 任务组 B : Core ⾏为与测试对⻬ 上次审查的 35 条 Core 差异⾥,最新代码修掉 2 条、部分修了 8 条, 25 条没动,另外新引⼊了 两处会丢数据的问题( B1 、 B2 )。路径省略前缀: C# ⽂件在 `Windows/src/NextThought.Core/` , Swift ⽂件在 `Sources/NextThoughtCore/` , Swift 测 试在 `Tests/NextThoughtCoreTests/` 。 先做:新引⼊的丢数据问题 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|B1|系列保存时不得删除没读成功<br>的⽂件。现在<br>`Load` 遇到解析<br>失败的<br>`Workspaces/*.json`<br>会跳过,<br>`Save` 随后删除所有<br>不在内存⾥的⽂件;<br>`Load` 遇<br>到其他异常返回空,下⼀次保<br>存会清空整个⽬录|`Workspace.cs:327` ⾄<br>`:370`|`WorkspaceStore.swift:280`|读失败的⽂件原样保留并记⼊<br>⽇志;<br>`Save` 只删除⽤户明确<br>删除的系列;写⼊先写临时⽂<br>件再替换。测试:⽬录⾥放⼀<br>个损坏的系列⽂件,读取再保<br>存后该⽂件字节不变|
|B2|术语表在设置⻚⼀进⼀出会丢<br>变体。<br>`ToText` 写「写法、变<br>体、变体」,<br>`ParseText` 按<br>Mac的四段格式读,把第⼆个<br>变体当成类型或备注|`Glossary.cs:594`<br>(<br>`ToText`)、<br>`:285`<br>(<br>`ParseText`);<br>`Desktop/AppState.Glossar`<br>`y.cs:31`、<br>`:44`|`Glossary.swift:534`<br>(<br>`parseText`)、<br>`:561`<br>(<br>`exportText`)|`ToText` 改成Swift的<br>`exportText`。删掉锁定错误<br>⾏为的测试<br>`ParseTextTakesPipesAndMe`<br>`rgesDuplicates`,翻译<br>`GlossaryTests.parseAndEx`<br>`portRoundTrip` 并通过|



### 数据安全相关 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|B3|删除会议时的清理按Mac完整<br>移植。现在题库条⽬会被清|`WorkspaceSessionDeletion`<br>`.cs`|`Workspace.swift:851` ⾄<br>`:1098`|翻译<br>`WorkspaceTests` ⾥<br>`deletingMeetingSourcesRe`|
||理,但来源被删后回答⽂本仍||(<br>`WorkspaceSourceIntegri`|`movesDependentKnowledgeAn`|
||保留,回答历史、提问次数、<br>系列变更候选、条⽬历史都没<br>处理;旧的单来源字段在有新<br>字段时被忽略||`ty.removeMeetings`)|`dKeepsOtherSources` 和<br>`explicitDeletionRetentio`<br>`n…` 等删除相关测试并通过|



Page 15 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|B4|备份合并按Mac完整移植。现<br>在仍是按ID取并集、本地总是<br>赢;删除标记只对已存在的系<br>列⽣效,新加⼊的系列不过滤|`Backup.cs:87` ⾄<br>`:164`|`Backup.swift:76` ⾄<br>`:586`<br>(<br>`mergeLessons`、<br>`mergeQuestionBank`、<br>`mergeSeriesItems`、<br>`mergeSeriesCandidates`、<br>`mergeCandidates`,同名系<br>列加「(导⼊)」后缀)|翻译<br>`WorkspaceTests` ⾥约<br>14项<br>`backupMerge*` 测试<br>(含<br>`backupMergeDoesNotResurr`<br>`ectDefaultDeletedSeriesCo`<br>`ntent`)并通过|
|B5|系列简报的证据检查改为默认<br>不放⾏|`SeriesBriefing.cs:79`、<br>`:`<br>`106`|`SeriesBriefing.swift:47`<br>⾄<br>`:123`|翻译<br>`SeriesBriefingTests`<br>12项并通过|
|B6|证据构建按Mac移植:⽀持程<br>度判定、论断指纹、结构化节<br>点的路径|`SessionEvidenceBuilder.c`<br>`s`、<br>`SessionNotesEvidence.cs`|`SessionEvidence.swift:14`<br>`6` ⾄<br>`:345`|翻译<br>`SessionEvidenceTests`<br>8项并通过|
|B7|投影构建按Mac移植:转写来<br>源修订号、纪要过期状态、可<br>搜索⾏|`SessionProjectionBuilder`<br>`.cs:73`|`SessionTextProjection.sw`<br>`ift:204` ⾄<br>`:486`|翻译<br>`SessionTextProjectionTes`<br>`ts`8项并通过;来源修订号不<br>再是空字符串|



### 纪要 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|B8|模板纪要的系统提⽰词逐字按<br>Mac:8条规则、会议⽇期、<br>含10个字段的输出格式、各段<br>的形状说明|`NotesTemplates.cs:490` ⾄<br>`:532`|`NotesTemplates.swift:582`<br>⾄<br>`:624`(<br>`organizeSystem`)|<br>删掉断⾔旧提⽰词的<br>`OrganizePromptListsSecti`<br>`onsAndSchema`;C#⽣成的提<br>⽰词与Swift对同⼀模板⽣成<br>的⽂本逐字相同(⽤夹具⽐较)|
|B9|恢复纪要的核对步骤,并把⻓<br>会议的分段归并接上,去掉1.2<br>万字的硬截断|`NotesTemplates.cs`;<br>`Desktop/AppState.Templat`<br>`es.cs:186` ⾄<br>`:207`|`NotesTemplates.swift:766`<br>⾄<br>`:795`<br>(<br>`NotesVerifier.verify`)<br>、<br>`:632` ⾄<br>`:648`<br>(<br>`partialUser`、<br>`mergeUser`)|翻译<br>`MeetingTypeAndNotesTests`<br>`.modelReplyIsParsedAndChe`<br>`cked` 并通过(期望丢弃2<br>条);⼀场3万字的演⽰会议能<br>⽣成完整纪要|
|B10|纪要的整形、容错解码、<br>Markdown渲染按Mac:条数<br>上限只对要点段、按议题分<br>组;接受字符串形式的布尔值<br>和数字时间;标题层级、事实<br>⾏、「待改进、答得好、待确<br>认」标记|`NotesTemplates.cs:374`、<br>`:404`、<br>`:435`|`NotesTemplates.swift:196`<br>、<br>`:456`、<br>`:820`|删掉<br>`TidyCapsDropsRepeatsAndO`<br>`neLineKeepsOne` 和<br>`VerdictsRenderWithTheBet`<br>`terAnswer`;翻译<br>`MeetingTypeAndNotesTests`<br>全部17项并通过|
|B11|补⻬纪要模型:<br>`bankEntries`、<br>`lessons(template:)`、计<br>数、<br>`named`、<br>`customCopy`、<br>整理提⽰⾥的回答段|`NotesTemplates.cs`|`NotesTemplates.swift:240`<br>附近及<br>`:626`|翻译<br>`reviewFeedsTheQuestionBa`<br>`nk` 并通过|
|B12|相对⽇期按本地⽇历⽇计算;<br>全⾓数字不得让解析抛异常|`NotesDates.cs:30`、<br>`:46`;<br>`Desktop/AppState.Templat`<br>`es.cs:207`|`NotesTemplates.swift:693`|翻译<br>`relativeDatesBecomeDates`<br>;增加⼀项「东⼋区早上7点<br>的会议,'明天'解析为次⽇」|
|B13|纪要提⽰词⾥的说话⼈标签按<br>Mac:现场发⾔标「现场」,不<br>是「对⽅」|`SessionSummaryPrompt.cs:`<br>`53`|`SessionRecord.swift:440`|翻译<br>`SessionRecordTests`9<br>项并通过|



Page 16 of 42 

NextThought Windows 修复任务清单 

### 术语、提问、导出及其他 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|B14|术语表的键规范化、热词、词<br>边界按Mac:<br>`strictKey` 转<br>⼩写并保留标点;<br>`looseKey`、<br>`tidy`;热词上限<br>80、误听优先、2到40字、<br>排除含冒号和逗号的词;<br>`hotwordString`;拉丁扩展字<br>符算作词字符;省略标记改为<br>「(另有N条未列出)」|`Glossary.cs:179` ⾄<br>`:280`、<br>`:513`|`Glossary.swift:143` ⾄<br>`:165`、<br>`:412` ⾄<br>`:481`、<br>`:489` ⾄<br>`:525`|逐项翻译<br>`GlossaryTests`18<br>项并通过,包括<br>`strictKey("`<br>`Open AI ") == "open ai"`<br>和热词的精确列表断⾔|
|B15|⼩组作业场景的提问阈值改为<br>0.55;补⻬候选跟踪器的<br>`Config`、<br>`reset`、<br>`noteAnswered`、<br>`confirm`、<br>可空的句⼦键|`QuestionIntent.cs:13`、<br>`QuestionCandidateTracker`<br>`.cs`|`QuestionIntent.swift:50`<br>、<br>`:302` ⾄<br>`:458`|翻译<br>`QuestionIntentTests`<br>8项、<br>`QuestionCorpusReportTest`<br>`s`5项(含召回率和误触发上<br>限的断⾔)、<br>`RealtimeRegressionTests`<br>9项到<br>`Core.Tests` 并通过|
|B16|字幕导出按Mac:译⽂⾏的写<br>法、⽆译⽂时的回退、过期标<br>记与计数、⽤校对后的⽂本、<br>⽤说话⼈名字|`SessionExport.cs:74` ⾄<br>`:101`|`SessionExport.swift:82`<br>⾄<br>`:105`|翻译<br>`SessionExportTests`<br>12项并通过|
|B17|知识候选的<br>`matchesCurrentSummary` 检<br>查、<br>`originID` 参数、来源清<br>单的时间精度|`WorkspaceKnowledge.cs:38`<br>`7`、<br>`:739`|`Workspace.swift:1171` ⾄<br>`:1185`|翻译<br>`WorkspaceTests` 其余测<br>试,39项全部有对应并通过|
|B18|系列的⾏为成员:<br>`effectiveMode`、带备注的<br>`materialText`、<br>`retrievalDocuments`(题库<br>作为可检索⽂档)、<br>`matchesDefaultTemplatePr`<br>`eferences`|`Workspace.cs:121` ⾄<br>`:151`|`Workspace.swift:36` ⾄<br>`:48`、<br>`:667` ⾄<br>`:830`|同B17|
|B19|幻灯⽚讲稿提⽰的摘录预算<br>(4000字、5条)和引⽤写法|`LiveInsights.cs:252`|`LiveInsights.swift:307`|翻译<br>`LiveInsightsTests`24<br>项并通过|
|B20|⽂档任务队列补上中断恢复和<br>删除⽇志;不得静默淘汰未完<br>成的任务;⽂件损坏时不得覆<br>盖。存储⽤JSON⽂件还是<br>SQLite都可以,这个⽂件不进<br>备份|`DocumentJobQueue.cs`|`DocumentJobs.swift:126`<br>⾄<br>`:466`|翻译<br>`DocumentJobTests`6项<br>并通过|
|B21|⼩的边界差异:追问判断去掉<br>的标点范围;去重时只去标点<br>不去符号;说话⼈名字按字素<br>簇截断;表意⽂字的判断范围|`QuestionIntent.cs:17`、<br>`QuestionDetector.cs:88`、<br>`TranscriptDeduplicator.c`<br>`s:104`、<br>`SpeakerClustering.cs:88`<br>、<br>`:186`|`QuestionIntent.swift:113`<br>、<br>`QuestionDetector.swift:6`<br>`8`、<br>`TranscriptDeduplicator.s`<br>`wift:90`、<br>`SpeakerClustering.swift:`<br>`97`|翻译<br>`QuestionDetectorTests`、<br>`TranscriptDeduplicatorTe`<br>`sts`(3项原样的输⼊)、<br>`SpeakerClusteringTests`<br>20项并通过|



Page 17 of 42 

NextThought Windows 修复任务清单 

### 测试对⻬ 

|编<br>号|任务|完成条件|
|---|---|---|
|B22|把592项Swift测试全部翻译过来。现在按字<br>符串重合度估计只有约75项有对应。每个<br>Swift测试⽂件对应⼀个同名的C#测试⽂<br>件,⽅法同名,输⼊和期望值不改。完全没有<br>对应的有:<br>`ProductArchiveUXTests`、<br>`StabilityTests`、<br>`EchoSuppressionTests`、<br>`VoiceActivitySegmenterTests`、<br>`MeetingEndWatchTests`(18项只有8项)、<br>`VoiceProfileTests`(16项)|`Windows/CORE_PARITY.md` 增加两列<br>「Swift测试数」「已翻译数」,每⾏两数<br>相等;合计592|
|B23|现有的866项C#测试保留,但凡是断⾔与<br>Swift不同⾏为的要删掉或改正|没有任何C#测试的期望值与对应Swift<br>测试⽭盾|
|B24|测试不得依赖机器时区和<br>`cmd.exe`。涉及本<br>地时间的测试显式传⼊时区|在UTC时区的机器上全部通过;进程相<br>关的两项测试在⾮Windows系统上改⽤<br>系统⾃带的命令或跳过并注明|



### 状态⽂件 

`Windows/CORE_PARITY.md` ⽬前把 `Workspace` 、 `Backup` 、 `Glossary` 、 `SessionTextProjection` 、 `SeriesBriefing` 、 `DocumentJobs` 、 `QuestionIntent` 、 `NotesTemplates` 、 `SessionEvidence` 标为「已移植」。在上表对应任务完成前,这 9 个⽂件 改回「部分」;⾏数列改成 Mac 主分⽀的实际⾏数。 8. 任务组 C :提词岛与浮窗 

提词岛是这个产品最有辨识度的部分,⽬前差距也最⼤: `CueIslandWindow.axaml` 从 1.3.7 到 现在没有改过,仍是⼀条只读的药丸。已经有的:三档宽度 440 、 540 、 660 ,译⽂⾏,随录⾳ 显⽰和隐藏,淡⼊淡出,防截屏。下⾯是缺的。 Windows ⽂件在 `Windows/src/NextThought.Desktop/Views/` , Mac 参照在 `Sources/NextThought/` 。 

Page 18 of 42 

NextThought Windows 修复任务清单 

### 提词岛 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|C1|窗⼝不抢焦点。加<br>`WS_EX_NOACTIVATE` 和<br>`WS_EX_TOOLWINDOW`,保持置<br>顶;点击、拖动岛上任何位<br>置,前台应⽤都不失去焦点。<br>现在只是去掉了<br>`Activate()`<br>调⽤,点⼀下仍会激活|`CueIslandWindow.axaml.cs`<br>`:68`|`CueIsland.swift:290`、<br>`:4`<br>`24`|实机:在记事本⾥打字时点岛<br>上的按钮,记事本的光标不消<br>失、输⼊不中断|
|C2|静⽌时⿏标穿透。岛收起时整<br>个窗⼝加<br>`WS_EX_TRANSPARENT`,点击<br>落到下⾯的窗⼝;⽤80毫秒<br>⼀次的指针轮询判断是否进⼊<br>岛的区域,进⼊后去掉穿透|`CueIslandWindow.axaml.cs`|`CueIsland.swift:174`、<br>`:3`<br>`21`、<br>`:375`|实机:岛收起时能点到它下⾯<br>的浏览器标签⻚|
|C3|悬停展开与离开收起。指针停<br>留0.22秒展开,离开0.2秒收<br>起;画布820×660,岛在画布<br>内变形,窗⼝本⾝不改⼤⼩|`CueIslandWindow.axaml`、<br>`.axaml.cs`|`CueIsland.swift:359`、<br>`:3`<br>`67`|指针快速划过不展开;停留后<br>展开;数值与Mac相同|
|C4|⼀直展开。加动作「提词岛⼀<br>直展开/恢复」(Mac的<br>`toggleCueOpen`),快捷键和<br>设置项都要有。现在的<br>`island` 快捷键只是显⽰和隐<br>藏|`CueIslandWindow.axaml.cs`<br>;<br>`MainWindow.axaml.cs:3125`<br>⾄<br>`:3145`|`CueIsland.swift:112` ⾄<br>`:121`;<br>`HotkeyManager.swift:10`|按⼀次保持展开,再按⼀次恢<br>复悬停⾏为|
|C5|各个阶段的形态:收起、听到<br>声⾳、正在听、有提词、启动<br>器。每个阶段的宽度、内容、<br>切换动画按Mac|`CueIslandWindow.axaml`|`CueIsland.swift:494` ⾄<br>`:552`(<br>`CueIslandView`)|与<br>`docs/screenshots/v3/isla`<br>`nd-cue.png`、<br>`island-`<br>`launcher.png` 并排对照⼀致|
|C6|展开后的⾯板和按钮:回答正<br>⽂、上⼀条和下⼀条、重新⽣<br>成、改短、复制、取消等,按<br>钮可点⽽其余区域仍穿透|`CueIslandWindow.axaml`|`CueIsland.swift:828`<br>(<br>`deck`)、<br>`:1287`<br>(<br>`IslandButton`)、<br>`:1322`<br>(<br>`IslandAction`)、<br>`:1375`<br>(<br>`ThinkingDots`)|与<br>`island-deck.png` 对照⼀<br>致;每个按钮都触发与Mac相<br>同的动作|
|C7|岛上的三类附加内容:讲稿当<br>前⻚的要点、教练提⽰、⾃动<br>结束倒计时。<br>`AppState` ⾥已<br>经有<br>`CoachHint` 和<br>`AutoEndNotice`,但界⾯没有<br>绑定|`CueIslandWindow.axaml`;<br>`Desktop/AppState.cs`|`CueIsland.swift:676`、<br>`:9`<br>`36`、<br>`:1005`|与<br>`island-slide.png` 对照<br>⼀致;演⽰模式下三类内容都<br>能出现|
|C8|双语回答在岛上的排版<br>(v3.3.0新增)。Core⾥的<br>`BilingualAnswer`、<br>`QuestionTurn` 现在没有被使<br>⽤|`CueIslandWindow.axaml`;<br>`Core/AnswerLanguage.cs`、<br>`QuestionTurn.cs`|`CueIsland.swift`;<br>`AnswerLanguage.swift`、<br>`QuestionTurn.swift`|与<br>`island-bilingual.png`<br>对照⼀致|
|C9|场景颜料描边和变形动画。岛<br>的边缘⽤当前场景的颜⾊;展<br>开收起⽤<br>`Motion.morph` 的弹<br>簧参数;系统关闭动画时不动|`CueIslandWindow.axaml`|`CueIsland.swift:466`、<br>`:5`<br>`97`;<br>`DesignSystem.swift:104`<br>⾄<br>`:117`|录⼀段展开和收起的视频放到<br>`Windows/validation/islan`<br>`d/`,与<br>`docs/screenshots/v3/isla`<br>`nd.gif` 节奏⼀致|
|C10|显⽰在哪块屏幕。按Mac的规<br>则选屏(有摄像头的内置屏优<br>先),可在设置⾥改;位置在该<br>屏顶部居中;多屏和不同缩放<br>⽐例下位置正确|`CueIslandWindow.axaml.cs`<br>`:38` ⾄<br>`:51`;<br>`MainWindow.axaml.cs:3482`<br>⾄<br>`:3489`|`CueIsland.swift:256`|实机:150%缩放的副屏上,<br>岛⽔平居中、贴顶|



Page 19 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|C11|关闭后不崩溃。⽤Alt+F4关<br>掉岛或字幕条后,字段仍指向<br>已关闭的窗⼝,下⼀个问题到<br>来时抛异常,⽽未处理异常会<br>直接结束程序。改为拦截关闭<br>(只隐藏),并在窗⼝真的关闭<br>时清空字段|`MainWindow.axaml.cs:3403`<br>、<br>`:3534`、<br>`:3568`;<br>`App.axaml.cs:22`|—|实机:Alt+F4关掉岛,再触发<br>⼀个问题,岛重新出现,程序<br>不退出|



Page 20 of 42 

NextThought Windows 修复任务清单 

### 字幕条 

|编号|任务|改哪⾥|Mac参照|完成<br>条件|
|---|---|---|---|---|
|C12|隐藏后不|`SubtitleBarWindow.axaml.cs:45`、<br>`:89`|`SubtitleBarController.swift`|实|
||得⾃⼰再<br>|⾄<br>`:96`;<br>`MainWindow.axaml.cs:3174`||机:<br>|
||出现。定<br>|||字幕<br>|
||时器在|||模式|
||`Hide()`|||下按<br>|
||之后还在|||「隐|
||跑,<br>`Tick`|||藏所|
||⾥会调|||有窗|
||`Show()`;|||⼝」,|
||加上新的|||有⼈|
||常驻模式|||继续|
||后,「隐藏|||说|
||所有窗⼝」|||话,|
||和结束录|||字幕|
||⾳都会在|||条保|
||100毫秒内|||持隐|
||被它重新|||藏|
||显⽰。改||||
||为:隐藏||||
||时停定时||||
||器;只有||||
||因为超时||||
||⾃动隐藏||||
||的才允许||||
||⾃动恢复||||
|C13|拖动改位|`SubtitleBarWindow.axaml`、<br>`.axaml.cs`|`SubtitleBarController.swift`|位置|
||置、重置||(<br>`panelMoved`、|重启|
||位置、指||`resetPosition`、|后保|
||针移上去||`setDimmed`);|留;|
||变淡;可||`SubtitleBarView.swift`;|截图|
||以放在任||Core的<br>`SubtitleBar.swift`|与|
||意屏幕;|||Mac|
||不抢焦点|||对照|
||(同C1);|||⼀致|
||排版逐项||||
||按Mac||||



Page 21 of 42 

NextThought Windows 修复任务清单 

### 提词浮窗 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|C14|内容和结构按<br>Mac:现在只显<br>⽰回答正⽂,缺<br>⻚眉、⻚脚、⼯<br>作台模式,以及<br>三种停靠位置<br>(屏幕顶部居<br>中、底部、右<br>侧)|`TeleprompterWindow.axaml`、<br>`.axaml.cs`|`TeleprompterController.swift:29`<br>(<br>`Placement`)、<br>`:245`<br>(<br>`TeleprompterView`)、<br>`showWorkbench`|三种位置各⼀张<br>截图,与Mac<br>对照⼀致|
|C15|透明度规则按<br>Mac:下限<br>0.35;选了「隐<br>藏背景」时窗⼝<br>保持不透明、只<br>有背景层变透<br>明。现在的淡⼊<br>淡出把透明度强<br>⾏设成1,并且<br>会把这个1存成<br>⽤户设置|`MainWindow.axaml.cs:3413` ⾄<br>`:3476`|`TeleprompterController.swift:170` ⾄<br>`:175`|把透明度调到<br>0.5,隐藏再显<br>⽰,仍是0.5;<br>重启后仍是0.5|
|C16|跟随阅读⾃动滚<br>动|`TeleprompterWindow.axaml.cs`;<br>`Core/ScrollFollowState.cs`|`ScrollFollowObserver.swift`;<br>`ReadingScrollMonitor.swift`;Core的<br>`ScrollFollowState.swift`、<br>`ScrollFollowInputClassifier.swift`|翻译Swift侧这<br>两个Core⽂件<br>的测试并通过;<br>实机:⼿动滚动<br>后暂停跟随,按<br>Mac的规则恢<br>复|
|C17|「隐藏所有窗<br>⼝」要把「提词<br>器已穿透/恢复<br>点击」那⼀⼩条<br>也隐藏;浮窗和<br>⼩条都不抢焦点<br>(同C1)|`MainWindow.axaml.cs:3175`;<br>`TeleprompterHandleWindow.axaml.cs`|`TeleprompterController.swift:184` 起|实机:穿透状态<br>下按「隐藏所有<br>窗⼝」,屏幕上<br>不留任何本应⽤<br>的窗⼝|



### 防截屏 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|C18|防截屏按Mac做成⼀个设置项|`WindowCaptureHiding.cs:25`;各窗⼝|`TeleprompterController.swift:178`|⾃检:开|
||(<br>`capturePrivacy`),打开时<br>作⽤于本应⽤的所有窗⼝,包<br>括主窗⼝和之后新开的窗⼝。<br>现在只作⽤于四个浮窗,主窗|的<br>`Opened`|⾄<br>`:182`(<br>`applyPrivacy`)|关打开时<br>每个窗⼝<br>的<br>affinity|
||⼝⾥的转写、回答、⼿机配对|||读回为|
||⼆维码在屏幕共享⾥都看得<br>⻅;也没有开关。|||17,关闭<br>时为0。|
||`SetWindowDisplayAffinity`|||实机验证|
||的返回值要检查,失败时在设<br>置⻚提⽰|||在第15<br>节|



Page 22 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|C19|屏幕共享检测降为兜底。Mac<br>没有这个机制。现在每2秒按<br>窗⼝标题猜测是否在共享,只<br>收起提词岛,⽽且任何标题⾥|`Platform/ScreenShareDetector.cs`;<br>`MainWindow.axaml.cs:3061` ⾄<br>`:3081`、<br>`:3398`|—|打开⼀个<br>标题为<br>「共享屏<br>幕」的记|
||带「共享屏幕」的窗⼝(浏览|||事本⽂|
||器标签、⽂件夹)都会让岛整<br>|||件,岛照<br>|
||场不出现。改为:只有C18的<br>系统调⽤失败时才启⽤;启⽤<br>时三个浮窗⼀起收起;去掉按<br>标题⼦串的判断,只保留按窗<br>⼝类名识别会议软件的共享⼯<br>具条|||常出现|



## 9. 任务组 D :设计系统与界⾯基础 

这⼀组先于 E 组做。 E 组的每个⻚⾯都建⽴在这⾥的字体、颜⾊、间距、动画、图标、控件和导 航外壳之上;基础不对,⻚⾯做出来也对不上截图。 Windows ⽂件在 `Windows/src/NextThought.Desktop/` , Mac 参照在 `Sources/NextThought/` 。 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|D1|截图⼯具。加启动参数<br>`--`<br>`screenshots <`⽬录`>`:⽤演<br>⽰数据依次打开每个⻚⾯,窗<br>⼝1120×820、100%缩放,浅<br>⾊和深⾊、中⽂和英⽂各截⼀<br>张后退出。⻚⾯清单与Mac的<br>截图脚本相同(⾸⻚、准备、<br>会中、会中纪要、回顾、⼯作<br>纪要、资料库、设置、设置的<br>纪要⻚),再加提词岛的五种形<br>态。演⽰数据逐条照搬Mac的<br>演⽰内容|新建<br>`Desktop/Screenshots.cs`;<br>`Desktop/DemoContent.cs`|`scripts/capture-`<br>`screenshots.sh:79` ⾄<br>`:91`;<br>`DemoMode.swift:371`<br>(<br>`populateDemo`)、<br>`:528`|⼀条命令⽣成全部截图到<br>`Windows/validation/scree`<br>`ns/`;演⽰数据的会议标题、<br>转写、回答与Mac截图⾥的⽂<br>字相同|
|D2|字体随包。把Inter、Noto<br>Sans SC、Source Serif 4、<br>Noto Serif SC的字体⽂件和许<br>可证放进安装包,现在只有<br>Inter的名字,仓库⾥没有任何<br>字体⽂件。字号表按Mac的⼗<br>级:hero 38衬线中粗、<br>display 30衬线、section 19<br>衬线、title 15半粗、prompt<br>22中粗、body 15、label 13中<br>粗、caption 12、overline 11<br>半粗、code 13等宽|新建<br>`Desktop/Assets/Fonts/`;<br>`Styles/Theme.axaml`|`DesignSystem.swift:69` ⾄<br>`:78`|在没装这些字体的⼲净<br>Windows上,标题是衬线<br>体,中⽂不回退到宋体或微软<br>雅⿊|
|D3|颜⾊逐个对上。按Mac的浅⾊<br>和深⾊两套值重写所有颜⾊资<br>源:<br>`sunken` 现在是3.1%,应<br>为4%;阴影颜⾊现在写在主<br>题字典外⾯,只有浅⾊值,深<br>⾊应为⿊⾊50%、40%、<br>32%。已定义但没⼈⽤的<br>`PromptPanel`、<br>`SealSoft`、<br>三种阴影要⽤到Mac⽤它们的<br>地⽅|`Styles/Theme.axaml:86` ⾄<br>`:87` 及全⽂件|`DesignSystem.swift:35` ⾄<br>`:63`|写⼀项测试读取资源字典,逐<br>个与Mac的⼗六进制值和透明<br>度⽐较,浅⾊深⾊全部相等|



Page 23 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|D4|间距与尺⼨。补⻬<br>`s12`<br>(48)、<br>`control`(32)、<br>`controlLarge`(40)、<br>`sidebar`(220)、<br>`content`<br>(720)和点击区域的尺⼨;<br>圆⾓8、14、22。⻚⾯⾥写死<br>的数字换成这些资源。侧栏现<br>在是222,⾸⻚920,设置<br>860,其余⻚⾯没有限宽,全<br>部改成Mac的值|`Styles/Theme.axaml`;各<br>`Views/*.axaml`|`DesignSystem.swift:83` ⾄<br>`:97`;<br>`ControlComponents.swift:`<br>`6`|在<br>`Views/` 下搜索不到写死的<br>间距、圆⾓数值(1像素的线<br>除外)|
|D5|动画。实现Mac的五组参数:<br>`hover`0.14秒、曲线(0.2,<br>0.8, 0.2, 1);<br>`expand` 弹簧<br>0.32 / 0.9;<br>`phase` 弹簧0.46 /<br>0.9;<br>`snappy` 弹簧0.28 /<br>0.78;<br>`morph` 弹簧0.5 /<br>0.82。系统关闭动画效果时全<br>部不动。现在只有翻⻚和浮窗<br>的160毫秒淡⼊、120毫秒淡<br>出,没有弹簧,也不看系统设<br>置|新建<br>`Desktop/Motion.cs`;<br>`MainWindow.axaml.cs:3413`<br>⾄<br>`:3476`|`DesignSystem.swift:104`<br>⾄<br>`:117`|每个⽤到动画的地⽅都引⽤这<br>五个之⼀;在系统⾥关闭「显<br>⽰动画」后界⾯没有任何过渡|
|D6|场景主题。每个场景的主⾊和<br>深⾊(浅⾊、深⾊两套)、图<br>标、⼀句承诺、三条说明,共<br>⼗种。现在只有九个浅⾊主<br>⾊,⽤在⾸⻚的6像素圆点<br>上。再做场景图标、标签、⽔<br>墨晕染背景三个组件|新建<br>`Desktop/SceneTheme.cs`、<br>`Views/Controls/SceneGlyp`<br>`h`、<br>`InkWash`|`Stage.swift:25` ⾄<br>`:71`、<br>`:77`<br>(<br>`SceneGlyph`)、<br>`:112`<br>(<br>`InkWash`)|测试逐个⽐较⼗种场景的四个<br>颜⾊值和⽂案;⾸⻚截图⾥场<br>景卡的颜⾊、图标、⽂案与<br>Mac⼀致|
|D7|图标。全部换成Fluent<br>System Icons,放进⼀个资源<br>⽂件;为Mac⽤到的每个SF<br>Symbol选⼀个对应图标并列<br>成对照表。现在只有11个<br>Segoe字形|新建<br>`Styles/Icons.axaml`、<br>`Windows/ICON_MAP.md`|全部<br>`Image(systemName:` 的<br>调⽤处|对照表覆盖Mac源码⾥出现的<br>每个符号名;界⾯⾥不再有⽂<br>字符号充当图标|
|D8|控件。按Mac的四种强调程度<br>(主要、次要、安静、破坏性)<br>做按钮;输⼊框、开关、菜<br>单、分段选择、标签、卡⽚同<br>样照搬;所有可聚焦控件都有<br>2像素、<br>`Ink.focus` 颜⾊的焦<br>点环(现在只有按钮有,1像<br>素,颜⾊是印章红);悬停和按<br>下的底⾊⽤<br>`hover`、<br>`pressed`|`Styles/Controls.axaml:77`<br>⾄<br>`:79` 及全⽂件|`ControlComponents.swift`<br>;<br>`DesignSystem.swift:119`<br>起的修饰符|做⼀个控件样张⻚(仅调试<br>⽤),浅⾊深⾊截图与Mac的<br>`LiveSessionAcceptanceCom`<br>`ponentSamples.swift` 渲染<br>结果对照⼀致|
|D9|恢复GPU渲染。删掉强制软件<br>渲染和<br>`NEXTTHOUGHT_GPU_RENDER`<br>环境变量。弹簧动画和透明浮<br>窗在软件渲染下达不到Mac的<br>流畅度|`Program.cs:32` ⾄<br>`:37`|—|启动⽇志显⽰使⽤硬件渲染;<br>C9的展开动画录屏⽆掉帧|



Page 24 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|D10|导航外壳改成Mac的结构。现<br>在是左侧七项的导航栏;Mac<br>是标题⾏中间⼀个㬵囊,⾥⾯<br>三个去处(⾸⻚、会议、资料<br>库),右侧是⼯具按钮和「重启<br>以更新」;会议和资料库各有⼀<br>个可收起的上下⽂侧栏;设置<br>是覆盖在上⾯的独⽴界⾯,不<br>是⼀个⻚⾯;录⾳时进⼊专注<br>模式。窗⼝的默认尺⼨和最⼩<br>尺⼨取Mac的值。标题⾏⾃<br>绘,系统按钮在右上⾓|`Views/MainWindow.axaml:4`<br>`4` ⾄<br>`:98`|`AppNavigationSidebar.swi`<br>`ft`(<br>`AppNavigationBar`);<br>`ContentView.swift:35` ⾄<br>`:47`、<br>`:182`<br>(<br>`FocusModeView`);<br>`Models.swift:83` ⾄<br>`:89`|⾸⻚截图的标题⾏与Mac⼀<br>致;㬵囊的选中态切换⽤<br>`snappy` 动画;侧栏收起状态<br>重启后保留|
|D11|深⾊模式全量检查。跟随系统<br>已经能⽤。D3完成后,把每个<br>⻚⾯的深⾊截图过⼀遍,凡是<br>写死浅⾊值的地⽅改成资源引<br>⽤|各<br>`Views/*.axaml`|—|深⾊截图⾥没有⽩底块、没有<br>看不清的⽂字|



## 10. 任务组 E :逐⻚逐功能补⻬ 

145 轮迭代主要是在快捷键和设置卡⽚后⾯加⾏为,界⾯结构没有动: `LiveView.axaml` 、 `LibraryView.axaml` 、 `MainWindow.axaml` 、 `CueIslandWindow.axaml` 与 1.3.7 逐字节相 同; Windows 现有 13 个界⾯⽂件加 3 个⾃绘控件, Mac 有 273 个视图。引导、会前准备、我 的声⾳、会中笔记、可拆分的模块、洞察⾯板、快速设置整个没有。 

#### 每个任务的完成条件相同: 

1. 「 Mac 参照」列的每个视图在 Windows 有同名的对应物(⽂件名、类型名与 Swift 视图相 同),并在 `Windows/VIEW_PARITY.md` ⾥标记。 

2. 该⻚⾯的浅⾊、深⾊截图与 Mac 截图并排放进 `Windows/validation/screens/<` 视图名 `>/` ,元素的有⽆、顺序、⽂案、间距⼀致。 

3. ⻚⾯上每个按钮和菜单项都接到与 Mac 相同的动作,在演⽰数据下逐个点过,步骤和结果 记⼊状态⽂件。 

4. ⻚⾯⾥的⽂案全部来⾃ Mac 的字符串表( G 组),不⾃⼰写。 

Windows ⽂件在 `Windows/src/NextThought.Desktop/` , Mac 参照在 `Sources/NextThought/` ,写法是「⽂件:视图或函数」。 

### 会前 

|编号|⻚⾯或功能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E1|⾸⻚|按标题猜场景(现在只在⾃检⾥<br>⽤);「继续系列」;截图卡⽚;<br>最近会议的排版;场景卡的图|`Views/HomeView.axaml:21` ⾄<br>`:41`|`HomeView.swift`:<br>`StartComposer`、<br>`ContinueSeries`、<br>`HomeScreenshotCard`、|
|||标、承诺、三条说明、晕染背||`RecentMeetings`、|
|||景、选中态;选场景后要设置⾃<br>动回答和回答⻓度||`OnboardingResumeRow`;<br>`Stage.swift`|



Page 25 of 42 

NextThought Windows 修复任务清单 

|编号|⻚⾯或功能<br>|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E2|会前准备<br>(整⻚没<br>有)<br><br><br><br><br><br>|准备⻚全部:类型选择、系列带<br>⼊卡、系列简报、资料区(导<br>⼊、⽂件⾏、资料洞察)、检查<br>卡(采⾳、场地、⾳源、回答、<br>⼿机)、开始按钮条。Core⾥的<br>`MeetingPlan`、<br>`MeetingPreparation`、<br>`MeetingCarry` 现在只有⾃检在<br>⽤,接进正式流程|新建<br>`Views/PrepareView.axaml`<br>及⼦视图;<br>`AppState` 加准备阶段<br>的状态|`PrepareView.swift`:<br>`PrepareView`、<br>`PrepareComposer`、<br>`TypePicker`、<br>`SeriesCarryCard`、<br>`SeriesBriefingPanel`、<br>`CarryRow`、<br>`MaterialsActions`、<br>`MaterialsBlock`、<br>`FileRow`、<br>`MaterialsInsight`、<br>`CheckCard`、<br>`AudioCheckRow`、<br>`VenuePicker`、<br>`AudioSourceMenu`、<br>`AnswerCheckRow`、<br>`PhoneCheckRow`、<br>`PreparationStartBar`;<br>`AppState+Prepare.swift`:<br>`beginNewMeeting`、<br>`setDraftCarry`、<br>`importDraftDocuments`、<br>`startPreparedMeeting`|
|E3|⾸次引导<br>(整个没<br>有)<br><br><br><br><br>|引导流程、设置清单、提词岛⽰<br>意、⽰例会议、声⾳录⼊⻚。去<br>掉屏幕录制权限那⼀步。Core<br>的<br>`OnboardingFlow`、<br>`OnboardingVersion` 已有,接<br>上|新建<br>`Views/Onboarding*.axaml`|`Onboarding.swift`:<br>`OnboardingView`、<br>`SetupChecklist`、<br>`SetupRow`、<br>`GuideRow`、<br>`CueIslandMock`;<br>`OnboardingSampleMeeting.swift`;<br>`MyVoiceViews.swift`:<br>`OnboardingVoicePage`|
|会中<br>编号|⻚⾯或功能|要补的内容<br>|改哪⾥|Mac参照|
|E4|会中主界⾯|顶部条(计时、⼿机和屏幕<br>图标、回答设置菜单);屏<br>幕上的暂停、重新⽣成、改<br>短、上⼀条、下⼀条(现在<br>只能⽤快捷键);候选问题<br>条;问题标记;证据⾏和证<br>据详情;联⽹状态和来源;<br>只记录模式的分栏;底部输<br>⼊框|`Views/LiveView.axaml`(重写)|`LiveSessionView.swift`:<br>`LiveTopBar`、<br>`PauseResumeButton`、<br>`AnswerSettingsMenu`、<br>`QuestionMark`、<br>`CandidateStrip`、<br>`NotesOnlyColumn`、<br>`WebStatusLine`、<br>`WebSources`、<br>`RefinementRow`、<br>`AnswerMoreMenu`、<br>`EvidenceLine`、<br>`EvidenceDetails`、<br>`LiveComposer`|
|E5|回答区的模块|回答历史⾯板(从归档读<br>取,现在只在内存⾥);检<br>测条、置信度条;截图卡<br>⽚;模块布局;模块拆成独<br>⽴窗⼝<br>|新建<br>`Views/Assistant*.axaml`、<br>`DetachedModule*.axaml`|`AssistantView.swift`:<br>`AnswerHistoryPanel`、<br>`DetectionStrip`、<br>`ConfidenceBar`、<br>`ScreenshotCard`;<br>`AssistantLayout.swift`;<br>`DetachedModules.swift`:<br>`DetachedModuleView`|
|E6|回答语⾔与双<br>语回答<br>(v3.3.0)|回答语⾔选择、双语回答的<br>显⽰。Core的<br>`BilingualAnswer`、<br>`QuestionTurn` 已有但没<br>被使⽤|`AppState.cs`;<br>`Views/LiveView.axa`|`ml`<br>`AnswerLanguage.swift`、<br>`QuestionTurn.swift`(Core)及其<br>在<br>`LiveSessionView.swift` ⾥的使<br>⽤处|
|E7|洞察⾯板|幻灯⽚区、屏幕区、回答进<br>度条<br>|新建<br>`Views/InsightsPanel.axaml`|`InsightsPanel.swift`:<br>`InsightsPanel`、<br>`SlideSection`、<br>`ScreenSection`、<br>`AnswerProgressStrip`|



### 会中 

Page 26 of 42 

NextThought Windows 修复任务清单 

|编号|⻚⾯或功能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E8|会中笔记|会中随⼿记、⾼亮;写⼊<br>`user-content.json`(依<br>赖A2)|新建<br>`Views/MeetingUserNotes.axaml`|`MeetingUserNotes.swift`:<br>`MeetingUserNotes`、<br>`toggleHighlight`|
|E9|讲稿与幻灯⽚<br>跟随|⾃动跟随幻灯⽚和讲话(现<br>在只有⼿动翻⻚,⾃动跟随<br>只在⾃检⾥);屏幕观察;<br>讲稿要点缓存;讲稿设置|`AppState.cs:1543` ⾄<br>`:1569`;<br>`MainWindow.axaml.cs:2211`|`AppState+Insights.swift`:<br>`followSlide`、<br>`followSpeech`、<br>`prepareSlideNotes`、<br>`startScreenWatch`;<br>`PresenterMode.swift`;<br>`SlideNotesCache.swift`;<br>`SettingsPresenter.swift`|
|E10|会中教练|摄像头视线提醒(请求摄像<br>头、校准)、教练⼩结。<br>Core的<br>`GazeEstimator`<br>已有。这是「看镜头」的提<br>醒,不是眼神矫正|`AppState.cs:1259`|`LiveCoach.swift`:<br>`GazeCamera`、<br>`requestCameraAndWatch`、<br>`calibrate`;<br>`CoachSummaryStore.swift`|
|E11|实时翻译的界<br>⾯|翻译开关、译⽂⾏的宿主、<br>听到外语时⾃动打开。翻译<br>本⾝⾛回答服务(允许的差<br>异)|`AppState.cs:1158`|`LiveTranslation.swift`:<br>`LiveTranslationToggle`、<br>`LiveTranslationHosts`、<br>`TranslationLaneHost`、<br>`turnOnForForeignSpeech`|
|E12|说话⼈与我的<br>声⾳|现在只有编号。补:命名、<br>⾓⾊、合并、「这是我」;说<br>话⼈条和编辑器;我的声⾳<br>的录⼊流程和设置。Core<br>的<br>`VoiceProfile` 已有|`Platform/MeetingVoiceTagger.cs`;新<br>建<br>`Views/Speaker*.axaml`、<br>`MyVoice*.axaml`|`SpeakerID.swift`:<br>`setVoiceName`、<br>`renameVoice`、<br>`mergeVoices`、<br>`setMeVoice`;<br>`SpeakerViews.swift`:<br>`SpeakerTag`、<br>`SpeakerStrip`、<br>`SpeakerEditor`、<br>`MeEditor`、<br>`MyVoiceSettings`;<br>`MyVoice.swift`;<br>`MyVoiceViews.swift`:<br>`MyVoiceSetupPanel`|
|E13|截图提问与屏<br>幕上下⽂|现在两种截图都只是导⼊资<br>料库。改为:截图后直接提<br>问;窗⼝选择器;区域选<br>择;提问时带上屏幕上下<br>⽂。同时修:临时图⽚不删<br>除、隐藏主窗⼝后弹窗失<br>败、窗⼝截图可能截到⾃⼰<br>的浮窗|`MainWindow.axaml.cs:3219` ⾄<br>`:3277`;<br>`ScreenCapture.cs:31`、<br>`:42`、<br>`:72`|`ScreenshotService.swift`:<br>`captureForReading`;<br>`ContentView.swift`:<br>`WindowPickerSheet`;<br>`ScreenRegionSheet.swift`;<br>`ScreenContextService.swift`:<br>`QuestionScreenContext`、<br>`refresh`|
|E14|联⽹搜索|现在密钥只能从环境变量<br>读。补:在设置⾥保存密<br>钥、开关、对已显⽰的回答<br>查证、来源列表|`AppState.cs`;设置⻚|`WebSearch.swift`:<br>`saveTavilyKey`、<br>`runWebCodex`、<br>`toggleWebAnswer`、<br>`lookUpDisplayedAnswer`、<br>`WebSourcesList`;<br>`AnswerServiceViews.swift`:<br>`WebSearchSettings`|



Page 27 of 42 

NextThought Windows 修复任务清单 

### 会后 

|编号|⻚⾯<br>或功<br>能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E15|会议<br>⻚|四个标签(纪<br>要、转写、回<br>答、个⼈笔<br>记);证据栏;<br>条⽬编辑框;<br>确认和评价徽<br>标;⽣成进度<br>⾏;会议信<br>息;提纲视图<br>(现在当<br>Markdown渲<br>染);重新⽣<br>成。从纪要跳<br>到转写时留在<br>会议⻚内(现<br>在会跳去会中<br>⻚)|`Views/NotesView.axaml`(重写);<br>`AppState.cs:759`|`NotesPageView.swift`:<br>`MeetingPage`、<br>`MeetingTab`、<br>`NotesProgressLine`、<br>`SessionEvidencePane`、<br>`NotesItemEditSheet`、<br>`ConfirmBadge`、<br>`VerdictBadge`;<br>`SessionInfoView.swift`;<br>`OutlineView.swift`;<br>`NotesRegeneration.swift`|
|E16|纪要<br>编辑|纪要编辑器和<br>证据侧栏;编<br>辑结果写⼊<br>`user-`<br>`content.json`|新建<br>`Views/MeetingNotesEditor.axaml`|`MeetingNotesEditor.swift`:<br>`MeetingNotesEditor`、<br>`MeetingEvidenceSidebar`|
|E17|校对<br>结果|校对后的转写<br>视图:逐条查<br>看和撤销修改|新建视图;<br>`AppState.Proofread.cs`|`NotesPageView.swift`:<br>`CorrectedTranscript`|
|E18|会议<br>列表|删除和批量删<br>除(B3的清理<br>现在没有任何<br>界⾯⼊⼝);按<br>内容搜索和命<br>中导航;按系<br>列分组和系列<br>管理菜单;归<br>档概览;回答<br>列表;归档的<br>截图;重命<br>名;会议操作<br>菜单。⽇期、<br>时⻓、排序筛<br>选已经与Mac<br>⼀致|`Views/HistoryView.axaml`|`HistoryView.swift`:<br>`deleteSession`、<br>`deleteSelectedSeries`、<br>`searchField`、<br>`searchResults`、<br>`searchHitNavigator`、<br>`seriesGroupHeader`、<br>`folderCard`、<br>`SeriesManagementMenuItems`、<br>`MeetingArchiveOverview`、<br>`SessionAnswersList`、<br>`ArchivedScreenshot`;<br>`MeetingActionsMenu.swift`|



Page 28 of 42 

NextThought Windows 修复任务清单 

|编号|⻚⾯<br>或功<br>能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E19|导出|导出对话框:|`AppState.cs:1659`、<br>`:1674`;|`SessionExportUI.swift`:|
|||格式、转写版|`Platform/PdfWriter.cs`|`SessionExportSheet`;|
|||本、语⾔三个||`SessionDocumentRenderer.swift`|
|||选择;Word和<br>PDF的排版与<br>Mac的渲染器|||
|||输出对照|||



### 资料库与系列 

|编号|⻚⾯或<br>功能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E20|系列编<br>辑器|基本信息、要求、⽂档<br>(每个系列⾃⼰的资<br>料)、复盘四个标签;<br>准备队列摘要;字段编<br>辑器;经验和题库可编<br>辑;系列的重命名、删<br>除、归档;资料删除|`Views/SeriesView.axaml`、<br>`LibraryView.axaml`(重写)|`WorkspacesView.swift`:<br>`WorkspaceEditor`、<br>`FieldEditor`、<br>`WorkspaceBasicTab`、<br>`WorkspaceRequirementsTab`、<br>`WorkspaceDocumentsTab`、<br>`ExperienceLessonRow`、<br>`BankRow`、<br>`ReviewTab`、<br>`PreparationQueueSummary`;<br>`WorkspaceActions.swift`|
|E21|知识复<br>核|系列变更候选卡(决<br>定、待办);记录标<br>签;资料复核框|新建视图|`KnowledgeReviewSection.swift`:<br>`SeriesChangeCandidateCard`;<br>`ProjectLibrarySupport.swift`:<br>`WorkspaceRecordsTab`、<br>`MaterialReviewSheet`|
|E22|术语表|现在是设置⾥的⼀个⽂<br>本框。改成Mac的术<br>语表界⾯:条⽬列表、<br>误听标记、在转写⾥查<br>找、从转写添加到术语<br>表;系列级术语表的叠<br>加|新建<br>`Views/GlossaryView.axaml`;<br>`AppState.Glossary.cs`|`GlossaryView.swift`:<br>`GlossarySettingsEntry`、<br>`GlossaryView`、<br>`MisheardMark`、<br>`TranscriptFindField`、<br>`AddToGlossarySheet`;<br>`AppState+Glossary.swift`|
|E23|⽂档处<br>理|⽂档运⾏时的安装、⽤<br>回答服务读⽂档、准备<br>队列、功能开启引导;<br>增强识别的开关(现在<br>没有任何⼊⼝);⽂档<br>模型的四个设置视图|`Platform/EnhancedOcrService.cs:28`;设<br>置⻚|`DocumentRuntimeInstaller.swift`;<br>`DocumentRuntime.swift`;<br>`CodexDocumentReader.swift`;<br>`DocumentPreparation.swift`;<br>`DocumentFeatureSetup.swift`;<br>`DocumentModelSettings.swift`|
|E24|检索|按系列限定范围;整份<br>资料阅读(v3.3.0)|`Platform/RetrievalService.cs`|`SemanticRetrieval.swift`(Core)及其<br>调⽤处|



Page 29 of 42 

NextThought Windows 修复任务清单 

### 设置与其他 

|编号|⻚⾯<br>或功<br>能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E25|设置<br>的结<br>构|现在是⼀个<br>滚动⻓⻚带<br>17个分段。<br>改成Mac的<br>覆盖层:左<br>侧184宽的<br>侧栏,六⻚<br>(场景、回<br>答与资料、<br>提词与教<br>练、纪要、<br>通⽤、⾼<br>级),每个设<br>置项归到<br>Mac所在的<br>那⼀⻚|`Views/SettingsView.axaml:22` ⾄<br>`:385`(重写)|`SettingsView.swift`:<br>`SettingsPage`、<br>`SettingsPageView`;<br>`SettingsPages.swift`|
|E26|场景<br>设置<br>与快<br>速设<br>置|每个场景的<br>默认值和领<br>域;快速设<br>置⾯板|新建视图|`SettingsPages.swift`:<br>`SceneSettingsPage`、<br>`SceneRow`;<br>`QuickSettings.swift`:<br>`QuickSettingsPanel`|
|E27|回答<br>服务<br>设置|预设管理<br>(现在是六<br>个固定项;<br>Mac可以新<br>增、重命<br>名、拉取模<br>型列表);推<br>理强度、快<br>速模式;引<br>擎测试⾏;<br>GitHub访<br>问|设置⻚;<br>`ViewModels/SettingsViewModel.cs`|`AnswerServiceViews.swift`:<br>`APIPresetManager`、<br>`CodexReasoningPicker`、<br>`CodexFastModeToggle`、<br>`EngineTestRow`、<br>`GitHubAccessSection`;<br>`APIPresets.swift`:<br>`addAPIPreset`、<br>`renameAPIPreset`、<br>`fetchAPIModels`|
|E28|技能|现在是⽂本<br>框。改成逐<br>个技能的编<br>辑器,⽀持<br>新建、删<br>除、安装⽰<br>例(依赖<br>A10)|`AppState.Skills.cs`;<br>`Views/SettingsView.axaml:195` ⾄<br>`:213`|`SettingsView.swift`:<br>`SkillSettings`、<br>`SkillEditor`;<br>`SkillLibrary.swift`:<br>`create`、<br>`delete`、<br>`installDemo`|



Page 30 of 42 

NextThought Windows 修复任务清单 

|编号|⻚⾯<br>或功<br>能|要补的内容|改哪⾥|Mac参照|
|---|---|---|---|---|
|E29|其余<br>设置<br>分组|洞察设置;<br>权限分组<br>(⻨克⻛、<br>摄像头;去<br>掉屏幕录<br>制);备份分<br>组;⾃动结<br>束的设置放<br>到Mac所在<br>的位置|设置⻚|`InsightSettingsView.swift`;<br>`SettingsGeneralData.swift`:<br>`BackupSettingsSection`、<br>`PermissionSettingsSection`|
|E30|备份<br>界⾯|⾃动备份、<br>恢复点、备<br>份列表;导<br>⼊前的复核<br>⻚(依赖<br>A13、A14)|新建视图;<br>`Platform/BackupService.cs`|`DataBackup.swift`:<br>`automaticBackup`、<br>`listBackups`、<br>`afterSession`;<br>`BackupRestoreReview.swift`:<br>`BackupRestoreReviewView`|
|E31|演⽰<br>模式|现在只有<br>`-`<br>`-demo` ⼀<br>种。按Mac<br>的分阶段演<br>⽰数据补⻬<br>(D1的截图<br>⼯具依赖它)|`DemoContent.cs`;<br>`AppState.cs:677`|`DemoMode.swift`:<br>`populateDemo`、<br>`populateDemoDraft`;<br>`DemoFilm.swift`|



做 E 组时⼀次做⼀个编号。每个编号开始前,先把「 Mac 参照」列的 Swift ⽂件从头读到尾, 列出其中所有视图和动作,表⾥没点名但⽂件⾥有的,也要做。 11. 任务组 F :采⾳、识别、⼿机伴侣、更新 

上次审查的 47 条运⾏时问题⾥,修掉 4 条、部分修了 17 条、 26 条没动,另有 13 条是这⼏轮新 引⼊的(多数在新加的云端识别⾥)。路径前缀同 A 组; Mac 参照在 `Sources/NextThought/` 。签名相关的问题(安装包没有签名、签名者校验靠环境变量开启) 本轮不做。 

Page 31 of 42 

NextThought Windows 修复任务清单 

### 采⾳ 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|F1|切换默认设备后录⾳会被旧设<br>备拖垮。新设备接上后,旧的<br>采集管道只是放进<br>`retired`<br>列表,没有停⽌:它的3秒缓<br>冲区写满后报「⾳频缓冲异<br>常」,拔掉旧设备时报「⾳频设<br>备意外停⽌」,两者都会结束整<br>场录⾳。改为:替换后⽴即停<br>⽌并释放旧管道,忽略它之后<br>的事件;只有⽤户选的是「系<br>统默认」时才跟随,⽤户明确<br>选过的设备不被覆盖|`Platform/AudioCaptureSer`<br>`vice.cs:157` ⾄<br>`:181`、<br>`:214`、<br>`:232` ⾄<br>`:244`|`MicrophoneCaptureService`<br>`.swift:112`、<br>`:169` ⾄<br>`:181`;<br>`AudioCaptureService.swif`<br>`t`|实机:录⾳中分别切换默认扬<br>声器和默认⻨克⻛,再拔掉旧<br>设备,录⾳继续60秒以上,<br>转写不断|
|F2|短暂卡顿不得结束录⾳。现在<br>三种情况会直接停掉整场会<br>议:采集线程间隔超过1.5秒<br>(当成休眠)、识别队列满100<br>帧、设备缓冲满3秒。按Mac<br>的做法处理中断和积压:能恢<br>复的恢复,积压时丢弃最旧的<br>⾳频并提⽰,不结束会议|`Platform/AudioCaptureSer`<br>`vice.cs:88`、<br>`:234`;<br>`Platform/SpeechRecognize`<br>`rSession.cs:144`、<br>`:162`|`AudioCaptureService.swif`<br>`t`;<br>`AppState+Audio.swift`|实机:录⾳中让电脑睡眠10秒<br>再唤醒,或让CPU满载5秒,<br>会议仍在进⾏|
|F3|停⽌时不丢尾部。停⽌时最多<br>只排空30帧,超出的⾳频被<br>丢弃。改为排到空为⽌,加总<br>时限|`Platform/AudioCaptureSer`<br>`vice.cs:189` ⾄<br>`:193`|`AudioCaptureService.swif`<br>`t`|说完最后⼀句话⽴刻按停⽌,<br>这句话出现在转写⾥|



### 云端识别(新加的代码) 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|F4|停⽌时最后⼀句丢失。<br>`DisposeAsync` 先取消再发送<br>结束消息,结束消息⽤的是已<br>取消的令牌,发不出去。改为<br>先发结束消息、等最终结果,<br>再取消|`Platform/CloudSttSession`<br>`.cs:106` ⾄<br>`:116`|`CloudStreamingTranscribe`<br>`r.swift`;Core的<br>`CloudTranscription.swift`|云端模式下说⼀句话后⽴刻停<br>⽌,这句话在转写⾥|
|F5|云端模式只转写⼀路声⾳。同<br>时采系统声⾳和⻨克⻛时,⻨<br>克⻛说的话全部消失。按Mac<br>的做法处理两路|`Desktop/AppState.cs:1008`<br>⾄<br>`:1010`|同上|云端模式下,对⽅和⾃⼰说的<br>话都出现在转写⾥,说话⼈标<br>注正确|
|F6|收发可靠性。接收端不看<br>`EndOfMessage`,超过16 KiB<br>的消息被截断后解析失败并触<br>发降级;发送是每帧⼀个不等<br>待的任务,没有上限;信号量<br>在还有等待者时被释放|`Platform/CloudSttSession`<br>`.cs:53`、<br>`:80` ⾄<br>`:88`;<br>`Platform/CloudSttClient.`<br>`cs`|同上|测试:分三⽚发来的⼀条40<br>KiB消息能正确解析;⽹络慢<br>时内存不增⻓|



Page 32 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|F7|地址和密钥。设置⽂件⾥的云<br>端地址不加校验就使⽤,<br>`ws://` 或任意主机都会收到密<br>钥和⾳频,改为只接受<br>`wss://` 且主机在Mac允许的<br>范围内;读密钥失败时返回空<br>字符串,随后每次开始录⾳都<br>会把空值存回去,等于删除密<br>钥(回答服务的密钥上⼀轮已<br>经修过同样的问题)|`Desktop/AppState.cs:892`<br>、<br>`:947`、<br>`:1800`、<br>`:1830`、<br>`:1919`;<br>`Platform/SettingsStore.c`<br>`s:38`|同上|测试:凭据读取抛错时,开始<br>录⾳不改变已存的密钥;<br>`ws://` 地址被拒绝|
|F8|⽂件转写。整个⽂件只有30<br>秒总时限,超时被吞掉,⻓录<br>⾳被静默截断;超过两个声道<br>的⽂件不做混⾳|`Platform/CloudSttClient.`<br>`cs:30`、<br>`:87`|同上|⼀段10分钟的录⾳能完整转<br>写;失败时有明确提⽰|
|F9|设置⻚的说明与⾏为不符。卡<br>⽚上写「实时录⾳留在本机」,<br>代码却会开启云端实时识别。<br>⽂案和开关逻辑都按Mac|`Desktop/Views/SettingsVi`<br>`ew.axaml:130`;<br>`Desktop/AppState.cs:948`|设置⾥云端识别那⼀组的<br>Swift视图|⽂案与Mac相同;关闭时没有<br>任何⾳频发出本机|



### ⼿机伴侣 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|F10|⼿机发来的动作⽴即应答。现<br>在要等整段回答⽣成完才返<br>回,⽽请求有8秒期限,⼿机<br>超时重试会取消并重新开始回<br>答。改为:先记下动作编号并<br>⽴即返回,再执⾏;相同编号<br>的重试不重复执⾏|`Platform/CompanionServer`<br>`.cs:94`、<br>`:132` ⾄<br>`:146`;<br>`Desktop/AppState.cs:2274`<br>⾄<br>`:2285`|`CompanionServer.swift`;<br>Core的<br>`Companion.swift`|实机:⼿机点「回答」⼀次,<br>电脑只⽣成⼀次回答|
|F11|状态同步完整。回答结束时没<br>有推送,⼿机⼀直停在「⽣成<br>中」;快照⾥的<br>`Position`、<br>`AnswerID`、<br>`Phase` 从来不<br>填;<br>`regenerate` 动作与<br>`answer` 完全⼀样|`Desktop/AppState.cs:515`<br>⾄<br>`:531`、<br>`:2305` ⾄<br>`:2334`|`CompanionServer.swift`;<br>`Companion.swift`|翻译Core的<br>CompanionBLETests、<br>CompanionCryptoTests并通<br>过;实机:⼿机上的状态、第<br>⼏条、上⼀条和下⼀条与电脑<br>⼀致|
|F12|服务的范围与⽣命期按Mac。<br>逐项对照:监听哪些地址段<br>(现在额外接受<br>100.64.0.0/10)、令牌怎么传<br>递、服务运⾏多久(现在启动<br>8⼩时后⾃动停⽌)、配对链接<br>⾥带什么。与Mac不同的地⽅<br>改成Mac的做法|`Platform/CompanionServer`<br>`.cs:24`、<br>`:47` ⾄<br>`:49`;<br>`Platform/CompanionRelayC`<br>`lient.cs`;<br>`Desktop/AppState.cs:2250`<br>⾄<br>`:2258`|`CompanionServer.swift`;<br>`CompanionRelay.swift`|写⼀份逐项对照表放进<br>`Windows/validation/compa`<br>`nion/`,每项注明Mac的做法<br>和Windows的做法,全部相<br>同|
|F13|蓝⽛备⽤通道。Windows不<br>能⾃定义⼴播名,⽽⼿机端按<br>`IH-` 开头的名字过滤设备,所<br>以现在整个通道是关闭的。这<br>是本清单唯⼀允许的⼿机端改<br>动:扫描时改为按服务标识过<br>滤,不再看名字。然后在<br>Windows上⽤<br>`GattServiceProvider` 实现<br>外设端|新建<br>`Platform/CompanionBlueto`<br>`oth.cs`;<br>`Mobile/Shared/Sources/Co`<br>`mpanionMobileCore/Bluetoo`<br>`thCentral.swift`;<br>`Mobile/Android/app/src/m`<br>`ain/java/com/nextthought/`<br>`companion/BluetoothTransp`<br>`ort.kt`|`CompanionBluetooth.swift`<br>;Core的<br>`CompanionBLEProtocol.swi`<br>`ft`|实机:关掉⽆线⽹络后,⼿机<br>通过蓝⽛连上Windows并收<br>到回答。改过的⼿机端仍能连<br>上Mac|



Page 33 of 42 

NextThought Windows 修复任务清单 

### 更新 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|F14|主线通道永远收不到滚动版<br>本。判断新旧时第⼀个参数传<br>的是空值,没有把发布⾥的<br>`update.json` 交给⽐较函数|`Desktop/UpdateFeed.cs:81`<br>;<br>`Core/UpdateCatalog.cs:13`<br>`6`|Core的<br>`UpdateCatalog.swift`;<br>`Updater.swift`|翻译<br>`UpdateCatalogTests`<br>并通过;⽤本地的假发布⽬录<br>测试:构建号更⾼的滚动版能<br>被发现|
|F15|后台检查在线程池⾥改界⾯属<br>性,错误被吞掉。改到界⾯线<br>程,失败时记⽇志|`Desktop/AppState.Update.`<br>`cs:66` ⾄<br>`:95`|`Updater.swift`:<br>`scheduleChecks`|断⽹时后台检查不抛异常、不<br>弹提⽰,⽇志⾥有⼀⾏|
|F16|更新的⽤户流程按Mac:后台<br>下载,下载好之后标题⾏出现<br>「重启以更新」按钮,点击后<br>安装并重新打开。现在后台检<br>查只是在状态⾏写⼀句话|`Desktop/AppState.Update.`<br>`cs:92`;标题⾏(D10)|`Updater.swift`:<br>`downloadUpdate`、<br>`installAndRelaunch`;<br>`AppNavigationSidebar.swi`<br>`ft:55`|⽤本地的假发布⽬录⾛完整流<br>程:发现、下载、按钮出现、<br>重启后版本号变化|
|F17|删掉绕过更新源限制的⼊<br>⼝:<br>`--update-check=<`地址`>`<br>启动参数和<br>`NEXTTHOUGHT_UPDATE_LAUNC`<br>`H` 环境变量。测试需要的假发<br>布源只在测试⼯程⾥注⼊|`Desktop/Views/MainWindow`<br>`.axaml.cs:88`、<br>`:208`;<br>`Platform/UpdateFeedPolic`<br>`y.cs`|—|正式构建的程序⾥搜不到这两<br>个字符串|



### 稳定性 

|编|任务|改哪⾥|完成条件|
|---|---|---|---|
|号||||
|F18|退出时等待收尾。|`Desktop/App.axaml.cs:54`;|录⾳中直接|
||退出事件⾥⽤了不<br>等待的异步处理,|`Desktop/AppState.cs:1144` ⾄<br>`:1147`|关闭窗⼝,<br>重新打开后|
||程序可能在会议写||这场会议完|
||盘完成前就退出。||整,<br>`meta`|
||改为:先取消这次||有结束时间|
||退出,等收尾完成|||
||后再真正退出;结<br>束会议时同样等所<br>有未完成的写⼊|||



Page 34 of 42 

NextThought Windows 修复任务清单 

|编<br>号|任务|改哪⾥|完成条件|
|---|---|---|---|
|F19|事件处理⾥的异常<br>不得结束程序。界<br>⾯的异步事件处理<br>函数没有异常保<br>护,任何⼀个抛错<br>都会变成未处理异<br>常。逐个加保护并<br>把错误显⽰在状态<br>⾏;全局的未处理<br>异常处理改为记录<br>后继续运⾏|`Desktop/Views/SettingsView.axaml.cs:35`、<br>`:10`<br>`4` 等所有<br>`async void`;<br>`Desktop/App.axaml.cs:22`|在<br>`Desktop/`<br>下搜索<br>`async`<br>`void`,每<br>⼀处都有异<br>常保护|
|F20|界⾯线程不做重<br>活。读会议列表和<br>单场会议时在界⾯<br>线程上同步等锁;<br>每场会议开始时在<br>界⾯线程上把资料<br>库每篇⽂档的全⽂<br>拼进<br>`context.json`。<br>改为后台执⾏;<br>`context.json` 的<br>内容按Mac的<br>`SessionContext`<br>来,不多存|`Platform/SessionStore.cs:206` ⾄<br>`:227`;<br>`Desktop/AppState.cs:906` ⾄<br>`:913`|资料库有<br>50篇⽂档<br>时,点「开<br>始」到开始<br>录⾳不超过<br>1秒,界⾯<br>不卡顿|
|F21|回答服务客户端⾥<br>写死的版本号<br>`0.2.0` 改为程序<br>的实际版本;解压<br>模型时使⽤系统⽬<br>录下<br>`tar.exe` 的<br>完整路径,不从<br>`PATH` 找|`Platform/CodexAnswerClient.cs:66`;<br>`Platform/ModelStore.cs:79`|代码检查|



Page 35 of 42 

NextThought Windows 修复任务清单 

## 12. 任务组 G :快捷键、设置项、本地化 

### 快捷键 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|G1|改过的快捷键保<br>存不住。设置的<br>视图模型在⽣成<br>要保存的设置时<br>没有带上快捷<br>键,⽽每次开始<br>录⾳和退出时都<br>会保存⼀次,结<br>果是快捷键被写<br>成空表。这个问<br>题的根源是「保<br>存时只写⾃⼰认<br>识的字段」,⼀并<br>改掉:保存时以<br>磁盘上的设置为<br>底,只覆盖本次<br>改动的字段|`Desktop/ViewModels/SettingsViewModel.cs`<br>(<br>`Build`);<br>`Desktop/AppState.cs:1714` ⾄<br>`:1740`|—|改⼀个快捷键,<br>开始并结束⼀次<br>录⾳,重启,改<br>动仍在|
|G2|动作集合与Mac<br>的18个⼀⼀对<br>应:补上「提词<br>岛⼀直展开/恢<br>复」(C4);去掉<br>Windows独有的<br>「显⽰主窗⼝」<br>(托盘图标已经<br>能做这件事);动<br>作的名称⽤Mac<br>的⽂案。默认键<br>位⽤与Mac相同<br>的字⺟,修饰键<br>按下表换算|`Desktop/Views/MainWindow.axaml.cs:3125`<br>⾄<br>`:3145`;<br>`Core/Shortcuts.cs`|`HotkeyManager.swift:6` ⾄<br>`:58`|测试:18个动作<br>的标识、名称、<br>默认按键逐个与<br>下表换算后的<br>Mac默认值相<br>同|
|G3|校验规则按<br>Mac:必须带修<br>饰键;两个动作<br>不能⽤同⼀组<br>键;系统注册失<br>败(被别的程序<br>占⽤)时在设置<br>⾥标出是哪⼀个|`Core/ShortcutBinding.cs`;<br>`Desktop/GlobalHotkeys.cs`|Core的<br>`ShortcutBinding.swift:12` ⾄<br>`:25`|翻译Swift侧快<br>捷键相关的测试<br>并通过;设成重<br>复键位时被拒<br>绝,提⽰⽂案与<br>Mac相同|
|G4|快捷键设置界⾯<br>⽤「按下即录⼊」<br>的录制控件,现<br>在是让⽤户打字<br>的⽂本框|设置⻚;新建录制控件|`ShortcutSettingsView.swift`;<br>`WorkspaceActions.swift:873`|截图与Mac对<br>照⼀致;录⼊、<br>清除、恢复默认<br>都能⽤|



#### 修饰键换算(字⺟键不变): 

|Mac|Windows|⽤在哪些动作|
|---|---|---|
|⌘⇧|Ctrl+Alt|⼤多数动作|



Page 36 of 42 

NextThought Windows 修复任务清单 

|Mac|Windows|⽤在哪些动作|
|---|---|---|
|⌘⌃ 加⽅向键|Ctrl+Alt加⽅向键|上⼀条、下⼀条回答|
|⌃⌥|Ctrl+Alt+Shift|切换会中窗⼝、讲稿上⼀⻚、讲稿下⼀⻚(⽤<br>Ctrl+Alt会与区域截图撞键)|



### 设置项 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|G5|设置项逐个对⻬。Mac有113个偏<br>好键。新建|`Platform/SettingsStore.cs`;<br>`Desktop/ViewModels/SettingsViewModel.cs`|`AppPreferences.swift`;<br>Core的|表⾥没有<br>「未|
||`Windows/SETTINGS_PARITY.md`,<br>每个键⼀⾏:键名、默认值、在<br>Mac哪个设置视图⾥、Windows的<br>状态。每个键在Windows上⽤相同<br>的键名、相同的默认值、相同的取<br>值范围,并且真正⽣效。属于「允<br>许的差异」的标「本版不做」||`Backup.swift:622`|做」;测<br>试逐个断<br>⾔默认值<br>与Mac<br>相同|
|G6|不认识的设置原样保留。从Mac备<br>份读到的、Windows⽤不上的键<br>(⽐如Mac的快捷键码),不解<br>释、不丢弃,导出时原样写回|`Platform/MacSettingsPlist.cs`;<br>`Platform/SettingsStore.cs`|—|Mac备<br>份导⼊再<br>导出,设<br>置plist<br>⾥的键⼀<br>个不少|



### 本地化 

现在的做法是界⾯加载后遍历控件树,⽤⼀个「中⽂整句到英⽂」的字典替换⽂字。字典⾥有 Mac 字符串表 2,607 条中的 2,356 条,但其中只有 261 条在 Windows 源码⾥真的出现;带占位 符的 246 条完全没有。 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|G7|字符串表从Mac的⽂件⾃动⽣<br>成,不⼿⼯维护。构建时读<br>`Localizable.xcstrings`,<br>⽣成全部2,607条,包括带占<br>位符和复数形式的;⼿写的<br>314条⾥与Mac重复的删掉,<br>Windows独有的留在单独的⽂<br>件⾥并补英⽂|`Core/UiText.cs`、<br>`Core/UiTextMac.g.cs`;新<br>建⽣成脚本|`Resources/Localization/L`<br>`ocalizable.xcstrings`|测试:字符串表的条数等于<br>Mac⽂件的条数;带占位符的<br>句⼦能正确填⼊参数|
|G8|取⽂案的⽅式改为显式调⽤:<br>界⾯⽂件⾥⽤标记扩展,代码<br>⾥⽤函数,都以Mac的中⽂原<br>句为键;去掉遍历控件树替换<br>⽂字的机制。切换语⾔后⽴即<br>⽣效;默认跟随系统语⾔,设<br>置⾥可以改|`Desktop/UiTranslation.cs`<br>`:14` ⾄<br>`:42`;各<br>`Views/*.axaml`|`SettingsGeneralData.swif`<br>`t` ⾥的语⾔分组|切换语⾔不需要重新打开⻚<br>⾯;系统是英⽂时⾸次启动即<br>为英⽂|



Page 37 of 42 

NextThought Windows 修复任务清单 

|编号|任务|改哪⾥|Mac参照|完成条件|
|---|---|---|---|---|
|G9|清掉写死的⽂字。现在有25处<br>⼯具提⽰、25处界⾯⽂字、<br>362条运⾏时消息⾥的216<br>条、70条拼接的句⼦不经过字<br>符串表。⽂案⼀律改⽤Mac的<br>原句;拼接的句⼦改成带占位<br>符的句⼦。回答提⽰词的附加<br>段和演⽰内容同样按Mac分语<br>⾔|`Desktop/` 全部源⽂件|Mac源码⾥的<br>`String(localized:` 调⽤|D1的截图⼯具在英⽂模式下扫<br>描每个⻚⾯的所有⽂字(含⼯<br>具提⽰),除⽤户数据外没有中<br>⽂字符|
|G10|Windows说法的替换表。只有<br>涉及系统名词的句⼦可以改<br>(例如钥匙串改为Windows<br>凭据管理器、访达改为⽂件资<br>源管理器),逐句列进<br>`Windows/WORDING.md`,中英<br>⽂都写|新建<br>`Windows/WORDING.md`|—|界⾯⽂案与Mac不同的每⼀句<br>都能在这张表⾥找到|



## 13. 任务组 H :⼯程结构 

结构问题不直接影响⽤户看到的东⻄,但它决定了 E 组这么⼤的量能不能做下去:现在 `AppState.cs` ⼀个⽂件承担了⼏乎全部逻辑, `MainWindow.axaml.cs` 有 3,675 ⾏,其中约 2,900 ⾏是⾃检代码。 H1 和 H2 在开始 E 组之前做,其余可以穿插。 

|编号|任务|改哪⾥|完成条件|
|---|---|---|---|
|H1|⾃检代码搬出主窗⼝。照Mac的<br>做法整理成⼀个⼊⼝<br>`--self-`<br>`test <`名字`>`,每项⾃检⼀个⽂<br>件,放在<br>`Desktop/SelfTest/`;<br>名字与Mac的40项⾃检能对应的<br>⽤相同的名字。现有的65个<br>`--`<br>`xxx=` 启动参数合并进来。能改变<br>正式⾏为的开关(更新源、渲染⽅<br>式、<br>`NEXTTHOUGHT_QA`)删掉,测<br>试需要的替⾝在测试⼯程⾥注⼊。<br>⾃检必须调⽤与⽤户操作相同的代<br>码路径|`Desktop/Views/MainWindow.axaml.cs:133`<br>⾄<br>`:3057`;<br>`Desktop/StartupOptions.cs`|`MainWindow.axaml.cs` 只剩窗⼝⾃⾝的<br>代码,少于400⾏;<br>`StartupOptions.cs` ⾥只有<br>`--self-`<br>`test`、<br>`--demo`、<br>`--screenshots` 三个<br>参数|
|H2|拆分<br>`AppState`。按Mac的扩展<br>⽂件拆成同名的部分类⽂件(例如<br>`AppState+Prepare.swift` 对应<br>`AppState.Prepare.cs`),每个⽅<br>法的名字与Swift相同。E组新增<br>的逻辑⼀律写进对应的⽂件,不再<br>往<br>`AppState.cs` ⾥加|`Desktop/AppState.cs`|`AppState.cs` 少于600⾏;给定⼀个<br>Swift⽅法名,能在同名的C#⽂件⾥找<br>到它|
|H3|系统调⽤集中到<br>`Platform`。<br>`Desktop` ⾥直接调⽤系统接⼝的<br>代码(截图、热键、窗⼝样式、防<br>截屏)移进<br>`Platform`,<br>`Desktop` 通过接⼝使⽤;<br>`Platform.Stub` 提供不依赖系统<br>的替⾝,让界⾯逻辑能在测试⾥运<br>⾏|`Desktop/ScreenCapture.cs`、<br>`GlobalHotkeys.cs`、<br>`Views/WindowCaptureHiding.cs` 等|`Desktop` ⼯程⾥没有<br>`DllImport` 和<br>`LibraryImport`|



Page 38 of 42 

NextThought Windows 修复任务清单 

|编号|任务||改哪⾥<br>完成|条件|
|---|---|---|---|---|
|H4|没被⽤到的 <br>个Core⽂件<br>调⽤。逐个<br>上;确实⽤|Core⽂件归位。120<br>⾥有41个没有被程序<br>写明由哪个E组任务接<br>不上的删除|`Windows/CORE_PARITY.md`<br>最终|验收时这个数字是0|
|H5|旧⼯程的去<br>`NextThoug`<br>WPF版)和<br>不再发布的<br>相关测试迁<br>⼈决定(第 <br>动|留。<br>`ht.Windows`(早期的<br><br>`NextThought.Cli`<br>话,从解决⽅案移除,<br>到新⼯程。是否移除由<br>15节),没有答复前不|`Windows/src/NextThought.Windows/`、<br>`NextThought.Cli/`<br>按答|复执⾏|
|H6|不新增妨碍 <br>的JSON读<br>射;系统调<br>`LibraryIm`<br>ReadyToRu|AOT的写法。新类型<br>写⽤源⽣成;不⽤反<br>⽤⽤<br>`port`。发布⽅式保持<br>n|全部新代码<br>`Win`<br>⾏数|`dows/AOT_JSON_WARNINGS.csv` 的<br>不增加|
|H7|⼀条命令完<br>在本机进⾏<br>有额度)。做<br>`Windows/v`<br>件还原依赖<br>程、跑A15<br>图,任何⼀|成本地验证。所有构建<br>(GitHub Actions没<br>⼀个<br>`erify.ps1`:按锁定⽂<br>、构建、跑两个测试⼯<br>的<br>`check`、跑D1的截<br>步失败就停|新建<br>`Windows/verify.ps1`<br>每轮|汇报⾥附这条命令的输出摘要|
|14.最<br>对应第<br>验收|终验收<br>1节的四<br>项|条定义。全部满<br>怎么判定|⾜才算「与Mac⼀样」。|谁来做|
|逻辑||Mac的592项 <br>过;<br>`CORE_PAR`<br>「已翻译数」;|Core测试全部有同名的C#测试并通<br>`ITY.md` 每⾏「Swift测试数」等于<br>没有未被程序⽤到的Core⽂件|Agent|
|数据|(⾃动)|A15的<br>`check` <br>录和导出的备份<br>读⼊再写出后与|对Windows跑完演⽰流程后的数据⽬<br>全部通过;⽤Swift写出的样本经C#<br>原⽂件相同|Agent|
|数据|(真实)|Mac导出→ Wi<br>导⼊,会议数、<br>最初相同,逐场|ndows导⼊→ Windows导出→ Mac<br>系列数、经验数、题⽬数、术语数与<br>会议能在Mac上打开|⼈,需要Mac|
|界⾯|(清单)|`VIEW_PARITY.`<br>⾊截图|`md` 没有「未做」;每⼀⾏都有浅⾊和深|Agent|
|界⾯|(对照)|逐⻚把Window<br>的有⽆和顺序、<br>词岛另看录屏:|s截图与Mac截图并排看⼀遍:元素<br>⽂案、间距、颜⾊、圆⾓、字号。提<br>展开、收起、悬停、穿透|⼈|



## 14. 最终验收 对应第 1 节的四条定义。全部满⾜才算「与 Mac ⼀样」。 

Page 39 of 42 

NextThought Windows 修复任务清单 

|验收项|怎么判定|谁来做|
|---|---|---|
|界⾯(英⽂)|英⽂模式下每个⻚⾯除⽤户数据外没有中⽂字符(G9<br>的⾃动扫描)|Agent|
|功能|`FEATURE_PARITY.md` 的37⾏、<br>`SETTINGS_PARITY.md` 的每个键都是「已验证」或<br>「本版不做」;18个快捷键动作逐个按过|Agent实现,⼈验证|
|实机|下⾯的实机清单逐项通过,记录放进<br>`Windows/validation/`|⼈|
|遗留|`OPEN_QUESTIONS.md` ⾥每⼀条都有答复并已处理;<br>`verify.ps1` 通过|Agent和⼈|



### 实机清单 

这些只能在真实的 Windows 电脑和设备上验证,⾃检和单元测试替代不了。每项记录⽇期、版 本、步骤、结果和证据。 

1. 防截屏:分别在 Zoom 、 Teams 、腾讯会议⾥共享整个屏幕,对⽅看不到主窗⼝、提词岛、 字幕条、提词浮窗;关掉开关后看得到。 

2. 不抢焦点与穿透:在会议软件⾥讲话时,点提词岛上的按钮,会议软件不失去焦点;岛收起 时能点到它下⾯的内容。 

3. 采⾳:录⾳中切换默认扬声器和⻨克⻛、拔掉⽿机、睡眠再唤醒,录⾳都继续。 4. ⻓会议:连续录 2 ⼩时,转写不断、内存不持续增⻓、结束后纪要正常⽣成。 

5. ⼿机伴侣:同⼀局域⽹、中转、蓝⽛三种⽅式各连⼀次,⼿机上的问题、回答、状态与电脑 ⼀致,⼿机发的每个动作都只执⾏⼀次。 

6. 多屏与缩放:主屏 100% 、副屏 150% ,提词岛、字幕条、提词浮窗的位置和⼤⼩正确,窗 ⼝拖到另⼀块屏后不模糊。 

7. ⼲净系统:在⼀台没装过开发⼯具和额外字体的 Windows 上安装,标题是衬线体,中⽂字 体正确,⾸次引导能⾛完。 

8. 更新:从上⼀个版本通过应⽤内更新升到新版本,数据和设置都在。 

### 不作为验收条件 

内存占⽤的具体数字、 Native AOT 能否发布、安装包是否签名,这三项本轮不看。 

Page 40 of 42 

NextThought Windows 修复任务清单 

## 15. 需要⼈来做或来定的事 

### 要在 Mac 上做的 

|事项|⽤在哪⾥|怎么做|
|---|---|---|
|导出夹具备份|A16、最终验收|按<br>`Windows/MAC_FIXTURE_REQUEST.md` 导出,另加<br>三种情况:校对过的会议、替换过的系列条⽬、带凭<br>据的备份|
|补Mac截图|所有界⾯任务|`docs/screenshots/v3/` 现在只有15张,多数⻚⾯没<br>有深⾊版。⽤已安装的应⽤运⾏<br>`APPEARANCES="light dark" scripts/capture-`<br>`screenshots.sh`,不需要重新构建或签名。脚本没覆<br>盖的⻚⾯⼿⼯截:会议⻚的四个标签、系列编辑器的<br>四个标签、设置的六⻚、术语表、⾸次引导的每⼀<br>步、导出对话框、备份复核⻚、字幕条、提词浮窗的<br>三种位置|
|最后⼀步导回|最终验收|把Windows导出的备份导⼊Mac,核对数量并逐场<br>打开|



### 要在 Windows 实机上做的 第 14 节的实机清单⼋项。 Agent 做完对应任务后状态只能到「已实现、未验证」,由⼈验证后 才改为「已验证」。 

### 要决定的 

|问题|我的建议|影响哪些<br>任务|
|---|---|---|
|拉取请求怎么处理|关掉#30和#31,从<br>`windows/parity` 开<br>⼀个新的草稿请求,说明⾥链接这份清单|组0|
|旧的WPF⼯程和命令⾏⼯程是否删除|删除。新界⾯已经取代它,留着会让⼈改<br>错地⽅|H5|
|随包字体让安装包变⼤(两套中⽂字体<br>⼤约增加30到40 MB)|接受。不带字体,标题和中⽂在不同电脑<br>上⻓得不⼀样,与Mac对不上|D2|
|默认快捷键⽤Ctrl+Alt|接受。部分显卡驱动占⽤Ctrl+Alt加⽅向<br>键来旋转屏幕,注册失败时G3会在设置<br>⾥提⽰,⽤户可以改|G2|



Page 41 of 42 

NextThought Windows 修复任务清单 

|问题|我的建议|影响哪些|
|---|---|---|
|||任务|
|⼿机端改为按服务标识扫描|同意。这是蓝⽛通道在Windows上能⽤|F13|
||的前提,改后⼿机端仍能连Mac||



### 留意 

- Mac 的蓝⽛⼴播名以 `NT-` 开头,⽽⼿机端按 `IH-` 过滤 ( `Sources/NextThought/CompanionBluetooth.swift:71` )。这是 Mac 侧的问题,不在 本清单范围内; F13 的⼿机端改动做完后它也随之解决。 对照基准固定在 `5ddb9e5` 。 Mac 主分⽀在这轮⼯作期间如果有新提交,先不跟,做完这份 清单再统⼀看差异。 

Page 42 of 42 

