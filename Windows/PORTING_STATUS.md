
# P6 阶段复盘提示词 · 0.72.0

0.72.0 移植 ``StageReviewPrompt``（SessionRecord.swift 末尾），测试 604 → 610。

- **系统提示**：同一个场景分区多次会话 + 资料要点 + 已有经验；声明输入只是数据、不执行其中指令；
  固定结构（总体进展/高频问题/薄弱点/已改进/下一步准备/经验沉淀）。
- **会话预算**：新的在前挑，块 = ``### 标题（日期）\n摘要``；放不下就停；最终按时间正序返回。
- **用户提示**：场景分区与模式、资料要点（前 3000 字）、已有经验（前 2000 字）、各次会话记录。
- **自检**：``--acceptance-check`` 增加第 21 项（分区头、会话块、系统结构）。
- 6 项测试：结构、正序选择、预算淘汰、资料/经验、截断、空输入。

统计：已移植 56 / 部分 24 / 基线 0 / 未移植 0。

# P2 Markdown 导出 · 0.71.0


0.71.0 移植 ``SessionMarkdownExporter``（SessionRecord.swift 的导出段）并适配 Windows 投影/转写模型，测试 597 → 604。

- **标题与事实**：优先摘要标题，否则用会议标题；第二行是「模式 · 本地开始时间 · 分钟 · 音频来源」。
- **新鲜度提示**：Stale → 「部分纪要来自较旧转写版本」；Unknown → 「旧纪要未记录转写版本」；Current 不提示。
- **正文**：有摘要时去掉摘要自带的标题段（从第一个 ``## `` 开始）；否则有检测问题时列出编号问题。
- **会中回答**：标题最多 200 字、换行转空格；未完成回答加提示。
- **完整转录**：``HH:mm`` 偏移、``**我/对方**`` 标签、正文；有译文时另起一行 ``*译文：* …``。
- **自检**：``--acceptance-check`` 增加第 20 项（标题、完整转录段、正文）。
- 7 项测试：标题/事实、摘要去标题、Stale/Unknown 提示、未完成回答、问题列表、时间戳/说话人/译文、空会议。

统计：已移植 56 / 部分 24 / 基线 0 / 未移植 0。

# P2 摘要 Markdown 解析 · 0.70.0


0.70.0 移植 ``SessionSummaryParser``（SessionRecord.swift 的解析段），测试 589 → 597。

- **标题**：``# 标题`` 段第一行，去掉 ``* _ ` `` 与空白，最长 60 字。
- **问题**：``# 问题`` 段接受数字/``-``/``*``/``•`` 列表标记；去掉末尾的 ``——``/``—``/``--``/``-``/``｜``/``|``；
  ``我的回答要点`` 之后作为回答要点；无标记的行忽略。
- **经验**：``# 经验`` 段接受列表标记或 ``·`` 开头，空行忽略。
- **题库**：``# 题库`` 段识别 ``问：/问:/Q：/Q:/**问**：`` 与 ``答：/答:/A：/A:/**答**：``；
  答案的连续行并入同一答案；新问题或新段落先结算上一条。
- **自检**：``--acceptance-check`` 增加第 19 项（标题/问题要点/经验/题库各一条）。
- 8 项测试：标题截断、问题标记/后缀/要点、无标记忽略、经验两种符号、连续答案、前缀变体、段落结算、空输入。

统计：已移植 56 / 部分 24 / 基线 0 / 未移植 0。

# P6 会话删除与知识清理 · 0.69.0


0.69.0 补上从系列删除会议时的知识清理与墓碑，测试 582 → 589。

- **墓碑**：删除的会议 id 记入 ``sourceDeletionTombstones``（去重），防止旧备份把知识「复活」。
- **经验**：只有这一场来源的经验被删；多来源的只去掉该来源并改挂剩余来源；``retainLessons`` 时保留正文、
  记录 ``sourceMissingSessionIDs`` 并标记 ``independentlyRetained``。
- **候选**：来源是这场会议的待确认候选直接丢弃，其它保留。
- **决定/待办**：正文保留供重新确认，但 ``sourceMissing=true`` 且清空引文与位置。
- **题库**：不随会议删除（知识独立存在）。
- **自检**：``--acceptance-check`` 增加第 18 项（删除后墓碑、成员、决定标记）。
- 7 项测试：非成员不变、成员/墓碑去重、单/多来源经验、手动保留、候选清理、决定标记、题库保留。

统计：已移植 56 / 部分 24 / 基线 0 / 未移植 0。

# P6 系列决定与待办 · 0.68.0


0.68.0 移植 ``WorkspaceSeriesItem`` 并把决定/待办接进系列简报，测试 575 → 582。

- **模型**：决定/待办两种类型、责任人与截止（文本 + 日期）、行动状态（open/in-progress/done/cancelled/superseded）、
  证据锚点（来源会议、证据 id、结论/来源修订、引文、位置、转写修订）、源已删除/独立保留标记、修订历史。
- **简报**：``useKnowledge`` 打开时按系列成员校验来源：来源仍在 → 活跃；来源已删且用户明确保留 → 活跃，
  否则进过期；来源不在系列 → 标记 sourceMissing 并清空引文/位置后进过期。
- **排序**：决定按更新时间倒序；待办按截止日期升序、无期限最后、同日期按更新时间倒序；已完成/取消/被替代按更新时间倒序。
- **自检**：``--acceptance-check`` 增加第 17 项（系列决定进入简报）。
- 7 项测试：决定排序、待办截止排序、已解决行动、来源删除/独立保留、跨系列来源、关闭知识、修订历史。

统计：已移植 56 / 部分 24 / 基线 0 / 未移植 0（sourceDeletionTombstones 仍缺）。

# P2 会话存储验收与核心清单收口 · 0.67.0


0.67.0 给验收加第 16 项（真实 `SessionStore` 往返），并把 `SessionRecord.swift`（740 行）的
「基线简化版」改为「部分移植（等价实现）」，核心文件清单不再有未移植/基线行。

- **会话存储验收**：临时数据目录里 `CreateAsync` → `AppendTranscriptAsync` → `EndAsync` →
  `Load`，校验标题与转写正文；说明 Windows 的 Models + SessionStore 能完成 Mac 会话记录的核心落盘/重载。
- **口径**：`SessionRecord` 的会话元数据、转写行、答案、摘要、恢复中断记录已有等价实现；
  未覆盖的是汇总提示词、Markdown 导出器等周边。
- 统计：已移植 56 / 部分移植 24 / 基线 0 / 未移植 0（`CORE_PARITY.md` 第 94 行）。
- 测试数保持 575；安装版验收 **16/16**。

# P7 退出后回滚入口 · 0.66.0

0.66.0 补上「回滚到上一版本」的生成脚本与设置页入口，测试 572 → 575。

- **回滚脚本**：`UpdateRestore.Script` 生成 PowerShell：等待当前进程退出（最多 60 秒）→ 用备份覆盖程序目录 →
  重新启动 NextThought；路径单引号转义，错误即停。
- **启动方式**：`UpdateRestore.Launch` 把脚本写到临时目录并以隐藏窗口 detached 启动，调用方随后退出。
- **设置页**：更新区新增「回滚到上一版本」按钮，仅当 `HasUpdateBackup`（存在备份目录）时显示；
  点击后启动脚本并触发退出。
- **自检**：`--update-restore-check` 生成脚本并校验等待/复制/重启三步（`qa\install-smoke-0660\restore.json`）。
- 3 项测试：脚本三步、路径引号转义、错误即停。
- 说明：页面缓存上限实验（1/3 页）实测不降工作集（约 190 MiB），已回退为缓存全部页面；
  0.59.0 的「启动只建首页」保留。

# P7 更新备份与回滚 · 0.65.0

0.65.0 给应用内自动更新加上「替换前备份当前版本」，测试 566 → 572。

- **备份**：启动安装包前把当前程序目录（含 runtimes 子目录）整体复制到
  `%LOCALAPPDATA%\NextThought\UpdateBackups\<版本>-<时间戳>`，只留最新 2 份；备份失败不阻塞更新，
  但会在状态栏明确提示。
- **回滚**：`UpdateBackup.Restore` 把备份覆盖回程序目录（保留备份里没有的新文件），供更新失败后恢复。
- **自检**：`--update-backup-check` 在临时目录建程序目录、备份、改文件、恢复并校验
  （`qa\install-smoke-0650\backup.json`）；`--acceptance-check` 增加第 15 项。
- 6 项测试：整目录复制、最新优先排序、只留 2 份、覆盖恢复、非法版本/缺失目录、空备份根。
- 未完成：恢复入口还没有 UI 按钮，安装包仍未做 Authenticode 签名（`sign_release.ps1` 仍是 EV 证书占位）。

# P3 Zipformer 模型描述 · 0.64.0

0.64.0 移植 `ZipformerStreaming.swift`（496 行）的模型/热词/错误部分，测试 557 → 566。

- **模型文件名**：encoder/decoder/joiner/tokens/bpe.vocab 与 16 kHz 采样率常量。
- **在线模型**：目录、四个文件名、SentencePiece 词表、modelingUnit、modelType；`LegacyBilingual`
  （cjkchar+bpe + zipformer）与 `XasrStreaming`（encoder.int8/decoder/joiner.int8、modelType 留空）两套预设；
  `RequiredFiles` 与 `HasVocabulary` 检查实际文件。
- **离线模型**：同样的文件描述与 `XasrOffline` 预设，用于整句重解码。
- **热词**：按中英文逗号/顿号/分号/换行切分、去空白；写入临时文件供识别器加载。
- **结果与错误**：`ZipformerHypothesis`（文本、是否终稿、起止、推理/音频秒数）；
  五种运行时错误与 Mac 相同的中文文案。
- **自检**：`--acceptance-check` 增加第 14 项，校验预设文件名、词表与热词切分
  （`qa\install-smoke-0640\acceptance.json`）。
- 9 项测试：常量、Legacy 预设、X-ASR 在线/离线、必需文件与词表、热词切分/写入、结果计时、错误文案。
- 未移植：`ZipformerRuntime` 的 native sherpa-onnx 调用与双流管理；Windows 由 `SpeechRecognizerSession` +
  `Utf8SherpaInterop` 直接承担（真实 WAV 转写已有证据）。

统计：已移植 56 / 部分 23 / 基线 1 / 未移植 1。

# P6 系列文档归档 · 0.63.0

0.63.0 移植 `WorkspaceArchiveOwnership.swift`（35 行），按 Windows 的资料模型适配，测试 551 → 557。

- **归属目录**：系列在 `Materials/<seriesId>/` 下拥有提取后的 `<documentId>.md` 与 `<documentId>.json`。
- **修复**：逐份资料确保两个文件存在；已存在则复用、绝不覆盖（人工改过的正文保留）；缺失时用块文本拼 Markdown、
  用资料元数据写 JSON，均为临时文件 + 移动的原子写。
- **安全**：资料/系列 id 含路径分隔符或 `..` 时拒绝，避免写出目录。
- **差异**：Windows 资料是全局内容寻址共享，系列记录没有 `markdownPath` 可回写，因此返回归档清单而不是改系列。
- **自检**：`--acceptance-check` 增加第 13 项，真实目录生成 md/json 并校验存在
  （`qa\install-smoke-0630\acceptance.json`）。
- 6 项测试：目录/内容、幂等且保留手改、无块资料、多份顺序、非法 id、空清单。

统计：已移植 56 / 部分 22 / 基线 1 / 未移植 2。

# P2 会话投影模型 · 0.62.0

0.62.0 移植 `SessionTextProjection.swift`（497 行）的投影模型部分，测试 544 → 551。

- **选项**：转写版本（原文/校对后）与语言视图（原文/译文/双语），默认校对后 + 双语。
- **投影文本**：稳定 id、类型（转写/校对转写/译文/问题/回答/摘要/纪要）、来源 id、行 id、位置、
  来源修订、过期标记、来源状态、正文与可搜索的备用写法。
- **回答/摘要**：回答携带问题、答案、来源修订与完成状态（缺省/完成算完成，中断/失败不算）；
  摘要携带标题、Markdown、来源转写修订与新鲜度。
- **整体新鲜度**：搜索文本与摘要里任一 Stale → Stale；否则任一 Unknown → Unknown；否则 Current；
  纯原文转写视为 Current。
- **自检**：`--acceptance-check` 增加第 12 项，验证 Stale 优先与中断回答不算完成
  （`qa\install-smoke-0620\acceptance.json`）。
- 7 项测试：默认选项、投影文本定位/备用写法、回答完成状态、Stale/Unknown/Current、摘要默认未知。
- 未移植：转写行投影（`SessionProjectedTranscriptLine`）、笔记/检测问题与 `SessionProjectionBuilder` 主体。

