# E22 术语表收尾：Mac ↔ Windows 对照

对照代码：`Sources/NextThought/GlossaryView.swift`（约 520 行：GlossarySettingsEntry / GlossaryView / MisheardMark / TranscriptFindField / GlossaryDraft+AddToGlossarySheet）、`Sources/NextThought/AppState+Glossary.swift`（212 行全文）
对照基准：Mac v3.3.0（`5ddb9e5`）；Mac 无 glossary 截图，按源码逐元素对照
Windows 代码：`Views/GlossaryWindow.axaml(.cs)`、`Views/AddToGlossarySheet.axaml(.cs)`、`AppState.Glossary.cs`、`GlossaryRows.cs`、`GlossaryEntryRow.cs`、`Views/NotesView.axaml(.cs)`（转写标签的 TranscriptFindField）、`NextThought.Core/GlossaryMaterialTerms.cs`（新增）、`GlossaryMerge.cs`（新增）
Windows 截图：`Windows/validation/screens/glossary/`（自检 `glossary-ui` 生成：glossary-light / add-to-glossary-light / notes-transcript-find-light）

## 1. 术语窗口（GlossaryView）

| Mac | Windows | 结论 |
|---|---|---|
| 标题「术语和人名」+「完成」+ 说明（名字术语统一、记下听错写法、原始转写不改写） | 同文案（`GlossaryTitleText`/`GlossaryHint`/完成按钮） | 一致 |
| 作用域切换：全局 / 当前系列（InkSegmented） | 两个 RadioButton（全局 / 当前系列）+ UseGlobal/UseSeries 命令；切换后清空选中并重载列表 | 一致（Radio 的 IsChecked 未绑定回显，仅点击生效 → 视觉小差异） |
| 查找框（过滤列表，按 spellings+note） | 「查找」TextBox + 过滤（`GlossaryFilter`） | 一致 |
| 「新增」按钮 + 条目列表（canonical、人名 chip、变体「→ a、b」） | 同（新增命令、人名 chip、`VariantsLine`） | 一致 |
| 选中条目编辑：规范写法、类型（术语/人名）、听错写法 tags+输入、备注、删除 | 编辑器字段同（规范写法 / 类型 Radio / 听错的写法逗号分隔 / 备注 / 删除 / 保存条目）；听错写法为文本输入而非 tag 流式布局 | 部分（无 tag 流式组件） |
| 自动保存（onChange commit） | 显式「保存条目」按钮（Mac 即时 commit，Windows 保存式，行为等价） | 差异（交互方式） |
| 批量粘贴（每行 规范写法\|变体\|类型\|备注，默认类型可选） | 「更多」菜单→批量粘贴…：粘贴文本 + 默认类型 + 加入（`ImportGlossaryPasted`） | 一致 |
| 从文件导入… / 导出为文件… / 复制为文字 / 整理重复项 | 「更多」菜单四项均已接（文件选择器导入、另存导出、复制到剪贴板、`TidyGlossariesCommand`） | 一致 |
| 保存到设置/即时持久化 | `SaveGlossaryCommand`（文本批量保存到 glossary.json）+ 条目立即保存 | 一致 |
| 系列「术语」区块（当前系列自己的条目 + 移除） | 编辑器下方「系列术语」区 + `SeriesGlossaryRows` + 移除按钮 | 一致 |
| 条目合并/重复提示（duplicatesAnother） | 合并由 `Glossary.Normalized()/Merging` 完成；无「已有同名条目…」提示 | 部分 |

## 2. 误听标记（MisheardMark）与转写查找（TranscriptFindField）

| Mac | Windows | 结论 |
|---|---|---|
| MisheardMark：命中行下小字「匹配到听错的写法：<form>」+ 放大镜图标 + help「…记下的听错写法，搜索时一并找出来」 | 命中行下小字 `MisheardMarkText`（同一句 Mac 文案）+ 行内 tooltip；无图标（纯文字） | 基本一致（缺图标） |
| TranscriptFindField：放大镜 + 占位「在转写里查找（听错的写法也会找出来）」+ 匹配数「N 处」+ 清空按钮 | 术语窗口与会议页转写标签均有同款查找：占位、`N 处`、清空搜索 | 一致（无放大镜图标） |
| 查找按「全局 + 该会议系列」表展开听错写法 | `EffectiveGlossary(CurrentSession).Search(query)` + `FirstMatch`：规范写法能找到只有听错写法的行，并标记 Misheard | 一致（自检验证「Kubernetes → 库伯尼提斯」「订单链路 → 定单链路」） |
| 会议页转写查找结果（点击定位） | 会议页转写标签：查找框 + 命中面板（文本 + MisheardMark + 「加入术语表…」按钮）；未做点击命中滚动到原行 | 部分（无滚动定位） |

## 3. 从转写加入术语表（GlossaryDraft / AddToGlossarySheet）

