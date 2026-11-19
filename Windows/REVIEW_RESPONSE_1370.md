# 1.3.7 审查报告 · 修复对照（0.5.42）

审查结论：1.3.7 是能跑通主线的早期版本，不是 1.x 完成版。本文件逐条记录处理，
未完成的明确标「未修」，不用「可用」代替实机验证。0.5.1 追加修复了 W3/W9/U3/U4 与第 2 节第 7 条；0.5.2 修了更新源限制与音频设备重连。
现在 14 条中 12 条完整、2 条部分（#9 证书/#14 真机）、0 条未修。

## 第 2 节的 14 条

| # | 问题 | 当前状态 | 证据 / 剩余 |
| --- | --- | --- | --- |
| 1 | 浮窗出现在屏幕共享 | **已修** | 0.5.44 起 `--capture-hide-check` 同时验证岛、字幕条、提词器三窗，均读回 `affinity=17`。尚未在 Zoom/Teams 实机共享验证（系统 API 已生效）。 |
| 2 | 重整理纪要抹掉 Mac 字段 | **已修** | `SaveSummaryAsync` 合并已有 JSON，只更新 title/markdown/generatedAt/model；`ResummarizingKeepsMacOnlySummaryGroups` 覆盖 structured/experience/questionBank。 |
| 3 | 校对改写转写原文 | **已修** | 0.5.18 起不再改写 `transcript.jsonl`；更正写入 `user-content.json` 的 `correctionDecisions`，显示时投影，历史加载时应用。`ProofreadDecisionsProjectWithoutRewritingTheTranscript` 断言磁盘原文不变。 |
| 4 | Mac 术语表读成空表 | **已修** | 读取 `{"entries":[...]}` 与裸数组，保存写回 Mac 信封；0.5.11 按 Mac 四段文本解析；0.5.19 起 JSON 只写 term/person，扩展类型写 `sourceKind`（Mac 忽略，Windows 读回保留）。 |
| 5 | 删会议后题库残留 | **已修** | `WorkspaceSessionDeletion` 同时清理题库来源并计数；`QuestionsFromTheDeletedMeetingLeaveTheBank` 覆盖。 |
| 6 | 旧备份让已删内容复活 | **已修** | `BackupMerge.Workspaces` 合并后执行 tombstone：移除会话/经验/题库/候选，条目标来源缺失；`TombstonesRemoveKnowledgeResurrectedByAnOldBackup` 覆盖。 |
| 7 | 带资料的 Mac 备份导入失败 | **已修** | `BackupService` 识别 `Materials/<系列>/<documentID>.md`，转成 Windows 资料并保留原始备份；`ImportsMacMaterialsFolderWithMarkdownOnly` 覆盖。 |
| 8 | API Key 读取失败后被删 | **已修** | 读取失败置 `apiKeyReadFailed`，保存设置不再调用删除；0.5.43 新增 `CorruptApiSecretSurvivesSavingOtherSettings`，断言损坏密钥在保存其他设置后仍原样保留。 |
| 9 | 静默更新未签名/更新源可改 | **部分** | 更新源仅在 `NEXTTHOUGHT_QA=1` 时可覆盖、拒绝 `http://`；0.5.27 起要求签名时同时校验签名者指纹白名单；0.5.37 用自签名测试文件验证
  `--signature-check` 能读出签名者（`8571AF7B...`）并与白名单匹配（`allowed=true`；自签名未受信任，状态 UnknownError）。正式证书仍待发布前。 |
| 10 | 音频设备切换后静音 | **已修** | 默认设备变化时新建管道并替换；0.5.31 在真实两路播放设备上验证：Realtek → Bose，切换后仍收到 24 帧、无错误（`qa\review-fixes-05310\audio.json`）。 |
| 11 | 提词器关闭后再开崩溃 | **已修** | 关闭时清空 `teleprompter` 字段；关闭窗口时清除穿透并保存。 |
| 12 | 提词器穿透关不掉 | **已修** | 隐藏/关闭前 `DisableClickThrough()`；重启不再恢复穿透。 |
| 13 | 语义检索误开增强 OCR | **已修** | `LibraryViewModel` 不再把 `UseSemanticRetrieval` 传给 `enhancedOcr`。 |
| 14 | 伴侣内容不更新/跨线程/缺动作 | **部分** | 问题/回答变化即 `Publish`；动作切 UI 线程并补 english/chinese/previous/next。0.5.32 起自检绑定实际网络地址，0.5.33 起走真实 `HandleCompanionActionAsync`，0.5.36 起通过 Mac 的 AES-GCM 固定向量，0.5.38 起中转只接受 HTTPS。0.5.73 已在 Chromium 中通过 CDP 读取真实页面 DOM，确认问题/回答渲染、快照更新、点击动作回传；本机中转协议也已跑通；仍缺真实手机与公网 TLS 中转。 |

