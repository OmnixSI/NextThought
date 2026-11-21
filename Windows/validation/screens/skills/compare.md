> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# 技能设置对照（E28）

Mac 参照：`Sources/NextThought/SettingsView.swift` 的 `SkillSettings`（第 742–798 行）与 `SkillEditor`（第 800–827 行）、`SkillLibrary.swift` 的 `create/delete/update/installDemo`；系列页参照 `WorkspacesView.swift` 的 `WorkspaceSkillsSection`（第 933–966 行）；示例技能取自 `DemoMode.swift` 的 `installDemo([spoken, design, rebuttal])`（第 409–412 行）。
Windows 参照：`Windows/src/NextThought.Desktop/Views/SettingsView.axaml` 的回答与资料 → 技能卡片、`SettingsView.axaml.cs` 的 `BuildSkillRows`、`Views/SkillEditorWindow.axaml(.cs)`、`Views/SeriesEditorWindow.axaml(.cs)`、`AppState.Skills.cs`、`AppState.Series.cs`、`Core/Skill.cs`。

| # | Mac 元素/行为 | Windows 元素/行为 | 一致性 |
|---|---|---|---|
| 1 | `SettingsSection("技能")` | 卡片标题 `{loc:Tr 技能}` | ✅ 同文案 |
| 2 | 说明「技能可用于所有系列，或在系列中按系列选择。」+ info Note「技能是一份 Markdown 说明（例如 SKILL.md）…按系列勾选。」 | 同两句 caption（`{loc:Tr}` Mac 原句） | ✅ 同文案 |
| 3 | `导入 .md 文件…`（primary） | `导入 .md 文件…` 按钮（primary 样式，`ImportSkill_Click`：README/文件选择；重复按钮已修） | ✅ |
| 4 | `新建技能`（secondary，`SkillLibrary.create(global:false)`，解析 `Skill.starterTemplate`） | `新建技能` → `AddSkill()`（Mac StarterName/Summary/Content 逐字，默认非「所有系列」）后立即打开编辑 sheet | ✅ |
| 5 | 空态 Note「还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。」 | 同句 `IsVisible="{Binding !HasSkills}"` | ✅ |
| 6 | 每行：图标（global=globe/doc，启用时高亮）、名称、摘要或「N 字」、非全局时「N 个系列在用/还没有系列使用」、`所有系列` toggle、`启用` toggle、`编辑`、删除图标 | `BuildSkillRows`：globe/doc 图标（禁用时降透明度）、名称、摘要或 `%lld 字 · …`、`N 个系列在用`/`还没有系列使用`、`所有系列`/`启用` CheckBox（改动即 `SaveSkillEdit` = Mac library.update）、`编辑`（开 sheet）、`删除` | ✅ |
| 7 | `SkillEditor` sheet：标题「编辑技能」、名称、一句话说明（可选）、Markdown TextEditor（≥320pt）、`所有系列都使用这个技能` toggle、字数说明、取消/保存 | `SkillEditorWindow`：`编辑技能` 标题 + 名称 + 一句话说明（可选）+ 可滚动 Markdown 编辑区 + 实时 `%lld 字 · 单个技能最多使用约 6000 字` + `所有系列都使用这个技能` + `取消`/`保存`（取消返回 null，不写盘） | ✅ |
| 8 | 删除：confirmationDialog「删除技能“\(name)”？」+ 删除/取消 → `library.delete(id)` | `删除` → `ConfirmDialog(UiText.Format("删除技能“%@”？"))` → `DeleteSkill(id)` | ✅ 同文案与行为 |
| 9 | 所有系列/启用 toggle 即时调 `library.update` 保存 | 行内两个 toggle 的 `IsCheckedChanged` 立即 `SaveSkillEdit`（同 Mac 即时保存）；sheet 的保存/取消另管正文改动 | ✅ |
| 10 | 示例技能 `installDemo`：面试口语化（global true）/系统设计答题框架/学术答辩应对，摘要/正文逐字；demo 模式整库替换为这三条并挂到面试系列 | `Skill.DemoSkills` 三条逐字（含 global 标志）；**设置页「安装示例技能」已删除**（Windows 额外入口移除，现在只在 `--demo` 的 `DemoScript.BuildSkills` 安装）；系列挂载在 demo 脚本处理 | ✅ |
| 11 | 单个技能约 6000 字上限的说明句 | 同句（sheet 内随输入实时更新） | ✅ 文案一致（不硬性截断，与 Mac 相同） |
| 12 | 系列「技能」页（WorkspaceSkillsSection）：标题「技能 N」+ 导入并用于此系列… + 管理技能；每行 toggle、全局/已停用标记、摘要；全局项禁用 | `SeriesEditorWindow` 技能页：`SeriesSkillsHeading`（技能 N）+ `导入并用于此系列…`（导入后 `AttachSkillToSelectedSeries`）+ `管理技能`（打开 设置 → 回答与资料 → 技能 并滚动定位）；行 = CheckBox（`IsAttached`）+ `所有系列`/`已停用` 徽标 + 摘要；`CanToggle` 对全局技能禁用、`ToggleSeriesSkill` 对全局 no-op | ✅ |

## 点按记录

1. `新建技能`：技能数 +1，立即弹出 `SkillEditorWindow`；取消不改动、保存后列表/系列使用计数更新。
2. 行内 `所有系列`/`启用`：改动即写 `skills.json`，刷新后保持。
3. `编辑`：sheet 保存改名称/摘要/正文（保留 id/导入时间），取消不改。
4. `删除`：确认对话框（Mac 同句）后移除。
5. 系列页：`导入并用于此系列…` 导入后自动勾选并保存到该系列；`管理技能` 打开设置技能卡；全局技能行禁用且显示「所有系列」。

## 未解决/差异

无。示例技能仅由演示模式安装（Mac 行为）；`安装示例技能` 按钮与 `已安装示例技能。` 状态文案已从设置页移除（`AppState.InstallDemoSkill` 仍被 `settings-presets` 自检调用，故字符串表保留该句）。
截图：`settings-skills-{light,dark,en-light,en-dark}.png` 与新布局的证据待 Lead 收口轮统一重拍。

## 结论

逐条一致。
