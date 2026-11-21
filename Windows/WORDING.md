# Windows 文案对照（G10）

这份表记录 Windows 版与 Mac 版**文案不同**的每一句。英文与中文都写；相同文案的句子
直接走 Mac 的 `Localizable.xcstrings`（由 `Windows/scripts/generate_ui_text.py` 生成
`UiTextMac.g.cs`，2,638 条），不在这张表里重复。Mac 是唯一标准：只有涉及系统名词的
句子可以改（钥匙串→凭据管理器、访达→文件资源管理器等），其余差异都是 Windows 移植过程
中的说明性文案（Windows 独有功能或 Windows 能力差异），在此逐句列出。

- 生成时间：2026-11-21
- 生成方式：`python Windows/scripts/localization_scan.py --write`
- Windows 独有/改写文案：**1289** 句（下表逐条）
- Mac 共享文案：2,638 条，由生成的字符串表提供，文案逐字相同

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
| 1 | 、 | ,  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:868 |
| 2 | 宽 | Wide | 宽 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:521 |
| 3 | 我 | Me | 我 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 4 | 段 | segment | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 5 | 窄 | Narrow | 窄 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:519 |
| 6 | 译 | Translate | — |  |
| 7 | Q： | Q:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:206 |
| 8 | 上移 | Move up | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 9 | 下移 | Move down | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 10 | 中文 | Chinese | 中文 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 11 | 代码 | Code | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:155 |
| 12 | 会议 | Meetings | 会议 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 13 | 依据 | Basis | — |  |
| 14 | 保存 | Save | 保存 | Windows/src/NextThought.Desktop/Views/EditTextDialog.axaml |
| 15 | 全部 | All | — | Windows/src/NextThought.Desktop/AppState.cs:300 |
| 16 | 关于 | About | — |  |
| 17 | 关闭 | Close | 关闭 | Windows/src/NextThought.Desktop/Views/CorrectedTranscriptView.axaml |
| 18 | 分钟 | minutes | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 19 | 删除 | Delete | 删除 | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml |
| 20 | 刷新 | Refresh | 刷新 | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml |
| 21 | 区域 | Region | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:576 |
| 22 | 历史 | History | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 23 | 原文 | Original | — | Windows/src/NextThought.Desktop/AppState.cs:219 |
| 24 | 双语 | Bilingual | — | Windows/src/NextThought.Desktop/AppState.cs:219 |
| 25 | 取消 | Cancel | 取消 | Windows/src/NextThought.Desktop/Views/AddToGlossarySheet.axaml |
| 26 | 名字 | Name | — |  |
| 27 | 听课 | Class | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:40 |
| 28 | 回答 | Answer | 回答 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 29 | 回顾 | Review | — |  |
| 30 | 场地 | Room | — |  |
| 31 | 复制 | Copy | — |  |
| 32 | 复核 | Review | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 33 | 外观 | Appearance | 外观 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 34 | 客户 | Client | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:37 |
| 35 | 导入 | Import | — |  |
| 36 | 导出 | Export | — |  |
| 37 | 已讲 | Spoken | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 38 | 应用 | Apply | — |  |
| 39 | 待办 | Todo | 待办 | Windows/src/NextThought.Desktop/OnboardingSample.cs:44 |
| 40 | 待机 | Idle | — | Windows/src/NextThought.Desktop/AppState.Live.cs:861 |
| 41 | 忽略 | Ignore | 忽略 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 42 | 悬停 | Hover | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 43 | 截图 | Screenshot | 截图 | Windows/src/NextThought.Desktop/Views/Assistant/ScreenshotCard.axaml |
| 44 | 打开 | Open | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml.cs:153 |
| 45 | 技能 | Skills | 技能 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 46 | 按下 | Pressed | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 47 | 搜索 | Search | — |  |
| 48 | 摘要 | Summary | — |  |
| 49 | 收起 | Collapse | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 50 | 改短 | Shorten | — |  |
| 51 | 数据 | Data | 数据 |  |
| 52 | 文件 | Files | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml.cs:163 |
| 53 | 文本 | Text | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:287 |
| 54 | 文档 | Document | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:157 |
| 55 | 新建 | New | — |  |
| 56 | 更新 | Update | 更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 57 | 本月 | This month | 本月 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 58 | 查看 | Open | 查看 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 59 | 查证 | Verify | — |  |
| 60 | 标准 | Regular | 标准 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:520 |
| 61 | 检查 | Checks | — |  |
| 62 | 模式 | Mode | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:197 |
| 63 | 洞察 | Insights | — | Windows/src/NextThought.Desktop/Views/InsightsPanelWindow.axaml |
| 64 | 浅色 | Light | 浅色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:510 |
| 65 | 测试 | Test | — | Windows/src/NextThought.Desktop/AppState.Models.cs:124 |
| 66 | 深色 | Dark | 深色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:511 |
| 67 | 添加 | Add | 添加 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 68 | 焦点 | Focus | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 69 | 确认 | Confirm | — | Windows/src/NextThought.Desktop/Views/ConfirmDialog.axaml |
| 70 | 示例 | Sample | 示例 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 71 | 移出 | Remove | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 72 | 穿透 | Click-through | — |  |
| 73 | 类型 | Type | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 74 | 系列 | Series | 系列 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 75 | 累计 | All time | 累计 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 76 | 纪要 | Notes | 纪要 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 77 | 经验 | Lesson | 经验 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 78 | 编辑 | Edit | 编辑 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 79 | 聊天 | Chat | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:158 |
| 80 | 自动 | Automatic | 自动 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 81 | 英文 | English | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:480 |
| 82 | 补译 | Translate missing | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 83 | 讨论 | Discussion | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:41 |
| 84 | 议程 | Agenda | 议程 | Windows/src/NextThought.Desktop/MeetingDraft.cs:76 |
| 85 | 记下 | Save | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 86 | 记录 | Note | 记录 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 87 | 设置 | Settings | 设置 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 88 | 证据 | Evidence | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 89 | 误听 | Misheard | — |  |
| 90 | 课件 | Course slides | 课件 | Windows/src/NextThought.Desktop/MeetingDraft.cs:78 |
| 91 | 返回 | Back | — |  |
| 92 | 通用 | General | 通用 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 93 | 采纳 | Accept | — |  |
| 94 | 采音 | Audio | — |  |
| 95 | 重播 | Replay | 重播 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 96 | 重试 | Retry | 重试 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 97 | 问题 | Question | 问题 | Windows/src/NextThought.Desktop/Views/Assistant/ConversationBubble.axaml |
| 98 | 问： | Q:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:34 |
| 99 | 音频 | Audio | — |  |
| 100 | 题库 | Question bank | 题库 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 101 | 题目 | Question | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:154 |
| 102 | 首页 | Home | 首页 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 103 | 高亮 | Highlight | — |  |
| 104 | 默认 | Default | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 105 | ⏎A： | 
A:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:206 |
| 106 |  路  |  ·  | — |  |
| 107 | 上一页 | Previous | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 108 | 上下文 | Context | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 109 | 下一条 | Next | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 110 | 不可用 | Unavailable | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 111 | 中译： | Chinese:  | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:19 |
| 112 | 会议  | Meeting  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:99 |
| 113 | 依据： | Based on: | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 114 | 去准备 | Set up | 去准备 |  |
| 115 | 场会议 | meetings | 场会议 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 116 | 工作台 | Workbench | — |  |
| 117 | 已删除 | Deleted | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:202 |
| 118 | 已就绪 | Ready | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:73 |
| 119 | 已收起 | Collapsed | — |  |
| 120 | 已连接 | Connected | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:204 |
| 121 | 幻灯片 | Slides | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:156 |
| 122 | 应用。 | Apply. | — |  |
| 123 | 待确认 | To confirm | 待确认 | Windows/src/NextThought.Desktop/AppState.Series.cs:296 |
| 124 | 忽略。 | Ignore. | — |  |
| 125 | 截止  | Due  | — | Windows/src/NextThought.Desktop/Views/ViewpointMatrixView.cs:181 |
| 126 | 提示： | Tip:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:96 |
| 127 | 提词器 | Teleprompter | 提词器 |  |
| 128 | 提词岛 | Cue island | 提词岛 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:532 |
| 129 | 文件夹 | Folder | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:206 |
| 130 | 新技能 | New skill | — |  |
| 131 | 新模板 | New template | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:142 |
| 132 | 新章节 | New section | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:150 |
| 133 | 显示器 | Displays | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:576 |
| 134 | 最大化 | Maximize | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 135 | 最小化 | Minimize | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 136 | 有纪要 | Has notes | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 137 | 未准备 | Not ready | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:23 |
| 138 | 未填写 | Not set | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:67 |
| 139 | 未整理 | Not organized | — | Windows/src/NextThought.Desktop/AppState.cs:328 |
| 140 | 未联网 | Offline | — | Windows/src/NextThought.Desktop/AppState.cs:314 |
| 141 | 未选择 | Not selected | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:398 |
| 142 | 术语表 | Glossary | — |  |
| 143 | 条高亮 | highlights | — |  |
| 144 | 模板名 | Template name | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 145 | 知道了 | Got it | 知道了 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 146 | 自定义 | Custom | 自定义 | Windows/src/NextThought.Desktop/AppState.Home.cs:247 |
| 147 | 英译： | English:  | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:19 |
| 148 | 要做的 | To do | 要做的 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 149 | 说话人 | Speakers | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 150 | 资料库 | Library | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 151 | 资料： | Materials: | — |  |
| 152 | 面试官 | Interviewer | 面试官 | Windows/src/NextThought.Desktop/OnboardingSample.cs:41 |
| 153 | 鱼骨图 | Fishbone | 鱼骨图 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 154 | 麦克风 | Microphone | 麦克风 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 155 |  · 我 |  · Me | — |  |
| 156 | 0 分钟 | 0 min | — |  |
| 157 | ★ 高亮 | ★ Highlight | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 158 | 下一句： | Next: | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 159 | 不再提示 | Don't ask again | 不再提示 | Windows/src/NextThought.Desktop/Views/Home/OnboardingResumeRow.axaml |
| 160 | 会中笔记 | Meeting notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 161 | 会后纪要 | Meeting notes | — |  |
| 162 | 会议信息 | Meeting info | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 163 | 会议标题 | Meeting title | — | Windows/src/NextThought.Desktop/Views/RenameSessionDialog.axaml |
| 164 | 保存备注 | Save note | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 165 | 保存密钥 | Save key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 166 | 保存技能 | Save skills | — |  |
| 167 | 保存模板 | Save template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 168 | 保存笔记 | Save notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 169 | 保存结构 | Save structure | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 170 | 保存设置 | Save settings | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 171 | 停止记录 | Stop recording | — |  |
| 172 | 先说结论 | Lead with the conclusion | — |  |
| 173 | 先选场合 | Pick a scene | — |  |
| 174 | 全文命中 | Full-text hits | — |  |
| 175 | 全文搜索 | Full-text search | — |  |
| 176 | 全部会议 | All meetings | — |  |
| 177 | 全部文件 | All files | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml.cs:25 |
| 178 | 全部术语 | All terms | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 179 | 全部设置 | All settings | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 180 | 删除会议 | Delete meeting | — |  |
| 181 | 删除选中 | Delete selected | — |  |
| 182 | 刷新设备 | Refresh devices | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 183 | 区域截图 | Region capture | 区域截图 | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 184 | 历史回答 | Past answers | — |  |
| 185 | 参考回答 | Reference answer | — | Windows/src/NextThought.Desktop/Views/DetachedAnswerWindow.axaml |
| 186 | 双屏演讲 | Presenter mode | 双屏演讲 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 187 | 只看回答 | Answers only | — |  |
| 188 | 回答列表 | Answers | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 189 | 回答引擎 | Answer engine | 回答引擎 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:18 |
| 190 | 回答服务 | Answer service | 回答服务 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 191 | 回答模型 | Answer model | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:201 |
| 192 | 回答记录 | Answer history | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 193 | 回答风格 | Answer style | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 194 | 声音来源 | Audio sources | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 195 | 声音测试 | Sound test | — | Windows/src/NextThought.Desktop/Views/OnboardingVoiceWindow.axaml |
| 196 | 复制修复 | Copy fix | — |  |
| 197 | 复核备注 | Review note | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:27 |
| 198 | 字体样张 | Type specimen | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 199 | 学术汇报 | Academic talk | — | Windows/src/NextThought.Desktop/AppState.Home.cs:70 |
| 200 | 定下来的 | Decided | 定下来的 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 201 | 实时转写 | Live transcript | 实时转写 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 202 | 导入备份 | Import backup | — |  |
| 203 | 导入资料 | Import materials | 导入资料 | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 204 | 导出备份 | Export backup | — | Windows/src/NextThought.Desktop/BackupFileRow.cs:26 |
| 205 | 导出术语 | Export terms | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:307 |
| 206 | 小组项目 | Group project | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:43 |
| 207 | 工作布置 | Work assignment | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:119 |
| 208 | 工作纪要 | Meeting notes | — |  |
| 209 | 已取消。 | Cancelled. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:359 |
| 210 | 已改为  | Changed to  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 211 | 已有纪要 | Notes exist | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:639 |
| 212 | 带入经验 | Bring lessons | — |  |
| 213 | 带入资料 | Bring materials | — |  |
| 214 | 带入题库 | Bring question bank | — |  |
| 215 | 开下一场 | Start next meeting | 开下一场 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 216 | 开始录制 | Start recording | — |  |
| 217 | 开始记录 | Start recording | 开始记录 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 218 | 引文支持 | Quote support | — | Windows/src/NextThought.Desktop/SessionEvidenceRow.cs:13 |
| 219 | 引用来源 | Cite source | — |  |
| 220 | 归档截图 | Archive screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 221 | 归档概览 | Archive overview | — |  |
| 222 | 归档系列 | Archive series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 223 | 当前场景 | Current scene | 当前场景 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:22 |
| 224 | 录音来源 | Audio source | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:199 |
| 225 | 快速设置 | Quick settings | — |  |
| 226 | 恢复点击 | Restore clicks | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 227 | 截图提问 | Ask about a screenshot | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 228 | 手机伴侣 | Phone companion | 手机伴侣 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 229 | 打开系列 | Open series | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 230 | 打开纪要 | Open notes | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 231 | 打开设置 | Open settings | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 232 | 接口预设 | API preset | — |  |
| 233 | 控件样张 | Control samples | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 234 | 推理强度 | Reasoning effort | 推理强度 | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 235 | 提词宽度 | Cue width | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 236 | 搜索设置 | Search settings | 搜索设置 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 237 | 撤销移除 | Undo removal | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 238 | 收起示例 | Hide sample | 收起示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:67 |
| 239 | 放大字号 | Larger text | — |  |
| 240 | 教练小结 | Coach summary | — |  |
| 241 | 数据目录 | Data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 242 | 文字纪要 | Text notes | 文字纪要 |  |
| 243 | 新增条目 | New entry | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:81 |
| 244 | 新建会议 | New meeting | — |  |
| 245 | 新建模板 | New template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 246 | 新建系列 | New series | 新建系列 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 247 | 新说话人 | New speaker | 新说话人 | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:200 |
| 248 | 显示译文 | Show translation | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 249 | 替换回答 | Replace answer | — |  |
| 250 | 最近会议 | Recent meetings | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 251 | 未在采集 | Not capturing | — |  |
| 252 | 未知应用 | Unknown app | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:114 |
| 253 | 查看截图 | View screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 254 | 查看来源 | View sources | — |  |
| 255 | 标准纪要 | Standard notes | — |  |
| 256 | 校对转写 | Proofread transcript | 校对转写 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 257 | 检查更新 | Check for updates | 检查更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 258 | 正在检查 | Checking | — | Windows/src/NextThought.Desktop/AppState.Update.cs:82 |
| 259 | 正在记录 | Recording | — | Windows/src/NextThought.Desktop/AppState.Live.cs:861 |
| 260 | 添加章节 | Add section | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 261 | 清除查询 | Clear query | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 262 | 独立窗口 | Detached window | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 263 | 用了多少 | How much you've recorded | 用了多少 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 264 | 章节标题 | Section title | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 265 | 简体中文 | Simplified Chinese | 简体中文 | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 266 | 简洁回答 | Concise answer | — |  |
| 267 | 系列文档 | Series documents | — |  |
| 268 | 系列术语 | Series glossary | — |  |
| 269 | 系列管理 | Manage series | — |  |
| 270 | 系列资料 | Series materials | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 271 | 系统识别 | System recognition | 系统识别 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:120 |
| 272 | 纪要编辑 | Edit notes | — |  |
| 273 | 组会讨论 | Group meeting | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:115 |
| 274 | 结束会议 | End meeting | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:54 |
| 275 | 继续记录 | Resume recording | 继续记录 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 276 | 编辑系列 | Edit series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 277 | 编辑经验 | Edit lesson | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:105 |
| 278 | 编辑题目 | Edit question | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:153 |
| 279 | 缩小字号 | Smaller text | — |  |
| 280 | 联网来源 | Web sources | — |  |
| 281 | 自动回答 | Automatic answers | 自动回答 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 282 | 自检回答 | Self-test answer | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:579 |
| 283 | 自检问题 | Self-test question | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:579 |
| 284 | 要点大纲 | Outline | 要点大纲 |  |
| 285 | 观点对照 | Viewpoints | 观点对照 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 286 | 视频通话 | Video call | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:159 |
| 287 | 解析结果 | Parse result | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 288 | 讨论题目 | Discussion topics | 讨论题目 | Windows/src/NextThought.Desktop/MeetingDraft.cs:79 |
| 289 | 记录时长 | recorded time | 记录时长 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 290 | 讲稿要点 | Script key points | — |  |
| 291 | 设置清单 | Setup checklist | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 292 | 识别模型 | Recognition model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 293 | 详细信息 | Details | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 294 | 语音识别 | Speech recognition | 语音识别 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 295 | 误听标记 | Misheard marker | — | Windows/src/NextThought.Desktop/GlossaryEntryRow.cs:11 |
| 296 | 资料复核 | Material review | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 297 | 资料引用 | Material citations | — |  |
| 298 | 跟随系统 | Follow system | 跟随系统 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:394 |
| 299 | 跳到原话 | Jump to the original | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 300 | 通义千问 | Qwen (Tongyi) | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:376 |
| 301 | 重新检查 | Check again | 重新检查 | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:174 |
| 302 | 附着技能 | Attach skills | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 303 | 面试复盘 | Interview review | — |  |
| 304 | 面试记录 | Interview notes | 面试记录 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 305 | 项目经历 | Project experience | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:292 |
| 306 | 领域字段 | Domain fields | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 307 | 首次引导 | First-run guide | — |  |
| 308 | （主屏） |  (main display) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:61 |
| 309 | 一句话印象 | One-line impression | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 310 | 上一场转写 | Last meeting's transcript | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:101 |
| 311 | 下一场会议 | Next meeting | — |  |
| 312 | 下载并安装 | Download and install | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 313 | 不自动结束 | Do not auto-end | 不自动结束 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:661 |
| 314 | 什么都可以 | Anything goes | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:18 |
| 315 | 使用此区域 | Use this area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 316 | 保存到设置 | Save to settings | — |  |
| 317 | 保存快捷键 | Save shortcuts | — |  |
| 318 | 保存术语表 | Save glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 319 | 全局快捷键 | Global shortcuts | 全局快捷键 |  |
| 320 | 关闭提词器 | Close teleprompter | — |  |
| 321 | 分工与对齐 | Assign and align | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:13 |
| 322 | 参考资料库 | Reference materials | — |  |
| 323 | 双屏演讲： | Presenter mode:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:673 |
| 324 | 只记录模式 | Record-only mode | — |  |
| 325 | 听到的提问 | questions heard | 听到的提问 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 326 | 回滚失败： | Rollback failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:183 |
| 327 | 安装失败： | Install failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:315 |
| 328 | 导入失败： | Import failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:49 |
| 329 | 屏幕与截图 | Screen and screenshots | — |  |
| 330 | 屏幕：%@ | Screen: %@ | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:150 |
| 331 | 已保存密钥 | Key saved | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 332 | 已加密保存 | Saved encrypted | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:461 |
| 333 | 已确认经验 | Confirmed lessons | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 334 | 已选择原话 | Quote selected | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 335 | 录入失败： | Enrollment failed:  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:159 |
| 336 | 恢复失败： | Restore failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:60 |
| 337 | 截图失败： | Screenshot failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:371 |
| 338 | 截图已归档 | Screenshot archived | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 339 | 打开下载页 | Open download page | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 340 | 打开主窗口 | Open main window | 打开主窗口 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 341 | 打开术语表 | Open glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 342 | 打开资料库 | Open library | — |  |
| 343 | 按模板整理 | Organize with template | 按模板整理 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 344 | 接着上一场 | Continue the last one | 接着上一场 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 345 | 提示岛宽度 | Cue island width | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 346 | 提词岛示意 | Cue island preview | — |  |
| 347 | 改这一句… | Edit this sentence… | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 348 | 数据与关于 | Data and about | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 349 | 未命名会议 | Untitled meeting | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:116 |
| 350 | 未命名窗口 | Untitled window | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:88 |
| 351 | 未归入系列 | Not in a series | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:198 |
| 352 | 未指定模型 | No model specified | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:389 |
| 353 | 模型与接口 | Model and API | — |  |
| 354 | 正在准备… | Preparing… | — | Windows/src/NextThought.Desktop/AppState.cs:267 |
| 355 | 正在更新… | Updating… | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:87 |
| 356 | 正在获取… | Fetching… | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:307 |
| 357 | 添加到系列 | Add to series | — |  |
| 358 | 类型与标题 | Type and title | — |  |
| 359 | 系列与资料 | Series and materials | — |  |
| 360 | 系列变更已 | Series change  | — |  |
| 361 | 系统 ·  | System ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:388 |
| 362 | 纪要已整理 | Notes ready | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:277 |
| 363 | 纪要整理中 | Notes being organized | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:278 |
| 364 | 结论与待办 | Conclusions and actions | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:15 |
| 365 | 给出的回答 | answers given | 给出的回答 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 366 | 继续记录。 | Recording resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:139 |
| 367 | 缓存一致性 | Cache consistency | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:293 |
| 368 | 角色：%@ | Role: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:48 |
| 369 | 讲给别人听 | Present to others | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:12 |
| 370 | 译文待更新 | Translation out of date | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 371 | 资料与检索 | Materials and retrieval | — |  |
| 372 | 还没有纪要 | No notes yet | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:280 |
| 373 | 选择麦克风 | Select microphone | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 374 | 重命名系列 | Rename series | — |  |
| 375 | 问题置信度 | Question confidence | — | Windows/src/NextThought.Desktop/Views/Assistant/DetectionStrip.axaml.cs:47 |
| 376 | ，签名者  | , signer  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 377 | ⏎⏎资料：⏎ | 

