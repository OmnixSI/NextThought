# NextThought Windows 移植工作区

Mac 版 NextThought（macOS / SwiftUI）向 Windows 的移植工作区。源码、工具链、中间产物与
结果文件都在 D 盘本目录下。

```
D:\NextThought开发\
├─ src\NextThought\            仓库克隆（分支 port/windows-native，基线提交 01606e2）
│  └─ Windows\                 移植代码：Core / Platform / Desktop(Avalonia) / Cli / tests / installer
├─ dist\                       发布结果（当前 0.20.0）
│  ├─ NextThought-0.6.0-win-x64\                自包含可执行目录
│  ├─ NextThought-0.6.0-windows-x64.zip         压缩包 + .sha256
│  └─ NextThought-0.6.0-windows-x64-setup.exe   每用户安装包（约 46 MB）
├─ docs\                       方案原文（plan_v2.txt）、决策与发现记录（DECISIONS.md）
├─ logs\                       构建与测试日志
├─ qa\                         启动自检、安装测试、界面截图、模型与真实识别结果
├─ tools\                      辅助脚本（窗口截图、整屏截图）
└─ swift-probe\                Windows Swift 编译 Core 的可行性实验
```

## 当前可用成果（1.3.4）

- **可运行的 Windows 程序**：`dist\NextThought-1.3.4-win-x64\NextThought.exe`，
  自包含（目标机无需安装 .NET），Avalonia 12 界面，按 Mac 的设计令牌复刻。
- **安装包**：`dist\NextThought-1.3.4-windows-x64-setup.exe`，每用户安装到
  `%LOCALAPPDATA%\Programs\NextThought`，不需要管理员权限；数据目录
  `%LOCALAPPDATA%\NextThought` 与安装目录分离，卸载不删会议。
