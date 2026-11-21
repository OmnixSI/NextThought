# Windows 文案对照（G10）

这份表记录 Windows 版与 Mac 版**文案不同**的每一句。英文与中文都写；相同文案的句子
直接走 Mac 的 `Localizable.xcstrings`（由 `Windows/scripts/generate_ui_text.py` 生成
`UiTextMac.g.cs`，2,638 条），不在这张表里重复。Mac 是唯一标准：只有涉及系统名词的
句子可以改（钥匙串→凭据管理器、访达→文件资源管理器等），其余差异都是 Windows 移植过程
中的说明性文案（Windows 独有功能或 Windows 能力差异），在此逐句列出。

- 生成时间：2026-11-21
- 生成方式：`python Windows/scripts/localization_scan.py --write`
- Windows 独有/改写文案：**1233** 句（下表逐条）
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
| 31 | 复制 | Copy | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
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
| 50 | 改短 | Shorten | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 51 | 数据 | Data | 数据 |  |
| 52 | 文件 | Files | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml.cs:163 |
| 53 | 文本 | Text | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:66 |
| 54 | 文档 | Document | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:157 |
| 55 | 新建 | New | — |  |
| 56 | 更新 | Update | 更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 57 | 本月 | This month | 本月 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 58 | 查看 | Open | 查看 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 59 | 查证 | Verify | — |  |
| 60 | 标准 | Regular | 标准 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:520 |
| 61 | 检查 | Checks | — |  |
| 62 | 模式 | Mode | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:183 |
| 63 | 洞察 | Insights | — | Windows/src/NextThought.Desktop/Views/InsightsPanelWindow.axaml |
| 64 | 浅色 | Light | 浅色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:510 |
| 65 | 测试 | Test | — | Windows/src/NextThought.Desktop/AppState.Models.cs:124 |
| 66 | 深色 | Dark | 深色 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:511 |
| 67 | 添加 | Add | 添加 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 68 | 焦点 | Focus | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 69 | 确认 | Confirm | — | Windows/src/NextThought.Desktop/Views/ConfirmDialog.axaml |
| 70 | 示例 | Sample | 示例 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 71 | 移出 | Remove | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 72 | 穿透 | Click-through | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 73 | 类型 | Type | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 74 | 系列 | Series | 系列 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 75 | 累计 | All time | 累计 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 76 | 纪要 | Notes | 纪要 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 77 | 经验 | Lesson | 经验 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 78 | 编辑 | Edit | 编辑 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 79 | 聊天 | Chat | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:158 |
| 80 | 自动 | Automatic | 自动 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 81 | 英文 | English | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 82 | 补译 | Translate missing | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 83 | 讨论 | Discussion | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:41 |
| 84 | 议程 | Agenda | 议程 | Windows/src/NextThought.Desktop/MeetingDraft.cs:76 |
| 85 | 记下 | Save | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 86 | 记录 | Note | 记录 | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 87 | 设置 | Settings | 设置 | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 88 | 证据 | Evidence | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 89 | 误听 | Misheard | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
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
| 116 | 工作台 | Workbench | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 117 | 已删除 | Deleted | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:35 |
| 118 | 已就绪 | Ready | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:73 |
| 119 | 已收起 | Collapsed | — |  |
| 120 | 已连接 | Connected | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:204 |
| 121 | 幻灯片 | Slides | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:156 |
| 122 | 应用。 | Apply. | — |  |
| 123 | 待确认 | To confirm | 待确认 | Windows/src/NextThought.Desktop/AppState.Series.cs:294 |
| 124 | 忽略。 | Ignore. | — |  |
| 125 | 截止  | Due  | — | Windows/src/NextThought.Desktop/Views/ViewpointMatrixView.cs:181 |
| 126 | 提示： | Tip:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:96 |
| 127 | 提词器 | Teleprompter | 提词器 | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 128 | 提词岛 | Cue island | 提词岛 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:532 |
| 129 | 文件夹 | Folder | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:192 |
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
| 141 | 术语表 | Glossary | — |  |
| 142 | 条高亮 | highlights | — |  |
| 143 | 模板名 | Template name | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 144 | 知道了 | Got it | 知道了 | Windows/src/NextThought.Desktop/Views/HomeView.axaml |
| 145 | 自定义 | Custom | 自定义 | Windows/src/NextThought.Desktop/AppState.Home.cs:241 |
| 146 | 英译： | English:  | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:19 |
| 147 | 要做的 | To do | 要做的 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 148 | 说话人 | Speakers | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 149 | 资料库 | Library | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 150 | 资料： | Materials: | — |  |
| 151 | 面试官 | Interviewer | 面试官 | Windows/src/NextThought.Desktop/OnboardingSample.cs:41 |
| 152 | 鱼骨图 | Fishbone | 鱼骨图 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 153 | 麦克风 | Microphone | 麦克风 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 154 |  · 我 |  · Me | — |  |
| 155 | 0 分钟 | 0 min | — |  |
| 156 | ★ 高亮 | ★ Highlight | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 157 | 下一句： | Next: | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 158 | 不再提示 | Don't ask again | 不再提示 | Windows/src/NextThought.Desktop/Views/Home/OnboardingResumeRow.axaml |
| 159 | 会中笔记 | Meeting notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 160 | 会后纪要 | Meeting notes | — |  |
| 161 | 会议信息 | Meeting info | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 162 | 会议标题 | Meeting title | — | Windows/src/NextThought.Desktop/Views/RenameSessionDialog.axaml |
| 163 | 保存备注 | Save note | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 164 | 保存密钥 | Save key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 165 | 保存技能 | Save skills | — |  |
| 166 | 保存模板 | Save template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 167 | 保存笔记 | Save notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 168 | 保存结构 | Save structure | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 169 | 保存设置 | Save settings | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 170 | 停止记录 | Stop recording | — |  |
| 171 | 先说结论 | Lead with the conclusion | — |  |
| 172 | 先选场合 | Pick a scene | — |  |
| 173 | 全文命中 | Full-text hits | — |  |
| 174 | 全文搜索 | Full-text search | — |  |
| 175 | 全部会议 | All meetings | — |  |
| 176 | 全部文件 | All files | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml.cs:25 |
| 177 | 全部术语 | All terms | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 178 | 全部设置 | All settings | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 179 | 删除会议 | Delete meeting | — |  |
| 180 | 删除选中 | Delete selected | — |  |
| 181 | 刷新设备 | Refresh devices | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 182 | 区域截图 | Region capture | 区域截图 | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 183 | 历史回答 | Past answers | — |  |
| 184 | 参考回答 | Reference answer | — | Windows/src/NextThought.Desktop/Views/DetachedAnswerWindow.axaml |
| 185 | 双屏演讲 | Presenter mode | 双屏演讲 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 186 | 只看回答 | Answers only | — |  |
| 187 | 回答列表 | Answers | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 188 | 回答引擎 | Answer engine | 回答引擎 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:18 |
| 189 | 回答服务 | Answer service | 回答服务 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 190 | 回答模型 | Answer model | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:187 |
| 191 | 回答记录 | Answer history | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 192 | 回答风格 | Answer style | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 193 | 声音来源 | Audio sources | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 194 | 声音测试 | Sound test | — | Windows/src/NextThought.Desktop/Views/OnboardingVoiceWindow.axaml |
| 195 | 复制修复 | Copy fix | — |  |
| 196 | 复核备注 | Review note | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:27 |
| 197 | 字体样张 | Type specimen | — | Windows/src/NextThought.Desktop/SelfTest/TypographySampleWindow.axaml |
| 198 | 学术汇报 | Academic talk | — | Windows/src/NextThought.Desktop/AppState.Home.cs:64 |
| 199 | 定下来的 | Decided | 定下来的 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 200 | 实时转写 | Live transcript | 实时转写 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 201 | 导入备份 | Import backup | — |  |
| 202 | 导入资料 | Import materials | 导入资料 | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 203 | 导出备份 | Export backup | — | Windows/src/NextThought.Desktop/BackupFileRow.cs:26 |
| 204 | 导出术语 | Export terms | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:86 |
| 205 | 小组项目 | Group project | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:43 |
| 206 | 工作布置 | Work assignment | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:119 |
| 207 | 工作纪要 | Meeting notes | — |  |
| 208 | 已取消。 | Cancelled. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:359 |
| 209 | 已改为  | Changed to  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 210 | 已有纪要 | Notes exist | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:639 |
| 211 | 带入经验 | Bring lessons | — |  |
| 212 | 带入资料 | Bring materials | — |  |
| 213 | 带入题库 | Bring question bank | — |  |
| 214 | 开下一场 | Start next meeting | 开下一场 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 215 | 开始录制 | Start recording | — |  |
| 216 | 开始记录 | Start recording | 开始记录 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 217 | 引文支持 | Quote support | — | Windows/src/NextThought.Desktop/SessionEvidenceRow.cs:13 |
| 218 | 引用来源 | Cite source | — |  |
| 219 | 归档截图 | Archive screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 220 | 归档概览 | Archive overview | — |  |
| 221 | 归档系列 | Archive series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 222 | 当前场景 | Current scene | 当前场景 | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:22 |
| 223 | 录音来源 | Audio source | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:185 |
| 224 | 快速设置 | Quick settings | — |  |
| 225 | 恢复点击 | Restore clicks | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 226 | 截图提问 | Ask about a screenshot | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 227 | 手机伴侣 | Phone companion | 手机伴侣 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 228 | 打开系列 | Open series | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 229 | 打开纪要 | Open notes | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 230 | 打开设置 | Open settings | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 231 | 接口预设 | API preset | — |  |
| 232 | 控件样张 | Control samples | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 233 | 推理强度 | Reasoning effort | 推理强度 | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 234 | 提词宽度 | Cue width | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 235 | 搜索设置 | Search settings | 搜索设置 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 236 | 撤销移除 | Undo removal | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 237 | 收起示例 | Hide sample | 收起示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:67 |
| 238 | 放大字号 | Larger text | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 239 | 教练小结 | Coach summary | — |  |
| 240 | 数据目录 | Data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 241 | 文字纪要 | Text notes | 文字纪要 |  |
| 242 | 新增条目 | New entry | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:67 |
| 243 | 新建会议 | New meeting | — |  |
| 244 | 新建模板 | New template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 245 | 新建系列 | New series | 新建系列 | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 246 | 新说话人 | New speaker | 新说话人 | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:200 |
| 247 | 显示译文 | Show translation | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 248 | 替换回答 | Replace answer | — |  |
| 249 | 最近会议 | Recent meetings | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 250 | 未在采集 | Not capturing | — |  |
| 251 | 未知应用 | Unknown app | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:114 |
| 252 | 查看截图 | View screenshot | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 253 | 查看来源 | View sources | — |  |
| 254 | 标准纪要 | Standard notes | — |  |
| 255 | 校对转写 | Proofread transcript | 校对转写 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 256 | 检查更新 | Check for updates | 检查更新 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 257 | 正在检查 | Checking | — | Windows/src/NextThought.Desktop/AppState.Update.cs:82 |
| 258 | 正在记录 | Recording | — | Windows/src/NextThought.Desktop/AppState.Live.cs:861 |
| 259 | 添加章节 | Add section | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 260 | 清除查询 | Clear query | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 261 | 独立窗口 | Detached window | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 262 | 用了多少 | How much you've recorded | 用了多少 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 263 | 章节标题 | Section title | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 264 | 简体中文 | Simplified Chinese | 简体中文 | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 265 | 简洁回答 | Concise answer | — |  |
| 266 | 系列文档 | Series documents | — |  |
| 267 | 系列术语 | Series glossary | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 268 | 系列管理 | Manage series | — |  |
| 269 | 系列资料 | Series materials | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 270 | 系统识别 | System recognition | 系统识别 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:120 |
| 271 | 纪要编辑 | Edit notes | — |  |
| 272 | 组会讨论 | Group meeting | — | Windows/src/NextThought.Desktop/SceneSettingRow.cs:115 |
| 273 | 结束会议 | End meeting | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:54 |
| 274 | 继续记录 | Resume recording | 继续记录 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 275 | 编辑系列 | Edit series | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 276 | 编辑经验 | Edit lesson | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:105 |
| 277 | 编辑题目 | Edit question | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml.cs:153 |
| 278 | 缩小字号 | Smaller text | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 279 | 联网来源 | Web sources | — |  |
| 280 | 自动回答 | Automatic answers | 自动回答 | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 281 | 自检回答 | Self-test answer | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:572 |
| 282 | 自检问题 | Self-test question | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:572 |
| 283 | 要点大纲 | Outline | 要点大纲 |  |
| 284 | 观点对照 | Viewpoints | 观点对照 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 285 | 视频通话 | Video call | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:159 |
| 286 | 解析结果 | Parse result | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 287 | 讨论题目 | Discussion topics | 讨论题目 | Windows/src/NextThought.Desktop/MeetingDraft.cs:79 |
| 288 | 记录时长 | recorded time | 记录时长 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 289 | 讲稿要点 | Script key points | — |  |
| 290 | 设置清单 | Setup checklist | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 291 | 识别模型 | Recognition model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 292 | 详细信息 | Details | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 293 | 语音识别 | Speech recognition | 语音识别 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 294 | 误听标记 | Misheard marker | — | Windows/src/NextThought.Desktop/GlossaryEntryRow.cs:11 |
| 295 | 资料复核 | Material review | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 296 | 资料引用 | Material citations | — |  |
| 297 | 跟随系统 | Follow system | 跟随系统 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:394 |
| 298 | 跳到原话 | Jump to the original | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 299 | 通义千问 | Qwen (Tongyi) | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:354 |
| 300 | 重新检查 | Check again | 重新检查 | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:174 |
| 301 | 附着技能 | Attach skills | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 302 | 面试复盘 | Interview review | — |  |
| 303 | 面试记录 | Interview notes | 面试记录 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 304 | 领域字段 | Domain fields | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 305 | 首次引导 | First-run guide | — |  |
| 306 | （主屏） |  (main display) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:61 |
| 307 | 一句话印象 | One-line impression | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 308 | 上一场转写 | Last meeting's transcript | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:101 |
| 309 | 下一场会议 | Next meeting | — |  |
| 310 | 下载并安装 | Download and install | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 311 | 不自动结束 | Do not auto-end | 不自动结束 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:661 |
| 312 | 什么都可以 | Anything goes | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:18 |
| 313 | 使用此区域 | Use this area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 314 | 保存到设置 | Save to settings | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 315 | 保存快捷键 | Save shortcuts | — |  |
| 316 | 保存术语表 | Save glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 317 | 全局快捷键 | Global shortcuts | 全局快捷键 |  |
| 318 | 关闭提词器 | Close teleprompter | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 319 | 分工与对齐 | Assign and align | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:13 |
| 320 | 参考资料库 | Reference materials | — |  |
| 321 | 双屏演讲： | Presenter mode:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:673 |
| 322 | 只记录模式 | Record-only mode | — |  |
| 323 | 听到的提问 | questions heard | 听到的提问 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 324 | 回滚失败： | Rollback failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:183 |
| 325 | 安装失败： | Install failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:315 |
| 326 | 导入失败： | Import failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:49 |
| 327 | 屏幕与截图 | Screen and screenshots | — |  |
| 328 | 屏幕：%@ | Screen: %@ | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:150 |
| 329 | 已保存密钥 | Key saved | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 330 | 已加密保存 | Saved encrypted | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:461 |
| 331 | 已确认经验 | Confirmed lessons | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 332 | 已选择原话 | Quote selected | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 333 | 录入失败： | Enrollment failed:  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:159 |
| 334 | 恢复失败： | Restore failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:60 |
| 335 | 截图失败： | Screenshot failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:371 |
| 336 | 截图已归档 | Screenshot archived | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 337 | 打开下载页 | Open download page | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 338 | 打开主窗口 | Open main window | 打开主窗口 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 339 | 打开术语表 | Open glossary | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 340 | 打开资料库 | Open library | — |  |
| 341 | 按模板整理 | Organize with template | 按模板整理 | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 342 | 接着上一场 | Continue the last one | 接着上一场 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 343 | 提示岛宽度 | Cue island width | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 344 | 提词岛示意 | Cue island preview | — |  |
| 345 | 改这一句… | Edit this sentence… | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 346 | 数据与关于 | Data and about | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 347 | 未命名会议 | Untitled meeting | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:101 |
| 348 | 未命名窗口 | Untitled window | — | Windows/src/NextThought.Desktop/ScreenCapture.cs:88 |
| 349 | 未归入系列 | Not in a series | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:184 |
| 350 | 模型与接口 | Model and API | — |  |
| 351 | 正在准备… | Preparing… | — | Windows/src/NextThought.Desktop/AppState.cs:267 |
| 352 | 正在更新… | Updating… | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:87 |
| 353 | 正在获取… | Fetching… | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:307 |
| 354 | 添加到系列 | Add to series | — |  |
| 355 | 类型与标题 | Type and title | — |  |
| 356 | 系列与资料 | Series and materials | — |  |
| 357 | 系列变更已 | Series change  | — |  |
| 358 | 系统 ·  | System ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:388 |
| 359 | 纪要已整理 | Notes ready | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:263 |
| 360 | 纪要整理中 | Notes being organized | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:264 |
| 361 | 结论与待办 | Conclusions and actions | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:15 |
| 362 | 给出的回答 | answers given | 给出的回答 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 363 | 继续记录。 | Recording resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:139 |
| 364 | 角色：%@ | Role: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:48 |
| 365 | 讲给别人听 | Present to others | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:12 |
| 366 | 译文待更新 | Translation out of date | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 367 | 资料与检索 | Materials and retrieval | — |  |
| 368 | 还没有纪要 | No notes yet | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:266 |
| 369 | 选择麦克风 | Select microphone | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 370 | 重命名系列 | Rename series | — |  |
| 371 | 问题置信度 | Question confidence | — | Windows/src/NextThought.Desktop/Views/Assistant/DetectionStrip.axaml.cs:47 |
| 372 | ，签名者  | , signer  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 373 | ⏎⏎资料：⏎ | 

