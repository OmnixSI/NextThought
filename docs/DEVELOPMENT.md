# 开发与发布

## 维护文档

本文件是随源码提交到 GitHub 的通用开发、构建和发布说明。机器专属的正式版与测试版工作区、应用数据位置及本机授权状态记录在 `.local/LOCAL-PROJECT-MANAGEMENT.md`；该文件由 `.gitignore` 排除，只存在于维护者的本机，不应提交。新克隆仓库或新建 Git worktree 时没有这份本机文件属于正常情况。

## 项目结构

```text
Sources/
  InterviewHackerCore/   问题意图与候选、VAD、流式协议解析、云端识别消息、资料检索、Prompt、手机伴侣
  InterviewHacker/       SwiftUI 应用、音频采集、Zipformer / Apple / 云端识别、回答引擎、截图与快捷键
Tests/InterviewHackerCoreTests/   Swift Testing 单元测试与可选的本机 Zipformer 集成测试
Resources/               Info.plist、entitlements、第三方声明
scripts/                 build-app.sh（构建+签名+打包）、sign-app.sh、make-icon.swift
Tools/Validation/        用于识别验证的音频样本、合成音频播放器和题图工具
docs/                    开发文档、验证记录、发布说明与截图
```

## 常用命令

Zipformer 权重来源、具体校验值与本机实际识别结果见[迁移和验证记录](ZIPFORMER-MIGRATION.md)。

```sh
swift test --disable-sandbox
swift build --disable-sandbox
./scripts/build-app.sh
INTERVIEWHACKER_MAKE_DMG=1 ./scripts/build-app.sh
```

构建脚本会将 SwiftPM 所选的静态 Sherpa-ONNX 和 ONNX Runtime 框架链接进应用，不要求运行时安装 Python、Homebrew 或外部服务。第一次使用本机识别时，应用从官方模型仓库下载完整的 Zipformer 文件组到 `~/Library/Application Support/InterviewHacker/models/`。

若要运行使用真实模型文件的集成测试：

```sh
IH_ZIPFORMER_MODEL_DIR="$HOME/Library/Application Support/InterviewHacker/models/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20" \
  swift test --disable-sandbox --filter ZipformerStreamingIntegrationTests
```

没有设置该变量时，这项本机模型集成测试会直接返回；其他测试不要求下载模型或联网。

## 截图与演示模式

`INTERVIEWHACKER_DEMO=1` 启动时使用示例会话，不采集音频、不调用识别或回答模型。可选设置 `INTERVIEWHACKER_DEMO_SECTION=home|prepare|live|live-notes|meetings|library|settings`（旧名 assistant / materials / history 仍可用）、`INTERVIEWHACKER_DEMO_APPEARANCE=light|dark`。`.github/workflows/screenshots.yml` 在 macOS runner 上生成 README 截图。

## 发布

`.github/workflows/release.yml` 在推送 `v*` 标签或手动执行时运行测试、构建并发布资产。`.github/workflows/verify.yml` 会在指定分支的源码推送时运行验证。执行机器由仓库变量 `MAC_RUNNER` 决定：值为 `github` 时使用 GitHub 托管的 `macos-latest`；其他值使用维护者自己的 Apple Silicon runner。

| 操作 | 结果 |
| --- | --- |
| 推送源码到 `main` | 触发 Verify；文档及 Markdown-only 改动按工作流规则跳过 |
| 推送 `v*` 版本标签 | 触发 Release：运行验证、构建正式资产并发布 Release |
| 手动执行 Release 工作流 | 按输入标签运行同一发布流程 |
| 手动执行 Screenshots，或推送含 `[screenshots]` 的提交 | 在配置的 macOS runner 上生成演示截图 |

Release 工作流会用 `--clobber` 上传并替换同名资产。要求使用本机生成的正式构建时，先确认发布操作不会触发远端构建或覆盖本机资产；若当前工作流会这样做，应在本机完成构建和核验后暂停发布，不能把云端生成物当成本机构件，也不要自行修改 Actions 配置或绕过仓库保护。

> 私有仓库的 GitHub 托管 macOS runner 按 10 倍计费分钟数。

### 签名与隐私权限

