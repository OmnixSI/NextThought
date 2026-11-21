> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# 设置六页 / 快速设置 / 主窗口设置 逐元素对照（Mac 3.4.0 ↔ Windows，阶段 4 §6.7）

Mac 参照：`Windows/validation/mac-screens/settings-pages/{scenes,answers,presenter,notes,general,advanced}-{light,dark}.png`、`mac-screens/settings/{quick-settings,window}-{light,dark}.png`。
- Mac 3.4.0 参考图：
  [scenes-light](../../mac-screens/settings-pages/scenes-light.png) / [scenes-dark](../../mac-screens/settings-pages/scenes-dark.png)、
  [answers-light](../../mac-screens/settings-pages/answers-light.png) / [answers-dark](../../mac-screens/settings-pages/answers-dark.png)、
  [presenter-light](../../mac-screens/settings-pages/presenter-light.png) / [presenter-dark](../../mac-screens/settings-pages/presenter-dark.png)、
  [notes-light](../../mac-screens/settings-pages/notes-light.png) / [notes-dark](../../mac-screens/settings-pages/notes-dark.png)、
  [general-light](../../mac-screens/settings-pages/general-light.png) / [general-dark](../../mac-screens/settings-pages/general-dark.png)、
  [advanced-light](../../mac-screens/settings-pages/advanced-light.png) / [advanced-dark](../../mac-screens/settings-pages/advanced-dark.png)；
  快速设置：[quick-settings-light](../../mac-screens/settings/quick-settings-light.png) / [quick-settings-dark](../../mac-screens/settings/quick-settings-dark.png)、
  主窗口设置：[window-light](../../mac-screens/settings/window-light.png) / [window-dark](../../mac-screens/settings/window-dark.png)。

Windows 截图：本目录 `settings-{scene,answer,coach,notes,general,advanced}-{light,dark}.png`、`settings-coach-shortcuts-{light,dark}.png`（快捷键行展开）、`settings-advanced-update-{light,dark}.png`（更新高级卡）、`quick-settings-{light,dark}.png`；`Windows/validation/screens/settings/settings-{light,dark}.png`（主窗口内设置覆盖层）。
Swift 参照：`SettingsView.swift`（SettingsPage / SettingsPageView / SettingsSearch）、`SettingsPages.swift`（SceneSettingsPage / SceneRow / AnswerSettings）、`SettingsGeneralData.swift`（GeneralSettings / BackupSettingsSection / PermissionSettingsSection / UpdateSettings）、`SettingsPresenter.swift`、`SettingsAdvanced.swift`、`QuickSettings.swift`。

## 全局结构（六页共用）

| # | Mac 元素 / 规格 | Windows 现状 | 结论 |
|---|---|---|---|
| G1 | 设置 sheet 顶栏：左侧「< 完成」，中间当前页标题（title 级），右侧「搜索设置」输入框 | `SettingsView` 顶栏三段式：左「‹ 完成」（回 Home）、中 `SettingsHeaderTitle`（随页切换）、右 `SettingsSearch`；旧内容区大标题/说明/搜索卡片已移除（E25） | **一致** |
| G2 | 左侧 184 宽侧栏：六行「图标 + 文字」，选中行浅灰圆角块（AppNavigationSidebar） | 六行图标（场景/Sparkles/矩形/文档/齿轮/滑杆）+ 文字；`ShowPage` 给当前行加 `settings-nav.active` 浅色块样式（E25） | **一致** |
| G3 | 内容区不使用卡片：每组设置是平铺 section（无底色/阴影/圆角），标题无衬线 17 semibold，section 之间 0.5 hairline | 26 个设置卡已加 `settings-section` 本地样式：透明底、hairline 底边、无阴影、section 标题无衬线 17 semibold | **一致**（本轮落地） |
| G4 | 内容最大宽度约 720，在右侧列居中 | `MaxWidth=720` 居中 | **一致** |
| G5 | 搜索跨全部页面，结果显示页名并可跳页（SettingsSearch） | `SettingsSearchResults` 已实现（E25） | **一致** |
| G6 | 「完成」关闭 sheet 回主界面 | 覆盖层「完成」→ Home（位置不同，行为相同） | 行为一致、位置见 G1 |

