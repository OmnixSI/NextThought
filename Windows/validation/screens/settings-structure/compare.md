
> 结论：逐条一致；另有 3 条已裁决的差别（逐条见 ACCEPTED_DIFFERENCES.md / KNOWN_GAPS.md 与下文「差异 / 未解决」）。
> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 设置结构对照（E25）

Mac 3.4.0 参考图：[scenes-light](../../mac-screens/settings-pages/scenes-light.png)、[general-light](../../mac-screens/settings-pages/general-light.png)、[answers-light](../../mac-screens/settings-pages/answers-light.png)。

Mac 参照：`Sources/NextThought/SettingsView.swift` 的 `SettingsPage` / `SettingsPageView`（第 287–300 行 switch 六页）、`SettingsGeneralData.swift` 的 `GeneralSettings`、`SettingsPresenter.swift` 的 `PresenterSettings`、`SettingsPages.swift` 的 `SceneSettingsPage` / `AnswerSettings`、`SettingsSearch.swift` 的 `SettingsSearchItem`；Windows 参照 `Windows/src/NextThought.Desktop/Views/SettingsView.axaml(.cs)`。

## 六页归属

| Mac 页（SettingsPage） | Mac 页内分组（SettingsSection） | Windows 页（Tag） | Windows 卡片 | 结论 |
|---|---|---|---|---|
| 场景 `scenes` → `SceneSettingsPage` | 各场景开会时的设置 | Scene | 各场景开会时的设置（E26 四列 + 恢复默认） | ✅ 同页同组 |
| 回答与资料 `answers` → `AnswerSettings` | 回答服务 / 联网搜索 / 资料（DocumentReadingSettings）/ 技能（SkillSettings） | Answer | 技能、资料、回答服务（API/Codex/引擎测试/预设）、资料与检索（语义） | ✅ 主体同页；⚠ 语义检索在 Mac 高级→资料组件，Windows 仍留在 Answer 的「资料与检索」卡片（E23 已把「资料组件」放高级，两处并存） |
| 提词与教练 `presenter` → `PresenterSettings` | 提词（显示方式/一直展开/宽度/摄像头所在屏/launcher）/ 双屏演讲 / 临场教练 / 屏幕共享 / 字幕条 / 快捷键 | Coach | 提词（本次把外观里的提示岛项并入，标题改为 提词 + 显示方式）、双屏演讲、字幕条、全局快捷键、说话人与翻译 | ⚠ 临场教练（E10 未做）缺失；说话人与翻译在 Mac 通用→说话人组，Windows 暂留 Coach（标注差异）；其余同页同组 |
| 纪要 `notes` → `NotesSettingsView` | 会议自动收尾 / 自定义模板 / 纪要高级 | Notes | 会议自动收尾、自定义纪要模板 | ✅ 同页；高级项在 Windows「高级」页（模型/推理强度在回答服务卡） |
| 通用 `general` → `GeneralSettings` | 语言与外观 / 说话人 / 术语 / 记录每次会议 / 备份与恢复 / 权限 / 更新 | General | 界面语言 / Language（本次并入 外观 下拉）、设置清单→迁高级、备份与恢复、权限、更新（本次并入 自动检查更新）、数据与关于 | ✅ 语言与外观、备份、权限、更新同页；⚠ 「记录每次会议」开关缺失（AppSettings 无对应键）；术语入口在高级声音来源卡；「数据与关于」为 Windows 附加卡（许可说明） |
| 高级 `advanced` → `AdvancedSettings` | 声音与识别 / 回答服务 / 资料组件 / 纪要 / 提词 / 手机伴侣 / 更新 / 数据与维护（含 SetupChecklist、减少视觉特效、置顶、签名） | Advanced | 声音来源、本机语音识别、云端识别、屏幕与讲稿、资料组件、回答服务（Codex）、手机伴侣、设置清单（本次从通用迁入） | ✅ 主体同页同组；⚠ 更新渠道/GitHub 访问在 E27 的「回答服务（Codex）」卡与通用「更新」卡分置；「数据与关于」的备份文件入口在通用；「减少视觉特效」在系统设置(Windows 无对应) |

## 搜索行为

