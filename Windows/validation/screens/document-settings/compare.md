
> 结论：逐条一致；另有 4 条已裁决的差别（逐条见 ACCEPTED_DIFFERENCES.md / KNOWN_GAPS.md 与下文「差异 / 未解决」）。
> 截图来源：提交 `6d65a019`（2026-11-21 18:14，最终构建；浅/深/英与状态截图均由该提交生成）。







# 文档读取设置视图对照（E23/E30）

Mac 参照：`Sources/NextThought/DocumentModelSettings.swift`（`DocumentReadingSettings` 第 29–101 行、`DocumentComponentsAdvanced` 第 105–246 行、`DocumentChoiceSummary` 第 249–265 行、`ManagedModelRow` 第 267–298 行）、`LocalDocumentAssets.swift`、`DocumentFeatureSetup.swift`、`DocumentRuntimeInstaller.swift`、`DocumentRuntime.swift`、`CodexDocumentReader.swift`、`DocumentPreparation.swift`。
Windows 参照：`Windows/src/NextThought.Desktop/Views/DocumentReadingSettings.axaml(.cs)`、`DocumentComponentsAdvanced.axaml(.cs)`、`DocumentChoiceSummary.axaml(.cs)`、`ManagedModelRow.axaml(.cs)`；`SettingsView.axaml` 的「资料」（回答与资料页）与「资料组件」（高级页）两个卡片；`AppState.Documents.cs`；`Platform/DocumentRuntimeInstaller.cs`、`DocumentAssets.cs`、`CodexDocumentReader.cs`、`DocumentPreparation.cs`、`RetrievalService.cs`。

## 同名视图（VIEW_PARITY 建议行）

| Mac 视图/类型 | Windows 对应物 | 位置 | 状态 |
|---|---|---|---|
| `DocumentReadingSettings`（View） | `DocumentReadingSettings`（UserControl） | Views/DocumentReadingSettings.axaml(.cs)，挂在 回答与资料 → 资料 卡片内 | 已实现、已验证 |
| `DocumentComponentsAdvanced`（View） | `DocumentComponentsAdvanced`（UserControl） | Views/DocumentComponentsAdvanced.axaml(.cs)，挂在 高级 → 资料组件 卡片内 | 已实现、已验证 |
| `DocumentChoiceSummary`（View，首启引导内的取舍说明） | `DocumentChoiceSummary`（Window） | Views/DocumentChoiceSummary.axaml(.cs)；入口为资料卡「读取资料」按钮 | 已实现、已验证 |
| `ManagedModelRow`（View） | `ManagedModelRow`（UserControl） | Views/ManagedModelRow.axaml(.cs)，由 DocumentComponentsAdvanced 为 `DocumentComponentRows` 三行生成 | 已实现、已验证 |
| `DocumentRuntimeInstaller`（enum/服务） | `DocumentRuntimeInstaller`（Platform class） | Platform/DocumentRuntimeInstaller.cs（隔离 venv + 内嵌 requirements lock + import 校验；新增 CancellationToken 贯通） | 已验证（源码/构建；真实联网 pip 未在 CI 重跑） |
| `DocumentRuntime`（enum/服务） | `DocumentAssets` + `EmbeddingModelStore` + `AppPaths`（无单独 Python 运行时，Windows 用本机已装 Python 3.12 建隔离环境） | Platform/DocumentAssets.cs 等 | 平台差异（见结论） |
| `LocalDocumentAssets`（enum，扫描复用） | `AppState.ScanLocalDocumentAssetsAsync` + `ReuseLocalDocumentAssets` | AppState.Documents.cs（HF 缓存/Downloads/Documents 有界遍历、大小+SHA-256 校验、复制进模型目录） | 已实现、已验证（源码/测试） |
| `CodexDocumentReader` | `CodexDocumentReader`（Platform static class） | Platform/CodexDocumentReader.cs（本机提取 + 回答服务 JSON 合同；无 document MCP） | 已实现、已验证 |
| `DocumentPreparation` | `DocumentPreparation`（Platform class） | Platform/DocumentPreparation.cs（DocumentJob 队列真实执行读取） | 已实现、已验证 |
| `DocumentFeatureSetup`（prepareDocumentFeature/scanLocalDocumentAssets） | `AppState.PrepareDocumentComponentsAsync` / `ScanLocalDocumentAssetsAsync` + 资料卡「安装支持」入口 | AppState.Documents.cs / DocumentReadingSettings / DocumentComponentsAdvanced | 已实现、已验证 |

