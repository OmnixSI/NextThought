# v1.7.0 本机验证

测试系统：Apple M2 Pro（16 GB、19 GPU 核），arm64，macOS **26.5.2**，Swift 6.1.2。产品兼容目标仍为 Apple Silicon / macOS 14+。这不是 macOS 14 真机验证：只对 190 个 Python／原生运行包文件检查了 Mach-O deployment target ≤14，并显式选用了 macOS14 MLX wheels。

## 固定六份样本

参考答案在跑识别之前人工编写，见 `Tools/Validation/Documents/Fixtures/reference.json`。全部为自绘／自建虚构资料，没有使用真实个人简历。样本为中文简历扫描 PDF、英文岗位数字 PDF、中英双栏扫描图、合并单元格与数字表、公式技术页、含嵌图 DOCX。系统与 Paddle 各自走真实本地管线（数字 PDF 的简单文本页使用 PDFKit），Luna 直接得到原文件路径并自行使用隔离工具。

最终输出保存在 [validation/v1.7.0](validation/v1.7.0)，包括 Markdown、块／定位元数据、版本和每文件耗时；`codex-tools.ndjson.gz` 是最终六次调用的原始 CLI 事件和工具读取过程。固定检查脚本为 `Tools/Validation/Documents/score.py`。

| 指标 | 系统 | Paddle Q8 + Layout | gpt-6-luna / medium / 标准 |
|---|---:|---:|---:|
| 完成文件 | 6/6 | 6/6 | 6/6 |
| 关键正文锚点 | 21/21 | 21/21 | 21/21 |
| 关键数字精确匹配 | 23/27 | 27/27 | 27/27 |
| 表格关键数字 | 3/6 | 6/6 | 6/6 |
| 表格行列与合并单元格（人工核对） | 丢失结构 | 5 行、3 列、colspan=2 正确 | 正确 |
| 三条公式语义（人工核对） | 0/3 | 3/3 | 3/3 |
| 双栏阅读顺序（人工核对） | 左列后右列 | 左列后右列 | 左列后右列 |
| Office 嵌图文字及正文插入位置 | 保留 | 保留 | 保留 |
| 观察到的新增事实／编造 | 无事实编造；公式出现乱码 | 无；曾观察到重复标题 | 无 |
| 这轮六文件累计耗时 | 2.3 s | 53.8 s | 149.1 s |

数字仅检查事先列明的 27 个关键值，正文只检查 21 个锚点；不是全字符准确率，也不是统计性通用基准。系统 OCR 将 `3,600` 读成 `3.600`，并把表格内三个千位逗号读成点。系统的正文主要有空格／标点差异，公式损坏严重。Paddle 和 Luna 的公式写法不同，但本轮数学语义一致。未将本机 Q8 结果等同上游原精度榜单。

耗时为实际冒烟记录，包含进程启动、模型加载、签名后二进制首次加载及 CLI 网络等待；期间本机还有验证／构建任务，不能据此宣称严格吞吐排名。Paddle 第一份用了 39.4 s，后续扫描图每份约 3.5–4.2 s；此前未重新签名的运行包同一六份约 24.9 s。Luna 逐文件约 14.5–54.2 s。精确记录见各 provider 的 summary.json。

迭代中曾发现两项测试问题并保留原始诊断于本机 `.local/document-validation`：最初公式字体缺少下标字形，已改为实际排版下标后重跑；CLI 第五次调用曾在已退出进程的 Foundation `waitUntilExit` 挂起，已改启动前登记 completion handler，主推理和 login/version 路径均替换，新增 20 次即时退出回归。另一次 Luna 曾把独立双栏正文改成关系表；加入明确的版面约束后，最终六次结果恢复正确。本报告只使用最终有效样本和最终成功结果，没有把失败结果冒充准确率。

## 模型与运行包