- **功能**：
  - 双路采音（系统声 + 麦克风）、sherpa-onnx 流式识别、问题识别与自动回答；
  - OpenAI 兼容接口与 Codex 两种后端、会后纪要（Markdown 渲染）、导出 Markdown；
  - 历史会议、资料库导入与检索、备份导入导出、设置与演示模式；
  - 历史页：按标题/日期/模式搜索，按「全部 / 有纪要 / 未整理」筛选，点开回看原话；
  - 提词岛（顶部黑色药丸，问题 + 三行回答，置顶不激活）；显示的是按场景排好的关键点或逐句讲稿；
  - 九个场景（面试 / 汇报答辩 / 组会 / 小组作业 / 客户沟通 / 工作会议 / 讲座课程 / 小组讨论 / 通用），
    选择后写入会议归档的 mode，并决定提词样式与教练规则；
  - 临场教练：语速、独白时长、发言占比按场景给出提醒（会议页提示条，可「知道了」）；
  - 系列与经验：把同一门课/项目/面试的会议归档到一个系列，记一句话经验，存于 `Workspace\workspace.json`；
  - **系列知识库**：按模板整理纪要后，模板里「下次改进 / 逐题回放」这类章节会变成待确认候选；
    系列页的「待确认」页签逐条 采纳 / 替换回答 / 忽略，「经验」与「题库」只收确认过的内容；
    确认后的经验与题库进入回答提示词（`WorkspaceKnowledgePrompt`），处理结果留回执（`workspace.json`）；
    离线自检 `--knowledge-check=<json>` 走完 模板 → 候选 → 确认 → 落盘 → 重载 → 提示词 全链路；
  - **逐字同传**：识别还在变化时，字幕条与提词岛上的译文也跟着更新；旧快照的译文不会盖住新句子
    （`LiveTranslationCoordinator` 限流 + 过期丢弃），整句落地后由整句译文接管；
  - **双屏演讲**：设置里选「幻灯片所在屏」，开始记录时主窗口收起、提词岛留在摄像头这一屏，
    Ctrl+Alt+N 唤回；只有一块屏时不会启用，避免把会议窗口藏起来（`PresenterLayout`）；
    幻灯片屏截图与页码匹配（LiveInsights）尚未移植，自检用合成第二块屏验证窗口行为；
  - 术语表：正确写法 | 识别错的写法，正确写法作为识别热词，转写里的错写自动改回（`Glossary\glossary.json`）；
    已导入资料里的技术术语（XL-MIMO / TSIR / RQ2 这类）由 `MaterialTerms` 自动抽取，和术语表一起成为识别热词；
  - 应用内更新：检查更新后可「下载并安装」——下载 Setup、校验发布里的 SHA-256、通过后静默安装并退出重启；
    没有校验文件的发布只给下载页；自检开关只记录启动命令，不真的替换当前构建；
  - 模板纪要：九个内置模板（面试复盘 / 组会 / 汇报答辩 / 工作会议 / 客户沟通 / 讲座课程 / 小组作业 / 小组讨论 / 通用），按场景自动选，
    「按模板整理」用模板提示词生成并解析成章节，渲染 Markdown 后写入 summary.md；
  - 校对转写：只改能确定的识别错误（人名/术语），按行定位并带修订指纹，改动留档 `corrections.json`；
  - 结构化纪要：文字纪要 / 因果鱼骨图 / 要点大纲 / 观点对照四种格式，后三种调用回答服务生成，
    每条带原话时间（`[05:20]`）；生成后逐条核对——时间必须在会议内、且 45 秒内有一条转写，
    找不到依据的丢弃并在状态栏报数；鱼骨图画成骨图、观点对照画成矩阵（共识 / 分歧 / 分工在下），大纲渲染为 Markdown，
    并缓存到会话目录的 `structured-<格式>.md` / `.json`；
  - **字幕导出**：纪要页可把当前会议导出 SRT / VTT（确定性时间戳；缺结束时间按文字长度估算并在状态栏报数，
    有译文时带「译文」行），UTF-8 无 BOM；
  - 字幕条（底部居中一句，带译文，4 秒后淡出）；三态切换：纪要窗口 / 字幕条 / 隐藏；
  - 全局快捷键：Ctrl+Alt+R 开始或停止记录、Ctrl+Alt+O 提词岛、Ctrl+Alt+M 三态切换、Ctrl+Alt+S 区域截图、Ctrl+Alt+T 提词器、Ctrl+Alt+N 唤起主窗口；
  - 手机伴侣：局域网地址选择、开启/关闭、配对二维码与链接；
  - **伴侣中转**：设置里填 HTTPS 中转地址后，配对链接走 WSS，端到端加密（HKDF-SHA256 + AES-256-GCM，
    中继只见密文）；状态上行、命令下行、回执都在 `CompanionRelayClient` 里；
    `--relay-check` 用本地中继 + 模拟手机跑通全链路；蓝牙通道在手机端改名前标为不可用；
  - 提词器浮窗：可拖动、可缩放、字号/透明度可调、鼠标穿透（已验证 WS_EX_TRANSPARENT 位）；
  - 区域截图：框选后裁切并存入资料库，由 Windows 系统 OCR 取字（已验证 1 段中文文本）；
  - 深色外观：`--dark` 全页面可用（`qa\shots\v07-dark-home2.png`）；
  - 会议自动收尾：长时间没有识别到文字后提示并结束（可设 5/10/20/30 分钟；直接结束/不自动结束）；
    「继续记录」或再次说话可取消，结束时把 `endedAt` 回填到最后一句之后；
  - 系统托盘与一键隐藏：托盘菜单（打开/隐藏全部/开始或停止记录/退出），Ctrl+Alt+\ 收起全部窗口，
    会议进行中关窗口只收到托盘。