Materials:
 | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:90 |
| 374 |  · 名字: |  · Name: | — |  |
| 375 | %lld 个 | %lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:190 |
| 376 | %lld 处 | %lld places | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:64 |
| 377 | %lld 段 | %lld blocks | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:225 |
| 378 | %lld 页 | %lld pages | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:84 |
| 379 | 上一场 ·  | Previous ·  | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:227 |
| 380 | 会议已导出。 | Meeting exported. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:159 |
| 381 | 会议自动收尾 | Auto-end meetings | 会议自动收尾 | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 382 | 会议记录操作 | Meeting record actions | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 383 | 保存个人笔记 | Save personal notes | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 384 | 共用文档组件 | Shared document components | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:82 |
| 385 | 准备检索组件 | Prepare retrieval components | — |  |
| 386 | 准备语音组件 | Prepare speech components | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 387 | 删除筛选结果 | Delete filtered results | — |  |
| 388 | 刷新应用列表 | Refresh apps | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 389 | 刷新网络地址 | Refresh addresses | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 390 | 双击恢复控件 | Double-click to restore controls | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml.cs:217 |
| 391 | 取消当前任务 | Cancel task | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 392 | 可选，未配置 | Optional, not configured | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:24 |
| 393 | 合并到上一个 | Merge into previous | — |  |
| 394 | 合并回主窗口 | Merge back into the main window | — | Windows/src/NextThought.Desktop/Views/DetachedModuleWindow.axaml |
| 395 | 回答已取消。 | Answer cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:646 |
| 396 | 回答已复制。 | Answer copied. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:782 |
| 397 | 回答模型名称 | Answer model | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 398 | 复制配对链接 | Copy pairing link | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 399 | 字幕显示译文 | Show translation in subtitles | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 400 | 安装包签名： | Installer signature:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:274 |
| 401 | 安装示例技能 | Install sample skill | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 402 | 导入 WAV | Import WAV | — |  |
| 403 | 导入纪要模板 | Import note template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:201 |
| 404 | 导出 PDF | Export PDF | — |  |
| 405 | 导出 SRT | Export SRT | — |  |
| 406 | 导出 VTT | Export VTT | — |  |
| 407 | 已保存 ·  | Saved ·  | — |  |
| 408 | 已删除会议： | Meeting deleted:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:83 |
| 409 | 已取消截图。 | Screenshot cancelled. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:220 |
| 410 | 已取消生成。 | Generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:104 |
| 411 | 已取消高亮。 | Highlight removed. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:143 |
| 412 | 已录入 ·  | Enrolled ·  | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:95 |
| 413 | 已恢复 %@ | Restored %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:186 |
| 414 | 已重新排队。 | Re-queued. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:343 |
| 415 | 幻灯片所在屏 | Slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 416 | 截取当前窗口 | Capture current window | — |  |
| 417 | 打开屏幕感知 | Turn on screen awareness | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 418 | 打开数据目录 | Open data folder | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 419 | 打开文件位置 | Open file location | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 420 | 技能已保存。 | Skill saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:98 |
| 421 | 技能已删除。 | Skill deleted. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:118 |
| 422 | 技能已清空。 | Skills cleared. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 423 | 把讨论留下来 | Keep the discussion | — |  |
| 424 | 把这场景加入 | Add this scene | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 425 | 按下组合键… | Press a key combination… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:433 |
| 426 | 提示岛所在屏 | Cue island display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 427 | 提词器已穿透 | Teleprompter click-through on | — | Windows/src/NextThought.Desktop/Views/TeleprompterHandleWindow.axaml |
| 428 | 提高不透明度 | More opaque | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 429 | 收起面试示例 | Hide sample interview | 收起面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:68 |
| 430 | 整理已取消。 | Note generation cancelled. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:255 |
| 431 | 整理这场会议 | Summarize this meeting | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 432 | 新系列的名字 | New series name | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 433 | 智谱 GLM | Zhipu GLM | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:354 |
| 434 | 更改尚未保存 | Changes are not saved yet | 更改尚未保存 | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:27 |
| 435 | 未使用资料库 | No materials used | — | Windows/src/NextThought.Desktop/AppState.Live.cs:557 |
| 436 | 本地（默认） | Local (default) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:468 |
| 437 | 本机语音识别 | On-device speech | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 438 | 术语已保存。 | Term saved. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:221 |
| 439 | 来回交换观点 | Exchange viewpoints | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:17 |
| 440 | 校对已取消。 | Proofreading cancelled. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:87 |
| 441 | 检查回答服务 | Test answer service | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:173 |
| 442 | 正在生成回答 | Generating answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:861 |
| 443 | 点击查看原图 | Click to view the full image | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 444 | 生成这种纪要 | Generate this note | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 445 | 筛选资料名称 | Filter material names | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 446 | 系列已归档。 | Series archived. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:444 |
| 447 | 纪要整理失败 | Notes failed | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:265 |
| 448 | 经验已删除。 | Lesson deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:402 |
| 449 | 经验已更新。 | Lesson updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:377 |
| 450 | 经验已记下。 | Lesson saved. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:523 |
| 451 | 继续一个系列 | Continue a series | 继续一个系列 | Windows/src/NextThought.Desktop/Views/Home/ContinueSeries.axaml |
| 452 | 继续完成设置 | Finish setting up | 继续完成设置 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:41 |
| 453 | 聆听已恢复。 | Listening resumed. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:227 |
| 454 | 说话人与翻译 | Speakers and translation | — |  |
| 455 | 还没有术语。 | No glossary terms yet. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:80 |
| 456 | 还没有经验。 | No lessons yet. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 457 | 选择输出设备 | Select output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 458 | 重命名失败： | Rename failed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:19 |
| 459 | 降低不透明度 | Less opaque | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 460 | 隐藏所有窗口 | Hide all windows | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:18 |
| 461 | 题库已更新。 | Question bank updated. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:417 |
| 462 | 题库还空着。 | The question bank is empty. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 463 | 题目已删除。 | Question deleted. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:431 |
| 464 | 麦克风 ·  | Microphone ·  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:391 |
| 465 | 默认，不指定 | Default, not specified | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:52 |
| 466 | ，并恢复设置 | , and settings restored | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:231 |
| 467 | ，立即生效。 | , takes effect immediately. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:462 |
| 468 |  · 可能回声 |  · possible echo | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:109 |
| 469 |  · 提词按句 |  · cue by sentence | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 470 | %lld 秒前 | %lld s ago | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:170 |
| 471 | 不到 1 分钟 | Under a minute | — |  |
| 472 | 不是同一个系列 | Not the same series | 不是同一个系列 | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 473 | 主屏（摄像头） | Main display (camera) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:608 |
| 474 | 主要听，记重点 | Mostly listen, note the key points | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:16 |
| 475 | 云端识别中断： | Cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:238 |
| 476 | 产品资料、报价 | Product materials, quotes | 产品资料、报价 | Windows/src/NextThought.Desktop/MeetingDraft.cs:77 |
| 477 | 从文件导入术语 | Import terms from a file | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml.cs:64 |
| 478 | 会议写入失败： | Writing the meeting failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:432 |
| 479 | 会议归档 ·  | Meeting archived ·  | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:494 |
| 480 | 保存条目失败： | Saving the item failed:  | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:158 |
| 481 | 保存高亮失败： | Saving the highlight failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:144 |
| 482 | 先听需求再回应 | Hear the need first | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:14 |
| 483 | 全文搜索失败： | Full-text search failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:89 |
| 484 | 删除这场会议？ | Delete this meeting? | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:128 |
| 485 | 双屏演讲已关闭 | Presenter mode is off | — |  |
| 486 | 后台下载失败： | Background download failed:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:225 |
| 487 | 回滚到上一版本 | Roll back to the previous version | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 488 | 回答正在生成… | Generating an answer… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 489 | 在转写中查找… | Find in transcript… | — |  |
| 490 | 实时纪要没有开 | Live notes are off | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:86 |
| 491 | 对方 · 流式 | Them · streaming | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 492 | 导入 .md… | Import .md… | — |  |
| 493 | 导出 Word | Export Word | — |  |
| 494 | 尚未加载技能。 | No skills loaded. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 495 | 展开识别的文字 | Show recognised text | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 496 | 已保存访问令牌 | Access token saved | 已保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 497 | 已暂停自动滚动 | Auto-scroll paused | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 498 | 已重命名会议： | Meeting renamed:  | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:17 |
| 499 | 已高亮并保存。 | Highlighted and saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:154 |
| 500 | 带入上一场转写 | Bring last transcript | — |  |
| 501 | 开始或停止记录 | Start or stop recording | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:19 |
| 502 | 当前场景：通用 | Current scene: General | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:27 |
| 503 | 当前系列的术语 | Terms in this series | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:52 |
| 504 | 当前问题与候选 | Current question and candidates | — | Windows/src/NextThought.Desktop/Views/DetachedModuleWindow.axaml.cs:68 |
| 505 | 打开位置失败： | Opening the location failed:  | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:82 |
| 506 | 打开来源失败： | Could not open the source:  | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:159 |
| 507 | 技能保存失败： | Saving skills failed:  | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:66 |
| 508 | 无法识别按键： | Unrecognized key:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:196 |
| 509 | 未保存访问令牌 | No access token saved | 未保存访问令牌 | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:337 |
| 510 | 未安装或待修复 | Not installed or needs repair | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:73 |
| 511 | 未导入 PPT | No PPT imported | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:82 |
| 512 | 术语表已清空。 | Glossary cleared. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:483 |
| 513 | 模板保存失败： | Saving the template failed:  | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:102 |
| 514 | 模板需要名字。 | A template needs a name. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:166 |
| 515 | 正在整理纪要… | Writing the notes… | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 516 | 正在校对转写… | Proofreading the transcript… | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:39 |
| 517 | 正在检查更新… | Checking for updates… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:84 |
| 518 | 正在联网核实… | Checking online… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:574 |
| 519 | 火山方舟 豆包 | Volcengine Ark Doubao | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:354 |
| 520 | 界面操作出错： | Interface error:  | — | Windows/src/NextThought.Desktop/App.axaml.cs:26 |
| 521 | 看一场示例会议 | Watch a sample meeting | — |  |
| 522 | 看一段面试示例 | Watch a sample interview | 看一段面试示例 | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:67 |
| 523 | 窗口截图失败： | Window capture failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:228 |
| 524 | 等待首次读屏… | Waiting for the first screen read… | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:63 |
| 525 | 系列变更待确认 | Series changes to confirm | — |  |
| 526 | 系列已重命名： | Series renamed:  | — | Windows/src/NextThought.Desktop/AppState.Series.cs:449 |
| 527 | 经验 %lld | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Series.cs:291 |
| 528 | 结束后自动整理 | Summarize automatically after ending | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 529 | 结束并整理纪要 | End and write notes | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 530 | 联网查证失败： | Web verification failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:750 |
| 531 | 自动滚动到最新 | Auto-scroll to the latest | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:53 |
| 532 | 自定义纪要模板 | Custom note templates | 自定义纪要模板 | Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml |
| 533 | 被问到 1 次 | Asked once | — |  |
| 534 | 要求 / 偏好 | Requirements / preferences | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 535 | 讲进展，听意见 | Share progress, hear feedback | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:11 |
| 536 | 译文 · 当前 | Translation · current | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 537 | 请先输入问题。 | Type a question first. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:515 |
| 538 | 请总结当前讨论 | Summarize the current discussion | — | Windows/src/NextThought.Desktop/AppState.Live.cs:757 |
| 539 | 还没有选择系列 | No series selected | — | Windows/src/NextThought.Desktop/AppState.Series.cs:31 |
| 540 | 选择自定义模板 | Select custom template | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 541 | 静音后直接结束 | End immediately after silence | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:660 |
| 542 | 静音多久后收尾 | End after silence for | — |  |
| 543 | 默认（不指定） | Default (not specified) | — |  |
| 544 |  · 提词按要点 |  · cue by key points | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 545 | %lld 个窗口 | %lld windows | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:28 |
| 546 | 中转地址（可选） | Relay URL (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 547 | 云端识别（实验） | Cloud transcription (experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 548 | 会中笔记已保存。 | Meeting notes saved. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:115 |
| 549 | 会中组件状态样板 | In-meeting component states | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 550 | 写下讨论的主题。 | Write the discussion topic. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:144 |
| 551 | 准备开始一场会议 | Ready to start a meeting | — |  |
| 552 | 删除已录入的声音 | Delete the enrolled voice | — |  |
| 553 | 可选，还没有资料 | Optional, no materials yet | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 554 | 合并说话人失败： | Could not merge the speakers:  | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:159 |
| 555 | 听到外语自动翻译 | Auto-translate foreign speech | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 556 | 回答语言已更新。 | Answer language updated. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml.cs:304 |
| 557 | 声音设备已刷新。 | Audio devices refreshed. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:185 |
| 558 | 尚无自定义模板。 | No custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 559 | 屏幕感知已关闭。 | Screen awareness turned off. | — |  |
| 560 | 屏幕感知已开启。 | Screen awareness turned on. | — |  |
| 561 | 工作会议模板纪要 | Meeting template notes | — |  |
| 562 | 已从系列中移出。 | Removed from the series. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:509 |
| 563 | 已保存 · %@ | Saved · %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:647 |
| 564 | 已关闭实时纪要。 | Live notes are off. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:152 |
| 565 | 已写入系列记录。 | Written to the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:114 |
| 566 | 已加入系列术语。 | Added to the series glossary. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:458 |
| 567 | 已加入系列术语： | Added to the series glossary:  | — |  |
| 568 | 已备份当前版本： | Current version backed up:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:299 |
| 569 | 已安装示例技能。 | Sample skill installed. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:133 |
| 570 | 已忽略这条建议。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:364 |
| 571 | 待确认 %lld | %lld to confirm | — | Windows/src/NextThought.Desktop/AppState.Series.cs:293 |
| 572 | 快捷键保存失败： | Saving shortcuts failed:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:213 |
| 573 | 恢复未全部完成： | Restore did not finish:  | — |  |
| 574 | 截图文件已不存在 | The screenshot file no longer exists | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:232 |
| 575 | 手机伴侣已关闭。 | Phone companion stopped. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:130 |
| 576 | 打开术语表失败： | Opening the glossary failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:334 |
| 577 | 拖动选择截图区域 | Drag to select a capture area | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 578 | 接上外接屏后可用 | Available once an external display is connected | 接上外接屏后可用 |  |
| 579 | 提词浮窗鼠标穿透 | Teleprompter click-through | 提词浮窗鼠标穿透 |  |
| 580 | 整个输出设备混音 | Entire output device | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 581 | 旧原话：缓存几穿 | Old wording: cache miss | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 582 | 显示器 %lld | Display %lld | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:608 |
| 583 | 未知快捷键动作： | Unknown shortcut action:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:194 |
| 584 | 正在下载安装包… | Downloading the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:254 |
| 585 | 正在准备安装包… | Preparing the installer… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:238 |
| 586 | 正在导入 %@… | Importing %@… | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:241 |
| 587 | 正在整理成%@… | Organizing into %@… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:395 |
| 588 | 正在解析 %@… | Parsing %@… | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:122 |
| 589 | 没有可用的显示器 | No display available | — |  |
| 590 | 系列信息已保存。 | Series details saved. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:147 |
| 591 | 系列变更未应用。 | Series change not applied. | — |  |
| 592 | 系列名不能为空。 | A series needs a name. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:130 |
| 593 | 系列文档已移除。 | Series document removed. | — |  |
| 594 | 系统输出设备混音 | System output mix | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 595 | 给这场会起个名字 | Name this meeting | — |  |
| 596 | 被问、要答得清楚 | Asked, answer clearly | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:10 |
| 597 | 讨论重点已整理。 | Discussion points are ready. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:99 |
| 598 | 说话人 %lld | Speaker %lld | 说话人 %lld | Windows/src/NextThought.Desktop/AppState.LivePage.cs:129 |
| 599 | 说话人修改失败： | Could not change the speaker:  | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:133 |
| 600 | 请总结当前讨论。 | Summarize the current discussion. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:821 |
| 601 | 资料库读取失败： | Reading the library failed:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:74 |
| 602 | 跟着讲话翻幻灯片 | Follow the talk across slides | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 603 | 还没有可用的文字 | No usable text yet | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:224 |
| 604 | 还没有归档截图。 | No archived screenshots yet. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 605 | 还没有检查更新。 | No update check yet. | — |  |
| 606 | 这一节要整理什么 | What should this section collect? | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 607 | 这条建议已忽略。 | Suggestion ignored. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:113 |
| 608 | 配对链接已复制。 | Pairing link copied. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:273 |
| 609 | 首字 %.1fs | first token %.1fs | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:183 |
| 610 | ；本机回退失败： | ; local fallback failed:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:235 |
| 611 | %@ %lld 段 | %@ %lld sections | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 612 | M月d日 dddd | MMMM d, dddd | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:52 |
| 613 | high · 最强 | high · strongest | — |  |
| 614 | main 最新构建 | main latest build | — | Windows/src/NextThought.Desktop/AppState.Update.cs:46 |
| 615 | 从以前的会议带入… | Carry from an earlier meeting… | 从以前的会议带入… | Windows/src/NextThought.Desktop/Views/PrepareView.axaml |
| 616 | 作业要求、评分标准 | Assignment requirements, grading rubric | 作业要求、评分标准 | Windows/src/NextThought.Desktop/MeetingDraft.cs:80 |
| 617 | 作为新决定加入系列 | Add to the series as a new decision | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:51 |
| 618 | 作为新行动加入系列 | Add to the series as a new action | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:50 |
| 619 | 保存会中笔记失败： | Saving meeting notes failed:  | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:117 |
| 620 | 保存复核备注失败： | Saving the review note failed:  | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:98 |
| 621 | 关闭局域网手机伴侣 | Stop LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:321 |
| 622 | 再决定是否调整…… | then decide whether to adjust… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 623 | 准备后可在会中引用 | Ready to cite during the meeting | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 624 | 删除会议和依赖内容 | Delete the meeting and its dependent content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:297 |
| 625 | 删除全部会议记录？ | Delete all meeting records? | 删除全部会议记录？ | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:155 |
| 626 | 刷新声音设备失败： | Refreshing audio devices failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:187 |
| 627 | 回答服务连接成功： | Answer service connected:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:644 |
| 628 | 在资源管理器中显示 | Show in File Explorer | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 629 | 复制配对链接失败： | Copying the pairing link failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:275 |
| 630 | 复盘 / 当前阶段 | Review / current stage | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 631 | 导入 .json… | Import .json… | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 632 | 开启局域网手机伴侣 | Start LAN phone companion | — | Windows/src/NextThought.Desktop/AppState.cs:321 |
| 633 | 手动校正：缓存击穿 | Manual correction: cache stampede | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 634 | 手机伴侣启动失败： | Starting the phone companion failed:  | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:159 |
| 635 | 打开令牌页面失败： | Opening the token page failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:313 |
| 636 | 打开我的声音失败： | Opening My Voice failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:351 |
| 637 | 更改尚未保存：%@ | Changes are not saved yet: %@ | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:126 |
| 638 | 有了内容就开始整理 | It will start once there is something to summarise | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:92 |
| 639 | 本周进展、相关论文 | This week's progress, related papers | 本周进展、相关论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:75 |
| 640 | 本机 Ollama | Local Ollama | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:355 |
| 641 | 本机离线识别已就绪 | On-device offline recognition is ready | — | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:15 |
| 642 | 框选读屏区域失败： | Selecting the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:341 |
| 643 | 模型（留空用默认） | Model (empty uses default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 644 | 模板文件格式无效： | The template file format is invalid:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:214 |
| 645 | 正在加载本场资料… | Loading this meeting's materials… | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 646 | 正在备份当前版本… | Backing up the current version… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:292 |
| 647 | 正在截屏读取屏幕… | Reading the screen… | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:45 |
| 648 | 正在更新实时纪要… | Updating live notes… | — |  |
| 649 | 正在本机读取录音… | Reading the recording on this device… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:391 |
| 650 | 正在查证当前回答… | Verifying the current answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:733 |
| 651 | 正在检查回答服务… | Checking the answer service… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:542 |
| 652 | 正在生成参考回答… | Generating a reference answer… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:542 |
| 653 | 没有新的知识候选。 | No new knowledge candidates. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:258 |
| 654 | 清除读屏区域失败： | Clearing the screen-read area failed:  | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:372 |
| 655 | 组会 / 研究讨论 | Lab meeting / research discussion | — |  |
| 656 | 经验 %lld 条 | %lld lessons | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:100 |
| 657 | 自定义模板已清空。 | Custom templates cleared. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 658 | 英语回答附中文对照 | Gloss English answers in Chinese | — | Windows/src/NextThought.Desktop/Views/QuickSettingsPanel.axaml |
| 659 | 规范写法不能为空。 | The canonical form cannot be empty. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:211 |
| 660 | 讲稿、PPT、论文 | Script, slides, paper | 讲稿、PPT、论文 | Windows/src/NextThought.Desktop/MeetingDraft.cs:74 |
| 661 | 语义模型路径无效。 | The semantic model path is not valid. | — | Windows/src/NextThought.Desktop/AppState.Documents.cs:134 |
| 662 | 请只回复：连接成功 | Reply only: connection succeeded | — | Windows/src/NextThought.Desktop/AppState.Live.cs:710 |
| 663 | 读取会议列表失败： | Reading the meeting list failed:  | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:63 |
| 664 | 读取窗口列表失败： | Could not read the window list:  | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:32 |
| 665 | 资料 %lld 份 | %lld materials | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:96 |
| 666 | 输入问题，回车回答 | Type a question and press Enter | — |  |
| 667 | 输入问题；回车发送 | Type a question; press Enter to send | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 668 | 这一句改成了%@。 | This line now belongs to %@. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:127 |
| 669 | 选择多个系列后删除 | Select multiple series to delete | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 670 | 问题 %lld 个 | %lld questions | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:260 |
| 671 | 题库 %lld 题 | %lld questions in the bank | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:98 |
| 672 | 麦克风已重新打开。 | Microphone reopened. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:329 |
| 673 | [%@] %@：%@ | [%@] %@: %@ | — |  |
| 674 | none（关闭思考） | none (reasoning off) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:53 |
| 675 | 。接下来是什么场合？ | . What's next? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:57 |
| 676 | 严格按用户要求回复。 | Follow the user's request exactly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:609 |
| 677 | 会议结束时遇到问题： | A problem occurred while ending the meeting:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:358 |
| 678 | 保存说话人设置失败： | Could not save the speaker settings:  | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:147 |
| 679 | 先给系列起一个名字。 | Give the series a name first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:474 |
| 680 | 写这一节要整理什么。 | What should this section collect? | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:150 |
| 681 | 单栏上下排列（小屏） | Single column, stacked (small screen) | — |  |
| 682 | 在设置中选择声音来源 | Choose an audio source in settings | — | Windows/src/NextThought.Desktop/AppState.cs:291 |
| 683 | 屏幕上没有读到文字。 | No text was read from the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:54 |
| 684 | 已导入 %lld 份 | Imported %lld | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:25 |
| 685 | 已新建系列「%@」。 | Series “%@” created. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:482 |
| 686 | 已经有同名的系列了。 | A series with that name already exists. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:477 |
| 687 | 已跳到原话 · %@ | Jumped to the original · %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:608 |
| 688 | 开始记录后才能读屏。 | Start recording before reading the screen. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:108 |
| 689 | 录音转写 · 已归档 | Recording transcribed · archived | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:453 |
| 690 | 提词岛会中一直展开。 | The cue island stays open during meetings. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 691 | 有 %lld 处提示 | %lld notes | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:222 |
| 692 | 未知的云端识别服务。 | Unknown cloud transcription service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:202 |
| 693 | 本机 · 不依赖网络 | On-device · works offline | — |  |
| 694 | 校验通过，正在安装… | Verified; installing… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:308 |
| 695 | 核对上一条回答的事实 | Check the facts in the last answer | — | Windows/src/NextThought.Desktop/AppState.Live.cs:736 |
| 696 | 模板「%@」已保存。 | Template “%@” saved. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:178 |
| 697 | 正在检查安装包签名… | Checking the installer signature… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:263 |
| 698 | 没有可用的更新备份。 | No update backup is available. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:170 |
| 699 | 没有需要更正的地方。 | Nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:47 |
| 700 | 看镜头 %.0f%% | Eye contact %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:56 |
| 701 | 第三方许可与版权说明 | Third-party licenses and copyright | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 702 | 联网查证缓存已清空。 | The web-verification cache was cleared. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:62 |
| 703 | 自动（第一块外接屏） | Automatic (first external display) | — | Windows/src/NextThought.Desktop/DisplayCatalog.cs:24 |
| 704 | 资料复核备注已保存。 | Material review note saved. | — | Windows/src/NextThought.Desktop/AppState.MaterialReview.cs:96 |
| 705 | 还没有可回答的转写。 | No transcript to answer yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:721 |
| 706 | 还没有可查证的回答。 | No answer to verify yet. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:731 |
| 707 | 这次不带入系列内容。 | Nothing from the series is carried into this meeting. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:102 |
| 708 | 麦克风云端识别中断： | Microphone cloud transcription interrupted:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:252 |
| 709 | （会议窗口保持显示） |  (meeting window stays visible) | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:690 |
| 710 | ；界面按钮仍可使用。 | ; window buttons still work. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 711 |  · 说话人 %lld |  · speaker %lld | — | Windows/src/NextThought.Desktop/TranscriptRow.cs:108 |
| 712 | %@ · 建议放：%@ | %@ · Suggested materials: %@ | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:93 |
| 713 | API · 未指定模型 | API · no model chosen | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:192 |
| 714 | 下午好。把讨论留下来。 | Good afternoon. Keep the discussion. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:48 |
| 715 | 中午好。会议还在继续。 | Good noon. The meeting goes on. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:47 |
| 716 | 云服务 API Key | Cloud service API key | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 717 | 会议已导出为 PDF。 | Meeting exported as PDF. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:189 |
| 718 | 先开始或打开一场会议。 | Start or open a meeting first. | — | Windows/src/NextThought.Desktop/AppState.MeetingNotes.cs:109 |
| 719 | 发言占比 %.0f%% | Speaking share %.0f%% | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:54 |
| 720 | 回答在左 · 对话在右 | Answers left · conversation right | — |  |
| 721 | 回答服务（Codex） | Answer service (Codex) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 722 | 备份失败，仍继续更新： | Backup failed; continuing the update:  | — | Windows/src/NextThought.Desktop/AppState.Update.cs:304 |
| 723 | 导入 %@ 失败：%@ | Importing %@ failed: %@ | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:245 |
| 724 | 导出 Markdown | Export Markdown | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 725 | 已把这一句算作我说的。 | This line now counts as mine. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:35 |
| 726 | 已结束 · 会议已归档 | Ended · meeting archived | — | Windows/src/NextThought.Desktop/AppState.Live.cs:355 |
| 727 | 幻灯片：%@ · %@ | Slide: %@ · %@ | — |  |
| 728 | 引用来自已删除的资料： | Citations from deleted materials:  | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:147 |
| 729 | 当前回答尚无资料引用。 | The current answer has no material citations yet. | — |  |
| 730 | 当前没有可命名的说话人 | No speaker to name right now | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:62 |
| 731 | 录入 / 管理我的声音 | Enroll / manage my voice | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 732 | 最长独白 %.0f 秒 | Longest monologue %.0f s | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:55 |
| 733 | 未找到第三方许可文件。 | Third-party license file not found. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:321 |
| 734 | 正在加载本地语音模型… | Loading the on-device speech model… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:172 |
| 735 | 正在记录的会议不能删除 | A meeting that is recording cannot be deleted | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:160 |
| 736 | 没有可导入的技能文件。 | No skill file to import. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:259 |
| 737 | 没有找到可截图的窗口。 | No window was found to capture. | — |  |
| 738 | 示例技能已经在列表里。 | The sample skill is already in the list. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:132 |
| 739 | 系列已有相近经验：%@ | The series already has related lessons: %@ | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:226 |
| 740 | 联网来源 %lld 条 | %lld web sources | — | Windows/src/NextThought.Desktop/AppState.cs:314 |
| 741 | 误听写法，用 \| 分隔 | Misheard forms separated by \| | — |  |
| 742 | 这场会议没有生成回答。 | This meeting has no answers. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 743 | 重新联网查一下当前回答 | Check the current answer again with the web | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 744 | 隐藏 / 恢复所有窗口 | Hide / restore all windows | — |  |
| 745 | %lld 个自定义模板。 | %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:83 |
| 746 | minimal · 最快 | minimal · fastest | — |  |
| 747 | 云端没有返回可用的文字。 | The cloud returned no usable text. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:426 |
| 748 | 使用语义与关键词混合检索 | Hybrid semantic and keyword retrieval | — |  |
| 749 | 依据：“%@” · %@ | Basis: “%@” · %@ | — | Windows/src/NextThought.Desktop/SeriesChangeRow.cs:36 |
| 750 | 全文搜索至少输入两个字。 | Enter at least two characters for full-text search. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:81 |
| 751 | 内置屏 / 主屏（自动） | Built-in / main display (automatic) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:57 |
| 752 | 可以继续对这场会议提问。 | You can keep asking about this meeting. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:508 |
| 753 | 在这里写这个技能的要求。 | Write what this skill requires. | — |  |
| 754 | 字幕已导出（%@）%@。 | Subtitles exported (%@)%@. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:359 |
| 755 | 已在浏览器中打开下载页。 | Opened the download page in the browser. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:332 |
| 756 | 已把 %@ 并入 %@。 | Merged %@ into %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:157 |
| 757 | 已把 %@ 移到「%@」 | Moved %@ to “%@” | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:220 |
| 758 | 已讲 %lld/%lld | Spoken %lld/%lld | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:270 |
| 759 | 已读屏 · %lld 字 | Screen read · %lld characters | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:64 |
| 760 | 已选择 %lld 场会议 | %lld meetings selected | — |  |
| 761 | 开始记录后才能暂停聆听。 | Start recording before pausing listening. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:223 |
| 762 | 当前场景：%@ · %@ | Current scene: %@ · %@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:26 |
| 763 | 技能（回答时遵守的说明） | Skills (instructions followed when answering) | — |  |
| 764 | 提词浮窗已恢复鼠标操作。 | Teleprompter clicks restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:493 |
| 765 | 搜索会议标题、日期或模式 | Search meeting title, date or mode | — |  |
| 766 | 早上好。今天要记下什么？ | Good morning. What should we capture today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:46 |
| 767 | 本机 LM Studio | Local LM Studio | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:355 |
| 768 | 术语表（人名与专有名词） | Glossary (names and terms) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 769 | 模板文件里没有可用模板。 | The template file has no usable templates. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:211 |
| 770 | 没有匹配到「数据库迁移」 | No match for “database migration” | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 771 | 演示模式：未加载识别模型 | Demo mode: recognition model not loaded | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:102 |
| 772 | 简历、职位描述、公司信息 | Résumé, job description, company info | 简历、职位描述、公司信息 | Windows/src/NextThought.Desktop/MeetingDraft.cs:73 |
| 773 | 继续完成设置（还差%@） | Finish setting up (missing %@) | 继续完成设置（还差%@） | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:47 |
| 774 | 选择声音来源，开始记录。 | Choose an audio source and start recording. | — | Windows/src/NextThought.Desktop/AppState.cs:266 |
| 775 | 麦克风 · 适合线下会议 | Microphone · for in-person meetings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:78 |
| 776 | ；已经记录的内容已保存。 | ; what was recorded is saved. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:356 |
| 777 |   ·  另 %lld 份 |   ·  %lld more | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:34 |
| 778 | Soniox · 中英混说 | Soniox · bilingual | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:469 |
| 779 | 会议已保存，可以整理纪要。 | Meeting saved; notes can be organized. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:356 |
| 780 | 使用系列知识（经验与题库） | Use series knowledge (lessons and question bank) | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 781 | 夜深了。要把想法留下来吗？ | Late night. Want to keep a thought? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:45 |
| 782 | 安装已下载的更新并重新打开 | Install the downloaded update and reopen | — | Windows/src/NextThought.Desktop/Views/MainWindow.axaml |
| 783 | 已从「%@」移除「%@」。 | Removed “%@” from “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:178 |
| 784 | 已删除 %lld 场会议。 | %lld meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:45 |
| 785 | 已导入 %lld 份资料。 | %lld materials imported. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:135 |
| 786 | 已暂停自动滚动（点击恢复） | Auto-scroll paused (choose to resume) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 787 | 已采纳，下次回答会带上它。 | Accepted; later answers will use it. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:367 |
| 788 | 手动提问，或开启自动回答。 | Ask manually, or turn on automatic answers. | — | Windows/src/NextThought.Desktop/AppState.cs:285 |
| 789 | 整句精修，提高最终转写质量 | Refine whole sentences for a better transcript | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 790 | 新增 2 句 · 回到现场 | +2 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 791 | 新增 6 句 · 回到现场 | +6 lines · back to live | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 792 | 正在校验后台下载的安装包… | Verifying the background download… | — | Windows/src/NextThought.Desktop/AppState.Update.cs:247 |
| 793 | 没有新的已核实决定或行动。 | No new verified decisions or actions. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:130 |
| 794 | 系列已删除；会议记录保留。 | Series deleted; meeting records kept. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:459 |
| 795 | 自动滚动到最新（点击暂停） | Auto-scroll to the latest (choose to pause) | — | Windows/src/NextThought.Desktop/Views/Assistant/ConversationPanel.axaml.cs:52 |
| 796 | 语速 %.0f 单位/分钟 | Pace %.0f units/min | — | Windows/src/NextThought.Desktop/AppState.Coach.cs:53 |
| 797 | 读屏失败，20 秒后重试。 | Screen read failed; retrying in 20 seconds. | — | Windows/src/NextThought.Desktop/AppState.ScreenWatch.cs:70 |
| 798 | 这个键不能用作全局快捷键。 | That key cannot be used as a global shortcut. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:455 |
| 799 | 这次没更新成功，稍后会再试 | This update did not come back; it will try again shortly. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:88 |
| 800 | 面试官在追问 · 参考回答 | The interviewer is following up · reference answer | 面试官在追问 · 参考回答 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 801 | %lld 小时 %lld 分 | %lld h %lld min | — |  |
| 802 | 中转地址不是有效的 URL。 | The relay address is not a valid URL. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:560 |
| 803 | 先停止当前任务，再删除会议。 | Stop the current task before deleting meetings. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:54 |
| 804 | 先停止当前任务，再删除系列。 | Stop the current task before deleting a series. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:327 |
| 805 | 全局快捷键已保存并重新注册。 | Shortcuts saved and re-registered. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:211 |
| 806 | 全文搜索：%lld 条命中。 | Full-text search: %lld hits. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:87 |
| 807 | 导入技能（Markdown） | Import skill (Markdown) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:243 |
| 808 | 已刷新可采集的应用程序列表。 | The list of recordable apps was refreshed. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:24 |
| 809 | 已把「%@」附着到「%@」。 | Attached “%@” to “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:177 |
| 810 | 已整理成%@，可复制或导出。 | Organized into %@; ready to copy or export. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:481 |
| 811 | 已新建技能，改完内容后保存。 | New skill created; edit it and save. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:113 |
| 812 | 幻灯片在 %@，提示在 %@ | Slides on %@, cue on %@ | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:607 |
| 813 | 拖动可移动 · 双击隐藏控件 | Drag to move · double-click to hide controls | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 814 | 搜索命中 %lld/%lld | Search hits %lld/%lld | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:42 |
| 815 | 正在加载模型并转写 WAV… | Loading the model and transcribing the WAV… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:393 |
| 816 | 正在按《%@》合并分段结果… | Merging parts for “%@”… | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:226 |
| 817 | 系列变更待确认 · %lld | Series changes to confirm · %lld | — | Windows/src/NextThought.Desktop/AppState.Series.cs:78 |
| 818 | 纪要模型（留空沿用回答模型） | Notes model (empty uses the answer model) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 819 | 线上会议听电脑里播放的声音。 | Online meetings listen to what this computer plays. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:173 |
| 820 | 识别出来的错误写法，逗号分隔 | Recognized misspellings, comma separated | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:68 |
| 821 | 语速偏快，适当放慢并加停顿。 | Speaking fast; slow down and pause. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:313 |
| 822 | 说话人命名已应用到当前转写。 | Speaker names applied to the current transcript. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:208 |
| 823 | 资料与 AI 都可稍后补充。 | Materials and an answer service can both be added later. | 资料与 AI 都可稍后补充。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:88 |
| 824 | 这场会议还没有可复核的条目。 | This meeting has nothing to review yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:123 |
| 825 | 这场会议还没有可收集的候选。 | This meeting has no candidates to collect yet. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:249 |
| 826 | 退出 NextThought | Quit NextThought | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:20 |
| 827 | 选一个类型作为基础，再改名字 | Pick a base type, then rename it | 选一个类型作为基础，再改名字 | Windows/src/NextThought.Desktop/MeetingDraft.cs:68 |
| 828 | 静音后先提示，再结束（推荐） | Prompt after silence, then end (recommended) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:659 |
| 829 | %lld 份资料 · 本机解析 | %lld materials · parsed on this device | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:57 |
| 830 | NextThought 字幕条 | NextThought Subtitles | NextThought 字幕条 | Windows/src/NextThought.Desktop/Views/SubtitleBarWindow.axaml |
| 831 | NextThought 提词器 | NextThought Teleprompter | NextThought 提词器 | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 832 | NextThought 提词岛 | NextThought Cue island | NextThought 提词岛 | Windows/src/NextThought.Desktop/Views/CueIslandWindow.axaml |
| 833 | 云端识别中断，已回退本机识别： | Cloud transcription interrupted; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:230 |
| 834 | 以下全局快捷键被其他程序占用： | These global shortcuts are used by other programs:  | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:124 |
| 835 | 会议已导出为 Word 文档。 | Meeting exported as a Word document. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:174 |
| 836 | 保存 Tavily 密钥失败： | Saving the Tavily key failed:  | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:33 |
| 837 | 先停止当前任务，再准备下一场。 | Stop the current task before preparing the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Home.cs:131 |
| 838 | 准备声纹组件（约 26 MB） | Prepare speaker components (~26 MB) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 839 | 删除会议，保留已确认的系列内容 | Delete the meeting and keep accepted series content | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:295 |
| 840 | 双屏演讲已结束，主窗口已恢复。 | Presenter mode ended; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:717 |
| 841 | 回答暂时不可用；记录仍在继续。 | Answers are temporarily unavailable; recording continues. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 842 | 对话在左 · 回答在右（默认） | Conversation left · answers right (default) | — |  |
| 843 | 尚未保存密钥；本机服务可不填。 | No key saved yet; optional for local services. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 844 | 已导入，共 %lld 个技能。 | Imported %lld skills. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:47 |
| 845 | 已把「%@」放进系列「%@」。 | Added “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:497 |
| 846 | 已按屏幕切到第 %lld 页。 | Switched to slide %lld by screen. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:130 |
| 847 | 已读取归档；%lld 项待写入 | Archive read; %lld items pending write | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:653 |
| 848 | 当前有任务在进行，稍后再截图。 | A task is running; capture shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:213 |
| 849 | 当前有任务在进行，稍后再框选。 | A task is running; select the area shortly. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:315 |
| 850 | 提词岛已收起，鼠标移上来就展开 | Cue island collapsed; hover to expand | — |  |
| 851 | 提词岛恢复为鼠标移上去才展开。 | Cue island expands on hover again. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:301 |
| 852 | 校对完成：没有需要更正的地方。 | Proofreading finished: nothing needed correction. | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:48 |
| 853 | 正在记录 · %lld 条转写 | Recording · %lld lines | — | Windows/src/NextThought.Desktop/AppState.Live.cs:10 |
| 854 | 正在通过云端识别转写 WAV… | Transcribing the WAV with cloud recognition… | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:408 |
| 855 | 流式模型：%@　整句精修：%@ | Streaming model: %@; sentence refinement: %@ | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:72 |
| 856 | 演示数据 · 不会写入你的资料 | Demo data · nothing is written to your materials | — | Windows/src/NextThought.Desktop/AppState.Demo.cs:161 |
| 857 | 界面语言 / Language | Interface language | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 858 | 程序正在退出，已取消开始记录。 | The app is quitting; start recording was cancelled. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:187 |
| 859 | 第 %lld / %lld 页 | Page %lld / %lld | — | Windows/src/NextThought.Desktop/AppState.Insights.cs:28 |
| 860 | 系统声音范围（每次开始前选择） | System audio scope (choose before each start) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 861 | 纪要窗口 → 字幕条 → 隐藏 | Notes window → subtitle bar → hidden | — |  |
| 862 | 还没开过 · %lld 份资料 | Not started yet · %lld files | — | Windows/src/NextThought.Desktop/AppState.Home.cs:118 |
| 863 | 这场会议还没有转写，无法整理。 | This meeting has no transcript yet. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:384 |
| 864 | 进行中 · 转写持续保存在本机 | In progress · transcript saved locally | — | Windows/src/NextThought.Desktop/AppState.Live.cs:304 |
| 865 | 选区 %lld×%lld 像素 | Selection %lld×%lld pixels | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml.cs:89 |
| 866 | …… 其余 %lld 段未显示。 | … %lld more sections are hidden. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:103 |
| 867 | 先停止当前任务，再重新整理纪要。 | Stop the current task before organizing notes again. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:301 |
| 868 | 先开始记录，再改这一句的说话人。 | Start recording before changing who said this line. | — | Windows/src/NextThought.Desktop/AppState.LivePage.cs:102 |
| 869 | 准备这场会 · 类型、资料与检查 | Prepare this meeting · type, materials and checks | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:23 |
| 870 | 只在本机运行，声纹只是一组数字。 | Runs on-device; the voiceprint is just numbers. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 871 | 回答已完成。会议内的回答已归档。 | Answer complete. Answers from the meeting are archived. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:644 |
| 872 | 已保存 %lld 个自定义模板。 | Saved %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:98 |
| 873 | 已删除系列「%@」及其会议记录。 | Deleted series “%@” and its meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:340 |
| 874 | 已删除选中的 %lld 场会议。 | %lld selected meetings deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:35 |
| 875 | 已把 %lld 份资料加入系列。 | Added %lld materials to the series. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:258 |
| 876 | 已清除说话人 %lld 的名字。 | Cleared the name of speaker %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:119 |
| 877 | 已跟随讲话切到第 %lld 页。 | Switched to slide %lld by speech. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:128 |
| 878 | 查证完成：%lld 条联网来源。 | Verification complete: %lld web sources. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:748 |
| 879 | 模型已下载并校验，可以离线记录。 | Model downloaded and verified; offline recording is ready. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:81 |
| 880 | 第 %lld 页 · %@⏎%@ | Slide %lld · %@
%@ | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:169 |
| 881 | 系统声音里出现提问时自动生成回答 | Answer automatically when a question appears in the system audio | — |  |
| 882 | 要求 / Preferences | Requirements / Preferences | — |  |
| 883 | 说了很久了，收个尾，把话交回去。 | You have talked a while; wrap up and hand it back. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:312 |
| 884 | 说话人区分暂不可用，转写仍保留： | Speaker separation unavailable; the transcript is kept:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:157 |
| 885 | 这场会用的资料 · %lld 份 | Materials for this meeting · %lld | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:532 |
| 886 | 高级：也可以直接按文本批量编辑。 | Advanced: you can also edit everything as text. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 887 | %@ 字幕 (*.%@)\|*.%@ | %@ subtitles (*.%@)\|*.%@ | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:347 |
| 888 | %lld 个技能；回答时按需遵守。 | %lld skills; followed as needed when answering. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:26 |
| 889 | %lld 个技能；已保存启停状态。 | %lld skills; on/off state saved. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:86 |
| 890 | X-ASR 中英 · 160 ms | X-ASR bilingual · 160 ms | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:47 |
| 891 | 上次会议被中断，已保存到所有记录。 | The last meeting was interrupted and saved in all records. | 上次会议被中断，已保存到所有记录。 | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:113 |
| 892 | 云端识别启动失败，已回退本机识别： | Cloud transcription failed to start; fell back to on-device:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:266 |
| 893 | 使用本机 Codex CLI 账号 | Use local Codex CLI account | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 894 | 删除系列「%@」？会议记录会保留。 | Delete series “%@”? Meeting records are kept. | — |  |
| 895 | 区分会议中的说话人（本机实验功能） | Separate speakers (on-device, experimental) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 896 | 历史回答的引用已保存，可查看来源。 | Citations for the past answer were saved; view the sources. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:497 |
| 897 | 只影响悬浮字幕条；保存后立即生效。 | Only affects the floating subtitle bar; takes effect immediately after saving. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 898 | 听众可能会问（点一下提前准备回答） | Questions the audience may ask (tap to prepare an answer) | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 899 | 启动后自动检查更新（每 6 小时） | Check for updates automatically after launch (every 6 hours) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 900 | 在屏幕顶部显示问题与回答，不抢焦点 | Show question and answer at the top of the screen without taking focus | — |  |
| 901 | 对方讲得比较多，可以主动补充一句。 | They are talking more; add a point when you can. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:315 |
| 902 | 尚未选择声音来源，请到设置中选择。 | No audio source chosen; pick one in settings. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:78 |
| 903 | 幻灯片：第 %lld 页（手动）。 | Slide: page %lld (manual). | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:129 |
| 904 | 引导已稍后继续；首页会留一个入口。 | The guide was put aside; the home page keeps an entry. | — | Windows/src/NextThought.Desktop/AppState.OnboardingResume.cs:66 |
| 905 | 录音已转写并归档，未保存原始音频。 | Recording transcribed and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:441 |
| 906 | 按资料原有的页、幻灯片或工作表定位 | Locate by the material's own page, slide or sheet | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 907 | 晚上好。今天有哪些值得留下的结论？ | Good evening. What conclusions are worth keeping today? | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:49 |
| 908 | 检测到屏幕共享，已自动收回提示岛。 | Screen sharing detected; the cue island was retracted. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:47 |
| 909 | 正在记录所选应用及其子进程的声音。 | Recording the selected app and its child processes. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:307 |
| 910 | 第 %lld 页，共 %lld 页 | %1$lld of %2$lld pages | 第 %lld 页，共 %lld 页 | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:85 |
| 911 | 纪要编辑已保存；生成结果未被覆盖。 | Notes edits saved; generated content was not overwritten. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:122 |
| 912 | 说话人 %lld 已命名为 %@。 | Speaker %lld is now named %@. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:120 |
| 913 | 请针对上一条回答给出一个具体例子。 | Give a concrete example for the last answer. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:827 |
| 914 | 还没有连接回答服务，写不了实时纪要 | No answer service is connected, so live notes cannot be written | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:89 |
| 915 | 这个技能的要求（Markdown） | Skill requirements (Markdown) | — |  |
| 916 | 这场会议没有任何发言，已自动停止。 | This meeting had no speech and stopped automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:121 |
| 917 | 这场会议还没有纪要，点击上方整理。 | This meeting has no notes yet; organize it above. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:509 |
| 918 | 这条内容已被重新生成，编辑未应用。 | This item was regenerated; the edit was not applied. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:143 |
| 919 |  回答暂时不可用；记录仍在继续。 |  Answers are temporarily unavailable; recording continues. | — |  |
| 920 | Tavily API Key（可选） | Tavily API key (optional) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 921 | 一句话经验，例如：先说结论，再说依据 | One-line lesson, e.g. lead with the conclusion, then the evidence | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 922 | 云端识别已连接；转写发送到所选服务。 | Cloud transcription connected; the transcript is sent to the chosen service. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:254 |
| 923 | 参考资料库时使用语义与关键词混合检索 | Use hybrid semantic and keyword retrieval for materials | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 924 | 在左边选择一个系列，或者先新建一个。 | Select a series on the left, or create one first. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:33 |
| 925 | 在转写中查找（听错的写法也会找出来） | Search the transcript (misspelled forms are found too) | — | Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml |
| 926 | 已取消，已经记录的内容仍保存在本机。 | Cancelled; what was recorded is still stored on this device. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:241 |
| 927 | 已把「%@」当作上一场，准备下一场。 | Treated “%@” as the previous meeting; preparing the next one. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:211 |
| 928 | 已框选读屏区域：%lld×%lld。 | Screen-read area selected: %lld×%lld. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:334 |
| 929 | 引导已完成；之后可在设置中重新查看。 | The guide is complete; see it again from settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:175 |
| 930 | 更新于 %@ · 约每 45 秒一次 | Updated %@ · about every 45 seconds | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:91 |
| 931 | 条目已编辑；生成结果的其他内容未改。 | Item edited; the rest of the generated content is unchanged. | — | Windows/src/NextThought.Desktop/AppState.NotesEditor.cs:156 |
| 932 | 模板至少需要一个有标题和要求的章节。 | A template needs at least one section with a title and requirement. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:173 |
| 933 | 正在根据这一页和你的资料写讲稿提示… | Writing talking points for this page from the slide and your materials… | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 934 | 正确写法，例如 Kubernetes | Correct form, e.g. Kubernetes | — |  |
| 935 | 系统 %.0f · 麦克风 %.0f | System %.0f · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.Live.cs:294 |
| 936 | 译文未生成，原文已保留，可稍后补译： | Translation not generated; the original is kept, retry later:  | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:177 |
| 937 | 过期状态来自版本比较；旧内容仍可读。 | Stale state comes from revision comparison; old content stays readable. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 938 | 还没有导入 PPTX，导入后可翻页。 | No PPTX imported yet; import one to turn pages. | — | Windows/src/NextThought.Desktop/AppState.SlideFollow.cs:107 |
| 939 | 这是连通性测试。只回复两个字：收到。 | This is a connectivity test. Reply with two words only: received. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:122 |
| 940 | 这段时间你讲得偏多，留点空间给对方。 | You have spoken a lot lately; leave room for them. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:314 |
| 941 | 连接检查通过 · 用时 %.1f 秒 | Connection check passed · %.1f s | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:181 |
| 942 | 选择一份资料，查看本机解析出的文本。 | Choose a material to see the text parsed on this device. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:22 |
| 943 | 长时间没有新的发言，会议已自动结束。 | No new speech for a long time; the meeting ended automatically. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:120 |
| 944 | 领域字段（每行 key=value） | Domain fields (key=value per line) | — |  |
| 945 | ；Ctrl+Alt+N 唤回主窗口。 | ; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:673 |
| 946 | %lld 场会议 · 共 %lld 场 | %lld meetings · %lld total | — | Windows/src/NextThought.Desktop/AppState.cs:324 |
| 947 | %lld 条转写 · %lld 条回答 | %lld lines · %lld answers | — | Windows/src/NextThought.Desktop/AppState.Live.cs:11 |
| 948 | 保留查询与范围；可扩大范围或清除查询。 | Query and scope kept; widen the scope or clear the query. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 949 | 全文搜索：%lld 场会议有匹配内容。 | Full-text search: %lld meetings have matching content. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:108 |
| 950 | 切换鼠标穿透（穿透后只能用快捷键改回） | Toggle click-through (afterwards only a shortcut can restore clicks) | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml |
| 951 | 声纹模型已准备，可在设置中开启并保存。 | Speaker model ready; enable and save it in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:90 |
| 952 | 已保存 · 修订 12 · 03:05 | Saved · revision 12 · 03:05 | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 953 | 已删除 %lld 个系列及其会议记录。 | Deleted %lld series and their meetings. | — | Windows/src/NextThought.Desktop/AppState.Meeting.cs:365 |
| 954 | 已删除 %lld 场会议及其依赖内容。 | %lld meetings and their dependent content deleted. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:60 |
| 955 | 已替换这道题的回答，旧回答留在历史里。 | The answer was replaced; the old one stays in history. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:365 |
| 956 | 已识别 · 是否用于回答见“本次依据” | Recognised · see “Based on” for whether it was used | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 957 | 必需：选择服务商，粘贴 API Key | Required: choose a provider and paste an API key | 必需：选择服务商，粘贴 API Key | Windows/src/NextThought.Desktop/AppState.SetupChecklist.cs:19 |
| 958 | 所选系统输出设备混音 · 不采集麦克风 | Selected system output mix · microphone not captured | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:77 |
| 959 | 把还没有译文的完整句发送到当前回答服务 | Send untranslated complete sentences to the current answer service | — |  |
| 960 | 旧正文基于修订前原话；手动校正仍保留。 | The old body is based on the previous wording; manual corrections are kept. | — |  |
| 961 | 旧译文基于修订前原话；手动校正仍保留。 | The old translation follows the previous wording; manual corrections are kept. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 962 | 检查完即可开始；会中仍可随时切换场合。 | Start once the checks pass; switch scenes during the meeting too. | — |  |
| 963 | 正在记录。系统声音为所选输出设备混音。 | Recording. System audio is the chosen output mix. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:307 |
| 964 | 纪要已保存，可导出 Markdown。 | Notes saved; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:553 |
| 965 | 纪要模板：章节由模板决定，模型只填内容 | Note template: the template defines sections, the model fills them | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 966 | 线下会议只用麦克风，转写标为“现场”。 | Offline meetings use the microphone only; lines are marked as on-site. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:172 |
| 967 | 聆听已暂停；恢复后从当前时刻继续记录。 | Listening paused; resume from this point. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:227 |
| 968 | 说话人 %lld 已合并到 %lld。 | Speaker %lld merged into %lld. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:276 |
| 969 | 请先选择一个系列，或在全局作用域编辑。 | Choose a series first, or edit in the global scope. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:217 |
| 970 | %@ · %lld 段 · %.2f 秒 | %@ · %lld sections · %.2f s | — | Windows/src/NextThought.Desktop/AppState.Live.cs:554 |
| 971 | Codex 模型（留空沿用账号默认模型） | Codex model (empty uses the account default) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 972 | Ctrl+Alt+N 不挑场合，立即记录 | Ctrl+Alt+N records right away, whatever you are doing | — |  |
| 973 | NextThought · 把讨论留下来 | NextThought · Keep the discussion | — | Windows/src/NextThought.Desktop/TrayIntegration.cs:40 |
| 974 | WAV 录音 (*.wav)\|*.wav | WAV recording (*.wav)\|*.wav | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:46 |
| 975 | 云端识别启动失败，且本机语音模型未就绪： | Cloud transcription failed and the on-device speech model is not ready:  | — | Windows/src/NextThought.Desktop/AppState.Live.cs:270 |
| 976 | 例如：先说结论再说依据；数据都标注来源。 | For example: state the conclusion first, then the evidence; mark every source. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 977 | 保存受阻 · 有 3 项待保存 · 重试 | Saving is blocked · 3 pending · Retry | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 978 | 保留了原来的经验，只记下这次会议的来源。 | Kept the original lesson; only the meeting source was recorded. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:366 |
| 979 | 先选场合、补资料、做检查，再开始这场会。 | Pick a scene, add materials, run the checks, then start. | — |  |
| 980 | 全局快捷键（Ctrl+Alt + 按键） | Global shortcuts (Ctrl+Alt + key) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 981 | 删除会议「%@」？会议归档会从磁盘移除。 | Delete meeting “%@”? Its archive will be removed from disk. | — |  |
| 982 | 已导入模板，共 %lld 个自定义模板。 | Imported %lld custom templates. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:117 |
| 983 | 已录入（来自旧模型的声纹，需要重新录入） | Enrolled (voiceprint from an old model; enroll again) | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:96 |
| 984 | 已把 %lld 条决定/行动加入待复核。 | Added %lld decisions/actions to the review list. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:131 |
| 985 | 已把「%@」从「%@」移到系列「%@」。 | Moved “%@” from “%@” to series “%@”. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:496 |
| 986 | 已收集 %lld 条知识候选，等待复核。 | Collected %lld knowledge candidates for review. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:259 |
| 987 | 截图与 OCR 文本已就绪；可直接发送。 | Screenshot and OCR text ready; send it directly. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:476 |
| 988 | 截图已就绪；可直接发送，或补充你的问题。 | Screenshot ready; send it directly or add your question. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:476 |
| 989 | 正在联网核实（使用 %@ 的查证结果）… | Verifying online (using %@ verification results)… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:570 |
| 990 | 讲稿在左 · 回答在右（演讲 / 答辩） | Script left · answers right (talk / defense) | — |  |
| 991 | 还没有幻灯片；导入 PPTX 后可翻页。 | No slides yet; import a PPTX to turn pages. | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:170 |
| 992 | 还没有术语；添加一个正确写法和常见误听。 | No glossary entries; add a correct form and common mishearing. | — |  |
| 993 | 麦克风增益（1–8 倍，声音偏小时调高） | Microphone gain (1–8×, raise for quiet audio) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 994 | # %@⏎⏎会议时间：%@ · 场景：%@ | # %@

Meeting time: %@ · Scene: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:400 |
| 995 | # %@⏎⏎会议时间：%@ · 模板：%@ | # %@

Meeting time: %@ · Template: %@ | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:202 |
| 996 | X-ASR 中英 · 480 ms（推荐） | X-ASR bilingual · 480 ms (recommended) | X-ASR 中英 · 480 ms（推荐） | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:46 |
| 997 | 保存后立即应用；跟随系统会随后台主题切换。 | Applied after saving; Follow system tracks the OS theme. | — |  |
| 998 | 切换为只记录：不再自动回答，仍可手动提问。 | Switched to record-only: no automatic answers, but you can still ask by hand. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 999 | 原凭据无法由当前用户解密，请重新输入密钥。 | The current user cannot decrypt the stored credential; enter the key again. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:772 |
| 1000 | 原始音频不保存；转写与回答保存在「记录」里 | Raw audio is not kept; the transcript and answers stay under Records. | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1001 | 回答不超过三句话：先给结论，再给一条依据。 | Answer in at most three sentences: conclusion first, then one reason. | — |  |
| 1002 | 已保存 %lld 个技能，下一场回答生效。 | Saved %lld skills; used in the next answer. | — | Windows/src/NextThought.Desktop/AppState.Skills.cs:63 |
| 1003 | 已保存（当前 Windows 用户加密）。 | Saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:344 |
| 1004 | 已开启实时纪要：大约每 45 秒整理一次。 | Live notes are on: they update about every 45 seconds. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:151 |
| 1005 | 录音已通过云端转写并归档，未保存原始音频。 | Recording transcribed by the cloud and archived; raw audio was not kept. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:429 |
| 1006 | 技能可用于所有系列，或在系列中按系列选择。 | Skills can apply to all series, or be chosen per series. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1007 | 整理结果不是可用的 JSON，稍后可重试。 | The result was not usable JSON; try again later. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:408 |
| 1008 | 正在检索相关资料；首次使用时生成本地索引… | Searching materials; the local index is built on first use… | — | Windows/src/NextThought.Desktop/AppState.Live.cs:550 |
| 1009 | 演示模式：内置样本数据，不会写入你的资料。 | Demo mode: built-in sample data, nothing is written to your materials. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:116 |
| 1010 | 记录时将完整句中英互译（使用当前回答服务） | Translate full sentences while recording (uses the answer service) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1011 | 请解释这张截图，指出关键信息、数字或风险。 | Explain this screenshot and point out key facts, numbers or risks. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:474 |
| 1012 | 转写已在界面更正，但用户更正记录写入失败： | The transcript was corrected on screen, but writing the correction record failed:  | — | Windows/src/NextThought.Desktop/AppState.Proofread.cs:77 |
| 1013 | 还不能开始记录：请先在设置里准备语音组件。 | Cannot start recording yet: prepare the speech components in settings. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:345 |
| 1014 | 采纳后进入这个系列的经验，下次回答会带上它 | Accepted lessons join this series and are used in later answers | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:240 |
| 1015 | 预览态由演示环境注入，不代表鼠标事件录制。 | Preview state is injected by the demo; it does not record pointer events. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1016 |  已保存 · 修订 12 · 03:05 |  Saved · revision 12 · 03:05 | — |  |
| 1017 |  旧正文基于修订前原话；手动校正仍保留。 |  The old body is based on the previous wording; manual corrections are kept. | — |  |
| 1018 | ，其中 %lld 条按文字长度估算结束时间 | , %lld with estimated end times | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:358 |
| 1019 | %lld 场 · %@ · %lld 份资料 | %lld meetings · %@ · %lld files | — | Windows/src/NextThought.Desktop/AppState.Home.cs:117 |
| 1020 | 共 %lld 份资料，均在本机解析，不上传。 | %lld materials, all parsed on this device, never uploaded. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:42 |
| 1021 | 切换为自动回答：识别到问题后会生成参考回答。 | Switched to automatic answers: a reference answer is generated when a question is recognized. | — | Windows/src/NextThought.Desktop/AppState.LiveUi.cs:83 |
| 1022 | 外接屏已断开，双屏演讲已退出，主窗口已恢复。 | External display disconnected; presenter mode exited; main window restored. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:647 |
| 1023 | 如果发布窗口需要延长，我们会先核对回滚指标。 | If the release window needs more time, we will check the rollback metrics first. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1024 | 密钥已保存（当前 Windows 用户加密） | Key saved (encrypted for the current Windows user) | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:766 |
| 1025 | 已删除会议「%@」，已确认的系列内容已保留。 | Deleted meeting “%@”; accepted series content kept. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:139 |
| 1026 | 已按《%@》整理，可导出 Markdown。 | Organized with “%@”; Markdown export is available. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:251 |
| 1027 | 把资料加入系列请在资料库中选择；这里可移除。 | Add materials to the series from the library; remove them here. | — |  |
| 1028 | 术语表已保存 %lld 条，下一场会议生效。 | Glossary saved (%lld terms); used in the next meeting. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:484 |
| 1029 | 没有符合条件的记录，可以清除条件后查看全部。 | No records match the filters; clear them to see everything. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml |
| 1030 | 系统声音与麦克风分别处理；只在本机保存文字。 | System audio and microphone are handled separately; only text is stored locally. | — |  |
| 1031 | 还没有会议记录。点击「开始记录」写下第一场。 | No meetings yet. Choose Start recording to write the first one. | — |  |
| 1032 | 鼠标移到提词岛上，可以暂停、结束或立即回答。 | Hover the cue island to pause, end or answer now. | — | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1033 |  保存受阻 · 有 3 项待保存 · 重试 |  Saving is blocked · 3 pending · Retry | — |  |
| 1034 | Word 文档 (*.docx)\|*.docx | Word document (*.docx)\|*.docx | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:169 |
| 1035 | 修改保存在独立用户版本里；原话依据只用于核对。 | Edits are kept in a separate user version; the original wording is only used for checking. | — | Windows/src/NextThought.Desktop/Views/NotesEditorWindow.axaml |
| 1036 | 浮在屏幕上的大字提词窗（Ctrl+Alt+T） | Large teleprompter over the screen (Ctrl+Alt+T) | — |  |
| 1037 | 还没有可用的资料：先导入资料，再整理讨论重点。 | No usable materials yet: import materials first, then prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:78 |
| 1038 | 这次没有新的经验和问题：之前整理过同一份纪要。 | No new lessons or questions: these notes were already organized. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:338 |
| 1039 | 允许联网核实（需 TAVILY_API_KEY） | Allow web verification (needs TAVILY_API_KEY) | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1040 | 如果时间允许，我想再补充一下数据和风险这两部分。 | If time allows, I would like to add the data and risk parts. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:17 |
| 1041 | 已取消「这是我」，这场会议里不会再识别为该声音。 | “This is me” cancelled; this voice is no longer recognized as you in this meeting. | — | Windows/src/NextThought.Desktop/AppState.Speakers.cs:138 |
| 1042 | 已整理重复项（%lld 条 → %lld 条）。 | Merged duplicates (%lld entries → %lld entries). | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:341 |
| 1043 | 已跳过 %lld 行损坏记录，其余内容仍可查看。 | Skipped %lld damaged lines; the rest is still readable. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:473 |
| 1044 | 手机伴侣已开启；手机扫码即可查看当前问题与回答。 | Phone companion is on; scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 1045 | 点「开始记录」就能开会；也可以先到准备页补资料。 | Choose Start recording to begin; add materials on the prepare page first if you like. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:35 |
| 1046 | 联网未配置：先在设置里保存 Tavily 密钥。 | Web search is not configured: save a Tavily key in settings. | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:72 |
| 1047 | 记下复核结论：哪些页需要人工核对、是否补齐原件。 | Record the review result: which pages need a human check and whether the original is missing. | — | Windows/src/NextThought.Desktop/Views/MaterialReviewWindow.axaml |
| 1048 | 请先选择一个局域网地址，或在设置里填写中转地址。 | Choose a LAN address, or set a relay URL in settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:137 |
| 1049 | 转写会显示在这里。也可以导入录音 WAV 验证。 | The transcript appears here. You can also import a WAV recording to try it. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1050 | 这个系列还没有自己的术语；全局条目仍会叠加生效。 | This series has no terms of its own yet; global entries still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:75 |
| 1051 | 这是这个系列的下一场：会自动带上它的资料与经验。 | This is the next meeting of this series: its materials and lessons are carried in automatically. | — |  |
| 1052 | %@ · %@ · %lld 段 · 解析器 %@ | %@ · %@ · %lld sections · parser %@ | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:95 |
| 1053 | NextThought 备份\|*.ntbackup | NextThought backup\|*.ntbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:206 |
| 1054 | 先准备声纹组件（约 26 MB），再录入我的声音。 | Prepare the speaker component (about 26 MB), then enroll my voice. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:107 |
| 1055 | 勾选的技能只加入这个系列的回答；全局技能仍然生效。 | Checked skills apply to this series only; global skills still apply. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1056 | 双屏演讲：%@；Ctrl+Alt+N 唤回主窗口。 | Presenter mode: %@; Ctrl+Alt+N brings the main window back. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:707 |
| 1057 | 左侧灰色为对方（系统声音），右侧彩色为我（麦克风） | Grey on the left is the other side (system audio), colour on the right is me (microphone) | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1058 | 已删除 %lld 场会议；已确认的系列内容已保留。 | %lld meetings deleted; accepted series content kept. | — | Windows/src/NextThought.Desktop/AppState.Deletion.cs:59 |
| 1059 | 所选系统输出设备混音 + 麦克风 · 在设置中调整 | Selected system output mix + microphone · adjust in settings | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:76 |
| 1060 | 正在按《%@》整理…（第 %lld/%lld 段） | Organizing with “%@”… (part %lld of %lld) | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:221 |
| 1061 | 此数据目录已有 NextThought 窗口运行。 | A NextThought window is already using this data folder. | — | Windows/src/NextThought.Desktop/App.axaml.cs:51 |
| 1062 | 没有匹配的会议。换个关键词，或把筛选切回「全部」。 | No matching meetings. Try another keyword or switch the filter back to All. | — |  |
| 1063 | 结束会议后，按转写整理总结、决定、待办和未决问题。 | After a meeting, organize the transcript into a summary, decisions, actions and open questions. | — | Windows/src/NextThought.Desktop/AppState.cs:288 |
| 1064 | 语义检索模型已准备，勾选语义检索并保存设置后使用。 | Semantic model ready; enable semantic retrieval and save settings. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:98 |
| 1065 | 语音已就绪。可以开始记录，或先导入 WAV 验证。 | Speech is ready. Start recording, or import a WAV to try it. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:64 |
| 1066 | 还没有系列。上面的输入框新建一个，再把会议放进来。 | No series yet. Create one above, then add meetings to it. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1067 | 随手记；保存到 user-content.json | Jot a note; saved to user-content.json | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1068 | 鼠标穿透中 · 快捷键 Ctrl+Alt+T 切回 | Click-through on · Ctrl+Alt+T restores clicks | — | Windows/src/NextThought.Desktop/Views/TeleprompterWindow.axaml.cs:253 |
| 1069 | %lld 秒后自动结束；说话或点「继续记录」可取消。 | %lld seconds until auto-end; speaking or Keep recording cancels it. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:129 |
| 1070 | 之前有效回答仍可读。取消后到达的旧请求结果会被丢弃。 | Earlier answers stay readable. Results from cancelled requests are discarded. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1071 | 先到设置准备语音组件；未连接回答服务时也可以只记录。 | Prepare the speech components in settings first; recording alone works without an answer service. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:65 |
| 1072 | 删除选中的 %lld 场会议？此操作会移除归档目录。 | Delete %lld selected meetings? Their archive folders will be removed. | — |  |
| 1073 | 快捷键行要写成 动作=按键，例如 record=R。 | Write one action=key per line, for example record=R. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:192 |
| 1074 | 时长按每场从开始记录到结束计算；暂停的时间也算在内。 | Recording time runs from the start to the end of each meeting; pauses count too. | 时长按每场从开始记录到结束计算；暂停的时间也算在内。 | Windows/src/NextThought.Desktop/Views/Home/HomeStats.axaml |
| 1075 | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | This meeting uses %lld ready materials; readable text is enough to start. | 本场使用 %lld 份已就绪资料；文字可用即可开始。 | Windows/src/NextThought.Desktop/AppState.Prepare.cs:87 |
| 1076 | 检测到屏幕共享，提示岛保持收回；共享结束后自动恢复。 | Screen sharing detected; the cue island stays retracted and returns when sharing ends. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:531 |
| 1077 | 窗口“%@”已关闭或无法捕获，请刷新窗口列表后重试。 | The window “%@” was closed or cannot be captured; refresh the window list and try again. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:251 |
| 1078 | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | The questions asked, your answers and what to prepare afterwards stay here. | 被问到的题、你的回答和会后要补的地方会在这里留下来。 | Windows/src/NextThought.Desktop/Views/OnboardingView.axaml |
| 1079 | 设置已保存，密钥已用当前 Windows 用户加密。 | Settings saved; the key is encrypted for the current Windows user. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:60 |
| 1080 | 这是「术语和人名」里记下的听错写法，搜索时一并找出来 | A misspelling saved under Terms and names; search finds it too | — | Windows/src/NextThought.Desktop/GlossaryRows.cs:12 |
| 1081 | 需要最新信息时联网核实回答；不联网时只用资料和转写。 | Look the answer up on the web when up-to-date information is needed; offline uses only materials and the transcript. | — |  |
| 1082 | ；%lld 个重复项保留现有版本。导入前备份：%@。 | ; %lld duplicates kept the existing versions. Pre-import backup: %@. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:232 |
| 1083 | 合成版本差异样例；实际过期标记由归档投影版本比较产生。 | Synthetic revision-diff sample; real stale markers come from archive projection comparison. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1084 | 命名后应用到当前转写；也可以把某一声道标成「这是我」。 | Names apply to the current transcript; you can also mark a channel as “Me”. | — | Windows/src/NextThought.Desktop/Views/SpeakerEditorWindow.axaml |
| 1085 | 密钥读取失败，已保留原密钥；如需更换请直接输入新密钥。 | Reading the key failed; the existing key was kept. Enter a new key to replace it. | — | Windows/src/NextThought.Desktop/ViewModels/SettingsViewModel.cs:778 |
| 1086 | 已整理成%@；%lld 条找不到原话依据的内容已丢弃。 | Organized into %@; dropped %lld items with no transcript support. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:480 |
| 1087 | 提词浮窗已开启鼠标穿透，Ctrl+Alt+J 可恢复。 | Teleprompter click-through is on; Ctrl+Alt+J restores clicks. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:493 |
| 1088 | 还没有条目。点「新增」，或用「批量粘贴」一次加入多条。 | No entries yet. Choose New, or use Bulk paste to add several at once. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:56 |
| 1089 | 这个问题我的理解是，先确认需求，再决定用哪种方案去做。 | My understanding is to confirm the need first, then decide on an approach. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:15 |
| 1090 | 选择系统输出混音或麦克风，并在设置里准备本机语音模型。 | Choose the system output mix or the microphone, and prepare the on-device speech model in settings. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:33 |
| 1091 | 音频只在本机识别，不保存录音；文字与回答只保存在本机。 | Audio is recognized on this device only and no recording is kept; text and answers stay on this device. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1092 | 会前准备：这个系列已有 %lld 份资料，均在本机解析。 | Preparation: this series already has %lld materials, all parsed on this device. | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:313 |
| 1093 | 兼容 Chat Completions 的 API 地址 | Chat Completions-compatible API URL | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1094 | 删除当前筛选的 %lld 场会议？此操作会移除归档目录。 | Delete the %lld filtered meetings? Their archive folders will be removed. | — |  |
| 1095 | 回答过的问题会按时间列在这里，点一下就能回看当时的回答。 | Answered questions are listed here by time; choose one to see the answer as it was. | — | Windows/src/NextThought.Desktop/Views/Assistant/AnswerHistoryPanel.axaml |
| 1096 | 归档有缺失或读取错误（跳过 %lld 行），导出已暂停。 | The archive has missing or unreadable rows (%lld lines skipped); the export is paused. | — | Windows/src/NextThought.Desktop/AppState.SessionExport.cs:159 |
| 1097 | 纪要整理已取消；原始转写和已完成片段已保留，可继续整理。 | Note generation cancelled; the transcript and finished parts were kept. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:558 |
| 1098 | 这些字段会进入这个系列的准备页和回答提示；留空表示不用。 | These fields feed this series' prepare page and answer prompt; leave them empty to skip. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1099 | 删除系列“%@”？系列及其会议记录将被永久删除，无法恢复。 | Delete series “%@”? The series and its meeting records will be permanently deleted and cannot be recovered. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:205 |
| 1100 | 对方 · 系统声音 %.0f · 我 · 麦克风 %.0f | Them · system audio %.0f · Me · microphone %.0f | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:147 |
| 1101 | 框选屏幕区域，存入资料库并 OCR（Ctrl+Alt+S） | Select a screen area, save it to the library and OCR it (Ctrl+Alt+S) | — |  |
| 1102 | 没有找到可截图的窗口。请先打开要截图的窗口，再点「刷新」。 | No window to capture was found. Open the window first, then choose Refresh. | — | Windows/src/NextThought.Desktop/Views/WindowPickerWindow.axaml.cs:27 |
| 1103 | 自检：安装包已下载并通过 SHA-256，启动命令已记录。 | Self-test: installer downloaded and SHA-256 verified; launch command recorded. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:286 |
| 1104 | 还没有配置回答服务；整理讨论重点需要它，记录和转写不需要。 | No answer service is configured yet; preparing discussion points needs one, recording and transcription do not. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:42 |
| 1105 | Tavily 密钥已保存（当前 Windows 用户加密）。 | Tavily key saved (encrypted for the current Windows user). | — | Windows/src/NextThought.Desktop/AppState.Lifecycle.cs:66 |
| 1106 | 今天上午我们先把上周的进展过一遍，然后讨论接下来两周的安排。 | This morning let us review last week's progress, then plan the next two weeks. | — | Windows/src/NextThought.Desktop/AppState.MyVoice.cs:14 |
| 1107 | 对着麦克风读一句话，观察下方电平。开始记录后这里会实时变化。 | Read a sentence into the microphone and watch the level below; it changes once recording starts. | — | Windows/src/NextThought.Desktop/Views/OnboardingVoiceWindow.axaml |
| 1108 | 已处理 %lld 场中断会议；可在历史中继续查看或整理纪要。 | Recovered %lld interrupted meetings; view or organize them in history. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:121 |
| 1109 | 已导入 %lld 场会议、%lld 份资料、%lld 个系列 | Imported %lld meetings, %lld materials, %lld series | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:230 |
| 1110 | 手机伴侣已开启（中转通道）；手机扫码即可查看当前问题与回答。 | Phone companion is on (relay); scan with your phone to see the current question and answer. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:152 |
| 1111 | 补译已结束；有译文的句子已分别保存。未成功的句子可再次补译。 | Translation finished; translated sentences were saved. Retry the ones that failed. | — | Windows/src/NextThought.Desktop/AppState.Live.cs:457 |
| 1112 | 识别为问题的句子会标出；把鼠标移到任意一句上可以「回答这句」 | Sentences recognized as questions are marked; rest the pointer on any line to “Answer this” | — | Windows/src/NextThought.Desktop/Views/Assistant/EmptyConversationHint.axaml |
| 1113 | 还没有记录。从这个系列开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:234 |
| 1114 | 这次回复不是可用的 JSON，可以重试；原来的纪要保持不变。 | The reply was not usable JSON; retry. The previous notes are unchanged. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:234 |
| 1115 | 中转已连接 · 端到端加密。配对链接走中转，不必在同一局域网。 | Relay connected · end-to-end encrypted. Pairing links use the relay, so no shared LAN is needed. | — | Windows/src/NextThought.Desktop/AppState.StructuredNotes.cs:565 |
| 1116 | 最近结果：先看命中率和 P95 延迟，再比较灰度前后的错误率。 | Latest results: check hit rate and P95 latency, then compare error rates before and after the rollout. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1117 | 还没有可用的语音识别；可以先只记录声音，或到设置准备语音组件。 | No speech recognition is ready yet; record the sound only for now, or set up the speech components. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:189 |
| 1118 | 还没有记录。从这个资料夹开始一场会议，记录和资料会保存在一起。 | No meetings yet. Start one from this series and its notes and materials stay together. | — |  |
| 1119 | 这场只记录对方的声音；想连自己的话一起记，可以重新打开麦克风。 | This meeting records the other side only; reopen the microphone to include your own voice. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:320 |
| 1120 | 这条建议对不上了：原话或已有内容在复核之后改过，刷新后再确认。 | This suggestion no longer matches: the original or existing content changed after review. Refresh and confirm again. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:103 |
| 1121 | NextThought 把会议讨论留在本机，变成可以带走的结论。 | NextThought keeps meeting discussions on this device and turns them into conclusions you can take away. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1122 | 「%@」有 %lld 条经验和问题待确认，确认后才会进入知识库。 | “%@” has %lld lessons and questions to confirm; nothing enters the knowledge base until you confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:345 |
| 1123 | 系列术语只叠加在这个系列的回答、纪要与查找里；全局术语仍然生效。 | Series terms only layer onto this series' answers, notes and search; global terms still apply. | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:74 |
| 1124 | 还没有术语。每行一个：正确写法 \| 识别错的写法 \| 另一种错法 | No glossary entries. One per line: correct form \| misheard form \| another mishearing | — |  |
| 1125 | 仅保留会议内编号，不保存声纹或原始音频。短句和不确定结果保持未知。 | Only in-meeting numbers are kept; voiceprints and raw audio are not stored. Short or uncertain results stay unknown. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1126 | 整理出的%@缺少可核对的转写依据，已丢弃；可重试，或改用文字纪要。 | The %@ lacked verifiable transcript support and was dropped; retry, or use text notes instead. | — | Windows/src/NextThought.Desktop/AppState.Notes.cs:415 |
| 1127 | 转写保存在本机。使用远程回答服务时，问题和相关转写会发送到该服务。 | Transcripts stay on this device. With a remote answer service, the question and relevant transcript are sent to that service. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1128 | 这条建议对不上了：题库里的回答在整理之后改过，重新整理一次再确认。 | This suggestion no longer matches: the question-bank answer changed after organizing. Organize again, then confirm. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:355 |
| 1129 | 题库里已有相似问题时，「替换」会用新回答覆盖旧的，旧回答留在历史里 | If a similar question exists, Replace overwrites its answer; the old one stays in history | — |  |
| 1130 | 个人笔记保存在 user-content.json，不覆盖生成纪要。 | Personal notes are saved to user-content.json and never overwrite generated notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1131 | 按这场会议的转写整理总结、决定、待办和未决问题；原始文字不会被改写。 | Summarize decisions, actions and open questions from the transcript; the original text is not rewritten. | — |  |
| 1132 | 文字纪要由模板整理；鱼骨图、大纲与观点对照是同一场会议的另一种看法。 | Text notes come from a template; fishbone, outline and viewpoint views are other ways to read the same meeting. | — | Windows/src/NextThought.Desktop/AppState.cs:356 |
| 1133 | 正在安装更新，NextThought 会退出并在安装完成后重新打开。 | Installing the update; NextThought will exit and reopen when it finishes. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:310 |
| 1134 | 添加正确写法与常见误听；识别时作为热词，转写中的错写会改回正确写法。 | Add correct forms and common mishearings; correct forms become hotwords and misheard forms are rewritten. | — |  |
| 1135 | 题库还空着。面试、汇报这类模板的「逐题回放」章节会在确认后进入题库。 | The question bank is empty. Question-by-question sections from interview or talk templates enter the bank after confirmation. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1136 | %lld 条术语 · 识别时作为热词，转写里出现的错写会被改回正确写法 | %lld terms · used as recognition hotwords; misheard forms are rewritten | — | Windows/src/NextThought.Desktop/AppState.Glossary.cs:81 |
| 1137 | 只更正名字与术语的识别错误，改动写入 corrections.json | Only fixes misrecognized names and terms; changes are written to corrections.json | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1138 | 安装包已在后台下载并通过 SHA-256 校验；点「重启以更新」安装。 | The installer was downloaded and verified with SHA-256; choose Restart to update to install. | — | Windows/src/NextThought.Desktop/AppState.Update.cs:216 |
| 1139 | 快捷键必须包含 Ctrl、Alt 或 Shift，不能只使用普通按键。 | A shortcut must include Ctrl, Alt or Shift; a plain key is not enough. | — | Windows/src/NextThought.Desktop/AppState.Audio.cs:197 |
| 1140 | 所有窗口已收起，会议继续记录。Ctrl+Alt+\ 或托盘图标可恢复。 | All windows hidden; the meeting keeps recording. Ctrl+Alt+\ or the tray icon restores them. | — | Windows/src/NextThought.Desktop/Views/MainWindow.Shell.cs:155 |
| 1141 | 把同一门课、同一个项目、同一类面试的会议放在一个系列里，经验会攒下来。 | Keep meetings from one course, project or interview loop in a series; lessons accumulate. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:29 |
| 1142 | 按下带 Ctrl/Alt/Shift/Win 的组合键；Esc 取消。 | Press a combination with Ctrl/Alt/Shift/Win; Esc cancels. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml.cs:435 |
| 1143 | 每一场会议都存在本机。搜索标题、日期或模式，看不清的细节可以回到原话。 | Every meeting stays on this device. Search by title, date or mode, and return to the exact words when details are unclear. | — |  |
| 1144 | 没有找到可用的局域网 IPv4 地址；请先连接 Wi-Fi 或以太网。 | No usable LAN IPv4 address; connect Wi-Fi or Ethernet first. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:116 |
| 1145 | 生成结果保持不变；这里的修改写入 user-content.json。 | Generated content stays unchanged; edits here are written to user-content.json. | — |  |
| 1146 | 说上几句之后，这里会出现这场会的要点：在谈什么、定了什么、谁要做什么。 | After a few sentences this will show the meeting so far: what is being discussed, what was decided, who is doing what. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:108 |
| 1147 | 还没有高亮。在转写里点「高亮」记下一句，会把原话时间一起存进个人笔记。 | No highlights yet. Choose Highlight in the transcript to keep that line, with its original timestamp, in your personal notes. | — | Windows/src/NextThought.Desktop/Views/MeetingUserNotes.axaml |
| 1148 | 这场会只保存转写与纪要，不自动生成回答。需要回答时可随时在输入框提问。 | This meeting only saves the transcript and notes; answers are not generated automatically. Ask in the input box whenever you need one. | — |  |
| 1149 | 选中文字荧光为合成标记；原生 textSelection 高亮未伪造。 | The text fluorescence is a synthetic marker; native textSelection highlighting is not faked. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1150 | 高亮与这场的原话关联；保存后写入 user-content.json。 | Highlights link to this meeting's original lines; saving writes them to user-content.json. | — | Windows/src/NextThought.Desktop/Views/LiveView.axaml |
| 1151 | NextThought 备份\|*.ntbackup;*.ihbackup | NextThought backup\|*.ntbackup;*.ihbackup | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:219 |
| 1152 | 备份已导出：包含会议与资料；不包含 API Key、语音模型和设备设置。 | Backup exported: contains meetings and materials; excludes API keys, speech models and device settings. | — | Windows/src/NextThought.Desktop/AppState.Archive.cs:211 |
| 1153 | 将调用回答服务把转写整理成「%@」；每条都带原话时间，找不到依据的不写。 | The answer service will organize the transcript into “%@”; every entry carries its original time and unsupported items are dropped. | — | Windows/src/NextThought.Desktop/AppState.cs:357 |
| 1154 | 还没有配置回答服务：到 设置 → 回答 填写 API Key 后再检查。 | No answer service is configured yet: fill in an API key under Settings → Answers, then check again. | — | Windows/src/NextThought.Desktop/AppState.Models.cs:103 |
| 1155 | X-ASR 流式识别加整句精修，CPU 离线运行；原始音频只在内存中经过。 | X-ASR streaming plus sentence refinement runs offline on CPU; raw audio only passes through memory. | — |  |
| 1156 | 会中的回答和会后的纪要都由它来写。选一个服务商，粘贴 Key，点一下检查。 | It writes the in-meeting answers and the meeting notes. Pick a provider, paste a key and check. | — |  |
| 1157 | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Answers automatically · concise answers · notes: needs, concerns and replies, what we promised, next steps | 会中自动回答 · 回答简洁 · 纪要：需求、顾虑与回应、我们答应的、下一步 | Windows/src/NextThought.Desktop/MeetingDraft.cs:64 |
| 1158 | 会话 %lld · 问题 %lld · 经验 %lld · 题库 %lld | sessions %lld · questions %lld · lessons %lld · bank %lld | — | Windows/src/NextThought.Desktop/AppState.SeriesEditor.cs:119 |
| 1159 | 共 %lld 场 · %@ · 提问 %lld 次 · 回答 %lld 次 | %lld meetings · %@ · %lld questions · %lld answers | — |  |
| 1160 | 决定与行动先留在这里复核；负责人和期限可在这里确认，确认后才进入系列记录。 | Decisions and actions wait here for review; owner and due date can be confirmed here before they enter the series record. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:79 |
| 1161 | 这场会议还不属于任何系列。到「系列」页把它加进去，之后整理纪要就会攒经验。 | This meeting is not in a series. Add it on the Series page to accumulate lessons when notes are generated. | — | Windows/src/NextThought.Desktop/AppState.Series.cs:332 |
| 1162 | 这场没有开实时纪要。结束后仍会整理完整纪要；想边开边看要点，点右上角开启。 | Live notes are off for this meeting. Full notes are still written at the end; to see key points as you go, turn it on in the top right. | — | Windows/src/NextThought.Desktop/AppState.LiveNotes.cs:109 |
| 1163 | 首页的场景会决定默认的会中模式、回答风格、纪要模板与提词方式。随时可以改。 | The Home scene sets the default in-meeting mode, answer style, note template and cue style. You can change it anytime. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:32 |
| 1164 | 选云端后，导入 WAV 走云端识别；实时记录仍用本机。音频会发送到所选服务。 | With cloud selected, WAV import uses the cloud; live recording stays on-device. Audio is sent to the chosen service. | — |  |
| 1165 | 已在 %@:%lld 监听 · 配对链接相当于临时遥控密钥，只在可信网络使用。 | Listening on %@:%lld · the pairing link is a temporary remote key, so use it only on trusted networks. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:146 |
| 1166 | ## 客户会⏎### 决定⏎只写已经拍板的事。⏎### 待办⏎写清负责人和日期。 | ## Client meeting
### Decisions
Only write what was decided.
### Actions
State the owner and the date. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1167 | 切换后重启生效；当前覆盖主导航、设置分节与常用按钮，动态状态和会议内容保持原文。 | Takes effect after a restart; covers navigation, settings sections and common buttons. Status text and meeting content stay in their original language. | — |  |
| 1168 | 同一门课、同一个项目、同一类面试放在一个系列里；每场会议的结论与经验会攒在一起。 | Keep one course, project or interview loop in a series; conclusions and lessons accumulate across meetings. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1169 | 开会时把提词岛留在摄像头这一屏，幻灯片屏不出现任何 NextThought 窗口 | Keep the cue island on the camera display; no NextThought window appears on the slides display | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1170 | 接入兼容 Chat Completions 的回答服务；只记录与转写时可以不填。 | Connect a Chat Completions-compatible answer service; leave it empty to just record and transcribe. | — | Windows/src/NextThought.Desktop/AppState.Onboarding.cs:34 |
| 1171 | 没有待确认的知识。按模板整理一份纪要后，模板里标了「带出经验」的章节会排到这里。 | Nothing to confirm. After organizing notes, sections marked to carry lessons appear here. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1172 | 还没有配置回答服务：到 设置 → 回答 填好 API Key 后可整理讨论重点。 | No answer service is configured yet: fill in an API key under Settings → Answers to prepare discussion points. | — | Windows/src/NextThought.Desktop/AppState.PrepareInsight.cs:71 |
| 1173 | 题库里已有相似问题时，「采用建议并保留旧版」会用新回答覆盖旧的，旧回答留在历史里 | When the question bank already has a similar question, “Apply and keep the old version” replaces it with the new answer; the old answer stays in history. | — | Windows/src/NextThought.Desktop/RecentMeetingRow.cs:239 |
| 1174 | 首次需下载流式模型约 134 MB；整句精修另约 136 MB，下载后离线运行。 | The streaming model is about 134 MB; sentence refinement adds about 136 MB. Both run offline after download. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1175 | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Answers automatically · concise answers · notes: review each question (saved to the question bank), what went poorly, next round | 会中自动回答 · 回答简洁 · 纪要：逐题复盘（存进题库）、说得不好的地方、下一轮 | Windows/src/NextThought.Desktop/MeetingDraft.cs:60 |
| 1176 | 还没有资料。导入 PDF、Word、PPT、Excel、Markdown 或图片。 | No materials yet. Import PDF, Word, PPT, Excel, Markdown or images. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:41 |
| 1177 | 系列按修改时间合并；应用前会先创建本机恢复点。模板和个人笔记属于数据，始终参与恢复。 | Series are merged by modification time. A local restore point is created before anything is applied. Templates and personal notes are data and are always restored. | — | Windows/src/NextThought.Desktop/Views/BackupRestoreReviewView.axaml |
| 1178 | 原生控件样板 · 所有文本与状态均为合成演示，不访问麦克风、ASR、模型、网络或归档。 | Native control samples · all text and states are synthetic; no microphone, ASR, model, network or archive access. | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1179 | 开会、听讲、面谈。NextThought 在本机把声音变成文字，再变成可以带走的结论。 | Meetings, lectures, interviews. NextThought turns speech into text on this device, then into conclusions you can take away. | — |  |
| 1180 | 还没有经验。整理纪要时会从「下次改进」这类章节提炼候选，确认后才会记下来；也可以手写一条。 | No lessons yet. When notes are organized, sections such as Next time produce candidates; nothing is saved until you confirm, and you can also write one. | — | Windows/src/NextThought.Desktop/Views/SeriesView.axaml |
| 1181 | 已捕获 %lld×%lld 像素（%lld,%lld）。拖动选择区域，然后点「使用此区域」。 | Captured %lld×%lld pixels (%lld,%lld). Drag to select an area, then choose Use this area. | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml.cs:29 |
| 1182 | 重新整理这份纪要？这场会议的纪要你改过。重新整理会生成新的 AI 版本，你改过的版本会保留。 | Regenerate these notes? You edited this meeting's notes. Regenerating creates a new AI version and keeps your edited version. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml.cs:160 |
| 1183 | 保存设置后立即生效；当前覆盖主导航、设置分节、常用按钮与说明文案，动态状态和会议内容保持原文。 | Takes effect immediately after saving; covers navigation, settings sections, common buttons and help text. Status and meeting content stay in their original language. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1184 | 密钥只保存在当前 Windows 用户；也可以继续用 TAVILY_API_KEY 环境变量。 | The key is stored only for the current Windows user; TAVILY_API_KEY still works too. | — | Windows/src/NextThought.Desktop/AppState.SettingsActions.cs:29 |
| 1185 | 导入 PPT、Keynote 或 PDF 后，放映时认出正在讲第几页，给出这一页的要点与提问。 | Import PPT, Keynote or PDF and the app recognizes the current slide while presenting, offering key points and questions for it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1186 | 截图会裁切到选中范围，然后存入资料库并由系统 OCR 读取文字。请把题目、代码或公式完整框入。 | The screenshot is cropped to the selection, saved to the library and read with system OCR. Frame the question, code or formula completely. | — | Windows/src/NextThought.Desktop/Views/RegionCaptureWindow.axaml |
| 1187 | 把 PDF、Word、PPT、Excel、Markdown 或图片拖到这里，或点「导入资料」。 | Drop PDF, Word, PPT, Excel, Markdown or images here, or choose Import materials. | — | Windows/src/NextThought.Desktop/AppState.PrepareDraft.cs:234 |
| 1188 | 还没有生成这种纪要。点上方「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This kind of note has not been generated yet. Choose Generate this note above to turn the transcript into it with the current answer service; every line keeps its original timestamp. | — | Windows/src/NextThought.Desktop/Views/NotesView.axaml |
| 1189 | Windows 没有系统翻译框架：开启后完整句会发送到所选回答服务，与 Mac 的本机翻译不同。 | Windows has no system translation framework: complete sentences are sent to the chosen answer service, unlike the on-device translation on Mac. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1190 | 还没有生成这种纪要。点右上角「生成这种纪要」，会用当前回答服务把转写整理成它；每条都带原话时间。 | This note has not been generated. Choose Generate this note to organize the transcript with the current answer service; every entry carries its original time. | — |  |
| 1191 | 问题涉及最新价格、版本、新闻等时检索 Tavily，并把带链接的结果交给回答；没有密钥时保持离线。 | Searches Tavily for the latest prices, versions or news and passes linked results to the answer; stays offline without a key. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1192 | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Answers automatically and follows the script · concise answers · notes: audience questions reviewed one by one (saved to the question bank), what to change, promised follow-ups | 会中自动回答，跟着讲稿 · 回答简洁 · 纪要：听众提问逐题复盘（存进题库）、要改的地方、答应的跟进 | Windows/src/NextThought.Desktop/MeetingDraft.cs:61 |
| 1193 | 安装包签名有效，但签名者不在允许列表（NEXTTHOUGHT_SIGNER_THUMBPRINT）。 | The installer signature is valid but the signer is not on the allow list (NEXTTHOUGHT_SIGNER_THUMBPRINT). | — | Windows/src/NextThought.Desktop/AppState.Update.cs:272 |
| 1194 | 默认关闭；手机与电脑需在同一可信网络。只发送当前问题、回答与状态，不发送资料、完整转写、音频或密钥。 | Off by default; phone and PC must share a trusted network. Only the current question, answer and status are sent — never materials, full transcripts, audio or keys. | — | Windows/src/NextThought.Desktop/AppState.cs:211 |
| 1195 | 选云端后，实时记录与导入 WAV 都走云端识别；音频会发送到所选服务。只选本机时，录音与识别都在本机。 | With cloud selected, both live recording and WAV import use cloud recognition and audio is sent to the chosen service; with local only, recording and recognition stay on this device. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1196 | 默认关闭；开启后手机与电脑需在同一可信网络。首次监听时 Windows 防火墙会询问，请只允许专用网络。 | Off by default; once on, phone and PC must share a trusted network. Windows Firewall asks on the first listen — allow private networks only. | — | Windows/src/NextThought.Desktop/AppState.Launcher.cs:117 |
| 1197 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：总结、要点、决定、待办 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: summary, key points, decisions, todos | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:59 |
| 1198 | 还没有资料。点「导入资料」把 PDF、Word、PPT、Excel、Markdown 或图片加进这个系列。 | No materials yet. Choose Import materials to add PDF, Word, PPT, Excel, Markdown or images to this series. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1199 | 还没有资料；导入 PDF、Word、PPT、Excel、Markdown 或图片后，这里显示页数与段落数。 | No materials yet; once you import PDF, Word, PPT, Excel, Markdown or images, their page and paragraph counts appear here. | — | Windows/src/NextThought.Desktop/ViewModels/LibraryViewModel.cs:32 |
| 1200 | .ntbackup 包含会议、资料与设置；不包含 API Key、语音模型与设备设置。卸载不会删除数据目录。 | .ntbackup contains meetings, materials and settings; it excludes API keys, speech models and device settings. Uninstalling does not delete the data folder. | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml |
| 1201 | 每行一个：正确写法 \| 识别错的写法 \| 另一种错法。正确写法会作为识别热词；错写法出现在转写里时会被改回。 | One per line: correct form \| misheard form \| another misheard form. Correct forms become recognition hotwords; misheard forms are rewritten in the transcript. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1202 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：决定、待办、风险、下次会议 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: decisions, todos, risks, next meeting | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:63 |
| 1203 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：各方观点、结论与分歧、分工 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: each side's view, conclusions and disagreements, assignments | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:66 |
| 1204 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：要点、概念、例子、我的疑问 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: key points, concepts, examples, my questions | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:65 |
| 1205 | Kubernetes \| K8s \| 库伯奈提斯⏎同济大学 \| 同技大学⏎OpenSees \| open sees | Kubernetes \| K8s \| kubernets
Tongji University \| Tongji Uni
OpenSees \| open sees | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1206 | 导入 PDF、Word、PPT、Excel、Markdown 与图片；解析在本机完成，检索时只把相关摘录发给回答服务。 | Import PDF, Word, PPT, Excel, Markdown and images; parsing is local and only relevant excerpts are sent to the answer service. | — | Windows/src/NextThought.Desktop/Views/LibraryView.axaml |
| 1207 | NextThought 备份\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | NextThought backup\|*.ntbackup;*.ihbackup;*.ntrestore;*.ihrestore | — | Windows/src/NextThought.Desktop/Views/BackupSettingsSection.axaml.cs:46 |
| 1208 | 「下载并安装」会下载安装包并校验发布里的 SHA-256，校验通过后静默安装并重新打开；发布没有校验文件时只提供下载页。 | Download and install fetches the installer and verifies its published SHA-256, then installs silently and reopens; without a checksum only the download page is offered. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1209 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答简洁 · 纪要：定下的方案、分工、我的任务、下次碰头 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · concise answers · notes: chosen plan, assignments, my tasks, next meetup | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:67 |
| 1210 | 手机与电脑连接同一可信网络后开启。手机扫码用浏览器打开，可查看当前问题、回答与状态，并触发回答、简短版、举例或停止生成。 | Turn on after the phone and PC join the same trusted network. Scan the code in a browser to see the current question, answer and status, and to trigger answer, shorter, example or stop. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1211 | 所有偏好保存在本机数据目录的 settings.json；API Key 用当前 Windows 用户加密，不会写进备份。 | Preferences live in settings.json in the local data folder; the API key is encrypted for the current Windows user and never written to backups. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1212 | 蓝牙备用通道暂不可用：手机端仍按 IH- 广播名过滤，Windows 无法自定义广播名，等手机端改成按服务标识扫描后再开放。 | The Bluetooth fallback is unavailable: the phone still filters by the IH- broadcast name, Windows cannot rename its broadcast, and it stays closed until the phone scans by service ID. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1213 | 填写后配对链接走 HTTPS/WSS 中转（端到端加密），不必在同一局域网；留空则只用局域网。改地址后重新开启伴侣并重新配对。 | When set, pairing uses an HTTPS/WSS relay (end-to-end encrypted) and no shared LAN is needed; leave empty for LAN only. Re-enable and re-pair after changing it. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1214 | 局域网 HTTP 不加密；配对链接相当于临时遥控密钥，请仅在可信网络使用。不会发送完整转写、资料文件、音频或 API Key。 | LAN HTTP is unencrypted; the pairing link is a temporary remote key, so use it only on a trusted network. Full transcripts, material files, audio and API keys are never sent. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1215 | 选应用时仅采集该进程及其子进程的声音；选整个设备时采集该设备全部混音。需要 Windows 10 build 20348 以上。 | App capture records only that process and its children; device capture records the whole mix. Requires Windows 10 build 20348 or later. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1216 | 会中只记录，需要时 Ctrl+Shift+Enter 回答 · 回答详细 · 纪要：讨论的问题、没讨论透的地方、意见与要求、下一步 | Records only during the meeting; Ctrl+Shift+Enter answers when needed · detailed answers · notes: questions discussed, what stayed unresolved, comments and requests, next steps | — | Windows/src/NextThought.Desktop/MeetingDraft.cs:62 |
| 1217 | 忘了结束的会议会在长时间没有识别到文字后自动收尾，避免把下一场会议的内容写进这场纪要。只统计识别到的文字，背景音乐不会让它一直开着。 | A meeting left running ends after a long stretch without recognized text, so the next meeting does not leak into these notes. Only recognized text counts; background music does not keep it open. | — |  |
| 1218 | 每个模板以 ## 模板名 开头，每个章节以 ### 章节标题 开头，下一行写要求。保存后会出现在纪要页的模板下拉框；原有内置模板不变。 | Each template starts with ## name; each section starts with ### title and the next line states the requirement. Saved templates appear in the Notes template picker; built-ins are unchanged. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1219 | Qwen3-Embedding-0.6B Q8 · CPU · 首次下载约 596 MB。索引仅在本机生成；模型缺失或失败时回退关键词。 | Qwen3-Embedding-0.6B Q8 · CPU · about 596 MB on first download. The index is local; missing or failed models fall back to keywords. | — | Windows/src/NextThought.Desktop/Views/DocumentComponentsAdvanced.axaml |
| 1220 | 每个技能以 ## 名称 开头，可写一行「摘要：…」，其余为 Markdown。启用后会加入回答提示；与真实性/安全约束冲突时以安全约束为准。 | Each skill starts with ## name, may include one Summary: line and is otherwise Markdown. Enabled skills join the answer prompt; truth and safety rules win on conflict. | — |  |
| 1221 | 读 4 句话，约 40 秒。只保存一串数字，不保存录音；只存在这台电脑上，不上传、不进备份，可随时删除。录入后，线下会议里你的发言会标为“我”。 | Read 4 sentences, about 40 seconds. Only a string of numbers is saved, never audio; it stays on this PC, is never uploaded or backed up, and can be deleted anytime. Afterwards your speech in in-person meetings is marked as “Me”. | — | Windows/src/NextThought.Desktop/Views/MyVoiceWindow.axaml |
| 1222 | Ctrl+L 聚焦输入 · Enter 发送 · Ctrl+Shift+Enter 强制回答 · Esc 取消当前回答 · ↑/PageUp 保持阅读位置 | Ctrl+L focuses input · Enter sends · Ctrl+Shift+Enter forces an answer · Esc cancels · ↑/PageUp keep your reading position | — | Windows/src/NextThought.Desktop/Views/ControlSampleWindow.axaml |
| 1223 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。已支持按讲话标题和截图 OCR 跟页；持续屏幕观察尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Following slides by spoken title and screenshot OCR is supported; continuous screen watching is not ported yet. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1224 | 支持 PDF（含扫描件 OCR）、Word、PowerPoint、Excel、Markdown、TXT 与图片；回答时只把与问题相关的片段交给模型，并标注来源。 | Supports PDF (includes scanned OCR), Word, PowerPoint, Excel, Markdown, TXT and images. Only excerpts relevant to the question are sent to the model while answering, with sources marked. | — | Windows/src/NextThought.Desktop/Views/SeriesEditorWindow.axaml |
| 1225 | 开始记录时主窗口会收起（Ctrl+Alt+N 唤回）；只有一块屏时不会启用，避免把会议窗口藏起来。幻灯片自动翻页识别（LiveInsights 页面匹配）尚未移植。 | The main window hides when recording starts (Ctrl+Alt+N brings it back); with a single display it stays off. Automatic slide-change detection (LiveInsights page matching) is not ported yet. | — |  |
| 1226 | 点按键位后直接按下组合键录入，Esc 取消；「清除」「默认」恢复该动作的 Mac 默认键位。必须包含 Ctrl/Alt/Shift/Win 之一，两个动作不能相同。 | Choose the key field and press the combination; Esc cancels. Clear and Default restore that action's Mac default key. It must include Ctrl/Alt/Shift/Win and no two actions may match. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1227 | 检查 GitHub 上的发布页：正式版本只看 vX.Y.Z，main 最新构建跟随 main 分支。发布带安装包与校验文件时可下载并校验后应用内安装，否则只打开下载页。 | Checks the GitHub release page: stable builds use vX.Y.Z, latest follows main. When a release has an installer and checksum, it downloads, verifies and installs; otherwise it opens the download page. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1228 | 技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在系列的“技能”页按系列勾选。 | A skill is a Markdown instruction describing the desired speaking style, answer method or subject knowledge. Skills opened to all series are used for every answer; other skills are chosen per series on the series' Skills tab. | — | Windows/src/NextThought.Desktop/Views/SettingsView.axaml |
| 1229 | 将永久删除这场会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖它的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes this meeting's transcript, audio, notes, index, questions, answers and notes. Accepted lessons, decisions and actions that only depend on it are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meeting's wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:129 |
| 1230 | 将永久删除所有归档会议的转写、音频、手记、索引、问题、回答和纪要。默认同时删除只依赖这些会议的已确认经验、决定和行动。若选择保留，内容会作为独立经验保存并标记原来源已删除，不保留会议原话。 | This permanently deletes the transcript, audio, notes, index, questions, answers and notes of every archived meeting. Accepted lessons, decisions and actions that only depend on them are deleted too by default. If you keep them, they become standalone lessons marked with a deleted source, without the meetings' wording. | — | Windows/src/NextThought.Desktop/Views/HistoryView.axaml.cs:156 |
| 1231 | 打开“屏幕感知”后，每隔几秒在本机识别屏幕文字：跟随你讲到 PPT 的哪一页并给出这一页的讲稿提示；屏幕上出现题目或代码时，判断是否和对话相关并纳入回答。屏幕在本机识别；相关文字与必要的当前画面会提供给回答引擎。 | With screen awareness on, the text on screen is recognised on this device every few seconds: it follows the slide you are on and offers talking points for that page. When a question or code appears on screen, it decides whether it relates to the conversation and includes it in answers. The screen is read on this device; the relevant text and, when needed, the current frame are provided to the answer engine. | — | Windows/src/NextThought.Desktop/Views/InsightsPanel.axaml |
| 1232 | 资料文件 (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | Material files (*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg)\|*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.png;*.jpg | — | Windows/src/NextThought.Desktop/AppState.Prepare.cs:110 |
| 1233 | 每行「动作=按键」，保存后立即重新注册。可用 Ctrl/Alt/Shift/Win 组合，按键支持 A–Z、0–9、F1–F24、Left/Right/Up/Down/PageUp/PageDown/Space/Backslash。 | One action=key per line; saving re-registers immediately. Modifiers may include Ctrl/Alt/Shift/Win, and keys may be A–Z, 0–9, F1–F24, Left/Right/Up/Down/PageUp/PageDown/Space/Backslash. | — |  |

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

