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

Windows 截图：本目录 `settings-{scene,answer,coach,notes,general,advanced}-{light,dark}.png`、`quick-settings-{light,dark}.png`；`Windows/validation/screens/settings/settings-{light,dark}.png`（主窗口内设置覆盖层）。
Swift 参照：`SettingsView.swift`（SettingsPage / SettingsPageView / SettingsSearch）、`SettingsPages.swift`（SceneSettingsPage / SceneRow / AnswerSettings）、`SettingsGeneralData.swift`（GeneralSettings / BackupSettingsSection / PermissionSettingsSection / UpdateSettings）、`SettingsPresenter.swift`、`SettingsAdvanced.swift`、`QuickSettings.swift`。

## 全局结构（六页共用）

| # | Mac 元素 / 规格 | Windows 现状 | 结论 |
|---|---|---|---|
| G1 | 设置 sheet 顶栏：左侧「< 完成」，中间当前页标题（title 级），右侧「搜索设置」输入框 | Windows 内容区顶部仍是「设置」大标题 + 说明 + 搜索卡片；关闭按钮在覆盖层右上（旧位置） | **不一致**：顶栏三段式未落地（上一轮尝试导致 XML 不合法已回退；需重做） |
| G2 | 左侧 184 宽侧栏：六行「图标 + 文字」，选中行浅灰圆角块（AppNavigationSidebar） | Windows 侧栏 184 宽、六项顺序正确，但只有文字、无图标、选中态未样式化 | **不一致**：图标与选中块未做 |
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
| A1 | 「回答服务」section：API / Codex 二段分段选择（下沉底 + 白色块） | 一个回答服务卡：API 预设下拉 + 分区；没有 API/Codex 分段控件 | **不一致**：缺二段分段选择外观 |
| A2 | Codex 已登录状态行（绿点 + 账号 + 刷新） | 有 Codex 状态/登录入口（E27） | 一致 |
| A3 | 「模型」下拉 | 模型名输入 + 模型列表（E27） | 部分一致（兼容自定义输入） |
| A4 | 「测试回答」按钮 + 说明 | 有测试回答服务按钮/状态（E27） | 一致 |
| A5 | 「联网搜索」独立 section：「联网：自动判断」下拉 + 说明 | 联网在回答服务卡内：CheckBox「允许联网核实（需 TAVILY_API_KEY）」+ Key + 说明（英文换行已修） | **不一致**：Mac 独立 section + 三态下拉，Windows 是 CheckBox（E14 三态字段未接 UI） |
| A6 | 「资料」section：读取方式下拉 + 说明 + Office 组件安装 | 资料卡内 `DocumentReadingSettings`（E23） | 一致 |
| A7 | 「技能」section：说明、导入 .md 文件…（实心）、新建技能（描边）、技能行（图标/名称/摘要/所有系列/启用/编辑/删除） | 技能卡（E28）：同结构；行图标/摘要样式有小差异 | 基本一致 |

## 提词与教练页（presenter-*.png ↔ settings-coach-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| P1 | 「提词」：显示方式、会中一直展开、宽度、摄像头屏幕、launcher、字号滑杆 | 提词卡（E26b/E5）：显示方式、一直展开、摄像头屏幕、提示岛宽度；字号在快速设置 | 基本一致（字号滑杆位置不同） |
| P2 | 「双屏演讲」：外接屏放幻灯片 + 讲稿开关 + 说明 | 双屏演讲卡（E26b） | 一致 |
| P3 | 「临场教练」：语速/发言占比、眼神检测 | Windows 无临场教练（E10 未做） | **不一致**（E10 未做） |
| P4 | 「屏幕共享」：自动收回刘海、排除本应用窗口、共享可见性测试 | Windows 有排除本应用窗口、共享可见性测试；无自动收回刘海选项 | 部分一致 |
| P5 | 「字幕条」：字号滑杆、恢复默认位置、说明 | 字幕条卡（字号/位置/说明） | 一致 |
| P6 | 「快捷键…」行 | 全局快捷键卡（键位录制；冲突只在此页显示） | 一致（呈现为独立卡） |
| P7 | §6.7：同声传译 + 字幕在提词与教练页 | `同声传译` + `字幕条` 卡均在 Coach 页（本轮从说话人与翻译卡拆出） | **一致** |
| P8 | §6.7：声纹在通用页 | 「区分说话人」已移到 General | **一致** |