- CI 默认使用临时 ad-hoc 签名，没有 Apple 账号要求。该签名每个版本都不同，因此 macOS 可能要求重新授予隐私权限。
- 本机 `scripts/build-app.sh` 使用应用设置中绑定的 **InterviewHacker Local Signing** 身份（`~/Library/Application Support/InterviewHacker/Signing/`）。首次安装且从未绑定过时，才通过应用设置完成一次绑定。若该目录已存在但身份不完整或不可用，停止构建并恢复原身份；不要再次绑定或创建新证书。麦克风、录屏与系统音频权限依赖稳定的 Bundle ID 和签名要求。
- 本机 `scripts/install-latest.sh` 会先用同一证书重签下载包，并比较它与当前安装包的 designated requirement；身份不一致时停止安装。本机脚本拒绝 ad-hoc 和其他证书，只有 GitHub Actions 发布流程可使用独立发布身份。
- GitHub Releases 可运行 `scripts/setup-release-signing.sh`，把独立的固定发布证书写入 Actions Secrets。该密钥与本机 Local Signing 身份分开，本机私钥不会上传到 CI。配置 Developer ID 和公证凭据后，release workflow 会使用 hardened runtime 与 `Resources/InterviewHacker.entitlements` 公证并装订。
- Bundle ID 保持为 `com.interviewhacker.app`。构建只签应用主程序；Sherpa-ONNX 和 ONNX Runtime 是静态链接，不生成需要单独签名的独立助手进程或动态运行库。

#### 本机身份文件与恢复

- 正式版 app-bound 身份的唯一配置目录是 `~/Library/Application Support/InterviewHacker/Signing/`。其中 `certificate.pem` 是证书，`identity-sha1` 记录该证书的身份值，`signing.keychain-db` 保存可用于签名的私钥，`keychain-password` 是解锁这个专用钥匙串的密码。私钥不以独立 PEM 文件保存在此目录。
- 保留这组原始文件及其权限，不要拆分、清空、轮换、删除或覆盖。该目录属于本机密钥材料：不得加入 Git、应用包、ZIP、GitHub Release 或 Actions Secrets。需要备份时，备份整组文件到访问受控的加密位置，并验证备份可恢复。
- `keychain-password` 是本机生成并以仅所有者可读方式保存的专用钥匙串密码；正常构建脚本会在进程内读取并用于解锁。不要 `cat`、打印、复制到聊天或日志，也不要写入 shell 历史。若系统需要交互式认证，让用户在 macOS 提示中自行输入。
- 构建前验证顺序：确认四个文件都存在且可读；用 `openssl x509 -in "$HOME/Library/Application Support/InterviewHacker/Signing/certificate.pem" -noout -fingerprint -sha1` 核对证书指纹与 `identity-sha1` 一致；使用 `security unlock-keychain` 解锁明确路径的 `signing.keychain-db`；再用 `security find-identity -v -p codesigning "$HOME/Library/Application Support/InterviewHacker/Signing/signing.keychain-db"` 确认该钥匙串里存在匹配的有效证书和私钥。证书文件指纹匹配只校验公钥证书与记录值，不能证明私钥存在或可用。
- `scripts/sign-app.sh` 是本机正式版构建的签名入口：它从上述目录读取身份，检查证书、私钥和 Bundle ID，使用该身份签名并验证稳定的 designated requirement。身份缺失、钥匙串解锁失败、有效身份数为零、指纹不匹配或 designated requirement 不符时，构建必须停止。保留完整 stderr 和退出状态；不要仅凭数字猜测错误含义。
- 不要用 `scripts/setup-local-signing.sh` 修复 app-bound 身份。该脚本操作仓库内的 `.local-signing/`，不是 `scripts/sign-app.sh` 使用的 Application Support 目录；在缺少文件时它会新建另一套身份。`.local-signing/` 也可能供分开的发布签名流程使用，不要在两处之间复制、替换或混用钥匙串。
- 应用的 `LocalSigning.ensureIdentity()` 用于首次绑定；当前实现发现身份未绑定时会尝试删除整个签名目录后重新生成。因此，只要上述目录存在任何身份文件而校验失败，就不得通过设置页“绑定到这台 Mac”、`ensureIdentity()` 或初始化脚本修复。先保全当前文件并从可信备份恢复同一钥匙串和私钥；找不到可验证的原身份时停止并报告，不生成替代证书。
- `security show-keychain-info` 的参数按本机 `man security` 核对。钥匙串命令失败时记录完整错误文本并区分 CLI 退出码与 Security framework OSStatus；即使 `security error <code>` 显示未知，也不要自行推断身份已损坏或创建新身份。
- 正式版与测试版 Bundle ID / designated requirement 必须各自保持稳定。当前测试工作区的测试签名逻辑复用正式版的 Application Support 签名目录：权限条目因 Bundle ID 不同而分开，但证书身份并未做到独立。不要把这种状态描述为证书级隔离，也不要为“修复权限”擅自生成测试证书。

### 本地构建

