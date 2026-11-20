# 技能设置对照（E28）

Mac 参照：`Sources/NextThought/SettingsView.swift` 的 `SkillSettings`（第 742–798 行）与 `SkillEditor`（第 800–827 行）、`SkillLibrary.swift` 的 `create/delete/update/installDemo`；示例技能取自 `DemoMode.swift` 的 `installDemo([spoken, design, rebuttal])`（第 409–412 行）。
Windows 参照：`Windows/src/NextThought.Desktop/Views/SettingsView.axaml` 的回答与资料 → 技能卡片、`SettingsView.axaml.cs` 的 `BuildSkillRows`、`AppState.Skills.cs`、`Core/Skill.cs`。

| # | Mac 元素/行为 | Windows 元素/行为 | 一致性 |
|---|---|---|---|
| 1 | `SettingsSection("技能")` | 卡片标题 `{loc:Tr 技能}` | ✅ 同文案 |
| 2 | 说明「技能可用于所有系列，或在资料夹中按系列选择。」+ info Note「技能是一份 Markdown 说明（例如 SKILL.md）…按系列勾选。」 | 同两句 caption（`{loc:Tr}` Mac 原句） | ✅ 同文案 |
| 3 | `导入 .md 文件…`（primary） | `导入 .md 文件…` 按钮（`ImportSkill_Click`，README/文件选择） | ✅ 同文案；⚠ Mac 为 primary 样式，Windows 为普通按钮 |
| 4 | `新建技能`（secondary，`SkillLibrary.create(global:false)`，解析 `Skill.starterTemplate`） | `新建技能` → `AddSkill()`：名称/摘要/正文取 `Skill.StarterName/Summary/Content`（Mac starterTemplate 正文逐字），默认非「所有系列」 | ✅ 行为对齐；⚠ 新建后内联编辑器而 Mac 立即打开 sheet |
| 5 | 空态 Note「还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。」 | 同句 `IsVisible="{Binding !HasSkills}"` | ✅ |
| 6 | 每行：图标（global=globe/doc，启用时高亮）、名称、摘要或「N 字」、非全局时「N 个系列在用/还没有系列使用」、`所有系列` toggle、`启用` toggle、`编辑`、删除图标 | 内联编辑器卡片：名称/摘要 TextBox、`启用` 与 `所有系列都使用这个技能` CheckBox、正文 TextBox、`%lld 字 · 单个技能最多使用约 6000 字`、`保存`、`删除`（文本按钮） | ⚠ 字段集合与保存行为一致；缺少图标、摘要/N 字只读行、系列使用计数；无「编辑」入口（始终可编辑，相当于常开 sheet） |
| 7 | `SkillEditor` sheet：标题「编辑技能」、名称、一句话说明（可选）、Markdown TextEditor（≥320pt，等宽）、`所有系列都使用这个技能` toggle、字数说明、取消/保存 | 内联卡片同字段（名称/摘要输入提示词为 Mac 标签；正文 TextBox 高 70）；`保存` 调 `SaveSkillEdit`（保留 `ImportedAt`，等价 `library.update`）；无「取消」 | ✅ 保存语义等价；⚠ 无 sheet/取消；正文高度较小 |
| 8 | 删除：confirmationDialog「删除技能“\(name)”？」+ 删除/取消 → `library.delete(id)` | `删除` → `ConfirmDialog(UiText.Format("删除技能“%@”？"))` → `DeleteSkill(id)` | ✅ 同文案与行为 |
| 9 | 所有系列/启用 toggle 即时调 `library.update` 保存 | 保存按钮统一写盘（`PersistSkills`），勾选在保存后生效 | ⚠ Mac 即时保存；Windows 需点保存（避免误触；行为结果一致） |
| 10 | 示例技能 `installDemo`：面试口语化（global true）/系统设计答题框架/学术答辩应对，摘要/正文逐字；demo 模式整库替换为这三条，并把系统设计技能挂到面试系列 | `Skill.DemoSkills` 三条逐字（含 global 标志）；设置页「安装示例技能」补装缺失项（Windows 额外入口，仿 E28 既有按钮）；`--demo` 的 `DemoScript.BuildSkills` 同三条（global 标志已按 Mac 修正）；系列挂载在 demo 脚本处理 | ✅ 名称/摘要/正文/global 逐字；⚠ 设置页「安装示例技能」为 Windows 额外按钮（Mac 只在 demo 模式安装） |
| 11 | 单个技能约 6000 字上限的说明句 | 同句 `%lld 字 · 单个技能最多使用约 6000 字`（随输入更新） | ✅ 文案一致；⚠ 未硬性截断（Mac 也只提示，实际截断在 prompt 组装侧） |
| 12 | 资料夹「技能」页按系列勾选（WorkspacesView） | `AppState.Series.cs` 的 `SeriesSkills` 行 + 系列设置 | ⚠ 已实现、未与 Mac 逐元素对照（不在本任务范围） |

## 点按记录（--demo --self-test=settings-presets 内新增步骤）
1. `新建技能`：技能数 +1，名称=新技能、摘要=一句话说明这个技能做什么，正文=Mac starterTemplate 正文，默认非「所有系列」、启用。
2. 编辑并 `保存`：名称改 QA 技能、摘要/正文改测试文本，重读列表一致（保留 id/导入时间）。
3. `安装示例技能`：三条 demo 技能全部存在，面试口语化 global=true，其余两条 global=false。
4. `删除`：删除新建技能后列表不再包含它（确认对话框文案与 Mac 同句）。

## 未解决/差异
- 列表行缺少 Mac 的图标、只读摘要行与「N 个系列在用」计数；编辑器为常开内联卡片而非 sheet，无「取消」。
- 「安装示例技能」是 Windows 设置页额外入口（Mac 仅 demo 模式安装），保留以便 QA。
- 资料夹按系列勾选页未在本任务逐元素对照。
- 截图证据：`--demo --screenshots`/self-test 证据目录中的 settings-skills-{light,dark,en-light,en-dark}.png（settings-presets 自检内捕获；本目录随提交附 PNG 若已收割）。