- **Core 逐文件移植**：已移植 `SubtitleBar`、`AudioTimeline`、`TranscriptDeduplicator`、`EchoSuppression`、
  `MeetingEndWatch`、`Scene`、`CoachSummary`、`StructuredNotes`、`Workspace`（系列 + 知识库 + `KnowledgeMerger`）、
  `Glossary`、`UpdateCatalog`、`UpdateChecksums`、`NotesTemplates`/`NotesDates`、`LiveTranslation`、`PresenterMode`、
  `CompanionRelayCrypto`、`AnswerLanguage`、`AnswerEvidence`（SearchDecision/AnswerGrounding）、
  `MaterialTerms`、`ShortcutBinding`、`CodexEventParser`、`TranscriptContextBuilder`/`OnboardingVersion`、`SessionExport`（SRT/VTT）、`QuestionDetector`、`DocumentNotice`、`BackupContent`/`BackupArchivePolicy`、`WebLookupPromptBuilder`、`SessionTranscriptLocator`、`OnboardingFlow`、`VoiceActivitySegmenter`（完整边界与 partial）、`LocalModelReuse`、`SpeakerClustering`（声音聚类 + 说话人编号/命名 + 提示词段落）、`LaTeXMath`（公式转 Unicode 并接入 Markdown 渲染）、`Skill`、`VoiceProfile`、`DocumentClassifier`、`ScreenDelta`、`CompanionBLEProtocol`、`IncrementalBackup`（内容寻址恢复点）、`DocumentStructure`（文档块与检索分块）、`QuestionTurn`（整轮提问与何时该答）、
  `WebSearchNeed`、`ScrollFollowState`/`ScrollFollowInputClassifier`、`RealtimeSetupReadiness`、
  `DocumentFeatureAvailability`（共 779 项 xunit 测试 + 基线 83 项），
  回声过滤、自动收尾、场景与教练、结构化纪要、系列与经验、系列知识库、逐字同传、双屏演讲、伴侣中转、应用内更新、术语表、检查更新、模板纪要、转写校对、
  到期日已接入运行时；其余 1 个文件的状态见
  [`CORE_PARITY.md`](<D:\NextThought开发\src\NextThought\Windows\CORE_PARITY.md>)。
