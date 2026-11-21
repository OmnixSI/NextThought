# 术语表 / 加入术语表（E22 · §6.7）Mac 3.4.0 源码 ↔ Windows GlossaryWindow 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`Sources/NextThought/GlossaryView.swift`（GlossaryView + AddToGlossarySheet + MisheardMark + TranscriptFindField）
- Windows 代码：`Windows/src/NextThought.Desktop/Views/GlossaryWindow.axaml(.cs)`、`Views/AddToGlossarySheet.axaml(.cs)`、`AppState.Glossary.cs`
- Windows 截图（本目录）：`glossary-light.png` / `glossary-dark.png`（列表 + 选中条目的编辑器）、`add-to-glossary-light.png` / `add-to-glossary-dark.png`（`--self-test glossary-ui` 产出）
- 注：Mac 没有术语表截图，按 Swift 源码逐元素对照

## GlossaryView

| # | Mac 元素（顺序） | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 窗口 | min 780×540 / ideal 820×580，paper 底，padding 24，spacing 12 | 820×580、Min 780×540、Paper、Margin 24、RowSpacing 12 | 一致 |
| 2 | 标题区 | 「术语和人名」section + caption 提示（含「原始转写不会被改写。」）；右上「完成」primary、默认动作 | 同（`DoneButton IsDefault`，文案与 Mac 逐字） | 一致 |
| 3 | 作用域 | 全局 / 当前系列 分段控件（有系列时） | 同（`seg` 分段样式，绑定 `UseGlobal/SeriesGlossaryScopeCommand`） | 一致 |
| 4 | 查找框 | 放大镜 + 「查找」，高 32、sunken、radiusSmall、最大宽 200 | 同（`Border.field` 32 pt + 图标 + 透明 TextBox） | 一致 |
| 5 | 状态文字 | 工具行右侧 caption | 同（`GlossaryStatusText`） | 一致 |
| 6 | 「…」菜单 | 批量粘贴… / 从文件导入… / 导出为文件… / 复制为文字 / 整理重复项 | 同（Flyout 五项同序） | 一致 |
| 7 | 左列 | paperCard padding 8，宽 270；「新增」ghost（加号）；条目行 | 同（card padding 8、列宽 270、`新增`带加号） | 一致 |
| 8 | 条目行 | 规范写法 label + 人名 chip（仅人名）；变体行「← a、b」caption；选中 Hover 底、radius 8；行 padding h8 v6 | 同（`glossaryRow`，hover Sunken、selected Hover；变体行为「← …」） | 一致 |
| 9 | 空态 | 「还没有条目。点“新增”，或用“批量粘贴”一次加入多条。」/「这个系列还没有自己的条目。」/「没有匹配的条目。」 | 同（三句 Mac 原文） | 一致 |
| 10 | 空编辑器 | 「选一条来编辑，或新增一条。」body 次要色、居中、minHeight 200 卡片 | 同 | 一致 |
| 11 | 规范写法 | overline + sunken 32 pt 单行框，placeholder「正确的写法，例如 Kubernetes」 | 同 | 一致 |
| 12 | 类型 | 术语 / 人名 分段控件 | 同 | 一致 |
| 13 | 听错的写法 | 可删除胶囊 tag（文字 + xmark 24 tap）+ 输入框；回车/逗号加入，长度≥2 且不等于规范写法 | 同（`VariantTags` + `VariantInput`，Enter/逗号/顿号分隔） | 一致 |
| 14 | 备注 | overline + 单行框，placeholder「可选，例如“我们的数据库项目”」 | 同 | 一致 |
| 15 | 重复提示 | 「已有同名条目，关闭这个页面时会合并。」（looseKey 比较） | 同（`HasGlossaryDuplicate`，`Glossary.LooseKey`） | 一致 |
| 16 | 删除 | 编辑器底部右侧 ghost「删除」 | 同 | 一致 |
| 17 | 自动保存 | 编辑内容变化即 commit（去抖），关闭时 finishVariants + tidyGlossaries | 同（500 ms 去抖 commit；Closing 时 tidy） | 一致 |
| 18 | 批量粘贴 | 「批量粘贴」label + 说明句 + mono TextEditor（minHeight 220、sunken）+ 「没写类型的当作」+ 分段 + 取消/加入（空时禁用） | 同 | 一致 |
| 19 | 作用域语义 | global + 当前系列 layered；原始转写不改写 | 同（`EffectiveGlossary`，自检覆盖资料术语叠加） | 一致 |

## AddToGlossarySheet

| # | Mac 元素 | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 窗口 | 宽 440、paper、padding 24、spacing 16 | 440×420、Paper、Margin 20（差 4 pt）、spacing 10 | 接近（间距略紧） |
| 2 | 标题/来源行 | 「加入术语表」section；来源句 caption 4 行、可选中；提示「在这句里选中听错的词并复制（⌘C）…」 | 同（`SelectableTextBlock` MaxLines 4；提示文案逐字） | 一致 |
| 3 | 字段 | 规范写法 / 听错的写法 overline + 框；placeholder 同 Mac | 同 | 一致 |
| 4 | 类型/范围 | 术语/人名 分段；「只用于当前系列」toggle（workspaces 非空时） | 同（Windows 仅在选中系列时显示 toggle，见差异） | 基本一致 |
| 5 | 底部 | 取消 ghost + 加入 primary（默认动作、规范写法空时禁用） | 同（`IsDefault`、`CommitAddToGlossary` 校验） | 一致 |

## 差异 / 未解决

1. **间距**：AddToGlossarySheet 的 padding 20/spacing 10 与 Mac 24/16 略有差别（功能与元素一致）。
2. **剪贴板预填**：Mac 打开 sheet 时读剪贴板自动填「听错的写法」；Windows 由调用方（转写命中行）预填，未读系统剪贴板（Avalonia 12 文本读取限制），已在 E22 说明。
3. **「只用于当前系列」显隐**：Mac 只要有任意系列就显示；Windows 仅在当前选中系列时显示。
4. Mac 的 TranscriptFindField / MisheardMark 用在转写表面（会议页/Notes），不在术语窗口内；Windows 同样把它们放在会议页转写查找里（`--self-test glossary-ui` 的 notes-transcript-find 截图覆盖），术语窗口不再重复。
5. 截图里的示例数据来自自检隔离目录（全局 3 条 + 系列 1 条），Mac 无对应截图。