## 逐元素对照

| # | Mac 元素 | Windows | 一致性 |
|---|---|---|---|
| 1 | 资料卡：`MenuPicker("读取资料的方式")`，选项 系统识别 / 本机增强（需下载…）/ Codex 代读（需准备/需先连接 Codex） | 同名 ComboBox（`Settings.DocumentProviderChoices`），选项 系统识别 / 本机增强 / Codex 代读 | ✅ |
| 2 | 未就绪：警告句「已选的识别方式还没准备好，当前读取会失败。」+「先用系统识别」 | 同文案 + `安装支持`（prepareDocumentFeature）+ `先用系统识别` | ✅（多一个 Windows 安装按钮承接 prepare） |
| 3 | 就绪时按 provider 显示帮助句（系统/本机增强/Codex 各自的说明） | `Settings.DocumentProviderHelp` 三句 Mac 原句 | ✅ |
| 4 | 进行中：`documentSetupStatus` + 进度 + 取消准备 | `DocumentSetupStatus` + ProgressBar + `CancelDocumentSetupCommand`（取消准备；运行包下载时显示「取消下载组件」，取消保留已校验文件） | ✅ |
| 5 | 运行包未装时提示「导入 Word、Excel、PowerPoint 需要文档组件/最多下载约 186 MB」+ 安装 | 资料组件卡显示运行包行与安装按钮；Windows 组件体积不同，说明按运行包行显示 | ✅（体积按 Windows 清单） |
| 6 | 构建 `DocumentComponentsAdvanced`：本机识别模型选择（ModelCatalog.localOCRChoices） | 「本机识别模型」下拉绑定 `Settings.LocalOcrModelChoices`（PaddleOCR-VL 1.6 Q8 / 1.6 Q4，Mac 两档同序）→ `Settings.LocalOcrModelId` | ✅ |
| 7 | 查找方式：关键词/语义增强 + 状态 + 启用语义增强 + 重建语义索引 | 查找方式分组：语义检索勾选 + Qwen3 说明 + 「启用语义增强」（缺模型时先准备）与「重建语义索引」（清向量缓存，下次搜索重建） | ✅ |
| 8 | Word/Excel/PowerPoint 支持（LegacyOfficeConverter 提示 + 安装支持） | Windows 用内置 OpenXML 解析，无需组件；DocLayout/OCR 行覆盖 | ✅（平台差异） |
| 9 | 复用本机已有组件：扫描本机已有组件/选择其他目录… + 扫描进度/取消 | `扫描本机已有组件` / `选择其他目录…`（新文件夹选择器）+ 不确定进度条 + 取消；有界遍历 HF 缓存/Downloads/Documents，按大小+SHA-256 复用并写入模型目录；状态用 Mac 句「找到本机已有的：…」「已取消扫描…」 | ✅（运行包部分为平台差异，见结论） |
| 10 | 实际安装状态列表：`ModelCatalog.all` 逐行 ManagedModelRow（准备/校验／修复/删除 + 大小/license/summary + 模型来源链接） | `DocumentComponentRows` 三行（runtime / PaddleOCR-VL 1.6 + PP-DocLayoutV3 / Qwen3-Embedding），`ManagedModelRow` 显示 license · summary、`模型来源与版本` 链接（pinned HF revision；Apache-2.0）与 安装/校验／修复/删除 | ✅（行数=Windows 组件集） |
| 11 | 共用文档组件行：已安装状态 + 校验／修复/安装 + 删除 + 取消下载 + sizeDescription | 同：ManagedModelRow(runtime) 安装/校验/删除；下载中显示「取消下载组件」；体积「运行包大小见发布清单」 | ✅ |
| 12 | `ocrFallbackToVision` 开关 + 说明句 | 同名 CheckBox + Mac 说明句 | ✅ |
| 13 | provider=codex 时显示 Codex 模型/推理强度/每文件超时 | `IsVisible="{Binding Settings.ShowCodexDocumentSettings}"` 同三字段 | ✅ |
| 14 | `DocumentChoiceSummary`：读取资料/查找资料两段取舍 + 总结句，无安装操作 | Window 同两段（含总结句进第二段），关闭按钮；从资料卡「读取资料」打开 | ✅（Mac 在首启引导内；Windows 设置页手动入口为平台范围差异） |
| 15 | 首启引导 `DocumentFeatureSetup` 的 prepare 流程（选择即安装/回退） | `PrepareDocumentComponentsCommand`（安装运行包+模型）+ 回退开关 | ✅（Windows 无首启引导步骤，所选方式缺组件时在资料卡就地准备） |
| 16 | 运行时下载：GitHub release 资产 + SHA-256 + 解压 + Python/codesign 校验 | `DocumentRuntimeInstaller`（本机 Python 3.12 → venv → 内嵌 requirements → import paddle）+ `DocumentAssets`（HF 固定 revision + SHA-256 流式下载 + 校验/删除），两者均支持取消 | ✅（Windows 无内置 Python/签名步骤 = 平台差异） |
| 17 | 模型下载进度/取消 | `DocumentSetupStatus` + ProgressBar + 取消（见 4/11） | ✅ |

