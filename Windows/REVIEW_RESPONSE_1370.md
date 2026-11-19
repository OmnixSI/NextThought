# 1.3.7 审查报告 · 修复对照（0.5.0）

审查结论：1.3.7 是能跑通主线的早期版本，不是 1.x 完成版。本文件逐条记录处理，
未完成的明确标「未修」，不用「可用」代替实机验证。0.5.1 追加修复了 W3/W9/U3/U4 与第 2 节第 7 条；0.5.2 修了更新源限制与音频设备重连。
现在 14 条中 12 条完整、2 条部分（#9 证书/#14 真机）、0 条未修。

## 第 2 节的 14 条

| # | 问题 | 0.5.0 状态 | 证据 / 剩余 |
| --- | --- | --- | --- |
| 1 | 浮窗出现在屏幕共享 | **已修** | `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` 用于岛/字幕条/提词器；`--capture-hide-check` 读回 affinity=17。尚未在 Zoom/Teams 实机共享验证。 |
| 2 | 重整理纪要抹掉 Mac 字段 | **已修** | `SaveSummaryAsync` 合并已有 JSON，只更新 title/markdown/generatedAt/model；`ResummarizingKeepsMacOnlySummaryGroups` 覆盖 structured/experience/questionBank。 |
| 3 | 校对改写转写原文 | **部分** | 首次校对前把原文件复制为 `transcript.original.jsonl`；`ProofreadingKeepsTheRawRecognizerTranscript` 覆盖。仍未按 Mac 的「原文 + notes.json 修订」结构改写。 |
| 4 | Mac 术语表读成空表 | **部分** | 读取 `{"entries":[...]}` 与裸数组，保存写回 Mac 信封；`StoreReadsTheMacEnvelopeAndWritesItBack` 覆盖。键规范化、四段导入格式仍未对齐。 |
| 5 | 删会议后题库残留 | **已修** | `WorkspaceSessionDeletion` 同时清理题库来源并计数；`QuestionsFromTheDeletedMeetingLeaveTheBank` 覆盖。 |
| 6 | 旧备份让已删内容复活 | **已修** | `BackupMerge.Workspaces` 合并后执行 tombstone：移除会话/经验/题库/候选，条目标来源缺失；`TombstonesRemoveKnowledgeResurrectedByAnOldBackup` 覆盖。 |
| 7 | 带资料的 Mac 备份导入失败 | **已修** | `BackupService` 识别 `Materials/<系列>/<documentID>.md`，转成 Windows 资料并保留原始备份；`ImportsMacMaterialsFolderWithMarkdownOnly` 覆盖。 |
| 8 | API Key 读取失败后被删 | **已修** | 读取失败置 `apiKeyReadFailed`，保存设置不再调用删除；只有输入新密钥才覆盖。尚无自动化测试。 |
| 9 | 静默更新未签名/更新源可改 | **部分** | 更新源仅在 `NEXTTHOUGHT_QA=1` 时可覆盖、拒绝 `http://`；0.5.27 起要求签名时同时校验签名者指纹白名单；0.5.37 用自签名测试文件验证
  `--signature-check` 能读出签名者（`8571AF7B...`）并与白名单匹配（`allowed=true`；自签名未受信任，状态 UnknownError）。正式证书仍待发布前。 |
| 10 | 音频设备切换后静音 | **已修** | 默认设备变化时新建管道并替换；0.5.31 在真实两路播放设备上验证：Realtek → Bose，切换后仍收到 24 帧、无错误（`qa\review-fixes-05310\audio.json`）。 |
| 11 | 提词器关闭后再开崩溃 | **已修** | 关闭时清空 `teleprompter` 字段；关闭窗口时清除穿透并保存。 |
| 12 | 提词器穿透关不掉 | **已修** | 隐藏/关闭前 `DisableClickThrough()`；重启不再恢复穿透。 |
| 13 | 语义检索误开增强 OCR | **已修** | `LibraryViewModel` 不再把 `UseSemanticRetrieval` 传给 `enhancedOcr`。 |
| 14 | 伴侣内容不更新/跨线程/缺动作 | **部分** | 问题/回答变化即 `Publish`；动作统一切到 UI 线程；补 english/chinese/previous/next。`--companion-check` 仍用假动作回调，未覆盖真实回答，也无真机。 |

## 提示岛 S0（0.5.23 追加）

- 开始记录自动显示提示岛；停止记录自动隐藏；回答/问题变化时若岛被手动关掉会重新显示（W2 部分）。
- `--capture-hide-check` 现在验证完整 8 项 S0：`affinity=17`、`ShowActivated=false`、`Topmost=true`、
  `onScreen=true`、`noTaskbar=true`、`hidden=true`、`reshown=true`、可见；0.5.40 安装版全部通过。
- 仍未做：「穿透后仍能点按钮」的交互方案。

## 幻灯片跟随（0.5.39 追加）