模型固定来源、revision、逐文件哈希见 `ModelCatalog.swift`。Qwen 通过 MIT 的 mlx-lm backbone、真实分词、官方 query instruction、末 token pooling、float32 L2 normalization 实际产生 1024 维向量。独立中文／英文相关 vs 无关相似度为 0.833/0.090、0.778/0.136；应用自检中文为 0.768/0.089。

运行包最终下载 **185,562,618 bytes**，安装文件总量 **631,880,552 bytes**（不含后续缓存及文件系统分配差异），61 个锁定包。实际安装测试从正式 tar.gz 校验哈希、验证 Python 签名、提取到新目录、运行健康协议。模型下载合计约 1.88 GB，运行包不计入该模型总量。

## 回归与边界

- 全量 Swift 单测：137 项通过，包括旧数据兼容、块引用、HTML/Markdown 长表、rowspan、公式边界和进程退出竞争。
- Python 协议／OTSL／阅读顺序单测：4 项通过。
- 既有 CodexTransport：并发 prewarm、取消 thread/start、迟到确认、取消后下一请求，以及 20 次即时 CLI 退出均通过；不会调用在线模型。
- 专用文档边界自检：空／超限文件、加密 PDF、扫描+数字混合 PDF、物理页码、默认明确降级、严格模式、取消及 worker 恢复通过。
- 生命周期自检：工作区切换、迟到索引拒绝、资料禁用／删除、工作区缓存隔离、未变块零新增 embedding 调用通过。
- 模型文件自检：真实固定小文件下载、校验文件复用、损坏修复、真实下载取消、删除后无迟到写回通过。
- MCP 实际子进程：仅五个只读工具，越界路径／`..`／联网参数／写入工具与参数、越界页号、原件篡改和符号链接替换均拒绝。

独立审查列出的资源打包、长 HTML 表格、Office 取消、工作表展开、图片定位、PDF 覆盖、TXT 基础导入、共享归档删除、状态进程等待和回答取消后联网等缺口均已修复。最终 release build、四项既有 App 自检、PR 检查和发布资产验证结果在发布前补记。

## 显式自检

必须使用独立 `.ci` bundle identifier 的 App 和项目内测试目录；不能对日常数据目录运行破坏性 data 自检。

```sh
export INTERVIEWHACKER_DATA_DIR="$PWD/.local/document-test/data"
export INTERVIEWHACKER_RUNTIME_DIR="$PWD/.local/document-test/runtime"
export INTERVIEWHACKER_DOCUMENT_MODELS_DIR="$PWD/.local/document-test/models"
APP="/absolute/path/to/InterviewHacker-ci.app/Contents/MacOS/InterviewHacker"
"$APP" --self-test runtime --archive dist/InterviewHacker-document-runtime-1.7.0-macOS-arm64.tar.gz
"$APP" --self-test models document-edges --fixtures Tools/Validation/Documents/Fixtures
INTERVIEWHACKER_DEMO=1 "$APP" --self-test document-lifecycle model-files
# 在线对照仅显式调用，不加入日常 CI：
"$APP" --self-test documents --provider codex --input Tools/Validation/Documents/Fixtures --output .local/document-test/codex
```

补充实际自检：运行中删除 Qwen 模型（使用项目内可丢弃的硬链接副本）通过；索引取消、worker 卸载、模型与索引清理后均没有迟到重建。正式 tar.gz 二次修复安装通过。结构化备份恢复保留 Markdown、表格／公式、warnings、真实页码与相对路径，且不包含原件、模型和运行包。

正式 App 的离线隔离验证已通过：Seatbelt 拒绝网络、仓库 Sources／Resources／Tools 和 /opt/homebrew、/usr/local 的读取，仍能使用随包 helper 与新目录中的运行包完成 Paddle 公式识别。实际 MCP 读取双页 PPT 的两个不同嵌图后，图片 ID 与幻灯片对应正确；App codesign --deep --strict 再次通过，资源目录没有产生 Python bytecode 缓存。

移除了旧的 2,000 检索片段截断；第 2,105 个片段仍可被关键词检索的回归已通过，全文导入后的尾部材料不会因该旧上限消失。