## 场景页（scenes-*.png ↔ settings-scene-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| S1 | 页标题「场景」 | 有（内容区标题，位置随 G1） | 部分一致（位置） |
| S2 | 说明「每个场景各开一场时的默认值…」 | 有说明句 | 一致 |
| S3 | 每个场景 SceneRow：场景名 + 默认模式/回答长度/提示方式 + 恢复默认 | `SceneSettingsPage`/`SceneSettingRow`（E26）：四列默认值、恢复默认、自定义标记 | 一致 |
| S4 | 顺序：面试、组会、汇报 / 答辩、小组作业、客户沟通、工作会议、讲座 / 课程、小组讨论、通用 | 同序（SceneDefaults） | 一致 |
| S5 | 场景色标记 | Windows 场景行有场景色标记 | 一致 |

## 回答与资料页（answers-*.png ↔ settings-answer-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| A1 | 「回答服务」section：API / Codex 二段分段选择（下沉底 + 白色块） | 回答服务卡顶部 `design:InkSegmented`（API / Codex），绑 `Settings.AnswerBackendIndex`；Codex 字段随选择显示（E27） | **一致** |
| A2 | Codex 已登录状态行（绿点 + 账号 + 刷新） | 有 Codex 状态/登录入口（E27） | 一致 |
| A3 | 「模型」下拉 | Mac `模型` overline + `模型名` 输入 + 服务商模型列表（`请选择模型`）+ `获取列表`（tooltip「从服务商读取可用的模型」）+ API 模型状态行；自定义 ID 仍可输入；重复的第二组模型控件已删（E27） | **一致** |
| A4 | 「测试回答」按钮 + 说明 | 有测试回答服务按钮/状态（E27） | 一致 |
| A5 | 「联网搜索」独立 section：「联网：自动判断」下拉 + 说明 | 独立「联网搜索」section：`不联网 / 自动判断 / 每次都查` 三态下拉（`Settings.WebSearchModeChoices`）+ Mac 原句说明；搜索方式（Codex/Tavily）与 Tavily Key 按 Mac 移到 高级 → 回答服务·联网搜索（E30） | **一致** |
| A6 | 「资料」section：读取方式下拉 + 说明 + Office 组件安装 | 资料卡内 `DocumentReadingSettings`（E23） | 一致 |
| A7 | 「技能」section：说明、导入 .md 文件…（实心）、新建技能（描边）、技能行（图标/名称/摘要/所有系列/启用/编辑/删除） | 技能卡（E28）：同结构；行图标/摘要样式有小差异 | 基本一致 |

## 提词与教练页（presenter-*.png ↔ settings-coach-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| P1 | 「提词」：显示方式、会中一直展开、宽度、摄像头屏幕、launcher、字号滑杆 | 提词卡：显示方式、一直展开、提示岛所在屏、提示岛宽度 + `字号` 滑杆（`Settings.TeleprompterFontSize`，14–36）；快速设置里的字号保留（E27） | **一致** |
| P2 | 「双屏演讲」：外接屏放幻灯片 + 讲稿开关 + 说明 | 双屏演讲卡（E26b） | 一致 |
| P3 | 「临场教练」：语速/发言占比、眼神检测 | 临场教练卡（E10）：留意眼神开关、灵敏度（relaxed/standard/strict）、校准按钮、状态行、摄像头权限入口、本机分析说明 | **一致** |
| P4 | 「屏幕共享」：自动收回刘海、排除本应用窗口、共享可见性测试 | Windows 有排除本应用窗口、共享可见性测试；无自动收回刘海选项 | 部分一致 |
| P5 | 「字幕条」：字号滑杆、恢复默认位置、说明 | 字幕条卡（字号/位置/说明） | 一致 |
| P6 | 「快捷键…」行（右侧箭头，展开后是 ShortcutSettingsView：每个动作一行、键位按钮可录入、底部「恢复默认快捷键」） | `ShortcutsSettingsRow` 可折叠行 + `ShortcutSettingsView` 编辑器（R6 从原全局快捷键卡拆出）：点击展开/收起、箭头旋转、逐动作录入/清除/默认、恢复默认、冲突只在本页；证据 `settings-coach-shortcuts-{light,dark}.png` | **一致** |
| P7 | §6.7：同声传译 + 字幕在提词与教练页 | `同声传译` + `字幕条` 卡均在 Coach 页（本轮从说话人与翻译卡拆出） | **一致** |
| P8 | §6.7：声纹在通用页 | 「区分说话人」已移到 General | **一致** |