- **验证**：
  - 真实语音识别：X-ASR 480 ms + 整句精修，10.05 秒音频输出两行、RTF 0.078（`qa\asr\xasr480-0.json`）；
  - 自动收尾实测：`mode=end`、1 分钟，60 秒无发言触发 `NobodySpoke`，会话 `endedAt` 落盘
    （`qa\autoend-data7\Sessions\...\meta.json`、`Logs\hotkeys.log` 中的 `autoend tick` 记录）；
  - 场景实测：`--scene=Interview --import-wave=…` 归档 `meta.json` 的 `mode` 为 `interview`；
  - 结构化纪要：16 项解析 / 校验 / 渲染 / 提示词测试；鱼骨图绘制可离线查看
    （`qa\shots\v12-fishbone-drawn3.png`、`v13-notes-viewpoints3.png`）；真实生成需要配置回答服务密钥；
  - `--startup-check` 自检通过，安装包静默安装后再次通过（`qa\install-smoke-0190\startup.json`，170 MiB）；
  - 系列知识库离线自检 10 项全过（`qa\knowledge-0200\knowledge.json`）：候选 3 条、采纳 1 条经验 + 1 个问题、
    重载后仍在、提示词包含经验、自检系列清理干净；系列页三个页签见 `qa\shots\v20-series-candidates2.png`；
  - 逐字同传：门控 / 限流 / 乱序完成 / 换通道 / 新句子作废 共 12 项测试；提词岛上的译文行见
    `qa\shots\v21-island-translation.png`（`--demo --island`）；
  - 双屏演讲：10 项布局测试（一块 / 两块 / 多块屏、指定屏、指定屏不存在、内置屏与主屏优先级）；
    `--presenter-check` 用合成第二块屏验证「主窗口收起、提词岛留下、主窗口恢复、单屏拒绝启用」4 项，
    见 `qa\presenter-0220\presenter-check.json`；
  - 伴侣中转：9 项加密测试（RFC 5869 HKDF 向量、AES-GCM 信封、配对链接、命令新鲜度与去重）；
    `--relay-check` + 本地中继 + 模拟手机端到端通过：状态加密上行、命令加密下行、回执
    （`qa\relay-check-0230\relay-check.json`、`phone.json`）；
  - 应用内更新：5 项校验和测试；`--update-install-check` 对本地发布源完成 下载 → SHA-256 → 记录静默安装命令，
    篡改校验值时拒绝并删除下载（`qa\update-install-0230\update-install.json`、`update-install-badhash.json`）；
  - 字幕导出：11 项 Core 测试（SRT/VTT、估算时长与上下限、译文、空会议、时间戳）；`--demo --subtitle-export-check`
    导出 10 条、0 条估算，SRT/VTT 均为 UTF-8（`qa\subtitle-export-0270\demo.srt` / `.vtt`）；
  - 资料热词：`--hotwords-check` 导入一份含 XL-MIMO/TSIR/DeepMIMO/RQ2 的文档，术语表加一条 Kubernetes，
    识别热词为 `[Kubernetes, XL-MIMO, TSIR, DeepMIMO, RQ2]`（`qa\hotwords-0280\hotwords-check.json`）；
  - VAD 与模型复用：完整 VAD 7 项、模型复用 4 项测试；真实导入 `xasr480-0.wav` 用新 VAD 归档 2 条转写
    （`qa\models-data\Sessions\B8DD1429...\transcript.jsonl`）；
  - 说话人聚类：12 项 Core 测试（join/startNew、合并清零、5 条门槛编号、我/对方、命名重写、提示词）；
    `VoiceGroups` 已委托到 `VoiceClusterer`，原有自测按上游语义更新后 79 项全过；
  - 公式渲染：17 项 Core 测试；`--formula-check` 用真实 `MarkdownText` 控件渲染出「½、x²、∑ᵢ₌₁ⁿ i」，
    代码里的 `$` 保持原样（`qa\formula-0320\formula-check.json`）；
  - 增量备份：16 项 Core 测试（内容去重、快照共享对象、对象篡改/外部清单拒绝、按数量裁剪与无引用清理）；
  - 文档结构：11 项 Core 测试（标题路径、代码/公式原子块、表格分片重复表头、长段落 120 字重叠、稳定块 id）；
  - 提问轮次：14 项 Core 测试（中英词数、整轮拼接、25 秒断轮、3 词回应、8 词增长、静默/耐心判定）；
  - 答案提示词：9 项 Core 测试；`--prompt-check` 用真实构建器生成 4250 字系统指令 + 1447 字 JSON；
  - 语义检索组件：7 项 Core 测试（FNV 指纹、短段落合并、长文重叠分块、索引指纹校验、RRF 融合）；
  - 词法检索与会话上下文：8 项 Core 测试（中英词项、IDF 排序、检索预算、手动提问、Office XML 文本、显式页码/文件名优先、语义融合）；
  - 云端转写：9 项 Core 测试（三家服务商配置/鉴权/消息、转写装配、16→24 kHz 重采样）；`--cloud-check` 在真实进程验证；
  - 流式协议：11 项 Core 测试（Codex 请求/通知/事件、SSE/[DONE]/错误、请求体、端点、模型列表）；`--stream-check` 在真实进程验证；
  - 问句语料与评估：139 条标注语料；`--question-check` 实测 82/82 命中、0 误触发（`qa\question-0440\question-check.json`）；
  - PDF 版式：9 项 Core 测试（页眉页脚、页码、换行合并、断词、编号/期刊标题）；`--pdf-check` 在真实进程验证；PDF 导入已接入该版式整理；
  - 注视估计：8 项 Core 测试（眼部几何、中位数标定、平滑与迟滞、注视比例）；`--gaze-check` 在真实进程验证；Windows 侧未接摄像头；
  - 字节级 BPE：8 项 Core 测试（tokenizer.json、合并、结束符、中文三字节、NFC）；`--bpe-check` 在真实进程验证；
  - 术语检索：9 项 Core 测试（展开顺序、宽松键、误听标记、包含式展开、32 上限、合并/分层）；`--glossary-search-check` 在真实进程验证；
  - 进程与取消：5 项 Core 测试（行读取、退出码、取消不杀进程、共享任务）；`--process-check` 在真实进程验证；
  - 会话内容检索：9 项 Core 测试（直接/备用命中、去重、摘录窗口、全角匹配）；`--session-search-check` 在真实进程验证；
  - 系列简报：7 项 Core 测试（近期会议限额、置顶经验优先、题库排序、候选来源过滤）；`--series-briefing-check` 在真实进程验证；
  - 历史检索：8 项 Core 测试（标签/打开目标/片段、命中与不可读计数、每场词表、取消）；`--history-search-check` 在真实进程验证；
  - 备份规则：9 项 Core 测试（清单校验、模板合并、设置白名单与旧键迁移、密钥账户）；`--backup-manifest-check` 在真实进程验证；
  - 会议携带与计划：7 项 Core 测试（复习/经验/材料开关、有效模式、计划不改原系列）；`--meeting-plan-check` 在真实进程验证；
  - 证据模型：5 项 Core 测试（依据四档文案、默认值、完整溯源、结构定位、评审）；`--evidence-check` 在真实进程验证；
  - 用户内容：7 项 Core 测试（个人笔记/重点/编辑稿、校对决定合并、修订存储与旧草稿拒绝）；`--user-content-check` 在真实进程验证；
  - 工作区删除事务：9 项 Core 测试（清单去重、暂存/回滚、拒绝覆盖、授权与失败回滚）；`--workspace-deletion-check` 在真实进程验证；
  - 文档任务模型：10 项 Core 测试（限制、阶段文案与终态、默认值、进度、依赖错误/事件/产物）；`--document-job-check` 在真实进程验证；
  - 验收汇总：`--acceptance-check` 在真实进程一次跑通 61 项 Core 功能（术语/检索/简报/系列决定/会话删除/摘要解析/导出/阶段复盘/汇总提示词/投影构建/迁移发布/证据匹配/写队列/转写行投影/纪要导出/笔记编辑/笔记证据/经验与摘要回写/模板字段/备会提示/场景档案/幻灯片匹配/检索字段/回答证据/会话快照/快照缓存/归档元数据/会话预置/共享原件/场景猜测/备份合并/笔记检索/携带资料/导出格式/简报证据/投影笔记/备份归档/幻灯片跟随/OCR 空格/系列资料/备会基线/任务队列/系列变更/候选收集/归档路径/会话记录/备份/会议/证据/用户内容/删除/文档任务/归档/所有权/投影/Zipformer/更新回滚/会话存储）；
  - 归档写入：11 项 Core 测试（缺失/不可读目录、内容标记、写收据、追加换行修复与回滚、残留判定）；`--archive-persistence-check` 在真实进程验证；
  - 会话投影模型：7 项 Core 测试（选项、可搜索文本、回答完成、整体新鲜度）；`--acceptance-check` 覆盖；
  - 系列文档归档：6 项 Core 测试（归属目录、幂等保留手改、无块资料、非法 id）；`--acceptance-check` 覆盖；
  - Zipformer 模型描述：9 项 Core 测试（文件名/预设、必需文件与词表、热词切分写入、错误文案）；`--acceptance-check` 覆盖；
  - 安装包签名：`--signature-check` 报告 Authenticode 状态；`NEXTTHOUGHT_REQUIRE_SIGNATURE=1` 时无效签名会中止更新；\n  - 更新备份与回滚：6 项 Core 测试（整目录复制、最新优先、只留 2 份、覆盖恢复、非法版本）；`--update-backup-check` 在真实进程验证；
  - 退出后回滚：3 项 Core 测试（等待进程、复制、重启脚本与引号转义）；`--update-restore-check` 在真实进程验证；
  - 会话存储：`--acceptance-check` 用真实 SessionStore 建会/追加转写/结束/重载校验；
  - 摘要解析：8 项 Core 测试（标题/问题要点/经验/题库、连续答案、前缀变体）；`--acceptance-check` 覆盖；
  - Markdown 导出：7 项 Core 测试（标题/事实、陈旧提示、回答未完成、问题列表、时间戳与译文）；`--acceptance-check` 覆盖；
  - 阶段复盘提示词：6 项 Core 测试（结构、会话预算与排序、资料/经验截断）；`--acceptance-check` 覆盖；
  - 汇总提示词：8 项 Core 测试（模式/身份/重点、转写分块、头部字段、分段与合并）；`--acceptance-check` 覆盖；
  - 投影构建：6 项 Core 测试（转写定位、译文开关、问题/回答/摘要条目、修订稳定）；`--acceptance-check` 覆盖；
  - 迁移发布：7 项 Core 测试（暂存/校验、重复/非法 id、替换备份、提交失败回滚、恢复目录）；`--acceptance-check` 覆盖；
  - 证据匹配：6 项 Core 测试（引文/时间匹配、位置解析、评审时效、陈旧标记）；`--acceptance-check` 覆盖；
  - 有界写队列：9 项 Core 测试（事件/字节上限、溢出计数、确认丢失、前缀确认、永久失败、替换清理）；`--acceptance-check` 覆盖；\n  - 转写行投影：6 项 Core 测试（原/校对/译文显示、修订、偏移、导出使用投影）；`--acceptance-check` 覆盖；\n  - 纪要导出：2 项 Core 测试（纪要优先于摘要、空纪要回退）；`--acceptance-check` 覆盖；\n  - 笔记编辑：6 项 Core 测试（大纲/鱼骨/观点矩阵路径、修订校验、非法路径拒绝）；`--acceptance-check` 覆盖；\n  - 笔记证据：6 项 Core 测试（三种格式提取、引文/位置匹配、待确认标记、载荷修订）；`--acceptance-check` 覆盖；\n  - 经验与摘要回写：5 项 Core 测试（置顶优先、预算、关闭知识、摘要写回与拒绝）；`--acceptance-check` 覆盖；\n  - 模板字段：2 项 Core 测试（字段顺序、未知键排序、空值跳过）；`--acceptance-check` 覆盖；\n  - 备会提示：6 项 Core 测试（各场景两节、角色/复盘重点、输出结构、默认回落）；`--acceptance-check` 覆盖；\n  - 场景档案：3 项 Core 测试（字段顺序/长输入、资料分类、回答偏好）；`--acceptance-check` 覆盖；\n  - 幻灯片匹配：12 项 Core 测试（中文/英文相似度、屏幕判定、页排序、决定、讲稿、回答进度）；`--acceptance-check` 覆盖；\n  - 投影检索字段：2 项 Core 测试（类型/位置/备用文本/新鲜度映射、字段直接检索）；`--acceptance-check` 覆盖；\n  - 回答证据：7 项 Core 测试（搜索决策解析、grounding 规则、证据包、回答状态）；`--acceptance-check` 覆盖；\n  - 会话快照：2 项 Core 测试（转写/回答/摘要入索引、错误成为读取问题）；`--acceptance-check` 覆盖；\n  - 快照缓存：5 项 Core 测试（LRU 淘汰、超大拒绝、替换计数、删除/清空）；`--acceptance-check` 覆盖；\n  - 归档元数据：2 项 Core 测试（缺失/不可读/损坏区分、旧版回答迁移）；`--acceptance-check` 覆盖；\n  - 会话预置：5 项 Core 测试（模式标题/往返、资料字符数、预置字段、模板偏好）；`--acceptance-check` 覆盖；\n  - 共享原件：3 项 Core 测试（按哈希/路径/工作区保留、非原件保留、删除任务不自我保留）；`--acceptance-check` 覆盖；\n  - 场景猜测/系列：19 项 Core 测试（关键词顺序、组会排除、系列基准标题、相似度、最佳匹配）；`--acceptance-check` 覆盖；\n  - 备份工作区合并：5 项 Core 测试（新增、较新覆盖、较旧保留但并集、相同跳过、按 id 去重）；`--acceptance-check` 覆盖；\n  - 笔记检索字段：2 项 Core 测试（结构化节点入索引、空笔记不变）；`--acceptance-check` 覆盖；\n  - 携带资料开关：1 项 Core 测试（材料开关清空系列资料 id，知识不受影响）；`--acceptance-check` 覆盖；\n  - 导出格式：3 项 Core 测试（扩展名、标题、唯一性）；`--acceptance-check` 覆盖；\n  - 简报证据：2 项 Core 测试（支持/失效/修订过期、来源缺失仍保留）；`--acceptance-check` 覆盖；\n  - 投影笔记：1 项 Core 测试（结构化纪要进入投影与检索条目）；`--acceptance-check` 覆盖；\n  - 备份归档：4 项 Core 测试（清单/设置/系列、会话目录、非法清单、非备份 ZIP）；`--acceptance-check` 覆盖；\n  - 幻灯片跟随：2 项 Core 测试（模糊屏幕保持当前页、清晰新页切换与重置）；`--acceptance-check` 覆盖；\n  - OCR 空格桥接：1 项 Core 测试（中文单字间空格仍能生成二元组）；`--acceptance-check` 覆盖；\n  - 系列资料：1 项 Core 测试（资料列表与启用过滤）；`--acceptance-check` 覆盖；\n  - 备会基线/转写：2 项 Core 测试（基线资料过滤、上一场转写仅在开启时附带）；`--acceptance-check` 覆盖；\n  - 文件任务队列：5 项 Core 测试（持久化、去重、顺序、更新/删除、清空）；`--acceptance-check` 覆盖；\n  - 系列变更候选：6 项 Core 测试（接受/拒绝、回执、重复审阅、候选时效、排序、从证据收集）；`--acceptance-check` 覆盖；\n  - 归档路径回写：1 项 Core 测试（系列资料写回 Markdown 路径）；`--acceptance-check` 覆盖；\n  - 会话记录模型：4 项 Core 测试（SessionData 往返、问题/译文、默认值）；`--acceptance-check` 覆盖；
    `--retrieval-check` 在真实进程里分块建索引（`qa\retrieval-0400\retrieval-check.json`）；
  - 答案服务：24 项 Core 测试（服务商表/切换计划/Key 清洗掩码/HTTP 与 URL 失败解释/时间线）；
    `--api-check` 在真实进程里验证分类与掩码（`qa\api-0390\api-check.json`）；
    `--backup-check` 用真实恢复点写入并还原到临时目录（35 字节对象、settings.json 往返一致，`qa\backup-0340\backup-check.json`）；
  - 测试：基线 79 项 + Core 779 项全部通过（`logs\test-1134.log`）；
  - RC 门禁：`scripts\run_release_checks.ps1` 对安装目录跑 17 项运行自检，1.0.0 实测 17/17 通过（`qa\rc-1000\summary.json`）；
  - 截图：`qa\shots\v05m-*.png`（五个页面）、`v05-subtitles3.png`（字幕条）、`v05-island-final.png`（提词岛）、`v06-teleprompter2.png`（提词器）、`v06-capture-picker3.png`（区域截图）、`v07-light-home2.png` / `v07-dark-home2.png`（浅色/深色，DPI 感知截图为 1984×1448）。
  - 截图 OCR：`qa\capture\shot.png` 导入资料库后 `document.json` 中 1 段中文文本（`qa\ui-data\Materials\...`）。