目标平台为 Apple Silicon arm64、macOS 14 或更新版本。源码构建需要 Swift 6 和 Apple Command Line Tools，不需要 CMake。构建会生成：

- `dist/InterviewHacker.app`
- `dist/InterviewHacker-macOS.zip`
- 可选的 `dist/InterviewHacker.dmg`

应用内首次模型下载与校验之后，识别离线运行。第三方 runtime 和模型的版本、来源、许可及模型文件校验值见 `Resources/ThirdPartyNotices.txt`。

## 语音识别实现

默认引擎是 Sherpa-ONNX 1.13.8 C API，固定 SwiftPM 依赖到 `v1.13.8`；该包把 ONNX Runtime 1.28.2 的静态 macOS 框架一起链接。

**两遍识别**（`ZipformerStreamingTranscriber` + `ZipformerRuntime` / `OfflineSegmentRecognizer`）：

1. 流式：X-ASR zh-en 流式 Zipformer（默认 480 ms 块，可选 160 ms），边说边给出带标点的临时文字。系统声音和麦克风各建独立 online stream，共享只读权重。
2. 精修：`VoiceActivitySegmenter` 以 700 ms 句尾静音提交一句时，流式流补 0.4 s 静音后定稿，同时把这一句的 PCM 交给 X-ASR 离线 Zipformer 整句重识别，结果作为最终文字（失败或为空时回退到流式结果）。分句超过 12 s 时 VAD 重复的半秒重叠会按样本序号去掉。

模型来自 sherpa-onnx `asr-models` 发布（`LocalASRPackage`），下载 `.tar.bz2` 后校验整包 SHA-256，用系统 `tar` 解压，只保留所需文件并按大小校验（`installed.json` 标记）。X-ASR 包里只有 `bpe.model`，热词需要的 `bpe.vocab`（piece + score）由 `sentencepiece` 导出后内嵌在 `XASRVocabulary.swift` 中，安装时写到模型目录。旧版 `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20` 仍可在设置里选择，按原来的逐文件方式下载。

选型与实测数据见 [ASR-MODELS-2026-10.md](ASR-MODELS-2026-10.md)。

## 问题识别

`QuestionIntentClassifier`（规则打分）+ `QuestionCandidateTracker`（流式候选调度），都在 Core，可在 Linux 上单测：

- 先去掉识别结果开头的口头禅，再按问号、疑问词、语气词、倒装、请求句式、附加问句、选择问句等证据打分，减去自述、自问自答、设备确认等反证。
- 同一句流式修订只更新一个候选；以问号或“吗 / 呢”结尾、或证据很强时 0.6 s 稳定即触发，否则 1.2 s 或等定稿。
- 提前触发后最终文字明显变长时，返回 `supersedes` 指向旧候选，应用取消旧回答改答完整问题。
- 问题前 8 s 内同一方的陈述句作为 `leadIn` 一起交给模型。
- `QuestionEvalCorpus`（Core，含留出集与扩展集）是贴近识别输出的标注语料，`QuestionDetectionEvaluator` 报告召回与误触发。

## 屏幕感知与讲稿跟随

`LiveInsights`（Core：`TextSimilarity`、`ScreenText`、`SlideMatcher`、`SlideNotes`、`AnswerPoints`）和应用里的 `AppState+Insights`：屏幕感知（ScreenCaptureKit 截主显示器并排除本应用窗口，Vision 识别文字，按和最近对话的文字重合判断相关性）、讲稿跟随、回答进度。界面在 `InsightsPanel.swift`（模块“屏幕与讲稿”），设置在 `InsightSettingsView.swift`（设置 → 识别）。

## 回声与抢话

`CrossTalkMonitor`（Core）比较两路 20 ms 帧的对数响度包络，在 −0.3…+0.5 s 延迟内找最大相关；1.6 s 窗口内相关 ≥ 0.7 视为麦克风里是扬声器回声。`EchoMonitor` 在两个音频队列上加锁调用它并提供音量表数据。麦克风定稿文字若与对方近 12 s 内的话大部分重合（`EchoTextFilter`）同样丢弃。`MicrophoneCaptureService.voiceProcessing` 可选开启 Apple 语音处理。

## 会话记录

`SessionArchive` 把每次会话写到 `~/Library/Application Support/InterviewHacker/Sessions/<id>/`：`meta.json`、`context.json`、`transcript.jsonl`、`translations.jsonl`、`questions.jsonl`、`answers.jsonl`、`summary.json`。JSONL 边发生边追加，最后一行写坏时读取会跳过。会后摘要由 `AppState.generateSummary` 用独立的 API 客户端或一次性 `codex exec` 生成（不占用常驻服务），超过 6 万字的转录先分段整理再合并（`SessionSummaryPrompt`），`SessionSummaryParser` 提取标题与问题清单。

