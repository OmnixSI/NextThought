# InterviewHacker

InterviewHacker 是一个原生 SwiftUI macOS 悬浮助手，用于会议、面试和网课。它分别采集用户选择的屏幕或窗口系统音频与麦克风，在本机进行中英文语音识别，再通过本机 Codex CLI 回答问题。也支持截取窗口或屏幕区域后提问。不包含模拟面试、面试评分、训练或题库。

## 数据处理位置

- 系统音频与麦克风音频在本机分别识别，不保存原始音频。
- 转录文本、当前问题、导入资料和用户主动截取的图片会交给 Codex CLI，并由 Codex 服务处理。应用不调用 OpenAI API，也不要求 API Key。
- Codex 复用本机 CLI 登录态。每次回答调用都传入用户在应用设置中选择的模型和推理强度，忽略用户全局配置，不修改 `config.toml`。请求使用 `--sandbox read-only`，工作目录为独立临时目录；会议内容、资料和图片均作为不可信数据处理。
- 本机 Codex CLI 的 JSONL 输出目前只观察到完成事件，没有文本增量事件；界面会显示“正在生成”，收到完整回答后再展示，不冒称逐 token 流式输出。

## 语音识别

应用启动时自动扫描常见 Whisper 缓存、应用模型目录、Homebrew 共享目录和 Hugging Face 的 Whisper 缓存（支持 `XDG_CACHE_HOME`、`HF_HOME`、`HUGGINGFACE_HUB_CACHE`）。发现的兼容 `.pt` / GGML `.bin` 显示名称、格式、体积和位置，点击“使用”进行校验与导入；其他位置可手动选择。扫描只读取文件信息，不加载权重，也不遍历私人文档。

应用包包含 Apple Silicon `whisper-server` 常驻运行时。设置中的“本地语音识别”提供模型选择、下载、切换、删除副本与本地导入：