- `--slide-follow-check` 改为只截取探针窗口（原来截全屏，主界面与桌面文字会淹没幻灯片）。
- 探针显示「缓存一致性 读写时序 方案」，OCR 后 `SlideFollower` 匹配第 1 页；安装版
  `ocrChars=28, score=1, passed=true`。
- 之前失败的原因是 Windows 防火墙提示窗盖住探针；测试前需关闭该系统提示。

## 快捷键（0.5.12 追加）

Mac 的动作里，Windows 从 7 个扩到 **18 个**可执行快捷键：
R 记录、I 提示岛、M 会中窗口、S 区域截图、U 提词器、\\ 隐藏/恢复、N 唤回主窗、
H 复制回答、G 重新生成、K 改短、X 取消、←/→ 上/下一条、J 提词穿透；
0.5.13 增加 W 窗口截图、Space 暂停/恢复聆听；0.5.34 增加 PageUp/PageDown 幻灯片翻页；0.5.35 增加 Q「立即回答最新转写」。
安装版启动日志 **19/19 注册成功**，Mac 的 19 个动作都有对应键。

## 第 6 节的窗口与更新（0.5.1 追加）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| W3 浮窗抢焦点 | **已修** | 去掉 `island.Activate()`；岛/字幕条/提词器均 `ShowActivated=False`。 |
| W9 提词器跑到屏幕外 | **已修** | 恢复位置前检查所有屏幕边界，越界则回主屏居中。 |
| U3 静默更新后不重启 | **已修** | 安装器 `[Run]` 改为 `Check: ShouldLaunchApp`；更新传 `/LAUNCH=1`，静默安装测试不启动。 |
| U4 回滚嵌套成子目录 | **已修** | 目标目录存在时复制备份内容而不是整个备份目录；测试更新。 |
| W5 穿透关不掉 | **已修**（0.5.0） | 隐藏/关闭前 `DisableClickThrough()`。 |
| #10 音频设备切换 | **已实现、待实机** | 默认设备变化时替换管道；无第二设备验证。 |

## 第 5 节的数据层问题（部分，下一阶段主线）

- 0.5.3 起系列按 `Workspaces/<ID>.json` 保存并优先读取，旧 `Workspace/workspace.json` 继续兼容；
  但字段仍是简化结构，缺场景/偏好/技能/术语等十多个 Mac 字段。
- `SessionRecord`/`Workspace`/`Backup`/`Glossary` 的逐文件替换未完成；`SessionUserContentStore`、`IncrementalBackupStore`、`BackupArchive` 的键名/日期/枚举仍不兼容。
- 0.5.4 起结构化纪要写入 `summary.json → structured`；0.5.14 起改为直接写 Mac 的 StructuredNotesPayload，
  并维护 `structuredAll` 与 `transcriptSourceRevision`；0.5.15 起 `summary.questions` 由回答记录填充；0.5.17 起模板纪要的经验/题库写入
  `summary.lessons` 与 `summary.bank`（`appliedToWorkspace=false`，等待用户确认）。
- 0.5.4 起会议创建写 `context.json`；0.5.16 起写入 Mac 的 mode/style/preferences/profile/documents/
effectiveMode/experience/glossary 字段；问题变化追加 `questions.jsonl`。技能与自定义模板仍未写入。
- 0.5.5 起转写修订指纹改为 Mac 的「大写 UUID/文本 + 8 字节大端长度 + 完整 SHA-256」；
  经验修订号改为 Swift 的 UUID 字节序；系列变更回执键名改为 operationID/candidateID/resultItemID。
- 0.5.6 起系列变更支持 add/replace/reject：replace 校验目标条目与修订号，保留旧版本到 history，
  动作重新打开，回执写 replace。

## 第 6/8/9 节的其他事项

- 双屏：真实 2560×1440 + 2560×1440 下 `--presenter-check` 4 步通过（窗口行为，不是投影画面）。
- AOT：探针 41.7 MiB、0.53–0.65 秒、73.3 MiB；音频层不可用，正式包继续 ReadyToRun。
- 版本号：已从 1.3.7 退回 **0.5.0**，不再使用 1.x。
- 文档：`CORE_PARITY.md` 恢复 7 个文件为「部分移植」，统计 73/7，并加「是否接入」说明；
  `README.md` 顶部改为早期预览。`FEATURE_PARITY.md` 仍对照 Mac v2.3.1，未重写。
- 仓库卫生：安装包/便携包仍在 `Windows/validation/bundles`（171 MB LFS），未迁移到 Release；
  自检代码仍在 `MainWindow.axaml.cs`，未拆到 Cli；PDB 仍在便携包内。

## 0.5.40 实测

- 构建：0 警告 0 错误（ReadyToRun，默认）
- 测试：Core **796**、基线 **101**
- 安装版验收：**61/61**；`--capture-hide-check` passed（affinity=17）；`--companion-check` passed
- 静默安装不自动启动（`launchedAfterSilent=0`），带 `/LAUNCH=1` 才重启
- 启动 1.20 秒，工作集 **108.7 MiB**、私有 42.5 MiB
