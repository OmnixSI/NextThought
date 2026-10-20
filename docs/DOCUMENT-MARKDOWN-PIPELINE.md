# 资料 → 结构化 Markdown 管线

> 需求 1：场景里可能导入 PDF、PPT、Word 等各种格式的文件，需要能把不同规格的文件统一转成结构化 Markdown。

## 1. 结论

导入的资料现在一律先转成**结构化 Markdown**，再进检索索引。转换分两层：

1. **内置转换（默认，零依赖）** —— 纯 Swift、全部在本机完成，不联网、不加载模型、不引入任何第三方代码。
   覆盖：PDF（含扫描件 OCR）、Word（.docx）、OpenDocument（.odt）、RTF / 旧版 .doc、PowerPoint（.pptx）、
   Excel（.xlsx）、CSV、HTML、Markdown、TXT、JSON，以及图片（Vision OCR）。
2. **外部转换器（可选，自动发现）** —— 如果本机装了 **Microsoft MarkItDown** 或 **Pandoc**，
   导入 Word / ODT / PPTX / Excel 时优先用它们，因为它们对复杂版式的处理比内置实现更细。
   两个工具都完全在本机运行，不上传任何文件；调用失败或超时自动回退到内置转换。

资料转好后会被**静默归档**成 `.md` 文件（`~/Library/Application Support/InterviewHacker/Materials/<场景ID>/`），
文件头带来源格式和归档时间。归档在后台完成，不弹窗、不打断操作。

## 2. 开源方案调研

目标是找一个"什么格式都能转 Markdown"的现成项目。2026 年仍在活跃维护、且有 CLI 的主要有这五个：