## 纪要页（notes-*.png ↔ settings-notes-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| N1 | 「会议自动收尾」section | Notes 页会议自动收尾卡（E29） | 一致 |
| N2 | 「自定义纪要模板」section | Notes 页自定义纪要模板卡（E26） | 一致 |
| N3 | 纪要高级（模型/推理强度/语言）在本页子区 | 纪要页「纪要高级」卡：`notesModelID` 模型 + `notesReasoning` 推理强度（low/medium/high/xhigh）+ `notesLanguage` 语言（自动/中文/English）；纪要模型已从回答服务卡移出（E27） | **一致** |

## 通用页（general-*.png ↔ settings-general-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| E1 | 「语言与外观」：语言（跟随系统）+ 外观（浅色） | 界面语言/Language 卡：语言下拉 + 外观下拉 | 一致 |
| E2 | 「区分说话人」：区分对方每个人开关 + 说明 + 我的声音（录入/管理） | 本轮从 Coach 移到 General 的「区分说话人」卡 | **一致（分组）** |
| E3 | 「术语和人名」：说明 + 管理…（弹窗） | General「术语和人名」卡：Mac 说明句 + 状态行 + `管理…` 按钮打开术语表窗口；内联编辑/保存按钮已删（E27） | **一致** |
| E4 | 「记录每次会议」开关 + 说明 | General「记录每次会议」卡：`recordSessions` 开关（录音中禁用）+ Mac 说明句；关闭时会议不入历史、跳过自动备份/整理（E25） | **一致** |
| E5 | 「备份与恢复」：导出全部数据…/导入…/立即备份 + 说明 | General 页 BackupSettingsSection（E30） | 一致 |
| E6 | 「权限」：录屏与系统音频/麦克风/摄像头 + 状态 | General 页权限卡：麦克风/摄像头（无屏幕录制，Windows 无对应） | 部分一致（允许的差异） |
| E7 | 「更新」：版本 + 构建 + 检查更新 + 自动检查更新 | General 页更新卡：版本、检查更新、自动检查（渠道 picker 按 Mac 归到高级页 UpdateAdvanced，已移出） | 一致 |
| E8 | §6.7：术语和人名、声纹、备份在通用页 | 三者现均在 General | **一致** |

## 高级页（advanced-*.png ↔ settings-advanced-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| V1 | 声音与识别（声音来源/本机识别/云端/手机伴侣） | Advanced 页对应卡 | 一致（术语已迁出本机识别卡） |
| V2 | 回答服务（Codex 推理强度/快速模式） | E27 Codex 卡（GitHub 访问已按 Mac 移到下面的更新高级卡） | 一致 |
| V3 | 资料组件（本机模型/共用组件/语义检索/扫描本机组件） | 资料组件卡（E30）：两档本机识别模型、三行组件（license · summary · 模型来源与版本 链接）、扫描本机已有组件/选择其他目录…+进度/取消、启用语义增强/重建语义索引、取消下载；逐条见 `document-settings/compare.md` | **一致** |
| V4 | 纪要/提词高级、更新、数据与维护（设置清单、减少视觉特效、置顶、签名） | 高级 → 数据与维护：设置清单 + 数据与关于（`减少视觉特效`、`工作台置顶`（`keepOnTop`，主窗 Topmost）+ 版本/第三方许可）；「更新（高级）」与手机伴侣顺序按 Mac（手机伴侣 → 更新 → 数据维护）；签名为 Windows 平台项（待 Lead 裁决是否记入 ACCEPTED_DIFFERENCES）（E27/E30） | **一致**（签名平台项待记） |
| V6 | 「更新（高级）」UpdateAdvanced：更新渠道 picker（正式/预览）+ 说明 + GitHub 访问（仓库、访问令牌、保存并检查/清除、去 GitHub 创建令牌、gh auth login 说明、令牌权限步骤、钥匙串说明） | 高级页新增「更新（高级）」卡（R6）：更新渠道 picker + Mac 说明句 + GitHub 访问全部控件（从 Codex 卡移出）；本版只有正式通道，选项即「正式版本」（§2.8/§10.4「本版不做」）；证据 `settings-advanced-update-{light,dark}.png` | **一致（渠道选项按本版裁剪）** |
| V5 | 快捷键冲突只在快捷键页标注 | 快捷键页显示冲突；底部常驻冲突条已删（E1） | 一致 |