Materials:
 | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:90 |
| 378 |  · 名字: |  · Name: | — |  |
| 379 | %@（%@） | %@ (%@) | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:29 |
| 380 | %lld 个 | %lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:204 |
| 381 | %lld 处 | %lld places | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:64 |
| 382 | %lld 段 | %lld blocks | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:225 |
| 383 | %lld 页 | %lld pages | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:84 |
| 384 | 上一场 ·  | Previous ·  | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:241 |
| 385 | 会议已导出。 | Meeting exported. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:159 |
| 386 | 会议自动收尾 | Auto-end meetings | 会议自动收尾 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 387 | 会议记录操作 | Meeting record actions | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 388 | 保存个人笔记 | Save personal notes | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 389 | 共用文档组件 | Shared document components | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:82 |
| 390 | 准备检索组件 | Prepare retrieval components | — |  |
| 391 | 准备语音组件 | Prepare speech components | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 392 | 删除筛选结果 | Delete filtered results | — |  |
| 393 | 刷新应用列表 | Refresh apps | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 394 | 刷新网络地址 | Refresh addresses | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 395 | 双击恢复控件 | Double-click to restore controls | — |  |
| 396 | 取消当前任务 | Cancel task | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 397 | 可选，未配置 | Optional, not configured | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 398 | 合并到上一个 | Merge into previous | — |  |
| 399 | 合并回主窗口 | Merge back into the main window | — | Windows/src/NextThought.Desktop/Views/DetachedModuleWindow.axaml |
| 400 | 回答已取消。 | Answer cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:646 |
| 401 | 回答已复制。 | Answer copied. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:782 |
| 402 | 回答模型名称 | Answer model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 403 | 复制配对链接 | Copy pairing link | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 404 | 字幕显示译文 | Show translation in subtitles | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 405 | 安装包签名： | Installer signature:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 406 | 安装示例技能 | Install sample skill | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 407 | 导入 WAV | Import WAV | — |  |
| 408 | 导入纪要模板 | Import note template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:201 |
| 409 | 导出 PDF | Export PDF | — |  |
| 410 | 导出 SRT | Export SRT | — |  |
| 411 | 导出 VTT | Export VTT | — |  |
| 412 | 已保存 ·  | Saved ·  | — |  |
| 413 | 已删除会议： | Meeting deleted:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:83 |
| 414 | 已取消截图。 | Screenshot cancelled. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:220 |
| 415 | 已取消生成。 | Generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:104 |
| 416 | 已取消高亮。 | Highlight removed. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:143 |
| 417 | 已录入 ·  | Enrolled ·  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:95 |
| 418 | 已恢复 %@ | Restored %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:186 |
| 419 | 已重新排队。 | Re-queued. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:343 |
| 420 | 幻灯片所在屏 | Slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 421 | 截取当前窗口 | Capture current window | — |  |
| 422 | 打开屏幕感知 | Turn on screen awareness | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 423 | 打开数据目录 | Open data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 424 | 打开文件位置 | Open file location | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 425 | 技能已保存。 | Skill saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:98 |
| 426 | 技能已删除。 | Skill deleted. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:118 |
| 427 | 技能已清空。 | Skills cleared. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 428 | 把讨论留下来 | Keep the discussion | — |  |
| 429 | 把这场景加入 | Add this scene | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 430 | 按下组合键… | Press a key combination… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:433 |
| 431 | 提示岛所在屏 | Cue island display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 432 | 提词器已穿透 | Teleprompter click-through on | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 433 | 提高不透明度 | More opaque | — |  |
| 434 | 收起面试示例 | Hide sample interview | 收起面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:68 |
| 435 | 整理已取消。 | Note generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:255 |
| 436 | 整理这场会议 | Summarize this meeting | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 437 | 新系列的名字 | New series name | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 438 | 智谱 GLM | Zhipu GLM | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:376 |
| 439 | 更改尚未保存 | Changes are not saved yet | 更改尚未保存 | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:27 |
| 440 | 未使用资料库 | No materials used | — | Windows/src/NextThought.Desktop/AppState.Live.cs:557 |
| 441 | 本地（默认） | Local (default) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:468 |
| 442 | 本机语音识别 | On-device speech | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 443 | 术语已保存。 | Term saved. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:236 |
| 444 | 来回交换观点 | Exchange viewpoints | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:17 |
| 445 | 校对已取消。 | Proofreading cancelled. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:87 |
| 446 | 检查回答服务 | Test answer service | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:173 |
| 447 | 正在生成回答 | Generating answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:861 |
| 448 | 点击查看原图 | Click to view the full image | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 449 | 生成这种纪要 | Generate this note | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 450 | 筛选资料名称 | Filter material names | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 451 | 系列已归档。 | Series archived. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:444 |
| 452 | 纪要整理失败 | Notes failed | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:279 |
| 453 | 经验已删除。 | Lesson deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:402 |
| 454 | 经验已更新。 | Lesson updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:377 |
| 455 | 经验已记下。 | Lesson saved. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:531 |
| 456 | 继续一个系列 | Continue a series | 继续一个系列 | Windows/src/NextThought.Desktop/Views/Home/ContinueSeries.axaml |
| 457 | 继续完成设置 | Finish setting up | 继续完成设置 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:41 |
| 458 | 聆听已恢复。 | Listening resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:227 |
| 459 | 说话人与翻译 | Speakers and translation | — |  |
| 460 | 还没有术语。 | No glossary terms yet. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:94 |
| 461 | 还没有经验。 | No lessons yet. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 462 | 选择输出设备 | Select output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 463 | 重命名失败： | Rename failed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:19 |
| 464 | 降低不透明度 | Less opaque | — |  |
| 465 | 隐藏所有窗口 | Hide all windows | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:18 |
| 466 | 题库已更新。 | Question bank updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:417 |
| 467 | 题库还空着。 | The question bank is empty. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 468 | 题目已删除。 | Question deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:431 |
| 469 | 麦克风 ·  | Microphone ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:391 |
| 470 | 默认，不指定 | Default, not specified | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:52 |
| 471 | ，并恢复设置 | , and settings restored | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:231 |
| 472 | ，立即生效。 | , takes effect immediately. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 473 |  · 可能回声 |  · possible echo | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:109 |
| 474 |  · 提词按句 |  · cue by sentence | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 475 | %lld 秒前 | %lld s ago | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:170 |
| 476 | 不到 1 分钟 | Under a minute | — |  |
| 477 | 不是同一个系列 | Not the same series | 不是同一个系列 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 478 | 主屏（摄像头） | Main display (camera) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:608 |
| 479 | 主要听，记重点 | Mostly listen, note the key points | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:16 |
| 480 | 云端识别中断： | Cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:238 |
| 481 | 产品资料、报价 | Product materials, quotes | 产品资料、报价 | Windows/src/NextThought.Desktop/MeetingDraft.cs:77 |
| 482 | 从文件导入术语 | Import terms from a file | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:285 |
| 483 | 会议写入失败： | Writing the meeting failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:432 |
| 484 | 会议归档 ·  | Meeting archived ·  | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:501 |
| 485 | 保存条目失败： | Saving the item failed:  | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:158 |
| 486 | 保存高亮失败： | Saving the highlight failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:144 |
| 487 | 先听需求再回应 | Hear the need first | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:14 |
| 488 | 全文搜索失败： | Full-text search failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:89 |
| 489 | 删除这场会议？ | Delete this meeting? | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:128 |
| 490 | 双屏演讲已关闭 | Presenter mode is off | — |  |
| 491 | 后台下载失败： | Background download failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:225 |
| 492 | 回滚到上一版本 | Roll back to the previous version | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 493 | 回答正在生成… | Generating an answer… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 494 | 在转写中查找… | Find in transcript… | — |  |
| 495 | 实时纪要没有开 | Live notes are off | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:86 |
| 496 | 对方 · 流式 | Them · streaming | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 497 | 导入 .md… | Import .md… | — |  |
| 498 | 导出 Word | Export Word | — |  |
| 499 | 尚未加载技能。 | No skills loaded. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 500 | 展开识别的文字 | Show recognised text | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 501 | 已保存访问令牌 | Access token saved | 已保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 502 | 已暂停自动滚动 | Auto-scroll paused | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 503 | 已连接回答服务 | Answer service connected | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:383 |
| 504 | 已重命名会议： | Meeting renamed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:17 |
| 505 | 已高亮并保存。 | Highlighted and saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:154 |
| 506 | 带入上一场转写 | Bring last transcript | — |  |
| 507 | 开始或停止记录 | Start or stop recording | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:19 |
| 508 | 当前场景：通用 | Current scene: General | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:27 |
| 509 | 当前系列的术语 | Terms in this series | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 510 | 当前问题与候选 | Current question and candidates | — | Windows/src/NextThought.Desktop/Views/DetachedModuleWindow.axaml.cs:68 |
| 511 | 打开位置失败： | Opening the location failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:82 |
| 512 | 打开来源失败： | Could not open the source:  | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:159 |
| 513 | 技能保存失败： | Saving skills failed:  | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:66 |
| 514 | 无法识别按键： | Unrecognized key:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:196 |
| 515 | 未保存访问令牌 | No access token saved | 未保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 516 | 未安装或待修复 | Not installed or needs repair | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:73 |
| 517 | 未导入 PPT | No PPT imported | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:82 |
| 518 | 术语表已清空。 | Glossary cleared. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:498 |
| 519 | 模板保存失败： | Saving the template failed:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:102 |
| 520 | 模板需要名字。 | A template needs a name. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:166 |
| 521 | 正在整理纪要… | Writing the notes… | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 522 | 正在校对转写… | Proofreading the transcript… | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:39 |
| 523 | 正在检查更新… | Checking for updates… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:84 |
| 524 | 正在联网核实… | Checking online… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:574 |
| 525 | 火山方舟 豆包 | Volcengine Ark Doubao | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:376 |
| 526 | 界面操作出错： | Interface error:  | — | Windows/src/NextThought.Desktop/App.axaml.cs:26 |
| 527 | 看一场示例会议 | Watch a sample meeting | — |  |
| 528 | 看一段面试示例 | Watch a sample interview | 看一段面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:67 |
| 529 | 窗口截图失败： | Window capture failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:228 |
| 530 | 等待首次读屏… | Waiting for the first screen read… | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:63 |
| 531 | 系列变更待确认 | Series changes to confirm | — |  |
| 532 | 系列已重命名： | Series renamed:  | — | Windows/src/NextThought.Desktop/AppState.Series.cs:457 |
| 533 | 经验 %lld | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Series.cs:293 |
| 534 | 结束后自动整理 | Summarize automatically after ending | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 535 | 结束并整理纪要 | End and write notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 536 | 联网查证失败： | Web verification failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:750 |
| 537 | 自动滚动到最新 | Auto-scroll to the latest | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 538 | 自定义纪要模板 | Custom note templates | 自定义纪要模板 | Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml |
| 539 | 被问到 1 次 | Asked once | — |  |
| 540 | 要求 / 偏好 | Requirements / preferences | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 541 | 讲进展，听意见 | Share progress, hear feedback | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:11 |
| 542 | 译文 · 当前 | Translation · current | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 543 | 请先输入问题。 | Type a question first. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:515 |
| 544 | 请总结当前讨论 | Summarize the current discussion | — | Windows/src/NextThought.Desktop/AppState.Live.cs:757 |
| 545 | 还没有选择系列 | No series selected | — | Windows/src/NextThought.Desktop/AppState.Series.cs:33 |
| 546 | 这一页讲了什么 | What this page covers | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 547 | 选择自定义模板 | Select custom template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 548 | 静音后直接结束 | End immediately after silence | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:660 |
| 549 | 静音多久后收尾 | End after silence for | — |  |
| 550 | 默认（不指定） | Default (not specified) | — |  |
| 551 |  · 提词按要点 |  · cue by key points | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 552 | %lld 个窗口 | %lld windows | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:28 |
| 553 | 中转地址（可选） | Relay URL (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 554 | 云端识别（实验） | Cloud transcription (experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 555 | 会中笔记已保存。 | Meeting notes saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:115 |
| 556 | 会中组件状态样板 | In-meeting component states | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 557 | 写下讨论的主题。 | Write the discussion topic. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:144 |
| 558 | 准备开始一场会议 | Ready to start a meeting | — |  |
| 559 | 删除已录入的声音 | Delete the enrolled voice | — |  |
| 560 | 可选，还没有资料 | Optional, no materials yet | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 561 | 合并说话人失败： | Could not merge the speakers:  | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:159 |
| 562 | 听到外语自动翻译 | Auto-translate foreign speech | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 563 | 回答语言已更新。 | Answer language updated. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:304 |
| 564 | 声音设备已刷新。 | Audio devices refreshed. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:185 |
| 565 | 尚无自定义模板。 | No custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 566 | 屏幕感知已关闭。 | Screen awareness turned off. | — |  |
| 567 | 屏幕感知已开启。 | Screen awareness turned on. | — |  |
| 568 | 工作会议模板纪要 | Meeting template notes | — |  |
| 569 | 已从系列中移出。 | Removed from the series. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:517 |
| 570 | 已保存 · %@ | Saved · %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:647 |
| 571 | 已关闭实时纪要。 | Live notes are off. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:152 |
| 572 | 已写入系列记录。 | Written to the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:116 |
| 573 | 已加入系列术语。 | Added to the series glossary. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:473 |
| 574 | 已加入系列术语： | Added to the series glossary:  | — |  |
| 575 | 已备份当前版本： | Current version backed up:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:299 |
| 576 | 已安装示例技能。 | Sample skill installed. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:133 |
| 577 | 已忽略这条建议。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:366 |
| 578 | 待确认 %lld | %lld to confirm | — | Windows/src/NextThought.Desktop/AppState.Series.cs:295 |
| 579 | 快捷键保存失败： | Saving shortcuts failed:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:213 |
| 580 | 恢复未全部完成： | Restore did not finish:  | — |  |
| 581 | 截图文件已不存在 | The screenshot file no longer exists | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:239 |
| 582 | 手机伴侣已关闭。 | Phone companion stopped. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:130 |
| 583 | 打开术语表失败： | Opening the glossary failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:334 |
| 584 | 拖动选择截图区域 | Drag to select a capture area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 585 | 接上外接屏后可用 | Available once an external display is connected | 接上外接屏后可用 |  |
| 586 | 提词浮窗鼠标穿透 | Teleprompter click-through | 提词浮窗鼠标穿透 |  |
| 587 | 整个输出设备混音 | Entire output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 588 | 旧原话：缓存几穿 | Old wording: cache miss | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 589 | 显示器 %lld | Display %lld | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:608 |
| 590 | 未知快捷键动作： | Unknown shortcut action:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:194 |
| 591 | 正在下载安装包… | Downloading the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:254 |
| 592 | 正在准备安装包… | Preparing the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:238 |
| 593 | 正在导入 %@… | Importing %@… | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:241 |
| 594 | 正在整理成%@… | Organizing into %@… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:395 |
| 595 | 正在解析 %@… | Parsing %@… | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:122 |
| 596 | 没有可用的显示器 | No display available | — |  |
| 597 | 系列信息已保存。 | Series details saved. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:147 |
| 598 | 系列变更未应用。 | Series change not applied. | — |  |
| 599 | 系列名不能为空。 | A series needs a name. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:130 |
| 600 | 系列文档已移除。 | Series document removed. | — |  |
| 601 | 系统输出设备混音 | System output mix | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 602 | 给这场会起个名字 | Name this meeting | — |  |
| 603 | 被问、要答得清楚 | Asked, answer clearly | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:10 |
| 604 | 讨论重点已整理。 | Discussion points are ready. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:99 |
| 605 | 说话人 %lld | Speaker %lld | 说话人 %lld | Windows/src/NextThought.Desktop/AppState.LivePage.cs:129 |
| 606 | 说话人修改失败： | Could not change the speaker:  | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:133 |
| 607 | 请总结当前讨论。 | Summarize the current discussion. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:821 |
| 608 | 资料库读取失败： | Reading the library failed:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:74 |
| 609 | 跟着讲话翻幻灯片 | Follow the talk across slides | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 610 | 还没有可用的文字 | No usable text yet | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:224 |
| 611 | 还没有归档截图。 | No archived screenshots yet. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 612 | 还没有检查更新。 | No update check yet. | — |  |
| 613 | 这一节要整理什么 | What should this section collect? | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 614 | 这条建议已忽略。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:115 |
| 615 | 配对链接已复制。 | Pairing link copied. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:273 |
| 616 | 首字 %.1fs | first token %.1fs | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:198 |
| 617 | ；本机回退失败： | ; local fallback failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:235 |
| 618 | %@ %lld 段 | %@ %lld sections | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 619 | M月d日 dddd | MMMM d, dddd | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:52 |
| 620 | high · 最强 | high · strongest | — |  |
| 621 | main 最新构建 | main latest build | — | Windows/src/NextThought.Desktop/AppState.Update.cs:46 |
| 622 | 从以前的会议带入… | Carry from an earlier meeting… | 从以前的会议带入… | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 623 | 作业要求、评分标准 | Assignment requirements, grading rubric | 作业要求、评分标准 | Windows/src/NextThought.Desktop/MeetingDraft.cs:80 |
| 624 | 作为新决定加入系列 | Add to the series as a new decision | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:51 |
| 625 | 作为新行动加入系列 | Add to the series as a new action | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:50 |
| 626 | 保存会中笔记失败： | Saving meeting notes failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:117 |
| 627 | 保存复核备注失败： | Saving the review note failed:  | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:98 |
| 628 | 关闭局域网手机伴侣 | Stop LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:321 |
| 629 | 再决定是否调整…… | then decide whether to adjust… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 630 | 准备后可在会中引用 | Ready to cite during the meeting | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 631 | 删除会议和依赖内容 | Delete the meeting and its dependent content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:304 |
| 632 | 删除全部会议记录？ | Delete all meeting records? | 删除全部会议记录？ | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:155 |
| 633 | 删除失败进重试队列 | Failed deletes go to the retry queue | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:293 |
| 634 | 刷新声音设备失败： | Refreshing audio devices failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:187 |
| 635 | 回答服务连接成功： | Answer service connected:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:644 |
| 636 | 在资源管理器中显示 | Show in File Explorer | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 637 | 复制配对链接失败： | Copying the pairing link failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:275 |
| 638 | 复盘 / 当前阶段 | Review / current stage | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 639 | 导入 .json… | Import .json… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 640 | 开启局域网手机伴侣 | Start LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:321 |
| 641 | 手动校正：缓存击穿 | Manual correction: cache stampede | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 642 | 手机伴侣启动失败： | Starting the phone companion failed:  | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:159 |
| 643 | 打开令牌页面失败： | Opening the token page failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:313 |
| 644 | 打开我的声音失败： | Opening My Voice failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:351 |
| 645 | 更改尚未保存：%@ | Changes are not saved yet: %@ | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:126 |
| 646 | 有了内容就开始整理 | It will start once there is something to summarise | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:92 |
| 647 | 本周进展、相关论文 | This week's progress, related papers | 本周进展、相关论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:75 |
| 648 | 本机 Ollama | Local Ollama | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:377 |
| 649 | 本机离线识别已就绪 | On-device offline recognition is ready | — | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:15 |
| 650 | 框选读屏区域失败： | Selecting the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:341 |
| 651 | 模型（留空用默认） | Model (empty uses default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 652 | 模板文件格式无效： | The template file format is invalid:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:214 |
| 653 | 正在加载本场资料… | Loading this meeting's materials… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 654 | 正在备份当前版本… | Backing up the current version… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:292 |
| 655 | 正在截屏读取屏幕… | Reading the screen… | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:45 |
| 656 | 正在更新实时纪要… | Updating live notes… | — |  |
| 657 | 正在本机读取录音… | Reading the recording on this device… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:398 |
| 658 | 正在查证当前回答… | Verifying the current answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:733 |
| 659 | 正在检查回答服务… | Checking the answer service… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:542 |
| 660 | 正在生成参考回答… | Generating a reference answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:542 |
| 661 | 没有新的知识候选。 | No new knowledge candidates. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:260 |
| 662 | 清除读屏区域失败： | Clearing the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:372 |
| 663 | 组会 / 研究讨论 | Lab meeting / research discussion | — |  |
| 664 | 经验 %lld 条 | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:100 |
| 665 | 自定义模板已清空。 | Custom templates cleared. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 666 | 英语回答附中文对照 | Gloss English answers in Chinese | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 667 | 规范写法不能为空。 | The canonical form cannot be empty. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:226 |
| 668 | 讲稿、PPT、论文 | Script, slides, paper | 讲稿、PPT、论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:74 |
| 669 | 语义模型路径无效。 | The semantic model path is not valid. | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:134 |
| 670 | 请只回复：连接成功 | Reply only: connection succeeded | — | Windows/src/NextThought.Desktop/AppState.Live.cs:710 |
| 671 | 读取会议列表失败： | Reading the meeting list failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:63 |
| 672 | 读取窗口列表失败： | Could not read the window list:  | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:32 |
| 673 | 资料 %lld 份 | %lld materials | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:96 |
| 674 | 输入问题，回车回答 | Type a question and press Enter | — |  |
| 675 | 输入问题；回车发送 | Type a question; press Enter to send | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 676 | 这一句改成了%@。 | This line now belongs to %@. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:127 |
| 677 | 选择多个系列后删除 | Select multiple series to delete | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 678 | 选择持续读取的区域 | Choose the area to keep reading | — |  |
| 679 | 问题 %lld 个 | %lld questions | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:274 |
| 680 | 题库 %lld 题 | %lld questions in the bank | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:98 |
| 681 | 麦克风已重新打开。 | Microphone reopened. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:329 |
| 682 | [%@] %@：%@ | [%@] %@: %@ | — |  |
| 683 | none（关闭思考） | none (reasoning off) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:53 |
| 684 | 。接下来是什么场合？ | . What's next? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:57 |
| 685 | 严格按用户要求回复。 | Follow the user's request exactly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:609 |
| 686 | 会后补一张读写时序图 | Add a read/write sequence diagram after the meeting | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:296 |
| 687 | 会议结束时遇到问题： | A problem occurred while ending the meeting:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:358 |
| 688 | 保存说话人设置失败： | Could not save the speaker settings:  | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:147 |
| 689 | 先给系列起一个名字。 | Give the series a name first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:482 |
| 690 | 写这一节要整理什么。 | What should this section collect? | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:150 |
| 691 | 单栏上下排列（小屏） | Single column, stacked (small screen) | — |  |
| 692 | 在设置中选择声音来源 | Choose an audio source in settings | — | Windows/src/NextThought.Desktop/AppState.cs:291 |
| 693 | 屏幕上没有读到文字。 | No text was read from the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:54 |
| 694 | 已导入 %lld 份 | Imported %lld | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 695 | 已新建系列「%@」。 | Series “%@” created. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:490 |
| 696 | 已经有同名的系列了。 | A series with that name already exists. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:485 |
| 697 | 已跳到原话 · %@ | Jumped to the original · %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:608 |
| 698 | 开始记录后才能读屏。 | Start recording before reading the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:108 |
| 699 | 录音转写 · 已归档 | Recording transcribed · archived | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:460 |
| 700 | 提词岛会中一直展开。 | The cue island stays open during meetings. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 701 | 有 %lld 处提示 | %lld notes | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:222 |
| 702 | 未知的云端识别服务。 | Unknown cloud transcription service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:202 |
| 703 | 本机 · 不依赖网络 | On-device · works offline | — |  |
| 704 | 校验通过，正在安装… | Verified; installing… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:308 |
| 705 | 核对上一条回答的事实 | Check the facts in the last answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:736 |
| 706 | 模板「%@」已保存。 | Template “%@” saved. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:178 |
| 707 | 正在检查安装包签名… | Checking the installer signature… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:263 |
| 708 | 没有可用的更新备份。 | No update backup is available. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:170 |
| 709 | 没有需要更正的地方。 | Nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:47 |
| 710 | 看镜头 %.0f%% | Eye contact %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:56 |
| 711 | 第三方许可与版权说明 | Third-party licenses and copyright | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 712 | 联网查证缓存已清空。 | The web-verification cache was cleared. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:62 |
| 713 | 自动（第一块外接屏） | Automatic (first external display) | — | Windows/src/NextThought.Desktop/DisplayCatalog.cs:24 |
| 714 | 负责订单服务的缓存层 | Owns the order-service cache layer | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:292 |
| 715 | 资料复核备注已保存。 | Material review note saved. | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:96 |
| 716 | 还没有可回答的转写。 | No transcript to answer yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:721 |
| 717 | 还没有可查证的回答。 | No answer to verify yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:731 |
| 718 | 这次不带入系列内容。 | Nothing from the series is carried into this meeting. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:102 |
| 719 | 需要先连接回答服务。 | Connect an answer service first | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:320 |
| 720 | 麦克风云端识别中断： | Microphone cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:252 |
| 721 | （会议窗口保持显示） |  (meeting window stays visible) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:690 |
| 722 | ；界面按钮仍可使用。 | ; window buttons still work. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 723 |  · 说话人 %lld |  · speaker %lld | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:108 |
| 724 | %@ · 建议放：%@ | %@ · Suggested materials: %@ | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:93 |
| 725 | API · 未指定模型 | API · no model chosen | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:192 |
| 726 | 下午好。把讨论留下来。 | Good afternoon. Keep the discussion. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:48 |
| 727 | 中午好。会议还在继续。 | Good noon. The meeting goes on. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:47 |
| 728 | 云服务 API Key | Cloud service API key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 729 | 会议已导出为 PDF。 | Meeting exported as PDF. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:189 |
| 730 | 先开始或打开一场会议。 | Start or open a meeting first. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:109 |
| 731 | 发言占比 %.0f%% | Speaking share %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:54 |
| 732 | 回答在左 · 对话在右 | Answers left · conversation right | — |  |
| 733 | 回答服务（Codex） | Answer service (Codex) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 734 | 备份失败，仍继续更新： | Backup failed; continuing the update:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:304 |
| 735 | 导入 %@ 失败：%@ | Importing %@ failed: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:245 |
| 736 | 导出 Markdown | Export Markdown | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 737 | 已把这一句算作我说的。 | This line now counts as mine. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:35 |
| 738 | 已结束 · 会议已归档 | Ended · meeting archived | — | Windows/src/NextThought.Desktop/AppState.Live.cs:355 |
| 739 | 幻灯片：%@ · %@ | Slide: %@ · %@ | — |  |
| 740 | 引用来自已删除的资料： | Citations from deleted materials:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:147 |
| 741 | 当前回答尚无资料引用。 | The current answer has no material citations yet. | — |  |
| 742 | 当前没有可命名的说话人 | No speaker to name right now | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:62 |
| 743 | 录入 / 管理我的声音 | Enroll / manage my voice | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 744 | 最长独白 %.0f 秒 | Longest monologue %.0f s | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:55 |
| 745 | 未找到第三方许可文件。 | Third-party license file not found. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:321 |
| 746 | 正在加载本地语音模型… | Loading the on-device speech model… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:172 |
| 747 | 正在记录的会议不能删除 | A meeting that is recording cannot be deleted | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:174 |
| 748 | 没有可导入的技能文件。 | No skill file to import. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:259 |
| 749 | 没有找到可截图的窗口。 | No window was found to capture. | — |  |
| 750 | 示例技能已经在列表里。 | The sample skill is already in the list. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:132 |
| 751 | 系列已有相近经验：%@ | The series already has related lessons: %@ | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:241 |
| 752 | 联网来源 %lld 条 | %lld web sources | — | Windows/src/NextThought.Desktop/AppState.cs:314 |
| 753 | 误听写法，用 \| 分隔 | Misheard forms separated by \| | — |  |
| 754 | 这场会议没有生成回答。 | This meeting has no answers. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 755 | 重新联网查一下当前回答 | Check the current answer again with the web | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 756 | 隐藏 / 恢复所有窗口 | Hide / restore all windows | — |  |
| 757 | %lld 个自定义模板。 | %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 758 | minimal · 最快 | minimal · fastest | — |  |
| 759 | 云端没有返回可用的文字。 | The cloud returned no usable text. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:433 |
| 760 | 交付之前，先把话说清楚。 | Say it clearly before the meeting. | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 761 | 使用语义与关键词混合检索 | Hybrid semantic and keyword retrieval | — |  |
| 762 | 依据：“%@” · %@ | Basis: “%@” · %@ | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:36 |
| 763 | 先更新数据库，再删除缓存 | Update the database first, then delete the cache | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:293 |
| 764 | 全文搜索至少输入两个字。 | Enter at least two characters for full-text search. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:81 |
| 765 | 内置屏 / 主屏（自动） | Built-in / main display (automatic) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:57 |
| 766 | 可以继续对这场会议提问。 | You can keep asking about this meeting. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:515 |
| 767 | 在这里写这个技能的要求。 | Write what this skill requires. | — |  |
| 768 | 字幕已导出（%@）%@。 | Subtitles exported (%@)%@. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:366 |
| 769 | 已在浏览器中打开下载页。 | Opened the download page in the browser. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:332 |
| 770 | 已把 %@ 并入 %@。 | Merged %@ into %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:157 |
| 771 | 已把 %@ 移到「%@」 | Moved %@ to “%@” | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:220 |
| 772 | 已讲 %lld/%lld | Spoken %lld/%lld | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:270 |
| 773 | 已读屏 · %lld 字 | Screen read · %lld characters | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:64 |
| 774 | 已选择 %lld 场会议 | %lld meetings selected | — |  |
| 775 | 开始记录后才能暂停聆听。 | Start recording before pausing listening. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:223 |
| 776 | 当前场景：%@ · %@ | Current scene: %@ · %@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 777 | 技能（回答时遵守的说明） | Skills (instructions followed when answering) | — |  |
| 778 | 提词浮窗已恢复鼠标操作。 | Teleprompter clicks restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:493 |
| 779 | 搜索会议标题、日期或模式 | Search meeting title, date or mode | — |  |
| 780 | 早上好。今天要记下什么？ | Good morning. What should we capture today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:46 |
| 781 | 本机 LM Studio | Local LM Studio | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:377 |
| 782 | 术语表（人名与专有名词） | Glossary (names and terms) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 783 | 模板文件里没有可用模板。 | The template file has no usable templates. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:211 |
| 784 | 没有匹配到「数据库迁移」 | No match for “database migration” | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 785 | 演示模式：未加载识别模型 | Demo mode: recognition model not loaded | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:102 |
| 786 | 简历、职位描述、公司信息 | Résumé, job description, company info | 简历、职位描述、公司信息 | Windows/src/NextThought.Desktop/MeetingDraft.cs:73 |
| 787 | 继续完成设置（还差%@） | Finish setting up (missing %@) | 继续完成设置（还差%@） | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:47 |
| 788 | 选择声音来源，开始记录。 | Choose an audio source and start recording. | — | Windows/src/NextThought.Desktop/AppState.cs:266 |
| 789 | 麦克风 · 适合线下会议 | Microphone · for in-person meetings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:78 |
| 790 | ；已经记录的内容已保存。 | ; what was recorded is saved. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:356 |
| 791 |   ·  另 %lld 份 |   ·  %lld more | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 792 | Soniox · 中英混说 | Soniox · bilingual | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:469 |
| 793 | 会议已保存，可以整理纪要。 | Meeting saved; notes can be organized. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:356 |
| 794 | 使用系列知识（经验与题库） | Use series knowledge (lessons and question bank) | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 795 | 夜深了。要把想法留下来吗？ | Late night. Want to keep a thought? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:45 |
| 796 | 安装已下载的更新并重新打开 | Install the downloaded update and reopen | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 797 | 已从「%@」移除「%@」。 | Removed “%@” from “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:180 |
| 798 | 已删除 %lld 场会议。 | %lld meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:45 |
| 799 | 已导入 %lld 份资料。 | %lld materials imported. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:135 |
| 800 | 已暂停自动滚动（点击恢复） | Auto-scroll paused (choose to resume) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 801 | 已采纳，下次回答会带上它。 | Accepted; later answers will use it. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:369 |
| 802 | 手动提问，或开启自动回答。 | Ask manually, or turn on automatic answers. | — | Windows/src/NextThought.Desktop/AppState.cs:285 |
| 803 | 整句精修，提高最终转写质量 | Refine whole sentences for a better transcript | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 804 | 新增 2 句 · 回到现场 | +2 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 805 | 新增 6 句 · 回到现场 | +6 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 806 | 正在校验后台下载的安装包… | Verifying the background download… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:247 |
| 807 | 没有新的已核实决定或行动。 | No new verified decisions or actions. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:132 |
| 808 | 系列已删除；会议记录保留。 | Series deleted; meeting records kept. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:467 |
| 809 | 自动滚动到最新（点击暂停） | Auto-scroll to the latest (choose to pause) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 810 | 被问到时，答案就在镜头旁。 | The answer sits beside the camera. | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 811 | 语速 %.0f 单位/分钟 | Pace %.0f units/min | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:53 |
| 812 | 读屏失败，20 秒后重试。 | Screen read failed; retrying in 20 seconds. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:70 |
| 813 | 这个键不能用作全局快捷键。 | That key cannot be used as a global shortcut. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:455 |
| 814 | 这次没更新成功，稍后会再试 | This update did not come back; it will try again shortly. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:88 |
| 815 | 面试官在追问 · 参考回答 | The interviewer is following up · reference answer | 面试官在追问 · 参考回答 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 816 | %lld 小时 %lld 分 | %lld h %lld min | — |  |
| 817 | 下一步讲热点 key 的处理 | Next: hot-key handling | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:295 |
| 818 | 中转地址不是有效的 URL。 | The relay address is not a valid URL. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:567 |
| 819 | 先停止当前任务，再删除会议。 | Stop the current task before deleting meetings. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:54 |
| 820 | 先停止当前任务，再删除系列。 | Stop the current task before deleting a series. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:341 |
| 821 | 全局快捷键已保存并重新注册。 | Shortcuts saved and re-registered. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:211 |
| 822 | 全文搜索：%lld 条命中。 | Full-text search: %lld hits. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:87 |
| 823 | 导入技能（Markdown） | Import skill (Markdown) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:243 |
| 824 | 已刷新可采集的应用程序列表。 | The list of recordable apps was refreshed. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:24 |
| 825 | 已把「%@」附着到「%@」。 | Attached “%@” to “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:179 |
| 826 | 已整理成%@，可复制或导出。 | Organized into %@; ready to copy or export. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:481 |
| 827 | 已新建技能，改完内容后保存。 | New skill created; edit it and save. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:113 |
| 828 | 幻灯片在 %@，提示在 %@ | Slides on %@, cue on %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:607 |
| 829 | 拖动可移动 · 双击隐藏控件 | Drag to move · double-click to hide controls | — |  |
| 830 | 搜索命中 %lld/%lld | Search hits %lld/%lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:46 |
| 831 | 正在加载模型并转写 WAV… | Loading the model and transcribing the WAV… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:400 |
| 832 | 正在按《%@》合并分段结果… | Merging parts for “%@”… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:226 |
| 833 | 系列变更待确认 · %lld | Series changes to confirm · %lld | — | Windows/src/NextThought.Desktop/AppState.Series.cs:80 |
| 834 | 纪要模型（留空沿用回答模型） | Notes model (empty uses the answer model) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 835 | 线上会议听电脑里播放的声音。 | Online meetings listen to what this computer plays. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:173 |
| 836 | 识别出来的错误写法，逗号分隔 | Recognized misspellings, comma separated | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:82 |
| 837 | 语速偏快，适当放慢并加停顿。 | Speaking fast; slow down and pause. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:320 |
| 838 | 说话人命名已应用到当前转写。 | Speaker names applied to the current transcript. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:208 |
| 839 | 资料与 AI 都可稍后补充。 | Materials and an answer service can both be added later. | 资料与 AI 都可稍后补充。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:88 |
| 840 | 这场会议还没有可复核的条目。 | This meeting has nothing to review yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:125 |
| 841 | 这场会议还没有可收集的候选。 | This meeting has no candidates to collect yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:251 |
| 842 | 退出 NextThought | Quit NextThought | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:20 |
| 843 | 选一个类型作为基础，再改名字 | Pick a base type, then rename it | 选一个类型作为基础，再改名字 | Windows/src/NextThought.Desktop/MeetingDraft.cs:68 |
| 844 | 静音后先提示，再结束（推荐） | Prompt after silence, then end (recommended) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:659 |
| 845 | %lld 份资料 · 本机解析 | %lld materials · parsed on this device | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:57 |
| 846 | NextThought 字幕条 | NextThought Subtitles | NextThought 字幕条 | Windows/src/NextThought.Desktop/Views/SubtitleBarWindow.axaml |
| 847 | NextThought 提词器 | NextThought Teleprompter | NextThought 提词器 |  |
| 848 | NextThought 提词岛 | NextThought Cue island | NextThought 提词岛 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 849 | 下周的评审，我们只带一个版本。 | One version for next week's review. | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 850 | 云端识别中断，已回退本机识别： | Cloud transcription interrupted; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:230 |
| 851 | 以下全局快捷键被其他程序占用： | These global shortcuts are used by other programs:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 852 | 会议已导出为 Word 文档。 | Meeting exported as a Word document. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:174 |
| 853 | 保存 Tavily 密钥失败： | Saving the Tavily key failed:  | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:33 |
| 854 | 先停止当前任务，再准备下一场。 | Stop the current task before preparing the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Home.cs:137 |
| 855 | 准备声纹组件（约 26 MB） | Prepare speaker components (~26 MB) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 856 | 删除会议，保留已确认的系列内容 | Delete the meeting and keep accepted series content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:302 |
| 857 | 双屏演讲已结束，主窗口已恢复。 | Presenter mode ended; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:717 |
| 858 | 回答暂时不可用；记录仍在继续。 | Answers are temporarily unavailable; recording continues. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 859 | 对话在左 · 回答在右（默认） | Conversation left · answers right (default) | — |  |
| 860 | 尚未保存密钥；本机服务可不填。 | No key saved yet; optional for local services. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 861 | 已导入，共 %lld 个技能。 | Imported %lld skills. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:47 |
| 862 | 已把「%@」放进系列「%@」。 | Added “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:505 |
| 863 | 已按屏幕切到第 %lld 页。 | Switched to slide %lld by screen. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:130 |
| 864 | 已读取归档；%lld 项待写入 | Archive read; %lld items pending write | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:653 |
| 865 | 当前有任务在进行，稍后再截图。 | A task is running; capture shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:213 |
| 866 | 当前有任务在进行，稍后再框选。 | A task is running; select the area shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:315 |
| 867 | 提词岛已收起，鼠标移上来就展开 | Cue island collapsed; hover to expand | — |  |
| 868 | 提词岛恢复为鼠标移上去才展开。 | Cue island expands on hover again. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 869 | 最近会议 · 共 %lld 场 | Recent meetings · %lld in total | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:498 |
| 870 | 校对完成：没有需要更正的地方。 | Proofreading finished: nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:48 |
| 871 | 正在记录 · %lld 条转写 | Recording · %lld lines | — | Windows/src/NextThought.Desktop/AppState.Live.cs:10 |
| 872 | 正在通过云端识别转写 WAV… | Transcribing the WAV with cloud recognition… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:415 |
| 873 | 流式模型：%@　整句精修：%@ | Streaming model: %@; sentence refinement: %@ | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:72 |
| 874 | 演示数据 · 不会写入你的资料 | Demo data · nothing is written to your materials | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:161 |
| 875 | 界面语言 / Language | Interface language | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 876 | 程序正在退出，已取消开始记录。 | The app is quitting; start recording was cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:187 |
| 877 | 第 %lld / %lld 页 | Page %lld / %lld | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:28 |
| 878 | 系统声音范围（每次开始前选择） | System audio scope (choose before each start) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 879 | 纪要窗口 → 字幕条 → 隐藏 | Notes window → subtitle bar → hidden | — |  |
| 880 | 还没开过 · %lld 份资料 | Not started yet · %lld files | — | Windows/src/NextThought.Desktop/AppState.Home.cs:124 |
| 881 | 这场会议还没有转写，无法整理。 | This meeting has no transcript yet. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:384 |
| 882 | 进行中 · 转写持续保存在本机 | In progress · transcript saved locally | — | Windows/src/NextThought.Desktop/AppState.Live.cs:304 |
| 883 | 选区 %lld×%lld 像素 | Selection %lld×%lld pixels | — |  |
| 884 | 面试官追问：删除缓存失败怎么办 | The interviewer asked: what if deleting the cache fails? | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:293 |
| 885 | …… 其余 %lld 段未显示。 | … %lld more sections are hidden. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:103 |
| 886 | 先停止当前任务，再重新整理纪要。 | Stop the current task before organizing notes again. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:315 |
| 887 | 先开始记录，再改这一句的说话人。 | Start recording before changing who said this line. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:102 |
| 888 | 准备这场会 · 类型、资料与检查 | Prepare this meeting · type, materials and checks | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:23 |
| 889 | 只在本机运行，声纹只是一组数字。 | Runs on-device; the voiceprint is just numbers. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 890 | 回答已完成。会议内的回答已归档。 | Answer complete. Answers from the meeting are archived. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:644 |
| 891 | 已保存 %lld 个自定义模板。 | Saved %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 892 | 已删除系列「%@」及其会议记录。 | Deleted series “%@” and its meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:354 |
| 893 | 已删除选中的 %lld 场会议。 | %lld selected meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:35 |
| 894 | 已把 %lld 份资料加入系列。 | Added %lld materials to the series. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:258 |
| 895 | 已清除说话人 %lld 的名字。 | Cleared the name of speaker %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:119 |
| 896 | 已跟随讲话切到第 %lld 页。 | Switched to slide %lld by speech. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:128 |
| 897 | 查证完成：%lld 条联网来源。 | Verification complete: %lld web sources. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:748 |
| 898 | 核心查询从数据库迁到 Redis | Moved the main query from the database to Redis | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:292 |
| 899 | 模型已下载并校验，可以离线记录。 | Model downloaded and verified; offline recording is ready. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:81 |
| 900 | 演示数据 · Demo data | Demo data | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 901 | 第 %lld 页 · %@⏎%@ | Slide %lld · %@
%@ | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:169 |
| 902 | 系统声音里出现提问时自动生成回答 | Answer automatically when a question appears in the system audio | — |  |
| 903 | 要求 / Preferences | Requirements / Preferences | — |  |
| 904 | 说了很久了，收个尾，把话交回去。 | You have talked a while; wrap up and hand it back. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:319 |
| 905 | 说话人区分暂不可用，转写仍保留： | Speaker separation unavailable; the transcript is kept:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:157 |
| 906 | 这场会用的资料 · %lld 份 | Materials for this meeting · %lld | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:532 |
| 907 | 高级：也可以直接按文本批量编辑。 | Advanced: you can also edit everything as text. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 908 | %@ 字幕 (*.%@)\|*.%@ | %@ subtitles (*.%@)\|*.%@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:354 |
| 909 | %lld 个技能；回答时按需遵守。 | %lld skills; followed as needed when answering. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 910 | %lld 个技能；已保存启停状态。 | %lld skills; on/off state saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:86 |
| 911 | X-ASR 中英 · 160 ms | X-ASR bilingual · 160 ms | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:47 |
| 912 | 上次会议被中断，已保存到所有记录。 | The last meeting was interrupted and saved in all records. | 上次会议被中断，已保存到所有记录。 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:113 |
| 913 | 云端识别启动失败，已回退本机识别： | Cloud transcription failed to start; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:266 |
| 914 | 使用本机 Codex CLI 账号 | Use local Codex CLI account | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 915 | 全部系列 · All series | All series | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 916 | 删除系列「%@」？会议记录会保留。 | Delete series “%@”? Meeting records are kept. | — |  |
| 917 | 区分会议中的说话人（本机实验功能） | Separate speakers (on-device, experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 918 | 历史回答的引用已保存，可查看来源。 | Citations for the past answer were saved; view the sources. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:504 |
| 919 | 只影响悬浮字幕条；保存后立即生效。 | Only affects the floating subtitle bar; takes effect immediately after saving. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 920 | 听众可能会问（点一下提前准备回答） | Questions the audience may ask (tap to prepare an answer) | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 921 | 启动后自动检查更新（每 6 小时） | Check for updates automatically after launch (every 6 hours) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 922 | 在屏幕顶部显示问题与回答，不抢焦点 | Show question and answer at the top of the screen without taking focus | — |  |
| 923 | 对方讲得比较多，可以主动补充一句。 | They are talking more; add a point when you can. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:322 |
| 924 | 尚未选择声音来源，请到设置中选择。 | No audio source chosen; pick one in settings. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:78 |
| 925 | 幻灯片：第 %lld 页（手动）。 | Slide: page %lld (manual). | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:129 |
| 926 | 引导已稍后继续；首页会留一个入口。 | The guide was put aside; the home page keeps an entry. | — | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:66 |
| 927 | 录音已转写并归档，未保存原始音频。 | Recording transcribed and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:448 |
| 928 | 按资料原有的页、幻灯片或工作表定位 | Locate by the material's own page, slide or sheet | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 929 | 晚上好。今天有哪些值得留下的结论？ | Good evening. What conclusions are worth keeping today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:49 |
| 930 | 检测到屏幕共享，已自动收回提示岛。 | Screen sharing detected; the cue island was retracted. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:47 |
| 931 | 正在记录所选应用及其子进程的声音。 | Recording the selected app and its child processes. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:307 |
| 932 | 第 %lld 页，共 %lld 页 | %1$lld of %2$lld pages | 第 %lld 页，共 %lld 页 | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:85 |
| 933 | 纪要编辑已保存；生成结果未被覆盖。 | Notes edits saved; generated content was not overwritten. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:122 |
| 934 | 说话人 %lld 已命名为 %@。 | Speaker %lld is now named %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:120 |
| 935 | 请针对上一条回答给出一个具体例子。 | Give a concrete example for the last answer. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:827 |
| 936 | 还没有连接回答服务，写不了实时纪要 | No answer service is connected, so live notes cannot be written | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:89 |
| 937 | 这个技能的要求（Markdown） | Skill requirements (Markdown) | — |  |
| 938 | 这场会议没有任何发言，已自动停止。 | This meeting had no speech and stopped automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:121 |
| 939 | 这场会议还没有纪要，点击上方整理。 | This meeting has no notes yet; organize it above. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:516 |
| 940 | 这条内容已被重新生成，编辑未应用。 | This item was regenerated; the edit was not applied. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:143 |
| 941 |  回答暂时不可用；记录仍在继续。 |  Answers are temporarily unavailable; recording continues. | — |  |
| 942 | Tavily API Key（可选） | Tavily API key (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 943 | 一句话经验，例如：先说结论，再说依据 | One-line lesson, e.g. lead with the conclusion, then the evidence | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 944 | 云端识别已连接；转写发送到所选服务。 | Cloud transcription connected; the transcript is sent to the chosen service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:254 |
| 945 | 参考资料库时使用语义与关键词混合检索 | Use hybrid semantic and keyword retrieval for materials | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 946 | 在左边选择一个系列，或者先新建一个。 | Select a series on the left, or create one first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:35 |
| 947 | 在转写中查找（听错的写法也会找出来） | Search the transcript (misspelled forms are found too) | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:51 |
| 948 | 声音在本机识别。应用不保存原始录音。 | Speech is recognized on this device. The app does not save the original audio | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:364 |
| 949 | 已取消，已经记录的内容仍保存在本机。 | Cancelled; what was recorded is still stored on this device. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:241 |
| 950 | 已把「%@」当作上一场，准备下一场。 | Treated “%@” as the previous meeting; preparing the next one. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:225 |
| 951 | 已框选读屏区域：%lld×%lld。 | Screen-read area selected: %lld×%lld. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:334 |
| 952 | 引导已完成；之后可在设置中重新查看。 | The guide is complete; see it again from settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:175 |
| 953 | 更新于 %@ · 约每 45 秒一次 | Updated %@ · about every 45 seconds | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:91 |
| 954 | 条目已编辑；生成结果的其他内容未改。 | Item edited; the rest of the generated content is unchanged. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:156 |
| 955 | 框选演示内容；后续画面均读取该区域。 | Select the demo content; every later frame reads that area. | — |  |
| 956 | 模板至少需要一个有标题和要求的章节。 | A template needs at least one section with a title and requirement. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:173 |
| 957 | 正在根据这一页和你的资料写讲稿提示… | Writing talking points for this page from the slide and your materials… | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 958 | 正确写法，例如 Kubernetes | Correct form, e.g. Kubernetes | — |  |
| 959 | 系统 %.0f · 麦克风 %.0f | System %.0f · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.Live.cs:294 |
| 960 | 译文未生成，原文已保留，可稍后补译： | Translation not generated; the original is kept, retry later:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:177 |
| 961 | 过期状态来自版本比较；旧内容仍可读。 | Stale state comes from revision comparison; old content stays readable. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 962 | 还没有导入 PPTX，导入后可翻页。 | No PPTX imported yet; import one to turn pages. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:107 |
| 963 | 这是连通性测试。只回复两个字：收到。 | This is a connectivity test. Reply with two words only: received. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:122 |
| 964 | 这段时间你讲得偏多，留点空间给对方。 | You have spoken a lot lately; leave room for them. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:321 |
| 965 | 连接检查通过 · 用时 %.1f 秒 | Connection check passed · %.1f s | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:181 |
| 966 | 选择一份资料，查看本机解析出的文本。 | Choose a material to see the text parsed on this device. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:22 |
| 967 | 长时间没有新的发言，会议已自动结束。 | No new speech for a long time; the meeting ended automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:120 |
| 968 | 领域字段（每行 key=value） | Domain fields (key=value per line) | — |  |
| 969 | ；Ctrl+Alt+N 唤回主窗口。 | ; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:673 |
| 970 | %lld 场会议 · 共 %lld 场 | %lld meetings · %lld total | — | Windows/src/NextThought.Desktop/AppState.cs:324 |
| 971 | %lld 条转写 · %lld 条回答 | %lld lines · %lld answers | — | Windows/src/NextThought.Desktop/AppState.Live.cs:11 |
| 972 | 保留查询与范围；可扩大范围或清除查询。 | Query and scope kept; widen the scope or clear the query. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 973 | 全文搜索：%lld 场会议有匹配内容。 | Full-text search: %lld meetings have matching content. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:122 |
| 974 | 切换鼠标穿透（穿透后只能用快捷键改回） | Toggle click-through (afterwards only a shortcut can restore clicks) | — |  |
| 975 | 声纹模型已准备，可在设置中开启并保存。 | Speaker model ready; enable and save it in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:90 |
| 976 | 已保存 · 修订 12 · 03:05 | Saved · revision 12 · 03:05 | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 977 | 已删除 %lld 个系列及其会议记录。 | Deleted %lld series and their meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:379 |
| 978 | 已删除 %lld 场会议及其依赖内容。 | %lld meetings and their dependent content deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:60 |
| 979 | 已替换这道题的回答，旧回答留在历史里。 | The answer was replaced; the old one stays in history. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:367 |
| 980 | 已识别 · 是否用于回答见“本次依据” | Recognised · see “Based on” for whether it was used | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 981 | 必需：选择服务商，粘贴 API Key | Required: choose a provider and paste an API key | 必需：选择服务商，粘贴 API Key | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:19 |
| 982 | 所选系统输出设备混音 · 不采集麦克风 | Selected system output mix · microphone not captured | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:77 |
| 983 | 把还没有译文的完整句发送到当前回答服务 | Send untranslated complete sentences to the current answer service | — |  |
| 984 | 无需授权 · 能听到会议里对方的声音。 | No permission needed · hears the other side through this PC | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:268 |
| 985 | 旧正文基于修订前原话；手动校正仍保留。 | The old body is based on the previous wording; manual corrections are kept. | — |  |
| 986 | 旧译文基于修订前原话；手动校正仍保留。 | The old translation follows the previous wording; manual corrections are kept. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 987 | 检查完即可开始；会中仍可随时切换场合。 | Start once the checks pass; switch scenes during the meeting too. | — |  |
| 988 | 正在记录。系统声音为所选输出设备混音。 | Recording. System audio is the chosen output mix. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:307 |
| 989 | 纪要已保存，可导出 Markdown。 | Notes saved; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:553 |
| 990 | 纪要模板：章节由模板决定，模型只填内容 | Note template: the template defines sections, the model fills them | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 991 | 线下会议只用麦克风，转写标为“现场”。 | Offline meetings use the microphone only; lines are marked as on-site. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:172 |
| 992 | 聆听已暂停；恢复后从当前时刻继续记录。 | Listening paused; resume from this point. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:227 |
| 993 | 说话人 %lld 已合并到 %lld。 | Speaker %lld merged into %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:276 |
| 994 | 请先选择一个系列，或在全局作用域编辑。 | Choose a series first, or edit in the global scope. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:232 |
| 995 | %@ · %lld 段 · %.2f 秒 | %@ · %lld sections · %.2f s | — | Windows/src/NextThought.Desktop/AppState.Live.cs:554 |
| 996 | Codex 模型（留空沿用账号默认模型） | Codex model (empty uses the account default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 997 | Ctrl+Alt+N 不挑场合，立即记录 | Ctrl+Alt+N records right away, whatever you are doing | — |  |
| 998 | NextThought · 把讨论留下来 | NextThought · Keep the discussion | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:40 |
| 999 | WAV 录音 (*.wav)\|*.wav | WAV recording (*.wav)\|*.wav | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:46 |
| 1000 | 下一场会议 · NEXT MEETING | NEXT MEETING | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1001 | 云端识别启动失败，且本机语音模型未就绪： | Cloud transcription failed and the on-device speech model is not ready:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:270 |
| 1002 | 例如：先说结论再说依据；数据都标注来源。 | For example: state the conclusion first, then the evidence; mark every source. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1003 | 保存受阻 · 有 3 项待保存 · 重试 | Saving is blocked · 3 pending · Retry | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1004 | 保留了原来的经验，只记下这次会议的来源。 | Kept the original lesson; only the meeting source was recorded. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:368 |
| 1005 | 先选场合、补资料、做检查，再开始这场会。 | Pick a scene, add materials, run the checks, then start. | — |  |
| 1006 | 全局快捷键（Ctrl+Alt + 按键） | Global shortcuts (Ctrl+Alt + key) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1007 | 删除会议「%@」？会议归档会从磁盘移除。 | Delete meeting “%@”? Its archive will be removed from disk. | — |  |
| 1008 | 发一句测试问题，确认回答服务能正常回答。 | Send a test question to confirm the answer service replies | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:401 |
| 1009 | 字体样张 · TYPE SPECIMEN | TYPE SPECIMEN | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1010 | 已导入模板，共 %lld 个自定义模板。 | Imported %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:117 |
| 1011 | 已录入（来自旧模型的声纹，需要重新录入） | Enrolled (voiceprint from an old model; enroll again) | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:96 |
| 1012 | 已把 %lld 条决定/行动加入待复核。 | Added %lld decisions/actions to the review list. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:133 |
| 1013 | 已把「%@」从「%@」移到系列「%@」。 | Moved “%@” from “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:504 |
| 1014 | 已收集 %lld 条知识候选，等待复核。 | Collected %lld knowledge candidates for review. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:261 |
| 1015 | 截图与 OCR 文本已就绪；可直接发送。 | Screenshot and OCR text ready; send it directly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:476 |
| 1016 | 截图已就绪；可直接发送，或补充你的问题。 | Screenshot ready; send it directly or add your question. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:476 |
| 1017 | 正在联网核实（使用 %@ 的查证结果）… | Verifying online (using %@ verification results)… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:570 |
| 1018 | 讲稿在左 · 回答在右（演讲 / 答辩） | Script left · answers right (talk / defense) | — |  |
| 1019 | 还没有幻灯片；导入 PPTX 后可翻页。 | No slides yet; import a PPTX to turn pages. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:170 |
| 1020 | 还没有术语；添加一个正确写法和常见误听。 | No glossary entries; add a correct form and common mishearing. | — |  |
| 1021 | 选一个服务商，粘贴 Key，点一下检查。 | Pick a provider, paste a key, then run the check | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:390 |
| 1022 | 麦克风增益（1–8 倍，声音偏小时调高） | Microphone gain (1–8×, raise for quiet audio) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1023 | # %@⏎⏎会议时间：%@ · 场景：%@ | # %@

Meeting time: %@ · Scene: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:400 |
| 1024 | # %@⏎⏎会议时间：%@ · 模板：%@ | # %@

Meeting time: %@ · Template: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:202 |
| 1025 | X-ASR 中英 · 480 ms（推荐） | X-ASR bilingual · 480 ms (recommended) | X-ASR 中英 · 480 ms（推荐） | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:46 |
| 1026 | 保存后立即应用；跟随系统会随后台主题切换。 | Applied after saving; Follow system tracks the OS theme. | — |  |
| 1027 | 切换为只记录：不再自动回答，仍可手动提问。 | Switched to record-only: no automatic answers, but you can still ask by hand. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 1028 | 原凭据无法由当前用户解密，请重新输入密钥。 | The current user cannot decrypt the stored credential; enter the key again. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:772 |
| 1029 | 原始音频不保存；转写与回答保存在「记录」里 | Raw audio is not kept; the transcript and answers stay under Records. | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1030 | 回答不超过三句话：先给结论，再给一条依据。 | Answer in at most three sentences: conclusion first, then one reason. | — |  |
| 1031 | 已保存 %lld 个技能，下一场回答生效。 | Saved %lld skills; used in the next answer. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 1032 | 已保存（当前 Windows 用户加密）。 | Saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:344 |
| 1033 | 已开启实时纪要：大约每 45 秒整理一次。 | Live notes are on: they update about every 45 seconds. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:151 |
| 1034 | 录音已通过云端转写并归档，未保存原始音频。 | Recording transcribed by the cloud and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:436 |
| 1035 | 技能可用于所有系列，或在系列中按系列选择。 | Skills can apply to all series, or be chosen per series. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1036 | 整理结果不是可用的 JSON，稍后可重试。 | The result was not usable JSON; try again later. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:408 |
| 1037 | 正在检索相关资料；首次使用时生成本地索引… | Searching materials; the local index is built on first use… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:550 |
| 1038 | 演示模式：内置样本数据，不会写入你的资料。 | Demo mode: built-in sample data, nothing is written to your materials. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:116 |
| 1039 | 记录时将完整句中英互译（使用当前回答服务） | Translate full sentences while recording (uses the answer service) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1040 | 请解释这张截图，指出关键信息、数字或风险。 | Explain this screenshot and point out key facts, numbers or risks. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:474 |
| 1041 | 转写已在界面更正，但用户更正记录写入失败： | The transcript was corrected on screen, but writing the correction record failed:  | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:77 |
| 1042 | 还不能开始记录：请先在设置里准备语音组件。 | Cannot start recording yet: prepare the speech components in settings. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:345 |
| 1043 | 采纳后进入这个系列的经验，下次回答会带上它 | Accepted lessons join this series and are used in later answers | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:255 |
| 1044 | 预览态由演示环境注入，不代表鼠标事件录制。 | Preview state is injected by the demo; it does not record pointer events. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1045 |  已保存 · 修订 12 · 03:05 |  Saved · revision 12 · 03:05 | — |  |
| 1046 |  旧正文基于修订前原话；手动校正仍保留。 |  The old body is based on the previous wording; manual corrections are kept. | — |  |
| 1047 | ，其中 %lld 条按文字长度估算结束时间 | , %lld with estimated end times | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:365 |
| 1048 | %lld 场 · %@ · %lld 份资料 | %lld meetings · %@ · %lld files | — | Windows/src/NextThought.Desktop/AppState.Home.cs:123 |
| 1049 | Codex 已登录 (ChatGPT 账号) | Codex signed in (ChatGPT account) | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:381 |
| 1050 | 共 %lld 份资料，均在本机解析，不上传。 | %lld materials, all parsed on this device, never uploaded. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:42 |
| 1051 | 切换为自动回答：识别到问题后会生成参考回答。 | Switched to automatic answers: a reference answer is generated when a question is recognized. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 1052 | 外接屏已断开，双屏演讲已退出，主窗口已恢复。 | External display disconnected; presenter mode exited; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:647 |
| 1053 | 如果发布窗口需要延长，我们会先核对回滚指标。 | If the release window needs more time, we will check the rollback metrics first. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1054 | 密钥已保存（当前 Windows 用户加密） | Key saved (encrypted for the current Windows user) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 1055 | 已删除会议「%@」，已确认的系列内容已保留。 | Deleted meeting “%@”; accepted series content kept. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:139 |
| 1056 | 已按《%@》整理，可导出 Markdown。 | Organized with “%@”; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:251 |
| 1057 | 把资料加入系列请在资料库中选择；这里可移除。 | Add materials to the series from the library; remove them here. | — |  |
| 1058 | 最近记录 · Recent meetings | Recent meetings | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1059 | 术语表已保存 %lld 条，下一场会议生效。 | Glossary saved (%lld terms); used in the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:499 |
| 1060 | 没有符合条件的记录，可以清除条件后查看全部。 | No records match the filters; clear them to see everything. | — |  |
| 1061 | 系统声音与麦克风分别处理；只在本机保存文字。 | System audio and microphone are handled separately; only text is stored locally. | — |  |
| 1062 | 线下会议靠它听现场的声音，开始时会询问一次。 | In-room meetings hear the room through it; you are asked once when starting | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:309 |
| 1063 | 还没有会议记录。点击「开始记录」写下第一场。 | No meetings yet. Choose Start recording to write the first one. | — |  |
| 1064 | 需要先连接回答服务（开始前检查里可以连接）。 | Connect an answer service first (the before-you-start check can do it) | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:316 |
| 1065 | 鼠标移到提词岛上，可以暂停、结束或立即回答。 | Hover the cue island to pause, end or answer now. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1066 |  保存受阻 · 有 3 项待保存 · 重试 |  Saving is blocked · 3 pending · Retry | — |  |
| 1067 | Word 文档 (*.docx)\|*.docx | Word document (*.docx)\|*.docx | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:169 |
| 1068 | 修改保存在独立用户版本里；原话依据只用于核对。 | Edits are kept in a separate user version; the original wording is only used for checking. | — | Windows/src/NextThought.Desktop/Views/NotesEditorWindow.axaml |
| 1069 | 浮在屏幕上的大字提词窗（Ctrl+Alt+T） | Large teleprompter over the screen (Ctrl+Alt+T) | — |  |
| 1070 | 还没有可用的资料：先导入资料，再整理讨论重点。 | No usable materials yet: import materials first, then prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:78 |
| 1071 | 这次没有新的经验和问题：之前整理过同一份纪要。 | No new lessons or questions: these notes were already organized. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:340 |
| 1072 | %lld 条建议待确认，不会作为已确认内容带入。 | %lld suggestions awaiting review; not brought in as confirmed content | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:512 |
| 1073 | 会议结束后，转写、纪要和我的笔记都保存在记录里。 | After the meeting the transcript, notes and highlights stay under Records. | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1074 | 允许联网核实（需 TAVILY_API_KEY） | Allow web verification (needs TAVILY_API_KEY) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1075 | 如果时间允许，我想再补充一下数据和风险这两部分。 | If time allows, I would like to add the data and risk parts. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:17 |
| 1076 | 已取消「这是我」，这场会议里不会再识别为该声音。 | “This is me” cancelled; this voice is no longer recognized as you in this meeting. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:138 |
| 1077 | 已整理重复项（%lld 条 → %lld 条）。 | Merged duplicates (%lld entries → %lld entries). | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:356 |
| 1078 | 已跳过 %lld 行损坏记录，其余内容仍可查看。 | Skipped %lld damaged lines; the rest is still readable. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:480 |
| 1079 | 手机伴侣已开启；手机扫码即可查看当前问题与回答。 | Phone companion is on; scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 1080 | 点「开始记录」就能开会；也可以先到准备页补资料。 | Choose Start recording to begin; add materials on the prepare page first if you like. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:35 |
| 1081 | 第一次开始记录时才会询问，用于识别你自己说的话。 | Asked the first time recording starts, to recognize your own speech | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:308 |
| 1082 | 联网未配置：先在设置里保存 Tavily 密钥。 | Web search is not configured: save a Tavily key in settings. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:72 |
| 1083 | 记下复核结论：哪些页需要人工核对、是否补齐原件。 | Record the review result: which pages need a human check and whether the original is missing. | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 1084 | 请先选择一个局域网地址，或在设置里填写中转地址。 | Choose a LAN address, or set a relay URL in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:137 |
| 1085 | 转写会显示在这里。也可以导入录音 WAV 验证。 | The transcript appears here. You can also import a WAV recording to try it. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1086 | 这个系列还没有自己的术语；全局条目仍会叠加生效。 | This series has no terms of its own yet; global entries still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:89 |
| 1087 | 这是这个系列的下一场：会自动带上它的资料与经验。 | This is the next meeting of this series: its materials and lessons are carried in automatically. | — |  |
| 1088 | %@ · %@ · %lld 段 · 解析器 %@ | %@ · %@ · %lld sections · parser %@ | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:95 |
| 1089 | NextThought 备份\|*.ntbackup | NextThought backup\|*.ntbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:206 |
| 1090 | X-ASR 中英模型 · 在本机离线识别，已就绪。 | X-ASR zh/en model · recognized offline on this PC, ready | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:336 |
| 1091 | 先准备声纹组件（约 26 MB），再录入我的声音。 | Prepare the speaker component (about 26 MB), then enroll my voice. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:107 |
| 1092 | 勾选的技能只加入这个系列的回答；全局技能仍然生效。 | Checked skills apply to this series only; global skills still apply. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1093 | 双屏演讲：%@；Ctrl+Alt+N 唤回主窗口。 | Presenter mode: %@; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:707 |
| 1094 | 双屏演讲：主窗口已由你叫回，本场会议不再自动隐藏。 | Two-screen presenting: you brought the main window back, so it will not be hidden automatically for the rest of this meeting. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:192 |
| 1095 | 左侧灰色为对方（系统声音），右侧彩色为我（麦克风） | Grey on the left is the other side (system audio), colour on the right is me (microphone) | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1096 | 已删除 %lld 场会议；已确认的系列内容已保留。 | %lld meetings deleted; accepted series content kept. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:59 |
| 1097 | 所选系统输出设备混音 + 麦克风 · 在设置中调整 | Selected system output mix + microphone · adjust in settings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:76 |
| 1098 | 正在按《%@》整理…（第 %lld/%lld 段） | Organizing with “%@”… (part %lld of %lld) | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:221 |
| 1099 | 此数据目录已有 NextThought 窗口运行。 | A NextThought window is already using this data folder. | — | Windows/src/NextThought.Desktop/App.axaml.cs:51 |
| 1100 | 没有匹配的会议。换个关键词，或把筛选切回「全部」。 | No matching meetings. Try another keyword or switch the filter back to All. | — |  |
| 1101 | 结束会议后，按转写整理总结、决定、待办和未决问题。 | After a meeting, organize the transcript into a summary, decisions, actions and open questions. | — | Windows/src/NextThought.Desktop/AppState.cs:288 |
| 1102 | 语义检索模型已准备，勾选语义检索并保存设置后使用。 | Semantic model ready; enable semantic retrieval and save settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:98 |
| 1103 | 语音已就绪。可以开始记录，或先导入 WAV 验证。 | Speech is ready. Start recording, or import a WAV to try it. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:64 |
| 1104 | 还没有系列。上面的输入框新建一个，再把会议放进来。 | No series yet. Create one above, then add meetings to it. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1105 | 随手记；保存到 user-content.json | Jot a note; saved to user-content.json | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1106 | 鼠标穿透中 · 快捷键 Ctrl+Alt+T 切回 | Click-through on · Ctrl+Alt+T restores clicks | — |  |
| 1107 | %lld 秒后自动结束；说话或点「继续记录」可取消。 | %lld seconds until auto-end; speaking or Keep recording cancels it. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:129 |
| 1108 | 之前有效回答仍可读。取消后到达的旧请求结果会被丢弃。 | Earlier answers stay readable. Results from cancelled requests are discarded. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1109 | 先到设置准备语音组件；未连接回答服务时也可以只记录。 | Prepare the speech components in settings first; recording alone works without an answer service. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:65 |
| 1110 | 删除选中的 %lld 场会议？此操作会移除归档目录。 | Delete %lld selected meetings? Their archive folders will be removed. | — |  |
| 1111 | 快捷键行要写成 动作=按键，例如 record=R。 | Write one action=key per line, for example record=R. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:192 |
| 1112 | 时长按每场从开始记录到结束计算；暂停的时间也算在内。 | Recording time runs from the start to the end of each meeting; pauses count too. | 时长按每场从开始记录到结束计算；暂停的时间也算在内。 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 1113 | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | This meeting uses %lld ready materials; readable text is enough to start. | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:87 |
| 1114 | 检测到屏幕共享，提示岛保持收回；共享结束后自动恢复。 | Screen sharing detected; the cue island stays retracted and returns when sharing ends. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:531 |
| 1115 | 窗口“%@”已关闭或无法捕获，请刷新窗口列表后重试。 | The window “%@” was closed or cannot be captured; refresh the window list and try again. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:251 |
| 1116 | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | The questions asked, your answers and what to prepare afterwards stay here. | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1117 | 设置已保存，密钥已用当前 Windows 用户加密。 | Settings saved; the key is encrypted for the current Windows user. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:60 |
| 1118 | 这是「术语和人名」里记下的听错写法，搜索时一并找出来 | A misspelling saved under Terms and names; search finds it too | — | Windows/src/NextThought.Desktop/GlossaryRows.cs:12 |
| 1119 | 需要最新信息时联网核实回答；不联网时只用资料和转写。 | Look the answer up on the web when up-to-date information is needed; offline uses only materials and the transcript. | — |  |
| 1120 | ；%lld 个重复项保留现有版本。导入前备份：%@。 | ; %lld duplicates kept the existing versions. Pre-import backup: %@. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:232 |
| 1121 | 合成版本差异样例；实际过期标记由归档投影版本比较产生。 | Synthetic revision-diff sample; real stale markers come from archive projection comparison. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1122 | 听到对方提问，参考回答出现在摄像头下方的提词岛和右侧。 | When the other side asks, the suggested answer appears in the cue island below the camera and on the right | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:315 |
| 1123 | 命名后应用到当前转写；也可以把某一声道标成「这是我」。 | Names apply to the current transcript; you can also mark a channel as “Me”. | — | Windows/src/NextThought.Desktop/Views/SpeakerEditorWindow.axaml |
| 1124 | 密钥读取失败，已保留原密钥；如需更换请直接输入新密钥。 | Reading the key failed; the existing key was kept. Enter a new key to replace it. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:778 |
| 1125 | 已整理成%@；%lld 条找不到原话依据的内容已丢弃。 | Organized into %@; dropped %lld items with no transcript support. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:480 |
| 1126 | 提词浮窗已开启鼠标穿透，Ctrl+Alt+J 可恢复。 | Teleprompter click-through is on; Ctrl+Alt+J restores clicks. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:493 |
| 1127 | 还没有条目。点「新增」，或用「批量粘贴」一次加入多条。 | No entries yet. Choose New, or use Bulk paste to add several at once. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:56 |
| 1128 | 这个问题我的理解是，先确认需求，再决定用哪种方案去做。 | My understanding is to confirm the need first, then decide on an approach. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:15 |
| 1129 | 选择系统输出混音或麦克风，并在设置里准备本机语音模型。 | Choose the system output mix or the microphone, and prepare the on-device speech model in settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:33 |
| 1130 | 音频只在本机识别，不保存录音；文字与回答只保存在本机。 | Audio is recognized on this device only and no recording is kept; text and answers stay on this device. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1131 | 会前准备：这个系列已有 %lld 份资料，均在本机解析。 | Preparation: this series already has %lld materials, all parsed on this device. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:313 |
| 1132 | 兼容 Chat Completions 的 API 地址 | Chat Completions-compatible API URL | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1133 | 删除当前筛选的 %lld 场会议？此操作会移除归档目录。 | Delete the %lld filtered meetings? Their archive folders will be removed. | — |  |
| 1134 | 回答过的问题会按时间列在这里，点一下就能回看当时的回答。 | Answered questions are listed here by time; choose one to see the answer as it was. | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 1135 | 声音在这台 Windows 上识别。应用不保存原始录音。 | Speech is recognized on this Windows PC. The app does not save the original audio | 声音在这台 Mac 上识别。应用不保存原始录音。 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:363 |
| 1136 | 归档有缺失或读取错误（跳过 %lld 行），导出已暂停。 | The archive has missing or unreadable rows (%lld lines skipped); the export is paused. | — | Windows/src/NextThought.Desktop/AppState.SessionExport.cs:159 |
| 1137 | 把双方说的话写成文字。回答、纪要和搜索都靠它，一直开着。 | Writes both sides' speech as text. Answers, notes and search depend on it; always on. | — | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 1138 | 纪要整理已取消；原始转写和已完成片段已保留，可继续整理。 | Note generation cancelled; the transcript and finished parts were kept. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:558 |
| 1139 | 这些字段会进入这个系列的准备页和回答提示；留空表示不用。 | These fields feed this series' prepare page and answer prompt; leave them empty to skip. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1140 | 删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。 | Delete series “%@”? The series and its meeting records will be permanently deleted and cannot be recovered. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:212 |
| 1141 | 对方 · 系统声音 %.0f · 我 · 麦克风 %.0f | Them · system audio %.0f · Me · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:147 |
| 1142 | 框选屏幕区域，存入资料库并 OCR（Ctrl+Alt+S） | Select a screen area, save it to the library and OCR it (Ctrl+Alt+S) | — |  |
| 1143 | 没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。 | No window to capture was found. Open the window first, then choose Refresh. | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:27 |
| 1144 | 自检：安装包已下载并通过 SHA-256，启动命令已记录。 | Self-test: installer downloaded and SHA-256 verified; launch command recorded. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:286 |
| 1145 | 还没有配置回答服务；整理讨论重点需要它，记录和转写不需要。 | No answer service is configured yet; preparing discussion points needs one, recording and transcription do not. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:42 |
| 1146 | Tavily 密钥已保存（当前 Windows 用户加密）。 | Tavily key saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:66 |
| 1147 | 今天上午我们先把上周的进展过一遍，然后讨论接下来两周的安排。 | This morning let us review last week's progress, then plan the next two weeks. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:14 |
| 1148 | 对着麦克风读一句话，观察下方电平。开始记录后这里会实时变化。 | Read a sentence into the microphone and watch the level below; it changes once recording starts. | — | Windows/src/NextThought.Desktop/Views/OnboardingVoiceWindow.axaml |
| 1149 | 已处理 %lld 场中断会议；可在历史中继续查看或整理纪要。 | Recovered %lld interrupted meetings; view or organize them in history. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:121 |
| 1150 | 已导入 %lld 场会议、%lld 份资料、%lld 个系列 | Imported %lld meetings, %lld materials, %lld series | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:230 |
| 1151 | 手机伴侣已开启（中转通道）；手机扫码即可查看当前问题与回答。 | Phone companion is on (relay); scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 1152 | 补译已结束；有译文的句子已分别保存。未成功的句子可再次补译。 | Translation finished; translated sentences were saved. Retry the ones that failed. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:457 |
| 1153 | 识别为问题的句子会标出；把鼠标移到任意一句上可以「回答这句」 | Sentences recognized as questions are marked; rest the pointer on any line to “Answer this” | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1154 | 还没有记录。从这个系列开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:236 |
| 1155 | 这次回复不是可用的 JSON，可以重试；原来的纪要保持不变。 | The reply was not usable JSON; retry. The previous notes are unchanged. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:234 |
| 1156 | 中转已连接 · 端到端加密。配对链接走中转，不必在同一局域网。 | Relay connected · end-to-end encrypted. Pairing links use the relay, so no shared LAN is needed. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:572 |
| 1157 | 按声音分出每个人，可以起名字；纪要里按人来写。认错了随时能改。 | Tells people apart by voice, lets you name them and writes notes per person. Fix a mistake anytime | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:326 |
| 1158 | 最近结果：先看命中率和 P95 延迟，再比较灰度前后的错误率。 | Latest results: check hit rate and P95 latency, then compare error rates before and after the rollout. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1159 | 还没有可用的语音识别；可以先只记录声音，或到设置准备语音组件。 | No speech recognition is ready yet; record the sound only for now, or set up the speech components. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:189 |
| 1160 | 还没有记录。从这个资料夹开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — |  |
| 1161 | 这场只记录对方的声音；想连自己的话一起记，可以重新打开麦克风。 | This meeting records the other side only; reopen the microphone to include your own voice. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:320 |
| 1162 | 这条建议对不上了：原话或已有内容在复核之后改过，刷新后再确认。 | This suggestion no longer matches: the original or existing content changed after review. Refresh and confirm again. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:105 |
| 1163 | NextThought 把会议讨论留在本机，变成可以带走的结论。 | NextThought keeps meeting discussions on this device and turns them into conclusions you can take away. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1164 | 「%@」有 %lld 条经验和问题待确认，确认后才会进入知识库。 | “%@” has %lld lessons and questions to confirm; nothing enters the knowledge base until you confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:347 |
| 1165 | 服务地址、模型、推理强度、联网搜索都可以在设置 → 回答 里改。 | Service address, model, reasoning effort and web search are all changed in Settings → Answers. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1166 | 系列术语只叠加在这个系列的回答、纪要与查找里；全局术语仍然生效。 | Series terms only layer onto this series' answers, notes and search; global terms still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:88 |
| 1167 | 还没有术语。每行一个：正确写法 \| 识别错的写法 \| 另一种错法 | No glossary entries. One per line: correct form \| misheard form \| another mishearing | — |  |
| 1168 | 仅保留会议内编号，不保存声纹或原始音频。短句和不确定结果保持未知。 | Only in-meeting numbers are kept; voiceprints and raw audio are not stored. Short or uncertain results stay unknown. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1169 | 把声音转成文字。需要下载本机识别模型，约 270 MB，只需一次。 | Turns speech into text. Downloads the on-device recognition model once (about 270 MB) | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:337 |
| 1170 | 整理出的%@缺少可核对的转写依据，已丢弃；可重试，或改用文字纪要。 | The %@ lacked verifiable transcript support and was dropped; retry, or use text notes instead. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:415 |
| 1171 | 文字颜色 · INK / SECONDARY / TERTIARY | TEXT COLOURS · INK / SECONDARY / TERTIARY | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1172 | 未允许 · 线下记录需要麦克风，请在 Windows 设置里打开。 | Not allowed · in-room recording needs the microphone; allow it in Windows Settings | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:306 |
| 1173 | 转写保存在本机。使用远程回答服务时，问题和相关转写会发送到该服务。 | Transcripts stay on this device. With a remote answer service, the question and relevant transcript are sent to that service. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1174 | 这条建议对不上了：题库里的回答在整理之后改过，重新整理一次再确认。 | This suggestion no longer matches: the question-bank answer changed after organizing. Organize again, then confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:357 |
| 1175 | 题库里已有相似问题时，「替换」会用新回答覆盖旧的，旧回答留在历史里 | If a similar question exists, Replace overwrites its answer; the old one stays in history | — |  |
| 1176 | 个人笔记保存在 user-content.json，不覆盖生成纪要。 | Personal notes are saved to user-content.json and never overwrite generated notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1177 | 按这场会议的转写整理总结、决定、待办和未决问题；原始文字不会被改写。 | Summarize decisions, actions and open questions from the transcript; the original text is not rewritten. | — |  |
| 1178 | 文字纪要由模板整理；鱼骨图、大纲与观点对照是同一场会议的另一种看法。 | Text notes come from a template; fishbone, outline and viewpoint views are other ways to read the same meeting. | — | Windows/src/NextThought.Desktop/AppState.cs:356 |
| 1179 | 正在安装更新，NextThought 会退出并在安装完成后重新打开。 | Installing the update; NextThought will exit and reopen when it finishes. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:310 |
| 1180 | 添加正确写法与常见误听；识别时作为热词，转写中的错写会改回正确写法。 | Add correct forms and common mishearings; correct forms become hotwords and misheard forms are rewritten. | — |  |
| 1181 | 题库还空着。面试、汇报这类模板的「逐题回放」章节会在确认后进入题库。 | The question bank is empty. Question-by-question sections from interview or talk templates enter the bank after confirmation. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1182 | %lld 条术语 · 识别时作为热词，转写里出现的错写会被改回正确写法 | %lld terms · used as recognition hotwords; misheard forms are rewritten | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:95 |
| 1183 | ink：正文和标题 Use for body and headings | ink: body and headings | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1184 | 只更正名字与术语的识别错误，改动写入 corrections.json | Only fixes misrecognized names and terms; changes are written to corrections.json | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1185 | 安装包已在后台下载并通过 SHA-256 校验；点「重启以更新」安装。 | The installer was downloaded and verified with SHA-256; choose Restart to update to install. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:216 |
| 1186 | 快捷键必须包含 Ctrl、Alt 或 Shift，不能只使用普通按键。 | A shortcut must include Ctrl, Alt or Shift; a plain key is not enough. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:197 |
| 1187 | 所有窗口已收起，会议继续记录。Ctrl+Alt+\ 或托盘图标可恢复。 | All windows hidden; the meeting keeps recording. Ctrl+Alt+\ or the tray icon restores them. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:155 |
| 1188 | 把同一门课、同一个项目、同一类面试的会议放在一个系列里，经验会攒下来。 | Keep meetings from one course, project or interview loop in a series; lessons accumulate. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:31 |
| 1189 | 按下带 Ctrl/Alt/Shift/Win 的组合键；Esc 取消。 | Press a combination with Ctrl/Alt/Shift/Win; Esc cancels. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:435 |
| 1190 | 每一场会议都存在本机。搜索标题、日期或模式，看不清的细节可以回到原话。 | Every meeting stays on this device. Search by title, date or mode, and return to the exact words when details are unclear. | — |  |
| 1191 | 没有找到可用的局域网 IPv4 地址；请先连接 Wi-Fi 或以太网。 | No usable LAN IPv4 address; connect Wi-Fi or Ethernet first. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:116 |
| 1192 | 生成结果保持不变；这里的修改写入 user-content.json。 | Generated content stays unchanged; edits here are written to user-content.json. | — |  |
| 1193 | 说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。 | After a few sentences this will show the meeting so far: what is being discussed, what was decided, who is doing what. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:108 |
| 1194 | 还没有高亮。在转写里点「高亮」记下一句，会把原话时间一起存进个人笔记。 | No highlights yet. Choose Highlight in the transcript to keep that line, with its original timestamp, in your personal notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1195 | 这场会只保存转写与纪要，不自动生成回答。需要回答时可随时在输入框提问。 | This meeting only saves the transcript and notes; answers are not generated automatically. Ask in the input box whenever you need one. | — |  |
| 1196 | 选中文字荧光为合成标记；原生 textSelection 高亮未伪造。 | The text fluorescence is a synthetic marker; native textSelection highlighting is not faked. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1197 | 高亮与这场的原话关联；保存后写入 user-content.json。 | Highlights link to this meeting's original lines; saving writes them to user-content.json. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1198 | NextThought 备份\|*.ntbackup;*.ihbackup | NextThought backup\|*.ntbackup;*.ihbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:219 |
| 1199 | 备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。 | Backup exported: contains meetings and materials; excludes API keys, speech models and device settings. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:211 |
| 1200 | 将调用回答服务把转写整理成「%@」；每条都带原话时间，找不到依据的不写。 | The answer service will organize the transcript into “%@”; every entry carries its original time and unsupported items are dropped. | — | Windows/src/NextThought.Desktop/AppState.cs:357 |
| 1201 | 还没有配置回答服务：到 设置 → 回答 填写 API Key 后再检查。 | No answer service is configured yet: fill in an API key under Settings → Answers, then check again. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:103 |
| 1202 | X-ASR 流式识别加整句精修，CPU 离线运行；原始音频只在内存中经过。 | X-ASR streaming plus sentence refinement runs offline on CPU; raw audio only passes through memory. | — |  |
| 1203 | 会中的回答和会后的纪要都由它来写。选一个服务商，粘贴 Key，点一下检查。 | It writes the in-meeting answers and the meeting notes. Pick a provider, paste a key and check. | — |  |
| 1204 | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Answers automatically · concise answers · notes: needs, concerns and replies, what we promised, next steps | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Windows/src/NextThought.Desktop/MeetingDraft.cs:64 |
| 1205 | 会话 %lld · 问题 %lld · 经验 %lld · 题库 %lld | sessions %lld · questions %lld · lessons %lld · bank %lld | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:119 |
| 1206 | 共 %lld 场 · %@ · 提问 %lld 次 · 回答 %lld 次 | %lld meetings · %@ · %lld questions · %lld answers | — |  |
| 1207 | 决定与行动先留在这里复核；负责人和期限可在这里确认，确认后才进入系列记录。 | Decisions and actions wait here for review; owner and due date can be confirmed here before they enter the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:81 |
| 1208 | 这场会议还不属于任何系列。到「系列」页把它加进去，之后整理纪要就会攒经验。 | This meeting is not in a series. Add it on the Series page to accumulate lessons when notes are generated. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:334 |
| 1209 | 这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。 | Live notes are off for this meeting. Full notes are still written at the end; to see key points as you go, turn it on in the top right. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:109 |
| 1210 | 首页的场景会决定默认的会中模式、回答风格、纪要模板与提词方式。随时可以改。 | The Home scene sets the default in-meeting mode, answer style, note template and cue style. You can change it anytime. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:32 |
| 1211 | 听到对方说英文时自动出现：每句话下面和提词岛上给出中文。在本机翻译，不联网。 | Appears automatically when English is heard: Chinese under each sentence and in the cue island. Translated on this PC, offline | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:323 |
| 1212 | 未允许 · 请在 Windows 设置里打开麦克风权限，或选“只记录对方”。 | Not allowed · allow the microphone in Windows Settings, or choose Record only the other side | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:305 |
| 1213 | 选云端后，导入 WAV 走云端识别；实时记录仍用本机。音频会发送到所选服务。 | With cloud selected, WAV import uses the cloud; live recording stays on-device. Audio is sent to the chosen service. | — |  |
| 1214 | 面试进行到系统设计：先聊了订单缓存层的项目，现在在追问数据库和缓存的一致性。 | The interview reached system design: we covered the order-cache project and are now on database/cache consistency. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:289 |
| 1215 | secondary：次要说明 Use for secondary labels | secondary: secondary labels | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1216 | 已在 %@:%lld 监听 · 配对链接相当于临时遥控密钥，只在可信网络使用。 | Listening on %@:%lld · the pairing link is a temporary remote key, so use it only on trusted networks. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:146 |
| 1217 | 边开边整理议题、决定和待办，大约每 45 秒更新一次；结束后仍会整理完整纪要。 | Organizes topics, decisions and to-dos while the meeting runs, about every 45 seconds; the full notes are still organized afterwards | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:319 |
| 1218 | ## 客户会⏎### 决定⏎只写已经拍板的事。⏎### 待办⏎写清负责人和日期。 | ## Client meeting
### Decisions
Only write what was decided.
### Actions
State the owner and the date. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1219 | 切换后重启生效；当前覆盖主导航、设置分节与常用按钮，动态状态和会议内容保持原文。 | Takes effect after a restart; covers navigation, settings sections and common buttons. Status text and meeting content stay in their original language. | — |  |
| 1220 | 同一门课、同一个项目、同一类面试放在一个系列里；每场会议的结论与经验会攒在一起。 | Keep one course, project or interview loop in a series; conclusions and lessons accumulate across meetings. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1221 | 开会时把提词岛留在摄像头这一屏，幻灯片屏不出现任何 NextThought 窗口 | Keep the cue island on the camera display; no NextThought window appears on the slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1222 | 接入兼容 Chat Completions 的回答服务；只记录与转写时可以不填。 | Connect a Chat Completions-compatible answer service; leave it empty to just record and transcribe. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:34 |
| 1223 | 本机模型需要下载并占内存；云端识别需要填写 API Key 并确认音频上传说明。 | The on-device model needs a download and memory; cloud recognition needs an API key and confirmation of the audio upload notice. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1224 | 没有待确认的知识。按模板整理一份纪要后，模板里标了「带出经验」的章节会排到这里。 | Nothing to confirm. After organizing notes, sections marked to carry lessons appear here. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1225 | 还没有配置回答服务：到 设置 → 回答 填好 API Key 后可整理讨论重点。 | No answer service is configured yet: fill in an API key under Settings → Answers to prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:71 |
| 1226 | 题库里已有相似问题时，「采用建议并保留旧版」会用新回答覆盖旧的，旧回答留在历史里 | When the question bank already has a similar question, “Apply and keep the old version” replaces it with the new answer; the old answer stays in history. | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:254 |
| 1227 | 首次需下载流式模型约 134 MB；整句精修另约 136 MB，下载后离线运行。 | The streaming model is about 134 MB; sentence refinement adds about 136 MB. Both run offline after download. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1228 | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Answers automatically · concise answers · notes: review each question (saved to the question bank), what went poorly, next round | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Windows/src/NextThought.Desktop/MeetingDraft.cs:60 |
| 1229 | 还没有资料。导入 PDF、Word、PPT、Excel、Markdown 或图片。 | No materials yet. Import PDF, Word, PPT, Excel, Markdown or images. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:41 |
| 1230 | 系列按修改时间合并；应用前会先创建本机恢复点。模板和个人笔记属于数据，始终参与恢复。 | Series are merged by modification time. A local restore point is created before anything is applied. Templates and personal notes are data and are always restored. | — |  |
| 1231 | 原生控件样板 · 所有文本与状态均为合成演示，不访问麦克风、ASR、模型、网络或归档。 | Native control samples · all text and states are synthetic; no microphone, ASR, model, network or archive access. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1232 | 开会、听讲、面谈。NextThought 在本机把声音变成文字，再变成可以带走的结论。 | Meetings, lectures, interviews. NextThought turns speech into text on this device, then into conclusions you can take away. | — |  |
| 1233 | 把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「添加」。 | Drag PDF, Word, PPT, Excel, Markdown or images here, or click Add | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:533 |
| 1234 | 还没有经验。整理纪要时会从「下次改进」这类章节提炼候选，确认后才会记下来；也可以手写一条。 | No lessons yet. When notes are organized, sections such as Next time produce candidates; nothing is saved until you confirm, and you can also write one. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1235 | 已捕获 %lld×%lld 像素（%lld,%lld）。拖动选择区域，然后点「使用此区域」。 | Captured %lld×%lld pixels (%lld,%lld). Drag to select an area, then choose Use this area. | — |  |
| 1236 | 重新整理这份纪要？这场会议的纪要你改过。重新整理会生成新的 AI 版本，你改过的版本会保留。 | Regenerate these notes? You edited this meeting's notes. Regenerating creates a new AI version and keeps your edited version. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml.cs:160 |
| 1237 | tertiary：占位和装饰 Placeholders and decoration only | tertiary: placeholders and decoration only | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1238 | 保存设置后立即生效；当前覆盖主导航、设置分节、常用按钮与说明文案，动态状态和会议内容保持原文。 | Takes effect immediately after saving; covers navigation, settings sections, common buttons and help text. Status and meeting content stay in their original language. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1239 | 密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。 | The key is stored only for the current Windows user; TAVILY_API_KEY still works too. | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:29 |
| 1240 | 导入 PPT、Keynote 或 PDF 后，放映时认出正在讲第几页，给出这一页的要点与提问。 | Import PPT, Keynote or PDF and the app recognizes the current slide while presenting, offering key points and questions for it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1241 | 截图会裁切到选中范围，然后存入资料库并由系统 OCR 读取文字。请把题目、代码或公式完整框入。 | The screenshot is cropped to the selection, saved to the library and read with system OCR. Frame the question, code or formula completely. | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 1242 | 把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「导入资料」。 | Drop PDF, Word, PPT, Excel, Markdown or images here, or choose Import materials. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:234 |
| 1243 | 还没有生成这种纪要。点上方「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This kind of note has not been generated yet. Choose Generate this note above to turn the transcript into it with the current answer service; every line keeps its original timestamp. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1244 | Windows 没有系统翻译框架：开启后完整句会发送到所选回答服务，与 Mac 的本机翻译不同。 | Windows has no system translation framework: complete sentences are sent to the chosen answer service, unlike the on-device translation on Mac. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1245 | 还没有生成这种纪要。点右上角「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This note has not been generated. Choose Generate this note to organize the transcript with the current answer service; every entry carries its original time. | — |  |
| 1246 | 问题涉及最新价格、版本、新闻等时检索 Tavily，并把带链接的结果交给回答；没有密钥时保持离线。 | Searches Tavily for the latest prices, versions or news and passes linked results to the answer; stays offline without a key. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1247 | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Answers automatically and follows the script · concise answers · notes: audience questions reviewed one by one (saved to the question bank), what to change, promised follow-ups | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Windows/src/NextThought.Desktop/MeetingDraft.cs:61 |
| 1248 | 安装包签名有效，但签名者不在允许列表（NEXTTHOUGHT_SIGNER_THUMBPRINT）。 | The installer signature is valid but the signer is not on the allow list (NEXTTHOUGHT_SIGNER_THUMBPRINT). | — | Windows/src/NextThought.Desktop/AppState.Update.cs:272 |
| 1249 | 默认关闭；手机与电脑需在同一可信网络。只发送当前问题、回答与状态，不发送资料、完整转写、音频或密钥。 | Off by default; phone and PC must share a trusted network. Only the current question, answer and status are sent — never materials, full transcripts, audio or keys. | — | Windows/src/NextThought.Desktop/AppState.cs:211 |
| 1250 | 选云端后，实时记录与导入 WAV 都走云端识别；音频会发送到所选服务。只选本机时，录音与识别都在本机。 | With cloud selected, both live recording and WAV import use cloud recognition and audio is sent to the chosen service; with local only, recording and recognition stay on this device. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1251 | 默认关闭；开启后手机与电脑需在同一可信网络。首次监听时 Windows 防火墙会询问，请只允许专用网络。 | Off by default; once on, phone and PC must share a trusted network. Windows Firewall asks on the first listen — allow private networks only. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:117 |
| 1252 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：总结、要点、决定、待办 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: summary, key points, decisions, todos | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:59 |
| 1253 | 还没有资料。点「导入资料」把 PDF、Word、PPT、Excel、Markdown 或图片加进这个系列。 | No materials yet. Choose Import materials to add PDF, Word, PPT, Excel, Markdown or images to this series. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1254 | 还没有资料；导入 PDF、Word、PPT、Excel、Markdown 或图片后，这里显示页数与段落数。 | No materials yet; once you import PDF, Word, PPT, Excel, Markdown or images, their page and paragraph counts appear here. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:32 |
| 1255 | .ntbackup 包含会议、资料与设置；不包含 API Key、语音模型与设备设置。卸载不会删除数据目录。 | .ntbackup contains meetings, materials and settings; it excludes API keys, speech models and device settings. Uninstalling does not delete the data folder. | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml |
| 1256 | 每一级字号一行，中英文混排；文字颜色只用 Ink 的 ink / secondary / tertiary。 | One line per size, Chinese and English mixed; text colours use only Ink ink / secondary / tertiary. | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 1257 | 每行一个：正确写法 \| 识别错的写法 \| 另一种错法。正确写法会作为识别热词；错写法出现在转写里时会被改回。 | One per line: correct form \| misheard form \| another misheard form. Correct forms become recognition hotwords; misheard forms are rewritten in the transcript. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1258 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：决定、待办、风险、下次会议 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: decisions, todos, risks, next meeting | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:63 |
| 1259 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：各方观点、结论与分歧、分工 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: each side's view, conclusions and disagreements, assignments | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:66 |
| 1260 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：要点、概念、例子、我的疑问 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: key points, concepts, examples, my questions | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:65 |
| 1261 | Kubernetes \| K8s \| 库伯奈提斯⏎同济大学 \| 同技大学⏎OpenSees \| open sees | Kubernetes \| K8s \| kubernets
Tongji University \| Tongji Uni
OpenSees \| open sees | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1262 | 导入 PDF、Word、PPT、Excel、Markdown 与图片；解析在本机完成，检索时只把相关摘录发给回答服务。 | Import PDF, Word, PPT, Excel, Markdown and images; parsing is local and only relevant excerpts are sent to the answer service. | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 1263 | NextThought 备份\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | NextThought backup\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:46 |
| 1264 | 「下载并安装」会下载安装包并校验发布里的 SHA-256，校验通过后静默安装并重新打开；发布没有校验文件时只提供下载页。 | Download and install fetches the installer and verifies its published SHA-256, then installs silently and reopens; without a checksum only the download page is offered. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1265 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：定下的方案、分工、我的任务、下次碰头 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: chosen plan, assignments, my tasks, next meetup | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:67 |
| 1266 | 手机与电脑连接同一可信网络后开启。手机扫码用浏览器打开，可查看当前问题、回答与状态，并触发回答、简短版、举例或停止生成。 | Turn on after the phone and PC join the same trusted network. Scan the code in a browser to see the current question, answer and status, and to trigger answer, shorter, example or stop. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1267 | 所有偏好保存在本机数据目录的 settings.json；API Key 用当前 Windows 用户加密，不会写进备份。 | Preferences live in settings.json in the local data folder; the API key is encrypted for the current Windows user and never written to backups. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1268 | 蓝牙备用通道暂不可用：手机端仍按 IH- 广播名过滤，Windows 无法自定义广播名，等手机端改成按服务标识扫描后再开放。 | The Bluetooth fallback is unavailable: the phone still filters by the IH- broadcast name, Windows cannot rename its broadcast, and it stays closed until the phone scans by service ID. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1269 | 填写后配对链接走 HTTPS/WSS 中转（端到端加密），不必在同一局域网；留空则只用局域网。改地址后重新开启伴侣并重新配对。 | When set, pairing uses an HTTPS/WSS relay (end-to-end encrypted) and no shared LAN is needed; leave empty for LAN only. Re-enable and re-pair after changing it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1270 | 局域网 HTTP 不加密；配对链接相当于临时遥控密钥，请仅在可信网络使用。不会发送完整转写、资料文件、音频或 API Key。 | LAN HTTP is unencrypted; the pairing link is a temporary remote key, so use it only on a trusted network. Full transcripts, material files, audio and API keys are never sent. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1271 | 选应用时仅采集该进程及其子进程的声音；选整个设备时采集该设备全部混音。需要 Windows 10 build 20348 以上。 | App capture records only that process and its children; device capture records the whole mix. Requires Windows 10 build 20348 or later. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1272 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：讨论的问题、没讨论透的地方、意见与要求、下一步 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: questions discussed, what stayed unresolved, comments and requests, next steps | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:62 |
| 1273 | 忘了结束的会议会在长时间没有识别到文字后自动收尾，避免把下一场会议的内容写进这场纪要。只统计识别到的文字，背景音乐不会让它一直开着。 | A meeting left running ends after a long stretch without recognized text, so the next meeting does not leak into these notes. Only recognized text counts; background music does not keep it open. | — |  |
| 1274 | 每个模板以 ## 模板名 开头，每个章节以 ### 章节标题 开头，下一行写要求。保存后会出现在纪要页的模板下拉框；原有内置模板不变。 | Each template starts with ## name; each section starts with ### title and the next line states the requirement. Saved templates appear in the Notes template picker; built-ins are unchanged. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1275 | Qwen3-Embedding-0.6B Q8 · CPU · 首次下载约 596 MB。索引仅在本机生成；模型缺失或失败时回退关键词。 | Qwen3-Embedding-0.6B Q8 · CPU · about 596 MB on first download. The index is local; missing or failed models fall back to keywords. | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 1276 | 每个技能以 ## 名称 开头，可写一行「摘要：…」，其余为 Markdown。启用后会加入回答提示；与真实性/安全约束冲突时以安全约束为准。 | Each skill starts with ## name, may include one Summary: line and is otherwise Markdown. Enabled skills join the answer prompt; truth and safety rules win on conflict. | — |  |
| 1277 | 读 4 句话，约 40 秒。只保存一串数字，不保存录音；只存在这台电脑上，不上传、不进备份，可随时删除。录入后，线下会议里你的发言会标为“我”。 | Read 4 sentences, about 40 seconds. Only a string of numbers is saved, never audio; it stays on this PC, is never uploaded or backed up, and can be deleted anytime. Afterwards your speech in in-person meetings is marked as “Me”. | — | Windows/src/NextThought.Desktop/Views/MyVoiceWindow.axaml |
| 1278 | Ctrl+L 聚焦输入 · Enter 发送 · Ctrl+Shift+Enter 强制回答 · Esc 取消当前回答 · ↑/PageUp 保持阅读位置 | Ctrl+L focuses input · Enter sends · Ctrl+Shift+Enter forces an answer · Esc cancels · ↑/PageUp keep your reading position | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1279 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。已支持按讲话标题和截图 OCR 跟页；持续屏幕观察尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Following slides by spoken title and screenshot OCR is supported; continuous screen watching is not ported yet. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1280 | 支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT 与图片；回答时只把与问题相关的片段交给模型，并标注来源。 | Supports PDF (includes scanned OCR), Word, PowerPoint, Excel, Markdown, TXT and images. Only excerpts relevant to the question are sent to the model while answering, with sources marked. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1281 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。幻灯片自动翻页识别（LiveInsights 页面匹配）尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Automatic slide-change detection (LiveInsights page matching) is not ported yet. | — |  |
| 1282 | 点按键位后直接按下组合键录入，Esc 取消；「清除」「默认」恢复该动作的 Mac 默认键位。必须包含 Ctrl/Alt/Shift/Win 之一，两个动作不能相同。 | Choose the key field and press the combination; Esc cancels. Clear and Default restore that action's Mac default key. It must include Ctrl/Alt/Shift/Win and no two actions may match. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1283 | 检查 GitHub 上的发布页：正式版本只看 vX.Y.Z，main 最新构建跟随 main 分支。发布带安装包与校验文件时可下载并校验后应用内安装，否则只打开下载页。 | Checks the GitHub release page: stable builds use vX.Y.Z, latest follows main. When a release has an installer and checksum, it downloads, verifies and installs; otherwise it opens the download page. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1284 | 技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在系列的“技能”页按系列勾选。 | A skill is a Markdown instruction describing the desired speaking style, answer method or subject knowledge. Skills opened to all series are used for every answer; other skills are chosen per series on the series' Skills tab. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1285 | 将永久删除这场会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖它的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes this meeting's transcript, audio, notes, index, questions, answers and notes. Accepted lessons, decisions and actions that only depend on it are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meeting's wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:129 |
| 1286 | 将永久删除所有归档会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖这些会议的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes the transcript, audio, notes, index, questions, answers and notes of every archived meeting. Accepted lessons, decisions and actions that only depend on them are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meetings' wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:156 |
| 1287 | 打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕在本机识别；相关文字与必要的当前画面会提供给回答引擎。 | With screen awareness on, the text on screen is recognised on this device every few seconds: it follows the slide you are on and offers talking points for that page. When a question or code appears on screen, it decides whether it relates to the conversation and includes it in answers. The screen is read on this device; the relevant text and, when needed, the current frame are provided to the answer engine. | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 1288 | 资料文件 (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | Material files (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:110 |
| 1289 | 每行「动作=按键」，保存后立即重新注册。可用 Ctrl/Alt/Shift/Win 组合，按键支持 A–Z、0–9、F1–F24、Left/Right/Up/Down/PageUp/PageDown/Space/Backslash。 | One action=key per line; saving re-registers immediately. Modifiers may include Ctrl/Alt/Shift/Win, and keys may be A–Z, 0–9, F1–F24, Left/Right/Up/Down/PageUp/PageDown/Space/Backslash. | — |  |
| 1290 | 系列按修改时间合并；应用前会先创建本机恢复点。模板和个人笔记属于数据，始终参与恢复。 | Series are merged by modification time. A local restore point is created before anything is applied. Templates and personal notes are data and are always restored. | — | Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml |

## 3. 英文模式逐页扫描

扫描对象：`--demo --screenshots=<dir>` 生成的英文模式页面文字清单（`text/*-en*.txt`，
包含未展开的 tooltip；文字清单由截图工具在渲染后遍历控件树得到，不依赖 OCR）。

- 页面数：**51**
- 含中文字符的行：**222**
- 其中用户数据/演示数据/系统设备名（允许）：**222**
- **残留（需要修）：0**
- **英文文字截断/溢出（需要修）：0**

### 逐页结论

| 页面 | 中文行 | 残留 | 截断 | 结论 |
|---|---|---|---|---|
| answers-en-dark.png | 1 | 0 | 0 | 通过 |
| answers-en-light.png | 1 | 0 | 0 | 通过 |
| corrected-transcript-en-dark.png | 10 | 0 | 0 | 通过 |
| corrected-transcript-en-light.png | 10 | 0 | 0 | 通过 |
| history-en-dark.png | 7 | 0 | 0 | 通过 |
| history-en-light.png | 7 | 0 | 0 | 通过 |
| home-en-dark.png | 5 | 0 | 0 | 通过 |
| home-en-light.png | 5 | 0 | 0 | 通过 |
| home-stats-en-dark.png | 5 | 0 | 0 | 通过 |
| home-stats-en-light.png | 5 | 0 | 0 | 通过 |
| island-autoend-en | 0 | 0 | 0 | 通过 |
| island-bilingual-en | 4 | 0 | 0 | 通过 |
| island-compact-en | 0 | 0 | 0 | 通过 |
| island-cue-en | 2 | 0 | 0 | 通过 |
| island-deck-en | 2 | 0 | 0 | 通过 |
| island-hearing-en | 1 | 0 | 0 | 通过 |
| island-launcher-en | 0 | 0 | 0 | 通过 |
| island-listening-en | 1 | 0 | 0 | 通过 |
| island-open-en | 2 | 0 | 0 | 通过 |
| island-pinned-en | 5 | 0 | 0 | 通过 |
| island-slide-en | 2 | 0 | 0 | 通过 |
| library-en-dark.png | 4 | 0 | 0 | 通过 |
| library-en-light.png | 4 | 0 | 0 | 通过 |
| live-en-dark.png | 8 | 0 | 0 | 通过 |
| live-en-light.png | 8 | 0 | 0 | 通过 |
| notes-en-dark.png | 10 | 0 | 0 | 通过 |
| notes-en-light.png | 10 | 0 | 0 | 通过 |
| personal-en-dark.png | 9 | 0 | 0 | 通过 |
| personal-en-light.png | 9 | 0 | 0 | 通过 |
| prepare-en-dark.png | 16 | 0 | 0 | 通过 |
| prepare-en-light.png | 16 | 0 | 0 | 通过 |
| quick-settings-en-dark | 0 | 0 | 0 | 通过 |
| quick-settings-en-light | 0 | 0 | 0 | 通过 |
| series-en-dark.png | 14 | 0 | 0 | 通过 |
| series-en-light.png | 14 | 0 | 0 | 通过 |
| settings-advanced-en-dark.png | 2 | 0 | 0 | 通过 |
| settings-advanced-en-light.png | 2 | 0 | 0 | 通过 |
| settings-answer-en-dark.png | 0 | 0 | 0 | 通过 |
| settings-answer-en-light.png | 0 | 0 | 0 | 通过 |
| settings-coach-en-dark.png | 0 | 0 | 0 | 通过 |
| settings-coach-en-light.png | 0 | 0 | 0 | 通过 |
| settings-en-dark.png | 2 | 0 | 0 | 通过 |
| settings-en-light.png | 2 | 0 | 0 | 通过 |
| settings-general-en-dark.png | 1 | 0 | 0 | 通过 |
| settings-general-en-light.png | 1 | 0 | 0 | 通过 |
| settings-notes-en-dark.png | 0 | 0 | 0 | 通过 |
| settings-notes-en-light.png | 0 | 0 | 0 | 通过 |
| settings-scene-en-dark.png | 0 | 0 | 0 | 通过 |
| settings-scene-en-light.png | 0 | 0 | 0 | 通过 |
| transcript-en-dark.png | 7 | 0 | 0 | 通过 |
| transcript-en-light.png | 8 | 0 | 0 | 通过 |

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