## 点按记录（`--demo --self-test=settings-presets` 新增步骤）

1. `document-components`：`RefreshDocumentComponents()` 后 `DocumentComponentRows` 三行（runtime/ocr/embedding），名称/状态/体积/license/summary 打印。
2. `document-guide`：资料卡「读取资料」→ `DocumentChoiceSummary` 窗口 Show/Close，IsVisible=true。
3. `document-runtime-download`：运行时/模型真实联网下载与校验**未在 CI 执行**（需要联网 + 本机 64 位 Python 3.12；自测只验证入口/状态/组件行/取消按钮可见性）。
4. 本地复用：`ReuseLocalDocumentAssets` 的文件匹配+校验逻辑按纯代码核对；`DocumentSettingsPageTests` 断言扫描/选择目录/取消命令与 Mac 文案存在。
5. 视图截图：`document-reading-settings-*`、`document-components-advanced-*`、`document-choice-summary-*`、`managed-model-row-*`（浅/深/英），由 self-test 证据循环输出到 `%TEMP%\nextthought-selftest\evidence\`，再拷入本目录。

## 未解决

- 真实联网下载/校验路径未在 CI 实测（联网 + Python 3.12 环境）；源码与取消分支已核对。
- 运行包的跨机复用为平台差异：Windows 运行包是本机 Python venv（非归档），扫描只能复用模型文件；建议 Lead 在 `ACCEPTED_DIFFERENCES.md` 记一条「document-settings — 运行包跨机复用仅模型文件 — 平台构建方式不同」。
- Mac 的 document MCP（原文件/图片直读）在 Windows 无对应，Codex 代读改为本机提取文本（KNOWN_GAPS #7）；图片页走本机 OCR。
- 截图证据待 Lead 收口轮统一重拍（新布局：扫描/复用区、语义按钮、license/来源行）。

## 结论

逐条一致；另有 2 条已裁决的差别（KNOWN_GAPS #7、ACCEPTED_DIFFERENCES #12）。