## 常用命令

```powershell
# 构建 + 发布 + 自检 + 安装包 + 压缩包
powershell -ExecutionPolicy Bypass -File src\NextThought\Windows\scripts\build_desktop.ps1 -Version 1.3.4

# 直接运行（开发）
src\NextThought\Windows\src\NextThought.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\NextThought.exe --demo

# 逐页截图（演示数据）
NextThought.exe --demo --section=Live|Notes|Library|Settings|Home
NextThought.exe --demo --subtitles      # 字幕条
NextThought.exe --demo --island         # 提词岛
NextThought.exe --demo --companion      # 手机伴侣（会触发防火墙询问）
NextThought.exe --demo --teleprompter    # 提词器浮窗
NextThought.exe --capture-test=D:\path\shot.png   # 区域截图与 OCR 自检
NextThought.exe --knowledge-check=D:\path\knowledge.json  # 系列知识库离线自检（不连模型）
NextThought.exe --presenter-check=D:\path\presenter.json  # 双屏演讲窗口行为自检（合成第二块屏）
NEXTTHOUGHT_RELAY_URL=http://127.0.0.1:8787 NextThought.exe --relay-check=D:\path\relay.json  # 中转端到端自检
NEXTTHOUGHT_UPDATE_FEED=http://127.0.0.1:8899/releases.json NEXTTHOUGHT_UPDATE_LAUNCH=D:\path\launch.json NextThought.exe --update-install-check=D:\path\update.json  # 应用内更新自检
NextThought.exe --section=Series --series-tab=1   # 系列页的「待确认」页签（截图用）
NextThought.exe --section=Settings --settings-scroll=1250  # 设置页滚到「双屏演讲」卡片（截图用）
```