| 项目 | 许可证 | 覆盖格式 | 运行开销 | CLI |
|---|---|---|---|---|
| [Microsoft MarkItDown](https://github.com/microsoft/markitdown) | MIT | PDF / PPTX / DOCX / XLSX / 图片(OCR) / 音频 / HTML / CSV / JSON / XML / ZIP / EPUB | 六个核心依赖，**不用模型、不用 PyTorch** | `markitdown file.pdf -o out.md` |
| [Pandoc](https://pandoc.org) | GPL | docx / odt / rtf / html / markdown / csv（**不读** pptx、xlsx、pdf） | 单个二进制 | `pandoc -f docx -t gfm file.docx` |
| [Docling](https://github.com/docling-project/docling) | MIT（IBM / LF AI & Data） | 36 种输入，PDF / Office / HTML / 图片 / EPUB | 可只跑 CPU，OCR 引擎可换 | `docling convert report.pdf --to md` |
| [MinerU](https://github.com/opendatalab/MinerU) | MinerU 开源协议（基于 Apache-2.0，附加条件） | PDF / 图片 / Office / ODT / EPUB / HTML / CSV | basic 档可纯 CPU，standard 档要 8 GB 内存以上，最好有 GPU | `mineru-kit parse report.pdf -o report.md --tier standard` |
| [Marker](https://github.com/datalab-to/marker) | 代码 Apache-2.0，模型权重另有 OpenRAIL-M 条款 | PDF / 图片 / PPTX / DOCX / XLSX / HTML / EPUB → markdown/json/html/chunks | fast 档纯 CPU，balanced 档要 GPU | `marker_single report.pdf --output_dir out/` |

公开基准（[pdf-to-markdown-benchmark](https://github.com/pdfmarkdownapp/pdf-to-markdown-benchmark)，
在论文、日财报、法律文书、纯表格页、1973 年扫描件五类难文档上）：

| 工具 | 论文 | 日财报 | 法律 | 表格 | 扫描件 | 平均 | 静默错误 |
|---|---|---|---|---|---|---|---|
| MinerU | 99 | 95 | 67 | 100 | 71 | **86** | 0 |
| Marker | 95 | 87 | 82 | 96 | 71 | **86** | 0 |
| Docling | 63 | 80 | 90 | 92 | 61 | 77 | 0 |
| PyMuPDF4LLM | 61 | 90 | 90 | 82 | 58 | 76 | 0 |
| MarkItDown | 51 | 65 | 70 | 66 | 33 | 57 | 0 |

在 olmOCR-bench 上：Marker balanced 76.0%、MinerU pipeline 72.7%、Docling 50.3%。

### 为什么默认不直接用这些项目

- **MarkItDown** 是最贴近"一个工具统一所有格式"的开源项目，质量对简单文档足够，
  但基准里排在最后（平均 57 分），而且要装 Python 环境和六个依赖。
  → 所以本项目把它做成**可选加速器**，而不是依赖。
- **Marker / MinerU / Docling** 质量最好，但要么要 GPU，要么要 PyTorch / ONNX 运行时和几 GB 内存。
  InterviewHacker 坚持"不打包第三方代码、全部本机处理、开箱即用"，把 2 GB 的模型塞进一个
  几百 MB 的 App 并不划算。
  → 所以文档写清楚怎么用它们处理**特别难的 PDF**，产出的 Markdown 可以再拖回场景里导入。
- **Pandoc** 读取 docx/odt/rtf/html 的质量好，而且是单个二进制、无需 Python。
  → 做成**可选加速器**，优先级排在 MarkItDown 之后（因为它读不了 pptx / xlsx / pdf）。

## 3. 架构

```
用户拖入 / 选择文件（可多个、可整个文件夹）
        │
        ▼
DocumentImporter.expand()          展开目录，只保留受支持的格式
        │
        ▼
DocumentImporter.read()             逐个转换；单个文件失败不影响其它文件
        │
        ├─► ExternalMarkdownConverter   装了 MarkItDown / Pandoc 就先用（仅 Office 格式）
        │      └─ 失败或超时 → 回退
        ▼
MarkdownConverter.convert()         内置转换，输出统一结构
        │                              标题 / 各级小标题 / 列表 / 表格 / 引用
        ▼
DocumentClassifier.classify()       按文件名 + 正文开头，自动归入场景的资料分类
        │
        ▼
MaterialArchive.archive()           静默归档成 .md（带 YAML 头）
        │
        ▼
ContextDocument(pages: sections)    每个小标题一个片段，进检索索引
        │
        ▼
rebuildContextIndex()               回答时按问题取相关片段给模型
```

### 文件位置

| 文件 | 作用 |
|---|---|
| `Sources/InterviewHacker/MarkdownConverter.swift` | 内置转换：各格式 → 结构化 Markdown |
| `Sources/InterviewHacker/ExternalMarkdownConverter.swift` | 发现并调用 MarkItDown / Pandoc |
| `Sources/InterviewHacker/DocumentImporter.swift` | 批量读取、逐个容错、结果汇总 |
| `Sources/InterviewHacker/MaterialArchive.swift` | 静默归档成 `.md` |
| `Sources/InterviewHackerCore/DocumentClassifier.swift` | 自动归类 |

### 各格式的转换方式

| 格式 | 内置做法 | 产出结构 |
|---|---|---|
| PDF | PDFKit 取文字；扫描页（文字 < 8 字）渲染后 Vision OCR，最多 80 页 | 每页一个 `## p.N` 小节 |
| Word .docx | 解包 `word/document.xml`，读 `w:pStyle` 的 Heading 级别、`w:numPr` 列表、`w:tbl` 表格 | 真实标题层级 + 列表 + 表格 |
| ODT .odt | 解包 `content.xml`，读 `text:outline-level` 和 `text:list` | 标题层级 + 列表 |
| .doc / .rtf | 系统 `NSAttributedString` 取正文，短独立行按版式判定为小标题 | 段落 + 推断的小标题 |
| PowerPoint | 解包 `ppt/slides/slideN.xml`，按 `a:pPr lvl` 还原缩进 | 每页一个 `## 幻灯片 N` |
| Excel | 解包 `xl/workbook.xml` + `sharedStrings.xml` + `worksheets/sheetN.xml`，按 `r="B7"` 补齐空列 | 每个工作表一张 Markdown 表格 |
| CSV | 自己写的 RFC 4180 解析（引号内可含逗号和换行） | 一张 Markdown 表格 |
| HTML | 正则分块提取 h1–h6 / p / li / blockquote / pre / table | 标题、列表、引用、表格 |
| 图片 | Vision OCR（中英） | 一个小节 + 识别出的每行文字 |
| TXT / JSON | 按空行分块，短独立行判为小标题 | 段落 + 小标题 |
| Markdown | 保留原有标题层级 | 原样 |

### 归档格式

`Materials/<场景ID>/<文件名>.md`：

```markdown
---
source-format: pdf
archived-at: 2026-10-20T03:14:22Z
---

## p.1
...
```

## 4. 开启外部转换器

```bash
# MarkItDown（推荐，六依赖，无需模型）
python3 -m venv ~/.venvs/markitdown
~/.venvs/markitdown/bin/pip install 'markitdown[all]'
# 让应用能找到它：
ln -s ~/.venvs/markitdown/bin/markitdown /opt/homebrew/bin/markitdown
```

```bash
# Pandoc（单个二进制，只对 docx / odt / rtf / html / csv 生效）
brew install pandoc
```

应用启动时会扫描 `PATH` 以及 `/opt/homebrew/bin`、`/usr/local/bin`、`/usr/bin`、`/bin`。
找到就直接用，**不需要在设置里打开任何开关**；导入的资料列表里会显示实际使用的转换器
（`内置` / `MarkItDown` / `Pandoc`）。

## 5. 处理特别难的 PDF（Marker / MinerU / Docling）

当 PDF 是扫描件、公式很多或表格极复杂时，可以用下面任一工具离线转好，再把 `.md` 拖回场景导入：

```bash
# Marker：fast 档纯 CPU
marker_single report.pdf --output_dir out/

# MinerU：basic 档纯 CPU
mineru-kit parse report.pdf -o report.md --tier basic

# Docling：可选 OCR 引擎
docling convert report.pdf --to md --ocr-engine rapidocr
```

## 6. 边界

- 单文件上限 20 MiB，单文件文字上限 20 万字，单场景资料总量上限 60 万字。
- 每次最多导入 60 个文件，单场景最多 40 份资料。
- 只读取用户选中的本地文件；不执行宏、脚本，不抓取远程资源。
- 转换在后台线程进行，导入期间界面不卡死。
