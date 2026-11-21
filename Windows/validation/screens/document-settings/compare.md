# 文档读取设置视图对照（E23 剩余）

Mac 参照：`Sources/NextThought/DocumentModelSettings.swift`（`DocumentReadingSettings` 第 29–101 行、`DocumentComponentsAdvanced` 第 105–246 行、`DocumentChoiceSummary` 第 249–265 行、`ManagedModelRow` 第 267–298 行）、`DocumentFeatureSetup.swift`、`DocumentRuntimeInstaller.swift`、`DocumentRuntime.swift`、`CodexDocumentReader.swift`、`DocumentPreparation.swift`。
Windows 参照：`Windows/src/NextThought.Desktop/Views/DocumentReadingSettings.axaml(.cs)`、`DocumentComponentsAdvanced.axaml(.cs)`、`DocumentChoiceSummary.axaml(.cs)`、`ManagedModelRow.axaml(.cs)`；`SettingsView.axaml` 的「资料」（回答与资料页）与「资料组件」（高级页）两个卡片；`AppState.Documents.cs`；`Platform/DocumentRuntimeInstaller.cs`、`DocumentAssets.cs`、`CodexDocumentReader.cs`、`DocumentPreparation.cs`。

## 同名视图（VIEW_PARITY 建议行）

| Mac 视图/类型 | Windows 对应物 | 位置 | 状态 |
|---|---|---|---|
| `DocumentReadingSettings`（View） | `DocumentReadingSettings`（UserControl） | Views/DocumentReadingSettings.axaml(.cs)，挂在 回答与资料 → 资料 卡片内 | 已实现、未验证 |
| `DocumentComponentsAdvanced`（View） | `DocumentComponentsAdvanced`（UserControl） | Views/DocumentComponentsAdvanced.axaml(.cs)，挂在 高级 → 资料组件 卡片内 | 已实现、未验证 |
| `DocumentChoiceSummary`（View，首启引导内的取舍说明） | `DocumentChoiceSummary`（Window） | Views/DocumentChoiceSummary.axaml(.cs)；入口为资料卡「读取资料」按钮（Windows 无首启引导步骤，改为设置页可开的小窗口） | 已实现、未验证 |
| `ManagedModelRow`（View） | `ManagedModelRow`（UserControl） | Views/ManagedModelRow.axaml(.cs)，由 DocumentComponentsAdvanced 为 `DocumentComponentRows` 三行生成 | 已实现、未验证 |
| `DocumentRuntimeInstaller`（enum/服务） | `DocumentRuntimeInstaller`（Platform class） | Platform/DocumentRuntimeInstaller.cs（隔离 venv + 内嵌 requirements lock + import 校验） | 已实现、未验证 |
| `DocumentRuntime`（enum/服务） | `DocumentAssets` + `EmbeddingModelStore` + `AppPaths`（无单独 Python 运行时，Windows 用本机已装 Python 3.12 建隔离环境） | Platform/DocumentAssets.cs 等 | 已实现、未验证（平台差异：Mac 自带可重定位 Python） |
| `CodexDocumentReader` | `CodexDocumentReader`（Platform static class） | Platform/CodexDocumentReader.cs（本机提取 + 回答服务 JSON 合同；无 document MCP） | 已实现、未验证 |
| `DocumentPreparation` | `DocumentPreparation`（Platform class） | Platform/DocumentPreparation.cs（DocumentJob 队列真实执行读取） | 已实现、未验证 |
| `DocumentFeatureSetup`（AppState 扩展：prepareDocumentFeature/scanLocalDocumentAssets） | `AppState.PrepareDocumentComponentsAsync` + `DocumentReadingSettings` 的「安装支持」入口 | AppState.Documents.cs / DocumentReadingSettings | 部分实现：prepare 已接；**扫描本机已有组件/选择目录未移植**（Windows 固定安装目录） |

## 逐元素对照