## 会中同声传译

`LiveTranslationCoordinator` 根据每段识别文本自动判断中英文并翻译为另一种语言，系统音频与麦克风两路都可双向翻译；语言判断在本机完成，不增加语音识别模型或网络调用。部分识别结果限频合并，最终译文优先提交，并按转写行 ID 写入 `translations.jsonl`，与原始转写分开保存；自动整理仍只读取原文。翻译仅在用户点击会中“同声传译”后启动，两个方向的系统语言资源并行准备；该入口需要 macOS 15 或更新版本。

## 场景（Workspace）与模式

`Workspace`（Core）是一个场景：模式、结构化字段（`ModeProfile.fields`）、分类资料、回答要求、背景、经验库（`WorkspaceLesson`）、题库（`QuestionBankItem`）和阶段复盘。`WorkspaceStore` 每个场景存一个 JSON（`Workspaces/<id>.json`，原子写入、0.6 s 合并保存）；读不了的文件改名为 `.unreadable` 保留。`AppState.sessionMode / documents / answerPreferences / profile` 是当前场景的视图，旧代码路径不变。回答提示词里 `user_material` 来自 `Workspace.materialText`，`experience_notes` 来自经验库，题库作为一份“题库”资料参与检索。

复盘：`SessionSummaryPrompt.system(for:)` 按模式加入复盘重点，并要求输出“经验沉淀”“题库更新”；`SessionSummaryParser.parseAll` 解析后由 `KnowledgeMerger` 合并（相似问题按归一化后的二元组相似度去重并更新答案）。`StageReviewPrompt` 汇总一个场景下所有带摘要的会话。

## 数据与备份

全部数据在 `AppPaths.root`（`INTERVIEWHACKER_DATA_DIR` 可重定向）。`DataBackup` 导出 `.ihbackup`（ZIP：`manifest.json`、`settings.plist`、可选 `secrets.plist`、`Workspaces/`、`Sessions/`）；导入用 `BackupMerge` 合并，从不删除。自动备份：每天一次、安装更新前、导入前，保留 10 份。

## 自检（self-test）

`InterviewHacker --self-test data signing update asr --fixtures Tools/Validation/Fixtures` 在应用包内运行真实代码路径（需设置 `INTERVIEWHACKER_DATA_DIR` 指向临时目录）。Verify 工作流的 `selftest` 任务逐项运行；Release 工作流在打包后运行 `data update`。

## 自动更新与本机签名

- 发布工作流为每个 Release 上传 `update.json`（tag、版本、构建号 = run number、ZIP 名与 SHA-256）。
- `Updater` 通过 GitHub API 读取 Releases（私有仓库用钥匙串里的只读令牌，或 `gh auth token`），比较构建号，后台下载 ZIP（重定向到存储时去掉 Authorization），校验 SHA-256、`codesign --verify` 并比较指定要求（designated requirement）。安装时写一个辅助脚本：等应用退出、替换 `.app`、去掉隔离属性、重新打开。
- `LocalSigning` 用系统 `openssl` 生成自签名代码签名证书，导入专用钥匙串（`Application Support/InterviewHacker/Signing`），以 `identifier "com.interviewhacker.app" and certificate leaf = H"…"` 作为指定要求重新签名。绑定后，下载的新版本若签名不同会先用本机证书重新签名，TCC 权限因此保持。

## 音频角色与回答模型

- 系统声音与麦克风分别采集、转换和流式解码，分别标记“对方”和“我”。麦克风首次使用请求系统授权；暂停、恢复、停止同时作用于两路。
- 转录、导出和发给 Codex 的上下文保留角色；自动问题检测只看系统声音，不做声纹识别，也不区分系统声音中的多位说话者。不戴耳机时扬声器回声由上面的回声检测处理，戴耳机仍是最干净的方式。
- 回答引擎与模型设置独立于识别模型；Codex 服务失败时显示错误，不静默切换其他回答模型。

## 构建机

Verify、Release 和 Screenshots 都按仓库变量 `MAC_RUNNER` 选择执行机器。设置维护者自托管 runner 的方法见 [SELF-HOSTED-RUNNER.md](SELF-HOSTED-RUNNER.md)。云端和截图工作流构建的临时 CI 应用使用独立 Bundle ID `com.interviewhacker.app.ci` 与临时数据目录；它们不等同于正式版或本机测试版构件。
