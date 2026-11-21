# Windows 文案对照（G10）

这份表记录 Windows 版与 Mac 版**文案不同**的每一句。英文与中文都写；相同文案的句子
直接走 Mac 的 `Localizable.xcstrings`（由 `Windows/scripts/generate_ui_text.py` 生成
`UiTextMac.g.cs`，2,607 条），不在这张表里重复。Mac 是唯一标准：只有涉及系统名词的
句子可以改（钥匙串→凭据管理器、访达→文件资源管理器等），其余差异都是 Windows 移植过程
中的说明性文案（Windows 独有功能或 Windows 能力差异），在此逐句列出。

- 生成时间：2026-11-21
- 生成方式：`python Windows/scripts/localization_scan.py --write`
- Windows 独有/改写文案：**1204** 句（下表逐条）
- Mac 共享文案：2,607 条，由生成的字符串表提供，文案逐字相同

## 1. 系统名词替换（允许与 Mac 不同）

| Mac 文案 | Windows 文案 | English | 位置 |
|---|---|---|---|
| 钥匙串 | 凭据管理器 | Keychain → Credential Manager | 密钥保存说明（设置 ▸ 高级、API Key 状态行） |
| 访达 | 文件资源管理器 | Finder → File Explorer | 打开数据目录 / 打开文件位置 |
| 这台 Mac / Mac | 这台电脑 / Windows | Mac → Windows | 帮助文案里指代系统的地方 |
| ⌘⇧ / ⌘⌃ / ⌃⌥ 等快捷键符号 | Ctrl+Alt / Ctrl+Alt+Shift | Mac shortcut glyphs → Windows keys | 全部快捷键与提示 |
| 系统语音识别 | 本机语音识别（不含系统识别） | System speech → on-device speech | 声音来源 / 引导第 2 步 |
| 系统翻译框架 | 当前回答服务（文本会离开本机） | System translation → answer service | 说话人与翻译设置 |
| 屏幕录制权限 | 无（Windows 不需要这一步） | Screen Recording permission → none | 首次引导 / 权限说明 |
| Menu 栏 / 菜单模式 | 主窗口 / 托盘图标 | Mac menu bar → Windows tray | 工作台与托盘入口 |
| Apple Speech 语言资源 | 随包的 X-ASR 模型 | Apple Speech assets → bundled X-ASR model | 本机语音模型状态 |
| 旧版 Office 格式 .doc/.odt/.rtf | 提示另存为新格式 | legacy Office formats → save as a new format | 导入资料 |

## 2. Windows 独有 / 改写文案逐句对照

「Mac 原文」一列能对应的句子是从 Windows 文案反推系统名词后，在 Mac 字符串表里命中
的同一句；「—」表示 Mac 版没有这句（Windows 独有功能或能力差异说明）。

| # | Windows 中文文案 | Windows 英文文案 | Mac 原文 | 位置 |
|---|---|---|---|---|
| 1 | 、 | ,  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:866 |
| 2 | 宽 | Wide | 宽 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:521 |
| 3 | 我 | Me | 我 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 4 | 段 | segment | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 5 | 窄 | Narrow | 窄 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:519 |
| 6 | 译 | Translate | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 7 | Q： | Q:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:206 |
| 8 | 上移 | Move up | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 9 | 下移 | Move down | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 10 | 中文 | Chinese | 中文 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 11 | 会议 | Meetings | 会议 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 12 | 依据 | Basis | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 13 | 保存 | Save | 保存 | Windows/src/NextThought.Desktop/Views/EditTextDialog.axaml |
| 14 | 全部 | All | — | Windows/src/NextThought.Desktop/AppState.cs:295 |
| 15 | 关于 | About | — |  |
| 16 | 关闭 | Close | 关闭 | Windows/src/NextThought.Desktop/Views/CorrectedTranscriptView.axaml |
| 17 | 分钟 | minutes | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 18 | 删除 | Delete | 删除 | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml |
| 19 | 刷新 | Refresh | 刷新 | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml |
| 20 | 区域 | Region | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:576 |
| 21 | 历史 | History | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 22 | 原文 | Original | — | Windows/src/NextThought.Desktop/AppState.cs:214 |
| 23 | 双语 | Bilingual | — | Windows/src/NextThought.Desktop/AppState.cs:214 |
| 24 | 取消 | Cancel | 取消 | Windows/src/NextThought.Desktop/Views/AddToGlossarySheet.axaml |
| 25 | 名字 | Name | — |  |
| 26 | 听课 | Class | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:40 |
| 27 | 回答 | Answer | 回答 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 28 | 回顾 | Review | — |  |
| 29 | 场地 | Room | — |  |
| 30 | 复制 | Copy | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 31 | 复核 | Review | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 32 | 外观 | Appearance | 外观 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 33 | 客户 | Client | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:37 |
| 34 | 导入 | Import | — |  |
| 35 | 导出 | Export | — |  |
| 36 | 应用 | Apply | — |  |
| 37 | 待办 | Todo | 待办 | Windows/src/NextThought.Desktop/OnboardingSample.cs:44 |
| 38 | 待机 | Idle | — | Windows/src/NextThought.Desktop/AppState.Live.cs:859 |
| 39 | 忽略 | Ignore | 忽略 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 40 | 悬停 | Hover | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 41 | 截图 | Screenshot | 截图 | Windows/src/NextThought.Desktop/Views/Assistant/ScreenshotCard.axaml |
| 42 | 打开 | Open | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml.cs:138 |
| 43 | 技能 | Skills | 技能 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 44 | 按下 | Pressed | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 45 | 搜索 | Search | — |  |
| 46 | 摘要 | Summary | — |  |
| 47 | 改短 | Shorten | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 48 | 数据 | Data | 数据 |  |
| 49 | 文件 | Files | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml.cs:148 |
| 50 | 文本 | Text | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:66 |
| 51 | 新建 | New | — |  |
| 52 | 更新 | Update | 更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 53 | 查看 | Open | 查看 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 54 | 查证 | Verify | — |  |
| 55 | 标准 | Regular | 标准 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:520 |
| 56 | 检查 | Checks | — |  |
| 57 | 模式 | Mode | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:183 |
| 58 | 洞察 | Insights | — | Windows/src/NextThought.Desktop/Views/InsightsPanelWindow.axaml |
| 59 | 浅色 | Light | 浅色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:510 |
| 60 | 测试 | Test | — | Windows/src/NextThought.Desktop/AppState.Models.cs:124 |
| 61 | 深色 | Dark | 深色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:511 |
| 62 | 添加 | Add | 添加 |  |
| 63 | 焦点 | Focus | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 64 | 确认 | Confirm | — | Windows/src/NextThought.Desktop/Views/ConfirmDialog.axaml |
| 65 | 示例 | Sample | 示例 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 66 | 移出 | Remove | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 67 | 穿透 | Click-through | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 68 | 类型 | Type | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 69 | 系列 | Series | 系列 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 70 | 纪要 | Notes | 纪要 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 71 | 经验 | Lesson | 经验 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 72 | 编辑 | Edit | 编辑 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 73 | 自动 | Automatic | 自动 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 74 | 英文 | English | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 75 | 补译 | Translate missing | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 76 | 讨论 | Discussion | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:41 |
| 77 | 议程 | Agenda | 议程 | Windows/src/NextThought.Desktop/MeetingDraft.cs:76 |
| 78 | 记下 | Save | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 79 | 记录 | Note | 记录 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 80 | 设置 | Settings | 设置 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 81 | 证据 | Evidence | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 82 | 误听 | Misheard | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 83 | 课件 | Course slides | 课件 | Windows/src/NextThought.Desktop/MeetingDraft.cs:78 |
| 84 | 返回 | Back | — |  |
| 85 | 通用 | General | 通用 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 86 | 采纳 | Accept | — |  |
| 87 | 采音 | Audio | — |  |
| 88 | 重播 | Replay | 重播 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 89 | 重试 | Retry | 重试 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 90 | 问题 | Question | 问题 | Windows/src/NextThought.Desktop/Views/Assistant/ConversationBubble.axaml |
| 91 | 问： | Q:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:34 |
| 92 | 音频 | Audio | — |  |
| 93 | 题库 | Question bank | 题库 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 94 | 首页 | Home | 首页 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 95 | 高亮 | Highlight | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 96 | 默认 | Default | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 97 | ⏎A： | 
A:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:206 |
| 98 |  路  |  ·  | — |  |
| 99 | 上下文 | Context | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 100 | 下一条 | Next | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 101 | 不可用 | Unavailable | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 102 | 中译： | Chinese:  | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:19 |
| 103 | 会议  | Meeting  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:99 |
| 104 | 去准备 | Set up | 去准备 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 105 | 工作台 | Workbench | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 106 | 已删除 | Deleted | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:35 |
| 107 | 已就绪 | Ready | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:71 |
| 108 | 已收起 | Collapsed | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml.cs:361 |
| 109 | 已连接 | Connected | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:204 |
| 110 | 幻灯片 | Slides | — |  |
| 111 | 应用。 | Apply. | — |  |
| 112 | 待确认 | To confirm | 待确认 | Windows/src/NextThought.Desktop/AppState.Series.cs:272 |
| 113 | 忽略。 | Ignore. | — |  |
| 114 | 截止  | Due  | — | Windows/src/NextThought.Desktop/Views/ViewpointMatrixView.cs:181 |
| 115 | 提示： | Tip:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:96 |
| 116 | 提词器 | Teleprompter | 提词器 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 117 | 提词岛 | Cue island | 提词岛 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 118 | 文件夹 | Folder | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:192 |
| 119 | 新技能 | New skill | — |  |
| 120 | 新模板 | New template | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:142 |
| 121 | 新章节 | New section | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:150 |
| 122 | 显示器 | Displays | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:576 |
| 123 | 最大化 | Maximize | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 124 | 最小化 | Minimize | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 125 | 有纪要 | Has notes | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 126 | 未准备 | Not ready | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:22 |
| 127 | 未填写 | Not set | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:67 |
| 128 | 未整理 | Not organized | — | Windows/src/NextThought.Desktop/AppState.cs:323 |
| 129 | 未联网 | Offline | — | Windows/src/NextThought.Desktop/AppState.cs:309 |
| 130 | 术语表 | Glossary | — |  |
| 131 | 条高亮 | highlights | — |  |
| 132 | 模板名 | Template name | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 133 | 知道了 | Got it | 知道了 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 134 | 自定义 | Custom | 自定义 | Windows/src/NextThought.Desktop/AppState.Home.cs:183 |
| 135 | 英译： | English:  | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:19 |
| 136 | 说话人 | Speakers | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 137 | 资料库 | Library | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 138 | 资料： | Materials: | — |  |
| 139 | 面试官 | Interviewer | 面试官 | Windows/src/NextThought.Desktop/OnboardingSample.cs:41 |
| 140 | 鱼骨图 | Fishbone | 鱼骨图 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 141 | 麦克风 | Microphone | 麦克风 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 142 |  · 我 |  · Me | — |  |
| 143 | ★ 高亮 | ★ Highlight | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 144 | 不再提示 | Don't ask again | 不再提示 | Windows/src/NextThought.Desktop/Views/Home/OnboardingResumeRow.axaml |
| 145 | 会中笔记 | Meeting notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 146 | 会后纪要 | Meeting notes | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 147 | 会议信息 | Meeting info | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 148 | 会议标题 | Meeting title | — | Windows/src/NextThought.Desktop/Views/RenameSessionDialog.axaml |
| 149 | 保存备注 | Save note | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 150 | 保存密钥 | Save key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 151 | 保存技能 | Save skills | — |  |
| 152 | 保存模板 | Save template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 153 | 保存笔记 | Save notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 154 | 保存结构 | Save structure | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 155 | 保存设置 | Save settings | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 156 | 停止记录 | Stop recording | — |  |
| 157 | 先说结论 | Lead with the conclusion | — |  |
| 158 | 先选场合 | Pick a scene | — |  |
| 159 | 全文命中 | Full-text hits | — |  |
| 160 | 全文搜索 | Full-text search | — |  |
| 161 | 全部会议 | All meetings | — |  |
| 162 | 全部文件 | All files | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml.cs:25 |
| 163 | 全部术语 | All terms | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 164 | 全部设置 | All settings | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 165 | 删除会议 | Delete meeting | — |  |
| 166 | 删除选中 | Delete selected | — |  |
| 167 | 刷新设备 | Refresh devices | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 168 | 区域截图 | Region capture | 区域截图 | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 169 | 历史回答 | Past answers | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 170 | 参考回答 | Reference answer | — | Windows/src/NextThought.Desktop/Views/DetachedAnswerWindow.axaml |
| 171 | 双屏演讲 | Presenter mode | 双屏演讲 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 172 | 只看回答 | Answers only | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 173 | 回答列表 | Answers | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 174 | 回答引擎 | Answer engine | 回答引擎 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:18 |
| 175 | 回答服务 | Answer service | 回答服务 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 176 | 回答模型 | Answer model | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:187 |
| 177 | 回答记录 | Answer history | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 178 | 回答风格 | Answer style | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 179 | 声音来源 | Audio sources | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 180 | 声音测试 | Sound test | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 181 | 复制修复 | Copy fix | — |  |
| 182 | 复核备注 | Review note | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:27 |
| 183 | 学术汇报 | Academic talk | — | Windows/src/NextThought.Desktop/AppState.Home.cs:64 |
| 184 | 实时转写 | Live transcript | 实时转写 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 185 | 导入备份 | Import backup | — |  |
| 186 | 导入资料 | Import materials | 导入资料 | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 187 | 导出备份 | Export backup | — | Windows/src/NextThought.Desktop/BackupFileRow.cs:26 |
| 188 | 导出术语 | Export terms | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:86 |
| 189 | 小组项目 | Group project | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:43 |
| 190 | 工作布置 | Work assignment | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:119 |
| 191 | 工作纪要 | Meeting notes | — |  |
| 192 | 已取消。 | Cancelled. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:359 |
| 193 | 已改为  | Changed to  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 194 | 已有纪要 | Notes exist | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:639 |
| 195 | 带入经验 | Bring lessons | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 196 | 带入资料 | Bring materials | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 197 | 带入题库 | Bring question bank | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 198 | 开始录制 | Start recording | — |  |
| 199 | 开始记录 | Start recording | 开始记录 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 200 | 引文支持 | Quote support | — | Windows/src/NextThought.Desktop/SessionEvidenceRow.cs:13 |
| 201 | 引用来源 | Cite source | — |  |
| 202 | 归档截图 | Archive screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 203 | 归档概览 | Archive overview | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 204 | 归档系列 | Archive series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 205 | 当前场景 | Current scene | 当前场景 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:22 |
| 206 | 录音来源 | Audio source | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:185 |
| 207 | 快速设置 | Quick settings | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 208 | 恢复点击 | Restore clicks | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 209 | 截图提问 | Ask about a screenshot | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 210 | 手机伴侣 | Phone companion | 手机伴侣 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 211 | 打开系列 | Open series | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 212 | 打开纪要 | Open notes | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 213 | 打开设置 | Open settings | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 214 | 接口预设 | API preset | — |  |
| 215 | 控件样张 | Control samples | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 216 | 推理强度 | Reasoning effort | 推理强度 | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 217 | 提词宽度 | Cue width | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 218 | 搜索设置 | Search settings | 搜索设置 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 219 | 撤销移除 | Undo removal | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 220 | 收起示例 | Hide sample | 收起示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:66 |
| 221 | 放大字号 | Larger text | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 222 | 教练小结 | Coach summary | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 223 | 数据目录 | Data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 224 | 文字纪要 | Text notes | 文字纪要 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 225 | 新增条目 | New entry | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:67 |
| 226 | 新建会议 | New meeting | — |  |
| 227 | 新建模板 | New template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 228 | 新建系列 | New series | 新建系列 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 229 | 显示译文 | Show translation | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 230 | 替换回答 | Replace answer | — |  |
| 231 | 最近会议 | Recent meetings | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 232 | 未在采集 | Not capturing | — |  |
| 233 | 未知应用 | Unknown app | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:114 |
| 234 | 查看截图 | View screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 235 | 查看来源 | View sources | — |  |
| 236 | 标准纪要 | Standard notes | — |  |
| 237 | 校对转写 | Proofread transcript | 校对转写 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 238 | 检查更新 | Check for updates | 检查更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 239 | 正在检查 | Checking | — | Windows/src/NextThought.Desktop/AppState.Update.cs:82 |
| 240 | 正在记录 | Recording | — | Windows/src/NextThought.Desktop/AppState.Live.cs:859 |
| 241 | 添加章节 | Add section | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 242 | 清除查询 | Clear query | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 243 | 独立窗口 | Detached window | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 244 | 章节标题 | Section title | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 245 | 简体中文 | Simplified Chinese | 简体中文 | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 246 | 简洁回答 | Concise answer | — |  |
| 247 | 系列文档 | Series documents | — |  |
| 248 | 系列术语 | Series glossary | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 249 | 系列管理 | Manage series | — |  |
| 250 | 系列资料 | Series materials | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 251 | 系统识别 | System recognition | 系统识别 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:120 |
| 252 | 纪要编辑 | Edit notes | — |  |
| 253 | 组会讨论 | Group meeting | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:115 |
| 254 | 结束会议 | End meeting | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:54 |
| 255 | 继续记录 | Resume recording | 继续记录 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 256 | 编辑系列 | Edit series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 257 | 编辑经验 | Edit lesson | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:105 |
| 258 | 编辑题目 | Edit question | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:153 |
| 259 | 缩小字号 | Smaller text | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 260 | 联网来源 | Web sources | — |  |
| 261 | 自动回答 | Automatic answers | 自动回答 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 262 | 自检回答 | Self-test answer | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:572 |
| 263 | 自检问题 | Self-test question | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:572 |
| 264 | 要点大纲 | Outline | 要点大纲 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 265 | 观点对照 | Viewpoints | 观点对照 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 266 | 解析结果 | Parse result | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 267 | 讨论题目 | Discussion topics | 讨论题目 | Windows/src/NextThought.Desktop/MeetingDraft.cs:79 |
| 268 | 讲稿要点 | Script key points | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 269 | 设置清单 | Setup checklist | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 270 | 识别模型 | Recognition model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 271 | 详细信息 | Details | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 272 | 语音识别 | Speech recognition | 语音识别 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 273 | 误听标记 | Misheard marker | — | Windows/src/NextThought.Desktop/GlossaryEntryRow.cs:11 |
| 274 | 资料复核 | Material review | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 275 | 资料引用 | Material citations | — |  |
| 276 | 跟随系统 | Follow system | 跟随系统 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:394 |
| 277 | 跳到原话 | Jump to the original | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 278 | 通义千问 | Qwen (Tongyi) | — |  |
| 279 | 重新检查 | Check again | 重新检查 | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:174 |
| 280 | 附着技能 | Attach skills | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 281 | 面试复盘 | Interview review | — |  |
| 282 | 面试记录 | Interview notes | 面试记录 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 283 | 领域字段 | Domain fields | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 284 | 首次引导 | First-run guide | — |  |
| 285 | （主屏） |  (main display) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:61 |
| 286 | 上一场转写 | Last meeting's transcript | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:91 |
| 287 | 下一场会议 | Next meeting | — |  |
| 288 | 下载并安装 | Download and install | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 289 | 不自动结束 | Do not auto-end | 不自动结束 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:661 |
| 290 | 什么都可以 | Anything goes | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:16 |
| 291 | 使用此区域 | Use this area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 292 | 保存到设置 | Save to settings | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 293 | 保存快捷键 | Save shortcuts | — |  |
| 294 | 保存术语表 | Save glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 295 | 全局快捷键 | Global shortcuts | 全局快捷键 |  |
| 296 | 关闭提词器 | Close teleprompter | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 297 | 分工与对齐 | Assign and align | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:11 |
| 298 | 参考资料库 | Reference materials | — |  |
| 299 | 双屏演讲： | Presenter mode:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:623 |
| 300 | 只记录模式 | Record-only mode | — |  |
| 301 | 回滚失败： | Rollback failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:183 |
| 302 | 安装失败： | Install failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:315 |
| 303 | 导入失败： | Import failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:49 |
| 304 | 屏幕与截图 | Screen and screenshots | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 305 | 已保存密钥 | Key saved | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:23 |
| 306 | 已加密保存 | Saved encrypted | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:461 |
| 307 | 已确认经验 | Confirmed lessons | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 308 | 已选择原话 | Quote selected | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 309 | 录入失败： | Enrollment failed:  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:157 |
| 310 | 恢复失败： | Restore failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:60 |
| 311 | 截图失败： | Screenshot failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:364 |
| 312 | 截图已归档 | Screenshot archived | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 313 | 打开下载页 | Open download page | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 314 | 打开主窗口 | Open main window | 打开主窗口 | Windows/src/NextThought.Desktop/TrayIntegration.cs:17 |
| 315 | 打开术语表 | Open glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 316 | 打开资料库 | Open library | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 317 | 按模板整理 | Organize with template | 按模板整理 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 318 | 接着上一场 | Continue the last one | 接着上一场 | Windows/src/NextThought.Desktop/Views/Home/ContinueSeries.axaml |
| 319 | 提示岛宽度 | Cue island width | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 320 | 提词岛示意 | Cue island preview | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 321 | 数据与关于 | Data and about | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 322 | 未命名会议 | Untitled meeting | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:100 |
| 323 | 未命名窗口 | Untitled window | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:88 |
| 324 | 未归入系列 | Not in a series | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:184 |
| 325 | 模型与接口 | Model and API | — |  |
| 326 | 正在准备… | Preparing… | — | Windows/src/NextThought.Desktop/AppState.cs:262 |
| 327 | 正在获取… | Fetching… | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:307 |
| 328 | 添加到系列 | Add to series | — |  |
| 329 | 类型与标题 | Type and title | — |  |
| 330 | 系列与资料 | Series and materials | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 331 | 系列变更已 | Series change  | — |  |
| 332 | 系统 ·  | System ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:386 |
| 333 | 纪要已整理 | Notes ready | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:263 |
| 334 | 纪要整理中 | Notes being organized | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:264 |
| 335 | 结论与待办 | Conclusions and actions | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:13 |
| 336 | 继续记录。 | Recording resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:137 |
| 337 | 角色：%@ | Role: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:48 |
| 338 | 讲给别人听 | Present to others | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:9 |
| 339 | 译文待更新 | Translation out of date | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 340 | 资料与检索 | Materials and retrieval | — |  |
| 341 | 还没有纪要 | No notes yet | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:266 |
| 342 | 选择麦克风 | Select microphone | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 343 | 重命名系列 | Rename series | — |  |
| 344 | 问题置信度 | Question confidence | — | Windows/src/NextThought.Desktop/Views/Assistant/DetectionStrip.axaml.cs:47 |
| 345 | ，签名者  | , signer  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 346 | ⏎⏎资料：⏎ | 