## 纪要页（notes-*.png ↔ settings-notes-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| N1 | 「会议自动收尾」section | Notes 页会议自动收尾卡（E29） | 一致 |
| N2 | 「自定义纪要模板」section | Notes 页自定义纪要模板卡（E26） | 一致 |
| N3 | 纪要高级（模型/推理强度/语言）在本页子区 | 在回答服务/高级页 | **不一致**（分组差异） |

## 通用页（general-*.png ↔ settings-general-*.png）

| # | Mac 元素（顺序） | Windows 现状 | 结论 |
|---|---|---|---|
| E1 | 「语言与外观」：语言（跟随系统）+ 外观（浅色） | 界面语言/Language 卡：语言下拉 + 外观下拉 | 一致 |
| E2 | 「区分说话人」：区分对方每个人开关 + 说明 + 我的声音（录入/管理） | 本轮从 Coach 移到 General 的「区分说话人」卡 | **一致（分组）** |
| E3 | 「术语和人名」：说明 + 管理…（弹窗） | 本轮从 Advanced 移到 General 的「术语和人名」卡：说明 + 内联编辑 + 保存/打开术语表 | 分组一致；UI 呈现不一致（内联 vs 弹窗） |
| E4 | 「记录每次会议」开关 + 说明 | Windows 无此开关（无 recordSessions 消费 UI） | **不一致**（已知缺口） |
| E5 | 「备份与恢复」：导出全部数据…/导入…/立即备份 + 说明 | General 页 BackupSettingsSection（E30） | 一致 |
| E6 | 「权限」：录屏与系统音频/麦克风/摄像头 + 状态 | General 页权限卡：麦克风/摄像头（无屏幕录制，Windows 无对应） | 部分一致（允许的差异） |
| E7 | 「更新」：版本 + 构建 + 检查更新 + 自动检查更新 | General 页更新卡：版本、渠道、检查更新、自动检查 | 一致 |
| E8 | §6.7：术语和人名、声纹、备份在通用页 | 三者现均在 General | **一致** |

## 高级页（advanced-*.png ↔ settings-advanced-*.png）

| # | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|
| V1 | 声音与识别（声音来源/本机识别/云端/手机伴侣） | Advanced 页对应卡 | 一致（术语已迁出本机识别卡） |
| V2 | 回答服务（Codex 推理强度/快速模式/GitHub） | E27 Codex 卡 | 一致 |
| V3 | 资料组件（本机模型/共用组件/语义检索/扫描本机组件） | E23 资料组件卡；Mac「扫描本机已有组件/选择目录」未移植 | 部分一致（缺口见 E23 compare） |
| V4 | 纪要/提词高级、更新、数据与维护（设置清单、减少视觉特效、置顶、签名） | 设置清单（E3，通用页）、数据与关于（Windows 附加）、屏幕与讲稿（E9） | 部分一致（数据与维护分组未逐项对齐） |
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
| W1 | 设置打开时是带遮罩的 sheet，主界面背景仍可见 | Windows 覆盖层是不透明 Paper 全屏覆盖，主界面不可见 | **不一致** |
| W2 | sheet 有圆角/阴影/顶栏（同 G1） | 覆盖层无圆角/阴影、顶栏未做 | **不一致** |
| W3 | 从标题栏齿轮弹出快速设置 | 齿轮 Flyout → QuickSettingsPanel | 一致 |

## 结论

- **本轮已一致**：§6.7 三条归属全部落地（声纹/术语/备份→通用；技能/资料/联网→回答与资料；同声传译/字幕→提词与教练）；26 个设置卡改为 Mac 平铺 section 样式（透明底、hairline 分隔、无衬线 17 semibold section 标题）；设置页英文两处截断修复（标题药丸 6d60d37c、TAVILY 标签换行 386f6630）；结构测试 `SettingsStructureTests` 4/4。
- **仍不一致（逐条）**：G1 顶栏三段式；G2 侧栏图标/选中块；A1 API/Codex 分段控件；A5 联网搜索独立 section + 三态下拉；P3 临场教练（E10）；N3 纪要高级分组；E3 术语 UI 弹窗化；E4 记录每次会议开关；V3/V4 资料/数据维护分组；W1/W2 覆盖层遮罩/圆角/顶栏。
- 截图：六页浅/深 + 快速设置 + 英文浅/深 + 主窗口设置覆盖层（1120×820，隔离数据目录）随本目录与 `validation/screens/settings/` 入库。
- 未解决：上面列出的不一致项按 Mac 继续逐项改（顶栏/侧栏/分段控件优先）；临场教练（E10）、联网三态下拉控件分属其它任务组。
