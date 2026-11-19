# 1.3.7 审查报告 · 修复对照（0.5.0）

审查结论：1.3.7 是能跑通主线的早期版本，不是 1.x 完成版。本文件逐条记录处理，
未完成的明确标「未修」，不用「可用」代替实机验证。0.5.1 追加修复了 W3/W9/U3/U4 与第 2 节
第 7 条：现在 14 条中 9 条完整、3 条部分、2 条未修（#9 签名/#10 音频切换）。

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
| 9 | 静默更新未签名/更新源可改 | **未修** | 按用户要求签名属发布前事项；`NEXTTHOUGHT_UPDATE_FEED` 仍可覆盖，签名者未校验。 |
| 10 | 音频设备切换后静音 | **未修** | `AudioCaptureService` 的默认设备变化回调仍是空函数。 |
| 11 | 提词器关闭后再开崩溃 | **已修** | 关闭时清空 `teleprompter` 字段；关闭窗口时清除穿透并保存。 |
| 12 | 提词器穿透关不掉 | **已修** | 隐藏/关闭前 `DisableClickThrough()`；重启不再恢复穿透。 |
| 13 | 语义检索误开增强 OCR | **已修** | `LibraryViewModel` 不再把 `UseSemanticRetrieval` 传给 `enhancedOcr`。 |
| 14 | 伴侣内容不更新/跨线程/缺动作 | **部分** | 问题/回答变化即 `Publish`；动作统一切到 UI 线程；补 english/chinese/previous/next。`--companion-check` 仍用假动作回调，未覆盖真实回答，也无真机。 |

## 第 6 节的窗口与更新（0.5.1 追加）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| W3 浮窗抢焦点 | **已修** | 去掉 `island.Activate()`；岛/字幕条/提词器均 `ShowActivated=False`。 |
| W9 提词器跑到屏幕外 | **已修** | 恢复位置前检查所有屏幕边界，越界则回主屏居中。 |
| U3 静默更新后不重启 | **已修** | 安装器 `[Run]` 改为 `Check: ShouldLaunchApp`；更新传 `/LAUNCH=1`，静默安装测试不启动。 |
| U4 回滚嵌套成子目录 | **已修** | 目标目录存在时复制备份内容而不是整个备份目录；测试更新。 |
| W5 穿透关不掉 | **已修**（0.5.0） | 隐藏/关闭前 `DisableClickThrough()`。 |
| #10 音频设备切换 | **未修** | 默认设备回调仍为空。 |

## 第 5 节的数据层问题（未修，下一阶段主线）

- Workspace 仍是单个 `workspace.json` 自定结构，不是 Mac 的 `Workspaces/<ID>.json`，缺场景/偏好/技能/术语等字段。
- `SessionRecord`/`Workspace`/`Backup`/`Glossary` 的逐文件替换未完成；`SessionUserContentStore`、`IncrementalBackupStore`、`BackupArchive` 的键名/日期/枚举仍不兼容。
- 结构化纪要仍分文件存放，未读写 Mac 的 `summary.json → structured`。
- 会议只写核心文件；`questions.jsonl`、`context.json`、技能与自定义模板未写入。
- 转写修订指纹、经验修订号、系列变更回执键名/枚举仍与 Mac 不同。

## 第 6/8/9 节的其他事项

- 双屏：真实 2560×1440 + 2560×1440 下 `--presenter-check` 4 步通过（窗口行为，不是投影画面）。
- AOT：探针 41.7 MiB、0.53–0.65 秒、73.3 MiB；音频层不可用，正式包继续 ReadyToRun。
- 版本号：已从 1.3.7 退回 **0.5.0**，不再使用 1.x。
- 文档：`CORE_PARITY.md` 恢复 7 个文件为「部分移植」，统计 73/7，并加「是否接入」说明；
  `README.md` 顶部改为早期预览。`FEATURE_PARITY.md` 仍对照 Mac v2.3.1，未重写。
- 仓库卫生：安装包/便携包仍在 `Windows/validation/bundles`（171 MB LFS），未迁移到 Release；
  自检代码仍在 `MainWindow.axaml.cs`，未拆到 Cli；PDB 仍在便携包内。

## 0.5.1 实测

- 构建：0 警告 0 错误（ReadyToRun，默认）
- 测试：Core **782**、基线 **86**
- 安装版验收：**61/61**；`--capture-hide-check` passed（affinity=17）；`--companion-check` passed
- 静默安装不自动启动（`launchedAfterSilent=0`），带 `/LAUNCH=1` 才重启
- 启动 1.21 秒，工作集 **109.3 MiB**、私有 42.9 MiB