## 提示岛 S0（0.5.23 追加）

- 开始记录自动显示提示岛；停止记录自动隐藏；回答/问题变化时若岛被手动关掉会重新显示（W2 部分）。
- `--capture-hide-check` 现在验证完整 8 项 S0：`affinity=17`、`ShowActivated=false`、`Topmost=true`、
  `onScreen=true`、`noTaskbar=true`、`hidden=true`、`reshown=true`、可见；0.5.40 安装版全部通过。
- 仍未做：「穿透后仍能点按钮」的交互方案。

## 提词器穿透控制（0.5.42 追加）

- 穿透开启时显示一个独立的「提词器已穿透 / 恢复点击」小窗；点击即可退出穿透，
  不再只能靠快捷键。
- `--teleprompter-handle-check` 验证：穿透时控制条可见、恢复后隐藏；安装版通过。

## 幻灯片跟随（0.5.39 追加）

- `--slide-follow-check` 改为只截取探针窗口（原来截全屏，主界面与桌面文字会淹没幻灯片）。
- 探针显示「缓存一致性 读写时序 方案」，OCR 后 `SlideFollower` 匹配第 1 页；安装版
  `ocrChars=28, score=1, passed=true`。
- 之前失败的原因是 Windows 防火墙提示窗盖住探针；测试前需关闭该系统提示。
- 0.5.41 修正 `WholeMaterialOf`：PPTX 块按「幻灯片 N」标记，不再统一写成 `p.N`；
  已移植 Mac `WholeMaterialTests` 的 deck/page 断言。

## 快捷键（0.5.12 追加）

Mac 的动作里，Windows 从 7 个扩到 **19 个**可执行快捷键：
R 记录、I 提示岛、M 会中窗口、S 区域截图、U 提词器、\\ 隐藏/恢复、N 唤回主窗、
H 复制回答、G 重新生成、K 改短、X 取消、←/→ 上/下一条、J 提词穿透；
0.5.13 增加 W 窗口截图、Space 暂停/恢复聆听；0.5.34 增加 PageUp/PageDown 幻灯片翻页；0.5.35 增加 Q「立即回答最新转写」。
安装版启动日志 **19/19 注册成功**；0.5.87 起键位可在设置页改，0.5.88 起修饰键组合也可改，保存后立即重新注册，Mac 的 19 个动作都有对应键。

## 场景准备（0.5.52 追加）

- `MeetingPlan.PreparedWorkspace` 现在真正应用本次会议的选择：模式、custom 类型/基准模式、
  风格、偏好（只有旧类型的默认偏好会被替换，显式要求保留）。
- 材料携带语义按 Mac 修正：`Materials=false` 只删除准备基线里的资料，准备开始后新增的
  文件仍保留；`Transcript` 仍只在显式勾选时附加上一场转写。
- 移植 Mac `MeetingPreparationTests` 5 个测试；另有携带语义测试。

## 第 6 节的窗口与更新（0.5.1 追加）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| W3 浮窗抢焦点 | **已修** | 去掉 `island.Activate()`；岛/字幕条/提词器均 `ShowActivated=False`。 |
| W9 提词器跑到屏幕外 | **已修** | 恢复位置前检查所有屏幕边界，越界则回主屏居中。 |
| U3 静默更新后不重启 | **已修** | 安装器 `[Run]` 改为 `Check: ShouldLaunchApp`；更新传 `/LAUNCH=1`，静默安装测试不启动。 |
| U4 回滚嵌套成子目录 | **已修** | 目标目录存在时复制备份内容而不是整个备份目录；测试更新。 |
| W5 穿透关不掉 | **已修**（0.5.0） | 隐藏/关闭前 `DisableClickThrough()`。 |
| #10 音频设备切换 | **已修** | 0.5.31 实机验证 Realtek → Bose，切换后 24 帧、无错误。 |

## 第 5 节的数据层问题（部分，下一阶段主线）

- 0.5.3 起系列按 `Workspaces/<ID>.json` 保存并优先读取，旧 `Workspace/workspace.json` 继续兼容；
  0.5.28/0.5.46 已补 `skillIDs`、`mode`、`style`、`preferences`、`notes`、`customTypeName`、
  `customBaseMode`、`glossary`、`archived`、`fields`、`standing` 等 Mac 字段；
  Workspace 的已知字段已全部保留。