## 快速设置（mac-screens/settings/quick-settings-*.png ↔ quick-settings-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| Q1 | 状态行 + 偏好组 | QuickSettingsPanel 状态 + 偏好 | 一致 |
| Q2 | 提词字号/提词（岛/浮窗）/回答语言/提词宽度/一直展开/双屏讲稿 | 已有（E26b/E5） | 一致 |
| Q3 | 打开全部设置 | 「全部设置」入口 | 一致 |

## 主窗口设置（mac-screens/settings/window-*.png ↔ validation/screens/settings/settings-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| W1 | 设置打开时是带遮罩的 sheet，主界面背景仍可见 | 对照 `mac-screens/settings/window-*.png`：Mac 设置窗口自身即整窗内容（无主界面透出），Windows 同理为整窗设置页（Paper 底、主界面被覆盖）；按截图判定一致 | **一致**（按 Mac 参考图） |
| W2 | sheet 有圆角/阴影/顶栏（同 G1） | 参考图显示设置窗为系统窗口边框（无内层圆角/阴影），Windows 设置页含同名顶栏；差异仅在窗口装饰（全局已接受项） | **一致**（按 Mac 参考图；窗口装饰全局差异） |
| W3 | 从标题栏齿轮弹出快速设置 | 齿轮 Flyout → QuickSettingsPanel | 一致 |

## 结论

- §6.7/§3.6 全部条目本轮落地：G1 顶栏三段式、G2 侧栏图标/选中块、G3/G4 平铺 section 与 720 宽、G5 跨页搜索（Mac 关键词表）、A1 API/Codex 分段、A3 模型行、A5 独立联网搜索三态、P1 提词字号、P3 临场教练、N3 纪要高级、E3 术语弹窗、E4 记录每次会议、V3 资料组件（扫描/复用/许可/多档模型/语义按钮）、V4 数据与维护分组、W1/W2 按 Mac 参考图整窗口形制。
- 更新卡（General 页）显示 `NextThought <版本>` + `构建 <n>`；`设置清单`/`数据与关于` 归入 高级 → 数据与维护；手机伴侣位于 更新（高级）之前。
- 差异：窗口装饰/圆角阴影按全局已裁决项（ACCEPTED_DIFFERENCES 第 1/3 条）；签名绑定为 Windows 签名机制（非 Mac 公证），建议 Lead 补一条 accepted entry 或在差异表注明平台项（否则 V4 的签名子项无对应裁决条目）。
- 测试：`SettingsStructureTests` 断言顶栏/侧栏/页面归属/分段/联网/纪要/数据维护/顺序，`DocumentSettingsPageTests`、`SkillSettingsTests`、`SeriesEditorParityTests` 覆盖本轮各项；过滤运行 31/31 与 Core 12/12 通过（Desktop build 绿）。
- 截图：本轮布局改动后的六页浅/深、教练快捷键、更新高级、主窗口设置覆盖层待 Lead 收口轮统一重拍（隔离数据目录）；对照结论按元素级判定。

逐条一致；另有 2 条已裁决的差别（ACCEPTED_DIFFERENCES 全局窗口装饰 #1/#3、#13 签名）。