统计：已移植 56 / 部分 21 / 基线 1 / 未移植 3。

# P2 验收汇总与 QuestionIntent 口径 · 0.61.0

0.61.0 增加一次进程内验收汇总，并把 ``QuestionIntent.swift``（532 行）的 Windows 等价重写按「已移植（等价重写）」记录。

- **`--acceptance-check`**：一次运行验证 11 项核心能力——术语检索展开、会话内容检索、历史检索标签、
  系列简报、备份清单/密钥、会议计划、证据文案、用户内容存储、工作区删除暂存回滚、文档任务进度、归档追加/残留。
  安装版实测 **11/11 通过**（``qa\install-smoke-0610\acceptance.json``）。
- **QuestionIntent 口径**：Windows 侧不是逐行复制，而是等价重写（阈值、追问、候选跟踪、评测器、词表）；
  139 条语料 82 问全部命中、57 陈述 0 误报（``qa\question-0440\question-check.json``），因此从「基线简化版」改为「已移植（等价重写）」，
  并在 ``CORE_PARITY.md`` 注明非逐行。
- 测试数保持 544；安装版启动 1.52 秒。

统计：已移植 56 / 部分 20 / 基线 1 / 未移植 4。

# P1 归档写入 · 0.60.0


0.60.0 移植 `SessionArchivePersistence.swift`（513 行）的归档写入基础，测试 533 → 544。

- **目录读取**：缺失目录默认视为「新归档」，若已知应有内容则报「原记录未显示或删除」；其它读错误带原因上报，
  不会把不可读误当成空归档。
- **内容标记**：`sessions-archive-present-v1` 写在 Sessions 目录之外，目录丢失后仍能区分「从未用过」与「记录丢失」。
- **写入收据**：只跟踪正修订号；确认持久化修订 N 会清掉 ≤N 的待写项，返回是否确有确认。
- **文件写入**：追加前补齐缺失的换行（保留老版本没有结尾换行的最后一条记录），追加失败回滚到原长度；
  替换走临时文件 + 覆盖移动，避免半写；`AppendLine` 以 UTF-8 追加一行。
- **残留判定**：没有 `meta.json` 且不含任何录制文件（transcript/questions/answers/context/translations/voices）
  的目录是「残留」而非损坏会议；隐藏文件不参与判断。
- **自检**：`--archive-persistence-check` 写标记、追加两行、替换 meta 并验证收据/残留判定
  （`qa\archive-0600\archive-check.json`）。
- 11 项测试：缺失/已知缺失/读错误/内容、标记幂等、收据、追加换行修复、保留换行、替换、委托写入、残留判定。
- 未移植：会话迁移发布、翻译写队列与元数据读取结果（依赖完整会话模型）。

统计：已移植 55 / 部分 20 / 基线 2 / 未移植 4。

# P7 页面懒加载与内存 · 0.59.0

0.59.0 把主窗口的 7 个页面从「启动时全部构造」改成「首次显示时构造并缓存」，测试数保持 533。

- **懒加载**：`MainWindow.axaml` 的 7 个 `IsVisible` 页面换成一个 `ContentControl`；代码后置按 `AppState.Section`
  首次显示时创建页面并缓存，切换只改 `Content`。启动只构造首页。
- **自检**：`--page-check` 依次切到 7 个页面并记录页面类型与工作集：
  初始 1 页 **161.7 MiB**，访问全部后 **189.7 MiB**（源码构建；安装版 161.7 / 189.7）。
- **空闲测量**（源码构建，启动 6 秒后）：工作集 **165.2 MiB**、私有提交 **148.7 MiB**、线程 117；
  较 0.58.0 的 168–173 / 186–191 下降。目标 150 MiB 工作集仍未达到，但私有提交已低于 150 MiB。
- **启动**：安装版 `--startup-check` 1.531 秒（0.58.0 为 1.608 秒）。
- **界面验证**：`--section=notes` 截图确认懒加载后的纪要页正常渲染（`qa\page-0590\notes.png`）。

# P5 文档任务模型 · 0.58.0


0.58.0 移植 `DocumentJobs.swift`（466 行）的任务模型部分，测试 523 → 533。

- **限制**：512 MB 单文件、2000 页、2 GB 展开后上限。
- **DocumentJob**：工作区、文件名、原始路径/哈希、分类、提供方/模型/推理强度/回退/超时、阶段、进度、等待原因、
  文档 id、时间戳；`Create` 填默认值；`ProgressText` 在知道总数时显示 `已完成/总数`。
- **阶段**：13 个阶段的中文文案，`Receiving/Queued/Parsing/Ocr/...`；终态只有 Ready/Warning/Failed/Cancelled。
- **依赖错误/事件/产物**：等待原因 + 文案；阶段进度事件；`DocumentArtifact`（结构化文档 + 源哈希 + 转换器 + 版本）。
- **自检**：`--document-job-check` 创建任务并校验默认值、阶段文案、进度与终态
  （`qa\document-job-0580\job-check.json`）。
- 10 项测试：限制、阶段文案、终态集合、默认值/时间戳、进度文案、依赖错误、事件/产物。
- 未移植：Mac 的 SQLite 任务队列与工作区删除日志（Windows 导入是同步 + 原子目录）。

统计：已移植 55 / 部分 19 / 基线 2 / 未移植 5。

# P6 工作区删除事务 · 0.57.0

0.57.0 移植 `WorkspaceDeletionTransaction.swift`（151 行）的文件事务部分，测试 514 → 523。

- **清单**：源路径去重并保持顺序，暂存名 `<序号>-<文件名>`；工作区记录应放最后（其缺失即代表提交）。
- **暂存**：只移动实际存在的源；自动建暂存目录；目录与文件都支持。
- **回滚**：逆序还原已移动项；目标已存在时报错而不覆盖；完成后删除暂存目录。
- **清理**：提交后删除暂存目录，失败可下次重试（幂等）。
- **事务**：`prepare → 授权 → stage → 再授权 → commit`；任一步失败或未授权都调用 rollback，
  提交失败回滚后重新抛出；未授权返回 false。
- **自检**：`--workspace-deletion-check` 在真实目录完成暂存/回滚并跑通授权事务序列
  （`qa\workspace-deletion-0570\deletion-check.json`）。
- 9 项测试：清单去重、仅移动存在项、回滚还原/清理、回滚拒绝覆盖、清理幂等、授权序列、未授权、stage/commit 失败回滚。
- 未移植：`DocumentJob` 记账与 `excludingSharedOriginals`（共享原件按内容哈希判定）。

统计：已移植 55 / 部分 18 / 基线 2 / 未移植 6。

# P6 用户内容 · 0.56.0

0.56.0 移植 `SessionUserContent.swift`（143 行），测试 507 → 514。

- **个人内容**：个人笔记、重点标记（行 id + 偏移 + 原话）、编辑后的结构化纪要/摘要/Markdown、
  生成基准时间、校对决定；`HasContent` 覆盖所有类别。
- **校对决定**：与模型生成的校对合并 —— 同一条同一原话改正确/撤销；按行 id 定位，
  原话不在当前文本里或行已不存在则忽略；兼容旧版无 id 的记录（用 SHA-256 生成稳定 UUID）。
- **存储**：`user-content.json`；乐观修订号，旧草稿保存抛「这场记录已更新」并保留草稿；
  原子写（临时文件 + 覆盖移动）；缺失文件返回空内容，未来 schema 拒绝读取。
- **自检**：`--user-content-check` 写读个人笔记与重点、验证修订号 1 与旧草稿被拒
  （`qa\user-content-0560\user-content-check.json`）。
- 7 项测试：HasContent、校对合并、旧 id 稳定、存取往返、旧草稿拒绝、缺文件/新 schema、无 id JSON 读取。

统计：已移植 55 / 部分 17 / 基线 2 / 未移植 7。

# P6 证据模型 · 0.55.0

0.55.0 移植 `SessionEvidence.swift`（456 行）的证据模型部分，测试 502 → 507。

- **评审**：`SessionEvidenceReview` 记录「谁在哪一版结论/转写/行修订上做的 supported/unsupported 决定」。
- **依据等级**：`SessionEvidenceSupport` 四档（引文找到 / 仅时间 / 未找到原话 / 无定位）并带中文说明，
  明确「来源匹配」不等于「结论已确认」。
- **来源与定位**：原文/校对后版本、`SessionStructuredNotesTarget`（格式 + 路径 + 载荷修订，防止重新生成后改错节点）。
- **证据引用**：`SessionEvidenceReference` 携带标题、结论、引文、时间、行 id、来源修订、章节类型、
  负责人/截止/日期、转写修订、过期状态与评审结果。
- **自检**：`--evidence-check` 在真实进程构造带结构定位与评审的引用并校验文案
  （`qa\evidence-0550\evidence-check.json`）。
- 5 项测试：四档文案、默认值、完整溯源、值相等、枚举区分。
- 未移植：`SessionEvidenceBuilder`/`SessionEvidenceEditor`（依赖 SessionTextProjection 与 SessionSummary）。

统计：已移植 54 / 部分 17 / 基线 2 / 未移植 8。

# P6 会议携带与计划 · 0.54.0

0.54.0 移植 `MeetingCarry.swift`（38 行）与 `MeetingPreparation.swift`（43 行）的可用部分，测试 495 → 502。

- **MeetingCarry**：材料 / 复习 / 经验 / 转写四个开关；`WorkspaceSnapshot` 按开关过滤题库与经验，
  `useKnowledge` 关闭时两者都清空；`Standard` 默认 材料+复习+经验、不要转写。
- **MeetingPlan**：标题、模式、自定义类型与基础模式、携带选择、基线文档 id、上一场会议 id；
  `EffectiveMode` 在自定义时回落到基础模式；`PreparedWorkspace` 只做值拷贝过滤，不改原系列。
- **自检**：`--meeting-plan-check` 用自定义→学术模式、关闭题库复习生成计划，验证有效模式与过滤
  （`qa\meeting-plan-0540\meeting-plan-check.json`）。
- 7 项测试：默认开关、快照保留、独立关闭复习/经验、关闭知识、材料开关无效（无系列文档）、有效模式、计划不改原系列。
- 未移植：系列文档列表（材料开关因此暂不生效）、风格继承与上一场转写附件的实际接线。

统计：已移植 54 / 部分 16 / 基线 2 / 未移植 9。

# P6 备份规则 · 0.53.0

0.53.0 移植 `Backup.swift`（667 行）的备份规则部分，测试 486 → 495。

- **BackupManifest**：格式标识（含旧名 `interviewhacker-backup`）、版本 1、工作区/会议计数；
  非备份文件与「版本比应用新」分别给出可读错误。
- **模板合并**：按 id 匹配；内置模板从不来自备份；相同则跳过，不同则默认保留本地、显式选择才替换；
  返回新增/替换/保留计数。
- **待导入会议**：只导入本地没有的 id。
- **SettingsSnapshot**：导出键白名单（约 80 项，含场景、提示词、云转写、搜狗/检索、字幕条等）、
  密钥账户（`answer.api`/`github.token`/`websearch.tavily`/三家 STT）、预置 `answer.api.<id>` 识别、
  过滤与旧键 `autoApplyLessons` → `autoCollectKnowledgeCandidates` 的恢复映射。
- **自检**：`--backup-manifest-check` 校验清单、模板合并、旧键迁移与新版本拒绝
  （`qa\backup-manifest-0530\backup-manifest-check.json`）。
- 9 项测试：格式/版本/计数、模板新增/替换/保留/内置忽略、会议过滤、密钥账户、设置过滤、旧键迁移。
- 未移植：`workspaces` 合并（依赖完整 Workspace 模型与源删除墓碑）与归档写入。

统计：已移植 54 / 部分 14 / 基线 2 / 未移植 11。

# P1 历史检索 · 0.52.0

0.52.0 移植 `SessionSearch.swift`（173 行）的检索外壳与命中导航，测试 478 → 486。

- **打开目标**：`SessionContentTarget`（转写行/会中回答/纪要/个人笔记/重点标记）；按 id 前缀与行号推断，
  历史列表点开命中时知道该跳到哪。
- **展示**：`Label`（转写/校对转写/译文/问题/会中回答/纪要/个人笔记/重点标记）、`Snippet`、`MisheardForm`。
- **进度与快照**：`SessionSearchProgress`（已查/总数/不可读/每条命中与命中列表/问题）、
  `SessionSearchSnapshot.ByteCount`（正文 + 备用文本 + id + 160）。
- **检索**：按 id 顺序、每场取第一条、支持每场自定义 GlossarySearch、每 20 条回调进度、取消即停。
- **自检**：`--history-search-check` 搜一场存在 + 一场缺失，命中标签「转写」、打开目标 Transcript
  （`qa\history-search-0520\history-search-check.json`）。
