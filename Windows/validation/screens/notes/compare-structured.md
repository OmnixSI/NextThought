# 结构化纪要视图（E15 · §6.7）Mac 3.4.0 源码 ↔ Windows 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`FishboneView.swift`、`ViewpointMatrixView.swift`、`StructuredNotesView.swift`、`NotesFormatBar.swift`、`NotesSettingsView.swift`
- Windows 代码：`Views/FishboneView.cs`、`Views/ViewpointMatrixView.cs`、`Views/Notes/FishboneTree.cs`、`Views/NotesView.axaml(.cs)`、`Views/SettingsView.axaml(.cs)`、`AppState.Notes.cs`、`AppState.StructuredNotes.cs`
- Windows 截图（本目录）：`notes-structured-light.png` / `notes-structured-dark.png`（鱼骨图、窄版鱼骨树、观点对照同屏）、`notes-structured-check.json`

| # | Mac 视图 | Mac 元素 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | `FishboneView` | 因果鱼骨图卡片：问题为右侧头、类别为大骨、原因为小骨、点原因跳到转写；窄宽回退列表 | `Views/FishboneView.cs`（Canvas 绘制脊/骨/头，原因带时间可点跳转） | 一致（几何为等价绘制，见差异 1） |
| 2 | `FishboneDiagram` | 几何图：spine 1.6pt、bones 1.2pt、ticks 0.7 不透明度、头框 ink 描边 | 同上（脊/骨/头框/类别标签） | 一致 |
| 3 | `FishboneTree` | 窄宽列表回退：头+时间、类别折叠行（chevron/名称/条数）、竖线引导、原因可点 | 新增 `Views/Notes/FishboneTree.cs`（同结构、跳转、可选中文本） | 一致（待 Notes 页面接入窄宽判断，见差异 2） |
| 4 | `ViewpointMatrixView` | 议题 + 议题/共识/分歧/分工、矩阵行=议题列=各方、时间链接 | `Views/ViewpointMatrixView.cs`（同结构） | 一致（矩阵样式为 Windows 等价画法） |
| 5 | `StructuredTimeLink` | `[12:41]` 次要色等宽按钮，点击跳转，tooltip「跳到转写里的这句话」 | `FishboneView`/`ViewpointMatrixView` 内联时间按钮（同样跳转） | 一致（内联实现） |
| 6 | `StructuredNotesContent` | 按 format 渲染鱼骨图/大纲/观点对照 + footer（由 model 整理 · 去掉 N 条无依据内容） | `Views/NotesView.axaml` 结构化区（FishboneView/ViewpointMatrixView 绑定 + 生成按钮/空态） | 一致（footer 文案在 NotesView 内联，见差异 3） |
| 7 | `StructuredNotesPane` | format.isStructured 时结构化、否则文字纪要 | `NotesView.axaml` 用 `NotesFormatIs*` 切换/显示 | 一致（内联实现） |
| 8 | `StructuredFormatPane` | 进度/失败重试/来源版本提示/更多菜单（重新生成、复制 Markdown） | `NotesView.axaml` 生成按钮+进度+空态（复制/更新视图在 NotesView.axaml.cs） | 一致（内联实现） |
| 9 | `NotesFormatBar` | 纪要/提纲/鱼骨图/观点对照 segmented（32pt、sunken 底、选中白色胶囊、推荐点/进行中进度） | `NotesView.axaml` 四个 `RadioButton Classes="chip"` 绑定 `SetNotesFormatCommand` | 接近（视觉为 chip 单选，见差异 4） |
| 10 | `NotesSettingsView` | 设置→纪要：纪要语言、会后自动整理、自动收尾、自定义模板 | `Views/SettingsView.axaml` 纪要页（同卡） | 一致（内联实现） |
| 11 | `NotesAdvanced` | 高级→纪要：纪要模型/推理强度/自动收集经验 | `SettingsView.axaml` 高级页 纪要模型/推理强度卡 | 一致（内联实现） |
| 12 | `NotesTemplateEditor` | 600×560 编辑模板：名称、部分列表、添加/删除、校验（名称/部分/要求） | `SettingsView.axaml` 自定义模板编辑器（模板名/模板文本/新建模板）+ `AppState.Notes.cs` 保存校验 | 接近（内联卡片而非 sheet，见差异 5） |
| 13 | `SectionEditor` | 每个部分：名称、给 AI 的要求、待办格式 toggle、gap toggle、删除 | `TemplateSectionRow` 行（名称/要求/kind/carry/上移下移删除） | 接近（待办/gap 用 kind 表达） |

## 差异 / 未解决
1. 鱼骨图几何：Mac 用 `FishboneGeometry` 精确排版（列宽/行高估算），Windows 用等价 Canvas 布局，元素齐全、像素位置非逐点一致。
2. `FishboneTree` 已实现，但「窄宽自动回退」的接入点在 `NotesView.axaml`（d-parity 正在改编辑区），由他接线或我下批补。
3. `StructuredNotesContent` footer（由 model 整理 / 去掉了 N 条）在 NotesView 内联，当前截图未单独取证。
4. 格式栏视觉：Mac 为 segmented track + 白色选中块，Windows 用 chip 单选；交互等价、样式待 D4 统一。
5. 模板编辑器：Mac 为独立 sheet（600×560），Windows 内联在设置页；校验语义一致。