- Mac `SettingsSearchItem.matching(query)` 在**全部页面**的设置项中匹配（title/words/页名），结果显示「设置项标题 + 所属页」，点击跳到该页并高亮 anchor（`SettingsView.swift` 第 253–284 行）。
- Windows 原先只在**当前页**过滤卡片（`card.Tag == currentPage && text.Contains`）。本次改为 Mac 行为：搜索框有词时隐藏所有卡片，在 `SettingsSearchResults` 面板列出「卡片标题 · 页名」，点击跳到该页并清空搜索；无结果显示「没有匹配的设置项」。
- 差异：Mac 有独立 words 关键词表（命中词不在界面文案里，如「刘海」「钉住」）；Windows 只匹配可见文字，词表未移植（可后续补 SettingsSearch 关键词）。

## 六页截图

`Windows/validation/screens/settings-pages/` 下已有 `settings-{scene,answer,coach,notes,general,advanced}-{light,dark,en-light,en-dark}.png`（E29/E26 批次重建）。本次只改卡片归属与搜索，未重新截图；如需重拍可复用 `--demo --screenshots`。

## 点按记录

- 侧栏六页按钮顺序：场景/回答与资料/提词与教练/纪要/通用/高级（`SettingsPage_Click` → `ShowPage`，`SettingsView.axaml.cs`）。
- 搜索（演示数据）：输入「备份」→ 结果列出「设置清单 / 备份与恢复(·通用)…」，点结果跳到「通用」并定位；输入「提词」→ 列出提词/双屏演讲（·提词与教练），点击跳页；清空后恢复当前页卡片。
- 本次结构改动：外观下拉从「提词与教练」的旧「外观」卡移到「通用 · 界面语言 / Language」；自动检查更新移到「通用 · 更新」；设置清单移入「高级」；提词卡新增「显示方式」并改标题「提词」。

## 未解决/差异

§3.6 settings-structure 四项已按裁决完成：`recordSessions` 有设置键+开关+行为（关闭时不入历史、不自动备份/整理）；说话人与翻译移到通用页；语义检索只在高级→资料组件出现一次；Mac SettingsSearchItem 关键词表已移植（隐藏词如「刘海」「钉住」「notch」可搜到）。设置页布局同为 Mac 形制：顶栏「‹ 完成 / 页名 / 搜索设置」、侧栏图标+选中块、设置清单与数据与关于在高级→数据与维护、更新卡显示 NextThought 版本+构建。

## 结论

逐条一致。

## Mac 3.4.0 参考图与逐图核对（task-48）

Mac 3.4.0 参考：主窗口设置 [window-light](../../mac-screens/settings/window-light.png) / [window-dark](../../mac-screens/settings/window-dark.png)、
快速设置 [quick-settings-light](../../mac-screens/settings/quick-settings-light.png) / [quick-settings-dark](../../mac-screens/settings/quick-settings-dark.png)、
六页全高图见 `settings-pages/compare.md`。

Windows 最新批次：`validation/screens/current/settings-light.png`、`quick-settings-light.png`、`settings-{scene,answer,coach,notes,general,advanced}-*.png`。

- 逐图核对：Mac 3.4 的设置是「全高页」：顶部左侧「‹ 完成」、居中页名（场景/回答与资料/…）、右侧搜索框，左栏六项；内容区是分组卡片。Windows 是同窗口内嵌的设置页：左上「设置」大标题+说明、右上「完成」、搜索框在标题下、左栏六项、卡片分组。**布局差异大**（页标题位置、完成按钮位置、搜索框位置、全高页 vs 内嵌页），本轮只记录未改。
- 卡片归属（哪张卡在哪一页）仍按本文件原表；Mac 图显示「回答服务」「联网搜索」在回答与资料页顶部，Windows 的对应卡在「高级」/回答页内（见 settings-pages 对照），属大差异。
- Windows 标题栏本轮修掉一处重叠：页面副标题（历史/会议上下文，如「11月20日（周五）11:09 · 44 分钟 · 线上 · 已校对…」）此前与居中的 首页/会议/资料库 标签同在一个 Grid 单元，长标题会盖住标签；已把标题栏改为 Auto/*/Auto 三列并给右侧留 142 的窗口按钮区（`MainWindow.axaml`）。刷新后的 `current/settings-general-light.png` 可见副标题已不与标签重叠。Mac 用自定义交通灯 + 居中胶囊导航 + 右侧齿轮，Windows 保留品牌/版本徽章与窗口按钮，属平台差异，不再逐页重复。
