
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