## 已知限制（下一阶段）

1. Native AOT 仍不作为发布方式，但探针已跑通：Build Tools（MSVC 14.52）下 AOT 单文件 **41.7 MiB**，
   设置 `NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` 时启动自检 **0.53–0.65 秒**、工作集 **73.3 MiB**、
   私有 **35.7 MiB**、线程 23。音频设备枚举在 NAudio 的 COM 互操作上抛 `InvalidProgramException`
   （另有 492 条 IL 分析警告），所以 AOT 只用于运行/启动/内存探针，正式包继续自包含。
   **1.3.6 默认 ReadyToRun，并在 R2R 构建关闭分层编译**：安装版热启动 **1.2 秒**、工作集
   **108.5 MiB**、私有 43 MiB、全部 7 页约 134 MiB、130 秒静置无增长；安装包 57.2 MiB。
2. 双屏演讲已在**真实双屏**（本机 2560×1440 + 外接 2560×1440）验收：`--presenter-check` 报
   `real multi-display topology`，规划「幻灯片在显示器 2、提示在主屏」，4 个步骤全部通过；
   幻灯片屏的整屏截图→OCR→页码跟随也已接通（`--slide-follow-check`）。逐字同传的界面与门控已验证，
   真实译文需要回答服务密钥；区域截图目前显示为普通窗口。