- 8 项测试：标签、目标、片段/误听、字节数、命中/不可读/进度、每场词表、取消、空检索。
- 未移植：磁盘快照加载器（读 meta/transcript/answers 等）与按字节上限的 LRU 缓存。

统计：已移植 54 / 部分 13 / 基线 2 / 未移植 12。

# P6 系列简报 · 0.51.0

0.51.0 移植 `SeriesBriefing.swift`（327 行）的简报主体，测试 471 → 478。

- **近期会议**：只算系列成员，按开始时间倒序，最多 `meetingLimit` 场；`sessionCount` 是成员总数。
- **经验**：`useKnowledge` 打开时置顶经验优先，其余按创建时间倒序，最多 `knowledgeLimit` 条；
  关闭时经验、题库、待确认候选都为空。
- **题库**：按被问次数降序、同次数按更新时间降序。
- **待确认候选**：只留 pending 且来源会议仍在系列里的（无来源的保留），按创建时间倒序。
- **自检**：`--series-briefing-check` 用两场会议、两条经验与一条题库生成简报并校验顺序/限额
  （`qa\series-briefing-0510\series-briefing-check.json`）。
- 7 项测试：顺序与限额、只算成员、置顶优先、题库排序、关闭知识、候选来源过滤、零限额。
- 未移植：`seriesItems`（决定/待办/过期项）、证据引用合并与行动状态（依赖 SessionEvidence/Workspace 完整模型）。

统计：已移植 54 / 部分 12 / 基线 2 / 未移植 13。

# P1 会话内容检索 · 0.50.0

0.50.0 移植 `SessionContentSearch.swift`（293 行）的检索主体，测试 462 → 471。

- **字段/命中模型**：`SessionSearchField`（类型、正文、位置、备用文本、行 id、修订、过期状态等）、
  `SessionSearchHit`、`SessionSearchDocument`、`SessionSearchResult`，以及`字段类型/衍生内容状态`枚举。
- **检索**：直接文本优先、备用文本（如纠正前的原文）次之；命中类型与 id 随来源切换；
  按 id 去重（同一条转写只报一次），保持文档顺序；`Search` 每场会议取第一条。
- **摘录**：命中位置前后 32/64 字，边界加省略号；无位置时取前 116 字加省略号；大小写与全半角不敏感。
- **自检**：`--session-search-check` 在两场会议里检索 Kubernetes，只命中第一场转写并带位置
  （`qa\session-search-0500\session-search-check.json`）。
- 9 项测试：直接/备用命中、大小写与全角、去重、无 id、短/长摘录、顺序与空输入。
- 未移植：`fields` 构建器（依赖 SessionTextProjection 与 SessionSummary）与结构化纪要字段。

统计：已移植 54 / 部分 11 / 基线 2 / 未移植 14。

# P1 进程与取消 · 0.49.0

0.49.0 移植 `ProcessLineReader.swift`（67 行）与 `CancellableTaskValue.swift`（34 行），测试 457 → 462。

- **ProcessLineReader**：在独立阻塞读取线程上把流按 `\n` 切成行（去掉 `\r`），通过 Channel 流式返回；
  错误随流结束抛出，取消调用方不会取消底层读取。
- **ProcessExitWaiter**：启动前注册退出事件，同步/异步等待都能拿到真实退出码；超时返回 null。
- **CancellableTask**：共享任务 + 调用方取消 —— 先检查取消，再 `WaitAsync`；取消一个调用方不会取消共享任务。
- **自检**：`--process-check` 读内存流三行、等待 `cmd /c exit 5`、等待共享任务
  （`qa\process-0490\process-check.json`）。
- 5 项测试：换行/CRLF/空行/中文、退出码、取消不杀进程、共享任务正常与取消。

统计：已移植 54 / 部分 10 / 基线 2 / 未移植 15。

# P2 术语检索与合并 · 0.48.0

0.48.0 补齐 `Glossary.swift` 的检索/合并部分（由「部分移植」转为「已移植」），测试 448 → 457。

- **GlossarySearch / GlossarySearchHit**：查询先原样，再补上该词条的全部写法；查询里含某个写法时，
  按「把该写法换成词条其它写法」生成组合（如 `k8s 部署` → `Kubernetes 部署`），最多 32 个；
  记录哪些写法属于「听错的写法」，`FirstMatch` 优先返回用户输入的原样，避免把普通命中报成误听。
- **EntryMatching**：按宽松键（忽略大小写、全半角、空白与标点）找词条。
- **Merging / Layered**：全局表与系列表合并（同词条保留前者 id/分类/备注，别名合并），系列为空时直接用全局。
- **自检**：`--glossary-search-check` 在真实进程执行 `k8s 部署` 展开、误听标记与 `open ai` 匹配
  （`qa\glossary-search-0480\glossary-check.json`）。
- 9 项测试：展开顺序、宽松键、误听标记、包含式展开、32 上限、词条匹配、合并/分层、空查询、全角匹配。

统计：已移植 52 / 部分 10 / 基线 2 / 未移植 17。

# P5 字节级 BPE · 0.47.0

0.47.0 移植 `ByteLevelBPETokenizer.swift`（219 行）为 `Core/ByteLevelBPETokenizer.cs`，测试 440 → 448。

- **词表来源**：直接读模型的 `tokenizer.json`（`model.type` 必须为 BPE、`vocab`、`merges` 两种写法、`added_tokens`），
  自定义 split 正则与 NFC 归一化可配置，默认追加 `<|endoftext|>`（151643）。
- **字节级**：GPT-2 可逆字节表（33–126、161–172、174–255 原样，其余映射到 256+），
  文本 → UTF-8 字节 → 可见码点 → 按最低 rank 合并 → token id；`decode` 反向还原 UTF-8。
- **自检**：`--bpe-check` 现场生成一个小 tokenizer.json，编码 `ab`、`中` 并解码回原文
  （`qa\bpe-0470\bpe-check.json`）。
- 8 项测试覆盖合并、追加/不追加结束符、字符串形式 merges、中文三字节往返、NFC 合成、坏文件与默认配置。
- 说明：应用内嵌入仍用 `Platform/QwenTokenizer.cs`（同一算法、已对参考输出校验）；本文件是 Core 参考实现。

统计：已移植 51 / 部分 11 / 基线 2 / 未移植 17。

# P4 注视估计 · 0.46.0

0.46.0 移植 `GazeEstimator.swift`（188 行）为 `Core/GazeEstimator.cs`，测试 432 → 440。

- **眼部几何**：由眼角与瞳孔计算「瞳孔在眼裂中的位置」（以半眼宽为单位）与睁眼度；
  垂直于眼角轴测量，歪头不会被读成上下看；睁眼度 <0.14 视为眨眼，单眼可用时用另一只。
- **标定**：收集若干帧，取中位数作为「看镜头」基线（可抗眨眼/瞟一眼），少于 8 帧失败；
  `Restore` 可直接恢复已有基线。
- **读数**：平滑（0.35 系数）、眼睛偏移（纵向权重 0.8）与头部偏转取较大者；
  迟滞：偏离 >1.25 判「移开」，回到 <0.85 才判「看镜头」；统计跟踪时长与注视比例（≥10 秒才给比例）。
- **自检**：`--gaze-check` 在真实进程完成 9 帧标定、基线看向镜头、瞳孔移开判 Away
  （`qa\gaze-0460\gaze-check.json`）。
- 未接线：Windows 侧没有摄像头注视功能，本文件为纯逻辑移植。

统计：已移植 50 / 部分 11 / 基线 2 / 未移植 18。

# P5 PDF 版式 · 0.45.0

0.45.0 把 `PDFTextLayout.swift`（171 行，原「基线简化版」）完整移植为 `Core/PDFTextLayout.cs` 并接入 PDF 读取，测试 423 → 432。

- **页眉页脚**：跨页重复（数字忽略）且出现在页首/页尾 30% 的行判为版式噪声；单页出现 >2 次的行不算；少于 4 页不判定。
- **单页 Markdown**：先去页码（首/尾）、去页眉；把被换行拆开的句子接回去（中文不空格、英文补空格、断词连字符去掉）；
  点线引导替换为省略号；编号标题（`3.2`、`II. SYSTEM MODEL`、`A. Array…`、`Abstract`、`APPENDIX B`）标记为 Markdown 标题。
- **接入**：`DocumentReader` 的 PDF 分支先一次取出各页文本，算版式噪声与「是否论文正文」，再逐页转 Markdown 后归档；短页仍走 OCR 兜底。
- **自检**：`--pdf-check` 用 5 页合成页验证页眉被识别、正文不被误判、`摘要` 成为标题、页眉与页码消失
  （`qa\pdf-0450\pdf-check.json`）。
- 9 项测试覆盖页眉/页码、换行合并、断词、中文不空格、编号与期刊标题、点线、正文判定。

统计：已移植 49 / 部分 11 / 基线 2 / 未移植 19。

# P4 问句语料与离线评估 · 0.44.0

0.44.0 移植三份 QuestionEvalCorpus 与 QuestionDetectionEvaluator（在 QuestionIntent.swift 末尾），测试 420 → 423。

- **139 条标注语料**：Extended（面试/会议风格、无标点、口头语、中英夹杂）+ HeldOut，标签为「对方是否在等我回答」。
  语料由 Swift 源自动提取，避免手抄偏差。
- **离线评估**：按 6 秒间隔逐条喂给 QuestionCandidateTracker（HasPriorAnswer = true），
  统计问题数/命中数/陈述数/误触发数、召回率与每小时误触发；未命中与误触发各保留前 5 条。
- **实测**：--question-check 在真实进程跑完整语料：139 条、82 个问题、82 命中、0 误触发（召回 1.0，
  qa\question-0440\question-check.json）。注意这是「语料 + 当前规则」的结果，不是现场录音的保证。
- 3 项 Core 测试：语料完整性、计数守恒、空集/漏检边界。

统计：已移植 48 / 部分 11 / 基线 3 / 未移植 19。

# P2 流式协议 · 0.43.0

0.43.0 移植 `AnswerStreaming.swift`（267 行）为 `Core/AnswerStreaming.cs`，测试 409 → 420。

- **Codex app-server 协议**：请求/通知行、审批拒绝（decline/denied）、thread/turn 参数、登录与 id 提取、
  行解析（响应/通知/服务端请求）、turn 事件映射（delta、agentMessage、webSearch、retrying、completed）。
- **AgentMessageAccumulator**：按 item 累积 delta，`final_answer` 优先，否则最后一条。
- **ChatCompletionStreamParser**：`data:` 行、`[DONE]`、纯 JSON 错误体、delta/message 内容；
  请求体（图片 data URL、reasoning_effort 与 temperature 互斥）；端点补全与「远程明文 HTTP 拒绝」。
- **ModelListParser**：`data`/`models` 两种形状、去重排序。
- **自检**：`--stream-check` 在真实进程里解析响应、SSE delta、模型列表与端点
  （`qa\stream-0430\stream-check.json`）。
- 未移植：实际 WebSocket/HTTP 传输仍在应用层客户端。

统计：已移植 45 / 部分 11 / 基线 3 / 未移植 22。

# P3 云端转写 · 0.42.0

0.42.0 移植 `CloudTranscription.swift`（293 行）为 `Core/CloudTranscription.cs`，测试 400 → 409。

- **三家服务商**：Soniox（16 kHz、websocket 配置消息、keepalive）、OpenAI Realtime（24 kHz、base64 追加消息、
  session.update）、Deepgram（URL 参数、50 个 keyterm、Token 认证）；含默认模型、采样率、废弃 Whisper 识别。
- **转写装配**：Soniox 的 final/tail 与 `<end>`/`finished`；Deepgram 的 `Results`/`UtteranceEnd`/
  `speech_final`；OpenAI 的 per-item delta/completed/failed；错误统一抛 `CloudTranscriptException`。
- **重采样**：16 kHz → 24 kHz 线性插值（2:3）与小端 PCM 打包。
- **自检**：`--cloud-check` 在真实进程里生成 Soniox 配置、装配「你好」、重采样 4 个样本
  （`qa\cloud-0420\cloud-check.json`）。
- 未移植：WebSocket 传输与设置界面仍属应用层（本项目当前没有开启云转写的 UI）。

统计：已移植 44 / 部分 11 / 基线 3 / 未移植 23。

# P5 词法检索与会话上下文 · 0.41.0

0.41.0 补上 `SessionContext.swift`（265 行）中的可用部分，并完成 `HybridContextIndex.retrieve`，测试 392 → 400。

- **SessionContextIndex**：离线词法索引 —— 拉丁词（>1 字母、去停用词）与中文单字/二元组；
  按「重叠词 IDF 之和 / √块词数」打分，按分数降序、序号升序；`Retrieve` 最多 8 段、字符预算封顶。