Materials:
 | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:90 |
| 347 |  · 名字: |  · Name: | — |  |
| 348 | %lld 个 | %lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:190 |
| 349 | %lld 处 | %lld places | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:64 |
| 350 | %lld 段 | %lld blocks | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:225 |
| 351 | %lld 页 | %lld pages | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:84 |
| 352 | 上一场 ·  | Previous ·  | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:227 |
| 353 | 会议已导出。 | Meeting exported. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:149 |
| 354 | 会议自动收尾 | Auto-end meetings | 会议自动收尾 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 355 | 会议记录操作 | Meeting record actions | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 356 | 保存个人笔记 | Save personal notes | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 357 | 共用文档组件 | Shared document components | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:82 |
| 358 | 准备检索组件 | Prepare retrieval components | — |  |
| 359 | 准备语音组件 | Prepare speech components | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 360 | 删除筛选结果 | Delete filtered results | — |  |
| 361 | 刷新应用列表 | Refresh apps | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 362 | 刷新网络地址 | Refresh addresses | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 363 | 双击恢复控件 | Double-click to restore controls | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml.cs:217 |
| 364 | 取消当前任务 | Cancel task | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 365 | 可选，未配置 | Optional, not configured | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:23 |
| 366 | 合并到上一个 | Merge into previous | — |  |
| 367 | 合并回主窗口 | Merge back into the main window | — | Windows/src/NextThought.Desktop/Views/DetachedModuleWindow.axaml |
| 368 | 回答已取消。 | Answer cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:644 |
| 369 | 回答已复制。 | Answer copied. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:780 |
| 370 | 回答模型名称 | Answer model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 371 | 复制配对链接 | Copy pairing link | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 372 | 字幕显示译文 | Show translation in subtitles | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 373 | 安装包签名： | Installer signature:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 374 | 安装示例技能 | Install sample skill | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 375 | 导入 WAV | Import WAV | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 376 | 导入纪要模板 | Import note template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:201 |
| 377 | 导出 PDF | Export PDF | — |  |
| 378 | 导出 SRT | Export SRT | — |  |
| 379 | 导出 VTT | Export VTT | — |  |
| 380 | 已保存 ·  | Saved ·  | — |  |
| 381 | 已删除会议： | Meeting deleted:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:83 |
| 382 | 已取消截图。 | Screenshot cancelled. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:213 |
| 383 | 已取消生成。 | Generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:104 |
| 384 | 已取消高亮。 | Highlight removed. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:143 |
| 385 | 已录入 ·  | Enrolled ·  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:95 |
| 386 | 已恢复 %@ | Restored %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:186 |
| 387 | 已重新排队。 | Re-queued. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:343 |
| 388 | 幻灯片所在屏 | Slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 389 | 截取当前窗口 | Capture current window | — |  |
| 390 | 打开数据目录 | Open data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 391 | 打开文件位置 | Open file location | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 392 | 技能已保存。 | Skill saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:98 |
| 393 | 技能已删除。 | Skill deleted. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:118 |
| 394 | 技能已清空。 | Skills cleared. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 395 | 把讨论留下来 | Keep the discussion | — |  |
| 396 | 把这场景加入 | Add this scene | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 397 | 按下组合键… | Press a key combination… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:433 |
| 398 | 提示岛所在屏 | Cue island display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 399 | 提词器已穿透 | Teleprompter click-through on | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 400 | 提高不透明度 | More opaque | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 401 | 收起面试示例 | Hide sample interview | 收起面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:67 |
| 402 | 整理已取消。 | Note generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:255 |
| 403 | 整理这场会议 | Summarize this meeting | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 404 | 新系列的名字 | New series name | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 405 | 智谱 GLM | Zhipu GLM | — |  |
| 406 | 更改尚未保存 | Changes are not saved yet | 更改尚未保存 | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:27 |
| 407 | 未使用资料库 | No materials used | — | Windows/src/NextThought.Desktop/AppState.Live.cs:555 |
| 408 | 本地（默认） | Local (default) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:468 |
| 409 | 本机语音识别 | On-device speech | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 410 | 术语已保存。 | Term saved. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:221 |
| 411 | 来回交换观点 | Exchange viewpoints | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:15 |
| 412 | 校对已取消。 | Proofreading cancelled. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:87 |
| 413 | 检查回答服务 | Test answer service | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 414 | 正在生成回答 | Generating answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:859 |
| 415 | 点击查看原图 | Click to view the full image | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 416 | 生成这种纪要 | Generate this note | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 417 | 筛选资料名称 | Filter material names | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 418 | 系列已归档。 | Series archived. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:444 |
| 419 | 纪要整理失败 | Notes failed | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:265 |
| 420 | 经验已删除。 | Lesson deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:402 |
| 421 | 经验已更新。 | Lesson updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:377 |
| 422 | 经验已记下。 | Lesson saved. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:501 |
| 423 | 继续完成设置 | Finish setting up | 继续完成设置 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:41 |
| 424 | 聆听已恢复。 | Listening resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:225 |
| 425 | 说话人与翻译 | Speakers and translation | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 426 | 还没有术语。 | No glossary terms yet. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:80 |
| 427 | 还没有经验。 | No lessons yet. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 428 | 选择输出设备 | Select output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 429 | 重命名失败： | Rename failed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:19 |
| 430 | 降低不透明度 | Less opaque | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 431 | 隐藏所有窗口 | Hide all windows | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:18 |
| 432 | 题库已更新。 | Question bank updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:417 |
| 433 | 题库还空着。 | The question bank is empty. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 434 | 题目已删除。 | Question deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:431 |
| 435 | 麦克风 ·  | Microphone ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:389 |
| 436 | 默认，不指定 | Default, not specified | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:52 |
| 437 | ，并恢复设置 | , and settings restored | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:221 |
| 438 | ，立即生效。 | , takes effect immediately. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 439 |  · 可能回声 |  · possible echo | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:56 |
| 440 |  · 提词按句 |  · cue by sentence | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 441 | 不是同一个系列 | Not the same series | 不是同一个系列 |  |
| 442 | 主屏（摄像头） | Main display (camera) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:597 |
| 443 | 主要听，记重点 | Mostly listen, note the key points | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:14 |
| 444 | 云端识别中断： | Cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:236 |
| 445 | 产品资料、报价 | Product materials, quotes | 产品资料、报价 | Windows/src/NextThought.Desktop/MeetingDraft.cs:77 |
| 446 | 从文件导入术语 | Import terms from a file | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:64 |
| 447 | 会议写入失败： | Writing the meeting failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:430 |
| 448 | 会议归档 ·  | Meeting archived ·  | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:494 |
| 449 | 保存条目失败： | Saving the item failed:  | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:158 |
| 450 | 保存高亮失败： | Saving the highlight failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:144 |
| 451 | 先听需求再回应 | Hear the need first | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:12 |
| 452 | 全文搜索失败： | Full-text search failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:79 |
| 453 | 删除这场会议？ | Delete this meeting? | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:128 |
| 454 | 双屏演讲已关闭 | Presenter mode is off | — |  |
| 455 | 后台下载失败： | Background download failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:225 |
| 456 | 回滚到上一版本 | Roll back to the previous version | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 457 | 回答正在生成… | Generating an answer… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 458 | 在转写中查找… | Find in transcript… | — |  |
| 459 | 对方 · 流式 | Them · streaming | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 460 | 导入 .md… | Import .md… | — |  |
| 461 | 导出 Word | Export Word | — |  |
| 462 | 尚未加载技能。 | No skills loaded. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 463 | 已保存访问令牌 | Access token saved | 已保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 464 | 已暂停自动滚动 | Auto-scroll paused | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 465 | 已重命名会议： | Meeting renamed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:17 |
| 466 | 已高亮并保存。 | Highlighted and saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:154 |
| 467 | 带入上一场转写 | Bring last transcript | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 468 | 开始或停止记录 | Start or stop recording | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:19 |
| 469 | 当前场景：通用 | Current scene: General | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:27 |
| 470 | 当前系列的术语 | Terms in this series | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 471 | 当前问题与候选 | Current question and candidates | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 472 | 打开位置失败： | Opening the location failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:82 |
| 473 | 打开来源失败： | Could not open the source:  | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:159 |
| 474 | 技能保存失败： | Saving skills failed:  | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:66 |
| 475 | 无法识别按键： | Unrecognized key:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:194 |
| 476 | 未保存访问令牌 | No access token saved | 未保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 477 | 未安装或待修复 | Not installed or needs repair | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:71 |
| 478 | 未导入 PPT | No PPT imported | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:82 |
| 479 | 术语表已清空。 | Glossary cleared. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:483 |
| 480 | 模板保存失败： | Saving the template failed:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:102 |
| 481 | 模板需要名字。 | A template needs a name. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:166 |
| 482 | 正在整理纪要… | Writing the notes… | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 483 | 正在校对转写… | Proofreading the transcript… | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:39 |
| 484 | 正在检查更新… | Checking for updates… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:84 |
| 485 | 正在联网核实… | Checking online… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:572 |
| 486 | 火山方舟 豆包 | Volcengine Ark Doubao | — |  |
| 487 | 界面操作出错： | Interface error:  | — | Windows/src/NextThought.Desktop/App.axaml.cs:26 |
| 488 | 看一场示例会议 | Watch a sample meeting | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 489 | 看一段面试示例 | Watch a sample interview | 看一段面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:66 |
| 490 | 窗口截图失败： | Window capture failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:221 |
| 491 | 等待首次读屏… | Waiting for the first screen read… | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:61 |
| 492 | 系列变更待确认 | Series changes to confirm | — |  |
| 493 | 系列已重命名： | Series renamed:  | — | Windows/src/NextThought.Desktop/AppState.Series.cs:427 |
| 494 | 经验 %lld | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Series.cs:269 |
| 495 | 结束后自动整理 | Summarize automatically after ending | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 496 | 联网查证失败： | Web verification failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:748 |
| 497 | 自动滚动到最新 | Auto-scroll to the latest | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 498 | 自定义纪要模板 | Custom note templates | 自定义纪要模板 | Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml |
| 499 | 被问到 1 次 | Asked once | — |  |
| 500 | 要求 / 偏好 | Requirements / preferences | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 501 | 讲进展，听意见 | Share progress, hear feedback | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:10 |
| 502 | 译文 · 当前 | Translation · current | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 503 | 请先输入问题。 | Type a question first. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:513 |
| 504 | 请总结当前讨论 | Summarize the current discussion | — | Windows/src/NextThought.Desktop/AppState.Live.cs:755 |
| 505 | 还没有选择系列 | No series selected | — | Windows/src/NextThought.Desktop/AppState.Series.cs:31 |
| 506 | 选择自定义模板 | Select custom template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 507 | 静音后直接结束 | End immediately after silence | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:660 |
| 508 | 静音多久后收尾 | End after silence for | — |  |
| 509 | 默认（不指定） | Default (not specified) | — |  |
| 510 |  · 提词按要点 |  · cue by key points | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 511 | %lld 个窗口 | %lld windows | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:28 |
| 512 | 中转地址（可选） | Relay URL (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 513 | 云端识别（实验） | Cloud transcription (experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 514 | 会中笔记已保存。 | Meeting notes saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:115 |
| 515 | 会中组件状态样板 | In-meeting component states | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 516 | 写下讨论的主题。 | Write the discussion topic. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:144 |
| 517 | 准备开始一场会议 | Ready to start a meeting | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml.cs:181 |
| 518 | 删除已录入的声音 | Delete the enrolled voice | — |  |
| 519 | 可选，还没有资料 | Optional, no materials yet | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 520 | 合并说话人失败： | Could not merge the speakers:  | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:159 |
| 521 | 听到外语自动翻译 | Auto-translate foreign speech | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 522 | 回答语言已更新。 | Answer language updated. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:238 |
| 523 | 声音设备已刷新。 | Audio devices refreshed. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:185 |
| 524 | 尚无自定义模板。 | No custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 525 | 屏幕感知已关闭。 | Screen awareness turned off. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:252 |
| 526 | 屏幕感知已开启。 | Screen awareness turned on. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:252 |
| 527 | 工作会议模板纪要 | Meeting template notes | — |  |
| 528 | 已从系列中移出。 | Removed from the series. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:487 |
| 529 | 已保存 · %@ | Saved · %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:647 |
| 530 | 已写入系列记录。 | Written to the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:93 |
| 531 | 已加入系列术语。 | Added to the series glossary. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:458 |
| 532 | 已加入系列术语： | Added to the series glossary:  | — |  |
| 533 | 已备份当前版本： | Current version backed up:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:299 |
| 534 | 已安装示例技能。 | Sample skill installed. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:133 |
| 535 | 已忽略这条建议。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:342 |
| 536 | 待确认 %lld | %lld to confirm | — | Windows/src/NextThought.Desktop/AppState.Series.cs:271 |
| 537 | 快捷键保存失败： | Saving shortcuts failed:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:211 |
| 538 | 恢复未全部完成： | Restore did not finish:  | — |  |
| 539 | 截图文件已不存在 | The screenshot file no longer exists | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:232 |
| 540 | 手机伴侣已关闭。 | Phone companion stopped. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:130 |
| 541 | 打开术语表失败： | Opening the glossary failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:334 |
| 542 | 拖动选择截图区域 | Drag to select a capture area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 543 | 接上外接屏后可用 | Available once an external display is connected | 接上外接屏后可用 |  |
| 544 | 提词浮窗鼠标穿透 | Teleprompter click-through | 提词浮窗鼠标穿透 |  |
| 545 | 整个输出设备混音 | Entire output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 546 | 旧原话：缓存几穿 | Old wording: cache miss | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 547 | 显示器 %lld | Display %lld | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:597 |
| 548 | 未知快捷键动作： | Unknown shortcut action:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:192 |
| 549 | 正在下载安装包… | Downloading the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:254 |
| 550 | 正在准备安装包… | Preparing the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:238 |
| 551 | 正在导入 %@… | Importing %@… | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:241 |
| 552 | 正在整理成%@… | Organizing into %@… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:395 |
| 553 | 正在解析 %@… | Parsing %@… | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:122 |
| 554 | 没有可用的显示器 | No display available | — |  |
| 555 | 系列信息已保存。 | Series details saved. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:147 |
| 556 | 系列变更未应用。 | Series change not applied. | — |  |
| 557 | 系列名不能为空。 | A series needs a name. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:130 |
| 558 | 系列文档已移除。 | Series document removed. | — |  |
| 559 | 系统输出设备混音 | System output mix | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 560 | 给这场会起个名字 | Name this meeting | — |  |
| 561 | 被问、要答得清楚 | Asked, answer clearly | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:8 |
| 562 | 讨论重点已整理。 | Discussion points are ready. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:99 |
| 563 | 说话人 %lld | Speaker %lld | 说话人 %lld | Windows/src/NextThought.Desktop/AppState.Speakers.cs:90 |
| 564 | 请总结当前讨论。 | Summarize the current discussion. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:819 |
| 565 | 资料库读取失败： | Reading the library failed:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:74 |
| 566 | 跟着讲话翻幻灯片 | Follow the talk across slides | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 567 | 还没有可用的文字 | No usable text yet | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:224 |
| 568 | 还没有归档截图。 | No archived screenshots yet. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 569 | 还没有检查更新。 | No update check yet. | — |  |
| 570 | 这一节要整理什么 | What should this section collect? | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 571 | 这条建议已忽略。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:92 |
| 572 | 配对链接已复制。 | Pairing link copied. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:273 |
| 573 | 首字 %.1fs | first token %.1fs | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:153 |
| 574 | ；本机回退失败： | ; local fallback failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:233 |
| 575 | %@ %lld 段 | %@ %lld sections | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 576 | M月d日 dddd | MMMM d, dddd | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:50 |
| 577 | high · 最强 | high · strongest | — |  |
| 578 | main 最新构建 | main latest build | — | Windows/src/NextThought.Desktop/AppState.Update.cs:46 |
| 579 | 从以前的会议带入… | Carry from an earlier meeting… | 从以前的会议带入… |  |
| 580 | 作业要求、评分标准 | Assignment requirements, grading rubric | 作业要求、评分标准 | Windows/src/NextThought.Desktop/MeetingDraft.cs:80 |
| 581 | 作为新决定加入系列 | Add to the series as a new decision | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:51 |
| 582 | 作为新行动加入系列 | Add to the series as a new action | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:50 |
| 583 | 保存会中笔记失败： | Saving meeting notes failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:117 |
| 584 | 保存复核备注失败： | Saving the review note failed:  | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:98 |
| 585 | 关闭局域网手机伴侣 | Stop LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:316 |
| 586 | 再决定是否调整…… | then decide whether to adjust… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 587 | 准备后可在会中引用 | Ready to cite during the meeting | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 588 | 删除会议和依赖内容 | Delete the meeting and its dependent content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:297 |
| 589 | 删除全部会议记录？ | Delete all meeting records? | 删除全部会议记录？ | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:155 |
| 590 | 刷新声音设备失败： | Refreshing audio devices failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:187 |
| 591 | 回答服务连接成功： | Answer service connected:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:642 |
| 592 | 在资源管理器中显示 | Show in File Explorer | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 593 | 复制配对链接失败： | Copying the pairing link failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:275 |
| 594 | 复盘 / 当前阶段 | Review / current stage | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 595 | 导入 .json… | Import .json… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 596 | 开启局域网手机伴侣 | Start LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:316 |
| 597 | 手动校正：缓存击穿 | Manual correction: cache stampede | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 598 | 手机伴侣启动失败： | Starting the phone companion failed:  | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:159 |
| 599 | 打开令牌页面失败： | Opening the token page failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:313 |
| 600 | 打开我的声音失败： | Opening My Voice failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:351 |
| 601 | 更改尚未保存：%@ | Changes are not saved yet: %@ | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:126 |
| 602 | 本周进展、相关论文 | This week's progress, related papers | 本周进展、相关论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:75 |
| 603 | 本机 Ollama | Local Ollama | — |  |
| 604 | 本机离线识别已就绪 | On-device offline recognition is ready | — | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:15 |
| 605 | 框选读屏区域失败： | Selecting the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:334 |
| 606 | 模型（留空用默认） | Model (empty uses default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 607 | 模板文件格式无效： | The template file format is invalid:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:214 |
| 608 | 正在加载本场资料… | Loading this meeting's materials… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 609 | 正在备份当前版本… | Backing up the current version… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:292 |
| 610 | 正在截屏读取屏幕… | Reading the screen… | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 611 | 正在本机读取录音… | Reading the recording on this device… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:391 |
| 612 | 正在查证当前回答… | Verifying the current answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:731 |
| 613 | 正在检查回答服务… | Checking the answer service… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:540 |
| 614 | 正在生成参考回答… | Generating a reference answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:540 |
| 615 | 没有新的知识候选。 | No new knowledge candidates. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:236 |
| 616 | 清除读屏区域失败： | Clearing the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:372 |
| 617 | 组会 / 研究讨论 | Lab meeting / research discussion | — |  |
| 618 | 经验 %lld 条 | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:90 |
| 619 | 自定义模板已清空。 | Custom templates cleared. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 620 | 英语回答附中文对照 | Gloss English answers in Chinese | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 621 | 规范写法不能为空。 | The canonical form cannot be empty. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:211 |
| 622 | 讲稿、PPT、论文 | Script, slides, paper | 讲稿、PPT、论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:74 |
| 623 | 语义模型路径无效。 | The semantic model path is not valid. | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:134 |
| 624 | 请只回复：连接成功 | Reply only: connection succeeded | — | Windows/src/NextThought.Desktop/AppState.Live.cs:708 |
| 625 | 读取会议列表失败： | Reading the meeting list failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:53 |
| 626 | 读取窗口列表失败： | Could not read the window list:  | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:32 |
| 627 | 资料 %lld 份 | %lld materials | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:86 |
| 628 | 输入问题，回车回答 | Type a question and press Enter | — |  |
| 629 | 输入问题；回车发送 | Type a question; press Enter to send | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 630 | 选择多个系列后删除 | Select multiple series to delete | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 631 | 问题 %lld 个 | %lld questions | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:260 |
| 632 | 题库 %lld 题 | %lld questions in the bank | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:88 |
| 633 | 麦克风已重新打开。 | Microphone reopened. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:327 |
| 634 | [%@] %@：%@ | [%@] %@: %@ | — |  |
| 635 | none（关闭思考） | none (reasoning off) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:53 |
| 636 | 。接下来是什么场合？ | . What's next? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:55 |
| 637 | 严格按用户要求回复。 | Follow the user's request exactly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:607 |
| 638 | 会议结束时遇到问题： | A problem occurred while ending the meeting:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:356 |
| 639 | 保存说话人设置失败： | Could not save the speaker settings:  | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:122 |
| 640 | 先给系列起一个名字。 | Give the series a name first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:452 |
| 641 | 写这一节要整理什么。 | What should this section collect? | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:150 |
| 642 | 单栏上下排列（小屏） | Single column, stacked (small screen) | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 643 | 在设置中选择声音来源 | Choose an audio source in settings | — | Windows/src/NextThought.Desktop/AppState.cs:286 |
| 644 | 屏幕上没有读到文字。 | No text was read from the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:54 |
| 645 | 已导入 %lld 份 | Imported %lld | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 646 | 已新建系列「%@」。 | Series “%@” created. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:460 |
| 647 | 已经有同名的系列了。 | A series with that name already exists. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:455 |
| 648 | 已跳到原话 · %@ | Jumped to the original · %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:608 |
| 649 | 开始记录后才能读屏。 | Start recording before reading the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:108 |
| 650 | 录音转写 · 已归档 | Recording transcribed · archived | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:453 |
| 651 | 提词岛会中一直展开。 | The cue island stays open during meetings. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 652 | 有 %lld 处提示 | %lld notes | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:222 |
| 653 | 未知的云端识别服务。 | Unknown cloud transcription service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:200 |
| 654 | 本机 · 不依赖网络 | On-device · works offline | — |  |
| 655 | 校验通过，正在安装… | Verified; installing… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:308 |
| 656 | 核对上一条回答的事实 | Check the facts in the last answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:734 |
| 657 | 模板「%@」已保存。 | Template “%@” saved. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:178 |
| 658 | 正在检查安装包签名… | Checking the installer signature… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:263 |
| 659 | 没有可用的更新备份。 | No update backup is available. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:170 |
| 660 | 没有需要更正的地方。 | Nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:47 |
| 661 | 看镜头 %.0f%% | Eye contact %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:56 |
| 662 | 第三方许可与版权说明 | Third-party licenses and copyright | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 663 | 联网查证缓存已清空。 | The web-verification cache was cleared. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:62 |
| 664 | 自动（第一块外接屏） | Automatic (first external display) | — | Windows/src/NextThought.Desktop/DisplayCatalog.cs:24 |
| 665 | 资料复核备注已保存。 | Material review note saved. | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:96 |
| 666 | 还没有可回答的转写。 | No transcript to answer yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:719 |
| 667 | 还没有可查证的回答。 | No answer to verify yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:729 |
| 668 | 这次不带入系列内容。 | Nothing from the series is carried into this meeting. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:92 |
| 669 | 麦克风云端识别中断： | Microphone cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:250 |
| 670 | （会议窗口保持显示） |  (meeting window stays visible) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:640 |
| 671 | ；界面按钮仍可使用。 | ; window buttons still work. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 672 |  · 说话人 %lld |  · speaker %lld | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:55 |
| 673 | %@ · 建议放：%@ | %@ · Suggested materials: %@ | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:93 |
| 674 | API · 未指定模型 | API · no model chosen | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:192 |
| 675 | 下午好。把讨论留下来。 | Good afternoon. Keep the discussion. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:46 |
| 676 | 中午好。会议还在继续。 | Good noon. The meeting goes on. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:45 |
| 677 | 云服务 API Key | Cloud service API key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 678 | 会议已导出为 PDF。 | Meeting exported as PDF. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:179 |
| 679 | 先开始或打开一场会议。 | Start or open a meeting first. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:109 |
| 680 | 发言占比 %.0f%% | Speaking share %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:54 |
| 681 | 回答在左 · 对话在右 | Answers left · conversation right | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 682 | 回答服务（Codex） | Answer service (Codex) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 683 | 备份失败，仍继续更新： | Backup failed; continuing the update:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:304 |
| 684 | 导入 %@ 失败：%@ | Importing %@ failed: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:245 |
| 685 | 导出 Markdown | Export Markdown | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 686 | 已结束 · 会议已归档 | Ended · meeting archived | — | Windows/src/NextThought.Desktop/AppState.Live.cs:353 |
| 687 | 幻灯片：%@ · %@ | Slide: %@ · %@ | — |  |
| 688 | 引用来自已删除的资料： | Citations from deleted materials:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:147 |
| 689 | 当前回答尚无资料引用。 | The current answer has no material citations yet. | — |  |
| 690 | 当前没有可命名的说话人 | No speaker to name right now | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:62 |
| 691 | 录入 / 管理我的声音 | Enroll / manage my voice | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 692 | 最长独白 %.0f 秒 | Longest monologue %.0f s | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:55 |
| 693 | 未找到第三方许可文件。 | Third-party license file not found. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:321 |
| 694 | 正在加载本地语音模型… | Loading the on-device speech model… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:172 |
| 695 | 正在记录的会议不能删除 | A meeting that is recording cannot be deleted | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:160 |
| 696 | 没有可导入的技能文件。 | No skill file to import. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:259 |
| 697 | 没有找到可截图的窗口。 | No window was found to capture. | — |  |
| 698 | 示例技能已经在列表里。 | The sample skill is already in the list. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:132 |
| 699 | 系列已有相近经验：%@ | The series already has related lessons: %@ | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:196 |
| 700 | 联网来源 %lld 条 | %lld web sources | — | Windows/src/NextThought.Desktop/AppState.cs:309 |
| 701 | 误听写法，用 \| 分隔 | Misheard forms separated by \| | — |  |
| 702 | 这场会议没有生成回答。 | This meeting has no answers. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 703 | 重新联网查一下当前回答 | Check the current answer again with the web | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 704 | 隐藏 / 恢复所有窗口 | Hide / restore all windows | — |  |
| 705 | %lld 个自定义模板。 | %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 706 | minimal · 最快 | minimal · fastest | — |  |
| 707 | 云端没有返回可用的文字。 | The cloud returned no usable text. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:426 |
| 708 | 使用语义与关键词混合检索 | Hybrid semantic and keyword retrieval | — |  |
| 709 | 依据：“%@” · %@ | Basis: “%@” · %@ | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:36 |
| 710 | 全文搜索至少输入两个字。 | Enter at least two characters for full-text search. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:71 |
| 711 | 内置屏 / 主屏（自动） | Built-in / main display (automatic) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:57 |
| 712 | 可以继续对这场会议提问。 | You can keep asking about this meeting. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:508 |
| 713 | 在这里写这个技能的要求。 | Write what this skill requires. | — |  |
| 714 | 字幕已导出（%@）%@。 | Subtitles exported (%@)%@. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:359 |
| 715 | 已在浏览器中打开下载页。 | Opened the download page in the browser. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:332 |
| 716 | 已把 %@ 并入 %@。 | Merged %@ into %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:157 |
| 717 | 已把 %@ 移到「%@」 | Moved %@ to “%@” | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:220 |
| 718 | 已读屏 · %lld 字 | Screen read · %lld characters | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:64 |
| 719 | 已选择 %lld 场会议 | %lld meetings selected | — |  |
| 720 | 开始记录后才能暂停聆听。 | Start recording before pausing listening. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:221 |
| 721 | 当前场景：%@ · %@ | Current scene: %@ · %@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 722 | 技能（回答时遵守的说明） | Skills (instructions followed when answering) | — |  |
| 723 | 提词浮窗已恢复鼠标操作。 | Teleprompter clicks restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:486 |
| 724 | 搜索会议标题、日期或模式 | Search meeting title, date or mode | — |  |
| 725 | 早上好。今天要记下什么？ | Good morning. What should we capture today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:44 |
| 726 | 本机 LM Studio | Local LM Studio | — |  |
| 727 | 术语表（人名与专有名词） | Glossary (names and terms) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 728 | 模板文件里没有可用模板。 | The template file has no usable templates. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:211 |
| 729 | 没有匹配到「数据库迁移」 | No match for “database migration” | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 730 | 演示模式：未加载识别模型 | Demo mode: recognition model not loaded | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:99 |
| 731 | 简历、职位描述、公司信息 | Résumé, job description, company info | 简历、职位描述、公司信息 | Windows/src/NextThought.Desktop/MeetingDraft.cs:73 |
| 732 | 继续完成设置（还差%@） | Finish setting up (missing %@) | 继续完成设置（还差%@） | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:47 |
| 733 | 选择声音来源，开始记录。 | Choose an audio source and start recording. | — | Windows/src/NextThought.Desktop/AppState.cs:261 |
| 734 | 麦克风 · 适合线下会议 | Microphone · for in-person meetings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:76 |
| 735 | ；已经记录的内容已保存。 | ; what was recorded is saved. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:354 |
| 736 |   ·  另 %lld 份 |   ·  %lld more | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 737 | Soniox · 中英混说 | Soniox · bilingual | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:469 |
| 738 | 会议已保存，可以整理纪要。 | Meeting saved; notes can be organized. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:354 |
| 739 | 使用系列知识（经验与题库） | Use series knowledge (lessons and question bank) | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 740 | 夜深了。要把想法留下来吗？ | Late night. Want to keep a thought? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:43 |
| 741 | 安装已下载的更新并重新打开 | Install the downloaded update and reopen | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 742 | 已从「%@」移除「%@」。 | Removed “%@” from “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:156 |
| 743 | 已删除 %lld 场会议。 | %lld meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:45 |
| 744 | 已导入 %lld 份资料。 | %lld materials imported. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:135 |
| 745 | 已暂停自动滚动（点击恢复） | Auto-scroll paused (choose to resume) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 746 | 已采纳，下次回答会带上它。 | Accepted; later answers will use it. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:345 |
| 747 | 手动提问，或开启自动回答。 | Ask manually, or turn on automatic answers. | — | Windows/src/NextThought.Desktop/AppState.cs:280 |
| 748 | 整句精修，提高最终转写质量 | Refine whole sentences for a better transcript | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 749 | 新增 2 句 · 回到现场 | +2 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 750 | 新增 6 句 · 回到现场 | +6 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 751 | 正在校验后台下载的安装包… | Verifying the background download… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:247 |
| 752 | 没有新的已核实决定或行动。 | No new verified decisions or actions. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:109 |
| 753 | 系列已删除；会议记录保留。 | Series deleted; meeting records kept. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:437 |
| 754 | 自动滚动到最新（点击暂停） | Auto-scroll to the latest (choose to pause) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 755 | 语速 %.0f 单位/分钟 | Pace %.0f units/min | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:53 |
| 756 | 读屏失败，20 秒后重试。 | Screen read failed; retrying in 20 seconds. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:70 |
| 757 | 这个键不能用作全局快捷键。 | That key cannot be used as a global shortcut. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:455 |
| 758 | 面试官在追问 · 参考回答 | The interviewer is following up · reference answer | 面试官在追问 · 参考回答 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 759 | 中转地址不是有效的 URL。 | The relay address is not a valid URL. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:560 |
| 760 | 先停止当前任务，再删除会议。 | Stop the current task before deleting meetings. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:54 |
| 761 | 先停止当前任务，再删除系列。 | Stop the current task before deleting a series. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:327 |
| 762 | 全局快捷键已保存并重新注册。 | Shortcuts saved and re-registered. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:209 |
| 763 | 全文搜索：%lld 条命中。 | Full-text search: %lld hits. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:77 |
| 764 | 导入技能（Markdown） | Import skill (Markdown) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:243 |
| 765 | 已刷新可采集的应用程序列表。 | The list of recordable apps was refreshed. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:24 |
| 766 | 已把「%@」附着到「%@」。 | Attached “%@” to “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:155 |
| 767 | 已整理成%@，可复制或导出。 | Organized into %@; ready to copy or export. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:481 |
| 768 | 已新建技能，改完内容后保存。 | New skill created; edit it and save. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:113 |
| 769 | 幻灯片在 %@，提示在 %@ | Slides on %@, cue on %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:607 |
| 770 | 拖动可移动 · 双击隐藏控件 | Drag to move · double-click to hide controls | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 771 | 搜索命中 %lld/%lld | Search hits %lld/%lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:42 |
| 772 | 正在加载模型并转写 WAV… | Loading the model and transcribing the WAV… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:393 |
| 773 | 正在按《%@》合并分段结果… | Merging parts for “%@”… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:226 |
| 774 | 系列变更待确认 · %lld | Series changes to confirm · %lld | — | Windows/src/NextThought.Desktop/AppState.Series.cs:57 |
| 775 | 纪要模型（留空沿用回答模型） | Notes model (empty uses the answer model) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 776 | 线上会议听电脑里播放的声音。 | Online meetings listen to what this computer plays. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:173 |
| 777 | 识别出来的错误写法，逗号分隔 | Recognized misspellings, comma separated | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:68 |
| 778 | 语速偏快，适当放慢并加停顿。 | Speaking fast; slow down and pause. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:313 |
| 779 | 说话人命名已应用到当前转写。 | Speaker names applied to the current transcript. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:208 |
| 780 | 资料与 AI 都可稍后补充。 | Materials and an answer service can both be added later. | 资料与 AI 都可稍后补充。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:78 |
| 781 | 这场会议还没有可复核的条目。 | This meeting has nothing to review yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:102 |
| 782 | 这场会议还没有可收集的候选。 | This meeting has no candidates to collect yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:227 |
| 783 | 退出 NextThought | Quit NextThought | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:20 |
| 784 | 选一个类型作为基础，再改名字 | Pick a base type, then rename it | 选一个类型作为基础，再改名字 | Windows/src/NextThought.Desktop/MeetingDraft.cs:68 |
| 785 | 静音后先提示，再结束（推荐） | Prompt after silence, then end (recommended) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:659 |
| 786 | %lld 份资料 · 本机解析 | %lld materials · parsed on this device | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:57 |
| 787 | NextThought 字幕条 | NextThought Subtitles | NextThought 字幕条 | Windows/src/NextThought.Desktop/Views/SubtitleBarWindow.axaml |
| 788 | NextThought 提词器 | NextThought Teleprompter | NextThought 提词器 | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 789 | NextThought 提词岛 | NextThought Cue island | NextThought 提词岛 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 790 | 云端识别中断，已回退本机识别： | Cloud transcription interrupted; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:228 |
| 791 | 以下全局快捷键被其他程序占用： | These global shortcuts are used by other programs:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 792 | 会议已导出为 Word 文档。 | Meeting exported as a Word document. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:164 |
| 793 | 保存 Tavily 密钥失败： | Saving the Tavily key failed:  | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:33 |
| 794 | 先停止当前任务，再准备下一场。 | Stop the current task before preparing the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:202 |
| 795 | 准备声纹组件（约 26 MB） | Prepare speaker components (~26 MB) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 796 | 删除会议，保留已确认的系列内容 | Delete the meeting and keep accepted series content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:295 |
| 797 | 双屏演讲已结束，主窗口已恢复。 | Presenter mode ended; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:666 |
| 798 | 回答暂时不可用；记录仍在继续。 | Answers are temporarily unavailable; recording continues. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 799 | 对话在左 · 回答在右（默认） | Conversation left · answers right (default) | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 800 | 尚未保存密钥；本机服务可不填。 | No key saved yet; optional for local services. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 801 | 已导入，共 %lld 个技能。 | Imported %lld skills. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:47 |
| 802 | 已把「%@」放进系列「%@」。 | Added “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:475 |
| 803 | 已按屏幕切到第 %lld 页。 | Switched to slide %lld by screen. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:130 |
| 804 | 已读取归档；%lld 项待写入 | Archive read; %lld items pending write | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:653 |
| 805 | 当前有任务在进行，稍后再截图。 | A task is running; capture shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:206 |
| 806 | 当前有任务在进行，稍后再框选。 | A task is running; select the area shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:308 |
| 807 | 提词岛已收起，鼠标移上来就展开 | Cue island collapsed; hover to expand | — | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml.cs:184 |
| 808 | 提词岛恢复为鼠标移上去才展开。 | Cue island expands on hover again. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 809 | 校对完成：没有需要更正的地方。 | Proofreading finished: nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:48 |
| 810 | 正在记录 · %lld 条转写 | Recording · %lld lines | — | Windows/src/NextThought.Desktop/AppState.Live.cs:10 |
| 811 | 正在通过云端识别转写 WAV… | Transcribing the WAV with cloud recognition… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:408 |
| 812 | 流式模型：%@　整句精修：%@ | Streaming model: %@; sentence refinement: %@ | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:70 |
| 813 | 演示数据 · 不会写入你的资料 | Demo data · nothing is written to your materials | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:158 |
| 814 | 界面语言 / Language | Interface language | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 815 | 程序正在退出，已取消开始记录。 | The app is quitting; start recording was cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:185 |
| 816 | 系统声音范围（每次开始前选择） | System audio scope (choose before each start) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 817 | 纪要窗口 → 字幕条 → 隐藏 | Notes window → subtitle bar → hidden | — |  |
| 818 | 这场会议还没有转写，无法整理。 | This meeting has no transcript yet. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:384 |
| 819 | 进行中 · 转写持续保存在本机 | In progress · transcript saved locally | — | Windows/src/NextThought.Desktop/AppState.Live.cs:302 |
| 820 | 选区 %lld×%lld 像素 | Selection %lld×%lld pixels | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml.cs:89 |
| 821 | …… 其余 %lld 段未显示。 | … %lld more sections are hidden. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:103 |
| 822 | 先停止当前任务，再重新整理纪要。 | Stop the current task before organizing notes again. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:301 |
| 823 | 准备这场会 · 类型、资料与检查 | Prepare this meeting · type, materials and checks | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:22 |
| 824 | 只在本机运行，声纹只是一组数字。 | Runs on-device; the voiceprint is just numbers. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 825 | 回答已完成。会议内的回答已归档。 | Answer complete. Answers from the meeting are archived. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:642 |
| 826 | 已保存 %lld 个自定义模板。 | Saved %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 827 | 已删除系列「%@」及其会议记录。 | Deleted series “%@” and its meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:340 |
| 828 | 已删除选中的 %lld 场会议。 | %lld selected meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:35 |
| 829 | 已把 %lld 份资料加入系列。 | Added %lld materials to the series. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:258 |
| 830 | 已清除说话人 %lld 的名字。 | Cleared the name of speaker %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:119 |
| 831 | 已跟随讲话切到第 %lld 页。 | Switched to slide %lld by speech. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:128 |
| 832 | 查证完成：%lld 条联网来源。 | Verification complete: %lld web sources. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:746 |
| 833 | 模型已下载并校验，可以离线记录。 | Model downloaded and verified; offline recording is ready. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:81 |
| 834 | 第 %lld 页 · %@⏎%@ | Slide %lld · %@
%@ | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:159 |
| 835 | 系统声音里出现提问时自动生成回答 | Answer automatically when a question appears in the system audio | — |  |
| 836 | 要求 / Preferences | Requirements / Preferences | — |  |
| 837 | 说了很久了，收个尾，把话交回去。 | You have talked a while; wrap up and hand it back. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:312 |
| 838 | 说话人区分暂不可用，转写仍保留： | Speaker separation unavailable; the transcript is kept:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:155 |
| 839 | 这场会用的资料 · %lld 份 | Materials for this meeting · %lld | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:233 |
| 840 | 高级：也可以直接按文本批量编辑。 | Advanced: you can also edit everything as text. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 841 | %@ 字幕 (*.%@)\|*.%@ | %@ subtitles (*.%@)\|*.%@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:347 |
| 842 | %lld 个技能；回答时按需遵守。 | %lld skills; followed as needed when answering. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 843 | %lld 个技能；已保存启停状态。 | %lld skills; on/off state saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:86 |
| 844 | X-ASR 中英 · 160 ms | X-ASR bilingual · 160 ms | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:47 |
| 845 | 上次会议被中断，已保存到所有记录。 | The last meeting was interrupted and saved in all records. | 上次会议被中断，已保存到所有记录。 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:113 |
| 846 | 云端识别启动失败，已回退本机识别： | Cloud transcription failed to start; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:264 |
| 847 | 使用本机 Codex CLI 账号 | Use local Codex CLI account | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 848 | 删除系列「%@」？会议记录会保留。 | Delete series “%@”? Meeting records are kept. | — |  |
| 849 | 区分会议中的说话人（本机实验功能） | Separate speakers (on-device, experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 850 | 历史回答的引用已保存，可查看来源。 | Citations for the past answer were saved; view the sources. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:497 |
| 851 | 只影响悬浮字幕条；保存后立即生效。 | Only affects the floating subtitle bar; takes effect immediately after saving. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 852 | 启动后自动检查更新（每 6 小时） | Check for updates automatically after launch (every 6 hours) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 853 | 在屏幕顶部显示问题与回答，不抢焦点 | Show question and answer at the top of the screen without taking focus | — |  |
| 854 | 对方讲得比较多，可以主动补充一句。 | They are talking more; add a point when you can. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:315 |
| 855 | 尚未选择声音来源，请到设置中选择。 | No audio source chosen; pick one in settings. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:76 |
| 856 | 幻灯片：第 %lld 页（手动）。 | Slide: page %lld (manual). | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:129 |
| 857 | 引导已稍后继续；首页会留一个入口。 | The guide was put aside; the home page keeps an entry. | — | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:66 |
| 858 | 录音已转写并归档，未保存原始音频。 | Recording transcribed and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:441 |
| 859 | 按资料原有的页、幻灯片或工作表定位 | Locate by the material's own page, slide or sheet | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 860 | 晚上好。今天有哪些值得留下的结论？ | Good evening. What conclusions are worth keeping today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:47 |
| 861 | 检测到屏幕共享，已自动收回提示岛。 | Screen sharing detected; the cue island was retracted. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:47 |
| 862 | 正在记录所选应用及其子进程的声音。 | Recording the selected app and its child processes. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:305 |
| 863 | 第 %lld 页，共 %lld 页 | %1$lld of %2$lld pages | 第 %lld 页，共 %lld 页 | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:85 |
| 864 | 纪要编辑已保存；生成结果未被覆盖。 | Notes edits saved; generated content was not overwritten. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:122 |
| 865 | 说话人 %lld 已命名为 %@。 | Speaker %lld is now named %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:120 |
| 866 | 请针对上一条回答给出一个具体例子。 | Give a concrete example for the last answer. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:825 |
| 867 | 这个技能的要求（Markdown） | Skill requirements (Markdown) | — |  |
| 868 | 这场会议没有任何发言，已自动停止。 | This meeting had no speech and stopped automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:119 |
| 869 | 这场会议还没有纪要，点击上方整理。 | This meeting has no notes yet; organize it above. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:509 |
| 870 | 这条内容已被重新生成，编辑未应用。 | This item was regenerated; the edit was not applied. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:143 |
| 871 |  回答暂时不可用；记录仍在继续。 |  Answers are temporarily unavailable; recording continues. | — |  |
| 872 | Tavily API Key（可选） | Tavily API key (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 873 | 一句话经验，例如：先说结论，再说依据 | One-line lesson, e.g. lead with the conclusion, then the evidence | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 874 | 云端识别已连接；转写发送到所选服务。 | Cloud transcription connected; the transcript is sent to the chosen service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:252 |
| 875 | 参考资料库时使用语义与关键词混合检索 | Use hybrid semantic and keyword retrieval for materials | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 876 | 在左边选择一个系列，或者先新建一个。 | Select a series on the left, or create one first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:33 |
| 877 | 在转写中查找（听错的写法也会找出来） | Search the transcript (misspelled forms are found too) | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 878 | 已取消，已经记录的内容仍保存在本机。 | Cancelled; what was recorded is still stored on this device. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:239 |
| 879 | 已把「%@」当作上一场，准备下一场。 | Treated “%@” as the previous meeting; preparing the next one. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:211 |
| 880 | 已框选读屏区域：%lld×%lld。 | Screen-read area selected: %lld×%lld. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:327 |
| 881 | 引导已完成；之后可在设置中重新查看。 | The guide is complete; see it again from settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:173 |
| 882 | 条目已编辑；生成结果的其他内容未改。 | Item edited; the rest of the generated content is unchanged. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:156 |
| 883 | 模板至少需要一个有标题和要求的章节。 | A template needs at least one section with a title and requirement. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:173 |
| 884 | 正确写法，例如 Kubernetes | Correct form, e.g. Kubernetes | — |  |
| 885 | 系统 %.0f · 麦克风 %.0f | System %.0f · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.Live.cs:292 |
| 886 | 译文未生成，原文已保留，可稍后补译： | Translation not generated; the original is kept, retry later:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:175 |
| 887 | 过期状态来自版本比较；旧内容仍可读。 | Stale state comes from revision comparison; old content stays readable. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 888 | 还没有导入 PPTX，导入后可翻页。 | No PPTX imported yet; import one to turn pages. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:107 |
| 889 | 这是连通性测试。只回复两个字：收到。 | This is a connectivity test. Reply with two words only: received. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:122 |
| 890 | 这段时间你讲得偏多，留点空间给对方。 | You have spoken a lot lately; leave room for them. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:314 |
| 891 | 连接检查通过 · 用时 %.1f 秒 | Connection check passed · %.1f s | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:181 |
| 892 | 选择一份资料，查看本机解析出的文本。 | Choose a material to see the text parsed on this device. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:22 |
| 893 | 长时间没有新的发言，会议已自动结束。 | No new speech for a long time; the meeting ended automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:118 |
| 894 | 领域字段（每行 key=value） | Domain fields (key=value per line) | — |  |
| 895 | ；Ctrl+Alt+N 唤回主窗口。 | ; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:623 |
| 896 | %lld 场会议 · 共 %lld 场 | %lld meetings · %lld total | — | Windows/src/NextThought.Desktop/AppState.cs:319 |
| 897 | %lld 条转写 · %lld 条回答 | %lld lines · %lld answers | — | Windows/src/NextThought.Desktop/AppState.Live.cs:11 |
| 898 | 保留查询与范围；可扩大范围或清除查询。 | Query and scope kept; widen the scope or clear the query. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 899 | 全文搜索：%lld 场会议有匹配内容。 | Full-text search: %lld meetings have matching content. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:108 |
| 900 | 切换鼠标穿透（穿透后只能用快捷键改回） | Toggle click-through (afterwards only a shortcut can restore clicks) | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 901 | 声纹模型已准备，可在设置中开启并保存。 | Speaker model ready; enable and save it in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:90 |
| 902 | 已保存 · 修订 12 · 03:05 | Saved · revision 12 · 03:05 | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 903 | 已删除 %lld 个系列及其会议记录。 | Deleted %lld series and their meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:365 |
| 904 | 已删除 %lld 场会议及其依赖内容。 | %lld meetings and their dependent content deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:60 |
| 905 | 已替换这道题的回答，旧回答留在历史里。 | The answer was replaced; the old one stays in history. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:343 |
| 906 | 必需：选择服务商，粘贴 API Key | Required: choose a provider and paste an API key | 必需：选择服务商，粘贴 API Key | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:19 |
| 907 | 所选系统输出设备混音 · 不采集麦克风 | Selected system output mix · microphone not captured | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:75 |
| 908 | 把还没有译文的完整句发送到当前回答服务 | Send untranslated complete sentences to the current answer service | — |  |
| 909 | 旧正文基于修订前原话；手动校正仍保留。 | The old body is based on the previous wording; manual corrections are kept. | — |  |
| 910 | 旧译文基于修订前原话；手动校正仍保留。 | The old translation follows the previous wording; manual corrections are kept. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 911 | 检查完即可开始；会中仍可随时切换场合。 | Start once the checks pass; switch scenes during the meeting too. | — |  |
| 912 | 正在记录。系统声音为所选输出设备混音。 | Recording. System audio is the chosen output mix. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:305 |
| 913 | 纪要已保存，可导出 Markdown。 | Notes saved; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:553 |
| 914 | 纪要模板：章节由模板决定，模型只填内容 | Note template: the template defines sections, the model fills them | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 915 | 线下会议只用麦克风，转写标为“现场”。 | Offline meetings use the microphone only; lines are marked as on-site. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:172 |
| 916 | 聆听已暂停；恢复后从当前时刻继续记录。 | Listening paused; resume from this point. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:225 |
| 917 | 说话人 %lld 已合并到 %lld。 | Speaker %lld merged into %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:276 |
| 918 | 请先选择一个系列，或在全局作用域编辑。 | Choose a series first, or edit in the global scope. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:217 |
| 919 | %@ · %lld 段 · %.2f 秒 | %@ · %lld sections · %.2f s | — | Windows/src/NextThought.Desktop/AppState.Live.cs:552 |
| 920 | Codex 模型（留空沿用账号默认模型） | Codex model (empty uses the account default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 921 | Ctrl+Alt+N 不挑场合，立即记录 | Ctrl+Alt+N records right away, whatever you are doing | — | Windows/src/NextThought.Desktop/Views/Home/StartComposer.axaml |
| 922 | NextThought · 把讨论留下来 | NextThought · Keep the discussion | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:40 |
| 923 | WAV 录音 (*.wav)\|*.wav | WAV recording (*.wav)\|*.wav | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:46 |
| 924 | 云端识别启动失败，且本机语音模型未就绪： | Cloud transcription failed and the on-device speech model is not ready:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:268 |
| 925 | 例如：先说结论再说依据；数据都标注来源。 | For example: state the conclusion first, then the evidence; mark every source. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 926 | 保存受阻 · 有 3 项待保存 · 重试 | Saving is blocked · 3 pending · Retry | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 927 | 保留了原来的经验，只记下这次会议的来源。 | Kept the original lesson; only the meeting source was recorded. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:344 |
| 928 | 先选场合、补资料、做检查，再开始这场会。 | Pick a scene, add materials, run the checks, then start. | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 929 | 全局快捷键（Ctrl+Alt + 按键） | Global shortcuts (Ctrl+Alt + key) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 930 | 删除会议「%@」？会议归档会从磁盘移除。 | Delete meeting “%@”? Its archive will be removed from disk. | — |  |
| 931 | 已导入模板，共 %lld 个自定义模板。 | Imported %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:117 |
| 932 | 已录入（来自旧模型的声纹，需要重新录入） | Enrolled (voiceprint from an old model; enroll again) | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:96 |
| 933 | 已把 %lld 条决定/行动加入待复核。 | Added %lld decisions/actions to the review list. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:110 |
| 934 | 已把「%@」从「%@」移到系列「%@」。 | Moved “%@” from “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:474 |
| 935 | 已收集 %lld 条知识候选，等待复核。 | Collected %lld knowledge candidates for review. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:237 |
| 936 | 截图与 OCR 文本已就绪；可直接发送。 | Screenshot and OCR text ready; send it directly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:474 |
| 937 | 截图已就绪；可直接发送，或补充你的问题。 | Screenshot ready; send it directly or add your question. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:474 |
| 938 | 正在联网核实（使用 %@ 的查证结果）… | Verifying online (using %@ verification results)… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:568 |
| 939 | 讲稿在左 · 回答在右（演讲 / 答辩） | Script left · answers right (talk / defense) | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 940 | 还没有幻灯片；导入 PPTX 后可翻页。 | No slides yet; import a PPTX to turn pages. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:160 |
| 941 | 还没有术语；添加一个正确写法和常见误听。 | No glossary entries; add a correct form and common mishearing. | — |  |
| 942 | 麦克风增益（1–8 倍，声音偏小时调高） | Microphone gain (1–8×, raise for quiet audio) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 943 | # %@⏎⏎会议时间：%@ · 场景：%@ | # %@

Meeting time: %@ · Scene: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:400 |
| 944 | # %@⏎⏎会议时间：%@ · 模板：%@ | # %@

Meeting time: %@ · Template: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:202 |
| 945 | X-ASR 中英 · 480 ms（推荐） | X-ASR bilingual · 480 ms (recommended) | X-ASR 中英 · 480 ms（推荐） | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:46 |
| 946 | 保存后立即应用；跟随系统会随后台主题切换。 | Applied after saving; Follow system tracks the OS theme. | — |  |
| 947 | 切换为只记录：不再自动回答，仍可手动提问。 | Switched to record-only: no automatic answers, but you can still ask by hand. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 948 | 原凭据无法由当前用户解密，请重新输入密钥。 | The current user cannot decrypt the stored credential; enter the key again. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:772 |
| 949 | 原始音频不保存；转写与回答保存在「记录」里 | Raw audio is not kept; the transcript and answers stay under Records. | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 950 | 回答不超过三句话：先给结论，再给一条依据。 | Answer in at most three sentences: conclusion first, then one reason. | — |  |
| 951 | 已保存 %lld 个技能，下一场回答生效。 | Saved %lld skills; used in the next answer. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 952 | 已保存（当前 Windows 用户加密）。 | Saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:344 |
| 953 | 录音已通过云端转写并归档，未保存原始音频。 | Recording transcribed by the cloud and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:429 |
| 954 | 整理结果不是可用的 JSON，稍后可重试。 | The result was not usable JSON; try again later. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:408 |
| 955 | 正在检索相关资料；首次使用时生成本地索引… | Searching materials; the local index is built on first use… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:548 |
| 956 | 演示模式：内置样本数据，不会写入你的资料。 | Demo mode: built-in sample data, nothing is written to your materials. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:116 |
| 957 | 记录时将完整句中英互译（使用当前回答服务） | Translate full sentences while recording (uses the answer service) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 958 | 请解释这张截图，指出关键信息、数字或风险。 | Explain this screenshot and point out key facts, numbers or risks. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:472 |
| 959 | 转写已在界面更正，但用户更正记录写入失败： | The transcript was corrected on screen, but writing the correction record failed:  | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:77 |
| 960 | 还不能开始记录：请先在设置里准备语音组件。 | Cannot start recording yet: prepare the speech components in settings. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:343 |
| 961 | 采纳后进入这个系列的经验，下次回答会带上它 | Accepted lessons join this series and are used in later answers | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:210 |
| 962 | 预览态由演示环境注入，不代表鼠标事件录制。 | Preview state is injected by the demo; it does not record pointer events. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 963 |  已保存 · 修订 12 · 03:05 |  Saved · revision 12 · 03:05 | — |  |
| 964 |  旧正文基于修订前原话；手动校正仍保留。 |  The old body is based on the previous wording; manual corrections are kept. | — |  |
| 965 | ，其中 %lld 条按文字长度估算结束时间 | , %lld with estimated end times | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:358 |
| 966 | 共 %lld 份资料，均在本机解析，不上传。 | %lld materials, all parsed on this device, never uploaded. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:42 |
| 967 | 切换为自动回答：识别到问题后会生成参考回答。 | Switched to automatic answers: a reference answer is generated when a question is recognized. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 968 | 外接屏已断开，双屏演讲已退出，主窗口已恢复。 | External display disconnected; presenter mode exited; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:622 |
| 969 | 如果发布窗口需要延长，我们会先核对回滚指标。 | If the release window needs more time, we will check the rollback metrics first. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 970 | 密钥已保存（当前 Windows 用户加密） | Key saved (encrypted for the current Windows user) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 971 | 已删除会议「%@」，已确认的系列内容已保留。 | Deleted meeting “%@”; accepted series content kept. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:139 |
| 972 | 已按《%@》整理，可导出 Markdown。 | Organized with “%@”; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:251 |
| 973 | 把资料加入系列请在资料库中选择；这里可移除。 | Add materials to the series from the library; remove them here. | — |  |
| 974 | 术语表已保存 %lld 条，下一场会议生效。 | Glossary saved (%lld terms); used in the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:484 |
| 975 | 没有符合条件的记录，可以清除条件后查看全部。 | No records match the filters; clear them to see everything. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 976 | 系统声音与麦克风分别处理；只在本机保存文字。 | System audio and microphone are handled separately; only text is stored locally. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 977 | 还没有会议记录。点击「开始记录」写下第一场。 | No meetings yet. Choose Start recording to write the first one. | — |  |
| 978 | 鼠标移到提词岛上，可以暂停、结束或立即回答。 | Hover the cue island to pause, end or answer now. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 979 |  保存受阻 · 有 3 项待保存 · 重试 |  Saving is blocked · 3 pending · Retry | — |  |
| 980 | Word 文档 (*.docx)\|*.docx | Word document (*.docx)\|*.docx | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:159 |
| 981 | 修改保存在独立用户版本里；原话依据只用于核对。 | Edits are kept in a separate user version; the original wording is only used for checking. | — | Windows/src/NextThought.Desktop/Views/NotesEditorWindow.axaml |
| 982 | 浮在屏幕上的大字提词窗（Ctrl+Alt+T） | Large teleprompter over the screen (Ctrl+Alt+T) | — |  |
| 983 | 还没有可用的资料：先导入资料，再整理讨论重点。 | No usable materials yet: import materials first, then prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:78 |
| 984 | 这次没有新的经验和问题：之前整理过同一份纪要。 | No new lessons or questions: these notes were already organized. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:316 |
| 985 | 允许联网核实（需 TAVILY_API_KEY） | Allow web verification (needs TAVILY_API_KEY) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 986 | 如果时间允许，我想再补充一下数据和风险这两部分。 | If time allows, I would like to add the data and risk parts. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:17 |
| 987 | 已取消「这是我」，这场会议里不会再识别为该声音。 | “This is me” cancelled; this voice is no longer recognized as you in this meeting. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:138 |
| 988 | 已整理重复项（%lld 条 → %lld 条）。 | Merged duplicates (%lld entries → %lld entries). | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:341 |
| 989 | 已跳过 %lld 行损坏记录，其余内容仍可查看。 | Skipped %lld damaged lines; the rest is still readable. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:473 |
| 990 | 手机伴侣已开启；手机扫码即可查看当前问题与回答。 | Phone companion is on; scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 991 | 点「开始记录」就能开会；也可以先到准备页补资料。 | Choose Start recording to begin; add materials on the prepare page first if you like. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:34 |
| 992 | 联网未配置：先在设置里保存 Tavily 密钥。 | Web search is not configured: save a Tavily key in settings. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:72 |
| 993 | 记下复核结论：哪些页需要人工核对、是否补齐原件。 | Record the review result: which pages need a human check and whether the original is missing. | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 994 | 请先选择一个局域网地址，或在设置里填写中转地址。 | Choose a LAN address, or set a relay URL in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:137 |
| 995 | 转写会显示在这里。也可以导入录音 WAV 验证。 | The transcript appears here. You can also import a WAV recording to try it. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 996 | 这个系列还没有自己的术语；全局条目仍会叠加生效。 | This series has no terms of its own yet; global entries still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:75 |
| 997 | 这是这个系列的下一场：会自动带上它的资料与经验。 | This is the next meeting of this series: its materials and lessons are carried in automatically. | — |  |
| 998 | %@ · %@ · %lld 段 · 解析器 %@ | %@ · %@ · %lld sections · parser %@ | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:95 |
| 999 | NextThought 备份\|*.ntbackup | NextThought backup\|*.ntbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:196 |
| 1000 | 先准备声纹组件（约 26 MB），再录入我的声音。 | Prepare the speaker component (about 26 MB), then enroll my voice. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:104 |
| 1001 | 勾选的技能只加入这个系列的回答；全局技能仍然生效。 | Checked skills apply to this series only; global skills still apply. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1002 | 双屏演讲：%@；Ctrl+Alt+N 唤回主窗口。 | Presenter mode: %@; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:656 |
| 1003 | 左侧灰色为对方（系统声音），右侧彩色为我（麦克风） | Grey on the left is the other side (system audio), colour on the right is me (microphone) | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1004 | 已删除 %lld 场会议；已确认的系列内容已保留。 | %lld meetings deleted; accepted series content kept. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:59 |
| 1005 | 所选系统输出设备混音 + 麦克风 · 在设置中调整 | Selected system output mix + microphone · adjust in settings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:74 |
| 1006 | 正在按《%@》整理…（第 %lld/%lld 段） | Organizing with “%@”… (part %lld of %lld) | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:221 |
| 1007 | 此数据目录已有 NextThought 窗口运行。 | A NextThought window is already using this data folder. | — | Windows/src/NextThought.Desktop/App.axaml.cs:51 |
| 1008 | 没有匹配的会议。换个关键词，或把筛选切回「全部」。 | No matching meetings. Try another keyword or switch the filter back to All. | — |  |
| 1009 | 结束会议后，按转写整理总结、决定、待办和未决问题。 | After a meeting, organize the transcript into a summary, decisions, actions and open questions. | — | Windows/src/NextThought.Desktop/AppState.cs:283 |
| 1010 | 语义检索模型已准备，勾选语义检索并保存设置后使用。 | Semantic model ready; enable semantic retrieval and save settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:98 |
| 1011 | 语音已就绪。可以开始记录，或先导入 WAV 验证。 | Speech is ready. Start recording, or import a WAV to try it. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:63 |
| 1012 | 还没有系列。上面的输入框新建一个，再把会议放进来。 | No series yet. Create one above, then add meetings to it. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1013 | 随手记；保存到 user-content.json | Jot a note; saved to user-content.json | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1014 | 鼠标穿透中 · 快捷键 Ctrl+Alt+T 切回 | Click-through on · Ctrl+Alt+T restores clicks | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml.cs:253 |
| 1015 | %lld 秒后自动结束；说话或点「继续记录」可取消。 | %lld seconds until auto-end; speaking or Keep recording cancels it. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:127 |
| 1016 | 之前有效回答仍可读。取消后到达的旧请求结果会被丢弃。 | Earlier answers stay readable. Results from cancelled requests are discarded. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1017 | 先到设置准备语音组件；未连接回答服务时也可以只记录。 | Prepare the speech components in settings first; recording alone works without an answer service. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:64 |
| 1018 | 删除选中的 %lld 场会议？此操作会移除归档目录。 | Delete %lld selected meetings? Their archive folders will be removed. | — |  |
| 1019 | 快捷键行要写成 动作=按键，例如 record=R。 | Write one action=key per line, for example record=R. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:190 |
| 1020 | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | This meeting uses %lld ready materials; readable text is enough to start. | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:77 |
| 1021 | 检测到屏幕共享，提示岛保持收回；共享结束后自动恢复。 | Screen sharing detected; the cue island stays retracted and returns when sharing ends. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:524 |
| 1022 | 窗口“%@”已关闭或无法捕获，请刷新窗口列表后重试。 | The window “%@” was closed or cannot be captured; refresh the window list and try again. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:244 |
| 1023 | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | The questions asked, your answers and what to prepare afterwards stay here. | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1024 | 设置已保存，密钥已用当前 Windows 用户加密。 | Settings saved; the key is encrypted for the current Windows user. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:60 |
| 1025 | 这是「术语和人名」里记下的听错写法，搜索时一并找出来 | A misspelling saved under Terms and names; search finds it too | — | Windows/src/NextThought.Desktop/GlossaryRows.cs:12 |
| 1026 | 需要最新信息时联网核实回答；不联网时只用资料和转写。 | Look the answer up on the web when up-to-date information is needed; offline uses only materials and the transcript. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1027 | ；%lld 个重复项保留现有版本。导入前备份：%@。 | ; %lld duplicates kept the existing versions. Pre-import backup: %@. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:222 |
| 1028 | 合成版本差异样例；实际过期标记由归档投影版本比较产生。 | Synthetic revision-diff sample; real stale markers come from archive projection comparison. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1029 | 命名后应用到当前转写；也可以把某一声道标成「这是我」。 | Names apply to the current transcript; you can also mark a channel as “Me”. | — | Windows/src/NextThought.Desktop/Views/SpeakerEditorWindow.axaml |
| 1030 | 密钥读取失败，已保留原密钥；如需更换请直接输入新密钥。 | Reading the key failed; the existing key was kept. Enter a new key to replace it. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:778 |
| 1031 | 已整理成%@；%lld 条找不到原话依据的内容已丢弃。 | Organized into %@; dropped %lld items with no transcript support. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:480 |
| 1032 | 提词浮窗已开启鼠标穿透，Ctrl+Alt+J 可恢复。 | Teleprompter click-through is on; Ctrl+Alt+J restores clicks. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:486 |
| 1033 | 还没有条目。点「新增」，或用「批量粘贴」一次加入多条。 | No entries yet. Choose New, or use Bulk paste to add several at once. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:56 |
| 1034 | 这个问题我的理解是，先确认需求，再决定用哪种方案去做。 | My understanding is to confirm the need first, then decide on an approach. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:15 |
| 1035 | 选择系统输出混音或麦克风，并在设置里准备本机语音模型。 | Choose the system output mix or the microphone, and prepare the on-device speech model in settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:32 |
| 1036 | 音频只在本机识别，不保存录音；文字与回答只保存在本机。 | Audio is recognized on this device only and no recording is kept; text and answers stay on this device. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1037 | 会前准备：这个系列已有 %lld 份资料，均在本机解析。 | Preparation: this series already has %lld materials, all parsed on this device. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:313 |
| 1038 | 兼容 Chat Completions 的 API 地址 | Chat Completions-compatible API URL | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1039 | 删除当前筛选的 %lld 场会议？此操作会移除归档目录。 | Delete the %lld filtered meetings? Their archive folders will be removed. | — |  |
| 1040 | 回答过的问题会按时间列在这里，点一下就能回看当时的回答。 | Answered questions are listed here by time; choose one to see the answer as it was. | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 1041 | 归档有缺失或读取错误（跳过 %lld 行），导出已暂停。 | The archive has missing or unreadable rows (%lld lines skipped); the export is paused. | — | Windows/src/NextThought.Desktop/AppState.SessionExport.cs:159 |
| 1042 | 纪要整理已取消；原始转写和已完成片段已保留，可继续整理。 | Note generation cancelled; the transcript and finished parts were kept. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:558 |
| 1043 | 这些字段会进入这个系列的准备页和回答提示；留空表示不用。 | These fields feed this series' prepare page and answer prompt; leave them empty to skip. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1044 | 删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。 | Delete series “%@”? The series and its meeting records will be permanently deleted and cannot be recovered. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:205 |
| 1045 | 对方 · 系统声音 %.0f · 我 · 麦克风 %.0f | Them · system audio %.0f · Me · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:147 |
| 1046 | 框选屏幕区域，存入资料库并 OCR（Ctrl+Alt+S） | Select a screen area, save it to the library and OCR it (Ctrl+Alt+S) | — |  |
| 1047 | 没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。 | No window to capture was found. Open the window first, then choose Refresh. | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:27 |
| 1048 | 自检：安装包已下载并通过 SHA-256，启动命令已记录。 | Self-test: installer downloaded and SHA-256 verified; launch command recorded. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:286 |
| 1049 | 还没有配置回答服务；整理讨论重点需要它，记录和转写不需要。 | No answer service is configured yet; preparing discussion points needs one, recording and transcription do not. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:42 |
| 1050 | Tavily 密钥已保存（当前 Windows 用户加密）。 | Tavily key saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:66 |
| 1051 | 今天上午我们先把上周的进展过一遍，然后讨论接下来两周的安排。 | This morning let us review last week's progress, then plan the next two weeks. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:14 |
| 1052 | 对着麦克风读一句话，观察下方电平。开始记录后这里会实时变化。 | Read a sentence into the microphone and watch the level below; it changes once recording starts. | — | Windows/src/NextThought.Desktop/Views/OnboardingVoiceWindow.axaml |
| 1053 | 已处理 %lld 场中断会议；可在历史中继续查看或整理纪要。 | Recovered %lld interrupted meetings; view or organize them in history. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:121 |
| 1054 | 已导入 %lld 场会议、%lld 份资料、%lld 个系列 | Imported %lld meetings, %lld materials, %lld series | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:220 |
| 1055 | 手机伴侣已开启（中转通道）；手机扫码即可查看当前问题与回答。 | Phone companion is on (relay); scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 1056 | 补译已结束；有译文的句子已分别保存。未成功的句子可再次补译。 | Translation finished; translated sentences were saved. Retry the ones that failed. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:455 |
| 1057 | 识别为问题的句子会标出；把鼠标移到任意一句上可以「回答这句」 | Sentences recognized as questions are marked; rest the pointer on any line to “Answer this” | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1058 | 这次回复不是可用的 JSON，可以重试；原来的纪要保持不变。 | The reply was not usable JSON; retry. The previous notes are unchanged. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:234 |
| 1059 | 中转已连接 · 端到端加密。配对链接走中转，不必在同一局域网。 | Relay connected · end-to-end encrypted. Pairing links use the relay, so no shared LAN is needed. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:565 |
| 1060 | 最近结果：先看命中率和 P95 延迟，再比较灰度前后的错误率。 | Latest results: check hit rate and P95 latency, then compare error rates before and after the rollout. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1061 | 还没有可用的语音识别；可以先只记录声音，或到设置准备语音组件。 | No speech recognition is ready yet; record the sound only for now, or set up the speech components. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:189 |
| 1062 | 还没有记录。从这个资料夹开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:212 |
| 1063 | 这场只记录对方的声音；想连自己的话一起记，可以重新打开麦克风。 | This meeting records the other side only; reopen the microphone to include your own voice. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:318 |
| 1064 | 这条建议对不上了：原话或已有内容在复核之后改过，刷新后再确认。 | This suggestion no longer matches: the original or existing content changed after review. Refresh and confirm again. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:82 |
| 1065 | NextThought 把会议讨论留在本机，变成可以带走的结论。 | NextThought keeps meeting discussions on this device and turns them into conclusions you can take away. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1066 | 「%@」有 %lld 条经验和问题待确认，确认后才会进入知识库。 | “%@” has %lld lessons and questions to confirm; nothing enters the knowledge base until you confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:323 |
| 1067 | 系列术语只叠加在这个系列的回答、纪要与查找里；全局术语仍然生效。 | Series terms only layer onto this series' answers, notes and search; global terms still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:74 |
| 1068 | 还没有术语。每行一个：正确写法 \| 识别错的写法 \| 另一种错法 | No glossary entries. One per line: correct form \| misheard form \| another mishearing | — |  |
| 1069 | 仅保留会议内编号，不保存声纹或原始音频。短句和不确定结果保持未知。 | Only in-meeting numbers are kept; voiceprints and raw audio are not stored. Short or uncertain results stay unknown. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1070 | 整理出的%@缺少可核对的转写依据，已丢弃；可重试，或改用文字纪要。 | The %@ lacked verifiable transcript support and was dropped; retry, or use text notes instead. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:415 |
| 1071 | 转写保存在本机。使用远程回答服务时，问题和相关转写会发送到该服务。 | Transcripts stay on this device. With a remote answer service, the question and relevant transcript are sent to that service. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1072 | 这条建议对不上了：题库里的回答在整理之后改过，重新整理一次再确认。 | This suggestion no longer matches: the question-bank answer changed after organizing. Organize again, then confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:333 |
| 1073 | 题库里已有相似问题时，「替换」会用新回答覆盖旧的，旧回答留在历史里 | If a similar question exists, Replace overwrites its answer; the old one stays in history | — |  |
| 1074 | 个人笔记保存在 user-content.json，不覆盖生成纪要。 | Personal notes are saved to user-content.json and never overwrite generated notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1075 | 按这场会议的转写整理总结、决定、待办和未决问题；原始文字不会被改写。 | Summarize decisions, actions and open questions from the transcript; the original text is not rewritten. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1076 | 文字纪要由模板整理；鱼骨图、大纲与观点对照是同一场会议的另一种看法。 | Text notes come from a template; fishbone, outline and viewpoint views are other ways to read the same meeting. | — | Windows/src/NextThought.Desktop/AppState.cs:351 |
| 1077 | 正在安装更新，NextThought 会退出并在安装完成后重新打开。 | Installing the update; NextThought will exit and reopen when it finishes. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:310 |
| 1078 | 添加正确写法与常见误听；识别时作为热词，转写中的错写会改回正确写法。 | Add correct forms and common mishearings; correct forms become hotwords and misheard forms are rewritten. | — |  |
| 1079 | 题库还空着。面试、汇报这类模板的「逐题回放」章节会在确认后进入题库。 | The question bank is empty. Question-by-question sections from interview or talk templates enter the bank after confirmation. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1080 | %lld 条术语 · 识别时作为热词，转写里出现的错写会被改回正确写法 | %lld terms · used as recognition hotwords; misheard forms are rewritten | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:81 |
| 1081 | 只更正名字与术语的识别错误，改动写入 corrections.json | Only fixes misrecognized names and terms; changes are written to corrections.json | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1082 | 安装包已在后台下载并通过 SHA-256 校验；点「重启以更新」安装。 | The installer was downloaded and verified with SHA-256; choose Restart to update to install. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:216 |
| 1083 | 快捷键必须包含 Ctrl、Alt 或 Shift，不能只使用普通按键。 | A shortcut must include Ctrl, Alt or Shift; a plain key is not enough. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:195 |
| 1084 | 所有窗口已收起，会议继续记录。Ctrl+Alt+\ 或托盘图标可恢复。 | All windows hidden; the meeting keeps recording. Ctrl+Alt+\ or the tray icon restores them. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:155 |
| 1085 | 把同一门课、同一个项目、同一类面试的会议放在一个系列里，经验会攒下来。 | Keep meetings from one course, project or interview loop in a series; lessons accumulate. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:29 |
| 1086 | 按下带 Ctrl/Alt/Shift/Win 的组合键；Esc 取消。 | Press a combination with Ctrl/Alt/Shift/Win; Esc cancels. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:435 |
| 1087 | 每一场会议都存在本机。搜索标题、日期或模式，看不清的细节可以回到原话。 | Every meeting stays on this device. Search by title, date or mode, and return to the exact words when details are unclear. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 1088 | 没有找到可用的局域网 IPv4 地址；请先连接 Wi-Fi 或以太网。 | No usable LAN IPv4 address; connect Wi-Fi or Ethernet first. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:116 |
| 1089 | 生成结果保持不变；这里的修改写入 user-content.json。 | Generated content stays unchanged; edits here are written to user-content.json. | — |  |
| 1090 | 还没有高亮。在转写里点「高亮」记下一句，会把原话时间一起存进个人笔记。 | No highlights yet. Choose Highlight in the transcript to keep that line, with its original timestamp, in your personal notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1091 | 这场会只保存转写与纪要，不自动生成回答。需要回答时可随时在输入框提问。 | This meeting only saves the transcript and notes; answers are not generated automatically. Ask in the input box whenever you need one. | — |  |
| 1092 | 选中文字荧光为合成标记；原生 textSelection 高亮未伪造。 | The text fluorescence is a synthetic marker; native textSelection highlighting is not faked. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1093 | 高亮与这场的原话关联；保存后写入 user-content.json。 | Highlights link to this meeting's original lines; saving writes them to user-content.json. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1094 | NextThought 备份\|*.ntbackup;*.ihbackup | NextThought backup\|*.ntbackup;*.ihbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:209 |
| 1095 | 备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。 | Backup exported: contains meetings and materials; excludes API keys, speech models and device settings. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:201 |
| 1096 | 将调用回答服务把转写整理成「%@」；每条都带原话时间，找不到依据的不写。 | The answer service will organize the transcript into “%@”; every entry carries its original time and unsupported items are dropped. | — | Windows/src/NextThought.Desktop/AppState.cs:352 |
| 1097 | 还没有配置回答服务：到 设置 → 回答 填写 API Key 后再检查。 | No answer service is configured yet: fill in an API key under Settings → Answers, then check again. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:103 |
| 1098 | X-ASR 流式识别加整句精修，CPU 离线运行；原始音频只在内存中经过。 | X-ASR streaming plus sentence refinement runs offline on CPU; raw audio only passes through memory. | — |  |
| 1099 | 会中的回答和会后的纪要都由它来写。选一个服务商，粘贴 Key，点一下检查。 | It writes the in-meeting answers and the meeting notes. Pick a provider, paste a key and check. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1100 | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Answers automatically · concise answers · notes: needs, concerns and replies, what we promised, next steps | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Windows/src/NextThought.Desktop/MeetingDraft.cs:64 |
| 1101 | 会话 %lld · 问题 %lld · 经验 %lld · 题库 %lld | sessions %lld · questions %lld · lessons %lld · bank %lld | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:119 |
| 1102 | 决定与行动先留在这里复核；负责人和期限可在这里确认，确认后才进入系列记录。 | Decisions and actions wait here for review; owner and due date can be confirmed here before they enter the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:58 |
| 1103 | 这场会议还不属于任何系列。到「系列」页把它加进去，之后整理纪要就会攒经验。 | This meeting is not in a series. Add it on the Series page to accumulate lessons when notes are generated. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:310 |
| 1104 | 首页的场景会决定默认的会中模式、回答风格、纪要模板与提词方式。随时可以改。 | The Home scene sets the default in-meeting mode, answer style, note template and cue style. You can change it anytime. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:31 |
| 1105 | 选云端后，导入 WAV 走云端识别；实时记录仍用本机。音频会发送到所选服务。 | With cloud selected, WAV import uses the cloud; live recording stays on-device. Audio is sent to the chosen service. | — |  |
| 1106 | 已在 %@:%lld 监听 · 配对链接相当于临时遥控密钥，只在可信网络使用。 | Listening on %@:%lld · the pairing link is a temporary remote key, so use it only on trusted networks. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:146 |
| 1107 | ## 客户会⏎### 决定⏎只写已经拍板的事。⏎### 待办⏎写清负责人和日期。 | ## Client meeting
### Decisions
Only write what was decided.
### Actions
State the owner and the date. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1108 | 切换后重启生效；当前覆盖主导航、设置分节与常用按钮，动态状态和会议内容保持原文。 | Takes effect after a restart; covers navigation, settings sections and common buttons. Status text and meeting content stay in their original language. | — |  |
| 1109 | 同一门课、同一个项目、同一类面试放在一个系列里；每场会议的结论与经验会攒在一起。 | Keep one course, project or interview loop in a series; conclusions and lessons accumulate across meetings. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1110 | 开会时把提词岛留在摄像头这一屏，幻灯片屏不出现任何 NextThought 窗口 | Keep the cue island on the camera display; no NextThought window appears on the slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1111 | 接入兼容 Chat Completions 的回答服务；只记录与转写时可以不填。 | Connect a Chat Completions-compatible answer service; leave it empty to just record and transcribe. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:33 |
| 1112 | 没有待确认的知识。按模板整理一份纪要后，模板里标了「带出经验」的章节会排到这里。 | Nothing to confirm. After organizing notes, sections marked to carry lessons appear here. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1113 | 还没有配置回答服务：到 设置 → 回答 填好 API Key 后可整理讨论重点。 | No answer service is configured yet: fill in an API key under Settings → Answers to prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:71 |
| 1114 | 题库里已有相似问题时，「采用建议并保留旧版」会用新回答覆盖旧的，旧回答留在历史里 | When the question bank already has a similar question, “Apply and keep the old version” replaces it with the new answer; the old answer stays in history. | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:209 |
| 1115 | 首次需下载流式模型约 134 MB；整句精修另约 136 MB，下载后离线运行。 | The streaming model is about 134 MB; sentence refinement adds about 136 MB. Both run offline after download. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1116 | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Answers automatically · concise answers · notes: review each question (saved to the question bank), what went poorly, next round | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Windows/src/NextThought.Desktop/MeetingDraft.cs:60 |
| 1117 | 还没有资料。导入 PDF、Word、PPT、Excel、Markdown 或图片。 | No materials yet. Import PDF, Word, PPT, Excel, Markdown or images. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:41 |
| 1118 | 原生控件样板 · 所有文本与状态均为合成演示，不访问麦克风、ASR、模型、网络或归档。 | Native control samples · all text and states are synthetic; no microphone, ASR, model, network or archive access. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1119 | 开会、听讲、面谈。NextThought 在本机把声音变成文字，再变成可以带走的结论。 | Meetings, lectures, interviews. NextThought turns speech into text on this device, then into conclusions you can take away. | — |  |
| 1120 | 还没有经验。整理纪要时会从「下次改进」这类章节提炼候选，确认后才会记下来；也可以手写一条。 | No lessons yet. When notes are organized, sections such as Next time produce candidates; nothing is saved until you confirm, and you can also write one. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1121 | 已捕获 %lld×%lld 像素（%lld,%lld）。拖动选择区域，然后点「使用此区域」。 | Captured %lld×%lld pixels (%lld,%lld). Drag to select an area, then choose Use this area. | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml.cs:29 |
| 1122 | 重新整理这份纪要？这场会议的纪要你改过。重新整理会生成新的 AI 版本，你改过的版本会保留。 | Regenerate these notes? You edited this meeting's notes. Regenerating creates a new AI version and keeps your edited version. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml.cs:159 |
| 1123 | 保存设置后立即生效；当前覆盖主导航、设置分节、常用按钮与说明文案，动态状态和会议内容保持原文。 | Takes effect immediately after saving; covers navigation, settings sections, common buttons and help text. Status and meeting content stay in their original language. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1124 | 密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。 | The key is stored only for the current Windows user; TAVILY_API_KEY still works too. | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:29 |
| 1125 | 导入 PPT、Keynote 或 PDF 后，放映时认出正在讲第几页，给出这一页的要点与提问。 | Import PPT, Keynote or PDF and the app recognizes the current slide while presenting, offering key points and questions for it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1126 | 截图会裁切到选中范围，然后存入资料库并由系统 OCR 读取文字。请把题目、代码或公式完整框入。 | The screenshot is cropped to the selection, saved to the library and read with system OCR. Frame the question, code or formula completely. | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 1127 | 把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「导入资料」。 | Drop PDF, Word, PPT, Excel, Markdown or images here, or choose Import materials. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:234 |
| 1128 | 还没有生成这种纪要。点上方「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This kind of note has not been generated yet. Choose Generate this note above to turn the transcript into it with the current answer service; every line keeps its original timestamp. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1129 | Windows 没有系统翻译框架：开启后完整句会发送到所选回答服务，与 Mac 的本机翻译不同。 | Windows has no system translation framework: complete sentences are sent to the chosen answer service, unlike the on-device translation on Mac. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1130 | 还没有生成这种纪要。点右上角「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This note has not been generated. Choose Generate this note to organize the transcript with the current answer service; every entry carries its original time. | — |  |
| 1131 | 问题涉及最新价格、版本、新闻等时检索 Tavily，并把带链接的结果交给回答；没有密钥时保持离线。 | Searches Tavily for the latest prices, versions or news and passes linked results to the answer; stays offline without a key. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1132 | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Answers automatically and follows the script · concise answers · notes: audience questions reviewed one by one (saved to the question bank), what to change, promised follow-ups | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Windows/src/NextThought.Desktop/MeetingDraft.cs:61 |
| 1133 | 安装包签名有效，但签名者不在允许列表（NEXTTHOUGHT_SIGNER_THUMBPRINT）。 | The installer signature is valid but the signer is not on the allow list (NEXTTHOUGHT_SIGNER_THUMBPRINT). | — | Windows/src/NextThought.Desktop/AppState.Update.cs:272 |
| 1134 | 默认关闭；手机与电脑需在同一可信网络。只发送当前问题、回答与状态，不发送资料、完整转写、音频或密钥。 | Off by default; phone and PC must share a trusted network. Only the current question, answer and status are sent — never materials, full transcripts, audio or keys. | — | Windows/src/NextThought.Desktop/AppState.cs:206 |
| 1135 | 选云端后，实时记录与导入 WAV 都走云端识别；音频会发送到所选服务。只选本机时，录音与识别都在本机。 | With cloud selected, both live recording and WAV import use cloud recognition and audio is sent to the chosen service; with local only, recording and recognition stay on this device. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1136 | 默认关闭；开启后手机与电脑需在同一可信网络。首次监听时 Windows 防火墙会询问，请只允许专用网络。 | Off by default; once on, phone and PC must share a trusted network. Windows Firewall asks on the first listen — allow private networks only. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:117 |
| 1137 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：总结、要点、决定、待办 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: summary, key points, decisions, todos | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:59 |
| 1138 | 还没有资料。点「导入资料」把 PDF、Word、PPT、Excel、Markdown 或图片加进这个系列。 | No materials yet. Choose Import materials to add PDF, Word, PPT, Excel, Markdown or images to this series. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1139 | 还没有资料；导入 PDF、Word、PPT、Excel、Markdown 或图片后，这里显示页数与段落数。 | No materials yet; once you import PDF, Word, PPT, Excel, Markdown or images, their page and paragraph counts appear here. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:32 |
| 1140 | .ntbackup 包含会议、资料与设置；不包含 API Key、语音模型与设备设置。卸载不会删除数据目录。 | .ntbackup contains meetings, materials and settings; it excludes API keys, speech models and device settings. Uninstalling does not delete the data folder. | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml |
| 1141 | 每行一个：正确写法 \| 识别错的写法 \| 另一种错法。正确写法会作为识别热词；错写法出现在转写里时会被改回。 | One per line: correct form \| misheard form \| another misheard form. Correct forms become recognition hotwords; misheard forms are rewritten in the transcript. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1142 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：决定、待办、风险、下次会议 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: decisions, todos, risks, next meeting | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:63 |
| 1143 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：各方观点、结论与分歧、分工 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: each side's view, conclusions and disagreements, assignments | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:66 |
| 1144 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：要点、概念、例子、我的疑问 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: key points, concepts, examples, my questions | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:65 |
| 1145 | Kubernetes \| K8s \| 库伯奈提斯⏎同济大学 \| 同技大学⏎OpenSees \| open sees | Kubernetes \| K8s \| kubernets
Tongji University \| Tongji Uni
OpenSees \| open sees | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1146 | 导入 PDF、Word、PPT、Excel、Markdown 与图片；解析在本机完成，检索时只把相关摘录发给回答服务。 | Import PDF, Word, PPT, Excel, Markdown and images; parsing is local and only relevant excerpts are sent to the answer service. | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 1147 | NextThought 备份\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | NextThought backup\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:46 |
| 1148 | 「下载并安装」会下载安装包并校验发布里的 SHA-256，校验通过后静默安装并重新打开；发布没有校验文件时只提供下载页。 | Download and install fetches the installer and verifies its published SHA-256, then installs silently and reopens; without a checksum only the download page is offered. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1149 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：定下的方案、分工、我的任务、下次碰头 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: chosen plan, assignments, my tasks, next meetup | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:67 |
| 1150 | 手机与电脑连接同一可信网络后开启。手机扫码用浏览器打开，可查看当前问题、回答与状态，并触发回答、简短版、举例或停止生成。 | Turn on after the phone and PC join the same trusted network. Scan the code in a browser to see the current question, answer and status, and to trigger answer, shorter, example or stop. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1151 | 所有偏好保存在本机数据目录的 settings.json；API Key 用当前 Windows 用户加密，不会写进备份。 | Preferences live in settings.json in the local data folder; the API key is encrypted for the current Windows user and never written to backups. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1152 | 蓝牙备用通道暂不可用：手机端仍按 IH- 广播名过滤，Windows 无法自定义广播名，等手机端改成按服务标识扫描后再开放。 | The Bluetooth fallback is unavailable: the phone still filters by the IH- broadcast name, Windows cannot rename its broadcast, and it stays closed until the phone scans by service ID. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1153 | 填写后配对链接走 HTTPS/WSS 中转（端到端加密），不必在同一局域网；留空则只用局域网。改地址后重新开启伴侣并重新配对。 | When set, pairing uses an HTTPS/WSS relay (end-to-end encrypted) and no shared LAN is needed; leave empty for LAN only. Re-enable and re-pair after changing it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1154 | 局域网 HTTP 不加密；配对链接相当于临时遥控密钥，请仅在可信网络使用。不会发送完整转写、资料文件、音频或 API Key。 | LAN HTTP is unencrypted; the pairing link is a temporary remote key, so use it only on a trusted network. Full transcripts, material files, audio and API keys are never sent. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1155 | 选应用时仅采集该进程及其子进程的声音；选整个设备时采集该设备全部混音。需要 Windows 10 build 20348 以上。 | App capture records only that process and its children; device capture records the whole mix. Requires Windows 10 build 20348 or later. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1156 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：讨论的问题、没讨论透的地方、意见与要求、下一步 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: questions discussed, what stayed unresolved, comments and requests, next steps | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:62 |
| 1157 | 忘了结束的会议会在长时间没有识别到文字后自动收尾，避免把下一场会议的内容写进这场纪要。只统计识别到的文字，背景音乐不会让它一直开着。 | A meeting left running ends after a long stretch without recognized text, so the next meeting does not leak into these notes. Only recognized text counts; background music does not keep it open. | — |  |
| 1158 | 每个模板以 ## 模板名 开头，每个章节以 ### 章节标题 开头，下一行写要求。保存后会出现在纪要页的模板下拉框；原有内置模板不变。 | Each template starts with ## name; each section starts with ### title and the next line states the requirement. Saved templates appear in the Notes template picker; built-ins are unchanged. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1159 | Qwen3-Embedding-0.6B Q8 · CPU · 首次下载约 596 MB。索引仅在本机生成；模型缺失或失败时回退关键词。 | Qwen3-Embedding-0.6B Q8 · CPU · about 596 MB on first download. The index is local; missing or failed models fall back to keywords. | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 1160 | 每个技能以 ## 名称 开头，可写一行「摘要：…」，其余为 Markdown。启用后会加入回答提示；与真实性/安全约束冲突时以安全约束为准。 | Each skill starts with ## name, may include one Summary: line and is otherwise Markdown. Enabled skills join the answer prompt; truth and safety rules win on conflict. | — |  |
| 1161 | 读 4 句话，约 40 秒。只保存一串数字，不保存录音；只存在这台电脑上，不上传、不进备份，可随时删除。录入后，线下会议里你的发言会标为“我”。 | Read 4 sentences, about 40 seconds. Only a string of numbers is saved, never audio; it stays on this PC, is never uploaded or backed up, and can be deleted anytime. Afterwards your speech in in-person meetings is marked as “Me”. | — | Windows/src/NextThought.Desktop/Views/MyVoiceWindow.axaml |
| 1162 | Ctrl+L 聚焦输入 · Enter 发送 · Ctrl+Shift+Enter 强制回答 · Esc 取消当前回答 · ↑/PageUp 保持阅读位置 | Ctrl+L focuses input · Enter sends · Ctrl+Shift+Enter forces an answer · Esc cancels · ↑/PageUp keep your reading position | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1163 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。已支持按讲话标题和截图 OCR 跟页；持续屏幕观察尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Following slides by spoken title and screenshot OCR is supported; continuous screen watching is not ported yet. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1164 | 支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT 与图片；回答时只把与问题相关的片段交给模型，并标注来源。 | Supports PDF (includes scanned OCR), Word, PowerPoint, Excel, Markdown, TXT and images. Only excerpts relevant to the question are sent to the model while answering, with sources marked. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1165 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。幻灯片自动翻页识别（LiveInsights 页面匹配）尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Automatic slide-change detection (LiveInsights page matching) is not ported yet. | — |  |
| 1166 | 点按键位后直接按下组合键录入，Esc 取消；「清除」「默认」恢复该动作的 Mac 默认键位。必须包含 Ctrl/Alt/Shift/Win 之一，两个动作不能相同。 | Choose the key field and press the combination; Esc cancels. Clear and Default restore that action's Mac default key. It must include Ctrl/Alt/Shift/Win and no two actions may match. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1167 | 检查 GitHub 上的发布页：正式版本只看 vX.Y.Z，main 最新构建跟随 main 分支。发布带安装包与校验文件时可下载并校验后应用内安装，否则只打开下载页。 | Checks the GitHub release page: stable builds use vX.Y.Z, latest follows main. When a release has an installer and checksum, it downloads, verifies and installs; otherwise it opens the download page. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1168 | 将永久删除这场会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖它的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes this meeting's transcript, audio, notes, index, questions, answers and notes. Accepted lessons, decisions and actions that only depend on it are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meeting's wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:129 |
| 1169 | 将永久删除所有归档会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖这些会议的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes the transcript, audio, notes, index, questions, answers and notes of every archived meeting. Accepted lessons, decisions and actions that only depend on them are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meetings' wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:156 |
| 1170 | 资料文件 (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | Material files (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:100 |
| 1171 | 每行「动作=按键」，保存后立即重新注册。可用 Ctrl/Alt/Shift/Win 组合，按键支持 A–Z、0–9、F1–F24、Left/Right/Up/Down/PageUp/PageDown/Space/Backslash。 | One action=key per line; saving re-registers immediately. Modifiers may include Ctrl/Alt/Shift/Win, and keys may be A–Z, 0–9, F1–F24, Left/Right/Up/Down/PageUp/PageDown/Space/Backslash. | — |  |


| 1172 | 上一页 | Previous | — | LiveView.axaml（面板翻页提示） |
| 1173 | 下一句： | Next: | — | InsightsPanel.axaml |
| 1174 | 依据： | Based on: | — | LiveView.axaml |
| 1175 | 听众可能会问（点一下提前准备回答） | Questions the audience may ask (tap to prepare an answer) | — | InsightsPanel.axaml |
| 1176 | 展开识别的文字 | Show recognised text | — | InsightsPanel.axaml |
| 1177 | 已讲 | Spoken | — | InsightsPanel.axaml |
| 1178 | 已识别 · 是否用于回答见“本次依据” | Recognised · see “Based on” for whether it was used | — | InsightsPanel.axaml |
| 1179 | 打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕在本机识别；相关文字与必要的当前画面会提供给回答引擎。 | With screen awareness on, the text on screen is recognised on this device every few seconds: it follows the slide you are on and offers talking points for that page. When a question or code appears on screen, it decides whether it relates to the conversation and includes it in answers. The screen is read on this device; the relevant text and, when needed, the current frame are provided to the answer engine. | — | InsightsPanel.axaml |
| 1180 | 打开屏幕感知 | Turn on screen awareness | — | InsightsPanel.axaml |
| 1181 | 收起 | Collapse | — | InsightsPanel.axaml |
| 1182 | 正在根据这一页和你的资料写讲稿提示… | Writing talking points for this page from the slide and your materials… | — | InsightsPanel.axaml |
| 1183 | 结束并整理纪要 | End and write notes | — | LiveView.axaml |
| 1184 | 屏幕：%@ | Screen: %@ | — | AppState.Insights.cs |
| 1185 | 题目 | Question | — | AppState.Insights.cs |
| 1186 | 代码 | Code | — | AppState.Insights.cs |
| 1187 | 文档 | Document | — | AppState.Insights.cs |
| 1188 | 聊天 | Chat | — | AppState.Insights.cs |
| 1189 | 视频通话 | Video call | — | AppState.Insights.cs |
| 1190 | %lld 秒前 | %lld s ago | — | AppState.Insights.cs |
| 1191 | 已讲 %lld/%lld | Spoken %lld/%lld | — | AppState.Insights.cs |
| 1192 | 第 %lld / %lld 页 | Page %lld / %lld | — | AppState.Insights.cs |
| 1193 | 实时纪要没有开 | Live notes are off | — | AppState.LivePage.cs |
| 1194 | 还没有连接回答服务，写不了实时纪要 | No answer service is connected, so live notes cannot be written | — | AppState.LivePage.cs |
| 1195 | 有了内容就开始整理 | It will start once there is something to summarise | — | AppState.LivePage.cs |
| 1196 | 说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。 | After a few sentences this will show the meeting so far: what is being discussed, what was decided, who is doing what. | — | AppState.LivePage.cs |
| 1197 | 这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。 | Live notes are off for this meeting. Full notes are still written at the end; to see key points as you go, turn it on in the top right. | — | AppState.LivePage.cs |
| 1198 | 已开启实时纪要：大约每 45 秒整理一次。 | Live notes are on: they update about every 45 seconds. | — | AppState.LivePage.cs |
| 1199 | 正在更新实时纪要… | Updating live notes… | — | AppState.LivePage.cs |
| 1200 | 已把这一句算作我说的。 | This line now counts as mine. | — | AppState.LivePage.cs |
| 1201 | 新说话人 | New speaker | — | AppState.Speakers.cs |
| 1202 | 先开始记录，再改这一句的说话人。 | Start recording before changing who said this line. | — | AppState.LivePage.cs |
| 1203 | 这一句改成了%@。 | This line now belongs to %@. | — | AppState.LivePage.cs |
| 1204 | 说话人修改失败： | Could not change the speaker:  | — | AppState.LivePage.cs |
## 2.1 J3/J4 新增文案（首页统计、系列命名与开下一场）

| 中文 | 英文 | Mac 原文 | 位置 |
|---|---|---|---|
| 一句话印象 | One-line impression | — | Windows 会后纪要（task-62 E15） |
| 用了多少 | How much you've recorded | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 本月 | This month | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 累计 | All time | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 场会议 | meetings | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 记录时长 | recorded time | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 听到的提问 | questions heard | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 给出的回答 | answers given | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 时长按每场从开始记录到结束计算；暂停的时间也算在内。 | Recording time runs from the start to the end of each meeting; pauses count too. | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| %lld 小时 %lld 分 | %lld h %lld min | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 不到 1 分钟 | Under a minute | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 0 分钟 | 0 min | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 共 %lld 场 · %@ · 提问 %lld 次 · 回答 %lld 次 | %lld meetings · %@ · %lld questions · %lld answers | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| %lld 场 · %@ · %lld 份资料 | %lld meetings · %@ · %lld files | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 还没开过 · %lld 份资料 | Not started yet · %lld files | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 继续一个系列 | Continue a series | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 还没有记录。从这个系列开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 系列按修改时间合并；应用前会先创建本机恢复点。模板和个人笔记属于数据，始终参与恢复。 | Series are merged by modification time. A local restore point is created before anything is applied. Templates and personal notes are data and are always restored. | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 技能可用于所有系列，或在系列中按系列选择。 | Skills can apply to all series, or be chosen per series. | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在系列的“技能”页按系列勾选。 | A skill is a Markdown instruction describing the desired speaking style, answer method or subject knowledge. Skills opened to all series are used for every answer; other skills are chosen per series on the series' Skills tab. | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |
| 开下一场 | Start next meeting | — | Windows 首页统计 / 系列改名（task-57 J3/J4） |

## 3. 英文模式逐页扫描

扫描对象：`--demo --screenshots=<dir>` 生成的英文模式页面文字清单（`text/*-en*.txt`，
包含未展开的 tooltip；文字清单由截图工具在渲染后遍历控件树得到，不依赖 OCR）。

- 页面数：**50**
- 含中文字符的行：**192**
- 其中用户数据/演示数据/系统设备名（允许）：**192**
- **残留（需要修）：0**

英文模式下除用户数据、演示数据与 Windows 设备名外，没有中文字符。

复算命令：
```
dotnet run --project src/NextThought.Desktop -c Release --no-build -- --demo \
  --screenshots=<临时目录>
python Windows/scripts/localization_scan.py --screens=<临时目录> --write
```

## 4. 已知未覆盖点

- `CorrectedTranscriptView`（E17）、`BackupSettingsSection` / `BackupRestoreReviewView`（E30）
  是其它任务新增的视图，落地后再迁 `{loc:Tr}` 并补英文，本表在其提交后重跑生成。
- `AppState.Proofread.cs`、`AppState.Update.cs`、`AppState.Archive.cs` 的文案由各自任务负责。
- 直播回答的系统提示词（`AppState.Live.cs`）仍为 Windows 自建中文 prompt，未切到 Mac 的
  `AnswerPromptBuilder.MakeParts`；提示词不渲染到界面，故不在上面的扫描范围。

