# 开发与发布

## 项目结构

```
Sources/
  InterviewHackerCore/   纯逻辑，可单元测试：问题意图与候选、VAD、流式协议解析（Codex app-server / SSE）、
                         云端识别消息组装、资料检索与 Office 文本抽取、Prompt、手机伴侣 HTTP
  InterviewHacker/       macOS 应用：SwiftUI 界面、音频采集、Whisper / Apple / 云端识别、回答引擎、
                         提词器、手机伴侣服务、截图、快捷键
Tests/InterviewHackerCoreTests/   Swift Testing 测试（含问题识别标注语料）
Resources/               Info.plist、entitlements、第三方声明、.pt 转换脚本
scripts/                 build-app.sh（构建+签名+打包）、sign-app.sh、make-icon.swift
Vendor/whisper.cpp/      固定版本的 whisper.cpp（MIT）
docs/                    审计、改进说明、验证记录与素材（docs/validation）、发布说明、截图
Tools/Validation/        生成验证用合成音频播放器与题图的工具
```

## 常用命令

```sh
swift test --disable-sandbox          # 运行核心测试
swift build                           # 编译（不含 Whisper 运行时）
./scripts/build-app.sh                # 完整构建 dist/InterviewHacker.app 与 ZIP（本机自签名）
INTERVIEWHACKER_MAKE_DMG=1 ./scripts/build-app.sh   # 另外生成 DMG
```

## 截图与演示模式

`INTERVIEWHACKER_DEMO=1` 启动时应用显示一组示例会话（不采集音频、不调用模型），可选 `INTERVIEWHACKER_DEMO_SECTION=assistant|materials|settings|history`、`INTERVIEWHACKER_DEMO_APPEARANCE=light|dark`。`.github/workflows/screenshots.yml` 在 macOS runner 上用它为每个页面截取浅色 / 深色截图并提交到 `docs/screenshots/`；手动运行或在提交信息中写 `[screenshots]` 触发。

## 发布新版本

`.github/workflows/release.yml` 在 Apple Silicon runner 上测试、编译 whisper.cpp、生成图标、签名，产出 DMG、ZIP 与 `SHA256SUMS.txt`：

| 操作 | 结果 |
| --- | --- |
| 推送到 `main` | 更新滚动预发布 **`main-latest`**（每次推送替换为最新构建）。提交信息含 `[skip release]` 时跳过 |
| 提交信息含 `[release v0.3.0]`（`main` 或 `claude/**`） | 在该提交创建正式版本 `v0.3.0` |
| 推送标签 `v0.3.0` | 创建正式版本 `v0.3.0` |
| 在 GitHub 网页 **Releases → Draft a new release** 发布 | 自动构建并把 DMG / ZIP 附加到这个 Release（约 8–10 分钟后出现） |
| Actions → Release → Run workflow | 用输入的标签创建或更新版本 |

带连字符的版本（如 `v0.3.0-beta.1`）自动标记为预发布；`docs/releases/<tag>.md` 存在时作为发布说明。

> 私有仓库的 macOS runner 按 10 倍计费分钟数；若 Actions 页面显示额度不足，构建不会运行。

### 签名与公证

- **默认 ad-hoc 签名**（没有配置任何证书时）：不需要 Apple 账号，但每个版本的签名都不同，macOS 会把每次更新当作新应用，录屏 / 麦克风权限需要重新授权；浏览器下载的版本首次打开还要点“仍要打开”。
- **固定自签名身份（推荐，免费）**：在自己的 Mac 上运行一次 `./scripts/setup-release-signing.sh`。脚本复用 `.local-signing/` 中的自签名证书（与本地构建同一身份），通过 `gh secret set` 写入 `MACOS_CERTIFICATE_P12_BASE64`、`MACOS_CERTIFICATE_PASSWORD`、`MACOS_SIGNING_IDENTITY`。之后发布工作流会在 runner 上信任该证书并签名，各版本 designated requirement 相同，**系统权限在升级后保留**。配合 `./scripts/install-latest.sh` 安装（`gh` 下载不带隔离标记），也不会再有 Gatekeeper 提示。请备份 `.local-signing/`。
- **Developer ID + 公证**（推荐正式分发）：在仓库 Settings → Secrets 添加
  `MACOS_CERTIFICATE_P12_BASE64`（`base64 -i cert.p12`）、`MACOS_CERTIFICATE_PASSWORD`、`MACOS_SIGNING_IDENTITY`（如 `Developer ID Application: 名字 (TEAMID)`），
  以及公证用的 `APPLE_ID`、`APPLE_TEAM_ID`、`APPLE_APP_SPECIFIC_PASSWORD`。工作流会自动启用 hardened runtime（`Resources/InterviewHacker.entitlements`）、公证并装订 DMG。签名身份固定后，更新不会再丢失系统权限。

## 本地构建细节

已验证的目标是 Apple Silicon arm64、macOS 14 或更新版本。应用不要求终端或 Homebrew；从源码构建需要 Swift 6、Apple Command Line Tools 和 CMake。构建脚本显式选择 SDK 的 C++ 头文件，可在只安装 Command Line Tools 的机器上构建。Whisper.cpp v1.9.4 源码固定在 `Vendor/whisper.cpp`，构建脚本会编译并把常驻运行时打进 app：