- **HybridContextIndex.retrieve**：显式「第 N 页 / p.N / 幻灯片 N」与文件名优先，其余用 RRF 融合；
  语义分数低于 0.43 的块不进入语义排序，词法块若被语义标记低分也会被剔除。
- **ManualQuestionBuilder**：有手动输入用输入（≤8000 字），否则取最近 3 句 + 半句（≤4000 字）并加固定引导语。
- **OfficeXMLText**：从 pptx/docx/xlsx 的 XML 中按段落抽文本、实体解码、文件名自然排序。
- **AnswerStyle / ContextExcerpt** 一并移植。
- **自检**：`--retrieval-check` 增加「第 1 页」显式请求断言
  （`qa\retrieval-0410\retrieval-check.json`，explicitPage=1）。
- 未移植：`ContextDocument`/`SessionMode`/`SessionPreset`（应用层会话/预设模型）。

统计：已移植 43 / 部分 11 / 基线 3 / 未移植 24。

# P5 语义检索组件 · 0.40.0

0.40.0 移植 `SemanticRetrieval.swift`（277 行）的核心组件，测试 385 → 392。

- **ContextFingerprint**：FNV-1a，只用于发现文本变化（非加密）。
- **ContextChunker**：900 字/块、120 字重叠；同页短段落合并，标题不进块但进入路径；
  表格/代码/公式保持完整；指纹 = 版本 + 文档 id + 块 id + 分片序号 + 正文。
- **SemanticIndex**：按块顺序存 L2 归一化向量，向量记住对应文本指纹；文本变了就视为缺失；
  `RankedOrders` 只对当前指纹匹配且维度一致的向量算点积，按分数降序、序号升序。
- **HybridRanker**：RRF（k=60）融合词法与语义排序；**RetrievalPolicy** 语义最低分 0.43。
- **自检**：`--retrieval-check` 在真实进程里分块、建索引、查排名
  （`qa\retrieval-0400\retrieval-check.json`，1 块、score=1）。
- 未移植：`HybridContextIndex.retrieve` 与词法 `SessionContextIndex`（依赖未移植的 ContextDocument/会话上下文）。

统计：已移植 42 / 部分 11 / 基线 3 / 未移植 25。

# P2 答案服务支持 · 0.39.0

0.39.0 移植 `AnswerServiceSupport.swift`（242 行），测试 361 → 385。

- **默认后端**：已保存的有效选择优先；否则装了 Codex 用 app-server，不然用 OpenAI 兼容接口。
- **服务商表**：DeepSeek/DashScope/Moonshot/智谱/火山/OpenAI/OpenRouter/Groq/Ollama/LM Studio 的排序、
  Key 页面、建议模型与本地标记；地址归一化（去空格、小写、去尾斜杠）；未知地址排最后。
- **切换计划**：活动预设地址相同则选中；已有同地址预设则选中；活动预设还是空的就复用，否则新建。
- **Key 输入**：去空白、8–400 个 ASCII 字符、不含空白；掩码显示 `sk-••••••••3456`。
- **失败解释**：HTTP 401/403/402/404/400/422/429/5xx 与 URL 错误码映射到原因，并给出可操作中文句子；
  模型列表失败单独说明（404/405 表示该服务没有列表）。
- **脚本时间线**：`VisibleCount` 与 `Progress`。
- **自检**：`--api-check` 在真实进程里验证分类、归一化、建议模型、掩码与默认后端
  （`qa\api-0390\api-check.json`）。
- 已知简化：`rejected(Int)` 的具体状态码未随枚举携带，消息仍为通用句（应用可补状态码）。

统计：已移植 42 / 部分 10 / 基线 4 / 未移植 25。

# P2 答案提示词 · 0.38.0

0.38.0 移植 `AnswerPromptBuilder.swift`（328 行）的主体，测试 352 → 361。

- **指令原文**：从 Swift 源自动提取 4,235 字的系统指令（含联网/不联网两种工具策略），没有手抄偏差。
- **预算化 JSON**：question 8000、transcript 12000（保留尾部）、profile 8000、偏好 4000、经验 2500、
  screen 2500、术语 2500、最近 6 轮问答（1000/2000）、检索片段 8000/最多 8 条、技能最多 8 条共 12000。
- **整份资料**：按「文档 · p.N」分组，超过 160,000 字返回 null；**精炼**：更简短/展开/举例/STAR/英文/中文/澄清/反问，
  `原问题：` 标记支持链式精炼并取回原问题。
- **自检**：`--prompt-check` 用真实构建器生成 prompt，校验系统指令、`labMeeting` 模式、检索片段与长度
  （`qa\prompt-0380\prompt-check.json`，system 4250 字 / user 1447 字）。
- 未移植：`PreparationPrompt`（依赖未移植的 `ModeProfile` 角色与 reviewFocus）。

统计：已移植 41 / 部分 10 / 基线 4 / 未移植 26。

# P3 屏幕上下文 · 0.37.0

0.37.0 移植 `ScreenContext.swift`（44 行），并补上它依赖的 `TextSimilarity` 二元组工具，测试 342 → 352。

- **ScreenContext / ScreenTextRegion**：OCR 区域文本与归一化边界、增强文本优先、低置信度判断、2 秒新鲜度。
- **ScreenReference**：中英文「这一页 / this slide」引用检测与是否需要图像（图/表/公式/位置/颜色）。
- **TextSimilarity**：小写字母数字归一化、字符二元组、Jaccard、containment，
  `Relevant` 用「最近发言的二元组至少 8 个、且 25% 出现在屏幕文本中」判断屏幕是否相关。
- 10 项测试覆盖引用、上下文文本/置信度/新鲜度、二元组边界、Jaccard/containment、相关度阈值。

统计：已移植 41 / 部分 9 / 基线 4 / 未移植 27。

# P4 提问轮次 · 0.36.0

0.36.0 把 `QuestionTurn.swift`（96 行，原基线简化）补齐为 `Core/QuestionTurn.cs`，测试 328 → 342。

- 整轮提问：把「对方自上次我发言以来的所有句子」拼成一次提问（可带正在说的半句），上限 1600 字；
- 词数：拉丁词按词、中文按字（`hello你好` = 3，`state-of-the-art` = 1）；
- 时机：间隔 >25 秒开新一轮、我回应 ≥3 词结束本轮、静音 2 秒且 ≥8 词才该答；
  已答过则要再新增 8 词才再问；30 秒未答丢弃；
- 14 项测试覆盖词数、拼接、间隔重置、回应结束、增长阈值、静默/耐心/半句、已回答覆盖与重置。

统计：已移植 40 / 部分 9 / 基线 4 / 未移植 28。

# P5 文档结构 · 0.35.0

0.35.0 移植 `DocumentStructure.swift`（210 行）为 `Core/DocumentStructure.cs`，测试 317 → 328。

- **块解析**：标题（1–6 级）维护标题路径；代码围栏、`\$\$`/`\\[` 公式、`<table>` 与管道表格各自成原子块；
  段落按空行/标题/围栏/表格边界合并。每个块带稳定 id（位置 + 内容指纹）与 locator（页码/幻灯片/工作表/标题路径/段号）。
- **表格分片**：HTML 表格按 `rowspan` 连通行分组，每组重复表头；管道表格按行分组并重复两行表头；
  长段落按 target 切分并保留 120 字重叠。
- **结构化文档**：`StructuredDocument` 带 schema、pipeline、warnings 状态与 Markdown 拼接。
- **测试**：11 项 —— 标题路径、代码/公式原子性、管道表、表格分片重复表头、HTML 表头分组、长段落重叠、
  locator 文案、稳定 id、状态与 Markdown。

统计：已移植 39 / 部分 9 / 基线 5 / 未移植 28。

# P6 增量备份恢复点 · 0.34.0

0.34.0 移植 `IncrementalBackup.swift`（123 行）为 `Core/IncrementalBackup.cs`，测试 301 → 317。

- **内容寻址**：每个文件按 SHA-256 存进 `Objects`，同一内容只存一次；清单（`RestorePoint`）记录
  `name → hash` 与整份指纹，写入前先校验全部对象。
- **目录快照**：`prefix → 目录`，递归读取并保留相对路径；不存在的可选目录跳过，符号链接拒绝。
- **恢复**：只接受本备份库 `RestorePoints` 下的清单；恢复前再次校验对象与指纹。
- **裁剪**：保留最近 `keep` 个恢复点或预算内，删除无人引用的对象；清单发布时间不影响内容判断。
- **测试**：16 项 —— 存取往返、内容去重、快照共享对象、目录前缀、对象篡改拒绝、外部清单拒绝、
  路径安全、按数量裁剪、无引用对象清理。

统计：已移植 38 / 部分 9 / 基线 5 / 未移植 29。

# P1 小文件批次四 · 0.33.0

0.33.0 再补 5 个自包含 Core 文件（测试 287 → 301）。

| Swift | C# | 测试 | 说明 |
| --- | --- | --- | --- |
| `ScreenDelta.swift` | `Core/ScreenDelta.cs` | 2 | 变化像素区域、边距、>75% 视为整屏、无上一帧返回全屏 |
| `DocumentClassifier.swift` | `Core/DocumentClassifier.cs` | 2 | 文件名 ×6 + 正文前 2000 字 ×1，映射到当前场景的资料槽 |
| `Skill.swift` | `Core/Skill.cs` | 2 | front matter 名称/描述、`# 标题` 回退、SKILL 文件名回退、启用/全局筛选 |
| `VoiceProfile.swift` | `Core/VoiceProfile.cs` | 3 | 0.55 阈值识别「我」、3 段起建、离群片段丢弃、同模型校验 |
| `CompanionBLEProtocol.swift` | `Core/CompanionBLEProtocol.cs` | 5 | 8 字节大端头分片/乱序重组、15 秒重置、384 KB 上限、重发 outbox |

统计：已移植 37 / 部分 9 / 基线 5 / 未移植 30。

# P2 公式渲染 · 0.32.0

0.32.0 移植 `LaTeXMath.swift`（545 行，未移植），答案和纪要里的公式不再是原始 LaTeX，测试 270 → 287。

- **`LaTeXMath`**：完整解析器（可处理流式输出的半截公式）——希腊字母/运算符/箭头、上下标
  （含 Unicode 上下标表与回退写法）、`\frac`（含 ½ 等常用分数）、`\sqrt[n]`、`\mathbb`、
  `\text` 等透传命令、`\left/\right` 丢弃、重音组合符、矩阵与 `cases`/`aligned` 环境、
  `\\` 与 `&` 的行列切分。
- **Markdown 预处理**：`\$\$…\$\$` / `\\[…\\]` 显示公式与 `\$…\$`、`\\(…\\)` 行内公式转 Unicode，
  代码段和 `\\\$` 不动；`MarkdownText` 渲染前统一走 `LaTeXMath.Markdown`。
- **自检**：`--formula-check` 用真实 `MarkdownText` 控件渲染
  「答案：面积为 ½，函数为 x²。 / ∑ᵢ₌₁ⁿ i / 代码 `\\$` 保持原样」，检查转换结果与控件文本
  （`qa\formula-0320\formula-check.json`）。
- 17 项测试覆盖符号、上下标回退、分数、根号、矩阵/cases、行内替换、代码段与金额、Markdown 预处理、
  表长校验与半截公式。

统计：已移植 32 / 部分 9 / 基线 5 / 未移植 35。

# P6 说话人聚类 · 0.31.0

0.31.0 补齐 `SpeakerClustering.swift`（272 行，原为「基线简化版，仅特征」），测试 258 → 270。

- **`VoiceClusterer`**：按到达顺序聚声音（join / startNew 均 0.5，短于 2 秒不新建），
  合并两个声音时把和向量相加、旧声音清零（不会再被选中）；`VoiceGroups` 保留为委托包装，
  原有 256 维/单位范数校验不变。
- **`SpeakerLabels`**：5 条实质发言才编号、至少两个声音才显示；`substantive`（3 词或 4 个宽字符）、
  声纹→编号、行→编号、命名、`MeLines`/`MergedMe`、换人位置、`label/shortName/cleanName`、
  旧纪要里的「说话人 N」按名字重写（长编号优先，避免 12 被当成 1）、命名合并、
  提示词标签与说话人说明段落（中英）。
- 12 项测试覆盖聚类、合并、编号门槛、我/对方、命名重写、提示词段落。
- 行为变化：旧实现要求「第一名比第二名高 0.05」才算确定，上游是直接并入最接近的声音；
  自测 `SpeakerTests` 已按上游语义更新（歧义向量并入最近声音，短向量仍不新建）。

统计：已移植 31 / 部分 9 / 基线 5 / 未移植 36。