- 0.5.53 修正 DocumentJob 的 Mac 键名（`workspaceID` / `waitingReason` / `documentID` / `localModelID`）。
- 0.5.54 修正系列变更候选的 `sourceSessionID` / `evidenceID` 键名。
- 0.5.55/0.5.56 修正屏幕上下文的 `frameID`/`targetID`、删除日志的 `transactionID`/`workspaceID`；
  0.5.57 伴侣快照补齐 `answerID`/`phase`/`slide`/`updateAvailable`；
  0.5.58 证据包改为 Mac 的 `requestID`；0.5.59 更正 `lineID` 与 `templateID`；0.5.60 回答事件补 `seconds`/`firstTokenSeconds`/
  `screenshotPath`/`webAnswer`/`webSources`/`evidence`；0.5.61 上下文文档保留 Mac `content`；0.5.62 摘录改为 `chunkID`/`blockID`；0.5.63 保留系列条目的 `seriesItemSourceTombstones`；0.5.64 保留 `stageReview`/`stageReviewAt`；0.5.65 会议记录保留未知 Mac 字段；0.5.66 系列也保留未知字段；0.5.67 `context.json` 同样保留；0.5.68 纪要补 `corrections`/`droppedCount`/`model`/生成时间/转写修订；
  0.5.69 `context.json` 的上一场改为 `previousSessionID`；0.5.70 条目历史保留 Mac 溯源字段；0.5.71 转写行保留译文与修订及未知字段；0.5.72 回答记录补首字延迟/截图/联网/证据；0.5.73 用 Chromium+CDP 实测手机页渲染；0.5.74 用本机 WebSocket 中转实测收状态/发命令；0.5.75 问题事件保留未知 Mac 字段；0.5.76 修复 `StructuredNotesPayload` 无法反序列化并保留未知字段；0.5.77 来源引用保留未知字段；0.5.78 Chromium 手机页点击「上一条」触发桌面动作；0.5.79 手机页收到新快照后自动更新；0.5.80 增加 Word（DOCX）导出；0.5.81 增加 PDF 导出；0.5.82 改用 SimHei 文件嵌入，单份约 5 MB；0.5.83 字幕可导出原文/译文/双语；0.5.84 术语表提示词上限 1500 字并支持英文版；0.5.85 九个内置纪要模板补齐英文版；0.5.86 结构化纪要也按英文转写使用英文提示；0.5.87 全局快捷键可改键；0.5.88 支持 Ctrl/Alt/Shift/Win 组合（如 Ctrl+Shift+R）；0.5.89 复验 `--demo` 演示模式不写数据；0.5.90 PDF 增加页码与标题元数据；0.5.91 补屏幕变化检测；0.5.92 补屏幕指代/新鲜度与通用科学回答提示的断言；0.5.93 补低相似度不返材料、显式页码优先的测试；0.5.94 用红色探针窗实测窗口截图裁剪；0.5.95 补提示条分句边界测试；0.5.96 提示岛/提词器加 160ms 淡入；0.5.97 隐藏时 120ms 淡出；0.5.98 界面语言机制落地；0.5.99 英文词典扩到 140 条；0.5.100 扩到 175 条并覆盖长说明；0.5.101 保存设置后即时切换；0.5.102 英文词典扩到 194 条；0.5.103 状态栏提示也走英文词典；0.5.104 输入框/下拉占位文字翻译；0.5.106 窗口标题、托盘菜单翻译；0.5.107 页面副标题中英切换；0.5.108 统计行与问候语中英切换；0.5.109 英文词典补到 273 条；0.5.110 动态计数也中英切换；0.5.111 浮窗打开时翻译；0.5.112 英文日期格式；0.5.113 下拉选项英文化；0.5.114 屏选择英文化；0.5.115 设置页搜索；0.5.116 多家接口预设；0.5.117 外观设置；0.5.118 动态状态英文化；0.5.119 默认会议名/空状态英文化；0.5.120 计算标签英文化；0.5.121 资料库计数；0.5.122 语言切换后重建列表；0.5.123 云端 STT WAV 客户端与本机 WebSocket 协议测试。
  备份清单的会话筛选改为普通循环，规避一次 ReadyToRun 下的 CLR 内部错误。