3. Core 的 80 个 Swift 文件已全部登记为「已移植」，779 项 xunit 覆盖（含 `Workspace` 知识库、
   `KnowledgeMerger`、逐字同传门控、`PresenterLayout` 与伴侣中转加密）；平台运行时（sherpa-onnx 识别、
   Windows OCR、SQLite→文件队列）按等价实现记录。可用 Windows Swift 编译真实 Core 生成夹具做逐字节对照
   （见 `docs\DECISIONS.md` 第 3 节）。
4. 空闲内存已达标：1.3.7 ReadyToRun（关闭分层编译）安装版工作集 **108.5 MiB**、私有 43 MiB、线程 29；130 秒静置无增长（1.3.4 实测 108.1→108.0 MiB），
   托管堆只有约 15 MiB、线程 118；其余是 .NET/Avalonia/Skia 与音频原生部分。
   `--startup-check` 现在同时报告 `workingSetMiB`、`privateMiB`、`gcHeapMiB`、`threads`。
   启动时只创建当前页（7 页按需创建，`--page-check` 实测初始 108.8 MiB、访问全部后 133.9 MiB，仍在目标内）；
   试过的 GC 调参、去内嵌字体、延迟设备枚举、partial trimming 与空闲工作集裁剪都没能真正减小占用
   （裁剪还会把工作集报成 0.8 MiB，属于指标游戏，已放弃），详见 `docs\DECISIONS.md` 第 16 节。