| Mac | Windows | 结论 |
|---|---|---|
| 转写菜单「加入术语表…」→ sheet：显示来源行、「选中并复制听错的词，会自动填入」、规范写法、听错写法、类型、只用于当前系列、取消/加入 | `AddToGlossarySheet` 同结构（来源行、规范写法、听错写法、类型、只用于当前系列、取消/加入）；**剪贴板自动预填未做**（Avalonia 12 剪贴板文本读取 API 不可用），改为查找命中已知听错形式时由调用方预填 `GlossaryDraftHeard` | 部分（无剪贴板预填；规范写法仍需手填） |
| 「只用于当前系列」仅在存在系列时可勾 | 同（`IsVisible="{Binding HasSelectedSeries}"`） | 一致 |
| 加入全局表或系列表（`addToGlossary` 合并同名） | `CommitAddToGlossary` → `Merging` 到全局 store 或系列表（自检：全局加入「订单链路」） | 一致 |

## 4. 系列级术语叠加生效（AppState+Glossary）

| Mac | Windows | 结论 |
|---|---|---|
| effectiveGlossary = 全局 + 会议系列 + 资料术语（MaterialTerms） | `EffectiveGlossary(meta)` = `Glossary.Layered(glossary, series.Glossary)` + `GlossaryMaterialTerms.WithTerms(MaterialTerms.Extract(series.Enabled 文档 pages))` | 一致 |
| 识别热词（recognitionHotwords）用有效表 + 设置词表 | `GlossaryHotwords()` 改用 `EffectiveGlossary(CurrentSession).Hotwords()` | 一致 |
| 回答提示 1,500 字符（glossaryPromptSection） | `GlossaryPromptSection(meta)` 用有效表（含资料术语）`PromptSection(1500)`，接在回答提示（AppState.Live） | 一致 |
| 纪要提示 glossaryNotesBlock（2,000 字符 + 校正/整理规则） | Windows 未把 glossary 块注入纪要提示（只用于回答与识别） | **未做**（记未解决） |
| 转写查找按该会议系列叠加 | `EffectiveGlossary(CurrentSession)`（自检命中系列条目的听错写法） | 一致 |
| 规范化整理（normalize 全局+系列） | `TidyGlossariesCommand` 同时整理全局与当前系列并保存 | 一致 |
| 同名合并/来源计数 | `GlossaryMerge.Count/Size` 统计条目+变体数；自检「已加入 4 项」 | 一致（无 Mac 的 duplicates 提示） |

## 5. 自检点按记录（`glossary-ui`，隔离数据目录）

| 步骤 | 操作 | 结果 |
|---|---|---|
| 1 | 载入全局术语（2 条） | 通过 |
| 2 | 查找「Kubernetes」→ 命中只有「库伯尼提斯」的行并标记 Misheard | 通过（1 处；匹配到听错的写法：库伯尼提斯） |
| 3 | 会议页转写查找「订单链路」→ 系列表叠加后命中「定单链路」 | 通过（1 处；匹配到听错的写法：定单链路） |
| 4 | 切换「当前系列」作用域 | 通过（1 条系列条目） |
| 5 | 编辑系列条目备注并保存 | 通过（workspace 系列 glossary 含备注） |
| 6 | 从转写行加入术语表（AddToGlossarySheet 流程） | 通过（全局表合并「订单链路」+ 变体） |
| 7 | 批量粘贴两行 | 通过（已加入 4 项） |
| 8 | 整理重复项 + 导出文字 | 通过（5 行文本） |
| 9 | 资料术语叠加（系列文档含「吞吐量」） | 通过（有效术语 5 条含吞吐量） |
| 截图 | glossary-light / add-to-glossary-light / notes-transcript-find-light | 生成 |

自检报告：`Windows/validation/screens/glossary/glossary-check.json`（9/9、exit 0）。
新增测试 `GlossaryMaterialTermsTests`（6 个）：分层查找规范/听错写法、资料术语去重、短词忽略、提示包含系列+资料术语且 ≤1500、合并计数、批量粘贴解析合并。

## 6. 未解决 / 待补

1. **剪贴板自动预填未做**：Avalonia 12 的 `IClipboard` 文本读取接口与 Mac `NSPasteboard` 不对应（`GetTextAsync/TryGetTextAsync` 不可用）；查找命中时由调用方预填已知听错形式，转写右键打开时需手填。若找到新 API（`IAsyncDataTransfer`）再补。
2. 纪要提示未注入 glossary 块（Mac glossaryNotesBlock：纪要/校对规则 + 2,000 字符）；回答提示、识别热词与查找已叠加。
3. 会议页转写查找命中不做滚动定位/高亮（只列出命中行并可加入术语表）；MisheardMark 无放大镜图标；作用域 Radio 的 IsChecked 无状态回显。
4. 无 tag 流式「听错写法」组件、无「已有同名条目，关闭时会合并」提示；条目保存为显式按钮（Mac 即时 commit）。
5. 无 Mac 对应视图截图（docs 无 glossary 图），本轮截图仅 Windows 自检取证；状态最多「已实现、未验证」。