```sh
./scripts/build-app.sh
open dist/InterviewHacker.app
```

生成文件：

- `dist/InterviewHacker.app`
- `dist/InterviewHacker-macOS.zip`

首次构建会创建 `InterviewHacker Local Development` 自签名开发身份，保存在被 Git 忽略的 `.local-signing/` 目录。后续构建复用相同证书，为 `com.interviewhacker.app` 和辅助程序写入固定的 designated requirement；代码变化会改变 CDHash，但不改变签名身份和权限匹配条件。不要删除或重新生成 `.local-signing/`，否则 macOS 会把它当作不同应用，可能再次要求隐私授权。此身份只用于这台 Mac 的本地开发与验证，不是 Apple Development 或 Developer ID 证书；应用没有 Developer ID 签名或公证，不适合公开分发。私钥不会放入应用包或 ZIP。

构建脚本把静态链接的识别服务、开源许可和第三方声明放进 App 包；模型权重在首次运行时下载。


## 语音识别实现细节

应用启动时自动扫描常见 Whisper 缓存、应用模型目录、Homebrew 共享目录和 Hugging Face 的 Whisper 缓存（支持 `XDG_CACHE_HOME`、`HF_HOME`、`HUGGINGFACE_HUB_CACHE`）。发现的兼容 `.pt` / GGML `.bin` 显示名称、格式、体积和位置，点击“使用”进行校验与导入；其他位置可手动选择。扫描只读取文件信息，不加载权重，也不遍历私人文档。

应用包包含 Apple Silicon `whisper-server` 常驻运行时。设置中的“本地语音识别”提供模型选择、下载、切换、删除副本与本地导入：

- 内置模型目录覆盖 Tiny、Base、Small、Medium、Large v1/v2/v3 和 Turbo，以及英语专用与 Q5/Q8 量化版本。每个选项显示下载大小，选中后显示语言、速度、识别质量与内存占用的简短说明。`.en` 仅适合英语，其他版本支持中文、英文等多语言。
- 用户可保留多个模型，通过“使用此模型”切换。下载完成后通过上游 SHA-256 校验才会启用；下载失败或取消会保留原先使用的模型。会议或模型操作期间禁用切换。
- 默认选项仍为 `large-v3-turbo-q5_0`（约 547 MiB），不强制下载。模型目录为 `~/Library/Application Support/InterviewHacker/models/`。
- “导入本地模型…”支持 whisper.cpp `.bin` 和 OpenAI Whisper `.pt`。`.pt` 使用电脑已有的 Python `openai-whisper` 转换，转换时禁止加载可执行 pickle 对象，不下载新权重。官方命名的 `.pt` 会先校验官方 SHA-256，损坏或未下载完成的缓存会提示失败。
- 导入文件复制到应用目录，记录 SHA-256，并通过 whisper.cpp 实际加载后启用；原始文件保留。“删除副本”只删除应用管理的文件。
- 模型大小、摘要与 SHA-256 来自 [whisper.cpp 模型仓库](https://huggingface.co/ggerganov/whisper.cpp/tree/main)，速度与语言说明参考 [OpenAI Whisper 模型说明](https://github.com/openai/whisper#available-models-and-languages)。实际速度与识别效果取决于设备、语言和音质。运行时和模型均为 MIT 许可。

ScreenCaptureKit 请求 48 kHz 双声道系统音频，应用用 `AVAudioConverter` 转成 16 kHz 单声道 Int16。音频缓冲、VAD 和分段在专用队列处理；模型驻留在本机辅助进程中，片段复用已加载模型，不逐段启动和加载。VAD 使用短前缓冲、静音收尾和有界转录队列；连续语音达到上限时带重叠切段，临时结果不会重复写入最终会话。

## 音频角色与回答模型

- 系统声音与麦克风分别采集、转换和分段，系统声音标记为“对方”，麦克风标记为“我”。麦克风首次使用请求系统授权，也可在设置关闭；暂停、恢复、停止会同时作用于两路。Whisper 与 Apple Speech 路径均保留来源，Apple Speech 为两路创建独立识别器。
- 转录、导出和发给 Codex 的上下文保留角色；去重只比较同一来源，自动问题检测仅使用系统声音。角色按输入通道分配，不做声纹识别，也不区分系统声音中的多位说话者。建议戴耳机，避免扬声器声音被麦克风再次收录。
- 深浅色使用一致的不透明系统语义背景与文本颜色，并同步 AppKit 外观；主窗口、卡片、原生弹窗与提词器不再混用透明背景。
- 设置 → Codex 助手可选择回答模型和推理强度。模型列表读取本机 Codex 的 `models_cache.json`，无缓存时可填写模型 ID。每次请求快照所选值并通过 CLI 的 `--model` / `model_reasoning_effort` 传入；失败显示错误，不静默切换其他模型。成功回答显示本次所选模型，诊断事件记录请求参数。模型可用性由 Codex 服务与登录账号决定。