5. 伴侣通道：局域网 HTTP 已用本机回环实测（`--companion-check`：配对 token、快照、动作、去重、无 token 403），
   中转已用本地中继 + 模拟手机验证；公网 TLS 与真实手机仍未联调。蓝牙通道在手机端取消按名字过滤前
   明确标为不可用（Windows 不能自定义 BLE 广播名）。
6. 知识库的「决定 / 待办」、`SeriesBriefing` 与系列变更候选（证据收集、接受/拒绝回执）已移植，
   含来源删除/独立保留/截止排序；`sourceDeletionTombstones` 与删除清理已实现。
7. **Authenticode 属于发布前事项，本阶段不作为阻塞条件**：本地构建的 exe 不带 Mark-of-the-Web，
   双击即可运行；只有对外分发、对方从网上下载时才需要正式 EV/OV 证书。签名链路已用自签名测试证书
   走通（能读回签名者，未受信任状态为 `UnknownError`），正式证书到位前发布包保持未签名、信任根仍是
   发布源与 SHA-256。
8. 联网搜索已接入 Tavily：设置页可关「允许联网核实」，设置 `TAVILY_API_KEY` 后，`WebSearchNeed` 判定需要时自动检索并把结果作为
   参考块交给回答（`NEXTTHOUGHT_WEB_SEARCH=off` 可关闭）；没有密钥时保持离线，界面只提示未配置。
   真实检索需要联网与密钥，本机自检只验证配置与解析。

9. **本地模型已安装并校验**：`xasr480`（X-ASR 480ms 中文/英文流式，169,289,689 字节）与
   `xasrOffline`（X-ASR 整句精修，175,026,745 字节）在 `%LOCALAPPDATA%\NextThought\models`；
   安装版 `--prepare-models-check` 复验两项均 `isInstalled=true`。另装有说话人、检索与 OCR 模型。