- `SessionRecord`/`Workspace`/`Backup`/`Glossary` 的逐文件替换未完成；`SessionUserContentStore`、`IncrementalBackupStore`、`BackupArchive` 的键名/日期/枚举仍不兼容。
- 0.5.4 起结构化纪要写入 `summary.json → structured`；0.5.14 起改为直接写 Mac 的 StructuredNotesPayload，
  并维护 `structuredAll` 与 `transcriptSourceRevision`；0.5.15 起 `summary.questions` 由回答记录填充；0.5.17 起模板纪要的经验/题库写入
  `summary.lessons` 与 `summary.bank`（`appliedToWorkspace=false`，等待用户确认）。
- 0.5.4 起会议创建写 `context.json`；0.5.16 起写入 Mac 的 mode/style/preferences/profile/documents/
effectiveMode/experience/glossary 字段；问题变化追加 `questions.jsonl`。
- 0.5.21–0.5.26 技能编辑/导入/启停；0.5.24–0.5.25 自定义模板导入/文本编辑；0.5.29 系列附着技能；
  0.5.30 备份 `Workspaces/<UUID 大写>.json`。
- 0.5.5 起转写修订指纹改为 Mac 的「大写 UUID/文本 + 8 字节大端长度 + 完整 SHA-256」；
  经验修订号改为 Swift 的 UUID 字节序；系列变更回执键名改为 operationID/candidateID/resultItemID。
- 0.5.6 起系列变更支持 add/replace/reject：replace 校验目标条目与修订号，保留旧版本到 history，
  动作重新打开，回执写 replace。

## 其他修正（0.5.83 追加）

- 字幕导出不再固定「原文 + 译文」：Notes 页可选原文、译文或双语，分别只写对应内容；
  Mac 的 `SessionExport` 语言选择已对齐。
- 英文内置纪要模板：九个场景全部有英文版，章节 ID/类型/携带设置与中文一致；
  转写被判定为英文时自动用英文模板与英文提示；结构化纪要（鱼骨/大纲/观点矩阵）同样切换英文 header 与 `language:en`。
- 术语表提示词：按 Mac `promptSection` 排序（有误听写法的先列）、截断、1500 字上限，
  并提供英文 header/“人名”标注；不再无限挤占提示词。

## 界面本地化（0.5.98，机制已落地）

- 设置页「界面语言 / Language」可选中文/English，写 `settings.json` 的 `language`；
- 启动后把英文词典应用到已实现控件：主导航 7 项、设置分节、主要按钮与常用文案/长说明共 273 条；
- 动态会议标题/转写/状态不翻译，未知文本原样保留；`--ui-language-check` 实测
  Home/Meetings/Notes/History/Series/Library/Settings 全部出现，反向切回「首页/设置」也通过。
- 仍未覆盖全部约 500 条界面文案，属于机制先行。

## 窗口截图（0.5.94 实测）

- `--window-shot-check` 弹一个纯红探针窗，取前台窗口矩形、截整屏并裁剪；
  中心像素实测 **R=255/G=0/B=0**，尺寸 376×279，证明「窗口截图」不是空实现。

## 演示模式（0.5.89 复验）

- `--demo` 载入内置样例会议（10 行转写、3 场历史、问题/回答/纪要），`--demo-check` 实测
  `lines=10, history=3, sessionFiles=0`，不写 `Sessions`。

## 第 6/8/9 节的其他事项

- 双屏：真实 2560×1440 + 2560×1440 下 `--presenter-check` 4 步通过（窗口行为，不是投影画面）。
- AOT：探针 41.7 MiB、0.53–0.65 秒、73.3 MiB；音频层不可用，正式包继续 ReadyToRun。
- 版本号：已从 1.3.7 退回 **0.5.0**，不再使用 1.x。
- 文档：`CORE_PARITY.md` 恢复 7 个文件为「部分移植」，统计 73/7，并加「是否接入」说明；
  `README.md` 顶部改为早期预览。`FEATURE_PARITY.md` 仍对照 Mac v2.3.1，未重写。
- 仓库卫生：安装包/便携包仍在 `Windows/validation/bundles`（171 MB LFS），未迁移到 Release；
  自检代码仍在 `MainWindow.axaml.cs`，未拆到 Cli；PDB 仍在便携包内。

## 0.5.129 实测

- 构建：0 警告 0 错误（ReadyToRun，默认）
- 测试：Core **863**、基线 **114**
- 安装版验收：**63/63**；`--capture-hide-check` passed（岛/字幕条/提词器 affinity=17）；
  `--companion-check` passed；手机页 Chromium 渲染与本机中转协议测试通过
- 静默安装不自动启动（`launchedAfterSilent=0`），带 `/LAUNCH=1` 才重启
- 启动 4.28 秒、工作集 **124.3 MiB**、私有 56.5 MiB（环境负载）
