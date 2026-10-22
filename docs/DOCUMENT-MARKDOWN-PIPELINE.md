# 文档管线 v1.7.0

资料长期保存为完整 Markdown、`DocumentContent` 元数据与内容块。原件仅在导入期间使用；Codex 使用字节相同的任务临时副本，任务结束删除。旧资料仍可关键词检索和重建向量；重新识别必须重新选择原件。

## 接口选择

设置 → 文档模型提供三种识别接口：

- 系统 OCR：默认、离线、零权重下载。PDFKit 提取简单文本页；扫描页、复杂页和图片使用 Apple Vision。
- PaddleOCR-VL-1.6 Q8：MLX／Metal，配合官方 PP-DocLayoutV3 ONNX。检测区域、按任务识别正文／OTSL 表格／LaTeX 公式，去除包含关系中的重复区域，按独立列与跨列标题重建顺序。失败默认用系统 OCR 补读并记录页码、原因；关闭补读即严格模式。不会自动转到 Codex。
- Codex CLI：现有 ChatGPT 登录，默认 `gpt-6-luna`、medium、标准速度、600 秒／文件。独立 ephemeral 任务只收到原文件临时路径。应用不预拆 PDF；CLI 经专属本地 MCP 工具按需读取文本、Office 结构、PDF 页或嵌图。

Codex 使用 `--ignore-user-config --ignore-rules`，禁用 shell、搜索、apps 和 multi-agent；deny-root 文件权限配置仅额外开放本任务和工具运行路径，保留 CLI 必需的平台最小读取集。MCP 单独校验任务清单、哈希、路径和文件标识，只暴露五个只读工具；其进程不依赖 CLI shell 沙箱来保护文件。没有新 OAuth 或 API 密钥。

## 格式、边界与定位

Office 使用运行包内的 MarkItDown；PPTX／XLSX 同时用其依赖的原生包 API 保留真实 slide／sheet，嵌图在 DOCX 正文锚点、PPT shape 或工作表位置归属处插入识别内容。XLSX 的图像以所属工作表内的嵌图块表示。复杂 Office 视觉版面建议导出 PDF。旧 doc／ODT／RTF 保留现有结构提取路线。

TXT／Markdown／CSV／JSON 等基础格式无需运行包。导入上限为每次 60 文件、每文件 20 MiB／20 万字符、工作区 40 文件／60 万字符、PDF 400 页；超限会明确失败，不静默少读页面。Office ZIP 解压内容上限 100 MiB，工作表展开区域上限 20 万单元格。

`DocumentLocator` 分开表达 PDF page、slide、sheet、headingPath 和旧资料 section。旧分段不再冒充页码。`DocumentContent` 包含完整块、warnings、complete/degraded、源文件 SHA-256 和组件版本。定位不依赖检索 chunk 序号。Codex PDF 会检查全部实际页码覆盖，缺页拒绝导入。

## 运行包与模型

应用下载固定 SHA-256 的可重定位 CPython 3.12.13 运行包及锁定依赖；用户无需 Python、pip、Homebrew。模型固定 revision 和每文件 SHA-256 见 `ModelCatalog.swift`。下载可取消、重试、复用已校验文件和修复损坏；启动时后台完整校验，UI 使用缓存与文件大小，不每次提问哈希 GB 权重。删除先取消任务、卸载 worker，再移除文件和对应索引。

Swift／Python 使用 version=1 NDJSON：health、convert、ocr、prepare_embedding、embed、cancel，响应 result/error/progress；stdout 仅协议。Paddle／Office 取消会终止进程，避免失控解析占据队列；向量查询 500 ms 截止后使用关键词结果。

## 检索、备份与并发

Qwen3-Embedding-0.6B Q8 通过真实 tokenizer、官方检索 query instruction、最后 token pooling 和 float32 L2 normalization 输出 1024 维向量。关键词与语义共用标题树切片；表格／公式不按固定字符截断，长 Markdown／HTML 表格按完整行组切开并重复表头，rowspan 行保持同组，chunk 的 blockID 可关联完整原表。

向量缓存包含工作区、模型 revision、切片版本和内容 fingerprint。材料变化取消当前回答和讲稿任务；索引提交校验 workspace、generation、资料 revision。未变化块按 fingerprint 复用，后台索引在回答／实时会话期间等待。回答对语义查询最多等待 500 ms，再用关键词；回答、联网与讲稿采用完整 chunk 的统一预算。

备份写入完整 Markdown 和元数据，使用相对引用。恢复从可信内联正文重建归档，不读取备份中的任意绝对路径。模型、原件、Python 运行包和可重建向量缓存均不进入备份。

## 验证

固定六份自绘样本、参考答案、原始输出、工具轨迹和本机报告见 [v1.7.0 验证](VALIDATION-v1.7.0.md)。在线对照必须显式运行，日常 CI 不调用 Luna。自检目录和命令见验证报告。


## 本机已有组件发现与复用

设置 → 资料提供“扫描本机已有组件”和目录选择。只查找当前签名目录清单支持的文档模型及固定文档运行包，不将任意模型架构视为兼容。准备组件与高级逐项准备也先扫描，之后仅下载缺失或损坏文件。

扫描先查询 Spotlight 文件名索引（包括已索引外置磁盘），并遍历应用模型目录、常见 Hugging Face 缓存、Downloads、Documents 和用户目录；隐藏缓存可发现，跳过 Git、构建、依赖、App 包等目录。单次最多 30 万条目录项／20 秒，索引查询最多 5 秒；受权限或预算影响的扫描明确提示，可指定具体目录补查。目录符号链接不递归跟随，HF snapshot 的文件符号链接可读取校验。

候选文件先核对大小，再逐文件核对固定 SHA-256。可复用不完整快照中的有效文件，导入暂存文件后再次校验再提交；完整模型才写入当前 revision 标记。原缓存不变，应用拥有自己可删除的模型副本。运行包还通过原有归档哈希、Python 签名、可重定位检查与安装流程。仅扫描绝不调用网络下载或改变用户接口选择。扫描／哈希在后台且可取消；删除模型和运行包会先等待扫描取消，避免晚到写回。已安装状态仍使用现有启动检查与内存缓存，不在每次提问或界面刷新遍历磁盘。

隔离集成自检：`INTERVIEWHACKER_DATA_DIR=<scratch> InterviewHacker --self-test local-models --input <existing-model-root> --archive-directory <runtime-archive-directory>`。该自检只复用显式目录，无在线调用；可随后运行 models 和 documents 自检验证真实推理。