- 内置模型目录覆盖 Tiny、Base、Small、Medium、Large v1/v2/v3 和 Turbo，以及英语专用与 Q5/Q8 量化版本。每个选项显示下载大小，选中后显示语言、速度、识别质量与内存占用的简短说明。`.en` 仅适合英语，其他版本支持中文、英文等多语言。
- 用户可保留多个模型，通过“使用此模型”切换。下载完成后通过上游 SHA-256 校验才会启用；下载失败或取消会保留原先使用的模型。会议或模型操作期间禁用切换。
- 默认选项仍为 `large-v3-turbo-q5_0`（约 547 MiB），不强制下载。模型目录为 `~/Library/Application Support/InterviewHacker/models/`。
- “导入本地模型…”支持 whisper.cpp `.bin` 和 OpenAI Whisper `.pt`。`.pt` 使用电脑已有的 Python `openai-whisper` 转换，转换时禁止加载可执行 pickle 对象，不下载新权重。官方命名的 `.pt` 会先校验官方 SHA-256，损坏或未下载完成的缓存会提示失败。
- 导入文件复制到应用目录，记录 SHA-256，并通过 whisper.cpp 实际加载后启用；原始文件保留。“删除副本”只删除应用管理的文件。
- 模型大小、摘要与 SHA-256 来自 [whisper.cpp 模型仓库](https://huggingface.co/ggerganov/whisper.cpp/tree/main)，速度与语言说明参考 [OpenAI Whisper 模型说明](https://github.com/openai/whisper#available-models-and-languages)。实际速度与识别效果取决于设备、语言和音质。运行时和模型均为 MIT 许可。

ScreenCaptureKit 请求 48 kHz 双声道系统音频，应用用 `AVAudioConverter` 转成 16 kHz 单声道 Int16。音频缓冲、VAD 和分段在专用队列处理；模型驻留在本机辅助进程中，片段复用已加载模型，不逐段启动和加载。VAD 使用短前缓冲、静音收尾和有界转录队列；连续语音达到上限时带重叠切段，临时结果不会重复写入最终会话。

## 音频角色、外观与回答模型

- 系统声音与麦克风分别采集、转换和分段，系统声音标记为“对方”，麦克风标记为“我”。麦克风首次使用请求系统授权，也可在设置关闭；暂停、恢复、停止会同时作用于两路。Whisper 与 Apple Speech 路径均保留来源，Apple Speech 为两路创建独立识别器。
- 转录、导出和发给 Codex 的上下文保留角色；去重只比较同一来源，自动问题检测仅使用系统声音。角色按输入通道分配，不做声纹识别，也不区分系统声音中的多位说话者。建议戴耳机，避免扬声器声音被麦克风再次收录。
- 深浅色使用一致的不透明系统语义背景与文本颜色，并同步 AppKit 外观；主窗口、卡片、原生弹窗与提词器不再混用透明背景。
- 设置 → Codex 助手可选择回答模型和推理强度。模型列表读取本机 Codex 的 `models_cache.json`，无缓存时可填写模型 ID。每次请求快照所选值并通过 CLI 的 `--model` / `model_reasoning_effort` 传入；失败显示错误，不静默切换其他模型。成功回答显示本次所选模型，诊断事件记录请求参数。模型可用性由 Codex 服务与登录账号决定。

## 首次运行

1. 安装并登录 ChatGPT/Codex 桌面客户端，确认本机 Codex CLI 可用。
2. 打开 InterviewHacker 的设置页，刷新 Codex 状态，选择下载模型或导入已有模型，再切换使用。首次下载需要网络。
3. 回到助手页，选择会议窗口或整个屏幕，再点击“开始辅助”。
4. macOS 首次请求屏幕与系统音频权限时，选择允许。若之前拒绝，到“系统设置 → 隐私与安全性”中的“屏幕与系统音频录制”和“麦克风”允许 InterviewHacker，再回应用刷新目标或重新启动辅助。
5. 可手动输入问题；也可开启规则型自动问题检测。点击窗口截图或区域截图后，可预览、取消或交给 Codex 分析。

模型、Codex CLI、目标音源和权限都就绪后，助手页会显示“可以开始”。没有 Codex 登录、模型校验失败或没有应用内识别运行时时，开始按钮会保持禁用。

## 构建与运行

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

## 功能与限制

- 支持开始、暂停、恢复、停止，屏幕/窗口系统音频选择，手动提问、规则型自动检测、最近 5 分钟转录上下文、资料导入、会话保存/导出/删除和固定快捷键。
- 自动检测覆盖常见中英文问句、解释请求和追问；不对每个词调用 Codex。检测使用启发式规则，仍可能漏掉或误判问题。
- 窗口截图先在应用内列出可见窗口，按窗口 ID 捕获；区域截图通过 ScreenCaptureKit 读取所选显示器，再在应用内预览、拖动裁切。捕获时隐藏助手窗，完成或取消后恢复窗口。图片会按比例缩小到最长边 2,400 像素并以 JPEG 质量 0.9 交给 Codex；代码截图要求模型先核对相关行是否清楚，看不清时请求更清晰图片。
- 多人声纹识别和扫描版 PDF OCR 尚未实现；当前角色按麦克风与系统声音通道划分。
- 系统音频、麦克风和截图受 macOS 隐私授权控制。用户拒绝权限后，应用不会绕过系统授权。

## 验证记录

以下为此前版本的历史验证记录。本次模型扫描、双路音频、外观和 Codex 模型选择改动按用户要求未运行本地构建或功能测试，由用户后续验证。

验证日期：2026-10-14。设备为 MacBook Air、Apple M4、16 GB 内存、macOS 26.5.2；Xcode 26.0.1、Swift 6.2、Codex CLI `0.162.0-alpha.17.2`。工程最低部署版本为 macOS 14，构建产物为 arm64。

### 构建、签名和权限

- `swift test --disable-sandbox --scratch-path .build/swift-tests`：26 项测试全部通过，覆盖 VAD、跨片段问题合并与去重、5 分钟上下文、提示词 JSON 转义和长度上限、Codex JSONL 事件解析。
- `scripts/build-app.sh` 完成 Release 构建；`codesign --verify --deep --strict` 验证通过，app 和 `Contents/Helpers/whisper-server` 均为 arm64；`dist/InterviewHacker-macOS.zip` 通过 `unzip -t`。
- 当前签名身份指纹为 `5B8B4BBACA96E69F133B309314530534304E9092`，designated requirement 固定为应用标识 `com.interviewhacker.app` 加证书指纹。源代码修改前的 CDHash 是 `f4dd7b529267abba59afd29f2aa2ee8a394a7fb6`；区域截图版本为 `6791370b69c72529f6f3d23967ecaec5a84f48ba`；本次提示词版本为 `5b13d0365fd69d86ad81d9cf7344dcfb76141613`。重启后的最终构建记录 `permission.checked: granted=true`，并读取 28 个捕获目标；没有重置 TCC 权限。
- 这是本机自签名开发版本（没有 Apple Team ID、Developer ID 签名或公证）。保留 `.local-signing/` 才能在此 Mac 上复用代码签名身份和隐私授权。将应用复制到另一台 Mac 或删除本地密钥链后不保证能直接启动或复用权限。

### 本地语音识别和系统音频

权重是多语言 `large-v3-turbo-q5_0`，SHA-1 校验通过。模型文件 574,041,195 字节，缓存于 `~/Library/Application Support/InterviewHacker/models/`。Whisper.cpp v1.9.4 的运行时在最终 app 包内，服务常驻并复用已加载模型。运行日志报告 `Apple M4`、`using MTL0 backend`，确认这次推理实际启动 Metal 后端；不是仅凭编译参数推断。

- 首次 Metal 初始化/模型服务就绪耗时 32.73 秒；之后同机重启观察到 0.53–0.80 秒。首次耗时包括 Metal shader 初始化，不能代表普通启动。
- 一个已采样的 Whisper 服务 RSS 快照约 758–802 MiB；CPU-only 样本的一次 `ps` 快照约 103% CPU。未测峰值内存、GPU 利用率或长时间会议的资源曲线。
- WAV 文件测试使用合成音频，通过本地 whisper.cpp 服务识别：英文 9.94 秒音频耗时 7.95 秒；中文 6.36 秒耗时 9.00 秒；中英混合 7.55 秒耗时 7.54–8.39 秒。英文的 `database index` 曾误为 `database syntax`，中文“数据库索引”也曾出现同音转写错误。没有计算准确率。
- 应用内的真实系统音频测试由合成 WAV 播放器输出，ScreenCaptureKit 实际输入为 48 kHz、双声道、32-bit float、非交错；应用转换为 16 kHz、单声道、16-bit PCM。不是麦克风录音。更早的中英文测试中，4.65 秒和 5.65 秒片段分别识别耗时 11.77 秒、11.68 秒；中文 7.65 秒片段耗时 13.32 秒。最终签名版的中英混合片段（VAD 得到 8.65 秒）识别耗时 9.85 秒，随后 Codex 用 6.12 秒返回自动回答。系统音频从开始采集、VAD、Whisper、自动问题检测到回答展示已端到端成功。
- 分段设置限制单段最长 12 秒，连续语音保留 0.5 秒重叠，等待队列上限为 3。实测英文和中文转写慢于音频播放速度；在较长或连续语音下可能积压或丢弃旧段，这是当前 MVP 的主要限制。

### Codex 问答和截图

最终包中打开设置页后，应用显示 Codex CLI 已登录并检测到上述版本；模型设置页显示本地模型存在且通过完整性校验。实际打包 app 成功回答手动中文问题，Codex CLI 用时 3.77 秒。自动混合语音问题回答用时 6.12 秒；请求日志确认模型 `gpt-5.6-luna`、推理强度 `none`，退出码 0，并只收到完整 `item.completed:agent_message` 事件，所以 UI 显示“正在整理回答”后呈现完整结果。取消中的 CLI 请求退出码为 15，旧答案保持显示，没有被晚到结果覆盖。

- 已生成 `validation/fixtures/binary-search.png` 和 `math.png`，分辨率为 1600×1100。最终包通过应用内窗口列表选择并捕获了合成二分查找题图；预览可读，记录的截图尺寸为 1624×1250。应用将图像缩放到最长边不超过 2,400 像素并以 JPEG 质量 0.9 交给 Codex。回答准确定位 `left = mid` 未推进边界导致死循环，给出 `left = mid + 1`、可运行修复和 `O(log n)`/`O(1)` 复杂度；Codex CLI 用时 6.25 秒。
- 暂停、恢复和停止在最终签名版上逐项操作，界面状态正确变化；开始后实际采集系统音频成功，停止后释放采集。窗口选择器可取消，截图卡片也可清除。另对一个 Codex 请求点了取消，旧回答保留。
- 最终版区域截图在应用内显示显示器预览；实际拖动选择、取消、裁切预览和 Codex 图片回答均已执行。一次测试从桌面上的合成二分查找题缩略图裁得 426×283 像素，Codex CLI 用时 4.12 秒，正确指出 `left = mid` 可能不推进边界并给出 `left = mid + 1`。同一低分辨率裁切在较早提示词下曾误报缩进问题；提示词加入“先核对相关代码行，无法辨认时不推测”后重测得到正确回答。窗口截图的 1624×1250 全图问答另已通过。区域截图这次验证来源是桌面缩略图，未测原尺寸 1600×1100 图片在区域选择器中的清晰度。
- 没有连接第二台显示器；多显示器坐标行为未实测。会话保存/导出/删除、模型重新下载取消与断网重试、音源消失后的恢复也未在这次回归中逐项验证。

提示词原文、截图默认问题、合成音频台词和复现说明见 [`validation/real-time-answer-prompts.md`](validation/real-time-answer-prompts.md)。测试 WAV 为合成语音，不能当作真人录音准确率评估；设备、样本时长和测量阶段均如上注明。

## 未完成能力与发布限制

- 区域截图完整交互已实测；本机仅有一个显示器，多显示器与不同缩放比例尚未实测。原尺寸区域图的可读性仍需补测。
- 持续系统音频转录慢于实时语速，长时间会议可能排队或丢弃旧段；当前没有可报告的通用准确率。
- 多说话人声纹识别、扫描版 PDF OCR 尚未实现；当前“我 / 对方”角色按输入通道划分。
- 本地自签名应用只适合此开发 Mac 上验证。正式分发需要 Developer ID 签名、公证和另一台干净 Mac 的安装测试。

本机运行步骤：确认 ChatGPT/Codex 客户端已登录；打开 `dist/InterviewHacker.app`；首次运行在设置中确认 Codex 状态，下载或导入本地模型并选择使用；在 macOS 隐私与安全性中允许“屏幕与系统音频录制”；回助手页开始辅助。重建后使用相同工作区、`.local-signing/` 和 `scripts/build-app.sh`，本次验证已确认系统权限保持有效。