| # | Mac 元素 | Windows | 一致性 |
|---|---|---|---|
| 1 | 资料卡：`MenuPicker("读取资料的方式")`，选项 系统识别 / 本机增强（需下载…）/ Codex 代读（需准备/需先连接 Codex） | 同名 ComboBox（`Settings.DocumentProviderChoices`），选项 系统识别 / 本机增强 / Codex 代读 | ✅ |
| 2 | 未就绪：警告句「已选的识别方式还没准备好，当前读取会失败。」+「先用系统识别」 | 同文案 + `安装支持`（prepareDocumentFeature）+ `先用系统识别` | ✅（多一个 Windows 安装按钮承接 prepare） |
| 3 | 就绪时按 provider 显示帮助句（系统/本机增强/Codex 各自的说明） | `Settings.DocumentProviderHelp` 三句 Mac 原句 | ✅ |
| 4 | 进行中：`documentSetupStatus` + 进度 + 取消准备 | `DocumentSetupStatus` + ProgressBar（无取消按钮；取消需杀 Python/下载进程，另行处理） | ⚠ 缺「取消准备」 |
| 5 | 运行包未装时提示「导入 Word、Excel、PowerPoint 需要文档组件/最多下载约 186 MB」+ 安装 | 资料组件卡显示运行包行与安装按钮；资料卡仅显示警告与安装支持 | ⚠ 文案未用 Mac 的 186 MB 句（Windows 组件体积不同） |
| 6 | 构建 `DocumentComponentsAdvanced`：本机识别模型选择（ModelCatalog.localOCRChoices） | `本机识别模型` label + ManagedModelRow 三行（windows 固定 PaddleOCR-VL 1.6），无型号选择 | ⚠ 无多档模型选择（SETTINGS_PARITY localOCRModelID 已注明） |
| 7 | 查找方式：关键词/语义增强 两个 choice + 状态 + 启用语义增强 + 重建语义索引 | 查找方式分组：语义检索勾选 + Qwen3 说明 + ManagedModelRow(embedding) 安装/校验/删除 | ⚠ 缺「启用语义增强」按钮与「重建语义索引」（Windows 索引在开会议时构建） |
| 8 | Word/Excel/PowerPoint 支持（LegacyOfficeConverter 提示 + 安装支持） | Windows 用内置 OpenXML 解析，无需组件；DocLayout/OCR 行覆盖 | ⚠ 平台差异（已写） |
| 9 | 复用本机已有组件：扫描本机已有组件/选择其他目录… | 未移植（Windows 固定 `%LOCALAPPDATA%\NextThought` 路径） | ❌ 缺口 |
| 10 | 实际安装状态列表：`ModelCatalog.all` 逐行 ManagedModelRow（准备/校验／修复/删除 + 大小/license/summary + 模型来源链接） | `DocumentComponentRows`：运行包 / PaddleOCR-VL 1.6 + PP-DocLayoutV3 / Qwen3-Embedding 三行，`ManagedModelRow` 提供 安装 / 校验／修复 / 删除；显示名称/状态/体积 | ⚠ 行数=3（Windows 组件集），缺 license/summary/来源链接 |
| 11 | 共用文档组件行：已安装状态 + 校验／修复/安装 + 删除 + 取消下载 + `DocumentRuntimeInstaller.sizeDescription` | 由 ManagedModelRow(runtime) 承担安装/校验/删除；大小显示「运行包大小见发布清单」 | ⚠ 无取消下载按钮 |
| 12 | `ocrFallbackToVision` 开关 + 说明句 | 同名 CheckBox + Mac 说明句 | ✅ |
| 13 | provider=codex 时显示 Codex 模型/推理强度/每文件超时 | `IsVisible="{Binding Settings.ShowCodexDocumentSettings}"` 同三字段 | ✅ |
| 14 | `DocumentChoiceSummary`：读取资料/查找资料两段取舍 + 总结句，无安装操作 | Window 同两段（含总结句进第二段），关闭按钮；从资料卡「读取资料」打开 | ⚠ Mac 在首启引导内；Windows 设置页手动入口 |
| 15 | 首启引导 `DocumentFeatureSetup` 的 prepare 流程（选择即安装/回退） | `PrepareDocumentComponentsCommand`（安装运行包+模型）+ 回退开关；无首启引导步骤 | ⚠ 无引导步骤，入口在设置页 |
| 16 | 运行时下载：GitHub release 资产 + SHA-256 + 解压 + Python/codesign 校验 | `DocumentRuntimeInstaller`（本机 Python 3.12 → venv → 内嵌 requirements → import paddle）；`DocumentAssets`（HF 固定 revision + SHA-256 流式下载 + 校验/删除） | ⚠ 实现路径不同（Windows 无内置 Python/签名步骤），真实下载/校验未实测 |
| 17 | 模型下载进度/取消 | `DocumentSetupStatus` + ProgressBar；无取消 | ⚠ 缺取消 |

## 点按记录（`--demo --self-test=settings-presets` 新增步骤）

1. `document-components`：`RefreshDocumentComponents()` 后 `DocumentComponentRows` 三行（runtime/ocr/embedding），名称打印。
2. `document-guide`：资料卡「读取资料」→ `DocumentChoiceSummary` 窗口 Show/Close，IsVisible=true。
3. `document-runtime-download`：运行时/模型真实下载与校验**未执行**（需要联网 + 本机 64 位 Python 3.12；自测只验证入口/状态/组件行）。
4. 视图截图：`document-reading-settings-*`、`document-components-advanced-*`、`document-choice-summary-*`、`managed-model-row-*`（浅/深/英），由自测证据循环输出到 `%TEMP%\nextthought-selftest\evidence\`，再拷入本目录。

## 未解决

- 真实下载/校验路径未实测（联网 + Python 3.12 环境）；`DocumentRuntimeInstaller` 的 pip 安装耗时/失败分支只在源码层核对。
- 缺：取消下载/取消准备、扫描本机已有组件/选择目录、License/来源链接列、多档本机识别模型、启用语义增强/重建索引按钮、首启引导步骤。
- Mac 的 document MCP（原文件/图片读取）在 Windows 无对应，Codex 代读改为本机提取文本（详见 E23b 提交说明）；图片页走本机 OCR。
- 截图证据待从 self-test 证据目录拷入（工作区偶发被其他任务 WIP/运行实例锁阻塞时无法跑完整 `--screenshots`）。