# P1/P3 完整 VAD 与模型复用 · 0.30.0

0.30.0 把两个仍有简化实现/缺失的 Core 文件补齐（测试 247 → 258）。

- **VoiceActivitySegmenter.swift（153 行，原为基线简化版）**：完整移植 —— 自适应噪声底、
  4,000 采样预卷、说话中每 1.5 秒发 partial、0.7 秒静音或 12 秒上限发 final（上限切分保留 0.5 秒重叠，
  且重叠段不重复触发 final）、Flush 收尾。识别器改为消费事件列表（只用 final，partial 留待后续接实时预览）。
  - 7 项测试：静音无事件、partial→final、预卷起点、12 秒切分与重叠起点、稳态噪声忽略、Flush、完整音频。
- **LocalModelReuse.swift（112 行，未移植）**：按文件名搜索模型目录（跳过 .git/node_modules 等、不跟随目录符号链接、
  条目/时间上限）、按固定大小 + SHA-256 校验候选、逐个文件原子复用（先复制到 .reuse- 临时名再校验再移动）。
  - 4 项测试：搜索命中/跳过缓存、条目上限、大小与哈希校验、复用一次与坏候选拒绝、路径穿越拒绝。
- **真实链路**：--import-wave 用已有 X-ASR 模型重新转写 xasr480-0.wav，归档 2 条转写
  （qa\models-data\Sessions\B8DD1429...\transcript.jsonl），说明新 VAD 接进识别器后链路正常。

统计：已移植 30 / 部分 9 / 基线 6 / 未移植 36。

# P1 Core 小文件批次三 · 0.29.0

0.29.0 继续补自包含 Core 文件（+7 已移植，测试 222 → 247）。

| Swift | C# | 测试 | 说明 |
| --- | --- | --- | --- |
| `QuestionDetector.swift` | `Core/QuestionDetector.cs` | 8 | 片段合并、8 秒过期、15 秒同文去重、问句/追问判定、中文二元组归一化 |
| `DocumentNotice.swift` | `Core/DocumentNotice.cs` | 2 | 提示 vs 警告、页码/幻灯片汇总 |
| `BackupContent.swift` | `Core/BackupContent.cs` | 2 | 相对路径 + 内容 SHA-256 指纹、新/相同/冲突 |
| `BackupArchivePolicy.swift` | `Core/BackupContent.cs` | 4 | 不安全路径、符号链接、重复项、文件数与解压上限 |
| `OnboardingFlow.swift` | `Core/OnboardingFlow.cs` | 2 | 步骤钳制/前后跳转/可跳过/待办列表 |
| `WebLookupPromptBuilder.swift` | `Core/WebLookupPromptBuilder.cs` | 3 | 字段预算、最近两轮回答、资料总预算、坏 JSON 兜底 |
| `SessionTranscriptLocator.swift` | `Core/SessionTranscriptLocator.cs` | 4 | 按 id 或 3 秒容差定位，旧归档用打印时间 |

统计：已移植 28 / 部分 9 / 基线 7 / 未移植 37。

# P7 资料术语进热词 · 0.28.0

0.28.0 把已移植的 `MaterialTerms` 接进识别链路：资料里的技术术语和术语表一起成为热词。

- **`AppState.Hotwords`**：`MaterialHotwords()` 从资料库的文本块里抽术语（上限 24，按文档 sha + 块数缓存，
  导入后失效）；`RecognitionHotwords()` 先放术语表、再放资料术语，大小写不敏感去重。
- **顺带修掉一个真 bug**：实时识别的 `LiveSpeechEngine` 之前只吃 `settings.Vocabulary`，
  `GlossaryHotwords()` 只进了导入与校对——也就是说**术语表对实时会议一直没生效**。
  `LiveSpeechEngine` 现在接受 `extraHotwords`，开始记录时传 `RecognitionHotwords()`。
- **自检**：`--hotwords-check=<dir>` 导入一份含 XL-MIMO/TSIR/DeepMIMO/RQ2 的文档，再写一条术语表
  Kubernetes；识别热词为 `[Kubernetes, XL-MIMO, TSIR, DeepMIMO, RQ2]`（`qa\hotwords-0280`）。
- 测试：沿用 Core 222（`MaterialTerms` 的 12 项已覆盖抽取规则；本轮是接线与缓存）。

# P6 字幕导出 · 0.27.0

0.27.0 把 SessionExport.swift 的字幕半边接上：纪要页可以直接导出 SRT / VTT。

- **Core/SessionExport.cs**：SessionSubtitleExporter —— 起始时间取 startedAt ?? date 相对会议开始；
  结束时间有效就用，否则按去空白字数 ÷5 估算（1.2–12 秒），再用下一句起点封顶、至少 0.25 秒；
  SRT 用逗号毫秒、VTT 带 `WEBVTT` 头；有译文时追加「译文：」行；返回值带估算条数。
- **界面**：纪要页「导出 SRT / 导出 VTT」，保存对话框、UTF-8 无 BOM；状态栏说明有多少条是估算时间。
- **自检**：`--demo --subtitle-export-check=<dir>` 导出 `demo.srt`/`demo.vtt` 与 `export-check.json`；
  实测 10 条转写、0 条估算、VTT 头正确、含译文（`qa\subtitle-export-0270`）。
- 测试：Core 211 → 222（11 项：SRT/VTT 结构、估算与上下限、下一句封顶、译文、空会议、时间戳补零）。
- 未移植：Markdown/Word/PDF 导出仍走既有实现；`SessionTextProjection` 的语言视图与过期译文字段没有对应物，
  译文一律按「译文：」追加。

# P1 小文件批次二 · 0.26.0

0.26.0 继续补自包含的 Core 小文件（+5 已移植，测试 193 → 211）。

| Swift | C# | 测试 | 说明 |
| --- | --- | --- | --- |
| `WebSearchNeed.swift` | `Core/WebSearchNeed.cs` | 5 | 时效词/话题/年份打分、`mentions` 英文词边界、问“你本人”与概念问题降权；17 条标注语料逐条断言。联网搜索提供方仍未接，只移植判断规则 |
| `ScrollFollowState.swift` | `Core/ScrollFollowState.cs` | 2 | 跟随 / 读历史 / 选中文字三态与未读计数 |
| `ScrollFollowInputClassifier.swift` | `Core/ScrollFollowState.cs` | 2 | macOS 滚动导航键码与命中测试（纯函数，Windows 侧未接 UI） |
| `RealtimeSetupReadiness.swift` | `Core/RealtimeSetupReadiness.cs` | 3 | 录音就绪清单、能否请求启动、离线会议跳过系统声检查 |
| `DocumentFeatureAvailability.swift` | `Core/DocumentFeatureAvailability.cs` | 1（多断言） | 依赖齐备才算可用 |

统计：已移植 21 / 部分 8 / 基线简化 7 / 未移植 45。

# P1/内存 小文件批次与内存测量 · 0.25.0

0.25.0 做两件事：补一批自包含的 Core 小文件，把「内存目标」用可复现的测量说清楚。

## Core 小文件批次（+6 已移植、+1 部分移植）

| Swift | C# | 测试 | 说明 |
| --- | --- | --- | --- |
| `AnswerLanguage.swift` | `Core/AnswerLanguage.cs` | 4 | 中英判断（英文里带中文术语仍是英文）、gloss 语言 |
| `AnswerEvidence.swift` | `Core/AnswerEvidence.cs` | 2 | `SearchDecision.parse` 宽容 JSON、`AnswerGrounding` 的改写/核实/搜索短语；`EvidenceBundle`、`AnswerState` 依赖未移植的 `ContextExcerpt`/`ScreenContext`/`WebResult` |
| `MaterialTerms.swift` | `Core/MaterialTerms.cs` | 12 | XL-MIMO/TSIR/DeepMIMO/RQ2 抽取、普通词与姓氏排除、稳定排序与上限 |
| `ShortcutBinding.swift` | `Core/ShortcutBinding.cs` | 2 | ⌃⌥⇧⌘ 标签、安全性与重复校验 |
| `CodexEventParser.swift` | `Core/CodexEventParser.cs` | 2 | `item.completed/agent_message` 与三种错误形态 |
| `TranscriptContextBuilder.swift` | `Core/OnboardingVersion.cs` | 1 | 最近五分钟上下文 |
| `OnboardingVersion.swift` | `Core/OnboardingVersion.cs` | 1（含多断言） | 功能版本才重新引导 |

合计新增 193 - 168 = 25 项测试；`CORE_PARITY.md` 统计变为 已移植 16 / 部分 8 / 基线 7 / 未移植 50。

## 内存：测量与结论

- 现状（0.25.0 构建，3 次 `--startup-check`）：工作集 **168–173 MiB**、私有提交 **186–191 MiB**、
  托管堆 **15 MiB**、线程 **118**。目标 150 MiB 未达到。
- 自检报告现在带 `workingSetMiB`、`privateMiB`、`gcHeapMiB`、`gcCommittedMiB`、`threads`，
  以后每次发布都能直接看到趋势。
- 试过并回退的优化：
  - GC `ConserveMemory` / `RetainVM` / `ServerGarbageCollection=false`：工作集无变化（只是把默认值写死）。
  - 去掉内嵌 Inter 字体：工作集约 −4 MiB，但拉丁字形改走系统字体，视觉有变化，回退。
  - 延迟音频设备/进程枚举到设置页：启动快 0.2 秒，但私有提交反而 +30 MiB 且保存的设备选择会失效，
    回退。
  - `PublishTrimmed`（partial）：启动即崩溃（0xC0000409/启动报告未生成），回退。
  - 空闲时 `EmptyWorkingSet`：工作集报成 **0.8 MiB**，但私有提交不变、下次交互全部缺页；
    这是指标游戏，删除，不做。
- 结论：剩下的占用在 .NET/Avalonia/Skia 与音频/OCR 原生库，应用层能省的不多；
  要真正下探需要换 UI 框架或 Native AOT（本机缺 MSVC 工具链），已记为后续专项。

## P8 应用内更新 · 0.24.0

0.24.0 把「检查更新」从只开下载页补成应用内替换：下载 Setup → 校验 SHA-256 → 静默安装 → 退出重启。

- **Core/UpdateChecksums.cs**：解析发布里的校验文件（`<setup>.sha256` 或 `SHA256SUMS.txt`，
  支持 `hash  name` / `hash *name` / 单行裸 hash），并对下载文件做 SHA-256 校验（定长比较）。
- **UpdateFeed / UpdateCheck**：发布里新增 `SetupAsset` 与 `ChecksumsAsset`；`UpdateReport` 带回
  `setupUrl` / `setupName` / `checksumsUrl` 与 `CanInstall`。
- **AppState.Update**：新增「下载并安装」命令 —— 流式下载带进度、校验失败删除文件、校验通过后以
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS` 启动安装包并触发退出；
  发布没有校验文件时按钮不出现，只保留下载页。
- **自检**：`--update-install-check=<json>` 对本地发布源跑完整链路；`NEXTTHOUGHT_UPDATE_LAUNCH`
  只记录启动命令而不真的替换当前构建，因此可以反复验证下载与校验。
- 测试：Core 163 → 168（5 项校验和解析/校验测试）；本地发布源实测 下载 → SHA-256 → 启动命令 记录，
  篡改校验值时 `passed=false` 且下载文件被删除（`qa\update-install-0230`）。
- 未做：Authenticode 签名（当前信任根是发布源与校验文件）、增量更新、回滚。

## P5 伴侣中转 · 0.23.0

0.23.0 把手机伴侣的第三条通道补上：中转。中继只转发密文，桌面端负责建房、发状态、收命令、回回执。

- **Core/CompanionRelayCrypto.cs**：逐文件移植 `CompanionCrypto.swift` —— `CompanionPairing`（含二维码
  fragment）、HKDF-SHA256（salt = 房间号，info = `ih-companion-v1/<kind>`）、AES-256-GCM
  （AAD = `房间号/kind`）、`CompanionCommand` 30 秒新鲜度、`CompanionCommandCache` 5 分钟去重。
- **Platform/CompanionRelayClient.cs**：`POST /rooms` 建房 → WSS `/socket` 以 publisher 加入 →
  状态帧加密上行（只发最新一帧）→ 命令下行解密后交给与局域网相同的动作处理 → 成功回 ack；
  断线指数退避重连，退出时 `POST /rooms/<id>/close` 关房。
- **设置**：新增「中转地址（可选）」；填了就用 HTTPS 配对链接与中转，留空仍只用局域网；
  两者都没有时明确提示。
- **蓝牙**：按方案在设置页与文档里标记为不可用（手机端仍按 `IH-` 广播名过滤，Windows 不能自定义广播名）。
- **验证**：9 项 Core 加密测试（HKDF 用 RFC 5869 测试向量）+ `--relay-check` 端到端：
  C# 加密状态 → Node 中继 → 模拟手机（Node `crypto`）解密并发命令 → C# 解密执行 → ack 回传。
  本机实测 `commandHandled=true`、`stateReceived/ackReceived=true`（`qa\relay-check-0230`）。
- 未做：真实手机联调、公网 TLS 部署、蓝牙（等手机端改动）、中继容量与限流压测。

## P5 双屏演讲 · 0.22.0

0.22.0 把 `PresenterMode.swift` 的窗口规则搬到 Windows：幻灯片屏不出现 NextThought 的任何窗口，
摄像头这一屏由提词岛承载会议。Mac 版的「读幻灯片屏、按页匹配讲稿」还没有对应实现，见下。

- **Core/PresenterMode.cs**：`DisplayInfo` + `PresenterLayout` 纯函数 ——
  摄像头屏 = 内置屏（Windows 用主屏代表）→ 主屏 → 第一块；幻灯片屏 = 指定 id 的非摄像头屏 → 第一块非摄像头屏；
  只有一块屏时 `CanActivate=false`，绝不把唯一窗口藏起来。
- **设置**：新增「双屏演讲」开关与「幻灯片所在屏」选择，键名沿用 Mac 的 `presenterMode` /
  `presenterSlidesScreen`；摘要行直接说明「幻灯片在 X，提示在 Y」或「接上外接屏后可用」。
- **会中行为**：开始记录时若可启用，提词岛显示、主窗口 `Hide()`；Ctrl+Alt+N 唤回；停止记录或外接屏断开时恢复。
  显示器拓扑变化通过 `Screens.Changed` 重新计算。
- **自检**：`--presenter-check=<json>` 用合成第二块屏走同一套布局与窗口副作用
  （主窗口收起 → 提词岛留下 → 主窗口恢复 → 真实单屏拒绝启用），4 项全过。
- **顺手修的坑**：`Hide()`/`Show()` 会再次触发 `Opened`，原来会重复 `InitializeAsync` 与 `SetupTray`；
  已加 `openedOnce` 守卫。关闭主窗口时若还有浮窗，进程原本会留在后台；现在先关浮窗再 `Shutdown`。
- 测试：Core 144 → 154（10 项布局测试）。
- 未做：幻灯片屏截图取字与 `LiveInsights` 页码匹配、`showsSlideNotes`、真实双屏验收（本机只有一块屏）。

## P5/P7 逐字同传 · 0.21.0

0.21.0 把「同传」从整句翻译补到逐字：识别还在变化时，字幕条和提词岛上的译文也会跟着更新，
但旧快照的译文不会盖住新句子。

- **Core/LiveTranslation.cs**：逐文件移植 `LiveTranslationRevision`、`LiveTranslationRevisionGate`
  （generation/value、`revise`/`invalidate`/`isLatest`/`accept`）与 `LiveTranslationPartialThrottle`
  （固定间隔而不是 debounce：长句子也会持续出结果）；新增 `LiveTranslationCoordinator`
  把「限流 → 发请求 → 只接受最新一版」串起来，网络与 UI 都不进 Core。
- **接入**：`ReceiveTranscript` 的 partial 分支把快照交给协调器，每 0.7 秒最多一次翻译请求；
  整句落地时 `Invalidate`，在途的旧译文直接丢弃。字幕条的两个通道带上 `PartialTranslation`
  （Core 的 `SubtitleBar` 早已支持「译文为主、原文为辅」的显示）。
- **同传上岛**：提词岛新增一行「译」，显示对方那句话的最新译文；`--demo --island` 用固定译文验收界面。
- 测试：Core 132 → 144（12 项门控/限流/乱序完成/换通道/新句子作废）。
- 已知边界：逐字译文要真实回答服务；没有密钥时界面与门控已由测试覆盖，真实译文未端到端跑。
  双屏演讲仍未移植。

## P7 系列知识库与合并 · 0.20.0

0.20.0 补上「越用越懂你」这条链路：模板纪要 → 待确认候选 → 用户确认 → 经验/题库 → 回答提示词。

- **Core/WorkspaceKnowledge.cs**：逐文件移植 `Workspace.swift` 的知识半边 ——
  `WorkspaceLessonRevision`、`WorkspaceLessonHistory`（改文本 / 加来源 / 恢复旧版本，版本号与历史）、
  `WorkspaceAnswerRevision`、`QuestionBankItem`（答案历史、`answerRevision`、旧记录的单来源回退）、
  `WorkspaceKnowledgeCandidate(+Source)`、`WorkspaceKnowledgeReceipt`、`KnowledgeMerger`
  （候选去重、`similarQuestion` 的问句意图区分、0.55 / 0.6 阈值、来源计数、过期基版本拒绝、回执幂等、
  字符二元组 Jaccard、FNV-1a 稳定 id）。
- **NotesCarry 接通**：`NotesKnowledge.Extract` 把模板里 `NotesCarry.Lesson/Bank` 的章节转成经验与题库条目
  （带章节前缀）；此前模板只声明、没有消费者。
- **系列页**：新增「待确认 / 题库」两个页签；候选逐条 采纳 / 替换回答 / 忽略，结果写回 `workspace.json`
  并留回执；已确认的经验与题库按「钉住优先、最新在前」进入回答提示词（`WorkspaceKnowledgePrompt`）。
- **回答**：`AnswerAsync` 的系统提示词追加本系列经验，用户提示词追加题库前三条；`useKnowledge=false` 时都不加。
- **离线自检**：`--knowledge-check=<json>` 不连模型、不开麦克风，走完
  模板 → 候选 → 采纳 → 落盘 → 重新加载 → 提示词包含经验 的全链路
  （`qa\knowledge-0200\knowledge.json`，10 项全过）。
- 测试：Core 116 → 132（新增 16 项 KnowledgeMerger / NotesCarry / JSON 往返）。
- 仍未做：`WorkspaceSeriesItem`（决定 / 待办）、源删除完整性、`SeriesBriefing`、系列级回顾候选的
  `contributingSources` 快照校验、应用内自动替换安装包。

## 0.19.0 之后的小修正

- 系列页「把这场景加入」现在会说明会议是从哪个系列移过来的（一个会议最多属于一个系列，加入即移动）。
- `--startup-check` 报告新增 `features`（当时 23 项，0.20.0 起 26 项），一份 JSON 既证明能启动，也说明这个构建带了什么。

# P6 到期日解析 · 0.19.0

0.19.0 补上模板纪要的到期日：

- **Core/NotesDates.cs**：移植 `NotesDates` —— 今天/明天/后天/大后天、月底、`10月30日`（跨半年自动算下一年）、
  中文与英文星期（含 `下周一` / `本周一` / `next Thursday` 的周次偏移）、以及 `11 月 19 日（周四）` 的格式。
- **NotesDueDates.Apply**：生成模板纪要后把能确定的到期说法换成具体日期与格式化文本（`周四` → `11 月 19 日（周四）`），
  模糊说法（`尽快`、`下周`）保持原样。
- 6 项测试（Core 合计 116）：今天/明天/后天、月底（含二月）、跨年、星期偏移、模糊说法、格式。
- 发布：0.19.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.78 秒、170 MiB）。

仍未做：`NotesShape` 的分组展示、`Workspace` 知识库与合并、`SpeakerClustering`、双屏演讲、同传上岛、
伴侣蓝牙/中转、应用内自动替换安装包。
# P6 转写校对 · 0.18.0

0.18.0 把模板纪要缺的「校对」这半边补上：

- **Core/TranscriptCorrections.cs**：移植 `TranscriptCorrection` 与 `NotesCorrections` ——
  更正按行 id、行号、唯一文本三级定位；带修订指纹（行 id + 原文的短哈希），指纹不符就不落地；
  已撤销（reverted）的跳过；上限 120 条。校对提示词与带行号的转写来自 `NotesPrompt.proofread*`。
- 7 项测试（Core 合计 110）：解析（`from`/`to` 与 `original`/`corrected`）、三级定位、
  精确子串替换、指纹不符跳过、撤销跳过、上限、提示词与编号转写、审计 JSON。
- **界面**：纪要页新增「校对转写」。模型只改上下文与资料能确定的识别错误（人名、术语）；
  确认后原子重写 `transcript.jsonl`，并把更正明细写入 `corrections.json` 作为审计记录；
  状态栏报告更正条数，会议过长时说明只校对了前 12000 字。
- 同时把纪要页头部改成两行（标题+主操作 / 模板+校对+自动整理），标题不再被挤成两行。
- 发布：0.18.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.76 秒、170 MiB）。

仍未做：`NotesDates`（到期日解析与相对日期）、`NotesShape` 的分组展示、`Workspace` 知识库与合并、
`SpeakerClustering`、双屏演讲、同传上岛、伴侣蓝牙/中转、应用内自动替换安装包。

# P6 模板纪要 · 0.17.0

0.17.0 把纪要从「一种固定格式」变成 Mac 版那样按场景换模板：

- **Core/NotesTemplates.cs**：移植 `NotesTemplates.swift` 与 `MeetingType.swift` 的模板部分 ——
  九种模板（面试复盘 / 组会研究讨论 / 汇报答辩 / 工作会议 / 客户沟通 / 讲座课程 / 小组作业 /
  小组讨论 / 通用），章节类型（一句话 / 要点 / 待办 / 逐题回放）、「不足」标记、
  存回系列的提示（经验 / 题库），以及场景 → 模板的默认表。
- 还移植了回复解析（按 id、标题、位置匹配章节；列表去重与截断）、Markdown 渲染（动作项带
  谁 / 何时 / 时间点，逐题回放带评价与更好的答法）和整理提示词（章节清单 + JSON 形状 + 说话人规则）。
- 8 项测试（Core 合计 103）。
- **界面**：纪要页新增模板下拉（默认跟随场景，例如 `--scene=Interview` → 面试复盘）与「按模板整理」；
  生成时用模板提示词调用回答服务，解析后渲染 Markdown 并写入 `summary.md`。会议过长时只取前
  12000 字，并在状态栏说明。
- 发布：0.17.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.74 秒、170 MiB）。

仍未做：`NotesTemplates` 的校对（proofread 提示与 `TranscriptCorrection`）与到期日解析
（`NotesDates`）、`NotesShape` 的分组展示、`Workspace` 知识库与合并、`SpeakerClustering`、
双屏演讲、同传上岛、伴侣蓝牙/中转、应用内自动替换安装包。

# P8 检查更新 · 0.16.0

0.16.0 加上方案 §14 的更新检查：

- **Core/UpdateCatalog.cs**：移植 `UpdateCatalog.swift` —— GitHub 发布页的 JSON、`update.json` 清单、
  正式版本与 main 最新构建两个频道、语义版本比较（预发布排在正式版之前，`beta.10 > beta.2`）、
  `shasum -a 256` 列表里的哈希查找。7 项测试（Core 合计 95）。
- **Desktop/UpdateFeed.cs**：从 HTTP(S) 或本地文件读发布页；挑出带 Windows 资产的候选，
  与当前程序集版本比较，生成 `UpdateReport`（当前版本、候选标签、是否可更新、下载地址、错误）。
- **界面**：设置页新增「更新」卡片（频道选择、检查更新、状态、打开下载页）。
  检查只提示并打开浏览器下载页，**不在应用内替换文件**——Windows 侧还没有签名安装包与发布流水线。
- **验收**：`--update-check=<feed> --update-report=<path>` 写出 JSON 报告；
  本地 feed 实测「有新版 v0.16.0 → available=true、下载地址取自 windows-x64.zip」，
  以及「已是最新 v0.15.0 → available=false」两条分支。
- 发布：0.16.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.75 秒、170 MiB）。

仍未做：应用内下载并校验后自动替换（需要签名与发布流水线）、`NotesTemplates`（模板与校对提示）、
`Workspace` 知识库与合并、`SpeakerClustering`、双屏演讲、同传上岛、伴侣蓝牙/中转。

# P7 术语表 · 0.15.0

0.15.0 把「术语与人名」从一行逗号分隔的热词升级成真正的术语表：

- **Core/Glossary.cs**：移植 `Glossary.swift` —— 条目的规范写法 / 错写法 / 备注、按 loose key 归并的
  `normalized()`、loose 与 strict 两种比较键、热词列表、提示词术语段、`正确写法 | 错写` 的文本导入，
  以及 `Glossary/glossary.json` 的读写。
- **规范写法引擎**同样移植：最长匹配优先、规范写法只保护不替换、错写法忽略大小写与全半角、
  拉丁字母/数字边界（`GPT` 不会命中 `GPTs`）、同一错写法按条目顺序取胜。
- 9 项测试（Core 合计 88）：比较键、纠正、边界、最长匹配、替换统计、热词、提示词、文本导入、存储往返。
- **接入**：设置页的术语表文本框（带条数与状态提示）；规范写法作为额外热词传给识别器
  （`SpeechRecognizerSession` 新增 `extraHotwords`）；最终转写里的错写法就地改回规范写法。
- 发布：0.15.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.74 秒、169 MiB）。

仍未做：`Glossary` 的检索命中与系列级术语表叠加（`layered`）、`NotesTemplates`（模板与校对提示）、
`Workspace` 知识库与合并、`SpeakerClustering`、双屏演讲、同传上岛、伴侣蓝牙/中转、自动更新。

# P7 系列与经验 · 0.14.0

0.14.0 加上 Mac 版「经验库」的第一层——系列：

- **Core/Workspace.cs**：移植 `Workspace.swift` 里系列相关的部分 —— `WorkspaceLesson`（一句话经验，
  带来源会议）、`WorkspaceSeries`（会议 id 列表 + 经验）、`WorkspaceData`（新建 / 归档 / 移出 /
  改名 / 加经验 / 删经验；一个会议最多属于一个系列），以及 `WorkspaceStore`
  （`Workspace/workspace.json`，容忍损坏文件与重复 id）。
- 6 项测试（Core 合计 79）：重名拒绝、一个会议只属于一个系列、经验增删、改名去重、JSON 往返、
  坏文件与重复 id 的清洗。
- **界面**：新增「系列」页 —— 左列列出所有系列（会议数、经验数与最后一条经验），右侧显示所选系列的
  会议、经验（可删）、以及「记一句话经验」输入框；顶部可新建系列，右上可把这场景归档进系列或移出。
- 演示会议改用固定 id，演示数据里预置两个系列（硕士论文 · 监测方案、秋招面试），可离线验收。

仍未做：`Workspace` 的知识库与变更候选（`KnowledgeMerger`、`WorkspaceSeriesChangeCandidate`）、
`NotesTemplates`（模板与校对提示）、`Glossary`（术语表）、双屏演讲、同传上岛、伴侣蓝牙/中转、
自动更新，以及 Core 其余 62 个文件。

# P6 观点对照矩阵 · 0.13.0

0.13.0 把「观点对照」也画出来（此前只有鱼骨图是画的）：

- **Views/ViewpointMatrixView.cs**：议题为行、各方为列的真矩阵，单元格里是那一方在该议题上的原话
  与时间；下方按「共识 / 分歧 / 分工」列出，没有观点的格子写「—」。带时间的条目可点击跳回原话。
- 纪要页按格式选视图：鱼骨图 → `FishboneView`，观点对照 → `ViewpointMatrixView`，大纲 → Markdown；
  `--notes-format=fishbone|outline|viewpoints` 可在演示模式下直接打开某一种，便于离线验收。
- 演示数据新增一份观点对照回复（我 / 导师两方、两个议题、共识、分歧、分工）。
- 发布：0.13.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.71 秒、168 MiB）。

仍未做：`Workspace`（系列/经验库）、`NotesTemplates`（模板与校对提示）、`Glossary`（术语表）、
双屏演讲、同传上岛、伴侣蓝牙/中转、自动更新，以及 Core 其余 63 个文件。

# P6 结构化纪要的核对与绘制 · 0.12.0

0.12.0 把 0.11.0 的三种格式从「生成的 Markdown」变成「核对过的图」：

- **Core/StructuredNotesCleaner.cs**：移植 `StructuredNotesCleaner` 与
  `NotesVerifier.isSupported` —— 每个节点的时间点必须在会议时长（+90 秒）内，且离某条转写的
  时间不超过 45 秒，否则整条丢弃并计数；随后按上限截断、同名类别合并、重复条目去掉、文本压平截短；
  剩下不足两个节点就整份不画。观点对照还会把「不在 parties 里、也没在任何观点里出现」的一方剔除。
- 8 项测试（Core 合计 73）：支持窗口、无时间丢弃、类别合并与去重、截短、大纲与观点对照过滤。
- **界面**：生成结构化纪要时先过核对，状态栏报告「N 条找不到原话依据的内容已丢弃」；
  核对不通过则不写缓存。鱼骨图由新的 `FishboneView` 画成骨图（脊线 + 上下交替的肋骨 + 带头部的
  问题），有时间的条目可点击跳回；大纲与观点对照仍以带时间的 Markdown 呈现。
- 缓存改为 Markdown + JSON 两份：JSON 用于重新画出骨图，Markdown 用于复制与导出。
- 发布：0.12.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.74 秒、168 MiB）。

仍未做：观点对照的矩阵表格绘制、`Workspace`（系列/经验库）、`NotesTemplates`（模板与校对提示）、
`Glossary`、双屏演讲、同传上岛、伴侣蓝牙/中转、自动更新，以及 Core 其余 63 个文件。

# P6 结构化纪要 · 0.11.0

0.11.0 补上 Mac 版四种纪要格式里的后三种：

- **Core/StructuredNotes.cs**：逐文件移植 `StructuredNotes.swift` —— `NotesFormat`（标准/鱼骨图/大纲/
  观点对照，未知值按标准读）、三套数据模型、`StructuredNotesPayload`、`StructuredNotesLimits`、
  容错 JSON 解析（代码围栏、尾逗号、纯字符串条目、外层包装键、`12:41` 或秒数时间）、
  Markdown 渲染，以及中英两套提示词（含 `partialUser`/`mergeUser` 分段合并）。
- **Core/NotesTime.cs**：`NotesTime`（`00:00` / `1:02:05` 互转）与 `StructuredNotesMarkdown`。
- 8 项测试（Core 合计 65）：三种格式解析、畸形回复、Markdown 结构、提示词里的上限与 schema、
  场景推荐格式。
- **界面**：纪要页新增格式胶囊（文字纪要 / 鱼骨图 / 要点大纲 / 观点对照）与「生成这种纪要」；
  生成时用 `StructuredNotesPrompt` 组提示词、调用当前回答服务、解析后渲染并缓存到
  `Sessions/<ID>/structured-<格式>.md`；打开一场会议时按场景推荐自动选中并加载缓存。
  演示数据内置一份鱼骨图回复，无需模型即可查看渲染效果。

尚未移植：`StructuredNotesCleaner`（依赖 `NotesVerifier`）与图形化绘制（鱼骨曲线、观点矩阵表格）——
目前结构化纪要以带原话时间的 Markdown 呈现，导出与复制走同一条路径。

# P6 场景与临场教练 · 0.10.0

0.10.0 落地 Mac 版的差异化主干——九个场景、提词样式与临场教练：

- **Core/Scene.cs**：逐文件移植 `Scene.swift`（`CueStyle`、`CoachingProfile`、`ScenePreset.For`、
  `CueLine`、`BilingualAnswer`、`CueScript` 要点/讲稿解析、`SpeechUnits`、`SpokenSegment`、
  `CoachSnapshot`、`SpeakingCoach`）；`CoachSummary.cs` 移植会议级统计；`AnswerPoints` 的
  `Split`/`StripMarkup` 从 `LiveInsights.swift` 先取过来供提词使用（标注为部分移植）。
  新增 11 项测试，Core 合计 57 项。
- **九个场景**：首页「下一场会议」卡片上的场景胶囊（面试 / 汇报答辩 / 组会 / 小组作业 / 客户沟通 /
  工作会议 / 讲座课程 / 小组讨论 / 通用），选中即写入会议 `mode`（实测 `interview` 落盘），
  并决定提词样式（按要点 / 按句）与教练规则。
- **提词岛升级**：不再显示原始回答，而是 `CueScript` 解析后的关键点（或逐句讲稿），右侧显示当前场景名。
- **临场教练**：会议中每 5 秒看一次语速、独白时长与发言占比，按场景阈值提示（独白超限 / 语速偏快 /
  讲得太多 / 讲得太少），会议页提示条可「知道了」。
- 发布：0.10.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.75 秒、168 MiB）。

仍未做：结构化纪要（`NotesTemplates` / `StructuredNotes`）、系列与经验库（`Workspace`）、
双屏演讲、同传上岛、伴侣蓝牙/中转、自动更新，以及 Core 其余 64 个文件。

# P5/P7 托盘、自动收尾 · 0.9.0

0.9.0 补齐桌面集成与「无人发言自动结束」：

- **系统托盘**（`TrayIntegration.cs`）：打开主窗口 / 隐藏所有窗口 / 开始或停止记录 / 退出，
  图标来自应用资源；托盘图标在通知区溢出面板中可见（`qa\shots\v08-tray-overflow.png`）。
- **一键隐藏**：Ctrl+Alt+\ 收起主窗口与全部浮窗，会议继续记录；再按或点托盘恢复
  （实测 `v08-hidden.png` / `v08-restored.png`：窗口消失、进程存活、可恢复）。
- **关闭窗口的生命周期**：会议进行中关窗口只收到托盘，不退出（对应 `AppState+Lifecycle.swift`）；
  退出走托盘的「退出 NextThought」。
- **会议自动收尾**：Core 新增 `MeetingEndWatch.cs`（8 项测试），AppState 每秒 tick，设置页新增
  「会议自动收尾」卡片（模式 + 5/10/20/30 分钟），会议页在倒计时时显示提示条与「继续记录」。
  结束时 `endedAt` 回填到最后一句话之后，不把静音尾巴写进记录。
- **修复的坑**：设置页 ComboBox 的 `SelectedItem` 在启动阶段会向绑定属性写 `null`，导致
  `AutoEndMinutes` 变空、自动收尾一直用默认 10 分钟。改为忽略空写入后实测：
  `mode=end`、`minutes=1`、60 秒无发言 → `NobodySpoke`，会话 `endedAt` 落盘。
- 发布：0.9.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.7 秒、166 MiB）。

仍未做：双屏演讲、同传上岛、伴侣蓝牙/中转、结构化纪要、场景预设、系列/经验库、
自动更新，以及 Core 其余 68 个文件。

# P4/P5 历史页与 Core 批次 · 0.8.0

0.8.0 增加独立的历史页，并把 Core 批次 A 又推进三步：

- **历史页**（`Views/HistoryView.axaml`）：标题/日期/模式搜索，`全部 / 有纪要 / 未整理` 筛选，
  计数（`N 场会议 · 共 M 场`），空结果提示，点行回到该会议原话。实测：筛选「未整理」
  时 3 场里只显示 1 场（`qa\shots\v07-history-filter.png`）。
- **Core 移植**：`AudioTimeline`（10 测试）、`TranscriptDeduplicator`（8 测试）、
  `EchoSuppression`（8 测试，已接入转写回声判定）。Core 测试 38 项全部通过，
  详见 `CORE_PARITY.md`。
- **桌面端链路实测**：`--import-wave=<wav>` 走与界面按钮相同的 `AppState.ImportWaveFileAsync`，
  用真实 X-ASR 模型归档出 `transcript.jsonl`（1 行）与会话目录，证明
  界面 → AppState → 识别引擎 → 归档 全链路可用。
- **DPI 结论**：界面在 175% 缩放下布局正确；此前「截断」是截图脚本不是 DPI 感知导致的
  虚拟化坐标裁剪，脚本已修（`SetProcessDpiAwarenessContext(-4)`），启动自检会记录
  `renderScaling`。
- 发布：0.8.0 自包含目录、ZIP、安装包；自检与安装后自检通过（1.7 秒、154 MiB）。

仍未做：双屏演讲、同传上岛、手机伴侣蓝牙/中转、系统托盘、结构化纪要（提纲/鱼骨图/观点对照）、
场景预设、系列与经验库，以及 Core 其余 69 个文件。

# P5 提词器与区域截图 · 0.7.0

0.7.0 补齐方案第 9 节剩下的两个浮窗类界面，并把它们接入快捷键与资料库：

- **提词器浮窗**（`Views/TeleprompterWindow.axaml`）：无边框、置顶、可拖动、可缩放（默认 640×320，
  最小 360×160），字号 `A-`/`A+`（12–64）、不透明度 `−`/`+`（0.25–1.0）、双击标题栏隐藏控件；
  「穿透」用 `WS_EX_LAYERED | WS_EX_TRANSPARENT` 切换鼠标穿透。位置、大小、字号、透明度与穿透状态
  存在数据目录的 `win-ui.json`（与方案第 13 节一致：Windows 独有设置不进 settings.json、不进备份）。
  实测：穿透开启时窗口扩展样式为 `0x280028`（含 0x20），关闭时为 `0x280008`。
- **区域截图**（`ScreenCapture.cs` + `Views/RegionCaptureWindow.axaml`）：抓屏前把主窗口最小化，
  GDI `BitBlt` 复制虚拟屏幕，拖选矩形后裁切，保存 PNG 并以资料形式导入资料库，由
  `Windows.Media.Ocr` 读取文字；`Ctrl+Alt+S` 或会议页「截图」按钮触发。
  实测：3000×2000 抓屏、1500×1000 裁切、134 KB PNG，导入后 `document.json` 有 1 段中文 OCR 文本。
  与 Mac 的差异：Mac 是在目标显示器铺满屏遮罩窗口框选，这里改为「先抓屏、再在普通窗口里框选」，
  多显示器时同样可用；已在 DECISIONS.md 记录。
- **快捷键**新增 Ctrl+Alt+S（区域截图）、Ctrl+Alt+T（提词器），与既有 R/O/M/N 一起注册在
  独立消息泵线程上。
- **发布**：0.7.0 自包含目录、ZIP、Inno Setup 安装包；自检通过、静默安装后自检通过（1.7 秒冷启动、
  154 MiB 空闲内存）。

仍未做：双屏演讲、同传上岛、手机伴侣的蓝牙/中转、系统托盘、Core 其余文件的逐文件移植。

# P4/P5 浮窗与快捷键 · 0.6.0（Avalonia）

0.6.0 在 0.5.0 的 Avalonia 外壳上补齐方案第 9 节的首批浮窗与第 7 节的全局快捷键：

- **提词岛**：无边框、透明、置顶、不激活的窗口，屏幕顶部居中，显示当前问题与三行回答，
  回答文本去掉 Markdown 标记；Ctrl+Alt+O 或会议页按钮开关。
- **字幕条**：底部居中一句，带译文时译文为主、原文在下；Core 新增 `SubtitleBar.cs`
  （LiveWindowMode、SubtitleBarTiming、SubtitleFeed、SubtitleText 半角宽度截断）逐文件移植自
  `SubtitleBar.swift`，12 项 xunit 测试通过；4 秒保持 + 0.5 秒淡出。
- **三态切换**：纪要窗口 / 字幕条 / 隐藏，界面按钮与 Ctrl+Alt+M 循环。
- **全局快捷键**：`GlobalHotkeys` 在独立消息泵线程上用线程级 `RegisterHotKey` 注册
  Ctrl+Alt+R/O/M/N，`WM_HOTKEY` 回投 UI 线程；实测四组注册成功并可用。
  注意 HWND_MESSAGE 窗口注册会返回 1408，不可用。
- **手机伴侣（局域网通道）**：地址列表、开启/关闭、配对二维码与链接、快照发布，
  复用 Platform 已有 `CompanionServer`；首次开启触发 Windows 防火墙询问，与方案第 12 节一致。
- **真实识别复核**：X-ASR 480 ms + 整句精修，10.05 秒样例输出两行、RTF 0.078；
  证据 `qa\asr\xasr480-0.json`。

发布：`build_desktop.ps1 -Version 0.6.0` 一次完成构建、自检、安装包与 ZIP；自检通过，
安装后自检通过，冷启动约 1.6 秒（含 400 ms 等待），空闲内存约 166 MiB。

仍未做：提词器浮窗、区域截图、双屏演讲、同传上岛、蓝牙/中转、Core 其余 79 个文件的逐文件移植。

# P4 界面主线 · 0.5.0（Avalonia）

0.5.0 用 Avalonia 12.1.6 重写界面，取代 WPF 预览窗口（WPF 工程暂留作对照，下一阶段删除）。
新增 `src/NextThought.Desktop`：自绘标题栏与侧栏外壳、五个页面（首页、会议、纪要、资料库、设置）、
`MarkdownText` 渲染控件、演示模式（`--demo` / `--section=` / `--dark`）与 `--startup-check` 自检。
应用层编排 `AppState` 按 WPF 基线逐段移植，服务仍复用 Platform。发布为自包含目录、ZIP 与
Inno Setup 每用户安装包；本机实测：自检通过、安装后自检通过、冷启动约 1.0 秒（首次运行含系统扫描约 6 秒）、
空闲内存约 160 MiB。Native AOT 因本机缺少 MSVC 生成工具按方案第 2 节回退，结构化差异见
[`docs\DECISIONS.md`]。

状态行：界面主线已可在 Windows 上完成「开始记录 → 转写 → 回答 → 结束 → 纪要 → 导出」的代码路径，
但真实采音与模型通路尚未在本轮实机复核（延用 0.4.0 的验收记录）；浮窗类界面、手机伴侣与 Core
逐文件移植仍未做。

# P4 分步更新 · 0.4.0

0.4.0 未签名验收预览已交付：每用户安装、独立版本目录、清单哈希校验、显式回退、卸载保留数据和一键自检已实现。79 项回归、20 项隔离安装生命周期、自包含 CLI/实际 WPF 启动，以及私有验收分支的真实 Windows CI 全部通过。7200 秒公开 PCM 实时双路组合回放通过，72,000 帧/路、720 条转写、480 次声纹、160 条译文、122 次检索、2 次引用回答；终止错误 0、网络重试 0。ASR 队列峰值 12 帧、结束 0 帧，最终句延迟 p95 0.445 秒，热身后主进程私有提交变化 -8.11 MiB。取消恢复、增强 OCR、纪要、导出及备份往返通过。

第二台 Windows 客户端实测由用户选择稍后进行，尚无实机报告；可信签名没有证书，只交付明确标记的未签名包。P4 因此保留待验项，不记为正式发布或全平台验收完成。手机/USB 等原有跳过项继续保留。长测是公开 PCM 每 30 秒约 10 秒片段的受控回放，不是两小时硬件采音或长时桌面交互。最新证据见 [P4_VALIDATION.md](P4_VALIDATION.md)、[第二台电脑说明](P4_SECOND_PC.md)、[Mac 功能差异表](FEATURE_PARITY.md)；以下各阶段段落是历史快照。

# P3 分步更新 · 0.3.1

P3 基础实现与本轮收尾验收已完成，手机命令及重连按用户要求暂跳。0.3.1 修正校园网 100.64.0.0/10 地址遗漏及回环二维码问题；桌面导入、声纹、补译取消重试、纪要、导出及正常退出重启重载通过。1 小时公开 PCM 实时双路组合长测通过，360 条转写、80 条译文、2 次引用回答，取消恢复、增强 OCR、纪要及备份往返通过，失败 0、网络重试 0；73 项回归全过。手机热点页面同步由用户确认，热点现已关闭；不推断校园网或普通路由器实机命令已通过。证据及硬件、OCR、Mac 兼容范围见 [P3_VALIDATION.md](P3_VALIDATION.md)。

# P2 更新 · 0.2.0

P2 五步已完成基础实现与本机验收，49 项回归通过。详细结果、证据与 Mac 迁移限制见 [P2_VALIDATION.md](P2_VALIDATION.md)。P3 已开始，最新状态见本页顶部。

# Windows 移植状态：2026-11-07

版本：Windows 预览 0.1.1。P1 的单/双路实采、回答、纪要和异常恢复已验证；完整会议的 30 分钟受控回放单独记录。上游快照 `af60a4def90c750872197f81ac8b91c726d05b63`；分支 `port/windows-native`。所有新实现位于 `Windows/`，并增加独立 Windows CI。Mac 源码及签名资料保持原状。最新逐步进度同步到用户指定的 `D:\md Vault\NextThought移植路线.md`。

## 实现与验证边界

| 环节 | Windows 实现 | 当前证据 |
|---|---|---|
| 桌面界面 | .NET 10 / WPF，会议、纪要、设置、历史 | 已启动真实窗口；检查布局、导入、重载、导出 |
| 系统声 / 麦克风 | NAudio WASAPI、独立端点、16 kHz 单声道、100 ms 音频帧 | HECATE 实际单路/双路通过；4 倍软件增益下固定句尾完整 |
| VAD | 上游 RMS 自适应阈值、静音结束、12 秒切段 | 结束与重叠尾段去重复回归通过 |
| 流式 ASR | CPU Sherpa-ONNX 1.13.8 + ONNX Runtime 1.28.2；X-ASR 480 / 160 | 480 ms 模型完成 4 段真实 WAV 推理；160 ms 未验收 |
| 整句精修 | 同一套 X-ASR 离线 int8 模型 | 4 段推理包含精修；非精度基准 |
| 中文路径 | 显式 UTF-8 原生配置封送、EXE UTF-8 清单 | 在中文源码路径及用户模型目录完成真实推理 |
| 模型安装 | 官方下载、SHA-256 校验、临时目录解压及替换 | 用已核验的模型压缩包完成 C# 安装器实测 |
| 问题检测 | 移植上游词表、意图规则、候选稳定、重复抑制、修订替换 | 原 60 条双语语料及候选跟踪回归通过 |
| 回答 | 兼容 Chat Completions 的流式 API；发言期间延后自动回答 | DeepSeek 官方 deepseek-flash 连接、手动、问题触发、取消均通过；截断/中断不保存为完整答案 |
| 纪要 | 分段检查点、归并、失败续作，保留旧纪要 | DeepSeek 短会议与分段归并通过；受控故障后复用已完成片段 |
| 凭据 | 当前 Windows 用户 DPAPI | 往返加解密及普通设置排除明文回归通过 |
| 会议存储 | JSON / JSONL，追加串行、元数据原子替换、导出 | 并发追加、末尾不完整行恢复、重载和导出通过 |
| 异常恢复 | 会话租约、启动修复、原文件备份、计时中断检测 | 独立进程异常退出后恢复通过；实际 WASAPI 暂停注入后同一对象可重新开始 |

## 本机验证

- 环境：Windows 11 x64，i9-14900HX，约 32 GB 内存。识别使用 CPU，未启用 RTX 4060 加速。
- Release 构建：0 警告、0 错误；37 项自动回归全部通过、0 跳过，涵盖问题检测、VAD、归档、DPAPI、流式接口、纪要续作、旧元数据覆盖、采音计时和会话恢复。
- 4 段音频来自官方 X-ASR 模型包，时长约 4.7–10.1 秒。进程加载模型加推理约 4.7–6.0 秒；纯推理 RTF 约 0.09–0.13，**不含模型初始化**。样本很短，不代表真实会议准确率或端到端实时延迟。
- 桌面操作：导入 `xasr480-0.wav`，显示 2 条最终转写；新建会议后可从历史恢复；导出 `artifacts/ui/官方样例转写.md`，内容与界面相符。
- 独立发布包：EXE 已实际启动、载入原有归档；历史时间显示为本地时间，纪要长文本从顶部排版。包内 CLI 再次完成真实转写；配置声明自带 .NET / WindowsDesktop 10.0.14。语音原生库导入检查未发现额外的 VC++ 运行库 DLL 依赖。
- 本机默认模型目录 `%LOCALAPPDATA%\NextThought\models` 已准备 480 ms 与整句精修模型。HECATE 单路/双路测试由用户确认开始，原始音频未保存。DeepSeek 仅使用合成会议或公开教学音频转写进行服务验收，没有发送麦克风测试内容。
- 0.1.1 验证明细在 `artifacts/p1/verification.json`，0.1.0 原始记录保留在 `artifacts/verification.json`。发布包有同名 `.sha256` 文件。CI 已编写，未推送到远程触发。
- P1-5：30 分钟实时受控双通道 PCM 回放通过，36,000 帧、90 条转写、1 条会中手动回答，时间线有效、错误为 0；随后真实纪要、重载、导出通过。热身后的工作集约 680.6–682.4 MiB。此项不是连续 30 分钟硬件采音。

## 下一阶段验收顺序

1. **已完成的 P1 核心**：实采、回答、分段纪要和恢复的本轮证据位于 `artifacts/p1`，各步骤完成后立即更新用户路线文档。
2. **范围调整**：USB 物理拔插按用户要求跳过；真实系统休眠/唤醒没有操作，暂停处理通过实际 WASAPI 链路的时钟故障注入验证。
3. **会议稳定性**：30 分钟受控双通道 PCM 回放单独验收；2 小时及连续真实硬件采音仍属后续专项。
4. **功能扩展**：先 Codex 后端，再独立 Python / ONNX 的 OCR 与检索服务、ONNX 声纹与翻译；最后手机伴侣和平台特有交互。MLX 与 CoreML 路线需要替换运行后端。
5. **发布工程**：干净机器安装验收、签名安装器、升级回滚、备份导入、ARM64 专项构建。当前为自带运行环境的 x64 ZIP。

## 明确尚未覆盖

当前未达到原 Mac 产品完整功能及验收覆盖；已移植功能见 P2/P3 验收记录。订阅授权、自动升级、手机 WSS/蓝牙及逐字同传等尚未移植。界面当前按会议通用模式使用问题规则；没有开放全部上游场景预设。

交付目标是可运行、可复现和可继续开发的第一阶段基线；不能作为已经达到 Mac 全功能等价的证明。
